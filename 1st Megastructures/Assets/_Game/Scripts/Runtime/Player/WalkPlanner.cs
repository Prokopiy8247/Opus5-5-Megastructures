using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Pure, deterministic speed and stop plan of the AutoWalker along the walked curve (no Unity objects, so EditMode tests
    /// can simulate a whole walk). The planner advances a walked distance S (PathGuide.WalkS) and its path distance D:
    ///
    /// - Autotest / plain autowalk: constant 1.5 m/s with short smooth starts and stops (1.0 s / 1.5 s), so -autotest
    ///   finishes at about 659 s (658 +/- 8) and the ride starts at about 308.4 s (+/- 1).
    /// - Cinematic: the speed eases slower at the reveals (six zones whose depth is solved so their slowdowns total
    ///   <see cref="CINEMATIC_SLOWDOWN_TOTAL"/> s together with beat 7's 1.1 m/s over the mouth and the first metres of the
    ///   balcony), then the 14 s hold H7 at z = -12 (14.5.7); starts and stops are gentler (2.0 s / 2.5 s).
    ///
    /// Stops: at the lift centre (W12) until the gate opens after the ride, at H7 in cinematic mode, and 3.2 m before the
    /// goal door (z -70.8, lead decision D2; <see cref="ExtendEnd"/> walks on past the door trigger if the goal did not fire
    /// there). A stop is an analytic smoothstep deceleration that ends exactly on the stop point, independent of the frame
    /// rate; a start is a smoothstep ramp of the speed. While aboard, d stays at D_LIFT and <see cref="DeckStand"/> gives the
    /// walk to the -Z rail and back inside the ride (R1-04 / R1-05).
    /// </summary>
    public sealed class WalkPlanner
    {
        public enum Stage : byte { Idle = 0, ToLift = 1, OnLift = 2, ToEnd = 3, Done = 4 }

        public const float START_TIME_AUTOTEST = 1.0f;
        public const float STOP_TIME_AUTOTEST = 1.5f;
        public const float START_TIME_CINEMATIC = 2.0f;
        public const float STOP_TIME_CINEMATIC = 2.5f;
        /// <summary>The walk never goes nearer the door than this (z = -73: the old stop, past the 14.11 trigger at z = -72.8).</summary>
        public const float END_MARGIN = 1.0f;
        /// <summary>
        /// Lead decision D2 (R2-05): the walk decelerates and stops at z -70.8, 3.2 m from the door plane (z -74), facing the
        /// door at pitch about -4 (GazeCues.END_PITCH), so the 12 degree leaf opening, the amber backing, the door spot and the
        /// sliver on the pier are framed. D2's door trigger is 3.0 m (feet z &lt;= -71.0); Beat07 also accepts a walker that has
        /// finished within 0.6 m of that line (Beat07_Reveal.WALKER_STOP_SLACK), which is how the stop 0.2 m short of it opens
        /// the door.
        /// </summary>
        public const float END_STOP_Z = -70.8f;
        /// <summary>D2's door trigger line (3.0 m from the door plane z -74).</summary>
        public const float D2_TRIGGER_Z = -71.0f;
        /// <summary>
        /// When the goal has not fired this long after a stop, the walker walks on: first to <see cref="GOAL_FALLBACK_PAST"/>
        /// past D2's trigger line (<see cref="GoalFallbackNearD"/>), then past AbyssConst.DOOR_TRIGGER_Z (<see cref="GoalFallbackD"/>,
        /// the spec's 1.2 m), so the ending always plays.
        /// </summary>
        public const float GOAL_FALLBACK_WAIT = 0.5f, GOAL_FALLBACK_PAST = 0.2f;
        public const float CINEMATIC_SLOWDOWN_TOTAL = 12f;
        public const float BEAT7_SLOW_SPEED = 1.1f;

        struct Zone
        {
            public float A, B, C, E, Depth;   // ramp in A..B, plateau B..C, ramp out C..E (d), relative depth
            public Zone(float a, float b, float c, float e, float depth) { A = a; B = b; C = c; E = e; Depth = depth; }
        }

        /// <summary>Cinematic reveal zones (d): doorway and bridge start, the look straight down (KS2b), the column, the hall
        /// portal, the hall exit into the shaft, the fan.</summary>
        static readonly Zone[] Zones =
        {
            new Zone(54f, 60f, 74f, 80f, 1.0f),
            new Zone(126f, 132f, 146f, 152f, 1.0f),
            new Zone(220f, 225f, 238f, 243f, 0.8f),
            new Zone(320f, 326f, 338f, 344f, 0.8f),
            new Zone(428f, 433f, 441f, 446f, 0.8f),
            new Zone(484f, 490f, 512f, 518f, 1.0f),
        };

        static float _slowK = -1f;
        static float _fixedExtra, _totalExtra;

        // ---------------------------------------------------------------- state
        public Stage Mode { get; private set; }
        public bool Cinematic { get; private set; }
        /// <summary>Path distance (polyline parametrisation).</summary>
        public float D { get; private set; }
        /// <summary>Walked metres (PathGuide.WalkS).</summary>
        public float S { get; private set; }
        /// <summary>Speed along the walked curve (m/s).</summary>
        public float Speed { get; private set; }
        public bool AfterRide { get; private set; }
        public bool Holding { get; private set; }
        public float HoldElapsed { get; private set; }
        public bool HoldDone { get; private set; }

        /// <summary>Planned end of the walk (z = <see cref="END_STOP_Z"/>), never nearer the door than END_MARGIN.</summary>
        public static float EndD => Mathf.Min(PathGuide.Beat7D(END_STOP_Z), PathGuide.EndD - END_MARGIN);

        /// <summary>First fallback stop when the goal did not fire at the planned stop: just past D2's trigger line.</summary>
        public static float GoalFallbackNearD =>
            Mathf.Min(PathGuide.Beat7D(D2_TRIGGER_Z - GOAL_FALLBACK_PAST), PathGuide.EndD - END_MARGIN);

        /// <summary>Last fallback stop: just past AbyssConst.DOOR_TRIGGER_Z, never nearer the door than END_MARGIN.</summary>
        public static float GoalFallbackD =>
            Mathf.Min(PathGuide.Beat7D(AbyssConst.DOOR_TRIGGER_Z - GOAL_FALLBACK_PAST), PathGuide.EndD - END_MARGIN);

        /// <summary>The stop the walk is heading for now (EndD, or GoalFallbackD after <see cref="ExtendEnd"/>).</summary>
        public float StopD => _endD;

        float _endD = -1f;
        float _startTime = START_TIME_AUTOTEST, _stopTime = STOP_TIME_AUTOTEST;
        bool _ramping; float _rampT, _rampFrom;
        bool _stopping; float _stopT, _stopDur, _stopV0, _stopS0;

        // ---------------------------------------------------------------- speed profile

        static float ZoneWeight(float d, float a, float b, float c, float e)
        {
            if (d <= a || d >= e) return 0f;
            if (d < b) return GazeCues.SmoothStep01((d - a) / (b - a));
            if (d <= c) return 1f;
            return 1f - GazeCues.SmoothStep01((d - c) / (e - c));
        }

        static float Beat7Factor(float d)
        {
            float w = ZoneWeight(d, PathGuide.Beat7D(5f), PathGuide.Beat7D(3f), PathGuide.Beat7D(GazeCues.H7_Z), PathGuide.Beat7D(GazeCues.H7_Z - 2f));
            return 1f - w * (1f - BEAT7_SLOW_SPEED / AbyssConst.V_WALK);
        }

        static float ZonesFactor(float d, float k)
        {
            float f = 1f;
            for (int i = 0; i < Zones.Length; i++)
            {
                ref readonly Zone z = ref Zones[i];
                if (d <= z.A || d >= z.E) continue;
                f *= 1f - k * z.Depth * ZoneWeight(d, z.A, z.B, z.C, z.E);
            }
            return f;
        }

        /// <summary>Target speed at d (m/s) without starts and stops.</summary>
        public static float ProfileSpeed(float d, bool cinematic)
        {
            if (!cinematic) return AbyssConst.V_WALK;
            EnsureSlowK();
            return AbyssConst.V_WALK * ZonesFactor(d, _slowK) * Beat7Factor(d);
        }

        /// <summary>Solved depth factor of the cinematic zones (speed at a full-depth plateau = 1.5 * (1 - k)).</summary>
        public static float SlowK { get { EnsureSlowK(); return _slowK; } }

        /// <summary>Total extra seconds of all cinematic slowdowns versus 1.5 m/s (target CINEMATIC_SLOWDOWN_TOTAL).</summary>
        public static float CinematicExtraSeconds { get { EnsureSlowK(); return _totalExtra; } }

        /// <summary>
        /// Extra seconds versus 1.5 m/s over d in [a, e]: of the beat-7 factor when k &lt; 0, else of the reveal zones at depth k.
        /// (No delegates: the solver allocates nothing.)
        /// </summary>
        static double ExtraTime(float k, float a, float e)
        {
            const float step = 0.02f;
            double sum = 0.0;
            for (float d = a + step * 0.5f; d < e; d += step)
            {
                float factor = k < 0f ? Beat7Factor(d) : ZonesFactor(d, k);
                sum += (1.0 / (AbyssConst.V_WALK * factor) - 1.0 / AbyssConst.V_WALK) * step;
            }
            return sum;
        }

        /// <summary>Solve the cinematic slowdown depth now (AutoWalker.Awake, during the load) instead of at the first cinematic Begin.</summary>
        public static void Prepare() => EnsureSlowK();

        static void EnsureSlowK()
        {
            if (_slowK >= 0f) return;
            _fixedExtra = (float)ExtraTime(-1f, PathGuide.Beat7D(5f), PathGuide.Beat7D(GazeCues.H7_Z - 2f));
            float target = CINEMATIC_SLOWDOWN_TOTAL - _fixedExtra;
            float lo = 0f, hi = 0.6f;
            for (int it = 0; it < 40; it++)
            {
                float mid = 0.5f * (lo + hi);
                double extra = 0.0;
                for (int i = 0; i < Zones.Length; i++) extra += ExtraTime(mid, Zones[i].A, Zones[i].E);
                if (extra < target) lo = mid; else hi = mid;
            }
            float slowK = 0.5f * (lo + hi);
            double total = _fixedExtra;
            for (int i = 0; i < Zones.Length; i++) total += ExtraTime(slowK, Zones[i].A, Zones[i].E);
            _totalExtra = (float)total;
            _slowK = slowK;   // published last: ProfileSpeed never sees a half-solved state
        }

        // ---------------------------------------------------------------- control

        /// <summary>
        /// Start from d0. rideDone: the ride is over (lower level); onLift: the player is aboard during the dwell or ride.
        /// v0 is the current speed (the start ramp begins from it).
        /// </summary>
        public void Begin(float d0, bool cinematic, bool rideDone, bool onLift, float v0)
        {
            Cinematic = cinematic;
            _startTime = cinematic ? START_TIME_CINEMATIC : START_TIME_AUTOTEST;
            _stopTime = cinematic ? STOP_TIME_CINEMATIC : STOP_TIME_AUTOTEST;
            if (cinematic) EnsureSlowK();
            D = Mathf.Clamp(d0, 0f, PathGuide.EndD);
            _endD = EndD;
            Holding = false;
            HoldElapsed = 0f;
            HoldDone = !cinematic || D > GazeCues.H7D - 0.25f;
            _stopping = false;

            if (onLift)
            {
                Mode = Stage.OnLift;
                AfterRide = false;
                D = AbyssConst.D_LIFT;
            }
            else if (!rideDone)
            {
                AfterRide = false;
                if (D >= AbyssConst.D_LIFT - 0.05f) { Mode = Stage.OnLift; D = AbyssConst.D_LIFT; }
                else Mode = Stage.ToLift;
            }
            else
            {
                AfterRide = true;
                D = Mathf.Max(D, AbyssConst.D_LIFT);
                Mode = D >= _endD - 0.01f ? Stage.Done : Stage.ToEnd;
            }
            S = PathGuide.WalkS(D);
            Speed = Mode == Stage.ToLift || Mode == Stage.ToEnd ? Mathf.Max(0f, v0) : 0f;
            StartRamp(Speed);
        }

        void StartRamp(float v0)
        {
            float vp = ProfileSpeed(D, Cinematic);
            _ramping = true;
            _rampT = 0f;
            _rampFrom = vp > 1e-4f ? Mathf.Clamp01(v0 / vp) : 0f;
        }

        /// <summary>Advance one frame. canLeaveLift: the gate has started to open after the ride.</summary>
        public void Step(float dt, bool canLeaveLift)
        {
            if (dt <= 0f) return;
            switch (Mode)
            {
                case Stage.ToLift:
                    if (AdvanceTo(PathGuide.WalkS(AbyssConst.D_LIFT), dt))
                    {
                        Mode = Stage.OnLift;
                        D = AbyssConst.D_LIFT;
                    }
                    break;
                case Stage.OnLift:
                    Speed = 0f;
                    if (canLeaveLift)
                    {
                        Mode = Stage.ToEnd;
                        AfterRide = true;
                        StartRamp(0f);
                    }
                    break;
                case Stage.ToEnd:
                {
                    if (Holding)
                    {
                        Speed = 0f;
                        HoldElapsed += dt;
                        if (HoldElapsed >= GazeCues.H7_DURATION)
                        {
                            HoldElapsed = GazeCues.H7_DURATION;
                            Holding = false;
                            HoldDone = true;
                            StartRamp(0f);
                        }
                        break;
                    }
                    bool toHold = Cinematic && !HoldDone;
                    float stopD = toHold ? GazeCues.H7D : _endD;
                    if (AdvanceTo(PathGuide.WalkS(stopD), dt))
                    {
                        if (toHold) { Holding = true; HoldElapsed = 0f; }
                        else Mode = Stage.Done;
                    }
                    break;
                }
                default:
                    Speed = 0f;
                    break;
            }
        }

        /// <summary>
        /// Walk on from a finished walk to a stop further along (the goal did not fire at the planned stop). Returns false when
        /// the walk is not finished or the new stop is not beyond the current one.
        /// </summary>
        public bool ExtendEnd(float stopD)
        {
            stopD = Mathf.Min(stopD, PathGuide.EndD);
            if (Mode != Stage.Done || !AfterRide || stopD <= Mathf.Max(_endD, D) + 0.01f) return false;
            _endD = stopD;
            Mode = Stage.ToEnd;
            HoldDone = true;
            StartRamp(0f);
            return true;
        }

        bool AdvanceTo(float sStop, float dt)
        {
            if (_stopping)
            {
                _stopT += dt;
                float u = Mathf.Min(_stopT / _stopDur, 1f);
                // distance of v0 (1 - smoothstep(t / T)) integrated: v0 T (u - u^3 + u^4 / 2)
                S = _stopS0 + _stopV0 * _stopDur * (u - u * u * u + 0.5f * u * u * u * u);
                Speed = _stopV0 * (1f - GazeCues.SmoothStep01(u));
                if (u >= 1f)
                {
                    S = sStop;
                    Speed = 0f;
                    _stopping = false;
                    D = PathGuide.WalkDAtS(S);
                    return true;
                }
                D = PathGuide.WalkDAtS(S);
                return false;
            }

            float ramp = 1f;
            if (_ramping)
            {
                _rampT += dt;
                float u = Mathf.Min(_rampT / _startTime, 1f);
                ramp = _rampFrom + (1f - _rampFrom) * GazeCues.SmoothStep01(u);
                if (u >= 1f) _ramping = false;
            }
            float v = ProfileSpeed(D, Cinematic) * ramp;
            float rem = sStop - S;
            if (rem <= 1e-5f)
            {
                S = sStop; Speed = 0f; _ramping = false;
                D = PathGuide.WalkDAtS(S);
                return true;
            }
            if (rem <= v * _stopTime * 0.5f)
            {
                _stopping = true;
                _stopT = 0f;
                _stopV0 = Mathf.Max(v, 0.05f);
                _stopDur = 2f * rem / _stopV0;
                _stopS0 = S;
                _ramping = false;
                return AdvanceTo(sStop, dt);
            }
            S += v * dt;
            Speed = v;
            D = PathGuide.WalkDAtS(S);
            return false;
        }

        // ---------------------------------------------------------------- ride choreography on the deck (R1-04 / R1-05)
        //
        // Arbitration of 14.5.5 Path ("the AutoWalker stands still at the centre for the whole ride"; logged by the lead in
        // DEVELOPMENT.md): from the deck centre the -Z deck edge sits 9.6 degrees below the horizon, so the fan hold (75-90)
        // loses the lower half of the blade ring behind the deck and the -55 look "down into the shaft" (95-101) sees only
        // deck. The walker therefore walks to the open front during the 65-75 cue, stands at the -Z rail (platform-local
        // z -8.6, world z 260.4 at the bottom dock, inside the 18 x 18 walk clamp of +/-8.65, 1.3 m behind the rail) through the
        // fan hold and the look down (KS5b at 75 s, KS5c at 100 s are captured there), and walks back to the centre while the
        // lift settles into the bottom dock, before the gate opens at 121 s. Dwell, ride and gate timing are untouched (the
        // walks happen inside the ride), so -autotest still starts the ride at 308.4 s and finishes at about 659 s.

        /// <summary>Platform-local z of the stand at the -Z rail.</summary>
        public const float RAIL_STAND_Z = -8.6f;
        /// <summary>Ride seconds of the walk out to the rail (eased, at most <see cref="RAIL_MAX_SPEED"/>).</summary>
        public const float RAIL_OUT_T0 = 64f, RAIL_OUT_T1 = 75f;
        /// <summary>Ride seconds of the walk back to the centre (ends before the gate opens at LIFT_DURATION + LIFT_GATE_DELAY).</summary>
        public const float RAIL_BACK_T0 = 109.5f, RAIL_BACK_T1 = 120.5f;
        /// <summary>Smoothstep speed ramp at both ends of each deck walk (s).</summary>
        public const float RAIL_RAMP = 2f;
        public const float RAIL_MAX_SPEED = 1f;
        /// <summary>A walker begun aboard later than this ride second holds the centre (no time left for the rail walks).</summary>
        public const float RAIL_PLAN_LATEST_BEGIN = 98f;

        /// <summary>
        /// Distance covered at time tau of an eased move of length dist over [0, T]: smoothstep speed ramps of length r at both
        /// ends around a constant cruise (C1 position, C1 speed). Outputs the speed.
        /// </summary>
        public static float EasedMove(float tau, float T, float r, float dist, out float speed)
        {
            float vmax = dist / (T - r);
            if (tau <= 0f) { speed = 0f; return 0f; }
            if (tau >= T) { speed = 0f; return dist; }
            if (tau < r)
            {
                float x = tau / r;
                speed = vmax * GazeCues.SmoothStep01(x);
                return vmax * r * (x * x * x - 0.5f * x * x * x * x);
            }
            if (tau > T - r)
            {
                float x = (T - tau) / r;
                speed = vmax * GazeCues.SmoothStep01(x);
                return dist - vmax * r * (x * x * x - 0.5f * x * x * x * x);
            }
            speed = vmax;
            return vmax * (0.5f * r + (tau - r));
        }

        /// <summary>
        /// Where the walker stands on the deck at ride second t (platform-local x, z; dwell -2..0, ride 0..120, arrival after)
        /// and the planned velocity (feed-forward for the steering).
        /// </summary>
        public static void DeckStand(float t, out Vector2 pos, out Vector2 vel)
        {
            float dist = -RAIL_STAND_Z;
            float s, v;
            if (t < RAIL_BACK_T0)
            {
                s = EasedMove(t - RAIL_OUT_T0, RAIL_OUT_T1 - RAIL_OUT_T0, RAIL_RAMP, dist, out v);
                pos = new Vector2(0f, -s);
                vel = new Vector2(0f, -v);
            }
            else
            {
                s = EasedMove(t - RAIL_BACK_T0, RAIL_BACK_T1 - RAIL_BACK_T0, RAIL_RAMP, dist, out v);
                pos = new Vector2(0f, RAIL_STAND_Z + s);
                vel = new Vector2(0f, v);
            }
        }

        // ---------------------------------------------------------------- offline simulation (tests)

        /// <summary>
        /// Simulate a whole walk from d = 0 with the nominal lift (dwell 2 s from the stop at the centre, ride 120 s, gate 1 s).
        /// Returns the total time; outputs the ride start and end times.
        /// </summary>
        public static double SimulateDuration(bool cinematic, float dt, out double rideStart, out double rideEnd)
        {
            var p = new WalkPlanner();
            p.Begin(0f, cinematic, false, false, 0f);
            double t = 0.0, dwellStart = -1.0;
            rideStart = -1.0;
            double gate = AbyssConst.LIFT_DWELL + AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY;
            while (p.Mode != Stage.Done && t < 3000.0)
            {
                bool canLeave = dwellStart >= 0.0 && t >= dwellStart + gate;
                p.Step(dt, canLeave);
                t += dt;
                if (p.Mode == Stage.OnLift && dwellStart < 0.0)
                {
                    dwellStart = t;
                    rideStart = t + AbyssConst.LIFT_DWELL;
                }
            }
            rideEnd = rideStart + AbyssConst.LIFT_DURATION;
            return t;
        }
    }
}
