// Megastructure/ThreadLine - thin-line material with a screen-space minimum width (MASTER_PROMPT 14.7.2,
// 14.2.3 M07, 14.5.4 cable ribbons, 14.5.7 KF7c thread). Owner: B01 (material shaders).
// Minimum full width w_min = _MinWidthPx, or the global _ThScreen.y = max(1.5, 1.75 H / 1080) px when 0.
// Energy preservation: when the true width is below w_min, alpha = max(_AlphaFloor, true / w_min).
//
// _LINE_TUBE   (handrails) an 8-gon tube mesh with outward smooth normals; each vertex is pushed radially
//              (= along its clip-space normal for a round tube) so the projected diameter is >= w_min. Opaque;
//              the energy alpha drives alpha-to-coverage, which needs _AlphaToMask = 1 and MSAA. Back faces are
//              discarded in the shader, so _Cull may stay Off. Lit with ThreadShadeFlat (main-light shadows +
//              analytic cable shadow), fogged by the shared fog. Passes: ForwardLit, DepthOnly, DepthNormals
//              (rails do not cast shadows: no ShadowCaster).
// _LINE_RIBBON (cable ribbons beyond 20 km, KF7c thread) camera-facing ribbon: the mesh gives centre-line
//              vertices with uv1.y = side (-1 / +1) and the line direction in normal.xyz; the vertex stage
//              expands the quad perpendicular to the projected line to max(true width, w_min) + a 1 px skirt.
//              The fragment computes its exact signed pixel distance d to the projected centre line from the
//              homogeneous image line (robust behind the camera and against near-plane clipping, independent of
//              tessellation), writes coverage saturate(0.5 + halfWidthPx - |d|), shades a cylinder across
//              u = d / halfWidthPx, and outputs premultiplied colour (Blend One OneMinusSrcAlpha, ZWrite Off,
//              Transparent queue, Cull Off).
// Properties: _BaseColor, _ShadeColor, _UseShadeColor, _LineWidth (true width, m), _MinWidthPx (0 = global),
//             _AlphaFloor (0.35), _CableShadowOn; render state _SrcBlend, _DstBlend, _ZWrite, _Cull,
//             _AlphaToMask (0 by default: must be 1 only for opaque tubes with MSAA > 1);
//             tube coverage fades: _FadeNear / _FadeFar (distance, off when _FadeFar <= 0: the handrails fade with the
//             posts, critique run07 B3) and _BelowFade (0/1, far below the walker at altitude, A-r2-1), by A2C (dither
//             without MSAA and in the depth passes); _BelowHaze (0/1, ribbons: aerial perspective far below, A5).
// Keywords (shader_feature_local): _LINE_TUBE (also the default when no keyword is set), _LINE_RIBBON.
Shader "Megastructure/ThreadLine"
{
    Properties
    {
        _BaseColor ("Base colour (linear lit albedo)", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade colour", Color) = (1, 1, 1, 1)
        [ToggleUI] _UseShadeColor ("Use shade colour (else base x shade tint)", Float) = 0
        _LineWidth ("True width (m)", Float) = 0.07
        _MinWidthPx ("Minimum width (px, 0 = global)", Float) = 0
        _AlphaFloor ("Energy alpha floor", Float) = 0.35
        [ToggleUI] _CableShadowOn ("Analytic cable shadow", Float) = 0
        _FadeNear ("Distance fade near (m, tube)", Float) = 0
        _FadeFar ("Distance fade far (m, 0 = off, tube)", Float) = 0
        [ToggleUI] _BelowFade ("Fade far below the walker at altitude (tube)", Float) = 0
        [ToggleUI] _BelowHaze ("Haze far below the camera at altitude (ribbon)", Float) = 0

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [ToggleUI] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [ToggleUI] _AlphaToMask ("Alpha to coverage (tube with MSAA)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadeColor;
            float _UseShadeColor;
            float _LineWidth;
            float _MinWidthPx;
            float _AlphaFloor;
            float _CableShadowOn;
            float _FadeNear;
            float _FadeFar;
            float _BelowFade;
            float _BelowHaze;
        CBUFFER_END

        #include "Include/ThreadCommon.hlsl"
        #include "Include/ThreadPatterns.hlsl"   // coverage fades and the ordered dither shared with ThreadFlat

        #define TL_REF_HEIGHT 1080.0   // reference render height of the width rule (14.7.2 line 796)
        #define TL_FADE_A2C_MIN 0.002  // below this keep factor the A2C mask is empty anyway: discard

        // Tube coverage fade (distance x far below), the same keep factor as ThreadFlat's posts in every pass.
        bool ThLineFadeOn() { return _FadeFar > 0.0 || _BelowFade > 0.5; }
        float ThLineFadeKeep(float3 positionWS)
        {
            float keep = (_FadeFar > 0.0) ? ThDistanceKeep(positionWS, _FadeNear, _FadeFar) : 1.0;
            if (_BelowFade > 0.5) keep *= ThBelowFadeKeep(positionWS);
            return keep;
        }

        struct ThLineAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;      // tube: outward smooth normal; ribbon: line direction
            float4 color      : COLOR;
            float4 uv1        : TEXCOORD1;   // ribbon: y = side (-1 / +1)
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ThLineVaryings
        {
            float4 positionCS        : SV_POSITION;
            float3 positionWS        : TEXCOORD0;   // tube: surface point; ribbon: centre-line point
            centroid float3 normalWS : TEXCOORD1;   // tube: surface normal; ribbon: line direction
            float4 lineData          : TEXCOORD2;   // tube: x = energy alpha; ribbon: xyz = image line (pixel units)
            float2 gw                : TEXCOORD3;   // ribbon: (g, w) with d = g / w the signed pixel distance
            float4 color             : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // Minimum full width in pixels: material override, else the global (ProgressDirector), else the rule.
        float ThLineMinWidthPx()
        {
            float m = (_MinWidthPx > 0.0) ? _MinWidthPx : _ThScreen.y;
            if (m < 0.5)
                m = max(TC_ThreadMinWidthFloorPx, TC_ThreadMinWidthPx * GetScaledScreenParams().y / TL_REF_HEIGHT);
            return m;
        }

        float ThLineAlphaFloor() { return (_AlphaFloor > 0.0) ? _AlphaFloor : TC_ThreadAlphaFloor; }

        // Pixels per metre at unit clip w (perspective: w = view depth; orthographic: w = 1 and this is exact).
        float ThLineFocalPx() { return abs(UNITY_MATRIX_P._m11) * 0.5 * GetScaledScreenParams().y; }

        // Tube: radial push of every vertex about its centre line so the projected diameter is >= w_min.
        void ThLineTubeVertex(ThLineAttributes v, inout ThLineVaryings o)
        {
            float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            float radius = 0.5 * _LineWidth;
            float3 axisWS = posWS - nWS * radius;                 // the 8-gon vertex lies on radius _LineWidth / 2
            float4 axisCS = TransformWorldToHClip(axisWS);
            float minPx = ThLineMinWidthPx();
            float trueDiamPx = _LineWidth * ThLineFocalPx() / max(axisCS.w, 1e-4);
            float inflate = max(1.0, minPx / max(trueDiamPx, 1e-6));
            posWS = axisWS + nWS * (radius * inflate);
            o.positionCS = TransformWorldToHClip(posWS);
            o.positionWS = posWS;
            o.normalWS = nWS;
            o.lineData = float4(max(ThLineAlphaFloor(), saturate(trueDiamPx / minPx)), 0.0, 0.0, 0.0); // pal:ok (energy alpha)
            o.gw = float2(0.0, 1.0);
        }

        // Ribbon: screen-space expansion perpendicular to the projected line.
        void ThLineRibbonVertex(ThLineAttributes v, inout ThLineVaryings o)
        {
            float3 pWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 dirWS = TransformObjectToWorldDir(v.normalOS);              // SafeNormalize inside
            float2 S = GetScaledScreenParams().xy;

            // Homogeneous image line L (dot(L, (x, y, w)) = 0 on the projected line): the cross product of the image
            // of the line point nearest the camera (small coordinates, so no cancellation at 200 km) and the image of
            // the line's point at infinity. It is the same for every vertex of the line, in front of or behind the
            // camera, so the hardware clipper reproduces the exact screen-space ribbon.
            float3 cam = _WorldSpaceCameraPos;
            float3 pRef = pWS - dirWS * dot(pWS - cam, dirWS);
            float4 c0 = TransformWorldToHClip(pRef);
            float4 dc = mul(GetWorldToHClipMatrix(), float4(dirWS, 0.0));  // pal:ok (direction, w = 0)
            float3 L = cross(c0.xyw, dc.xyw);
            // in centred pixel units q = ndc * S / 2: signed distance d(q) = dot(Lpx, (q, 1)) once normalised
            float3 Lpx = float3(L.x * 2.0 / S.x, L.y * 2.0 / S.y, L.z);     // pal:ok (line coefficients)
            float len = length(Lpx.xy);

            float4 c = TransformWorldToHClip(pWS);
            if (len < 1e-10)
            {
                // the line points straight at the eye: it projects to a point; draw nothing
                o.positionCS = c;
                o.positionWS = pWS;
                o.normalWS = dirWS;
                o.lineData = 0.0;
                o.gw = float2(1e6, 1.0);
                return;
            }
            Lpx /= len;

            float trueHalfPx = (c.w > 1e-4) ? 0.5 * _LineWidth * ThLineFocalPx() / c.w : 0.0;
            float extentPx = max(trueHalfPx, 0.5 * ThLineMinWidthPx()) + 1.0;     // + 1 px skirt for the AA ramp
            float side = (v.uv1.y >= 0.0) ? 1.0 : -1.0;
            c.xy += Lpx.xy * (side * extentPx) * (2.0 / S) * c.w;                 // linear in clip space (w may be < 0)

            o.positionCS = c;
            o.positionWS = pWS;
            o.normalWS = dirWS;
            o.lineData = float4(Lpx, 0.0);                                      // pal:ok (line coefficients)
            // g / w at any fragment (after clipping too) = exact signed pixel distance to the centre line
            o.gw = float2(dot(Lpx, float3(c.xy * (0.5 * S), c.w)), c.w);         // pal:ok (homogeneous point)
        }

        ThLineVaryings ThLineVert(ThLineAttributes v)
        {
            ThLineVaryings o = (ThLineVaryings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        #if defined(_LINE_RIBBON)
            ThLineRibbonVertex(v, o);
        #else
            ThLineTubeVertex(v, o);
        #endif
            o.color = v.color;
            return o;
        }

        // Ribbon coverage terms at a fragment: signed pixel distance d, drawn half width, energy alpha.
        void ThLineRibbonCoverage(ThLineVaryings i, out float d, out float halfPx, out float alpha)
        {
            float w = max(i.gw.y, 1e-6);
            d = i.gw.x / w;
            float trueHalf = 0.5 * _LineWidth * ThLineFocalPx() / w;
            float minHalf = 0.5 * ThLineMinWidthPx();
            halfPx = max(trueHalf, minHalf);
            float cov = saturate(0.5 + halfPx - abs(d));
            float energy = max(ThLineAlphaFloor(), saturate(trueHalf / minHalf));
            alpha = cov * energy;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThLineVert
            #pragma fragment ThLineForwardFrag
            #pragma shader_feature_local _ _LINE_TUBE _LINE_RIBBON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define THREAD_PASS_FORWARD 1
            #include "Include/ThreadFlatPasses.hlsl"

            float4 ThLineForwardFrag(ThLineVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 positionWS = i.positionWS;
                float3 albedo = _BaseColor.rgb;
                float3 shade = (_UseShadeColor > 0.5) ? _ShadeColor.rgb : albedo * TH_SHADE_TINT;

                float tauDist, tauCloud;
                ThreadFogTaus(positionWS, tauDist, tauCloud);

                ThreadSurface s;
                s.albedo = albedo;
                s.shadeAlbedo = shade;
                s.positionWS = positionWS;
                s.ao = 1.0;
                s.heightShade = 1.0;
                s.rimCoral = 0.0;
                s.rimScale = 1.0;
                s.cableShadowOn = _CableShadowOn;
                s.forceShade = 0.0;
                s.fogAmount = ThreadFogAmountFromTaus(tauDist, tauCloud);

            #if defined(_LINE_RIBBON)
                float d, halfPx, alpha;
                ThLineRibbonCoverage(i, d, halfPx, alpha);
                clip(alpha - 1e-4);

                // cylinder normal across the line: side vector perpendicular to the line and the view ray, signed
                // so that it points toward +d on screen
                float3 dirWS = SafeNormalize(i.normalWS);
                float3 toCam = SafeNormalize(_WorldSpaceCameraPos - positionWS);
                float3 sideWS = cross(dirWS, toCam);
                float sl2 = dot(sideWS, sideWS);
                sideWS = (sl2 > 1e-10) ? sideWS * rsqrt(sl2) : SafeNormalize(cross(dirWS, float3(dirWS.y, dirWS.z, dirWS.x))); // pal:ok (any perpendicular)
                float4 cs = mul(GetWorldToHClipMatrix(), float4(sideWS, 0.0));      // pal:ok (direction)
                float4 cp = mul(GetWorldToHClipMatrix(), float4(positionWS, 1.0));  // pal:ok (point)
                float2 dq = (cs.xy * cp.w - cp.xy * cs.w) * (0.5 * GetScaledScreenParams().xy);
                sideWS *= (dot(i.lineData.xy, dq) >= 0.0) ? 1.0 : -1.0;
                float u = clamp(d / halfPx, -1.0, 1.0);
                s.normalWS = SafeNormalize(u * sideWS + sqrt(saturate(1.0 - u * u)) * toCam);
                s.shadowAtten = 1.0;   // ribbons are kilometres away: far beyond the shadow distance

                float3 color = ThreadFogMix(ThreadShadeFlat(s), tauDist, tauCloud);
                if (_BelowHaze > 0.5) color = ThBelowHaze(color, positionWS);          // identical to M05 at 20 km (A5)
                return float4(color * alpha, alpha);                                   // pal:ok (premultiplied)
            #else
                if (!isFront) clip(-1.0);   // closed tube: back faces never contribute (keeps A2C coverage exact)
                float lineAlpha = i.lineData.x;
                if (ThLineFadeOn())
                {
                    float keep = ThLineFadeKeep(positionWS);
                    if (_AlphaToMaskAvailable > 0.5)
                    {
                        clip(keep - TL_FADE_A2C_MIN);
                        lineAlpha *= keep;
                    }
                    else
                    {
                        ThDitherClip(i.positionCS.xy, keep);
                    }
                }
                s.normalWS = SafeNormalize(i.normalWS);
                s.shadowAtten = ThMainLightShadow(positionWS);
                float3 color = ThreadFogMix(ThreadShadeFlat(s), tauDist, tauCloud);
                return float4(color, lineAlpha);                                       // pal:ok (A2C energy alpha x fade)
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
            #pragma vertex ThLineVert
            #pragma fragment ThLineDepthFrag
            #pragma shader_feature_local _ _LINE_TUBE _LINE_RIBBON
            #pragma multi_compile_instancing

            half ThLineDepthFrag(ThLineVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #if defined(_LINE_RIBBON)
                clip(-1.0);   // blended ribbons never write depth
            #else
                if (!isFront) clip(-1.0);
                if (ThLineFadeOn()) ThDitherClip(i.positionCS.xy, ThLineFadeKeep(i.positionWS));
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
            #pragma vertex ThLineVert
            #pragma fragment ThLineDepthNormalsFrag
            #pragma shader_feature_local _ _LINE_TUBE _LINE_RIBBON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            void ThLineDepthNormalsFrag(ThLineVaryings i, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            #if defined(_LINE_RIBBON)
                clip(-1.0);
            #else
                if (!isFront) clip(-1.0);
                if (ThLineFadeOn()) ThDitherClip(i.positionCS.xy, ThLineFadeKeep(i.positionWS));
            #endif
                float3 n = SafeNormalize(i.normalWS);
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
