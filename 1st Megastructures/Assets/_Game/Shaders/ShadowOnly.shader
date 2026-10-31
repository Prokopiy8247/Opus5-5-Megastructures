// =====================================================================================================================
// Megastructure/ShadowOnly - invisible shadow caster (MatId.ShadowCaster): the 0.5 x 1.8 m capsule that follows the
// player in beats 2, 3 and 7 to give a player shadow (14.2). Only a ShadowCaster pass: nothing is drawn in colour,
// depth or depth-normals passes, so the capsule never occludes or appears in _CameraDepthTexture.
// =====================================================================================================================
Shader "Megastructure/ShadowOnly"
{
    Properties
    {
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine (unused)", Float) = 0
        _DistFade ("Distance Fade (unused)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _DistFade;
            float  _AbFade;
            float  _ThinLine;
        CBUFFER_END
        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define AB_SHADOW_CASTER_PASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
