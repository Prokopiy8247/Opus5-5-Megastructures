// M01 RingCeramic - CeramicWhite glaze: smoothness 0.55 (roughness floor 0.35), triplanar panel lines 8 mm filtered
// (fade when the spacing drops below 4 px), vertex R = AO, G = edge wear from the 2 cm chamfers, macro tone +-3 % at
// 40 m. Reference implementation of the RingLitCore / RingLitPass contract; copy its structure for new materials.
Shader "Megastructure/RingCeramic"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (0.815, 0.791, 0.730, 1)
        _JointColor ("Joint colour (linear)", Color) = (0.485, 0.456, 0.392, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.55
        _PanelSize ("Panel size (m)", Vector) = (1.2, 0.6, 1.2, 0)
        _PanelLine ("Panel line width (m)", Float) = 0.008
        _AO ("Vertex AO strength", Range(0, 1)) = 1
        _EdgeWear ("Edge wear strength", Range(0, 1)) = 0.6
        [HDR] _Emission ("Emission (linear HDR)", Color) = (0, 0, 0, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _JointColor;
        float4 _PanelSize;
        float4 _Emission;
        float _Smoothness;
        float _PanelLine;
        float _AO;
        float _EdgeWear;
    CBUFFER_END

    // Triplanar filtered panel grooves in flat world space (camera-near coordinates, no precision issue within 1 km)
    float RingPanelGrooves(float3 p, float3 n, float fp)
    {
        float3 an = abs(n);
        float2 q; float2 size;
        if (an.y >= an.x && an.y >= an.z) { q = p.xz; size = _PanelSize.xz; }
        else if (an.x >= an.z) { q = p.zy; size = _PanelSize.zy; }
        else { q = p.xy; size = _PanelSize.xy; }
        float gx = RingFilteredGrid1(q.x, size.x, _PanelLine, fp, 4.0);
        float gy = RingFilteredGrid1(q.y, size.y, _PanelLine, fp, 4.0);
        return saturate(gx + gy - gx * gy);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float macro = RingValueNoise(f.positionFlatWS.xz / 40.0 + f.positionFlatWS.y / 57.0);
        float3 base = _BaseColor.rgb * (0.97 + 0.06 * macro);
        float groove = RingPanelGrooves(f.positionFlatWS, f.normalFlatWS, f.footprint);
        float ao = lerp(1.0, f.color.r, _AO);
        float wear = f.color.g * _EdgeWear;
        float3 alb = lerp(base, _JointColor.rgb, groove * 0.85);
        alb = lerp(alb, alb * 1.04 + 0.01, wear);           // chamfers catch a slightly brighter, rougher glaze
        s.albedo = alb * lerp(0.82, 1.0, ao);
        s.smoothness = min(_Smoothness * (1.0 - 0.35 * groove) * (1.0 - 0.25 * wear), 0.65);
        s.occlusion = ao;
        s.emission = _Emission.rgb;
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
