// Megastructure/ThreadEmissive - emissive materials of Thread (MASTER_PROMPT 14.2.3 M08 windows and M11 beacon,
// 14.5.6 line 726/736, 14.5.7 line 743/747/754, 14.6 beacon ramps). Owner: B01 (material shaders).
// Every result is fogged by the shared fog and its emissive output is clamped to <= 6 (TC_BeaconEmissive)
// by a hue-preserving scale (no firefly can exceed the bloom budget, 14.5.7 line 754). SRP Batcher
// compatible, GPU instancing. Passes: ForwardLit, DepthOnly, DepthNormals (emitters cast no shadow).
//
// _EMISSIVE_BEACON  the crystal: _BaseColor * _Emission * _ThBeacon.x (ramp, s 700-780) * pulse, opaque, on top of
//                   a flat-shaded _BaseColor body (so the unlit crystal is never a black hole). The pulse
//                   _ThBeacon.z = 1 +- 0.06 is normalised by 1 / 1.06 so its peak is exactly _Emission (6.0):
//                   the +-6 percent pulse survives the <= 6 cap instead of being clipped flat.
// _EMISSIVE_HALO    camera-facing billboard built in the vertex shader from a quad whose uv0 is in [-1, 1],
//                   centred on the object pivot, radius _HaloRadius x the object's uniform scale (two halo
//                   objects of scale 1 and 4 with _HaloRadius 3 give the 3 m and 12 m halos); smooth radial
//                   falloff with zero value and slope at the rim (no hard edge), soft depth fade against
//                   _CameraDepthTexture, x _ThBeacon.y (ramp, s 730-790) x pulse; additive (Blend One One,
//                   ZWrite Off, queue Transparent+10). Fog attenuates it by the transmittance only (an additive
//                   layer must not add fog colour a second time).
// _EMISSIVE_WINDOW  instanced window quads: _BaseColor * _Emission (CREAM x 1.5) near; beyond _FadeNear .. _FadeFar
//                   (150 .. 200 m) they fade to the bead body as ThreadFlat would shade it with the bead colour
//                   _SecondColor (set per bead through a MaterialPropertyBlock), so the dots dissolve into the lens.
// (no keyword)      plain emissive _BaseColor * _Emission, opaque.
// Properties: _BaseColor, _Emission, _SecondColor (window far colour), _FadeNear, _FadeFar, _HaloRadius (m);
//             render state _SrcBlend, _DstBlend, _ZWrite, _Cull (Off by default: halo quads, single-sided windows).
// Keywords (shader_feature_local): _EMISSIVE_BEACON, _EMISSIVE_HALO, _EMISSIVE_WINDOW.
Shader "Megastructure/ThreadEmissive"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Float) = 1
        _SecondColor ("Far colour (window: bead colour)", Color) = (1, 1, 1, 1)
        _FadeNear ("Far fade near (m)", Float) = 150
        _FadeFar ("Far fade far (m)", Float) = 200
        _HaloRadius ("Halo radius (m)", Float) = 3

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [ToggleUI] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _SecondColor;
            float _Emission;
            float _FadeNear;
            float _FadeFar;
            float _HaloRadius;
        CBUFFER_END

        #include "Include/ThreadCommon.hlsl"

        #define TE_HALO_SOFT  0.5    // soft depth fade distance as a fraction of the halo radius
        #define TE_HALO_CORE  12.0   // core concentration of the halo falloff: f = (1 - r^2)^3 / (1 + 12 r^2)
        #define TE_HALO_NEAR  1.0    // fade the halo out within 1 m of the near plane

        struct ThEmisAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv0        : TEXCOORD0;   // halo: quad corner in [-1, 1]
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ThEmisVaryings
        {
            float4 positionCS        : SV_POSITION;
            float3 positionWS        : TEXCOORD0;
            centroid float3 normalWS : TEXCOORD1;
            float4 haloData          : TEXCOORD2;   // halo: xy = quad uv, z = soft-fade distance (m), w = eye depth (m)
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ThEmisVaryings ThEmisVert(ThEmisAttributes v)
        {
            ThEmisVaryings o = (ThEmisVaryings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        #if defined(_EMISSIVE_HALO)
            float4x4 m = GetObjectToWorldMatrix();
            float3 centreWS = m._m03_m13_m23;
            float radius = _HaloRadius * length(m._m00_m10_m20);
            float3 rightWS = UNITY_MATRIX_V._m00_m01_m02;    // camera axes in world space (view-plane billboard)
            float3 upWS = UNITY_MATRIX_V._m10_m11_m12;
            float3 positionWS = centreWS + (rightWS * v.uv0.x + upWS * v.uv0.y) * radius;
            o.normalWS = SafeNormalize(_WorldSpaceCameraPos - centreWS);
            o.haloData = float4(v.uv0, TE_HALO_SOFT * radius, -TransformWorldToView(positionWS).z); // pal:ok (packed data)
        #else
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
        #endif
            o.positionWS = positionWS;
            o.positionCS = TransformWorldToHClip(positionWS);
            return o;
        }

        // Hue-preserving cap of the emissive output at TC_BeaconEmissive (6.0).
        float3 ThEmisClamp(float3 c)
        {
            float mx = max(max(c.r, c.g), max(c.b, 1e-6));
            return c * min(1.0, TC_BeaconEmissive / mx);
        }

        // Beacon pulse normalised so that its peak equals 1 (see the header).
        float ThEmisPulse() { return _ThBeacon.z / (1.0 + TC_BeaconPulseDepth); }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThEmisVert
            #pragma fragment ThEmisForwardFrag
            #pragma shader_feature_local _ _EMISSIVE_BEACON _EMISSIVE_HALO _EMISSIVE_WINDOW

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #define THREAD_PASS_FORWARD 1
            #include "Include/ThreadFlatPasses.hlsl"

            // The bead body / crystal body as ThreadFlat shades it (default rim, cable shadow on, as the bead materials).
            float3 ThEmisShadeBody(float3 albedo, float3 N, float3 positionWS, float fogAmount)
            {
                ThreadSurface s;
                s.albedo = albedo;
                s.shadeAlbedo = albedo * TH_SHADE_TINT;
                s.normalWS = N;
                s.positionWS = positionWS;
                s.ao = 1.0;
                s.heightShade = 1.0;
                s.shadowAtten = ThMainLightShadow(positionWS);
                s.rimCoral = 0.0;
                s.rimScale = 1.0;
                s.cableShadowOn = 1.0;
                s.forceShade = 0.0;
                s.fogAmount = fogAmount;
                return ThreadShadeFlat(s);
            }

            float4 ThEmisForwardFrag(ThEmisVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 positionWS = i.positionWS;
                float tauDist, tauCloud;
                ThreadFogTaus(positionWS, tauDist, tauCloud);

            #if defined(_EMISSIVE_HALO)
                float rho2 = dot(i.haloData.xy, i.haloData.xy);
                float edge = saturate(1.0 - rho2);
                float falloff = edge * edge * edge / (1.0 + TE_HALO_CORE * rho2);   // C2 zero at the rim
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float soft = saturate((sceneEye - i.haloData.w) / max(i.haloData.z, 1e-3));
                float nearFade = saturate((i.haloData.w - _ProjectionParams.y) / TE_HALO_NEAR);
                float transmittance = exp(-min(tauDist + tauCloud, 80.0));
                float k = _Emission * falloff * soft * nearFade * saturate(_ThBeacon.y) * ThEmisPulse() * transmittance;
                return float4(ThEmisClamp(_BaseColor.rgb * k), 0.0);                 // pal:ok (additive, alpha unused)
            #else
                float3 N = SafeNormalize(i.normalWS);
                N = IS_FRONT_VFACE(isFront, N, -N);
                float fogAmount = ThreadFogAmountFromTaus(tauDist, tauCloud);
                #if defined(_EMISSIVE_WINDOW)
                    float dist = distance(positionWS, _WorldSpaceCameraPos);
                    float farWeight = smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 1e-3), dist);
                    float3 glow = _BaseColor.rgb * _Emission;
                    float3 body = ThEmisShadeBody(_SecondColor.rgb, N, positionWS, fogAmount);
                    float3 radiance = lerp(glow, body, farWeight);
                #elif defined(_EMISSIVE_BEACON)
                    float3 body = ThEmisShadeBody(_BaseColor.rgb, N, positionWS, fogAmount);
                    float3 radiance = body + _BaseColor.rgb * (_Emission * saturate(_ThBeacon.x) * ThEmisPulse());
                #else
                    float3 radiance = _BaseColor.rgb * _Emission;
                #endif
                float3 color = ThEmisClamp(ThreadFogMix(radiance, tauDist, tauCloud));
                return float4(color, 1.0);                                             // pal:ok (opaque)
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThEmisVert
            #pragma fragment ThEmisDepthFrag
            #pragma shader_feature_local _ _EMISSIVE_BEACON _EMISSIVE_HALO _EMISSIVE_WINDOW
            #pragma multi_compile_instancing

            half ThEmisDepthFrag(ThEmisVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #if defined(_EMISSIVE_HALO)
                clip(-1.0);   // the additive halo never writes depth
            #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThEmisVert
            #pragma fragment ThEmisDepthNormalsFrag
            #pragma shader_feature_local _ _EMISSIVE_BEACON _EMISSIVE_HALO _EMISSIVE_WINDOW
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            void ThEmisDepthNormalsFrag(ThEmisVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #if defined(_EMISSIVE_HALO)
                clip(-1.0);
            #endif
                float3 n = SafeNormalize(i.normalWS);
                n = IS_FRONT_VFACE(isFront, n, -n);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(n);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(remappedOctNormalWS), 0.0); // pal:ok (packed normal)
            #else
                outNormalWS = half4(n, 0.0);                                    // pal:ok (normal)
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
