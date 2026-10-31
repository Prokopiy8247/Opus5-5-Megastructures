// Megastructure/ThreadFlat - the "flat with a rim" material family of Thread (MASTER_PROMPT 14.2.2 - 14.2.3).
// Owner: B01 (material shaders). Opaque by default, SRP Batcher compatible (one UnityPerMaterial CBUFFER shared
// by every pass), GPU instancing, passes ForwardLit / ShadowCaster / DepthOnly / DepthNormals. Lighting is the
// recipe ThreadShadeFlat of ThreadCommon.hlsl: sun from _ThSunDir/_ThSunColor (not URP's light colour), URP
// main-light cascaded soft shadows and the analytic cable shadow multiplied into litTotal, the three-colour
// ambient, the cream (or coral) rim, vertex AO, then the one shared fog (ThreadFogTaus once, ThreadFogMix).
// Unity fog is not used (no multi_compile_fog). No textures; detail comes from the world-space patterns of
// Include/ThreadPatterns.hlsl, all filtered analytically.
//
// Properties (set by MaterialFactory; colours are linear palette tokens):
//   _BaseColor      lit albedo
//   _ShadeColor     shade albedo, used when _UseShadeColor = 1 (else shade = _BaseColor * shade tint)
//   _UseShadeColor  0/1
//   _ShadeTint      Vector: xyz = per-material shade tint, used instead of TH_SHADE_TINT when w > 0.5 (default off;
//                   applies to every derived shade albedo: the base without _UseShadeColor and the pattern colours)
//   _SecondColor    pattern colour 2 (steps riser, mandala lines, cable bands, deck stripes)
//   _ThirdColor     pattern colour 3 (steps inlay stripes)
//   _RimTint        0 = CREAM rim, 1 = lerp(CREAM, CORAL, 0.5) rim
//   _RimScale       rim multiplier (default 1)
//   _CableShadowOn  0/1 analytic cable shadow (14.7.7)
//   _ShadowMode     0/1 colossus ground-shadow mode: litTotal 0, alpha = vertex colour A, no depth/shadow output
//   _Sway           robe hem sway amplitude (m) x vertex colour B, period 11 s (_ThTime.y)
//   _ReliefFade     0/1 cable ridge relief fade (tangent.xyz = smooth normal, uv1.x = relief)
//   _Emission       adds _BaseColor * _Emission (default 0)
//   _FadeNear/_FadeFar  distance fade-out (m), off when _FadeFar <= 0; all passes. ForwardLit outputs it as
//                   alpha-to-coverage when _AlphaToMask = 1 and the opaque pass has MSAA (URP _AlphaToMaskAvailable),
//                   else (and in the shadow / depth / normals passes) as a screen-space ordered dither
//   _BelowFade      0/1 walkway far below the walker: once the virtual altitude is high (beats 6-7), fragments 30-70 m
//                   below the camera fade out (A-r2-1), through the same coverage path as the distance fade
//   _BelowHaze      0/1 aerial perspective of the cable far below the camera (beats 6-7, A5): ThBelowHaze
//   render state: _SrcBlend, _DstBlend, _ZWrite, _Cull, _OffsetFactor, _OffsetUnits, _AlphaToMask (ForwardLit; 0 by
//                   default, 1 only for opaque materials with a coverage fade, e.g. RailPost, Deck, Arm), _ShadowCull
//                   (ShadowCaster cull, Back by default; Off for the thin deck and arm casters, whose normal-offset
//                   inset otherwise leaks lit slivers into their shadow on the cable, critique run07 B7)
// Keywords (shader_feature_local, at most one per material):
//   _PATTERN_SALT (M01), _PATTERN_STEPS (M03), _PATTERN_MANDALA (M04), _PATTERN_CABLE (M05), _PATTERN_DECK (M06)
//   (_PATTERN_CABLE also draws _ThirdColor marker plates; _PATTERN_DECK reads uv1.xy = lateral, height)
Shader "Megastructure/ThreadFlat"
{
    Properties
    {
        _BaseColor ("Base colour (linear lit albedo)", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade colour", Color) = (1, 1, 1, 1)
        [ToggleUI] _UseShadeColor ("Use shade colour (else base x shade tint)", Float) = 0
        _ShadeTint ("Shade tint override (xyz, on when w = 1)", Vector) = (0, 0, 0, 0)
        _SecondColor ("Second colour (pattern)", Color) = (1, 1, 1, 1)
        _ThirdColor ("Third colour (pattern)", Color) = (1, 1, 1, 1)
        [ToggleUI] _RimTint ("Coral rim tint", Float) = 0
        _RimScale ("Rim scale", Float) = 1
        [ToggleUI] _CableShadowOn ("Analytic cable shadow", Float) = 0
        [ToggleUI] _ShadowMode ("Ground-shadow mode", Float) = 0
        _Sway ("Hem sway amplitude (m)", Float) = 0
        [ToggleUI] _ReliefFade ("Cable relief fade", Float) = 0
        _Emission ("Emission", Float) = 0
        _FadeNear ("Distance fade near (m)", Float) = 0
        _FadeFar ("Distance fade far (m, 0 = off)", Float) = 0
        [ToggleUI] _AlphaToMask ("Alpha to coverage (coverage fade with MSAA)", Float) = 0
        [ToggleUI] _BelowFade ("Fade far below the walker at altitude", Float) = 0
        [ToggleUI] _BelowHaze ("Haze far below the camera at altitude", Float) = 0

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [ToggleUI] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _ShadowCull ("Shadow caster cull", Float) = 2
        _OffsetFactor ("Depth offset factor", Float) = 0
        _OffsetUnits ("Depth offset units", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Identical in every pass and every variant (SRP Batcher). Render-state properties are not HLSL variables,
        // except _AlphaToMask, which ForwardLit also reads to choose A2C over the dither.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadeColor;
            float4 _SecondColor;
            float4 _ThirdColor;
            float4 _ShadeTint;
            float _UseShadeColor;
            float _RimTint;
            float _RimScale;
            float _CableShadowOn;
            float _ShadowMode;
            float _Sway;
            float _ReliefFade;
            float _Emission;
            float _FadeNear;
            float _FadeFar;
            float _AlphaToMask;
            float _BelowFade;
            float _BelowHaze;
        CBUFFER_END

        #define THREAD_FLAT 1
        #include "Include/ThreadCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThFlatForwardVert
            #pragma fragment ThFlatForwardFrag

            // material patterns
            #pragma shader_feature_local _ _PATTERN_SALT _PATTERN_STEPS _PATTERN_MANDALA _PATTERN_CABLE _PATTERN_DECK

            // URP 17.6 Lit keywords for main-light shadows + cascades + soft shadows, and Forward+ correctness
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define THREAD_PASS_FORWARD 1
            #include "Include/ThreadFlatPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_ShadowCull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ThFlatShadowVert
            #pragma fragment ThFlatShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define THREAD_PASS_SHADOW 1
            #include "Include/ThreadFlatPasses.hlsl"
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
            #pragma vertex ThFlatDepthVert
            #pragma fragment ThFlatDepthFrag
            #pragma multi_compile_instancing

            #define THREAD_PASS_DEPTH 1
            #include "Include/ThreadFlatPasses.hlsl"
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
            #pragma vertex ThFlatDepthNormalsVert
            #pragma fragment ThFlatDepthNormalsFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #define THREAD_PASS_DEPTHNORMALS 1
            #include "Include/ThreadFlatPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
