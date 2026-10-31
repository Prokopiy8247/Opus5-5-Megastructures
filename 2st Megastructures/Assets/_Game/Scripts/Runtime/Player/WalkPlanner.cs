using UnityEngine;

namespace Megastructure
{
    /// <summary>Walk modes of the AutoWalker.</summary>
    public enum WalkMode { None = 0, AutoTest = 1, Cinematic = 2 }

    /// <summary>
    /// Pure walk logic of the AutoWalker (no Unity objects, allocation-free; usable from EditMode tests):
    /// path distance and speed profile (smootherstep acceleration, braking into the holds with a smootherstep velocity
    /// profile whose integral lands exactly on the hold point), the 8 cinematic holds, glances and releases of the
    /// GazeSchedule, the critically damped look follower (omega 3 rad/s), the deterministic handheld sway (&lt;= 0.3 deg)
    /// and the capsule lateral offset. The owner feeds the player's position each step and moves the player to
    /// <see cref="Target"/>; the planner waits when the player lags by more than 0.6 m (blocked, e.g. by the hatch).
    /// </summary>
    public sealed class WalkPlanner
    {
        public readonly FilletPath Path;

        // tuning (local choices; spec numbers live in RingConstants)
        public const float LookOmega = 3f;               // 14.5: gaze follower critically damped, omega 3 rad/s
        public const float SwayMax = 0.3f;               // 14.5: handheld sway <= 0.3 degrees
        const float SwayAmp = 0.2f;                      // per axis before the 0.3 clamp
        public const float QaAccelTime = RingConstants.Accel;   // 0.2 s
        public const float CineAccelTime = 1.5f;         // gentle start after a hold
        public const float BrakeTime = 2.0f;             // nominal braking time into a hold (distance = v T / 2)
        public const float MaxLag = 0.6f;                // wait when the player lags the target by more than this
        const float LookAhead = 1.5f;                    // heading look-ahead along the path (m)
        const float LatDecay = 1.5f;                     // s, decay of the starting lateral offset

        enum Phase { Accel, Cruise, Brake, Hold, Done }

        public WalkMode Mode { get; private set; }
        /// <summary>Path distance (m along the filleted path) and speed (m/s).</summary>
        public float D => _d;
        public float Speed => _v;
        /// <summary>Sharp progress s of the walker position.</summary>
        public float SharpS => Path.SharpS(_d);
        /// <summary>Where the player's feet should be this frame (x, z).</summary>
        public Vector2 Target { get; private set; }
        /// <summary>Camera look output (degrees).</summary>
        public float OutYaw { get; private set; }
        public float OutPitch { get; private set; }
        public bool Holding => _phase == Phase.Hold;
        public bool Finished => _phase == Phase.Done;
        /// <summary>Index of the hold being played (-1 none).</summary>
        public int HoldIndex => _phase == Phase.Hold ? _holdIdx : -1;

        Phase _phase = Phase.Done;
        float _d, _v, _vCruise;
        float _tau, _phaseT, _dStart, _vStart, _d0, _dTarget;
        float _latResid;
        int _holdIdx = -1;
        readonly bool[] _holdDone = new bool[16];
        readonly bool[] _glanceDone = new bool[8];
        int _glanceIdx = -1; float _glanceTau;
        bool _releasing; float _relT, _relDur, _relYaw, _relPitch;
        float _startYaw, _startPitch;
        float _evalFloorS = float.NegativeInfinity;
        float _yaw, _yawV, _pitch, _pitchV;
        readonly float _seedX, _seedY;

        public WalkPlanner(FilletPath path, int seed)
        {
            Path = path;
            uint h = RingMath.Pcg((uint)seed);
            _seedX = 17.3f + 97f * RingMath.U01(h);
            _seedY = 51.9f + 97f * RingMath.U01(RingMath.Pcg(h + 1u));
        }

        /// <summary>Starts a walk from the player's current position (xz), current progress hint, velocity and look.</summary>
        public void Begin(WalkMode mode, Vector2 playerXZ, float hintS, Vector2 playerVelXZ, float yaw, float pitch, float cruise)
        {
            Mode = mode;
            _d = Path.Project(playerXZ, Path.DAtSharp(hintS), 25f, out float dist);
            if (dist > 10f) _d = Path.Project(playerXZ, 0f, -1f, out dist);
            float s = Path.SharpS(_d);
            _latResid = Path.Lateral(playerXZ, _d) - (mode == WalkMode.Cinematic ? GazeSchedule.LateralOffset(s) : 0f);
            _vCruise = Mathf.Clamp(cruise, 0.1f, 5f);
            Vector2 tng = Path.TangentAt(_d);
            float v0 = Mathf.Clamp(Vector2.Dot(playerVelXZ, tng), 0f, _vCruise);
            StartAccel(v0, mode == WalkMode.AutoTest ? QaAccelTime : CineAccelTime);

            var holds = GazeSchedule.Holds;
            for (int i = 0; i < _holdDone.Length; i++) _holdDone[i] = i >= holds.Length || mode != WalkMode.Cinematic || holds[i].S < s - 0.02f;
            var gl = GazeSchedule.Glances;
            for (int i = 0; i < _glanceDone.Length; i++) _glanceDone[i] = i >= gl.Length || mode != WalkMode.Cinematic || gl[i].S < s;
            _holdIdx = -1; _glanceIdx = -1; _releasing = false;
            _evalFloorS = float.NegativeInfinity;
            _yaw = yaw; _pitch = pitch; _yawV = 0f; _pitchV = 0f;
            OutYaw = yaw; OutPitch = pitch;
            Target = TargetPoint();
        }

        public void Stop() { Mode = WalkMode.None; _phase = Phase.Done; }

        /// <summary>One frame. playerXZ = where the player is now; t = game time (sway).</summary>
        public void Step(float dt, Vector2 playerXZ, double t)
        {
            if (Mode == WalkMode.None || dt <= 0f) return;
            bool blocked = (TargetPoint() - playerXZ).magnitude > MaxLag;
            if (!blocked) Advance(dt);
            if (_latResid != 0f && _v > 0.01f) _latResid *= Mathf.Exp(-dt / LatDecay);
            Target = TargetPoint();
            Look(dt, Path.SharpS(_d), t);
        }

        Vector2 TargetPoint()
        {
            float s = Path.SharpS(_d);
            float lat = (Mode == WalkMode.Cinematic ? GazeSchedule.LateralOffset(s) : 0f) + _latResid;
            return Path.PointAt(_d) + Path.RightAt(_d) * lat;
        }

        // ---------------- speed profile ----------------
        static float Ss(float x) => RingMath.Smootherstep01(x);
        /// <summary>Integral of smootherstep from 0 to x (x in 0..1): x^6 - 3x^5 + 2.5x^4.</summary>
        static float SsInt(float x) { float x2 = x * x, x4 = x2 * x2; return x4 * x2 - 3f * x4 * x + 2.5f * x4; }

        void StartAccel(float vStart, float time)
        {
            _phase = Phase.Accel; _tau = 0f; _phaseT = Mathf.Max(0.05f, time); _dStart = _d; _vStart = vStart; _v = vStart;
        }

        void StartBrake(float remaining, float dTarget)
        {
            _phase = Phase.Brake; _tau = 0f; _dStart = _d; _d0 = Mathf.Max(0f, remaining); _dTarget = dTarget;
            _vStart = Mathf.Max(_v, 0.05f);
            _phaseT = Mathf.Max(0.05f, 2f * _d0 / _vStart);
        }

        float NextStopD(out int holdIdx)
        {
            holdIdx = -1;
            if (Mode == WalkMode.Cinematic)
            {
                var holds = GazeSchedule.Holds;
                for (int i = 0; i < holds.Length; i++)
                    if (!_holdDone[i]) { holdIdx = i; return Mathf.Min(Path.DAtSharp(holds[i].S), Path.Length); }
            }
            return Path.Length;
        }

        void Advance(float dt)
        {
            switch (_phase)
            {
                case Phase.Accel:
                {
                    _tau += dt;
                    float x = Mathf.Min(1f, _tau / _phaseT);
                    _d = _dStart + _phaseT * (_vStart * x + (_vCruise - _vStart) * SsInt(x));
                    _v = _vStart + (_vCruise - _vStart) * Ss(x);
                    if (x >= 1f) _phase = Phase.Cruise;
                    break;
                }
                case Phase.Cruise:
                    _v = _vCruise;
                    _d += _v * dt;
                    break;
                case Phase.Brake:
                {
                    _tau += dt;
                    float x = Mathf.Min(1f, _tau / _phaseT);
                    _d = _dStart + _d0 * 2f * (x - SsInt(x));
                    _v = _vStart * (1f - Ss(x));
                    if (x >= 1f)
                    {
                        _d = _dTarget; _v = 0f;
                        if (_holdIdx >= 0) BeginHold(); else _phase = Phase.Done;
                    }
                    return;
                }
                case Phase.Hold:
                {
                    _tau += dt;
                    if (_tau >= GazeSchedule.Holds[_holdIdx].Duration) EndHold();
                    return;
                }
                default:
                    _v = 0f;
                    return;
            }
            // braking trigger (accel or cruise): braking distance = v * BrakeTime / 2
            float dStop = NextStopD(out int idx);
            float rem = dStop - _d;
            if (rem <= Mathf.Max(0.02f, _v * 0.5f * BrakeTime))
            {
                _holdIdx = idx;
                if (rem <= 0.02f || (_v < 0.02f && rem < 0.05f))
                {
                    _d = dStop; _v = 0f;
                    if (_holdIdx >= 0) BeginHold(); else _phase = Phase.Done;
                }
                else StartBrake(rem, dStop);
            }
        }

        void BeginHold()
        {
            _phase = Phase.Hold; _tau = 0f;
            var h = GazeSchedule.Holds[_holdIdx];
            GazeSchedule.Evaluate(h.S, Path.HeadingAt(Mathf.Min(_d + LookAhead, Path.Length)), out _startYaw, out _startPitch);
            _releasing = false;
            _glanceIdx = -1;
        }

        void EndHold()
        {
            var h = GazeSchedule.Holds[_holdIdx];
            _holdDone[_holdIdx] = true;
            _evalFloorS = h.S + 0.06f;   // read the keys just past the hold point (hold keys come in pairs s, s + 0.05)
            GazeSchedule.StepPose(h, h.Duration, _startYaw, _startPitch, _startYaw, _startPitch, out _relYaw, out _relPitch);
            BeginRelease(h.Release);
            _holdIdx = -1;
            if (_d >= Path.Length - 1e-3f) { _phase = Phase.Done; return; }
            StartAccel(0f, CineAccelTime);
        }

        void BeginRelease(float dur)
        {
            _releasing = dur > 0f; _relT = 0f; _relDur = dur;
        }

        // ---------------- gaze ----------------
        void Look(float dt, float s, double t)
        {
            float heading = Path.HeadingAt(Mathf.Min(_d + LookAhead, Path.Length));
            float ty, tp;
            if (Mode == WalkMode.AutoTest)
            {
                ty = heading; tp = 0f;   // QA: forward along the path, pitch 0
            }
            else
            {
                GazeSchedule.Evaluate(Mathf.Max(s, _evalFloorS), heading, out float ky, out float kp);
                ty = ky; tp = kp;
                if (_phase == Phase.Hold && _holdIdx >= 0)
                {
                    GazeSchedule.StepPose(GazeSchedule.Holds[_holdIdx], _tau, _startYaw, _startPitch, ky, kp, out ty, out tp);
                }
                else
                {
                    var gl = GazeSchedule.Glances;
                    if (_glanceIdx < 0)
                        for (int i = 0; i < gl.Length; i++)
                            if (!_glanceDone[i] && s >= gl[i].S) { _glanceIdx = i; _glanceTau = 0f; _startYaw = _yaw; _startPitch = _pitch; _releasing = false; break; }
                    if (_glanceIdx >= 0)
                    {
                        var g = gl[_glanceIdx];
                        _glanceTau += dt;
                        GazeSchedule.StepPose(g, _glanceTau, _startYaw, _startPitch, ky, kp, out ty, out tp);
                        if (_glanceTau >= g.Duration)
                        {
                            _glanceDone[_glanceIdx] = true;
                            _relYaw = ty; _relPitch = tp;
                            BeginRelease(g.Release);
                            _glanceIdx = -1;
                        }
                    }
                    else if (_releasing)
                    {
                        _relT += dt;
                        float u = RingMath.Smootherstep01(_relT / Mathf.Max(1e-3f, _relDur));
                        ty = _relYaw + Mathf.DeltaAngle(_relYaw, ky) * u;
                        tp = Mathf.Lerp(_relPitch, kp, u);
                        if (_relT >= _relDur) _releasing = false;
                    }
                }
            }

            _yaw = RingMath.CriticalDampAngle(_yaw, ref _yawV, ty, LookOmega, dt);
            _pitch = RingMath.CriticalDamp(_pitch, ref _pitchV, tp, LookOmega, dt);
            if (_yaw > 360f || _yaw < -360f) _yaw = Mathf.Repeat(_yaw + 180f, 360f) - 180f;

            float oy = _yaw, op = _pitch;
            if (Mode == WalkMode.Cinematic)
            {
                // deterministic handheld sway: 3 octaves of value noise of the game time, seeded
                float tt = (float)(t % 3600.0);
                Vector2 sw = new Vector2(Octaves(tt, _seedX), Octaves(tt, _seedY)) * SwayAmp;
                if (sw.sqrMagnitude > SwayMax * SwayMax) sw = sw.normalized * SwayMax;
                oy += sw.x; op += sw.y;
            }
            OutYaw = oy; OutPitch = op;
        }

        static float Octaves(float t, float seed)
        {
            // smooth random in [-1, 1]: about 0.35, 0.8 and 1.7 Hz, weights 1, 0.5, 0.25
            float n = (RingMath.ValueNoise(t * 0.35f, seed) * 2f - 1f)
                    + 0.5f * (RingMath.ValueNoise(t * 0.8f, seed + 31.7f) * 2f - 1f)
                    + 0.25f * (RingMath.ValueNoise(t * 1.7f, seed + 63.1f) * 2f - 1f);
            return n / 1.75f;
        }
    }
}
