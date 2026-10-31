using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 7 honeycomb (14.5.7 Geometry, 14.7.7): the cells of the lattice that S0 belongs to, on the terrace plane y = -1087.
    ///
    /// Cell (q, r): flat-top axial coordinates exactly as the Lattice shader (size 760 / sqrt 3, Red Blob Games), centre =
    /// S0 centre + q b2 + r b1 with b1 = (0, 760), b2 = (658.18, 380); lamp (the neighbour-bridge midpoint) at the centre +
    /// (0, -269). Every neighbour cell is the 14.5.7 recipe: a hexagonal tube of apothem 350, 600 m deep, no cap, its rings at
    /// the S0 ring levels (y = 1170 - 60 k, belts where k % 4 == 0), the landing at the near flat and the bridge (deck y -1089)
    /// from the near flat + 6 to + 156 with one amber lamp 1.1 m above the deck.
    ///
    /// R1-03 (critique r1, S1 on the money shot): the r0 cells also carried S0's columns C1-C4 with flat caps 0.65 m under the
    /// web tops. Seen from the balcony they read as sunlit pegs in a shallow egg crate, not as shafts falling 600 m; the 14.5.7
    /// recipe has no columns, so they are gone (with C1's ledge ring). The holes now fall to near-black with depth (R1-03 fix
    /// (2): "depth attenuation of all in-tube lighting"): the sky light that reaches into a 700 m wide, 600 m deep tube falls
    /// off as the visible sky shrinks, vertex colour R (the AO term of ambient and environment) = exp(-depth /
    /// HONEY_AO_FALLOFF); the rings and the landing carry no Key (the inner walls the balcony looks into face away from the
    /// sun, which stands ahead at elevation 40, so their rib tops lie in the wall's shadow: the sunlit rib tops were the
    /// "terrace steps"); the walls keep the Key with bent normals so the sun on the sun-facing walls fades with the same
    /// exp(-depth / L). The Lattice (tier 2) darkens its analytic holes, sun and sky alike, with the same falloff
    /// (_DepthFalloff), so tier 1 and tier 2 agree (14.10 c). Aerial perspective comes from the Exterior fog on top of the
    /// near-black holes (near cells about 0.05-0.1, the 3-6 km cells lifted by the haze).
    ///
    /// Bridges (R1-12): every neighbour bridge is at least 0.8 km from any camera of the walk, where its 1.2 m deck is 0.3-1 px.
    /// The chord, posts, lamp post and steel bevels drew dotted 1 px strings with sub-pixel specular specks; each bridge is now
    /// the deck alone, a pale concrete thread widened by the ThinLine pass to BRIDGE_LINE_PX with alpha-to-coverage, the
    /// same mesh and material values on tiers 0 and 1 (no stipple at the 1.6 km hand-over), dimming to the hole tone by
    /// _FarRange before the 5.4 km lattice hand-over (the Lattice draws no bridges). The lamp is the Glow sprite.
    ///
    /// Tiers (world-fixed cell sets around the walk; the camera-distance part is the per-pixel dithered _DistFade):
    /// - tier 0 (real tubes: walls, 10 rings, landing, bridge deck) to 1.6 km, near-tier fade over 1.44-1.76 km (+/-10 % of the
    ///   tier radius, 14.5.7 pitfalls);
    /// - tier 1 from 1.44 km (far-tier fade) to 6.6 km. The cells that take part in the 1.6 km hand-over draw the tier-0
    ///   tube and bridge, so the dithered swap exchanges identical geometry; the others draw walls, deck, the 2 belts, and
    ///   out to 3.7 km (a fixed cell set) the 8 ribs. Their tops (web annulus, top 0.65 m of the walls) fade out over
    ///   5.4-6.6 km; everything below the lattice plane is never faded and is covered by the lattice plane wherever that is
    ///   kept, so the per-pixel fades of two surfaces at different depths can never leave a pixel uncovered. Below the plane the
    ///   tier-1 tube shows what the Lattice's analytic hole shows (walls with the same depth falloff, no columns, the belts too
    ///   deep to be seen past 5 km), so the 5.4-6.6 km dither exchanges matching tones (R1-13);
    /// - tier 2: the Lattice shader on a camera-following annulus (BackdropRig: 100 m grid, rotation locked, y -1087.6,
    ///   radius 28 km), shown beyond 5.4 km (_DistFade 5400-6600, far tier).
    /// Lamps: one instanced Glow sprite per cell (>= 2.5 px, ignite from _AbLampIgnite) out to the lattice band; inside the
    /// cross-fade band the sprites are grouped in 150 m rings whose brightness follows 1 - s(r) so sprites + lattice dots sum
    /// to one lamp. Simplified tier: tiers 0 and 2 only (lattice and lamp hand-over at 1.44-1.76 km).
    /// QaHooks.TierOverride (14.10 c): 1 = tier 1 only (tier-1 tops unfaded, the lattice only beyond tier 1's coverage, so the
    /// field beyond 7 km is the same in both images), 0 = tier 2 only (tier 1 and the sprites off, lattice unfaded).
    /// </summary>
    public sealed partial class Beat07_Reveal
    {
        // ---------------------------------------------------------------- honeycomb constants (14.5.7 / 14.7.7)
        /// <summary>The lamp of a cell (its bridge midpoint) sits at the cell centre + (0, -HONEY_LAMP_OFFSET).</summary>
        public const float HONEY_LAMP_OFFSET = 269f;
        public const float HONEY_TUBE_DEPTH = 600f;
        public const float HONEY_DECK_TOP_Y = -1089f;
        public const float HONEY_LAMP_HEIGHT = 1.1f;
        /// <summary>The bridge runs along z from the cell's near flat + 6 to + 156 (S0: z 66-216 with the near flat at 60).</summary>
        public const float HONEY_BRIDGE_NEAR = 6f, HONEY_BRIDGE_FAR = 156f;
        /// <summary>The lattice plane lies 0.6 m under the web tops (y -1087.6).</summary>
        public const float LATTICE_DROP = 0.6f;
        /// <summary>Tops of the tubes (annulus and the top band of the walls) are everything above -0.65 m: above the lattice plane.</summary>
        public const float HONEY_TOP_BAND = 0.65f;
        public const float TIER_FADE_FRACTION = 0.1f;
        public static readonly float TIER0_FADE_START = AbyssConst.HONEY_TIER0 * (1f - TIER_FADE_FRACTION);   // 1440
        public static readonly float TIER0_FADE_END = AbyssConst.HONEY_TIER0 * (1f + TIER_FADE_FRACTION);     // 1760
        /// <summary>Lattice / tier-1 dithered cross-fade end (14.7.7: 5.4-6.6 km).</summary>
        public const float TIER2_FADE_END = 6600f;
        /// <summary>Tier-1 cells nearer than this (centre) use the 1.6 km far-tier fade, the others the 6 km near-tier fade.</summary>
        public const float TIER1_SPLIT = 3500f;
        /// <summary>
        /// The 6 m ribs of the plain tier-1 cells reach to TIER1_RIBS_END (cell centre from HONEY_REF; a fixed cell set, no
        /// fade): there a rib is about 1.6 px, beyond it would only alias. The set starts past every hand-over cell. The belts
        /// (12 m) and columns stay to 6.6 km, under the lattice plane, which hands them over with the walls. (r02: a 2.9-3.7 km
        /// dithered dissolve of columns and belts showed as IGN stipple on the 44-72 m column tops, 10+ px wide there.)
        /// </summary>
        public const float TIER1_RIBS_START = 2900f, TIER1_RIBS_END = 3700f;
        /// <summary>Margin added to every cell set for the camera travelling around HONEY_REF (d 640 in the tunnel to the door).</summary>
        public const float HONEY_MARGIN = 100f;
        /// <summary>Centre of the camera range from which the honeycomb is seen (x -45, z from 88.5 at d 640 to -74).</summary>
        public static readonly Vector2 HONEY_REF = new Vector2(AbyssConst.W[15].x,
            0.5f * (PathGuide.Beat7Z(AbyssConst.D_EXTERIOR_ON) + AbyssConst.GOAL_DOOR_Z));
        /// <summary>Circumradius of the half-pitch hexagon (web centre lines) of a cell.</summary>
        public static readonly float CELL_CIRCUMRADIUS = 0.5f * AbyssConst.LATTICE_PITCH / Mathf.Cos(30f * Mathf.Deg2Rad);

        const float RING_EMBED = 0.05f, ANNULUS_OVERLAP = 0.05f;
        const float LANDING_T = 1f;
        const float DECK_W = 1.2f, DECK_T = 0.3f, DECK_DROP = 0.02f;
        /// <summary>
        /// Sky-light falloff into a tube (m): the AO of every surface below the web band is exp(-depth / this), and the
        /// Lattice's holes use the same value as _DepthFalloff. R1-03 asked for L about 60-80 m plus a downward per-tube fog
        /// in domain 2 (a shader change outside this beat); 45 m is the single falloff that does both jobs: reveal r07 at
        /// 60 m left the 1.5-3 km holes at 0.15-0.27 luma (their visible far walls reach only 140-200 m down), 45 m takes them
        /// toward 0.1 while the near holes are black below two rib levels. The old 140 m albedo-only falloff left the tubes an
        /// even mid-grey. R2-04: 32 m now that the aerial veil (HONEY_AIR) lifts the pits off black: the
        /// wall gradient steps down faster under the nested ring outlines (110 m), and the far holes of tier 1 and the Lattice
        /// (whose visible depth is 25-80 m past 5 km) read darker against their webs (f03: 4-6 px cells at 0.013 median
        /// web/hole difference, the bulk of the unreadable cells).
        /// R2-04 (lead look at lead_b01 / reveal r31): at 32 m the near pits were dark below the first ring and the veil made
        /// them one flat tone, a shallow tray. The tube walls now carry 80 m (the far wall of a near pit, 200-500 m of it in
        /// view, grades from the lit lip down into darkness across three or four rings); the far holes keep their contrast
        /// through HONEY_FAR_FALLOFF on the Lattice and the matching far tint of the tier-1 walls (WALL_FAR_TINT).
        /// </summary>
        public const float HONEY_AO_FALLOFF = 80f;
        /// <summary>
        /// The Lattice's analytic holes (5.4 km on, visible depth 25-80 m) fall off over this short length: dark holes against
        /// the sunlit webs, readable cells (reveal r32 at 32 m: the 4-8 km row at a median web/hole difference of 0.033, 60 of
        /// 112 cells readable, 129 in all; a shorter length, about 20 m, is the next lever, not yet verified).
        /// </summary>
        public const float HONEY_FAR_FALLOFF = 32f;
        /// <summary>
        /// Albedo factor of the tier-1 walls and rings beyond 3.2 km (their _FarTint, x FAR_ALBEDO_GAIN): the mean of
        /// exp(-depth / HONEY_FAR_FALLOFF) over the top 60 m (a 5-6 km hole's visible depth) over the mean of
        /// exp(-depth / HONEY_AO_FALLOFF), so the 5.4-6.6 km dither exchanges holes of equal mean tone (14.10 c).
        /// </summary>
        public static float WALL_FAR_TINT => MeanAO(HONEY_FAR_FALLOFF, 60f) / MeanAO(HONEY_AO_FALLOFF, 60f);
        static float MeanAO(float L, float depth) => L / depth * (1f - Mathf.Exp(-depth / L));
        /// <summary>The HoneyTube recipe's own albedo falloff is switched off (the AO above carries the depth).</summary>
        const float NO_ALBEDO_FALLOFF = 1e7f;
        /// <summary>
        /// Depths (m below the web tops) of the vertex rows of every tube wall: dense near the top where the AO changes fast,
        /// so the linear interpolation of exp(-depth / 45) between rows stays within about 4 % (16 rows, 192 triangles).
        /// </summary>
        static readonly float[] WALL_ROWS = { 6f, 12f, 19f, 27f, 36f, 46f, 58f, 72f, 88f, 106f, 128f, 155f, 190f, 240f, 290f, 345f, 420f };
        /// <summary>
        /// Neighbour bridges as threads: ThinLine width (px) of the deck, and the camera distances (m) over which the deck's
        /// albedo goes to 0 (_FarRange) so it has vanished into the hole before the lattice hand-over at 5.4 km.
        /// </summary>
        public const float BRIDGE_LINE_PX = 2f;
        static readonly Vector4 BRIDGE_FAR_RANGE = new Vector4(2800f, 5200f, 0f, 0f);
        static readonly int FarTintId = Shader.PropertyToID("_FarTint");
        static readonly int FarRangeId = Shader.PropertyToID("_FarRange");
        static readonly int DepthFalloffId = Shader.PropertyToID("_DepthFalloff");
        /// <summary>
        /// Honeycomb lamps (Glow sprites and the Lattice's analytic dots). AMBER x 8 (the 14.2 peak) went through the Neutral
        /// tonemapper as near-white dots (f01 KS7b: sRGB about 220, 215, 209). R2-21 / lead decision D4: small and distant amber
        /// emitters stay at or under 2.5; at 3.0 the f03 dot cores still measured (221, 198, 171), HSV saturation 0.22 (near
        /// dots over the pits hue 34-38, saturation 0.30-0.39). reveal r21 at 1.6: near dots hue 24-26, saturation 0.42-0.47;
        /// 2.0 sits between (hue rises, saturation falls with strength). The sprite size (>= 3.2 px), the halo and bloom carry
        /// the visibility.
        /// </summary>
        public const float GLOW_DIAMETER = 0.6f, GLOW_STRENGTH = 2.0f, GLOW_MIN_PX = 3.2f;
        static readonly int LatticeParamsId = Shader.PropertyToID("_LatticeParams");
        static readonly int GlowIgniteId = Shader.PropertyToID("_Ignite");
        /// <summary>
        /// The Lattice's dots (beyond 6.6 km, cross-faded with the sprites over 5.4-6.6 km, so the step is a smooth ramp) are
        /// brighter by this factor: past 7 km the Exterior fog keeps under 40 % of an additive lamp and the dots would end
        /// before the readable cells do (14.7.7: "far the lattice to 10 km with readable cells and amber dots").
        /// GLOW_STRENGTH x this = 2.24, under the D4 cap of 2.5.
        /// </summary>
        public const float LATTICE_LAMP_GAIN = 1.4f;
        /// <summary>D4: the emissive cap of small / distant amber emitters (honeycomb dots, far sprites).</summary>
        public const float AMBER_DOT_CAP = 2.5f;
        /// <summary>
        /// R2-20: a faint amber halo around every lamp nearer than HALO_RANGE (a second Glow sprite: 4 x the true lamp
        /// diameter, at least HALO_MIN_PX, strength at most 0.15), so the near shafts visibly light up around their bridge.
        /// </summary>
        public const float HALO_DIAMETER = 2.4f, HALO_STRENGTH = 0.1f, HALO_MIN_PX = 11f, HALO_RANGE = 3200f;

        // ---------------------------------------------------------------- ignition (R2-20, arbitration of 14.5.7 Motion)
        /// <summary>
        /// R2-20: the ignition wave is distance-warped so the visible band takes most of the time: a lamp at distance d from
        /// the ignition point lights after IGNITE_SECONDS x (sqrt d - sqrt D0) / (sqrt D1 - sqrt D0) (critic A's square-root
        /// curve): 0.7 km at 0 s, 1 km 0.6 s, 2 km 3.0 s, 3 km 4.1 s, 5 km 6.4 s, 8 km 9.1 s, 10 km 10.6 s, 12 km 12 s. f03 (linear
        /// 2.33 km/s) lit everything visible within about 1.8 s. Each lamp still ramps with a 1 s smoothstep (no flicker).
        /// </summary>
        public const float IGNITE_D0 = 700f, IGNITE_D1 = 12000f;
        /// <summary>Per-lamp ramp (s), as the shader's AB_LAMP_RAMP_S.</summary>
        public const float LAMP_RAMP = 1f;
        /// <summary>The sprites of one ignition ring light together; the rings are this many seconds of the curve apart.</summary>
        const float IGN_RING_STEP = 0.4f;
        /// <summary>Ignition reference point (the balcony front on the walk axis) for the ring assignment of the sprites.</summary>
        public static readonly Vector2 IGNITE_REF = new Vector2(AbyssConst.W[15].x, HONEY_IGNITE_Z);
        /// <summary>
        /// The Lattice's analytic dots can only ignite linearly (_AbLampIgnite: delay = distance / speed in the shader), so its
        /// start is shifted and its speed fitted to meet the curve at 5.4 km (the sprite / lattice hand-over) and at 12 km:
        /// 1 267 m/s and +2.5 s, within 0.3 s of the curve over 5.4-12 km.
        /// </summary>
        public static float LatticeIgniteSpeed => (IGNITE_D1 - AbyssConst.HONEY_TIER2_START) / (IGNITE_SECONDS - IgniteDelay(AbyssConst.HONEY_TIER2_START));
        public static float LatticeIgniteOffset => IgniteDelay(AbyssConst.HONEY_TIER2_START) - AbyssConst.HONEY_TIER2_START / LatticeIgniteSpeed;

        /// <summary>Ignition delay (s) of a lamp at horizontal distance d (m) from the ignition point (R2-20 curve).</summary>
        public static float IgniteDelay(float d)
        {
            float s0 = Mathf.Sqrt(IGNITE_D0), s1 = Mathf.Sqrt(IGNITE_D1);
            return IGNITE_SECONDS * (Mathf.Sqrt(Mathf.Max(d, IGNITE_D0)) - s0) / (s1 - s0);
        }

        // ---------------------------------------------------------------- aerial perspective and value staging (R2-04, R2-19)
        /// <summary>
        /// R2-04 (1) / R2-19 (3): the air of the climax, the _HoneyAir of the shader (AbyssHoneycomb.hlsl AbHoneyAir: x aerial
        /// veil amount toward OUT_HORIZ, y its camera-distance length m, z tube-air depth length m, w tube-air amount toward
        /// OUT_HORIZ x 0.085), set on EVERY honeycomb variant and the Lattice (one vector, a continuous function of camera
        /// distance and depth: no per-cell steps, and the 14.10 (c) tier match holds by construction). The Exterior fog
        /// (D50 7 000) adds only about 2 % at the 0.7-1.2 km slant to the near cells, so nothing marked the 600 m of air.
        /// lead_b01 (MaterialBuilder default (0.22, 3000, 120, 1) plus a banded additive lift) failed the other way: the tube
        /// air took every wall below about 150 m to one flat 0.22 haze tone, brighter than the dip under the lit lip, so the
        /// deepest part of each pit was its lightest and the pits read as shallow trays with a floor (lead look at lead_b01).
        /// Here the walls fall into darkness under their own sky-light AO (HONEY_AO_FALLOFF) with the nested ring outlines
        /// (HONEY_RING_AO_FALLOFF) stepping down, the tube air only keeps the bottom off black (w 0.35: the deep floor about
        /// OUT_HORIZ x 0.03, darker than every ring above it, so depth reads monotonic), and the aerial veil carries the air
        /// between the balcony and the terrace: 8 % at 0.8 km, 11 % at 1.5 km, 13.5 % at 3 km, saturating at 14 % (the
        /// Exterior fog takes over beyond 5 km) so the 5-9 km cells keep their web/hole contrast (readable cells, R2-04 (4)).
        /// reveal r31 at 20 % (with 32 m wall AO): the near pits one flat 0.23-0.24 tone, the veil hid the fall into darkness.
        /// The pier and balcony (ConcreteFloor, no _HoneyAir) stay unveiled in front of it.
        /// </summary>
        public static readonly Vector4 HONEY_AIR = new Vector4(0.14f, 900f, 150f, 0.35f);
        /// <summary>Mirror of the shader's AB_TUBE_AIR_K (tube-air colour = OUT_HORIZ x this).</summary>
        public const float TUBE_AIR_K = 0.085f;
        static readonly int HoneyAirId = Shader.PropertyToID("_HoneyAir");
        /// <summary>
        /// R2-04 (3): the ring tops and inner faces fall off with this sky-light length instead of the walls' 45 m, so 4-5
        /// nested ring outlines step down into each near pit, each dimmer and closer to the haze (the shaft's "ribs fading to
        /// black" motif). The Lattice draws no rings, and below 5.4 km the visible depth of a hole (under 80 m) never reaches
        /// a belt, so 14.10 (c) is unaffected.
        /// </summary>
        public const float HONEY_RING_AO_FALLOFF = 110f;
        /// <summary>
        /// R2-04 (2) / R2-19 (1): the near web tops are lighter than the walls by this gain (was 1.35: the near web tops at
        /// 0.48-0.50 were the brightest layer below the horizon, brighter than the pier). With distance their albedo blends
        /// (_FarTint over WEB_FAR_RANGE) to WEB_FAR_GAIN, the Lattice's web (lit rim x 1.35 over the whole 30 m half web, the
        /// only lever on the far web-to-hole contrast that survives its footprint rules past 7 km), so near webs sit under the
        /// pier and the far field keeps its readable cells; the tier-1 tops meet the Lattice's webs at 5.4-6.6 km.
        /// </summary>
        public const float WEB_TOP_GAIN = 1.0f, WEB_FAR_GAIN = 1.35f;
        public static float LATTICE_RIM_W => 0.5f * (AbyssConst.LATTICE_PITCH - AbyssConst.SHAFT_FLAT_TO_FLAT) * (WEB_FAR_GAIN - 1f) / 0.35f;
        /// <summary>
        /// R2-19 (2): the web tops carry the ConcreteBoard macro tone at this amplitude (x +/-12 % of the 9 m / 37 m noise), so
        /// the 60 m webs read as weathered pours at 0.7-3 km instead of a moulded tray; their albedo blends to the plain web
        /// tone (_FarTint) over WEB_FAR_RANGE, so the tier-1 tops meet the Lattice's uniform webs at the 5.4-6.6 km dither.
        /// </summary>
        public const float WEB_MACRO_AMP = 2.4f;
        static readonly Vector4 WEB_FAR_RANGE = new Vector4(3200f, 5200f, 0f, 0f);
        /// <summary>
        /// R2-04 (4): beyond 3.2 km every honeycomb albedo (tier-1 webs, walls, rings, via _FarTint over WEB_FAR_RANGE) and the
        /// Lattice's base colour are this much lighter, which scales the 5-9 km web/hole difference by the same factor
        /// (lead_b01: median 0.026 against the 0.03 readable threshold in the 4-8 km rows; 114 readable cells). The near
        /// cells keep CONC_LIGHT (under the pier); tiers 1 and 2 still share every albedo (14.10 c).
        /// </summary>
        public const float FAR_ALBEDO_GAIN = 1.2f;
        /// <summary>
        /// R2-30: the C1 landing of every neighbour cell (14.5.7 recipe: S0 has its column C1 at the bridge's far end): a flat
        /// round pad of the C1 ledge's outer radius (28 m) at deck level, 1 m thick, in the rings' depth material (no Key, the
        /// ring AO), so each bridge thread ends on something instead of in mid-air. Flat and below the deck top: no peg.
        /// </summary>
        public const float PAD_THICK = 1f, PAD_BEVEL = 0.3f, PAD_DROP = 0.05f;
        const int PAD_SEGMENTS = 40;
        /// <summary>Cell-local centre (x, z) of the C1 landing: C1's offset from the S0 centre.</summary>
        public static Vector2 PadCentre => new Vector2(AbyssConst.COLUMNS[0].x - AbyssConst.SHAFT_CENTRE.x, AbyssConst.COLUMNS[0].y - AbyssConst.SHAFT_CENTRE.y);
        /// <summary>
        /// R2-29: the deck thread stops this far (m) either side of the lamp: the ThinLine-widened thread passing in front of
        /// the 3 px lamp sprite took part of its MSAA samples (a 2 x 2 checker inside the dots near 1.6 km); the sprite covers
        /// the gap (6 m along the bridge is under 3 px from every camera of the walk).
        /// </summary>
        public const float DECK_LAMP_GAP = 3f;
        /// <summary>
        /// Lamp colour as uploaded. The [HDR] _EmissionColor of Glow / Lattice reaches the GPU unconverted (r01 KS7b: the
        /// dots measured as AMBER's sRGB triple (1, 0.706, 0.353) through the grade, not its linear value), so the palette's
        /// LINEAR amber (1, 0.456, 0.102) is passed explicitly.
        /// </summary>
        static Color LampColor => AbyssPalette.AMBER.linear;
        /// <summary>
        /// The honeycomb concrete is CONC_LIGHT (linear 0.26, a realistic albedo for sunlit exterior concrete; CONC_MID, 0.11,
        /// is the shaded interior tone), on every tier (tubes, tops and the Lattice) so the tier-1 / tier-2 match of 14.10 (c)
        /// holds. Above all the absolute web-to-hole difference must survive the 7 km haze: at CONC_MID the cells beyond 7 km
        /// fell to 0.017 luma (KS7b f01: 77 readable cells), under the 0.03 of the 14.5.7 acceptance.
        /// </summary>
        static Color HoneyAlbedoLinear => AbyssPalette.CONC_LIGHT.linear;
        const int GLOW_BANDS = 9;
        const float GLOW_BAND_TAIL = 50f;
        const float T0_CHUNK = 5000f, T1_CHUNK = 8000f, GLOW_CHUNK = 16000f;
        const int LATTICE_SEGMENTS = 256;
        const float LATTICE_GRID = 100f;
        /// <summary>
        /// Lattice annulus rings (m). The full tier shows the lattice only beyond 5.4 km (_DistFade), but the mesh starts at
        /// 1.3 km so that the QA "tier 2 only" image (QaHooks.TierOverride 0, lattice unfaded) covers the whole 14.10 (c)
        /// comparison region (reveal r06: from 5.25 km the region's lower third showed sky against tier 1's cells, 0.033). In
        /// play the inner rings are discarded per pixel by the fade before any shading.
        /// </summary>
        static readonly float[] LATTICE_RINGS_FULL = { 1300f, 1800f, 3000f, 5250f, 6600f, 9000f, 13000f, 19000f, AbyssConst.HONEY_TIER2 };
        static readonly float[] LATTICE_RINGS_SIMPLE = { 1300f, 1800f, 3000f, 6000f, 10000f, 16000f, AbyssConst.HONEY_TIER2 };
        /// <summary>
        /// Bottom of every tube 600 m down (linear): the walls' asymptote under the tube air (OUT_HORIZ x TUBE_AIR_K x
        /// HONEY_AIR.w, a dark blue-grey), never pure black. The ProxyCard has no _HoneyAir, so the card carries the tube air
        /// and the aerial veil of the only cells whose bottom can be seen (within about 0.8 km: depression over 40 degrees).
        /// </summary>
        static Color CAP_LINEAR
        {
            get
            {
                Color h = AbyssPalette.OUT_HORIZ.linear;
                float veil = HONEY_AIR.x * (1f - Mathf.Exp(-800f / HONEY_AIR.y));
                float k = TUBE_AIR_K * HONEY_AIR.w * (1f - veil) + veil;
                return new Color(h.r * k, h.g * k, h.b * k, 1f);
            }
        }

        readonly List<Vector2Int> _tier0Cells = new List<Vector2Int>(64);
        readonly List<InstancedBatch> _allBatches = new List<InstancedBatch>(32);
        readonly List<InstancedBatch> _tier0Batches = new List<InstancedBatch>(4);
        readonly List<InstancedBatch> _tier1Batches = new List<InstancedBatch>(8);
        readonly List<InstancedBatch> _glowBatches = new List<InstancedBatch>(GLOW_BANDS + 48);
        InstancedBatch[] _bandBatches;
        float[] _bandMid, _bandIgnDelay;
        InstancedBatch[] _ringBatches, _haloBatches;
        float[] _ringDelay;
        float _glowFadeStart, _glowFadeEnd;
        Material _t1TopMat;
        Vector4 _t1TopFade, _latticeFade, _latticeT1OnlyFade;
        int _honeyMode = -1;
        bool _honeyOnApplied, _glowShown;
        int _tier1Count, _lampCount, _ringCount;
        float _tubeDepth, _bridgeLength, _deckTopY;

        // ================================================================================================ cell maths

        /// <summary>Lattice hex size (flat-top, centre to corner of the pitch hexagon): 760 / sqrt 3 = 438.786.</summary>
        public static double HexSize => AbyssConst.LATTICE_PITCH / System.Math.Sqrt(3.0);

        /// <summary>Centre (x, z) of cell (q, r): S0 centre + q b2 + r b1 (flat-top axial, as the Lattice shader).</summary>
        public static Vector2 CellCentre(int q, int r)
        {
            double s = HexSize, r3 = System.Math.Sqrt(3.0);
            double x = 1.5 * s * q;
            double z = s * (0.5 * r3 * q + r3 * r);
            return new Vector2((float)(AbyssConst.SHAFT_CENTRE.x + x), (float)(AbyssConst.SHAFT_CENTRE.y + z));
        }

        /// <summary>Lamp (bridge midpoint) of cell (q, r): its centre + (0, -269).</summary>
        public static Vector2 LampXZ(int q, int r) => CellCentre(q, r) + new Vector2(0f, -HONEY_LAMP_OFFSET);

        /// <summary>Cell containing the world point (x, z): axial coordinates with cube rounding (Red Blob Games, as the shader).</summary>
        public static Vector2Int CellOf(Vector2 xz)
        {
            double s = HexSize, r3 = System.Math.Sqrt(3.0);
            double px = xz.x - AbyssConst.SHAFT_CENTRE.x, pz = xz.y - AbyssConst.SHAFT_CENTRE.y;
            double fq = (2.0 / 3.0 * px) / s;
            double fr = (-1.0 / 3.0 * px + r3 / 3.0 * pz) / s;
            double fs = -fq - fr;
            double rq = System.Math.Round(fq, System.MidpointRounding.AwayFromZero);
            double rr = System.Math.Round(fr, System.MidpointRounding.AwayFromZero);
            double rs = System.Math.Round(fs, System.MidpointRounding.AwayFromZero);
            double dq = System.Math.Abs(rq - fq), dr = System.Math.Abs(rr - fr), ds = System.Math.Abs(rs - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new Vector2Int((int)rq, (int)rr);
        }

        /// <summary>Cells whose centre lies at a distance in [rMin, rMax) from centre (x, z), S0 (0, 0) optionally excluded.</summary>
        public static void CellsInRange(Vector2 centre, float rMin, float rMax, List<Vector2Int> into, bool excludeS0 = true)
        {
            double sx = 1.5 * HexSize, pitch = AbyssConst.LATTICE_PITCH;
            Vector2 s0 = AbyssConst.SHAFT_CENTRE;
            int qMin = (int)System.Math.Floor((centre.x - rMax - s0.x) / sx) - 1;
            int qMax = (int)System.Math.Ceiling((centre.x + rMax - s0.x) / sx) + 1;
            for (int q = qMin; q <= qMax; q++)
            {
                double z0 = s0.y + 0.5 * pitch * q;
                int rLo = (int)System.Math.Floor((centre.y - rMax - z0) / pitch) - 1;
                int rHi = (int)System.Math.Ceiling((centre.y + rMax - z0) / pitch) + 1;
                for (int r = rLo; r <= rHi; r++)
                {
                    if (excludeS0 && q == 0 && r == 0) continue;
                    float d = (CellCentre(q, r) - centre).magnitude;
                    if (d >= rMin && d < rMax) into.Add(new Vector2Int(q, r));
                }
            }
        }

        /// <summary>Number of cells (S0 included) whose centre lies within radius of centre: 4 927 inside 28 km of S0 (14.11: "Shaft 1 of 4,920").</summary>
        public static int CountCellsWithin(Vector2 centre, float radius)
        {
            double sx = 1.5 * HexSize, pitch = AbyssConst.LATTICE_PITCH;
            Vector2 s0 = AbyssConst.SHAFT_CENTRE;
            int n = 0;
            int qMin = (int)System.Math.Floor((centre.x - radius - s0.x) / sx) - 1;
            int qMax = (int)System.Math.Ceiling((centre.x + radius - s0.x) / sx) + 1;
            for (int q = qMin; q <= qMax; q++)
            {
                double z0 = s0.y + 0.5 * pitch * q;
                int rLo = (int)System.Math.Floor((centre.y - radius - z0) / pitch) - 1;
                int rHi = (int)System.Math.Ceiling((centre.y + radius - z0) / pitch) + 1;
                for (int r = rLo; r <= rHi; r++)
                    if ((CellCentre(q, r) - centre).sqrMagnitude <= radius * radius) n++;
            }
            return n;
        }

        // ================================================================================================ build

        void BuildHoneycomb(BeatContext ctx, MeshBuilder mb, bool simplified)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform root = BeatKit.Group(_geo, "Honeycomb");

            // ---- world-fixed cell sets around the camera range
            _tier0Cells.Clear();
            CellsInRange(HONEY_REF, 0f, TIER0_FADE_END + CELL_CIRCUMRADIUS + HONEY_MARGIN, _tier0Cells);
            // tier-1 cells nearer than TIER1_SPLIT: those that meet the 1.6 km hand-over (t1Hand) and the rest (t1Rest)
            float handOver = TIER0_FADE_END + CELL_CIRCUMRADIUS + HONEY_MARGIN;
            var t1Hand = new List<Vector2Int>(64);
            var t1Rest = new List<Vector2Int>(96);
            var t1In = new List<Vector2Int>(160);
            var t1Out = new List<Vector2Int>(320);
            if (!simplified)
            {
                CellsInRange(HONEY_REF, TIER0_FADE_START - CELL_CIRCUMRADIUS - HONEY_MARGIN, handOver, t1Hand);
                CellsInRange(HONEY_REF, handOver, TIER1_SPLIT, t1Rest);
                t1In.AddRange(t1Hand);
                t1In.AddRange(t1Rest);
                CellsInRange(HONEY_REF, TIER1_SPLIT, TIER2_FADE_END + CELL_CIRCUMRADIUS + HONEY_MARGIN, t1Out);
            }
            _tier1Count = t1In.Count + t1Out.Count;

            // ---- meshes, local to the cell centre on the terrace plane (identical for every cell); one wall mesh and one bridge
            // mesh for all tiers
            Mesh top = BuildCellTop(mb);
            Mesh walls = BuildWalls(mb);
            Mesh t0Deep = BuildDeepT0(mb);
            Mesh t0Steel = BuildBridge(mb);
            Mesh cap = BuildCap(mb);

            // ---- materials: _DistFade (start, end, mode): -1 = near tier shown before, +1 = far tier shown beyond, 0 = off.
            // Walls: Key on through the bent normals, depth AO; rings and landing (below the web band): no Key, depth AO;
            // tops: Key on, web gain.
            // Every variant carries HONEY_AIR (the tube air and the aerial veil of R2-04, one continuous function for all tiers).
            var near0 = new Vector4(TIER0_FADE_START, TIER0_FADE_END, -1f, 0f);
            var far0 = new Vector4(TIER0_FADE_START, TIER0_FADE_END, 1f, 0f);
            Material hDeep = HoneyMat(mats, "deep", Vector4.zero, 1f, true), sDeep = BridgeMat(mats, "deep", Vector4.zero), cDeep = CapMat(mats, "deep", Vector4.zero);
            Material hT0 = HoneyMat(mats, "t0", near0, 1f, true), sT0 = BridgeMat(mats, "t0", near0), cT0 = CapMat(mats, "t0", near0);
            Material wDeep = HoneyMat(mats, "wall", Vector4.zero), wT0 = HoneyMat(mats, "t0wall", near0);
            Material tT0 = TopMat(mats, "t0top", near0);
            if (simplified)
            {
                // tiers 0 and 2 only: the tops hand over to the lattice plane, the deep parts are covered by it
                AddBatch("HoneyT0Top", top, tT0, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Walls", walls, wDeep, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Deep", t0Deep, hDeep, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Steel", t0Steel, sDeep, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Cap", cap, cDeep, _tier0Cells, T0_CHUNK, _tier0Batches);
            }
            else
            {
                AddBatch("HoneyT0Top", top, tT0, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Walls", walls, wT0, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Deep", t0Deep, hT0, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Steel", t0Steel, sT0, _tier0Cells, T0_CHUNK, _tier0Batches);
                AddBatch("HoneyT0Cap", cap, cT0, _tier0Cells, T0_CHUNK, _tier0Batches);

                Mesh t1Extras = BuildExtrasT1(mb);
                Mesh t1Ribs = BuildRibsT1(mb);
                Mesh t1Steel = t0Steel;
                Material hIn = HoneyMat(mats, "t1in", far0, 1f, true), sIn = BridgeMat(mats, "t1in", far0), cIn = CapMat(mats, "t1in", far0);
                AddBatch("HoneyT1InTop", top, TopMat(mats, "t1intop", far0), t1In, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1InCap", cap, cIn, t1In, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1InWalls", walls, HoneyMat(mats, "t1inwall", far0), t1In, T1_CHUNK, _tier1Batches);
                // The cells that meet the 1.6 km hand-over draw the tier-0 meshes in their far tier as well: the per-pixel
                // dithered cross-fade then swaps identical geometry and leaves no stipple (f01 KS7b showed the landing, ledge,
                // ribs and 48-gon columns of those cells as IGN dot patterns). Cells beyond can never be nearer than the end of
                // the hand-over, so they keep the plain tier-1 meshes.
                AddBatch("HoneyT1HandDeep", t0Deep, hIn, t1Hand, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1HandSteel", t0Steel, sIn, t1Hand, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1InSteel", t1Steel, sIn, t1Rest, T1_CHUNK, _tier1Batches);

                // belts and pads of every plain tier-1 cell, ribs out to 3.7 km: never faded (under the lattice plane)
                var ribCells = new List<Vector2Int>(96);
                CellsInRange(HONEY_REF, handOver, TIER1_RIBS_END, ribCells);
                AddBatch("HoneyT1InExtras", t1Extras, hDeep, t1Rest, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1Ribs", t1Ribs, hDeep, ribCells, T1_CHUNK, _tier1Batches);

                _t1TopFade = new Vector4(AbyssConst.HONEY_TIER2_START, TIER2_FADE_END, -1f, 0f);
                float t1Edge = TIER2_FADE_END + CELL_CIRCUMRADIUS;   // tier 1 reaches at least this far from every camera of the walk
                _latticeT1OnlyFade = new Vector4(t1Edge, t1Edge + 10f, 1f, 0f);
                _t1TopMat = TopMat(mats, "t1top", _t1TopFade);
                AddBatch("HoneyT1OutTop", top, _t1TopMat, t1Out, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1OutWalls", walls, wDeep, t1Out, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1OutExtras", t1Extras, hDeep, t1Out, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1OutSteel", t1Steel, sDeep, t1Out, T1_CHUNK, _tier1Batches);
                AddBatch("HoneyT1OutCap", cap, cDeep, t1Out, T1_CHUNK, _tier1Batches);
            }

            BuildLamps(mats, mb, simplified);
            BuildLattice(mats, mb, root, simplified ? far0 : new Vector4(AbyssConst.HONEY_TIER2_START, TIER2_FADE_END, 1f, 0f),
                simplified ? LATTICE_RINGS_SIMPLE : LATTICE_RINGS_FULL, simplified ? "b7simple" : "b7full");
        }

        void AddBatch(string name, Mesh mesh, Material mat, List<Vector2Int> cells, float chunk, List<InstancedBatch> into)
        {
            if (cells.Count == 0 || mesh == null) return;
            var b = new InstancedBatch("Beat07." + name, mesh, mat, ShadowCastingMode.Off, false, 0, chunk);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 c = CellCentre(cells[i].x, cells[i].y);
                b.Add(Matrix4x4.Translate(new Vector3(c.x, AbyssConst.TERRACE_Y, c.y)));
            }
            Register(b);
            into.Add(b);
        }

        void Register(InstancedBatch b)
        {
            b.Seal();
            _allBatches.Add(b);
            if (!_editMode) InstancedRegistry.Register(b);
        }

        // ---------------------------------------------------------------- materials (instancing enabled at build time)

        /// <summary>Honeycomb concrete albedo as an sRGB material colour (Unity converts colour properties to linear).</summary>
        static Color HoneyConcrete => HoneyConcreteTimes(1f);

        static Color HoneyConcreteTimes(float gain)
        {
            Color lin = HoneyAlbedoLinear * gain;
            lin.a = 1f;
            return lin.gamma;
        }

        /// <summary>
        /// Honeycomb concrete. deep = below the web band: the Key is masked (every such surface the walk can see lies in the
        /// sun's shadow) and the depth darkening comes from the vertex AO; the recipe's albedo falloff is off on every variant.
        /// </summary>
        static Material HoneyMat(MaterialLibrary mats, string key, Vector4 distFade, float gain = 1f, bool deep = false)
        {
            Material m = mats.Variant(MatId.HoneyTube, FogDomain.Exterior, "b7" + key, v =>
            {
                v.SetColor(ShaderIds.BaseColor, HoneyConcreteTimes(gain));
                v.enableInstancing = true;
            });
            m.SetFloat(DepthFalloffId, NO_ALBEDO_FALLOFF);
            m.SetFloat(KeyLightMaskId, deep ? 0f : 1f);
            m.SetVector(BeatKit.DistFadeId, distFade);
            m.SetVector(HoneyAirId, HONEY_AIR);   // R2-04: the air of the climax, the same on every tier
            // beyond 3.2 km: the far albedo, and the walls / rings darkened to the Lattice's 32 m hole tone (WALL_FAR_TINT)
            m.SetColor(FarTintId, HoneyConcreteTimes(gain * FAR_ALBEDO_GAIN * WALL_FAR_TINT));   // colour property: converted to linear by Unity
            m.SetVector(FarRangeId, WEB_FAR_RANGE);
            return m;
        }

        /// <summary>
        /// Web tops (R2-19): the web gain, the macro tone at WEB_MACRO_AMP, and the albedo blended to the plain web tone over
        /// WEB_FAR_RANGE (the Lattice's uniform webs take over at 5.4-6.6 km).
        /// </summary>
        static Material TopMat(MaterialLibrary mats, string key, Vector4 distFade)
        {
            Material m = HoneyMat(mats, key, distFade, WEB_TOP_GAIN);
            m.SetFloat(MacroAmpId, WEB_MACRO_AMP);
            m.SetColor(FarTintId, HoneyConcreteTimes(WEB_FAR_GAIN * FAR_ALBEDO_GAIN));   // colour property: converted to linear by Unity
            m.SetVector(FarRangeId, WEB_FAR_RANGE);
            return m;
        }

        /// <summary>
        /// Neighbour-bridge deck: pale matte concrete (the web-top tone, sunlit like the webs) drawn as a ThinLine thread, its
        /// albedo going to 0 over BRIDGE_FAR_RANGE (camera distance, per pixel, no dither).
        /// </summary>
        static Material BridgeMat(MaterialLibrary mats, string key, Vector4 distFade)
        {
            Material m = mats.Variant(MatId.HoneyTube, FogDomain.Exterior, "b7bridge" + key, v =>
            {
                v.SetColor(ShaderIds.BaseColor, HoneyConcreteTimes(WEB_TOP_GAIN));
                v.SetFloat(MacroAmpId, 0f);
                v.enableInstancing = true;
            });
            m.SetFloat(ShaderIds.ThinLine, BRIDGE_LINE_PX);
            m.SetFloat(DepthFalloffId, NO_ALBEDO_FALLOFF);
            m.SetFloat(KeyLightMaskId, 1f);
            m.SetColor(FarTintId, Color.black);
            m.SetVector(FarRangeId, BRIDGE_FAR_RANGE);
            m.SetVector(BeatKit.DistFadeId, distFade);
            m.SetVector(HoneyAirId, HONEY_AIR);
            return m;
        }

        static Material CapMat(MaterialLibrary mats, string key, Vector4 distFade)
        {
            Material m = mats.Variant(MatId.ProxyCard, FogDomain.Exterior, "b7cap" + key, v =>
            {
                v.SetColor(ShaderIds.BaseColor, CAP_LINEAR.gamma);   // colour properties are converted to linear by Unity
                v.enableInstancing = true;
            });
            m.SetVector(BeatKit.DistFadeId, distFade);
            return m;
        }

        // ---------------------------------------------------------------- cell meshes

        /// <summary>Above the lattice plane: the web-top annulus (apothem 350 to 380 + 5 cm overlap with the neighbours) and the top band of the walls.</summary>
        Mesh BuildCellTop(MeshBuilder mb)
        {
            float a = AbyssConst.SHAFT_APOTHEM, half = 0.5f * AbyssConst.LATTICE_PITCH;
            MeshKit.HexAnnulus(mb, Vector2.zero, 0f, a, half + ANNULUS_OVERLAP, true);
            for (int k = 0; k < 6; k++) MeshKit.HexWallPanel(mb, Vector2.zero, a, k, -HONEY_TOP_BAND, 0f, 1, 1, true);
            return BeatKit.Bake(mb, "HoneyCellTop", _meshes);
        }

        /// <summary>
        /// The six walls below the web band, every tier (never faded beyond 1.6 km; the lattice plane covers them): depth AO in
        /// the vertex colours and bent normals on the sun-facing walls (BendWallNormals), drawn with the Key on.
        /// </summary>
        Mesh BuildWalls(MeshBuilder mb)
        {
            AddWalls(mb, AbyssConst.SHAFT_APOTHEM, -HONEY_TOP_BAND, -HONEY_TUBE_DEPTH);
            ApplyDepthAO(mb, 0);
            BendWallNormals(mb, 0, ProgressTables.ExtSunDir);
            Mesh m = BeatKit.Bake(mb, "HoneyWalls", _meshes);
            _tubeDepth = -m.bounds.min.y;
            return m;
        }

        /// <summary>Tier 0 below the lattice plane besides the walls: the 10 rings of the S0 levels and the landing; depth AO.</summary>
        Mesh BuildDeepT0(MeshBuilder mb)
        {
            float a = AbyssConst.SHAFT_APOTHEM, topY = -HONEY_TOP_BAND, bottom = -HONEY_TUBE_DEPTH;
            _ringCount = 0;
            for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
            {
                float yl = AbyssConst.RingY(k) - AbyssConst.TERRACE_Y;
                bool belt = AbyssConst.RingIsBelt(k);
                float h = belt ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT;
                if (yl + 0.5f * h > topY || yl - 0.5f * h < bottom) continue;
                float inner = belt ? AbyssConst.BELT_INNER_APOTHEM : AbyssConst.RIB_INNER_APOTHEM;
                MeshKit.MitredRing(mb, Vector2.zero, yl, inner, a - inner, h, belt ? AbyssConst.BELT_BEVEL : AbyssConst.RIB_BEVEL, false, RING_EMBED);
                _ringCount++;
            }
            ApplyRingAO(mb, 0);

            // landing at the near flat (S0: 8 x 6), 10 cm into the wall, its top at the deck
            int landing = mb.VertexCount;
            float deck = HONEY_DECK_TOP_Y - AbyssConst.TERRACE_Y;
            float nearFlat = -a;
            MeshKit.ChamferBox(mb, new Vector3(0f, deck - 0.5f * LANDING_T, nearFlat - 0.1f + 0.5f * (AbyssConst.LANDING_L + 0.1f)),
                new Vector3(AbyssConst.LANDING_W, LANDING_T, AbyssConst.LANDING_L + 0.1f), 0.03f);
            ApplyDepthAO(mb, landing);
            AddPad(mb);
            return BeatKit.Bake(mb, "HoneyT0Deep", _meshes);
        }

        /// <summary>Tier-1 extras in front of the walls: the two belts (no bevel) and the C1 landing pad.</summary>
        Mesh BuildExtrasT1(MeshBuilder mb)
        {
            AddRings(mb, AbyssConst.SHAFT_APOTHEM, -HONEY_TOP_BAND, -HONEY_TUBE_DEPTH, true);
            ApplyRingAO(mb, 0);
            AddPad(mb);
            return BeatKit.Bake(mb, "HoneyT1Extras", _meshes);
        }

        /// <summary>Tier-1 ribs: the 8 non-belt rings of the S0 levels (no bevel), as the tier-0 cells beside them show.</summary>
        Mesh BuildRibsT1(MeshBuilder mb)
        {
            AddRings(mb, AbyssConst.SHAFT_APOTHEM, -HONEY_TOP_BAND, -HONEY_TUBE_DEPTH, false);
            ApplyRingAO(mb, 0);
            return BeatKit.Bake(mb, "HoneyT1Ribs", _meshes);
        }

        /// <summary>
        /// R2-30: the C1 landing pad (cell-local, PadCentre): a flat disc of radius LEDGE_R_OUT with a chamfered rim and a
        /// flat underside, its top PAD_DROP under the deck top (the deck's end rests on it, no shared face), AO as the rings.
        /// </summary>
        static void AddPad(MeshBuilder mb)
        {
            int first = mb.VertexCount;
            Vector2 pc = PadCentre;
            float top = HONEY_DECK_TOP_Y - AbyssConst.TERRACE_Y - PAD_DROP, r = AbyssConst.LEDGE_R_OUT, b = PAD_BEVEL;
            var c = new Vector3(pc.x, top, pc.y);
            var cb = new Vector3(pc.x, top - PAD_THICK, pc.y);
            int ct = mb.Vertex(c, Vector3.up, new Vector2(c.x, c.z));
            int cBot = mb.Vertex(cb, Vector3.down, new Vector2(cb.x, cb.z));
            float step = 2f * Mathf.PI / PAD_SEGMENTS;
            for (int k = 0; k < PAD_SEGMENTS; k++)
            {
                float a0 = k * step, a1 = (k + 1) * step;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                // top fan (flat, up) and bottom fan (flat, down)
                Vector3 t0 = c + d0 * (r - b), t1 = c + d1 * (r - b);
                int i0 = mb.Vertex(t0, Vector3.up, new Vector2(t0.x, t0.z)), i1 = mb.Vertex(t1, Vector3.up, new Vector2(t1.x, t1.z));
                BeatKit.Tri(mb, ct, i0, i1);
                Vector3 b0 = cb + d0 * r, b1 = cb + d1 * r;
                int j0 = mb.Vertex(b0, Vector3.down, new Vector2(b0.x, b0.z)), j1 = mb.Vertex(b1, Vector3.down, new Vector2(b1.x, b1.z));
                BeatKit.Tri(mb, cBot, j1, j0);
                // 45 degree chamfer and the vertical rim, flat-shaded per segment
                Vector3 dm = (d0 + d1).normalized;
                BeatKit.Quad(mb, t0, t1, c + d1 * r + Vector3.down * b, c + d0 * r + Vector3.down * b, (dm + Vector3.up).normalized);
                BeatKit.Quad(mb, c + d0 * r + Vector3.down * b, c + d1 * r + Vector3.down * b, b1, b0, dm);
            }
            ApplyRingAO(mb, first);
        }

        /// <summary>The rings' and pad's sky-light AO from index first on: R = exp(-depth / HONEY_RING_AO_FALLOFF) (R2-04 (3)).</summary>
        public static void ApplyRingAO(MeshBuilder mb, int first)
        {
            for (int i = first; i < mb.VertexCount; i++)
            {
                Color32 c = mb.Colors[i];
                c.r = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * RingAO(-mb.Positions[i].y)), 0, 255);
                mb.Colors[i] = c;
            }
        }

        /// <summary>The rings' sky-light AO at a depth (m) below the web tops.</summary>
        public static float RingAO(float depth) => Mathf.Exp(-Mathf.Max(depth, 0f) / HONEY_RING_AO_FALLOFF);

        /// <summary>The six inward walls of a tube from topY down to bottom in the rows of WALL_ROWS (identical on every tier).</summary>
        static void AddWalls(MeshBuilder mb, float a, float topY, float bottom)
        {
            for (int k = 0; k < 6; k++)
            {
                float y1 = topY;
                for (int i = 0; i <= WALL_ROWS.Length; i++)
                {
                    float y0 = i < WALL_ROWS.Length ? Mathf.Max(-WALL_ROWS[i], bottom) : bottom;
                    if (y0 < y1 - 1e-3f) MeshKit.HexWallPanel(mb, Vector2.zero, a, k, y0, y1, 1, 1, true);
                    y1 = y0;
                }
            }
        }

        /// <summary>
        /// Sky-light AO of the cell-local vertices from index first on: R = exp(-depth / HONEY_AO_FALLOFF), depth below the
        /// web tops (local y 0). G (edge wear) and the rest are kept.
        /// </summary>
        public static void ApplyDepthAO(MeshBuilder mb, int first)
        {
            for (int i = first; i < mb.VertexCount; i++)
            {
                Color32 c = mb.Colors[i];
                c.r = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * DepthAO(-mb.Positions[i].y)), 0, 255);
                mb.Colors[i] = c;
            }
        }

        /// <summary>The sky-light AO at a depth (m) below the web tops.</summary>
        public static float DepthAO(float depth) => Mathf.Exp(-Mathf.Max(depth, 0f) / HONEY_AO_FALLOFF);

        /// <summary>
        /// Bent normals for the sun-facing tube walls (vertices from first on with horizontal normals): each normal is tilted
        /// down about the wall's horizontal tangent so that N.L = (N.L of the wall) x DepthAO(depth), with L the unit vector
        /// toward the beat-7 sun. The sun then fades into the tube exactly as the Lattice's analytic holes fade it (their wall
        /// radiance x exp(-depth / L), tier 2) while the walls keep the Key; tier 1 and tier 2 agree at the 5.4-6.6 km dither
        /// (a Key-less tier 1 left the sunlit oblique walls of the lattice as stipple wedges, reveal r06). Walls facing away
        /// from the sun (every wall the balcony looks into head-on) keep their normal: the Key does not reach them anyway.
        /// </summary>
        public static void BendWallNormals(MeshBuilder mb, int first, Vector3 towardSun)
        {
            float lx = towardSun.x, lz = towardSun.z, b = towardSun.y;
            for (int i = first; i < mb.VertexCount; i++)
            {
                Vector3 n = mb.Normals[i];
                if (Mathf.Abs(n.y) > 1e-3f) continue;
                float a = n.x * lx + n.z * lz;
                if (a <= 1e-4f) continue;
                float t = a * DepthAO(-mb.Positions[i].y);
                float r = Mathf.Sqrt(a * a + b * b), phi = Mathf.Atan2(b, a);
                float th = Mathf.Acos(Mathf.Clamp(t / r, -1f, 1f)) - phi;
                if (th <= 0f) continue;
                mb.Normals[i] = (n * Mathf.Cos(th) - Vector3.up * Mathf.Sin(th)).normalized;
            }
        }

        /// <summary>The belts (belts = true) or the plain ribs of the S0 ring levels inside a cell, without bevels.</summary>
        static void AddRings(MeshBuilder mb, float a, float topY, float bottom, bool belts)
        {
            for (int k = 0; k < AbyssConst.RIB_LEVELS; k++)
            {
                bool belt = AbyssConst.RingIsBelt(k);
                if (belt != belts) continue;
                float yl = AbyssConst.RingY(k) - AbyssConst.TERRACE_Y, h = belt ? AbyssConst.BELT_HEIGHT : AbyssConst.RIB_HEIGHT;
                if (yl + 0.5f * h > topY || yl - 0.5f * h < bottom) continue;
                float inner = belt ? AbyssConst.BELT_INNER_APOTHEM : AbyssConst.RIB_INNER_APOTHEM;
                MeshKit.MitredRing(mb, Vector2.zero, yl, inner, a - inner, h, 0f, false, RING_EMBED);
            }
        }

        /// <summary>
        /// The neighbour bridge on every tier: the deck alone (1.2 x 0.3, from the near flat + 6 to + 156, 10 cm into the
        /// landing and past the far end) as a plain ThinLine box (12 triangles). At 0.8-6 km a chord, posts or a lamp post are
        /// 0.05-1 px and could only alias (R1-12); the lamp is the Glow sprite 1.1 m above the deck.
        /// </summary>
        Mesh BuildBridge(MeshBuilder mb)
        {
            float a = AbyssConst.SHAFT_APOTHEM, dt = HONEY_DECK_TOP_Y - AbyssConst.TERRACE_Y - DECK_DROP;
            float zNear = -a + HONEY_BRIDGE_NEAR - 0.1f, zFar = -a + HONEY_BRIDGE_FAR + 0.1f;
            float zLamp = -HONEY_LAMP_OFFSET;
            // two boxes with a DECK_LAMP_GAP gap either side of the lamp (R2-29), each with its own ThinLine axis point
            for (int s = 0; s < 2; s++)
            {
                float z0 = s == 0 ? zNear : zLamp + DECK_LAMP_GAP, z1 = s == 0 ? zLamp - DECK_LAMP_GAP : zFar;
                var c = new Vector3(0f, dt - 0.5f * DECK_T, 0.5f * (z0 + z1));
                mb.WriteUV1 = true; mb.CurrentUV1 = new Vector4(0f, 0f, 1f, 0.5f * DECK_W);
                mb.WriteUV2 = true; mb.CurrentUV2 = new Vector4(c.x, c.y, c.z, 1f);
                mb.Box(c, new Vector3(DECK_W, DECK_T, z1 - z0));
            }
            mb.WriteUV1 = false; mb.WriteUV2 = false;
            Bounds deckB = BeatKit.VertexBounds(mb, 0);
            _deckTopY = deckB.max.y + AbyssConst.TERRACE_Y;
            _bridgeLength = HONEY_BRIDGE_FAR - HONEY_BRIDGE_NEAR;
            return BeatKit.Bake(mb, "HoneyT0Steel", _meshes);
        }

        /// <summary>World position of the amber lamp (Glow sprite) of cell (q, r): its bridge midpoint, 1.1 m above the deck.</summary>
        public static Vector3 LampWorld(int q, int r)
        {
            Vector2 l = LampXZ(q, r);
            return new Vector3(l.x, HONEY_DECK_TOP_Y + HONEY_LAMP_HEIGHT, l.y);
        }

        /// <summary>The bottom of the tube: a hexagon 600 m down facing up (instanced ProxyCard).</summary>
        Mesh BuildCap(MeshBuilder mb)
        {
            float y = -HONEY_TUBE_DEPTH;
            int c = mb.Vertex(new Vector3(0f, y, 0f), Vector3.up, Vector2.zero);
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = MeshKit.HexCorner(Vector2.zero, AbyssConst.SHAFT_APOTHEM, i);
                mb.Vertex(new Vector3(p.x, y, p.y), Vector3.up, p);
            }
            for (int i = 0; i < 6; i++) BeatKit.Tri(mb, c, c + 1 + i, c + 1 + (i + 1) % 6);
            return BeatKit.Bake(mb, "HoneyCap", _meshes);
        }

        // ---------------------------------------------------------------- lamps (Glow sprites, one per cell)

        void BuildLamps(MaterialLibrary mats, MeshBuilder mb, bool simplified)
        {
            Vector3 n = Vector3.back;
            BeatKit.Quad(mb, new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), n);
            Mesh quad = BeatKit.Bake(mb, "HoneyLampQuad", _meshes);
            quad.bounds = new Bounds(Vector3.zero, new Vector3(16f, 16f, 16f));   // the sprite grows to >= 2.5 px in the vertex stage
            // R2-20: the sprites no longer ignite per instance in the shader (its delay is linear in distance); every ignition
            // ring and cross-fade band is a batch whose |_AbFade| brightness follows the warped curve (UpdateGlowFades)
            Material glow = mats.Variant(MatId.Glow, FogDomain.Exterior, "b7honey", v =>
            {
                v.SetColor(ShaderIds.EmissionColor, LampColor);
                v.SetFloat(ShaderIds.EmissionStrength, GLOW_STRENGTH);
                v.SetFloat(ShaderIds.MinPixels, GLOW_MIN_PX);
                v.enableInstancing = true;
            });
            glow.SetFloat(GlowIgniteId, 0f);
            Material halo = mats.Variant(MatId.Glow, FogDomain.Exterior, "b7halo", v =>
            {
                v.SetColor(ShaderIds.EmissionColor, LampColor);
                v.SetFloat(ShaderIds.EmissionStrength, HALO_STRENGTH);
                v.SetFloat(ShaderIds.MinPixels, HALO_MIN_PX);
                v.enableInstancing = true;
            });
            halo.SetFloat(GlowIgniteId, 0f);

            _glowFadeStart = simplified ? TIER0_FADE_START : AbyssConst.HONEY_TIER2_START;
            _glowFadeEnd = simplified ? TIER0_FADE_END : TIER2_FADE_END;
            float bandStart = _glowFadeStart - HONEY_MARGIN, bandEnd = _glowFadeEnd + GLOW_BAND_TAIL;
            float bandW = (bandEnd - bandStart) / GLOW_BANDS;
            var cells = new List<Vector2Int>(512);
            CellsInRange(HONEY_REF, 0f, bandEnd + HONEY_LAMP_OFFSET + 10f, cells);
            int rings = Mathf.Max(1, Mathf.CeilToInt(IgniteDelay(bandStart + 2f * HONEY_MARGIN) / IGN_RING_STEP));
            _ringBatches = new InstancedBatch[rings];
            _haloBatches = new InstancedBatch[rings];
            _ringDelay = new float[rings];
            var ringSum = new float[rings];
            var ringN = new int[rings];
            for (int k = 0; k < rings; k++)
            {
                _ringBatches[k] = new InstancedBatch("Beat07.HoneyLamps.r" + k, quad, glow, ShadowCastingMode.Off, false, 0, GLOW_CHUNK);
                _haloBatches[k] = new InstancedBatch("Beat07.HoneyHalos.r" + k, quad, halo, ShadowCastingMode.Off, false, 0, GLOW_CHUNK);
            }
            _bandBatches = new InstancedBatch[GLOW_BANDS];
            _bandMid = new float[GLOW_BANDS];
            _bandIgnDelay = new float[GLOW_BANDS];
            var bandSum = new float[GLOW_BANDS];
            var bandN = new int[GLOW_BANDS];
            for (int b = 0; b < GLOW_BANDS; b++)
            {
                _bandBatches[b] = new InstancedBatch("Beat07.HoneyLampsBand" + b, quad, glow, ShadowCastingMode.Off, false, 0, GLOW_CHUNK);
                _bandMid[b] = bandStart + (b + 0.5f) * bandW;
            }
            Vector3 scale = Vector3.one * GLOW_DIAMETER, haloScale = Vector3.one * HALO_DIAMETER;
            _lampCount = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 lw = LampWorld(cells[i].x, cells[i].y);
                var lxz = new Vector2(lw.x, lw.z);
                float r = (lxz - HONEY_REF).magnitude;
                if (r >= bandEnd) continue;
                float delay = IgniteDelay((lxz - IGNITE_REF).magnitude);
                Matrix4x4 m = Matrix4x4.TRS(lw, Quaternion.identity, scale);
                if (r < bandStart)
                {
                    int k = Mathf.Clamp((int)(delay / IGN_RING_STEP), 0, rings - 1);
                    _ringBatches[k].Add(m);
                    ringSum[k] += delay; ringN[k]++;
                    if (r < HALO_RANGE) _haloBatches[k].Add(Matrix4x4.TRS(lw, Quaternion.identity, haloScale));
                }
                else
                {
                    int b = Mathf.Min(GLOW_BANDS - 1, (int)((r - bandStart) / bandW));
                    _bandBatches[b].Add(m);
                    bandSum[b] += delay; bandN[b]++;
                }
                _lampCount++;
            }
            for (int k = 0; k < rings; k++)
            {
                _ringDelay[k] = ringN[k] > 0 ? ringSum[k] / ringN[k] : k * IGN_RING_STEP;
                RegisterGlow(_ringBatches[k]);
                RegisterGlow(_haloBatches[k]);
            }
            for (int b = 0; b < GLOW_BANDS; b++)
            {
                _bandIgnDelay[b] = bandN[b] > 0 ? bandSum[b] / bandN[b] : IgniteDelay(_bandMid[b]);
                RegisterGlow(_bandBatches[b]);
            }
        }

        /// <summary>Seal and register a lamp batch, allocate its property block now (no allocation during the walk), start dark.</summary>
        void RegisterGlow(InstancedBatch b)
        {
            if (b.Count == 0) { b.Seal(); b.Visible = false; return; }   // empty ring (no halo beyond HALO_RANGE): never drawn
            Register(b);
            _glowBatches.Add(b);
            b.Fade = 0.5f;
            b.Fade = 0f;
            b.Visible = false;
        }

        /// <summary>Ignition level (0..1) of a lamp batch whose curve delay is delay, elapsed seconds after the ignition.</summary>
        static float IgniteLevel(float elapsed, float delay) => Smooth01(0f, LAMP_RAMP, elapsed - delay);

        void UpdateGlowFades(Vector3 cam, double sceneTime)
        {
            if (_bandBatches == null || !_glowShown) return;
            float elapsed = _igniteStart >= 0.0 ? (float)(sceneTime - _igniteStart) : -1e6f;
            for (int k = 0; k < _ringBatches.Length; k++)
            {
                float f = IgniteLevel(elapsed, _ringDelay[k]);
                SetGlow(_ringBatches[k], f);
                SetGlow(_haloBatches[k], f);
            }
            // the visible lamps lie toward -Z (the view from the balcony): their distance grows with the camera's z offset
            float dz = Mathf.Clamp(cam.z - HONEY_REF.y, -2f * HONEY_MARGIN, 2f * HONEY_MARGIN);
            for (int i = 0; i < _bandBatches.Length; i++)
            {
                float f = _honeyMode == 1 ? 1f : 1f - Smooth01(_glowFadeStart, _glowFadeEnd, _bandMid[i] + dz);
                SetGlow(_bandBatches[i], f * IgniteLevel(elapsed, _bandIgnDelay[i]));
            }
        }

        static void SetGlow(InstancedBatch b, float f)
        {
            bool vis = f > 0.002f && b.Count > 0;
            if (b.Visible != vis) b.Visible = vis;
            if (vis) b.Fade = f;
        }

        /// <summary>QA / tests: the curve delays of the sprite ignition rings (s), nearest first.</summary>
        public float[] IgnitionRingDelays => _ringDelay;

        // ---------------------------------------------------------------- tier 2: the Lattice on a camera-following annulus

        void BuildLattice(MaterialLibrary mats, MeshBuilder mb, Transform root, Vector4 fade, float[] rings, string key)
        {
            _latticeFade = fade;
            _latticeMat = mats.Variant(MatId.Lattice, key, v =>
            {
                v.SetColor(ShaderIds.BaseColor, HoneyConcreteTimes(FAR_ALBEDO_GAIN));   // = the tier-1 far tint (R2-04 (4))
                // lit rim (x 1.35 in the shader) across the whole half web = WEB_FAR_GAIN on the top meshes' far tint
                v.SetVector(LatticeParamsId, new Vector4((float)HexSize, AbyssConst.SHAFT_APOTHEM, LATTICE_RIM_W, HONEY_LAMP_OFFSET));
                v.SetColor(ShaderIds.EmissionColor, LampColor);
                v.SetFloat(ShaderIds.EmissionStrength, Mathf.Min(GLOW_STRENGTH * LATTICE_LAMP_GAIN, AMBER_DOT_CAP));
                v.SetFloat(ShaderIds.MinPixels, GLOW_MIN_PX);
            });
            _latticeMat.SetVector(BeatKit.DistFadeId, fade);
            // the holes darken with the tubes' sky-light falloff (the tier-1 walls carry it as vertex AO): 14.10 (c)
            _latticeMat.SetFloat(DepthFalloffId, HONEY_FAR_FALLOFF);
            _latticeMat.SetVector(HoneyAirId, HONEY_AIR);   // the same air as the tube tiers (14.10 c)
            for (int i = 0; i + 1 < rings.Length; i++) MeshKit.Annulus(mb, Vector3.zero, rings[i], rings[i + 1], LATTICE_SEGMENTS, true);
            _latticeRadius = BeatKit.VertexBounds(mb, 0).extents.x;
            float y = AbyssConst.TERRACE_Y - LATTICE_DROP;
            MeshRenderer r = BeatKit.Emit(mb, root, "HoneycombLattice", _latticeMat, _meshes, ShadowCastingMode.Off, new Vector3(0f, y, 0f), true);
            if (r == null) return;
            r.receiveShadows = false;
            _latticeGo = r.gameObject;
            var rig = _latticeGo.AddComponent<BackdropRig>();
            rig.Configure(LATTICE_GRID, true, false, true, new Vector3(0f, y, 0f), Quaternion.identity);
        }

        // ---------------------------------------------------------------- visibility, QA tier override

        /// <summary>
        /// Honeycomb visibility from the activation state (d 640, hysteresis 20) and QaHooks.TierOverride (-1 normal, 1 tier 1
        /// only, 0 tier 2 only). Runs only when either changes (material writes are on this beat's own variants).
        /// </summary>
        void ApplyHoneycomb(bool force)
        {
            float o = QaHooks.TierOverride;
            int mode = o < 0f ? 0 : (o >= 0.5f ? 1 : 2);
            if (!force && mode == _honeyMode && _latticeOn == _honeyOnApplied) return;
            _honeyMode = mode;
            _honeyOnApplied = _latticeOn;
            bool on = _latticeOn;
            SetVisible(_tier0Batches, on);
            SetVisible(_tier1Batches, on && mode != 2);
            _glowShown = on && mode != 2;
            SetVisible(_glowBatches, false);   // UpdateGlowFades shows the lit ones
            bool t1Only = mode == 1 && _t1TopMat != null;
            bool latticeOn = on && (mode != 1 || t1Only);
            if (_latticeGo != null && _latticeGo.activeSelf != latticeOn) _latticeGo.SetActive(latticeOn);
            if (_t1TopMat != null) _t1TopMat.SetVector(BeatKit.DistFadeId, mode == 1 ? Vector4.zero : _t1TopFade);
            if (_latticeMat != null) _latticeMat.SetVector(BeatKit.DistFadeId, mode == 2 ? Vector4.zero : (t1Only ? _latticeT1OnlyFade : _latticeFade));
            if (_glowShown) UpdateGlowFades(GameState.CameraPos, SceneClock.Time);
        }

        /// <summary>QA / tests: the honeycomb instanced batch with this name ("Beat07.HoneyT1HandSteel", ...), or null.</summary>
        public InstancedBatch FindHoneyBatch(string name)
        {
            for (int i = 0; i < _allBatches.Count; i++) if (_allBatches[i].Name == name) return _allBatches[i];
            return null;
        }

        /// <summary>
        /// QA / tests: every honeycomb batch named prefix or prefix.b{band} (the aerial bands) / prefix.r{ring} (the ignition
        /// rings), appended to into; returns the number found.
        /// </summary>
        public int FindHoneyBatches(string prefix, List<InstancedBatch> into)
        {
            int n = 0;
            for (int i = 0; i < _allBatches.Count; i++)
            {
                string s = _allBatches[i].Name;
                if (s == prefix || (s.Length > prefix.Length + 1 && s.StartsWith(prefix, System.StringComparison.Ordinal) && s[prefix.Length] == '.'))
                {
                    into.Add(_allBatches[i]);
                    n++;
                }
            }
            return n;
        }

        /// <summary>QA / tests: the Lattice (tier 2) material variant.</summary>
        public Material LatticeMaterial => _latticeMat;

        static void SetVisible(List<InstancedBatch> list, bool v)
        {
            for (int i = 0; i < list.Count; i++) list[i].Visible = v;
        }
    }
}
