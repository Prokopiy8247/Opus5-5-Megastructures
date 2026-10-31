// ThreadPatterns.hlsl - the procedural world-space patterns of Megastructure/ThreadFlat (MASTER_PROMPT
// 14.2.2 - 14.2.3): M01 salt crack network, M03 steps (stripes + tread filter), M04 terrace mandala,
// M05 cable lay lines, bands and marker plates, M06 deck edge stripes and trim; plus the coverage fades (distance,
// far below the walker) and the ordered dither shared by ThreadFlat and ThreadLine.
// Owner: B01 (material shaders).
//
// Filtering contract: every pattern is filtered analytically. The caller computes the screen-space
// derivatives of the world position ONCE, in uniform control flow, at the top of the fragment
// (dpdx = ddx(positionWS), dpdy = ddy(positionWS)) and passes them in. Nothing in this file calls
// ddx/ddy/fwidth, so every function is safe inside a dynamic branch, and no derivative is ever taken of a
// wrapped or seamed quantity (atan2 seams, coordinate wraps): the derivative of each pattern coordinate is
// formed from the continuous world-position derivatives and the analytic gradient of that coordinate.
//
// Constants that Part 14 states but ThreadConstants does not carry as scalars (array members, recipe numbers)
// are #defined here with the spec line they come from (TP_ prefix); they are listed as contract requests.
// Every float3/float4 literal carries the marker  pal:ok  (T-PAL, 14.7.8).
#ifndef THREAD_PATTERNS_INCLUDED
#define THREAD_PATTERNS_INCLUDED

#include "ThreadCommon.hlsl"

#define TP_TWO_PI 6.28318530717958647693
#define TP_HEX_H  0.86602540378443864676   // sqrt(3) / 2: row height of a unit hexagonal lattice

// "upward faces" (M04 mandala, M06 deck stripes): weight by the normal's y component.
#define TP_UP_N0 0.70
#define TP_UP_N1 0.90

// ------------------------------------------------------------------------------------------------
// Analytic box filters.
// ------------------------------------------------------------------------------------------------

// Exact box filter (width w, same units as x) of the indicator lo <= x <= hi.
float ThBoxInterval(float x, float lo, float hi, float w)
{
    w = max(w, 1e-6);
    return saturate((min(x + 0.5 * w, hi) - max(x - 0.5 * w, lo)) / w);
}

// Exact box filter of the indicator |x| <= h (a line of full width 2h centred on x = 0).
float ThBoxPulse(float x, float h, float w)
{
    return ThBoxInterval(x, -h, h, w);
}

// Antiderivative of a periodic pulse train (period P, pulses of width W centred on every multiple of P).
float ThPulseTrainIntegral(float y, float P, float W)
{
    float t = y + 0.5 * W;            // pulses now occupy [kP, kP + W]
    float k = floor(t / P);
    return k * W + min(t - k * P, W);
}

// Box-filtered periodic pulse train (exact for any filter width; the integral form never aliases a pulse
// away). Beyond half a period per pixel the box filter itself starts to beat against the period, so the
// result settles on the exact area mean W / P.
float ThPulseTrain(float y, float P, float W, float w)
{
    w = max(w, 1e-6);
    float c = (ThPulseTrainIntegral(y + 0.5 * w, P, W) - ThPulseTrainIntegral(y - 0.5 * w, P, W)) / w;
    return lerp(saturate(c), W / P, smoothstep(0.5 * P, P, w));
}

// Same, but fading out (to 0) instead of to the mean once the period is under about two pixels. Used by the
// cable bands so the far cable converges to the plain TEAL / TEAL_DEEP of the ThreadLine ribbons and of the
// analytic far thread (no colour step at the 20 km ribbon junction).
float ThPulseTrainFadeOut(float y, float P, float W, float w)
{
    w = max(w, 1e-6);
    float c = (ThPulseTrainIntegral(y + 0.5 * w, P, W) - ThPulseTrainIntegral(y - 0.5 * w, P, W)) / w;
    return saturate(c) * (1.0 - smoothstep(0.25 * P, 0.5 * P, w));
}

float ThUpFacing(float3 N) { return smoothstep(TP_UP_N0, TP_UP_N1, N.y); }

// ------------------------------------------------------------------------------------------------
// Coverage fades shared by ThreadFlat and ThreadLine (every pass): the keep factor 0..1 of a fragment, resolved by
// alpha-to-coverage in ForwardLit when MSAA is available, else (and in the shadow / depth passes) by the ordered
// dither below.
// ------------------------------------------------------------------------------------------------
static const float kThBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

void ThDitherClip(float2 pixel, float keep)
{
    uint2 p = (uint2)pixel & 3u;
    float threshold = (kThBayer4[p.y * 4u + p.x] + 0.5) / 16.0;
    clip(keep - threshold);
}

// Distance fade-out between fadeNear and fadeFar metres from the camera (D-042 posts, B3 handrails).
float ThDistanceKeep(float3 positionWS, float fadeNear, float fadeFar)
{
    float d = distance(positionWS, _WorldSpaceCameraPos);
    return 1.0 - smoothstep(fadeNear, max(fadeFar, fadeNear + 1e-3), d);
}

// Walkway far below the walker (critique run07 A-r2-1 / A5): once the virtual altitude is high, deck, rails, posts and
// arms lying 30-70 m below the camera dissolve into the sea / Earth drawn behind them, so the one-turn helix never
// reads as a spoked wheel 100 m down that measures the whole climb. Purely spatial (a function of the height below
// the camera), so nothing pops while walking; the deck under the walker (1.7 m down) and the 120 m of path behind are
// untouched. The altitude gate ramps over A = 2.0-2.4 km (s 581.6-587.5), inside the cloud deck, where a point 30 m
// below the camera already lies behind an optical depth above 2.7, so the gate itself is never seen.
#define TP_BELOW_DROP0 30.0     // m below the camera where the fade starts ...
#define TP_BELOW_DROP1 70.0     // ... and is complete
#define TP_BELOW_A0    2000.0   // virtual altitude (m) where the gate opens (s 581.6, camera 200 m into the deck slab)
#define TP_BELOW_A1    2400.0   // ... and is fully on (s 587.5)

float ThBelowFadeKeep(float3 positionWS)
{
    float gate = smoothstep(TP_BELOW_A0, TP_BELOW_A1, _ThSky.y * 1000.0);   // _ThSky.y = A_virt in km
    return 1.0 - gate * smoothstep(TP_BELOW_DROP0, TP_BELOW_DROP1, _WorldSpaceCameraPos.y - positionWS.y);
}

// ------------------------------------------------------------------------------------------------
// M01 salt: irregular polygonal crack network (14.2.3 line 460, 14.12 line 1012, 14.5.1 line 652).
// Four hierarchical levels; level l has cell 2.4 * 10^l m and line 0.035 * 10^l m; it fades in over
// [40, 60] * 10^(l-1) m (level 0: always in) and out over [40, 60] * 10^l m of camera distance, and fades to
// zero when its cell is smaller than two pixels (footprint > 0.5 cell, 14.2.2 line 454).
// Cells: a jittered hexagonal lattice (spacing = cell) with IQ's two-pass exact border distance
// ("Voronoi edges", iquilezles.org/articles/voronoilines). With a jitter radius below 0.289 spacings the
// nearest jittered point is always among the nearest lattice point and its 6 neighbours, and every
// Voronoi neighbour of it is among its 12 nearest lattice neighbours, so 7 + 12 hashes are exact.
// Wrapping: per level, the pattern is periodic over 64 columns x 74 rows (74 rows = 37 * sqrt(3) cells, an
// exact period of the hexagonal lattice) because the hash is taken on the wrapped offset coordinates; the
// lattice is evaluated relative to a camera-snapped origin (a multiple of that period), so coordinates stay
// small and the pattern does not move when the origin snaps. The ground sinks only in y, so XZ is enough.
//
// Altitude look (critique run06 A2 / C-12 / E4): seen from 200-650 m above, the spec network (lines 5 percent
// darker, about one pixel wide, then hazed) is 2-3 percent contrast and the salt reads as blank wallpaper. With
// the camera height above the salt h, levels 2-4 (loop index 1-3) blend by smoothstep(40, 150, h) to
// 13 percent darkening, 1.71x wider lines (0.6 / 6 / 60 m), a fade-out window twice as far (so the 24 m level
// still shows next to the 240 m level from 640 m, acceptance 14.5.5 (6)) and +-3 percent flat per-cell tones on
// level 3. Below 40 m (beats 1-4) the spec look is unchanged, and level 1 never changes (5 percent, the subtle
// ground-flow cue near the walker). Every level still fades by its pixel footprint; that fade is energy-preserving
// (the coverage blends to the network's exact area mean 2 * line / cell instead of vanishing), so nothing shimmers
// and the average ground tone does not move while levels hand over.
// ------------------------------------------------------------------------------------------------
#define TP_SALT_CELL0     2.4      // level 0 cell (m), 14.2.3 line 460
#define TP_SALT_LINE0     0.035    // level 0 line width (m), 14.2.3 line 460
#define TP_SALT_WIN0      40.0     // level 0 fade-out start (m); each level x10, 14.2.3 line 460
#define TP_SALT_WIN1      60.0     // level 0 fade-out end (m)
#define TP_SALT_RATIO     10.0     // cell / line / window ratio between levels, 14.12 line 1012
#define TP_SALT_DARKEN    0.05     // lines 5 percent darker (line colour = base * 0.95), 14.2.3 line 460
#define TP_SALT_FOOT0     0.25     // footprint (cells per pixel) where a level starts to fade
#define TP_SALT_FOOT1     0.5      // ... and is gone (fwidth * cell > 0.5), 14.2.2 line 454
#define TP_SALT_JITTER    0.28     // jitter radius in lattice spacings (must stay below 0.289, see above)
#define TP_SALT_WRAP_COLS 64       // wrap period in x (columns)
#define TP_SALT_WRAP_ROWS 74       // wrap period in z (rows; even, = 2 * 37)
#define TP_SALT_ALT0       40.0    // camera height above the salt (m) where the altitude look starts (terrace eye 37.7 m), A2
#define TP_SALT_ALT1       150.0   // ... and is complete (KF5a is 231 m, KF5c 637 m above the salt), A2 / E4
#define TP_SALT_LINE_ALT   0.06    // level-0-scaled line at altitude: levels 2-4 draw 0.6 / 6 / 60 m (spec 0.35 / 3.5 / 35), A2
#define TP_SALT_DARKEN_ALT 0.13    // levels 2-4 darkening at altitude (spec 0.05; A2 / C-12 / E4 ask 12-15 percent)
#define TP_SALT_WIN_ALT    2.0     // fade-out window multiplier of levels 2-4 at altitude (level 2: 800-1200 m), E4 / 14.5.5 (6)
#define TP_SALT_TONE       0.03    // level 3 flat per-cell value variation at altitude, +-3 percent, C-12
#define TP_SALT_TONE_LEVEL 2       // loop index of spec level 3 (cell 240 m)

static const int2 kTpHex7[7] =      // a lattice point and its 6 neighbours (skew coordinates)
{
    int2(0, 0), int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1), int2(1, -1), int2(-1, 1)
};
static const int2 kTpHex12[12] =    // first ring (distance 1) and the sqrt(3) ring
{
    int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1), int2(1, -1), int2(-1, 1),
    int2(1, 1), int2(-1, -1), int2(2, -1), int2(-2, 1), int2(1, -2), int2(-1, 2)
};

// Unjittered position of lattice point (i, j): i * (1, 0) + j * (0.5, sqrt(3)/2).
float2 TpHexPos(int2 ij) { return float2(ij.x + 0.5 * ij.y, TP_HEX_H * ij.y); }

// Wrapped index of lattice point (i, j), periodic over the wrap (offset coordinates c = i + floor(j/2), r = j: a
// translation by the z period maps (i, j) to (i - 37, j + 74), which leaves c unchanged). Range < 64 + 73 * 128.
uint TpHexIndex(int2 ij)
{
    int c = ij.x + (ij.y >> 1);                   // arithmetic shift = floor(j / 2) for negative j too
    int r = ij.y;
    // positive before the (unsigned, fast) modulus: the offsets are whole periods, |c|, |r| stay far below them
    uint cm = (uint)(c + TP_SALT_WRAP_COLS * 4096) % (uint)TP_SALT_WRAP_COLS;
    uint rm = (uint)(r + TP_SALT_WRAP_ROWS * 4096) % (uint)TP_SALT_WRAP_ROWS;
    return cm + rm * 128u;
}

// Jitter of lattice point (i, j) (hash stream salt = 1..4).
float2 TpHexJitter(int2 ij, uint salt)
{
    uint h = ThPcg(TpHexIndex(ij) + salt * 16384u);
    float2 j = float2(h & 0xFFFFu, h >> 16) * (2.0 / 65535.0) - 1.0;
    float l2 = dot(j, j);
    j *= (l2 > 1.0) ? rsqrt(l2) : 1.0;            // clamp the square jitter to the unit disc
    return j * TP_SALT_JITTER;
}

// Flat tone of the cell of lattice point (i, j) in [-1, 1] (own hash stream salt + 8, disjoint from the jitter
// inputs; periodic over the wrap like the jitter, so the tones never move when the origin snaps).
float TpHexTone(int2 ij, uint salt)
{
    uint h = ThPcg(TpHexIndex(ij) + (salt + 8u) * 16384u);
    return (float)(h & 0xFFFFu) * (2.0 / 65535.0) - 1.0;
}

// Distance (lattice units) from x to the border of its Voronoi cell, the unit direction u of that border's
// normal (d = dot(m - x, u) for the bisector midpoint m, so grad_x d = -u), and the lattice points of the own
// cell and of the neighbour across that border.
float TpSaltBorder(float2 x, uint salt, out float2 u, out int2 own, out int2 nb)
{
    float jf = x.y / TP_HEX_H;
    float i_f = x.x - 0.5 * jf;
    int2 b = int2((int)floor(i_f), (int)floor(jf));

    // nearest unjittered lattice point among the four corners of the skew cell
    int2 n0 = b;
    float best = 1e9;
    [unroll] for (int k = 0; k < 4; k++)
    {
        int2 ij = b + int2(k & 1, k >> 1);
        float2 r = TpHexPos(ij) - x;
        float d2 = dot(r, r);
        if (d2 < best) { best = d2; n0 = ij; }
    }

    // pass 1: nearest jittered point
    int2 nn = n0;
    float2 rn = 0.0;
    best = 1e9;
    [unroll] for (int k1 = 0; k1 < 7; k1++)
    {
        int2 ij = n0 + kTpHex7[k1];
        float2 r = TpHexPos(ij) + TpHexJitter(ij, salt) - x;
        float d2 = dot(r, r);
        if (d2 < best) { best = d2; nn = ij; rn = r; }
    }

    // pass 2: exact distance to the nearest bisector
    float md = 1e9;
    float2 bu = float2(1.0, 0.0);
    int2 bn = nn + kTpHex12[0];
    [unroll] for (int k2 = 0; k2 < 12; k2++)
    {
        int2 ij = nn + kTpHex12[k2];
        float2 r = TpHexPos(ij) + TpHexJitter(ij, salt) - x;
        float2 e = r - rn;                          // never zero: lattice points are >= 0.44 apart
        float2 eu = e * rsqrt(max(dot(e, e), 1e-8));
        float d = dot(0.5 * (rn + r), eu);
        if (d < md) { md = d; bu = eu; bn = ij; }
    }
    u = bu;
    own = nn;
    nb = bn;
    return max(md, 0.0);
}

// Box-filtered crack coverage of one level; with withTone, also the cell's flat tone in [-1, 1], box-filtered
// across the nearest border (own-side weight saturate(0.5 + d / wd): the two cells meet at their mean on the
// border, so the tone step never aliases; the crack line covers it anyway).
float TpSaltLevel(float3 positionWS, float3 dpdx, float3 dpdy, float cell, float lineW, uint salt, bool withTone, out float tone)
{
    float2 period = float2(TP_SALT_WRAP_COLS, TP_SALT_WRAP_ROWS * TP_HEX_H) * cell;   // metres
    float2 origin = period * floor(_WorldSpaceCameraPos.xz / period);              // camera-snapped wrap origin
    float2 x = (positionWS.xz - origin) / cell;
    float2 u;
    int2 own, nb;
    float d = TpSaltBorder(x, salt, u, own, nb);
    // fwidth(d) from the analytic gradient (-u) and the continuous position derivatives
    float wd = (abs(dot(u, dpdx.xz)) + abs(dot(u, dpdy.xz))) / cell;
    tone = 0.0;
    if (withTone)
    {
        float c = saturate(0.5 + d / max(wd, 1e-6));
        tone = lerp(TpHexTone(nb, salt), TpHexTone(own, salt), c);
    }
    return ThBoxPulse(d, 0.5 * lineW / cell, wd);
}

// Albedo multiplier of the salt (all four crack levels, plus the level-3 cell tones at altitude): at most 1.03,
// 1 on bare salt near the walker. Applied to the lit and the shade albedo alike.
float ThSaltCrackMul(float3 positionWS, float3 dpdx, float3 dpdy)
{
    float dist = distance(positionWS, _WorldSpaceCameraPos);
    float footM = max(length(dpdx.xz), length(dpdy.xz));   // metres per pixel along the long footprint axis
    // camera height above this salt point (the salt is flat, so this is the camera's height above the ground)
    float alt = smoothstep(TP_SALT_ALT0, TP_SALT_ALT1, _WorldSpaceCameraPos.y - positionWS.y);
    float mul = 1.0;
    float cell = TP_SALT_CELL0;
    float scale = 1.0;
    [unroll] for (int l = 0; l < 4; l++)
    {
        bool upper = (l > 0);                                         // spec levels 2-4: the altitude look applies
        float winOut = upper ? lerp(1.0, TP_SALT_WIN_ALT, alt) : 1.0;
        float fadeIn = upper ? smoothstep(TP_SALT_WIN0 * scale / TP_SALT_RATIO, TP_SALT_WIN1 * scale / TP_SALT_RATIO, dist) : 1.0;
        float fadeOut = 1.0 - smoothstep(TP_SALT_WIN0 * scale * winOut, TP_SALT_WIN1 * scale * winOut, dist);
        float win = fadeIn * fadeOut;
        float footT = smoothstep(TP_SALT_FOOT0, TP_SALT_FOOT1, footM / cell);   // 1 = cells under two pixels
        float lineW = scale * (upper ? lerp(TP_SALT_LINE0, TP_SALT_LINE_ALT, alt) : TP_SALT_LINE0);
        float darken = upper ? lerp(TP_SALT_DARKEN, TP_SALT_DARKEN_ALT, alt) : TP_SALT_DARKEN;
        float meanCov = saturate(2.0 * lineW / cell);                 // exact area fraction of a hexagonal network
        float cov = meanCov;
        float tone = 0.0;
        [branch] if (win * (1.0 - footT) > 1e-3)
            cov = lerp(TpSaltLevel(positionWS, dpdx, dpdy, cell, lineW, (uint)(l + 1), l == TP_SALT_TONE_LEVEL, tone), meanCov, footT);
        mul *= 1.0 - win * darken * cov;
        mul *= 1.0 + win * (1.0 - footT) * alt * TP_SALT_TONE * tone;  // tone mean is 0: fading it keeps the average
        cell *= TP_SALT_RATIO;
        scale *= TP_SALT_RATIO;
    }
    return mul;
}

// ------------------------------------------------------------------------------------------------
// M03 steps (14.2.3 line 462, 14.5.3 lines 677-678 and 686): SALT_WHITE inlay stripes 0.3 m wide on every
// 20th tread (8 m of world z from z = 240, centred on the tread), and the analytic tread filter: when one
// step is smaller than 1.5 px the surface blends to the mean of tread and riser.
// ------------------------------------------------------------------------------------------------
#define TP_STEP_STRIPE_W     0.3     // stripe width (m), 14.5.3 line 677
#define TP_STEP_STRIPE_EVERY 20.0    // one stripe every 20 treads, 14.5.3 line 677
#define TP_STEP_FILTER_PX    1.5     // fully blended when one step < 1.5 px, 14.2.3 line 462
#define TP_TREAD_N0          0.50    // tread vs riser by the normal's y component
#define TP_TREAD_N1          0.80

static const float3 TP_STEP_TREAD_NORMAL = float3(0.0, 1.0, 0.0);    // pal:ok (stairs rise toward +z)
static const float3 TP_STEP_RISER_NORMAL = float3(0.0, 0.0, -1.0);   // pal:ok

float ThTreadWeight(float3 N) { return smoothstep(TP_TREAD_N0, TP_TREAD_N1, N.y); }

// Stripe coverage at world z; fzEff = z footprint per pixel (on risers, where z is constant, the caller
// passes the y footprint divided by the stair slope, so treads and risers filter alike).
float ThStepStripe(float z, float fzEff)
{
    float period = TC_TreadDepth * TP_STEP_STRIPE_EVERY;
    float centre0 = TC_StairStartZ + 0.5 * TC_TreadDepth;
    return ThPulseTrain(z - centre0, period, TP_STEP_STRIPE_W, fzEff);
}

// 0 = detailed treads and risers, 1 = the mean of tread and riser (one step under 1.5 px). Steps per pixel
// from the z footprint over the tread depth (treads) and the y footprint over the riser height (risers).
float ThStepFilter(float3 dpdx, float3 dpdy)
{
    float fz = abs(dpdx.z) + abs(dpdy.z);
    float fy = abs(dpdx.y) + abs(dpdy.y);
    float stepsPerPx = max(fz / TC_TreadDepth, fy / TC_RiserHeight);
    return smoothstep(0.5 / TP_STEP_FILTER_PX, 1.0 / TP_STEP_FILTER_PX, stepsPerPx);
}

// ------------------------------------------------------------------------------------------------
// M04 terrace mandala (14.2.3 line 463, 14.12 line 1015): SALT_WHITE rings at r = 60, 80, 100, 120 m
// (0.5 m wide) and 36 radial lines (0.3 m wide, r = 55 to 120 m) around the cable axis, at the strand angles
// at terrace height: theta_n = (n + 0.5) * 2 pi / 36 + 2 pi * TerraceY / pitch, theta = atan2(x - axisX,
// z - axisZ) (from +Z toward +X; the cable builder uses the same convention).
//
// Readability from altitude (critique run06 A2 / E4): from 200-600 m the 0.5 m rings and 0.3 m radials are under
// one pixel and the target vanishes. Each line family is drawn at least TP_MANDALA_MIN_PX filter widths wide
// (fwidth across the line: 2.0 px for a line along a screen axis, up to 2.8 px diagonally; never wider than half
// the line spacing) at full contrast while its spacing is resolved; when the spacing falls toward a few pixels the
// contrast blends to true / drawn width, so the far terrace converges on the exact area mean of the spec pattern
// (energy-preserving, no shimmer). The widening is phased in with camera distance (120-200 m), so the beat-4 walk
// across the terrace (grazing views, every line within ~150 m) keeps the spec widths.
// ------------------------------------------------------------------------------------------------
#define TP_MANDALA_RING0     60.0   // innermost ring radius (m), 14.12 line 1015
#define TP_MANDALA_RING_STEP 20.0   // ring spacing (m): 60, 80, 100, 120
#define TP_MANDALA_RINGS     4.0
#define TP_MANDALA_MIN_PX    2.0    // minimum drawn width in filter widths, 2.0-2.8 px (A2 / E4: "max(spec, ~2.5 px)")
#define TP_MANDALA_GROW0     120.0  // camera distance (m) where the widening starts ...
#define TP_MANDALA_GROW1     200.0  // ... and is complete (beat 5 views the target from 200-640 m), A2 / E4
#define TP_MANDALA_RES0      3.0    // line spacing (filter widths) at or below which the drawn line is energy-preserving
#define TP_MANDALA_RES1      8.0    // line spacing (filter widths) at or above which it keeps full contrast

// One family of mandala lines as a box-filtered pulse train: y = coordinate across the lines (m; a line centred on
// y = 0 and on every multiple of the period P), W = true width (m, W <= P / 2), fw = filter width (m across the
// lines per pixel), grow = 0..1 widening weight. Weight 0..1 of the line colour.
float TpMandalaLines(float y, float P, float W, float fw, float grow)
{
    float fwc = max(fw, 1e-6);
    // drawn width in [W, P/2]: ThPulseTrain needs pulses narrower than the period
    float drawn = max(W, min(grow * TP_MANDALA_MIN_PX * fwc, 0.5 * P));
    float contrast = lerp(W / drawn, 1.0, smoothstep(TP_MANDALA_RES0, TP_MANDALA_RES1, P / fwc));
    return ThPulseTrain(y, P, drawn, fwc) * contrast;
}

// Mandala weight 0..1 (the caller restricts it to upward faces).
float ThMandala(float3 positionWS, float3 dpdx, float3 dpdy)
{
    float2 q = positionWS.xz - float2(TC_CableAxisX, TC_CableAxisZ);
    float r = sqrt(max(dot(q, q), 1e-6));
    float2 radial = q / r;
    float2 tangential = float2(radial.y, -radial.x);
    float fr = abs(dot(radial, dpdx.xz)) + abs(dot(radial, dpdy.xz));          // fwidth(r)
    float ft = abs(dot(tangential, dpdx.xz)) + abs(dot(tangential, dpdy.xz));  // fwidth across a radial line
    float grow = smoothstep(TP_MANDALA_GROW0, TP_MANDALA_GROW1, distance(positionWS, _WorldSpaceCameraPos));

    // rings: a pulse train in r (lines at 60 + 20 k), kept to the four rings by a window whose edges lie midway
    // between rings (r = 50 and 130), so the cut never lands on a line
    float ringWin = ThBoxInterval(r, TP_MANDALA_RING0 - 0.5 * TP_MANDALA_RING_STEP,
                                  TP_MANDALA_RING0 + (TP_MANDALA_RINGS - 0.5) * TP_MANDALA_RING_STEP, fr);
    float ring = TpMandalaLines(r - TP_MANDALA_RING0, TP_MANDALA_RING_STEP, TC_MandalaRingWidth, fr, grow) * ringWin;

    // radial lines: u is integer on a line; across the atan2 seam u jumps by exactly 36 lines, so du is
    // continuous, and no derivative of theta is taken (ft comes from the tangential direction). Across the lines
    // the coordinate is the arc length du * r * delta (within 0.13 percent of the perpendicular distance), a
    // pulse train of period r * delta that is continuous where du wraps at +-0.5.
    float delta = TP_TWO_PI / TC_MandalaRadials;
    float theta0 = TP_TWO_PI * TC_TerraceY / TC_CablePitch;
    float u = (atan2(q.x, q.y) - theta0) / delta - 0.5;
    float du = u - round(u);
    float arc = r * delta;                                                       // line spacing (m) at this radius
    float span = ThBoxInterval(r, TC_MandalaRadialMin, TC_MandalaRadialMax, fr);
    float rad = TpMandalaLines(du * arc, arc, TC_MandalaRadialWidth, ft, grow) * span;

    return max(ring, rad);
}

// ------------------------------------------------------------------------------------------------
// M05 cable (14.2.3 line 464, 14.5.4 line 694, 14.12 line 1018). Lay lines: u = R theta - (2 pi R / pitch) y,
// phase = u / (2 pi R / 36), i.e. phase = 36 (theta / 2 pi - y / pitch); ridge crests at half-integer phase
// (crest n at theta_n(y) = (n + 0.5) 2 pi / 36 + 2 pi y / 600). The spec prints the period as 8.727 m and
// the rise as 0.5236; the exact values are used so the phase jumps by exactly 36 across the atan2 seam and the
// lines sit exactly on the geometric ridges. CORAL bands 10 m tall centred on every multiple of 50 m of world y.
// ------------------------------------------------------------------------------------------------
#define TP_LAY_DARKEN 0.10   // albedo darkening in the valleys, 14.2.3 line 464
#define TP_LAY_EDGE0  0.30   // smoothstep(0.30, 0.5, |frac(phase) - 0.5|)
#define TP_LAY_EDGE1  0.50
#define TP_LAY_FADE0  0.50   // faded by smoothstep(0.5, 0.2, fwidth(phase))
#define TP_LAY_FADE1  0.20

// Lay-line phase and its seam-free filter width (fwidth of the unwrapped angle, from the position derivatives:
// d theta = (qz dx - qx dz) / r^2).
float ThCableLayPhase(float3 positionWS, float3 dpdx, float3 dpdy, out float fwPhase)
{
    float2 q = positionWS.xz - float2(TC_CableAxisX, TC_CableAxisZ);
    float r2 = max(dot(q, q), 1e-4);
    float theta = atan2(q.x, q.y);
    float phase = TC_CableStrands * (theta / TP_TWO_PI - positionWS.y / TC_CablePitch);
    float dthx = (q.y * dpdx.x - q.x * dpdx.z) / r2;
    float dthy = (q.y * dpdy.x - q.x * dpdy.z) / r2;
    float dpx = TC_CableStrands * (dthx / TP_TWO_PI - dpdx.y / TC_CablePitch);
    float dpy = TC_CableStrands * (dthy / TP_TWO_PI - dpdy.y / TC_CablePitch);
    fwPhase = abs(dpx) + abs(dpy);
    return phase;
}

// Albedo darkening (0..0.10) of the lay-line term.
float ThCableLayDarken(float phase, float fwPhase)
{
    float v = abs(frac(phase) - 0.5);
    return TP_LAY_DARKEN * smoothstep(TP_LAY_EDGE0, TP_LAY_EDGE1, v) * smoothstep(TP_LAY_FADE0, TP_LAY_FADE1, fwPhase);
}

// CORAL band coverage at world y (fy = y footprint per pixel).
float ThCableBand(float y, float fy)
{
    return ThPulseTrainFadeOut(y, TC_CableBandSpacing, TC_CableBandHeight, fy);
}

// Human-scale marker plates (critique run07 A11): with nothing on the cable between the 0.04 m lay-line darkening and
// the 10 m bands, the 100 m drum read as pleated fabric. Small square plates (0.6 m) sit on every third ridge crest
// (12 per ring) at world y = 12.5 k, i.e. three per 50 m bay at 7.5 / 12.5 / 12.5 / 7.5 m from the bands (the slot at
// each band centre is left out). Each plate is upright: its centre follows the leaning crest, its edges are world y
// and the horizontal arc. Coverage is the exact box filter of the square; the plates fade out by their own pixel
// footprint (full above 2.9 px, gone below 1.7 px), so they never sparkle and the far cable keeps the plain M05 tones
// (their area mean is under 0.1 percent of the surface). The caller blends the albedo toward _ThirdColor (CREAM) and
// adds a small glow at night (TP_MARKER_GLOW x darkness): marker lights.
#define TP_MARKER_SIZE   0.6     // plate edge (m): the 0.5-2 m scale the cable lacked (A11)
#define TP_MARKER_DY     12.5    // row spacing (m): four slots per 50 m band period, the band-centre slot dropped
#define TP_MARKER_CRESTS 3.0     // on every third ridge crest
#define TP_MARKER_FOOT0  0.35    // footprint / size where the plates start to fade (2.9 px) ...
#define TP_MARKER_FOOT1  0.60    // ... and are gone (1.7 px)
#define TP_MARKER_GLOW   0.35    // night glow (x CREAM x darkness D): below the bloom threshold on the shade side

float ThCableMarker(float3 positionWS, float3 dpdx, float3 dpdy)
{
    float2 q = positionWS.xz - float2(TC_CableAxisX, TC_CableAxisZ);
    float r2 = max(dot(q, q), 1e-4);
    float r = sqrt(r2);
    float slot = round(positionWS.y / TP_MARKER_DY);
    float yk = slot * TP_MARKER_DY;
    float dy = positionWS.y - yk;
    // crest phase at the row height: phase jumps by exactly 36 (= 12 x 3) across the atan2 seam, so the marked crests
    // are continuous; the plate's arc coordinate is measured at fixed y (upright square, not sheared along the lay)
    float phaseK = TC_CableStrands * (atan2(q.x, q.y) / TP_TWO_PI - yk / TC_CablePitch);
    float m = (phaseK - 0.5) / TP_MARKER_CRESTS;
    float a = (m - round(m)) * TP_MARKER_CRESTS * (TP_TWO_PI * r / TC_CableStrands);   // m along the arc
    // filter widths from the continuous position derivatives (d theta = (qz dx - qx dz) / r^2; no seam)
    float dthx = (q.y * dpdx.x - q.x * dpdx.z) / r2;
    float dthy = (q.y * dpdy.x - q.x * dpdy.z) / r2;
    float fa = r * (abs(dthx) + abs(dthy));
    float fy = abs(dpdx.y) + abs(dpdy.y);
    float cov = ThBoxPulse(a, 0.5 * TP_MARKER_SIZE, fa) * ThBoxPulse(dy, 0.5 * TP_MARKER_SIZE, fy);
    float bandSlot = abs(slot - 4.0 * round(0.25 * slot)) < 0.5 ? 0.0 : 1.0;   // no plate at a band centre
    float foot = 1.0 - smoothstep(TP_MARKER_FOOT0, TP_MARKER_FOOT1, max(fa, fy) / TP_MARKER_SIZE);
    return cov * bandSlot * foot;
}

// ------------------------------------------------------------------------------------------------
// M06 deck (14.2.3 line 465): CREAM edge stripes where |lateral| lies in
// [DeckWidth/2 - chamfer - stripe, DeckWidth/2 - chamfer] (profile.x = lateral, metres from the centre line; profile.y =
// height h relative to the deck top, uv1 of the helix sweep and of the pier / pad slab).
// Edge trim (critique run07 B-r2-5): the stripe colour continues over the 2 cm top chamfer and down the outer side face
// to the lower-bevel crease (h >= -(thickness - bevel)), so the chamfer and side face, seen edge-on as a sub-pixel
// sliver, never draw a SAND_SHADE hairline beside the stripe (it crawled as dashes along the walkway edge). The colour
// edge on the side lies on the geometric crease to the bevel; the top interior, bevels, bottom and caps are unchanged.
// ------------------------------------------------------------------------------------------------
#define TP_DECK_BEVEL 0.25   // lower side bevel depth (m), 14.2.3 line 465 (WalkwayLayout.DeckBevel)

float ThDeckStripe(float2 profile, float2 fwProfile)
{
    float hi = 0.5 * TC_DeckWidth - TC_DeckChamfer;
    float lo = hi - TC_DeckEdgeStripe;
    float across = ThBoxInterval(abs(profile.x), lo, 0.5 * TC_DeckWidth + 1.0, fwProfile.x);    // stripe, chamfer, side
    float above = ThBoxInterval(profile.y, -(TC_DeckThickness - TP_DECK_BEVEL), 1.0, fwProfile.y); // above the bevel crease
    return across * above;
}

#endif // THREAD_PATTERNS_INCLUDED
