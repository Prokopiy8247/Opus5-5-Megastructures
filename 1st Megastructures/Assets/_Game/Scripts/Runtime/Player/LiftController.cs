using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The house-sized lift of beat 5 (14.5.5, 14.7.1). Beat05 adds this component to its platform root in Build; the
    /// transform origin is the deck centre at deck-top height, axis-aligned (no tilt), and every visual of the platform
    /// (deck, rails, pylons, cables, guide shoes) is a child so it moves with it.
    ///
    /// Motion is analytic: a double clock <c>liftT</c> advanced by <c>Time.deltaTime</c> in Update (execution order -200, before
    /// PlayerController -100 and CameraRig LateUpdate +100) gives the position <c>LIFT_TOP + (s(t) / 480) * (117, -480, 0)</c>
    /// with s(t) = AbyssConst.LiftS. No physics, no parenting of the player: the PlayerController rides kinematically in
    /// platform coordinates, so camera-to-platform jitter is one float rounding (14.7.1).
    ///
    /// Phases (GameState.Lift): DockedTop -> (player aboard on the 18 x 18 deck and at the centre, or aboard 8 s) Dwell 2 s
    /// (boarding rail closes over 1.5 s, the bridging plate retracts in the last second) -> Riding 120 s -> Arrived (plate
    /// extends over 1 s, gate opens 1 s after arrival over 1.5 s) -> Left (the player stepped off through the gate).
    /// Publishes GameState.Lift, RideSeconds (-1 before departure, 0..120 riding, keeps counting after), RideU and RideStarted.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class LiftController : MonoBehaviour
    {
        public static LiftController Instance;

        // ---------------------------------------------------------------- timings (14.5.5)
        public const float BOARD_CENTRE_RADIUS = 0.35f;   // dwell starts when the player stands this close to the deck centre
        public const float BOARD_TIMEOUT = 8f;            // ... or after this long anywhere on the deck
        public const float RAIL_CLOSE_TIME = 1.5f;
        public const float PLATE_TIME = 1f;
        public const float GATE_TIME = 1.5f;
        /// <summary>Half-size of the deck rectangle the capsule centre may occupy: walkable 18 x 18 minus the capsule radius.</summary>
        public static float WalkHalf => AbyssConst.LIFT_WALKABLE * 0.5f - AbyssConst.CAPSULE_R;
        /// <summary>Half-width of the boarding opening (+X, 4 m) and the gate (-Z, 4 m) for the capsule centre.</summary>
        public const float OPENING_HALF = 2f - AbyssConst.CAPSULE_R;

        /// <summary>
        /// Capsule-centre keep-out rectangles on the deck in platform-local (x, z) (the four pylons at (+/-7, +/-7) stand inside
        /// the 18 x 18 walk square, 14.5.5). Set by Beat05 in Build; applied by PlayerController.MoveOnLift after its clamps.
        /// </summary>
        public static Rect[] DeckKeepOut;

        /// <summary>
        /// Keep a deck-local capsule-centre move (cx, cz) -> (nx, nz) out of the keep-out rects: a move from outside that ends
        /// inside stops on the face it crossed and slides along it; a position already inside never moves deeper.
        /// (Same algorithm as Beat05Layout.KeepOut, verified by Beat05Tests.) Allocation-free.
        /// </summary>
        public static void KeepOut(Rect[] rects, double cx, double cz, ref double nx, ref double nz)
        {
            if (rects == null) return;
            for (int i = 0; i < rects.Length; i++)
            {
                Rect r = rects[i];
                double x0 = r.xMin, x1 = r.xMax, z0 = r.yMin, z1 = r.yMax;
                if (nx <= x0 || nx >= x1 || nz <= z0 || nz >= z1) continue;
                bool outX = cx <= x0 || cx >= x1, outZ = cz <= z0 || cz >= z1;
                if (!outX && !outZ)
                {
                    double pc = Depth(cx, cz, x0, x1, z0, z1);
                    if (Depth(nx, nz, x0, x1, z0, z1) <= pc) continue;
                    if (Depth(nx, cz, x0, x1, z0, z1) <= pc) { nz = cz; continue; }
                    if (Depth(cx, nz, x0, x1, z0, z1) <= pc) { nx = cx; continue; }
                    nx = cx; nz = cz;
                    continue;
                }
                bool alongX;
                if (outX && outZ)
                {
                    double px = System.Math.Min(nx - x0, x1 - nx), pz = System.Math.Min(nz - z0, z1 - nz);
                    alongX = px <= pz;
                }
                else alongX = outX;
                if (alongX) nx = cx <= x0 ? x0 : x1;
                else nz = cz <= z0 ? z0 : z1;
            }
        }

        static double Depth(double x, double z, double x0, double x1, double z0, double z1)
            => System.Math.Min(System.Math.Min(x - x0, x1 - x), System.Math.Min(z - z0, z1 - z));

        // ---------------------------------------------------------------- outputs for Beat05 visuals (0..1, eased)
        public float GateOpen01;
        public float BoardingRailClosed01;
        public float PlateExtend01 = 1f;

        // ---------------------------------------------------------------- state
        /// <summary>QA: when true the lift clock does not advance (platform frozen; independent of SceneClock.Frozen).</summary>
        public bool Frozen;

        /// <summary>Move the lift to the player's level when the player is teleported to the other level (-beat / -progress).</summary>
        public bool AutoSnapToPlayerLevel = true;

        /// <summary>QA: max |cameraWorld - TransformPoint(localPos + eye)| in metres over the ride (14.7.1, at most 0.5 mm).</summary>
        public float MaxOffsetError;

        /// <summary>Number of camera offset samples recorded during the ride.</summary>
        public int OffsetSamples;

        LiftPhase _phase = LiftPhase.DockedTop;
        double _liftT;          // ride seconds 0..120
        float _dwell;           // seconds into the dwell
        float _arrival;         // seconds since arrival
        float _aboard;          // seconds the player has been aboard while docked at the top
        double _s;              // travel in metres
        bool _forceDeparture;

        public LiftPhase Phase => _phase;
        public double LiftT => _liftT;
        public float DwellSeconds => _dwell;
        public float ArrivalSeconds => _arrival;
        public float AboardSeconds => _aboard;
        /// <summary>Travel s(t) in metres (0..480).</summary>
        public float S => (float)_s;
        /// <summary>Speed along the lift line ds/dt (m/s).</summary>
        public float Speed { get; private set; }
        /// <summary>World velocity of the platform (m/s).</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>True when the platform does not move this frame (docked at either end or in the dwell).</summary>
        public bool PlatformStatic => _phase != LiftPhase.Riding;
        /// <summary>True while the +X boarding opening is open (docked at the top, before the dwell).</summary>
        public bool BoardingOpen => _phase == LiftPhase.DockedTop;
        /// <summary>True once the gate is open enough for the capsule to pass (Arrived / Left).</summary>
        public bool GatePassable => (_phase == LiftPhase.Arrived || _phase == LiftPhase.Left) && GateOpen01 >= 0.7f;
        /// <summary>True from the moment the gate starts opening (1 s after arrival): the AutoWalker may set off.</summary>
        public bool GateOpening => (_phase == LiftPhase.Arrived || _phase == LiftPhase.Left) && _arrival >= AbyssConst.LIFT_GATE_DELAY;

        static readonly Vector3 Travel = AbyssConst.LIFT_BOTTOM - AbyssConst.LIFT_TOP;   // (117, -480, 0)
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[ABYSS] LiftController: replacing a previous instance");
            Instance = this;
            if (transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                Debug.LogWarning("[ABYSS] LiftController: platform root must be axis-aligned with unit scale (14.7.1)");
            ResetToTop();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- kinematics

        // exact decimal values of the float constants, matching AbyssConst.LiftS (4.8f is 4.80000019 as a float)
        static readonly double RampD = System.Math.Round((double)AbyssConst.LIFT_RAMP, 6);
        static readonly double DurationD = System.Math.Round((double)AbyssConst.LIFT_DURATION, 6);
        static readonly double CruiseD = System.Math.Round((double)AbyssConst.LIFT_CRUISE, 6);

        /// <summary>Lift speed ds/dt (m/s) at ride seconds t (derivative of AbyssConst.LiftS, same exact double constants).</summary>
        public static double LiftSpeed(double t)
        {
            double ramp = RampD, dur = DurationD;
            if (t <= 0.0 || t >= dur) return 0.0;
            // ramp s = cruise ramp (x^3 - x^4 / 2), x = t / ramp  ->  ds/dt = cruise (3 x^2 - 2 x^3)
            if (t < ramp) { double x = t / ramp; return CruiseD * (3.0 * x * x - 2.0 * x * x * x); }
            if (t <= dur - ramp) return CruiseD;
            double y = (dur - t) / ramp;
            return CruiseD * (3.0 * y * y - 2.0 * y * y * y);
        }

        /// <summary>Deck-centre position (deck-top height) for a travel s in metres, computed in double.</summary>
        public static Vector3 PositionAtS(double s)
        {
            double u = s / AbyssConst.LIFT_DROP;
            var top = AbyssConst.LIFT_TOP;
            return new Vector3((float)(top.x + u * Travel.x), (float)(top.y + u * Travel.y), (float)(top.z + u * Travel.z));
        }

        void ApplyTransform()
        {
            double s;
            switch (_phase)
            {
                case LiftPhase.Riding: s = AbyssConst.LiftS(_liftT); break;
                case LiftPhase.Arrived:
                case LiftPhase.Left: s = AbyssConst.LIFT_DROP; break;
                default: s = 0.0; break;
            }
            _s = s;
            Vector3 p = PositionAtS(s);
            Vector3 cur = transform.position;
            if (cur.x != p.x || cur.y != p.y || cur.z != p.z)   // exact compare (Vector3 == is approximate)
            {
                transform.position = p;
                Physics.SyncTransforms();   // platform colliders (if any) must be where the transform is for this frame's queries
            }
            double v = _phase == LiftPhase.Riding ? LiftSpeed(_liftT) : 0.0;
            Speed = (float)v;
            Velocity = Travel * (float)(v / AbyssConst.LIFT_DROP);
        }

        void Publish()
        {
            GameState.Lift = _phase;
            switch (_phase)
            {
                case LiftPhase.Riding: GameState.RideSeconds = (float)_liftT; break;
                case LiftPhase.Arrived:
                case LiftPhase.Left: GameState.RideSeconds = AbyssConst.LIFT_DURATION + _arrival; break;
                default: GameState.RideSeconds = -1f; break;
            }
            GameState.RideU = (float)(_s / AbyssConst.LIFT_DROP);
            GameState.RideStarted = _phase >= LiftPhase.Riding;
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (Frozen || GameState.Paused) dt = 0f;
            var pc = PlayerController.Instance;

            switch (_phase)
            {
                case LiftPhase.DockedTop:
                {
                    if (AutoSnapToPlayerLevel && !Frozen && !GameState.CaptureMode && pc != null && !pc.OnLift && pc.HasBeenPlaced &&
                        pc.FeetPosition.y < AbyssConst.LIFT_TOP.y - 100f)
                    {
                        SetLeft();
                        break;
                    }
                    bool aboard = IsPlayerAboard(pc, out float centreDist);
                    _aboard = aboard ? _aboard + dt : 0f;
                    if (_forceDeparture || (aboard && (centreDist <= BOARD_CENTRE_RADIUS || _aboard >= BOARD_TIMEOUT)))
                        StartDwell();
                    break;
                }
                case LiftPhase.Dwell:
                {
                    _dwell += dt;
                    BoardingRailClosed01 = Ease(_dwell / RAIL_CLOSE_TIME);
                    PlateExtend01 = 1f - Ease((_dwell - (AbyssConst.LIFT_DWELL - PLATE_TIME)) / PLATE_TIME);
                    if (_dwell >= AbyssConst.LIFT_DWELL)
                    {
                        _phase = LiftPhase.Riding;
                        _liftT = _dwell - AbyssConst.LIFT_DWELL;   // carry the remainder: exact 2.0 s dwell
                        BoardingRailClosed01 = 1f;
                        PlateExtend01 = 0f;
                        MaxOffsetError = 0f;
                        OffsetSamples = 0;
                        Debug.Log("[ABYSS] Lift departure at t=" + SceneClock.Time.ToString("F2", Inv));
                    }
                    break;
                }
                case LiftPhase.Riding:
                {
                    _liftT += dt;
                    if (_liftT >= AbyssConst.LIFT_DURATION)
                    {
                        _arrival = (float)(_liftT - AbyssConst.LIFT_DURATION);
                        _liftT = AbyssConst.LIFT_DURATION;
                        _phase = LiftPhase.Arrived;
                        Debug.Log("[ABYSS] Lift arrival at t=" + SceneClock.Time.ToString("F2", Inv) +
                                  ", max camera offset error " + (MaxOffsetError * 1000f).ToString("F4", Inv) + " mm over " + OffsetSamples + " frames");
                    }
                    break;
                }
                case LiftPhase.Arrived:
                {
                    _arrival += dt;
                    UpdateArrivalVisuals();
                    if (_arrival > AbyssConst.LIFT_GATE_DELAY && pc != null && !pc.OnLift) _phase = LiftPhase.Left;
                    break;
                }
                case LiftPhase.Left:
                {
                    _arrival += dt;
                    UpdateArrivalVisuals();
                    if (AutoSnapToPlayerLevel && !Frozen && !GameState.CaptureMode && pc != null && !pc.OnLift && pc.HasBeenPlaced &&
                        pc.FeetPosition.y > AbyssConst.LIFT_BOTTOM.y + 100f)
                        ResetToTop();
                    break;
                }
            }
            ApplyTransform();
            Publish();
        }

        void UpdateArrivalVisuals()
        {
            BoardingRailClosed01 = 1f;
            PlateExtend01 = Ease(_arrival / PLATE_TIME);
            GateOpen01 = Ease((_arrival - AbyssConst.LIFT_GATE_DELAY) / GATE_TIME);
        }

        bool IsPlayerAboard(PlayerController pc, out float centreDist)
        {
            centreDist = float.MaxValue;
            if (pc == null || !pc.OnLift) return false;
            Vector3 l = pc.LiftLocalFeet;
            float h = WalkHalf + 1e-3f;
            if (Mathf.Abs(l.x) > h || Mathf.Abs(l.z) > h) return false;
            centreDist = Mathf.Sqrt(l.x * l.x + l.z * l.z);
            return true;
        }

        void StartDwell()
        {
            _phase = LiftPhase.Dwell;
            _dwell = 0f;
            _forceDeparture = false;
            Debug.Log("[ABYSS] Lift dwell at t=" + SceneClock.Time.ToString("F2", Inv));
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);   // smootherstep
        }

        // ---------------------------------------------------------------- public control (QA, bootstrap)

        /// <summary>Start the dwell now (QA bots that do not walk to the centre). Only while docked at the top.</summary>
        public void RequestDeparture()
        {
            if (_phase == LiftPhase.DockedTop) _forceDeparture = true;
        }

        /// <summary>QA (camera offset test): record one |cameraWorld - TransformPoint(localPos + eye)| sample (metres).</summary>
        public void RecordCameraOffset(float error)
        {
            if (_phase != LiftPhase.Riding && _phase != LiftPhase.Dwell) return;
            OffsetSamples++;
            if (error > MaxOffsetError) MaxOffsetError = error;
        }

        /// <summary>Platform at the top dock, waiting (initial state, restart).</summary>
        public void ResetToTop()
        {
            _phase = LiftPhase.DockedTop;
            _liftT = 0.0; _dwell = 0f; _arrival = 0f; _aboard = 0f; _forceDeparture = false;
            GateOpen01 = 0f; BoardingRailClosed01 = 0f; PlateExtend01 = 1f;
            ApplyTransform();
            Publish();
        }

        /// <summary>Platform at the bottom dock with the gate open, as after the ride (player placed below the lift).</summary>
        public void SetLeft()
        {
            _phase = LiftPhase.Left;
            _liftT = AbyssConst.LIFT_DURATION;
            _dwell = AbyssConst.LIFT_DWELL;
            _arrival = AbyssConst.LIFT_GATE_DELAY + GATE_TIME;
            UpdateArrivalVisuals();
            ApplyTransform();
            Publish();
        }

        /// <summary>Place the lift for a start distance d (GameBootstrap after -beat / -progress): bottom when d is past the lift.</summary>
        public void SnapToProgress(float d, bool afterRide)
        {
            if (afterRide || d > AbyssConst.D_LIFT) SetLeft();
            else ResetToTop();
        }

        /// <summary>
        /// Place the platform at s(t) for ride seconds t and set every GameState lift field and visual consistently
        /// (t &lt; 0: docked at the top; 0..120: riding; &gt; 120: arrived, gate per the arrival timeline). Does not freeze the lift:
        /// set <see cref="Frozen"/> for stills.
        /// </summary>
        public void SetRideSecondsForCapture(double rideSeconds)
        {
            if (rideSeconds < 0.0)
            {
                ResetToTop();
                return;
            }
            if (rideSeconds < AbyssConst.LIFT_DURATION)
            {
                _phase = LiftPhase.Riding;
                _liftT = rideSeconds;
                _dwell = AbyssConst.LIFT_DWELL;
                _arrival = 0f;
                GateOpen01 = 0f; BoardingRailClosed01 = 1f; PlateExtend01 = 0f;
            }
            else
            {
                _phase = LiftPhase.Arrived;
                _liftT = AbyssConst.LIFT_DURATION;
                _dwell = AbyssConst.LIFT_DWELL;
                _arrival = (float)(rideSeconds - AbyssConst.LIFT_DURATION);
                UpdateArrivalVisuals();
            }
            ApplyTransform();
            Publish();
        }
    }
}
