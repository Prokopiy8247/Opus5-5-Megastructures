// =====================================================================================================================
// Megastructure/Sky - the skybox (RenderSettings.skybox = M_Sky). Draws FogSky of the Shaft domain blended into the
// Exterior domain by _AbSkyBlend (14.2: Shaft sky below d 640, Exterior from d 668.54, smootherstep in between on the CPU).
// The same AbyssFog.hlsl as every surface, so the fog colour at the horizon equals the sky colour at the horizon and
// geometry reaching the far plane dissolves into the sky without a seam. HDR float output, never quantised here (the
// one display dither is FinalDither). No sun disc.
// =====================================================================================================================
Shader "Megastructure/Sky"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"

            struct SkyAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SkyVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            SkyVaryings SkyVert(SkyAttributes input)
            {
                SkyVaryings output = (SkyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;      // skybox mesh is centred on the camera, unrotated
                return output;
            }

            half4 SkyFrag(SkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float l = length(input.direction);
                float3 v = l > 1e-6 ? input.direction / l : float3(0.0, 0.0, 1.0);
                return half4(AbyssSkyColor(v), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
