// =====================================================================================================================
// Megastructure/Glow - camera-facing light points with a minimum on-screen size (lamp dots, freight lights, the falling
// bolt, hoist lamps, honeycomb bridge lamps). Additive, ZWrite Off, fogged as an additive term (fades to nothing).
// Mesh contract: a quad whose vertices have positionOS.xy in [-0.5, 0.5] (z ignored), e.g. ShaderMeshes.GlowQuad().
// The object (or instance) matrix gives the light centre (translation) and its TRUE world diameter (uniform scale).
// Vertex stage: drawn diameter = max(true diameter, _MinPixels x pixel size at that distance) (>= 2.5 px), the quad
// spans 2x the drawn diameter for the soft falloff, and is pulled toward the camera by half the drawn diameter so it
// never cuts into the lamp head. Fragment: smooth Gaussian-like disc (sub-pixel stable, no hard edge), soft depth fade
// against _CameraDepthTexture. _Ignite = 1: brightness x per-instance ignite from _AbLampIgnite (14.5.7) evaluated at
// the instance position. _EnergyConserve 0..1 dims enlarged points by (true / drawn)^2 (0 = constant brightness).
// GPU instancing: one instanced batch per colour; no per-instance properties.
// =====================================================================================================================
Shader "Megastructure/Glow"
{
    Properties
    {
        [HDR] _EmissionColor ("Color", Color) = (1.0, 0.706, 0.353, 1)
        _EmissionStrength ("Strength", Float) = 8
        _MinPixels ("Minimum Diameter px", Float) = 2.5
        _EnergyConserve ("Energy Conservation 0..1", Range(0, 1)) = 0
        _Ignite ("Honeycomb Ignite (0/1)", Float) = 0
        _SoftDepth ("Soft Depth Fade m (min)", Float) = 0.05
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (additive: visibility = abs)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _EmissionColor;
            float  _EmissionStrength;
            float  _MinPixels;
            float  _EnergyConserve;
            float  _Ignite;
            float  _SoftDepth;
            float  _AbDomain;
            float  _AbFade;
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"
        ENDHLSL

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha   // premultiplied: alpha 0 = additive; alpha > 0 = an enlarged warm point's amber disc (R2-21)

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GlowVert
            #pragma fragment GlowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // R2-21 / lead D4: an enlarged (sub-pixel) warm point is drawn as a small amber disc that covers what lies behind
            // it: coverage = saturate(gain x profile) (about 1 on the core pixel, which receives about 0.45-0.6 of the profile)
            #define AB_GLOW_DISC_GAIN 3.0
            // energy of the windowed profile exp(-5 r^2) (1 - r^2)^2 over the quad = 0.4269 diam^2 = 4.269 sigma^2 (sigma = 0.316 diam):
            // the pixel-integrated points keep the brightness of the R1 look
            #define AB_GLOW_ENERGY 4.269

            struct GlowAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GlowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 quad       : TEXCOORD0;    // [-1, 1] across the quad
                float3 tint       : TEXCOORD1;    // ignited colour x strength (unfogged)
                float  eyeDepth   : TEXCOORD2;    // view depth of the sprite plane
                float  softDepth  : TEXCOORD3;    // soft fade distance (m)
                nointerpolation float4 centre : TEXCOORD4;   // xyz light centre (fog), w = drawn diameter in pixels
                nointerpolation float4 disc   : TEXCOORD5;   // rgb lamp colour x strength, w = amber-disc weight (R2-21)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            GlowVaryings GlowVert(GlowAttributes input)
            {
                GlowVaryings output = (GlowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 centre = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float trueDiam = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 toCam = _WorldSpaceCameraPos - centre;
                float dist = max(length(toCam), 1e-3);
                float3 toCamN = toCam / dist;
                float pixelWorld = dist * AbPixelAngle();
                float diam = max(trueDiam, max(_MinPixels, 2.5) * pixelWorld);

                // camera-facing basis (view matrix rows are the camera axes in world space)
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float2 q = input.positionOS.xy * 2.0;                      // [-1, 1]
                float pull = min(0.5 * diam, 0.5 * dist);
                float3 positionWS = centre + (right * q.x + up * q.y) * diam + toCamN * pull;

                float energy = lerp(1.0, saturate(trueDiam / diam) * saturate(trueDiam / diam), saturate(_EnergyConserve));
                float ignite = _Ignite > 0.5 ? AbLampIgnite(centre.xz) : 1.0;
                float vis = saturate(abs(_AbFade));                         // additive: visibility = fraction kept by the dither convention = |f|
                output.tint = _EmissionColor.rgb * (_EmissionStrength * energy * ignite * vis);
                // R2-21 / lead D4: an enlarged (sub-pixel) warm point is a small amber disc over the haze it sits on (an additive
                // point over the 0.2-0.5 haze reads cream or salmon: lead_b01 KS7b cores (184, 148, 141), hue 10, S 0.24). The
                // weight is 0 for true-size sprites, cold lamps, dim warm sprites (halos: strength <= 1) and energy-conserved
                // points; it follows ignition and the tier fade, so a disc fades in and out with its lamp
                float enlarged = smoothstep(1.2, 2.0, diam / max(trueDiam, 1e-4));
                float discW = enlarged * AbWarmth(_EmissionColor.rgb) * saturate(_EmissionStrength - 1.0) * energy * saturate(ignite * vis);
                output.disc = float4(_EmissionColor.rgb * _EmissionStrength, discW);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.quad = q;
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.softDepth = max(_SoftDepth, 0.5 * diam);
                float eyeC = max(-TransformWorldToView(centre + toCamN * pull).z, 1e-3);
                output.centre = float4(centre, diam / (eyeC * AbPixelAngle()));
                return output;
            }

            half4 GlowFrag(GlowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // soft disc: Gaussian exp(-5 r^2) of the quad radius (sigma = 0.316 x the drawn diameter), windowed to 0 at
                // the rim. R2-25: a point drawn at 2.5-6 px is integrated over each pixel's footprint (erf), so the energy a
                // pixel receives no longer jumps as the point moves by a fraction of a pixel (no twinkle in slow pans); the
                // pixel Gaussian is at least 0.8 px wide, its energy that of the R1 windowed profile (AB_GLOW_ENERGY)
                float r2 = dot(input.quad, input.quad);
                float window = saturate(1.0 - r2);
                float diamPx = input.centre.w;
                float sigmaPx = 0.3162278 * diamPx;
                float profile;
                if (sigmaPx < 2.0)
                {
                    float s = max(sigmaPx, 0.8) * 1.4142136;
                    float2 dpx = input.quad * diamPx;
                    float2 e = AbErf2((dpx + 0.5) / s) - AbErf2((dpx - 0.5) / s);
                    profile = 0.25 * e.x * e.y * (AB_GLOW_ENERGY * sigmaPx * sigmaPx) * saturate(4.0 * (1.0 - r2));
                }
                else
                {
                    profile = exp(-r2 * 5.0) * window * window;
                }
                // soft depth fade against the scene
                float raw = LoadSceneDepth(uint2(input.positionCS.xy));
                float sceneEye = (raw == UNITY_RAW_FAR_CLIP_VALUE) ? 1e7 : LinearEyeDepth(raw, _ZBufferParams);
                float soft = saturate((sceneEye - input.eyeDepth) / input.softDepth);
                float k = profile * soft;
                // R2-21 / lead D4: warm points stay amber through the Neutral tonemapper (hue-safe), then the additive fog
                float3 add = AbyssFogAdditive(AbWarmEmission(input.tint * k), input.centre.xyz, (int)_AbDomain);
                // enlarged warm points: an amber disc of coverage a over the (already fogged) background; its colour is the
                // hue-safe lamp dimmed by half of the fog (the in-scatter stays in the background around the disc)
                float a = saturate(AB_GLOW_DISC_GAIN * k);
                float T = AbyssFogAdditive(float3(1.0, 1.0, 1.0), input.centre.xyz, (int)_AbDomain).g;
                float w = input.disc.w * smoothstep(0.1, 0.4, T);           // far points hand over to the fogged additive dot
                float3 disc = AbWarmEmission(input.disc.rgb * lerp(1.0, T, 0.5)) * a;
                return half4(lerp(add, disc, w), a * w);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
