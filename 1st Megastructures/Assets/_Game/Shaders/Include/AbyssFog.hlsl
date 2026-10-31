#ifndef ABYSS_FOG_INCLUDED
#define ABYSS_FOG_INCLUDED

// =====================================================================================================================
// AbyssFog.hlsl - the ONE shared fog / sky model of Abyss (MASTER_PROMPT 8.4, 14.2 "Shared fog and sky model").
//
//   FogSky(v):  c = lerp(FOG_DOWN, FOG_HORIZ, smoothstep(-0.55, 0, v.y))
//               c = lerp(c, FOG_UP, smoothstep(0, 0.8, v.y))
//               c += SCATTER * HG(dot(v, sunDir), 0.55) * scatterAmt
//   Fog factor: F = 1 - exp(-(sigma * d_eff)^2),  d_eff = d * (1 + m_down * saturate(-v.y)),  sigma = 0.8326 / D50
//   Aerial perspective: rgb = lerp(rgb, luma(rgb), 0.5 F), then rgb = lerp(rgb, FogSky(v), F)
//
// Fog domains (material property _AbDomain): 0 Shaft (globals written by ProgressDirector), 1 Tunnel (constants),
// 2 Exterior (constants + the beat-7 sun; its HG lobe is weighted by smoothstep(0.05, 0.35, v.y) and capped at 0.15, so
// the horizon row equals OUT_HORIZ). The skybox (Sky.shader), every Megastructure shader, the cards and the backdrops
// use these functions, so fog and sky can never drift apart. Unity fog is never used (no FOG_* keywords anywhere).
// Requires Core.hlsl (for _WorldSpaceCameraPos and PI). Colours are LINEAR.
// =====================================================================================================================

// ------------------------------------------------------------------------------------------------ domain 0 globals
float  _AbFogSigma;      // 0.8326 / D50
float  _AbFogDownMul;    // m_down
float4 _AbFogDown;       // linear rgb
float4 _AbFogHoriz;      // linear rgb
float4 _AbFogUp;         // linear rgb
float  _AbScatterAmt;    // scatter amount
float4 _AbScatterColor;  // linear rgb (SCATTER)
float4 _AbSunDir;        // xyz = unit vector TOWARD the key light
float4 _AbExtSunDir;     // xyz = unit vector toward the beat-7 sun (domain 2)
float  _AbSkyBlend;      // skybox: 0 = Shaft sky, 1 = Exterior sky (already eased by ProgressDirector)

// ------------------------------------------------------------------------------------------------ domain 1 / 2 constants
// Mirrors AbyssPalette.TUNNEL_FOG / EXT_* (14.2 rows "7 tunnel" and "7 outside"); ShaderTests checks the values.
#define AB_HG_G               0.55
#define AB_TUNNEL_D50         90.0
#define AB_EXT_D50            7000.0
#define AB_EXT_M_DOWN         0.4
#define AB_EXT_LOBE_CAP       0.15     // cap of the weighted Exterior HG lobe
// Exterior scatter amount (tuning value, horizon unaffected). Round 1 (R1-03): 0.6 -> 0.25. At 0.6 the capped lobe (flat
// within 44 degrees of the sun) filled the whole upper KS7b frame, so the sky read 0.46 at the top against 0.49 at the
// horizon; at 0.25 the OUT_HORIZ -> OUT_ZENITH gradient shows (about 0.44 at +22 degrees) and the frame brightens toward
// the sun (upper right in KS7b, azimuth 200). With the 14.2 gradient alone the +22 degree row cannot go below 0.42.
#define AB_EXT_SCATTER_AMT    0.25
// Shaft-domain lobe weight (round 1, R1-06): smoothstep(Y0, Y1, v.y) on the domain-0 HG lobe. The shaft sun's scatter was
// raised to make the haze glow toward the light (layer separation in KS2a); the HG tail (0.015 at the nadir for g 0.55)
// would otherwise lift the abyss, which 14.7.2 fixes at FOG_DOWN (luma 0.041). Zero below v.y -0.5 (30 degrees down), full
// from the horizon up. The Exterior lobe keeps its own weighting.
#define AB_SHAFT_LOBE_Y0     -0.5
#define AB_SHAFT_LOBE_Y1      0.0
static const float3 AB_TUNNEL_FOG    = float3(0.003035, 0.004025, 0.005182);  // #0A0D10
static const float3 AB_EXT_FOG_DOWN  = float3(0.003035, 0.005182, 0.007499);  // #0A1015
static const float3 AB_EXT_FOG_HORIZ = float3(0.230740, 0.304987, 0.376262);  // OUT_HORIZ #8496A5
static const float3 AB_EXT_FOG_UP    = float3(0.048172, 0.080220, 0.122139);  // OUT_ZENITH #3E5062
static const float3 AB_EXT_SCATTER   = float3(0.672443, 0.760525, 0.838799);  // beat-7 sun #D6E2EC

#define AB_DOMAIN_SHAFT    0
#define AB_DOMAIN_TUNNEL   1
#define AB_DOMAIN_EXTERIOR 2

struct AbFogSet
{
    float  sigma;
    float  mDown;
    float3 down;
    float3 horiz;
    float3 up;
    float3 scatter;
    float  scatterAmt;
    float3 sunDir;
    float  lobeExterior;   // 1: Exterior lobe weighting and cap
};

// The fog set of a domain. The branch is uniform (material constant) everywhere it is used.
AbFogSet AbGetFogSet(int domain)
{
    AbFogSet s;
    if (domain == AB_DOMAIN_TUNNEL)
    {
        s.sigma = 0.8326 / AB_TUNNEL_D50;
        s.mDown = 0.0;
        s.down = AB_TUNNEL_FOG;
        s.horiz = AB_TUNNEL_FOG;
        s.up = AB_TUNNEL_FOG;
        s.scatter = float3(0.0, 0.0, 0.0);
        s.scatterAmt = 0.0;
        s.sunDir = float3(0.0, 1.0, 0.0);
        s.lobeExterior = 0.0;
    }
    else if (domain == AB_DOMAIN_EXTERIOR)
    {
        s.sigma = 0.8326 / AB_EXT_D50;
        s.mDown = AB_EXT_M_DOWN;
        s.down = AB_EXT_FOG_DOWN;
        s.horiz = AB_EXT_FOG_HORIZ;
        s.up = AB_EXT_FOG_UP;
        s.scatter = AB_EXT_SCATTER;
        s.scatterAmt = AB_EXT_SCATTER_AMT;
        s.sunDir = _AbExtSunDir.xyz;
        s.lobeExterior = 1.0;
    }
    else
    {
        s.sigma = _AbFogSigma;
        s.mDown = _AbFogDownMul;
        s.down = _AbFogDown.rgb;
        s.horiz = _AbFogHoriz.rgb;
        s.up = _AbFogUp.rgb;
        s.scatter = _AbScatterColor.rgb;
        s.scatterAmt = _AbScatterAmt;
        s.sunDir = _AbSunDir.xyz;
        s.lobeExterior = 0.0;
    }
    return s;
}

// Rec. 709 luma (same weights as AbLuma in AbyssCommon.hlsl; this file stays self-contained).
float AbLumaFog(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// Normalised Henyey-Greenstein phase function; cosTheta = dot(view ray, direction toward the light), g > 0 forward.
float HG(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4);
    return (1.0 - g2) / (4.0 * PI * denom * sqrt(denom));
}

// Sky / fog colour seen along the unit direction v for a fog set.
float3 AbFogSkySet(float3 v, AbFogSet s)
{
    float3 c = lerp(s.down, s.horiz, smoothstep(-0.55, 0.0, v.y));
    c = lerp(c, s.up, smoothstep(0.0, 0.8, v.y));
    float hg = HG(dot(v, s.sunDir), AB_HG_G);
    float lobe = s.lobeExterior > 0.5 ? min(hg * smoothstep(0.05, 0.35, v.y), AB_EXT_LOBE_CAP)
                                      : hg * smoothstep(AB_SHAFT_LOBE_Y0, AB_SHAFT_LOBE_Y1, v.y);
    return c + s.scatter * (lobe * s.scatterAmt);
}

// FogSky(v) of a domain (14.2). v must be normalised.
float3 FogSky(float3 v, int domain)
{
    return AbFogSkySet(v, AbGetFogSet(domain));
}

// Fog factor F for distance d (m) along the unit view direction v.
float AbFogFactorSet(float d, float3 v, AbFogSet s)
{
    float dEff = d * (1.0 + s.mDown * saturate(-v.y));
    float x = s.sigma * dEff;
    return 1.0 - exp(-x * x);
}

float AbyssFogFactor(float d, float3 v, int domain)
{
    return AbFogFactorSet(d, v, AbGetFogSet(domain));
}

// Unit view direction and distance from the camera to a world position.
float3 AbViewRay(float3 positionWS, out float dist)
{
    float3 toP = positionWS - _WorldSpaceCameraPos;
    dist = length(toP);
    return dist > 1e-5 ? toP / dist : float3(0.0, 0.0, 1.0);
}

// Aerial perspective + fog of an opaque surface colour (14.2). The only fog entry point of opaque shaders.
float3 AbyssApplyFog(float3 rgb, float3 positionWS, int domain)
{
    float d;
    float3 v = AbViewRay(positionWS, d);
    AbFogSet s = AbGetFogSet(domain);
    float F = AbFogFactorSet(d, v, s);
    rgb = lerp(rgb, AbLumaFog(rgb).xxx, 0.5 * F);
    return lerp(rgb, AbFogSkySet(v, s), F);
}

// Fog of an additive contribution (glows, beams): aerial perspective, then attenuation by the transmittance 1 - F
// (an additive term must fade to nothing in fog, not to the fog colour).
float3 AbyssFogAdditive(float3 rgb, float3 positionWS, int domain)
{
    float d;
    float3 v = AbViewRay(positionWS, d);
    float F = AbFogFactorSet(d, v, AbGetFogSet(domain));
    rgb = lerp(rgb, AbLumaFog(rgb).xxx, 0.5 * F);
    return rgb * (1.0 - F);
}

// Skybox colour: Shaft sky blended into the Exterior sky by _AbSkyBlend (14.2: d 640-668.54, smootherstep on the CPU).
float3 AbyssSkyColor(float3 v)
{
    float b = saturate(_AbSkyBlend);
    if (b <= 0.0) return FogSky(v, AB_DOMAIN_SHAFT);
    if (b >= 1.0) return FogSky(v, AB_DOMAIN_EXTERIOR);
    return lerp(FogSky(v, AB_DOMAIN_SHAFT), FogSky(v, AB_DOMAIN_EXTERIOR), b);
}

#endif // ABYSS_FOG_INCLUDED
