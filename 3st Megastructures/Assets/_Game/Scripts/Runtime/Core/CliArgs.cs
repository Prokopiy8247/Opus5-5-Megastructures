// CliArgs: deterministic debug entry points (MASTER_PROMPT 11.1 plus the Part 14 flags).
//   -autotest            AutoWalker start to goal, assertions, exit code
//   -beat N              start at the start of beat N (1..7)
//   -progress X          start at plan distance X (m)
//   -capture <dir>       run the capture harness and write PNGs + report.json to <dir>
//   -capture-set <name>  which capture list (all, keyshots, beats, sequences, encode, ...)
//   -res WxH             window / capture resolution
//   -cinematic           start the cinematic autowalk
//   -seed N              PRNG seed (audio, autowalk sway, colossus asymmetry is fixed by design)
//   -quit-after S        quit after S seconds of wall time
//   -perf-log <file>     write frame-time CSV
//   -nofade              skip the title fade
//   -freeze-dither       static dither tile (no R2 offset)
//   -freeze-ambient      frozen ambient clock
//   -audio-dump <file>   dump N seconds of the real OnAudioFilterRead output to WAV
//   -brisk               autowalk / autotest at 2.5 m/s
//   -wallbot             wall-hugging bot (both corridor edges)
//   -soak S              soak test of S seconds
//   -speed X             autowalk speed override (m/s)
//   -tier N              force a fallback tier for diagnostics (0 = full)
using System;
using System.Globalization;

namespace Megastructure
{
    public static class CliArgs
    {
        static string[] s_args;
        public static string[] Args
        {
            get
            {
                if (s_args == null)
                {
                    try { s_args = Environment.GetCommandLineArgs(); } catch { s_args = new string[0]; }
                }
                return s_args;
            }
        }

        /// <summary>Test hook: replace the argument list.</summary>
        public static void Override(string[] args) { s_args = args; }

        public static bool Has(string flag)
        {
            foreach (var a in Args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string Get(string flag, string fallback = null)
        {
            var a = Args;
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], flag, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return fallback;
        }

        public static double GetDouble(string flag, double fallback)
        {
            var v = Get(flag);
            return v != null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;
        }

        public static int GetInt(string flag, int fallback)
        {
            var v = Get(flag);
            return v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) ? d : fallback;
        }

        public static bool TryGetRes(out int w, out int h)
        {
            w = h = 0;
            var v = Get("-res");
            if (v == null) return false;
            var p = v.ToLowerInvariant().Split('x');
            return p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w > 0 && h > 0;
        }

        public static bool AutoTest => Has("-autotest");
        public static bool Cinematic => Has("-cinematic");
        public static bool NoFade => Has("-nofade");
        public static bool FreezeDither => Has("-freeze-dither");
        public static bool FreezeAmbient => Has("-freeze-ambient");
        public static bool Brisk => Has("-brisk");
        public static bool WallBot => Has("-wallbot");
        public static string CaptureDir => Get("-capture");
        public static string CaptureSet => Get("-capture-set", "all");
        public static string PerfLog => Get("-perf-log");
        public static string AudioDump => Get("-audio-dump");
        public static int Seed => GetInt("-seed", 1);
        public static double QuitAfter => GetDouble("-quit-after", -1);
        public static double SoakSeconds => GetDouble("-soak", -1);
        public static int Tier => GetInt("-tier", 0);

        /// <summary>Start progress from -progress X or -beat N (1-based), else 0.</summary>
        public static double StartS
        {
            get
            {
                double p = GetDouble("-progress", -1);
                if (p >= 0) return Math.Min(p, ThreadConstants.PathLength);
                int b = GetInt("-beat", 0);
                if (b >= 1 && b <= ThreadConstants.BeatCount) return ThreadConstants.BeatBoundaries[b - 1];
                return 0;
            }
        }
    }
}
