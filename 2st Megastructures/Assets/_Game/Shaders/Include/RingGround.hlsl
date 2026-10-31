// RingGround.hlsl - analytic ground shared by the terrain (M02) and the arch in the sky (M05).
// RingHeight is the twin of RingWorld.Height (C#). RingGroundAlbedo(arc, x, fp) is the ONE colour function of the
// ring surface: the valley meadow near the walk and a filtered patchwork (fields, wheat, terraces, towns, rivers)
// everywhere else. Arc is periodic with the circumference so the far side never shows a seam.
#ifndef RING_GROUND_INCLUDED
#define RING_GROUND_INCLUDED

#include "RingCommon.hlsl"

float RingSegDist(float2 p, float4 s)
{
    float2 a = s.xy, b = s.zw, ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
    return length(a + ab * t - p);
}

float RingPathDist(float2 p)
{
    float d = 1e9;
    [unroll] for (int i = 0; i < RING_PATH_SEGS; i++) d = min(d, RingSegDist(p, RING_PATH[i]));
    return d;
}

float RingRiverDist(float2 p)
{
    float d = 1e9;
    [unroll] for (int i = 0; i < RING_RIVER_PTS - 1; i++) d = min(d, RingSegDist(p, float4(RING_RIVER[i], RING_RIVER[i + 1])));
    return d;
}

float RingRiverCarve(float d) { return clamp(3.2 - (d - 15.0) / 3.0, 0.0, 3.2); }

float RingLeveeMask(float2 p)
{
    float dx = max(max(RING_LEVEE.x - p.x, p.x - RING_LEVEE.y), 0.0);
    float dz = max(max(RING_LEVEE.z - p.y, p.y - RING_LEVEE.w), 0.0);
    return 1.0 - smoothstep(4.0, 14.0, sqrt(dx * dx + dz * dz));
}

// Twin of RingWorld.Height. p = (x, z); setv = _RingSet.x (flank only while < 1)
float RingHeight(float2 p, float setv)
{
    float pd = RingPathDist(p);
    float rd = RingRiverDist(p);
    float n = 6.0 * (RingValueNoise(p / 420.0) * 2.0 - 1.0) + 1.5 * (RingValueNoise(p / 90.0 + float2(17.3, 5.1)) * 2.0 - 1.0);
    float zNear = smoothstep(4500.0, 1800.0, abs(p.y - 300.0));   // the walk's stretch of the valley (x-only terms)
    n *= 1.0 - 0.85 * smoothstep(40.0, 100.0, p.x) * zNear;
    n *= smoothstep(24.6, 60.0, rd);
    n *= smoothstep(8.0, 60.0, pd);   // no trench: the rolling land settles within 60 m of a path (critics run04)
    float hd = length(p - RING_HILL.xy);
    float hill = hd < RING_HILL.z ? RING_HILL.w * (0.5 + 0.5 * cos(RING_PI * hd / RING_HILL.z)) : 0.0;
    float flank = 22.0 * smoothstep(60.0, 260.0, p.x) * (1.0 - saturate(setv)) * zNear;
    float h = (n + hill + flank) * smoothstep(6.0, 16.0, pd) * (1.0 - RingLeveeMask(p));
    // the river valley cuts through any land term (identical to h - RingRiverCarve(rd) where the land term is 0)
    return min(h, -3.2 + max(rd - 15.0, 0.0) / 3.0 + max(rd - 24.6, 0.0) * 0.9);
}

// ---------------- ground colour ----------------
// ---------------- periodic noise (exactly periodic around the ring through a circle mapping of the arc) ----------------
float RingValueNoise3(float3 x)
{
    float3 f = floor(x);
    int3 i = (int3)f;
    float3 t = x - f;
    float3 u = t * t * (3.0 - 2.0 * t);
    float n000 = RingU01(RingPcg3(i)), n100 = RingU01(RingPcg3(i + int3(1, 0, 0)));
    float n010 = RingU01(RingPcg3(i + int3(0, 1, 0))), n110 = RingU01(RingPcg3(i + int3(1, 1, 0)));
    float n001 = RingU01(RingPcg3(i + int3(0, 0, 1))), n101 = RingU01(RingPcg3(i + int3(1, 0, 1)));
    float n011 = RingU01(RingPcg3(i + int3(0, 1, 1))), n111 = RingU01(RingPcg3(i + int3(1, 1, 1)));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y), lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

// value noise of wavelength lambda (m) on the ring surface; phi = 2 pi arc / circumference
float RingPeriodicNoise(float phi, float x, float lambda, float seed)
{
    float3 q = float3(cos(phi) * RING_R, sin(phi) * RING_R, x) / lambda + seed * 17.37;
    return RingValueNoise3(q);
}

// ---------------- far-side geography (rework 2026-10-30, critics run04: no graticule, no towns on a lattice) ----------------
// Every pattern is periodic in arc with the circumference C (cell counts divide C exactly), filtered for footprint fp
// (sub-footprint detail converges to its mean: no shimmer, no moire, no tiling, 14.7 H2).

float RingWrapArc(float arc) { return arc - RING_CIRC * floor(arc / RING_CIRC); }

// Arch rivers: 4 meandering rivers along the arc direction; wavelengths are integer fractions of the circumference.
float RingArchRiverSdf(float arc, float x, float halfWidth)
{
    float a = RING_TWO_PI * arc / RING_CIRC;
    float d = 1e9;
    [unroll] for (int k = 0; k < 4; k++)
    {
        float x0 = -9100.0 + 5600.0 * k + 900.0 * sin(a * 3.0 + k * 1.7);
        float xr = x0 + 420.0 * sin(a * 44.0 + k * 2.3) + 150.0 * sin(a * 144.0 + 1.3 + k) + 60.0 * sin(a * 387.0 + 2.1 * k);
        d = min(d, abs(x - xr) - halfWidth);
    }
    return d;
}

// Arterial roads: three long, gently curving roads along the ring (14 m), irregularly spaced in x. They converge toward
// both horizons overhead, which reads as "a ceiling of land", without the old cross-road graticule.
float RingArterialDist(float arc, float x)
{
    float a = RING_TWO_PI * arc / RING_CIRC;
    float d = 1e9;
    [unroll] for (int k = 0; k < 3; k++)
    {
        float x0 = -7300.0 + 6900.0 * k + 1300.0 * sin(a * 2.0 + 2.6 * k) + 260.0 * sin(a * 29.0 + 1.1 * k) + 70.0 * sin(a * 211.0 + k);
        d = min(d, abs(x - x0));
    }
    return d;
}

// Valley region (meadow) around the walk; 1 inside, 0 in the far patchwork.
float RingValleyMask(float arc, float x)
{
    float ax = smoothstep(520.0, 260.0, abs(x - 20.0));
    float az = smoothstep(-700.0, -300.0, arc) * smoothstep(1700.0, 1250.0, arc);
    return ax * az;
}

// Field colour (linear albedo) from a field hash and the region (0 green country, 1 grain country). Close tones: at
// 8-32 km the patchwork is a quiet mosaic of greens with some ripe fields, never a camouflage print.
float3 RingFieldColor(uint h, float region, float farm)
{
    // farm (0..1, a 2.4 km noise) correlates neighbouring fields: whole districts of pasture, of crops, of grain
    float r = lerp(RingU01(h), farm, 0.5);
    float v = RingU01(RingPcg(h)) - 0.5;
    float3 green = lerp(PAL_MeadowA, PAL_MeadowB, 0.45 + 0.5 * v);
    float3 crop = lerp(PAL_MeadowA, PAL_LeveeGreen, 0.55 + 0.3 * v);
    float3 wheat = lerp(PAL_Wheat, PAL_MeadowA, 0.40 + 0.2 * v);
    float3 fallow = lerp(PAL_Wheat, PAL_LeveeGreen, 0.62) * (0.92 + 0.1 * v);
    float pWheat = lerp(0.04, 0.22, region), pFallow = 0.05;
    float b = 0.93 + 0.14 * RingU01(RingPcg(h + 23u));          // per-field brightness
    if (r < pWheat) return wheat * b;
    r -= pWheat;
    if (r < pFallow) return fallow * b;
    return ((r < 0.55) ? green : crop) * b;
}

float3 RingFieldMean(float region)
{
    return lerp(float3(0.150, 0.345, 0.085), float3(0.235, 0.370, 0.098), region);
}

// Towns and villages. One candidate per cell of two jittered grids (towns: C/32 = 3 141.6 m of arc x 2 400 m of x,
// villages: C/120 = 837.8 m x 900 m); each exists with a hashed probability, at a hashed position, with a hashed size
// and shape, so nothing lines up. A settlement smaller than the footprint is drawn one footprint wide with its energy
// scaled by (r / footprint)^2: far villages stay faint points instead of sparkling or vanishing.
// dens: built-up cover 0..1 (roofs by day, light density by night); tint: the light colour (warm or cool).
void RingSettlements(float wa, float x, float fp, out float dens, out float3 tint, out float lights)
{
    dens = 0.0;
    tint = PAL_CityWarm;
    lights = 0.0;
    float best = 0.0;
    [unroll] for (int L = 0; L < 2; L++)
    {
        float cellA = L == 0 ? 3141.5927 : 837.7580;
        float nA = L == 0 ? 32.0 : 120.0;
        float cellX = L == 0 ? 2400.0 : 900.0;
        float ga = wa / cellA, gx = (x + 530.0) / cellX;
        float fa0 = floor(ga), fx0 = floor(gx);
        [unroll] for (int j = -1; j <= 1; j++)
        [unroll] for (int i = -1; i <= 1; i++)
        {
            float ia = fa0 + i, ix = fx0 + j;
            float iaw = ia - nA * floor(ia / nA);                         // wrapped cell index (hash only)
            uint h = RingPcg3(int3((int)iaw, (int)ix, 101 + L));
            float p = RingU01(h);
            if (p > (L == 0 ? 0.40 : 0.32)) continue;
            float2 jit = float2(RingU01(RingPcg(h + 1u)), RingU01(RingPcg(h + 2u)));
            float2 c = float2((ia + 0.12 + 0.76 * jit.x) * cellA, (ix + 0.12 + 0.76 * jit.y) * cellX - 530.0);
            float sz = RingU01(RingPcg(h + 3u));
            float r = L == 0 ? lerp(150.0, 560.0, sz * sz) * (p < 0.035 ? 2.3 : 1.0) : lerp(30.0, 80.0, sz);
            float stretch = 0.65 + 0.7 * RingU01(RingPcg(h + 4u));       // elongated along the arc or across it
            float2 dv0 = float2(wa - c.x, x - c.y);
            float2 dv = dv0 * float2(1.0 / stretch, stretch);
            float re = max(r, fp);
            float energy = (r * r) / (re * re);
            // ragged outline: the radius breathes with a noise of the direction scale (resolved only when large)
            float detailW = saturate(2.0 - 2.0 * fp / max(0.35 * r, 1.0));
            float n = RingValueNoise(dv / max(0.3 * r, 1.0) + float2(RingU01(h) * 61.0, sz * 37.0));
            float dist = length(dv) * (1.0 + 0.45 * (n - 0.5) * detailW);
            float k = (1.0 - smoothstep(0.35 * re, re, dist)) * energy;
            if (k <= 0.0) continue;
            // districts and parks inside the larger towns (converge to their mean under large footprints)
            k *= lerp(0.8, 0.45 + 0.75 * n, detailW);
            if (k > best)
            {
                best = k;
                tint = lerp(PAL_CityWarm, PAL_CityCool, RingU01(RingPcg(h + 5u)) < 0.3 ? 0.75 : 0.12);
                // night: a glitter of individual lights, one candidate per 70 m cell (55 % lit, random position and
                // brightness), each a gaussian of radius max(0.6 footprint, 6 m) whose peak is scaled by (6 / radius)^2,
                // so its energy is constant and far towns converge to a steady glow (mean 1 per unit density)
                float2 g = dv0 / 70.0;
                float2 gi = floor(g);
                float sig = max(0.6 * fp, 6.0);
                float norm = 36.0 / (sig * sig);
                float acc = 0.0;
                [unroll] for (int lj = -1; lj <= 1; lj++)
                [unroll] for (int li = -1; li <= 1; li++)
                {
                    float2 cc = gi + float2(li, lj);
                    uint hh = RingPcg3(int3((int)cc.x, (int)cc.y, (int)(h & 0xffffu)));
                    if (RingU01(hh) > 0.55) continue;
                    float2 pp = cc + float2(RingU01(RingPcg(hh + 1u)), RingU01(RingPcg(hh + 2u)));
                    float2 dd = (g - pp) * 70.0;
                    acc += exp(-dot(dd, dd) / (sig * sig)) * (0.5 + RingU01(RingPcg(hh + 3u)));
                }
                float core = 1.0 - smoothstep(0.0, 0.6 * re, dist);
                lights = k * acc * norm * 78.0 * (0.7 + 0.6 * core);     // 78 = 1 / (0.55 pi 36 / 4 900)
            }
            dens = max(dens, k);
        }
    }
    dens = saturate(dens);
}

// Ring surface albedo at arc coordinate (m) and axial x (m), filtered for footprint fp (m).
// Outputs: town 0..1 (night lights), water 0..1 (sky reflection in the arch), road 0..1 (night lamp strings),
// tint (the light colour of the nearest settlement).
float3 RingGroundAlbedoEx(float arc, float x, float fp, out float townOut, out float waterOut, out float roadOut, out float3 tintOut)
{
    // valley meadow near the walk (inside it the patchwork is never seen: early out)
    float vm = RingValleyMask(arc, x);
    float nv = RingValueNoise(float2(arc, x) / 37.0);
    float nv2 = RingValueNoise(float2(arc, x) / 9.0 + 3.7);
    float detailW = 1.0 - smoothstep(1.0, 8.0, fp);
    float3 meadow = lerp(PAL_MeadowA, PAL_MeadowB, saturate(0.35 + 0.5 * nv + 0.25 * (nv2 - 0.5) * detailW));
    townOut = 0.0; waterOut = 0.0; roadOut = 0.0; tintOut = PAL_CityWarm;
    if (vm >= 0.9999) return meadow;
    float wa = RingWrapArc(arc);
    float phi = RING_TWO_PI * wa / RING_CIRC;
    // macro geography (periodic): region tint, forests, lakes
    float nLake = RingPeriodicNoise(phi, x, 9000.0, 1.0) * 0.62 + RingPeriodicNoise(phi, x, 2600.0, 2.0) * 0.28
                + RingPeriodicNoise(phi, x, 700.0, 9.0) * 0.10 * (1.0 - smoothstep(150.0, 500.0, fp));
    float nWood = RingPeriodicNoise(phi, x, 3800.0, 3.0) * 0.6 + RingPeriodicNoise(phi, x, 1100.0, 5.0) * 0.28
                + RingPeriodicNoise(phi, x, 330.0, 10.0) * 0.12 * (1.0 - smoothstep(60.0, 250.0, fp));
    float region = saturate(0.5 + 1.6 * (RingPeriodicNoise(phi, x, 14000.0, 6.0) - 0.5));
    // rectilinear field blocks (engineered farmland follows the ring: edges along the arc and the axis)
    const float CELL = 598.3986;                 // RING_CIRC / 168
    float ra = wa / CELL;
    uint ia = (uint)max(floor(ra), 0.0) % 168u;
    float rowOff = RingU01(RingPcg(ia + 7u)) * 600.0;
    float rx = (x + rowOff) / 600.0;
    int ix = (int)floor(rx);
    int2 cell = int2((int)ia, ix);
    uint hc = RingPcg2(cell);
    float fa = frac(ra), fx = frac(rx);
    uint split = hc % 3u;
    int2 sub = int2(0, 0);
    float splitA = 0.35 + 0.3 * RingU01(RingPcg(hc + 31u));
    float splitX = 0.35 + 0.3 * RingU01(RingPcg(hc + 37u));
    if (split >= 1u) sub.x = fa > splitA ? 1 : 0;
    if (split == 2u) sub.y = fx > splitX ? 1 : 0;
    uint hb = RingPcg3(int3(cell, sub.x * 2 + sub.y));
    float farm = RingPeriodicNoise(phi, x, 2400.0, 12.0);
    float3 patch = RingFieldColor(hb, region, farm);
    // field edges: hedgerows (3 m, gone above 6 m footprint) everywhere, tree lines (10 m, energy-preserving
    // coverage, so they survive as faint darker borders overhead) on a third of the cells
    float ea = min(fa, 1.0 - fa) * CELL, ex = min(fx, 1.0 - fx) * 600.0;
    if (split >= 1u) ea = min(ea, abs(fa - splitA) * CELL);
    if (split == 2u) ex = min(ex, abs(fx - splitX) * 600.0);
    float edge = min(ea, ex);
    float hedge = RingFilteredLine(edge, 3.0, fp) * (1.0 - smoothstep(3.0, 6.0, fp));
    float treeLine = RingFilteredLine(edge, 10.0, fp) * (RingU01(RingPcg(hc + 11u)) < 0.34 ? 1.0 : 0.0);
    float3 hedgeCol = lerp(PAL_MeadowB, PAL_LeveeGreen, 0.4) * 0.42;
    patch = lerp(patch, hedgeCol, saturate(hedge * 0.8 + treeLine * 0.85));
    patch = lerp(patch, RingFieldMean(region), smoothstep(200.0, 700.0, fp));
    // forests: dark green masses with a soft, footprint-filtered edge and inner mottling
    float woodEdge = max(fp / 1100.0, 0.02);
    float wood = smoothstep(0.60 - woodEdge, 0.60 + woodEdge, nWood);
    float mott = RingPeriodicNoise(phi, x, 420.0, 7.0);
    float3 woods = lerp(PAL_MeadowB, PAL_LeveeGreen, 0.45) * (0.50 + 0.16 * lerp(0.5, mott, 1.0 - smoothstep(60.0, 300.0, fp)));
    patch = lerp(patch, woods, wood);
    // gentle soil variation so large uniform areas still carry structure
    patch *= 0.92 + 0.16 * RingPeriodicNoise(phi, x, 1700.0, 8.0);
    // towns and villages: pale ceramic roofs (converging to their mean far away)
    float town, lightsTex;
    float3 tint;
    RingSettlements(wa, x, fp, town, tint, lightsTex);
    float3 roofs = lerp(PAL_CeramicShade, PAL_CeramicWhite, 0.30 + 0.35 * RingU01(RingPcg(hb + 9u))) * 0.62;
    patch = lerp(patch, roofs, town * (1.0 - 0.6 * wood));
    // arterial roads (14 m, light ceramic)
    float road = RingFilteredLine(RingArterialDist(wa, x), 14.0, fp);
    float3 roadCol = lerp(PAL_CeramicShade, PAL_CeramicWhite, 0.4) * 0.8;
    patch = lerp(patch, roadCol, road * (1.0 - wood * 0.8));
    // rivers (120 m main rivers) and lakes: water reflects the sky in the arch shader
    float rs = RingArchRiverSdf(arc, x, 60.0);
    float riverCov = saturate(0.5 - rs / max(fp, 1e-3));
    float lakeEdge = max(fp / 9000.0, 0.004);
    float lake = smoothstep(0.745 - lakeEdge, 0.745 + lakeEdge, nLake);
    float water = max(riverCov, lake);
    float3 waterCol = lerp(PAL_WaterDeep, PAL_TealRiver, 0.30);
    patch = lerp(patch, waterCol, water);
    float3 c = lerp(patch, meadow, vm);
    float notValley = 1.0 - vm;
    townOut = lightsTex * (1.0 - water) * notValley;   // night light density (street-lit texture)
    waterOut = water * notValley;
    roadOut = road * (1.0 - water) * notValley;
    tintOut = tint;
    return c;
}

float3 RingGroundAlbedoEx(float arc, float x, float fp, out float townOut, out float waterOut, out float roadOut)
{
    float3 tint;
    return RingGroundAlbedoEx(arc, x, fp, townOut, waterOut, roadOut, tint);
}

float3 RingGroundAlbedo(float arc, float x, float fp, out float townOut)
{
    float w, r;
    return RingGroundAlbedoEx(arc, x, fp, townOut, w, r);
}

float3 RingGroundAlbedo(float arc, float x, float fp)
{
    float t, w, r;
    return RingGroundAlbedoEx(arc, x, fp, t, w, r);
}

#endif
