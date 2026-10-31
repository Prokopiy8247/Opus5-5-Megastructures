using System.Globalization;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Title card, controls hint and ending of MASTER_PROMPT 14.11 / Part 9 as pure functions of time, plus the shared
    /// typography. FadeController evaluates them every frame; EditMode tests can call them directly.
    ///
    /// Title timeline (t = seconds since the load finished): 0-0.8 black; 0.8-3.3 the picture fades in (2.5 s, smootherstep);
    /// "ABYSS" fades in 1.0-2.0, holds to 5.0, fades out 5.0-6.5; the controls hint appears at 7.0 for 6 s or until the
    /// first movement, never in cinematic mode.
    /// Ending (tg = seconds since GameState.GoalRealTime): the picture fades to black over 1.0-5.0; the line
    /// "Shaft 1 of 4,920." fades in over 6.0-8.0 to 70 % opacity, holds 6 s (to 14.0) and fades out by 16.0; then black.
    /// </summary>
    public static class TitleCard
    {
        // ---------------------------------------------------------------- texts
        public const string TITLE = "ABYSS";

        /// <summary>Ending line; the count is WORLD_CELLS_28KM (4 923.6 computed, rounded down to tens).</summary>
        public static readonly string ENDING_LINE =
            "Shaft 1 of " + AbyssConst.WORLD_CELLS_28KM.ToString("N0", CultureInfo.InvariantCulture) + ".";

        /// <summary>One-line controls hint (Part 9 controls).</summary>
        public const string HINT = "W A S D   move      mouse   look      shift   brisk      C   cinematic      esc   pause";

        public static readonly string[] MENU_ITEMS = { "RESUME", "RESTART", "QUIT" };

        // ---------------------------------------------------------------- typography (fractions of the screen height)
        /// <summary>#C8D2DA; per-element opacity is applied on top.</summary>
        public static readonly Color TEXT_COLOUR = AbyssPalette.Hex(0xC8D2DA);

        public const float LETTER_SPACING_EM = 0.3f;     // +0.3 em tracking (USS letter-spacing has no em unit: px = em x size)
        public const float TITLE_SIZE = 0.06f;           // 6 % of screen height
        public const float TITLE_CENTRE_Y = 0.46f;       // slightly above the middle (from the top)
        public const float TITLE_OPACITY = 0.85f;
        public const float HINT_SIZE = 0.019f;
        public const float HINT_SPACING_EM = 0.18f;      // quieter tracking for the long hint line
        public const float HINT_CENTRE_Y = 0.92f;
        public const float HINT_OPACITY = 0.55f;
        /// <summary>
        /// Ending line size: 14.11 "in the same typography" as the title, so the title's size and tracking (R2-34: at 2.6 %
        /// the line rendered as a 25 px glyph box at 1080p, hard to read after encoding and on a phone). About 12.4 em with
        /// the tracking, i.e. 0.75 x the screen height wide, so it fits every aspect down to 4:3.
        /// </summary>
        public const float END_SIZE = TITLE_SIZE;
        public const float END_CENTRE_Y = 0.5f;
        public const float END_OPACITY = 0.70f;          // "at 70 % opacity"
        public const float MENU_SIZE = 0.03f;
        public const float MENU_GAP = 0.035f;            // vertical gap between items
        public const float MENU_SELECTED_OPACITY = 0.95f;
        public const float MENU_IDLE_OPACITY = 0.40f;
        public const float PAUSE_DIM = 0.6f;             // black layer behind the menu

        // ---------------------------------------------------------------- title timeline (s)
        public const float BLACK_END = 0.8f;
        public const float PICTURE_FADE = 2.5f;
        public const float TITLE_IN0 = 1.0f, TITLE_IN1 = 2.0f, TITLE_OUT0 = 5.0f, TITLE_OUT1 = 6.5f;
        public const float HINT_START = 7.0f, HINT_DURATION = 6.0f, HINT_FADE = 0.6f, HINT_CANCEL_FADE = 0.4f;

        // ---------------------------------------------------------------- ending timeline (s after GoalRealTime)
        public const float END_FADE_START = 1.0f, END_FADE_DURATION = 4.0f;
        public const float END_LINE_START = 6.0f, END_LINE_FADE_IN = 2.0f, END_LINE_HOLD = 6.0f, END_LINE_FADE_OUT = 2.0f;

        /// <summary>Picture multiplier of the title timeline (0 = black).</summary>
        public static float PictureFade(double t) => S((t - BLACK_END) / PICTURE_FADE);

        /// <summary>Opacity factor (0..1) of the title; multiply by TITLE_OPACITY.</summary>
        public static float TitleAlpha(double t)
        {
            if (t < TITLE_IN0 || t >= TITLE_OUT1) return 0f;
            if (t < TITLE_IN1) return S((t - TITLE_IN0) / (TITLE_IN1 - TITLE_IN0));
            if (t < TITLE_OUT0) return 1f;
            return 1f - S((t - TITLE_OUT0) / (TITLE_OUT1 - TITLE_OUT0));
        }

        /// <summary>Opacity factor of the hint ignoring cancellation (fades 0.6 s in and out inside its 6 s window).</summary>
        public static float HintAlpha(double t)
        {
            double end = HINT_START + HINT_DURATION;
            if (t < HINT_START || t >= end) return 0f;
            float a = S((t - HINT_START) / HINT_FADE);
            float b = 1f - S((t - (end - HINT_FADE)) / HINT_FADE);
            return Mathf.Min(a, b);
        }

        /// <summary>Hint opacity factor when the first movement happened at time cancelT (negative = not cancelled).</summary>
        public static float HintAlpha(double t, double cancelT)
        {
            float a = HintAlpha(t);
            if (cancelT < 0) return a;
            if (cancelT <= HINT_START) return 0f;   // moved before the hint was due: never show it
            return Mathf.Min(a, 1f - S((t - cancelT) / HINT_CANCEL_FADE));
        }

        /// <summary>Picture multiplier of the ending (1 until +1.0 s, black at +5.0 s).</summary>
        public static float EndingPicture(double tg) => 1f - S((tg - END_FADE_START) / END_FADE_DURATION);

        /// <summary>Opacity factor of the ending line (multiply by END_OPACITY).</summary>
        public static float EndingLine(double tg)
        {
            double inEnd = END_LINE_START + END_LINE_FADE_IN;
            double holdEnd = inEnd + END_LINE_HOLD;
            if (tg < END_LINE_START) return 0f;
            if (tg < inEnd) return S((tg - END_LINE_START) / END_LINE_FADE_IN);
            if (tg < holdEnd) return 1f;
            return 1f - S((tg - holdEnd) / END_LINE_FADE_OUT);
        }

        /// <summary>Seconds after GoalRealTime when the ending sequence is complete (line gone, screen black).</summary>
        public static float EndingDuration => END_LINE_START + END_LINE_FADE_IN + END_LINE_HOLD + END_LINE_FADE_OUT;

        static float S(double x) => ProgressTables.Smootherstep((float)x);
    }
}
