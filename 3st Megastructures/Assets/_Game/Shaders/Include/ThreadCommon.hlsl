// ThreadCommon.hlsl - the shared contract of every Thread shader (MASTER_PROMPT 14.2, 14.7).
// Owner: lead. Contains: global uniforms (set every frame by ProgressDirector, outside any
// UnityPerMaterial CBUFFER so SRP Batcher compatibility is unaffected), the sky gradient
// ThreadSkyColor, the one shared fog function ThreadFogApply (distance + bank + deck terms),
// the analytic cable shadow, the flat-with-a-rim shading recipe, hashes without sin.
// Every float3/float4 literal outside palette macros carries the marker  pal:ok  (T-PAL, 14.7.8).
#ifndef THREAD_COMMON_INCLUDED
#define THREAD_COMMON_INCLUDED

#include "../Generated/ThreadPalette.hlsl"
#include "../Generated/ThreadConstantsGen.hlsl"

// ------------------------------------------------------------------------------------------------
// Global uniforms (ShaderIds.cs mirrors these names).
// ------------------------------------------------------------------------------------------------
float4 _ThSunDir;      // xyz unit vector toward the sun, w = sun-disc alpha
float4 _ThSunColor;    // rgb = linear sun colour * intensity, w = intensity
float4 _ThAmbUp;       // rgb ambient from above
float4 _ThAmbSide;     // rgb ambient from the side
float4 _ThAmbDown;     // rgb ambient from below
float4 _ThSky;         // x = D (darkness 0..1), y = A_virt in km, z = rho at camera altitude, w = star alpha
float4 _ThFog;         // x = k (distance fog 3.912 / V, 0 = off), y = H_f (m), z = A_virt (m), w = fog colour cap (0.97 / exposure gain)
float4 _ThBank;        // x = bank on (0/1), y = x_c drift (m), z = ground-group y offset (-A_virt, m), w = density scale
float4 _ThDeck;        // x = gate 0..1, y = deck top world y, z = deck bottom world y, w = density (1/m)
float4 _ThCable;       // x = cable shadow on (0/1), y = sun_h.x, z = sun_h.z (horizontal unit sun dir), w = unused
float4 _ThThread;      // x = x_T, y = z_T (apparent far-thread axis, world), z = halfE (true half width as a sine), w = alpha (0 after swap)
float4 _ThThread2;     // x = haze (<= 0.74), y = lit-side sign, zw = unused
float4 _ThPlanet;      // x = alpha, y = axis tilt (rad), z = band phase (cycles), w = glint 0..1
float4 _ThPlanetDir;   // xyz = unit direction to the planet centre
float4 _ThEarth;       // x = cos(psi_limb) of the cloud shell, y = h = A - 3 km (m), z = cloud coverage, w = eps0 (rad)
float4 _ThSea;         // x = sea path on (0/1), y = scroll x (m), z = scroll z (m), w = earth cloud drift (rad)
float4 _ThTime;        // x = ambient time (s, wrapped at 3600), y = robe sway phase (rad, 11 s period), z = 0, w = 0
float4 _ThBeacon;      // x = crystal emissive ramp 0..1 (s 700-780), y = halo ramp 0..1 (s 730-790), z = pulse factor (1 +- 0.06), w = 0
float4 _ThScreen;      // x = render height (px), y = minimum line width (px) = max(1.5, 1.75 H / 1080), z = 1/H, w = 0

// ------------------------------------------------------------------------------------------------
// Small helpers.
// ------------------------------------------------------------------------------------------------
#define TH_EPS 1e-5

float ThSafeDiv(float a, float b) { return a / (abs(b) > TH_EPS ? b : (b >= 0 ? TH_EPS : -TH_EPS)); }

// smootherstep on [0,1]
float ThSmootherstep01(float x) { float t = saturate(x); return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }

// Integral of smoothstep(a, a + w, t) dt from -inf to y (exact, used by the deck optical depth).
float ThSmoothstepIntegral(float a, float w, float y)
{
    float u = saturate((y - a) / w);
    return w * (u * u * u - 0.5 * u * u * u * u) + max(y - (a + w), 0.0);
}

// Hash without sine (Dave Hoskins, "Hash without Sine", MIT); input should be moderate-sized (cell indices).
float ThHash12(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}
float2 ThHash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973)); // pal:ok (hash constants)
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}
float3 ThHash32(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973)); // pal:ok (hash constants)
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yzz) * p3.zyx);
}
// Integer hash (PCG) for exact cell hashing.
uint ThPcg(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

// ------------------------------------------------------------------------------------------------
// Sky gradient (14.2.1). Stops at elevation 0 (H), 25 deg (M), 90 deg (Z), interpolated in
// t = sqrt(saturate(dirY)) with a C1 cubic Hermite (zero tangent at H and Z, Catmull-Rom tangent at
// M, clamped to the stop range) so no flat band forms at the middle stop. D in [0, .5] blends
// DAY -> DUSK, D in [.5, 1] DUSK -> NIGHT. Below the horizon the horizon colour is returned.
// C# mirror: FogMath.SkyColor (EditMode tests compare both).
// ------------------------------------------------------------------------------------------------
void ThreadSkyStops(float D, out float3 h, out float3 m, out float3 z)
{
    float a = saturate(D * 2.0);
    float b = saturate(D * 2.0 - 1.0);
    h = lerp(lerp(PAL_SKY_H_DAY, PAL_SKY_H_DUSK, a), PAL_SKY_H_NIGHT, b);
    m = lerp(lerp(PAL_SKY_M_DAY, PAL_SKY_M_DUSK, a), PAL_SKY_M_NIGHT, b);
    z = lerp(lerp(PAL_SKY_Z_DAY, PAL_SKY_Z_DUSK, a), PAL_SKY_Z_NIGHT, b);
}

float3 ThHermite(float3 p0, float3 p1, float3 m0, float3 m1, float u)
{
    float u2 = u * u, u3 = u2 * u;
    return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1;
}

float3 ThreadSkyColor(float dirY, float D)
{
    float3 h, m, z;
    ThreadSkyStops(D, h, m, z);
    const float tM = 0.650074; // sqrt(sin(25 deg))
    float t = sqrt(saturate(dirY));
    float3 slopeM = (z - h);   // Catmull-Rom tangent at M in t units (t_Z - t_H = 1)
    float3 c;
    if (t < tM)
    {
        float u = t / tM;
        c = ThHermite(h, m, 0, slopeM * tM, u);
        c = clamp(c, min(h, m), max(h, m));
    }
    else
    {
        float u = (t - tM) / (1.0 - tM);
        c = ThHermite(m, z, slopeM * (1.0 - tM), 0, u);
        c = clamp(c, min(m, z), max(m, z));
    }
    return c;
}

float3 ThreadHorizonColor() { return ThreadSkyColor(0.0, _ThSky.x); }

// ------------------------------------------------------------------------------------------------
// Fog (14.7.6): tau = tau_dist + tau_bank + tau_deck. Composition (D-0xx "atmosphere"): the distance fog first
// (toward the distance colour of the ray), then the bank / deck over it (toward CLOUD_TOP), each step a
// hue-preserving haze mix (ThreadHazeMix): luminance converges at the physical rate e^-tau, chroma more slowly.
// Sky rays apply the same cloud step to the gradient, so ground fully fogged at the horizon equals the sky there.
// Virtual altitude of a world point: y + A_virt (the ground group is shifted by -A_virt).
// C# mirror: FogMath (HazeMix, FogMix, ApplySky, TauDeckSky, TauBankFootprint); FogTests compares them.
// ------------------------------------------------------------------------------------------------
#define TH_FOG_CHROMA_N 1.5            // haze chroma weight 1 - (1 - T)^n: chroma converges more slowly than luminance
#define TH_FOG_BANK_FOOT_PX 12.0      // bank footprint window (pixels beyond the surface): soft bank foot on the ground
#define TH_FOG_BANK_UNDERGROUND 1.0e4  // the window's medium continues under the ground (bottom slab), m

float ThreadLuma(float3 c) { return dot(c, float3(0.2126729, 0.7151522, 0.0721750)); } // pal:ok (Rec.709 luminance weights)

// OKLab (B. Ottosson 2020, public domain): linear sRGB <-> Lab.
float3 ThreadOklabFromLinear(float3 c)
{
    float3 lms = float3(dot(float3(0.4122214708, 0.5363325363, 0.0514459929), c),   // pal:ok (matrix row)
                        dot(float3(0.2119034982, 0.6806995451, 0.1073969566), c),   // pal:ok (matrix row)
                        dot(float3(0.0883024619, 0.2817188376, 0.6299787005), c));  // pal:ok (matrix row)
    lms = pow(max(lms, 0.0), 1.0 / 3.0);
    return float3(dot(float3(0.2104542553, 0.7936177850, -0.0040720468), lms),      // pal:ok (matrix row)
                  dot(float3(1.9779984951, -2.4285922050, 0.4505937099), lms),      // pal:ok (matrix row)
                  dot(float3(0.0259040371, 0.7827717662, -0.8086757660), lms));     // pal:ok (matrix row)
}
float3 ThreadLinearFromOklab(float3 lab)
{
    float3 lms = float3(lab.x + 0.3963377774 * lab.y + 0.2158037573 * lab.z,   // pal:ok (matrix, not a colour)
                        lab.x - 0.1055613458 * lab.y - 0.0638541728 * lab.z,
                        lab.x - 0.0894841775 * lab.y - 1.2914855480 * lab.z);
    lms = lms * lms * lms;
    return float3(dot(float3(4.0767416621, -3.3077115913, 0.2309699292), lms),      // pal:ok (matrix row)
                  dot(float3(-1.2684380046, 2.6097574011, -0.3413193965), lms),     // pal:ok (matrix row)
                  dot(float3(-0.0041960863, -0.7034186147, 1.7076147010), lms));    // pal:ok (matrix row)
}

// Hue-preserving haze (critic C-09): colour c seen through transmittance T toward the fog colour f.
// Luminance is exactly the physical lerp(Y_f, Y_c, T) (contrast laws such as D-015 hold unchanged); hue and chroma
// (OKLab a, b and the chromatic part of L) follow the slower weight Tc = 1 - (1 - T)^n, so hazed teal stays a pale
// teal and coral a pale coral instead of slate / mauve. Exact at both ends: T = 1 gives c, T = 0 gives f.
float3 ThreadHazeMix(float3 c, float3 f, float T)
{
    T = saturate(T);
    if (T >= 1.0) return c;
    if (T <= 1e-6) return f;
    float Yc = ThreadLuma(c), Yf = ThreadLuma(f);
    float Y = lerp(Yf, Yc, T);
    float Tc = 1.0 - pow(1.0 - T, TH_FOG_CHROMA_N);
    float3 lc = ThreadOklabFromLinear(c), lf = ThreadOklabFromLinear(f);
    float3 lab = lerp(lf, lc, Tc);
    // lightness: the neutral lightness of the physical luminance plus the chromatic offset of each end
    const float third = 1.0 / 3.0;
    lab.x = pow(max(Y, 0.0), third) + lerp(lf.x - pow(max(Yf, 0.0), third), lc.x - pow(max(Yc, 0.0), third), Tc);
    float3 rgb = max(ThreadLinearFromOklab(lab), 0.0);
    return rgb * (Y / max(ThreadLuma(rgb), 1e-6));   // exact physical luminance
}

// Colour of the bank and deck terms (CLOUD_TOP under the fog colour cap of 14.7.6).
float3 ThreadCloudFogColour() { return min(PAL_CLOUD_TOP, _ThFog.w); }

// Closed-form height-fog integral k * int_0^d exp(-(y0 + ry t) / H) dt.
float ThreadTauDistance(float3 cam, float3 dir, float d)
{
    float k = _ThFog.x;
    if (k <= 0.0) return 0.0;
    float H = max(_ThFog.y, 1.0);
    float y0 = cam.y + _ThFog.z;
    float e0 = exp(-clamp(y0, -2000.0, 1.0e6) / H);
    float x = dir.y * d / H;
    float f;
    if (abs(x) < 1e-3)
        f = d * (1.0 - 0.5 * x + x * x / 6.0);
    else
        f = H * (1.0 - exp(-clamp(x, -60.0, 60.0))) / (abs(dir.y) > 1e-7 ? dir.y : 1e-7);
    return k * e0 * max(f, 0.0);
}

// Bank density at a ground-group-local point.
float ThreadBankDensityLocal(float3 p)
{
    float g = (p.x - _ThBank.y) / TC_BankSigmaX;
    g *= g;
    g = exp(-g * g);                       // flat-topped lateral profile exp(-(x/sigma)^4) (D-025)
    float pz = smoothstep(TC_BankZ0, TC_BankZ1, p.z) * (1.0 - smoothstep(TC_BankZ2, TC_BankZ3, p.z));
    float yv = 1.0;
    [unroll] for (int i = 0; i < 4; i++)
    {
        float top = TC_BankSlabTop0 + TC_BankSlabTopStep * i;
        yv -= 0.25 * smoothstep(top - TC_BankSlabHalfEdge, top + TC_BankSlabHalfEdge, p.y);
    }
    return TC_BankDensity * _ThBank.w * g * pz * yv;
}

// Direct-sun visibility of a surface inside the bank (D-052): the mist scatters the sun, so lit and shaded faces
// converge on a diffuse middle (lit weight TC_BankDiffuseLit) as exp(-density x TC_BankSunPath). Smooth in space
// and in s (the density follows the bank's own profiles and its fade behind the walker), so it never pops.
float ThreadBankSunVisibility(float3 positionWS)
{
    if (_ThBank.x <= 0.0) return 1.0;
    float3 p = positionWS;
    p.y -= _ThBank.z;   // world -> ground-group-local
    return exp(-ThreadBankDensityLocal(p) * TC_BankSunPath);
}

// 16 fixed midpoint samples over the ray's clip against the bank box (no jitter).
float ThreadTauBank(float3 cam, float3 dir, float d)
{
    if (_ThBank.x <= 0.0) return 0.0;
    float3 o = cam;
    o.y -= _ThBank.z;   // world -> ground-group-local
    float3 bmin = float3(TC_BankBoxXMin, TC_BankBoxYMin, TC_BankBoxZMin); // pal:ok (box)
    float3 bmax = float3(TC_BankBoxXMax, TC_BankBoxYMax, TC_BankBoxZMax); // pal:ok (box)
    float3 sd = float3(abs(dir.x) > 1e-6 ? dir.x : 1e-6, abs(dir.y) > 1e-6 ? dir.y : 1e-6, abs(dir.z) > 1e-6 ? dir.z : 1e-6); // pal:ok
    float3 inv = 1.0 / sd;
    float3 t0 = (bmin - o) * inv, t1 = (bmax - o) * inv;
    float3 tn = min(t0, t1), tf = max(t0, t1);
    float ta = max(max(tn.x, tn.y), max(tn.z, 0.0));
    float tb = min(min(tf.x, tf.y), min(tf.z, d));
    if (tb <= ta) return 0.0;
    float dt = (tb - ta) / TC_BankSamples;
    float sum = 0.0;
    [unroll] for (int i = 0; i < 16; i++)
        sum += ThreadBankDensityLocal(o + dir * (ta + (i + 0.5) * dt));
    return sum * dt;
}

// Bank optical depth of a geometry pixel with its screen footprint (critics C-r2-3, E-r2-3): ThreadTauBank to the
// surface, plus -ln of the mean transmittance over the pixel's window of hit distances beyond it, d .. 1 / uLo
// (uLo = 1/d - TH_FOG_BANK_FOOT_PX fwidth(1/d); 1/d is affine in screen space on a plane). On the ground at grazing
// angles the bank's 40 m z ramp collapses into one pixel row; the window spreads its foot over ~12 px below it.
// The window looks only beyond the surface (toward the horizon), so the first ground row under the horizon averages
// sky-like rays and meets the sky. A surface facing the camera has uLo ~ 1/d: exactly ThreadTauBank (T-BANK).
float ThreadTauBankFootprint(float3 cam, float3 dir, float d, float uLo)
{
    float tau = ThreadTauBank(cam, dir, d);
    if (_ThBank.x <= 0.0) return tau;
    float u = 1.0 / max(d, 1e-4);
    float dFar = uLo > 1e-9 ? 1.0 / uLo : 1.0e9;
    [branch] if (dFar > d * 1.001)
    {
        float3 o = cam;
        o.y -= _ThBank.z;
        // the medium beyond the surface continues under the ground (the bottom slab, Y = 1), as neighbouring rays see it
        float3 bmin = float3(TC_BankBoxXMin, TC_BankBoxYMin - TH_FOG_BANK_UNDERGROUND, TC_BankBoxZMin); // pal:ok (box)
        float3 bmax = float3(TC_BankBoxXMax, TC_BankBoxYMax, TC_BankBoxZMax);                          // pal:ok (box)
        float3 sd = float3(abs(dir.x) > 1e-6 ? dir.x : 1e-6, abs(dir.y) > 1e-6 ? dir.y : 1e-6, abs(dir.z) > 1e-6 ? dir.z : 1e-6); // pal:ok
        float3 inv = 1.0 / sd;
        float3 t0 = (bmin - o) * inv, t1 = (bmax - o) * inv;
        float3 tn = min(t0, t1), tf = max(t0, t1);
        float ta = max(max(tn.x, tn.y), max(tn.z, d));
        float tb = min(min(tf.x, tf.y), min(tf.z, dFar));
        if (tb > ta)
        {
            // march uniformly in 1/t, i.e. uniformly in screen rows on a plane: every step carries the same window share
            float W = max(u - uLo, 1e-12);
            float ua = 1.0 / ta, ub = 1.0 / tb;
            float du = (ua - ub) / TC_BankSamples;
            float meanT = (u - ua) / W;                         // window part in front of the box: clear
            float tx = 0.0, tPrev = ta;
            [unroll] for (int i = 0; i < 16; i++)
            {
                float tN = 1.0 / max(ua - (i + 1) * du, 1e-12);
                float rd = ThreadBankDensityLocal(o + dir * (0.5 * (tPrev + tN))) * (tN - tPrev);
                meanT += du / W * exp(-(tx + 0.5 * rd));
                tx += rd;
                tPrev = tN;
            }
            meanT += (ub - uLo) / W * exp(-tx);                 // window part beyond the box (sky-like rays)
            tau -= log(clamp(meanT, 1e-30, 1.0));
        }
    }
    return tau;
}

// Deck profile f(y) = smoothstep(bot, bot + 100, y) - 1/3 * sum_i smoothstep(top - 150 + 50 i, +50, y), evaluated in
// TOP-RELATIVE coordinates y' = y - top (bot' = bot - top = -1200): f = 0 above the top and below the bottom, so the
// exact antiderivative F' is constant outside [bot', 0] and every evaluation can be clamped to that interval. This
// avoids subtracting two ~1e7 m values (float cancellation produced per-pixel noise and a speckled horizon line once
// the camera was far above the slab; decision D-024).
float ThreadDeckFRel(float yRel, float botRel)
{
    float y = clamp(yRel, botRel, 0.0);
    float F = ThSmoothstepIntegral(botRel, TC_DeckBottomSkin, y);
    [unroll] for (int i = 0; i < 3; i++)
        F -= ThSmoothstepIntegral(-TC_DeckSkin + TC_DeckTopSkinStep * i, TC_DeckTopSkinStep, y) / 3.0;
    return F;
}
float ThreadDeckProfileRel(float yRel, float botRel)
{
    float f = smoothstep(botRel, botRel + TC_DeckBottomSkin, yRel);
    [unroll] for (int i = 0; i < 3; i++)
    {
        float a = -TC_DeckSkin + TC_DeckTopSkinStep * i;
        f -= smoothstep(a, a + TC_DeckTopSkinStep, yRel) / 3.0;
    }
    return yRel > 0.0 ? 0.0 : max(f, 0.0);
}

// Optical depth of the deck slab along a ray of length d (d may be huge for sky rays).
float ThreadTauDeck(float3 cam, float3 dir, float d)
{
    if (_ThDeck.x <= 0.0) return 0.0;
    float botRel = _ThDeck.z - _ThDeck.y;           // -1200
    float y0 = cam.y - _ThDeck.y;                    // camera height relative to the deck top
    float tau;
    if (abs(dir.y) < 1e-4)
    {
        tau = ThreadDeckProfileRel(y0, botRel) * d;
    }
    else
    {
        // clamp the end point to the slab before forming it so no huge numbers are ever subtracted
        float yEnd = y0 + dir.y * min(d, 1.0e7);
        float a = clamp(y0, botRel, 0.0), b = clamp(yEnd, botRel, 0.0);
        tau = abs(ThreadDeckFRel(b, botRel) - ThreadDeckFRel(a, botRel)) / abs(dir.y);
    }
    return _ThDeck.x * _ThDeck.w * tau;
}

// Deck optical depth along an upward sky ray at infinity (elevation sine dirY >= 0). The deck is curved with the
// Earth: a grazing ray meets the deck bottom lying dh above the camera at a slope of at least sqrt(2 dh / R) (chord
// limit), so below the slab 1/r_y no longer diverges at the horizon. The planar slab drew a white band of zero width
// hugging the horizon as the gate opened, and an exactly horizontal ray (|r_y| < 1e-4) got no deck at all. Inside
// the slab dh = 0 and the planar closed form is unchanged (D-006, D-007, D-031).
float ThreadTauDeckSky(float dirY)
{
    if (_ThDeck.x <= 0.0) return 0.0;
    float3 cam = _WorldSpaceCameraPos;
    float dh = max(_ThDeck.z - cam.y, 0.0);   // camera below the deck: distance up to its bottom
    float dy = max(sqrt(dirY * dirY + 2.0 * dh / TC_EarthRadius), 1e-4);
    return ThreadTauDeck(cam, float3(0.0, dy, 0.0), 1.0e7); // pal:ok (direction)
}

// Deck veil of a direction: transmittance of the deck along the sky ray of that elevation, mirrored into the upper
// hemisphere below the horizon (the overcast seen above the camera). 1 when the camera is above the deck.
float ThreadDeckSkyTransmittance(float dirY)
{
    if (_ThDeck.x <= 0.0) return 1.0;
    return exp(-min(ThreadTauDeckSky(abs(dirY)), 80.0));
}

// The ONE shared colour of everything at or beyond the end of the distance fog in a direction (D-0xx, supersedes
// D-037): the colour c (the sky gradient of that direction) veiled toward CLOUD_TOP by the deck as seen along the
// mirrored sky ray. Geometry fog (ThreadFogDistanceColour) and sky rays below the horizon (ThreadFogApplySky) both
// use it, so the ground, the below-horizon sky and the overcast above meet at the horizon for every gate value.
// Not capped again: both ends are under the cap (sky gradient <= 0.9, ThreadCloudFogColour), and the overcast above
// (ThreadFogApplySky on the gradient, uncapped like every sky colour) must stay identical to it.
float3 ThreadFogVeil(float3 c, float dirY)
{
    return ThreadHazeMix(c, ThreadCloudFogColour(), ThreadDeckSkyTransmittance(dirY));
}

// Distance-fog colour of a ray: the sky colour along it (horizon colour at and below the horizon, 14.7.2; D-013),
// under the deck veil.
float3 ThreadFogDistanceColour(float dirY) { return ThreadFogVeil(ThreadSkyColor(dirY, _ThSky.x), dirY); }

// Direction (y) of the last ray evaluated by ThreadFogTaus in this shader invocation. The distance fog
// takes the colour of the sky along that ray (= the horizon colour at and below the horizon, 14.7.2), so
// geometry seen along steep rays (cable top, ribbons) fades toward the sky behind it (D-013).
static float th_FogDirY = 0.0;

// Fog mix: the distance term first (far: toward the distance colour of the ray), then the bank / deck over it (near:
// toward CLOUD_TOP), the same order as a sky ray, whose gradient already is the distance-fogged scene at infinity
// (D-003). With the old optical-depth-weighted single mix, ground under the bank met the horizon ~4 percent darker
// than the sky above it (critics C-r2-3, E-r2-3).
float3 ThreadFogMix(float3 color, float tauDist, float tauCloud)
{
    if (tauDist + tauCloud <= 1e-6) return color;
    float3 c = color;
    [branch] if (tauDist > 1e-6)
        c = ThreadHazeMix(c, ThreadFogDistanceColour(th_FogDirY), exp(-min(tauDist, 80.0)));
    [branch] if (tauCloud > 1e-6)
        c = ThreadHazeMix(c, ThreadCloudFogColour(), exp(-min(tauCloud, 80.0)));
    return c;
}

// Optical depths from the camera to a world position (evaluate once per pixel, reuse). In the fragment stage the
// bank term carries the pixel footprint (ThreadTauBankFootprint); elsewhere it is ThreadTauBank.
void ThreadFogTaus(float3 positionWS, out float tauDist, out float tauCloud)
{
    float3 cam = _WorldSpaceCameraPos;
    float3 r = positionWS - cam;
    float d = length(r);
    float3 dir = r / max(d, 1e-4);
    th_FogDirY = dir.y;
    tauDist = ThreadTauDistance(cam, dir, d);
    float uLo = 1.0 / max(d, 1e-4);
#if defined(SHADER_STAGE_FRAGMENT)
    uLo -= TH_FOG_BANK_FOOT_PX * fwidth(uLo);
#endif
    tauCloud = ThreadTauBankFootprint(cam, dir, d, uLo) + ThreadTauDeck(cam, dir, d);
}
float ThreadFogAmountFromTaus(float tauDist, float tauCloud) { return 1.0 - exp(-min(tauDist + tauCloud, 80.0)); }

// The one shared fog function (every material shader calls it as the last step).
float3 ThreadFogApply(float3 color, float3 positionWS)
{
    float tauDist, tauCloud;
    ThreadFogTaus(positionWS, tauDist, tauCloud);
    return ThreadFogMix(color, tauDist, tauCloud);
}

// Fog amount only (1 - transmittance).
float ThreadFogAmount(float3 positionWS)
{
    float tauDist, tauCloud;
    ThreadFogTaus(positionWS, tauDist, tauCloud);
    return ThreadFogAmountFromTaus(tauDist, tauCloud);
}

// Sky rays (at infinity): the gradient already is the atmosphere's colour toward infinity, so only
// the finite bank and deck terms are applied (D-003). maxD limits a downward ray (e.g. to the deck top plane above
// the cloud sea); upward sky rays are rays at infinity and take the curved-deck depth ThreadTauDeckSky. Below the
// horizon a sky ray stands for fully distance-fogged ground: it first takes the deck veil of ThreadFogVeil, exactly
// as the ground's distance colour does, so the plain, the strip beyond the 14 km ground disc and the overcast meet
// without a seam during the deck approach (critics C-06, A12, B8, PAC-r2-1).
float3 ThreadFogApplySky(float3 color, float3 dir, float maxD)
{
    float3 cam = _WorldSpaceCameraPos;
    [branch] if (dir.y < 0.0 && _ThDeck.x > 0.0)
        color = ThreadFogVeil(color, dir.y);
    float tauDeck = dir.y >= 0.0 ? ThreadTauDeckSky(dir.y) : ThreadTauDeck(cam, dir, maxD);
    float tauCloud = ThreadTauBank(cam, dir, min(maxD, 2000.0)) + tauDeck;
    [branch] if (tauCloud > 1e-6)
        color = ThreadHazeMix(color, ThreadCloudFogColour(), exp(-min(tauCloud, 80.0)));
    return color;
}

// ------------------------------------------------------------------------------------------------
// Analytic cable shadow on deck, rails, arms and beads (14.7.7). 1 = lit, 0 = in the shadow.
// The t > 0 test is softened over +-2 m so an arm at exactly t = 0 has no discontinuity.
// ------------------------------------------------------------------------------------------------
float ThreadCableShadow(float3 positionWS)
{
    if (_ThCable.x <= 0.0) return 1.0;
    float2 sh = _ThCable.yz;
    float2 q = positionWS.xz - float2(TC_CableAxisX, TC_CableAxisZ); // pal:ok (axis)
    float t = -dot(q, sh);
    float n = abs(q.x * sh.y - q.y * sh.x);
    float lit = smoothstep(TC_DeckShadowN0, TC_DeckShadowN1, n);
    return lerp(1.0, lit, smoothstep(-2.0, 2.0, t));
}

// ------------------------------------------------------------------------------------------------
// Ambient and the "flat with a rim" recipe (14.2.2).
// ------------------------------------------------------------------------------------------------
static const float3 TH_SHADE_TINT = float3(0.52, 0.58, 0.78);   // pal:ok (named shade tint, 14.2.2)
static const float3 TH_RIM_CORAL_MIX = float3(0.5, 0.5, 0.5);    // pal:ok (lerp weight CREAM -> CORAL)

float3 ThreadAmbient(float3 N)
{
    float up = saturate(N.y);
    float down = saturate(-N.y);
    float side = saturate(1.0 - up - down);
    return _ThAmbUp.rgb * up + _ThAmbSide.rgb * side + _ThAmbDown.rgb * down;
}

struct ThreadSurface
{
    float3 albedo;       // lit albedo (linear)
    float3 shadeAlbedo;  // shade albedo (albedo * TH_SHADE_TINT unless the material gives one)
    float3 normalWS;     // unit
    float3 positionWS;
    float ao;            // vertex R, 0.55..1
    float heightShade;   // vertex G (1 = no darkening)
    float shadowAtten;   // main-light shadow map attenuation
    float rimCoral;      // 0 = CREAM rim, 1 = lerp(CREAM, CORAL, .5) rim
    float rimScale;      // usually 1
    float cableShadowOn; // 1 = apply the analytic cable shadow
    float forceShade;    // 1 = litTotal 0 (colossus ground shadow mode)
    float fogAmount;     // 1 - fog transmittance at positionWS (from ThreadFogTaus), suppresses the rim in haze
};

float3 ThreadShadeFlat(ThreadSurface s)
{
    float3 N = s.normalWS;
    float3 L = _ThSunDir.xyz;
    float3 V = normalize(_WorldSpaceCameraPos - s.positionWS);
    float nl = dot(N, L);
    float lit = smoothstep(TC_TerminatorLo, TC_TerminatorHi, nl);
    float cable = s.cableShadowOn > 0.5 ? ThreadCableShadow(s.positionWS) : 1.0;
    float litTotal = lit * s.shadowAtten * cable * (1.0 - s.forceShade);
    litTotal = lerp(TC_BankDiffuseLit * (1.0 - s.forceShade), litTotal, ThreadBankSunVisibility(s.positionWS));
    float3 base = lerp(s.shadeAlbedo, s.albedo, litTotal);
    base *= lerp(TC_AoMin, 1.0, saturate(s.ao));
    base *= lerp(0.97, 1.0, saturate(s.heightShade));
    float3 radiance = base * (TC_AmbientWeight * ThreadAmbient(N) + TC_SunWeight * _ThSunColor.rgb);
    float rim = pow(1.0 - saturate(dot(N, V)), TC_RimPower) * TC_RimStrength * (0.35 + 0.65 * lit);
    rim *= 1.0 - smoothstep(0.85, 0.95, abs(N.y));
    rim *= 1.0 - s.fogAmount;
    rim *= s.rimScale * (1.0 - s.forceShade);
    float3 rimTint = lerp(PAL_CREAM, lerp(PAL_CREAM, PAL_CORAL, TH_RIM_CORAL_MIX), s.rimCoral);
    radiance += rimTint * rim;
    return radiance;
}

#endif // THREAD_COMMON_INCLUDED
