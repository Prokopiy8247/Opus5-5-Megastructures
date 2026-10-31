using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 1 - service gallery (14.5.1, d 0-60). The board-formed shell lofted along the path (2.4 x 2.5, ceiling = floor + 2.5
    /// everywhere, chamfer 0.08), the floor with the 16 treads (rise 0.1875, run 0.28) at z 40-44.48, the open pressure door at
    /// z = 22 (opening 1.1 x 2.0), the exit doorway at z = 60 (opening 1.2 x 2.0, jambs 0.6, depth 0.8), 10 fixtures that wake
    /// ahead of the player, the amber lamp over the exit, the cold spill of the lamp over the doorway, the stair-head bounce
    /// (the KS1 far glow) and the world-fixed doorway haze that carries the reveal across the threshold (R1-07).
    ///
    /// Tiers (ctx.Tier, 14.5.1 Fallbacks): Full adds the dressing in <c>Beat01_Gallery.Full.cs</c> (20 beams and 20 pilasters
    /// with haunches, the lighting trunk with 10 fixtures, the cable tray with 3 cables, the pipe with 20 flanges and 160
    /// instanced hex bolts, the steel bulkhead with the open leaf and its torus handwheel, the 6-blade wall fan, the drain, the
    /// floor line and stair nosings, the stair handrail, the closed start door), the interior detail tier that hands over to an
    /// equal-luma proxy card by d 90 (14.6) and the BeamWedge doorway shaft. Simplified drops pilasters, bolts and the fan and
    /// keeps 5 fixtures; Graybox is the box corridor with stairs, door and exit light.
    ///
    /// Light: the gallery is never lit by the Key (14.2): every interior material is the _KeyLightMask = 0 variant, and the
    /// gallery still blocks the beat-2 sun on its own (shell TwoSided, closed volumes On, a shadow-only roof). Colliders are final:
    /// flat floors, a prism slope over the stairs that abuts (never overlaps) the floor boxes, flush walls, the bulkhead, the
    /// narrowed doorway, ceiling clamps and safety volumes; Full and Simplified add boxes around the open door leaf and the fan
    /// (clear of the 0.85 m capsule-centre lanes the QA ray grid probes).
    /// </summary>
    public sealed partial class Beat01_Gallery : BeatBase
    {
        public override int Index => 1;

        // ---------------------------------------------------------------- beat-specific constants (14.5.1, 14.2 rig)
        const float SHELL_CHAMFER = 0.08f;
        /// <summary>The start wall stands behind W0 so the capsule (r 0.35) fits at d = 0; the collider face is at -0.5.</summary>
        const float SHELL_START_Z = -0.6f, BACK_COLLIDER_Z = -0.5f;
        /// <summary>The walls run this far below the path floor (deeper than a tread's back, 0.1875) so no gap shows at the treads.</summary>
        const float SHELL_SKIRT = 0.25f;
        const float FLOOR_SLAB = 0.3f;
        const float JAMB_Z0 = AbyssConst.EXIT_DOOR_Z - AbyssConst.EXIT_DEPTH;   // 59.2
        const float JAMB_RECESS = 0.01f;   // jamb fronts 1 cm behind the shaft wall plane (no co-planar faces)
        const float BULKHEAD_T = 0.2f;
        const int FIXTURE_COUNT = 10;
        const float FIXTURE_Z0 = 3f, FIXTURE_SPACING = 6f, FIXTURE_RANGE = 7f;
        /// <summary>
        /// Fixture intensity. 14.2 gives 3.2 as a starting value to tune against the luma targets; the g01 graybox capture at 3.2
        /// measured KS1 mean luma 0.32 (target 0.10-0.20, the walls 1.2 m from the lights read 0.4-0.55); 1.4 still left KS1 at
        /// 0.19-0.22 (21:9 over the limit) with evenly grey walls. Look-dev (gallery r01-r05; mean luma scales about as I^0.6
        /// here): 0.85 gave KS1 0.136-0.161 but the B1_middle still (d 30, looking down the dark stairs) 0.098; 1.1 puts KS1 at
        /// about 0.16-0.19 and B1_middle at about 0.12, with the walls well under the lenses (calm dread). Ranges as specified.
        /// </summary>
        const float FIXTURE_I = 1.1f;
        /// <summary>
        /// Diffuser emission at the lens centre (COLD_LAMP x 1.25). R2-22: at x 1.6 the KS1 lens (0.82, std 4.8 levels) sat on the
        /// tonemap shoulder, a flat white trapezoid brighter than the far stair-head glow, so the eye went up instead of down
        /// the corridor (gallery r21 at x 1.35 with edges at 0.55: 0.79 centre / 0.74 edge, still flat). Lower, with the
        /// Full tier's falloff across the width and along the length (edges 0.15, ends about 0.3 of the centre in linear,
        /// about 0.6-0.7 of it on screen after the tonemap shoulder), it reads as a diffuser (gallery r22 with edges 0.32: 0.76 centre / 0.66 edge).
        /// </summary>
        const float FIXTURE_EMISSION = 1.25f;
        /// <summary>
        /// The exit section (the last 12 m, z 48-60) belongs to the doorway (14.7.6: "the doorway is the only bright feature at
        /// d = 50"): its two fixtures (z 51, 57) give a reduced light (I 0.34) and a slightly dimmer lens, so the end wall stays
        /// dark and frames the bright opening instead of being washed by the fixture 2 m in front of it (f01: KS1b mean 0.29;
        /// r04: 0.145-0.170).
        /// </summary>
        const float EXIT_SECTION_Z0 = AbyssConst.EXIT_DOOR_Z - 12f;
        const float EXIT_FIXTURE_LIGHT_GAIN = 0.31f, EXIT_FIXTURE_LENS_GAIN = 0.75f;
        const float WAKE_AHEAD = 20f, WAKE_TIME = 0.8f;
        /// <summary>
        /// Amber point of the exit lamp (14.2 rig: "amber point over the exit", I 4.5, range 6; M_Lamp is always paired with
        /// an amber point). R1-18: 4.5 washed the end wall to 0.6+ (g01), 1.1 and 0.45 still turned the whole end wall and the
        /// exit-section walls muddy brown (f02 B1_end 52 % warm pixels, KS1b 10.8 %). Amber is the rare human trace, so the
        /// light only warms the lamp's own housing and a hand-sized patch of wall: I 0.03, range 1.5, 0.25 m in front of the
        /// dome (never against the mount, R1-32). The dome's emission and the glow carry the accent.
        /// </summary>
        const float EXIT_LAMP_Z = 58.5f, EXIT_LAMP_I = 0.03f, EXIT_LAMP_RANGE = 1.5f, EXIT_LAMP_LIGHT_OFFSET = 0.25f;
        /// <summary>
        /// Cold spill (14.2 rig: "cold spill behind the door (z 66, I 6, range 25)"). R1-37 / R1-07: from z 66 the point lit the
        /// whole landing to 0.30-0.40 luma (B2_start mean 0.164 against 0.05-0.13) while the doorway relied on a sprite veil.
        /// Now it is the light of the bulkhead lamp Beat02 mounts over the doorway outside (housing at z 60.0-60.22, y -0.79 to
        /// -0.63): a spot 0.16 m under and 0.23 m in front of the housing, pointing down and tilted 15 degrees into the
        /// gallery, cone 140 degrees. It lights the threshold, the jamb reveals and the landing within about 2.5 m of the door
        /// (the bright floor in the lower half of the opening that the player then stands in) and spills down the exit
        /// section's floor (14.5.1 "the last 12 m brighten"); the far landing (z 63-66, the foreground of B2_start) lies outside
        /// the cone. Range as specified. gallery r13 (I 8.8): near landing in the KS1b opening 0.51, B2_start landing strip
        /// 0.31 (its cold part is now the beat-2 Key on sunlit concrete, 0.22-0.29; the warm pool is the bridge's z 66 lamp).
        /// </summary>
        static readonly Vector3 SPILL_POS = new Vector3(0f, -0.95f, 60.45f);
        const float SPILL_TILT = 15f, SPILL_I = 10f, SPILL_RANGE = 25f, SPILL_ANGLE = 140f;
        /// <summary>Direction of the spill spot: down, tilted SPILL_TILT degrees toward the gallery (-Z).</summary>
        static Vector3 SpillDir => new Vector3(0f, -Mathf.Cos(SPILL_TILT * Mathf.Deg2Rad), -Mathf.Sin(SPILL_TILT * Mathf.Deg2Rad));
        /// <summary>
        /// Stair-head glow (R1-16, KS1 far layer). From the KS1 eye the doorway, the amber lamp and the exit section are
        /// hidden: every ray over the nosing (z 40, y 0) meets the sloped stair ceiling by z 44.06, so the only far surface is
        /// that ceiling's underside, which faces the viewer and was lit by nothing (f02: a black room past the pressure door).
        /// A non-shadowing cold bounce in the lower stairwell (0.43 m over tread 13, in front of the slope plane by 1.8 m) aims
        /// up the slope: the light that comes up the stairs from the bright exit section. The slope glows brightest just above
        /// the nosing sight line and fades upward, so KS1 has a far glow from second 0 (no fade-in), the wall fan stands dark
        /// against it, and the treads, the risers and the flat exit ceiling lie outside its 100-degree cone.
        /// </summary>
        static readonly Vector3 STAIR_GLOW_POS = new Vector3(0f, -2.2f, 43.8f), STAIR_GLOW_AIM = new Vector3(0f, 0.76f, 42.6f);
        /// <summary>
        /// gallery r11: I 5.5 lit the visible slope to 0.19 mean (0.33 peak), a dim room rather than a focal glow. f03 (I 11):
        /// far-glow p90 0.32, outshone by the nearest fixture lens (R2-22, target p90 about 0.40); gallery r21 (I 16) 0.37.
        /// I 18, in the doorway's colour (the light that comes up the stairs from the exit is the doorway's, cooler than the
        /// fixtures).
        /// </summary>
        const float STAIR_GLOW_I = 18f, STAIR_GLOW_RANGE = 8f, STAIR_GLOW_ANGLE = 100f;
        /// <summary>
        /// The glow hands over to the woken stair fixtures (z 33, 39, 45 wake at d 13, 19, 25): full up to d 14, smoothstep
        /// to off at d 40, the stair head. gallery r12 kept it on: descending the stairs the camera looks straight into the lit
        /// slope 1-3 m ahead and leaves it under the slope's foot, a 0.22 -> 0.40 -> 0.10 frame-mean pulse over d 43-45
        /// (5-8 EV/s at 2.5 m/s in t12_brisk). Local lights may change faster than the exposure (the fixtures wake in 0.8 s),
        /// but this 26 m ease is invisible: the glow is a few per cent of the frame while the fixtures take over.
        /// </summary>
        public const float STAIR_GLOW_D0 = 14f, STAIR_GLOW_D1 = 40f;

        /// <summary>Stair-glow level at path distance d (1 up to STAIR_GLOW_D0, smoothstep to 0 at STAIR_GLOW_D1).</summary>
        public static float StairGlowLevel(float d) => 1f - BeatKit.Smoothstep((d - STAIR_GLOW_D0) / (STAIR_GLOW_D1 - STAIR_GLOW_D0));
        /// <summary>KS1 "far glow" subject: the lit lower slope between z 42.6 and 44.0 (above the nosing sight line).</summary>
        const float FAR_GLOW_Z0 = 42.6f, FAR_GLOW_Z1 = 44.0f, FAR_GLOW_HW = 1.15f;
        /// <summary>
        /// Shadow-only roof (the rock over the gallery): gap above the ceiling (clear of the 5 cm jamb and lintel tops),
        /// thickness, half width (overhangs the walls by more than the Key's 0.14 m lateral travel per metre of height times
        /// the 2.75 m wall), start just behind the Face plane z = 0, end 5 cm short of the shaft wall z = 60.
        /// </summary>
        const float ROOF_GAP = 0.1f, ROOF_T = 0.5f, ROOF_HW = 1.7f;
        const float ROOF_Z0 = 0.05f, ROOF_Z1 = AbyssConst.EXIT_DOOR_Z - 0.05f;

        // Doorway reveal (14.5.1 KS1/KS1b, 14.7.6, R1-07, R2-01). The light sits behind the subject. Geometry first: C1 (r 22 at
        // z 244) subtends +/-6.7 degrees from the axis at 190 m, the same as the clear opening seen from 5 m (+/-6.8 degrees
        // at KS1b), so from every pose of the approach (d < 55) C1 fills the doorway's whole width at every height (the column
        // runs from y -3000 to 1200): there is no sky behind it to light. Only on the threshold (d 55-60, opening +/-7 to
        // +/-17 degrees) and on the landing does the shaft beside C1 come into view. So the reveal has two layers, both
        // world-fixed, unfogged (domain 2: no fog at 140-500 m) and pale cold (COLD_LAMP, chroma x1.2, saturation about 0.2):
        // (1) the near veil (renderer DoorwayHaze): the haze between the doorway and C1, wrapped around C1 so it fills the
        //     opening on the approach (the bright doorway of 14.5.1 / 14.7.6, the KS1b aperture 0.40-0.60 of the D6 band) and
        //     thins out over d HAZE_D0-HAZE_D1 as the player crosses the threshold, so that on the landing C1 stands dark in
        //     front of (2) instead of behind a glow (R2-01: the f03 veil was a round, sourceless cloud on C1's base until d 98);
        // (2) the backlight (renderer DoorwayBacklight): the lit haze of the shaft beyond C1, well behind it (plane about
        //     z 400), depth-tested, so C1, its ledge, the deck with its lamps, the lift rails and the hull stand against it as
        //     silhouettes (MSAA-resolved occlusion, no hard sprite edge), centred above the horizon so the abyss below the
        //     deck stays darker than the shaft above it (no 'obelisk in a white room'). It dissolves slowly over
        //     d FAR_D0-FAR_D1 on the bridge, so the far walls emerge as the fog thins.
        // Both follow a displayed (post-exposure, pre-tonemap) luminance schedule exactly: the strength divides out the exposure
        // of ProgressTables (+0.4 to -0.25 over d 52-62), so neither steps with EV.
        // The domain-0 fog of the approach (D50 45-48 m until d 55) leaves C1 at about 9 % transmittance at KS1b, so C1's own
        // shading cannot show through the doorway; the near veil draws it instead, from the depth buffer (R2-01, gallery r31):
        /// <summary>
        /// Near veil centre on the bridge axis inside C1 (z 236) and 19 m above the KS1b eye line, diameter 90 m. Pulled 45 m
        /// toward the camera its plane lies near z 191, so its 45 m soft-depth fade wraps C1: the face centre (z 222) takes
        /// about 0.68 of it and the limbs (z 233-244) up to all of it, so C1 reads as a column (darker middle, haze-lit
        /// flanks) and its ledge and the deck's last 25 m sink into it with distance (the deck nearer than z 191 stands clear).
        /// Centred well above the eye line, the Gaussian falls off downward: in the KS1b opening the abyss below the horizon
        /// (C1's lower face) shows about half the veil of the upper face (R2-01 A: dimmer toward FOG_DOWN, no 'obelisk in a
        /// white room'). gallery r31 (13 m up, 100 m): above / below the horizon 0.44 / 0.38-0.40, too even. Half-maximum
        /// radius 0.31 x 90 = 28 m.
        /// </summary>
        static readonly Vector3 HAZE_GLOW_POS = new Vector3(0f, 18f, 236f);
        const float HAZE_GLOW_DIAMETER = 90f;
        /// <summary>
        /// Near veil: displayed linear luminance at its reference point, C1's face centre on the KS1b eye line (the fogged
        /// column itself is HAZE_BG): HAZE_PEAK on the approach (sRGB about 0.49), then log-luminance falls linearly with d
        /// (eased over the first and last HAZE_EASE of the window) to HAZE_BG at HAZE_D1. Values: d 55 (KS1b) 0.147 (sRGB
        /// about 0.42), d 58 (B1_end) 0.089, d 62 (B2_start) 0.048 (sRGB 0.25, the R2-01 cap over C1). The linear part
        /// covers d 53.4-69.0 at log2(0.212 / 0.0137) / (18.5 x 0.92) = 0.232 EV per metre: 0.35 EV/s at the cinematic
        /// 1.5 m/s, the fastest the 14.6 rate allows between a doorway in the D6 band (KS1b) and C1 at the cap (B2_start). It
        /// is local to C1's area while the opening widens onto the backlight, so the doorway-aperture and frame means change
        /// far more slowly (t12 sequences, 14.6).
        /// </summary>
        public const float HAZE_PEAK = 0.212f, HAZE_BG = 0.0137f, HAZE_D0 = 52f, HAZE_D1 = 70.5f, HAZE_EASE = 0.08f;
        /// <summary>Reference pose of the near-veil schedule: the KS1b eye (14.5.1) and C1's face centre on its line of sight.</summary>
        static readonly Vector3 HAZE_REF_EYE = new Vector3(0f, AbyssConst.W[2].y + AbyssConst.EYE, 55f);
        /// <summary>
        /// Weight of the near-veil sprite at its reference point (C1's face centre seen from HAZE_REF_EYE), as the Glow shader
        /// computes it: the windowed Gaussian exp(-5 r^2) (1 - r^2)^2 at the point's offset from the pulled centre (r in
        /// diameters), times the soft-depth fade of the face behind the pulled, camera-facing plane (half a diameter).
        /// </summary>
        public static float HazeRefGain()
        {
            Vector3 c = HAZE_GLOW_POS, eye = HAZE_REF_EYE;
            float diam = HAZE_GLOW_DIAMETER;
            Vector3 toCam = eye - c;
            float dist = toCam.magnitude;
            Vector3 plane = c + toCam / dist * Mathf.Min(0.5f * diam, 0.5f * dist);
            float faceZ = AbyssConst.COLUMNS[0].y - AbyssConst.COLUMNS[0].z;
            float fade = Mathf.Clamp01((faceZ - plane.z) / (0.5f * diam));    // pitch-0 reference view: eye depth = z - eye z
            float qy = (eye.y - plane.y) / diam, r2 = qy * qy;
            float w = Mathf.Clamp01(1f - r2);
            return Mathf.Exp(-5f * r2) * w * w * fade;
        }
        /// <summary>Haze fog domain: the Exterior constants (D50 7 000 m) leave a 140-500 m sprite unfogged at any camera d.</summary>
        const float HAZE_DOMAIN = 2f;
        /// <summary>
        /// Backlight centre: behind C1, 20 m right of the axis (the hull fills the left of the landing view from x -32) and
        /// 40 m up (4.5 degrees above the eye line from the landing, so the glow falls off below the deck: the abyss stays
        /// darker than the shaft above), diameter 300 m. Pulled 150 m toward the camera its plane lies near z 400, 150 m
        /// behind C1's limbs and 360 m in front of the far flat (soft depth 150 m: the far walls take it fully, C2 at 0.6).
        /// Half-maximum radius 0.31 x 300 = 93 m, about 15 degrees from the landing.
        /// </summary>
        static readonly Vector3 FAR_GLOW_POS = new Vector3(20f, 40f, 550f);
        const float FAR_GLOW_DIAMETER = 300f;
        /// <summary>
        /// Backlight: displayed linear luminance at its centre (hidden behind C1 from the walking line; the flanks beside C1
        /// show 0.6-0.9 of it), FAR_PEAK (sRGB about 0.48) until FAR_D0, then a smoothstep log-luminance ease to HAZE_BG at
        /// FAR_D1. Peak rate 1.5 x log2(0.2 / 0.0137) / 48 = 0.12 EV per metre, 0.30 EV/s at the brisk 2.5 m/s (14.6 cap).
        /// gallery r23 at 0.24: B2_start mean 0.194 of the 0.20 ceiling; 0.20 pays for the brighter near-veil flanks beside C1.
        /// </summary>
        public const float FAR_PEAK = 0.20f, FAR_D0 = 56f, FAR_D1 = 104f;
        /// <summary>
        /// Doorway colour: COLD_LAMP with its linear chroma x1.2 around its luma (R1-07: at most x1.2, saturation 20-25 %;
        /// the f02 WIN_COLD x2 read as a saturated daylight blue, #83AFCC).
        /// </summary>
        const float DOORWAY_CHROMA_GAIN = 1.2f;
        static readonly Color DOORWAY_COLD = Chroma(AbyssPalette.COLD_LAMP, DOORWAY_CHROMA_GAIN);
        static readonly float DOORWAY_COLD_LUMA = Luma(DOORWAY_COLD.linear);
        static readonly float LOG2_HAZE_PEAK = Mathf.Log(HAZE_PEAK, 2f), LOG2_HAZE_BG = Mathf.Log(HAZE_BG, 2f);
        static readonly float LOG2_FAR_PEAK = Mathf.Log(FAR_PEAK, 2f);

        /// <summary>sRGB colour whose linear chroma (distance from its Rec. 709 luma) is scaled by gain.</summary>
        static Color Chroma(Color srgb, float gain)
        {
            Color l = srgb.linear;
            float y = Luma(l);
            var c = new Color(Mathf.Max(0f, y + (l.r - y) * gain), Mathf.Max(0f, y + (l.g - y) * gain), Mathf.Max(0f, y + (l.b - y) * gain), 1f);
            return c.gamma;
        }
        // interior detail tier (14.6: the gallery interior is off at d 90 behind an equal-luma proxy card)
        /// <summary>Camera distance to the gallery bounds at which the interior is gone (the bridge at d 90 is 30 m from z 60).</summary>
        const float INTERIOR_NEAR = 30f, INTERIOR_HYSTERESIS = 20f, INTERIOR_FADE = 8f;

        // KS1 composition (14.10 key shot (0, 1.7, 8), FOV 62): which subjects the pose can actually see
        /// <summary>KS1 eye z, and how far ahead ceiling-mounted parts (0.4-0.8 m over the eye) are whole inside the 62 degree
        /// frame with the 3 % lint margin: 0.8 / (0.94 tan 31) = 1.42 m. The fixture at z 9 hangs over the top edge.</summary>
        const float KS1_EYE_Z = 8f, KS1_FRAME_DZ = 1.5f, KS1_FOREGROUND_Z1 = 16f;
        /// <summary>
        /// Depth of the stair head the KS1 subject covers (the upper floor's front edge and the top egress markers). From the
        /// KS1 eye every ray that clears the nosing (z 40, y 0) descends at most 1.7 / 32 per metre and meets the sloped stair
        /// ceiling by z 44.1, so the treads below the nosing, the whole exit section, the amber lamp and the doorway are hidden
        /// (14.5.1 KS1 lists them, but the fixed geometry with ceiling = floor + 2.5 cannot show them; they are KS1b subjects).
        /// </summary>
        const float STAIR_HEAD_DEPTH = 0.3f;

        readonly List<Mesh> _meshes = new List<Mesh>();
        Transform _geo;
        BeatTier _tier;
        MeshRenderer _shell, _floor, _treads, _jambL, _jambR, _lintel, _exitLamp;
        readonly MeshRenderer[] _fixtureR = new MeshRenderer[FIXTURE_COUNT];
        readonly Light[] _fixtureL = new Light[FIXTURE_COUNT];
        readonly MaterialPropertyBlock[] _fixtureMpb = new MaterialPropertyBlock[FIXTURE_COUNT];
        readonly bool[] _fixtureOn = new bool[FIXTURE_COUNT];
        /// <summary>Seconds since the fixture began to wake (-1 = asleep), accumulated from SceneClock.RealTime deltas.</summary>
        readonly float[] _wakeT = new float[FIXTURE_COUNT];
        readonly float[] _wakeLevel = new float[FIXTURE_COUNT];
        double _lastReal = -1.0;
        readonly float[] _treadTop = new float[AbyssConst.STAIR_COUNT];
        readonly float[] _treadRun = new float[AbyssConst.STAIR_COUNT];
        bool _editMode;

        // measurements taken from generated vertices (ReportScale)
        float _doorWidth, _doorHeight, _galleryLength, _pdWidth = -1f, _pdHeight = -1f;
        Bounds _doorwayBounds, _pressureDoorBounds, _stairHeadBounds, _farGlowBounds;
        /// <summary>Position of the exit lamp's amber point (in front of the tier's lamp head), set by the tier builder.</summary>
        Vector3 _exitLightPos;

        /// <summary>The stair-head glow light and its last applied level (-1 = not applied yet).</summary>
        Light _stairGlow;
        float _stairGlowLevel = -1f;

        // doorway near veil and backlight (world-fixed, R1-07 / R2-01), interior tier, wall fan
        MeshRenderer _hazeGlow, _farGlow;
        MaterialPropertyBlock _hazeGlowMpb, _farGlowMpb; // created in Build (Unity forbids engine objects in field initializers)
        float _hazeGlowStrength = -1f, _farGlowStrength = -1f;
        DetailTier _interior;
        float _coverage = 1f;
        Transform _fanRotor;
        Quaternion _fanBase = Quaternion.identity;

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Light and lens gains of fixture i: the two in the exit section are dimmed (EXIT_SECTION_Z0).</summary>
        static float FixtureLightGain(int i) => FixtureZ(i) > EXIT_SECTION_Z0 ? EXIT_FIXTURE_LIGHT_GAIN : 1f;
        static float FixtureLensGain(int i) => FixtureZ(i) > EXIT_SECTION_Z0 ? EXIT_FIXTURE_LENS_GAIN : 1f;

        /// <summary>Path floor height of the gallery at z (flat, the straight stair ramp W1-W2, flat).</summary>
        public static float FloorY(float z)
        {
            if (z <= AbyssConst.STAIR_Z0) return AbyssConst.W[1].y;
            if (z >= AbyssConst.STAIR_Z1) return AbyssConst.W[2].y;
            return AbyssConst.W[1].y - AbyssConst.STAIR_DROP * (z - AbyssConst.STAIR_Z0) / (AbyssConst.STAIR_Z1 - AbyssConst.STAIR_Z0);
        }

        /// <summary>Ceiling height of the gallery at z (floor + 2.5 everywhere, 14.5.1).</summary>
        public static float CeilY(float z) => FloorY(z) + AbyssConst.GALLERY_H;

        static float FixtureZ(int i) => FIXTURE_Z0 + FIXTURE_SPACING * i;

        /// <summary>The Key-masked variant of a base material (14.2: the gallery is never lit by the Key).</summary>
        static Material NoKey(MaterialLibrary mats, MatId id) => mats.Variant(id, "nokey", m => m.SetFloat(KeyLightMaskId, 0f));

        static readonly int KeyLightMaskId = Shader.PropertyToID("_KeyLightMask");

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat01");
            if (_hazeGlowMpb == null) _hazeGlowMpb = new MaterialPropertyBlock();
            if (_farGlowMpb == null) _farGlowMpb = new MaterialPropertyBlock();
            if (_shaftMpb == null) _shaftMpb = new MaterialPropertyBlock();
            _editMode = ctx.EditMode;
            _tier = ctx.Tier;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            for (int i = 0; i < FIXTURE_COUNT; i++) _fixtureOn[i] = ctx.Tier != BeatTier.Simplified || i % 2 == 0;
            BuildColliders(ctx);
            var mb = new MeshBuilder();
            BuildShared(ctx, mb);
            if (ctx.Tier == BeatTier.Graybox) BuildGraybox(ctx, mb);
            else BuildFull(ctx, mb, ctx.Tier == BeatTier.Simplified);
            BuildDoorwayGlow(ctx, mb);
            BuildLights(ctx);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            float hw = AbyssConst.GALLERY_W * 0.5f;
            float z0 = AbyssConst.STAIR_Z0, z1 = AbyssConst.STAIR_Z1, zx = AbyssConst.EXIT_DOOR_Z;
            float yTop = AbyssConst.W[1].y, yLow = AbyssConst.W[2].y;
            const float floorW = 3.4f;   // floors run under the walls

            // flat floors that end exactly where the stair prism begins and ends
            CorridorKit.Floor(c, "GalleryFloorUpper", new Vector3(0f, yTop, (BACK_COLLIDER_Z - 0.5f + z0) * 0.5f), new Vector2(floorW, z0 - (BACK_COLLIDER_Z - 0.5f)));
            CorridorKit.Floor(c, "GalleryFloorLower", new Vector3(0f, yLow, (z1 + zx) * 0.5f), new Vector2(floorW, zx - z1));

            // stair slope: a closed prism whose top runs W1 -> W2 and whose end faces abut the floor boxes (no overlap, no step)
            var mb = new MeshBuilder();
            var sec = new CrossSection();
            // Extrude along +X maps section x to world -z and section y to world y
            sec.Clear(true);
            sec.Add(new Vector2(-z0, yTop), true);
            sec.Add(new Vector2(-z1, yLow), true);
            sec.Add(new Vector2(-z1, yLow - 1f), true);
            sec.Add(new Vector2(-z0, yLow - 1f), true);
            MeshKit.Extrude(mb, sec, new Vector3(-floorW * 0.5f, 0f, 0f), new Vector3(floorW * 0.5f, 0f, 0f), Vector3.up, true);
            Mesh ramp = BeatKit.Bake(mb, "GalleryStairSlope", _meshes);
            CorridorKit.MeshSurface(c, "GalleryStairSlope", ramp);

            // walls flush with the visible shell (inner faces at +/- 1.2), then the doorway jambs (+/- 0.6) through the web
            CorridorKit.WallPair(c, "GalleryWall", new Vector3(0f, 0f, BACK_COLLIDER_Z - 0.5f), new Vector3(0f, 0f, JAMB_Z0), hw, yLow - 0.5f, 7f);
            CorridorKit.WallPair(c, "DoorwayJamb", new Vector3(0f, 0f, JAMB_Z0), new Vector3(0f, 0f, zx), AbyssConst.EXIT_DOOR_W * 0.5f, yLow - 0.5f, 7f);
            CorridorKit.Box(c, "GalleryBackWall", new Vector3(0f, 1.5f, BACK_COLLIDER_Z - CorridorKit.WallThickness * 0.5f),
                new Vector3(AbyssConst.GALLERY_W + 2f * CorridorKit.WallThickness, 8f, CorridorKit.WallThickness), Quaternion.identity);

            // pressure-door bulkhead (opening 1.1 wide): two panels from the opening edge into the walls
            float pd = AbyssConst.PRESSURE_DOOR_W * 0.5f, pz = AbyssConst.PRESSURE_DOOR_Z;
            float panelW = hw + CorridorKit.WallThickness - pd;
            CorridorKit.Box(c, "BulkheadL", new Vector3(-(pd + panelW * 0.5f), 1.5f, pz), new Vector3(panelW, 4f, BULKHEAD_T), Quaternion.identity);
            CorridorKit.Box(c, "BulkheadR", new Vector3(pd + panelW * 0.5f, 1.5f, pz), new Vector3(panelW, 4f, BULKHEAD_T), Quaternion.identity);

            // ceiling clamps (ceiling = floor + 2.5 everywhere)
            float h = AbyssConst.GALLERY_H, cw = AbyssConst.GALLERY_W + 0.6f;
            BeatKit.CeilingClamp(c, "CeilingUpper", new Vector3(0f, yTop + h, BACK_COLLIDER_Z - 0.5f), new Vector3(0f, yTop + h, z0), cw);
            BeatKit.CeilingClamp(c, "CeilingStairs", new Vector3(0f, yTop + h, z0), new Vector3(0f, yLow + h, z1), cw);
            BeatKit.CeilingClamp(c, "CeilingLower", new Vector3(0f, yLow + h, z1), new Vector3(0f, yLow + h, zx), cw);

            // safety volumes 3 m below the walkable surfaces
            CorridorKit.SafetyBelow(c, "SafetyUpper", new Vector3(0f, yTop, z0 * 0.5f), new Vector2(AbyssConst.GALLERY_W, z0));
            CorridorKit.SafetyBelow(c, "SafetyLower", new Vector3(0f, yLow, (z1 + zx) * 0.5f), new Vector2(AbyssConst.GALLERY_W, zx - z1));

            if (ctx.Tier == BeatTier.Graybox) return;
            // Full / Simplified: the open leaf stands 92 degrees into the corridor (x -0.78..-0.52 with its handwheel) and the fan
            // reaches x -0.54 at eye height; both boxes stay inside x > -0.84 so the ray grid's ceiling probes at the 0.85 m
            // capsule-centre lane (and the wall-hug pocket check: the 0.42 m gap behind the leaf is narrower than the capsule)
            // never meet them.
            CorridorKit.Box(c, "PressureDoorLeaf", new Vector3(-0.65f, 1.05f, 21.205f), new Vector3(0.26f, 2.1f, 1.35f), Quaternion.identity);
            if (ctx.Tier == BeatTier.Full)
                CorridorKit.Box(c, "WallFan", new Vector3(-0.67f, 1.70f, 29.915f), new Vector3(0.34f, 0.64f, 0.47f), Quaternion.identity);
        }

        /// <summary>
        /// Geometry every tier shares: shell (TwoSided) and the shadow-only roof, start wall and Face boss, floors (the Full
        /// upper floor carries the drain hole), the 16 treads, the exit doorway jambs and lintel.
        /// </summary>
        void BuildShared(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Material conc = NoKey(mats, MatId.ConcreteBoard), floorMat = NoKey(mats, MatId.ConcreteFloor);
            float hw = AbyssConst.GALLERY_W * 0.5f, h = AbyssConst.GALLERY_H;
            float yTop = AbyssConst.W[1].y, yLow = AbyssConst.W[2].y;
            bool full = ctx.Tier != BeatTier.Graybox;

            // Shadows: beat 1 is unlit by the Key (14.2), but the KEY_SUN2 window opens at d 52 (14.6) while the player is still
            // inside, and the gallery is seen back through the doorway from the landing and the bridge. So the gallery itself
            // must block the sun: every closed volume casts (On), the open inward-facing shell casts TwoSided (its ceiling is a
            // back face for a downward light, which the ShadowCaster pass would cull), and a shadow-only roof closes the
            // cracks the URP normal bias opens at the shell's split vertices (chamfers, stair kinks).

            // ---- shell: open U section (walls + ceiling, facing inward) lofted along W0..W3 with vertical sections
            var sec = new CrossSection();
            sec.Clear(false);
            sec.Add(new Vector2(-hw, -SHELL_SKIRT), true);
            sec.Add(new Vector2(-hw, h - SHELL_CHAMFER), true);
            sec.Add(new Vector2(-hw + SHELL_CHAMFER, h), true);
            sec.Add(new Vector2(hw - SHELL_CHAMFER, h), true);
            sec.Add(new Vector2(hw, h - SHELL_CHAMFER), true);
            sec.Add(new Vector2(hw, -SHELL_SKIRT), true);
            var pts = new List<Vector3>
            {
                new Vector3(0f, yTop, SHELL_START_Z), AbyssConst.W[1], AbyssConst.W[2], new Vector3(0f, yLow, JAMB_Z0 + 0.05f),
            };
            var frames = new List<LoftFrame>();
            MeshKit.PathFrames(pts, Vector3.up, frames, true);
            MeshKit.Loft(mb, sec, frames);
            int chamferStart = 0;
            for (int i = chamferStart; i < mb.VertexCount; i++)   // the 0.08 chamfer facets catch the light: edge wear G
                if (Mathf.Abs(mb.Normals[i].x) > 0.3f && Mathf.Abs(mb.Normals[i].y) > 0.3f) { Color32 col = mb.Colors[i]; col.g = 255; mb.Colors[i] = col; }
            _shell = BeatKit.Emit(mb, _geo, "GalleryShell", conc, _meshes, ShadowCastingMode.TwoSided);
            AddNear(_shell);

            // ---- shadow-only roof: one closed solid 0.1-0.6 m over the ceiling along the same vertical-section path, with
            // smooth section points and smooth frames (shared vertices, so the normal-bias inset opens no crack), drawn only
            // into the shadow map
            sec.Clear(true);
            sec.Add(new Vector2(-ROOF_HW, h + ROOF_GAP));
            sec.Add(new Vector2(-ROOF_HW, h + ROOF_GAP + ROOF_T));
            sec.Add(new Vector2(ROOF_HW, h + ROOF_GAP + ROOF_T));
            sec.Add(new Vector2(ROOF_HW, h + ROOF_GAP));
            pts.Clear();
            pts.Add(new Vector3(0f, yTop, ROOF_Z0));
            pts.Add(AbyssConst.W[1]);
            pts.Add(AbyssConst.W[2]);
            pts.Add(new Vector3(0f, yLow, ROOF_Z1));
            MeshKit.PathFrames(pts, Vector3.up, frames, true, 90f);
            MeshKit.Loft(mb, sec, frames, true);
            BeatKit.Emit(mb, _geo, "GalleryRoofCaster", mats[MatId.ShadowCaster], _meshes, ShadowCastingMode.ShadowsOnly);

            // ---- start wall (single-sided, facing into the gallery) and the boss that closes the Face hole from outside:
            // the gallery's first 0.6 m lie in front of the Face plane z = 0; every boss face points away from the gallery,
            // so nothing of it is visible from inside, and from the balcony it reads as a small block on the Face
            BeatKit.Rect2 hole = BeatKit.GALLERY_FACE_HOLE;
            BeatKit.ZRect(mb, SHELL_START_Z, true, -hw - 0.05f, hw + 0.05f, -SHELL_SKIRT - 0.05f, h + 0.05f, 1f);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryStartWall", conc, _meshes, ShadowCastingMode.On));
            float bz0 = BeatKit.GALLERY_BOSS_Z, bz1 = AbyssConst.W[0].z + 0.05f;
            BeatKit.ZRect(mb, bz0, false, hole.X0, hole.X1, hole.Y0, hole.Y1, 1f);
            BeatKit.Quad(mb, new Vector3(hole.X0, hole.Y0, bz0), new Vector3(hole.X0, hole.Y1, bz0), new Vector3(hole.X0, hole.Y1, bz1), new Vector3(hole.X0, hole.Y0, bz1), Vector3.left);
            BeatKit.Quad(mb, new Vector3(hole.X1, hole.Y0, bz0), new Vector3(hole.X1, hole.Y1, bz0), new Vector3(hole.X1, hole.Y1, bz1), new Vector3(hole.X1, hole.Y0, bz1), Vector3.right);
            BeatKit.Quad(mb, new Vector3(hole.X0, hole.Y1, bz0), new Vector3(hole.X1, hole.Y1, bz0), new Vector3(hole.X1, hole.Y1, bz1), new Vector3(hole.X0, hole.Y1, bz1), Vector3.up);
            BeatKit.Quad(mb, new Vector3(hole.X0, hole.Y0, bz0), new Vector3(hole.X1, hole.Y0, bz0), new Vector3(hole.X1, hole.Y0, bz1), new Vector3(hole.X0, hole.Y0, bz1), Vector3.down);
            BeatKit.Emit(mb, _geo, "GalleryFaceBoss", mats.Get(MatId.ConcreteBoard, FogDomain.Exterior), _meshes, ShadowCastingMode.On);

            // ---- floor slabs (worn centre path: UV0.x = lateral offset). The lower slab stops at the end-wall plane: the
            // threshold between the jambs is a separate, Key-lit slab (the sun reaches the doorway floor, 14.2 rig)
            float fw = AbyssConst.GALLERY_W + 0.1f;
            float z0 = AbyssConst.STAIR_Z0, z1 = AbyssConst.STAIR_Z1, zx = AbyssConst.EXIT_DOOR_Z;
            if (full) BuildUpperFloorWithDrain(mb);
            else MeshKit.ChamferBox(mb, new Vector3(0f, yTop - FLOOR_SLAB * 0.5f, (SHELL_START_Z + z0) * 0.5f), new Vector3(fw, FLOOR_SLAB, z0 - SHELL_START_Z), 0.02f);
            MeshKit.ChamferBox(mb, new Vector3(0f, yLow - FLOOR_SLAB * 0.5f, (z1 + JAMB_Z0) * 0.5f), new Vector3(fw, FLOOR_SLAB, JAMB_Z0 - z1), 0.02f);
            BeatKit.FloorUVAlongZ(mb, 0, 0f);
            _floor = BeatKit.Emit(mb, _geo, "GalleryFloor", floorMat, _meshes, ShadowCastingMode.On);
            AddNear(_floor);
            MeshKit.ChamferBox(mb, new Vector3(0f, yLow - FLOOR_SLAB * 0.5f, (JAMB_Z0 + zx) * 0.5f), new Vector3(fw, FLOOR_SLAB, zx - JAMB_Z0), 0.02f);
            BeatKit.FloorUVAlongZ(mb, 0, 0f);
            BeatKit.Emit(mb, _geo, "DoorwayThreshold", mats[MatId.ConcreteFloor], _meshes, ShadowCastingMode.On);

            // the stair head seen from KS1: the upper floor's front edge (with its painted nosing in Full / Simplified); the
            // top egress markers are added by BuildStairMarkers
            _stairHeadBounds = new Bounds();
            _stairHeadBounds.SetMinMax(new Vector3(-hw, yTop - 0.02f, z0 - STAIR_HEAD_DEPTH), new Vector3(hw, yTop + 0.005f, z0));
            // KS1 far layer (R1-16): the lower sloped stair ceiling the StairGlow lights, above the sight line over the nosing
            _farGlowBounds = new Bounds();
            _farGlowBounds.SetMinMax(new Vector3(-FAR_GLOW_HW, CeilY(FAR_GLOW_Z1), FAR_GLOW_Z0), new Vector3(FAR_GLOW_HW, CeilY(FAR_GLOW_Z0), FAR_GLOW_Z1));

            // ---- 16 treads: tread k spans z 40 + 0.28 k .. + 0.28, top at -0.1875 (k + 1); the nosings lie on the slope line
            float bottom = yLow - FLOOR_SLAB;
            for (int k = 0; k < AbyssConst.STAIR_COUNT; k++)
            {
                float za = z0 + AbyssConst.STAIR_RUN * k, zb = za + AbyssConst.STAIR_RUN;
                float top = yTop - AbyssConst.STAIR_RISE * (k + 1);
                int first = mb.VertexCount;
                MeshKit.ChamferBox(mb, new Vector3(0f, (top + bottom) * 0.5f, (za + zb) * 0.5f), new Vector3(fw, top - bottom, zb - za), 0.01f);
                Bounds tb = BeatKit.VertexBounds(mb, first);
                _treadTop[k] = tb.max.y;
                _treadRun[k] = tb.size.z;
            }
            BeatKit.FloorUVAlongZ(mb, 0, 0f);
            _treads = BeatKit.Emit(mb, _geo, "GalleryTreads", floorMat, _meshes, ShadowCastingMode.On);
            AddNear(_treads);

            // ---- exit doorway: jambs 0.6 wide, depth 0.8, lintel; they run 5 cm into floor, ceiling and walls (no leaks).
            // Persistent (seen from the bridge for the whole walk); Key-masked like the interior (the reveal is in shadow).
            float dw = AbyssConst.EXIT_DOOR_W * 0.5f, jd = AbyssConst.EXIT_DEPTH - JAMB_RECESS;
            float jz = JAMB_Z0 + jd * 0.5f;
            float jy0 = yLow - 0.05f, jy1 = yLow + h + 0.05f;
            float jw = AbyssConst.EXIT_JAMB_W + 0.05f;
            MeshKit.ChamferBox(mb, new Vector3(-(dw + jw * 0.5f), (jy0 + jy1) * 0.5f, jz), new Vector3(jw, jy1 - jy0, jd), 0.02f);
            Bounds jl = BeatKit.VertexBounds(mb, 0);
            _jambL = BeatKit.Emit(mb, _geo, "DoorJambL", conc, _meshes, ShadowCastingMode.On);
            MeshKit.ChamferBox(mb, new Vector3(dw + jw * 0.5f, (jy0 + jy1) * 0.5f, jz), new Vector3(jw, jy1 - jy0, jd), 0.02f);
            Bounds jr = BeatKit.VertexBounds(mb, 0);
            _jambR = BeatKit.Emit(mb, _geo, "DoorJambR", conc, _meshes, ShadowCastingMode.On);
            float ly0 = yLow + AbyssConst.EXIT_DOOR_H;
            MeshKit.ChamferBox(mb, new Vector3(0f, (ly0 + jy1) * 0.5f, jz), new Vector3(2f * dw + 0.1f, jy1 - ly0, jd), 0.02f);
            Bounds lb = BeatKit.VertexBounds(mb, 0);
            _lintel = BeatKit.Emit(mb, _geo, "DoorLintel", conc, _meshes, ShadowCastingMode.On);
            _doorWidth = jr.min.x - jl.max.x;
            _doorHeight = lb.min.y - AbyssConst.W[2].y;
            _galleryLength = jl.max.z + JAMB_RECESS - AbyssConst.W[0].z;
            _doorwayBounds = new Bounds(new Vector3(0f, yLow + AbyssConst.EXIT_DOOR_H * 0.5f, AbyssConst.EXIT_DOOR_Z),
                new Vector3(AbyssConst.EXIT_DOOR_W, AbyssConst.EXIT_DOOR_H, 0.1f));
        }

        void BuildGraybox(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Material steel = NoKey(mats, MatId.SteelPainted);
            float hw = AbyssConst.GALLERY_W * 0.5f, h = AbyssConst.GALLERY_H;
            float yTop = AbyssConst.W[1].y, yLow = AbyssConst.W[2].y;

            // ---- open pressure door at z = 22: bulkhead panels and lintel (opening 1.1 x 2.0), leaf folded against the left wall
            float pd = AbyssConst.PRESSURE_DOOR_W * 0.5f, pz = AbyssConst.PRESSURE_DOOR_Z;
            float pw = hw + 0.05f - pd;
            MeshKit.ChamferBox(mb, new Vector3(-(pd + pw * 0.5f), h * 0.5f, pz), new Vector3(pw, h + 0.1f, BULKHEAD_T), 0.02f);
            Bounds bl = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, _geo, "BulkheadL", steel, _meshes, ShadowCastingMode.On);
            MeshKit.ChamferBox(mb, new Vector3(pd + pw * 0.5f, h * 0.5f, pz), new Vector3(pw, h + 0.1f, BULKHEAD_T), 0.02f);
            Bounds br = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, _geo, "BulkheadR", steel, _meshes, ShadowCastingMode.On);
            float py0 = yTop + AbyssConst.PRESSURE_DOOR_H;
            MeshKit.ChamferBox(mb, new Vector3(0f, (py0 + h + 0.05f) * 0.5f, pz), new Vector3(2f * pd + 0.1f, h + 0.05f - py0, BULKHEAD_T), 0.02f);
            Bounds bt = BeatKit.VertexBounds(mb, 0);
            BeatKit.Emit(mb, _geo, "BulkheadLintel", steel, _meshes, ShadowCastingMode.On);
            _pdWidth = br.min.x - bl.max.x;
            _pdHeight = bt.min.y - AbyssConst.W[0].y;
            _pressureDoorBounds = bl;
            _pressureDoorBounds.Encapsulate(br);
            MeshKit.ChamferBox(mb, new Vector3(-hw + 0.03f, AbyssConst.PRESSURE_DOOR_H * 0.5f, pz - BULKHEAD_T * 0.5f - AbyssConst.PRESSURE_DOOR_W * 0.5f - 0.05f),
                new Vector3(0.06f, AbyssConst.PRESSURE_DOOR_H, AbyssConst.PRESSURE_DOOR_W), 0.015f);
            BeatKit.Emit(mb, _geo, "PressureDoorLeaf", NoKey(mats, MatId.DoorLeaf), _meshes, ShadowCastingMode.On);

            // ---- fixtures (cold emissive strips on the ceiling), one renderer each so they can wake individually
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(0.25f, 0.08f, 1.2f), 0.015f);
            Mesh fixture = BeatKit.Bake(mb, "GalleryFixture", _meshes);
            Material lamp = mats.Variant(MatId.LampCold, "gallery", m => m.SetFloat(ShaderIds.EmissionStrength, FIXTURE_EMISSION));
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                float z = FixtureZ(i);
                float ceil = FloorY(z) + h;
                _fixtureR[i] = BeatKit.Renderer(_geo, "Fixture" + i, fixture, lamp, new Vector3(0f, ceil - 0.045f, z),
                    Quaternion.identity, ShadowCastingMode.Off);
                _fixtureMpb[i] = new MaterialPropertyBlock();
            }

            // ---- amber lamp over the exit (hangs from the ceiling at z = 58.5); its point light hangs 0.25 m under the head
            MeshKit.ChamferBox(mb, new Vector3(0f, yLow + h - 0.08f, EXIT_LAMP_Z), new Vector3(0.22f, 0.14f, 0.22f), 0.02f);
            _exitLamp = BeatKit.Emit(mb, _geo, "ExitLamp", mats[MatId.LampAmber], _meshes, ShadowCastingMode.Off);
            _exitLightPos = new Vector3(0f, yLow + h - 0.15f - EXIT_LAMP_LIGHT_OFFSET, EXIT_LAMP_Z);
        }

        /// <summary>
        /// The doorway reveal (all tiers, R1-07 / R2-01): two world-fixed Megastructure/Glow sprites (DOORWAY_COLD, unfogged),
        /// hidden by depth (per MSAA sample) wherever the gallery, the jambs, the landing, the deck, C1 or the hull stand in
        /// front of them. The near veil on C1's face fills the doorway on the approach and thins out over the threshold
        /// (HazeStrength); the backlight far behind C1 is the lit shaft the column and the deck stand against, dissolving on
        /// the bridge (FarStrength). Strengths are set per frame from d.
        /// </summary>
        void BuildDoorwayGlow(BeatContext ctx, MeshBuilder mb)
        {
            Mesh quad = Beat01Geo.GlowQuad(mb, "GalleryDoorwayGlowQuad", _meshes);
            _hazeGlow = GlowSprite(ctx, quad, "galleryDoorwayHaze", "DoorwayHaze", DOORWAY_COLD, HAZE_GLOW_POS, HAZE_GLOW_DIAMETER, HAZE_DOMAIN);
            _farGlow = GlowSprite(ctx, quad, "galleryDoorwayBacklight", "DoorwayBacklight", DOORWAY_COLD, FAR_GLOW_POS, FAR_GLOW_DIAMETER, HAZE_DOMAIN);
            _hazeGlowStrength = -1f;
            _farGlowStrength = -1f;
            UpdateDoorGlow(ctx.EditMode ? 0f : GameState.D);
        }

        /// <summary>One doorway glow sprite in the given fog domain (strength set per frame through its property block).</summary>
        MeshRenderer GlowSprite(BeatContext ctx, Mesh quad, string variant, string name, Color colour, Vector3 pos, float diameter, float domain)
        {
            Material glow = ctx.Mats.Variant(MatId.Glow, variant, m =>
            {
                m.SetColor(ShaderIds.EmissionColor, colour.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, 0f);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.SetFloat("_EnergyConserve", 0f);
                m.SetFloat("_Ignite", 0f);
                m.SetFloat(ShaderIds.AbDomain, domain);
            });
            MeshRenderer r = BeatKit.Renderer(_geo, name, quad, glow, pos, Quaternion.identity, ShadowCastingMode.Off);
            r.transform.localScale = new Vector3(diameter, diameter, diameter);
            r.receiveShadows = false;
            return r;
        }

        void BuildLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            bool gray = ctx.Tier == BeatTier.Graybox;
            float h = AbyssConst.GALLERY_H;
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                _wakeT[i] = -1f;
                _wakeLevel[i] = ctx.EditMode ? 1f : 0f;
                if (!_fixtureOn[i]) continue;
                float z = FixtureZ(i);
                float y = gray ? FloorY(z) + h - 0.3f : CeilY(z) - FIX_LIGHT_BELOW;
                _fixtureL[i] = BeatKit.Point(lights, "FixtureLight" + i, new Vector3(0f, y, z), AbyssPalette.FIXTURE_LIGHT,
                    ctx.EditMode ? FIXTURE_I : 0f, FIXTURE_RANGE);
                ApplyFixture(i, _wakeLevel[i]);
            }
            // the exit lamp's own small amber point (R1-18), the cold spill of the bulkhead lamp over the doorway outside
            // (R1-07 / R1-37) and the persistent stair-head bounce (R1-16); none fades with d, none casts shadows
            BeatKit.Point(lights, "ExitAmber", _exitLightPos, AbyssPalette.AMBER_LIGHT, EXIT_LAMP_I, EXIT_LAMP_RANGE);
            BeatKit.Spot(lights, "DoorSpill", SPILL_POS, SpillDir, AbyssPalette.COLD_LAMP, SPILL_I, SPILL_RANGE, SPILL_ANGLE);
            _stairGlow = BeatKit.Spot(lights, "StairGlow", STAIR_GLOW_POS, STAIR_GLOW_AIM - STAIR_GLOW_POS, DOORWAY_COLD,
                STAIR_GLOW_I, STAIR_GLOW_RANGE, STAIR_GLOW_ANGLE);
            _stairGlowLevel = -1f;
            ApplyStairGlow(ctx.EditMode ? 0f : GameState.D);
        }

        /// <summary>Stair-glow intensity from d (set on change only; the light is disabled once it is off).</summary>
        void ApplyStairGlow(float d)
        {
            if (_stairGlow == null) return;
            float level = StairGlowLevel(d);
            if (level == _stairGlowLevel) return;
            _stairGlowLevel = level;
            bool on = level > 0f;
            if (_stairGlow.enabled != on) _stairGlow.enabled = on;
            _stairGlow.intensity = STAIR_GLOW_I * level;
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            // The wake ramp runs on real time, which GameBootstrap restarts at 0 when the walk clock begins (ClockDriver.Begin)
            // after the settle frames in which the beats already tick. So no fixture starts waking while the walk clock is
            // held, and the ramp accumulates this beat's own elapsed time from RealTime deltas, a backward jump counting as 0:
            // the fixtures within 20 m of the start ease on over 0.8 s from the first walk frame (14.5.1).
            double now = SceneClock.RealTime;
            float dt = _lastReal >= 0.0 && now > _lastReal ? (float)(now - _lastReal) : 0f;
            _lastReal = now;
            bool walking = ClockDriver.Running;
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                if (_wakeLevel[i] >= 1f) continue;
                if (_wakeT[i] < 0f)
                {
                    if (!walking || f.D + WAKE_AHEAD < FixtureZ(i)) continue;
                    _wakeT[i] = 0f;
                }
                else _wakeT[i] += dt;
                float s = BeatKit.Smoothstep(_wakeT[i] / WAKE_TIME);
                if (s != _wakeLevel[i])
                {
                    _wakeLevel[i] = s;
                    ApplyFixture(i, s);
                }
            }

            UpdateInterior(f.CameraPos);
            if (_fanRotor != null) SetFanAngle(SceneClock.Wrapped(1.0 / FAN_REV_PER_S));
            UpdateDoorGlow(f.D);
            ApplyStairGlow(f.D);
        }

        /// <summary>Interior detail tier by camera distance (dithered, 20 m hysteresis); the fixture lenses and the doorway
        /// spill shaft (own property blocks) follow its coverage.</summary>
        void UpdateInterior(Vector3 cameraPos)
        {
            if (_interior == null) return;
            _interior.Update(cameraPos);
            float c = _interior.Coverage;
            if (c == _coverage) return;
            _coverage = c;
            for (int i = 0; i < FIXTURE_COUNT; i++) ApplyFixture(i, _wakeLevel[i]);
            ApplyShaft();
        }

        /// <summary>Wall fan rotor at 0.5 rev/s from the wrapped scene clock (seconds into the current 2 s turn).</summary>
        void SetFanAngle(double wrappedSeconds)
        {
            float deg = (float)(360.0 * FAN_REV_PER_S * wrappedSeconds);
            _fanRotor.localRotation = _fanBase * Quaternion.AngleAxis(deg, Vector3.forward);
        }

        void UpdateDoorGlow(float d)
        {
            SetGlow(_hazeGlow, _hazeGlowMpb, ref _hazeGlowStrength, HazeStrength(d));
            SetGlow(_farGlow, _farGlowMpb, ref _farGlowStrength, FarStrength(d));
        }

        /// <summary>
        /// Near veil: displayed (post-exposure, pre-tonemap) linear luminance over C1 (the fogged column is HAZE_BG) at path
        /// distance d: HAZE_PEAK up to HAZE_D0, then log2 falls linearly with eased ends (EasedRamp) to log2 HAZE_BG at HAZE_D1.
        /// </summary>
        public static float HazeDisplayed(float d)
        {
            float u = (d - HAZE_D0) / (HAZE_D1 - HAZE_D0);
            if (!(u > 0f)) return HAZE_PEAK;
            if (u >= 1f) return HAZE_BG;
            return Mathf.Pow(2f, Mathf.Lerp(LOG2_HAZE_PEAK, LOG2_HAZE_BG, EasedRamp(u, HAZE_EASE)));
        }

        /// <summary>
        /// Backlight: displayed linear luminance at its centre (background included) at d: FAR_PEAK up to FAR_D0, then log2
        /// eases (smoothstep) to log2 HAZE_BG at FAR_D1 (within 0.35 EV/s at the brisk 2.5 m/s).
        /// </summary>
        public static float FarDisplayed(float d)
        {
            float u = (d - FAR_D0) / (FAR_D1 - FAR_D0);
            if (!(u > 0f)) return FAR_PEAK;
            if (u >= 1f) return HAZE_BG;
            return Mathf.Pow(2f, Mathf.Lerp(LOG2_FAR_PEAK, LOG2_HAZE_BG, BeatKit.Smoothstep(u)));
        }

        /// <summary>
        /// A 0..1 ramp that is linear in its middle and eased (quadratic, C1-continuous) over the first and last fraction e:
        /// slope 1 / (1 - e) in the middle, 0 at both ends.
        /// </summary>
        public static float EasedRamp(float u, float e)
        {
            if (!(u > 0f)) return 0f;
            if (u >= 1f) return 1f;
            float k = 1f / (2f * e * (1f - e));
            if (u < e) return k * u * u;
            if (u > 1f - e) { float v = 1f - u; return 1f - k * v * v; }
            return (u - 0.5f * e) / (1f - e);
        }

        /// <summary>
        /// Near-veil sprite strength at d: its own share of HazeDisplayed (minus the background) divided by the exposure that
        /// ProgressTables applies at d (E = d before the lift) and by the colour's linear luma; the sprite is unfogged (domain 2),
        /// so its centre displays exactly that share. 0 from HAZE_D1 on (renderer disabled).
        /// </summary>
        public static float HazeStrength(float d) => GlowShare(HazeDisplayed(d), d) / HAZE_REF_GAIN;
        static readonly float HAZE_REF_GAIN = HazeRefGain();

        /// <summary>Backlight sprite strength at d (as HazeStrength, from FarDisplayed); 0 from FAR_D1 on.</summary>
        public static float FarStrength(float d) => GlowShare(FarDisplayed(d), d);

        static float GlowShare(float displayed, float d)
        {
            float add = displayed - HAZE_BG;
            if (!(add > 1e-5f)) return 0f;
            float ev = ProgressTables.ExposureAt(ProgressTables.EPre(d));
            return add / (Mathf.Pow(2f, ev) * DOORWAY_COLD_LUMA);
        }

        static void SetGlow(MeshRenderer r, MaterialPropertyBlock mpb, ref float cached, float s)
        {
            if (r == null || mpb == null) return;
            if (Mathf.Abs(s - cached) <= 1e-3f * Mathf.Max(s, 1e-2f)) return;
            cached = s;
            bool on = s > 1e-4f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            mpb.SetFloat(ShaderIds.EmissionStrength, s);
            r.SetPropertyBlock(mpb);
        }

        void ApplyFixture(int i, float level)
        {
            if (_fixtureL[i] != null) _fixtureL[i].intensity = FIXTURE_I * FixtureLightGain(i) * level;
            MeshRenderer r = _fixtureR[i];
            if (r == null || _fixtureMpb[i] == null) return;
            float c = _coverage;
            bool visible = c > 0f;
            if (r.enabled != visible) r.enabled = visible;
            float lens = FixtureLensGain(i);
            if (level >= 1f && c >= 1f && lens >= 1f) { if (r.HasPropertyBlock()) r.SetPropertyBlock(null); return; }
            _fixtureMpb[i].SetFloat(ShaderIds.EmissionStrength, FIXTURE_EMISSION * lens * level);
            _fixtureMpb[i].SetFloat(ShaderIds.AbFade, c >= 1f ? 1f : c);
            r.SetPropertyBlock(_fixtureMpb[i]);
        }

        public override void PrepareCapture(double sceneTime)
        {
            // fixtures that would be awake at the current progress are fully on (the wake ramp is a one-off)
            float d = GameState.D;
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                if (!_editMode && d + WAKE_AHEAD < FixtureZ(i)) continue;
                _wakeT[i] = WAKE_TIME;
                _wakeLevel[i] = 1f;
                ApplyFixture(i, 1f);
            }
            if (_fanRotor != null)
            {
                double period = 1.0 / FAN_REV_PER_S;
                SetFanAngle(sceneTime - period * System.Math.Floor(sceneTime / period));
            }
            UpdateInterior(GameState.CameraPos);
            UpdateDoorGlow(d);
            ApplyStairGlow(d);
        }

        void OnDestroy()
        {
            if (_bolts != null) InstancedRegistry.Unregister(_bolts);
        }

        // ================================================================================================ QA hooks

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            if (keyShotTag == "KS1_gallery_start")
            {
                // Only what the KS1 pose can see: the foreground layer as far as it lies whole inside the frame (the fixture
                // at z 9 and the first metre of tray pass over the frame edges, and a box reaching the near plane projects its
                // centre off screen), the mid layer, the stair head as the far layer and, as the extreme-far substitute for
                // the hidden doorway, the far glow: the lower sloped stair ceiling lit by the StairGlow (R1-16). The exit
                // section, the amber lamp and the doorway are hidden behind that ceiling (STAIR_HEAD_DEPTH); they are KS1b's.
                AddSubject(subjects, "foreground fixtures", ForegroundFixtureBounds(), 0f);
                AddSubject(subjects, "foreground tray", _trayNearBounds, 0f);
                AddSubject(subjects, "pressure door", _pressureDoorBounds, 0f);
                AddSubject(subjects, "wall fan", _fanBounds, 0f);
                AddSubject(subjects, "stair head", _stairHeadBounds, 0f);
                AddSubject(subjects, "far glow", _farGlowBounds, 0f);
            }
            else if (keyShotTag == "KS1b_doorway")
            {
                AddSubject(subjects, "doorway", _doorwayBounds, 0.2f);
                AddSubject(subjects, "exit amber lamp", BeatKit.BoundsOf(_exitLamp), 0f);
            }
        }

        /// <summary>
        /// Union of the built fixtures (any tier: Simplified keeps the even ones) that lie whole between KS1_EYE_Z +
        /// KS1_FRAME_DZ and KS1_FOREGROUND_Z1, i.e. the z 8-16 foreground fixtures the KS1 frame shows entirely (fixture 2).
        /// </summary>
        Bounds ForegroundFixtureBounds()
        {
            var u = new Bounds();
            bool any = false;
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                MeshRenderer r = _fixtureR[i];
                if (r == null) continue;
                Bounds b = r.bounds;
                if (b.min.z < KS1_EYE_Z + KS1_FRAME_DZ || b.max.z > KS1_FOREGROUND_Z1) continue;
                if (any) u.Encapsulate(b);
                else { u = b; any = true; }
            }
            return u;
        }

        static void AddSubject(List<KeyShotSubject> subjects, string name, Bounds b, float minFraction)
        {
            if (b.extents.sqrMagnitude <= 0f) return;   // part absent in this tier
            subjects.Add(new KeyShotSubject(name, b, minFraction));
        }

        public override void ReportScale(ScaleReport report)
        {
            if (_shell != null)
            {
                Bounds b = _shell.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("gallery.width", b.size.x);
                report.Add("gallery.height", b.max.y - AbyssConst.W[0].y);
            }
            if (_jambL != null && _jambR != null && _lintel != null)
            {
                report.Add("gallery.door.width", _doorWidth);
                report.Add("gallery.door.height", _doorHeight);
                report.Add("gallery.length", _galleryLength);
                report.Add("gallery.door.depth", AbyssConst.EXIT_DEPTH);
            }
            if (_pdWidth > 0f)
            {
                report.Add("pressure.door.width", _pdWidth);
                report.Add("pressure.door.height", _pdHeight);
            }
            if (_treads != null)
            {
                float riseMin = float.MaxValue, riseMax = 0f, runMin = float.MaxValue, runMax = 0f;
                float prev = AbyssConst.W[1].y;
                int count = 0;
                for (int k = 0; k < AbyssConst.STAIR_COUNT; k++)
                {
                    if (_treadRun[k] > 0f) count++;
                    float rise = prev - _treadTop[k];
                    prev = _treadTop[k];
                    riseMin = Mathf.Min(riseMin, rise); riseMax = Mathf.Max(riseMax, rise);
                    runMin = Mathf.Min(runMin, _treadRun[k]); runMax = Mathf.Max(runMax, _treadRun[k]);
                }
                report.Add("gallery.stair.count", count);
                report.Add("gallery.stair.rise.min", riseMin);
                report.Add("gallery.stair.rise.max", riseMax);
                report.Add("gallery.stair.run.min", runMin);
                report.Add("gallery.stair.run.max", runMax);
                report.Add("gallery.stair.drop", AbyssConst.W[1].y - _treadTop[AbyssConst.STAIR_COUNT - 1]);
            }
            int fixtures = 0;
            for (int i = 0; i < FIXTURE_COUNT; i++) if (_fixtureR[i] != null) fixtures++;
            report.Add("gallery.fixtures.count", fixtures);
            ReportFullScale(report);
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);
    }
}
