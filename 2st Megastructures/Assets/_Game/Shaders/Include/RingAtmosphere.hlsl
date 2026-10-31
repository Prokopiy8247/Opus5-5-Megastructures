// RingAtmosphere.hlsl - the ONE source of truth for sky colour and fog (Part 14.2.2, 14.7 H2/H4).
// Terrain, water, ark, wall and hull call RingFog; the sky dome calls RingSkyAtmosphere and, for the arch only,
// replaces RingFog by RingArchHaze. The fog colour is the atmosphere colour at 3 degrees elevation along the view
// heading, so the horizon of the sky and the fog can never disagree.
#ifndef RING_ATMOSPHERE_INCLUDED
#define RING_ATMOSPHERE_INCLUDED

#include "RingCommon.hlsl"

// Precise angle between two unit vectors (rad); stable for tiny angles, unlike acos(dot).
float RingAngle(float3 a, float3 b)
{
    return atan2(length(cross(a, b)), dot(a, b));
}

// ---------------- sky gradient ----------------
float RingTowardStar(float3 dir)
{
    // azimuthal closeness of dir to the star heading, 0 (away) .. 1 (toward); 0.5 where the heading is undefined
    float2 h = dir.xz;
    float hl = length(h);
    float2 sh = normalize(_StarDir.xz + float2(1e-6, 0.0));
    float c = dot(h / max(hl, 1e-5), sh);
    float t = 0.5 + 0.5 * c;
    t = t * t;
    return lerp(0.25, t, smoothstep(0.0, 0.2, hl));
}

float3 RingGradient3(float eDeg, float3 hor, float3 mid, float3 zen)
{
    float e = clamp(eDeg, 0.0, 90.0);
    float3 lo = lerp(hor, mid, smoothstep(0.0, 35.0, e));
    return lerp(lo, zen, smoothstep(35.0, 90.0, e));
}

// Atmosphere only (gradient + Mie lobe), linear HDR. dir must be normalised.
float3 RingSkyAtmosphere(float3 dir)
{
    float eDeg = asin(clamp(dir.y, -1.0, 1.0)) * RING_RAD2DEG;
    float tw = RingTowardStar(dir);
    float3 day = RingGradient3(eDeg, lerp(PAL_SkyDayHorAway, PAL_SkyDayHorToward, tw), PAL_SkyDayMid, PAL_SkyDayZenith);
    float3 dusk = RingGradient3(eDeg, lerp(PAL_SkyDuskHorAway, PAL_SkyDuskHorToward, tw), PAL_SkyDuskMid, PAL_SkyDuskZenith);
    float3 night = RingGradient3(eDeg, PAL_SkyNightHor, PAL_SkyNightMid, PAL_SkyNightZenith);
    float st = _SkyParams.x;
    float3 c = st < 1.0 ? lerp(day, dusk, st) : lerp(dusk, night, saturate(st - 1.0));

    // warm Mie lobe toward the star: A exp(-(angle / 14 deg)^0.8)
    float ang = RingAngle(dir, _StarDir.xyz) * RING_RAD2DEG;
    float lobe = exp(-pow(max(ang, 0.0) / 14.0, 0.8));
    float A = lerp(0.55, 0.9, saturate(st)) * (1.0 - saturate(st - 1.0));
    float3 lobeCol = lerp(PAL_Aureole, PAL_DuskKey, saturate(st) * 0.7);
    float V = _SkyParams.z;
    c += lobeCol * (A * lobe * (0.25 + 0.75 * V));
    return c;
}

// Star corona around the inside star (no disc): 6 / (1 + (r / 1.6 deg)^2), faded with the visible fraction.
float3 RingStarCorona(float3 dir, float3 starDir)
{
    float r = RingAngle(dir, starDir) * RING_RAD2DEG;
    float k = r / 1.6;
    return PAL_StarCore * (6.0 / (1.0 + k * k));
}

// Sky colour used for reflections: atmosphere plus a softened corona (never the disc: no fireflies).
float3 RingSkyColor(float3 dir)
{
    float3 c = RingSkyAtmosphere(dir);
    float V = _SkyParams.z;
    c += RingStarCorona(dir, _StarDir.xyz) * (0.15 + 0.85 * V) * 0.25 * (1.0 - saturate(_SkyParams.x - 1.0));
    return min(c, 40.0);
}

// ---------------- fog ----------------
float3 RingFogHeadingColor(float3 viewDir)
{
    float2 h = viewDir.xz;
    float hl = length(h);
    float2 hn = hl > 1e-5 ? h / hl : float2(0.0, 1.0);
    const float c3 = 0.99862953, s3 = 0.05233596; // cos / sin 3 deg
    float3 d = float3(hn.x * c3, s3, hn.y * c3);
    float3 c = RingSkyAtmosphere(d);
    c = lerp(c, _FogTint.rgb, _FogTint.a);
    return c;
}

// Fog height factor (ADR-012): the haze thins with height (density exp(-h / H)); a point at unbent height y seen from
// the floor is reached through the mean density (1 - exp(-y / H)) / (y / H). invH = 1 / H (0: off, beats 5-7, so the
// rim wall still dissolves into the fog with height).
float RingFogHeightFactor(float y)
{
    float k = max(y, 0.0) * _FogExtra.y;
    return k > 1e-4 ? (1.0 - exp(-k)) / k : 1.0;
}

// Fog amount for an effective distance de (m) and unbent end-point height y (m), sigma and mist k given:
// 1 - exp(-(sigma de)^2 (1 + k exp(-max(y,0)/18)) - de / L)   (14.2.2 plus the weak linear aerial term of ADR-012)
float RingFogAmountS(float de, float y, float sigma, float mistK)
{
    float sd = sigma * de;
    float mist = 1.0 + mistK * exp(-max(y, 0.0) / 18.0);
    return 1.0 - exp(-(sd * sd * mist + de * _FogExtra.x));
}

// Fog amount of the current beat for a bent-space distance d and unbent height y.
float RingFogAmount(float d, float y)
{
    return RingFogAmountS(d * RingFogHeightFactor(y), y, _FogParams.x, _FogParams.y);
}

// Aerial perspective (Part 8.4, ADR-012): distance desaturation that starts in the midground (1 - exp(-d / 600 m)),
// so saturation falls layer by layer (14.5.2 acceptance 6) long before the exponential-squared fog builds up.
float3 RingAerial(float3 colour, float de)
{
    float a = (1.0 - exp(-de / 600.0)) * _FogExtra.z;
    return lerp(colour, RingLuma(colour).xxx, a);
}

// Fog colour along viewDir for fog amount f (desaturated by 0.25 f, 14.2.2).
float3 RingFogColour(float3 viewDir, float f)
{
    float3 fc = RingFogHeadingColor(viewDir);
    return lerp(fc, RingLuma(fc).xxx, _FogParams.z * f);
}

// Applies the shared fog. positionBentWS = the bent world position of the fragment, worldY = its unbent height.
float3 RingFog(float3 colour, float3 positionBentWS, float worldY)
{
    float3 v = positionBentWS - _RingCam.xyz;
    float d = length(v);
    float3 viewDir = v / max(d, 1e-4);
    float de = d * RingFogHeightFactor(worldY);
    float f = RingFogAmountS(de, worldY, _FogParams.x, _FogParams.y);
    float3 c = lerp(RingAerial(colour, de), RingFogColour(viewDir, f), f);
    // beat 7: no fog, distance darkening toward SpaceBlack (exp(-d / 25 000))
    float dark = exp(-d / 25000.0);
    c = lerp(c, lerp(PAL_SpaceBlack, c, dark), _FogParams.w);
    return c;
}

// ---------------- arch haze (replaces RingFog for the arch, 14.7 H2, ADR-012) ----------------
float _RingDebugHazeOff;   // QA only (14.5.2 acceptance 8): 1 forces the arch haze to 0

// Thickness of the near-floor haze layer: a ray of elevation e leaves it after RING_HAZE_LAYER / sin e metres. 2 079 m =
// 8 192 m x sin 14.67 deg, so the layer limit meets the terrain edge exactly where the clipmap ends (yaw 0) and the
// arch above it is seen through a path that shortens with elevation: the ArchHaze curve of 14.7 H2 falls out of the
// fog itself (with sigma 1e-4: 0.72 at 14.7 deg, 0.51 at 20, 0.29 at 30, 0.19 at 40, 0.09 at 90 deg; monotone).
#define RING_HAZE_LAYER 2079.0

// Effective haze distance of an arch hit at distance t along a ray of elevation sin e (the day haze sigma _FogExtra.w,
// which equals the beat 1-3 fog sigma, so the arch and the terrain edge agree in the valley).
float RingArchHazeDistance(float t, float sinE)
{
    return min(t, RING_HAZE_LAYER / max(sinE, 1e-3));
}

// Bent distance from the camera to the terrain clipmap edge along the horizontal heading of d (the clipmap is a square of
// half-extent 8 192 m around the camera; along z the ring bends, so the edge point lies on the chord 2 R sin(z / 2R)).
float RingTerrainEdgeDistance(float3 d)
{
    float2 h = d.xz;
    float hl = length(h);
    if (hl < 1e-4) return 1e9;
    h /= hl;
    float rho = 8192.0 / max(max(abs(h.x), abs(h.y)), 1e-4);
    float x = rho * h.x;
    float cz = 2.0 * RING_R * sin(0.5 * rho * h.y / RING_R);
    return sqrt(x * x + cz * cz);
}

// Arch haze for a hit at distance t, elevation eDeg, terrain edge distance tEdge along the heading: the current fog on
// the true distance up to the terrain edge (so it meets the fogged clipmap edge in every direction, and stays constant
// above it), blended over 14.7..19.7 deg into the layer haze of the day sigma (which keeps the arch legible in beats 4
// and 5, whose geometry fog is far thicker), then the night lift 1 - 0.5 N smoothstep(16, 32, e). Monotone above the
// terrain edge (14.7 H2 acceptance 8).
float RingArchHaze(float t, float eDeg, float tEdge)
{
    if (_RingDebugHazeOff > 0.5) return 0.0;
    float sinE = sin(clamp(eDeg, 0.0, 90.0) * RING_DEG2RAD);
    float low = RingFogAmountS(min(t, tEdge), 0.0, _FogParams.x, _FogParams.y);
    float high = RingFogAmountS(RingArchHazeDistance(t, sinE), 0.0, _FogExtra.w, 0.8);
    float hz = lerp(low, high, smoothstep(14.7, 19.7, eDeg));
    hz *= 1.0 - _SkyParams.w * smoothstep(16.0, 32.0, eDeg);
    return hz;
}

// Capsule SDF along X: total length L, width W, centre c (xz plane)
float RingCapsuleSdf(float2 p, float2 c, float L, float Wd)
{
    float2 q = abs(p - c);
    q.x = max(q.x - 0.5 * (L - Wd), 0.0);
    return length(q) - 0.5 * Wd;
}

// Analytic key attenuation from the ship shadow (beat 3) and the two cloud shadows (beat 2); flat xz in metres.
float RingKeyAtten(float2 xz)
{
    float a = 1.0;
    if (_ShipShadow.z > 0.0)
    {
        float sdf = RingCapsuleSdf(xz, _ShipShadow.xy, 3000.0, 620.0);
        float mask = 1.0 - smoothstep(-86.0, 86.0, sdf);            // penumbra 172 m
        a *= 1.0 - 0.62 * mask * _ShipShadow.z;
    }
    if (_CloudShadowA.z > 0.0)
    {
        float m = 1.0 - smoothstep(-300.0, 300.0, RingCapsuleSdf(xz, _CloudShadowA.xy, 900.0, 350.0)); // penumbra 600 m
        a *= 1.0 - m * _CloudShadowA.z;
    }
    if (_CloudShadowB.z > 0.0)
    {
        float m = 1.0 - smoothstep(-300.0, 300.0, RingCapsuleSdf(xz, _CloudShadowB.xy, 900.0, 350.0));
        a *= 1.0 - m * _CloudShadowB.z;
    }
    return a;
}

// ---------------- ground lighting shared by the terrain and the arch (ADR-012) ----------------
// Diffuse irradiance of flat ring ground (normal = local up, NdotL constant on a ring): the key (0.438), the arch fill
// (elevation 74 deg: 0.961) and the trilight sky term. "Now" is the current rig, "Day" the beat-2 rig. The far side
// beyond the squares' terminator stays in daylight; the arch shows lerp(now, day x RingLitSideScale, lit) and the far
// terrain adds the same daylight beyond the terminator (RingTerrain), so the terrain edge never shows a seam.
float3 RingGroundLightNow() { return _KeyLightColor.rgb * 0.438 + _RingFillLight.rgb * 0.961 + _AmbSky.rgb; }
float3 RingGroundLightDay() { return (PAL_KeyLight * (3.0 * 0.438) + PAL_ArchFill * (0.55 * 0.961) + PAL_DayTriSky) * RING_LIGHT_CAL; }
// The sunlit far side seen at night: art-directed exposure compensation (the night EV +1.6 would blow it out; 14.5.4
// acceptance (3) wants the lit arch at luma 150-230).
float RingLitSideScale() { return lerp(1.0, 0.50, _SkyParams.y); }
// 1 where the ring surface at bend angle thetaDeg (deg from the viewer) lies outside the squares' night band.
float RingTerminatorLit(float thetaDeg)
{
    float D = _SunsetParams.z;
    if (D < 0.5) return 1.0;
    return smoothstep(D - 3.0, D + 3.0, abs(thetaDeg));
}

// ---------------- star frame and shadow squares ----------------
// Star-tangent frame: w toward the star, a right, b up.
void RingStarFrame(float3 w, out float3 a, out float3 b)
{
    a = normalize(cross(float3(0.0, 1.0, 0.0), w));
    b = cross(w, a);
}

// Angular coordinates (deg) of dir in the star-tangent plane (valid within +-60 deg of the star).
float2 RingStarPlane(float3 dir, float3 w)
{
    float3 a, b;
    RingStarFrame(w, a, b);
    float z = max(dot(dir, w), 1e-4);
    return float2(atan2(dot(dir, a), z), atan2(dot(dir, b), z)) * RING_RAD2DEG;
}

// Box SDF of a square of side `side` centred at c, rotated by rotDeg (all degrees).
float RingSquareSdf(float2 p, float2 c, float side, float rotDeg)
{
    float s, co;
    sincos(-rotDeg * RING_DEG2RAD, s, co);
    float2 q = p - c;
    q = float2(q.x * co - q.y * s, q.x * s + q.y * co);
    float2 d = abs(q) - 0.5 * side;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// Coverage 0..1 of the three shadow squares at plane position p (deg) for sunset phase u; fpDeg = pixel footprint.
float RingSquaresCoverage(float2 p, float u, float fpDeg)
{
    const float rot = 8.0;
    const float cr = 0.99026807; // cos 8 deg
    float e = 1.2 - 5.33 * (u - 0.10);                 // left edge of A at the star's height
    float cA = e + 11.0 / cr;
    float cB = e + 22.0 / cr + 6.0 + 8.0 / cr;         // B starts 6 deg right of A
    float cC = cB + 8.0 / cr + 6.0 + 6.0 / cr;         // C starts 6 deg right of B
    float soft = max(0.12, fpDeg);
    float sdf = RingSquareSdf(p, float2(cA, 0.0), 22.0, rot);
    float cov = saturate(0.5 - sdf / soft);
    if (_SunsetParams.w > 0.5)
    {
        cov = max(cov, saturate(0.5 - RingSquareSdf(p, float2(cB, 3.0), 16.0, rot) / soft));
        cov = max(cov, saturate(0.5 - RingSquareSdf(p, float2(cC, -2.0), 12.0, rot) / soft));
    }
    return cov;
}

// Star disc (radius rDeg) with limb darkening 0.85 and 1 px AA; returns HDR radiance (x 90, clamp 200).
float3 RingStarDisc(float3 dir, float3 starDir, float rDeg, float fpDeg, float3 tint)
{
    float r = RingAngle(dir, starDir) * RING_RAD2DEG;
    float cov = saturate((rDeg - r) / max(fpDeg, 1e-4) + 0.5);
    float x = saturate(r / rDeg);
    float mu = sqrt(saturate(1.0 - x * x));
    float limb = 1.0 - 0.85 * (1.0 - mu);
    return min(tint * (90.0 * limb), 200.0) * cov;
}

#endif
