#ifndef ABYSS_COMMON_INCLUDED
#define ABYSS_COMMON_INCLUDED

// =====================================================================================================================
// AbyssCommon.hlsl - shared helpers for every Megastructure shader (MASTER_PROMPT 8.6, 14.2, 14.7.9, CONTRACTS 7/8).
//   * integer PCG hashes (never sin-based), always fed with integer cell indices taken modulo their period count
//   * static interleaved gradient noise and the _AbFade dithered tier clip (CONTRACTS section 7 convention)
//   * PatternPos: rotated object-space position + (object origin mod 1332 m) (DEVELOPMENT.md ADR 4)
//   * analytic, footprint-filtered pattern primitives (lines, grooves, discs) that fade to their mean under 1.5 px
//   * ThinLine vertex expansion to a minimum on-screen width (all passes)
// Requires "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" to be included first.
// Globals are written from C# (owners in Runtime/Rendering/ShaderIds.cs); they are NOT material properties.
// =====================================================================================================================

// ------------------------------------------------------------------------------------------------ globals
float  _AbPixelAngle;    // 2 tan(vfov/2) / targetHeightPx: world size of one pixel at distance 1 (owner CameraRig)
float4 _AbTime;          // (SceneClock mod 3600, RealTime mod 3600, 0, 0) (owner ClockDriver)
float4 _AbDitherParams;  // (amplitude LSB, temporal 0/1, frame index mod 64, enabled 0/1) (owner CaptureHarness/bootstrap)
float4 _AbLampIgnite;    // (start SceneClock time mod 3600 or -1, origin x, origin z, metres per second) (owner Beat07)
float  _AbBeamFade;      // 0..1 beam visibility (owner Beat06 / ProgressDirector)
float  _AbFanTheta;      // fan rotation in radians (wall patches) (owner Beat06)
float  _AbPictureFade;   // final picture fade 0..1 applied by FinalDither (owner FadeController / bootstrap)
float4 _AbDownView;      // downward-view attenuation of city emission (R1-01): x = weight 0..1 (0 = off; an unset global
                         // is 0), y = reference height (deck y) for the albedo darkening, z = darkening depth scale m
                         // (owner: lift / ProgressDirector; on while the bridge looks down into the void, off for the ride)

TEXTURE2D(_AbNoiseTex);  SAMPLER(sampler_AbNoiseTex);   // 256x256 tileable value noise, linear data (ProceduralTextures)
TEXTURE2D(_AbStreakTex); SAMPLER(sampler_AbStreakTex);  // 512x128 vertical water streaks, linear data, mean 0.5

// ------------------------------------------------------------------------------------------------ constants
#define AB_NOISE_WRAP      1332.0   // NOISE_WRAP: least common multiple of every pattern period (14.2)
#define AB_MIN_LINE_PX     1.5      // MIN_LINE_WIDTH (14.12)
#define AB_LAMP_RAMP_S     1.0      // per-lamp ignite ramp of the honeycomb lamps (14.5.7), seconds
#define AB_DEFAULT_PIXEL_ANGLE 0.0012 // 66 degrees at 1080p, used only while _AbPixelAngle is unset (edit mode)

// Rec. 709 luma of a linear colour.
float AbLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// NaN guard for normalisation: a zero-length (degenerate mesh normal, cancelled perturbation) vector returns the fallback
// instead of NaN, so one bad vertex can never poison lighting, fog or the depth-normals buffer.
float3 AbSafeNormalize(float3 v, float3 fallback)
{
    float l2 = dot(v, v);
    return l2 > 1e-12 ? v * rsqrt(l2) : fallback;
}

// Smoothstep that tolerates a == b (a step at a) instead of dividing by zero.
float AbSmoothstepSafe(float a, float b, float x)
{
    float t = saturate((x - a) / max(b - a, 1e-4));
    return t * t * (3.0 - 2.0 * t);
}

// Quintic smootherstep (C2), used for every eased blend in the spec.
float AbSmootherstep(float a, float b, float x)
{
    float t = saturate((x - a) / (b - a));
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// World size of one pixel at distance 1 (falls back to a sane constant before the CameraRig wrote it).
float AbPixelAngle() { return _AbPixelAngle > 0.0 ? _AbPixelAngle : AB_DEFAULT_PIXEL_ANGLE; }

// ------------------------------------------------------------------------------------------------ hashes (PCG, integer)
// PCG RXS-M-XS (Jarzynski & Olano, JCGT 2020; docs/research/art-technique.md section 5).
uint AbPcg(uint v)
{
    uint s = v * 747796405u + 2891336453u;
    uint w = ((s >> ((s >> 28u) + 4u)) ^ s) * 277803737u;
    return (w >> 22u) ^ w;
}

// pcg3d: 3 -> 3 hash.
uint3 AbPcg3d(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return v;
}

// Uniform float in [0, 1) from the top 24 bits.
float AbU01(uint h) { return (float)(h >> 8) * (1.0 / 16777216.0); }

// Non-negative integer modulo for |i| < 65536 n (HLSL int % truncates toward zero for negative operands and is slow;
// the offset keeps the operand positive so the fast unsigned modulus is exact). n must be in 1..32767.
int AbWrap(int i, int n)
{
    return (int)((uint)(i + 65536 * n) % (uint)n);
}

// Integer cell index of a coordinate for a cell size (floor, exact for negative values).
int AbCell(float x, float cellSize) { return (int)floor(x / cellSize); }

// Hash of one integer cell index wrapped to its period count n, with a salt per feature. Returns [0, 1).
float AbHash1(int c, int n, uint salt)
{
    return AbU01(AbPcg((uint)AbWrap(c, n) ^ AbPcg(salt)));
}

// Hash of a 2D integer cell (each component wrapped to its own period count). Returns [0, 1).
float AbHash2(int2 c, int2 n, uint salt)
{
    uint3 h = AbPcg3d(uint3((uint)AbWrap(c.x, n.x), (uint)AbWrap(c.y, n.y), salt));
    return AbU01(h.x);
}

// Hash of a 3D integer cell (each component wrapped to its own period count). Returns [0, 1).
float AbHash3(int3 c, int3 n, uint salt)
{
    uint3 h = AbPcg3d(uint3((uint)AbWrap(c.x, n.x), (uint)AbWrap(c.y, n.y), (uint)AbWrap(c.z, n.z)) ^ AbPcg(salt));
    return AbU01(h.x ^ h.y);
}

// Periodic 3D value noise (quintic fade) on a lattice of 'cell' metres with n cells per wrap (cell * n == 1332).
// Returns [0, 1). Exactly periodic with AB_NOISE_WRAP so pattern coordinates may wrap without a seam.
float AbValueNoise3(float3 p, float cell, int n, uint salt)
{
    float3 x = p / cell;
    float3 i = floor(x);
    float3 f = x - i;
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    int3 c = (int3)i;
    int3 nn = int3(n, n, n);
    float a = AbHash3(c + int3(0, 0, 0), nn, salt);
    float b = AbHash3(c + int3(1, 0, 0), nn, salt);
    float d = AbHash3(c + int3(0, 1, 0), nn, salt);
    float e = AbHash3(c + int3(1, 1, 0), nn, salt);
    float g = AbHash3(c + int3(0, 0, 1), nn, salt);
    float h = AbHash3(c + int3(1, 0, 1), nn, salt);
    float k = AbHash3(c + int3(0, 1, 1), nn, salt);
    float l = AbHash3(c + int3(1, 1, 1), nn, salt);
    float x0 = lerp(lerp(a, b, u.x), lerp(d, e, u.x), u.y);
    float x1 = lerp(lerp(g, h, u.x), lerp(k, l, u.x), u.y);
    return lerp(x0, x1, u.z);
}

// Value noise with a per-axis cell count per wrap (anisotropic lattices, e.g. vertical grime runs: p.y pre-scaled by 1/4
// needs n.y = n.x / 4 so the pattern stays periodic with the 1332 m wrap on every axis).
float AbValueNoise3N(float3 p, float cell, int3 nn, uint salt)
{
    float3 x = p / cell;
    float3 i = floor(x);
    float3 f = x - i;
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    int3 c = (int3)i;
    float a = AbHash3(c + int3(0, 0, 0), nn, salt);
    float b = AbHash3(c + int3(1, 0, 0), nn, salt);
    float d = AbHash3(c + int3(0, 1, 0), nn, salt);
    float e = AbHash3(c + int3(1, 1, 0), nn, salt);
    float g = AbHash3(c + int3(0, 0, 1), nn, salt);
    float h = AbHash3(c + int3(1, 0, 1), nn, salt);
    float k = AbHash3(c + int3(0, 1, 1), nn, salt);
    float l = AbHash3(c + int3(1, 1, 1), nn, salt);
    float x0 = lerp(lerp(a, b, u.x), lerp(d, e, u.x), u.y);
    float x1 = lerp(lerp(g, h, u.x), lerp(k, l, u.x), u.y);
    return lerp(x0, x1, u.z);
}

// ------------------------------------------------------------------------------------------------ dither / tier fades
// Interleaved gradient noise (Jimenez 2014) of an integer pixel position, [0, 1).
float AbIGN(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

// Static fade noise of a fragment: IGN of the integer pixel, quantised to the open interval (0, 1)
// (values (k + 0.5) / 256) so that f = 1 and f = -1 keep every pixel and f = 0 keeps none.
float AbFadeNoise(float2 positionCSxy)
{
    float n = AbIGN(floor(positionCSxy));
    return (floor(n * 256.0) + 0.5) * (1.0 / 256.0);
}

// _AbFade convention (CONTRACTS 7): f >= 0 keeps the pixel where noise < f; f < 0 keeps it where noise > 1 + f.
// near = c and far = -(1 - c) are exact complements. Used identically in colour and depth passes.
bool AbFadeKeep(float f, float2 positionCSxy)
{
    float n = AbFadeNoise(positionCSxy);
    return (f >= 0.0) ? (n < f) : (n > 1.0 + f);
}

void AbFadeClip(float f, float2 positionCSxy)
{
    if (f >= 1.0) return;
    clip(AbFadeKeep(f, positionCSxy) ? 1.0 : -1.0);
}

// Per-pixel distance cross-fade between two tiers of one structure (honeycomb tier 1 / tier 2, 5.4-6.6 km).
// distFade = (start m, end m, mode, 0): mode +1 = far tier (shown beyond), mode -1 = near tier (shown before), 0 = off.
// s = smoothstep(start, end, horizontal camera distance); near uses f = 1 - s, far uses f = -s (exact complements).
void AbDistFadeClip(float4 distFade, float3 positionWS, float2 positionCSxy)
{
    if (abs(distFade.z) < 0.5) return;
    float r = length(positionWS.xz - _WorldSpaceCameraPos.xz);
    float s = AbSmoothstepSafe(distFade.x, distFade.y, r);   // start == end: a hard (still dithered-complementary) switch
    float f = distFade.z > 0.0 ? -s : 1.0 - s;
    if (f >= 1.0) return;
    clip(AbFadeKeep(f, positionCSxy) ? 1.0 : -1.0);
}

// ------------------------------------------------------------------------------------------------ pattern coordinates
// Object origin reduced modulo 1332 m. 1332 is a multiple of every pattern period (0.2, 1.0, 1.2, 2.4, 3, 9, 12, 36, 37 m),
// so PatternPos = world position - 1332 k (integer vector k): patterns are continuous across chunks and never jump.
float3 AbObjectOriginWrapped()
{
    float3 o = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    return o - AB_NOISE_WRAP * floor(o / AB_NOISE_WRAP);
}

// Object-space position rotated (and scaled) into world orientation, without the translation. Small numbers, precise
// derivatives: use it for footprints (fwidth) and add AbObjectOriginWrapped() for pattern evaluation.
float3 AbRotatedLocal(float3 positionOS) { return mul((float3x3)UNITY_MATRIX_M, positionOS); }

// PatternPos(posOS) = mul((float3x3)UNITY_MATRIX_M, posOS) + (object origin mod 1332).
float3 AbPatternPos(float3 positionOS) { return AbRotatedLocal(positionOS) + AbObjectOriginWrapped(); }

// ------------------------------------------------------------------------------------------------ filtered patterns
// Fade weight of a feature of the given on-screen size in pixels: 0 (mean tone) under 1.5 px, 1 (full detail) over 3 px.
float AbFeatureFade(float sizePx) { return smoothstep(AB_MIN_LINE_PX, 2.0 * AB_MIN_LINE_PX, sizePx); }

// Exact box-filtered coverage of lines of width w centred on multiples of period p, for a pixel footprint fw
// (all in the same units). Integral of the pulse train: F(y) = floor(y/p) w + min(y - floor(y/p) p, w).
float AbLineBox(float x, float p, float w, float fw)
{
    fw = max(fw, 1e-6);
    float a = x + 0.5 * w - 0.5 * fw;
    float b = a + fw;
    float ia = floor(a / p);
    float ib = floor(b / p);
    float fa = min(a - ia * p, w);
    float fb = min(b - ib * p, w);
    return saturate(((ib - ia) * w + fb - fa) / fw);
}

// Box-filtered line coverage that fades to its mean (w / p) when the line is under 1.5 px wide (14.2 rule).
float AbLineCoverage(float x, float p, float w, float fw)
{
    float k = AbFeatureFade(w / max(fw, 1e-6));
    return lerp(w / p, AbLineBox(x, p, w, fw), k);
}

// Rounded groove height profile (<= 0, depth d at the centre, 0 at the edges) of width w on multiples of period p.
float AbGrooveH(float x, float p, float w, float d)
{
    float t = (x - p * round(x / p)) / (0.5 * w);
    return -d * saturate(1.0 - t * t);
}

// Filtered disc coverage: distance dist to a disc of radius r, footprint fw; fades to the area mean meanCov under 1.5 px.
float AbDiscCoverage(float dist, float r, float fw, float meanCov)
{
    fw = max(fw, 1e-6);
    float c = saturate((r - dist) / fw + 0.5);
    return lerp(meanCov, c, AbFeatureFade(2.0 * r / fw));
}

// Box-filter blend weights of a piecewise-constant 1D cell function: x in cell units, footprint fwc in cell units.
// Returns (weight of previous cell, weight of next cell); the current cell keeps 1 - prev - next.
float2 AbCellBlend(float x, float fwc)
{
    float f = frac(x);
    float h = 0.5 * max(fwc, 1e-6);
    float prev = saturate((h - f) / (2.0 * h));
    float next = saturate((f + h - 1.0) / (2.0 * h));
    return float2(prev, next);
}

// Footprint (fwidth) of a linear coordinate c = dot(Q, axis) from the screen derivatives of Q.
float AbFootprint(float3 dQdx, float3 dQdy, float3 axis) { return abs(dot(dQdx, axis)) + abs(dot(dQdy, axis)); }

// Fade weight with an explicit pixel range (0 under px0, 1 over px1). Board-scale tones use 2.5-5 px: a per-row random
// tone at 1.5-3 px rows reads as horizontal "brushed metal" hatching (R1-08 / R1-09), so it fades out earlier.
float AbFeatureFadeRange(float sizePx, float px0, float px1) { return smoothstep(px0, px1, sizePx); }

// One-sided exponential stain f(t) = exp(-t / L) for 0 <= t < P (0 above the joint), periodic with period P >> L:
// exact box filter over [t - h, t + h] from the antiderivative F(x) = L (1 - exp(-x / L)) (x >= 0). The image of the
// next period is added where the box crosses t = P (just below the next joint), so the band never aliases.
float AbExpStainF(float x, float L) { return x > 0.0 ? L * (1.0 - exp(-x / L)) : 0.0; }
float AbExpStainBox(float t, float L, float P, float h)
{
    h = max(h, 1e-5);
    float a = t - h, b = t + h;
    float Fb = AbExpStainF(min(b, P), L) + AbExpStainF(b - P, L);
    float Fa = AbExpStainF(min(a, P), L) + AbExpStainF(a - P, L);
    return saturate((Fb - Fa) / (2.0 * h));
}

// Line of true width w on multiples of p, drawn at least minPx wide (box-filtered, never sub-pixel) with its contrast
// scaled by sqrt(w / drawn): large-scale joints (12 m expansion joints, 3 m pour lifts) stay readable as soft 1.5-2 px
// lines on a 70-500 m wall instead of vanishing into the mean ("graph-paper cliff", R1-09). Returns the coverage of the
// drawn line; contrastK receives the contrast scale. When the period itself drops under 4 px the line fades out.
float AbLineMinWidth(float x, float p, float w, float fw, float minPx, out float contrastK)
{
    fw = max(fw, 1e-6);
    float drawn = max(w, minPx * fw);
    contrastK = sqrt(w / drawn) * smoothstep(4.0, 8.0, p / fw);
    return AbLineBox(x, p, drawn, fw);
}

// Uniform [0, 1] from two independent uniform samples: max(a, b)^2 is uniform (P(max(a, b) <= x) = x^2). Combining two
// lookups of an equalised NoiseTex channel at incommensurate tiles (7.4 m and 4.5 m: common period 333 m) keeps the
// coverage-exact threshold of AbRustMask while removing the visible 3 m repetition of stains and rust (R1-27, R1-33).
float AbUniformMax2(float a, float b) { float m = max(a, b); return m * m; }

// ------------------------------------------------------------------------------------------------ rust (14.2 RUST)
// RUST is "worn steel, at most 12 % of a surface set" and must stay subtle on the dark painted steel (STEEL_DARK).
// Mask: a soft threshold on ONE histogram-equalised NoiseTex channel (uniform on [0, 1]), band [t - a/2, t + a/2] with
// t = 1 - a. The band is symmetric and stays inside [0, 1] for a <= 0.12, so the area where mask > 0.5 is exactly a and
// E[mask] = a: coverage is exact by construction (the earlier 0.75 n3 + 0.25 n1 sum was not uniform and is gone). A second
// independent channel only modulates the density inside a patch (x 0.55..1.0, mean 0.775), never the coverage. When the
// patches (0.3-1 m) shrink under a few pixels the mask blends to its mean (mip filtering would otherwise remove them).
// Colour: RUST #5B3A27 reduced to AB_RUST_CHROMA of its chroma at AB_RUST_LUMA x the paint luma (a warm, rough tint of
// about the paint's own value, not an orange blotch); the variation input scales it by about +/-15 %.
#define AB_RUST_MAX     0.12     // 14.2 cap: at most 12 % of a surface set
#define AB_RUST_CHROMA  0.62     // fraction of RUST's chroma kept (R1-27: 0.40 left "no RUST hue anywhere")
#define AB_RUST_LUMA    0.95     // rust albedo luma / paint albedo luma
#define AB_RUST_EDGE    0.08     // edge bias of the patch value (vertex G): rust gathers on worn edges
static const float3 AB_RUST_LIN = float3(0.104616, 0.042311, 0.020289);   // RUST #5B3A27 (linear)

// Rust weight 0..1. coarse = equalised channel (uniform) that defines the patches, fine = independent equalised channel,
// footprintM = pixel footprint on the surface in metres. The band is [t - a, t + a] (the widest symmetric band inside
// [0, 1]): soft gradients instead of decal-like edges, still exactly 'amount' of the surface above 0.5.
float AbRustMask(float amount, float coarse, float fine, float footprintM)
{
    float a = clamp(amount, 0.0, AB_RUST_MAX);
    if (a <= 0.0) return 0.0;
    float t = 1.0 - a;
    float m = AbSmoothstepSafe(t - a, t + a, coarse) * (0.55 + 0.45 * saturate(fine));
    // R2-10: patches (0.5-3 m) blend to their mean once they are under about 10 px (3-15 cm/px): at 150-250 m (KS5b, KS7d
    // blades) the half-resolved patches read as a pock-mark speckle
    float far = smoothstep(0.03, 0.15, footprintM);
    return lerp(m, 0.775 * a, far);
}

// Dark-neutral stain colour for walking surfaces (R1-27: oil stains must never read red): the paint darkened, no hue.
// R2-10: at most 10-14 % darker (the 18-34 % of R1 read as ink blots, paw prints and puddles on the sunlit gantry deck).
float3 AbStainColor(float3 paint, float variation) { return paint * (0.86 + 0.06 * saturate(variation)); }

// ------------------------------------------------------------------------------------------------ stochastic lookups (R2-10)
// A single tiled lookup repeats its shapes with the tile period: the stains of R1 (max of a 7.4 m and a 4.5 m lookup)
// repeated the same paired "dog-bone" blot every 4.5-7.4 m along the gantry deck, because every patch that comes from one
// lookup recurs with that lookup's own period. AbBombG samples an equalised NoiseTex channel on a horizontal plane with a
// random offset and a random 90-degree rotation / mirror per cell and blends the 2 x 2 nearest cells with smooth weights by
// histogram-preserving blending (Heitz and Neyret 2018): each uniform value is mapped to a standard normal (AbPhiInv), the
// normals are blended with the variance kept (sum w z / sqrt(sum w^2)), and the result is mapped back (AbPhi). The output is
// uniform again, so the coverage of a threshold stays exact (CPU check: 2.8 % for 3 %), while no shape repeats. cell must
// divide 1332 (n = 1332 / cell) and tile too: the result is then periodic with the 1332 m pattern wrap.
// Standard normal CDF and its inverse (Abramowitz-Stegun 26.2.23, |error| < 4.5e-4).
float AbErf1(float x)
{
    float x2 = x * x;
    float k = x2 * (1.2732395 + 0.147 * x2) / (1.0 + 0.147 * x2);
    return sign(x) * sqrt(saturate(1.0 - exp(-k)));
}
float AbPhi(float z) { return 0.5 + 0.5 * AbErf1(z * 0.70710678); }
float AbPhiInv(float p)
{
    p = clamp(p, 1e-5, 1.0 - 1e-5);
    float q = min(p, 1.0 - p);
    float t = sqrt(-2.0 * log(q));
    float z = t - (2.515517 + 0.802853 * t + 0.010328 * t * t) / (1.0 + 1.432788 * t + 0.189269 * t * t + 0.001308 * t * t * t);
    return p > 0.5 ? z : -z;
}
float AbBombG(float2 xz, float2 dxz, float2 dyz, float tile, float cell, int n, uint salt)
{
    float2 c = xz / cell - 0.5;
    float2 i0 = floor(c);
    float2 f = c - i0;
    float2 w = f * f * (3.0 - 2.0 * f);
    float acc = 0.0;
    float w2 = 0.0;
    float2 uv0 = xz / tile, ddx0 = dxz / tile, ddy0 = dyz / tile;
    [unroll] for (int j = 0; j < 4; j++)
    {
        int2 o = int2(j & 1, j >> 1);
        float wj = (o.x == 1 ? w.x : 1.0 - w.x) * (o.y == 1 ? w.y : 1.0 - w.y);
        int2 ci = (int2)i0 + o;
        uint3 h = AbPcg3d(uint3((uint)AbWrap(ci.x, n), (uint)AbWrap(ci.y, n), salt));
        float2 uv = uv0, gx = ddx0, gy = ddy0;
        if ((h.z & 1u) != 0u) { uv = uv.yx; gx = gx.yx; gy = gy.yx; }
        float2 s = float2((h.z & 2u) != 0u ? -1.0 : 1.0, (h.z & 4u) != 0u ? -1.0 : 1.0);
        uv = uv * s + float2(AbU01(h.x), AbU01(h.y));
        float g = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, uv, gx * s, gy * s).g;
        acc += wj * AbPhiInv(g);
        w2 += wj * wj;
    }
    return AbPhi(acc * rsqrt(max(w2, 1e-4)));
}

// ------------------------------------------------------------------------------------------------ display-safe emitters (R2-21)
// URP's Neutral tonemapper compresses each channel separately toward 1, so an AMBER (linear 1, 0.456, 0.102) emitter driven
// to 3-8 displays cream-white (f03 KS7b dot cores sRGB 221, 198, 171, saturation 0.22 against AMBER's 0.65). The lead's D4:
// small and distant amber emitters stay amber. AbHueSafe pre-compensates the tonemapper exactly: the emitter's peak channel
// is tonemapped (capped at 'cap' display-linear), every channel is scaled to the emitter's own ratios and mapped back
// through the exact inverse of the Neutral curve, so after the (EV 0) tonemap the emitter shows its own hue and saturation
// at the peak brightness the curve allows (AMBER x 3 -> sRGB 243, 172, 85: hue 33 deg, saturation 0.65). Below about 0.3 the
// curve is near the identity and the mapping changes nothing. The post exposure of a beat (EV -0.6..0) only darkens the
// result slightly; its hue holds.
#define AB_NEUTRAL_WS   1.3133861   // 1 / NeutralCurve(white level 5.3)
#define AB_NEUTRAL_EF   0.0666667   // e / f
float AbNeutralCurve1(float x) { return ((x * (0.2 * x + 0.0696) + 0.00544) / (x * (0.2 * x + 0.29) + 0.0816)) - AB_NEUTRAL_EF; }
float AbNeutral1(float x) { return AbNeutralCurve1(max(x, 0.0) * AB_NEUTRAL_WS) * AB_NEUTRAL_WS; }
// Exact inverse of AbNeutral1 per channel (the root of a(t-1)x^2 + b(t-c)x + d(tf-e) = 0 with t = y/ws + e/f < 1).
float3 AbNeutralInverse(float3 y)
{
    float3 t = min(max(y, 0.0), 1.2) / AB_NEUTRAL_WS + AB_NEUTRAL_EF;
    float3 A = 0.2 * (t - 1.0);
    float3 B = 0.29 * (t - 0.24);
    float3 C = 0.272 * (0.3 * t - 0.02);
    float3 disc = max(B * B - 4.0 * A * C, 0.0);
    float3 x = (-B - sqrt(disc)) / min(2.0 * A, -1e-5);
    return max(x, 0.0) / AB_NEUTRAL_WS;
}
// The beat grades (ColorAdjustments saturation -14..-24, WhiteBalance temperature -4..-12) desaturate and cool every
// emitter before the tonemapper (shaders_r32 KS7b dot cores (216, 152, 104): hue 26 for an exact AMBER disc), so the display
// target's chroma around its luma is raised by AB_GRADE_CHROMA (AMBER -> (1, 0.42, 0.05): sRGB hue 34.6 ungraded).
#define AB_GRADE_CHROMA 1.12
float3 AbHueSafe(float3 c, float cap)
{
    float m = max(c.r, max(c.g, c.b));
    if (m <= 1e-5) return c;
    float p = min(AbNeutral1(m), cap);
    float3 t = c * (p / m);
    float l = dot(t, float3(0.2126, 0.7152, 0.0722));
    t = max(l + (t - l) * AB_GRADE_CHROMA, 0.0);
    t *= p / max(max(t.r, max(t.g, t.b)), 1e-5);          // the peak stays at p
    return AbNeutralInverse(t);
}
// Warm emitters only (amber, sodium): 1 for AMBER's chroma ((r - b) / r = 0.898 -> 1.24, saturated), 0 for neutral, white
// and cold colours (cold lamps keep their look); warm-white (r - b) / r = 0.4 is the zero point.
float AbWarmth(float3 c) { return saturate(2.5 * (c.r - c.b) / max(c.r, 1e-5) - 1.0); }
#define AB_AMBER_CAP 0.92           // display-linear peak of a hue-safe amber emitter (sRGB about 246)
float3 AbWarmEmission(float3 c) { return lerp(c, AbHueSafe(c, AB_AMBER_CAP), AbWarmth(c)); }

// erf (Abramowitz-Stegun style closed form, |error| < 1.3e-4), for pixel-integrated Gaussian light points.
float2 AbErf2(float2 x)
{
    float2 x2 = x * x;
    float2 k = x2 * (1.2732395 + 0.147 * x2) / (1.0 + 0.147 * x2);
    return sign(x) * sqrt(saturate(1.0 - exp(-k)));
}

// ------------------------------------------------------------------------------------------------ geometric specular AA (R2-17)
// Hot 1-px hairlines (a lattice chord, the fan blades' rounded leading edge, rails seen against the bright haze): where the
// geometric normal sweeps quickly across a pixel (rounded edges, thin round members, silhouettes), the GGX lobe and, above
// all, the Fresnel environment term (EnvironmentBRDF's grazing term = smoothness + reflectivity) light a 1-px line that
// crawls. Widen the perceptual roughness by the screen-space variance of the normal (Kaplanyan / Tokuyoshi) with a much
// higher limit than the shared kernel (alpha^2 up to 0.6): flat faces (variance 0) and large curved walls are untouched.
float AbGeometricSpecularAA(float perceptualRoughness, float3 dNdx, float3 dNdy)
{
    float variance = dot(dNdx, dNdx) + dot(dNdy, dNdy);
    float a = perceptualRoughness * perceptualRoughness;
    float a2 = saturate(a * a + min(2.0 * variance, 0.6));
    return sqrt(sqrt(a2));
}

// Sub-pixel ThinLine members (R2-17): a member widened to its minimum on-screen width (coverage = true / drawn < 1) is a
// stand-in for something thinner than a pixel. Its shading must be the member's average, not the grazing rim of the
// widened shell (Fresnel rims on the expanded silhouette made 1-px white chords). Bend the shading normal toward the
// viewer and make it matte as the coverage falls.
void AbThinLineShading(float coverage, float3 V, inout float3 normalWS, inout float roughness)
{
    float k = saturate((1.0 - coverage) * 1.5);
    if (k <= 0.0) return;
    normalWS = AbSafeNormalize(lerp(normalWS, V, 0.85 * k), normalWS);
    roughness = lerp(roughness, max(roughness, 0.8), k);
}

// Subtle rust albedo matched to the paint it replaces. variation in [0, 1] (an equalised noise channel).
float3 AbRustColor(float3 paint, float variation)
{
    float3 hue = AB_RUST_LIN / AbLuma(AB_RUST_LIN);                     // luma 1
    float3 tint = lerp(float3(1.0, 1.0, 1.0), hue, AB_RUST_CHROMA);
    return tint * (AbLuma(paint) * AB_RUST_LUMA * (0.85 + 0.3 * saturate(variation)));
}

// ------------------------------------------------------------------------------------------------ ThinLine (14.7.9)
// Widen thin members (rails, cables, truss members, lamp posts) to at least minPx on screen. The vertex is pushed
// radially away from the member axis by e = max(0, rMin - r), rMin = 0.5 * minPx * distance * pixelAngle, so the
// silhouette never drops under minPx; 'coverage' = r / (r + e) is exported as alpha-to-coverage in the colour pass.
// Mesh data (MeshBuilder convention): UV1.xyz = member axis direction (object space), UV1.w = member radius (object).
// Optional UV2 = (point on the member axis in object space, 1): crack-free expansion for split-normal sections (boxes).
// Without UV2 the push direction is the normal minus its axial component (crack-free for smooth-normal lathes).
// Distances use _WorldSpaceCameraPos, which URP keeps at the camera position in the shadow-caster pass as well.
float3 AbThinLineExpand(float3 positionWS, float3 normalWS, float4 uv1, float4 uv2, float minPx, out float coverage)
{
    coverage = 1.0;
    if (minPx <= 0.0) return positionWS;
    if (uv1.w <= 0.0) return positionWS;   // vertices without member data (decks, plates, bolts) are never inflated
    float dist = max(length(positionWS - _WorldSpaceCameraPos), 1e-3);
    float rMin = 0.5 * minPx * dist * AbPixelAngle();
    float3 axisWS = mul((float3x3)UNITY_MATRIX_M, uv1.xyz);
    float axisLen = length(axisWS);
    float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
    float r = max(uv1.w, 0.0) * scale;
    float e = max(rMin - r, 0.0);
    float3 axis = axisLen > 1e-6 ? axisWS / axisLen : float3(0.0, 0.0, 0.0);
    float3 dir;
    if (uv2.w > 0.5)
    {
        float3 axisPoint = mul(UNITY_MATRIX_M, float4(uv2.xyz, 1.0)).xyz;
        dir = positionWS - axisPoint;
    }
    else
    {
        dir = normalWS;
    }
    dir -= axis * dot(dir, axis);
    float dl = length(dir);
    dir = dl > 1e-6 ? dir / dl : float3(0.0, 0.0, 0.0);
    coverage = r > 0.0 ? saturate(r / (r + e)) : 1.0;
    return positionWS + dir * e;
}

// Alpha exported by opaque passes with AlphaToMask: the ThinLine coverage when alpha-to-coverage is available (MSAA),
// otherwise 1 (the widened member stays opaque; never dither a thin line without MSAA).
float AbCoverageAlpha(float coverage) { return (_AlphaToMaskAvailable != 0.0) ? coverage : 1.0; }

// ------------------------------------------------------------------------------------------------ downward view (R1-01)
// From the bridge the void below must read as void: city emission seen within 50 degrees of the nadir is switched off
// (and the city concrete darkens toward FOG_DOWN with depth below the deck), while views toward the horizon keep the faint
// far lights. Opt-in per material (optIn = _DownViewAtten, 1 on M_Facade / M_FacadeModule), driven by the global weight
// _AbDownView.x so the lift ride (weight 0) still shows the full city. down = sin(angle below the horizon):
// 1 above 20 degrees below the horizon, 0.0025 (a faint far-tier dot at most) from 38 degrees down (50 degrees of the
// nadir = 40 degrees down).
#define AB_DOWNVIEW_DOWN0   0.342    // sin 20 deg
#define AB_DOWNVIEW_DOWN1   0.62     // sin 38.3 deg: fully off before 50 deg from the nadir (sin 40 deg = 0.643)
#define AB_DOWNVIEW_FLOOR   0.0025
float AbDownViewEmission(float3 positionWS, float optIn, out float albedoK)
{
    albedoK = 1.0;
    float w = saturate(_AbDownView.x) * saturate(optIn);
    if (w <= 0.0) return 1.0;
    float3 toFrag = positionWS - _WorldSpaceCameraPos;
    float len = max(length(toFrag), 1e-4);
    float down = saturate(-toFrag.y / len);
    float e = lerp(1.0, AB_DOWNVIEW_FLOOR, smoothstep(AB_DOWNVIEW_DOWN0, AB_DOWNVIEW_DOWN1, down));
    float depth = max(_AbDownView.y - positionWS.y, 0.0);
    albedoK = lerp(1.0, exp(-depth / max(_AbDownView.z, 1.0)), w);
    return lerp(1.0, e, w);
}

// ------------------------------------------------------------------------------------------------ sub-pixel bevels (R1-26)
// MeshKit.ChamferBox marks its chamfer and corner facets with edge wear G = 255. A 1-3 cm chamfer under 1.5 px is
// sampled by MSAA as dashes, and its specular / bare-steel wear highlight crawls ("marching ants" on deck joints and rib
// bevels). Fade it by its projected width: bevelPx = width * sqrt2 * max(|N.V|, 0.25) / (distance * pixel angle).
// Returns k (1 = resolved, 0 = sub-pixel). For 45-degree chamfers of boxes that are axis-aligned in object space,
// faceN receives the normal of the adjoining face that looks most toward the camera (the shade the bevel merges into);
// otherwise faceN = N.
float AbBevelResolve(float3 N, float3 positionWS, float edgeG, float bevelWidth, out float3 faceN)
{
    faceN = N;
    float3 toCam = _WorldSpaceCameraPos - positionWS;
    float dist = max(length(toCam), 1e-3);
    float3 V = toCam / dist;
    float px = bevelWidth * 1.41421356 * max(abs(dot(N, V)), 0.25) / (dist * AbPixelAngle());
    float k = smoothstep(1.0, 2.5, px);
    float onBevel = smoothstep(0.85, 0.97, edgeG);
    if (onBevel <= 0.0 || k >= 1.0) return lerp(1.0, k, onBevel);
    float3x3 M = (float3x3)UNITY_MATRIX_M;
    float3 nOS = AbSafeNormalize(mul(N, M), N);          // inverse rotation (rotation x uniform scale)
    float3 vOS = AbSafeNormalize(mul(V, M), V);
    float3 a = abs(nOS);
    float minA = min(a.x, min(a.y, a.z));
    float maxA = max(a.x, max(a.y, a.z));
    if (minA < 0.15 && maxA < 0.82 && maxA > 0.6)          // two components ~0.707: a 45-degree chamfer
    {
        float3 s = sign(nOS);
        float3 cand = float3(a.x > 0.5 ? 1.0 : 0.0, a.y > 0.5 ? 1.0 : 0.0, a.z > 0.5 ? 1.0 : 0.0) * s;
        float3 c1 = cand.x != 0.0 ? float3(cand.x, 0.0, 0.0) : float3(0.0, cand.y, 0.0);
        float3 c2 = cand.z != 0.0 ? float3(0.0, 0.0, cand.z) : float3(0.0, cand.y, 0.0);
        float3 pick = dot(c1, vOS) >= dot(c2, vOS) ? c1 : c2;
        float3 fN = AbSafeNormalize(mul(M, pick), N);
        faceN = AbSafeNormalize(lerp(fN, N, k), N);
    }
    return lerp(1.0, k, onBevel);
}

// ------------------------------------------------------------------------------------------------ honeycomb lamps
// Ignite factor of a honeycomb lamp at world xz (14.5.7): lamps light from nearest to farthest at _AbLampIgnite.w m/s
// from the origin (_AbLampIgnite.yz), each with a smoothstep ramp of AB_LAMP_RAMP_S. Start < 0: lamps are off.
// The start time and _AbTime.x share the 3600 s wrap; a wrapped difference is unwrapped once.
float AbLampIgnite(float2 lampXZ)
{
    float start = _AbLampIgnite.x;
    if (start < 0.0) return 0.0;
    float elapsed = _AbTime.x - start;
    if (elapsed < -1800.0) elapsed += 3600.0;
    float delay = length(lampXZ - _AbLampIgnite.yz) / max(_AbLampIgnite.w, 1e-3);
    return smoothstep(0.0, AB_LAMP_RAMP_S, elapsed - delay);
}

#endif // ABYSS_COMMON_INCLUDED
