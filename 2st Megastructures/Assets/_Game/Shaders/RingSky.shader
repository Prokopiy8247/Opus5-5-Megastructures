// M05 RingSky: camera-following dome, ZWrite off, queue Background, drawn at the far plane.
// All sky content is analytic (RingSkyArch.hlsl). No local dither: the final pass dithers (14.7 H5).
Shader "Megastructure/RingSky"
{
    Properties
    {
        _DomeRadius ("Dome radius (m)", Float) = 100
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/RingSkyArch.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DomeRadius;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 d = normalize(v.positionOS.xyz);
                float3 posWS = _WorldSpaceCameraPos.xyz + d * _DomeRadius;
                o.positionCS = TransformWorldToHClip(posWS);
                // put the dome on the far plane so it never clips and never occludes geometry
            #if UNITY_REVERSED_Z
                o.positionCS.z = o.positionCS.w * 1e-7;
            #else
                o.positionCS.z = o.positionCS.w * (1.0 - 1e-7);
            #endif
                o.dir = d;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 c = RingSkyDome(d);
                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
