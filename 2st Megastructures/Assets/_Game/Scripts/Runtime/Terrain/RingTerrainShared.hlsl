// RingTerrainShared.hlsl - ONE source for the code shared by RingTerrain.shader (M02), RingWater.shader (M03) and
// RingGrass.shader (M04 tufts and flowers). Owner: BG terrain. It lives in the terrain script folder because that is
// the folder this owner holds; shaders include it as "Assets/_Game/Scripts/Runtime/Terrain/RingTerrainShared.hlsl".
// Requires RingLitCore.hlsl (RingCommon, RingBend, RingAtmosphere, RingGround) to be included first. No material
// properties are referenced here (the UnityPerMaterial CBUFFER of each shader stays its own).
#ifndef RING_TERRAIN_SHARED_INCLUDED
#define RING_TERRAIN_SHARED_INCLUDED

// ---------------- constants (MASTER_PROMPT 14.4 / 14.5.2 / 14.5.5, DEVELOPMENT.md ADR-006) ----------------
#define RING_WATER_Y        (-2.2)     // river surface y (14.5.2)
#define RING_WATERLINE_D    18.0       // waterline half-width (36 m river, ADR-006)
#define RING_RIVER_W        36.0       // visible river width
#define RING_FLOW           0.6        // water flow speed m/s (14.2.6 M03)
#define RING_TIME_WRAP      7200.0     // _RingTime.x wraps at 7200 s (DEVELOPMENT.md uniform table)
#define RING_BRIDGE_Z0      120.0      // bridge deck z range and half width (14.4 P2, 14.5.2)
#define RING_BRIDGE_Z1      180.0
#define RING_BRIDGE_HALFW   2.5
#define RING_GRASS_TIP_LIFT 1.08       // grass tips = terrain near detail x 1.08 (14.5.2; TerrainConfig.GrassTipLift)

// ---------------- the plain clip (14.5.5): x >= 114 when _Set >= 1 ----------------
// Wall x from the global contract, guarded against an unset global at startup.
float RingWallX() { return _RingSet.w > 1.0 ? _RingSet.w : 114.0; }
bool RingPlainClipOn() { return _RingSet.x >= 0.5; }

// Vertex side: collapse x onto the wall face (quads beyond the wall become zero-area slivers).
float RingPlainClampX(float x) { return RingPlainClipOn() ? min(x, RingWallX()) : x; }

// Pixel side: discard everything with flat x beyond the wall face (all passes).
void RingPlainClip(float flatX)
{
    if (RingPlainClipOn() && flatX > RingWallX() + 0.002) clip(-1.0);
}

// ---------------- path ceiling of the coarse clipmap levels (M02; C# twin ClipmapLayout.PathCeil) ----------------
// A coarse quad (morphed size e) that straddles a path strip interpolates RingHeight's trench walls across the strip
// and would bury it. Every vertex within reach of a strip (strip and kerb half-width 1.64 m plus one quad diagonal:
// 1.64 + 1.414 e < 1.7 + 1.5 e) is therefore held at or below 0 (the strips lie at +0.012 m and more); beyond that band
// the ceiling rises 2.25 : 1, so it only trims the walls next to the band.
// * Continuous in e (no pop): its weight ramps from 0 at e = 2 m to 1 at e = 3 m. Up to e = 3.08 m the reach stays
//   below 6 m, inside RingHeight's flat zone (PathDist < 6 m, height <= 0), so the strip is safe at every weight.
// * Crack free: a fully morphed fine edge and the coarse inner edge have the same e, hence the same ceiling.
// * Slow: e moves by at most 1 / 7 m per metre of camera travel (MorphWidth), the band edge by 1.5 x that, a trimmed
//   vertex by 2.25 x 1.5 / 7 = 0.48 m per metre: at most 4.8 mm per cm of camera travel (TerrainConfig.PathCeil*).
// * Only the segments that carry a strip mesh over the terrain: the walk legs RING_PATH[0..8] (main strip, underpass,
//   plain legs), the footbridge cross path [11] and the belvedere and pavilion spurs [12], [13]. Segments 9 and 10 run
//   beyond the wall face (x > 114), where the terrain is clipped in every set that has a path there (tunnel, outside).
#if RING_PATH_SEGS != 14
#error RingTerrainShared.hlsl: RingStripDist assumes the 14 RingWorld.PathSegments (update its index ranges)
#endif
#define RING_CEIL_SLOPE   2.25    // ramp beyond the zero band (TerrainConfig.PathCeilSlope)
#define RING_CEIL_E0      2.0     // weight 0 at this morphed spacing (m)
#define RING_CEIL_E1      3.0     // weight 1 from this morphed spacing on (m)
#define RING_CEIL_BAND0   1.7     // zero band: BAND0 + BANDE x e (m)
#define RING_CEIL_BANDE   1.5
// largest land term of RingHeight (noise 6 + 1.5, hill, flank 22): beyond band + HMAX / SLOPE the ceiling never binds
#define RING_CEIL_HMAX    (7.5 + RING_HILL.w + 22.0)

float RingStripDist(float2 p)
{
    float d = 1e9;
    [unroll] for (int i = 0; i < 9; i++) d = min(d, RingSegDist(p, RING_PATH[i]));
    [unroll] for (int j = 11; j < 14; j++) d = min(d, RingSegDist(p, RING_PATH[j]));
    return d;
}

float RingCeilWeight(float e) { return saturate((e - RING_CEIL_E0) * (1.0 / (RING_CEIL_E1 - RING_CEIL_E0))); }

float RingCeilBand(float e) { return RING_CEIL_BAND0 + RING_CEIL_BANDE * e; }

// Height h limited by the ceiling at strip distance sd, morphed spacing e, weight cw = RingCeilWeight(e).
float RingPathCeil(float h, float sd, float e, float cw)
{
    float c = RING_CEIL_SLOPE * max(sd - RingCeilBand(e), 0.0);
    return lerp(h, min(h, c), cw);
}

// True when the ceiling can bind anywhere within radius r of a point whose strip distance is sd (cheap early-out for
// the extra RingStripDist evaluations of the normal samples).
bool RingCeilNear(float sd, float r, float e)
{
    return sd - r < RingCeilBand(e) + RING_CEIL_HMAX * (1.0 / RING_CEIL_SLOPE);
}

// ---------------- time ----------------
// Phase 2 pi n t / 7200 with n = round(omega 7200 / 2 pi): the nearest frequency to omega that is exactly periodic with
// the wrap of _RingTime.x, so nothing jumps when the animation clock wraps (Part 10 #31). 0.9 rad/s -> n = 1031.
float RingWrapPhase(float omega, float t)
{
    float n = max(round(omega * (RING_TIME_WRAP / RING_TWO_PI)), 1.0);
    return RING_TWO_PI * frac(n * t / RING_TIME_WRAP);
}

// ---------------- filtering ----------------
// Octave weight of a pattern of wavelength lambda under footprint fp (14.7 H2 rule: saturate(2 - 2 fp / lambda)).
float RingOctaveW(float lambda, float fp) { return saturate(2.0 - 2.0 * fp / max(lambda, 1e-4)); }

// Ground footprint of a fragment: the isotropic pixel footprint, or the anisotropic one (largest screen-space
// derivative of the flat position) when it is larger, so grazing views converge to the mean instead of aliasing.
float RingSurfaceFootprint(float2 flatXZ, float isoFootprint)
{
    float2 dx = ddx(flatXZ), dy = ddy(flatXZ);
    float an = sqrt(max(max(dot(dx, dx), dot(dy, dy)), 0.0));
    return max(isoFootprint, an);
}

// ---------------- terrain albedo (M02): RingGroundAlbedo + near detail faded above 1 m footprint ----------------
// The grass tuft tips (M04) use exactly this colour (x 1.08) at the footprint the terrain has at the tuft, and from
// 16.5 m on the tufts are lit like the terrain with a visible mean of exactly this colour, so the 22-28 m grass fade is
// invisible (14.5.2).
float3 RingTerrainAlbedo(float2 p, float fp)
{
    float3 c = RingGroundAlbedo(p.y, p.x, fp);          // arc = flat z, axial = x
    float nearW = 1.0 - smoothstep(0.5, 1.0, fp);
    if (nearW > 0.0)
    {
        float n1 = RingValueNoise(p * (1.0 / 3.1) + float2(11.3, 4.2)) - 0.5;    // fresh / dry grass patches
        float n2 = RingValueNoise(p * (1.0 / 0.93) + float2(4.7, 19.1)) - 0.5;   // mottling
        float n3 = RingValueNoise(p * (1.0 / 0.29) + float2(9.1, 2.6)) - 0.5;    // fine mottling
        float w1 = RingOctaveW(3.1, fp), w2 = RingOctaveW(0.93, fp), w3 = RingOctaveW(0.29, fp);
        float mott = n2 * 0.10 * w2 + n3 * 0.07 * w3;
        float hue = n1 * w1;
        c *= 1.0 + nearW * (mott + hue * float3(0.12, 0.04, -0.10));
    }
    return max(c, 0.0);
}

// ---------------- water (M03) ----------------
float3 RingWaterBody(float depth) { return lerp(PAL_TealRiver, PAL_WaterDeep, saturate(depth / 3.2)); }

float RingSchlick02(float cosTheta)
{
    float m = 1.0 - saturate(cosTheta);
    float m2 = m * m;
    return 0.02 + 0.98 * m2 * m2 * m;
}

// Ripple waves: (m, kv, slope amplitude A k). Along-flow wavenumber ku = 2 pi m / 43.2 m; 0.6 m/s x 7200 s = 4320 m
// = 100 x 43.2 m, so the advected pattern is exactly periodic with the clock wrap. Layer 1 = first three (4.8 - 2.2 m),
// layer 2 = last three (1.05 - 0.6 m).
#define RING_RIPPLE_N 6
#define RING_RIPPLE_P 43.2
static const float3 RING_RIPPLES[RING_RIPPLE_N] =
{
    float3(9.0, 0.55, 0.050), float3(14.0, -0.90, 0.045), float3(20.0, 1.30, 0.040),
    float3(41.0, -2.60, 0.035), float3(57.0, 3.40, 0.030), float3(72.0, -5.10, 0.025)
};

// Slope (dh/du, dh/dv) of the ripples in river coordinates (u along the flow, v across), flattened by the footprint
// fp; the slope variance that is filtered away is added to a2 (GGX alpha^2), so distant water turns into a stable
// broad highlight instead of sparkling (research 07 section 8).
float2 RingWaterRipples(float u, float v, float t, float fp, inout float a2)
{
    float uf = u - RING_FLOW * t;
    float2 slope = float2(0.0, 0.0);
    float lost = 0.0;
    [unroll] for (int i = 0; i < RING_RIPPLE_N; i++)
    {
        float3 w = RING_RIPPLES[i];
        float ku = RING_TWO_PI * w.x / RING_RIPPLE_P;
        float kv = w.y;
        float k = sqrt(ku * ku + kv * kv);
        float ph = RING_TWO_PI * frac(w.x * uf / RING_RIPPLE_P) + kv * v + 1.7 * i;
        float att = 1.0 - smoothstep(0.6, 2.2, k * fp);
        slope += (w.z * cos(ph) * att / k) * float2(ku, kv);
        lost += (1.0 - att * att) * w.z * w.z * 0.5;
    }
    a2 += 2.0 * lost;
    return slope;
}

// GGX specular term in the URP form (BRDF.hlsl DirectBRDFSpecular), guarded: d >= 1e-5.
float RingGgxSpec(float NoH, float LoH, float a2)
{
    float d = NoH * NoH * (a2 - 1.0) + 1.00001;
    float a = sqrt(max(a2, 0.0));
    return a2 / (d * d * max(0.1, LoH * LoH) * (4.0 * a + 2.0));
}

// Analytic occlusion of the sky reflection under the bridge deck (the deck hangs over the water).
float RingBridgeReflOcc(float2 p)
{
    float inZ = 1.0 - smoothstep(0.0, 3.0, max(RING_BRIDGE_Z0 - p.y, p.y - RING_BRIDGE_Z1));
    float underDeck = 1.0 - smoothstep(RING_BRIDGE_HALFW, RING_BRIDGE_HALFW + 1.5, abs(p.x));
    return 1.0 - 0.85 * inZ * underDeck;
}

// Additive specular of the water: sky reflection RingSkyColor(reflect) x Schlick (F0 0.02) plus the key glint, all in
// render (bent) space; clamped (highlight clamp 6). nB = bent normal, V = toward the camera, upB = bent local up,
// L = bent key direction, lightCol = key colour x attenuation, a2 = GGX alpha^2.
float3 RingWaterSpecular(float3 nB, float3 V, float3 upB, float3 L, float3 lightCol, float a2, float reflOcc)
{
    float NoV = saturate(dot(nB, V));
    float F = RingSchlick02(NoV);
    float3 R = reflect(-V, nB);
    float ru = dot(R, upB);
    R = R + upB * max(0.02 - ru, 0.0);                    // never sample below the local horizon
    R = R / max(length(R), 1e-4);
    float3 refl = RingSkyColor(R) * (F * reflOcc);
    float3 hv = L + V;
    float3 H = hv / max(length(hv), 1e-4);
    float NoL = saturate(dot(nB, L));
    float NoH = saturate(dot(nB, H));
    float LoH = saturate(dot(L, H));
    float3 glint = lightCol * (RingGgxSpec(NoH, LoH, a2) * RingSchlick02(LoH) * NoL);
    return min(refl + min(glint, 6.0), 6.0);
}

// Periodic value noise along x (period in lattice cells), plain along y; quintic interpolation (foam advection).
float RingValueNoisePX(float2 x, int periodX)
{
    float2 f = floor(x);
    float2 t = x - f;
    float2 u = t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    // lattice x wrapped into [0, P) with float arithmetic (exact for integer-valued f.x well below 2^24; no integer
    // modulus, which is slow on GPUs)
    float P = (float)periodX;
    float wx = f.x - P * floor(f.x / P);
    int x0 = (int)wx;
    int x1 = (x0 + 1 >= periodX) ? 0 : x0 + 1;
    int y0 = (int)f.y;
    float a = RingHash21(int2(x0, y0)), b = RingHash21(int2(x1, y0));
    float c = RingHash21(int2(x0, y0 + 1)), d = RingHash21(int2(x1, y0 + 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Analytic river coverage used by the terrain where the clipmap is too coarse to carve the 36 m river (spacing >= 4 m):
// exact box-filtered band of the waterline width, faded where the land rises above the water (dry stretches).
float RingFarWaterCover(float2 p, float rd, float fp, float spacing, float2 ramp, float setv)
{
    float farW = smoothstep(ramp.x, ramp.y, spacing);
    if (farW <= 0.0 || rd > RING_WATERLINE_D + fp) return 0.0;
    float hLand = RingHeight(p, setv) + RingRiverCarve(rd);   // land height before the carve
    float wet = saturate((1.0 - hLand) * 2.0);                // water at the centre while the land is below +1 m
    return RingFilteredLine(rd, RING_RIVER_W, fp) * wet * farW;
}

#endif
