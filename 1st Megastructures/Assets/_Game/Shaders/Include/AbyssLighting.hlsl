#ifndef ABYSS_LIGHTING_INCLUDED
#define ABYSS_LIGHTING_INCLUDED

// =====================================================================================================================
// AbyssLighting.hlsl - physically based lighting for the lit Megastructure shaders (MASTER_PROMPT 8.3, 8.6, 14.2).
//   * BRDF: URP BRDFData (metallic workflow), GGX specular from DirectBRDFSpecular, clamped against fireflies.
//   * Main light with cascaded (soft) shadows; additional lights through the Forward+ cluster loop (_CLUSTER_LIGHT_LOOP,
//     LIGHT_LOOP_BEGIN/END, plus the separate loop for additional directional lights), classic loop under Forward.
//   * Ambient ONLY from a trilight x vertex AO (no SH, no probes): domain 0 the _AbAmbUp/_AbAmbMid/_AbAmbDown globals,
//     domains 1 and 2 constants (their fog colours x 1.2; the Exterior ground raised to half the equator), see AbTrilight;
//     plus, in domain 0, the shaft sun's directional haze skylight (AbHazeSkylight).
//   * Shaft-depth falloff of the Key in domain 0 (AbKeyFactor): _AbKeyDepth for every surface, plus a slower second falloff
//     on the shaft ring top chamfers (_AbKeyChamfer, _AbRingBand; R2-06).
//   * Radiance floor: nothing lit renders darker than its domain's FOG_DOWN (AbRadianceFloor, 14.2 "no pure black").
//   * Environment ONLY FogSky(reflect(-V, N), domain) x Fresnel x AO (no reflection probes), so reflections always equal
//     the current beat's sky and fog domain (defect 38).
//   * Specular anti-aliasing: roughness widened by the screen-space variance of the geometric normal (Kaplanyan /
//     Tokuyoshi) plus a roughness floor.
// Light layers, light cookies and SSAO are not used. Requires Core.hlsl, AbyssCommon.hlsl and AbyssFog.hlsl.
// The pass must declare the lighting keywords listed in Surface.shader (ForwardLit).
// =====================================================================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float4 _AbAmbUp;    // linear, trilight sky term (already multiplied by the beat's ambient scale)
float4 _AbAmbMid;   // linear, equator term
float4 _AbAmbDown;  // linear, ground term
// Shaft-depth falloff of the main light (Key) for domain-0 materials (Part 8.5: an analytic shadow term beyond the cascade
// range). x = world y of full light, y = world y of zero light (x > y), z = 1 enabled / 0 disabled, w unused. Written by
// ProgressDirector from the active Key row: on for the shaft sun (beats 2, 3, 5), off for the beat-6 fan light and the
// beat-7 sun. An unset global is (0, 0, 0, 0) = disabled (edit mode, tests).
float4 _AbKeyDepth;
// Haze skylight of the shaft sun (R1-06): linear rgb = key colour x KeyRow.HazeSky x key fraction, written by ProgressDirector
// (0 when the Key is off or the row has none). Domain 0 only, see AbHazeSkylight. An unset global is 0 = off.
float4 _AbHazeSky;
// Second, slow shaft-depth falloff of the Key for the ring top chamfers only (round 2, R2-06 / R2-07): x = amplitude (the
// Key fraction at and above y0; 0 = off), y = y0 (world y), z = e-folding length (m), w = world y of zero light. Written by
// ProgressDirector from the active Key row (on for the shaft sun of beats 2 and 3). An unset global is 0 = off.
float4 _AbKeyChamfer;
// The shaft S0 ring band the chamfer term applies to: xy = shaft axis (world x, z), z = inner and w = outer hexagonal radius
// (the belt / rib chamfers lie at hex radius 330-346). Written by ProgressDirector from AbyssConst.
float4 _AbRingBand;

#define AB_ROUGHNESS_FLOOR  0.12   // global floor (recipes set higher floors, e.g. steel 0.25, rods 0.30)
#define AB_SPEC_MAX         40.0   // clamp of the GGX specular term (fireflies under bloom, defect 15)
#define AB_SPECAA_VARIANCE  0.5    // screen-space variance scale of the normal-variance roughness widening
#define AB_SPECAA_THRESHOLD 0.18   // maximum added alpha^2

// Surface description produced by a recipe.
struct AbSurface
{
    float3 albedo;
    float  metallic;
    float  roughness;      // perceptual roughness
    float3 normalWS;       // shading normal (unit)
    float  ao;             // baked ambient occlusion (vertex colour R), 1 = open
    float3 emission;       // linear, added after lighting
};

AbSurface AbDefaultSurface(float3 normalWS)
{
    AbSurface s;
    s.albedo = float3(0.5, 0.5, 0.5);
    s.metallic = 0.0;
    s.roughness = 0.6;
    s.normalWS = normalWS;
    s.ao = 1.0;
    s.emission = float3(0.0, 0.0, 0.0);
    return s;
}

// Ambient of the constant fog domains (lead decision): like their fog, the Tunnel and Exterior ambients are constant per
// domain instead of following the camera-position-blended _AbAmb* globals (which stay for domain 0). Trilight = the
// domain's fog colours x the beat-7 ambient scale (14.2 lighting rig "x1.2"; ProgressTables rows 7 tunnel / 7 outside,
// checked by ShaderTests). Tunnel: TUNNEL_FOG for all three terms; Exterior: FOG_DOWN / OUT_HORIZ / OUT_ZENITH as
// ground / equator / sky.
#define AB_AMB_DOMAIN_SCALE 1.2

// Exterior ground bounce (round 1, R1-15): below the balcony, pier and Face lies the sunlit honeycomb haze (luma about 0.3),
// not the FOG_DOWN of the far depths, so the domain-2 ground term is raised to this fraction of the equator term (OUT_HORIZ
// x 1.2), per channel: the corbel and collar undersides read as shaded concrete (luma 0.15-0.2) instead of black holes,
// and the tunnel mouth stays the darkest accent of KS7d. Equals ProgressTables.AmbGroundMin of the "7 outside" row.
#define AB_EXT_GROUND_MIN 0.6

// Trilight ambient irradiance for a unit normal (linear in n.y: down - mid - up) in a fog domain. Domain 0 (Shaft) reads
// the _AbAmb* globals; domains 1 and 2 use their constants. The branch is uniform (material constant).
float3 AbTrilight(float3 n, int domain)
{
    float3 up, mid, down;
    if (domain == AB_DOMAIN_TUNNEL)
    {
        up = AB_TUNNEL_FOG * AB_AMB_DOMAIN_SCALE;
        mid = up;
        down = up;
    }
    else if (domain == AB_DOMAIN_EXTERIOR)
    {
        up = AB_EXT_FOG_UP * AB_AMB_DOMAIN_SCALE;
        mid = AB_EXT_FOG_HORIZ * AB_AMB_DOMAIN_SCALE;
        down = AB_EXT_FOG_DOWN * AB_AMB_DOMAIN_SCALE;
        down = max(down, AB_EXT_GROUND_MIN * mid);   // ground bounce of the lit honeycomb (R1-15)
    }
    else
    {
        up = _AbAmbUp.rgb;
        mid = _AbAmbMid.rgb;
        down = _AbAmbDown.rgb;
    }
    float y = clamp(n.y, -1.0, 1.0);
    return up * max(y, 0.0) + mid * (1.0 - abs(y)) + down * max(-y, 0.0);
}

// Specular AA kernel: added alpha^2 from the screen-space variance of the GEOMETRIC normal ("normal variance clamped by
// fwidth(normal)", 14.2 M_RodPolished). Call it at the top level of the fragment (outside non-uniform branches): it
// takes derivatives.
float AbSpecularAAKernel(float3 geomNormalWS)
{
    float3 du = ddx(geomNormalWS);
    float3 dv = ddy(geomNormalWS);
    float variance = AB_SPECAA_VARIANCE * (dot(du, du) + dot(dv, dv));
    return min(2.0 * variance, AB_SPECAA_THRESHOLD);
}

// Perceptual roughness widened by a specular AA kernel (alpha^2 space).
float AbApplySpecularAA(float perceptualRoughness, float kernel)
{
    float a = perceptualRoughness * perceptualRoughness;
    float a2 = saturate(a * a + kernel);
    return sqrt(sqrt(a2));
}

// Shaft-depth attenuation of the main light at world height y (14.7.2 bottomless shaft): the sun that falls into the
// shaft does not reach the city and walls hundreds of metres below, so its contribution fades to zero with depth instead
// of lighting everything at any depth. Smoothstep between the two heights of _AbKeyDepth; 1 when disabled or outside
// domain 0 (the Tunnel and Exterior domains have their own constant lighting). Uniform branch on the material domain.
float AbKeyDepthFactor(float y, int domain)
{
    if (domain != AB_DOMAIN_SHAFT || _AbKeyDepth.z < 0.5) return 1.0;
    return AbSmoothstepSafe(_AbKeyDepth.y, _AbKeyDepth.x, y);
}

// Ring-chamfer depth falloff (round 2, R2-06 / R2-07): the 45-degree top chamfers of the shaft-S0 ribs and belts keep a slow
// second falloff of the Key, so the ring rhythm continues below the first rib as nested lines that dim toward FOG_DOWN:
// f(y) = A (e(y) - e0) / (1 - e0), e(y) = exp(min(y - y0, 0) / L), e0 = e(yZero); with the sun-2/3 row (A 0.20, y0 -330,
// L 120, yZero -470; ProgressTables.SUN_CHAMFER_*): 0.20 on the -150 / -210 / -270 / -330 rings (the shaft fog steps them
// down with distance), 0.09 at -390, 0.02 at -450, 0 from -470.
// Selected by geometry only (no material flag): a face that looks up and toward the shaft axis at about 45 degrees
// (0.5 < N.y < 0.9, horizontal normal within about 35 degrees of the inward radial) inside the ring band of hexagonal radius
// (_AbRingBand). Walls (N.y 0), ring and roof tops (N.y 1), columns and everything off the band keep the plain falloff.
// Returns the Key fraction of this term (0 when off or outside domain 0); combined with max() in AbKeyFactor.
float AbRingChamferFactor(float3 n, float3 positionWS, int domain)
{
    if (domain != AB_DOMAIN_SHAFT || _AbKeyChamfer.x <= 0.0) return 0.0;
    float wN = smoothstep(0.50, 0.60, n.y) * (1.0 - smoothstep(0.80, 0.90, n.y));
    float2 r = positionWS.xz - _AbRingBand.xy;
    float hexR = max(abs(r.y), max(abs(0.8660254 * r.x + 0.5 * r.y), abs(0.8660254 * r.x - 0.5 * r.y)));
    float wB = smoothstep(_AbRingBand.z - 2.0, _AbRingBand.z, hexR) * (1.0 - smoothstep(_AbRingBand.w, _AbRingBand.w + 2.0, hexR));
    float2 nh = n.xz;
    float inward = dot(nh, -r) * rsqrt(max(dot(nh, nh) * dot(r, r), 1e-8));
    float wI = smoothstep(0.72, 0.82, inward);
    float L = max(_AbKeyChamfer.z, 1.0);
    float e0 = exp(min(_AbKeyChamfer.w - _AbKeyChamfer.y, 0.0) / L);
    float e = exp(min(positionWS.y - _AbKeyChamfer.y, 0.0) / L);
    float f = _AbKeyChamfer.x * saturate((e - e0) / max(1.0 - e0, 1e-4));
    return f * wN * wB * wI;
}

// Key fraction at a surface point: the shaft-depth falloff, raised on the ring top chamfers by their slow second falloff.
float AbKeyFactor(float3 n, float3 positionWS, int domain)
{
    return max(AbKeyDepthFactor(positionWS.y, domain), AbRingChamferFactor(n, positionWS, domain));
}

// Haze skylight of the shaft sun (round 1, R1-06): the sunlight scattered by the shaft haze arrives mostly from the sun side,
// so it lights a surface as a soft directional ambient ((1 + N.L) / 2)^2 (1 facing the sun, 0.25 side-on, 0 facing away),
// faded with depth like the Key (keyFactor = AbKeyFactor). Up-facing rib and belt tops and the sun-side flanks of the
// columns catch it and read against the backlit walls. Ambient-type (never shadowed, so no edge at the cascade distance);
// domain 0 only; the caller scales it by the material's Key mask (interiors the Key cannot reach do not see the haze either).
// Zero when the Key is off.
float3 AbHazeSkylight(float3 n, float keyFactor, int domain)
{
    if (domain != AB_DOMAIN_SHAFT) return float3(0.0, 0.0, 0.0);
    float w = 0.5 + 0.5 * dot(n, _AbSunDir.xyz);
    return _AbHazeSky.rgb * (w * w) * keyFactor;
}

// Radiance floor (round 1, R1-14 backstop): no lit surface renders darker than its domain's FOG_DOWN, the darkest colour of
// each fog set (14.2 "no pure black": FOG_DOWN is never below linear 0.002). Per channel soft knee c + f (1 - c / 2f)^2 below
// 2f, identity above (monotonic, continuous slope at 2f), so lit tones are untouched and faces no light reaches (steel
// undersides at albedo 0.02, crevices) settle on the abyss colour instead of 0 and never band against a hard clamp.
float3 AbRadianceFloor(float3 c, int domain)
{
    float3 f = max(AbGetFogSet(domain).down, float3(1e-5, 1e-5, 1e-5));
    float3 t = saturate(1.0 - c / (2.0 * f));
    return c + f * (t * t);
}

// One light, GGX + Lambert, specular term clamped.
float3 AbDirectLight(BRDFData brdf, Light light, float3 N, float3 V)
{
    float NdotL = saturate(dot(N, light.direction));
    float atten = light.distanceAttenuation * light.shadowAttenuation;
    float3 radiance = light.color * (atten * NdotL);
    float spec = min(DirectBRDFSpecular(brdf, N, light.direction, V), AB_SPEC_MAX);
    return (brdf.diffuse + brdf.specular * spec) * radiance;
}

// Fill URP's InputData for our light loop. The variable that LIGHT_LOOP_BEGIN reads MUST be named "inputData".
InputData AbMakeInputData(float3 positionWS, float3 normalWS, float4 positionCS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);   // per-pixel cascade selection
#else
    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
#endif
    inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);                     // no baked shadow mask
    return inputData;
}

// Full lighting of an opaque surface (direct + ambient + environment + emission), BEFORE fog, floored at the domain's
// FOG_DOWN (AbRadianceFloor). keyLightMask (material _KeyLightMask, 0..1, default 1) scales ONLY the Key: diffuse and
// specular of the directional light after its shadow, and the Key's haze skylight (AbHazeSkylight). In domain 0 both are
// also faded with world height by AbKeyFactor (shaft-depth falloff, slower on the ring chamfers). Additional lights, the
// trilight ambient, the FogSky environment and emission are unaffected, so an interior surface the Key cannot reach
// (outside the shadow cascades) can be masked per material.
float3 AbyssShadeLit(AbSurface s, InputData inputData, int domain, float keyLightMask)
{
    float alpha = 1.0;
    float roughness = max(s.roughness, AB_ROUGHNESS_FLOOR);
    BRDFData brdf;
    InitializeBRDFData(s.albedo, s.metallic, half3(0.0, 0.0, 0.0), 1.0 - roughness, alpha, brdf);

    float3 N = s.normalWS;
    float3 V = inputData.viewDirectionWS;
    float keyFactor = AbKeyFactor(N, inputData.positionWS, domain);                // shaft-depth falloff (domain 0)

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    float3 color = AbDirectLight(brdf, mainLight, N, V) * saturate(keyLightMask)    // saturate also maps NaN to 0
                 * keyFactor;

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();   // 0 under the cluster loop (by design)
  #if USE_CLUSTER_LIGHT_LOOP
    // Additional directional lights are not in the clusters (Lighting.hlsl:359-375).
    [loop] for (uint dirIndex = 0u; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light dirLight = GetAdditionalLight(dirIndex, inputData.positionWS);
        color += AbDirectLight(brdf, dirLight, N, V);
    }
  #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        color += AbDirectLight(brdf, light, N, V);
    LIGHT_LOOP_END
#endif

    // Ambient (trilight + the shaft sun's haze skylight) and environment (FogSky of the reflected ray), occluded by the baked AO.
    float NoV = saturate(dot(N, V));
    float3 ambient = AbTrilight(N, domain);
    // the haze skylight is the Key's own scattered light: interiors masked from the Key (gallery, hall) do not receive it
    ambient += AbHazeSkylight(N, keyFactor, domain) * clamp(keyLightMask, 0.0, 1.0);
    float3 R = reflect(-V, N);
    float3 env = FogSky(AbSafeNormalize(R, N), domain);
    float fresnelTerm = Pow4(1.0 - NoV);
    color += EnvironmentBRDF(brdf, ambient, env, fresnelTerm) * s.ao;

    color += s.emission;
    return AbRadianceFloor(color, domain);
}

#endif // ABYSS_LIGHTING_INCLUDED
