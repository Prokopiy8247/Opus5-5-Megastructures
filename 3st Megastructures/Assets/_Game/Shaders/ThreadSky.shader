// ThreadSky - the scene skybox of Thread (MASTER_PROMPT 14.2.3 M09/M10, 14.7.3, 14.7.4, 14.7.7).
// Owner: builder B02, revised by F1 (run06). One full-screen analytic sky: gradient, sun disc, stars, the far thread of
// beats 1-3, the giant planet, the Earth / cloud sea with the cable shadow band, the limb glow, and the bank and deck
// terms of the shared fog (ThreadFogApplySky). All maths lives in Include/ThreadSkyLib.hlsl; the uniforms are the _Th*
// globals computed in double by ProgressDirector.ApplyGlobals each frame. The sun and the planet are screen-space
// circular impostors built with the camera's view matrix (UNITY_MATRIX_V) and the pixel's own image-plane derivatives,
// so they stay round at every aspect ratio, render scale and in RenderTexture captures (critique A4 / B4 / C-16).
// URP 17.6 (docs/research/R2 1.5): the skybox is drawn after the opaques and before the transparents, depth tested
// against the opaque depth (ZTest LEqual, auto-flipped under reversed Z), into the MSAA camera colour target, only
// when the camera background type is Skybox; the material is RenderSettings.skybox (set by Beat00_Sky).
// No Unity fog keywords (all fog is ThreadCommon); output is un-quantised linear HDR.
Shader "Megastructure/ThreadSky"
{
    Properties
    {
        // cut-order switches of 14.8 item 4 ("sky richness"); full quality is 3 / 3 / 1
        [IntRange] _CloudOctaves ("Cloud octaves (3 -> 2 when cut)", Range(1, 3)) = 3
        [IntRange] _StarTiers ("Star tiers (3 -> 2 when cut)", Range(2, 3)) = 3
        _PlanetWobble ("Planet band wobble (0 = cut)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ThreadSky"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThreadSkyVert
            #pragma fragment ThreadSkyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // material properties (one UnityPerMaterial buffer, SRP Batcher layout rules)
            CBUFFER_START(UnityPerMaterial)
                float _CloudOctaves;
                float _StarTiers;
                float _PlanetWobble;
            CBUFFER_END

            #include "Include/ThreadSkyLib.hlsl"

            struct ThSkyAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ThSkyVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 rayWS : TEXCOORD0;   // object-space position of the sky mesh in world orientation (not normalised)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ThSkyVaryings ThreadSkyVert(ThSkyAttributes input)
            {
                ThSkyVaryings output = (ThSkyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The sky mesh is centred on the camera, so the interpolated position is the exact pixel ray once it is
                // normalised per pixel (also valid for the engine's own cubemap captures, R2 1.5).
                output.rayWS = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz);
                return output;
            }

            float4 ThreadSkyFrag(ThSkyVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d = SafeNormalize(input.rayWS);
                float3 c = ThreadSkyRadiance(d, _CloudOctaves, _StarTiers, _PlanetWobble);
                return float4(c, 1.0); // pal:ok (colour + opaque alpha)
            }
            ENDHLSL
        }
    }
    Fallback Off
}
