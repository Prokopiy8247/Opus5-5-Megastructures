// RingLighting.hlsl - shared lit model of every Ring* material (Part 14.7 H10).
// Mirrors URP 17.6 UniversalFragmentPBR (Lighting.hlsl:302-404) but: the key (main light) vector and the extra
// directional fills rotate with the bend angle (NdotL of flat ground is 0.438 at any distance), ambient is our
// Trilight from globals (never SH), reflections come only from RingSkyColor (never a probe), the ark / cloud
// shadows attenuate the key analytically, and RingFog is applied last. Requires: Core.hlsl, Lighting.hlsl included
// first, a local variable named `inputData` (LIGHT_LOOP_BEGIN reads it).
#ifndef RING_LIGHTING_INCLUDED
#define RING_LIGHTING_INCLUDED

#include "RingBend.hlsl"
#include "RingAtmosphere.hlsl"

// RingCapsuleSdf and RingKeyAtten (analytic ship / cloud shadows) live in RingAtmosphere.hlsl: the sky dome uses them too.

// Trilight ambient around the bent local up.
half3 RingAmbient(float3 normalWS, float theta)
{
    float3 up = RingBendRotate(float3(0.0, 1.0, 0.0), theta);
    float u = dot(normalWS, up);
    float sky = saturate(u), ground = saturate(-u);
    float eq = 1.0 - sky - ground;
    return _AmbSky.rgb * sky + _AmbEquator.rgb * eq + _AmbGround.rgb * ground;
}

// Environment reflection from the analytic sky, mixed toward the fog colour as roughness rises.
half3 RingReflection(float3 reflectWS, float perceptualRoughness)
{
    float3 r = normalize(reflectWS);
    float3 sky = RingSkyColor(r);
    float3 fogc = RingFogHeadingColor(r);
    return lerp(sky, fogc, saturate(perceptualRoughness * 1.6));
}

// Full shading. positionFlatWS = unbent world position (for analytic shadows), theta = bend angle.
// specOcclusion scales the environment term (cheap cavity AO from vertex colour).
half3 RingShade(InputData inputData, SurfaceData surfaceData, float3 positionFlatWS, float theta)
{
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    const BRDFData noClearCoat = (BRDFData)0;
    const half4 shadowMask = half4(1, 1, 1, 1);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    mainLight.direction = RingBendRotate(mainLight.direction, theta);
    mainLight.shadowAttenuation *= RingKeyAtten(positionFlatWS.xz);
    // From the tunnel on (set >= 2) the key is the OUTSIDE star (L7): it cannot reach anything inside the rim wall
    // (flat x < 234); light inside the tunnel mouth is the additive spill. Prevents L7 lighting the plain through the door.
    if (_RingSet.x > 1.5) mainLight.color *= saturate(positionFlatWS.x - 233.0);

    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0h - NoV);
    half3 indirectSpecular = RingReflection(reflectVector, brdfData.perceptualRoughness);
    half3 color = EnvironmentBRDF(brdfData, inputData.bakedGI, indirectSpecular, fresnelTerm) * surfaceData.occlusion;

    color += LightingPhysicallyBased(brdfData, noClearCoat, mainLight, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    // non-main directional lights (arch fill, rim wash) are not in the clusters
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        light.direction = RingBendRotate(light.direction, theta);
        color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
    LIGHT_LOOP_END
#endif

    color += surfaceData.emission;
    return min(color, 1000.0);
}

#endif
