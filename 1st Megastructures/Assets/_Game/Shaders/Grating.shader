// =====================================================================================================================
// Megastructure/Grating - analytic steel grating (14.2 M_Grating): pitch 0.04 m, bar 0.008 m, bar depth 0.03 m.
// Each bar family is box-filtered over ITS OWN pixel footprint and fades to its own mean when its pitch drops under 8 px
// (complete at 4 px), so the family running along the view direction stays readable as converging lines far beyond the
// family seen across (R1-27: the f01/f02 catwalk faded as a whole at the larger footprint and read as a solid slab).
// A bar's visible extent is its top plus the side face exposed by the bar depth at the viewing angle (a real grating
// closes up when seen edge-on); tops are lit paint, exposed side faces are darker (_SideShade), so a grazing view reads
// as bright bar tops over dark slots. Far mean opacity per family: m0 = 1 - sqrt(1 - _FarOpacity) (25 % for both
// families at normal incidence) rising toward 1 at grazing angles. Panels of _PanelSize (1 m) with a 30 mm banding bar
// (solid), a 6 mm dark seam and +/-4 % per-panel tone add the mid-scale rhythm that survives to 15-20 m.
// Under MSAA the coverage is exported with AlphaToMask (5 coverage levels at 4x, keep the fade); without MSAA it is a
// static dithered clip (_AlphaToMaskAvailable selects the path per draw). Depth-type passes clip by the same coverage.
// Pattern plane: the grating plane is taken from the dominant axis of the normal (x/z for floors). Lit like painted
// steel (STEEL_DARK), dark-neutral stains from two incommensurate equalised lookups (no 3 m repetition), fog via
// AbyssApplyFog. Passes: ForwardLit, ShadowCaster, DepthOnly, DepthNormals.
// =====================================================================================================================
Shader "Megastructure/Grating"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.149, 0.169, 0.196, 1)
        _Roughness ("Roughness", Range(0, 1)) = 0.6
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _RustAmount ("Rust Coverage (exact, <= 0.12)", Range(0, 0.12)) = 0.03
        _Pitch ("Pitch m", Float) = 0.04
        _Bar ("Bar Width m", Float) = 0.008
        _BarDepth ("Bar Depth m", Float) = 0.03
        _FarOpacity ("Far Mean Opacity", Range(0, 1)) = 0.25
        _SideShade ("Bar Side Shade (x paint)", Range(0, 1)) = 0.42
        _PanelSize ("Panel Size m (0 = none)", Float) = 1
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine min width (px, 0 = off)", Float) = 0
        _DistFade ("Distance Fade (start, end, mode)", Vector) = (0, 0, 0, 0)
        _KeyLightMask ("Key Light Mask (main light only)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        _AlphaToMask ("Alpha To Mask", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _DistFade;
            float  _Roughness;
            float  _Metallic;
            float  _RustAmount;
            float  _Pitch;
            float  _Bar;
            float  _BarDepth;
            float  _FarOpacity;
            float  _SideShade;
            float  _PanelSize;
            float  _AbDomain;
            float  _AbFade;
            float  _ThinLine;
            float  _KeyLightMask;
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"

        // Grating plane frame from the dominant normal axis: (Tu, Tv) tangents, returns pattern coordinates.
        void GratingFrame(float3 N, float3 P, out float3 Tu, out float3 Tv, out float2 uv)
        {
            float3 a = abs(N);
            if (a.y >= a.x && a.y >= a.z)      { Tu = float3(1, 0, 0); Tv = float3(0, 0, 1); }
            else if (a.x >= a.z)               { Tu = float3(0, 0, 1); Tv = float3(0, 1, 0); }
            else                               { Tu = float3(1, 0, 0); Tv = float3(0, 1, 0); }
            uv = float2(dot(P, Tu), dot(P, Tv));
        }

        struct GratingSample
        {
            float opacity;   // covered fraction of the pixel (0 = hole, 1 = steel)
            float shade;     // albedo factor of the covered part (bar tops 1, exposed sides _SideShade, seam, panel tone)
        };

        // One bar family: bars of width 'bar' every 'pitch' covering intervals of x, widened by depth * tanA (the side face
        // exposed at the viewing angle). Returns (visible coverage, top coverage), each fading to its own mean with its
        // own footprint between 6 and 3 px of pitch (R2-10: the 4 cm pitch must read within 6-8 m of the eye on the KS4
        // catwalk; the box filter keeps a 3 px period free of aliasing).
        float2 GratingFamily(float x, float fw, float tanA, float pitch, float bar, float open)
        {
            float w = min(bar + _BarDepth * tanA, pitch);
            float k = smoothstep(3.0, 6.0, pitch / max(fw, 1e-6));
            float m0 = 1.0 - sqrt(saturate(1.0 - _FarOpacity));
            float farMean = lerp(m0, 1.0, saturate(_BarDepth * tanA / open));
            float full = lerp(farMean, AbLineBox(x, pitch, w, fw), k);
            float topShare = bar / max(w, 1e-5);
            return float2(full, full * topShare);
        }

        GratingSample GratingEval(float3 P, float3 dPdx, float3 dPdy, float3 N, float3 positionWS)
        {
            GratingSample o;
            float3 Tu, Tv;
            float2 uv;
            GratingFrame(N, P, Tu, Tv, uv);
            float fwU = AbFootprint(dPdx, dPdy, Tu);
            float fwV = AbFootprint(dPdx, dPdy, Tv);
            float camDist;
            float3 V = -AbViewRay(positionWS, camDist);          // NaN-safe unit direction toward the camera
            float vn = max(abs(dot(V, N)), 0.05);
            float tanU = abs(dot(V, Tu)) / vn;
            float tanV = abs(dot(V, Tv)) / vn;
            float pitch = max(_Pitch, 1e-3);
            float bar = clamp(_Bar, 0.0, pitch);
            float open = max(pitch - bar, 1e-4);
            // bars running along v cover u-intervals and are widened by the depth seen along u (and vice versa)
            float2 cU = GratingFamily(uv.x, fwU, tanU, pitch, bar, open);
            float2 cV = GratingFamily(uv.y, fwV, tanV, pitch, bar, open);
            float opacity = 1.0 - (1.0 - cU.x) * (1.0 - cV.x);
            float top = 1.0 - (1.0 - cU.y) * (1.0 - cV.y);
            float topFrac = saturate(top / max(opacity, 1e-4));
            float shade = lerp(saturate(_SideShade), 1.0, topFrac);

            // panels: 30 mm solid banding bar (lit top) at every panel edge, a 6 mm dark seam in it, +/-4 % per-panel tone
            if (_PanelSize > 0.0)
            {
                float ps = _PanelSize;
                float bU = AbLineCoverage(uv.x, ps, 0.03, fwU);
                float bV = AbLineCoverage(uv.y, ps, 0.03, fwV);
                float band = 1.0 - (1.0 - bU) * (1.0 - bV);
                float sU = AbLineCoverage(uv.x, ps, 0.006, fwU);
                float sV = AbLineCoverage(uv.y, ps, 0.006, fwV);
                float seam = 1.0 - (1.0 - sU) * (1.0 - sV);
                float newOpacity = 1.0 - (1.0 - opacity) * (1.0 - band);
                shade = (shade * opacity * (1.0 - band) + 0.95 * band) / max(newOpacity, 1e-4);
                shade *= 1.0 - 0.6 * seam;
                opacity = newOpacity;
                int n = max((int)round(AB_NOISE_WRAP / ps), 1);
                float2 pc = uv / ps;
                float2 wx = AbCellBlend(pc.x, fwU / ps);
                int2 c = (int2)floor(pc);
                float t = (AbHash2(c, int2(n, n), 97u) * (1.0 - wx.x - wx.y)
                         + AbHash2(c - int2(1, 0), int2(n, n), 97u) * wx.x
                         + AbHash2(c + int2(1, 0), int2(n, n), 97u) * wx.y) * 2.0 - 1.0;
                shade *= 1.0 + 0.04 * t * AbFeatureFade(ps / max(max(fwU, fwV), 1e-6) / 2.0);
            }
            o.opacity = saturate(opacity);
            o.shade = shade;
            return o;
        }

        // Analytic grating coverage (0 = hole, 1 = steel) for a fragment (depth-type passes).
        float GratingCoverage(float3 P, float3 dPdx, float3 dPdy, float3 N, float3 positionWS)
        {
            return GratingEval(P, dPdx, dPdy, N, positionWS).opacity;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment GratingForwardFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Assets/_Game/Shaders/Include/AbyssLighting.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"

            half4 GratingForwardFrag(AbVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                AbFadeClip(_AbFade, input.positionCS.xy);
                AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);

                float3 N = AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                N = IS_FRONT_VFACE(face, N, -N);
                float specKernel = AbSpecularAAKernel(N);
                float3 dNdx = ddx(N);
                float3 dNdy = ddy(N);
                float3 P = input.patternLocal + input.patternOrigin;
                float3 dPdx = ddx(input.patternLocal);
                float3 dPdy = ddy(input.patternLocal);
                GratingSample g = GratingEval(P, dPdx, dPdy, N, input.positionWS);
                float coverage = g.opacity;

                float alphaOut = 1.0;
                if (_AlphaToMaskAvailable != 0.0)
                {
                    clip(coverage - 0.004);
                    alphaOut = coverage;
                }
                else
                {
                    clip(coverage - AbFadeNoise(input.positionCS.xy));
                }

                // painted steel; dark-neutral stains (a walking surface), soft edges, coverage-exact AbRustMask. R2-10: the
                // stain patches come from stochastic per-6 m-cell lookups (AbBombG: random offset and rotation per cell),
                // so no stain shape repeats along the gantry (the 7.4 / 4.5 m tiles of R1 repeated a paired blot every
                // 4.5-7.4 m); the two equalised values are combined by AbUniformMax2 (uniform: coverage kept)
                float4 na = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, P.xz / 7.4, dPdx.xz / 7.4, dPdy.xz / 7.4);
                float4 nb = SAMPLE_TEXTURE2D_GRAD(_AbNoiseTex, sampler_AbNoiseTex, P.xz / 4.5 + 0.31, dPdx.xz / 4.5, dPdy.xz / 4.5);
                float stainPatch = AbUniformMax2(AbBombG(P.xz, dPdx.xz, dPdy.xz, 7.4, 6.0, 222, 197u),
                                                 AbBombG(P.xz + 3.0, dPdx.xz, dPdy.xz, 4.44, 6.0, 222, 199u));
                float rust = AbRustMask(_RustAmount, stainPatch, nb.a, max(length(dPdx), length(dPdy)));
                float3 paint = _BaseColor.rgb * (0.97 + 0.06 * na.r) * g.shade;
                AbSurface s = AbDefaultSurface(N);
                // grime: dark-neutral, a little rougher than the paint and only 60 % strength. Glossy (0.4) stains caught the low
                // beat-6 Key as white puddles (shaders r01 / r02), fully matte non-metallic ones read as black decals (r03)
                float grime = rust;   // R2-10: AbStainColor itself is now only 8-14 % darker (full strength here)
                s.albedo = lerp(paint, AbStainColor(paint, na.b), grime);
                s.metallic = lerp(_Metallic, 0.6 * _Metallic, grime);
                s.roughness = AbGeometricSpecularAA(AbApplySpecularAA(lerp(max(_Roughness, 0.25), min(max(_Roughness, 0.25) + 0.2, 0.85), grime), specKernel), dNdx, dNdy);
                s.ao = saturate(input.color.r) * lerp(0.6, 1.0, saturate(g.shade));

                InputData inputData = AbMakeInputData(input.positionWS, N, input.positionCS);
                float3 color = AbyssShadeLit(s, inputData, (int)_AbDomain, _KeyLightMask);
                color = AbyssApplyFog(color, input.positionWS, (int)_AbDomain);
                return half4(color, alphaOut);
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
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define AB_SHADOW_CASTER_PASS
            #define ABYSS_DEPTH_ALPHA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            float AbyssDepthAlpha(AbVaryings input)
            {
                return GratingCoverage(input.patternLocal + input.patternOrigin, ddx(input.patternLocal), ddy(input.patternLocal),
                                       AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0)), input.positionWS);
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
            #pragma vertex AbVert
            #pragma fragment AbDepthOnlyFrag
            #pragma multi_compile_instancing
            #define ABYSS_DEPTH_ALPHA
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            float AbyssDepthAlpha(AbVaryings input)
            {
                return GratingCoverage(input.patternLocal + input.patternOrigin, ddx(input.patternLocal), ddy(input.patternLocal),
                                       AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0)), input.positionWS);
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
            #pragma vertex AbVert
            #pragma fragment AbDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #define ABYSS_DEPTH_ALPHA
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            float AbyssDepthAlpha(AbVaryings input)
            {
                return GratingCoverage(input.patternLocal + input.patternOrigin, ddx(input.patternLocal), ddy(input.patternLocal),
                                       AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0)), input.positionWS);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
