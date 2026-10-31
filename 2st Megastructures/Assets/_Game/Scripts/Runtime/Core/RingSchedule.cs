using UnityEngine;

namespace Megastructure
{
    /// <summary>All parameter targets for one instant (before slew limiting).</summary>
    public struct RingParams
    {
        // camera / post (14.2.5)
        public float Vfov, Ev, BloomThreshold, BloomIntensity, BloomScatter, BloomClamp, Saturation, Contrast, WbTemp, Vignette, Far;
        // fog (14.2.4)
        public float FogSigma, FogMistK, FogTintW, SpaceDark; public Color FogTint;
        public float FogAerialInv, FogHeightInv, AerialDesat;   // ADR-012 (_FogExtra)
        // key light (14.2.3)
        public float KeyIntensity; public Color KeyColor; public Vector3 KeyDir; public bool KeyShadows; public int Cascades; public float ShadowDistance;
        public float ShadowDepthBias, ShadowNormalBias;
        // fills
        public float ArchFill; public Color ArchFillColor; public Vector3 ArchFillDir;
        public float RimWash; public Color RimWashColor; public Vector3 RimWashDir;
        // ambient (already multiplied)
        public Color AmbSky, AmbEquator, AmbGround;
        // sky / phases
        public float SkyState, Night, StarV, ArchNightLift, SquaresAlpha, TerminatorDeg, SunsetU;
        public float OutsideSky, FarRim; public int Set;
        // emissive levels
        public float LampLevel, CityLevel, WindowLevel, SwarmGlint, TunnelSpill;
    }

    /// <summary>
    /// Pure functions of progress s and the phases (14.2.3-14.2.5, 14.6). Rule P: every parameter without its own
    /// window crossfades with smootherstep over s_b - 12 .. s_b + 12 around each beat boundary. No state: the
    /// AtmosphereDriver applies slew limiters (EV, WB, bloom, FOV) on top in live play.
    /// </summary>
    public static class RingSchedule
    {
        static readonly float[] B = RingConstants.BeatS;
        const float W = 12f;

        static float SS(float a, float b, float x) => RingMath.Smootherstep(a, b, x);

        // Cached delegates: C# 9 allocates a new delegate for every method-group argument, so every row is cached once.
        static readonly BeatFn FEv = EvBeat, FBloomThr = BloomThrBeat, FBloomInt = BloomIntBeat, FBloomScatter = BloomScatterBeat,
            FSat = SatBeat, FContrast = ContrastBeat, FWb = WbBeat, FVignette = VignetteBeat, FFogSigma = FogSigmaBeat, FMist = MistBeat,
            FFogTintW = FogTintWBeat, FSpaceDark = SpaceDarkBeat, FKeyInt = KeyIntBeat, FArchFill = ArchFillBeat, FRimWash = RimWashBeat,
            FAerialInv = AerialInvBeat, FHeightInv = HeightInvBeat, FAerialDesat = AerialDesatBeat;
        static readonly System.Func<int, float, float, Color> FFogTint = FogTintBeat, FKeyCol = KeyColBeat, FArchFillCol = ArchFillColBeat,
            FAmbSky = (b, x, uu) => AmbBeat(b, x, uu, 0), FAmbEq = (b, x, uu) => AmbBeat(b, x, uu, 1), FAmbGround = (b, x, uu) => AmbBeat(b, x, uu, 2);
        static readonly string[] DayTri = { "DayTriSky", "DayTriEq", "DayTriGround" };
        static readonly string[] NightTri = { "NightTriSky", "NightTriEq", "NightTriGround" };
        static float Rmp(float x, float a, float b, float v0, float v1) => RingMath.Ramp(x, a, b, v0, v1);

        /// <summary>Generic Rule P: f(beat, s, u) evaluated per beat, crossfaded at boundaries unless own[i] is true.</summary>
        delegate float BeatFn(int beat, float s, float u);

        static float RuleP(BeatFn f, float s, float u, bool[] own)
        {
            for (int i = 1; i <= 6; i++)
            {
                float sb = B[i];
                if (own != null && own[i]) continue;
                if (s > sb - W && s < sb + W)
                {
                    float w = SS(sb - W, sb + W, s);
                    return Mathf.Lerp(f(i - 1, s, u), f(i, s, u), w);
                }
            }
            return f(RingConstants.BeatAt(s), s, u);
        }

        static Color LerpC(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);

        static Color RuleP(System.Func<int, float, float, Color> f, float s, float u, bool[] own)
        {
            for (int i = 1; i <= 6; i++)
            {
                float sb = B[i];
                if (own != null && own[i]) continue;
                if (s > sb - W && s < sb + W)
                    return LerpC(f(i - 1, s, u), f(i, s, u), SS(sb - W, sb + W, s));
            }
            return f(RingConstants.BeatAt(s), s, u);
        }

        // own-window flags per boundary index 1..6 (index 0 unused)
        static readonly bool[] OwnNone = new bool[7];
        static readonly bool[] OwnEv = { false, true, false, true, false, true, true };
        static readonly bool[] OwnBloomI = { false, true, false, false, false, false, false };
        static readonly bool[] OwnFill = { false, false, false, false, false, true, false };

        // ---------------- individual parameters ----------------
        public static float Vfov(float s)
        {
            if (s < 10f) return 60f;
            if (s < 24f) return Rmp(s, 10f, 24f, 60f, 64f);
            if (s < 250f) return 64f;
            if (s < 276f) return Rmp(s, 250f, 276f, 64f, 66f);
            if (s < 399f) return 66f;
            if (s < 423f) return Rmp(s, 399f, 423f, 66f, 64f);
            if (s < 540f) return 64f;
            if (s < 558f) return Rmp(s, 540f, 558f, 64f, 60f);
            if (s < 594f) return 60f;
            if (s < 606f) return Rmp(s, 594f, 606f, 60f, 66f);
            if (s < 760f) return 66f;
            if (s < 774f) return Rmp(s, 760f, 774f, 66f, 60f);
            if (s < 894f) return 60f;
            return 60f + 6f * RingMath.Smootherstep01((s - 894f) / 18f);
        }

        static float EvBeat(int b, float s, float u)
        {
            switch (b)
            {
                case 0: return -0.4f;
                case 1: return Rmp(s, 9.5f, 24f, -0.4f, 0f);
                case 2: return -0.2f;
                case 3: return s < 423f ? Rmp(s, 399f, 423f, -0.2f, 0f) : Rmp(u, 0.30f, 0.90f, 0f, 1.6f);
                case 4: return Rmp(s, 594f, 774f, 1.6f, 1.4f);
                case 5: return s < 866f ? Rmp(s, 774f, 806f, 1.4f, 2.0f) : Rmp(s, 866f, 894f, 2.0f, -0.8f);
                default: return Rmp(s, 894f, 903f, -0.8f, -0.3f);
            }
        }

        public static float Ev(float s, float u)
        {
            // own windows: 1->2 (9.5-24) lives in beat 1's function, 3->4 (399-423) in beat 3's, 5->6 and 6->7 in beats 6/7
            if (s >= 9.5f && s < 24f) return EvBeat(1, s, u);
            if (s >= 399f && s < 411f) return EvBeat(3, s, u);
            return RuleP(FEv, s, u, OwnEv);
        }

        static float BloomThrBeat(int b, float s, float u) => (b == 4 || b == 5) ? 0.9f : 1.0f;

        static float BloomIntBeat(int b, float s, float u)
        {
            switch (b)
            {
                case 0: return 0.25f;
                case 1: return Rmp(s, 9.5f, 24f, 0.25f, 0.18f);
                case 2: return 0.22f;
                case 3: return Rmp(u, 0.30f, 0.90f, 0.30f, 0.40f);
                case 4: return 0.30f;
                case 5: return Rmp(s, 866f, 894f, 0.30f, 0.50f);
                default: return 0.45f;
            }
        }

        public static float BloomIntensity(float s, float u)
        {
            if (s >= 9.5f && s < 24f) return BloomIntBeat(1, s, u);
            return RuleP(FBloomInt, s, u, OwnBloomI);
        }

        static float BloomScatterBeat(int b, float s, float u)
        {
            switch (b) { case 0: return 0.60f; case 1: case 2: return 0.70f; case 3: case 4: case 5: return 0.75f; default: return 0.80f; }
        }

        static float SatBeat(int b, float s, float u)
        {
            switch (b) { case 0: return 4f; case 1: return 10f; case 2: return 8f; case 3: return Rmp(u, 0.30f, 0.90f, 10f, 2f); case 4: return 4f; case 5: return 0f; default: return 6f; }
        }

        static float ContrastBeat(int b, float s, float u)
        {
            switch (b) { case 0: return 6f; case 1: return 8f; case 2: return 10f; case 3: return 8f; case 4: return 6f; case 5: return 6f; default: return 12f; }
        }

        static float WbBeat(int b, float s, float u)
        {
            // Night white balance -16 / -12 of 14.2.5 halved (-8 / -6): with the blue night fills it turned the warm lamps
            // and bollard pools cold white and the meadow cyan (critics run04, S2); the blue mood stays.
            switch (b) { case 0: return 4f; case 1: return 10f; case 2: return 8f; case 3: return Rmp(u, 0.30f, 0.90f, 10f, -8f); case 4: return Rmp(s, 594f, 774f, -8f, -6f); case 5: return -6f; default: return 0f; }
        }

        static float VignetteBeat(int b, float s, float u)
        {
            switch (b) { case 0: return 0.22f; case 1: return 0.12f; case 2: return 0.14f; case 3: return Rmp(u, 0.30f, 0.90f, 0.16f, 0.22f); case 4: return 0.22f; case 5: return 0.28f; default: return 0.18f; }
        }

        static float FogSigmaBeat(int b, float s, float u)
        {
            if (b == 3) return Rmp(u, 0.30f, 0.90f, 2.5e-4f, 3.6e-4f);
            return RingConstants.FogSigma[b];
        }

        static float MistBeat(int b, float s, float u) => b <= 2 ? 0.8f : 0f;
        static float AerialInvBeat(int b, float s, float u) => b <= 5 ? 1f / RingConstants.FogAerialLength : 0f;
        static float HeightInvBeat(int b, float s, float u) => b <= 3 ? 1f / RingConstants.FogScaleHeight : 0f;
        static float AerialDesatBeat(int b, float s, float u) => b <= 5 ? RingConstants.AerialDesat : 0f;
        static float FogTintWBeat(int b, float s, float u) => b >= 5 ? 1f : 0f;
        static float SpaceDarkBeat(int b, float s, float u) => b == 6 ? 1f : 0f;
        static Color FogTintBeat(int b, float s, float u) => b == 6 ? RingPalette.Lin("SpaceBlack") : RingPalette.Lin("TunnelFog");

        static float KeyIntBeat(int b, float s, float u)
        {
            switch (b)
            {
                case 0: case 1: case 2: return 3.0f;
                case 3: return 3.0f * SunsetModel.KeyFactor(u);
                case 4: return s < 760f ? 0.09f : Rmp(s, 760f, 774f, 0.09f, 0f);   // own window: dark before the tunnel (no leak, shadows are off)
                // the outside key (L7, masked to flat x >= 233 by RingLighting) rises while the walker is deep in the tunnel,
                // so the deck seen through the mouth is already sunlit at the exit hold (KS11; critics run04, S2)
                case 5: return s < 834f ? 0f : Rmp(s, 834f, 866f, 0f, 3.4f);
                default: return 3.4f;
            }
        }

        static Color KeyColBeat(int b, float s, float u)
        {
            switch (b)
            {
                case 0: case 1: case 2: return RingPalette.Lin("KeyLight");
                case 3:
                {
                    // the key turns DuskKey as soon as the squares bite into the disc (V 1 -> 0.45), not only near totality
                    float v = SunsetModel.V(u);
                    return LerpC(RingPalette.Lin("DuskKey"), RingPalette.Lin("KeyLight"), RingMath.Smoothstep(0.45f, 1.0f, v));
                }
                case 4: return RingPalette.Lin("RimFill");
                case 5: return s < 834f ? RingPalette.Lin("RimFill") : RingPalette.Lin("StarOutside");
                default: return RingPalette.Lin("StarOutside");
            }
        }

        static float ArchFillBeat(int b, float s, float u)
        {
            switch (b)
            {
                case 0: return 0f;
                case 1: case 2: return 0.55f;
                case 3: return 0.14f + 0.41f * EclipseLight(u);
                case 4: return s < 760f ? 0.14f : Rmp(s, 760f, 774f, 0.14f, 0f);
                default: return 0f;
            }
        }

        static float RimWashBeat(int b, float s, float u) => b == 4 ? (s < 760f ? 0.35f : Rmp(s, 760f, 774f, 0.35f, 0f)) : 0f;

        // Night fill = the light of the sunlit arch overhead: halfway between RimFill and the pale ArchFill (pure RimFill
        // painted the wall and the meadow a saturated royal blue; critics run04, S1).
        static Color NightFill => LerpC(RingPalette.Lin("RimFill"), RingPalette.Lin("ArchFill"), 0.5f);

        static Color ArchFillColBeat(int b, float s, float u)
        {
            if (b == 3) return LerpC(RingPalette.Lin("ArchFill"), NightFill, RingMath.Smootherstep(0.30f, 0.90f, u));
            return b >= 4 ? NightFill : RingPalette.Lin("ArchFill");
        }

        static Color Mul(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, 1f);

        /// <summary>Sky light of beat 4 (ambient and arch fill, 0..1): the near sky is lit by the star, so it dims with the
        /// eclipse (V^3), not on the later u 0.30-0.90 window: at KS05 (V 0.585) 0.20, so the pavilion reads as a silhouette
        /// against the dusk glow (14.5.4 acceptance 2); 1 before the squares touch the disc, 0 from totality on.</summary>
        static float EclipseLight(float u) { float v = SunsetModel.V(u); return v * v * v; }

        static float AmbMulBeat(int b, float s, float u)
        {
            switch (b) { case 0: return 0.25f; case 1: return 1.0f; case 2: return 0.9f; case 3: return 0.12f + 0.88f * EclipseLight(u); case 4: return 0.12f; case 5: return 0.03f; default: return 0.08f; }
        }

        static Color AmbBeat(int b, float s, float u, int which)
        {
            var day = DayTri; var night = NightTri;
            Color c;
            if (b <= 2) c = RingPalette.Lin(day[which]);
            else if (b == 3) c = LerpC(RingPalette.Lin(day[which]), RingPalette.Lin(night[which]), RingMath.Smootherstep(0.30f, 0.90f, u));
            else if (b <= 5) c = RingPalette.Lin(night[which]);
            else c = RingPalette.Lin("SpaceBounce");
            return Mul(c, AmbMulBeat(b, s, u));
        }

        /// <summary>World set for s with the swap points of 14.6 (hysteresis is applied by the BeatDirector).</summary>
        public static int SetAt(float s)
        {
            if (s < RingConstants.SwapTriggerS) return 0;
            if (s < 810f) return 1;
            if (s < 894f) return 2;
            return 3;
        }

        /// <summary>Evaluates every target. u = sunset phase (capture: (s - 411) / 105).</summary>
        public static RingParams Evaluate(float sIn, float u, int setHyst)
        {
            float s = sIn;
            var p = new RingParams();
            float uc = Mathf.Clamp(u, -2f, 1f);
            p.SunsetU = u;
            p.Vfov = Vfov(s);
            // (walk-back after the sunset has started: AtmosphereDriver.BlendTowardSunset moves the light rows toward beat 4)
            p.Ev = Ev(s, uc);
            p.BloomThreshold = RuleP(FBloomThr, s, uc, OwnNone);
            p.BloomIntensity = BloomIntensity(s, uc);
            p.BloomScatter = RuleP(FBloomScatter, s, uc, OwnNone);
            p.BloomClamp = 40f;
            p.Saturation = RuleP(FSat, s, uc, OwnNone);
            p.Contrast = RuleP(FContrast, s, uc, OwnNone);
            p.WbTemp = RuleP(FWb, s, uc, OwnNone);
            p.Vignette = RuleP(FVignette, s, uc, OwnNone);
            p.Far = sIn >= RingConstants.FarOutsideFromS ? RingConstants.FarOutside : RingConstants.FarDefault;

            p.FogSigma = RuleP(FFogSigma, s, uc, OwnNone);
            p.FogMistK = RuleP(FMist, s, uc, OwnNone);
            p.FogTintW = RuleP(FFogTintW, s, uc, OwnNone);
            p.FogTint = RuleP(FFogTint, s, uc, OwnNone);
            p.SpaceDark = RuleP(FSpaceDark, s, uc, OwnNone);
            p.FogAerialInv = RuleP(FAerialInv, s, uc, OwnNone);
            p.FogHeightInv = RuleP(FHeightInv, s, uc, OwnNone);
            p.AerialDesat = RuleP(FAerialDesat, s, uc, OwnNone);

            // key: direction L until the middle of the tunnel (key 0 there), L7 afterwards
            p.KeyIntensity = RuleP(FKeyInt, s, uc, OwnFill);
            p.KeyIntensity *= 1f - 0.2f * RingState.ArkCue;   // beat-3 felt cue (envelope from the ProgressDirector)
            p.KeyColor = RuleP(FKeyCol, s, uc, OwnNone);
            bool outside = sIn >= 834f;
            p.KeyDir = outside ? RingConstants.StarL7 : RingConstants.StarL;
            p.KeyShadows = sIn < SwapS2 || sIn >= 834f;
            p.Cascades = sIn >= 834f ? 3 : 4;
            // ADR-010: 220 m from the start (the 30 m beat-1 distance would pop every valley shadow at the switch)
            p.ShadowDistance = sIn >= 834f ? 90f : 220f;
            p.ShadowDepthBias = sIn >= 834f ? 0.1f : 0.04f;
            p.ShadowNormalBias = sIn >= 834f ? 1.0f : 0.8f;

            p.ArchFill = RuleP(FArchFill, s, uc, OwnFill);
            p.ArchFillColor = RuleP(FArchFillCol, s, uc, OwnNone);
            p.ArchFillDir = RingConstants.DirFromAzEl(112f, 74f);
            p.RimWash = RuleP(FRimWash, s, uc, OwnFill);
            p.RimWashColor = NightFill;
            p.RimWashDir = RingConstants.DirFromAzEl(-90f, 30f);

            p.AmbSky = RuleP(FAmbSky, s, uc, OwnNone);
            p.AmbEquator = RuleP(FAmbEq, s, uc, OwnNone);
            p.AmbGround = RuleP(FAmbGround, s, uc, OwnNone);

            bool night = s >= RingConstants.BeatS[4];
            p.SkyState = night ? 2f : SunsetModel.SkyState(uc);
            p.Night = night ? 1f : SunsetModel.Night(uc);
            p.StarV = night ? 0f : SunsetModel.V(uc);
            p.SquaresAlpha = night ? 1f : SunsetModel.SquaresAlpha(uc);
            p.TerminatorDeg = night ? RingConstants.TerminatorMax : SunsetModel.TerminatorDeg(uc);
            p.ArchNightLift = 0.5f * p.Night;

            p.Set = setHyst;
            p.OutsideSky = s >= 778f ? 1f : 0f;
            p.FarRim = setHyst >= 1 ? 1f : 0f;

            p.LampLevel = night ? 3.0f : 3.0f * RingMath.Smootherstep(0.30f, 0.60f, uc);
            p.CityLevel = p.Night;
            p.WindowLevel = 0.4f + 0.6f * p.Night;
            p.SwarmGlint = 0.35f + 0.65f * RingMath.Smootherstep01((s - 894f) / 18f);
            p.TunnelSpill = RingMath.Smootherstep(854f, 888f, s);
            return p;
        }

        // Shadows switch off inside the sealed underpass (key 0.09 from there on) and on again in the tunnel (key 0).
        const float SwapS2 = RingConstants.SwapTriggerS;
    }
}
