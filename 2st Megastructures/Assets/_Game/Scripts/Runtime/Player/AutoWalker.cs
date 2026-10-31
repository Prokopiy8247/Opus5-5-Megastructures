using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// QA and cinematic autowalk (Part 9, 11, 14.1, 14.5 holds, 14.10 autoplay); the logic lives in
    /// <see cref="WalkPlanner"/>, this component feeds it the player state and applies its output.
    /// The walker follows the polyline with circular fillets (r 1.2 at (0, 576), 4 at (100, 576) and (100, 660)) by
    /// moving the CharacterController through PlayerController.DriveExternal, so every collider applies.
    /// - QA / -autotest: constant 1.5 m/s (2.5 with -speed brisk), 0.2 s start, seed from -seed, no holds, gaze forward
    ///   along the path at pitch 0; reaches the goal trigger (s 959) at about 636.8 s.
    /// - Cinematic (C key or -cinematic): starts from the current progress, eases into and out of the 8 holds
    ///   (49 s), plays the GazeSchedule with the critically damped follower (omega 3 rad/s), a deterministic handheld
    ///   sway (&lt;= 0.3 degrees) and the capsule lateral offset of 14.5.1.
    /// Runs before the PlayerController (execution order -120). Allocation-free per frame.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class AutoWalker : MonoBehaviour
    {
        /// <summary>The walk path (14.1 fillets 1.2 / 4 / 4 m).</summary>
        public static readonly FilletPath Path = new FilletPath(1.2f, 4f, 4f);

        public static AutoWalker Instance { get; private set; }
        /// <summary>True while the walker drives the player.</summary>
        public static bool Active { get; private set; }
        public static WalkMode Mode { get; private set; }
        /// <summary>RingState.T when the goal trigger (s 959) fired; NaN before.</summary>
        public static double TimeAtGoal { get; private set; } = double.NaN;
        /// <summary>Sharp progress of the walker's own path position (diagnostics).</summary>
        public static float WalkerS { get; private set; }

        WalkPlanner _plan;
        int _seed;
        WalkMode _forced;
        float _forcedSpeed = RingConstants.Walk;
        bool _restart;
        PlayerController _pc;
        bool _subscribed;

        public static void ResetStatics()
        {
            // Instance / Active are owned by the component (it may wake before the bootstrap)
            TimeAtGoal = double.NaN; WalkerS = 0f;
        }

        /// <summary>External control (QA harness): force a walk mode from the current progress; None returns to normal.
        /// A request while a walk is running restarts it with the new mode and speed on the next frame.</summary>
        public static void Request(WalkMode mode, float speed = RingConstants.Walk)
        {
            if (Instance == null) return;
            Instance._forced = mode;
            Instance._forcedSpeed = speed;
            Instance._restart = true;
        }

        /// <summary>QA entry point (RingQaHooks.StartAutoWalk by reflection): constant-speed walk along the filleted
        /// path, no holds, no sway, gaze forward at pitch 0, sway seed = seed. fromS (NaN = current progress) places
        /// the player on the path first when it differs from the current progress by more than 1 m.</summary>
        public static bool BeginQa(float speed, int seed, float fromS)
        {
            var w = Instance;
            if (w == null || RingRig.Player == null) return false;
            if (w._plan == null || w._seed != seed) { w._plan = new WalkPlanner(Path, seed); w._seed = seed; w.Mode0(); }
            if (!float.IsNaN(fromS) && Mathf.Abs(fromS - RingState.S) > 1f) PlaceAt(fromS);
            Request(WalkMode.AutoTest, Mathf.Clamp(speed, 0.1f, 5f));
            return true;
        }

        /// <summary>Starts the cinematic autowalk (QA: RingQaHooks.StartCinematic by reflection).</summary>
        public static void StartCinematic() => SetCinematic(true);

        /// <summary>Cinematic on / off. During a restart (scene reload pending) the request is carried to the next load.</summary>
        public static void SetCinematic(bool on)
        {
            if (GameBootstrap.Restarting) { GameBootstrap.CinematicOnLoad = on; return; }
            PlayerController.SetCinematic(on);
        }

        /// <summary>Places the player's feet on the walk path at sharp progress s, facing the path direction.</summary>
        public static void PlaceAt(float s)
        {
            s = Mathf.Clamp(s, 0f, RingConstants.PathLength);
            float d = Path.DAtSharp(s);
            Vector2 xz = Path.PointAt(d);
            var feet = new Vector3(xz.x, PathGuide.WalkHeight(xz.x, xz.y), xz.y);
            var pc = PlayerController.Instance;
            if (pc != null) pc.Teleport(feet, Path.HeadingAt(d));
            ProgressDirector.SnapTo(s);
        }

        void Mode0()
        {
            if (Mode != WalkMode.None) { Mode = WalkMode.None; Active = false; var rig = CameraRig.Instance; if (rig != null) rig.ExternalLook = false; }
        }

        /// <summary>The planner (QA reads hold state and speed).</summary>
        public WalkPlanner Planner => _plan;

        void Awake()
        {
            Instance = this;
            Active = false; Mode = WalkMode.None;   // a reloaded scene never inherits the previous walker's state
            _pc = GetComponent<PlayerController>();
        }

        void Start()
        {
            if (_plan == null) { _seed = CliArgs.Seed; _plan = new WalkPlanner(Path, _seed); }
            RingEvents.GoalReached += OnGoal;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed) RingEvents.GoalReached -= OnGoal;
            if (Instance == this) { Instance = null; Active = false; Mode = WalkMode.None; }
        }

        static void OnGoal()
        {
            if (double.IsNaN(TimeAtGoal)) TimeAtGoal = RingState.T;
        }

        WalkMode Wanted()
        {
            if (_forced != WalkMode.None) return _forced;
            if (CliArgs.AutoTest && !CliArgs.Capturing) return WalkMode.AutoTest;
            if (RingState.Cinematic) return WalkMode.Cinematic;
            return WalkMode.None;
        }

        void Update()
        {
            if (!RingRig.WorldReady || RingRig.Player == null || _plan == null) return;
            if (_pc == null) _pc = PlayerController.Instance;
            var want = Wanted();
            if (want != Mode || _restart)
            {
                _restart = false;
                if (Mode != WalkMode.None) Deactivate();
                if (want != WalkMode.None) Activate(want);
            }
            if (Mode == WalkMode.None || RingState.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = RingRig.Player.position;
            var xz = new Vector2(pos.x, pos.z);
            _plan.Step(dt, xz, RingState.T);
            WalkerS = _plan.SharpS;

            // drive the controller toward the target (catch-up limited to 1.5 x walking speed)
            Vector2 delta = _plan.Target - xz;
            float maxStep = 1.5f * Mathf.Max(_plan.Speed, RingConstants.Walk) * dt;
            if (delta.sqrMagnitude > maxStep * maxStep) delta = delta.normalized * maxStep;
            if (_pc != null) _pc.DriveExternal(new Vector3(delta.x, 0f, delta.y));

            var rig = CameraRig.Instance;
            if (rig != null) { rig.ExternalLook = true; rig.SetLook(_plan.OutYaw, _plan.OutPitch); }
        }

        void Activate(WalkMode m)
        {
            float cruise = RingConstants.Walk;
            if (m == WalkMode.AutoTest)
            {
                string sp = CliArgs.Str("speed");
                if (sp != null && sp.Equals("brisk", System.StringComparison.OrdinalIgnoreCase)) cruise = RingConstants.Brisk;
            }
            if (_forced != WalkMode.None) cruise = _forcedSpeed;
            Vector3 pos = RingRig.Player.position;
            Vector3 pv = RingRig.PlayerVelocity;
            var rig = CameraRig.Instance;
            float yaw = rig != null ? rig.Yaw : RingRig.Yaw, pitch = rig != null ? rig.Pitch : RingRig.Pitch;
            _plan.Begin(m, new Vector2(pos.x, pos.z), RingState.S, new Vector2(pv.x, pv.z), yaw, pitch, cruise);
            Mode = m;
            Active = true;
            if (rig != null) rig.ExternalLook = true;
            Debug.Log("[Ring] AutoWalker " + m + " from s " + _plan.SharpS.ToString("0.0") + " at " + cruise.ToString("0.0") + " m/s");
        }

        void Deactivate()
        {
            _plan.Stop();
            Mode = WalkMode.None;
            Active = false;
            var rig = CameraRig.Instance;
            if (rig != null) rig.ExternalLook = false;
        }
    }
}
