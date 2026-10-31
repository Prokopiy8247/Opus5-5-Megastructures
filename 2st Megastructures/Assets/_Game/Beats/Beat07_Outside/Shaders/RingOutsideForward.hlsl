// RingOutsideForward.hlsl - forward fragment of the beat-7 outside materials (M09 RingHull, RingOuterFace).
// It is RingFrag of Include/RingLitPass.hlsl line for line (same InputData, the shared RingShade of
// RingLighting.hlsl, then RingFog) plus two optional hooks, so the outside keeps the ONE lighting model (14.7 H10):
//   RING_HAS_KEY_HOOK : float RingKeyVisibilityHook(RingFragData f)  -> 0..1 analytic visibility of the key
//                       (hull self-shadow, gantry shadows). Applied exactly: the key's own LightingPhysicallyBased
//                       term (identical light, identical attenuation as inside RingShade) is subtracted (1 - vis)
//                       times, so ambient, reflections, fills and local lights are untouched.
//   RING_HAS_POST_HOOK: float3 RingPostHook(RingFragData f, float3 c) -> runs after RingFog (outer-face darkening).
// Include it in the ForwardLit pass AFTER "Include/RingLitPass.hlsl" and use #pragma fragment RingOutsideFrag.
#ifndef RING_OUTSIDE_FORWARD_INCLUDED
#define RING_OUTSIDE_FORWARD_INCLUDED

#if defined(RING_PASS_FORWARD)
half4 RingOutsideFrag(RingVaryings i) : SV_Target0
{
    RingFragData f = RingMakeFrag(i);
#if defined(RING_HAS_CLIP_HOOK)
    RingClipHook(f);
#endif
    SurfaceData s = (SurfaceData)0;
    s.albedo = half3(0.5, 0.5, 0.5);
    s.alpha = 1.0h;
    s.metallic = 0.0h;
    s.specular = half3(0, 0, 0);
    s.smoothness = 0.5h;
    s.occlusion = 1.0h;
    s.emission = half3(0, 0, 0);
    s.normalTS = half3(0, 0, 1);
    RingSurfaceHook(f, s);

    InputData inputData = (InputData)0;
    inputData.positionWS = f.positionWS;
    inputData.positionCS = i.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(f.normalWS);
    inputData.viewDirectionWS = f.viewDirWS;
    inputData.normalizedScreenSpaceUV = f.screenUV;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(f.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.shadowMask = half4(1, 1, 1, 1);
    inputData.bakedGI = RingAmbient(inputData.normalWS, f.theta);

    half3 c = RingShade(inputData, s, f.positionFlatWS, f.theta);

#if defined(RING_HAS_KEY_HOOK)
    float vis = saturate(RingKeyVisibilityHook(f));
    if (vis < 0.999)
    {
        // the exact key term RingShade added (RingLighting.hlsl: same light, rotation and analytic attenuation)
        BRDFData brdfData;
        InitializeBRDFData(s, brdfData);
        const BRDFData noClearCoat = (BRDFData)0;
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1, 1, 1, 1));
        mainLight.direction = RingBendRotate(mainLight.direction, f.theta);
        mainLight.shadowAttenuation *= RingKeyAtten(f.positionFlatWS.xz);
        half3 keyTerm = LightingPhysicallyBased(brdfData, noClearCoat, mainLight, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
        c = max(c - (half)(1.0 - vis) * keyTerm, half3(0, 0, 0));
    }
#endif

#if !defined(RING_NO_FOG)
    c = RingFog(c, f.positionWS, f.positionFlatWS.y);
#endif
#if defined(RING_HAS_POST_HOOK)
    c = RingPostHook(f, c);
#endif
    return half4(c, 1.0h);
}
#endif

#endif
