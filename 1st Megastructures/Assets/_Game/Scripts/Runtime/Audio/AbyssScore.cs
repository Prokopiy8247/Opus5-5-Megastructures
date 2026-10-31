using System;
using System.Collections.Generic;

namespace Megastructure.Audio
{
    /// <summary>Everything the score needs to know about the walk at one instant (filled from GameState or the nominal walk).</summary>
    public struct ScoreInput
    {
        /// <summary>Path distance d (frozen at D_LIFT during dwell and ride).</summary>
        public float D;
        /// <summary>Ride seconds: -1 before departure, 0..120 during the ride, keeps counting afterwards.</summary>
        public float RideSeconds;
        public bool RideStarted;
        /// <summary>Seconds since the 2 s lift dwell began (-1 when not dwelling).</summary>
        public float DwellSeconds;
        public double SceneTime;
        public bool ClockRunning;
        public double RealTime;
        /// <summary>True once the walk clock has begun (ClockDriver.Running); RealTime is the walk's real time from then on.</summary>
        public bool WalkStarted;
        /// <summary>GameState.PictureFade (title fade-in / ending fade-out), 0..1.</summary>
        public float PictureFade;
        public bool Paused;
        public bool GoalReached;
        public double GoalRealTime;
        /// <summary>Cinematic hold H7 weight 0..1 (smoothed by the director): WIND +4 dB.</summary>
        public float HoldH7;
        /// <summary>Measurement only: ignore the per-beat master trims (used to measure T_b).</summary>
        public bool DisableTrim;
    }

    /// <summary>
    /// A level curve over the extended progress E: points (E_i, dB_i) joined by smootherstep segments. Segments between two
    /// audible levels interpolate in dB; segments touching "off" (-80 dB) interpolate in linear amplitude, so fades to and
    /// from silence are heard over their whole length.
    /// </summary>
    public sealed class ScoreTrack
    {
        readonly float[] _e, _v;

        public ScoreTrack(params float[] pairs)
        {
            int n = pairs.Length / 2;
            _e = new float[n]; _v = new float[n];
            for (int i = 0; i < n; i++) { _e[i] = pairs[2 * i]; _v[i] = pairs[2 * i + 1]; }
        }

        int Segment(float e)
        {
            int i = 0;
            while (i < _e.Length - 2 && e >= _e[i + 1]) i++;
            return i;
        }

        /// <summary>Linear gain at E.</summary>
        public float Gain(float e)
        {
            int n = _e.Length;
            if (e <= _e[0]) return DspMath.DbToLin(_v[0]);
            if (e >= _e[n - 1]) return DspMath.DbToLin(_v[n - 1]);
            int i = Segment(e);
            float e0 = _e[i], e1 = _e[i + 1], v0 = _v[i], v1 = _v[i + 1];
            double s = DspMath.Smootherstep(e1 > e0 ? (e - e0) / (e1 - e0) : 1.0);
            if (v0 <= AudioMix.Off || v1 <= AudioMix.Off)
            {
                float g0 = DspMath.DbToLin(v0), g1 = DspMath.DbToLin(v1);
                return (float)(g0 + (g1 - g0) * s);
            }
            return DspMath.DbToLin((float)(v0 + (v1 - v0) * s));
        }

        /// <summary>Value at E interpolated in dB (for trims, which are never "off").</summary>
        public float Db(float e)
        {
            int n = _e.Length;
            if (e <= _e[0]) return _v[0];
            if (e >= _e[n - 1]) return _v[n - 1];
            int i = Segment(e);
            float e0 = _e[i], e1 = _e[i + 1];
            double s = DspMath.Smootherstep(e1 > e0 ? (e - e0) / (e1 - e0) : 1.0);
            return (float)(_v[i] + (_v[i + 1] - _v[i]) * s);
        }
    }

    /// <summary>
    /// The Abyss score: a pure function from the walk state to an <see cref="AudioParams"/> snapshot (14.3 layer table,
    /// 14.6 audio transitions, deliberate silences a-d, title and ending), plus the nominal 658 s walk and offline renders.
    ///
    /// All level curves run on the extended progress E (speed independent, 14.6): E = d before the ride,
    /// D_LIFT + ride seconds during dwell and ride, d + 121 after the gate. Transitions that 14.3 states in seconds at
    /// walking speed ("HUM to -60 over 3 s", "cut over 2 s") are laid out as the same distance at 1.5 m/s; the reverb
    /// blends are true time blends (4 s, 5 s at the hall exit) performed by the engine when the preset changes.
    /// </summary>
    public static class AbyssScore
    {
        public const int SampleRate = 48000;
        public const int SnapshotsPerSecond = 60;
        public const double PreRollSeconds = 10.0;
        /// <summary>Zones 1..8 (beats 1-6, 7 tunnel, 7 outside) and 9 = the ending.</summary>
        public const int FirstZone = 1, LastZone = 8, ZoneEnding = 9;

        // ------------------------------------------------------------------ E axis and 14.6 transition windows
        /// <summary>E offset after the ride: d + 121 (120 s ride + 1 s gate delay).</summary>
        public const float Post = AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY;
        const float DLift = AbyssConst.D_LIFT;
        static float Ride(float r) => DLift + r;
        static float After(float d) => d + Post;

        const float Blend12A = 52f, Blend12B = 72f;     // 14.6 row 1 -> 2 (fog window), also SUB / AIR / GROAN / BELL
        const float Reverb12D = 56f;                    // "RT60 0.7 to 7.5 s over 4 s from d 56"
        const float HumDropD = 60f;                     // silence (a): HUM to -60 after d 60 ...
        const float HumDropLen = 3f * AbyssConst.V_WALK; // ... over 3 s at walking speed
        const float HumSilenceADb = -60f;
        const float HumGoneD = 90f;                     // the gallery interior turns off at d 90 (14.6)
        const float Blend23A = 205f, Blend23B = 225f;
        const float Blend34A = 285f, Blend34B = 310f;
        const float ReverbHallD = 326f;                 // the 4 s hall reverb blend ends about at the portal (W9)
        const float HallExitLen = 12f;                  // "over the last 12 m" of the hall
        const float PulseLeaveDb = -30f;                // pulse on the dock apron and the deck, hall behind the player
        const float SilenceBLen = 4f;                   // (b) at departure over 4 s (ride seconds)
        const float FanRideOn = 50f, FanRideA = 60f, FanRideB = 100f;   // FAN -50 -> -34 dB over ride seconds 60-100
        const float HumReturnA = 100f, HumReturnB = 120f;               // HUM back (-34, beat 6) as the fan grows
        const float Ride56A = 110f, Ride56B = 120f;     // 14.6 row 5 -> 6
        const float LiftFadeLen = 3f;                   // LIFT fades out over 3 s from the gate (ride second 121)
        const float MouthCutLen = 2f * AbyssConst.V_WALK; // silence (c): machine layers cut over 2 s at the mouth
        const float MouthBlend = 4f;                    // SUB / trim blend half-width around the mouth (m)
        const float TunnelBellFade = 6f;
        const float WindPreLen = 12.54f;                // wind heard faintly from 12.5 m inside the tunnel
        const float WindPreDb = -46f;
        const float ExitBlend = 4f * AbyssConst.V_WALK; // tunnel exit blends over 4 s at walking speed

        static readonly float MouthD = AbyssConst.BEAT_START_D[6];   // W15 668.54
        static readonly float FaceD = AbyssConst.WD[16];            // W16 728.54 (tunnel exit)
        static readonly float PortalD = AbyssConst.WD[9];           // W9 329.54
        static readonly float HallExitD = AbyssConst.WD[10];        // W10 439.54

        static readonly ScoreTrack SubTrack, AirTrack, TrimTrack, HumTrack, PulseTrack, GroanTrack, GroanDryTrack, BellTrack, FanTrack, WindTrack;

        static AbyssScore()
        {
            SubTrack = ZoneTrack(AudioMix.SubDb);
            AirTrack = ZoneTrack(AudioMix.AirDb);
            TrimTrack = ZoneTrack(AudioMix.MasterTrimDb);
            float off = AudioMix.Off;
            float[] hum = AudioMix.HumDb;
            HumTrack = new ScoreTrack(
                0f, hum[0], HumDropD, hum[0], HumDropD + HumDropLen, HumSilenceADb, AbyssConst.BRIDGE_Z0, HumSilenceADb, HumGoneD, off,
                Blend23A, off, Blend23B, hum[2], Blend34A, hum[2], PortalD, hum[3], HallExitD - HallExitLen, hum[3], HallExitD, hum[4],
                Ride(0f), hum[4], Ride(SilenceBLen), off, Ride(HumReturnA), off, Ride(HumReturnB), hum[5],
                After(MouthD), hum[5], After(MouthD + MouthCutLen), off);
            PulseTrack = new ScoreTrack(
                0f, off, AudioMix.PulseRiseStartD, off, AudioMix.PulseRiseStartD + 0.01f, AudioMix.PulseStartDb, PortalD, AudioMix.PulsePeakDb,
                HallExitD - HallExitLen, AudioMix.PulsePeakDb, DLift, PulseLeaveDb, Ride(SilenceBLen), off);
            GroanTrack = new ScoreTrack(
                0f, off, Reverb12D, off, Blend12B, AudioMix.GroanPeakDb, After(MouthD), AudioMix.GroanPeakDb, After(MouthD + MouthCutLen), off,
                After(FaceD), off, After(FaceD + ExitBlend), AudioMix.GroanFarDb);
            GroanDryTrack = new ScoreTrack(0f, 0f, After(FaceD), 0f, After(FaceD + ExitBlend), AudioMix.GroanFarDryDb);
            BellTrack = new ScoreTrack(
                0f, off, Reverb12D, off, Blend12B, AudioMix.BellPeakDb, After(MouthD), AudioMix.BellPeakDb, After(MouthD + TunnelBellFade), AudioMix.BellFarDb);
            FanTrack = new ScoreTrack(
                0f, off, Ride(FanRideOn), off, Ride(FanRideA), AudioMix.FanRideStartDb, Ride(FanRideB), AudioMix.FanFullDb,
                After(MouthD), AudioMix.FanFullDb, After(MouthD + MouthCutLen), off);
            WindTrack = new ScoreTrack(
                0f, off, After(FaceD - WindPreLen), off, After(FaceD), WindPreDb, After(FaceD + ExitBlend), AudioMix.WindDb);
        }

        /// <summary>Zone plateau values joined across the 14.6 transition windows.</summary>
        static ScoreTrack ZoneTrack(float[] z)
        {
            return new ScoreTrack(
                0f, z[0], Blend12A, z[0], Blend12B, z[1], Blend23A, z[1], Blend23B, z[2], Blend34A, z[2], Blend34B, z[3],
                HallExitD - HallExitLen, z[3], HallExitD, z[4], Ride(Ride56A), z[4], Ride(Ride56B), z[5],
                After(MouthD - MouthBlend), z[5], After(MouthD + MouthBlend), z[6], After(FaceD), z[6], After(FaceD + ExitBlend), z[7]);
        }

        // ------------------------------------------------------------------ state helpers

        /// <summary>Extended progress E (see the class summary).</summary>
        public static float ExtendedProgress(in ScoreInput s)
        {
            if (!s.RideStarted) return s.D;
            if (s.RideSeconds < Post) return DLift + Math.Max(0f, s.RideSeconds);
            return Math.Max(s.D, DLift) + Post;
        }

        /// <summary>Mix zone 1..8 (beats 1-6, 7 tunnel, 7 outside) at extended progress E.</summary>
        public static int Zone(float e)
        {
            float[] b = AbyssConst.BEAT_START_D;
            if (e < b[1]) return 1;
            if (e < b[2]) return 2;
            if (e < b[3]) return 3;
            if (e < b[4]) return 4;
            if (e < After(b[5])) return 5;
            if (e < After(MouthD)) return 6;
            if (e < After(FaceD)) return 7;
            return 8;
        }

        /// <summary>Reverb preset index 0..7 at E: changes at d 56, 216, 326, 439.54, the gate, the mouth and the tunnel exit.</summary>
        public static int ReverbZone(float e)
        {
            if (e < Reverb12D) return 0;
            if (e < AbyssConst.BEAT_START_D[2]) return 1;
            if (e < ReverbHallD) return 2;
            if (e < HallExitD) return 3;
            if (e < Ride(Post)) return 4;
            if (e < After(MouthD)) return 5;
            if (e < After(FaceD)) return 6;
            return 7;
        }

        /// <summary>Distance from the nominal eye to the fan centre (airflow proximity); far before the ride.</summary>
        public static double FanDistance(in ScoreInput s)
        {
            var c = AbyssConst.FAN_CENTRE;
            double x, y, z;
            if (s.RideStarted && s.D <= DLift + 0.01f)
            {
                double u = AbyssConst.LiftS(DspMath.Clamp(s.RideSeconds, 0.0, AbyssConst.LIFT_DURATION)) / AbyssConst.LIFT_DROP;
                var a = AbyssConst.LIFT_TOP; var b = AbyssConst.LIFT_BOTTOM;
                x = a.x + (b.x - a.x) * u; y = a.y + (b.y - a.y) * u + AbyssConst.EYE; z = a.z + (b.z - a.z) * u;
            }
            else if (s.RideStarted)
            {
                var w13 = AbyssConst.W[13];
                double zz = w13.z - (s.D - DLift);
                double zMin = AbyssConst.W[15].z;
                x = w13.x; y = w13.y + AbyssConst.EYE; z = zz < zMin ? zMin : zz;
            }
            else return 1e4;
            double dx = x - c.x, dy = y - c.y, dz = z - c.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static double LiftSpeed(double r)
        {
            const double h = 1e-3;
            double a = Math.Max(0.0, r - h), b = Math.Min(AbyssConst.LIFT_DURATION, r + h);
            return b > a ? (AbyssConst.LiftS(b) - AbyssConst.LiftS(a)) / (b - a) : 0.0;
        }

        // ------------------------------------------------------------------ the score

        /// <summary>Compute the parameter snapshot for one instant. Pure: same input, same output; allocation-free.</summary>
        public static void Evaluate(in ScoreInput s, ref AudioParams p)
        {
            float e = ExtendedProgress(s);

            // clocks
            p.SceneTime = s.SceneTime;
            p.ClockRate = s.ClockRunning ? 1f : 0f;
            p.RealTime = s.RealTime;
            p.WalkStarted = s.WalkStarted;
            p.CrankTheta = AbyssConst.CrankThetaIL(s.SceneTime);
            p.CrankRate = (float)(AbyssConst.CrankThetaIL(1.0) - AbyssConst.CrankThetaIL(0.0)) * p.ClockRate;
            float[] bankV = AbyssConst.BANK_V;
            p.PulsePan0 = bankV[0] / 96f; p.PulsePan1 = bankV[1] / 96f; p.PulsePan2 = bankV[2] / 96f; p.PulsePan3 = bankV[3] / 96f;
            p.FanPhase = s.SceneTime / AbyssConst.FAN_BLADE_PASS;
            p.FanRate = p.ClockRate / AbyssConst.FAN_BLADE_PASS;

            // beds and event buses from the E curves
            p.Sub = SubTrack.Gain(e);
            p.Air = AirTrack.Gain(e);
            p.Hum = HumTrack.Gain(e);
            p.Pulse = PulseTrack.Gain(e);
            p.Groan = GroanTrack.Gain(e);
            p.GroanDry = GroanDryTrack.Gain(e);
            p.Bell = BellTrack.Gain(e);
            p.Fan = FanTrack.Gain(e);
            p.Footsteps = 1f;

            // airflow: -40 .. -30 dB with proximity to the fan, riding on the FAN envelope (14.3)
            double dist = FanDistance(s);
            double prox = DspMath.Clamp01((AudioMix.AirflowFarDist - dist) / (AudioMix.AirflowFarDist - AudioMix.AirflowNearDist));
            float airflowDb = (float)(AudioMix.AirflowFarDb + (AudioMix.AirflowNearDb - AudioMix.AirflowFarDb) * prox);
            p.Airflow = DspMath.DbToLin(airflowDb) * (p.Fan / DspMath.DbToLin(AudioMix.FanFullDb));

            // wind (+4 dB in the cinematic hold H7)
            p.Wind = WindTrack.Gain(e) * (float)Math.Pow(10.0, AudioMix.WindHoldBoostDb * DspMath.Clamp01(s.HoldH7) / 20.0);

            // LIFT: rumble with speed, whine during the dwell fading over the first 4 s (silence b), ticks every 12 m
            double r = s.RideSeconds;
            double v = 0.0, liftS = 0.0, env = 0.0, whine = 0.0;
            bool ticking = false;
            if (s.RideStarted && r >= 0.0)
            {
                double rc = Math.Min(r, AbyssConst.LIFT_DURATION);
                liftS = AbyssConst.LiftS(rc);
                v = r < AbyssConst.LIFT_DURATION ? LiftSpeed(rc) : 0.0;
                env = r <= Post ? 1.0 : 1.0 - DspMath.Smootherstep((r - Post) / LiftFadeLen);
                whine = 1.0 - DspMath.Smootherstep(r / SilenceBLen);
                ticking = r <= AbyssConst.LIFT_DURATION + 0.5;
            }
            else if (s.DwellSeconds >= 0f)
            {
                env = DspMath.Smootherstep(s.DwellSeconds / AbyssConst.LIFT_DWELL);
                whine = DspMath.Smootherstep(s.DwellSeconds / (0.75 * AbyssConst.LIFT_DWELL));
            }
            double motion = 0.25 + 0.75 * v / AbyssConst.LIFT_CRUISE;
            p.LiftS = liftS;
            p.LiftV = (float)v * p.ClockRate;
            p.LiftTicking = ticking;
            p.LiftRumble = (float)(DspMath.DbToLin(AudioMix.LiftRumbleDb) * motion * env);
            p.LiftWhine = (float)(DspMath.DbToLin(AudioMix.LiftWhineDb) * whine);
            p.LiftTick = (float)(DspMath.DbToLin(AudioMix.LiftTickDb) * env);

            // reverb target preset
            ReverbPreset rv = AudioMix.Reverb[ReverbZone(e)];
            p.RevRt60 = rv.Rt60; p.RevPreDelay = rv.PreDelay; p.RevDamping = rv.Damping; p.RevWetDb = rv.WetDb; p.RevBlendSeconds = rv.BlendSeconds;

            // master: fade with the picture; the ending fades even if the picture does not (14.3 d), then the tone
            float fade = (float)DspMath.Clamp01(s.PictureFade);
            p.TailGain = 1f;
            p.EndTone = 0f;
            if (s.GoalReached && s.GoalRealTime >= 0.0)
            {
                double te = s.RealTime - s.GoalRealTime;
                float endFade = (float)(1.0 - DspMath.Smootherstep((te - AudioMix.EndingFadeDelay) / AudioMix.EndingFade));
                if (endFade < fade) fade = endFade;
                if (te >= AudioMix.EndingFadeDelay + AudioMix.EndingFade) fade = 0f;
                p.TailGain = fade;
                double ts = te - AudioMix.EndToneStart;
                double toneEnv = ts <= 0.0 ? 0.0
                    : ts < AudioMix.EndToneIn ? DspMath.Smootherstep(ts / AudioMix.EndToneIn)
                    : 1.0 - DspMath.Smootherstep((ts - AudioMix.EndToneIn) / AudioMix.EndToneOut);
                p.EndTone = (float)(DspMath.DbToLin(AudioMix.EndToneDb) * toneEnv);
            }
            p.MasterFade = fade;
            p.Duck = s.Paused ? DspMath.DbToLin(AudioMix.PauseDuckDb) : 1f;
            p.TrimDb = s.DisableTrim ? 0f : TrimTrack.Db(e);
            p.ThudTime = AudioMix.TitleBlack;
        }

        // ------------------------------------------------------------------ nominal walk (14.4 timeline at 1.5 m/s)

        /// <summary>d at which the goal triggers (within 1.2 m of the door, z = -72.8).</summary>
        public static float GoalD => AbyssConst.WD[16] - AbyssConst.DOOR_TRIGGER_Z;

        /// <summary>Nominal time of GoalReached (657.2 s).</summary>
        public static double GoalTime => AbyssConst.T_GATE_OPEN + (GoalD - DLift) / AbyssConst.V_WALK;

        /// <summary>End of the nominal render: the ending tone is over (goal + 16 s) plus 1 s of silence.</summary>
        public static double WalkEndTime => GoalTime + AudioMix.EndToneStart + AudioMix.EndToneIn + AudioMix.EndToneOut + 1.0;

        /// <summary>Nominal state at scene time t (the scene clock equals real time in the nominal walk).</summary>
        public static void Nominal(double t, ref ScoreInput s)
        {
            s.SceneTime = t;
            s.RealTime = t;
            s.WalkStarted = true;
            s.ClockRunning = true;
            s.Paused = false;
            s.HoldH7 = 0f;
            s.DwellSeconds = -1f;
            if (t < AbyssConst.T_PLATFORM_CENTRE)
            {
                s.D = (float)Math.Min(t * AbyssConst.V_WALK, DLift);
                s.RideStarted = false;
                s.RideSeconds = -1f;
            }
            else if (t < AbyssConst.T_RIDE_START)
            {
                s.D = DLift;
                s.RideStarted = false;
                s.RideSeconds = -1f;
                s.DwellSeconds = (float)(t - AbyssConst.T_PLATFORM_CENTRE);
            }
            else
            {
                s.RideStarted = true;
                s.RideSeconds = (float)(t - AbyssConst.T_RIDE_START);
                s.D = t < AbyssConst.T_GATE_OPEN ? DLift : (float)Math.Min(DLift + (t - AbyssConst.T_GATE_OPEN) * AbyssConst.V_WALK, AbyssConst.POLYLINE_LENGTH);
            }
            double tg = GoalTime;
            s.GoalReached = t >= tg;
            s.GoalRealTime = s.GoalReached ? tg : -1.0;
            double title = DspMath.Smootherstep((t - AudioMix.TitleBlack) / AudioMix.TitleFade);
            double ending = s.GoalReached ? 1.0 - DspMath.Smootherstep((t - tg - AudioMix.EndingFadeDelay) / AudioMix.EndingFade) : 1.0;
            s.PictureFade = (float)Math.Min(title, ending);
            s.DisableTrim = false;
        }

        /// <summary>Footstep surface along the nominal path (the player uses PathGuide.Surface; this mirrors 14.3 / 14.4).</summary>
        public static SurfaceKind NominalSurface(float d)
        {
            if (d < AbyssConst.STAIR_Z0) return SurfaceKind.Concrete;
            if (d <= AbyssConst.STAIR_Z1) return SurfaceKind.Stair;
            if (d < AbyssConst.BRIDGE_Z0) return SurfaceKind.Concrete;     // gallery exit and landing
            if (d <= AbyssConst.BRIDGE_Z1) return SurfaceKind.SteelPlate;  // bridge deck
            if (d < AbyssConst.WD[9]) return SurfaceKind.Concrete;         // ledge and apron
            if (d < AbyssConst.WD[11]) return SurfaceKind.Grating;         // catwalk and dock apron
            if (d < AbyssConst.WD[14]) return SurfaceKind.LiftDeck;        // platform deck (on and off)
            if (d < AbyssConst.WD[15]) return SurfaceKind.Grating;         // gantry
            return SurfaceKind.Concrete;                                    // tunnel, balcony, pier
        }

        public const float StepSpacing = 0.75f;

        sealed class StepList
        {
            public double[] Time;
            public SurfaceKind[] Kind;
            public int Count;
        }

        static StepList _steps;
        static readonly object StepLock = new object();

        /// <summary>Nominal footsteps: one per 0.75 m, one per 0.28 m tread on the stairs, none while riding.</summary>
        static StepList Steps
        {
            get
            {
                lock (StepLock)
                {
                    if (_steps != null) return _steps;
                    var t = new List<double>(1200);
                    var k = new List<SurfaceKind>(1200);
                    float d = StepSpacing;
                    while (d < AbyssConst.STAIR_Z0) { t.Add(d / AbyssConst.V_WALK); k.Add(NominalSurface(d)); d += StepSpacing; }
                    for (int j = 1; j <= AbyssConst.STAIR_COUNT; j++)
                    {
                        float dj = AbyssConst.STAIR_Z0 + AbyssConst.STAIR_RUN * j;
                        t.Add(dj / AbyssConst.V_WALK); k.Add(SurfaceKind.Stair);
                    }
                    d = AbyssConst.STAIR_Z1 + StepSpacing;
                    while (d <= DLift) { t.Add(d / AbyssConst.V_WALK); k.Add(NominalSurface(d)); d += StepSpacing; }
                    d = DLift + StepSpacing;
                    float last = GoalD + 0.5f;
                    while (d <= last)
                    {
                        t.Add(AbyssConst.T_GATE_OPEN + (d - DLift) / AbyssConst.V_WALK); k.Add(NominalSurface(d));
                        d += StepSpacing;
                    }
                    _steps = new StepList { Time = t.ToArray(), Kind = k.ToArray(), Count = t.Count };
                    return _steps;
                }
            }
        }

        /// <summary>Number of nominal footsteps (diagnostics, tests).</summary>
        public static int NominalStepCount => Steps.Count;

        // ------------------------------------------------------------------ offline rendering

        /// <summary>Nominal start time of the 20 s analysis window of each zone (inside the beat, after its transitions).</summary>
        public static double ZoneWindowStart(int zone)
        {
            float[] bt = AbyssConst.BEAT_START_T;
            switch (zone)
            {
                case 1: return bt[0] + 6.0;
                case 2: return bt[1] + 20.0;
                case 3: return bt[2] + 8.0;
                case 4: return bt[3] + 32.0;
                case 5: return AbyssConst.T_RIDE_START + 30.0;
                case 6: return bt[5] + 34.0;
                case 7: return bt[6] + 9.3;
                case 8: return AbyssConst.T_GATE_OPEN + (FaceD - DLift) / AbyssConst.V_WALK + 16.0;
                default: return GoalTime - 2.0;
            }
        }

        /// <summary>Render [tStart, tEnd) of the nominal walk into dest (interleaved stereo) and/or a WAV stream.</summary>
        static void RenderNominal(ProceduralAudioEngine eng, double tStart, double tEnd, float[] dest, WavStreamWriter wav, bool disableTrim)
        {
            int fs = eng.SampleRate;
            int frame = fs / SnapshotsPerSecond;
            StepList steps = Steps;
            int si = 0;
            while (si < steps.Count && steps.Time[si] < tStart) si++;
            var input = new ScoreInput();
            var p = new AudioParams();
            long n0 = (long)Math.Round(tStart * fs), n1 = (long)Math.Round(tEnd * fs);
            float[] chunk = dest == null ? new float[frame * 2] : null;
            long n = n0;
            int written = 0;
            while (n < n1)
            {
                double t = (double)n / fs;
                Nominal(t, ref input);
                input.DisableTrim = disableTrim;
                Evaluate(in input, ref p);
                p.Version++;
                eng.Exchange.Publish(in p);
                long end = Math.Min(n + frame, n1);
                while (n < end)
                {
                    long until = end;
                    while (si < steps.Count)
                    {
                        long sn = (long)Math.Round(steps.Time[si] * fs);
                        if (sn <= n) { eng.TriggerFootstep(steps.Kind[si]); si++; continue; }
                        if (sn < until) until = sn;
                        break;
                    }
                    int cnt = (int)(until - n);
                    if (dest != null) eng.RenderInterleavedStereo(dest, written, cnt);
                    else
                    {
                        eng.RenderInterleavedStereo(chunk, 0, cnt);
                        wav?.Write(chunk, cnt);
                    }
                    written += cnt;
                    n = until;
                }
            }
        }

        /// <summary>
        /// Render the analysis window of a zone (1..8 = beats 1-6, 7 tunnel, 7 outside; 9 = the ending from 2 s before the
        /// goal): 10 s of discarded pre-roll so smoothers and the reverb settle, then <paramref name="seconds"/> at 48 kHz.
        /// Returns interleaved stereo.
        /// </summary>
        public static float[] RenderBeat(int beatIndex, float seconds, uint seed) => RenderZone(beatIndex, seconds, seed, false);

        static float[] RenderZone(int zone, double seconds, uint seed, bool disableTrim)
        {
            return RenderSpan(ZoneWindowStart(zone), seconds, seed, SampleRate, disableTrim, out _);
        }

        /// <summary>
        /// Render <paramref name="seconds"/> of the nominal walk starting at nominal time <paramref name="t0"/>, after up to
        /// 10 s of discarded pre-roll (none before t = 0, so a span from 0 includes the title timeline exactly).
        /// Returns interleaved stereo at <paramref name="sampleRate"/> and the engine (event counters, reverb state).
        /// </summary>
        public static float[] RenderSpan(double t0, double seconds, uint seed, int sampleRate, bool disableTrim, out ProceduralAudioEngine engine)
        {
            var eng = new ProceduralAudioEngine(sampleRate, seed);
            if (t0 > 0.0) RenderNominal(eng, Math.Max(0.0, t0 - PreRollSeconds), t0, null, null, disableTrim);
            int frames = (int)Math.Round(seconds * sampleRate);
            var buf = new float[frames * 2];
            RenderNominal(eng, t0, t0 + (double)frames / sampleRate, buf, null, disableTrim);
            engine = eng;
            return buf;
        }

        /// <summary>The whole nominal walk plus the ending, interleaved stereo at 48 kHz (about 260 MB: prefer RenderWalkToWav).</summary>
        public static float[] RenderWalk(uint seed)
        {
            var eng = new ProceduralAudioEngine(SampleRate, seed);
            int frames = (int)Math.Round(WalkEndTime * SampleRate);
            var buf = new float[frames * 2];
            RenderNominal(eng, 0.0, (double)frames / SampleRate, buf, null, false);
            return buf;
        }

        /// <summary>Stream the whole nominal walk plus the ending to a 48 kHz float WAV. Returns the engine for diagnostics.</summary>
        public static ProceduralAudioEngine RenderWalkToWav(string path, uint seed)
        {
            var eng = new ProceduralAudioEngine(SampleRate, seed);
            using (var wav = new WavStreamWriter(path, 2, SampleRate))
                RenderNominal(eng, 0.0, WalkEndTime, null, wav, false);
            return eng;
        }

        /// <summary>
        /// Measure the per-beat master trims: render each zone window with trims disabled and return
        /// T_b = target - median 0.4 s window RMS (dB). Used to fill <see cref="AudioMix.MasterTrimDb"/>.
        /// </summary>
        public static float[] MeasureTrims(uint seed, float seconds, out float[] measuredDb)
        {
            var trims = new float[AudioMix.ZoneCount];
            measuredDb = new float[AudioMix.ZoneCount];
            for (int z = FirstZone; z <= LastZone; z++)
            {
                float[] buf = RenderZone(z, seconds, seed, true);
                double med = AudioAnalysis.Median(AudioAnalysis.WindowRmsDb(buf, SampleRate, AudioMix.RmsWindowSeconds));
                measuredDb[z - 1] = (float)med;
                trims[z - 1] = (float)(AudioMix.TargetRmsDb[z - 1] - med);
            }
            return trims;
        }
    }
}
