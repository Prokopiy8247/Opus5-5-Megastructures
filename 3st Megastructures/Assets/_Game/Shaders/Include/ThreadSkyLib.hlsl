// ThreadSkyLib.hlsl - the analytic sky of Thread (MASTER_PROMPT 14.2.1, 14.5.1, 14.5.6, 14.5.7, 14.7.2 - 14.7.7).
// Owner: builder B02, revised by fixer F1 (critique run06: A3 A4 A10 B2 B4 B5 C-01 C-02 C-15 C-16 E3 E10 E11 PAC-10 PAC-13)
// and by the sky fixer of wave 3 (critique run07: B-r2-1 B-r2-2 B-r2-3 B-r2-7 C-r2-1 C-r2-2 C-r2-4 E10 E16 E-r2-1 E-r2-2
// E-r2-6 A-r2-2 PAC-r2-4 PAC-r2-5 PAC-12).
// Included by ThreadSky.shader after URP Core.hlsl (needs _WorldSpaceCameraPos, SafeNormalize, UNITY_MATRIX_V).
//
// Draw order for a view ray d (unit, world orientation, camera-relative):
//   gradient (ThreadSkyColor with the dusk hold) -> sun disc -> stars (+ glint star) -> far thread (beats 1-3) ->
//   planet -> Earth / cloud shell (one code path, no crossfade) -> limb glow -> bank and deck terms (ThreadFogApplySky).
// Rules followed everywhere below:
//   * every quantity whose screen derivative is needed is computed in uniform control flow, never inside a
//     per-pixel branch (derivatives in divergent flow are undefined); branches only skip derivative-free work;
//   * planetary maths in stable forms only (no 6e6-sized discriminants, CPU double cosines, atan2 central angle);
//   * every division, sqrt, asin, pow and atan2 is guarded; no sin-based hash (ThPcg integer hashing on cells);
//   * the result is un-quantised linear HDR (14.7.1 item 5); the one dither lives in ThreadGrade.
// F1 decisions (the lead logs them in DEVELOPMENT.md):
//   * Sun and planet are SCREEN-SPACE CIRCULAR IMPOSTORS (A4, B4, C-16): the centre direction is projected with the
//     camera's view matrix onto the image plane (tangent plane at unit distance, where pixel = f * coordinate with
//     f = (H/2) |P11|, P11 = 1 / tan(FOV_v / 2)); the disc is the circle of image-plane radius tan(angular radius)
//     around it, i.e. f tan(r) pixels, the on-axis apparent size, at any position in the frame. The pixel size of
//     the image plane is measured from the screen derivatives of the pixel's own image-plane position (exact for a
//     rectilinear camera, immune to aspect ratio, render scale and the y flip of RenderTexture projections), so the
//     edge is one pixel wide everywhere. The planet is shaded as a sphere seen head-on: normal, band latitude, key
//     light L_p and the ATMO rim all come from the normalised screen offset in a disc frame whose y axis is the
//     projected world vertical through the centre (the band tilt stays relative to the vertical).
//   * Planet reveal (A10, E11, PAC-13): stars and the sun are hidden by the disc's geometric coverage (complete from
//     A = 150 km, ramping in over A 90 -> 150 km while the disc is still the sky colour), and the disc colour is lerped
//     from the local sky colour to the planet colour with _ThPlanet.x. Nothing is ever alpha-blended over stars.
//   * Cloud sea (C-01): rounded puffs. A 6 km cluster octave (smooth sum of domes, broad soft shade between clusters)
//     carries two octaves of puffs (union of spherical domes of random radius and base height, 1.5 km and 400 m).
//     The tone variable is the dome lighting with the sun relative to a flat surface (leeward flanks and the gaps
//     between puffs and clusters fall into the shade), mapped to exactly 3 flat tones with wide smoothsteps (edge
//     filtering: fix wave 3 below). Octaves fade by pixel footprint (12 -> 24 px, E10) and by altitude (B2, C-02, E10: all
//     three are gone at 60 km, so from orbit the clouds are only the weather systems: flat CLOUD_TOP, soft edges).
//   * Earth from orbit (B2, C-02): three flat tones (EARTH_OCEAN, EARTH_LAND, CLOUD_TOP); continents are two warped
//     low-frequency octaves with fwidth-crisp coasts (ocean about 70 percent; edges: wave 3); detail that the footprint cannot
//     resolve toward the limb fades to clear ocean, and the haze from orbit grades toward ATMO_RIM, so the limb
//     reads ATMO_RIM -> EARTH_OCEAN with no CLOUD_SHADE ring.
//   * Cable shadow band (A3, B5, C-01, E3, PAC-10): own deeper tone CLOUD_SHADE x TC_ShadowBandColourGain blended
//     toward SKY_M_DUSK with D, opacity 0.8 (applied as a multiplier since wave 3), the half width widened by
//     0.012 h and the fade stretched to [max(12 km, 4 h), max(30 km, 16 h)] with the height h above the cloud tops,
//     so the band reaches toward the horizon at every altitude.
//   * Dusk sky (C-15): below the 25 deg stop the gradient keeps the dusk stops near the horizon (the horizon darkness
//     is held at 0.5 until D = 0.85) and restores the OKLab chroma of the H -> M segment (peach -> rose -> indigo
//     instead of a grey midpoint). Active only for D > 0.5 (from the deck pass on, after the ground and its fog are
//     gone); the sea haze uses the same held horizon colour, so sea and sky still meet at one colour (14.7.2).
// Fix wave 3 (critique run07):
//   * Puff anti-aliasing (B-r2-2, C-r2-1, E10): the puff union has no discontinuity left. Each dome enters over its
//     own pixel-scale disc coverage (C1 ramp of SKY_PUFF_AA_PX px), a taller dome takes over across a crease band of
//     SKY_PUFF_CREASE_PX px, the domes are blended in normal space (Nd = g / sqrt(1 + g.g), so the lighting changes
//     linearly across every ramp), and the steep rim never gets thinner than ~2 px (rim floor grows with the
//     footprint). The tone v therefore is continuous, and its screen gradient is computed ANALYTICALLY (dome
//     Hessians, blend ramps, warp Jacobians, chained with ddx/ddy of the surface point): every tone edge is
//     smoothed over SKY_TONE_PX px by the pixel's own gradient, independent of the 2 x 2 quad (fwidth misses a jump
//     that falls between two quads, which is what drew the stair-steps and dashed seams; it is kept only as a lower
//     bound). The octave footprint is analytic too (distance x pixel angle / cos incidence): per-quad footprint steps
//     moved the fades and drew saw-tooth edges near the limb.
//   * Shadow band (B-r2-1, C-r2-2, E-r2-1, PAC-r2-4): a pure multiplier on the sea, tint = band tone / CLOUD_TOP, so
//     over lit cloud it is exactly the D-045 tone, over the shaded flanks it is darker still, and it never lifts the
//     sea (the round-2 shade corridor and its pale verges are gone).
//   * Earth from orbit (A-r2-2, C-02, E-r2-2, PAC-03, C-r2-4, PAC-r2-5): weather and coast edges are crisp
//     (SKY_EARTH_AA_PX x fwidth, ~1.5 px; the fixed soft widths drew the coffee stains), and the weather and
//     continent phases put open ocean and coast under the beat-7 nadir and ocean or cloud under the right-hand limb
//     of KF7b (no tan plain next to the pad).
//   * Sea limb (E16, B-r2-7): the sea is the exact horizon haze for the first SKY_LIMB_FULL (0.2 deg) below the R-limb
//     and resolves over the next SKY_LIMB_RAMP (1.2 deg; C1, combined with the path haze as 1 - (1 - a)(1 - b)), so sea
//     and sky meet without a line; from orbit the same band reads as the blue atmosphere over the limb.
//   * Stars (B-r2-3, E-r2-6): faded by the luminance of the sky behind them (gradient plus limb glow), so they vanish
//     in the bright twilight band and the ATMO glow and keep full strength in the dark sky.
// C# double mirror: Assets/_Game/Scripts/Runtime/Sky/SkyMath.cs (EditMode tests: Tests/EditMode/SkyTests.cs).
// Every float3/float4 constructor that is not a palette macro carries the marker  pal:ok  (T-PAL, 14.7.8).
#ifndef THREAD_SKY_LIB_INCLUDED
#define THREAD_SKY_LIB_INCLUDED

#include "ThreadCommon.hlsl"

// ------------------------------------------------------------------------------------------------
// Constants. Spec numbers that ThreadConstants does not export yet are named here with their spec section
// and mirrored in C# by SkyConstants (SkyMath.cs); B02 design choices are marked [B02], F1 choices cite the
// critique ID they answer.
// ------------------------------------------------------------------------------------------------
#define SKY_DEG2RAD 0.017453292519943295
static const float SKY_FAR_D = 1.0e7;               // "large" sky-ray length (ThreadTauDeck clamps at 1e7)

// stars (14.7.7, 14.5.7)
static const float SKY_STAR_CELLS = 82.0;           // 14.7.7 "cell about 1.1 deg": 90 / 82 = 1.098 deg (equi-angular cube face)
static const float SKY_STAR_OCCUPANCY = 0.08;       // 14.7.7 8 percent occupancy
static const float SKY_STAR_P_TIER1 = 0.80;         // 14.7.7 tiers 80 / 17 / 3 percent
static const float SKY_STAR_P_TIER12 = 0.97;
static const float SKY_STAR_PEAK1 = 0.9;            // 14.7.7 peak linear 0.9 / 1.6 / 2.5
static const float SKY_STAR_PEAK2 = 1.6;
static const float SKY_STAR_PEAK3 = 2.5;
static const float SKY_GLINT_PEAK = 3.5;            // 14.5.7 glint 2.5 -> 3.5, no change of size
static const float SKY_STAR_SIGMA_MIN_PX = 0.6;     // 14.7.7 sigma = max(0.6 px, projected size)
static const float SKY_STAR_SIGMA_RAD = 1.4e-4;     // [B02] intrinsic star size 0.008 deg (< 0.6 px even at 4K)
static const float SKY_STAR_MARGIN = 0.2;           // [B02] jitter kept 0.2 cell inside the cell (>= 3 sigma down to ~720p)
static const float SKY_STAR_GLINT_EXCL = 0.99966;   // [B02] cos(1.5 deg): the hashed field leaves the glint star alone
static const float SKY_STAR_TINT = 0.22;            // [B02] at most 22 percent toward ATMO_RIM (cool) or BEACON (warm)
static const float SKY_STAR_E3 = 0.011108996538242; // exp(-4.5): the Gaussian is truncated at 3 sigma (C0 at the cut)
static const uint  SKY_STAR_SEED = 0x7A3Du;
static const float SKY_STAR_HORIZON_FADE = 0.2;     // [B02] extinction near the horizon while rho > 0 (d.y below 0.2) ...
static const float SKY_STAR_EXTINCTION = 2.0;       // [B02] ... with weight saturate(2 rho): gone below the horizon at KF6a
static const float SKY_STAR_SKY_Y0 = 0.25;          // B-r2-3/E-r2-6: stars fade with the linear luminance of the sky behind them
static const float SKY_STAR_SKY_Y1 = 0.36;          //      (gradient + limb glow): full below 0.25 (KF6a's upper sky), gone in the
                                                    //      twilight band (0.33-0.44 at KF6a2) and the bright ATMO glow

// sun and planet impostors (14.7.7, 14.5.7; A4, B4, C-16)
static const float SKY_IMP_MIN_FWD = 0.02;          // [F1] a disc is drawn only while its centre is within ~88.9 deg of the
                                                    //      view axis (projection guard; off screen long before that)
static const float SKY_PLANET_OCCL_A0_KM = 90.0;    // A10/E11/PAC-13: the disc starts to hide stars at A = 90 km and hides
                                                    //      them completely from TC_PlanetAlphaA0 (150 km), before its colour rises
static const float3 SKY_PLANET_LIGHT = float3(0.55, 0.40, 0.73); // pal:ok (14.5.7 L_p in the disc frame; normalised in use)
static const float SKY_PLANET_BANDS = 7.0;          // 14.5.7 seven bands
static const float3 SKY_PLANET_BAND_COL[7] = { PAL_PLANET_A, PAL_PLANET_B, PAL_PLANET_C, PAL_PLANET_B, PAL_PLANET_A, PAL_PLANET_C, PAL_PLANET_B };
static const float SKY_PLANET_LIT = 1.0;            // [B02] lit band radiance = band colour
static const float SKY_PLANET_SHADE = 0.30;         // [B02] night side = band * TH_SHADE_TINT * 0.30
static const float SKY_PLANET_RIM_POW = 6.0;        // [B02] thin ATMO_RIM rim on the lit limb (outer ~3 percent of the radius)
static const float SKY_PLANET_RIM_GAIN = 0.6;

// dusk sky (C-15)
static const float SKY_HOLD_ON = 0.5;               // C-15: the hold starts at D = 0.5 (s ~ 579, inside the deck white-out)
static const float SKY_HOLD_D0 = 0.85;              // C-15: the horizon keeps the SKY_H_DUSK stop until D = 0.85 ...
static const float SKY_HOLD_D1 = 1.0;               //       ... and reaches the night stop at D = 1
static const float SKY_HOLD_TOP = 0.650074;         // the hold fades out toward the 25 deg stop, t = sqrt(sin 25 deg) (14.2.1)
static const float SKY_DUSK_CHROMA = 0.8;           // C-15: share of the H -> M stop chroma restored in OKLab (no grey midpoint)
static const float SKY_DUSK_CHROMA_D0 = 0.5;        //       on over D 0.5 -> 0.6 (deck pass), never in beats 1-5
static const float SKY_DUSK_CHROMA_D1 = 0.6;
static const float SKY_DUSK_CHROMA_MAXGAIN = 4.0;   // [F1] numeric guard on the chroma scale

// cloud sea (14.7.7; C-01, B2, C-02, E10)
static const float3 SKY_CLOUD_CELL = float3(6000.0, 1500.0, 400.0); // pal:ok (14.7.7 octave cell sizes in metres)
static const float SKY_CLOUD_FADE_PX0 = 12.0;       // E10: octave weight 0 below a 12 px cell footprint ... (D-028: 6 px)
static const float SKY_CLOUD_FADE_PX1 = 24.0;       // ... full weight from 24 px (D-028: 16 px)
static const float3 SKY_CLOUD_ALT0 = float3(40.0, 30.0, 10.0);   // pal:ok (B2/C-02/E10: altitude (km) where each octave starts to fade ...)
static const float3 SKY_CLOUD_ALT1 = float3(60.0, 50.0, 25.0);   // pal:ok (... and where it is gone; above 60 km only the weather systems)
static const float SKY_PUFF_WARP = 0.2;             // C-01: domain warp 0.2 cell (the spec's 0.6 made cell borders vein-like)
static const float SKY_PUFF_JIT0 = 0.1;             // [F1] dome centre jitter 0.1 .. 0.9 of the cell ...
static const float SKY_PUFF_JIT = 0.8;
static const float SKY_CLUSTER_R0 = 0.70;           // [F1] ... cluster dome radius 0.70 .. 0.95 cell (< 1 cell: the 3 x 3 search is exact)
static const float SKY_CLUSTER_R1 = 0.95;
static const float SKY_PUFF_R0 = 0.60;              // [F1] puff dome radius 0.60 .. 0.95 cell
static const float SKY_PUFF_R1 = 0.95;
static const float SKY_PUFF_BASE = 1.2;             // [F1] random base height (radii): a taller puff overlaps its neighbour with a round outline
static const float SKY_PUFF_RIM = 0.2;              // [F1] floor of sqrt(1 - u^2) in the dome slope (rim normal near horizontal, finite)
static const float SKY_PUFF_RIM_PX = 4.0;           // C-r2-1: ... raised to sqrt(4 fp / r) (fp = footprint in cells): the steep rim band
static const float SKY_PUFF_RIM_MAX = 0.9;          //      (width ~ rho^2 r / 2) never gets thinner than ~2 px, so no sub-pixel crescent
static const float SKY_PUFF_AA_PX = 3.0;            // B-r2-2: a dome's outline enters over a C1 ramp of 3 px (its disc coverage) ...
static const float SKY_PUFF_CREASE_PX = 3.0;        //      ... and a taller dome takes over across a crease band of ~3 px
static const float SKY_CLUSTER_SLOPE = 0.35;        // [F1] slope gains: 6 km clusters ...
static const float SKY_PUFF_SLOPE1 = 0.55;          //      ... 1.5 km puffs ...
static const float SKY_PUFF_SLOPE2 = 0.20;          //      ... 400 m sub-puffs (texture, not crescents)
static const float SKY_CLUSTER_BODY1 = 0.10;        // [F1] cluster body = smoothstep(0, 0.1, H0): puffs sit on the clusters
static const float SKY_GAP_CLUSTER = 0.45;          // C-01: broad soft shade between clusters (tone drop)
static const float SKY_GAP_PUFF = 0.25;             // [F1] smaller drop between the puffs of a cluster
static const float SKY_TONE_TOP = -0.185;           // C-01: tone v = lit - sin(sun elevation): CLOUD_TOP above -0.185 ...
static const float SKY_TONE_SHADE = -0.45;          //      ... CLOUD_SHADE below -0.45: about 60 / 22 / 18 percent top / mid / shade
static const float SKY_TONE_EDGE = 0.03;            // 14.7.7 "wide smoothsteps": half width of a tone edge in v (0.05 read as airbrush) ...
static const float SKY_TONE_PX = 2.5;               // B-r2-2/C-r2-1: ... and never narrower than 2.5 px by the analytic screen gradient of v
static const float SKY_TONE_FW = 0.7;               //      (or by 0.7 fwidth(v), whichever is larger)
static const float SKY_EARTH_AA_PX = 0.75;          // A-r2-2/E-r2-2: weather and coast edges w = 0.75 fwidth (~1.5 px, crisp at every scale)
static const float SKY_COVERAGE_MIN = 0.45;         // 14.7.7 c(A) = lerp(1.0, 0.45, smoothstep(40, 200, A_km))
static const float SKY_SEA_HAZE_L = 80000.0;        // 14.7.7 haze exp(-t / 80 km)
static const float SKY_LIMB_FULL = 0.0035;          // E16/B-r2-7: the sea is the full horizon haze for 0.2 deg below the R-limb ...
static const float SKY_LIMB_RAMP = 0.0209;          //      ... and resolves over the next 1.2 deg (D-017's 0.35 deg ramp drew a 2 px line)
static const float SKY_SEA_EMERGE_H = 300.0;        // [B02] the sea resolves out of the deck white over 300 m above the tops
static const float SKY_EARTH_HAZE_GAIN = 0.6;       // C-02: from orbit the haze grades toward ATMO_RIM x 0.6 (gated like the glow)

// Earth from orbit (B2, C-02)
static const float3 SKY_WX_CELL = float3(9.0e5, 2.5e5, 7.0e4);  // pal:ok (B2/C-02: weather systems, cells of 900 / 250 / 70 km)
static const float3 SKY_WX_AMP = float3(0.6, 0.28, 0.12);       // pal:ok ([F1] octave weights of the weather mask W)
static const float SKY_WX_FADE_PX0 = 24.0;          // B2: weather / continent octaves fade over a 24 -> 40 px footprint
static const float SKY_WX_FADE_PX1 = 40.0;
static const float SKY_WX_THR_FULL = 0.10;          // [F1] W threshold at c = 1 (0.1 percent quantile of W) ...
static const float SKY_WX_THR_MIN = 0.514;          // [F1] ... and at c = 0.45 (P(W > 0.514) = 0.45, measured over 4e7 m)
static const float SKY_WX_EDGE = 1.0e-4;            // A-r2-2/E-r2-2: weather edge = SKY_EARTH_AA_PX x fwidth(W) (this is only a floor; the
                                                    //      fixed 0.012 of D-047 blurred the edges over 30-60 px into "coffee stains")
static const float2 SKY_WX_PHASE = float2(2.94, 8.50);   // E-r2-2/PAC-r2-5: phase of the 900 km octave (searched offline: ~45 percent
                                                         //      ocean and 40 percent cloud under the beat-7 nadir, no land under KF7b's right limb)
static const float SKY_CONT_CELL0 = 3.0e6;          // C-02: continents, two low-frequency octaves (m) ...
static const float SKY_CONT_CELL1 = 1.0e6;
static const float SKY_CONT_AMP0 = 0.7;             // [F1] ... weights 0.7 / 0.3 ...
static const float SKY_CONT_AMP1 = 0.3;
static const float SKY_CONT_WARP = 0.35;            // [F1] ... warped by 0.35 cell for natural coasts
static const float SKY_CONT_LAND = 0.58;            // [F1] land above 0.58: about 30 percent land (ocean dominant, C-02)
static const float SKY_CONT_EDGE = 1.0e-4;          // A-r2-2: coast edge = SKY_EARTH_AA_PX x fwidth(m) (floor only)
static const float2 SKY_CONT_PHASE = float2(6.56, 1.18); // E-r2-2/C-r2-4: phase of the 3000 km octave (a coast under the nadir, ocean at
                                                         //      KF7b's right-hand limb, which read as the pad continuing into a tan plain)

// cable shadow band on the sea (14.7.7; the remaining numbers are TC_ShadowBand*)
static const float SKY_SHADOW_NARROW = 0.3;         // 14.7.7 w(t) = 50 (1 - 0.3 smoothstep(0, 30 km, t)) + 0.0033 t
static const float SKY_SHADOW_NARROW_T = 30000.0;
static const float SKY_SHADOW_EDGE = 6.0;           // 14.7.7 says +- 20 m; D-032: 6 m so the band core reads at 5-12 km (pixel AA via fwS)
static const float SKY_SHADOW_START = 25.0;         // [B02] the t > 0 test softened over +- 25 m (cf. D-004)
static const float SKY_BAND_ALT_WIDEN = 0.012;      // E3: half width + 0.012 h (h = height above the cloud tops)
static const float SKY_BAND_FADE_H0 = 4.0;          // E3: opacity fade over [max(12 km, 4 h), max(30 km, 16 h)]
static const float SKY_BAND_FADE_H1 = 16.0;
static const float SKY_BAND_OPACITY = 0.8;          // C-01 0.85 / E3 0.75 (spec TC_ShadowBandOpacity 0.62): a clean stripe
static const float SKY_BAND_DUSK_MIX = 0.5;         // A3/C-01: band tone lerp(CLOUD_SHADE x gain, SKY_M_DUSK, 0.5 smoothstep(0.5, 1, D))

// sunward glare on the sea (PAC-12 "dazzle")
static const float SKY_GLARE_GAIN = 0.6;            // PAC-12: the sea toward the sun is lifted toward the sun disc colour (CREAM x
static const float SKY_GLARE_POW = 6.0;             //      TC_SunDiscGain, above the bloom threshold) by 0.6 cos^6 of the angle to the sun,
                                                    //      times the sun disc alpha (gone with the sun at D = 0.85), before the haze

// limb glow (14.7.7, R7 2.4)
static const float SKY_GLOW_GAIN = 1.2;             // 14.7.7 ATMO_RIM x 1.2 x exp(-eps / eps0)
static const float SKY_GLOW_INNER = 0.35;           // [B02] the glow continues 0.35 eps0 onto the disc (no dark notch at the limb)
static const float SKY_GLOW_A0_KM = 20.0;           // [B02] R7 2.4 gate: eps0 is 9-31 deg at low altitude
static const float SKY_GLOW_A1_KM = 80.0;
static const float SKY_GLOW_SUN0 = -0.20;           // [B02] sunlit-side weight at the tangent point
static const float SKY_GLOW_SUN1 = 0.45;

static const float3 SKY_LUMA = float3(0.2126, 0.7152, 0.0722); // pal:ok (Rec. 709 luma weights)

// ------------------------------------------------------------------------------------------------
// Hashes on integer cells (PCG, no sine) and smooth / cellular noise in cell space.
// ------------------------------------------------------------------------------------------------
uint ThSkyHash(int2 c, uint seed) { return ThPcg(ThPcg(asuint(c.x) ^ seed) + asuint(c.y)); }
float ThSkyU24(uint h) { return (float)(h >> 8) * (1.0 / 16777216.0); }
float2 ThSkyU16x2(uint h) { return float2((float)(h & 0xFFFFu), (float)(h >> 16)) * (1.0 / 65536.0); }

// Value noise with a C2 quintic fade (used for the domain warps, the weather mask and the continents).
float ThSkyValueNoise(float2 x, uint seed)
{
    float2 ip = floor(x);
    float2 f = x - ip;
    int2 c = int2(ip);
    float a = ThSkyU24(ThSkyHash(c, seed));
    float b = ThSkyU24(ThSkyHash(c + int2(1, 0), seed));
    float e = ThSkyU24(ThSkyHash(c + int2(0, 1), seed));
    float g = ThSkyU24(ThSkyHash(c + int2(1, 1), seed));
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(a, b, u.x), lerp(e, g, u.x), u.y);
}

// Value noise with its gradient (cell units), for the warps whose Jacobian the cloud tone carries.
float ThSkyValueNoiseD(float2 x, uint seed, out float2 grad)
{
    float2 ip = floor(x);
    float2 f = x - ip;
    int2 c = int2(ip);
    float a = ThSkyU24(ThSkyHash(c, seed));
    float b = ThSkyU24(ThSkyHash(c + int2(1, 0), seed));
    float e = ThSkyU24(ThSkyHash(c + int2(0, 1), seed));
    float g = ThSkyU24(ThSkyHash(c + int2(1, 1), seed));
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
    grad = du * float2(lerp(b - a, g - e, u.y), lerp(e, g, u.x) - lerp(a, b, u.x));
    return lerp(lerp(a, b, u.x), lerp(e, g, u.x), u.y);
}

// Outer product a b^T (row a.x b, row a.y b).
float2x2 ThSkyOuter(float2 a, float2 b) { return float2x2(a.x * b, a.y * b); }

// Domain warp of 0.2 cell (C-01) shared by the cloud octaves; Wj[a][j] = d warped_a / d x_j.
float2 ThSkyWarpD(float2 x, uint seed, out float2x2 Wj)
{
    float2 g1, g2;
    float n1 = ThSkyValueNoiseD(x, seed + 101u, g1);
    float n2 = ThSkyValueNoiseD(x, seed + 202u, g2);
    Wj = float2x2(1.0, 0.0, 0.0, 1.0) + ThSkyOuter(float2(1.0, 0.0), 2.0 * SKY_PUFF_WARP * g1) + ThSkyOuter(float2(0.0, 1.0), 2.0 * SKY_PUFF_WARP * g2);
    return x + SKY_PUFF_WARP * (float2(n1, n2) * 2.0 - 1.0);
}

// Cluster octave (C-01): smooth sum of domes r (1 - |d|^2 / r^2)^2, one per cell (radius 0.70 .. 0.95 cell, so the
// 3 x 3 search is exact). Returns the height H (0 in the gaps, about r on a dome), its gradient grad (warped cell
// units, the shading slope), the Jacobian J of grad and the gradient gH of H, both per unwarped cell unit.
float ThSkyClusterOctave(float2 x, uint seed, out float2 grad, out float2x2 J, out float2 gH)
{
    float2x2 Wj;
    x = ThSkyWarpD(x, seed, Wj);
    float2 ip = floor(x);
    float2 fp = x - ip;
    int2 ic = int2(ip);
    float H = 0.0;
    grad = 0.0;
    float2x2 Jw = 0.0;
    [unroll] for (int j = -1; j <= 1; j++)
    {
        [unroll] for (int i = -1; i <= 1; i++)
        {
            int2 b = int2(i, j);
            uint h = ThSkyHash(ic + b, seed);
            float r = lerp(SKY_CLUSTER_R0, SKY_CLUSTER_R1, ThSkyU24(ThPcg(h)));
            float2 dlt = fp - (float2(b) + SKY_PUFF_JIT0 + SKY_PUFF_JIT * ThSkyU16x2(h));
            float k = max(1.0 - dot(dlt, dlt) / (r * r), 0.0);
            H += r * k * k;
            grad += (-4.0 * k / r) * dlt;
            float on = k > 0.0 ? -4.0 / r : 0.0;
            Jw += on * (float2x2(k, 0.0, 0.0, k) - ThSkyOuter(dlt, dlt) * (2.0 / (r * r)));
        }
    }
    J = mul(Jw, Wj);
    gH = mul(grad, Wj);
    return H;
}

// Puff octave (C-01, B-r2-2, C-r2-1): union of spherical domes r (base + sqrt(1 - u^2)), u = |d| / r, radius 0.60 ..
// 0.95 cell, random base 0 .. 1.2 r, so a taller puff covers its neighbour with a round outline. Nothing in it is
// discontinuous: each dome enters with a C1 disc coverage A over SKY_PUFF_AA_PX px, takes over from the running
// surface over a crease band k of ~SKY_PUFF_CREASE_PX px (smooth max), and the domes are blended in normal space
// Nd = g / sqrt(1 + g.g) so the lighting changes linearly across each ramp. The rim floor rho of the dome slope
// grows with the footprint fpCell (cells per px), so the steep rim band never gets thinner than ~2 px. Returns the
// coverage (1 on a puff, 0 in the gaps) and, per unwarped cell unit, its gradient gCov; G = slope (warped cell units),
// J = dG/dx.
float ThSkyPuffOctave(float2 x, uint seed, float fpCell, out float2 G, out float2x2 J, out float2 gCov)
{
    float2x2 Wj;
    x = ThSkyWarpD(x, seed, Wj);
    float2 ip = floor(x);
    float2 fp = x - ip;
    int2 ic = int2(ip);
    float fpc = max(fpCell, 1e-6);
    float H = 0.0, cov = 0.0;
    float2 Nd = 0.0, Gs = 0.0, gc = 0.0;
    float2x2 JN = 0.0;
    float2x2 I2 = float2x2(1.0, 0.0, 0.0, 1.0);
    [unroll] for (int j = -1; j <= 1; j++)
    {
        [unroll] for (int i = -1; i <= 1; i++)
        {
            int2 b = int2(i, j);
            uint h = ThSkyHash(ic + b, seed);
            uint h2 = ThPcg(h);
            float r = lerp(SKY_PUFF_R0, SKY_PUFF_R1, ThSkyU24(h2));
            float base = SKY_PUFF_BASE * ThSkyU24(ThPcg(h2));
            float2 q = (fp - (float2(b) + SKY_PUFF_JIT0 + SKY_PUFF_JIT * ThSkyU16x2(h))) / r;
            float uu = dot(q, q);
            float u = sqrt(uu);
            float sq = sqrt(saturate(1.0 - uu));
            // dome slope and its Jacobian: the sphere -q / sq, continued past sq = rho (rho >= sqrt(4 fp / r)) as a cone of
            // constant slope uc / rho, with the height hd continued to match, so the crease test sees the same slope
            float rho = clamp(sqrt(SKY_PUFF_RIM_PX * fpc / r), SKY_PUFF_RIM, SKY_PUFF_RIM_MAX);
            float uc = sqrt(1.0 - rho * rho);
            float cs = uc / rho;
            float ui = max(u, 1e-6);
            bool cone = sq <= rho;
            float ms = max(sq, rho);
            float2 g = cone ? -(cs / ui) * q : -q / ms;
            float2x2 Jg = cone ? -(cs / r) * (I2 / ui - ThSkyOuter(q, q) / (ui * ui * ui))
                               : -(I2 / ms + ThSkyOuter(q, q) / (ms * ms * ms)) / r;
            // normal-space vector of the dome and its Jacobian
            float cg = rsqrt(1.0 + dot(g, g));
            float2 nd = g * cg;
            float2x2 Jnd = cg * (Jg - ThSkyOuter(nd, mul(nd, Jg)));
            // pixel-scale disc coverage A (C1) and smooth max t = A s against the running surface
            float aa = SKY_PUFF_AA_PX * fpc;
            float za = saturate(0.5 + (1.0 - u) * r / aa);
            float A = za * za * (3.0 - 2.0 * za);
            float2 gA = (6.0 * za * (1.0 - za)) * (-q / ui / aa);
            float hd = r * (base + (cone ? rho - cs * (u - uc) : sq));
            float k = SKY_PUFF_CREASE_PX * fpc / rho;
            float zs = saturate(0.5 + 0.5 * (hd - H) / k);
            float s = zs * zs * (3.0 - 2.0 * zs);
            float2 gs = (6.0 * zs * (1.0 - zs)) * (0.5 / k) * (g - Gs);   // d(hd - H) = g - (slope of the running surface)
            float t = A * s;
            float2 gt = s * gA + A * gs;
            float2 dN = nd - Nd;
            JN += t * (Jnd - JN) + ThSkyOuter(dN, gt);
            Nd += t * dN;
            Gs += t * (g - Gs);
            H += t * (hd - H);
            gc = A > cov ? gA : gc;
            cov = max(cov, A);
        }
    }
    // back to slope space: G = Nd / sqrt(1 - Nd.Nd)
    float c = rsqrt(max(1.0 - dot(Nd, Nd), 1e-4));
    G = Nd * c;
    float2x2 JG = c * JN + (c * c * c) * ThSkyOuter(Nd, mul(Nd, JN));
    J = mul(JG, Wj);
    gCov = mul(gc, Wj);
    return cov;
}

// Cloud tone v at surface coordinates p (m) for the pixel footprint fpM (m per pixel), the altitude aKm and the sun S:
// v = lighting of the dome surface relative to a flat one (0 = flat, lit), the gaps between clusters and puffs pulled
// toward the shade. Octave weights fade by footprint (12 -> 24 px) and by altitude (all gone at 60 km); with every
// octave faded the surface is flat (v = 0, CLOUD_TOP). cloudOctaves is the cut-order switch of 14.8 item 4.
// gV = analytic surface gradient of v (per metre; footprint-driven weights are treated as constant), so the caller
// gets the screen gradient of v per pixel without fwidth (B-r2-2).
float ThSkyCloudTone(float2 p, float fpM, float aKm, float cloudOctaves, float3 S, out float2 gV)
{
    float3 cellPx = SKY_CLOUD_CELL / max(fpM, 1e-3);
    float3 fade = smoothstep(SKY_CLOUD_FADE_PX0, SKY_CLOUD_FADE_PX1, cellPx) * (1.0 - smoothstep(SKY_CLOUD_ALT0, SKY_CLOUD_ALT1, aKm));
    fade.y *= step(1.5, cloudOctaves);
    fade.z *= step(2.5, cloudOctaves);
    float2 G = 0.0;
    float2x2 J = 0.0;                       // dG/dp (per metre)
    float gap = 0.0;
    float2 gGap = 0.0;
    float body = 1.0;
    float2 gBody = 0.0;
    [branch] if (fade.x > 0.0)
    {
        float2 g0, gH0;
        float2x2 J0;
        float H0 = ThSkyClusterOctave(p / SKY_CLOUD_CELL.x + float2(0.37, 0.71), 11u, g0, J0, gH0);
        float tb = saturate(H0 / SKY_CLUSTER_BODY1);
        body = tb * tb * (3.0 - 2.0 * tb);                           // = smoothstep(0, SKY_CLUSTER_BODY1, H0)
        gBody = (6.0 * tb * (1.0 - tb) / (SKY_CLUSTER_BODY1 * SKY_CLOUD_CELL.x)) * gH0;
        G += (SKY_CLUSTER_SLOPE * fade.x) * g0;
        J += (SKY_CLUSTER_SLOPE * fade.x / SKY_CLOUD_CELL.x) * J0;
        gap += SKY_GAP_CLUSTER * fade.x * (1.0 - body);
        gGap -= (SKY_GAP_CLUSTER * fade.x) * gBody;
    }
    // the puff octaves fade (footprint) before the cluster octave does, so body is never used where fade.x = 0
    float w1 = fade.y * body;
    float2 gW1 = fade.y * gBody;
    [branch] if (w1 > 0.0)
    {
        float2 g1, gc1;
        float2x2 J1;
        float c1 = ThSkyPuffOctave(p / SKY_CLOUD_CELL.y + float2(17.3, 41.9), 23u, fpM / SKY_CLOUD_CELL.y, g1, J1, gc1);
        G += (SKY_PUFF_SLOPE1 * w1) * g1;
        J += SKY_PUFF_SLOPE1 * ((w1 / SKY_CLOUD_CELL.y) * J1 + ThSkyOuter(g1, gW1));
        gap += SKY_GAP_PUFF * w1 * (1.0 - c1);
        gGap += SKY_GAP_PUFF * (gW1 * (1.0 - c1) - (w1 / SKY_CLOUD_CELL.y) * gc1);
    }
    float w2 = fade.z * body;
    float2 gW2 = fade.z * gBody;
    [branch] if (w2 > 0.0)
    {
        float2 g2, gc2;
        float2x2 J2;
        ThSkyPuffOctave(p / SKY_CLOUD_CELL.z + float2(53.1, 7.70), 37u, fpM / SKY_CLOUD_CELL.z, g2, J2, gc2);
        G += (SKY_PUFF_SLOPE2 * w2) * g2;
        J += SKY_PUFF_SLOPE2 * ((w2 / SKY_CLOUD_CELL.z) * J2 + ThSkyOuter(g2, gW2));
    }
    float cE = length(S.xz);
    float2 sh = cE > 1e-5 ? S.xz / cE : float2(0.0, 1.0);
    float n = rsqrt(1.0 + dot(G, G));
    float a = S.y - cE * dot(G, sh);
    float lit = a * n - gap;
    float2 dLit = (-cE * n) * sh - (a * n * n * n) * G;          // d lit / d G
    gV = mul(dLit, J) - gGap;
    return lit - S.y;
}

// The three flat cloud tones (14.7.7): CLOUD_SHADE / 50 percent mix / CLOUD_TOP with wide smoothsteps (half width
// SKY_TONE_EDGE in v) that are never sharper than SKY_TONE_PX px: gPx = |screen gradient of v| per pixel (analytic).
float3 ThSkyCloudColour(float v, float gPx)
{
    float w = max(SKY_TONE_EDGE, 0.5 * SKY_TONE_PX * gPx);
    float s1 = smoothstep(SKY_TONE_SHADE - w, SKY_TONE_SHADE + w, v);
    float s2 = smoothstep(SKY_TONE_TOP - w, SKY_TONE_TOP + w, v);
    float3 mix50 = 0.5 * (PAL_CLOUD_TOP + PAL_CLOUD_SHADE);
    return lerp(lerp(PAL_CLOUD_SHADE, mix50, s1), PAL_CLOUD_TOP, s2);
}

// Weather systems seen from orbit (B2, C-02): W in about [0, 1] from three value-noise octaves (900 / 250 / 70 km),
// each faded to its mean below a 24 -> 40 px footprint; resolved = the coarsest octave's weight.
float ThSkyWeather(float2 p, float fpM, out float resolved)
{
    float3 f = smoothstep(SKY_WX_FADE_PX0, SKY_WX_FADE_PX1, SKY_WX_CELL / max(fpM, 1e-3));
    float v0 = ThSkyValueNoise(p / SKY_WX_CELL.x + SKY_WX_PHASE, 97u);
    float v1 = ThSkyValueNoise(p / SKY_WX_CELL.y + float2(2.71, 8.31), 89u);
    float v2 = ThSkyValueNoise(p / SKY_WX_CELL.z + float2(7.37, 3.19), 79u);
    resolved = f.x;
    return 0.5 + dot(SKY_WX_AMP * f, float3(v0, v1, v2) - 0.5); // pal:ok (noise vector, not a colour)
}

// Continents (C-02): two warped low-frequency octaves; resolved = the coarse octave's footprint weight.
float ThSkyContinents(float2 p, float fpM, out float resolved)
{
    float2 x = p / SKY_CONT_CELL0;
    float2 w = float2(ThSkyValueNoise(x + float2(9.1, 3.3), 61u), ThSkyValueNoise(x + float2(4.7, 6.2), 67u)) - 0.5;
    float v0 = ThSkyValueNoise(x + SKY_CONT_WARP * w + SKY_CONT_PHASE, 71u);
    float v1 = ThSkyValueNoise(p / SKY_CONT_CELL1 + float2(11.7, 2.31), 83u);
    float f1 = smoothstep(SKY_WX_FADE_PX0, SKY_WX_FADE_PX1, SKY_CONT_CELL1 / max(fpM, 1e-3));
    resolved = smoothstep(SKY_WX_FADE_PX0, SKY_WX_FADE_PX1, SKY_CONT_CELL0 / max(fpM, 1e-3));
    return 0.5 + SKY_CONT_AMP0 * (v0 - 0.5) + SKY_CONT_AMP1 * f1 * (v1 - 0.5);
}

// ------------------------------------------------------------------------------------------------
// Dusk gradient (C-15). OKLab (Ottosson 2020) for the chroma restoration of the H -> M segment.
// ------------------------------------------------------------------------------------------------
float3 ThSkyToOklab(float3 c)
{
    float3 lms = float3(dot(float3(0.4122214708, 0.5363325363, 0.0514459929), c),   // pal:ok (OKLab M1)
                        dot(float3(0.2119034982, 0.6806995451, 0.1073969566), c),   // pal:ok
                        dot(float3(0.0883024619, 0.2817188376, 0.6299787005), c));  // pal:ok
    lms = pow(max(lms, 0.0), 1.0 / 3.0);
    return float3(dot(float3(0.2104542553, 0.7936177850, -0.0040720468), lms),      // pal:ok (OKLab M2)
                  dot(float3(1.9779984951, -2.4285922050, 0.4505937099), lms),      // pal:ok
                  dot(float3(0.0259040371, 0.7827717662, -0.8086757660), lms));     // pal:ok
}

float3 ThSkyFromOklab(float3 L)
{
    float3 lms = float3(dot(float3(1.0, 0.3963377774, 0.2158037573), L),            // pal:ok (inverse M2)
                        dot(float3(1.0, -0.1055613458, -0.0638541728), L),          // pal:ok
                        dot(float3(1.0, -0.0894841775, -1.2914855480), L));         // pal:ok
    lms = lms * lms * lms;
    return float3(dot(float3(4.0767416621, -3.3077115913, 0.2309699292), lms),      // pal:ok (inverse M1)
                  dot(float3(-1.2684380046, 2.6097574011, -0.3413193965), lms),     // pal:ok
                  dot(float3(-0.0041960863, -0.7034186147, 1.7076147010), lms));    // pal:ok
}

// Darkness of the held horizon (C-15): D for D <= 0.5; then 0.5 (the dusk stop) until D = 0.85, reaching 1 at D = 1.
float ThSkyHoldD(float D)
{
    float Dh = lerp(SKY_HOLD_ON, 1.0, smoothstep(SKY_HOLD_D0, SKY_HOLD_D1, D));
    return D > SKY_HOLD_ON ? min(Dh, D) : D;
}

// Sky gradient of the sky shader: ThreadSkyColor with the darkness blended from the held horizon value to D at the
// 25 deg stop, and the OKLab chroma of the H -> M segment restored. horizon = the held horizon colour (sea haze).
// Identical to ThreadSkyColor(dirY, D) for D <= 0.5 (beats 1-5, where geometry fog must match the sky).
float3 ThSkyGradient(float dirY, float D, out float3 horizon)
{
    float Dh = ThSkyHoldD(D);
    float t = sqrt(saturate(dirY));
    float De = lerp(Dh, D, smoothstep(0.0, SKY_HOLD_TOP, t));
    float3 c = ThreadSkyColor(dirY, De);
    horizon = ThreadSkyColor(0.0, Dh);
    float k = SKY_DUSK_CHROMA * smoothstep(SKY_DUSK_CHROMA_D0, SKY_DUSK_CHROMA_D1, D);
    [branch] if (k > 0.0 && t < SKY_HOLD_TOP)
    {
        float3 h, m, z;
        ThreadSkyStops(De, h, m, z);
        float3 dm = m - h;
        float u = saturate(dot(c - h, dm) / max(dot(dm, dm), 1e-8));   // position on the H -> M segment
        float3 Lc = ThSkyToOklab(c);
        float C = length(Lc.yz);
        float Ct = lerp(length(ThSkyToOklab(h).yz), length(ThSkyToOklab(m).yz), u);
        float Cn = max(C, lerp(C, Ct, k));
        Lc.yz *= min(Cn / max(C, 1e-5), SKY_DUSK_CHROMA_MAXGAIN);      // identity at both stops (u = 0, 1)
        c = max(ThSkyFromOklab(Lc), 0.0);
    }
    return c;
}

// ------------------------------------------------------------------------------------------------
// Stars (14.7.7): equi-angular cube-face cells (~1.1 deg), 8 percent occupancy, three tiers, Gaussian splats of
// sigma = max(0.6 px, intrinsic size). The offset between the ray and a star is converted to pixels with the screen
// Jacobian of the ray (dX = ddx(d), dY = ddy(d)), so a splat is round and of constant pixel size anywhere in the
// frame and at any resolution: it never falls below one pixel and never crawls. The jitter keeps every splat
// (truncated at 3 sigma) inside its own cell, so the 3 x 3 search within the face never misses a neighbour.
// ------------------------------------------------------------------------------------------------
void ThSkyCubeFace(float3 d, out uint face, out float2 uv)
{
    float3 a = abs(d);
    if (a.x >= a.y && a.x >= a.z) { face = d.x >= 0.0 ? 0u : 1u; uv = d.yz / max(a.x, 1e-6); }
    else if (a.y >= a.z)          { face = d.y >= 0.0 ? 2u : 3u; uv = d.zx / max(a.y, 1e-6); }
    else                          { face = d.z >= 0.0 ? 4u : 5u; uv = d.xy / max(a.z, 1e-6); }
}

float3 ThSkyCubeDir(uint face, float2 uv)
{
    float3 p;
    if (face < 2u)      p = float3(face == 0u ? 1.0 : -1.0, uv.x, uv.y); // pal:ok (direction)
    else if (face < 4u) p = float3(uv.y, face == 2u ? 1.0 : -1.0, uv.x); // pal:ok (direction)
    else                p = float3(uv.x, uv.y, face == 4u ? 1.0 : -1.0); // pal:ok (direction)
    return normalize(p);
}

float3 ThSkyGlintDir()
{
    float az = TC_GlintAzimuthDeg * SKY_DEG2RAD, el = TC_GlintElevationDeg * SKY_DEG2RAD;
    return float3(cos(el) * sin(az), sin(el), cos(el) * cos(az)); // pal:ok (direction, 14.5.7)
}

// Star colour: CREAM, nudged by at most 22 percent toward ATMO_RIM or BEACON, normalised to unit luminance so the
// tier peaks are luminance peaks.
float3 ThSkyStarTint(float hc)
{
    float k = SKY_STAR_TINT * abs(2.0 * hc - 1.0);
    float3 t = lerp(PAL_CREAM, hc < 0.5 ? PAL_ATMO_RIM : PAL_BEACON, k);
    return t / max(dot(t, SKY_LUMA), 1e-3);
}

struct ThSkyJacobian
{
    float3 dX, dY;     // ray change per pixel along x and y
    float XX, XY, YY;  // Gram matrix
    float invDet;
    float inv2s2;      // 1 / (2 sigma^2), sigma in pixels
};

ThSkyJacobian ThSkyMakeJacobian(float3 dX, float3 dY)
{
    ThSkyJacobian J;
    J.dX = dX; J.dY = dY;
    J.XX = dot(dX, dX); J.XY = dot(dX, dY); J.YY = dot(dY, dY);
    float det = max(J.XX * J.YY - J.XY * J.XY, 1e-30);
    J.invDet = 1.0 / det;
    float pxAng = sqrt(sqrt(det));                                   // radians per pixel
    float sigma = max(SKY_STAR_SIGMA_MIN_PX, SKY_STAR_SIGMA_RAD / max(pxAng, 1e-9));
    J.inv2s2 = 0.5 / (sigma * sigma);
    return J;
}

// Truncated Gaussian splat of a star at direction sd seen from the ray d (offset solved in pixel units).
float ThSkyStarSplat(float3 sd, float3 d, ThSkyJacobian J)
{
    float3 dl = sd - d;
    float rx = dot(J.dX, dl), ry = dot(J.dY, dl);
    float px = (J.YY * rx - J.XY * ry) * J.invDet;
    float py = (J.XX * ry - J.XY * rx) * J.invDet;
    float g = exp(-(px * px + py * py) * J.inv2s2);
    return saturate((g - SKY_STAR_E3) / (1.0 - SKY_STAR_E3)) * step(0.0, dot(sd, d));
}

// All stars around d (no derivative instructions inside: J was built in uniform control flow).
float3 ThSkyStars(float3 d, ThSkyJacobian J, float tiers, float glintAmt)
{
    uint face; float2 uv;
    ThSkyCubeFace(d, face, uv);
    float2 aq = atan(uv) * (4.0 / PI);                                 // equi-angular face coordinates in [-1, 1]
    int2 cell = int2(clamp(floor((aq * 0.5 + 0.5) * SKY_STAR_CELLS), 0.0, SKY_STAR_CELLS - 1.0));
    const int N = 82;                                                  // = SKY_STAR_CELLS
    float3 G = ThSkyGlintDir();
    float3 acc = 0.0;
    [unroll] for (int j = -1; j <= 1; j++)
    {
        [unroll] for (int i = -1; i <= 1; i++)
        {
            int2 c = cell + int2(i, j);
            if (c.x >= 0 && c.y >= 0 && c.x < N && c.y < N)
            {
                uint h = ThPcg((face * (uint)N + (uint)c.y) * (uint)N + (uint)c.x + SKY_STAR_SEED);
                if (ThSkyU24(h) < SKY_STAR_OCCUPANCY)
                {
                    uint h2 = ThPcg(h);
                    uint h3 = ThPcg(h2 ^ 0x9E3779B9u);
                    uint h4 = ThPcg(h3);
                    float2 jit = ThSkyU16x2(h2);
                    float2 aS = ((float2(c) + SKY_STAR_MARGIN + jit * (1.0 - 2.0 * SKY_STAR_MARGIN)) / SKY_STAR_CELLS) * 2.0 - 1.0;
                    float3 sd = ThSkyCubeDir(face, tan(aS * (PI * 0.25)));
                    if (dot(sd, G) < SKY_STAR_GLINT_EXCL)
                    {
                        float ht = ThSkyU24(h3);
                        float peak = ht < SKY_STAR_P_TIER1 ? SKY_STAR_PEAK1 : ((ht < SKY_STAR_P_TIER12 || tiers < 2.5) ? SKY_STAR_PEAK2 : SKY_STAR_PEAK3);
                        acc += ThSkyStarTint(ThSkyU24(h4)) * (peak * ThSkyStarSplat(sd, d, J));
                    }
                }
            }
        }
    }
    // the glint star of 14.5.7: a tier-3 star at azimuth 0, elevation 20 deg, peak 2.5 -> 3.5, same splat size
    float gp = lerp(SKY_STAR_PEAK3, SKY_GLINT_PEAK, saturate(glintAmt));
    acc += ThSkyStarTint(0.5) * (gp * ThSkyStarSplat(G, d, J));
    return acc;
}

// ------------------------------------------------------------------------------------------------
// Far thread of beats 1-3 (14.7.3, 14.7.2). e = sine of the signed azimuth difference between the ray and the
// apparent axis (x_T, z_T); dPx = e / |grad e| is the signed pixel distance; coverage is the pixel-area coverage
// of a band of half width max(true, minimum); the energy rule dims a clamped line instead of aliasing it.
// Haze: the line is mixed toward the sky colour of its own direction (the gradient is the colour of the
// atmosphere toward infinity, D-003), so its contrast is exactly (1 - haze) >= 26 percent of the unfogged contrast
// against the sky behind it at every elevation, the rationale of the 0.74 cap (14.5.1). At the horizon this is
// the horizon colour of the spec text; higher up, mixing toward the horizon colour would cross the sky luminance
// near 32 deg elevation in KF1 and the line would vanish there (see the B02 report).
// ------------------------------------------------------------------------------------------------
float3 ThSkyFarThread(float3 col, float3 skyGrad, float3 d, float2 axz, float lxz, float2 bxz, float e, float eGrad,
                      float fwY, float pxAng, float3 S)
{
    float halfE = max(_ThThread.z, 1e-7);
    float trueHalfPx = halfE / eGrad;
    float minHalfPx = 0.5 * max(_ThScreen.y, TC_ThreadMinWidthFloorPx);
    float halfPx = max(trueHalfPx, minHalfPx);
    float dPx = e / eGrad;
    float cov = saturate(0.5 + halfPx - abs(dPx));
    float energy = trueHalfPx < minHalfPx ? max(trueHalfPx / minHalfPx, TC_ThreadAlphaFloor) : 1.0;
    float front = step(0.0, dot(axz, bxz));                       // never the antipodal azimuth
    float above = saturate(0.5 + d.y / fwY);                      // only above the horizon (AA'd)
    float zenith = smoothstep(4.0 * pxAng, 12.0 * pxAng, lxz);    // fade within 12 px of the zenith vanishing point
    float alpha = cov * energy * front * above * zenith * saturate(_ThThread.w);

    // cylinder shading in the ThreadFlat recipe (M05/M10 albedo: TEAL lit, TEAL_DEEP shade)
    float u = clamp(dPx / halfPx, -1.0, 1.0);
    float3 rightT = float3(bxz.y, 0.0, -bxz.x);    // pal:ok (direction)
    float3 towardCam = float3(-bxz.x, 0.0, -bxz.y); // pal:ok (direction)
    float3 N = u * rightT + sqrt(saturate(1.0 - u * u)) * towardCam;
    float lit = smoothstep(TC_TerminatorLo, TC_TerminatorHi, dot(N, S));
    float3 albedo = lerp(PAL_TEAL_DEEP, PAL_TEAL, lit);
    float3 radiance = albedo * (TC_AmbientWeight * ThreadAmbient(N) + TC_SunWeight * _ThSunColor.rgb);
    float3 thread = lerp(radiance, skyGrad, saturate(_ThThread2.x));
    return lerp(col, thread, alpha);
}

// ------------------------------------------------------------------------------------------------
// Screen-space circular impostors (A4, B4, C-16). ip = image-plane position (view space x, y over the forward
// distance) of the pixel; pxIP = image-plane units per pixel (1 / f). A disc of angular radius r around the world
// direction c is the circle of image-plane radius tan(r) around the projection of c.
// ------------------------------------------------------------------------------------------------
float3 ThSkyToView(float3 dirWS) { return mul((float3x3)UNITY_MATRIX_V, dirWS); }

// Projection of a world direction onto the image plane; fwd = its component along the view axis (> 0 in front).
float2 ThSkyImagePlane(float3 dirWS, out float fwd)
{
    float3 v = ThSkyToView(dirWS);
    fwd = -v.z;                                       // Unity view space looks down -z
    return v.xy / max(fwd, SKY_IMP_MIN_FWD);
}

// Signed pixel distance inside the disc (> 0 inside) and the offset from its centre in image-plane units.
float ThSkyImpostorPx(float2 ip, float2 ipC, float fwdC, float tanR, float pxIP, out float2 o)
{
    o = ip - ipC;
    float inside = (tanR - length(o)) / max(pxIP, 1e-9);
    return fwdC > SKY_IMP_MIN_FWD ? inside : -1.0e4;
}

// ------------------------------------------------------------------------------------------------
// Planet (14.5.7, 14.7.7): a sphere seen head-on in the disc frame (x right, y up = projected world vertical through
// the centre, z toward the viewer), q = screen offset / screen radius; seven latitude bands with a sinusoidal wobble,
// own key light L_p (never the sun), soft terminator, thin ATMO_RIM rim on the lit limb.
// ------------------------------------------------------------------------------------------------
float3 ThSkyPlanetNormal(float2 q)
{
    float r2 = dot(q, q);
    float2 nxy = q * rsqrt(max(r2, 1.0));            // continued past the rim as the limb normal (q / |q|, 0)
    return float3(nxy, sqrt(saturate(1.0 - r2)));     // pal:ok (normal in the disc frame)
}

float3 ThSkyPlanetColor(float3 n, float bc, float bcFw)
{
    float3 band = SKY_PLANET_BAND_COL[0];
    [unroll] for (int k = 1; k < 7; k++)
        band += (SKY_PLANET_BAND_COL[k] - SKY_PLANET_BAND_COL[k - 1]) * saturate(0.5 + (bc - (float)k) / bcFw);
    float ndl = dot(n, normalize(SKY_PLANET_LIGHT));
    float lit = smoothstep(TC_TerminatorLo, TC_TerminatorHi, ndl);
    float3 c = lerp(band * TH_SHADE_TINT * SKY_PLANET_SHADE, band * SKY_PLANET_LIT, lit);
    float rim = pow(saturate(1.0 - n.z), SKY_PLANET_RIM_POW) * SKY_PLANET_RIM_GAIN * smoothstep(0.0, TC_TerminatorHi, ndl);
    return c + PAL_ATMO_RIM * rim;
}

// ------------------------------------------------------------------------------------------------
// Earth / cloud sea surface (14.7.7): three flat cloud tones (puffs in beat 6, flat CLOUD_TOP weather systems from
// orbit); the coverage reveals EARTH_OCEAN / EARTH_LAND. Unresolved detail toward the limb grades to clear ocean.
// ------------------------------------------------------------------------------------------------
float3 ThSkySeaSurface(float3 cloud, float W, float fwW, float wRes, float mC, float fwM, float mRes, float coverage)
{
    float reveal = saturate((1.0 - coverage) / (1.0 - SKY_COVERAGE_MIN));
    float thr = lerp(SKY_WX_THR_FULL, SKY_WX_THR_MIN, reveal);
    float wT = max(SKY_WX_EDGE, SKY_EARTH_AA_PX * fwW);           // crisp ~1.5 px weather edge (A-r2-2)
    float amt = smoothstep(thr - wT, thr + wT, W);
    amt = lerp(1.0 - reveal, amt, wRes);                          // unresolved weather: cloud while c = 1, clear at c = 0.45
    amt = lerp(1.0, amt, saturate(reveal * 20.0));                // exactly all cloud while c = 1 (beat 6)
    float wC = max(SKY_CONT_EDGE, SKY_EARTH_AA_PX * fwM);         // crisp ~1.5 px coast
    float land = smoothstep(SKY_CONT_LAND - wC, SKY_CONT_LAND + wC, mC) * mRes;
    float3 earth = lerp(PAL_EARTH_OCEAN, PAL_EARTH_LAND, land);
    return lerp(earth, cloud, amt);
}

// Cable shadow band geometry (14.7.7 with E3's altitude terms): half width w, penumbra pen and opacity op at the along /
// across coordinates (tS, nS); fwS = pixel footprint of nS, h = camera height above the cloud tops (m).
void ThSkyBandGeom(float tS, float fwS, float h, out float w, out float pen, out float op)
{
    float tp = max(tS, 0.0);
    pen = TC_ShadowBandPenumbra * tp;
    w = TC_ShadowBandHalfWidth * (1.0 - SKY_SHADOW_NARROW * smoothstep(0.0, SKY_SHADOW_NARROW_T, tp)) + pen + SKY_BAND_ALT_WIDEN * h;
    float f0 = max(TC_ShadowBandFade0, SKY_BAND_FADE_H0 * h);
    float f1 = max(TC_ShadowBandFade1, SKY_BAND_FADE_H1 * h);
    op = SKY_BAND_OPACITY * (1.0 - smoothstep(f0, f1, tS)) * smoothstep(-SKY_SHADOW_START, SKY_SHADOW_START, tS);
    float core = w + SKY_SHADOW_EDGE;
    op *= saturate(core / (core + fwS));                          // a sub-pixel band dims instead of aliasing
    op *= step(0.5, _ThCable.x);
}

// Cable shadow band on the sea (14.7.7): its own deeper tone (A3, C-01), CLOUD_SHADE x gain blended toward SKY_M_DUSK,
// applied as a pure multiplier (B-r2-1, C-r2-2, E-r2-1, PAC-r2-4): tint = band tone / CLOUD_TOP, so over lit cloud the
// band is exactly that tone, every puff under it stays visible and darker, and the band never lifts the sea.
float3 ThSkyShadowBand(float3 surf, float nS, float fwS, float w, float pen, float op, float D)
{
    float cov = 1.0 - smoothstep(w - pen - SKY_SHADOW_EDGE - fwS, w + pen + SKY_SHADOW_EDGE + fwS, nS);
    float3 bandCol = lerp(PAL_CLOUD_SHADE * TC_ShadowBandColourGain, PAL_SKY_M_DUSK, SKY_BAND_DUSK_MIX * smoothstep(0.5, 1.0, D));
    return surf * (1.0 - (1.0 - bandCol / PAL_CLOUD_TOP) * (cov * op));
}

// Limb glow (14.7.7): ATMO_RIM x 1.2 x exp(-eps / eps0) above the R-limb, a short inner tail on the disc, the
// cusp at the limb rounded over one pixel (fwE), weighted by the sunlit side of the tangent point, gated by A.
float3 ThSkyLimbGlow(float epsR, float fwE, float dipR, float2 axz, float3 S)
{
    float eps0 = max(_ThEarth.w, 1e-4);
    float scale = eps0 * lerp(SKY_GLOW_INNER, 1.0, smoothstep(-fwE, fwE, epsR));
    float g = exp(-sqrt(epsR * epsR + fwE * fwE) / scale);
    float sd = sin(dipR), cd = cos(dipR);
    float3 nT = float3(axz.x * sd, cd, axz.y * sd);   // pal:ok (outward normal at the tangent point of this azimuth)
    float sunSide = smoothstep(SKY_GLOW_SUN0, SKY_GLOW_SUN1, dot(nT, S));
    float gate = smoothstep(SKY_GLOW_A0_KM, SKY_GLOW_A1_KM, _ThSky.y);
    return PAL_ATMO_RIM * (SKY_GLOW_GAIN * g * sunSide * gate);
}

// ------------------------------------------------------------------------------------------------
// The whole sky for the unit view ray d. cloudOctaves / starTiers / planetWobble are the cut-order switches of
// 14.8 item 4 (material properties; full quality 3 / 3 / 1).
// ------------------------------------------------------------------------------------------------
float3 ThreadSkyRadiance(float3 d, float cloudOctaves, float starTiers, float planetWobble)
{
    float3 cam = _WorldSpaceCameraPos;
    float D = _ThSky.x;
    float aKm = _ThSky.y;

    // ---- every derivative is taken here, in uniform control flow ----
    float3 dX = ddx(d), dY = ddy(d);
    ThSkyJacobian J = ThSkyMakeJacobian(dX, dY);
    float fwY = max(fwidth(d.y), 1e-6);
    float pxAng = sqrt(max(length(dX) * length(dY), 1e-14));
    float lxz = length(d.xz);
    float2 axz = d.xz / max(lxz, 1e-6);

    float3 S = _ThSunDir.xyz;
    float sl = dot(S, S);
    S = sl > 1e-8 ? S * rsqrt(sl) : float3(0.0, 1.0, 0.0); // pal:ok (fallback direction)

    // image plane of this pixel and its pixel size (exact for the rectilinear camera; |det| ignores RT y flips)
    float fwdPx;
    float2 ip = ThSkyImagePlane(d, fwdPx);
    float2 ipX = ddx(ip), ipY = ddy(ip);
    float pxIP = sqrt(max(abs(ipX.x * ipY.y - ipX.y * ipY.x), 1e-20));

    // sun impostor (0.9 deg, f tan(0.9 deg) pixels)
    float fwdS;
    float2 ipS = ThSkyImagePlane(S, fwdS);
    float2 oS;
    float sunPx = ThSkyImpostorPx(ip, ipS, fwdS, tan(TC_SunDiscRadiusDeg * SKY_DEG2RAD), pxIP, oS);

    // far-thread offset
    float2 tb = _ThThread.xy - cam.xz;
    float2 bxz = tb / max(length(tb), 1e-3);
    float e = axz.x * bxz.y - axz.y * bxz.x;
    float eGrad = max(length(float2(ddx(e), ddy(e))), 1e-9);

    // planet impostor (14 deg, f tan(14 deg) pixels) in its disc frame
    float3 pc = _ThPlanetDir.xyz;
    float pl = dot(pc, pc);
    pc = pl > 1e-8 ? pc * rsqrt(pl) : float3(0.0, 0.0, 1.0); // pal:ok (fallback direction)
    float fwdP;
    float2 ipP = ThSkyImagePlane(pc, fwdP);
    float tanP = tan(TC_PlanetRadiusDeg * SKY_DEG2RAD);
    float2 oP;
    float planetPx = ThSkyImpostorPx(ip, ipP, fwdP, tanP, pxIP, oP);
    float3 vc = ThSkyToView(pc);
    float3 vu = ThSkyToView(float3(0.0, 1.0, 0.0));               // pal:ok (world up in view space)
    float2 ey = vu.xy * max(-vc.z, SKY_IMP_MIN_FWD) + vc.xy * vu.z; // image-plane direction of the world vertical at the centre
    float eyl = length(ey);
    ey = eyl > 1e-6 ? ey / eyl : float2(0.0, 1.0);
    float2 ex = float2(ey.y, -ey.x);
    float2 q = float2(dot(oP, ex), dot(oP, ey)) / tanP;
    float3 nP = ThSkyPlanetNormal(q);
    float3 axisP = float3(sin(_ThPlanet.y), cos(_ThPlanet.y), 0.0); // pal:ok (spin axis in the disc frame, tilt from the vertical)
    float3 lonX = float3(axisP.y, -axisP.x, 0.0);                   // pal:ok (= cross(axis, view), view = +z)
    float lat = asin(clamp(dot(nP, axisP), -1.0, 1.0));
    float lon = atan2(dot(nP, lonX), max(nP.z, 1e-5));
    float wob = saturate(planetWobble) * TC_PlanetBandWobbleAmp * sin(TC_PlanetBandWobbleCycles * lon + TWO_PI * _ThPlanet.z);
    float bc = (0.5 - (lat + wob) * INV_PI) * SKY_PLANET_BANDS;   // 0 at the north pole, 7 at the south pole
    float bcFw = max(fwidth(bc), 1e-4);

    // Earth / cloud shell geometry, stable angular form (R7 5.3): camera at rho = Rc + h from the centre,
    // m = cos(angle to the nadir), mu_h = cos(psi_limb) of the shell from the CPU (double). Misses are clamped
    // onto the limb, so t, gamma and the surface coordinates are finite and continuous for every pixel.
    float seaOn = _ThSea.x > 0.5 ? 1.0 : 0.0;
    float hE = max(_ThEarth.y, 1.0);
    float muH = clamp(_ThEarth.x, 1e-4, 0.9999);
    float Rc = TC_EarthRadius + TC_CloudTop;
    float rhoC = Rc + hE;
    float m = -d.y;
    float mc = max(m, muH);
    float root = sqrt(max((mc - muH) * (mc + muH), 0.0));
    float tHit = (hE * (2.0 * Rc + hE) / rhoC) / (mc + root);         // = c / q, no cancellation
    float baseC = max(rhoC - tHit * mc, 1.0);                          // >= Rc^2 / rho > 0
    float gam = atan2(tHit * lxz, baseC);                              // central angle (well conditioned to the limb)
    float kArc = lxz > 1e-4 ? gam / lxz : tHit / baseC;                // finite at the nadir
    float2 pLocal = d.xz * (kArc * Rc);                                // camera-relative surface coordinates (m)
    float2 dpX = ddx(pLocal), dpY = ddy(pLocal);                       // surface metres per pixel step (smooth, for the tone gradient)
    float rootRho = rhoC * root;                                       // = Rc cos(incidence), stable
    // metres per pixel along the most stretched direction, analytic (slant distance x pixel angle / cos incidence):
    // a footprint from ddx / ddy is constant per 2 x 2 quad, and near the limb its quad steps moved the octave fades
    // and the weather iso-lines by a pixel per quad (saw-tooth edges)
    float fpM = tHit * pxAng / max(rootRho / Rc, 1e-3);
    float edge = m - muH;
    float earthCov = saturate(0.5 + edge / max(fwidth(edge), 1e-7)) * seaOn;
    float2 pW = cam.xz + pLocal;                                       // world XZ of the surface point
    float2 pDrift = pW - _ThSea.yz;                                    // the clouds drift along azimuth 60 deg

    // the R-based limb (dip of 14.7.5): the visible sea edge, where the haze is forced full and the glow starts
    float aE = hE + TC_CloudTop;
    float dipR = atan2(sqrt(aE * (2.0 * TC_EarthRadius + aE)), TC_EarthRadius);
    float elev = asin(clamp(d.y, -1.0, 1.0));
    float epsR = elev + dipR;                                          // angular distance above the R-limb (rad)
    float fwE = max(fwidth(elev), 1e-6);

    // cloud tone, weather and continents (heavy noise only on sea pixels; their fwidth is taken outside the branch)
    float coverage = saturate(_ThEarth.z);
    float vC = 0.0;
    float2 gVC = 0.0;
    float W = 0.5, wRes = 1.0;
    float mCont = 0.0, mRes = 1.0;
    float2 driftDir = float2(sin(TC_CloudDriftAzimuthDeg * SKY_DEG2RAD), cos(TC_CloudDriftAzimuthDeg * SKY_DEG2RAD));
    [branch] if (earthCov > 0.0)
    {
        vC = ThSkyCloudTone(pDrift, fpM, aKm, cloudOctaves, S, gVC);
        [branch] if (coverage < 0.999)
        {
            W = ThSkyWeather(pDrift, fpM, wRes);
            mCont = ThSkyContinents(pW - driftDir * (_ThSea.w * TC_EarthRadius), fpM, mRes); // Earth rotation under the camera
        }
    }
    // |screen gradient of the cloud tone| per pixel: analytic (sees every ramp, wherever the 2 x 2 quads fall), and at least
    // 0.7 fwidth (catches the curvature of v inside a 3 px ramp that the linear gradient underestimates)
    float gPxV = max(length(float2(dot(gVC, dpX), dot(gVC, dpY))), SKY_TONE_FW * fwidth(vC));
    float fwW = fwidth(W);
    float fwM = fwidth(mCont);

    // cable shadow coordinates (unscrolled surface point, so the band follows the walker's lateral position)
    float2 sh = _ThCable.yz;
    sh = dot(sh, sh) > 1e-8 ? normalize(sh) : float2(0.0, 1.0);
    float2 qS = pW - float2(TC_CableAxisX, TC_CableAxisZ);
    float tS = -dot(qS, sh);
    float nSs = qS.x * sh.y - qS.y * sh.x;                             // signed across-coordinate (smooth through the axis)
    float nS = abs(nSs);
    float fwS = fwidth(nSs);                                           // fwidth(|n|) doubles in the quads on the axis: a dotted centre line

    // ---- composition (no derivative instruction below this line) ----
    // 1. gradient (dusk hold, C-15)
    float3 skyHorizon;
    float3 skyGrad = ThSkyGradient(d.y, D, skyHorizon);
    float3 col = skyGrad;

    // 2. sun disc: flat CREAM x 1.3, analytic one-pixel edge, alpha 1 - smoothstep(0.55, 0.85, D)
    float sunCov = saturate(0.5 + sunPx) * saturate(_ThSunDir.w);
    col = lerp(col, PAL_CREAM * TC_SunDiscGain, sunCov);

    // planet presence: geometric coverage, ramped in over A 90 -> 150 km while its colour is still the sky colour
    float pCov = saturate(0.5 + planetPx) * smoothstep(SKY_PLANET_OCCL_A0_KM, TC_PlanetAlphaA0, aKm);

    // limb glow (added in step 7; known here so the stars can fade in it)
    float3 glow = seaOn > 0.5 ? ThSkyLimbGlow(epsR, fwE, dipR, axz, S) : 0.0;

    // 3. stars: alpha = star alpha x (1 - rho^2) washout, extinction toward the horizon while in air, hidden by the sun
    //    and by the planet disc (never seen through it, A10 / E11), and faded by the luminance of the sky behind them
    //    (gradient + limb glow; B-r2-3, E-r2-6), so none shows in the bright twilight band or the ATMO glow
    float rho = saturate(_ThSky.z);
    float starVis = saturate(_ThSky.w) * (1.0 - rho * rho)
                  * lerp(1.0, smoothstep(0.0, SKY_STAR_HORIZON_FADE, d.y), saturate(SKY_STAR_EXTINCTION * rho))
                  * (1.0 - smoothstep(SKY_STAR_SKY_Y0, SKY_STAR_SKY_Y1, dot(skyGrad + glow, SKY_LUMA)))
                  * (1.0 - sunCov) * (1.0 - pCov);
    float glintAmt = saturate(_ThPlanet.w) * saturate(_ThPlanet.x);    // the glint belongs to beat 7 (planet revealed)
    [branch] if (starVis > 1e-4)
        col += ThSkyStars(d, J, starTiers, glintAmt) * starVis;

    // 4. far thread (beats 1-3; _ThThread.w = 0 after the swap at s = 285)
    [branch] if (_ThThread.w > 0.0)
        col = ThSkyFarThread(col, skyGrad, d, axz, lxz, bxz, e, eGrad, fwY, pxAng, S);

    // 5. planet over the sun and the stars: full geometric coverage, colour rising from the local sky colour with
    //    alpha = smoothstep(150, 300, A_km) (PAC-13: it rises out of the dark, never a translucent overlay)
    [branch] if (pCov > 0.0)
    {
        float3 planet = lerp(skyGrad, ThSkyPlanetColor(nP, bc, bcFw), saturate(_ThPlanet.x));
        col = lerp(col, planet, pCov);
    }

    // 6. bank and deck terms along the sky ray (beats 1-5 and the deck pass): sky rays at infinity
    float3 colFar = ThreadFogApplySky(col, d, SKY_FAR_D);
    float3 result = colFar;

    // 7. Earth / cloud shell + limb glow (s >= 590 with h > 0)
    [branch] if (seaOn > 0.5)
    {
        float bandW, bandPen, bandOp;
        ThSkyBandGeom(tS, fwS, hE, bandW, bandPen, bandOp);
        float3 cloud = ThSkyCloudColour(vC, gPxV);
        float3 surf = ThSkySeaSurface(cloud, W, fwW, wRes, mCont, fwM, mRes, coverage);
        surf = ThSkyShadowBand(surf, nS, fwS, bandW, bandPen, bandOp, D);
        // sunward glare (PAC-12): forward scatter toward the sun while its disc is drawn, before the haze
        float glare = SKY_GLARE_GAIN * pow(max(dot(d, S), 1e-6), SKY_GLARE_POW) * saturate(_ThSunDir.w);
        surf = lerp(surf, PAL_CREAM * TC_SunDiscGain, glare);
        // haze toward the horizon colour with exp(-t_eff / 80 km); t_eff = min(t, path below the 8.5 km haze layer)
        // so the Earth seen from orbit is not erased by vacuum (t > 600 km); identical to t in beat 6 (h < 8.5 km).
        // From orbit (glow gate) the haze colour grades toward ATMO_RIM (C-02); in beat 6 it is the held sky horizon.
        float hh = TC_ScaleHeight * (2.0 * Rc + TC_ScaleHeight);
        float sH = hh / (rootRho + sqrt(rootRho * rootRho + hh));
        float haze = 1.0 - exp(-min(tHit, sH) / SKY_SEA_HAZE_L);
        // limb band (E16, B-r2-7): full haze above the R-limb and for SKY_LIMB_FULL below it, resolving over
        // SKY_LIMB_RAMP (C1), combined with the path haze without a kink, so the sea meets the sky without a line
        float limb = 1.0 - smoothstep(SKY_LIMB_FULL, SKY_LIMB_FULL + SKY_LIMB_RAMP, -epsR);
        haze = 1.0 - (1.0 - haze) * (1.0 - limb);
        float3 hazeCol = lerp(skyHorizon, PAL_ATMO_RIM * SKY_EARTH_HAZE_GAIN, smoothstep(SKY_GLOW_A0_KM, SKY_GLOW_A1_KM, aKm));
        float3 sea = lerp(surf, hazeCol, haze);
        float3 colE = lerp(col, sea, earthCov) + glow;

        // the deck slab of ThreadCommon is planar: stop the ray at its top plane when the camera is above it (a ray
        // to the curved shell would otherwise cross the planar slab beyond ~10 km and paint the sea white)
        float aboveTop = cam.y - _ThDeck.y;
        float maxD = (d.y < 0.0 && aboveTop > 0.0) ? aboveTop / max(-d.y, 1e-6) : SKY_FAR_D;
        float3 colNear = ThreadFogApplySky(colE, d, maxD);
        // emergence: the sea resolves out of the deck white over the first 300 m above the cloud tops
        result = lerp(colFar, colNear, smoothstep(0.0, SKY_SEA_EMERGE_H, _ThEarth.y));
    }
    return max(result, 0.0);   // also maps a NaN to 0 (D3D min/max return the non-NaN operand)
}

#endif // THREAD_SKY_LIB_INCLUDED
