using System;
using System.Collections.Generic;
using System.Globalization;

namespace Megastructure
{
    /// <summary>
    /// Command-line flags (Part 11.1 + Part 14). Parsed once from Environment.GetCommandLineArgs (or a test array).
    /// Flags: -autotest, -beat N, -progress X, -capture DIR, -res WxH, -cinematic, -seed N, -quit-after S,
    /// -perf-log FILE, -nofade, -set N (debug override of the world set), -shots LIST (capture plan),
    /// -sequence NAME (frame-sequence plan), -walltest, -soak SECONDS, -audio-dump FILE, -perf (vsync off, no frame cap),
    /// -window-capture (window-vs-RT parity capture), -speed brisk|walk (autotest speed).
    /// </summary>
    public static class CliArgs
    {
        static Dictionary<string, string> _map;
        static string[] _raw;

        public static void Parse(string[] args = null)
        {
            _raw = args ?? Environment.GetCommandLineArgs();
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _raw.Length; i++)
            {
                string a = _raw[i];
                if (!a.StartsWith("-") || a.Length < 2) continue;
                string key = a.TrimStart('-');
                string val = "";
                if (i + 1 < _raw.Length && !IsFlag(_raw[i + 1])) { val = _raw[i + 1]; i++; }
                _map[key] = val;
            }
        }

        static bool IsFlag(string s)
        {
            if (!s.StartsWith("-") || s.Length < 2) return false;
            return !(char.IsDigit(s[1]) || s[1] == '.');   // negative numbers are values
        }

        static void Ensure() { if (_map == null) Parse(); }

        public static bool Has(string key) { Ensure(); return _map.ContainsKey(key); }

        public static string Str(string key, string def = null) { Ensure(); return _map.TryGetValue(key, out var v) && v.Length > 0 ? v : def; }

        public static float Float(string key, float def)
        {
            var s = Str(key);
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        public static int Int(string key, int def)
        {
            var s = Str(key);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        public static bool AutoTest => Has("autotest");
        public static bool Cinematic => Has("cinematic");
        public static bool NoFade => Has("nofade");
        public static bool WallTest => Has("walltest");
        public static bool PerfMode => Has("perf") || Has("perf-log");
        public static string CaptureDir => Str("capture");
        public static bool Capturing => CaptureDir != null;
        public static int Seed => Int("seed", 1);
        public static float QuitAfter => Float("quit-after", -1f);
        public static string PerfLog => Str("perf-log");
        public static float Progress => Float("progress", float.NaN);
        public static int Beat => Int("beat", -1);
        public static int SetOverride => Int("set", -1);
        public static string Shots => Str("shots", "all");
        public static string Sequence => Str("sequence");
        public static float Soak => Float("soak", -1f);
        public static string AudioDump => Str("audio-dump");

        /// <summary>Resolution from -res WxH; false if absent or malformed.</summary>
        public static bool Res(out int w, out int h)
        {
            w = h = 0;
            var s = Str("res");
            if (s == null) return false;
            var p = s.ToLowerInvariant().Split('x');
            return p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w > 0 && h > 0;
        }

        /// <summary>Start progress implied by -progress or -beat (NaN = start of the walk).</summary>
        public static float StartS()
        {
            float p = Progress;
            if (!float.IsNaN(p)) return p;
            int b = Beat;
            if (b >= 1 && b <= 7) return RingConstants.BeatS[b - 1];
            return float.NaN;
        }
    }
}
