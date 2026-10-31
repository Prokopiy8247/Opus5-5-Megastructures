// =====================================================================================================================
// Megastructure/Surface - the lit opaque material of Abyss (MASTER_PROMPT 8.6, 14.2 recipes, 14.7.9 ThinLine).
// One shader, recipe selected by the uniform _Recipe (see Include/AbyssSurfaceRecipes.hlsl and docs/SHADERS.md):
//   0 ConcreteBoard, 1 ConcreteFloor, 2 ConcreteCyl, 3 SteelPainted, 4 SteelWeathered, 5 RodPolished, 6 Cable,
//   7 DoorLeaf, 8 HoneyTube.
// Passes: ForwardLit (UniversalForward, Forward+ cluster loop), ShadowCaster, DepthOnly, DepthNormals.
// SRP Batcher: one UnityPerMaterial CBUFFER, declared once in the SubShader-level include block (all passes share it).
// GPU instancing: multi_compile_instancing (no per-instance material properties). No DOTS variant: the GPU Resident
// Drawer skips these renderers on purpose (moving platform / per-frame transforms, 14.7.1).
// Fog: AbyssApplyFog with the material's _AbDomain; Unity fog keywords are never used.
// _KeyLightMask (0..1, default 1) scales only the main-light (Key) term of AbyssShadeLit (interiors the Key cannot reach).
// _BevelWidth (m, default 0.01): nominal chamfer width for the sub-pixel bevel fade of AbBevelResolve (R1-26).
// _DownViewAtten (0/1, default 0): opt-in to the global downward-view emission attenuation _AbDownView (R1-01).
// =====================================================================================================================
Shader "Megastructure/Surface"
{
    Properties
    {
        // no [Enum] drawer: Unity's Enum drawer takes at most 7 pairs ("Failed to create material drawer Enum" in every build log)
        _Recipe ("Recipe 0 Board 1 Floor 2 Cyl 3 SteelPainted 4 SteelWeathered 5 Rod 6 Cable 7 DoorLeaf 8 HoneyTube", Float) = 0
        [MainColor] _BaseColor ("Base Color", Color) = (0.365, 0.384, 0.404, 1)
        _Roughness ("Roughness", Range(0, 1)) = 0.88
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine min width (px, 0 = off)", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Float) = 0
        _MacroAmp ("Macro Variation (x +/-12 %)", Range(0, 3)) = 1
        _WearAmount ("Edge Wear (vertex G)", Range(0, 2)) = 1
        _RustAmount ("Rust Coverage (exact, <= 0.12)", Range(0, 0.12)) = 0.01
        _WearWidth ("Worn Path Half-Width m (floor, UV0.x)", Float) = 0.5
        _PanelCount ("Cyl Panel Count N", Float) = 115
        _CylRadius ("Cyl Radius m", Float) = 22
        _CylJoints ("Cyl Expansion Joints", Float) = 4
        _TopY ("HoneyTube Top Y", Float) = -1087
        _DepthFalloff ("HoneyTube Depth Falloff m", Float) = 140
        _FarTint ("Far Tint (albedo)", Color) = (0.365, 0.384, 0.404, 1)
        _FarRange ("Far Tint Range (start, end m)", Vector) = (0, 0, 0, 0)
        _DistFade ("Distance Fade (start, end, mode)", Vector) = (0, 0, 0, 0)
        _KeyLightMask ("Key Light Mask (main light only)", Range(0, 1)) = 1
        _BevelWidth ("Chamfer Width m (sub-pixel bevel fade)", Float) = 0.01
        _DownViewAtten ("Down-View Attenuation opt-in (x global _AbDownView.x)", Range(0, 1)) = 0
        _HoneyAir ("Honeycomb Air (aerial amount, aerial length m, tube-air depth m, tube-air amount)", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _AlphaToMask ("Alpha To Mask (ThinLine coverage under MSAA)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _FarTint;
            float4 _FarRange;
            float4 _DistFade;
            float4 _HoneyAir;
            float  _Recipe;
            float  _Roughness;
            float  _Metallic;
            float  _AbDomain;
            float  _AbFade;
            float  _ThinLine;
            float  _EmissionStrength;
            float  _MacroAmp;
            float  _WearAmount;
            float  _RustAmount;
            float  _WearWidth;
            float  _PanelCount;
            float  _CylRadius;
            float  _CylJoints;
            float  _TopY;
            float  _DepthFalloff;
            float  _KeyLightMask;
            float  _BevelWidth;
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
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment SurfaceForwardFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Assets/_Game/Shaders/Include/AbyssLighting.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssHoneycomb.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssSurfaceRecipes.hlsl"

            half4 SurfaceForwardFrag(AbVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                AbFadeClip(_AbFade, input.positionCS.xy);
                AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);

                // Top-level derivatives (all later branching is either uniform or derivative-free).
                float3 Ngeo = AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));
                Ngeo = IS_FRONT_VFACE(face, Ngeo, -Ngeo);
                float specKernel = AbSpecularAAKernel(Ngeo);
                float3 dNdx = ddx(Ngeo);
                float3 dNdy = ddy(Ngeo);

                AbRecipeInput ri;
                ri.P = input.patternLocal + input.patternOrigin;
                ri.dPdx = ddx(input.patternLocal);
                ri.dPdy = ddy(input.patternLocal);
                ri.N = Ngeo;
                ri.positionWS = input.positionWS;
                ri.uv0 = input.uv.xy;
                ri.duvdx = ddx(input.uv.xy);
                ri.duvdy = ddy(input.uv.xy);
                ri.color = input.color;

                AbSurface s = AbEvaluateRecipe((int)_Recipe, ri);
                s.roughness = AbApplySpecularAA(s.roughness, specKernel);
                s.roughness = AbGeometricSpecularAA(s.roughness, dNdx, dNdy);   // R2-17: no hot 1-px rims / hairlines
                s.emission += _EmissionColor.rgb * _EmissionStrength;
                float downAlbedo;
                float downEmit = AbDownViewEmission(input.positionWS, _DownViewAtten, downAlbedo);   // R1-01 (off unless driven)
                s.emission *= downEmit;
                s.albedo *= downAlbedo;

                float d;
                float3 viewRay = AbViewRay(input.positionWS, d);
                if (_FarRange.y > _FarRange.x)
                    s.albedo = lerp(s.albedo, _FarTint.rgb, smoothstep(_FarRange.x, _FarRange.y, d));
                AbThinLineShading(input.uv.z, -viewRay, s.normalWS, s.roughness);   // R2-17: sub-pixel members shade as their mean

                InputData inputData = AbMakeInputData(input.positionWS, s.normalWS, input.positionCS);
                float3 color = AbyssShadeLit(s, inputData, (int)_AbDomain, _KeyLightMask);
                // R2-04 / R2-19: tube air and aerial veil of the honeycomb (HoneyTube variants; 0 = off elsewhere)
                color = AbHoneyAir(color, _TopY - input.positionWS.y, d, _HoneyAir);
                color = AbyssApplyFog(color, input.positionWS, (int)_AbDomain);
                return half4(color, AbCoverageAlpha(input.uv.z));
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
