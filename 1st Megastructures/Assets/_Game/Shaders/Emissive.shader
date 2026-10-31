// =====================================================================================================================
// Megastructure/Emissive - unlit opaque emitters and cards (14.2 M_Lamp, 14.6 proxy cards, bore card, dark card).
//   _Recipe 0 LampAmber:    _EmissionColor (AMBER) x _EmissionStrength (8.0), fogged.
//   _Recipe 1 LampCold:     _EmissionColor (COLD_LAMP) x _EmissionStrength, fogged.
//   _Recipe 2 ProxyCard:    flat unlit _BaseColor (linear, e.g. an equal-luma proxy or a dark interior card), fogged.
//   _Recipe 3 GradientCard: _CardMode 0 = vertical gradient lerp(_CardBottom, _CardTop, UV0.y) (dark card, doorway glow),
//                           _CardMode 1 = Exterior sky FogSky(view direction, domain 2) (the bore card: a window onto the
//                           outside sky); x _EmissionStrength; fog by the material domain only when _CardFog = 1.
// Lamp modes 0/1 may opt in to the downward-view attenuation of R1-01 (_DownViewAtten, default 0; x global _AbDownView.x).
// Opaque, single-sided by default (_Cull Back: the bore and dark cards are culled from behind). Passes: ForwardLit,
// ShadowCaster, DepthOnly, DepthNormals. _AbFade dithered tier fades as everywhere.
// =====================================================================================================================
Shader "Megastructure/Emissive"
{
    Properties
    {
        [Enum(LampAmber,0,LampCold,1,ProxyCard,2,GradientCard,3)] _Recipe ("Mode", Float) = 0
        [MainColor] _BaseColor ("Card Color (ProxyCard)", Color) = (0.05, 0.06, 0.07, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (1.0, 0.706, 0.353, 1)
        _EmissionStrength ("Emission Strength", Float) = 8
        _CardTop ("Card Top", Color) = (0.039, 0.063, 0.082, 1)
        _CardBottom ("Card Bottom", Color) = (0.027, 0.043, 0.063, 1)
        [Enum(Gradient,0,ExteriorSky,1)] _CardMode ("Card Mode", Float) = 0
        _CardFog ("Card Fog (0/1)", Float) = 1
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine min width (px, 0 = off)", Float) = 0
        _DistFade ("Distance Fade (start, end, mode)", Vector) = (0, 0, 0, 0)
        _DownViewAtten ("Down-View Attenuation opt-in (lamp modes, x global _AbDownView.x)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" "IgnoreProjector" = "True" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _CardTop;
            float4 _CardBottom;
            float4 _DistFade;
            float  _Recipe;
            float  _EmissionStrength;
            float  _CardMode;
            float  _CardFog;
            float  _AbDomain;
            float  _AbFade;
            float  _ThinLine;
            float  _DownViewAtten;
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment EmissiveFrag
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"

            half4 EmissiveFrag(AbVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                AbFadeClip(_AbFade, input.positionCS.xy);
                AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);

                int mode = (int)_Recipe;
                int domain = (int)_AbDomain;
                float3 color;
                bool fog = true;
                float dv;
                float3 v = AbViewRay(input.positionWS, dv);
                // R2-21 / lead D4: warm lamp heads keep their 8.0 peak only in a small white-hot core (N.V > 0.97 of a
                // convex head: within about 10-24 % of its radius); everywhere else the emitter is hue-safe amber (AbHueSafe),
                // so lamp heads, lenses and backings read amber, not cream, through the Neutral tonemapper
                float3 Nf = AbSafeNormalize(input.normalWS, -v);
                // curved heads only: a flat lens, backing or window facing the camera (fwidth(N) = 0) has no white-hot core
                // (ledge note, shaders_r32 KS3 globe top pixels (246, 221, 183), S 0.26: an N.V > 0.90 core is 44 % of the radius;
                // 0.97-0.995 keeps the raw value within 10-24 % of the radius, the white-hot point of D4)
                float core = smoothstep(0.97, 0.995, abs(dot(Nf, -v))) * saturate(200.0 * length(fwidth(Nf)));
                [branch] if (mode == 2)
                {
                    color = _BaseColor.rgb;
                }
                else if (mode == 3)
                {
                    if (_CardMode > 0.5)
                    {
                        color = FogSky(v, AB_DOMAIN_EXTERIOR);
                    }
                    else
                    {
                        color = lerp(_CardBottom.rgb, _CardTop.rgb, saturate(input.uv.y));
                    }
                    color *= _EmissionStrength;
                    color = lerp(AbWarmEmission(color), color, core);
                    fog = _CardFog > 0.5;
                }
                else
                {
                    float downAlbedo;
                    color = _EmissionColor.rgb * (_EmissionStrength * AbDownViewEmission(input.positionWS, _DownViewAtten, downAlbedo));
                    color = lerp(AbWarmEmission(color), color, core);
                }
                if (fog) color = AbyssApplyFog(color, input.positionWS, domain);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

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

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbDepthOnlyFrag
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
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
            #pragma vertex AbVert
            #pragma fragment AbDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
