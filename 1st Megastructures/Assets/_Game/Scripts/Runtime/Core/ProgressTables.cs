using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// One row of the per-beat look table of MASTER_PROMPT 14.2 ("Fog, post, FOV per beat" plus the ambient column of the
    /// lighting rig). Colours are authored in sRGB; the *Lin fields are the same colours converted once to linear.
    /// </summary>
    public struct LookRow
    {
        public string Name;
        public float D50;              // distance of 50 % fog (m); sigma = 0.8326 / D50
        public float MDown;            // downward fog multiplier m_down
        public Color FogDown, FogHoriz, FogUp;            // sRGB (as authored)
        public Color FogDownLin, FogHorizLin, FogUpLin;   // linear
        public float EV;               // ColorAdjustments.postExposure
        public float BloomThreshold, BloomIntensity, BloomScatter;
        public float Contrast, Saturation, Temperature;   // ColorAdjustments contrast / saturation, WhiteBalance temperature
        public float Vignette;
        public float Fov;              // vertical FOV (deg)
        public float AmbientScale;     // trilight = fog colours x this (lighting rig "Ambient" column)
    }

    /// <summary>One state of the single directional "Key" light (14.2 lighting rig). Yaw/pitch = direction of travel.</summary>
    public struct KeyRow
    {
        public string Name;
        public bool On;
        public float Yaw, Pitch;
        public Color Colour;           // sRGB (Light.color expects the authored colour)
        public float Intensity;
        public float ShadowDistance;   // URP main-light shadow distance while this row is active (m)
        public float ScatterAmt;       // domain-0 HG scatter amount at full key intensity (_AbScatterAmt)
        /// <summary>
        /// Haze skylight gain (_AbHazeSky = linear key colour x this x key fraction; AbyssLighting.hlsl AbHazeSkylight): the sun
        /// scattered by the shaft haze lights domain-0 surfaces as a soft directional ambient, ((1 + N.L) / 2)^2, faded with the
        /// same shaft-depth falloff as the Key. Up-facing rib and belt tops and the sun-side flanks of the columns catch it; the
        /// camera-facing (backlit) walls get about 13 % of it (R1-06). Not shadowed (it is sky light), so no cascade edge. 0 = off.
        /// </summary>
        public float HazeSky;
        /// <summary>
        /// Shaft-depth falloff of the main light for domain-0 materials (_AbKeyDepth, AbyssLighting.hlsl): full light above world
        /// y = DepthFull, smoothstep to zero at y = DepthZero (Part 8.5 analytic term beyond the cascades, 14.7.2 bottomless
        /// shaft). Off (no falloff) when DepthOn is false: the beat-6 fan light and the beat-7 sun.
        /// </summary>
        public bool DepthOn;
        public float DepthFull, DepthZero;
        /// <summary>
        /// Slow second depth falloff on the shaft ring top chamfers only (_AbKeyChamfer, AbyssLighting.hlsl AbRingChamferFactor;
        /// round 2, R2-06 / R2-07): Key fraction ChamferAmp at and above ChamferY0, decaying with the e-folding length
        /// ChamferLength to exactly 0 at ChamferZero (<see cref="ProgressTables.ChamferFactor"/>). The chamfers take
        /// max(depth falloff, this). ChamferAmp 0 = off.
        /// </summary>
        public float ChamferAmp, ChamferY0, ChamferLength, ChamferZero;

        /// <summary>True when direction, colour, shadow distance and depth falloffs match (only the intensity differs), so no dip to 0 is needed.</summary>
        public bool SameLook(in KeyRow o)
        {
            return On && o.On && Yaw == o.Yaw && Pitch == o.Pitch && Colour == o.Colour && ShadowDistance == o.ShadowDistance
                   && DepthOn == o.DepthOn && DepthFull == o.DepthFull && DepthZero == o.DepthZero
                   && ChamferAmp == o.ChamferAmp && ChamferY0 == o.ChamferY0 && ChamferLength == o.ChamferLength && ChamferZero == o.ChamferZero;
        }

        /// <summary>The _AbKeyDepth global of this row: (y full, y zero, 1, 0), or zero (disabled).</summary>
        public Vector4 DepthVector => On && DepthOn ? new Vector4(DepthFull, DepthZero, 1f, 0f) : Vector4.zero;

        /// <summary>The _AbKeyChamfer global of this row: (amplitude, y0, length, y zero), or zero (off).</summary>
        public Vector4 ChamferVector => On && DepthOn && ChamferAmp > 0f ? new Vector4(ChamferAmp, ChamferY0, ChamferLength, ChamferZero) : Vector4.zero;
    }

    /// <summary>Knot of a row curve: at extended progress E the channel equals row Row; between knots it eases (smootherstep).</summary>
    public struct RowKnot
    {
        public float E; public int Row;
        public RowKnot(float e, int row) { E = e; Row = row; }
    }

    /// <summary>Knot of a scalar curve (smootherstep between knots, held before the first and after the last).</summary>
    public struct FloatKnot
    {
        public float E; public float V;
        public FloatKnot(float e, float v) { E = e; V = v; }
    }

    /// <summary>Knot of a step function: from E on the value is Value (until the next knot).</summary>
    public struct StepKnot
    {
        public float E; public float Value;
        public StepKnot(float e, float value) { E = e; Value = value; }
    }

    /// <summary>Every continuous value ProgressDirector writes, evaluated at one extended progress E.</summary>
    public struct ProgressSample
    {
        // domain 0 (Shaft) fog / sky, linear colours
        public float FogSigma, FogDownMul;
        public Color FogDown, FogHoriz, FogUp;
        // trilight ambient, linear, already multiplied by the ambient scale
        public Color AmbUp, AmbMid, AmbDown;
        // post (Volume overrides)
        public float EV, Contrast, Saturation, Temperature, Vignette;
        public float BloomThreshold, BloomIntensity, BloomScatter;
        // camera
        public float Fov, FarClip;
        // misc globals
        public float SkyBlend, BeamFade;
        public int KeyRow;
    }

    /// <summary>
    /// The per-beat tables of MASTER_PROMPT 14.2 and the transition windows of 14.6 as data, plus a pure evaluator.
    ///
    /// Every blend is keyed on the extended progress E (CONTRACTS section 4): E = d before the ride, D_LIFT + ride seconds during
    /// the dwell and ride, d + 121 after it. So "d 420-440" is E 420-440, "ride seconds 110-120" is E 569.54-579.54 and
    /// "d 640-668" is E 761-789. Beat 7 windows given in z (the tunnel and balcony run along x = -45 toward -Z) are converted
    /// with d = 728.54 - z (<see cref="DAtZ7"/>).
    ///
    /// Rate limits (14.6) hold by construction of the windows; ProgressTests asserts them: exposure at most 0.35 EV/s
    /// everywhere at the brisk 2.5 m/s and during the ride (smootherstep peak = 1.875 x the mean rate), fog sigma mean rate
    /// at most 15 % of the blend's change per second at 2.5 m/s (every fog window is at least 16.7 m or 6.67 ride seconds),
    /// FOV mean rate at most 1 deg/s per window.
    /// </summary>
    public static class ProgressTables
    {
        // ---------------------------------------------------------------- extended progress helpers
        /// <summary>E span of the dwell/ride/gate segment: E = d + 121 after the ride (120 s ride + 1 s gate delay).</summary>
        public const float RIDE_E_SPAN = AbyssConst.LIFT_DURATION + AbyssConst.LIFT_GATE_DELAY;

        /// <summary>E of a path distance before the ride.</summary>
        public static float EPre(float d) => d;

        /// <summary>E of a ride second (0 = departure).</summary>
        public static float ERide(float rideSeconds) => AbyssConst.D_LIFT + rideSeconds;

        /// <summary>E of a path distance after the ride.</summary>
        public static float EPost(float d) => d + RIDE_E_SPAN;

        /// <summary>Path distance d of a beat-7 z coordinate on the tunnel/balcony/pier axis (W15 z = 60 at d 668.54, Face z = 0 at d 728.54).</summary>
        public static float DAtZ7(float z) => AbyssConst.WD[16] - z;

        /// <summary>True for E inside the dwell/ride/gate segment, where E advances at 1 per ride second instead of with walking.</summary>
        public static bool IsRideE(float e) => e > AbyssConst.D_LIFT && e < AbyssConst.D_LIFT + RIDE_E_SPAN;

        /// <summary>dE/dt for a walker at the given speed (1 per second during the ride, where d is frozen).</summary>
        public static float ERate(float e, float walkSpeed) => IsRideE(e) ? 1f : walkSpeed;

        /// <summary>Total E at the goal door.</summary>
        public static float EEnd => EPost(AbyssConst.POLYLINE_LENGTH);

        // ---------------------------------------------------------------- look rows (14.2), index 0..5 = beats 1..6
        public const int ROW_B1 = 0, ROW_B2 = 1, ROW_B3 = 2, ROW_B4 = 3, ROW_B5 = 4, ROW_B6 = 5, ROW_TUNNEL = 6, ROW_OUTSIDE = 7;

        // Look-dev calibration of the shaft rows (beats 2, 3, 5; global slot, KS2/KS3/KS5 against 14.10 and 14.7.2):
        // * Contrast 0 instead of the 14.2 +12. URP's ColorAdjustments contrast works in LogC around ACEScc mid grey; in the
        //   LogC linear toe (below 0.0114 linear) it maps v -> c v - (c - 1) 0.0605, so +12 subtracts 0.0073 and clamps every
        //   channel below 0.0065 linear to 0. FOG_DOWN #070B10 (max linear 0.0052) rendered as sRGB (0, 1, 5) instead of the
        //   14.7.2 nadir luma 0.041 +/- 0.008 ("no pure black", 14.2), and the ambient-lit side of C1 read as a black void.
        //   At 0 the grading passes FOG_DOWN through (Neutral toe slope 1.06), so the abyss displays as #070B10.
        // * EV 2 / 3 / 5 = -0.25 / -0.3 / -0.2 (14.2: 0.0 / -0.1 / -0.1): with the toe no longer clipped the frames are
        //   brighter; KS2b (the steel deck's sky reflection, not the sun, dominates it) needs -0.25 to stay under mean 0.13
        //   in 16:10, and the nadir still reads FOG_DOWN (measured sRGB (7, 10, 15), luma 0.038, target 0.041 +/- 0.008).
        // * Ambient x1.7 in beats 2 and 3 (14.2: x1.0): C1 dead ahead in KS2a (102 m, about 3 % fog) is lit only by the
        //   trilight; it now reads as a pale-dark cylinder (luma 0.063) against the fogged far wall (0.14), not a black void.
        // * Key intensity 0.65 / 0.75 / 1.0 (14.2: 0.9 / 1.0 / 1.1) and the shaft-depth falloff of the KeyRows below.
        //   Look-dev run global_r05: KS2a 0.091, KS2b 0.116 (p99 0.633), KS3 0.120, KS5a 0.125, KS5c 0.093.
        // * Lead (Part 1.6.2): the same toe clamp applies to every row, so contrast is 0 in ALL rows (14.2 +8..+14 would crush
        //   everything below about 0.006 linear to pure black, which 14.2 forbids); beat 6 EV -0.3 (14.2 -0.2) compensates the lifted toe.
        // * Round 1 (R1-06): ambient back to the 14.2 x1.0 in beats 2 and 3 (look-dev had x1.7). The omni ambient that look-dev
        //   added now comes as the directional haze skylight of the shaft sun (SHAFT_HAZE_SKY): the backlit, camera-facing walls
        //   and the front of C1 keep the same light (C1 about 0.065 in KS2a), while up-facing ledges and the limbs of C1 and C3
        //   (rim toward the sun azimuth) read; the ground term is floored at AmbGroundMin x equator.

        /// <summary>Ambient scale of the shaft rows of beats 2 and 3 (see the calibration notes above).</summary>
        public const float AMB_SHAFT = 1.0f;

        public static readonly LookRow[] Rows =
        {
            //   name        D50   m_down  FOG_DOWN  FOG_HORIZ FOG_UP    EV     bloom thr/int/scat   con  sat   temp  vig   FOV  amb
            Row("1 gallery",  45f,  0f,    0x0C1013, 0x14191D, 0x14191D, +0.4f, 1.0f, 0.20f, 0.60f,  0f, -22f, -6f,  0.18f, 62f, 0.35f),
            Row("2 bridge",  520f,  1.5f,  0x070B10, 0x1F2C37, 0x2B3C4D, -0.25f, 1.0f, 0.30f, 0.70f, 0f, -24f, -10f, 0.16f, 66f, AMB_SHAFT),
            Row("3 ledge",   450f,  1.5f,  0x070B10, 0x1F2C37, 0x2B3C4D, -0.3f, 1.0f, 0.30f, 0.70f,  0f, -22f, -8f,  0.16f, 64f, AMB_SHAFT),
            Row("4 hall",    160f,  0.3f,  0x0A0D10, 0x1B232A, 0x252F38, +0.3f, 1.0f, 0.35f, 0.70f,  0f, -18f, -4f,  0.20f, 62f, 0.7f),
            Row("5 lift",    380f,  1.2f,  0x070B10, 0x22303B, 0x2E4052, -0.2f, 1.0f, 0.35f, 0.72f,  0f, -22f, -10f, 0.14f, 66f, 1.0f),
            Row("6 fan",     300f,  0.8f,  0x080C11, 0x2B3B49, 0x4C6578, -0.3f, 0.9f, 0.45f, 0.75f,  0f, -18f, -12f, 0.14f, 66f, 1.0f),
            Row("7 tunnel", AbyssPalette.TUNNEL_D50, 0f, AbyssPalette.TUNNEL_FOG, AbyssPalette.TUNNEL_FOG, AbyssPalette.TUNNEL_FOG,
                +0.9f, 1.0f, 0.35f, 0.70f, 0f, -16f, -8f, 0.20f, 62f, 1.2f),
            Row("7 outside", AbyssPalette.EXT_D50, AbyssPalette.EXT_M_DOWN, AbyssPalette.EXT_FOG_DOWN, AbyssPalette.OUT_HORIZ, AbyssPalette.OUT_ZENITH,
                -0.5f, 1.0f, 0.40f, 0.75f, 0f, -14f, -6f, 0.10f, 68f, 1.2f),
        };

        // ---------------------------------------------------------------- key light rows (14.2 lighting rig)
        public const int KEY_OFF = 0, KEY_SUN2 = 1, KEY_SUN3 = 2, KEY_SUN5 = 3, KEY_FAN6 = 4, KEY_EXT7 = 5;

        /// <summary>Gain from the linear key colour to the domain-0 SCATTER colour (_AbScatterColor); tuning value.</summary>
        public const float SCATTER_GAIN = 0.25f;

        /// <summary>Key-light ramp to 0 and back up (14.2: "ramps to 0 over 3 s ... ramps up over 3 s").</summary>
        public const float KEY_RAMP_SECONDS = 3f;

        /// <summary>
        /// Shaft-depth falloff of the shaft sun (beats 2 and 3, _AbKeyDepth): full light down to the bridge truss (y -6), zero
        /// by y -160 (look-dev; round 1 fix R1-06, was -80). The sun reaches the -30 belt top and the -90 and -150 rib tops, so
        /// the 60 m ring rhythm reads below the deck in KS2a as lit ledges fading into the fog (rib -90 at 300 m: about 0.3 of
        /// the key, +0.05 luma over the wall); every ring below -160 stays unlit, so in KS2b (side walls seen from about y -210
        /// down) no lit edge appears below the first rib line (14.5.2 acceptance) and the abyss keeps no lit floor (14.7.2).
        /// </summary>
        public const float SUN_DEPTH_FULL = -6f, SUN_DEPTH_ZERO = -160f;

        /// <summary>
        /// Second, slow depth falloff of the shaft sun on the ring top chamfers only (round 2, R2-06, shared with R2-07). In
        /// KS2b both 30 % side strips were a flat 0.036 field (the plank over black cloth): every ring that enters the frame lies
        /// below -160 and was unlit. The 45-degree top chamfers of ribs and belts (AbyssLighting.hlsl AbRingChamferFactor
        /// selects them by normal, inward direction and hexagonal radius) keep a Key fraction of 0.20 on the four ring levels
        /// the KS2b strips see (the -150 rib on the near flat, the -210 rib, the -270 belt and the -330 rib on the side flats),
        /// then fall to 0.09 at -390, 0.02 at -450 and 0 from -470, so 4 nested ring lines descend toward the nadir, each
        /// dimmer and closer to FOG_DOWN through the shaft fog alone (the deeper lines are 300-450 m from the eye and 1-2 px
        /// thin), and the 5th (-390) only glimmers. Walls, ring tops and everything else keep SUN_DEPTH_ZERO, so the abyss
        /// keeps no lit floor (14.7.2). Calibration (Part 1.6.2): the critique's 0.35 at -150 put the near-flat -150 rib line
        /// (168 m below the KS2b eye, the least fogged) at luma 0.135 with a 0.031 edge (global_r21), over the KS2b strip
        /// limits (max 0.12, edge 0.03), so the amplitude is 0.20 (line 0.096, edge 0.023). With the critique's decay shape
        /// at that amplitude (0.14 / 0.10 / 0.06 at -210 / -270 / -330; global_r23, lead_b01) the source lines read 25 / 18.5 /
        /// 14.5 / 11.7 sRGB levels over a 10.7 field, but after an x264 CRF 20 encode the -330 line was 1 level above the
        /// field: only 3 lines survived. Holding the amplitude down to -330 keeps every line under the strip limits and lets
        /// the fog do the stepping (calibration global_r31). The same lines descend below deck level on the far walls of
        /// KS2a (R2-07 (2)).
        /// </summary>
        public const float SUN_CHAMFER_AMP = 0.20f, SUN_CHAMFER_Y0 = -330f, SUN_CHAMFER_LENGTH = 120f, SUN_CHAMFER_ZERO = -470f;

        /// <summary>Ring band of AbRingChamferFactor (_AbRingBand z, w): hexagonal radius of the belt / rib chamfers (330-346) with margin.</summary>
        public const float RING_BAND_INNER = AbyssConst.BELT_INNER_APOTHEM - 1f, RING_BAND_OUTER = AbyssConst.RIB_INNER_APOTHEM + 4f;

        /// <summary>_AbRingBand: (shaft axis x, z, inner hex radius, outer hex radius).</summary>
        public static Vector4 RingBand => new Vector4(AbyssConst.SHAFT_CENTRE.x, AbyssConst.SHAFT_CENTRE.y, RING_BAND_INNER, RING_BAND_OUTER);

        /// <summary>
        /// CPU mirror of the chamfer falloff of AbRingChamferFactor (without the geometric selection): A (e(y) - e0) / (1 - e0),
        /// e(y) = exp(min(y - y0, 0) / L), e0 = e(yZero); 0 when the row has none.
        /// </summary>
        public static float ChamferFactor(in KeyRow k, float y)
        {
            Vector4 c = k.ChamferVector;
            if (c.x <= 0f) return 0f;
            float L = Mathf.Max(c.z, 1f);
            float e0 = Mathf.Exp(Mathf.Min(c.w - c.y, 0f) / L);
            float e = Mathf.Exp(Mathf.Min(y - c.y, 0f) / L);
            return c.x * Mathf.Clamp01((e - e0) / Mathf.Max(1f - e0, 1e-4f));
        }

        /// <summary>
        /// Domain-0 ambient ground floor per look row (R1-14): the trilight ground term is at least this fraction of the equator
        /// term, per channel, so down-facing faces (rib and belt undersides, cable trays, collars, beam soffits) read as dark grey
        /// instead of the pure-black cut-outs that FOG_DOWN x scale gave (about 1e-4 linear after albedo). 0.5 where the critics
        /// found black undersides against open fog (gallery, bridge, ledge, the Exterior row = AB_EXT_GROUND_MIN of
        /// AbyssLighting.hlsl); lower in the hall, the ride and the fan, whose key frames look up at many block and frame
        /// undersides and sit near their mean-luma ceilings (KS6 0.22). The radiance floor (AbRadianceFloor) keeps every row off
        /// pure black regardless.
        /// </summary>
        public static readonly float[] AmbGroundMin = { 0.5f, 0.5f, 0.5f, 0.35f, 0.25f, 0.2f, 0.5f, 0.6f };

        /// <summary>
        /// Shaft-depth falloff of the beat-5 sun (the ride descends from y -7 to -487): the light fades over the whole city, so
        /// the deck darkens with depth (the descent) while the pylon spots and the ambient keep it readable (look-dev).
        /// </summary>
        public const float SUN5_DEPTH_FULL = -30f, SUN5_DEPTH_ZERO = -700f;

        /// <summary>
        /// Shaft-sun scatter and haze skylight of beats 2 and 3 (R1-06, round 1). The Key at yaw 205 arrives from ahead of a
        /// walker heading +Z, so every camera-facing wall is backlit and the depth layers (C1 102 m, C3 480 m, far wall 590-640 m)
        /// separate only through the fog. SHAFT_SCATTER makes the haze glow toward the sun (contre-jour), so the more fogged a
        /// layer is, the lighter it reads; the lobe is weighted to zero below v.y -0.5 (AbyssFog.hlsl), so the nadir stays
        /// FOG_DOWN (14.7.2). SHAFT_HAZE_SKY is the directional skylight of that glow (see KeyRow.HazeSky). Tuning values (14.2
        /// gives no scatter amount for the shaft sun; 0.10 before).
        /// </summary>
        public const float SHAFT_SCATTER = 0.35f, SHAFT_HAZE_SKY = 0.18f;

        public static readonly KeyRow[] KeyRows =
        {
            new KeyRow { Name = "off", On = false },
            WithChamfer(Key("sun 2", 205f, -72f, 0xC4D6E8, 0.65f, 120f, SHAFT_SCATTER, SHAFT_HAZE_SKY, true, SUN_DEPTH_FULL, SUN_DEPTH_ZERO)),
            WithChamfer(Key("sun 3", 205f, -72f, 0xC4D6E8, 0.75f, 120f, SHAFT_SCATTER, SHAFT_HAZE_SKY, true, SUN_DEPTH_FULL, SUN_DEPTH_ZERO)),
            Key("sun 5", 205f, -72f, 0xC4D6E8, 1.0f, 200f, 0.10f, 0f, true, SUN5_DEPTH_FULL, SUN5_DEPTH_ZERO),
            Key("fan 6",   0f, -18f, 0xC9DDF0, 1.4f, 260f, 0.35f, 0f, false, 0f, 0f),   // 14.5.6: "scatter 0.35 toward -Z" (toward the light); no depth falloff
            Key("ext 7",  20f, -40f, 0xD6E2EC, 1.5f, 150f, 0.0f, 0f, false, 0f, 0f),    // the Exterior domain has its own capped HG lobe (shader constant)
        };

        /// <summary>Unit vector toward the beat-7 sun (_AbExtSunDir): the reverse of its direction of travel.</summary>
        public static Vector3 ExtSunDir => -AbyssConst.Dir(KeyRows[KEY_EXT7].Yaw, KeyRows[KEY_EXT7].Pitch);

        // ---------------------------------------------------------------- transition windows (14.6), keyed on E
        /// <summary>Domain-0 fog: D50 (geometric sigma blend), m_down and the three fog colours.</summary>
        public static readonly RowKnot[] FogKnots =
        {
            new RowKnot(52f, ROW_B1), new RowKnot(72f, ROW_B2),                     // 1 to 2: D50 45 to 520 over d 52-72
            new RowKnot(205f, ROW_B2), new RowKnot(225f, ROW_B3),                   // 2 to 3: 520 to 450 over d 205-225
            new RowKnot(285f, ROW_B3), new RowKnot(310f, ROW_B4),                   // 3 to 4: 450 to 160 over d 285-310
            new RowKnot(420f, ROW_B4), new RowKnot(440f, ROW_B5),                   // 4 to 5: 160 to 380 over d 420-440
            new RowKnot(ERide(110f), ROW_B5), new RowKnot(ERide(120f), ROW_B6),     // 5 to 6: 380 to 300 over ride s 110-120
            // 6 to 7: none - tunnel and exterior materials use their constant domains; domain 0 keeps the beat-6 set.
        };

        /// <summary>Exposure window: EV and the other post values (contrast, saturation, temperature, vignette, bloom threshold/scatter).</summary>
        public static readonly RowKnot[] ExposureKnots =
        {
            new RowKnot(52f, ROW_B1), new RowKnot(62f, ROW_B2),                     // +0.4 to 0.0 over d 52-62
            new RowKnot(205f, ROW_B2), new RowKnot(225f, ROW_B3),                   // 0.0 to -0.1 over d 205-225
            new RowKnot(285f, ROW_B3), new RowKnot(310f, ROW_B4),                   // -0.1 to +0.3 over d 285-310
            new RowKnot(AbyssConst.WD[10] - 12f, ROW_B4), new RowKnot(AbyssConst.WD[10], ROW_B5), // +0.3 to -0.1 over the last 12 m of the hall
            new RowKnot(ERide(110f), ROW_B5), new RowKnot(ERide(120f), ROW_B6),     // -0.1 to -0.2 over ride s 110-120
            new RowKnot(EPost(640f), ROW_B6), new RowKnot(EPost(668f), ROW_TUNNEL), // -0.2 to +0.9 over d 640-668
            new RowKnot(EPost(DAtZ7(25f)), ROW_TUNNEL), new RowKnot(EPost(DAtZ7(0f)), ROW_OUTSIDE), // +0.9 to -0.5 over z 25 to 0
        };

        /// <summary>Vertical FOV. Windows chosen so that every mean rate is at most 1 deg/s at the brisk speed (Part 9).</summary>
        public static readonly RowKnot[] FovKnots =
        {
            new RowKnot(52f, ROW_B1), new RowKnot(72f, ROW_B2),                     // 62 to 66 over 20 m (0.5 deg/s brisk)
            new RowKnot(205f, ROW_B2), new RowKnot(225f, ROW_B3),                   // 66 to 64
            new RowKnot(285f, ROW_B3), new RowKnot(310f, ROW_B4),                   // 64 to 62
            new RowKnot(420f, ROW_B4), new RowKnot(440f, ROW_B5),                   // 62 to 66 (the shaft bursts into view)
            new RowKnot(EPost(640f), ROW_B5), new RowKnot(EPost(668f), ROW_TUNNEL), // 66 to 62 into the tunnel (beats 5 and 6 are both 66)
            new RowKnot(EPost(DAtZ7(15f)), ROW_TUNNEL), new RowKnot(EPost(DAtZ7(0f)), ROW_OUTSIDE), // 62 to 68 over the last 15 m of the tunnel
        };

        /// <summary>Trilight ambient from the beat fog colours x the ambient scale. Follows the fog windows, then the beat-7 exposure windows.</summary>
        public static readonly RowKnot[] AmbientKnots =
        {
            new RowKnot(52f, ROW_B1), new RowKnot(72f, ROW_B2),
            new RowKnot(205f, ROW_B2), new RowKnot(225f, ROW_B3),
            new RowKnot(285f, ROW_B3), new RowKnot(310f, ROW_B4),
            new RowKnot(420f, ROW_B4), new RowKnot(440f, ROW_B5),
            new RowKnot(ERide(110f), ROW_B5), new RowKnot(ERide(120f), ROW_B6),
            new RowKnot(EPost(640f), ROW_B6), new RowKnot(EPost(668f), ROW_TUNNEL),
            new RowKnot(EPost(DAtZ7(25f)), ROW_TUNNEL), new RowKnot(EPost(DAtZ7(0f)), ROW_OUTSIDE),
        };

        /// <summary>Bloom intensity: the exposure windows, except at the mouth where it peaks at 0.60 at z = 3 and settles to 0.40 by z = -8 (14.7.7).</summary>
        public const float BLOOM_MOUTH_PEAK = 0.60f;
        public static readonly FloatKnot[] BloomIntensityKnots = BuildBloomKnots();

        /// <summary>_AbSkyBlend: 0 = Shaft sky below d 640, smootherstep to 1 (Exterior sky) at d 668.54 (14.2).</summary>
        public static readonly FloatKnot[] SkyBlendKnots =
        {
            new FloatKnot(EPost(AbyssConst.D_EXTERIOR_ON), 0f), new FloatKnot(EPost(AbyssConst.D_SKY_BLEND_END), 1f),
        };

        /// <summary>_AbBeamFade: the fan beams fade in over ride seconds 0-10 (14.6, 4 to 5) and stay.</summary>
        public static readonly FloatKnot[] BeamFadeKnots =
        {
            new FloatKnot(ERide(0f), 0f), new FloatKnot(ERide(10f), 1f),
        };

        /// <summary>
        /// Key-light row by E (a step function; KeyLightRig turns each change into the 3 s dip-and-rise of 14.2). Changes sit at
        /// the start of the exposure windows: on at d 52, intensity 0.9 to 1.0 at d 205, off for the hall at d 285, the beat-5 sun
        /// 12 m before the hall exit, the fan light at ride second 110, off for the tunnel at d 640, the outside sun at z = 25.
        /// </summary>
        public static readonly StepKnot[] KeyKnots =
        {
            new StepKnot(0f, KEY_OFF),
            new StepKnot(52f, KEY_SUN2),
            new StepKnot(205f, KEY_SUN3),
            new StepKnot(285f, KEY_OFF),
            new StepKnot(AbyssConst.WD[10] - 12f, KEY_SUN5),
            new StepKnot(ERide(110f), KEY_FAN6),
            new StepKnot(EPost(640f), KEY_OFF),
            new StepKnot(EPost(DAtZ7(25f)), KEY_EXT7),
        };

        /// <summary>
        /// Camera far plane by E. Switches sit where the fog already hides everything near the old plane: 200 m in the gallery
        /// until d 40 (D50 45 m: 100 % fog at 200 m), 600 m in the hall from d 315 to 420 (D50 160 m settled at d 310: 99.99 %
        /// at 600 m; the exit proxy closes the shaft view until 420), 30 000 m from d 640 when the Face, honeycomb and Exterior
        /// sky activate. Deviation from the 14.2 "beat 7 tunnel 300 m" entry (reported to the lead): a 300 m plane would clip
        /// the honeycomb seen through the mouth (lattice at 10 km is only 76 % fogged) and the shaft seen back through the
        /// tunnel (beat-6 fog: 50 % at 300 m); reversed-Z D32F keeps 1.8 mm resolution at 30 km, so nothing is gained by it.
        /// </summary>
        public static readonly StepKnot[] FarKnots =
        {
            new StepKnot(0f, AbyssConst.FAR_PLANE[0]),            // 200
            new StepKnot(40f, AbyssConst.FAR_PLANE[1]),           // 4 500 (shaft skeleton active from d 40)
            new StepKnot(315f, AbyssConst.FAR_PLANE[3]),          // 600 hall
            new StepKnot(420f, AbyssConst.FAR_PLANE[4]),          // 4 500
            new StepKnot(EPost(AbyssConst.D_EXTERIOR_ON), AbyssConst.FAR_PLANE[7]), // 30 000
        };

        // ---------------------------------------------------------------- evaluation
        /// <summary>Quintic smootherstep on [0, 1] (zero first and second derivative at both ends).</summary>
        public static float Smootherstep(float x)
        {
            x = x < 0f ? 0f : (x > 1f ? 1f : x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        /// <summary>Rows and eased weight of a row curve at E.</summary>
        public static void EvalRows(RowKnot[] knots, float e, out int rowA, out int rowB, out float w)
        {
            int n = knots.Length;
            if (e <= knots[0].E) { rowA = rowB = knots[0].Row; w = 0f; return; }
            for (int i = 0; i < n - 1; i++)
            {
                if (e < knots[i + 1].E)
                {
                    rowA = knots[i].Row; rowB = knots[i + 1].Row;
                    float span = knots[i + 1].E - knots[i].E;
                    w = span > 1e-6f ? Smootherstep((e - knots[i].E) / span) : 1f;
                    return;
                }
            }
            rowA = rowB = knots[n - 1].Row; w = 0f;
        }

        /// <summary>Scalar curve value at E.</summary>
        public static float EvalFloat(FloatKnot[] knots, float e)
        {
            int n = knots.Length;
            if (e <= knots[0].E) return knots[0].V;
            for (int i = 0; i < n - 1; i++)
            {
                if (e < knots[i + 1].E)
                {
                    float span = knots[i + 1].E - knots[i].E;
                    float w = span > 1e-6f ? Smootherstep((e - knots[i].E) / span) : 1f;
                    return knots[i].V + (knots[i + 1].V - knots[i].V) * w;
                }
            }
            return knots[n - 1].V;
        }

        /// <summary>Step function value at E.</summary>
        public static float EvalStep(StepKnot[] knots, float e)
        {
            float v = knots[0].Value;
            for (int i = 1; i < knots.Length; i++)
            {
                if (e >= knots[i].E) v = knots[i].Value; else break;
            }
            return v;
        }

        /// <summary>Domain-0 fog sigma between two rows: geometric (log-space) blend of 0.8326 / D50.</summary>
        public static float BlendSigma(int rowA, int rowB, float w)
        {
            float sa = AbyssPalette.D50ToSigma(Rows[rowA].D50), sb = AbyssPalette.D50ToSigma(Rows[rowB].D50);
            if (rowA == rowB || w <= 0f) return sa;
            if (w >= 1f) return sb;
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(sa), Mathf.Log(sb), w));
        }

        public static float FogSigmaAt(float e) { EvalRows(FogKnots, e, out int a, out int b, out float w); return BlendSigma(a, b, w); }
        public static float ExposureAt(float e) { EvalRows(ExposureKnots, e, out int a, out int b, out float w); return Mathf.Lerp(Rows[a].EV, Rows[b].EV, w); }
        public static float FovAt(float e) { EvalRows(FovKnots, e, out int a, out int b, out float w); return Mathf.Lerp(Rows[a].Fov, Rows[b].Fov, w); }

        /// <summary>Evaluate every continuous value at extended progress E (allocation-free).</summary>
        public static void Evaluate(float e, ref ProgressSample s)
        {
            int a, b; float w;

            EvalRows(FogKnots, e, out a, out b, out w);
            s.FogSigma = BlendSigma(a, b, w);
            s.FogDownMul = Mathf.Lerp(Rows[a].MDown, Rows[b].MDown, w);
            s.FogDown = Color.Lerp(Rows[a].FogDownLin, Rows[b].FogDownLin, w);
            s.FogHoriz = Color.Lerp(Rows[a].FogHorizLin, Rows[b].FogHorizLin, w);
            s.FogUp = Color.Lerp(Rows[a].FogUpLin, Rows[b].FogUpLin, w);

            EvalRows(AmbientKnots, e, out a, out b, out w);
            float ka = Rows[a].AmbientScale, kb = Rows[b].AmbientScale;
            s.AmbUp = Color.Lerp(Rows[a].FogUpLin * ka, Rows[b].FogUpLin * kb, w);
            s.AmbMid = Color.Lerp(Rows[a].FogHorizLin * ka, Rows[b].FogHorizLin * kb, w);
            s.AmbDown = Color.Lerp(AmbientGround(a), AmbientGround(b), w);

            EvalRows(ExposureKnots, e, out a, out b, out w);
            s.EV = Mathf.Lerp(Rows[a].EV, Rows[b].EV, w);
            s.Contrast = Mathf.Lerp(Rows[a].Contrast, Rows[b].Contrast, w);
            s.Saturation = Mathf.Lerp(Rows[a].Saturation, Rows[b].Saturation, w);
            s.Temperature = Mathf.Lerp(Rows[a].Temperature, Rows[b].Temperature, w);
            s.Vignette = Mathf.Lerp(Rows[a].Vignette, Rows[b].Vignette, w);
            s.BloomThreshold = Mathf.Lerp(Rows[a].BloomThreshold, Rows[b].BloomThreshold, w);
            s.BloomScatter = Mathf.Lerp(Rows[a].BloomScatter, Rows[b].BloomScatter, w);
            s.BloomIntensity = EvalFloat(BloomIntensityKnots, e);

            EvalRows(FovKnots, e, out a, out b, out w);
            s.Fov = Mathf.Lerp(Rows[a].Fov, Rows[b].Fov, w);
            s.FarClip = EvalStep(FarKnots, e);

            s.SkyBlend = EvalFloat(SkyBlendKnots, e);
            s.BeamFade = EvalFloat(BeamFadeKnots, e);
            s.KeyRow = (int)EvalStep(KeyKnots, e);
        }

        /// <summary>Trilight ground term of a row (linear): FOG_DOWN x scale, raised per channel to AmbGroundMin[row] x the equator term.</summary>
        public static Color AmbientGround(int row)
        {
            LookRow r = Rows[row];
            Color down = r.FogDownLin * r.AmbientScale, floor = r.FogHorizLin * (r.AmbientScale * AmbGroundMin[row]);
            return new Color(Mathf.Max(down.r, floor.r), Mathf.Max(down.g, floor.g), Mathf.Max(down.b, floor.b), 1f);
        }

        // ---------------------------------------------------------------- construction helpers
        static LookRow Row(string name, float d50, float mDown, int down, int horiz, int up, float ev,
            float bloomThr, float bloomInt, float bloomScat, float contrast, float sat, float temp, float vig, float fov, float amb)
        {
            return Row(name, d50, mDown, AbyssPalette.Hex(down), AbyssPalette.Hex(horiz), AbyssPalette.Hex(up), ev,
                bloomThr, bloomInt, bloomScat, contrast, sat, temp, vig, fov, amb);
        }

        static LookRow Row(string name, float d50, float mDown, Color down, Color horiz, Color up, float ev,
            float bloomThr, float bloomInt, float bloomScat, float contrast, float sat, float temp, float vig, float fov, float amb)
        {
            return new LookRow
            {
                Name = name, D50 = d50, MDown = mDown,
                FogDown = down, FogHoriz = horiz, FogUp = up,
                FogDownLin = down.linear, FogHorizLin = horiz.linear, FogUpLin = up.linear,
                EV = ev, BloomThreshold = bloomThr, BloomIntensity = bloomInt, BloomScatter = bloomScat,
                Contrast = contrast, Saturation = sat, Temperature = temp, Vignette = vig, Fov = fov, AmbientScale = amb,
            };
        }

        static KeyRow Key(string name, float yaw, float pitch, int colour, float intensity, float shadowDistance, float scatter,
            float hazeSky, bool depthOn, float depthFull, float depthZero)
        {
            return new KeyRow
            {
                Name = name, On = true, Yaw = yaw, Pitch = pitch, Colour = AbyssPalette.Hex(colour),
                Intensity = intensity, ShadowDistance = shadowDistance, ScatterAmt = scatter, HazeSky = hazeSky,
                DepthOn = depthOn, DepthFull = depthFull, DepthZero = depthZero,
            };
        }

        /// <summary>The shaft-sun ring-chamfer falloff of beats 2 and 3 (SUN_CHAMFER_*).</summary>
        static KeyRow WithChamfer(KeyRow k)
        {
            k.ChamferAmp = SUN_CHAMFER_AMP; k.ChamferY0 = SUN_CHAMFER_Y0; k.ChamferLength = SUN_CHAMFER_LENGTH; k.ChamferZero = SUN_CHAMFER_ZERO;
            return k;
        }

        static FloatKnot[] BuildBloomKnots()
        {
            // Exposure windows mapped to each row's bloom intensity, with the final tunnel-to-outside knot replaced by the
            // mouth peak (z = 3) and the settle (z = -8).
            RowKnot[] k = ExposureKnots;
            var list = new FloatKnot[k.Length + 1];
            for (int i = 0; i < k.Length - 1; i++) list[i] = new FloatKnot(k[i].E, Rows[k[i].Row].BloomIntensity);
            list[k.Length - 1] = new FloatKnot(EPost(DAtZ7(3f)), BLOOM_MOUTH_PEAK);
            list[k.Length] = new FloatKnot(EPost(DAtZ7(-8f)), Rows[ROW_OUTSIDE].BloomIntensity);
            return list;
        }
    }
}
