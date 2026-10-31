// RingHullCore.hlsl - analytic hull of beat 7 (M09, Part 14.5.7, ADR-011): the convex spherical cap (R 30 000 m, top
// 200 m below the deck at (300, 660)), the ridges, the gantry lattice and the analytic key visibility (12-step horizon
// self-shadow + gantry shadows). C# twin: Beats/Beat07_Outside/OutsideLayout.cs (CapDrop, GantryFrame), which
// places the gantry meshes with the same PCG hashes, so geometry and analytic shadows agree.
// Requires (declared by RingHull.shader in UnityPerMaterial): _HullCentre, _Ridge, _Rib, _RidgeGeom, _Gantry, _GantryB,
// _GantryC, _GantryD. Everything is in flat world space: the hull is RING_NO_BEND (it is outside the ring, 14.5.7).
#ifndef RING_HULL_CORE_INCLUDED
#define RING_HULL_CORE_INCLUDED

// Drop of the cap below its top at flat (x, z): rho^2 / (R + sqrt(R^2 - rho^2)). This is the stable form of
// R - sqrt(R^2 - rho^2) (Part 8.6: no subtraction of two numbers near R = 30 000). s = sqrt(R^2 - rho^2), so the
// outward cap normal is (dx, s, dz) / R.
float HullCapDrop(float2 xz, out float s)
{
    float2 d = xz - _HullCentre.xz;
    float rho2 = dot(d, d);
    float R = _HullCentre.w;
    s = sqrt(max(R * R - rho2, 1.0));
    return rho2 / (R + s);
}

// Ridge profile: crests every _Ridge.x (300 m) along x at x = _Ridge.w + k _Ridge.x, running along z (the spin
// direction, like hoop ribs), height _Ridge.y (14 m), C1 quartic bump H (1 - (d/W)^2)^2 of half-width W = _Ridge.z.
// slope = dh/dx.
float HullRidge(float x, out float slope)
{
    float u = (x - _Ridge.w) / _Ridge.x;
    float d = (u - floor(u + 0.5)) * _Ridge.x;
    float iw2 = 1.0 / max(_Ridge.z * _Ridge.z, 1e-3);
    float q = saturate(1.0 - d * d * iw2);
    slope = -4.0 * _Ridge.y * q * d * iw2;
    return _Ridge.y * q * q;
}

// Minor ribs between the ridges: crests every _Rib.x (75 m) at x = _Rib.w + k _Rib.x, height _Rib.y (2.5 m), same
// quartic profile of half-width _Rib.z (12 m). Shading and self-shadow only (20 m shadows under the 7 deg key): the
// texture gradient that tells 0.7 km from 3.5 km, never geometry.
float HullRib(float x, out float slope)
{
    float u = (x - _Rib.w) / max(_Rib.x, 1.0);
    float d = (u - floor(u + 0.5)) * _Rib.x;
    float iw2 = 1.0 / max(_Rib.z * _Rib.z, 1e-3);
    float q = saturate(1.0 - d * d * iw2);
    slope = -4.0 * _Rib.y * q * d * iw2;
    return _Rib.y * q * q;
}

// Geometric ridges only where the clipmap grid (2-8 m) can carry them (rho < 480 m); beyond that the ridges are
// shading only (analytic normal + self-shadow), so the coarse levels never alias a 120 m wide bump.
float HullGeomWeight(float2 xz)
{
    float rho = length(xz - _HullCentre.xz);
    return 1.0 - smoothstep(_RidgeGeom.x, _RidgeGeom.y, rho);
}

float HullGeomHeight(float2 xz)
{
    float s, slope;
    float drop = HullCapDrop(xz, s);
    return _HullCentre.y - drop + HullRidge(xz.x, slope) * HullGeomWeight(xz);
}

float HullFullHeight(float2 xz)
{
    float s, slope, ribSlope;
    float drop = HullCapDrop(xz, s);
    return _HullCentre.y - drop + HullRidge(xz.x, slope) + HullRib(xz.x, ribSlope);
}

// Gantry k (column 0..5), j (row 0..3): base point G on the cap and its local frame (ey = cap normal, ez along z).
// Rows sit at lateral -far, -near, +near, +far (_GantryD.x = far, _Gantry.w = near; ADR-011: inside the band of hull
// visible beyond the side rails). Twin of OutsideLayout.GantryFrame (C#): same PCG hashes (RingPcg2 == RingMath.Pcg2).
void HullGantryFrame(int k, int j, out float3 G, out float3 ex, out float3 ey, out float3 ez)
{
    float jx = RingU01(RingPcg2(int2(k + 701, j + 37))) - 0.5;
    float jz = RingU01(RingPcg2(int2(k + 913, j + 59))) - 0.5;
    float gx = _Gantry.x + _Gantry.y * (float)k + 2.0 * _Gantry.z * jx;
    float lateral = (j < 2 ? -1.0 : 1.0) * ((j == 0 || j == 3) ? _GantryD.x : _Gantry.w);
    float gz = _HullCentre.z + lateral + 2.0 * _GantryB.x * jz;
    float s;
    float drop = HullCapDrop(float2(gx, gz), s);
    G = float3(gx, _HullCentre.y - drop, gz);
    ey = normalize(float3(gx - _HullCentre.x, s, gz - _HullCentre.z));
    ez = normalize(float3(0.0, 0.0, 1.0) - ey * ey.z);
    ex = cross(ey, ez);
}

float HullSoftBox(float x, float a, float b, float s)
{
    float inv = 1.0 / max(s, 1e-4);
    return saturate((x - a) * inv + 0.5) * saturate((b - x) * inv + 0.5);
}

// Shadow 0..0.8 of gantry (k, j) on the surface point P for the key direction L (toward the star).
// The point is projected along L onto the gantry's local ground plane (u along ex, w along ez); with the shear
// ws = w - (Lz/Lx) u the beam's shadow is an axis-aligned box: u in [-hw - y1 kx, hw - y0 kx], ws in +-(hl + hw |c|).
// Soft edges: the star's 2.4 degree disc gives long penumbrae along the light (>= 3 m) and ~1 m across it,
// widened to the pixel footprint so thin leg shadows converge to their mean instead of aliasing.
float HullGantryShadow(float3 P, int k, int j, float3 L, float fp)
{
    float3 G, ex, ey, ez;
    HullGantryFrame(k, j, G, ex, ey, ez);
    float3 q = P - G;
    float3 ql = float3(dot(q, ex), dot(q, ey), dot(q, ez));
    float3 Ll = float3(dot(L, ex), dot(L, ey), dot(L, ez));
    if (Ll.y < 0.02 || Ll.x < 0.05) return 0.0;
    float kx = Ll.x / Ll.y;
    float kz = Ll.z / Ll.y;
    float c = Ll.z / Ll.x;
    float u = ql.x - ql.y * kx;
    float ws = (ql.z - ql.y * kz) - c * u;
    float y0 = _GantryB.y, y1 = _GantryB.z, hl = _GantryB.w, hw = _GantryC.w;
    if (u > hw + 4.0 || u < -hw - y1 * kx - 8.0 || abs(ws) > hl + 6.0) return 0.0;
    float su = max(3.0, fp);
    float sw = max(1.0, fp);
    float beam = HullSoftBox(u, -hw - y1 * kx, hw - y0 * kx, su) * HullSoftBox(ws, -hl - hw * abs(c), hl + hw * abs(c), sw);
    float legs = 0.0;
    float lh = _GantryC.z;
    float sl = max(0.3, fp);
    [unroll] for (int i = 0; i < 4; i++)
    {
        float lx = ((i & 1) != 0) ? _GantryC.x : -_GantryC.x;
        float lz = ((i & 2) != 0) ? _GantryC.y : -_GantryC.y;
        float wl = lz - c * lx;
        legs = max(legs, HullSoftBox(u, lx - lh - y0 * kx, lx + lh, su) * HullSoftBox(ws, wl - lh, wl + lh, sl));
    }
    return 0.8 * max(beam, legs);
}

// Visibility of the key at the analytic surface point above xz: 12-step horizon march toward the light over the
// full height field (cap curvature + ridges + ribs; samples from 3.6 m, quadratic spacing out to 240 m, so the 20 m
// rib shadows and the 114 m ridge shadows are both resolved), soft by the star's angular radius (0.021 rad) and the footprint,
// converging to the mean lit fraction once the ridges are sub-footprint (no shimmer at the horizon), times the
// analytic gantry shadows. L must be normalised; fp = pixel footprint (m).
float HullKeyVisibility(float2 xz, float3 L, float fp)
{
    if (L.y < 0.005) return 1.0;
    float h0 = HullFullHeight(xz);
    float m = 1.0;
    [unroll] for (int i = 0; i < 12; i++)
    {
        float u = (i + 1.0) / 12.0;
        float t = 2.0 + 238.0 * u * u;
        float2 q = xz + L.xz * t;
        float clearance = h0 + L.y * t - HullFullHeight(q);
        m = min(m, clearance / t);
    }
    float soft = 0.021 + fp / 200.0;
    float vis = smoothstep(-soft, soft, m);
    vis = lerp(0.62, vis, saturate(2.0 - fp / 20.0));   // mean lit fraction of the 300 m ridge train (114 m shadows)

    // gantry shadows fall at most ~135 m on the -x side of a gantry (73 m shadow + 60 m jitter): one column per pixel
    float kc = floor((xz.x - _Gantry.x + 180.0) / max(_Gantry.y, 1.0));
    float visG = 1.0;
    if (kc >= 0.0 && kc <= 5.0)
    {
        float3 P = float3(xz.x, h0, xz.y);
        int k = (int)kc;
        [unroll] for (int j = 0; j < 4; j++) visG *= 1.0 - HullGantryShadow(P, k, j, L, fp);
    }
    return vis * visG;
}

#endif
