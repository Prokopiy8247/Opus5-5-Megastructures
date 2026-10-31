using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Camera position offsets layered on top of the player eye by <see cref="CameraRig"/>. Positional only: nothing here
    /// ever rotates the camera, so roll stays exactly zero (Part 9, Part 10 item 18).
    ///
    /// - Stair tread dip (14.3): 1.0 mm raised cosine over 100 ms at each tread contact; its 60 Hz second difference is at
    ///   most 0.55 mm, inside the 2.5 mm stair budget together with the 0.15 s eye smoothing (14.5.1 Acceptance).
    /// - Head bob (Part 9): optional, a few millimetres, one dip per footstep, faded out on the stairs.
    /// - Machine-hall vibration (14.5.4): inside the hall (x -32..-142) a 9 Hz band of RMS 3.0 mm (three seeded sines around
    ///   9 Hz, vertical), a 0.4 Hz lateral sway of peak 12 mm (world Z, across the catwalk) and a 6 mm vertical impulse at
    ///   each piston dead centre (one every 15 s from AbyssConst.CrankThetaIL), decaying over 0.8 s. Combined RMS about 9 mm.
    /// All functions are pure, deterministic (double time, seed-derived phases) and allocation-free.
    /// </summary>
    public static class HeadMotion
    {
        // ---------------------------------------------------------------- stairs (14.3)
        public const float TREAD_DIP = 0.001f;
        public const float TREAD_DIP_TIME = 0.1f;

        // ---------------------------------------------------------------- bob (Part 9: at most 1.5 cm)
        public const float BOB_AMPLITUDE = 0.004f;
        public const float BOB_AMPLITUDE_CINEMATIC = 0.0025f;
        public const float STEP_LENGTH = 0.75f;

        // ---------------------------------------------------------------- hall vibration (14.5.4)
        public const float HALL_BAND_RMS = 0.003f;
        public const float HALL_SWAY_PEAK = 0.012f;
        public const float HALL_SWAY_HZ = 0.4f;
        public const float HALL_IMPULSE = 0.006f;
        // double (the envelope is evaluated on double ages): as floats 0.8f = 0.80000001 left a 2.5e-16 tail at age 0.8
        public const double HALL_IMPULSE_ATTACK = 0.04;
        public const double HALL_IMPULSE_DECAY = 0.8;
        /// <summary>Fade-in length inside the portal and exit openings, so the vibration never pops on.</summary>
        public const float HALL_FADE = 4f;

        static readonly double[] BandHz = { 8.4, 9.0, 9.6 };
        // three equal sines: RMS = sqrt(3 a^2 / 2) = 3 mm  ->  a = 3 mm * sqrt(2/3)
        static readonly double BandAmp = HALL_BAND_RMS * System.Math.Sqrt(2.0 / 3.0);

        static uint _phaseSeed = 0xFFFFFFFFu;
        static readonly double[] _phase = new double[4];

        /// <summary>Tread contact dip (negative, metres) t seconds after the contact.</summary>
        public static float TreadDip(float t)
        {
            if (t < 0f || t >= TREAD_DIP_TIME) return 0f;
            return -0.5f * TREAD_DIP * (1f - Mathf.Cos(2f * Mathf.PI * t / TREAD_DIP_TIME));
        }

        /// <summary>Unit bob shape over one step (phase 0 = foot contact, lowest): -1 at contact, 0 mid-stride. Continuous across wraps.</summary>
        public static float Bob(float phase01)
        {
            return -0.5f * (1f + Mathf.Cos(2f * Mathf.PI * phase01));
        }

        /// <summary>Weight 0..1 of the hall vibration at a camera position (0 outside the hall, eased over HALL_FADE at the openings).</summary>
        public static float HallWeight(Vector3 p)
        {
            if (p.x > AbyssConst.HALL_X0 || p.x < AbyssConst.HALL_X1) return 0f;
            if (p.z < AbyssConst.HALL_Z0 || p.z > AbyssConst.HALL_Z1) return 0f;
            if (p.y < AbyssConst.HALL_FLOOR_Y || p.y > AbyssConst.HALL_ROOF_Y) return 0f;
            float a = GazeCues.SmoothStep01((AbyssConst.HALL_X0 - p.x) / HALL_FADE);
            float b = GazeCues.SmoothStep01((p.x - AbyssConst.HALL_X1) / HALL_FADE);
            return a * b;
        }

        /// <summary>Seconds since the most recent piston dead centre (0..15): dead centres fall at theta_IL = 0 mod 45 degrees.</summary>
        public static double DeadCentreAge(double sceneTime)
        {
            double th = AbyssConst.CrankThetaIL(sceneTime);
            double m = th - 45.0 * System.Math.Floor(th / 45.0);
            return m / 3.0;   // 3 degrees per second
        }

        /// <summary>Dead-centre impulse envelope 0..1: raised-cosine attack over 40 ms to the peak, quadratic decay to 0 at 0.8 s.</summary>
        public static float ImpulseEnvelope(double age)
        {
            if (age <= 0.0 || age >= HALL_IMPULSE_DECAY) return 0f;
            if (age < HALL_IMPULSE_ATTACK) return (float)(0.5 * (1.0 - System.Math.Cos(System.Math.PI * age / HALL_IMPULSE_ATTACK)));
            double u = (HALL_IMPULSE_DECAY - age) / (HALL_IMPULSE_DECAY - HALL_IMPULSE_ATTACK);
            return (float)(u * u);
        }

        /// <summary>Hall vibration offset (world metres) at the scene time: y = 9 Hz band + impulse, z = 0.4 Hz sway.</summary>
        public static Vector3 HallVibration(double sceneTime, uint seed)
        {
            EnsurePhases(seed);
            double y = 0.0;
            for (int i = 0; i < BandHz.Length; i++)
                y += BandAmp * System.Math.Sin(2.0 * System.Math.PI * BandHz[i] * sceneTime + _phase[i]);
            y -= HALL_IMPULSE * ImpulseEnvelope(DeadCentreAge(sceneTime));
            double z = HALL_SWAY_PEAK * System.Math.Sin(2.0 * System.Math.PI * HALL_SWAY_HZ * sceneTime + _phase[3]);
            return new Vector3(0f, (float)y, (float)z);
        }

        static void EnsurePhases(uint seed)
        {
            if (seed == _phaseSeed) return;
            _phaseSeed = seed;
            for (int i = 0; i < _phase.Length; i++)
                _phase[i] = Hash01(seed, (uint)(i + 101)) * 2.0 * System.Math.PI;
        }

        /// <summary>PCG-style integer hash of (seed, index) to [0, 1). Never sin-based (CONTRACTS 2).</summary>
        public static double Hash01(uint seed, uint index)
        {
            uint v = seed * 0x9E3779B9u ^ (index * 0x85EBCA6Bu + 0x68E31DA4u);
            uint state = v * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            word = (word >> 22) ^ word;
            return word / 4294967296.0;
        }
    }
}
