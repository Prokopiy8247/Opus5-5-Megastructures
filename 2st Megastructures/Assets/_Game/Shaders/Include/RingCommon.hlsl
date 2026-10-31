// RingCommon.hlsl - shared helpers and the global uniform contract of The Ring.
// Every global below is written by AtmosphereDriver (per frame in LateUpdate, per camera in beginCameraRendering).
// See DEVELOPMENT.md "Global shader uniform contract". Twins of the C# helpers live in Scripts/Runtime/Core/RingMath.cs.
#ifndef RING_COMMON_INCLUDED
#define RING_COMMON_INCLUDED

#include "RingPalette.hlsl"
#include "RingWorldData.hlsl"

#define RING_R 16000.0
#define RING_PI 3.14159265
#define RING_TWO_PI 6.2831853
#define RING_DEG2RAD 0.01745329
#define RING_RAD2DEG 57.2957795
#define RING_CIRC 100530.9649   // 2 pi R
#define RING_LIGHT_CAL 0.45      // twin of RingConstants.LightCal (light calibration, 14.2.3)

// ---------------- global uniforms (not material properties: never inside UnityPerMaterial) ----------------
float4 _RingCam;        // xyz camera position (flat world), w = vertical pixel angle (rad / px)
float4 _RingTime;       // x = animation time (s, wrapped to 7200), y = t mod 60, z = capture flag, w = progress s
float4 _RingSet;        // x = set (0 valley, 1 plain, 2 tunnel, 3 outside), y = outside weight beyond the wall, z = far-rim rule, w = wall x
float4 _SkyParams;      // x = sky state 0..2, y = night N(u), z = star visible fraction V, w = arch night lift 0.5 N
float4 _SunsetParams;   // x = sunset u, y = squares haze alpha, z = terminator half-width (deg), w = squares B/C (1 on)
float4 _StarDir;        // xyz toward the inside star, w = angular radius (rad)
float4 _StarDirOut;     // xyz toward the outside star (L7), w = angular radius (rad)
float4 _FogParams;      // x = sigma (1/m), y = valley mist k, z = desaturation (0.25), w = space darkening weight
float4 _FogTint;        // rgb = override fog colour (linear), a = weight
float4 _FogExtra;       // x = 1 / aerial length (1/m, 0 off), y = 1 / fog scale height (1/m, 0 off), z = aerial desaturation, w = arch haze sigma (ADR-012)
float4 _RingFillLight;      // rgb = arch fill colour x intensity x light calibration (the directional fill URP also renders)
float4 _AmbSky;         // trilight sky colour, premultiplied by the beat multiplier
float4 _AmbEquator;
float4 _AmbGround;
float4 _ShipShadow;     // x = centre x, y = centre z, z = strength 0..1, w = key dip (felt cue)
float4 _CloudShadowA;   // x = centre x, y = centre z, z = strength
float4 _CloudShadowB;
float4 _ArkParams;      // x = x_c, y = window level, z = engine level, w = roll (rad)
float4 _Lamps;          // x = bollard lamp level, y = city level, z = beacon pulse 0..1, w = tunnel mouth spill 0..1
float4 _Wind;           // x = gust 0..1, y = wind phase time
float4 _Swarm;          // x = glint scale g(s), y = swarm time (s)
float4 _KeyLightDir;    // xyz toward the key light currently used by URP (L or L7), w = key intensity
float4 _KeyLightColor;  // rgb key colour x intensity (linear)

// ---------------- scalar helpers ----------------
float RingSmootherstep01(float t) { t = saturate(t); return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
float RingSmootherstep(float a, float b, float x) { return RingSmootherstep01((x - a) / (b - a)); }
float RingLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }
float RingSafeDiv(float a, float b) { return a / (abs(b) < 1e-8 ? (b < 0.0 ? -1e-8 : 1e-8) : b); }

// ---------------- hashes (PCG, Jarzynski & Olano 2020); twin of RingMath.Pcg ----------------
uint RingPcg(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}
uint RingPcg2(int2 p) { return RingPcg(asuint(p.x) + RingPcg(asuint(p.y))); }
uint RingPcg3(int3 p) { return RingPcg(asuint(p.x) + RingPcg(asuint(p.y) + RingPcg(asuint(p.z)))); }
float RingU01(uint h) { return (h >> 8u) * (1.0 / 16777216.0); }
float RingHash21(int2 p) { return RingU01(RingPcg2(p)); }
float2 RingHash22(int2 p) { uint h = RingPcg2(p); return float2(RingU01(h), RingU01(RingPcg(h))); }

// 2D value noise in [0, 1], quintic interpolation; twin of RingMath.ValueNoise
float RingValueNoise(float2 x)
{
    float2 f = floor(x);
    int2 i = (int2)f;
    float2 t = x - f;
    float2 u = t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    float a = RingHash21(i), b = RingHash21(i + int2(1, 0));
    float c = RingHash21(i + int2(0, 1)), d = RingHash21(i + int2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Interleaved gradient noise (Jimenez 2014), pixel coordinates
float RingIGN(float2 px) { return frac(52.9829189 * frac(dot(px, float2(0.06711056, 0.00583715)))); }

// Exact box-filtered coverage of a single line of width w centred at distance 0, for a filter footprint fp
// (same units). Converges to w / fp (energy preserving) once the line is sub-footprint.
float RingFilteredLine(float distToLine, float width, float fp)
{
    float h = 0.5 * max(fp, 1e-6);
    float a = min(distToLine + h, 0.5 * width);
    float b = max(distToLine - h, -0.5 * width);
    return saturate(max(a - b, 0.0) / (2.0 * h));
}

// Exact box-filtered periodic line train (IQ, "filterable procedurals"): lines of width w every period P,
// centred on multiples of P. fp = footprint in the same units. Fades to 0 once the spacing drops below
// fadeSpacingPx footprints (so far surfaces turn smooth instead of grey).
float RingFilteredGrid1(float x, float period, float width, float fp, float fadeSpacingPx)
{
    float N = period / max(width, 1e-6);
    float p = x / period + 0.5 / N;
    float w = max(fp / period, 1e-6);
    float a = p + 0.5 * w, b = p - 0.5 * w;
    float cov = (floor(a) + min(frac(a) * N, 1.0) - floor(b) - min(frac(b) * N, 1.0)) / (N * w);
    float spacingPx = period / max(fp, 1e-6);
    float fade = saturate((spacingPx - fadeSpacingPx) / max(fadeSpacingPx, 1e-3));
    return saturate(cov) * fade;
}

#endif
