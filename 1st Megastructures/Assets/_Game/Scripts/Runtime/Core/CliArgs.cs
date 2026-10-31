using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Command-line flags of the game (Part 11.1, CONTRACTS section 9), parsed once from
    /// <see cref="Environment.GetCommandLineArgs"/>. Flags are case-insensitive and may be written with one or two dashes.
    /// Every known flag has a fixed value count (arity), so negative numbers (for example <c>-clock -5</c>) parse correctly.
    /// Unknown flags (Unity's own <c>-logFile</c>, <c>-screen-width</c>, ...) are kept with the following token when that token
    /// does not look like a flag, and are otherwise ignored.
    ///
    /// <see cref="Parse"/> (called by GameBootstrap first in the load sequence, and lazily by every accessor) also applies the
    /// mode flags to <see cref="GameState"/>: Seed, Cinematic, AutoTest and CaptureMode.
    /// Allocation happens only while parsing; all accessors are allocation-free.
    /// </summary>
    public static class CliArgs
    {
        // ---------------------------------------------------------------- flag names (CONTRACTS section 9)
        public const string AutoTestFlag = "-autotest";
        public const string BeatFlag = "-beat";
        public const string ProgressFlag = "-progress";
        public const string DFlag = "-d";
        public const string CaptureFlag = "-capture";
        public const string ShotsFlag = "-shots";
        public const string ResFlag = "-res";
        public const string CinematicFlag = "-cinematic";
        public const string SeedFlag = "-seed";
        public const string QuitAfterFlag = "-quit-after";
        public const string PerfLogFlag = "-perf-log";
        public const string NoFadeFlag = "-nofade";
        public const string FreezeClockFlag = "-freeze-clock";
        public const string ClockFlag = "-clock";
        public const string TierFlag = "-tier";
        public const string NoPostFlag = "-nopost";
        public const string DitherFlag = "-dither";
        public const string DitherAmpFlag = "-dither-amp";
        public const string RenderAudioFlag = "-render-audio";
        public const string AudioDumpFlag = "-audio-dump";
        public const string SoakFlag = "-soak";
        public const string BotFlag = "-bot";

        /// <summary>Value count of every known flag.</summary>
        static readonly Dictionary<string, int> Arity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { AutoTestFlag, 0 }, { CinematicFlag, 0 }, { NoFadeFlag, 0 }, { FreezeClockFlag, 0 }, { NoPostFlag, 0 },
            { BeatFlag, 1 }, { ProgressFlag, 1 }, { DFlag, 1 }, { CaptureFlag, 1 }, { ShotsFlag, 1 }, { ResFlag, 1 },
            { SeedFlag, 1 }, { QuitAfterFlag, 1 }, { PerfLogFlag, 1 }, { ClockFlag, 1 }, { TierFlag, 1 },
            { DitherFlag, 1 }, { DitherAmpFlag, 1 }, { RenderAudioFlag, 1 }, { SoakFlag, 1 }, { BotFlag, 1 },
            { AudioDumpFlag, 2 },
            // QA harness flags (Runtime/QA, docs/QA_RUNTIME.md); -capture-parity [TAG] and -firstlast [DIR] keep the optional-value rule
            { "-qa-out", 1 }, { "-seq-seconds", 1 }, { "-seq-fps", 1 }, { "-capture-flip", 1 }, { "-bot-filter", 1 },
            { "-bot-speeds", 1 }, { "-soak-mem-tol-mb", 1 }, { "-capture-settle", 1 },
            { "-lint-only", 0 }, { "-seq-static", 0 }, { "-realtime", 0 }, { "-soak-virtual", 0 },
        };

        static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] NoValues = new string[0];
        static bool _parsed;

        /// <summary>The raw arguments that were parsed (without the executable path).</summary>
        public static string[] Raw { get; private set; } = new string[0];

        // ---------------------------------------------------------------- typed accessors (valid after Parse)
        public static bool AutoTest => Has(AutoTestFlag);
        public static bool Cinematic => Has(CinematicFlag);
        public static bool NoFade => Has(NoFadeFlag);
        public static bool FreezeClock => Has(FreezeClockFlag);
        public static bool NoPost => Has(NoPostFlag);

        /// <summary>-beat N (1..7), or -1 when absent.</summary>
        public static int Beat => Mathf.Clamp(Int(BeatFlag, -1), -1, 7);

        /// <summary>
        /// Requested start distance in metres from <c>-d D</c> or <c>-progress X</c> (X in 0..1 of POLYLINE_LENGTH), or -1 when absent.
        /// <c>-d</c> wins when both are given.
        /// </summary>
        public static float StartD
        {
            get
            {
                if (Has(DFlag)) return Mathf.Clamp(Float(DFlag, 0f), 0f, AbyssConst.POLYLINE_LENGTH);
                if (Has(ProgressFlag)) return Mathf.Clamp01(Float(ProgressFlag, 0f)) * AbyssConst.POLYLINE_LENGTH;
                return -1f;
            }
        }

        /// <summary>-capture &lt;dir&gt;, or null.</summary>
        public static string CaptureDir => String(CaptureFlag, null);

        /// <summary>-shots &lt;list&gt;, or null.</summary>
        public static string Shots => String(ShotsFlag, null);

        /// <summary>-seed N (default 1).</summary>
        public static uint Seed
        {
            get
            {
                string s = String(SeedFlag, null);
                if (s != null && uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v)) return v;
                if (s != null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return unchecked((uint)l);
                return 1u;
            }
        }

        /// <summary>-quit-after S in seconds, or -1.</summary>
        public static float QuitAfter => Float(QuitAfterFlag, -1f);

        /// <summary>-perf-log &lt;file&gt;, or null.</summary>
        public static string PerfLog => String(PerfLogFlag, null);

        /// <summary>-clock T (scene clock start), or NaN when absent.</summary>
        public static double Clock => Has(ClockFlag) ? Double(ClockFlag, 0.0) : double.NaN;

        /// <summary>-tier full|simplified|graybox (default Full).</summary>
        public static BeatTier Tier
        {
            get
            {
                string s = String(TierFlag, null);
                if (s == null) return BeatTier.Full;
                if (s.Equals("simplified", StringComparison.OrdinalIgnoreCase) || s.Equals("simple", StringComparison.OrdinalIgnoreCase)) return BeatTier.Simplified;
                if (s.Equals("graybox", StringComparison.OrdinalIgnoreCase) || s.Equals("greybox", StringComparison.OrdinalIgnoreCase)) return BeatTier.Graybox;
                return BeatTier.Full;
            }
        }

        /// <summary>-dither static|temporal|off, or null when absent.</summary>
        public static string Dither => String(DitherFlag, null);

        /// <summary>-dither-amp LSB, or -1 when absent.</summary>
        public static float DitherAmp => Float(DitherAmpFlag, -1f);

        /// <summary>-render-audio &lt;wav&gt;, or null.</summary>
        public static string RenderAudio => String(RenderAudioFlag, null);

        /// <summary>-audio-dump &lt;wav&gt; &lt;seconds&gt;: the file, or null.</summary>
        public static string AudioDumpFile => String(AudioDumpFlag, null);

        /// <summary>-audio-dump &lt;wav&gt; &lt;seconds&gt;: the duration, or -1.</summary>
        public static float AudioDumpSeconds => Float(AudioDumpFlag, -1f, 1);

        /// <summary>-soak S in seconds, or -1.</summary>
        public static float Soak => Float(SoakFlag, -1f);

        /// <summary>-bot &lt;name&gt;, or null.</summary>
        public static string Bot => String(BotFlag, null);

        // ---------------------------------------------------------------- parsing

        /// <summary>Parse the process arguments once and apply the mode flags to GameState. Idempotent.</summary>
        public static void Parse()
        {
            if (_parsed) return;
            string[] all;
            try { all = Environment.GetCommandLineArgs(); }
            catch (Exception) { all = new string[0]; }
            // element 0 is the executable
            var args = new string[Math.Max(0, all.Length - 1)];
            for (int i = 1; i < all.Length; i++) args[i - 1] = all[i];
            ParseFrom(args);
        }

        /// <summary>Replace the parsed arguments (tests, editor tools). Applies the mode flags to GameState.</summary>
        public static void Override(string[] args)
        {
            ParseFrom(args ?? new string[0]);
        }

        static void ParseFrom(string[] args)
        {
            Values.Clear();
            Raw = args;
            int i = 0;
            while (i < args.Length)
            {
                string tok = args[i];
                if (!IsFlagToken(tok)) { i++; continue; }
                string key = Normalise(tok);
                int n;
                if (!Arity.TryGetValue(key, out n))
                {
                    // unknown flag: keep one following value when it is not itself a flag
                    n = (i + 1 < args.Length && !LooksLikeFlag(args[i + 1])) ? 1 : 0;
                }
                var vals = n == 0 ? NoValues : new string[n];
                int got = 0;
                for (int k = 0; k < n && i + 1 + k < args.Length; k++)
                {
                    vals[k] = args[i + 1 + k];
                    got++;
                }
                if (got < n)
                {
                    Debug.LogWarning("[ABYSS] CliArgs: flag " + key + " expects " + n + " value(s), got " + got);
                    var trimmed = new string[got];
                    Array.Copy(vals, trimmed, got);
                    vals = trimmed;
                }
                Values[key] = vals;
                i += 1 + got;
            }
            _parsed = true;
            Apply();
            if (args.Length > 0) Debug.Log("[ABYSS] CliArgs: " + string.Join(" ", args));
        }

        /// <summary>Write the mode flags to GameState (owner of Seed, Cinematic, AutoTest, CaptureMode at load).</summary>
        static void Apply()
        {
            GameState.Seed = Seed;
            GameState.AutoTest = AutoTest;
            GameState.Cinematic = Cinematic;
            GameState.CaptureMode = Has(CaptureFlag);
        }

        static bool IsFlagToken(string s) => !string.IsNullOrEmpty(s) && s.Length > 1 && s[0] == '-' && !IsNumber(s);

        static bool LooksLikeFlag(string s) => IsFlagToken(s);

        static bool IsNumber(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        /// <summary>"--Seed" and "-seed" both become "-seed" (the dictionary is case-insensitive).</summary>
        static string Normalise(string s)
        {
            if (s.StartsWith("--", StringComparison.Ordinal)) return s.Substring(1);
            return s;
        }

        static void Ensure() { if (!_parsed) Parse(); }

        // ---------------------------------------------------------------- generic accessors

        /// <summary>True when the flag was given.</summary>
        public static bool Has(string flag)
        {
            Ensure();
            return Values.ContainsKey(Normalise(flag));
        }

        /// <summary>The value at index of the flag, or def.</summary>
        public static string String(string flag, string def, int index = 0)
        {
            Ensure();
            if (Values.TryGetValue(Normalise(flag), out var v) && index < v.Length && v[index] != null) return v[index];
            return def;
        }

        public static int Int(string flag, int def, int index = 0)
        {
            string s = String(flag, null, index);
            if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return (int)Math.Round(d);
            return def;
        }

        public static float Float(string flag, float def, int index = 0)
        {
            string s = String(flag, null, index);
            if (s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
            return def;
        }

        public static double Double(string flag, double def, int index = 0)
        {
            string s = String(flag, null, index);
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return v;
            return def;
        }

        /// <summary>-res WxH (also "W*H" or "W,H"). Returns false and leaves the outputs at the defaults when absent or invalid.</summary>
        public static bool Res(out int width, out int height, int defWidth = 1920, int defHeight = 1080)
        {
            width = defWidth; height = defHeight;
            string s = String(ResFlag, null);
            if (string.IsNullOrEmpty(s)) return false;
            int sep = s.IndexOfAny(new[] { 'x', 'X', '*', ',' });
            if (sep <= 0 || sep >= s.Length - 1) return false;
            if (!int.TryParse(s.Substring(0, sep), NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)) return false;
            if (!int.TryParse(s.Substring(sep + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)) return false;
            if (w < 16 || h < 16) return false;
            width = w; height = h;
            return true;
        }
    }
}
