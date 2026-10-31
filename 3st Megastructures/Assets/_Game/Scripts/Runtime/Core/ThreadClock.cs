// ThreadClock: the ambient clock (bank breathing, cloud drift, planet bands, star glint, sway) and the
// grade frame counter. Play: Time.timeAsDouble. Capture: stepped by the harness. Static tests: frozen.
// Ambient motion never feeds a beat parameter (14.6).
namespace Megastructure
{
    public static class ThreadClock
    {
        public static bool Frozen;            // -freeze-ambient or static tests
        public static bool Stepped;           // capture harness drives Time explicitly
        static double s_time;
        static double s_lastUnity;
        static bool s_init;
        public static long Frame;             // grade frame index (drives the R2 dither offset)
        public static bool FreezeDither;      // -freeze-dither

        public static double Ambient => s_time;

        /// <summary>Called once per frame by ProgressDirector with Time.timeAsDouble.</summary>
        public static void Tick(double unityTime)
        {
            if (!s_init) { s_lastUnity = unityTime; s_init = true; }
            double dt = unityTime - s_lastUnity;
            s_lastUnity = unityTime;
            if (!Frozen && !Stepped && dt > 0 && dt < 1.0) s_time += dt;
            if (!FreezeDither) Frame++;
        }

        /// <summary>Capture harness: set the ambient time explicitly (deterministic sequences).</summary>
        public static void Set(double t) { s_time = t; }
        public static void Advance(double dt) { if (!Frozen) s_time += dt; }
        public static void Reset() { s_time = 0; s_init = false; Frame = 0; }
    }

    /// <summary>
    /// GradeState: parameters of the ThreadGrade pass (exposure, bloom, vignette, saturation, contrast,
    /// dither). Written by ProgressDirector each frame from ThreadMath, read by ThreadGradeFeature.
    /// </summary>
    public static class GradeState
    {
        public static float ExposureEV;
        public static float BloomThreshold = 1.0f;
        public static float BloomIntensity = 0.10f;
        public static float BloomScatter = 0.60f;
        public static float Vignette = 0.10f;
        public static float Saturation;      // -100..100 (URP convention), +4 = 4 percent
        public static float Contrast;        // -100..100
        public static float DitherLsb = 1.0f;
        public static float Darkness;        // D (dither x1.5 where D > 0.5)
        public static float FadeBlack;       // 0..1 black overlay applied inside the grade (title/ending fades)
        public static bool Enabled = true;
    }
}
