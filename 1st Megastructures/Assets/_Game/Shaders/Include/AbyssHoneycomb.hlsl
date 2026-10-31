#ifndef ABYSS_HONEYCOMB_INCLUDED
#define ABYSS_HONEYCOMB_INCLUDED

// =====================================================================================================================
// AbyssHoneycomb.hlsl - terms shared by every honeycomb tier (14.5.7 / 14.7.7), so the 14.10 (c) tier match holds by
// construction: the tier-0/1 tube meshes (Megastructure/Surface recipe 8 HoneyTube) and the tier-2 Megastructure/Lattice
// field evaluate the same functions of the world position.
//   * AbHexCellCentre: flat-top axial hex cell of a world xz point (size 760 / sqrt 3 around the S0 centre), cube rounding.
//   * AbHoneyCellTone: per-cell +/-6 % albedo by hash of the cell (R2-19: 4 920 shafts must not read as one moulded tray).
//   * AbHoneyAir: the air of the climax (R2-04 / R2-19). Two terms, applied after lighting and before the Exterior fog:
//       tube air  - the air column inside a 600 m shaft: with depth below the web tops the wall tone goes to a dim cool
//                   haze (OUT_HORIZ x AB_TUBE_AIR_K) instead of a clean gradient to black, so the near pits read as deep
//                   blue-grey air (display about 0.15-0.20) and not as black holes a few metres deep;
//       aerial    - the 600 m of air between the balcony and the terrace: a veil toward OUT_HORIZ that grows with the
//                   camera distance (amount x (1 - exp(-d / length))), so the near cells are the most contrasted ones and
//                   the field recedes into the haze instead of the near web tops being the brightest layer.
//     _HoneyAir = (aerial amount, aerial length m, tube-air depth length m, tube-air amount); (0, *, *, 0) = off. Set on the
//     HoneyTube and Lattice base materials (MaterialBuilder), every honeycomb variant inherits it; the pier, balcony and
//     Face (ConcreteBoard / ConcreteFloor in domain 2) have no such property and stay unfogged in front of the air.
// Requires Core.hlsl, AbyssCommon.hlsl and AbyssFog.hlsl.
// =====================================================================================================================

#define AB_HONEY_SQRT3      1.7320508
#define AB_HONEY_ORIGIN_X   0.0         // S0 centre (AbyssConst.SHAFT_CENTRE), as the Lattice's _LatticeOrigin
#define AB_HONEY_ORIGIN_Z   410.0
#define AB_HONEY_SIZE       438.78621   // hex size = LATTICE_PITCH 760 / sqrt 3, as the Lattice's _LatticeParams.x
#define AB_HONEY_CELL_TONE  0.06        // per-cell albedo +/-6 % (R2-19)
#define AB_TUBE_AIR_K       0.085       // tube-air colour = OUT_HORIZ x this (display about 0.14 alone)

// Flat-top axial cell of a point p (relative to the lattice origin) with cube rounding; returns the cell centre (relative).
float2 AbHexCellCentre(float2 p, float size, out int2 qr)
{
    float q = (2.0 / 3.0 * p.x) / size;
    float r = (-1.0 / 3.0 * p.x + AB_HONEY_SQRT3 / 3.0 * p.y) / size;
    float s = -q - r;
    float rq = round(q), rr = round(r), rs = round(s);
    float dq = abs(rq - q), dr = abs(rr - r), ds = abs(rs - s);
    if (dq > dr && dq > ds) rq = -rr - rs;
    else if (dr > ds) rr = -rq - rs;
    qr = int2((int)rq, (int)rr);
    return float2(size * 1.5 * rq, size * (AB_HONEY_SQRT3 * 0.5 * rq + AB_HONEY_SQRT3 * rr));
}

// Per-cell albedo factor (1 +/- 6 %) of the honeycomb cell containing world xz. origin = S0 centre (x, z), size = 438.80.
// The hash input is the axial cell index wrapped to 4096 (no seam inside the 28 km field).
float AbHoneyCellTone(float2 worldXZ, float2 origin, float size)
{
    int2 qr;
    AbHexCellCentre(worldXZ - origin, size, qr);
    float h = AbHash2(qr, int2(4096, 4096), 157u);
    return 1.0 + AB_HONEY_CELL_TONE * (2.0 * h - 1.0);
}

// Honeycomb air on a lit colour (before AbyssApplyFog). depthBelowTop: metres below the web tops of the surface point seen
// (tube walls: _TopY - y; Lattice holes: the visible wall depth). camDist: distance camera -> point.
float3 AbHoneyAir(float3 color, float depthBelowTop, float camDist, float4 air)
{
    float tube = saturate(air.w) * (1.0 - exp(-max(depthBelowTop, 0.0) / max(air.z, 1.0)));
    color = lerp(color, AB_EXT_FOG_HORIZ * AB_TUBE_AIR_K, tube);
    float aerial = saturate(air.x) * (1.0 - exp(-max(camDist, 0.0) / max(air.y, 1.0)));
    return lerp(color, AB_EXT_FOG_HORIZ, aerial);
}

#endif // ABYSS_HONEYCOMB_INCLUDED
