// RingBeaconLens - the beacon lens (Part 14.5.7): a glazed amber sphere, BeaconAmber HDR x8 x the pulse.
// The pulse is the global _BeaconPulse (0.55..1.0) written every frame by BeaconPulse.Tick (Beat07_Outside), so the
// material needs no MaterialPropertyBlock and stays SRP-Batcher compatible. Built on the shared RingLitCore /
// RingLitPass contract (bend, RingShade, RingFog, the four passes), like RingCeramic: it sits on the bent pylon,
// so it bends with it (the 2 mm-level bend at 0-15 m keeps the two identical).
Shader "Megastructure/RingBeaconLens"
{
    Properties
    {
        _BaseColor ("Glass base colour (linear)", Color) = (0.06, 0.035, 0.02, 1)
        [HDR] _EmissionColor ("Emission colour (linear)", Color) = (1.0, 0.434, 0.068, 1)
        _Intensity ("Emission multiplier (HDR)", Float) = 8
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.62
        _Rim ("Rim brightening", Range(0, 1)) = 0.25
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Assets/_Game/Shaders/Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _EmissionColor;
        float _Intensity;
        float _Smoothness;
        float _Rim;
    CBUFFER_END

    float _BeaconPulse;   // global, written by BeaconPulse.Tick (Shader.SetGlobalFloat)

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float nv = saturate(dot(f.normalWS, f.viewDirWS));
        float pulse = clamp(_BeaconPulse, 0.0, 1.5);
        // a lens glows slightly hotter at its centre than at its limb (no texture, no fireflies: clamp 200)
        float shape = lerp(1.0 - _Rim, 1.0, nv);
        s.albedo = _BaseColor.rgb;
        s.smoothness = _Smoothness;
        s.occlusion = 1.0;
        s.emission = min(_EmissionColor.rgb * (_Intensity * pulse * shape), 200.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define RING_PASS_SHADOW
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTH
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTHNORMALS
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
