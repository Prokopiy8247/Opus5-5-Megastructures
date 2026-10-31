// M12 RingEmissive - flat HDR emissive (lamp lenses, door lamp, tunnel and underpass strips, markers).
// Built on the RingLitCore / RingLitPass contract (same four passes and vertex path as RingCeramic, so RingBend,
// shadows and depth match bit for bit; RingFog is applied by the forward pass). The body is lit like a frosted
// lens (_BaseColor) so an unlit lamp in daylight reads as glass, not as a black hole; the emission
// _EmissionColor x _Intensity x level is added on top and dominates whenever the lamp is on.
// level = product of the enabled global levels (_Lamps, written by AtmosphereDriver):
//   _UseLampLevel  -> _Lamps.x / 3   (bollard / lamp level, 0..3 in the schedule: 3.0 = WarmLamp x3.0 fully on)
//   _UseCityLevel  -> _Lamps.y       (city level N(u))
//   _UseBeaconPulse-> _Lamps.z       (beacon pulse 0..1)
//   _UseSpill      -> _Lamps.w       (tunnel mouth spill 0..1)
// then max(level, _MinLevel). Opaque; renderers normally disable shadow casting (the pass exists for completeness).
Shader "Megastructure/RingEmissive"
{
    Properties
    {
        _BaseColor ("Lens / body colour when off (linear)", Color) = (0.75, 0.73, 0.67, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.45
        _EmissionColor ("Emission colour (linear)", Color) = (1, 0.694, 0.352, 1)
        _Intensity ("Emission intensity (HDR multiplier)", Float) = 3
        _UseLampLevel ("x _Lamps.x / 3 (lamp level)", Range(0, 1)) = 0
        _UseCityLevel ("x _Lamps.y (city level)", Range(0, 1)) = 0
        _UseBeaconPulse ("x _Lamps.z (beacon pulse)", Range(0, 1)) = 0
        _UseSpill ("x _Lamps.w (tunnel mouth spill)", Range(0, 1)) = 0
        _MinLevel ("Level floor", Range(0, 1)) = 0
        _AO ("Vertex AO strength", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _EmissionColor;
        float _Smoothness;
        float _Intensity;
        float _UseLampLevel;
        float _UseCityLevel;
        float _UseBeaconPulse;
        float _UseSpill;
        float _MinLevel;
        float _AO;
    CBUFFER_END

    float RingEmissiveLevel()
    {
        float l = lerp(1.0, _Lamps.x * (1.0 / 3.0), saturate(_UseLampLevel));
        l *= lerp(1.0, _Lamps.y, saturate(_UseCityLevel));
        l *= lerp(1.0, _Lamps.z, saturate(_UseBeaconPulse));
        l *= lerp(1.0, _Lamps.w, saturate(_UseSpill));
        return max(max(l, 0.0), _MinLevel);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float ao = lerp(1.0, saturate(f.color.r), _AO);
        float level = RingEmissiveLevel();
        s.albedo = _BaseColor.rgb * lerp(0.85, 1.0, ao);
        s.smoothness = clamp(_Smoothness, 0.0, 0.65);
        s.occlusion = ao;
        s.emission = max(_EmissionColor.rgb, 0.0) * (max(_Intensity, 0.0) * level);
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
            #include "Include/RingLitPass.hlsl"
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
            #include "Include/RingLitPass.hlsl"
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
            #include "Include/RingLitPass.hlsl"
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
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
