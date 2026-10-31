using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    /// <summary>
    /// QA and cinematic autowalk (Part 9, 14.5). An <see cref="IPlayerDriver"/> that walks the player along the walked curve
    /// (PathGuide.WalkPoint) through the real CharacterController, so the autotest exercises the same colliders as a human.
    ///
    /// - <c>-autotest</c> (or <c>Begin(false)</c>): constant 1.5 m/s, stops at the lift centre for the dwell, rides, leaves
    ///   when the gate opens, stops at the goal door; finishes at about 659 s (658 +/- 8).
    /// - On the lift (both modes): the deck choreography of <see cref="WalkPlanner.DeckStand"/> (R1-04 / R1-05): out to the
    ///   -Z rail over ride seconds 64-75, the fan hold and the look down into the shaft from there, back to the centre over
    ///   109.5-120.5, before the gate opens. A walker begun aboard after ride second 98 holds the centre.
    /// - Cinematic (<c>C</c> key toggles, <c>-cinematic</c>): speed eases slower at the reveals (12 s in total), 1.1 m/s over the
    ///   tunnel mouth and the first metres of the balcony, the 14 s hold H7 at z = -12 with the FOV push 68 -> 66 and the
    ///   pan; any human input cancels it (except in -autotest).
    /// - Gaze (both modes): GazeCues targets followed by a critically damped look (omega 2.5 rad/s) plus a handheld sway of at
    ///   most 0.3 degrees (deterministic from GameState.Seed, yaw sway scaled by cos(pitch) so nothing turns near the nadir).
    ///   Never roll.
    /// Steering: feed-forward along the curve tangent at the planned speed plus a bounded proportional correction toward the
    /// planned point; on the lift the same in platform coordinates toward the deck stand.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class AutoWalker : MonoBehaviour, IPlayerDriver
    {
        public static AutoWalker Instance;

        /// <summary>True while the walker drives the player (also while holding still at the end).</summary>
        public bool IsRunning;

        /// <summary>True once the walk reached the goal door.</summary>
        public bool Finished;

        [Tooltip("Begin automatically when -autotest or -cinematic is set, once the player is placed and the clock runs.")]
        public bool AutoBeginFromFlags = true;

        [Header("Look")]
        public float LookOmega = 2.5f;
        public float SwayMaxDeg = 0.3f;

        [Header("Steering")]
        public float SteerGain = 1.5f;
        public float SteerMaxCorrection = 0.6f;
        [Tooltip("Seconds after Begin during which human input does not cancel the cinematic walk (cursor-lock spikes).")]
        public float CancelGrace = 0.5f;

        /// <summary>Maximum FOV change rate of the H7 push and its release (degrees per second).</summary>
        public const float FOV_RATE = 1f;

        readonly WalkPlanner _planner = new WalkPlanner();
        public WalkPlanner Planner => _planner;
        public bool Cinematic => IsRunning && _planner.Cinematic;

        /// <summary>Current gaze target (after interpolation, before damping), for QA logs.</summary>
        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; }

        float _yaw, _pitch, _yawVel, _pitchVel;
        float _swayT, _sinceBegin;
        readonly float[] _swayPhase = new float[6];
        float _fovOffset;
        bool _autoBegun;
        float _noLiftWait;
        double _beginTime;
        bool _deckPlan;                 // the ride choreography (walk to the -Z rail and back) is active
        float _goalWait;                // seconds stopped at the end without the goal firing
        int _goalStage;                 // fallback walks done toward the door trigger (WalkPlanner.ExtendEnd), at most 2

        // sway: three sines per axis, amplitudes sum to 0.21 degrees per axis (combined at most 0.3)
        static readonly float[] SwayYawHz = { 0.11f, 0.23f, 0.37f };
        static readonly float[] SwayYawAmp = { 0.09f, 0.07f, 0.05f };
        static readonly float[] SwayPitchHz = { 0.13f, 0.19f, 0.31f };
        static readonly float[] SwayPitchAmp = { 0.08f, 0.07f, 0.06f };

        public bool WantsControl => IsRunning;

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        void Awake()
        {
            Instance = this;
            // solve the cinematic slowdown depth now (scene load, behind the black fade), not on the first C press mid-walk
            WalkPlanner.Prepare();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var pc = PlayerController.Instance;
            float dt = Time.deltaTime;

            if (AutoBeginFromFlags && !_autoBegun && pc != null && pc.HasBeenPlaced && ClockDriver.Running &&
                (GameState.AutoTest || GameState.Cinematic) && !GameState.CaptureMode)
            {
                _autoBegun = true;
                Begin(GameState.Cinematic && !GameState.AutoTest);
            }

            if (!GameState.Paused && !GameState.AutoTest && !GameState.CaptureMode && !GameState.Ended && TogglePressed())
            {
                if (IsRunning) Stop();
                else Begin(true);
            }

            if (!IsRunning)
            {
                GameState.HoldH7 = false;
                if (_fovOffset != 0f)
                {
                    _fovOffset = Mathf.MoveTowards(_fovOffset, 0f, FOV_RATE * 0.5f * dt);
                    ApplyFov();
                }
            }
        }

        static bool TogglePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Start walking from the player's current progress. Idempotent for the same mode.</summary>
        public void Begin(bool cinematic)
        {
            var pc = PlayerController.Instance;
            if (pc == null) { Debug.LogWarning("[ABYSS] AutoWalker.Begin: no PlayerController"); return; }
            if (IsRunning && _planner.Cinematic == cinematic) return;

            SyncPlanner(pc, cinematic, pc.HorizontalSpeed);
            IsRunning = true;
            Finished = _planner.Mode == WalkPlanner.Stage.Done;
            _sinceBegin = 0f;
            _noLiftWait = 0f;
            _beginTime = SceneClock.Time;
            pc.Driver = this;
            _yaw = pc.Yaw; _pitch = pc.Pitch; _yawVel = 0f; _pitchVel = 0f;
            _swayT = 0f;
            for (int i = 0; i < _swayPhase.Length; i++)
                _swayPhase[i] = (float)(HeadMotion.Hash01(GameState.Seed, (uint)(i + 211)) * 2.0 * System.Math.PI);
            if (cinematic) GameState.Cinematic = true;
            Debug.Log("[ABYSS] AutoWalker begin (" + (cinematic ? "cinematic" : "autowalk") + ") at d=" + pc.PathD.ToString("F2", Inv) +
                      ", t=" + SceneClock.Time.ToString("F2", Inv) + ", stage " + _planner.Mode);
        }

        /// <summary>Hand control back to the human.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            var pc = PlayerController.Instance;
            if (pc != null && ReferenceEquals(pc.Driver, this)) pc.Driver = null;
            GameState.HoldH7 = false;
            if (_planner.Cinematic) GameState.Cinematic = false;
            Debug.Log("[ABYSS] AutoWalker stop at d=" + (pc != null ? pc.PathD.ToString("F2", Inv) : "?") + ", t=" + SceneClock.Time.ToString("F2", Inv));
        }

        /// <summary>Called by PlayerController.Teleport: re-plan from the new position (safety returns, -beat placement).</summary>
        public void OnPlayerTeleported()
        {
            if (!IsRunning) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;
            SyncPlanner(pc, _planner.Cinematic, 0f);
            _yaw = pc.Yaw; _pitch = pc.Pitch; _yawVel = 0f; _pitchVel = 0f;
            Finished = _planner.Mode == WalkPlanner.Stage.Done;
        }

        void SyncPlanner(PlayerController pc, bool cinematic, float v0)
        {
            var lift = LiftController.Instance;
            bool riding = lift != null && pc.OnLift && (lift.Phase == LiftPhase.Dwell || lift.Phase == LiftPhase.Riding);
            bool rideDone = pc.AfterRide || (lift != null && lift.Phase >= LiftPhase.Arrived);
            _planner.Begin(pc.PathD, cinematic, rideDone, riding, v0);
            _goalWait = 0f;
            _goalStage = 0;
            // begun aboard late in the ride: no time for the walk to the rail and back, hold the centre
            _deckPlan = !riding || RideClock(lift) <= WalkPlanner.RAIL_PLAN_LATEST_BEGIN;
        }

        // ---------------------------------------------------------------- driving

        public void Drive(ref PlayerInputFrame input, float dt)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            _sinceBegin += dt;

            // any human input cancels the cinematic walk (never the autotest)
            if (_planner.Cinematic && !GameState.AutoTest && _sinceBegin > CancelGrace && pc.HumanInputThisFrame)
            {
                Stop();
                return;   // the human input in the frame passes through this very frame
            }

            var lift = LiftController.Instance;
            bool canLeave = false;
            if (lift != null) canLeave = lift.GateOpening;
            else if (_planner.Mode == WalkPlanner.Stage.OnLift)
            {
                // no platform (broken beat 5): wait the nominal dwell + ride + gate, then place the player at the bottom dock
                _noLiftWait += dt;
                if (_noLiftWait > AbyssConst.LIFT_DWELL + AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY)
                {
                    Debug.LogWarning("[ABYSS] AutoWalker: no LiftController, placing the player at the bottom dock");
                    _noLiftWait = 0f;
                    pc.Teleport(AbyssConst.W[13], pc.Yaw, pc.Pitch);
                    return;
                }
            }

            _planner.Step(dt, canLeave);
            float rideT = RideClock(lift);
            Steer(ref input, pc, rideT);
            Look(ref input, pc, rideT, dt);

            GameState.HoldH7 = _planner.Holding;
            if (_planner.Mode != WalkPlanner.Stage.Done) return;
            if (!Finished)
            {
                // Beat07 (D2) also opens the door for a walker that has finished within its slack of the 3.0 m trigger line
                Finished = true;
                Debug.Log("[ABYSS] AutoWalker finished at t=" + SceneClock.Time.ToString("F2", Inv) +
                          " (walk " + (SceneClock.Time - _beginTime).ToString("F2", Inv) + " s), d=" + pc.PathD.ToString("F2", Inv) +
                          ", safety returns " + pc.SafetyReturns);
            }
            if (GameState.GoalReached || _goalStage >= 2 || !_planner.AfterRide) return;
            // D2: the walk stops 3.2 m from the door. If the goal has not fired there (a door trigger nearer the door), walk on:
            // first just past D2's 3.0 m line, then just past AbyssConst.DOOR_TRIGGER_Z, so the ending always plays.
            _goalWait += dt;
            if (_goalWait < WalkPlanner.GOAL_FALLBACK_WAIT) return;
            _goalWait = 0f;
            float target = _goalStage == 0 ? WalkPlanner.GoalFallbackNearD : WalkPlanner.GoalFallbackD;
            _goalStage++;
            if (_planner.ExtendEnd(target))
            {
                Finished = false;
                Debug.Log("[ABYSS] AutoWalker: the goal did not fire at z " + PathGuide.Beat7Z(pc.PathD).ToString("F2", Inv) +
                          ", walking on to z " + PathGuide.Beat7Z(_planner.StopD).ToString("F2", Inv));
            }
        }

        Vector2 Correction(Vector2 err)
        {
            float m = err.magnitude;
            if (m < 0.005f) return Vector2.zero;
            Vector2 c = err * SteerGain;
            float cm = m * SteerGain;
            return cm > SteerMaxCorrection ? c * (SteerMaxCorrection / cm) : c;
        }

        void Steer(ref PlayerInputFrame input, PlayerController pc, float rideT)
        {
            Vector2 desired;
            if (_planner.Mode == WalkPlanner.Stage.OnLift && pc.OnLift)
            {
                // the deck stand in platform coordinates (deck axes = world axes): planned velocity + bounded correction
                Vector2 stand = Vector2.zero, ff = Vector2.zero;
                if (_deckPlan) WalkPlanner.DeckStand(rideT, out stand, out ff);
                Vector3 l = pc.LiftLocalFeet;
                desired = ff + Correction(new Vector2(stand.x - l.x, stand.y - l.z));
            }
            else
            {
                Vector3 target = PathGuide.WalkPoint(_planner.D, _planner.AfterRide);
                Vector3 tan = PathGuide.WalkTangent(_planner.D, _planner.AfterRide);
                Vector3 f = pc.FeetPosition;
                desired = new Vector2(tan.x, tan.z) * _planner.Speed + Correction(new Vector2(target.x - f.x, target.z - f.z));
            }
            float sp = desired.magnitude;
            if (sp > 1e-4f)
            {
                input.Move = desired / sp;
                input.SpeedOverride = sp;
            }
            else
            {
                input.Move = Vector2.zero;
                input.SpeedOverride = 0f;
            }
            input.MoveIsWorld = true;
            input.Brisk = false;
        }

        // ---------------------------------------------------------------- gaze

        float RideClock(LiftController lift)
        {
            if (lift == null) return -2f + _noLiftWait;
            switch (lift.Phase)
            {
                case LiftPhase.DockedTop: return -AbyssConst.LIFT_DWELL;
                case LiftPhase.Dwell: return lift.DwellSeconds - AbyssConst.LIFT_DWELL;
                case LiftPhase.Riding: return (float)lift.LiftT;
                default: return AbyssConst.LIFT_DURATION + lift.ArrivalSeconds;
            }
        }

        void GazeTarget(PlayerController pc, float rideT, out float yaw, out float pitch, out float tau7)
        {
            float d = _planner.D;
            bool after = _planner.AfterRide;
            int beat = PathGuide.BeatAt(d);
            var ctx = new GazeCues.Context
            {
                Eye = pc.EyePosition,
                HeadingYaw = PathGuide.HeadingYaw(d, after),
                SceneTime = SceneClock.Time,
            };
            tau7 = -1f;
            if (beat == 5 && (_planner.Mode == WalkPlanner.Stage.OnLift || after))
                GazeCues.Evaluate(GazeCues.Beat5Ride, rideT, in ctx, out yaw, out pitch);
            else if (beat == 7)
            {
                tau7 = GazeCues.Beat7Clock(d, _planner.Holding, _planner.HoldElapsed);
                GazeCues.Evaluate(GazeCues.Beat7, tau7, in ctx, out yaw, out pitch);
            }
            else
                GazeCues.Evaluate(GazeCues.ForBeat(beat), GazeCues.BeatTime(beat, d), in ctx, out yaw, out pitch);
        }

        static void CritDamp(ref float x, ref float v, float target, float omega, float dt)
        {
            float delta = x - target;
            float j = (v + omega * delta) * dt;
            float e = Mathf.Exp(-omega * dt);
            x = target + (delta + j) * e;
            v = (v - omega * j) * e;
        }

        void Look(ref PlayerInputFrame input, PlayerController pc, float rideT, float dt)
        {
            GazeTarget(pc, rideT, out float ty, out float tp, out float tau7);
            tp = Mathf.Clamp(tp, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
            TargetYaw = ty;
            TargetPitch = tp;

            float gy = _yaw + Mathf.DeltaAngle(_yaw, ty);
            CritDamp(ref _yaw, ref _yawVel, gy, LookOmega, dt);
            CritDamp(ref _pitch, ref _pitchVel, tp, LookOmega, dt);
            if (_yaw >= 360f || _yaw < 0f) _yaw = Mathf.Repeat(_yaw, 360f);

            _swayT += dt;
            float sy = 0f, sp = 0f;
            for (int i = 0; i < 3; i++)
            {
                sy += SwayYawAmp[i] * Mathf.Sin(2f * Mathf.PI * SwayYawHz[i] * _swayT + _swayPhase[i]);
                sp += SwayPitchAmp[i] * Mathf.Sin(2f * Mathf.PI * SwayPitchHz[i] * _swayT + _swayPhase[i + 3]);
            }
            float scale = SwayMaxDeg / 0.3f;
            sy *= scale * Mathf.Cos(_pitch * Mathf.Deg2Rad);
            sp *= scale;
            if (_swayT > 3600f) _swayT -= 3600f;

            input.AbsoluteLook = true;
            input.Yaw = _yaw + sy;
            input.Pitch = Mathf.Clamp(_pitch + sp, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
            input.YawDelta = 0f;
            input.PitchDelta = 0f;

            // FOV push of the hold H7 (cinematic only), rate limited so no kick ever happens
            float fovTarget = (_planner.Cinematic && tau7 >= 0f) ? GazeCues.Beat7FovOffset(tau7) : 0f;
            _fovOffset = Mathf.MoveTowards(_fovOffset, fovTarget, FOV_RATE * dt);
            ApplyFov();
        }

        void ApplyFov()
        {
            var rig = CameraRig.Instance;
            if (rig != null) rig.FovOffset = _fovOffset;
        }
    }
}
