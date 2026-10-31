// RingSkyArch.hlsl - the sky dome (M05): cylinder ray hit for the arch (far side of the ring), the filtered
// ground, aerial inscatter, ArchHaze, terminator night band, city lights, clouds, star, shadow squares, the far-rim
// rule of beat 5 and the outside sky of beat 7 (Part 14.2.2, 14.5.2-14.5.7, 14.7 H2/H6/H9).
#ifndef RING_SKY_ARCH_INCLUDED
#define RING_SKY_ARCH_INCLUDED

#include "RingAtmosphere.hlsl"
#include "RingGround.hlsl"

// ---------------- clouds over the far side ----------------
// Cumulus fields: fbm with octave weights saturate(2 - 2 fp / lambda) (sub-footprint octaves converge to their mean),
// arc enters through a circle mapping so the field is exactly periodic around the ring; drifts 12 m/s along the arc.
float RingCloudField(float arc, float x, float fp)
{
    float phi = RING_TWO_PI * (arc + 12.0 * _RingTime.x) / RING_CIRC;
    float3 base = float3(cos(phi) * RING_R, sin(phi) * RING_R, x);
    float sum = 0.0, wsum = 0.0, amp = 1.0, lambda = 3200.0;
    [unroll] for (int o = 0; o < 4; o++)
    {
        float w = saturate(2.0 - 2.0 * fp / lambda);
        float n = RingValueNoise3(base / lambda + o * 17.31);
        sum += amp * lerp(0.5, n, w);
        wsum += amp;
        amp *= 0.5;
        lambda *= 0.5;
    }
    return sum / wsum;
}

float RingCloudCover(float arc, float x, float fp)
{
    float v = RingCloudField(arc, x, fp);
    // clustered: a slow regional term decides where cumulus fields grow
    float phi = RING_TWO_PI * RingWrapArc(arc) / RING_CIRC;
    float region = RingPeriodicNoise(phi, x, 16000.0, 11.0);
    float th = lerp(0.70, 0.58, smoothstep(0.35, 0.75, region));
    float soft = max(0.06, fp / 6000.0);
    return smoothstep(th, th + soft, v);
}

// ---------------- cylinder intersection ----------------
// Returns t (> 0) along dir from the camera to the inner ring surface, or -1 when the ray is parallel to the axis.
float RingCylinderHit(float3 C, float3 d)
{
    float c = C.y - RING_R;
    float a = d.y * d.y + d.z * d.z;
    if (a < 1e-6) return -1.0;
    float b = 2.0 * c * d.y;
    float k = C.y * C.y - 2.0 * RING_R * C.y;          // c^2 - R^2 without cancellation
    float disc = max(b * b - 4.0 * a * k, 0.0);
    float sq = sqrt(disc);
    float q = -0.5 * (b + (b >= 0.0 ? sq : -sq));
    float t1 = q / a;
    float t2 = abs(q) > 1e-6 ? k / q : -1.0;
    return max(t1, t2);
}

// ---------------- the arch (far side of the ring) ----------------
// Lit, inscattered colour of the ring surface hit by dir (before the haze). Lighting: the current rig inside the
// squares' night band, daylight (x RingLitSideScale) outside it, exactly as the far terrain (ADR-012), so the
// clipmap edge never shows; night lights from the settlements in the band.
float3 RingArchColor(float3 C, float3 d, float t, out float xHit, out float thetaDeg, out float fp)
{
    float hy = C.y + t * d.y;
    float hz = t * d.z;
    float theta = atan2(hz, RING_R - hy);
    thetaDeg = theta * RING_RAD2DEG;
    float arc = C.z + RING_R * theta;
    xHit = C.x + t * d.x;
    // footprint: distance x pixel angle / cos(incidence) (analytic, seam-free)
    float3 nHit = float3(0.0, RING_R - hy, -hz) * (1.0 / RING_R);
    float cosInc = abs(dot(d, nHit));
    fp = t * _RingCam.w / max(cosInc, 0.05);

    float town, water, road;
    float3 tint;
    float3 alb = RingGroundAlbedoEx(arc, xHit, fp, town, water, road, tint);
    float cloud = RingCloudCover(arc, xHit, fp);
    // cloud shadows on the far ground (clouds float ~1.5 km above it; the star sits 26 deg off the local zenith)
    float cloudShadow = RingCloudCover(arc - 650.0, xHit + 1300.0, fp * 1.5);

    float lit = RingTerminatorLit(thetaDeg);
    float k = RingLitSideScale();
    float atten = RingKeyAtten(float2(xHit, arc));
    float3 dayKey = PAL_KeyLight * (3.0 * 0.438 * RING_LIGHT_CAL);
    float3 day = (RingGroundLightDay() - dayKey * (0.55 * cloudShadow + (1.0 - atten))) * k;
    float3 now = RingGroundLightNow() - _KeyLightColor.rgb * (0.438 * (1.0 - atten));
    float3 light = lerp(now, day, lit);
    float3 ground = alb * light;
    // water: a darker body with a cool sheen of the sky it mirrors
    float3 sheen = lerp(_AmbSky.rgb * 0.25, PAL_SkyDayMid * (0.10 * k), lit);
    ground = lerp(ground, alb * light * 0.65 + sheen, water);
    float3 cloudNow = _KeyLightColor.rgb * 0.5 + _AmbSky.rgb * 0.55 + _RingFillLight.rgb * 0.3;
    float3 cloudDay = (PAL_KeyLight * 1.5 + PAL_DayTriSky * 0.55 + PAL_ArchFill * 0.3) * (RING_LIGHT_CAL * k);
    ground = lerp(ground, PAL_CloudWhite * lerp(cloudNow, cloudDay, lit), cloud * 0.92);

    // the twilight rim of the lit side glows ArchGlow just beyond the terminator
    float D = _SunsetParams.z;
    float at = abs(thetaDeg);
    float rim = (1.0 - smoothstep(D + 3.0, D + 16.0, at)) * lit * saturate(D / 10.0);
    ground = lerp(ground, ground * 0.55 + PAL_ArchGlow * (0.9 * k), rim * 0.5);

    // night lights of the settlements and the arterial roads inside the band (energy-preserving at any footprint),
    // with a slow +-2 % brightness wave travelling along the ring (period 60 s)
    float phi = RING_TWO_PI * RingWrapArc(arc) / RING_CIRC;
    float wave = 1.0 + 0.02 * sin(RING_TWO_PI * _RingTime.y / 60.0 - 20.0 * phi);
    float3 city = (tint * (0.30 * town) + PAL_CityWarm * (0.035 * road)) * (1.0 - cloud * 0.8);
    ground += min(city * (_Lamps.y * wave), 6.0) * (1.0 - lit);

    // aerial inscatter of the far side's own atmosphere over the path length t (keeps contrast, adds blue)
    float T = exp(-t / 34000.0);
    float3 inscatter = lerp(PAL_SkyDayZenith * 1.2, PAL_SkyDayMid, 0.35) * k;
    inscatter = lerp(PAL_SkyNightMid * 0.6 + PAL_ArchGlow * 0.15, inscatter, lit);
    return lerp(inscatter, ground, T);
}

// ---------------- outside sky (beat 7 and the view through the tunnel mouth) ----------------
float3 RingOutsideStars(float3 d, float fpRad)
{
    // cube-face cell hashing, about 1 500 faint stars over the sphere (6 faces x 40 x 40 cells, 16 % filled)
    float3 ad = abs(d);
    int face; float2 uv; float ma;
    if (ad.x >= ad.y && ad.x >= ad.z) { face = d.x > 0 ? 0 : 1; uv = d.yz / ad.x; ma = ad.x; }
    else if (ad.y >= ad.z) { face = d.y > 0 ? 2 : 3; uv = d.xz / ad.y; ma = ad.y; }
    else { face = d.z > 0 ? 4 : 5; uv = d.xy / ad.z; ma = ad.z; }
    const float N = 40.0;
    float2 g = (uv * 0.5 + 0.5) * N;
    int2 ci = (int2)floor(g);
    float3 acc = 0.0;
    float sigma = max(fpRad, 0.00025) * N * 0.5;  // gaussian radius in cell units (about 1 px, so >= 2 px wide)
    [unroll] for (int j = -1; j <= 1; j++)
    [unroll] for (int i = -1; i <= 1; i++)
    {
        int2 c = ci + int2(i, j);
        uint h = RingPcg3(int3(c, face));
        if (RingU01(h) > 0.16) continue;
        float2 p = float2(c) + RingHash22(int2(h & 0xffff, (h >> 16) + face));
        float2 dd = g - p;
        float r2 = dot(dd, dd) / max(sigma * sigma, 1e-6);
        float b = 0.015 + 0.25 * pow(RingU01(RingPcg(h + 3u)), 6.0);
        float3 tint = lerp(float3(0.75, 0.82, 1.0), float3(1.0, 0.86, 0.70), RingU01(RingPcg(h + 9u)));
        acc += tint * b * exp(-r2);
    }
    return acc;
}

float3 RingOutsideSky(float3 d, float fpDeg)
{
    float3 c = PAL_SpaceBlack;
    c += RingOutsideStars(d, fpDeg * RING_DEG2RAD);
    float3 w = _StarDirOut.xyz;
    float r = RingAngle(d, w) * RING_RAD2DEG;
    float k = r / 1.6;
    // corona with a steeper far wing (beyond 4 deg), so the swarm bands sit on near-black space
    c += PAL_StarOutside * (6.0 / (1.0 + k * k)) * lerp(1.0, exp(-(r / 5.0) * (r / 5.0)), smoothstep(2.0, 4.0, r));
    c += RingStarDisc(d, w, 1.2, fpDeg, PAL_StarOutside);
    return min(c, 200.0);
}

// ---------------- full sky ----------------
float _RingForceOutside;   // capture-only (orbital stills): 1 = outside sky for any camera position
float3 RingSkyDome(float3 d)
{
    float3 C = _RingCam.xyz;
    float fpDeg = _RingCam.w * RING_RAD2DEG;
    float eDeg = asin(clamp(d.y, -1.0, 1.0)) * RING_RAD2DEG;

    float3 atm = RingSkyAtmosphere(d);
    float3 horizon = RingFogColour(d, 1.0);          // fully fogged geometry along this heading (terrain edge, horizon)
    float3 col = horizon;
    float t = RingCylinderHit(C, d);
    if (t > 0.0 && eDeg >= 0.0)
    {
        float xHit, thetaDeg, fp;
        float3 arch = RingArchColor(C, d, t, xHit, thetaDeg, fp);
        float tEdge = RingTerrainEdgeDistance(d);
        float hz = RingArchHaze(t, eDeg, tEdge);
        // the same aerial desaturation as the geometry, over the same effective path as the haze
        float sinE = sin(eDeg * RING_DEG2RAD);
        float de = lerp(min(t, tEdge), RingArchHazeDistance(t, sinE), smoothstep(14.7, 19.7, eDeg));
        arch = lerp(RingAerial(arch, de), arch, smoothstep(14.7, 19.7, eDeg));
        // haze colour: the fog colour along the heading near the horizon (it meets the terrain edge), the sky's own
        // colour higher up, so the upper sky reads blue through the haze (14.5.2 acceptance 3)
        float3 hzc = lerp(RingFogColour(d, hz), atm, 0.6 * smoothstep(16.0, 45.0, eDeg));
        float3 archC = lerp(arch, hzc, hz);
        // far-rim rule (beat 5): hits beyond the right rim wall return the wall fog colour (WallNight fogged 100 %)
        if (_RingSet.z > 0.5)
        {
            float wallW = saturate((xHit - _RingSet.w) / max(1.5 * fp, 1e-3) + 0.5);
            archC = lerp(archC, horizon, wallW);
        }
        col = archC;
    }

    // star corona and disc (inside star), faded by the visible fraction, then the shadow squares in front
    float V = _SkyParams.z;
    float nightK = 1.0 - saturate(_SkyParams.x - 1.0);
    col += RingStarCorona(d, _StarDir.xyz) * (0.15 + 0.85 * V) * nightK;
    float3 tint = lerp(PAL_StarCore, lerp(PAL_StarCore, PAL_DuskKey, 0.45), saturate(1.0 - V));
    col += RingStarDisc(d, _StarDir.xyz, 1.2, fpDeg, tint);

    // shadow squares: opaque SquareSlate silhouettes with a trace of the air in front (14.5.4 KS05: luma < 40)
    float alpha = _SunsetParams.y;
    if (alpha > 0.0 && dot(d, _StarDir.xyz) > 0.35)
    {
        float2 p = RingStarPlane(d, _StarDir.xyz);
        float sq = RingSquaresCoverage(p, _SunsetParams.x, fpDeg) * alpha;
        float3 sqCol = PAL_SquareSlate + atm * 0.012;
        col = lerp(col, sqCol, sq);
    }

    // outside sky: inside the wall (x 114..234) rays with dir.x > 0 see outside; beyond x 234 every ray does
    float outW = 0.0;
    if (C.x >= 234.0) outW = 1.0;
    else if (C.x > 114.0) outW = smoothstep(-0.05, 0.05, d.x);
    outW *= _RingSet.y;
    if (_RingForceOutside > 0.5) outW = 1.0;      // orbital stills: the camera is in space wherever it is
    if (outW > 0.0) col = lerp(col, RingOutsideSky(d, fpDeg), outW);

    return min(col, 200.0);
}

#endif
