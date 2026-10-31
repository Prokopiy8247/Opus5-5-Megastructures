using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    /// <summary>
    /// First-person walker (Part 9, 14.4, 14.7.1). Owns the CharacterController (radius 0.35, height 1.8), reads human input
    /// through the Input System (keyboard and mouse primary, gamepad secondary; never UnityEngine.Input), or takes it from an
    /// <see cref="IPlayerDriver"/> (AutoWalker, QA bots), and publishes GameState.PlayerPos.
    ///
    /// Movement: walk 1.5 m/s, brisk 2.5 m/s (Shift / gamepad shoulder), velocity smoothed by a critically damped filter
    /// (about 0.2 s to full speed), gravity with a ground snap so slope colliders (stairs, ledge ramp) are followed without
    /// bouncing. Raw mouse look (no smoothing, Y not inverted), pitch clamped to +/-89.5, yaw from +Z toward +X.
    /// Eye height 1.7 m above the floor, its vertical component smoothed by a critically damped filter (0.15 s) so the 60 Hz
    /// second difference on the stairs stays under 2.5 mm (14.5.1 Acceptance).
    ///
    /// Lift (14.7.1): whenever the feet are on the deck footprint while the platform is docked, the CharacterController is
    /// disabled and the player becomes kinematic in platform coordinates: state <c>localPos</c> in double, 2D clamp to the
    /// 18 x 18 walkable rectangle (open toward the boarding opening at the top dock before the dwell, and through the gate once
    /// it is open at the bottom), world = platform position + localPos. Boarding and leaving happen only while docked, so the
    /// conversions are exact. The eye is never smoothed in world space on the lift (only the local height offset).
    ///
    /// Footsteps (14.3): one AudioEvents.Footstep per 0.75 m of travel; on the gallery stairs one per tread (0.28 m of
    /// horizontal travel) together with a 1.0 mm / 100 ms camera dip exposed through <see cref="TimeSinceTread"/>.
    /// Safety (Part 9): entering a Safety-layer trigger, or falling 4 m below the path floor, returns the player to the path
    /// centreline at the last safe distance and increments <see cref="SafetyReturns"/>. The first returns are logged one by
    /// one, later ones only every <see cref="SAFETY_LOG_EVERY"/>th. If <see cref="NO_FLOOR_RETURNS"/> fall returns follow each
    /// other without the player ever standing on ground in between, there is no corridor collider under the path (beat not
    /// built or broken): one warning is logged and from then on the feet are held on the analytic path floor (PathGuide) as a
    /// backstop, so the player never falls forever. SafetyReturns keeps the defect visible to QA.
    /// The pause overlay is FadeController's: while GameState.Paused all input is ignored and the cursor is released.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance;

        /// <summary>AutoWalker or a QA bot; null = human input.</summary>
        public IPlayerDriver Driver;

        /// <summary>Look orientation in degrees (yaw [0, 360) from +Z toward +X; pitch up positive, clamped to +/-89.5).</summary>
        public float Yaw, Pitch;

        /// <summary>World velocity of the feet this frame (includes the lift motion).</summary>
        public Vector3 Velocity;

        /// <summary>True while kinematic on the lift deck (CharacterController disabled).</summary>
        public bool OnLift;

        /// <summary>Number of safety returns this session (QA asserts zero for the bots).</summary>
        public int SafetyReturns;

        // ---------------------------------------------------------------- tunables
        [Header("Look")]
        public float MouseDegPerPixel = 0.075f;
        public float GamepadYawRate = 140f;
        public float GamepadPitchRate = 100f;

        [Header("Character controller")]
        public float SlopeLimit = 48f;
        public float StepOffset = 0.3f;
        public float SkinWidth = 0.035f;
        public float Gravity = 9.81f;

        [Header("Behaviour")]
        public bool ManageCursor = true;
        /// <summary>Metres below the path floor that count as a fall (backstop behind the safety volumes).</summary>
        public float FallLimit = 4f;

        // ---------------------------------------------------------------- constants
        public const float STEP_LENGTH = 0.75f;
        /// <summary>Safety returns logged individually; after that only every SAFETY_LOG_EVERY-th return is logged.</summary>
        public const int SAFETY_LOG_FIRST = 8, SAFETY_LOG_EVERY = 50;
        /// <summary>Consecutive fall returns without standing on ground that switch on the analytic floor backstop.</summary>
        public const int NO_FLOOR_RETURNS = 3;
        const float MaxDt = 0.1f;
        const float DeckFootprint = 10.25f;          // half-size of the deck footprint (20 x 20 deck plus a margin)
        const float DeckExit = 10.6f;                // capsule centre may reach this far out through an opening
        const int SafetyLayerMask = 1 << CorridorKit.LayerSafety;
        /// <summary>Capsule-centre half-width through the bottom gate: the gate leads onto the 3 m gantry, so the corridor is the
        /// gantry's (1.15 m) and leaving the deck never overlaps the gantry edge walls.</summary>
        static readonly double GateHalf = System.Math.Min(LiftController.OPENING_HALF, AbyssConst.GANTRY_W * 0.5 - AbyssConst.CAPSULE_R);

        // ---------------------------------------------------------------- published state
        /// <summary>Path distance of the player (PathGuide.Project, frozen at D_LIFT during dwell and ride).</summary>
        public float PathD { get; private set; }
        /// <summary>True on the lower level (after the ride).</summary>
        public bool AfterRide { get; private set; }
        /// <summary>Feet position on the floor (world).</summary>
        public Vector3 FeetPosition { get; private set; }
        /// <summary>Eye position (world): feet + 1.7 m with the smoothed height, without head motion.</summary>
        public Vector3 EyePosition { get; private set; }
        /// <summary>Velocity relative to the ground or the deck (m/s, horizontal).</summary>
        public Vector3 LocalVelocity { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public SurfaceKind CurrentSurface { get; private set; }
        /// <summary>Phase 0..1 within the current 0.75 m step (0 = foot contact).</summary>
        public float StepPhase01 { get; private set; }
        /// <summary>Seconds since the last stair tread contact (drives the 1 mm tread dip).</summary>
        public float TimeSinceTread { get; private set; } = 10f;
        /// <summary>True once Teleport placed the player or the first frame ran.</summary>
        public bool HasBeenPlaced { get; private set; }
        /// <summary>Feet in platform coordinates while <see cref="OnLift"/> (y = height above the deck top).</summary>
        public Vector3 LiftLocalFeet => new Vector3((float)_lx, (float)_ly, (float)_lz);
        /// <summary>Eye in platform coordinates while <see cref="OnLift"/> (for the 14.7.1 offset test).</summary>
        public Vector3 LiftLocalEye => new Vector3((float)_lx, (float)(_eyeY + AbyssConst.EYE), (float)_lz);
        /// <summary>True when the human produced any input this frame (movement, look, brisk).</summary>
        public bool HumanInputThisFrame { get; private set; }
        /// <summary>True once repeated falls showed that no collider carries the path: the feet are held on the analytic floor.</summary>
        public bool FloorBackstop { get; private set; }
        /// <summary>Number of <see cref="Teleport"/> calls (PlayerShadow snaps the body yaw when it changes).</summary>
        public int TeleportCount { get; private set; }

        // ---------------------------------------------------------------- internals
        CharacterController _cc;
        Vector2 _vel, _acc;              // smoothed horizontal velocity (world axes = deck axes) and its rate
        float _vy;
        bool _grounded;
        double _eyeY, _eyeVy;            // smoothed feet height (world in CC mode, deck-local on the lift) and its rate
        bool _kinematic;
        double _lx, _ly, _lz;            // feet in platform coordinates
        float _stepDist;
        int _lastTread = -1;
        float _groundedTime;
        float _lastSafeD;
        bool _lastSafeAfter;
        float _safetyCooldown;
        bool _safetyHit;
        int _fallsWithoutGround;         // fall returns in a row with no grounded stretch in between
        Vector3 _prevFeet;
        readonly Collider[] _overlap = new Collider[4];

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            if (_cc == null) _cc = gameObject.AddComponent<CharacterController>();
            _cc.radius = AbyssConst.CAPSULE_R;
            _cc.height = AbyssConst.CAPSULE_H;
            _cc.center = new Vector3(0f, AbyssConst.CAPSULE_H * 0.5f, 0f);
            _cc.slopeLimit = SlopeLimit;
            _cc.stepOffset = StepOffset;
            _cc.skinWidth = SkinWidth;
            _cc.minMoveDistance = 0f;
            _cc.enableOverlapRecovery = true;
            if (gameObject.layer == CorridorKit.LayerCorridor || gameObject.layer == CorridorKit.LayerSafety)
                Debug.LogWarning("[ABYSS] PlayerController: the Player must not be on the Corridor or Safety layer");
            if (GetComponent<PlayerShadow>() == null) gameObject.AddComponent<PlayerShadow>();

            Vector3 f = transform.position - new Vector3(0f, SkinWidth, 0f);
            FeetPosition = f;
            _prevFeet = f;
            _eyeY = f.y;
            PathD = PathGuide.Project(f, -1f);
            _lastSafeD = PathD;
            _lastSafeAfter = f.y < AbyssConst.LIFT_TOP.y - 100f;
            AfterRide = _lastSafeAfter;
            EyePosition = new Vector3(f.x, f.y + AbyssConst.EYE, f.z);
            Publish(0f);
        }

        void Start()
        {
            HasBeenPlaced = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) UpdateCursor(true);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == CorridorKit.LayerSafety) _safetyHit = true;
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            UpdateCursor(false);
            float dt = Mathf.Min(Time.deltaTime, MaxDt);
            // paused (overlay or timeScale), or the walk clock is held while GameBootstrap loads (beats and colliders not
            // built yet): the player stays exactly where it is, no gravity, no safety checks; velocity resumes afterwards
            if (dt <= 0f || !ClockDriver.Running || GameState.Paused)
            {
                HumanInputThisFrame = false;
                Publish(0f);
                return;
            }

            var input = default(PlayerInputFrame);
            ReadHuman(ref input, dt);
            if (Driver != null && Driver.WantsControl) Driver.Drive(ref input, dt);
            if (GameState.Ended) input.Clear();

            ApplyLook(in input);

            var lift = LiftController.Instance;
            if (!_kinematic) TryEnterLift(lift);
            else if (lift == null) ExitLift(null);

            Vector2 wish = WishVelocity(in input);
            FilterVelocity(wish, dt);

            Vector2 moved = _kinematic ? MoveOnLift(lift, dt) : MoveCharacter(dt);

            UpdatePathAndSurface(lift);
            UpdateFootsteps(moved, dt);
            if (!_kinematic) UpdateSafety(dt);
            Publish(dt);
        }

        // ---------------------------------------------------------------- input

        void ReadHuman(ref PlayerInputFrame input, float dt)
        {
            Vector2 move = Vector2.zero;
            bool brisk = false;
            float yawD = 0f, pitchD = 0f;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                brisk = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                move += gp.leftStick.ReadValue();
                brisk |= gp.leftShoulder.isPressed || gp.rightShoulder.isPressed;
                Vector2 rs = gp.rightStick.ReadValue();
                yawD += rs.x * Mathf.Abs(rs.x) * GamepadYawRate * dt;      // squared response, sign kept
                pitchD += rs.y * Mathf.Abs(rs.y) * GamepadPitchRate * dt;
            }

            var mouse = Mouse.current;
            if (mouse != null && MouseLookActive())
            {
                Vector2 md = mouse.delta.ReadValue();   // pixels, accumulated for this frame (raw input, no smoothing)
                yawD += md.x * MouseDegPerPixel;
                pitchD += md.y * MouseDegPerPixel;     // mouse up looks up (Y not inverted)
            }

            if (move.sqrMagnitude > 1f) move.Normalize();
            input.Move = move;
            input.Brisk = brisk;
            input.YawDelta = yawD;
            input.PitchDelta = pitchD;
            HumanInputThisFrame = move.sqrMagnitude > 0.01f || brisk || Mathf.Abs(yawD) + Mathf.Abs(pitchD) > 0.2f;
        }

        bool MouseLookActive()
        {
            if (!ManageCursor || GameState.CaptureMode) return true;
            return Cursor.lockState == CursorLockMode.Locked;
        }

        void UpdateCursor(bool force)
        {
            if (!ManageCursor || GameState.CaptureMode) return;
            if (GameState.Paused)
            {
                if (force || Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                if (force || !Cursor.visible) Cursor.visible = true;
            }
            else if (Application.isFocused)
            {
                if (force || Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                if (force || Cursor.visible) Cursor.visible = false;
            }
        }

        void ApplyLook(in PlayerInputFrame input)
        {
            if (input.AbsoluteLook)
            {
                Yaw = input.Yaw;
                Pitch = input.Pitch;
            }
            else
            {
                Yaw += input.YawDelta;
                Pitch += input.PitchDelta;
            }
            Yaw = Mathf.Repeat(Yaw, 360f);
            Pitch = Mathf.Clamp(Pitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
        }

        /// <summary>Desired horizontal velocity (world x, z) from the input frame.</summary>
        Vector2 WishVelocity(in PlayerInputFrame input)
        {
            float mag = input.Move.magnitude;
            if (mag < 1e-4f) return Vector2.zero;
            Vector2 dir = input.Move / mag;
            if (mag > 1f) mag = 1f;
            Vector2 world;
            if (input.MoveIsWorld) world = dir;
            else
            {
                float yr = Yaw * Mathf.Deg2Rad;
                float s = Mathf.Sin(yr), c = Mathf.Cos(yr);
                // forward = (sin, cos), right = (cos, -sin) in (x, z)
                world = new Vector2(c * dir.x + s * dir.y, -s * dir.x + c * dir.y);
            }
            float speed = input.SpeedOverride > 0f ? input.SpeedOverride : (input.Brisk ? AbyssConst.V_BRISK : AbyssConst.V_WALK);
            return world * (speed * mag);
        }

        /// <summary>Critically damped velocity filter, about 0.2 s to 90 % (Part 9 acceleration smoothing), exact for any dt.</summary>
        void FilterVelocity(Vector2 wish, float dt)
        {
            float w = 4f / AbyssConst.ACCEL_SMOOTH;
            Vector2 delta = _vel - wish;
            Vector2 j = (_acc + w * delta) * dt;
            float e = Mathf.Exp(-w * dt);
            _vel = wish + (delta + j) * e;
            _acc = (_acc - w * j) * e;
            if (wish == Vector2.zero && _vel.sqrMagnitude < 1e-8f) { _vel = Vector2.zero; _acc = Vector2.zero; }
        }

        // ---------------------------------------------------------------- character controller movement

        Vector2 MoveCharacter(float dt)
        {
            Vector3 before = transform.position;
            Vector3 motion = new Vector3(_vel.x * dt, 0f, _vel.y * dt);
            float hs = _vel.magnitude;
            if (_grounded)
            {
                _vy = 0f;
                // ground snap: enough to follow a slopeLimit ramp downhill at the current speed, never a visible drop
                float snap = Mathf.Clamp(hs * dt * Mathf.Tan(SlopeLimit * Mathf.Deg2Rad) + 0.005f, 0.02f, StepOffset);
                motion.y = -snap;
            }
            else
            {
                _vy = Mathf.Max(_vy - Gravity * dt, -30f);
                motion.y = _vy * dt;
            }

            float backstopY = 0f;
            if (FloorBackstop)
            {
                // no collider carries the path here: never sink below the analytic floor at the destination (slopes followed)
                float dNext = PathGuide.Project(new Vector3(before.x + motion.x, before.y - SkinWidth, before.z + motion.z), PathD);
                backstopY = PathGuide.FloorPoint(dNext, AfterRide).y + SkinWidth;
                float minDy = backstopY - before.y;
                if (motion.y < minDy) motion.y = minDy;
            }

            CollisionFlags flags = _cc.Move(motion);
            bool below = (flags & CollisionFlags.Below) != 0 || _cc.isGrounded;
            if (FloorBackstop && transform.position.y <= backstopY + 0.002f) below = true;
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0f) _vy = 0f;
            _grounded = below;

            Vector3 after = transform.position;
            Vector3 f = after - new Vector3(0f, SkinWidth, 0f);
            FeetPosition = f;
            SmoothEye(f.y, dt);
            EyePosition = new Vector3(f.x, (float)_eyeY + AbyssConst.EYE, f.z);
            return new Vector2(after.x - before.x, after.z - before.z);
        }

        /// <summary>Critically damped smoothing of the feet height (omega = 2 / 0.15 s), exact step for constant target.</summary>
        void SmoothEye(double target, float dt)
        {
            double w = 2.0 / AbyssConst.EYE_SMOOTH;
            double delta = _eyeY - target;
            double j = (_eyeVy + w * delta) * dt;
            double e = System.Math.Exp(-w * dt);
            _eyeY = target + (delta + j) * e;
            _eyeVy = (_eyeVy - w * j) * e;
        }

        // ---------------------------------------------------------------- lift (14.7.1)

        void TryEnterLift(LiftController lift)
        {
            if (lift == null || !lift.PlatformStatic) return;
            Vector3 pp = lift.transform.position;
            Vector3 f = transform.position - new Vector3(0f, SkinWidth, 0f);
            double lx = (double)f.x - pp.x, lz = (double)f.z - pp.z, ly = (double)f.y - pp.y;
            if (System.Math.Abs(lx) > DeckFootprint || System.Math.Abs(lz) > DeckFootprint || System.Math.Abs(ly) > 0.75) return;
            // enter only on the walkable rectangle or through an open opening; elsewhere the deck rails (Beat05 colliders) apply
            const double m = 0.05;
            double lim = LiftController.WalkHalf;
            bool inRect = System.Math.Abs(lx) <= lim + m && System.Math.Abs(lz) <= lim + m;
            bool inBoarding = lift.BoardingOpen && lx > 0.0 && System.Math.Abs(lz) <= LiftController.OPENING_HALF + m;
            bool inGate = (lift.Phase == LiftPhase.Arrived || lift.Phase == LiftPhase.Left) && lz < 0.0 && System.Math.Abs(lx) <= GateHalf + m;
            if (!inRect && !inBoarding && !inGate) return;

            _lx = lx; _lz = lz; _ly = 0.0;
            _eyeY -= pp.y;                       // smoother state into deck-local height (platform docked: exact)
            _cc.enabled = false;
            _kinematic = true;
            OnLift = true;
            _grounded = true;
            _vy = 0f;
        }

        void ExitLift(LiftController lift)
        {
            Vector3 f;
            if (lift != null)
            {
                Vector3 pp = lift.transform.position;
                f = new Vector3((float)(pp.x + _lx), (float)(pp.y + _ly), (float)(pp.z + _lz));
                _eyeY += pp.y;
            }
            else
            {
                // platform destroyed while aboard: keep the last world feet position
                f = FeetPosition;
                _eyeY = f.y;
                _eyeVy = 0.0;
            }
            _kinematic = false;
            OnLift = false;
            transform.position = f + new Vector3(0f, SkinWidth, 0f);
            _cc.enabled = true;
            Physics.SyncTransforms();
            _grounded = true;
            _vy = 0f;
            FeetPosition = f;
        }

        /// <summary>Clamp v into [lo, hi] without ever pushing a position that is already outside further out or snapping it in.</summary>
        static double ClampSoft(double v, double lo, double hi, double cur)
        {
            if (cur < lo) lo = cur;
            if (cur > hi) hi = cur;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        Vector2 MoveOnLift(LiftController lift, float dt)
        {
            double lim = LiftController.WalkHalf;
            double open = LiftController.OPENING_HALF;
            double gate = GateHalf;
            double nx = _lx + _vel.x * dt, nz = _lz + _vel.y * dt;

            bool plusX = lift.BoardingOpen && lift.BoardingRailClosed01 < 0.05f;
            bool minusZ = lift.GatePassable;

            double hiX = (plusX && System.Math.Abs(nz) <= open) ? DeckExit : lim;
            nx = ClampSoft(nx, -lim, hiX, _lx);
            if (nx > lim) nz = ClampSoft(nz, -open, open, _lz);          // inside the boarding opening corridor
            double loZ = (minusZ && System.Math.Abs(nx) <= gate) ? -DeckExit : -lim;
            nz = ClampSoft(nz, loZ, lim, _lz);
            if (nz < -lim) nx = ClampSoft(nx, -gate, gate, _lx);         // inside the gate corridor
            LiftController.KeepOut(LiftController.DeckKeepOut, _lx, _lz, ref nx, ref nz); // pylons inside the walk square

            Vector2 moved = new Vector2((float)(nx - _lx), (float)(nz - _lz));
            _lx = nx; _lz = nz; _ly = 0.0;

            SmoothEye(0.0, dt);
            Vector3 pp = lift.transform.position;
            Vector3 f = new Vector3((float)(pp.x + _lx), (float)(pp.y + _ly), (float)(pp.z + _lz));
            transform.position = f + new Vector3(0f, SkinWidth, 0f);    // keeps the disabled controller consistent for leaving
            FeetPosition = f;
            EyePosition = new Vector3(f.x, (float)(pp.y + _eyeY + AbyssConst.EYE), f.z);

            // leave through an opening (only possible while docked: the clamps keep the player inside during the ride)
            if (lift.PlatformStatic && (System.Math.Abs(_lx) > DeckFootprint || System.Math.Abs(_lz) > DeckFootprint))
                ExitLift(lift);
            return moved;
        }

        /// <summary>
        /// Eye position recomputed from the current platform transform (CameraRig LateUpdate). Off the lift this is
        /// <see cref="EyePosition"/>.
        /// </summary>
        public Vector3 ComputeEyeWorld()
        {
            if (_kinematic)
            {
                var lift = LiftController.Instance;
                if (lift != null)
                {
                    Vector3 pp = lift.transform.position;
                    return new Vector3((float)(pp.x + _lx), (float)(pp.y + _eyeY + AbyssConst.EYE), (float)(pp.z + _lz));
                }
            }
            return EyePosition;
        }

        // ---------------------------------------------------------------- path, footsteps, safety

        void UpdatePathAndSurface(LiftController lift)
        {
            if (_kinematic && lift != null)
            {
                switch (lift.Phase)
                {
                    case LiftPhase.DockedTop:
                        PathD = AbyssConst.D_LIFT - Mathf.Clamp((float)_lx, 0f, AbyssConst.LIFT_SIZE * 0.5f);
                        AfterRide = false;
                        break;
                    case LiftPhase.Dwell:
                    case LiftPhase.Riding:
                        PathD = AbyssConst.D_LIFT;
                        AfterRide = false;
                        break;
                    default:
                        PathD = AbyssConst.D_LIFT + Mathf.Clamp(-(float)_lz, 0f, AbyssConst.LIFT_SIZE * 0.5f);
                        AfterRide = true;
                        break;
                }
                CurrentSurface = SurfaceKind.LiftDeck;
                return;
            }
            PathD = PathGuide.Project(FeetPosition, PathD);
            AfterRide = PathD > AbyssConst.D_LIFT || (lift != null && lift.Phase >= LiftPhase.Arrived);
            CurrentSurface = PathGuide.Surface(PathD);
        }

        void UpdateFootsteps(Vector2 moved, float dt)
        {
            TimeSinceTread += dt;
            if (GameState.Ended) return;
            float dist = moved.magnitude;
            bool contact = _grounded || _kinematic;
            if (CurrentSurface == SurfaceKind.Stair)
            {
                int tread = PathGuide.StairTread(PathD);
                if (tread >= 0 && tread != _lastTread && contact)
                {
                    AudioEvents.Footstep(SurfaceKind.Stair);
                    TimeSinceTread = 0f;
                    _stepDist = 0f;
                }
                _lastTread = tread;
            }
            else
            {
                _lastTread = -1;
                if (contact && dist > 0f)
                {
                    _stepDist += dist;
                    if (_stepDist >= STEP_LENGTH)
                    {
                        _stepDist -= STEP_LENGTH;
                        if (_stepDist > STEP_LENGTH) _stepDist = 0f;
                        AudioEvents.Footstep(CurrentSurface);
                    }
                }
            }
            StepPhase01 = Mathf.Clamp01(_stepDist / STEP_LENGTH);
        }

        void UpdateSafety(float dt)
        {
            _safetyCooldown -= dt;
            Vector3 f = FeetPosition;
            Vector3 floor = PathGuide.FloorPoint(PathD, AfterRide);

            if (_grounded)
            {
                _groundedTime += dt;
                if (_groundedTime > 0.3f && Mathf.Abs(f.y - floor.y) < 1.0f)
                {
                    _lastSafeD = PathD;
                    _lastSafeAfter = AfterRide;
                    _fallsWithoutGround = 0;
                }
            }
            else _groundedTime = 0f;

            if (_safetyCooldown > 0f) { _safetyHit = false; return; }

            if (!_safetyHit)
            {
                Vector3 p0 = transform.position + new Vector3(0f, AbyssConst.CAPSULE_R, 0f);
                Vector3 p1 = transform.position + new Vector3(0f, AbyssConst.CAPSULE_H - AbyssConst.CAPSULE_R, 0f);
                int n = Physics.OverlapCapsuleNonAlloc(p0, p1, AbyssConst.CAPSULE_R, _overlap, SafetyLayerMask, QueryTriggerInteraction.Collide);
                if (n > 0) _safetyHit = true;
            }

            if (_safetyHit) ReturnToSafePoint("safety volume", false);
            else if (f.y < floor.y - FallLimit || f.y < AbyssConst.SHAFT_BOTTOM_Y) ReturnToSafePoint("fall", true);
            _safetyHit = false;
        }

        void ReturnToSafePoint(string reason, bool fall)
        {
            Vector3 from = FeetPosition;
            // never return onto a lift deck that is not docked at that level (it would be empty air)
            var lift = LiftController.Instance;
            var WD = AbyssConst.WD;
            if (lift != null && _lastSafeD > WD[11] - 0.5f && _lastSafeD < WD[14] + 0.5f)
            {
                if (!_lastSafeAfter && lift.Phase != LiftPhase.DockedTop) _lastSafeD = WD[11] - 1.5f;
                else if (_lastSafeAfter && lift.Phase < LiftPhase.Arrived) _lastSafeD = WD[14] + 1.5f;
            }
            Vector3 p = PathGuide.WalkPoint(_lastSafeD, _lastSafeAfter);
            SafetyReturns++;
            if (fall) _fallsWithoutGround++;
            // Vector3.ToString(format) is culture-invariant; floats need the invariant culture explicitly ("d=0,00" otherwise)
            if (SafetyReturns <= SAFETY_LOG_FIRST || SafetyReturns % SAFETY_LOG_EVERY == 0)
                Debug.Log("[ABYSS] Safety return #" + SafetyReturns + " (" + reason + ") from " + from.ToString("F2") +
                          " to d=" + _lastSafeD.ToString("F2", CultureInfo.InvariantCulture) +
                          (SafetyReturns == SAFETY_LOG_FIRST ? " (from now on only every " + SAFETY_LOG_EVERY + "th return is logged)" : ""));
            if (!FloorBackstop && _fallsWithoutGround >= NO_FLOOR_RETURNS)
            {
                FloorBackstop = true;
                Debug.LogWarning("[ABYSS] PlayerController: " + _fallsWithoutGround + " falls in a row without ground near d=" +
                                 _lastSafeD.ToString("F2", CultureInfo.InvariantCulture) +
                                 ": no corridor collider under the path (beat not built?); feet held on the analytic path floor from now on");
            }
            Teleport(p, Yaw, Pitch);
            _safetyCooldown = 1f;
        }

        void Publish(float dt)
        {
            Vector3 f = FeetPosition;
            if (dt > 0f) Velocity = (f - _prevFeet) / dt;
            _prevFeet = f;
            LocalVelocity = new Vector3(_vel.x, 0f, _vel.y);
            HorizontalSpeed = _vel.magnitude;
            Grounded = _grounded || _kinematic;
            GameState.PlayerPos = f;
        }

        // ---------------------------------------------------------------- public API

        /// <summary>
        /// Place the feet at a world position with a look orientation (start, -beat / -progress, safety returns, QA).
        /// Leaves the lift if aboard; resets velocity, eye smoothing and footstep state. The lift re-enters kinematic mode on
        /// the next frame if the position is on the docked deck.
        /// </summary>
        public void Teleport(Vector3 feetPos, float yawDeg, float pitchDeg)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _kinematic = false;
            OnLift = false;
            _cc.enabled = false;
            transform.position = feetPos + new Vector3(0f, SkinWidth, 0f);
            _cc.enabled = true;
            Physics.SyncTransforms();

            Yaw = Mathf.Repeat(yawDeg, 360f);
            Pitch = Mathf.Clamp(pitchDeg, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
            _vel = Vector2.zero; _acc = Vector2.zero; _vy = 0f;
            _grounded = true;
            _groundedTime = 0f;
            _eyeY = feetPos.y; _eyeVy = 0.0;
            FeetPosition = feetPos;
            _prevFeet = feetPos;
            EyePosition = new Vector3(feetPos.x, feetPos.y + AbyssConst.EYE, feetPos.z);

            PathD = PathGuide.Project(feetPos, -1f);
            AfterRide = PathD > AbyssConst.D_LIFT || feetPos.y < AbyssConst.LIFT_TOP.y - 100f;
            _lastSafeD = PathD;
            _lastSafeAfter = AfterRide;
            CurrentSurface = PathGuide.Surface(PathD);
            _stepDist = 0f;
            _lastTread = PathGuide.StairTread(PathD);
            TimeSinceTread = 10f;
            _safetyHit = false;
            HasBeenPlaced = true;
            TeleportCount++;
            Velocity = Vector3.zero;
            Publish(0f);

            var walker = AutoWalker.Instance;
            if (walker != null) walker.OnPlayerTeleported();
        }

        /// <summary>Teleport to the walked curve at d, facing along the path.</summary>
        public void TeleportToD(float d, bool afterRide, float pitchDeg = 0f)
        {
            Teleport(PathGuide.WalkPoint(d, afterRide), PathGuide.HeadingYaw(d, afterRide), pitchDeg);
        }
    }
}
