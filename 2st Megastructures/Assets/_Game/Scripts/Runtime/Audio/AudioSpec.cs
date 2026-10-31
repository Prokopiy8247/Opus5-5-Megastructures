using System;

namespace Megastructure
{
    /// <summary>
    /// Unity-free constants of the sound bible (MASTER_PROMPT 14.3) plus the few world numbers the DSP needs.
    /// The DSP classes must not reference UnityEngine (Part 11.9: EditMode tests and a plain .NET harness render them
    /// without an audio device), so the world numbers below are mirrors of <c>RingConstants</c> / <c>RingEvents</c>;
    /// <c>AudioTests.SpecMirrorsRingConstants</c> asserts that every mirror equals its source (single source of truth).
    /// Levels are RMS dBFS with RMS = 10 log10(mean(x^2)) over both channels (no AES17 +3 dB offset).
    /// </summary>
    public static class AudioSpec
    {
        public const double SampleRate = 48000.0;          // 14.3: 48 kHz (computed from the real rate at runtime)

        // ---------------- world mirrors (RingConstants, 14.4 / 14.12) ----------------
        public static readonly double[] BeatS = { 0, 24, 276, 411, 594, 774, 894, 960 };
        public const double GoalS = 959.0;
        public const double Walk = 1.5, Brisk = 2.5, StrideWalk = 0.75, StrideBrisk = 0.85, Eye = 1.7;
        public const double RingR = 16000.0;
        public const double HatchTriggerS = 6.5;
        public const double BridgeZ0 = 120.0, BridgeZ1 = 180.0;
        public const double RiverHalfWidth = 18.0;          // 14.5.2 river 36 m wide (RingConstants.RiverWidth / 2)
        public const double ArkX0 = -9000.0, ArkX1 = 6000.0, ArkT = 190.0, ArkD = 285.0, ArkTriggerS = 231.0;
        public const double ArkAltitude = 1800.0, ArkTrackZ = 3180.0;
        public const double SunsetS0 = 411.0, SunsetT = 70.0, SunsetD = 105.0;
        public const double SwapS = 576.0;
        public const double WallX = 114.0, TunnelRibX0 = 117.0, TunnelRibPitch = 6.0; public const int TunnelRibs = 20;
        public const double BeaconFirstPingS = 905.0, BeaconPeriod = 4.0;
        public const double FadeInStart = 0.6, FadeInEnd = 3.1, EndDwell = 3.0, EndFade = 4.0;

        /// <summary>Waypoints of the sharp polyline (x, z) = RingConstants.Waypoints.</summary>
        public static readonly double[] WaypointX = { 0, 0, 0, 0, 0, 0, 18, 100, 100, 114, 234, 300 };
        public static readonly double[] WaypointZ = { 0, 24, 276, 411, 558, 576, 576, 576, 660, 660, 660, 660 };

        /// <summary>River centre line (x, z) = RingConstants.RiverCentre (extended along the end tangents to +-8 200 m like RingWorld.River).</summary>
        public static readonly double[] RiverX = { -900, -420, -260, -120, 0, 110, 240, 420, 900 };
        public static readonly double[] RiverZ = { 232, 205, 178, 160, 150, 151, 166, 196, 238 };

        /// <summary>Cinematic holds (s, seconds) = RingConstants.CinematicHolds (49 s).</summary>
        public static readonly double[] HoldS = { 5.1, 150, 440, 505, 606, 888, 912, 960 };
        public static readonly double[] HoldT = { 5, 8, 6, 6, 6, 4, 8, 6 };

        // Footstep surfaces = (int)RingSurface
        public const int SurfMetal = 0, SurfMetalRamp = 1, SurfCeramic = 2, SurfGrass = 3, SurfBridge = 4, SurfUnderpass = 5,
                         SurfStone = 6, SurfTunnel = 7, SurfDeck = 8, SurfCount = 9;

        /// <summary>Twin of RingEvents.SurfaceAt (14.4 "Surfaces").</summary>
        public static int SurfaceAt(double s, bool onGrass)
        {
            if (s < 10.0) return SurfMetal;
            if (s < 24.0) return SurfMetalRamp;
            if (s >= 120.0 && s < 180.0) return SurfBridge;
            if (s < 558.0) return onGrass ? SurfGrass : SurfCeramic;
            if (s < 594.0) return SurfUnderpass;
            if (s < 774.0) return SurfStone;
            if (s < 894.0) return SurfTunnel;
            return SurfDeck;
        }

        public static int BeatAt(double s)
        {
            for (int i = 6; i >= 0; i--) if (s >= BeatS[i]) return i;
            return 0;
        }

        // ---------------- master (14.3 "Master") ----------------
        /// <summary>Sliding 3 s RMS targets: start and end of each beat (B4 and B6 ramp linearly in s).</summary>
        public static readonly double[] TargetStartDb = { -38, -27, -24, -28, -36, -40, -28 };
        public static readonly double[] TargetEndDb = { -38, -27, -24, -31, -36, -32, -28 };
        public const double TargetToleranceDb = 1.5;
        public const double PeakCeilingDbfs = -1.0;         // true peak
        public const double LimiterThresholdDb = -3.0, LimiterCeilingDb = -1.2, LimiterLookaheadMs = 5.0, LimiterReleaseMs = 80.0;
        public const double DcBlockHz = 20.0;
        public const double MaxDc = 0.001, MaxStep = 0.25;
        public const double PauseDuckDb = -12.0, PauseDuckTime = 0.4;
        public const double LayerRampTau = 0.050;           // 14.3: every layer ramps >= 50 ms (one-pole time constant)
        public const double FilterSmoothTau = 0.020;        // 14.3: filter modulation smoothed 20 ms

        /// <summary>Target RMS (dBFS) at progress s inside beat b.</summary>
        public static double TargetDb(int b, double s)
        {
            double s0 = BeatS[b], s1 = BeatS[b + 1];
            double t = (s - s0) / Math.Max(1e-6, s1 - s0);
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return TargetStartDb[b] + (TargetEndDb[b] - TargetStartDb[b]) * t;
        }

        // ---------------- layer levels (14.3 table, RMS dBFS before the per-beat trim) ----------------
        public const double C1Hum54Db = -46, C1Hum108Db = -50, C1AirDb = -45;          // C1 total -41.8 (spec -42)
        public const double HatchSweepPeakDb = -30, HatchServoDb = -40;
        public const double W1B1Db = -40, W1B2Db = -27, W1B3Db = -27, W1B4StartDb = -28, W1B4EndDb = -33, W1B5Db = -36, W1B7Db = -34;
        public const double W1SecondVoiceDb = -9;
        public const double G1Db = -42;
        public const double R1Db = -34;
        public const double S1Db = -38;
        public const double A1PeakDb = -24;
        public const double P1Db = -34, P1S2Dbfs = -60;
        public const double N1Db = -46, N2Db = -50;
        public const double U1Db = -36, H1Db = -46, T1Db = -36, T1TickDb = -54, T1AeolStartDb = -60, T1AeolEndDb = -34;
        public const double X1DroneDb = -30, X1GrainDb = -38;
        public const double B1Db = -42;
        public const double FootPeakDb = -30;

        /// <summary>Minimum listener-to-ark range on the walk (bent space, m); A1 is scaled so this range gives A1PeakDb.
        /// Measured by AudioWalkSim (closest approach of the 14.5.3 trajectory, about s 402).</summary>
        public const double A1RangeMin = 3175.5;
    }
}
