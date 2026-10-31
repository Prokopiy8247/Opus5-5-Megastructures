namespace Megastructure
{
    /// <summary>
    /// Pure, Unity-free timeline curves of the title card, controls hint and ending (Part 9, 14.11).
    /// Every value is an envelope in 0..1; the UiDirector multiplies it by the element's alpha.
    /// Intro t = seconds since RingEvents.LoadComplete; ending te = seconds since RingEvents.GoalReached.
    /// EditMode tests can sample these directly (no scene needed).
    /// </summary>
    public static class UiTimeline
    {
        // ---------------- intro (14.11 "Title") ----------------
        // HintFade and HintKill are 14.11 numbers not yet in RingConstants (contract request: add HintFade 0.6 and HintGone 0.4 there).

        /// <summary>14.11: hint "in at 6.0 s for 6 s with 0.6 s fades" (the 6 s include both fades).</summary>
        public const float HintFade = 0.6f;
        /// <summary>14.11: the hint is "gone within 0.4 s of first movement"; 0.35 s leaves one frame of margin.</summary>
        public const float HintKill = 0.35f;
        /// <summary>End of the intro (title out at 5.4 s, hint out at 12.0 s).</summary>
        public const float IntroEnd = RingConstants.HintAt + RingConstants.HintDur;

        /// <summary>Picture (and audio) visibility: black 0.0-0.6 s, smoothstep 0.6 -> 3.1 s.</summary>
        public static float Picture(float t) => RingMath.Smoothstep(RingConstants.FadeInStart, RingConstants.FadeInEnd, t);

        /// <summary>Title envelope: in 0.8 -> 2.0 s, hold to 4.0 s, out 4.0 -> 5.4 s.</summary>
        public static float Title(float t) =>
            RingMath.Smoothstep(RingConstants.TitleIn0, RingConstants.TitleIn1, t) *
            (1f - RingMath.Smoothstep(RingConstants.TitleOut0, RingConstants.TitleOut1, t));

        /// <summary>Hint envelope before gating: 6.0 -> 6.6 in, 11.4 -> 12.0 out.</summary>
        public static float Hint(float t)
        {
            float a = RingConstants.HintAt, b = RingConstants.HintAt + RingConstants.HintDur;
            return RingMath.Smoothstep(a, a + HintFade, t) * (1f - RingMath.Smoothstep(b - HintFade, b, t));
        }

        // ---------------- ending (14.11 "Ending") ----------------

        /// <summary>Picture fade-out starts after the 3.0 s dwell.</summary>
        public const float EndFadeStart = RingConstants.EndDwell;
        /// <summary>Picture fully black (3.0 + 4.0 = 7.0 s).</summary>
        public const float EndFadeEnd = EndFadeStart + RingConstants.EndFade;
        /// <summary>End line starts fading in after 1.5 s of black (8.5 s).</summary>
        public const float EndLineIn0 = EndFadeEnd + RingConstants.EndBlack;
        /// <summary>End line fully visible (10.5 s).</summary>
        public const float EndLineIn1 = EndLineIn0 + RingConstants.EndTextIn;
        /// <summary>End line starts fading out after the 6.0 s hold (16.5 s).</summary>
        public const float EndLineOut0 = EndLineIn1 + RingConstants.EndTextHold;
        /// <summary>End line gone; RingEvents.EndingComplete is raised (18.5 s).</summary>
        public const float EndTotal = EndLineOut0 + RingConstants.EndTextOut;

        /// <summary>Picture (and audio) visibility during the ending: 1 until 3.0 s, smootherstep to 0 at 7.0 s.</summary>
        public static float EndPicture(float te) => 1f - RingMath.Smootherstep(EndFadeStart, EndFadeEnd, te);

        /// <summary>End-line envelope: smoothstep in 8.5 -> 10.5 s, hold, smoothstep out 16.5 -> 18.5 s.</summary>
        public static float EndLine(float te) =>
            RingMath.Smoothstep(EndLineIn0, EndLineIn1, te) * (1f - RingMath.Smoothstep(EndLineOut0, EndTotal, te));
    }
}
