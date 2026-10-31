# Research log — Abyss

Compiled 2026-10-29 from six read-only research passes run in parallel (one researcher per topic). All web pages were accessed on **2026-10-29** and treated as untrusted data: no instructions found in them were followed, and no assets were downloaded.

**Ground truth.** The package source in this project's `Library/PackageCache` (URP 17.6.0, Core RP, URP config, Input System, UGUI, Test Framework), the Unity 6000.6.0f1 managed assemblies and XML docs, and the project's own `ProjectSettings` / `Assets/Settings` files take precedence over web docs. Where a web page and the source disagree, the source wins. Each section keeps its researcher's own verification tags (for example [S]/[D]/[U], [VS]/[VD]/[UV] or VERIFIED-SOURCE / VERIFIED-DOCS / UNVERIFIED); the key is stated at the top of each section. Anything marked unverified must be proven by a compile check, a runtime probe or a test in the built Windows player before it is relied on.

## Table of contents

1. [urp-shaders — Handwritten URP Lit shaders (URP 17.6.0)](#urp-shaders)
2. [urp-csharp — URP 17.6 C# configuration and renderer features](#urp-csharp)
3. [mesh-instancing — Procedural meshes and instancing](#mesh-instancing)
4. [capture-player — Deterministic capture, player build behaviour, text and CLI](#capture-player)
5. [audio — Procedural audio in Unity 6000.6 and DSP design](#audio)
6. [art-technique — Art direction and rendering-technique references](#art-technique)
7. [Source index](#source-index)

---

<a id="urp-shaders"></a>
## urp-shaders — Handwritten URP Lit Shaders: URP 17.6.0 (Unity 6000.6.0f1)

Researcher: read-only pass over the package source. Path aliases: `URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`, `CORE` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`. The package version is 17.6.0 (`URP/package.json:4`) [VERIFIED-SOURCE]. Tags: **[S]** = VERIFIED-SOURCE, **[D]** = VERIFIED-DOCS, **[U]** = UNVERIFIED.

### 0. Project context that affects shader authoring [S]

- `Assets/Settings/PC_Renderer.asset:56` has `m_RenderingMode: 2`, which is **Forward+** (enum values at `URP/Runtime/UniversalRenderer.cs:11-19`). Depth priming is off. SSAO is present with `Source: 1` (DepthNormals, enum at `ScreenSpaceAmbientOcclusion.cs:65-69`) and `AfterOpaque: 0`.
- `PC_RPAsset.asset` settings:
  - MSAA 1 (off), render scale 1
  - LOD cross-fade on, dithering type 1 (BlueNoise)
  - 4 cascades, soft shadows on with quality 3 (High)
  - additional lights per-pixel, with shadows
  - light layers on, light cookies on
  - GPU Resident Drawer mode 1, depth texture and opaque texture on
- `UniversalRenderPipelineGlobalSettings.asset:23` has `m_StripUnusedVariants: 1`. `GraphicsSettings.asset:49` has `m_BrgStripping: 2` (likely KeepAll [U]). `ProjectSettings.asset:197` has `strictShaderVariantMatching: 0`.
- GPU Resident Drawer only works with a cluster light loop (`URP/Runtime/Data/UniversalRenderPipelineAsset.cs:1755,1780`). Renderers whose shader has no `DOTS_INSTANCING_ON` variant are excluded from it (`CORE/Runtime/GPUDriven/SceneProcessors/WorldProcessor.cs:199-213`).

### 1. Exact keywords used by Lit (`URP/Shaders/Lit.shader`) [S]

| Feature | Exact pragma (17.6.0) | Line |
|---|---|---|
| Main light shadows / cascades / screen-space | `multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` | 144 |
| Additional lights | `multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS` | 145 |
| Additional light shadows | `multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS` | 147 |
| Soft shadows | `multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH` | 152 |
| SSAO | `multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION` | 153 |
| DBuffer decals | `multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3` | 155 |
| Light cookies | `multi_compile_fragment _ _LIGHT_COOKIES` | 156 |
| Light layers | `multi_compile _ _LIGHT_LAYERS` | 157 |
| **Forward+ / cluster** | `multi_compile _ _CLUSTER_LIGHT_LOOP` | 158 |
| Rendering layers (MRT write) | `#include_with_pragmas ".../ShaderLibrary/RenderingLayers.hlsl"`, which adds `multi_compile_fragment _ _WRITE_RENDERING_LAYERS` and `target 4.5 _WRITE_RENDERING_LAYERS` (`RenderingLayers.hlsl:4-5`) | 164 |
| LOD cross-fade | `multi_compile _ LOD_FADE_CROSSFADE` (in every pass) | 177, 231, 364, 413 |
| Instancing | `multi_compile_instancing` + `instancing_options renderinglayer` | 184-185 |
| DOTS / BRG | `#include_with_pragmas ".../ShaderLibrary/DOTS.hlsl"`, which adds `multi_compile _ DOTS_INSTANCING_ON` at target 4.5 (`DOTS.hlsl:5-10`) | 186 |
| Shadow caster (punctual) | `multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW` | 234 |
| DepthNormals extras | `multi_compile _ _WRITE_SMOOTHNESS` (for SSR) and `multi_compile_fragment _ _GBUFFER_NORMALS_OCT` | 418-419 |

Keywords that are new in 17.6 or easy to miss (lines 146-154): `_REFLECTION_PROBE_ATLAS`, `_SCREEN_SPACE_REFLECTION`, `_SCREEN_SPACE_IRRADIANCE`, `REFLECTION_PROBE_ROTATION`, `EVALUATE_SH_MIXED/VERTEX`, and `PROBE_VOLUMES_L1/L2` (via `ProbeVolumeVariants.hlsl`). Fog comes from `Fog.hlsl`, which emits `multi_compile_fog` because `USE_DYNAMIC_BRANCH_FOG_KEYWORD` is 0 (`universal-config/Runtime/ShaderConfig.cs.hlsl`).

**Forward+ keyword:** in this version it is `_CLUSTER_LIGHT_LOOP`. `_FORWARD_PLUS` is deprecated: it emits a `#warning` and is aliased to `_CLUSTER_LIGHT_LOOP` (`ShaderLibrary/ForwardPlusKeyword.deprecated.hlsl:8-22`). `Core.hlsl:13-22` defines `USE_CLUSTER_LIGHT_LOOP`. When it is 1, it also forces `#define _ADDITIONAL_LIGHTS 1` and `#undef _ADDITIONAL_LIGHTS_VERTEX` [S][D].

### 2. Light loop, InputData, shadows, BRDF [S]

- **LIGHT_LOOP_BEGIN/END** (`RealtimeLights.hlsl:34-47`):
  - In cluster mode the macro opens a block, calls `ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0)`, loops `ClusterNext`, and adds `URP_FP_DIRECTIONAL_LIGHTS_COUNT` to `lightIndex`.
  - It refers to the identifier **`inputData`** literally. A variable with that exact name, with `normalizedScreenSpaceUV` and `positionWS` filled, must be in scope [S][D].
  - Without the cluster loop it is a plain `for (uint lightIndex...)`.
- `GetAdditionalLightsCount()` **returns 0 under the cluster loop** (`RealtimeLights.hlsl:292-303`).
- Additional **directional** lights are not in the clusters. They need a separate loop: `for (i < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS))`, as Lit does at `Lighting.hlsl:359-375` [S][D].
- `MAX_VISIBLE_LIGHTS` is 256 on desktop (`Input.hlsl:17-25`).
- **InputData** fields are listed at `Input.hlsl:43-55`. For a custom loop, fill: `positionWS`, `normalWS`, `viewDirectionWS`, `normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS)` (`ShaderVariablesFunctions.hlsl:574-584`), `shadowCoord`, `shadowMask`. `positionCS` is only needed for `DEBUG_DISPLAY`.
- **Main light:**
  - `GetMainLight()` (`RealtimeLights.hlsl:100-119`) sets `distanceAttenuation = 1` under the cluster loop. Otherwise it uses `unity_LightData.z` (the culling mask).
  - Overloads: `GetMainLight(float4 shadowCoord)`, `GetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)` (shadow, distance fade and cookie), and `GetMainLight(InputData, half4 shadowMask, AmbientOcclusionFactor)` (adds SSAO direct term) (`:121-153`).
  - Additional lights use the same pattern: `GetAdditionalLight(uint i, InputData, half4 shadowMask, AmbientOcclusionFactor)` (`:278-290`).
  - `Light` struct fields: `direction, color, distanceAttenuation, shadowAttenuation, layerMask`.
  - Light layers check: `IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer())` (`CORE/ShaderLibrary/CommonLighting.hlsl:550`).
- **Shadow coords / cascades:**
  - `TransformWorldToShadowCoord(positionWS)` picks the cascade per pixel with `ComputeCascadeIndex` when `_MAIN_LIGHT_SHADOWS_CASCADE` is on. Under `_MAIN_LIGHT_SHADOWS_SCREEN` it returns NDC (`Shadows.hlsl:346-374`).
  - `MAIN_LIGHT_CALCULATE_SHADOWS` is defined unless `_RECEIVE_SHADOWS_OFF` (`:13-25`). Lit interpolates the shadow coord per vertex only for `_MAIN_LIGHT_SHADOWS` or screen-space shadows, never for cascades.
  - On Windows, soft shadows use `_SHADOWS_SOFT` with per-light quality in `shadowParams.y` (`Shadows.hlsl:310-344`). LOW/MEDIUM/HIGH keywords are used only on XR-mobile/Quest (`Runtime/ShadowUtils.cs:662-711`, `Editor/ShaderBuildPreprocessor.cs:400-407`).
- **Shadow caster:**
  - The shader must declare `float3 _LightDirection; float3 _LightPosition;` (`Shaders/ShadowCasterPass.hlsl:13-14`).
  - Light direction is `normalize(_LightPosition - positionWS)` when `_CASTING_PUNCTUAL_LIGHT_SHADOW`, else `_LightDirection`.
  - Then `TransformWorldToHClip(ApplyShadowBias(posWS, nWS, L))` followed by `ApplyShadowClamping` (directional pancaking) (`ShadowCasterPass.hlsl:33-47`, `Shadows.hlsl:542-569`). `_ShadowBias` is x = depth, y = normal, z = light type (`:114-118`).
- **BRDF** (`BRDF.hlsl`):
  - `InitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, inout half alpha, out BRDFData)` (`:82-97`); metallic workflow unless `_SPECULAR_SETUP`.
  - `EnvironmentBRDF(brdf, indirectDiffuse, indirectSpecular, fresnelTerm)` (`:167-172`); URP computes `fresnelTerm = Pow4(1-NoV)` (`GlobalIllumination.hlsl:543`).
  - Direct lighting: `LightingPhysicallyBased(BRDFData, BRDFData clearCoat, Light, half3 normalWS, half3 viewDirWS, half clearCoatMask, bool specularHighlightsOff)` (`Lighting.hlsl:98-101`).
- **Source bug:** the overload `LightingPhysicallyBased(brdf, lightColor, lightDir, atten, normalWS, viewWS, bool)` passes its arguments in the wrong order (view direction goes in as the normal) (`Lighting.hlsl:131-139`). Avoid it.
- SSAO: `GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV)` returns `{indirect, direct}`. Without `_SCREEN_SPACE_OCCLUSION` both are 1 (`AmbientOcclusion.hlsl:28-38`).
- Rendering layers MRT output is **`uint` at `SV_Target1`**, written as `EncodeMeshRenderingLayer()` (`LitForwardPass.hlsl:228-271`, `ShaderVariablesFunctions.hlsl:608-617`).

### 3. Template "Megastructure/TemplateLit"

This was assembled only from the APIs verified above. **It was not compiled in this session** [U until an Editor compile-check]. Design choices:

- Pipeline lighting keywords are `multi_compile_fragment` because the vertex stage never reads them. Lit uses plain `multi_compile` only because of per-vertex lighting and vertex shadow coordinates.
- No SH, no reflection probes, no lightmaps, no `FOG_*` keywords.

```hlsl
Shader "Megastructure/TemplateLit"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.5,0.5,0.5,1)
        _Metallic   ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        [HDR] _AmbientColor ("Ambient Irradiance (author)", Color) = (0.03,0.03,0.035,1)
        _AmbientSpecular ("Ambient Specular Scale", Range(0,1)) = 0.5
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Single UnityPerMaterial, identical in every pass => SRP Batcher compatible. Render-state-only props (_Cull) stay out.
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _AmbientColor;
            half4 _EmissionColor;
            half  _Metallic;
            half  _Smoothness;
            half  _AmbientSpecular;
        CBUFFER_END
        // Optional: UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata) block for per-instance overrides (see LitInput.hlsl:39-110).

        // Global fog uniforms set from C# (Shader.SetGlobalVector). NOT in Properties. Zero => no fog.
        float4 _AbyssFogColor;   // rgb
        float4 _AbyssFogParams;  // x density(1/m), y height falloff(1/m), z reference height(m), w max opacity
        half3 ApplyAbyssFog(half3 color, float3 positionWS)
        {
            float dist = length(positionWS - GetCameraPositionWS());
            float heightTerm = exp(-max(0.0, positionWS.y - _AbyssFogParams.z) * _AbyssFogParams.y);
            float fog = min(1.0 - exp(-dist * _AbyssFogParams.x * heightTerm), _AbyssFogParams.w);
            return lerp(color, (half3)_AbyssFogColor.rgb, (half)fog);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ForwardVert
            #pragma fragment ForwardFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ForwardVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half3 ShadePBR(BRDFData brdf, Light light, InputData inputData, uint meshLayers)
            {
            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(light.layerMask, meshLayers)) return 0;
            #endif
                const BRDFData noCoat = (BRDFData)0;
                return LightingPhysicallyBased(brdf, noCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
            }

            void ForwardFrag(Varyings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                // MUST be named "inputData": LIGHT_LOOP_BEGIN reads inputData.normalizedScreenSpaceUV / positionWS.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS); // per-pixel cascade select
            #endif
                inputData.shadowMask = half4(1,1,1,1); // no baked shadowmask/lightmaps

                half alpha = 1.0h;
                BRDFData brdf;
                InitializeBRDFData(_BaseColor.rgb, _Metallic, half3(0,0,0), _Smoothness, alpha, brdf);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                uint meshLayers = GetMeshRenderingLayer();

                Light mainLight = GetMainLight(inputData, inputData.shadowMask, ao); // shadow+fade+cookie+SSAO direct
                half3 color = ShadePBR(brdf, mainLight, inputData, meshLayers);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount(); // 0 under _CLUSTER_LIGHT_LOOP (by design)
              #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light dirLight = GetAdditionalLight(dirIndex, inputData, inputData.shadowMask, ao);
                    color += ShadePBR(brdf, dirLight, inputData, meshLayers);
                }
              #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, ao);
                    color += ShadePBR(brdf, light, inputData, meshLayers);
                LIGHT_LOOP_END
            #endif

                // Author-supplied ambient/env term (no SH, no reflection probes).
                half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half3 ambD = _AmbientColor.rgb;
                half3 ambS = _AmbientColor.rgb * _AmbientSpecular;
                color += EnvironmentBRDF(brdf, ambD, ambS, Pow4(1.0h - NoV)) * ao.indirectAmbientOcclusion;
                color += _EmissionColor.rgb;
                color  = ApplyAbyssFog(color, inputData.positionWS);
                outColor = half4(color, 1.0h);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            float3 _LightDirection;   // set by ShadowUtils.SetupShadowCasterConstantBuffer
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 L = normalize(_LightPosition - positionWS);
            #else
                float3 L = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, L)));
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            float DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"            // no ColorMask: Lit writes RGBA (Lit.shader:387-390)
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DNVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            void DNFrag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                float3 n = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                outNormalWS = half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0.0);
            #else
                outNormalWS = half4(n, 0.0);   // signed RT; .a = smoothness only for SSR (_WRITE_SMOOTHNESS)
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
```

### 4. Alpha-to-coverage

- Lit uses `AlphaToMask[_AlphaToMask]` in ForwardLit only (`Lit.shader:111`). `BaseShaderGUI.cs:1027-1034,1127-1129` sets `_AlphaToMask=1` for opaque + alpha-clip materials. Shader Graph emits `AlphaToMask On` for opaque alpha-clip passes (`Editor/ShaderGraph/Targets/UniversalTarget.cs:922-934`). DepthOnly, DepthNormals and ShadowCaster passes only `clip()` [S].
- `DrawObjectsPass.cs:138-141` sets the global `_AlphaToMaskAvailable = (msaaSamples>1 && opaque) ? 1 : 0`. `AlphaClip()` / `SharpenAlphaStrict()` export a sharpened coverage alpha only when that flag is 1; otherwise they just `clip()` (`ShaderVariablesFunctions.hlsl:184-264`) [S].
- Unity's docs say alpha-to-coverage is intended for MSAA, and that enabling it without MSAA gives results that vary by graphics API and GPU [D].
- The project's MSAA is off, so do not use `AlphaToMask On`. If MSAA is enabled later, use `AlphaToMask [_AlphaToMask]` with `_ALPHATEST_ON` and `AlphaClip()`.

### 5. Reversed-Z and the depth texture

- `UNITY_REVERSED_Z 1`, `UNITY_NEAR_CLIP_VALUE 1.0`, `UNITY_RAW_FAR_CLIP_VALUE 0.0` on D3D11/12 and Vulkan (`CORE/ShaderLibrary/API/D3D11.hlsl:3-7`, `Vulkan.hlsl:4-8`) [S].
- `#include ".../ShaderLibrary/DeclareDepthTexture.hlsl"` declares `TEXTURE2D_X_FLOAT(_CameraDepthTexture)`.
  - `SampleSceneDepth(uv)` now does a point **LOAD** at `uv*_ScreenSize.xy` (not a bilinear sample). `LoadSceneDepth(uint2)` is also available (`DeclareDepthTexture.hlsl:31-45`) [S].
  - Eye depth: `LinearEyeDepth(raw, _ZBufferParams)`; 0..1 depth: `Linear01Depth(raw, _ZBufferParams)` (perspective only) (`CORE/ShaderLibrary/Common.hlsl:1196-1209`).
  - For orthographic use `LinearDepthToEyeDepth` (`ShaderVariablesFunctions.hlsl:535-551`).
  - The `_ZBufferParams` layout under reversed-Z is at `UnityInput.hlsl:66-75` [S].
- The camera depth texture is **never MSAA** (`UniversalRendererRenderGraph.cs:1820-1822`).
  - With MSAA and a sampled MSAA source, `CopyDepthPass.hlsl:35-56` keeps the farthest sample (`min` under reversed-Z).
  - When hardware resolve is available, it uses the resolved depth instead (`CopyDepthPass.cs:174-188`; the resolve mode itself is [U]).
- With a depth(-normals) prepass and no depth priming, the prepass renders **directly into** `_CameraDepthTexture`, with no copy (`UniversalRendererRenderGraph.cs:986-992`). In this project (SSAO with DepthNormals source), any shader **without a DepthNormals pass is missing from depth and normals**.

### 6. LOD cross-fade helpers [S]

- `LODCrossFade.hlsl:15-24` provides `LODFadeCrossFade(float4 positionCS)`. It samples `_DitheringTexture.a` with `sampler_PointRepeat` at `positionCS.xy * _DitheringTextureInvSize`, computes `unity_LODFade.x - CopySign(d, unity_LODFade.x)`, then `clip()`.
- The texture is set every frame from `lodCrossFadeDitheringType` (Bayer, BlueNoise64L or Stencil) (`UniversalRenderPipeline.cs:2447-2468`).
- `unity_LODFade` lives in UnityPerDraw (`UnityInput.hlsl:114`) and is DOTS-instanced under BRG (`UniversalDOTSInstancing.hlsl:25`).
- For custom tier fades outside a LODGroup, compute a fade from camera distance in the shader and `clip(fade - d)` with the same dither texture, or with your own interleaved-gradient noise. Use complementary thresholds for the incoming and outgoing tiers.

### 7. Player-build stripping [S unless marked]

- `ShaderScriptableStripper` runs on **all shaders**, including custom ones. It is active whenever a URP asset exists (`:13`), looks keywords up by **name** with `shader.keywordSpace.FindKeyword` (`:205-208`), and keys some rules on **pass names** "ForwardLit", "DepthNormals", "GBuffer", "Unlit" (`:125-130`).
- Nothing is stripped unless **Strip Unused Variants** is on (`:1365-1388`; default true per `URPShaderStrippingSetting.cs:63`). A variant is removed only if every URP asset or renderer combination in the build considers it unused (`ShaderBuildPreprocessor.cs:506,535`) [S][D].
- `StripMultiCompile` removes the keyword-on variant when the feature is off. With Strip Unused Variants on, it also removes the **off** variant when the feature is on (`ShaderStripTool.cs:89-114`).
- Rules that matter for this project:
  - Forward+ strips `_CLUSTER_LIGHT_LOOP`-off variants and all `_ADDITIONAL_LIGHTS` / `_ADDITIONAL_LIGHTS_VERTEX` variants (`:601-663`, `ShaderBuildPreprocessor.cs:860-865`). A shader without `_CLUSTER_LIGHT_LOOP` therefore **gets no point or spot lights in the player**.
  - Shadow off variants are kept only if the renderer keeps them (`:494-529`, `ShaderBuildPreprocessor.cs:804-809`).
  - The ShadowCaster pass is removed if neither main nor additional light shadows are enabled (`:1159-1168`).
  - `_CASTING_PUNCTUAL_LIGHT_SHADOW` depends on additional-light shadows (`:449-466`).
  - `LOD_FADE_CROSSFADE` is stripped if `enableLODCrossFade` is off. In Stencil mode it is also stripped for non-instanced variants, except ShadowCaster and MotionVectors (`:856-872`).
  - `_WRITE_RENDERING_LAYERS` is stripped per pass name (`:747-783`); `_LIGHT_LAYERS`, `_LIGHT_COOKIES` and SSAO follow their asset or renderer flags (`:576, 790, 665`).
- Before stripping, `ShaderKeywordFilter` prefiltering attributes on the URP asset (`UniversalRenderPipelineAssetPrefiltering.cs:47-231`) are keyed by keyword name only. The `m_Prefilter*` values serialized in the asset are **recomputed at build time** (`ShaderBuildPreprocessor.cs:520,597`), so do not hand-edit them. That this filtering reaches custom shaders is [U] (it is name-based).
- To keep the variants you need [D]:
  - Use `multi_compile` with URP's exact keyword names, never `shader_feature`, for pipeline keywords.
  - Name passes exactly as above.
  - Check `ShaderStrippingReport` in Editor.log.
  - Optionally force keyword sets under Project Settings > Graphics > Shader Build Settings.
  - Run a dev player with `PlayerSettings.strictShaderVariantMatching` on (the API exists, per `Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.xml`) to surface missing variants.

### Pitfalls (summary)

1. Using `_FORWARD_PLUS` (deprecated, compile warning) instead of `_CLUSTER_LIGHT_LOOP`.
2. `LIGHT_LOOP_BEGIN` needs a variable literally named `inputData` with `normalizedScreenSpaceUV` and `positionWS` set.
3. `GetAdditionalLightsCount()` returns 0 under Forward+, and extra directional lights need their own loop.
4. The `LightingPhysicallyBased` overload at `Lighting.hlsl:131-139` has its arguments in the wrong order.
5. The rendering-layers MRT output must be `uint`.
6. A missing DepthNormals pass means the object is missing from depth, normals and SSAO in this project.
7. A shadow caster without `_LightPosition` or `_CASTING_PUNCTUAL_LIGHT_SHADOW` gets the wrong normal-bias direction for spot and point lights.
8. Under Forward+ the main light ignores the culling mask (`distanceAttenuation` = 1).
9. Leaving out `DEBUG_DISPLAY` means Rendering Debugger modes do not affect these shaders (acceptable).
10. `_WRITE_SMOOTHNESS` is omitted, so SSR, if added later, treats these surfaces as rough.
11. A second material cbuffer, for example from `UNITY_INSTANCING_BUFFER_START` (which expands to `CBUFFER_START` without instancing, `CORE/ShaderLibrary/UnityInstancing.hlsl:495`), or a MaterialPropertyBlock likely breaks SRP Batcher compatibility [U].
12. Without the `DOTS.hlsl` pragma include, renderers silently drop out of the GPU Resident Drawer.

### Recommendations

1. Copy the template above as the base for every opaque procedural surface. Compile-check it first and keep the UnityPerMaterial layout identical across all passes.
2. Always ship four passes: ForwardLit, ShadowCaster, DepthOnly, DepthNormals. DepthNormals is mandatory here because SSAO uses the DepthNormals source.
3. Declare `_CLUSTER_LIGHT_LOOP` plus `_ADDITIONAL_LIGHTS`. Keep the Forward+ directional pre-loop.
4. Compute shadow coordinates per pixel; there are 4 cascades.
5. Keep fog as global uniforms set from C#, not material properties and not `FOG_*` keywords.
6. Do not use AlphaToMask while MSAA is off.
7. For depth reads, use `LoadSceneDepth` / `SampleSceneDepth` with `LinearEyeDepth(raw, _ZBufferParams)`. Remember that far depth is 0.
8. Use LODGroup cross-fade (BlueNoise) for geometry tiers. For non-LODGroup tiers, use in-shader distance dithering.
9. Before a release build, enable strict variant matching in a development player. Check `ShaderStrippingReport` and confirm there are no pink shaders, and that spot, point and shadow lighting appear in the **Windows player**.
10. Keep `m_StripUnusedVariants` on. If a scene toggles shadows at runtime, keep the renderer's shadow off variants.

### Web sources (accessed 2026-10-29; treated as untrusted data)

- https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus : Forward+ custom loop, `_CLUSTER_LIGHT_LOOP`, InputData requirement, directional pre-loop (nearest available docs version).
- https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-alpha-to-mask : AlphaToMask syntax and values.
- https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-render-state-commands/alpha-to-mask : MSAA requirement and unpredictable behaviour without MSAA.
- https://docs.unity3d.com/6/Documentation/Manual/urp/shader-stripping.html : Strip Unused Variants; `ShaderStrippingReport` in Editor.log.
- https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/built-in/shaders-in-universalrp/shader-stripping/features : Shader Build Settings keyword sets; Strip Unused Variants note.

---

<a id="urp-csharp"></a>
## urp-csharp — URP 17.6 C# configuration and renderer features (research)

**Scope.** Everything below was checked against the package source in this project. Path prefixes:

- `URP/` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184/` (package.json says 17.6.0)
- `CORE/` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e/`
- `CFG/` = `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/`

**Tags.** [VS] = VERIFIED-SOURCE, [VD] = VERIFIED-DOCS, [UV] = UNVERIFIED.

**Current project state** [VS]:

- Quality level "PC" (index 1, the Standalone default) uses `Assets/Settings/PC_RPAsset.asset`.
  - `m_SupportsHDR: 1`, but `m_HDRColorBufferPrecision: 0` (32-bit).
  - `m_MSAA: 1` (MSAA disabled), `m_GPUResidentDrawerMode: 1` (InstancedDrawing).
  - `m_SoftShadowQuality: 3` (High), `m_ColorGradingMode: 0` (LDR).
- `PC_Renderer.asset` has `m_RenderingMode: 2` (Forward+) and one feature, ScreenSpaceAmbientOcclusion (Source = DepthNormals).
- Quality level "Mobile" uses `Mobile_RPAsset`, whose renderer is set to Forward.
- `ProjectSettings/GraphicsSettings.asset` has `m_BrgStripping: 2`.

### 1. UniversalRenderPipelineAsset (`URP/Runtime/Data/UniversalRenderPipelineAsset.cs`)

Serialized fields are on lines 435–628 [VS].

**Properties with a public setter** (safe to set from C#):

| Property | Serialized field / notes |
|---|---|
| `supportsHDR` | `m_SupportsHDR` |
| `hdrColorBufferPrecision` | `m_HDRColorBufferPrecision`. `HDRColorBufferPrecision._32Bits=0`, `._64Bits=1` |
| `msaaSampleCount` | int, stored as `MsaaQuality` in `m_MSAA`. Disabled=1, _2x=2, _4x=4, _8x=8 |
| `renderScale` | `m_RenderScale`, clamped to 0.1–3 |
| `supportsCameraDepthTexture` | `m_RequireDepthTexture` |
| `supportsCameraOpaqueTexture` | `m_RequireOpaqueTexture` |
| `maxAdditionalLightsCount` | `m_AdditionalLightsPerObjectLimit`, clamped to 0..`maxPerObjectLights` = 8 |
| `mainLightShadowmapResolution` | `m_MainLightShadowmapResolution` |
| `additionalLightsShadowmapResolution` | |
| `shadowDistance` | `m_ShadowDistance` |
| `shadowCascadeCount` | `m_ShadowCascadeCount`, 1–4. Values outside that range throw `ArgumentException` |
| `cascade2Split` / `cascade3Split` / `cascade4Split` | `m_Cascade2Split` (float), `m_Cascade3Split` (Vector2), `m_Cascade4Split` (Vector3). Fractions of the shadow distance |
| `cascadeBorder` | `m_CascadeBorder` |
| `shadowDepthBias` / `shadowNormalBias` | `m_ShadowDepthBias` / `m_ShadowNormalBias`, clamped to 0–10 |
| `useSRPBatcher` | `m_UseSRPBatcher` |
| `colorGradingMode` | `m_ColorGradingMode` (LowDynamicRange / HighDynamicRange) |
| `colorGradingLutSize` | `m_ColorGradingLutSize` |
| `volumeProfile` | `m_VolumeProfile` |
| `gpuResidentDrawerMode` | `m_GPUResidentDrawerMode`. The setter calls `OnValidate` |
| `gpuResidentDrawerEnableOcclusionCullingInCameras` | |
| `smallMeshScreenPercentage` | |

**Properties that are read-only or have an internal setter.** Set these with `SerializedObject` [VS]:

| Property | Serialized field |
|---|---|
| `mainLightRenderingMode` | `m_MainLightRenderingMode` (`LightRenderingMode`: Disabled=0, PerPixel=1, PerVertex=2) |
| `supportsMainLightShadows` | `m_MainLightShadowsSupported` |
| `additionalLightsRenderingMode` | `m_AdditionalLightsRenderingMode` |
| `supportsAdditionalLightShadows` | `m_AdditionalLightShadowsSupported` |
| `supportsSoftShadows` | `m_SoftShadowsSupported` |
| `softShadowQuality` (the whole property is internal) | `m_SoftShadowQuality` (UsePipelineSettings=0, Low=1, Medium=2, High=3) |
| `enableLODCrossFade` | `m_EnableLODCrossFade` |
| `lodCrossFadeDitheringType` | `m_LODCrossFadeDitheringType` (BayerMatrix, BlueNoise, Stencil) |
| `supportsLightCookies` / `supportsLightLayers` / `supportsMixedLighting` | `m_SupportsLightCookies` / `m_SupportsLightLayers` / `m_MixedLightingSupported` |
| `reflectionProbeBlending` / `reflectionProbeBoxProjection` | `m_ReflectionProbeBlending` / `m_ReflectionProbeBoxProjection` |
| `volumeFrameworkUpdateMode` | `m_VolumeFrameworkUpdateMode` |
| `opaqueDownsampling` | `m_OpaqueDownsampling` |
| additional-light shadow tiers | `m_AdditionalLightsShadowResolutionTierLow/Medium/High` |

**Pitfall: `m_AnyShadowsSupported`.** The internal setters for main and additional shadows also recompute `m_AnyShadowsSupported`. That field exists only in the Editor and is used by `ShaderKeywordFilter` for the `_SHADOWS_SOFT` keyword. When you write the shadow fields through `SerializedObject`, also set `m_AnyShadowsSupported = main || additional` (lines 539–546, 1316–1322) [VS].

**Upscaling.** The `upscalingFilter` property is `[Obsolete(... #from(6000.3))]`. Use `upscalerName`, a string stored in `m_SelectedUpscalerName` (default "Bilinear"). Valid names: "Automatic", "Nearest-Neighbor", "Bilinear", "FidelityFX Super Resolution 1.0", "Spatial-Temporal Post-Processing" (`UniversalRenderPipeline.cs:36-40`). Render scale is ignored when |1 − scale| < 0.05 (`UniversalRenderPipeline.cs:1788`) [VS].

**GPU Resident Drawer (GRD) prerequisites.** `GPUResidentDrawerMode` has two values, `Disabled` and `InstancedDrawing` (`CORE/Runtime/GPUDriven/IGPUResidentRenderPipeline.cs:9`). GRD turns itself off unless all of the following hold:

- Every renderer in `m_RendererDataList` is a `UniversalRendererData` whose `usesClusterLightLoop` is true, meaning ForwardPlus or DeferredPlus (`UniversalRenderPipelineAsset.cs:1759-1787`).
- `BatchRendererGroup.BufferTarget == RawBuffer`.
- `EditorGraphicsSettings.batchRendererGroupShaderStrippingMode == BatchRendererGroupStrippingMode.KeepAll` (`CORE/Runtime/GPUDriven/GPUResidentDrawer.Validator.cs:61`) [VS].

That getter is read-only [VD: docs.unity.com EditorGraphicsSettings.batchRendererGroupShaderStrippingMode]. Change it with `new SerializedObject(GraphicsSettings.GetGraphicsSettings()).FindProperty("m_BrgStripping").intValue = (int)BatchRendererGroupStrippingMode.KeepAll`. The field name was found in `UnityEditor.CoreModule.dll` [VS]. The project already has the value 2, which probably means KeepAll; the numeric mapping is [UV].

Renderers whose shaders lack `DOTS_INSTANCING_ON` fall back to the normal SRP path (`CORE/Runtime/GPUDriven/SceneProcessors/WorldProcessor.cs:199-223`) [VS]. Unity docs add three more conditions: compute shader support, MeshRenderer only, and no MaterialPropertyBlock [VD].

### 2. UniversalRendererData (`URP/Runtime/UniversalRendererData.cs`)

**Rendering mode.** `RenderingMode { Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3 }` (`UniversalRenderer.cs:11-23`) [VS].

**Public settable properties** [VS]:

- `renderingMode`
- `depthPrimingMode` (`DepthPrimingMode` Disabled / Auto / Forced)
- `copyDepthMode` (AfterOpaques / AfterTransparents / ForcePrepass)
- `depthAttachmentFormat`, `depthTextureFormat`
- `intermediateTextureMode` (Auto / Always)
- `tileOnlyMode`, `accurateGbufferNormals`

The matching serialized fields are `m_RenderingMode`, `m_DepthPrimingMode`, `m_CopyDepthMode`, `m_DepthAttachmentFormat`, `m_DepthTextureFormat`, `m_IntermediateTextureMode` (lines 143–157).

**Renderer features.** `ScriptableRendererData` holds `[SerializeField] internal List<ScriptableRendererFeature> m_RendererFeatures` and `List<long> m_RendererFeatureMap`, which stores the local file IDs (`URP/Runtime/ScriptableRendererData.cs:40-41`). The public `rendererFeatures` list is get-only and does not update the map.

The Inspector's own add path is `ScriptableRendererDataEditor.AddComponent` (`URP/Editor/ScriptableRendererDataEditor.cs:270-306`). It does the following [VS]:

1. `CreateInstance(type)`, sets `name`, adds `hideFlags |= HideInHierarchy`.
2. `AssetDatabase.AddObjectToAsset(component, rendererData)`.
3. `TryGetGUIDAndLocalFileIdentifier(component, out _, out long localId)`.
4. Grows both serialized arrays: sets the object reference in `m_RendererFeatures` and the `longValue` in `m_RendererFeatureMap`.
5. `ApplyModifiedProperties()`, then `EditorUtility.SetDirty(rendererData)`.

Replicate it like this:

```csharp
var f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
f.name = "AbyssGrade"; f.hideFlags |= HideFlags.HideInHierarchy;
AssetDatabase.AddObjectToAsset(f, data);
AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out _, out long id);
var so = new SerializedObject(data);
var l = so.FindProperty("m_RendererFeatures"); var m = so.FindProperty("m_RendererFeatureMap");
l.arraySize++; l.GetArrayElementAtIndex(l.arraySize-1).objectReferenceValue = f;
m.arraySize++; m.GetArrayElementAtIndex(m.arraySize-1).longValue = id;
so.ApplyModifiedProperties(); data.SetDirty(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
```

`data.SetDirty()` is `public new void SetDirty()`; it sets `isInvalidated`, which rebuilds the renderer. To make the script idempotent, check `TryGetRendererFeature<T>` first; it matches the exact type. Turn a feature on or off with `SetActive(bool)`, which writes the serialized `m_Active`.

### 3. Forward+ light limits [VS]

- **Visible lights on desktop:** `UniversalRenderPipeline.maxVisibleAdditionalLights` returns `ShaderOptions.k_MaxVisibleLightCountDesktop = 256` (`CFG/Runtime/ShaderConfig.cs:22`; `UniversalRenderPipeline.cs:220-233`). Mobile, OpenGL Core, GLES3 and WebGPU get 32, low-end GLES3.0 gets 16.
- **Main light counts toward the cap:** in Forward+, culling sets `maximumVisibleLights = 256`, and the main light is treated as one of those lights (`UniversalRenderer.cs:656-660`).
- **Per tile:** `MAX_LIGHTS_PER_TILE = MAX_VISIBLE_LIGHTS` (256) on desktop. `MAX_ZBIN_VEC4S 1024`, `MAX_TILE_VEC4S 4096`, `MAX_REFLECTION_PROBES 64` (`URP/ShaderLibrary/Input.hlsl:17-41`).
- **Tile size:** starts at 8 px and doubles until tiles × ceil((lights + probes) / 32) ≤ 16384 words (`ForwardLights.cs:324-333`). Z-bins are logarithmic in perspective. More lights or a higher resolution only coarsen the tiles; results stay correct.
- **Per-object limit:** ignored in Forward+, because `SetupPerObjectLightIndices` returns early when `m_UseForwardPlus` is set (`ForwardLights.cs:862`). The hard per-object cap in the Forward path is 8.
- **Additional directional lights:** supported in Forward+. They are stored first, counted in `_FPParams0.w` (`URP_FP_DIRECTIONAL_LIGHTS_COUNT`), and looped separately (`Lighting.hlsl:360`).
- **Shadows on additional directional lights:** not supported. `IsValidShadowCastingLight` returns false for Directional lights that are not the main light (`ShadowUtils.cs:731`). Only the main light gets cascades.
- **Keyword:** `_CLUSTER_LIGHT_LOOP`. The older `_FORWARD_PLUS` is deprecated (`UniversalRenderPipelineCore.cs:1067,1376`). Custom shaders need `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP` and the `LIGHT_LOOP_BEGIN` / `LIGHT_LOOP_END` macros from `RealtimeLights.hlsl`.

### 4. Render Graph, FullScreenPassRendererFeature, post order

**Compatibility Mode is gone** [VS]:

- `RenderGraphSettings` is `[Obsolete("These settings are not used. #from(6000.4)")]`, and `enableRenderCompatibilityMode => false` (`URP/Runtime/Settings/RenderGraphSettings.cs:42,65`).
- `ScriptableRenderPass` no longer has `Execute`.
- A pass must override `public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)`. The base implementation only logs a warning, and the pass has no effect (`Passes/ScriptableRenderPass.cs:311`).
- A feature implements `Create()` and `AddRenderPasses(ScriptableRenderer, ref RenderingData)`, plus optionally `protected override void Dispose(bool)`.
- Useful building blocks: `UniversalResourceData` (`activeColorTexture`, `cameraColor`, `cameraDepthTexture`, `isActiveTargetBackBuffer`), `renderGraph.AddRasterRenderPass<T>`, `builder.UseTexture`, `builder.SetRenderAttachment`, `builder.SetRenderFunc`, and `requiresIntermediateTexture`.

**`FullScreenPassRendererFeature`** (`RendererFeatures/FullScreenPassRendererFeature.cs`) [VS]:

- Public fields:
  - `injectionPoint`: BeforeRenderingTransparents, BeforeRenderingPostProcessing, or AfterRenderingPostProcessing (the default).
  - `fetchColorBuffer = true`
  - `requirements` (`ScriptableRenderPassInput`)
  - `passMaterial`, `passIndex`
  - `bindDepthStencilAttachment = false`
- With `fetchColorBuffer`, the pass copies `cameraColor` to `_CameraColorFullScreenPass` and binds it as `_BlitTexture`. Without it, `_BlitTexture` is not set at all.
- It always sets `_BlitScaleBias` and draws `DrawProcedural(..., Triangles, 3 verts)` into `activeColorTexture`.
- It skips Preview and Reflection cameras.

**Shader contract** [VS] (`CORE/Runtime/Utilities/Blit.hlsl`; the pattern is also shown in the URP docs [VD]):

- `#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"`
- `#pragma vertex Vert`. `Vert` builds the full-screen triangle from `SV_VertexID`, and `texcoord = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv)` uses `_BlitScaleBias`.
- Sample with `SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv)`.

**What runs after post-processing** (`UniversalRendererRenderGraph.cs:1364-1500`) [VS]:

1. Uber post.
2. Custom passes in [AfterRenderingPostProcessing, AfterRendering).
3. Capture actions.
4. `FinalPost`, which runs only when FXAA is on, FSR1 upscaling is active, or TAA sharpening > 0. It does FXAA, film grain and dithering.
5. `FinalBlit` to the backbuffer if the target is not already the backbuffer (a copy, plus `_LINEAR_TO_SRGB_CONVERSION` only when `requireSrgbConversion`).
6. Overlay UI.

Without FXAA, FSR or TAA sharpening, nothing after your AfterRenderingPostProcessing pass changes the color except the final copy. Uber still applies dithering and grain before your pass (`m_IsFinalPass = !hasFinalPass`, `UberPostProcessPass.cs:203-207`).

**Formats and where 8-bit quantisation happens** [VS]:

- If a pass runs after post (or a capture or final pass follows), Uber writes to `renderGraph.CreateTexture(cameraColor, "_CameraColorAfterPostProcessing")` (`UberPostProcessPass.cs:141-146`). That texture has the same format as the HDR camera color:
  - 32-bit precision gives `B10G11R11_UFloatPack32`, because alpha is not needed.
  - 64-bit precision gives `R16G16B16A16_SFloat` (`UniversalRenderPipelineCore.cs:1477-1491`).
- Values in that texture are tonemapped linear.
- The 8-bit quantisation happens on the write into the sRGB backbuffer (or into `camera.targetTexture`'s format).
- **B10G11R11 is coarser than 8-bit.** It has 6 mantissa bits for R/G and 5 for B [VD: Microsoft D3D10 float rules]. In [0.5, 1) the step is 1/128 for R/G and 1/64 for B, which is coarser than 8-bit. That causes banding and hue shifts, and it erases the dither.
- The exact swapchain format is [UV].

### 5. Camera dithering [VS]

- **Switch:** `UniversalAdditionalCameraData.dithering` (`m_Dithering`, default false) feeds `cameraData.isDitheringEnabled` (`UniversalRenderPipeline.cs:1745`). It only works when post-processing is on, using the `_DITHERING` keyword in UberPost or FinalPost.
- **Noise:** 32 blue-noise textures of 16×16, sampled from the alpha channel (`URP/Textures/BlueNoise16/L/LDR_LLL1_{0..31}.png`; IHDR header checked).
- **Varies every frame:** the texture index advances on every post render, and the UV offset is `Random.InitState(Time.frameCount)` (`PostProcess.cs:112-127`, `PostProcessUtils.cs:202-214`).
- **Amplitude** (`ApplyDithering` in `URP/Shaders/PostProcessing/Common.hlsl:188-206`):
  - n = 2a − 1, then n = sign(n)·(1 − √(1 − |n|)). This is a triangular distribution on [−1, 1].
  - `c = SRGBToLinear(LinearToSRGB(c) + n/255)`, which peaks at ±1 LSB in 8-bit sRGB.
  - The result is then clamped with `max(c, 0)`.
- **Static dither for tests:** with `LWRP_DEBUG_STATIC_POSTFX` defined, the index and offset stay at 0. Whether that define reaches the package assembly is [UV].

### 6. Volume overrides [VS]

All of these are in `namespace UnityEngine.Rendering.Universal` (`URP/Runtime/Overrides/*.cs`):

- **`Tonemapping`:** `mode` (`TonemappingModeParameter`: None, Neutral, ACES).
- **`Bloom`:**
  - `threshold` (MinFloat, default 0.9). The value is **in gamma space**.
  - `intensity` (MinFloat 0; `IsActive` means intensity > 0).
  - `scatter` (0–1, default 0.7), `clamp` (default 65472), `tint`, `highQualityFiltering`.
  - `filter` (`BloomFilterMode`: Gaussian, Dual, Kawase), `downscale` (Half / Quarter), `maxIterations` (2–8).
- **`ColorAdjustments`:** `postExposure` (Float), `contrast` and `saturation` (−100..100), `hueShift`, `colorFilter`.
- **`WhiteBalance`:** `temperature`, `tint` (−100..100).
- **`Vignette`:** `color`, `center`, `intensity` (0–1), `smoothness`, `rounded`.

**Changing values at runtime:** call `profile.TryGet<Bloom>(out var b); b.intensity.Override(x);`. `Override` sets `overrideState = true` (`CORE/Runtime/Volume/VolumeParameter.cs:235`). `Volume.profile` returns an instantiated copy, while `sharedProfile` edits the asset itself, so in the Editor those changes persist. The default `volumeFrameworkUpdateMode` is EveryFrame.

### 7. Depth buffer [VS]

- **Format options:** `DepthFormat.Depth_32` (D32_SFloat, Forward and Forward+ only) and `Depth_32_Stencil_8`. Apply them through `UniversalRendererData.depthAttachmentFormat` and `.depthTextureFormat`. There is no per-camera option.
- **Default on Windows:** `D32_SFloat_S8_UInt` (`CORE/Runtime/Utilities/CoreUtils.cs:1944-1950`). Unsupported formats fall back to Default with a warning.
- **Intermediate texture:** with HDR on, on-screen cameras always render into an intermediate texture (`UniversalRenderer.cs:849`), so the camera depth uses this format.
- **Reversed-Z:** `UNITY_REVERSED_Z 1` and `UNITY_NEAR_CLIP_VALUE 1.0` in `CORE/ShaderLibrary/API/D3D11.hlsl:4-5`. That file is selected whenever `SHADER_API_D3D11` is defined (`Common.hlsl:224`). D3D12 using the same path is [UV].

### 8. Rendering into a RenderTexture [VS]

- **Request types:** `RenderPipeline.SubmitRenderRequest` supports `RenderPipeline.StandardRequest` (`destination`, `mipLevel`, `face`, `slice`) and `UniversalRenderPipeline.SingleCameraRequest`. Both temporarily set `camera.targetTexture = destination` (`UniversalRenderPipeline.cs:675-820`).
- **Post-processing:** applies if the camera has `renderPostProcessing` on. An offscreen camera with post-processing always uses an intermediate texture.
- **Critical (format):** with a `targetTexture`, the intermediate color buffer **inherits the RT's format**, not the HDR format. The source itself calls this "incorrect but kept for backward compatibility" (`UniversalRenderPipelineCore.cs:1595-1604, 1529-1545`). An R8G8B8A8_SRGB RT therefore clips HDR before bloom and tonemapping, and the look changes.
- **Critical (MSAA):** MSAA comes from `targetTexture.antiAliasing`, not from the asset (`:1555-1561`).
- **Depth:** the RT must have a depth-stencil format. Without one you get a warning and a fallback.
- **Capture hook:** `CameraCaptureBridge.AddCaptureAction(camera, Action<RenderTargetIdentifier, CommandBuffer>)` reads `activeColorTexture` after the after-post passes and before FinalPost (`Passes/CapturePass.cs`).

### 9. Ambient [VS unless noted]

- **`unity_Ambient*` globals:** every camera, URP sets `unity_AmbientSky`, `unity_AmbientEquator` and `unity_AmbientGround` from `RenderSettings.ambient*Color` (sRGB converted to linear) (`UniversalRenderPipeline.cs:2485-2490`; IDs at `UniversalRenderPipelineCore.cs:787`). Changing them at runtime takes effect on the next camera render. URP overwrites these names, so do not reuse them for your own globals.
- **`unity_SHAr`…:** these are per-draw engine data (`UnityInput.hlsl:147`), or DOTS-instanced data under GRD (`UniversalDOTSInstancing.hlsl:31`). They are derived from `RenderSettings.ambientProbe`.
- **`_GlossyEnvironmentColor`:** comes from ambientProbe L0 × `reflectionIntensity` (`:2476-2479`).
- **Trilight color changes:** whether changing a trilight color regenerates `ambientProbe` at runtime without `DynamicGI.UpdateEnvironment` is [UV]; the docs say nothing about it. `ambientProbe` is settable [VD].
- **Own-globals plan:** if your shaders compute ambient from their own uniquely named globals and never call `SampleSH` or `GlossyEnvironmentReflection`, nothing else in URP needs these values.

### Recommendations

1. Set `hdrColorBufferPrecision = _64Bits`. With 32-bit, the post-process intermediate is B10G11R11, which is coarser than 8-bit and bands. It also has to be 64-bit for the 4K RT capture to match the on-screen path.
2. Set `colorGradingMode = HighDynamicRange` and Tonemapping `Neutral`. Remember the bloom threshold is in gamma space.
3. Do grading and dithering in exactly one place:
   - Either use camera `dithering = true` and run your own effects at `BeforeRenderingPostProcessing`, where the input is HDR linear.
   - Or turn camera dithering off and dither yourself in an AfterRenderingPostProcessing pass.
   - Avoid FXAA: it moves dithering into FinalPost, after your pass.
4. For the 3840×2160 capture:
   - Preferred: run the built player at 3840×2160 and grab the backbuffer. This is exactly the on-screen path.
   - Fallback: an RT of `R16G16B16A16_SFloat` + `D32_SFloat_S8_UInt` with `antiAliasing` equal to the asset's MSAA, rendered via `SubmitRenderRequest(StandardRequest)`. Read it back as RGBAHalf, convert linear→sRGB on the CPU, and round. URP's dither is already baked into those linear values.
5. Configure the read-only fields (soft shadows, LOD crossfade, light modes) with `SerializedObject` on the m_ names in section 1. Keep `m_AnyShadowsSupported` consistent with the two shadow fields. Then `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`.
6. Make Mobile_RPAsset and its renderer match PC, or remove the Mobile quality level. Build-time keyword stripping keeps the union of variants across all URP assets included in the build [UV mechanism, VS attributes].
7. For GRD: keep Forward+, keep BRG stripping at KeepAll (verify by reading `EditorGraphicsSettings.batchRendererGroupShaderStrippingMode`), and add `#pragma multi_compile _ DOTS_INSTANCING_ON` with target 4.5 to custom shaders. Otherwise they silently fall back to the SRP path.
8. SSAO with Source = DepthNormals forces a DepthNormals prepass. In Forward+ without depth priming, that prepass writes `_CameraDepthTexture` directly (`UniversalRendererRenderGraph.cs:659-664`). Every handwritten shader therefore needs `DepthOnly`, `DepthNormals` and `ShadowCaster` passes, or its objects disappear from depth-based effects.
9. Keep additional lights to ≤ 256 visible per camera (the main light counts). Do not rely on shadows from additional directional lights.
10. For frame-diff tests, either disable dithering or allow a ±1 LSB tolerance, because the dither pattern changes every frame.
11. Write your own ambient into uniquely named globals. To make the URP fallbacks deterministic, also set `RenderSettings.ambientMode = Flat`, a black `ambientLight`, and `ambientProbe = new SphericalHarmonicsL2()`.

### Web sources (all accessed 2026-10-29)

- https://docs.unity.com/en-us/engine/current/script-reference/unityeditor/rendering/editorgraphicssettings/batchrenderergroupshaderstrippingmode: shows the BRG stripping getter is read-only.
- https://docs.unity.com/en-us/engine/current/script-reference/unityeditor/rendering/batchrenderergroupstrippingmode: enum member names.
- https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html (search summary): GRD requirements and setup steps.
- https://docs.unity3d.com/Manual/urp/renderer-features/how-to-fullscreen-blit.html and https://docs.unity3d.com/kr/current/Manual/urp/renderer-features/write-a-scriptable-render-pass.html (search summary): the Blit.hlsl / Vert / _BlitTexture shader pattern.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/rendersettings/ambientprobe: ambientProbe is settable; docs say nothing about runtime regeneration.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/dynamicgi/updateenvironment: UpdateEnvironment is an asynchronous, scheduled cubemap update.
- https://discussions.unity.com/t/is-it-possible-to-modify-update-skybox-ambient-light-values-at-runtime/590700 (search summary): anecdotal only; skybox changes do not recalculate lighting.
- https://learn.microsoft.com/en-us/windows/win32/direct3d10/d3d10-graphics-programming-guide-resources-float-rules: 11/10-bit float mantissa sizes (6 and 5 bits).

---

<a id="mesh-instancing"></a>
## mesh-instancing — Procedural meshes and instancing (Unity 6000.6.0f1 / URP 17.6)

Researched 2026-10-29, read-only. In the citations, `XML` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml`, `PXML` = `...\UnityEngine.PhysicsModule.xml`, `URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` and `CORE` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`. Where it says "DLL IL" or "DLL string", I read the 6000.6 `UnityEngine.CoreModule.dll` / `UnityEngine.PhysicsModule.dll` through a reflection-only load or a UTF-16 string scan, in memory only.

Note: `docs.unity3d.com/6000.x/...` URLs now return 301 redirects to `docs.unity.com/en-us/engine/<ver>/...`. Pages for 6000.6 exist there.

### 1. Mesh API

- **VERIFIED-SOURCE**: the job-friendly API is `Mesh.AllocateWritableMeshData(int | Mesh | Mesh[] | List<Mesh>)` → `Mesh.MeshDataArray`, then `Mesh.ApplyAndDisposeWritableMeshData(MeshDataArray, Mesh | Mesh[] | List<Mesh>, MeshUpdateFlags flags = Default)` (XML 26559-26594). **There is no `AcquireWritableMeshData`.** `AcquireReadOnlyMeshData` exists, but it is read-only.
- **VERIFIED-DOCS** ([AllocateWritableMeshData 6000.6]): the workflow is `data.SetVertexBufferParams` → `GetVertexData<T>()` → `SetIndexBufferParams(count, IndexFormat)` → `GetIndexData<T>()` → `subMeshCount` → `SetSubMesh` → Apply. The official example then calls `RecalculateNormals()` and `RecalculateBounds()`.
- **VERIFIED-SOURCE**: `MeshUpdateFlags` values are `Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4, DontRecalculateBounds=8, DontValidateLodRanges=16` (DLL reflection; XML 45693-45727).
- **VERIFIED-SOURCE**: `SetIndices(int[] / List<int> / ushort[] / NativeArray<T>, MeshTopology, int submesh, bool calculateBounds = true, int baseVertex = 0[, int meshLod])` recalculates submesh bounds by default (XML 27750-27760). Mesh LOD (`lodCount`, `RenderParams.forceMeshLod`) exists in this version.
- **VERIFIED-DOCS** ([indexFormat]): the default is 16-bit (max 65535 vertices), and the max index may not render. **Changing `indexFormat` sets `subMeshCount` to 1 and clears the index buffer**, so set `mesh.indexFormat = IndexFormat.UInt32` *before* `SetIndices`/`SetTriangles`.
- **VERIFIED-DOCS** ([SetVertexBufferParams]): "The total byte size of each vertex attribute (format x dimension) must be a multiple of 4 bytes." For example, Float16×3 is invalid. `VertexAttributeFormat` has Float32/16, UNorm/SNorm8/16, U/SInt8/16/32 (XML 52079-52138).
- **VERIFIED-DOCS** ([UploadMeshData]): modified data uploads on the next render. `UploadMeshData(true)` frees the CPU copy, and after that the script can't read the mesh.
- **UNVERIFIED (known behaviour)**:
  - A runtime `MeshCollider` and `StaticBatchingUtility` need a readable mesh, so assign colliders before calling `UploadMeshData(true)`, or never mark collider meshes unreadable.
  - `RecalculateNormals` averages across shared vertices. Brutalist hard edges need split vertices per face (24 per box) with explicit normals.
  - `RecalculateTangents` needs normals and UV0.
- **Pitfall**: if a vertex shader displaces geometry (fan blades, pistons animated in the shader), inflate `mesh.bounds` manually and set it **last**, because `SetIndices(calculateBounds:true)` overwrites it. Otherwise the geometry and its shadows get frustum-culled.

### 2. Graphics.RenderMeshInstanced / Indirect / BRG

- **VERIFIED-SOURCE** (XML 16147-16179, DLL reflection): there are three overloads:
  - `RenderMeshInstanced<T>(in RenderParams, Mesh, int submeshIndex, T[] instanceData, int instanceCount=-1, int startInstance=0) where T: unmanaged`
  - the same with `List<T>`
  - the same with `NativeArray<T>`, which takes `RenderParams` **by value**.
- **VERIFIED-DOCS** ([RenderMeshInstanced 6000.6]): T is `Matrix4x4` or a struct with fields named exactly `objectToWorld` (Matrix4x4, mandatory), `renderingLayerMask` (uint, optional) and `prevObjectToWorld` (optional), in any order. The page says "You can only render a maximum of 1023 instances at once… By default… objectToWorld and worldToObject… maximum of 511… add `#pragma instancing_options assumeuniformscaling`." Bounds are auto-computed unless `worldBounds` is set, and "Unity uses the bounds to cull and sort all the instances … as a single entity."
- **VERIFIED-SOURCE**: the managed code has no instance-count check and no splitting loop. Each overload makes one `Internal_RenderMeshInstanced` call (6000.6 DLL: no 1023/511 constants in the IL; [UnityCsReference Graphics.cs] shows `fixed` pinning). The DLL strings "Instancing is not supported." and "Material needs to enable instancing for use with RenderMeshInstanced." match the reference source.
- **UNVERIFIED**: whether native code splits calls above the limit. Treat ≤511 per call (≤1023 with assumeuniformscaling) as the hard rule. The numbers line up with a 64 KB cbuffer ÷ 128 B or 64 B per instance (CORE `ShaderLibrary/UnityInstancing.hlsl` 17-18 and 258-269: the flexible array size on D3D/Vulkan). Extra per-instance props live in their own cbuffer.
- **VERIFIED-SOURCE (6000.6 DLL IL of `RenderParams(Material)`)**:
  - `layer=0`
  - `renderingLayerMask=RenderingLayerMask.defaultRenderingLayerMask`
  - `worldBounds=new Bounds(0,0)` (auto)
  - `camera=null`
  - `motionVectorMode=Camera`
  - `reflectionProbeUsage=Off`
  - `matProps=null`
  - **`shadowCastingMode=Off`, `receiveShadows=false`, `lightProbeUsage=Off`**
  - `entityId=None`
  - `forceMeshLod=-1`

  You must set shadows explicitly.
- **VERIFIED-DOCS** ([RenderParams]): `camera == null` renders for all cameras, including the Scene view. Fields: camera, entityId, forceMeshLod, layer, lightProbeProxyVolume, lightProbeUsage, material, matProps, meshLodSelectionBias, motionVectorMode, overrideSceneCullingMask, receiveShadows, reflectionProbeUsage, rendererPriority, renderingLayerMask, sceneCullingMask, shadowCastingMode, sortingLayerID, sortingOrder, worldBounds.
- **VERIFIED-SOURCE**: `RenderParams.instanceID` is obsolete ("Please use entityId instead.") and `lightProbeProxyVolume` is obsolete `#from(6000.5)` (DLL attributes).
- **VERIFIED-SOURCE (reference source)**:
  - GC: `RenderParams` is a struct, and the T[]/NativeArray paths pin memory, so they make no per-call managed allocation. The only allocation is a one-time layout cache entry per T.
  - You must keep the arrays and `MaterialPropertyBlock` preallocated yourself.
  - The draw is for the current frame, so call it every frame from `Update`/`LateUpdate`.
- **VERIFIED-DOCS** ([RenderMeshIndirect]):
  - The args buffer is `new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, n, GraphicsBuffer.IndirectDrawIndexedArgs.size)`.
  - You must set `worldBounds` yourself.
  - Shader side: `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs`, `#include "UnityIndirect.cginc"` and `InitIndirectDrawArgs(0)`. That file is `Editor/Data/Resources/CGIncludes/UnityIndirect.cginc` lines 45-80, and its 20-byte stride is VERIFIED-SOURCE.
  - The shader must fetch transforms from your own StructuredBuffer.
- **VERIFIED-SOURCE**: BRG/GRD needs `BatchRendererGroup.BufferTarget == RawBuffer` (CORE `Runtime/GPUDriven/GPUResidentDrawer.Validator.cs:44`) and DOTS-instancing shaders. That is overkill for this project.

### 3. Shader side (URP)

- **VERIFIED-SOURCE**: URP Lit uses `#pragma multi_compile_instancing`, `#pragma instancing_options renderinglayer` and `#include_with_pragmas ".../DOTS.hlsl"` in **every** pass (URP `Shaders/Lit.shader` 184-186, 223-224, 322-324, 368-369, 423-424). `DOTS.hlsl` adds `multi_compile _ DOTS_INSTANCING_ON` at target 4.5 (URP `ShaderLibrary/DOTS.hlsl:5-9`).
- **VERIFIED-SOURCE** (CORE `UnityInstancing.hlsl` 297-300):
  - `UNITY_INSTANCING_BUFFER_START(buf)` expands to `cbuffer UnityInstancing_buf { struct {...} arrArray[N]; }`.
  - Read props with `UNITY_ACCESS_INSTANCED_PROP(arr, var)`.
  - `UNITY_MATRIX_M` becomes `unity_Builtins0.unity_ObjectToWorldArray[unity_InstanceID]` (lines 458-482).
- **VERIFIED-SOURCE**: in **non-instanced** variants the macro becomes `CBUFFER_START(buf)` (lines 495-497). That is a second material cbuffer.
- **VERIFIED-DOCS + inference**: [SRP Batcher materials] requires all material properties in a single `UnityPerMaterial` cbuffer, built-ins in `UnityPerDraw`, and no MaterialPropertyBlocks on the renderer. So instanced props make the shader's MeshRenderer path SRP-Batcher-incompatible. Use a **dedicated instanced shader** for RenderMeshInstanced.
- **VERIFIED-SOURCE**: `UnityPerDraw` (including `unity_LODFade`) is declared by URP `ShaderLibrary/UnityInput.hlsl:108-114` ("Block Layout should be respected due to SRP Batcher"). Include URP `Core.hlsl` and never redeclare the built-ins.
- **VERIFIED-SOURCE**: `assumeuniformscaling` defines `UNITY_ASSUME_UNIFORM_SCALING`. It moves `unity_WorldToObjectArray` to a separate CB (`UnityInstancing.hlsl:317-322`), and `TransformObjectToWorldNormal` then uses M instead of the inverse-transpose (CORE `SpaceTransforms.hlsl:199-209`). **Non-uniformly scaled instances get wrong normals.**
- **VERIFIED-DOCS** ([MPB.SetVectorArray]): an array's length is fixed at the first set; longer arrays are capped. Use one MPB per chunk with a fixed size.
- **UNVERIFIED**: MPB arrays are indexed by the per-draw instance ID, not offset by `startInstance`.
- **VERIFIED-DOCS** ([GPU instancing]): instancing of *MeshRenderers* with custom URP shaders only works if the SRP Batcher is off or the shader is incompatible. RenderMeshInstanced is a direct instanced draw, so the SRP Batcher doesn't apply to it.
- **Pitfall (UNVERIFIED, classic)**: without `UNITY_VERTEX_INPUT_INSTANCE_ID` + `UNITY_SETUP_INSTANCE_ID(input)` (+ `UNITY_TRANSFER_INSTANCE_ID` if the fragment reads props) in the ShadowCaster/DepthOnly/DepthNormals passes, all instances' shadows and depth collapse onto instance 0.

### 4. GPU Resident Drawer

- **VERIFIED-SOURCE (project)**: `Assets/Settings/PC_RPAsset.asset:85 m_GPUResidentDrawerMode: 1` (= `InstancedDrawing`; enum in CORE `Runtime/GPUDriven/IGPUResidentRenderPipeline.cs:9-20`). Also `ProjectSettings/GraphicsSettings.asset:49 m_BrgStripping: 2` (= KeepAll; reflected enum `KeepIfEntitiesGraphics=0, StripAll=1, KeepAll=2`) and `PC_Renderer.asset:56 m_RenderingMode: 2` (= Forward+). **GRD is currently ON.**
- **VERIFIED-SOURCE**: URP allows GRD only with the cluster light loop, i.e. Forward+/Deferred+ (URP `Runtime/Data/UniversalRenderPipelineAsset.cs:1759-1788`). In the Editor it also requires BRG variants = Keep All (`Validator.cs:61`).
- **VERIFIED-SOURCE** — exclusion reasons (CORE `Runtime/GPUDriven/Debug/GPUResidentDrawerDebug.cs:22-45`): `LODAnimateCrossFading`, `CustomMaterialPropertyBlock`, `RenderCallback`, `ProxyVolumeProbe`, `MissingDOTSInstancing`, `TooManySubmeshes`, `GPUDrivenDisabled`, and others.
- **VERIFIED-DOCS** ([GRD], [GRD compat]):
  - It handles MeshRenderers only, with DOTS-instancing shaders, static GI only, no MPB and no `OnRenderObject`.
  - An object must not "move position after one camera finishes rendering and before another camera starts".
  - Per-frame changes are tracked.
  - Exclude an object with the *Disallow GPU Driven Rendering* component.
- **Recommendation**: set GRD to **Disabled** for ~300 renderers. Handwritten shaders without `DOTS_INSTANCING_ON` are excluded anyway; leaving it on mixes two render paths, makes builds longer, and adds no benefit at this scale.

### 5. Batching and LOD

- **VERIFIED-SOURCE**: `m_UseSRPBatcher: 1` and `m_SupportsDynamicBatching: 0` (PC_RPAsset 72-73).
- **VERIFIED-DOCS** ([StaticBatchingUtility.Combine]): it copies mesh data into one internal mesh (extra memory), and child transforms are locked afterwards.
- **UNVERIFIED estimate**: 300-500 SRP-batched MeshRenderers are cheap on a desktop CPU (well under 1 ms). Don't static-batch runtime-generated geometry; merge per module+material at generation time instead (UInt32 indices).
- **VERIFIED-SOURCE**: `LODFadeMode {None=0, CrossFade=1, SpeedTree=2}`. `LODGroup.fadeMode`, `animateCrossFading` and `static float crossFadeAnimationDuration` exist; `LOD.fadeTransitionWidth` is "only used if it's not animated" (XML 22988-23104; reflection).
- **VERIFIED-SOURCE**:
  - URP LOD crossfade is on: `m_EnableLODCrossFade: 1` and BlueNoise dither (PC_RPAsset 33-34).
  - If you turn it off, `LOD_FADE_CROSSFADE` is stripped from builds (URP `UniversalRenderPipelineAsset.cs:471-474`).
  - Shader: add `#pragma multi_compile _ LOD_FADE_CROSSFADE` to every pass, and under `#if defined(LOD_FADE_CROSSFADE)` include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl` and call `LODFadeCrossFade(input.positionCS)` in the fragment (URP `ShaderLibrary/LODCrossFade.hlsl:15-24`; `Shaders/ShadowCasterPass.hlsl:6-8,71-73`).
- **Pitfall**: dithered fades are visible in captures as noise, and `animateCrossFading` excludes the object from GRD.

### 6. Physics

- **VERIFIED-SOURCE**: in 6000.6, `Physics.autoSyncTransforms` **is deprecated** (DLL string: "has been deprecated please use Physics.SyncTransforms instead…").
- **VERIFIED-DOCS** ([SyncTransforms 6000.6]): transforms sync automatically before each physics step. Call `SyncTransforms()` after Transform changes in Update/LateUpdate before queries, and don't overuse it.
- **VERIFIED-SOURCE**: `CollisionFlags Move(Vector3 motion)`, `SimpleMove`, `stepOffset`, `slopeLimit`, `skinWidth`, `minMoveDistance`, `enableOverlapRecovery` (PXML 1077-1153).
- **VERIFIED-DOCS** ([CC manual], [CC.Move]):
  - Move does not apply gravity.
  - Use skinWidth ≈ 10% of radius (>0.01).
  - Use stepOffset 0.1-0.4 for a 2 m character; it must not exceed the height.
  - Keep minMoveDistance at 0.
  - Changing properties recreates the controller.
- **VERIFIED-DOCS (pre-6 manual, [MeshCollider 2023.1])**: convex colliders max 255 triangles; non-default cooking needs a readable mesh.
- **VERIFIED-SOURCE**: `Physics.BakeMesh(int, …)` is obsolete; use `BakeMesh(EntityId, bool[, MeshColliderCookingOptions])` (DLL string, PXML 3303-3332). `Object.GetInstanceID()` is obsolete; use `GetEntityId()` (DLL string).
- **UNVERIFIED (community, [CC ignores transform.position])**: the CC keeps an internal position. Direct `transform.position` writes get overwritten unless you disable/enable the controller or call `Physics.SyncTransforms()`.
- **Stairs (design)**:
  - Give visual steps no collider and use one invisible rotated BoxCollider ramp.
  - Keep the ramp at ≤35° with `slopeLimit` 45-50°, plus `stepOffset` 0.3 for the lips.
  - When grounded and descending, add a downward snap of at least `speed*tan(angle)*dt` (or a constant −stepOffset) so `isGrounded` doesn't flicker or bounce.

### 7. Moving platforms and precision

- **VERIFIED-SOURCE**: `[DefaultExecutionOrder(int order)]` exists (XML 8553-8568).
- **Why parenting jitters (UNVERIFIED, community [platform thread])**: the CC's internal PhysX position isn't moved by the parent. The next `Move` writes the stale position back and the player snaps or falls, and FixedUpdate/Update rate mismatch adds aliasing.
- **Recommended order**:
  1. Platform script at `[DefaultExecutionOrder(-100)]`: in Update, move the kinematic platforms from an analytic time curve, then call `Physics.SyncTransforms()` once.
  2. Player at order 0: one `Move(carry + input + gravity)` per frame.
  3. Camera in LateUpdate.
- **For the lift ride (most robust)**: use local coordinates.
  - Store the player's position in lift space, set `cc.enabled=false`, write `transform.position = lift.TransformPoint(local)` every frame, and clamp input to the lift's floor rectangle.
  - On arrival, re-enable the CC and call `Physics.SyncTransforms()`.
- **VERIFIED (computed)**: the float32 ULP at 487 m is **2^-15 = 3.05e-5 m** (the whole 256-512 range; 512-1024 is 2^-14). That is not a jitter source.
- **Pitfall**: high-frequency shader noise or hashes on world position degrade. At 50 000 the ULP is 2^-8 (0.004), so wrap coordinates (`frac(posWS/period)`) or use object space.

### Recommendations

1. Large meshes: `AllocateWritableMeshData` (one call for many meshes), explicit layout of Float32×3 position, Float32×3 or SNorm16×4 normal, and UInt32 indices. Set indexFormat before indices. Set bounds last. Use `RecalculateBounds()`, or explicit inflated bounds for shader-animated parts.
2. Honeycomb and repeated parts: RenderMeshInstanced in spatial chunks of ≤511 instances. Pass precomputed `worldBounds` per chunk and set `shadowCastingMode=On, receiveShadows=true` explicitly. Use preallocated `Matrix4x4[]` and a `RenderParams` built once. Set `material.enableInstancing = true` in code.
3. Use a dedicated instanced shader with `multi_compile_instancing` and instance-ID setup in **all** passes. Derive per-instance variation from a hash of `UNITY_MATRIX_M._m03_m13_m23` instead of MPB arrays. Never use assumeuniformscaling with non-uniform scale.
4. Keep every other handwritten shader SRP-Batcher compatible: one identical `CBUFFER_START(UnityPerMaterial)` in every pass, URP Core.hlsl, and no MPB on MeshRenderers. Check with the Inspector's "SRP Batcher: compatible".
5. Set `m_GPUResidentDrawerMode` to 0 (Disabled) in PC_RPAsset (and Mobile_RPAsset).
6. Avoid LODGroups unless profiling demands them. If you use them, add the crossfade keyword and call to all passes.
7. Use BoxColliders and ramp colliders over MeshColliders. Only bake MeshColliders from readable meshes, with `BakeMesh(EntityId, …)` at load.
8. Run platforms in Update with execution order, then call `Physics.SyncTransforms()`, then one CC.Move. Use local-space kinematic riding for the lift. Don't parent the CC.
9. Avoid obsolete APIs so builds stay warning-free: GetInstanceID, BakeMesh(int), RenderParams.instanceID/lightProbeProxyVolume, `Physics.autoSyncTransforms`.

### Web sources (all accessed 2026-10-29)

- [RenderMeshInstanced 6000.6] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshinstanced — instance limits, struct fields, culling, exceptions.
- [RenderParams] https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderparams — field list; camera null means all cameras.
- [UnityCsReference Graphics.cs] https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/Graphics/Graphics.cs — managed implementation (no splitting, pinning). Cross-checked against the 6000.6 DLL IL.
- [RenderMeshIndirect] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshindirect — args buffer, shader macros, bounds.
- [GRD] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer — requirements.
- [GRD compat] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering — compatibility conditions.
- [SRP Batcher materials] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials — compatibility rules.
- [GPU instancing] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing — SRP Batcher vs instancing for MeshRenderers.
- [MPB.SetVectorArray] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/materialpropertyblock/setvectorarray — fixed array length.
- [AllocateWritableMeshData] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/allocatewritablemeshdata — MeshData workflow.
- [indexFormat] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/indexformat — 16/32-bit rules.
- [UploadMeshData] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/uploadmeshdata — upload and readability.
- [SetVertexBufferParams] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/setvertexbufferparams — 4-byte attribute rule.
- [StaticBatchingUtility.Combine] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/staticbatchingutility/combine — copy and lock semantics.
- [CC manual] https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/character-control-section/class-character-controller — property guidance.
- [CC.Move] https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/charactercontroller/move — Move semantics.
- [SyncTransforms 6000.6] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/physics/synctransforms — when to sync.
- [MeshCollider 2023.1] https://docs.unity3d.com/2023.1/Documentation/Manual/class-MeshCollider.html — convex 255 triangles, readable requirement. Older version, so UNVERIFIED for 6000.6.
- [CC ignores transform.position] https://discussions.unity.com/t/character-controller-ignores-transform-position/729515 — community teleport behaviour.
- [platform thread] https://discussions.unity.com/t/moving-platform-and-charactercontroller-move-not-working-the-player-falls/223755 — community platform-delta approach.

---

<a id="capture-player"></a>
## capture-player — Deterministic capture, player build behaviour, text and CLI (Unity 6000.6.0f1 / URP 17.6.0)

Key: **[S]** = VERIFIED-SOURCE (package source, assembly reflection/strings, or project files), **[D]** = VERIFIED-DOCS (official docs or issue tracker), **[U]** = UNVERIFIED (inference or community source; test it in the build).
`URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`. `INPUT` = `Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime`. `MANAGED` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine`.

### 0. Project facts as they stand now

- `ProjectSettings/ProjectSettings.asset` currently has: `companyName: DefaultCompany`, `productName: 1st Megastructures`, `m_ShowUnitySplashScreen: 1`, `defaultScreenWidth/Height: 1024/768`, `m_ActiveColorSpace: 1` (Linear), `runInBackground: 0`, `resizableWindow: 0`, `allowFullscreenSwitch: 1`, `fullscreenMode: 1` (FullScreenWindow), `usePlayerLog: 1`, `activeInputHandler: 1` (Input System only, so legacy `UnityEngine.Input` is unusable). Windows graphics APIs are `0200000012000000`, which decodes to Direct3D11 (2) then Direct3D12 (18), with `m_Automatic: 0`. **[S]**
- `Assets/Settings/PC_RPAsset.asset` has `m_SupportsHDR: 1`, `m_HDRColorBufferPrecision: 0` (32-bit), `m_MSAA: 1` (MSAA off), `m_RenderScale: 1`. `QualitySettings` has `vSyncCount: 0`. **[S]**
- Only **Mono** Windows player variations are installed. `PlaybackEngines/windowsstandalonesupport/Variations` contains `win64_player_{development,nondevelopment}_mono` and no il2cpp folder, so the backend must be Mono. **[S]**
- `WindowsPlayer.exe` (which becomes `Abyss.exe`) has PE subsystem 2 (GUI), not console. **[S]**

### 1. Time and capture

- `Time.captureDeltaTime` (float): "If this property has a non-zero value then Time.time increases at an interval of captureDeltaTime (scaled by Time.timeScale) regardless of real time and the duration of a frame." It does **not** affect `Time.unscaledTime`. **[D]**
- `Time.captureFramerate` (int) is (1/captureDeltaTime) rounded to the nearest integer. Setting either property sets the other, and 0 turns capture off. `Time.captureDeltaTimeRational` (`RationalTime`) also exists, for exact rates. **[D]/[S]**
- The audio DSP clock (`AudioSettings.dspTime`) runs on its own timeline, so procedural audio does not follow capture time. **[U]** (search-result snippet from the Video clock manual page.)
- URP sets `_Time` from `Time.time` in the player and in Play Mode, but uses **`Time.realtimeSinceStartup` in Edit Mode** (`URP/Runtime/ScriptableRenderer.cs:630-634`). As a result, shader animation captured in Edit Mode is non-deterministic. **[S]**
- `WaitForEndOfFrame` runs "after Unity has rendered every Camera and GUI, just before displaying the frame". It freezes in the Editor while the Scene view is focused, and it **does not run in Editor batch mode**. **[D]**
- In URP 17.6, Screen-Space-Overlay UI is drawn by URP itself, for both UI Toolkit and UGUI. `AdjustUIOverlayOwnership` sets `rendersUIOverlay = true` unless XR is active or there are 0 cameras (`URP/Runtime/UniversalRenderPipeline.cs:2911-2926`). The UI pass is recorded after the final blit for the last base camera (`URP/Runtime/UniversalRendererRenderGraph.cs:1502-1505`) using `UISubset.UIToolkit_UGUI` (`URP/Runtime/Passes/DrawScreenSpaceUIPass.cs:263`). It is only drawn when `resolveToScreen` is true, which requires `targetTexture == null` (`URP/Runtime/FrameData/UniversalCameraData.cs:307,384`). **[S]** The consequences:
  - A backbuffer grab at `WaitForEndOfFrame` includes post-processing and UI. **[S]+[D]**
  - A camera rendered into a RenderTexture contains **no overlay UI**. **[S]**
- `ScreenCapture.CaptureScreenshot(path[, superSize])` captures the composed screen. On desktop, relative paths resolve against the project folder (Editor), so pass absolute paths. **[D]**
- `CaptureScreenshotAsTexture` and `CaptureScreenshotIntoRenderTexture(rt)` should be called after `WaitForEndOfFrame`. The official example then calls `AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, cb)`. **[D]** Vertical orientation of the readback is undocumented. **[U]**
- **Do not use `superSize`.** Issue 4340 is "Won't Fix": superSize "is designed to increase the pixel dimensions of the output image file rather than modifying the engine's internal rendering resolution". The result is blocky output, and it is affected in all versions up to 6.6. **[D]**
- `Texture2D.ReadPixels` reads the active render target. Source and destination "must use the same format", and you must call `Apply()`. The call is slow because it waits for the GPU. **[D]**
- `AsyncGPUReadback.Request(Texture, int mip, GraphicsFormat|TextureFormat, Action<AsyncGPUReadbackRequest>)`: format conversion is automatic when formats differ. `WaitForCompletion()` "stalls both GPU and CPU", and you should check `hasError` afterwards. `AsyncGPUReadback.WaitAllRequests()` exists. `ImageConversion.EncodeArrayToPNG(Array, GraphicsFormat, uint w, uint h, uint rowBytes)` exists. **[D]/[S]**
- In Linear color space, non-HDR render textures are sRGB, and shader output is linear-to-sRGB encoded on write. Float/HDR formats are "always linear". **[D]**

**URP and RenderTexture target internals (important for "matches the screen")**

- When `camera.targetTexture != null`, URP takes the intermediate MSAA count from **`camera.targetTexture.antiAliasing`**. The asset's `msaaSampleCount` is ignored, and `camera.allowMSAA` must be true (`URP/Runtime/UniversalRenderPipelineCore.cs:1555-1564`). **[S]**
- The intermediate color buffer **inherits the target RT's graphicsFormat**. The source comment says: "This is incorrect but kept for backward compatibility" (`UniversalRenderPipelineCore.cs:1595-1604`, `CreateBackbufferInfo` at 1509-1545). On screen, the intermediate is `B10G11R11_UFloatPack32` (HDR, 32-bit precision, no alpha; see 1477-1492). **[S]** The consequence: an 8-bit RT target clamps HDR before bloom and tonemapping, so the capture will not match the screen. **[S]-derived**
- The target RT must have `depthStencilFormat != None`. Otherwise you get the warning "In the render graph API, the output Render Texture must have a depth buffer…" (`UniversalRenderPipelineCore.cs:1540-1544`). **[S]**
- `RenderPipeline.SubmitRenderRequest(camera, data)` runs synchronously, and URP supports `RenderPipeline.StandardRequest` (the full camera stack) and `UniversalRenderPipeline.SingleCameraRequest` (base camera only). URP temporarily swaps `camera.targetTexture = destination`, uses `destination.descriptor.msaaSamples`, then restores (`UniversalRenderPipeline.cs:675-823`). `Camera.Render()` remains available. **[S]+[D]**
- Window size: `Screen.SetResolution` is applied at the end of the frame, and "if no matching resolution is supported, the closest one is set". Behaviour for a windowed size larger than the monitor is undocumented; assume it is clamped to the display. **[D]/[U]** For 3840x2160 output on a smaller monitor, render offscreen.

### 2. Command line and player

- Player arguments (documented for 6000.6):
  - `-screen-width`, `-screen-height` (integer from a supported resolution), `-screen-fullscreen 0|1`, `-window-mode exclusive|borderless`, `-popupwindow`, `-monitor N`
  - `-force-d3d11`, `-force-d3d12`, `-logFile <path>` (`-` sends output to stdout; quote paths with spaces), `-nolog`, `-timestamps`, `-single-instance`
  - `-batchmode`: "doesn't display anything or accept user input"
  - **[D]**
- Startup precedence is: command line, then changed Player settings, then stored PlayerPrefs (fullscreen mode, resolution, window position, saved on SetResolution, Alt+Enter, resize or move), then defaults. **[D]**
- Player log: `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Player.log`. Editor log: `%LOCALAPPDATA%\Unity\Editor\Editor.log`. **[D]**
- `Application.Quit(int exitCode)` returns the code on Windows, Mac and Linux, and "is ignored in the Editor". For the Editor, use `EditorApplication.Exit(int)`. **[D]/[S]**
- `System.Environment.GetCommandLineArgs()` is how you read custom arguments. **[D]** (the Editor docs state it for `-executeMethod`; standard .NET in the player.)
- Because the exe is a GUI app, PowerShell's `& exe` does not wait for it and `$LASTEXITCODE` is unreliable. **[S] (subsystem) / [U] (PowerShell behaviour)** Use this pattern instead: `$p = Start-Process -FilePath $exe -ArgumentList $args -PassThru; $p.WaitForExit(); $p.ExitCode`.

### 3. Batchmode Editor

- You **cannot open the project in batch mode while the Editor has it open**. **[D]**
- Exceptions make the process exit with code 1; otherwise use `EditorApplication.Exit(code)`. `-quit` can quit before `-runTests` finishes, or hang on async code. **[D]**
- `-nographics` means no graphics device, so it cannot be used for rendering. **[D]**
- In batch mode, cameras are not rendered automatically. **[D]** (the PolySpatial doc says batch mode "cameras do not render automatically" and uses `Camera.Render()`.) `WaitForEndOfFrame` does not fire. **[D]**
- `EditorApplication.EnterPlaymode()` exists. The Test Framework enters Play Mode in batch mode via `EditorApplication.isPlaying = true` (`com.unity.test-framework@7a3849e09bd0/UnityEditor.TestRunner/TestRun/Tasks/EnterPlayModeTask.cs:10`). **[S]**
- URP rendering to an RT via `SubmitRenderRequest` in a GPU batch Editor is plausible but **[U]**. Edit Mode `_Time` is also non-deterministic (see section 1).

### 4. Text: UI Toolkit from code, versus UGUI

- `PanelSettings` properties: `themeStyleSheet`, `scaleMode` (`PanelScaleMode.ConstantPixelSize|ConstantPhysicalSize|ScaleWithScreenSize`), `referenceResolution` (Vector2Int), `screenMatchMode`, `match`, `sortingOrder`, `targetTexture`, `renderMode`, `clearColor`, and the field `textSettings` (a `PanelTextSettings` : `TextSettings`). `UIDocument.panelSettings` and `rootVisualElement` exist. **[S]**
- `IStyle.letterSpacing` is a `StyleLength`. `LengthUnit` has only **`Pixel` and `Percent`**; there is no em unit. **[S]** To emulate em, use px = em × fontSize.
- `StyleFontDefinition` has constructors taking `FontDefinition`, `FontAsset`, `Font` and `StyleKeyword`. `FontDefinition.FromFont(Font)` and `FontAsset.CreateFontAsset(Font)` exist. The text generator can be Standard or Advanced (`TextGeneratorType`). **[S]**
- The Advanced generator does not support static font assets. **[D]**
- A panel without a theme logs "**No Theme Style Sheet set to PanelSettings {name}, UI will not render properly**" (string in `MANAGED/UnityEngine.UIElementsModule.dll`). **[S]**
- The Editor's default runtime theme is generated at `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` with the content `@import url("unity-theme://default");` (strings in `UnityEditor.UIElementsModule.dll`). **[S]**
- If no Text Settings asset is set, "UI Toolkit automatically creates one with the default settings". **[D]**
- A PanelSettings created at runtime has no ICU data. The assembly string reads: "falling back to minimal text segmentation… It will not be present on PanelSettings created at runtime, so make sure the build contains at least one PanelSettings asset". **[S]**
- Issue UUM-150415: a PanelSettings created at runtime in the Player logged NullReferenceException and ICU errors continuously. Reproduced in 6000.6.0b9; listed as fixed in **6000.6.0f1**. The workaround is to load a PanelSettings asset from Resources. **[D]**
- UGUI `Text` falls back to `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (`com.unity.ugui@23caec89ae27/Runtime/UGUI/UI/Core/Text.cs:591`). This path is robust in builds. **[S]**

### 5. Splash screen

- Unity support article (updated 2025-10-20): in Unity 6 the release notes "Added the option on Unity Personal to disable or customize the Made with Unity splash screen". **[D]**
- `PlayerSettings.SplashScreen.show` (bool, settable) and `showUnityLogo` exist. **[S]**

### 6. PlayerSettings and build by script (6000.6 surface, reflected from `UnityEditor.CoreModule.dll`) **[S]**

- Settable properties: `productName`, `companyName`, `fullScreenMode`, `resizableWindow`, `runInBackground`, `visibleInBackground`, `allowFullscreenSwitch`, `defaultScreenWidth/Height`, `defaultIsNativeResolution`, `colorSpace`, `usePlayerLog`, `forceSingleInstance`, `useFlipModelSwapchain`.
- Graphics API methods: `SetUseDefaultGraphicsAPIs(BuildTarget, bool)` and `SetGraphicsAPIs(BuildTarget, GraphicsDeviceType[])`.
- Scripting backend: `SetScriptingBackend(NamedBuildTarget, ScriptingImplementation)`. The `BuildTargetGroup` overload is `[Obsolete]`.
- `BuildPipeline.BuildPlayer(BuildPlayerOptions)` returns `BuildReport`. A `BuildPlayerWithProfileOptions` overload also exists. `BuildPlayerOptions` fields: `scenes`, `locationPathName`, `target`, `targetGroup`, `subtarget`, `options`, `extraScriptingDefines`.
- `EditorBuildSettingsScene(string path, bool enabled)`.

### 7. Input System 1.20.0

- `InputSettings.updateMode` defaults to `ProcessEventsInDynamicUpdate`. Reading input in FixedUpdate logs errors in the Editor and dev builds (`INPUT/InputSettings.cs:774,835-867`). **[S]**
- `backgroundBehavior` defaults to `ResetAndDisableNonBackgroundDevices` (`:780`). Stick deadzone defaults are 0.125 and 0.925 (`:783-784`). **[S]**
- `Mouse.current.delta` is in screen pixels and comes from RAWINPUT on Windows. It "might not update every frame" (`INPUT/Devices/Mouse.cs:30-46`). It resets to 0 every update and **accumulates** within a frame (`INPUT/Devices/Pointer.cs:103-121`). Do not multiply it by deltaTime. **[S]**
- `Cursor.lockState` takes `CursorLockMode.None|Locked|Confined`. **[S]**

### 8. Fullscreen toggle

- Alt+Enter toggles fullscreen in the Windows player (Screen docs). The Player setting "Allow Fullscreen Switch" enables the "default OS full-screen key presses". The toggle is saved to PlayerPrefs. **[D]**
- F11 has to be implemented in script. `Screen.mainWindowDisplayInfo`, `Display.main.systemWidth/Height`, and `Screen.SetResolution(w, h, FullScreenMode[, RefreshRate])` exist. **[S]**
- `FullScreenWindow` renders at the script-set resolution and letterboxes. **[D]**

### Recommendations

1. **Deterministic sequences:**
   - Set `Time.captureDeltaTime = 1f/60f` (or `captureDeltaTimeRational`) before the first scripted frame.
   - Drive every animation and camera spline from `Time.time`/`Time.deltaTime`. Never use `unscaledTime`, `realtimeSinceStartup`, `DateTime` or unseeded `Random`.
   - Push a custom global `_AbyssTime` with `Shader.SetGlobalFloat` instead of relying on `_Time`.
   - Disable input-driven motion during captures.
   - Record audio separately, or mute it.
2. **WYSIWYG frames (post and UI, window size)** in the **built player**:
   - Coroutine: `yield return new WaitForEndOfFrame();` → `ScreenCapture.CaptureScreenshotIntoRenderTexture(rt)` → `AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, cb)` → `ImageConversion.EncodeArrayToPNG`.
   - Verify orientation with a corner marker.
3. **4K offscreen frames (no UI):**
   - Create the RT with `RenderTextureDescriptor(3840, 2160) { graphicsFormat = B10G11R11_UFloatPack32, depthStencilFormat = D32_SFloat_S8_UInt, msaaSamples = <asset MSAA> }`. This matches the on-screen HDR intermediate.
   - Render with `RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest{ destination = hdrRT })`.
   - `Graphics.Blit(hdrRT, srgbRT /*R8G8B8A8_SRGB*/)` so the hardware applies the sRGB encode.
   - Read back with `ReadPixels` into `new Texture2D(w, h, TextureFormat.RGBA32, false, false)`, then `EncodeToPNG`.
   - Add an automated test that compares a grey patch and a full frame against a path-2 capture at the same size. **[U] until tested**
4. Never use `superSize`. Never rely on `Screen.SetResolution` above the monitor size.
5. **Automated player runs:**
   - Always pass `-screen-width -screen-height -screen-fullscreen` (this overrides stored PlayerPrefs), plus `-logFile "<abs path>"` and custom flags (for example `-abyssCapture "<dir>"`).
   - Parse them with `Environment.GetCommandLineArgs()`.
   - Before quitting, call `AsyncGPUReadback.WaitAllRequests()`, flush the files, then `Application.Quit(code)`.
   - In PowerShell, use `Start-Process -PassThru` + `WaitForExit()` + `ExitCode`.
   - Do not use player `-batchmode` for rendering.
6. **Batch Editor:** use it only for builds and settings (`-batchmode -projectPath "<p>" -executeMethod Ns.Cls.Method -logFile "<f>"`, exiting with `EditorApplication.Exit`). First check that no interactive Editor has the project open. Do the visual capture in the player.
7. **Text:**
   - Primary: an Editor bootstrap writes `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` (`@import url("unity-theme://default");`), imports it, and creates a PanelSettings **asset** (`AssetDatabase.CreateAsset`, `themeStyleSheet` assigned, `scaleMode = ScaleWithScreenSize`, `referenceResolution = (1920,1080)`, `match = 0.5`). A scene `UIDocument` references that asset, or it is loaded via `Resources`.
   - Set the font explicitly: `root.style.unityFontDefinition = new StyleFontDefinition(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))`.
   - Set `letterSpacing` in px.
   - Verify text in the **player** log (no "No Theme"/ICU errors) and in a capture.
   - Fallback: a UGUI `Canvas` (ScreenSpaceOverlay) + `UnityEngine.UI.Text` with LegacyRuntime.ttf.
8. **Build script:**
   - `PlayerSettings.companyName/productName = "…"/"Abyss"`. Both change the log path and the PlayerPrefs registry key.
   - `SplashScreen.show = false`.
   - `fullScreenMode = FullScreenWindow`, `resizableWindow = true`, `runInBackground = true` (so captures don't pause when unfocused), `allowFullscreenSwitch = true`.
   - `defaultScreenWidth/Height = 1920/1080`, `colorSpace = Linear`.
   - `SetUseDefaultGraphicsAPIs(StandaloneWindows64, false)` + `SetGraphicsAPIs(..., new[]{ GraphicsDeviceType.Direct3D11 })`, or D3D12 first. Pick one and test the Editor and player on the same API.
   - `SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x)`.
   - Set `EditorBuildSettings.scenes`.
   - Call `BuildPipeline.BuildPlayer(new BuildPlayerOptions{ scenes, locationPathName = @"…\Abyss.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None })` and check `report.summary.result == BuildResult.Succeeded`.
9. **Input:**
   - Mouse look: `yaw += Mouse.current.delta.ReadValue().x * degPerPixel`.
   - Gamepad: `Gamepad.current?.rightStick.ReadValue() * degPerSec * Time.deltaTime`, with a null check.
   - `Keyboard.current?.f11Key.wasPressedThisFrame`.
   - Lock the cursor (`Locked` + `visible = false`) on first click or focus. Re-lock in `OnApplicationFocus(true)` and release on Esc.
10. **F11:**
    - If the mode is FullScreenWindow: `Screen.SetResolution(1600, 900, FullScreenMode.Windowed)`.
    - Otherwise: `var d = Screen.mainWindowDisplayInfo; Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow)`.
    - Read `Screen.width` only on the next frame.

### Web sources (all accessed 2026-10-29; untrusted data, no instructions followed)

- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/capturedeltatime — captureDeltaTime semantics
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/captureframerate — reciprocal/rounding
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screencapture/capturescreenshot — composed screen, paths
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screencapture/capturescreenshotastexture — timing, cleanup
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshotintorendertexture — RT + async readback example
- https://issuetracker.unity.com/issues/4340/screencapturecapturescreenshot-renders-blocky-artifacts-when-super-size-is-greater-than-1 — superSize Won't Fix
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/waitforendofframe — end-of-frame, batchmode note
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendering/asyncgpureadback/request — overloads/conversion
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadbackrequest/waitforcompletion — stall semantics
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/texture2d/readpixels — same-format rule
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexturereadwrite — sRGB/linear rules
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screen/setresolution — end-of-frame, closest resolution
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen — startup precedence, PlayerPrefs, Alt+Enter
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player — player CLI
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor — batchmode, -quit, -executeMethod
- https://docs.unity.com/en-us/engine/6000.6/manual/get-started/project-configuration/log-files — log paths
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/quit — exit code
- https://docs.unity.com/en-us/engine/6000.6/manual/cameras/urp/multiple/user-render-requests — SingleCameraRequest
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/renderpipeline/submitrenderrequest — StandardRequest = full stack, synchronous
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/render — Camera.Render
- https://docs.unity3d.com/Packages/com.unity.polyspatial.visionos@2.1/manual/RenderTextures.html — batch mode cameras don't auto-render
- https://issuetracker.unity.com/issues/24220/errors-are-logged-continuously-in-the-player-when-panelsettings-is-created-at-runtime — runtime PanelSettings bug/fix version
- https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-runtime-panel-settings — Panel Settings properties
- https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-work-with-text/uie-get-started-with-text — Advanced Text Generator, no static fonts
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/uielements/panelsettings/themestylesheet — theme semantics
- https://support.unity.com/hc/en-us/articles/212165803 — Personal can disable splash in Unity 6
- https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/windows/playersettings — Windows Player settings rows
- https://github.com/PowerShell/PowerShell/issues/20400 — Start-Process ExitCode caveats (search snippet only)
- https://docs.unity.com/en-us/engine/6000.6/manual/video/player/clock — capture clock vs audio DSP clock (search snippet only)

---

<a id="audio"></a>
## audio — Procedural audio in Unity 6000.6 and DSP design

Tags: **[VS]** VERIFIED-SOURCE (local file and line), **[VD]** VERIFIED-DOCS (official or reference web page), **[U]** UNVERIFIED (inference or common practice, prove it with a runtime probe), **[C]** computed here (numbers checked with PowerShell on 2026-10-29).
`AM.xml` means `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml`. `AMgr` means `ProjectSettings\AudioManager.asset`.

### 1. `MonoBehaviour.OnAudioFilterRead(float[] data, int channels)`

- **[VD]** (6000.6 page) Unity inserts a custom filter into the DSP chain, in the component order shown in the Inspector. It is called "every ~20ms depending on the sample rate and platform". `data` holds interleaved floats in [-1, 1]. Frames = `data.Length / channels`. The callback runs on **the audio thread**: "calling into many Unity functions from this function is not allowed". It is not supported on Web.
- **[VD]** Without a clip: "If this is the first filter in the chain and a clip isn't attached to the audio source, this filter will be played as the audio source" (6000.6, identical to 6000.3). **[U]** The issue tracker says the AudioSource's `Play` entry point is "where the logic to identify audio generators is executed". A generator regression was fixed in 2023.2.8f1 and 2023.3.0b5. So call `Play()` explicitly **after** the filter component is added. `playOnAwake` fires before the filter exists if `AudioSource` is added first.
- **Dummy-clip carrier (as the spec asks, and the most robust option):** `AudioClip.Create(string name, int lengthSamples, int channels, int frequency, bool stream)` **[VS AM.xml:1502]**. Call it non-streamed with no callback (`AudioClip.Create("AbyssCarrier", fs, 2, fs, false)`). Then set `loop = true`, `spatialBlend = 0` (**[VS AM.xml:2570]**: 0 = full 2D), `dopplerLevel = 0`, `priority = 0`, `bypassReverbZones = true`, `volume = 1`, `pitch = 1`, and call `Play()`. The filter **overwrites** `data` (`data[i] = out`) and never adds to it.
- **`PCMReaderCallback` alternative:** **[VD]** a streamed clip calls the reader "continuously", and the docs warn that the array must be filled with zeros when there is no data. The docs do not say which thread runs it **[U]**. It adds buffering latency. Do not use it.
- **New in 6.x:** **[VS AM.xml:829, 2480, 685]** the Scriptable Audio Pipeline (`IAudioGenerator`, `AudioSource.generator`, `GeneratorInstance.IRealtime.Process(context, pipe, buffer, args)`, `ControlContext.CreateManualControlContext`). **[VD]** "You can't use `AudioClip.CreateInstance` with clips you generate at runtime via `AudioClip.Create`". This API is new and thinly documented. Do not adopt it.
- **Forbidden on the audio thread [VD/U]:** any `UnityEngine.Object` access (transform, components, `AudioSource.*`), `Time.*`, `UnityEngine.Random` (main thread only), allocation (GC), locks, string formatting and LINQ. Pure math (`System.Math`, `Mathf`) is fine. The docs example calls `Debug.Log` there, but avoid it: set a flag and log from the main thread. Wrap the body in `try/catch`. On an exception, zero `data` and set a flag.
- **Buffer size and rate:** **[VS AMgr:11-12]** `m_SampleRate: 0` (0 = system rate **[VD]**) and `m_DSPBufferSize: 1024`. `Default Speaker Mode: 2` = stereo **[VS AMgr:10, VD]**. **[U]** 1024 is probably the "Best Performance" setting, about 21.3 ms at 48 kHz. Never assume it: size every buffer from `data.Length` and support any `channels`.
- **Pause, timeScale, capture:** **[VD]** the dspTime docs say that "While the game is paused or suspended, this time will not be updated". This covers Editor pause, `AudioListener.pause` and app suspension. **[VS AM.xml:189-196]** `AudioMixerUpdateMode.Normal` uses *scaled* game time (only relevant if an AudioMixer is used, so do not use one). **[U]** `Time.timeScale = 0` does not stop the device-driven callback. **[VD]** `captureDeltaTime`/`captureFramerate` make `Time.time` advance "regardless of real time", and the page says nothing about audio. So **[U]** the audio thread keeps real time and drifts from captured frames. Use offline rendering for capture (Recommendation 9).
- **Batchmode:** not documented anywhere I found **[U]**. **[VD]** "Disable Unity Audio" turns off audio in standalone builds. It is `m_DisableAudio: 0` here **[VS AMgr:17]**. **[VD]** "Virtualize Effect" turns off effects on culled sources. It is `m_VirtualizeEffects: 1` **[VS AMgr:18]**, with `m_RealVoiceCount: 32`. Keep at most 1 or 2 sources at priority 0. **[VD]** "Enable Output Suspension" (Editor only) suspends output after long silence.

### 2. AudioSettings

- **[VS AM.xml:1762-1790]** `struct AudioConfiguration { speakerMode, dspBufferSize, sampleRate, numRealVoices, numVirtualVoices }`. **[VS AM.xml:2357, 2430]** `AudioConfiguration AudioSettings.GetConfiguration()` and `bool AudioSettings.Reset(AudioConfiguration)`. Reset gives "no guarantee that the exact settings specified are used … closest match", raises `OnAudioConfigurationChanged(bool deviceWasChanged)`, and can stall the main thread during async loads.
- **[VD]** Valid values from the docs example: buffers 32, 64, 128, 256, 340, 480, 512, 1024, 2048, 4096, 8192; rates 11025, 22050, 44100, 48000, 88200, 96000. The example calls `Play()` again after Reset. **[U]** Reset stops playing sources.
- **[VS AM.xml:2365]** `GetDSPBufferSize(out int bufferLength, out int numBuffers)`. **[VS AM.xml:2336, 2341]** `double AudioSettings.dspTime` (based on samples processed **[VD]**) and `int AudioSettings.outputSampleRate`.

### 3. Thread-safe parameters and events

- C# `volatile` does not apply to `double`/`long`. Use `System.Threading.Volatile.Read/Write(ref double)` or `Interlocked`.
- The spec says "double-buffered". Implement it as a wait-free triple buffer, which has no race when the main thread publishes twice inside one callback:

  ```
  Main:  slots[back] = p;  back = Interlocked.Exchange(ref middle, back | FRESH) & 3;
  Audio: if ((Volatile.Read(ref middle) & FRESH) != 0) front = Interlocked.Exchange(ref middle, front) & 3;
         var p = slots[front];   // once per callback, then smooth per sample
  ```

- One-shot events (footsteps, rail ticks): use a preallocated SPSC ring of structs (head and tail written with `Volatile.Write`). Keep the audio state a pure function of (progress d, rideU, time, seed), so the offline render uses the same code.

### 4. DSP blocks (fs = 48000)

- **One-pole smoother [VD JOS / C]:** `a = exp(-1/(tau*fs))`, `y = x + a*(y - x)`. For tau = 50 ms, a = 0.99958342. Note that tau is the 63 % point and 99 % is reached at about 4.6·tau = 230 ms. Write down which meaning "50 ms" has.
- **DC blocker [VD JOS]:** `y = g*(x - x1) + R*y1`, `H = (1 - z^-1)/(1 - R z^-1)`. Normalised with `g = (1 + R)/2`, because the peak gain is 2/(1 + R). For an 8 Hz corner, `R = exp(-2π·8/fs) = 0.99895335` **[C]**. That gives -3.01 dB at 8 Hz, -0.96 dB at 16 Hz and -0.29 dB at 30 Hz, so SUB at 31 Hz is barely touched **[C]**.
- **xorshift32 [VD Wikipedia, Marsaglia 2003, JSS 8(14)]:** `x ^= x<<13; x ^= x>>17; x ^= x<<5;`. The period is 2^32-1 and the state must never be 0. White noise in [-1, 1): `(int)x * 4.656613e-10f`. Give each layer and each L/R stream its own state, hashed from (seed, id) and forced nonzero.
- **Brown noise (leaky integrator) [C]:** `y = a*y + b*w` with `a = exp(-2π f_leak/fs)` and `b = sqrt(1 - a²)` (unit variance for unit-variance input; uniform w has variance 1/3). For f_leak = 10 Hz, a = 0.99869186 and b = 0.05113287. The slope is -6 dB/oct above f_leak.
- **Pink noise (Paul Kellet, "refined", ±0.05 dB above 9.2 Hz at 44.1 kHz) [VD musicdsp, JUCE transcription]:**

  ```
  b0=0.99886*b0+w*0.0555179; b1=0.99332*b1+w*0.0750759; b2=0.96900*b2+w*0.1538520;
  b3=0.86650*b3+w*0.3104856; b4=0.55000*b4+w*0.5329522; b5=-0.7616*b5-w*0.0168980;
  pink=b0+b1+b2+b3+b4+b5+b6+w*0.5362; b6=w*0.115926;
  ```

  The economy version (±0.5 dB) is `0.99765/0.0990460, 0.96300/0.2965164, 0.57000/1.0526913, +w*0.1848`. The coefficients are tuned for 44.1 kHz. **[U]** At 48 kHz the poles move up about 0.12 octave, which is harmless above 20 Hz. Calibrate the output gain with an RMS measurement in a test.
- **TPT state-variable filter (Simper/Cytomic) [VD, transcription of the Cytomic paper; the PDF itself could not be text-extracted]:**

  ```
  g=tan(π fc/fs); k=1/Q; a1=1/(1+g*(g+k)); a2=g*a1; a3=g*a2;
  v3=v0-ic2eq; v1=a1*ic1eq+a2*v3; v2=ic2eq+a2*ic1eq+a3*v3; ic1eq=2*v1-ic1eq; ic2eq=2*v2-ic2eq;
  low=v2; band=v1 (peak gain Q); bandUnity=k*v1; high=v0-k*v1-v2; notch=v0-k*v1;
  ```

  Recompute g every 32 samples from the smoothed fc and clamp fc to at most 0.45·fs. Band edges map to centre `fc = sqrt(f_lo*f_hi)`: AIR 120-600 gives 268 Hz, WIND 300-1200 gives 600 Hz, FAN 200-900 gives 424 Hz. The spec's Q values are kept as given **[interpretation]**.
- **Soft limiter (pitfall) [C]:** in the spec's `0.708*tanh(1.2x)/tanh(1.2)`, tanh(1.2) = 0.833655. So the output is 0.708 (-3 dBFS) only at |x| = 1. The **asymptotic ceiling is 0.8493 = -1.42 dBFS**, and the small-signal gain is 1.019. That still meets "peak ≤ -1 dBFS". For a true -3 dBFS ceiling, use `c*tanh(x/c)` with c = 0.708. Put the DC blocker **before** the limiter so the ceiling holds.
- **FDN, 8 lines [VD JOS/Jot; VS spec delays]:** the delays 1049, 1217, 1423, 1601, 1801, 2039, 2269, 2503 are all prime, with ΣL = 13902 samples = 0.29 s **[C]**. Hadamard: `H2N = (1/√2)[[HN, HN], [HN, -HN]]`, computed as an in-place FWHT (3 butterfly stages) then ×0.35355339. Line gain `G0_i = 10^(-3 L_i/(fs·T60))`. For example, at T60 0.7 s G0 is 0.806/0.598 (shortest/longest line), and at 11 s it is 0.986/0.968 **[C]**.

  **HF damping:** JOS gives the first-order design `H_i = g_i/(1 - p_i z^-1)`, `p_i = (R0^M - Rπ^M)/(R0^M + Rπ^M)`, `g_i = 2 R0^M Rπ^M/(R0^M + Rπ^M)` **[VD]**. The spec gives a *damping frequency* f_d, so use this exact one-pole design instead. Take `T60(f_d) = T60/2` (an interpretation to record), `r = G_d/G0`, `c = cos(2π f_d/fs)`, `A = 1 - r²`, `B = 1 - r²c`, `p = A/(B + sqrt(B² - A²))`, and in-loop gain `k_i = G0_i(1 - p_i)`. Check: L = 2503, T60 = 7.5 s, f_d = 2.5 kHz gives p = 0.3732, and the gain at f_d matches the target exactly **[C]**.

  ```
  x = predelay.Read(preSamples)            // 16384-sample ring buffer, up to 250 ms
  for i: z[i] = k[i]*line[i].Read(L[i]) + p[i]*z[i]
  v = FWHT8(z) * 0.35355339
  for i: line[i].Write(v[i] + sgn[i]*x*inGain)
  yL = Σ oL[i]*z[i]; yR = Σ oR[i]*z[i]     // two orthogonal ±1 rows, /√8
  ```

  **Mode density [VD JOS]:** Schroeder asks for M ≥ 0.15·T60·fs, which is 79200 at 11 s against 13902 available. The risk is metallic ringing on bells and long tails. Mitigate with 2-4 input allpass diffusers (g about 0.6) and/or ±0.5-sample delay modulation. Optional reading: Schlecht/Habets on echo and modal density **[VD]**.

  **Wet level:** tail energy grows with T60 (∝ T60). Define "wet" against a calibrated reference so the 4 s blends do not jump in loudness.

  **Pre-delay changes:** crossfade two read taps. Do not move one tap, which clicks or shifts pitch.
- **Modal resonator [VD JOS]:** `y = 2r cosθ·y1 - r²·y2 + x`, with `θ = 2πf/fs` and `r = 10^(-3/(T60·fs))`. An impulse `x0 = A sinθ` gives `y[n] = A r^n sin((n+1)θ)`. Use `double` state at 31-83 Hz. For BELL: 83/137/211/283 Hz with T60 5/4/3/2 s.
- **Click-free envelopes [U, standard practice]:** raised-cosine attack `0.5 - 0.5cos(πt/Ta)`. Exponential decay `env *= 10^(-3/(T60·fs))` (record that "decay X s" means T60). Free a voice below 1e-5. A stolen voice fades over 5 ms. Layer on/off goes through the smoothers. Use double phase accumulators. Add an anti-denormal guard (flush |s| < 1e-15) in FDN and IIR states.

### 5. Offline WAV and metrics (EditMode)

- **WAV layout [VD McGill]:** little-endian (`BinaryWriter`). `RIFF <4+chunks> WAVE`, then `fmt `: tag (1 = PCM, 3 = float), channels, rate, byteRate = rate·blockAlign, blockAlign = ch·bits/8, bits. The fmt chunk is 16 bytes for PCM. Non-PCM float needs an 18-byte fmt (cbSize = 0) **and** a `fact` chunk (frames per channel). EXTENSIBLE is needed only for more than 2 channels or more than 16-bit PCM. Pad odd data with one byte. Write a placeholder header and patch the sizes afterwards. For 16-bit: `(short)clamp(round(x*32767))`.
- **Metrics [C/U]:**
  - RMS in 0.4 s windows (19200 frames, as BS.1770 momentary): `20log10(sqrt(mean x²))`. Under this convention a full-scale sine reads -3.01 dBFS (AES17 reads +3 dB higher), so write the convention down.
  - Sample peak: `20log10(max|x|)`.
  - True peak: 4× polyphase windowed-sinc interpolator (for example 4 phases × 16-24 taps, Kaiser β ≈ 7). BS.1770 Annex 2 uses a 48-tap, 4× filter with about ±0.1 dB ripple **[VD secondary]**.
  - Also measure max |x[n] - x[n-1]| ≤ 0.1 and |mean| < 1e-4.
  - The live callback cannot be sample-identical to the offline render (parameters arrive at different times). Compare RMS/peak statistics instead.

### 6. `AudioReverbFilter` (fallback)

- **[VS AM.xml:1986-2065, VD manual]** `[RequireComponent(typeof(AudioBehaviour))]`. Properties: `decayTime` 0.1-20 s, `decayHFRatio` 0.1-2.0, `hfReference` 1000-20000 Hz, `lfReference` 20-1000 Hz, `dryLevel`/`room`/`roomHF`/`roomLF` -10000..0 mB, `reflectionsLevel` -10000..1000 mB, `reflectionsDelay` **0-0.3 s** (the scripting-reference text at AM.xml:2026 wrongly copies the "late reverberation level" description), `reverbLevel` -10000..2000 mB, `reverbDelay` 0-0.1 s, `diffusion`/`density` 0-100 %, `reverbPreset`.
- **[VD]** Custom values need `reverbPreset = AudioReverbPreset.User`. Mapping: decayTime = RT60, reflectionsDelay = pre-delay, hfReference = f_d, wet -14 dB = -1400 mB. Ramp the values from the main thread at no more than 6 dB/s.

### Sources (all accessed 2026-10-29)

- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/onaudiofilterread: threading, generator rule, data layout.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset: Reset semantics, valid buffer sizes and rates.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/dsptime: dspTime and pause behaviour.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audioclip/create: Create overloads, stream semantics, Scriptable Audio Pipeline note.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/time/captureframerate and .../time/capturedeltatime: capture timing.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager.md: sample rate 0, Disable Unity Audio, Virtualize Effect, Output Suspension.
- https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/filters/class-audio-reverb-filter.md and https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audioreverbfilter.md: reverb filter ranges, User preset, RequireComponent.
- https://docs.unity.com/en-us/engine/6000.6/manual/audio/scriptable-processors/generators: IAudioGenerator overview (not adopted).
- https://issuetracker.unity3d.com/issues/onaudiofilterread-not-working-as-expected-for-generator-scripts and https://issuetracker.unity3d.com/issues/empty-data-buffer-gets-passed-to-onaudiofilterread-when-using-video-player: generator detection in Play() (search snippet only; the pages are dynamic).
- https://discussions.unity.com/t/onaudiofilterread-not-playing/503615: clipless-source anecdotes.
- https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf (primary) and https://forum.juce.com/t/svf-resonant/56889/17 (text transcription): SVF.
- https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html and .../DC_Blocker_Frequency_Response.html: DC blocker and gain normalisation.
- https://ccrma.stanford.edu/~jos/filters/Two_Pole.html: resonator.
- https://ccrma.stanford.edu/~jos/pasp/ pages First_Order_Delay_Filter_Design, Delay_Line_Damping_Filter_Design, Achieving_Desired_Reverberation_Times, Hadamard_Matrix, Choice_Delay_Lengths, Mode_Density_Requirement: FDN design.
- https://www.musicdsp.org/en/latest/Filters/76-pink-noise-filter.html, its pink.txt, and https://forum.juce.com/t/pink-noise-generator/40013/2: Kellet coefficients.
- https://en.wikipedia.org/wiki/Xorshift: xorshift32 (Marsaglia, doi:10.18637/jss.v008.i14).
- https://mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html: WAV format.
- https://cacophony.js.org/classes/TruePeakDetector.html and https://github.com/audionuma/r128x/issues/10: BS.1770 true-peak structure.
- https://www.audiolabs-erlangen.com/resources/2018-IEEE-Modal, https://audiolabs-erlangen.de/resources/2019-WASPAA-DFM-FDN, https://ar5iv.arxiv.org/html/1912.08888: Schlecht FDN density research.

### Recommendations

1. Put the DSP in a Unity-free `AbyssSynth` class with `Render(Span<float> interleaved, int frames, int ch)`. Use a thin `ProceduralAudioEngine : MonoBehaviour` whose `OnAudioFilterRead` only calls it.
2. Set System Sample Rate to 48000 in AudioManager instead of calling Reset at runtime. Still cache `outputSampleRate` on the main thread and derive every coefficient from it. Call Reset only as a fallback, before the source plays.
3. Use a looping stereo carrier clip, add the filter component, then call `Play()`. Re-`Play()` and re-initialise state in `OnAudioConfigurationChanged`.
4. Use a triple buffer for parameters and an SPSC ring for events. Do not allocate, lock or call Unity APIs on the audio thread. Wrap the callback in try/catch.
5. Duck for the pause overlay inside the engine (smoothed gain). Never use `AudioListener.pause`, which freezes dspTime and can click. Set `Application.runInBackground = true`.
6. Signal order: mix → master trim → fade → DC blocker (R = 0.99895335, normalised) → limiter. Decide between -1.42 dBFS (spec formula) and a true -3 dBFS ceiling (`c*tanh(x/c)`), and record the choice.
7. FDN: use the exact per-line one-pole damping design above, energy-normalised wet, crossfaded pre-delay taps, optional input diffusers to counter the low mode density at 7.5-11 s, and an anti-denormal guard.
8. Built-player proof: a listener-side `OnAudioFilterRead` tap copies the final mix into a preallocated 10 s ring buffer, and the main thread writes a WAV. Compare its statistics (not samples) with the offline render. Log the callback count, `GetDSPBufferSize` and `outputSampleRate`.
9. For `captureFramerate` capture and `-render-audio`, render exactly fs/fps samples per frame on the main thread (800 at 60 fps, 1600 at 30) through the same synth, so picture and sound stay frame-locked.
10. EditMode tests: per-beat WAVs, 0.4 s RMS against target ±3 dB, sample peak and 4× true peak ≤ -1 dBFS, step ≤ 0.1, |DC| < 1e-4, presence of the silences.

---

<a id="art-technique"></a>
## art-technique — Art direction and rendering-technique references (web research, accessed 2026-10-29)

Tags: **VS** = VERIFIED-SOURCE (package source, original paper/slides/code, or checked numerically by me). **VD** = VERIFIED-DOCS (an authoritative web page). **UV** = UNVERIFIED (industry rule of thumb or my own recommendation). Every web page was treated as untrusted data. I downloaded no assets.

### 1. Brutalist concrete (scale, surface, joints)

- **Barbican:** poured in-situ concrete, most exposed surfaces deep pick-hammered to show granite aggregate, with bush or brush hammering in interiors. Formwork was 3/4 in (19 mm) plywood boxes, and sections waited about 28 days. **VD** (barbicanliving, City of London guide)
- **National Theatre (Lasdun):** cast against rough-sawn boards. The grain of each plank and a horizontal ridge at each board joint are printed into the surface. **VD** (studiomatrx/RIBA)
- **Boston City Hall:** about half precast (about 22,000 components) and half cast-in-place, with heavy board-marked piers under a precast office block. **VD/UV** (Architectuul is a wiki)
- **Habitat 67:** 354 precast boxes of 17'6" x 38'6" x 10' (5.3 x 11.7 x 3.0 m), 70-90 tons when lifted, 5000 psi concrete, steam cured. **VD** (PCI Journal 1967)
- **Ties:** put tie holes on rustication or reveal lines. One spec caps holes at 7/8 in (22 mm). Breakback ties need at least 1 in (25 mm) of cover. **VD** (SpecFormliners guide, Lee County spec)
- **Fresh-concrete pressure:** about 150-160 psf per ft of depth (about 23.6-25 kPa/m), so ties sit closer near the bottom of tall pours. **VD** (patent text)
- **Shader numbers, all UV (I found no authoritative source):**
  - Boards 90-150 mm wide (nominal 1x4/1x6 = 89/140 mm actual), lengths 2.4-4.8 m with staggered butt joints.
  - Joint ridges 1-3 mm proud of the surface. Each board gets its own depth offset (±1-2 mm) and albedo jitter (±3-6%). Grain is stretched about 1:20 along the board.
  - Tie-cone holes 22-32 mm across and 25-40 mm deep, on a grid of about 600 mm horizontal by 600-900 mm vertical, aligned to board joints.
  - Pour lifts 1.2-3.6 m (often one storey). Each lift line shows a thin horizontal seam with a small tone shift and grout-bleed streaks.
  - Control or expansion joints every 6-9 m along walls.

### 2. BLAME! / Control: composition principles (no frames copied)

- **BLAME!:** the City is a layered megastructure, and the underside of each layer lights the layer below. The hero is a silent loner. **VD** (Wikipedia). Figures read as tiny next to the structures ("like ants"). Blocky, repeating man-made lines are set against chaotic organic forms. Nihei trained in architecture. **VD** (Carroll News)
- **Control:** sharp brutalist architecture against the fluid Hiss, Escher-like spaces, tall pipes that read like a forest, and an intense red reserved for the Hiss. **VD** (Game Informer). Brutalism is at the core of the look, and the team deliberately avoided other games as references. **VD** (alanwake.info, quoting Remedy)
- **Turned into rules (UV):**
  - Repeat verticals at a regular rhythm so perspective shrinkage shows the distance.
  - One small figure or human-scale object per vista.
  - One hard key light against near-black.
  - Fog layers that separate depth planes.
  - Mostly cool, desaturated concrete with a single warm sodium or red accent. Concrete linear albedo 0.25-0.40.

### 3. Crank-slider (pistons) — VS (Wikipedia "Piston motion equations", checked by finite difference)

- Position: `x(θ) = r cosθ + sqrt(l² − r² sin²θ)`, with r = half stroke, l = rod length, θ measured from top dead centre (TDC).
- Velocity: `dx/dθ = −r sinθ − r² sinθ cosθ / sqrt(l² − r² sin²θ)`. Multiply by ω in the time domain.
- Acceleration: `−r cosθ − r²(cos²θ − sin²θ)/sqrt(l²−r²sin²θ) − r⁴ sin²θ cos²θ/(l²−r²sin²θ)^{3/2}`.
- Rod angle: `φ = asin((r/l) sinθ)`, so φ_max = asin(r/l), about 19.5° at l/r = 3. Peak velocity is not at 90°: for l = 6 in, r = 2 in it is at about ±73.2°.
- Typical rod length is about 1.4-2.2 times the stroke, so l/r ≈ 2.8-4.4. **VD-weak.** Marine crosshead engines use short rods. **VD**
- Real reference: RTA96-C mean piston speed 8.5 m/s at 102 rpm, which matches 2·S·n/60. **VS**

### 4. Fog, light shafts, phase functions, quadrature

- **Wronski, SIGGRAPH 2014 (AC4).** **VS** (slide text extracted from the PDF)
  - Frustum-aligned volume of 160x90x64 or 160x90x128 with an exponential depth distribution packed near the camera. Range 50-128 m.
  - Pass order: light the froxels, then a 2D compute march through the depth slices, then apply with one 3D-texture lookup and an FMA (works for transparents too).
  - Shadows: ESM downsampled to R32F 1024x256 plus a separable box blur. ESM leaking was not visible in fog.
  - Density: one octave of wind-animated Perlin noise plus exponential height falloff. A 2-colour art-driven phase in AC4; HG and SH extensions came later.
  - Cost about 1.1 ms. A 1-sample temporal jitter plus reprojection removed almost all edge artefacts.
- **Hillaire, SIGGRAPH 2015 (Frostbite).** **VD** (slide text on SlideShare)
  - Default froxels: 8x8-pixel tiles, 64 depth slices.
  - Artists author {absorption, scattering} or {albedo, extinction}. σt = σs + σa and albedo = σs/σt.
  - Temporal: blend 5% current into history (exponential moving average) with Halton jitter, using the same offset along each view ray.
  - Uses a single average phase g ("no multi lobe"). PS4 900p total cost: 2.95 ms at 8x8 tiles, 0.83 ms at 16x16.
  - Slide 28 says to "integrate lighting w.r.t. transmittance over froxel depth D". The closed form is `S_int = (S − S·exp(−σt·D)) / σt`, then `L += T·S_int; T *= exp(−σt·D)`, where S = σs·phase·L_light·shadow. This is **VS-math** (∫₀ᴰ S e^{−σt x} dx); the slide itself is garbled in the text extraction.
- **Schneider, SIGGRAPH 2015 (Horizon Zero Dawn clouds).** **VS** (PDF text)
  - Lighting model: Beer's law, Henyey-Greenstein, and a "powder" darkening term that depends on view direction.
  - 64-128 potential march samples plus 6 cone light samples. Cheap large steps until density is found, then one step back.
  - Quarter-res buffer updates 1 of 16 pixels per 4x4 block each frame, with reprojection. About 20 ms naive, about 2 ms target.
  - The powder formula `2·e^{−d}(1−e^{−2d})` is **UV** (it is drawn as an image in the slides).
- **Henyey-Greenstein:** `p(θ) = (1−g²) / (4π (1 + g² − 2g cosθ)^{3/2})`, with cosθ = dot(rayDir, dirToLight) and g > 0 meaning forward scattering. It integrates to 1 over the sphere (numerically 0.99999999977 at g = 0.6). **VS**
  - pbrt writes `+2g cosθ` because both its directions point away from the scattering point. This sign flip is a common bug. **VD**
  - Forward/back ratio is (1+g)³/(1−g)³: about 730:1 at g = 0.8. **VS-math**
- **Schlick approximation:** `(1−k²)/(4π(1 − k cosθ)²)` with `k ≈ 1.55g − 0.55g³`. I confirmed the normalisation analytically; the k fit is **UV**.
- **Haines 1991**, Graphics Gems II §V.1, pp. 247-250. **VS** (erich666/GraphicsGems `gemsii/RayCPhdron.c`). Planes are `n·x + w = 0` with outward normals.

  ```
  tnear=-INF; tfar=tmax;
  foreach plane: vd=dot(n,dir); vn=dot(n,org)+w;
    if(vd==0){ if(vn>0) MISS; }
    else { t=-vn/vd;
      if(vd<0){ if(t>tfar) MISS; if(t>tnear) tnear=t; }   // front face
      else    { if(t<tnear) MISS; if(t<tfar)  tfar=t;  } } // back face
  hit: tnear>=0 → entering at tnear; else inside, exit at tfar (if < tmax)
  ```

  For fog, use the segment `[max(tnear,0), min(tfar, sceneDepth)]`.
- **Gauss-Legendre, 12 points on [−1,1].** Computed by Newton iteration at 40 digits; matches the pomax table to 16 digits and the weights sum to 2. **VS**

| ±x_i | w_i |
|---|---|
| 0.12523340851146891328 | 0.24914704581340277323 |
| 0.36783149899818018413 | 0.23349253653835480571 |
| 0.58731795428661748293 | 0.20316742672306592477 |
| 0.76990267419430469253 | 0.16007832854334622108 |
| 0.90411725637047490878 | 0.10693932599531842664 |
| 0.98156063424671924356 | 0.04717533638651182776 |

To map onto [a, b]: `x = (b−a)/2·t + (a+b)/2`, and multiply each weight by (b−a)/2.

### 5. Procedural materials

- **Quilez, box-filtered checker** (shadertoy XlcSz2). **VS**: I checked it numerically; it matches the naive checker within 2e-12 as w → 0 and returns 0.5 for large w.

  ```
  w = max(abs(dpdx),abs(dpdy));
  i = 2*(abs(frac((p-0.5*w)*0.5)-0.5) - abs(frac((p+0.5*w)*0.5)-0.5))/w;
  return 0.5 - 0.5*i.x*i.y;
  ```

  Pitfall: the `tri()`-based snippet printed in the checkerfiltering article returns values outside [0, 1]. Use the version from the filterableprocedurals article above.
- **Box-filtered grid** (XtBfzz), where 1/N is the line width. **VS**: the result is 0 on lines, 1 inside cells, and (1−1/N)² as the footprint gets large.

  ```
  a=p+0.5*w; b=p-0.5*w;
  i=(floor(a)+min(frac(a)*N,1)-floor(b)-min(frac(b)*N,1))/(N*w);
  return (1-i.x)*(1-i.y);
  ```

  Add a small epsilon to w, because w = 0 gives NaN.
- **Value noise** (Quilez "morenoise"): quintic fade `6t⁵−15t⁴+10t³`, with derivative `30t²(t−1)²`. The cubic fade causes discontinuity artefacts. Analytic derivatives are about 5x cheaper than central differences. **VS**
- **fBm** (Quilez "fbm"): gain `G = 2^{−H}`, and G = 0.5 (H = 1) is the natural choice. Detune lacunarity to 1.98-2.01 and rotate the domain each octave (`mat2(0.8,-0.6,0.6,0.8)`). **VS**
- **PCG hash.** **VS** (reedbeta; JCGT paper text)

  ```
  uint pcg(uint v){ uint s=v*747796405u+2891336453u;
    uint w=((s>>((s>>28u)+4u))^s)*277803737u; return (w>>22u)^w; }
  uint3 pcg3d(uint3 v){ v=v*1664525u+1013904223u;
    v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y; v^=v>>16u;
    v.x+=v.y*v.z; v.y+=v.z*v.x; v.z+=v.x*v.y; return v; }
  ```

  Jarzynski and Olano recommend pcg3d/pcg4d for N→N hashes and xxhash32 for 3→1.
- **"Hash without Sine"** (David Hoskins, MIT licence, 4djSRW; not by Quilez): `p3=frac(p.xyx*.1031); p3+=dot(p3,p3.yzx+33.33); return frac((p3.x+p3.y)*p3.z);`. **VD** (third-party copy). It is documented to fail with small changes in p.
- **Pitfalls (VS-math):**
  - At world coordinates around 4 km, the float32 step is about 0.5 mm, so mm-scale noise and float hashes break down. Hash integer cell coordinates with pcg3d, or evaluate noise in local, rebased space.
  - HLSL `fmod` is not GLSL `mod` for negative inputs; use `x - y*floor(x/y)`.
  - GLSL `mat2(...)` is column-major and HLSL `float2x2(...)` is row-major, so a ported matrix comes out transposed.

### 6. Hex grids — VS (Red Blob implementation guide)

- Flat-top matrices: `f = (3/2, 0, √3/2, √3)`, `b = (2/3, 0, −1/3, √3/3)`, start_angle 0.
- So `q = (2/3·x)/s` and `r = (−1/3·x + √3/3·z)/s` are confirmed. The inverse is `x = s·3/2·q`, `z = s·(√3/2·q + √3·r)`.
- Flat-top hex width = 2s, height = √3·s. Pointy-top: `f = (√3, √3/2, 0, 3/2)`, `b = (√3/3, −1/3, 0, 2/3)`, start_angle 0.5.
- Rounding:

  ```
  round q,r,s; if(dq>dr && dq>ds) q=-r-s; else if(dr>ds) r=-q-s; else s=-q-r;
  ```

- Distance `(|dq|+|dr|+|ds|)/2`. Directions: (1,0,−1), (1,−1,0), (0,−1,1), (−1,0,1), (−1,1,0), (0,1,−1).
- Pitfall (UV): the guide assumes y-down screen space. When mapped to Unity XZ, corner order flips handedness, so check triangle winding for upward-facing caps (Unity front faces are clockwise).

### 7. Banding, dithering, YouTube

- **Gjoel, INSIDE (GDC 2016).** **VS** (slide text extracted)
  - Use the highest precision available, use sRGB or another dark-biased encoding, and dither when tonemapping to 8 bit: "almost always dither any 8bit write".
  - Uniform 1-LSB noise causes noise modulation. Triangular (TPDF) noise makes the error independent of the signal, but it must be 2 LSB wide.
  - Two ways to make TPDF: add two uniform randoms, or reshape one uniform (shadertoy 4t2SDh; reference ltBSRG).
  - INSIDE dithered after every pass and also dithered its 8-bit G-buffer normals.
- **URP 17.6 source:** `UniversalAdditionalCameraData.dithering` (bool, `UniversalAdditionalCameraData.cs:919`), keyword `_DITHERING` (`UniversalRenderPipelineCore.cs:1217`). **VS**
  - `ApplyDithering` (`Shaders/PostProcessing/Common.hlsl:188-206`) applies blue-noise-driven TPDF noise on [−1,1]/255 in sRGB space, in UberPost or FinalPost, cycling the `blueNoise16LTex` textures each frame.
  - It is forced off in the Scene view (`UniversalRenderPipeline.cs:1734`), so take captures from the Game view or the player.
  - It only runs when post-processing is on (`UniversalRendererRenderGraph.cs:648,1384`). Passes after post-processing are not dithered.
- **Codec effects.** **VD** (forasoft)
  - Quantization erases low-amplitude gradients. 10-bit coding narrows bands about 4x.
  - Dither added just before a lossy encode is often quantized away, so prefer higher bit depth and grain added near delivery.
- **YouTube upload settings:** MP4, H.264 High Profile, progressive, 2 B-frames, closed GOP of half the frame rate, CABAC, 4:2:0, BT.709. YouTube converts full range to limited range. **VD**
  - SDR bitrates (24-30 / 48-60 fps): 1080p 8/12 Mbps, 1440p 16/24, 2160p 35-45/53-68.
  - Uploading at 2160p to get VP9/AV1 is anecdotal. **UV**

### 8. Real scales

| Object | Figure | Tag |
|---|---|---|
| Bingham Canyon | 1.2 km deep, ~4 km across (Guinness); one infographic says 0.97 km | VD |
| Mponeng | ~4 km deep (Guinness); ~3.8 km per Wikipedia, planned 4.27 km | VD |
| Burj Khalifa | 828 m architectural, 829.8 m tip, 154+9 floors | VD |
| RTA96-C | bore 960 mm, stroke 2500 mm, 6-14 cylinders inline; 14-cyl 13.5 m high, 26.59 m long, >2300 t; crankshaft 300 t; 15-102 rpm; 80,080 kW | VD |
| NASA Ames 40x80 | six 40 ft (12.2 m), 15-blade, variable-pitch fans; 18,000 hp each | VD |
| Mine fans | Broken Hill 1954: 5.3 m single-stage axial; TLT range 1.8-5.3 m, up to 13.5 MW; Zitrón 3.8 m; Shanghai Electric 4.22 m | VD (vendor) |
| Habitat 67 module | 5.3 x 11.7 x 3.0 m | VD |

**Fan maths (VS-math):** tip speed is `v = π·D·n/60`; an 80 m fan at 1 rpm has a 4.2 m/s tip. To avoid wagon-wheel strobing, keep the per-frame rotation `ω/F` well below `(360°/B)/2` for B blades. Check this at 30 fps, not only at 60.

### 9. Megalophobia staging

- "The trick is people": viewers cannot judge scale without a reference, so keep a human-scale element in frame (Greig Fraser). **VD** (Inverse)
- **Aerial perspective:** contrast and saturation drop and fine detail is lost with distance as colour shifts toward the haze colour. **VD**
- **Koschmieder visual range:** `V = 3.912/β_ext` at 2% contrast. **VD.** So β = 3.912/V: a shaft wall that should fade out at 400 m needs β ≈ 0.0098 m⁻¹, and one that fades at 1.5 km needs β ≈ 0.0026 m⁻¹.
- **UV recommendations:**
  - Slow walk (1.0-1.3 m/s) with minimal head bob; never cut on a reveal.
  - Frame reveals through doorways or occluders, then open to the void.
  - Repeat known sizes (2.1 m doors, 1.1 m rails, regular lamps) receding into haze.
  - Stack discrete haze layers by height: `σ(h) = σ0·exp(−(h−h0)/H)`.
  - A narrower FOV (50-60° vertical) on vistas compresses depth and enlarges distant mass.
  - Slow pistons with 10-30 s periods; real engines run about 100 rpm.

### Sources (all accessed 2026-10-29)

- https://advances.realtimerendering.com/s2014/wronski/bwronski_volumetric_fog_siggraph2014.pdf — froxel fog numbers
- https://www.slideshare.net/DICEStudio/physically-based-and-unified-volumetric-rendering-in-frostbite — Hillaire froxels, temporal blend, costs
- https://advances.realtimerendering.com/s2015/index.html and its "The Real-time Volumetric Cloudscapes of Horizon - Zero Dawn - ARTR.pdf" — HZD clouds
- https://pbr-book.org/3ed-2018/Volume_Scattering/Phase_Functions — HG formula and sign convention
- https://raw.githubusercontent.com/erich666/GraphicsGems/master/gemsii/RayCPhdron.c ; https://www.gameenginegems.com/gemsdb/article.php?id=819 — Haines code and citation
- https://pomax.github.io/bezierinfo/legendre-gauss.html — Gauss-Legendre cross-check
- http://iquilezles.org/articles/checkerfiltering/ , /filterableprocedurals/ , /fbm/ , /morenoise/ — filtering, fBm, value noise
- https://reedbeta.com/blog/hash-functions-for-gpu-rendering/ ; https://www.jcgt.org/published/0009/03/02/paper.pdf — PCG and pcg3d
- https://pyssv.readthedocs.io/en/latest/_modules/random.glsl.html — Hoskins hash copy (MIT)
- https://www.redblobgames.com/grids/hexagons/implementation.html — hex formulas
- https://blog.playdead.com/articles/inside_presentations/inside_publications.html ; https://media.githubusercontent.com/media/playdeadgames/publications/master/INSIDE/rendering_inside_gdc2016.pdf — INSIDE dithering
- https://www.forasoft.com/learn/video-quality/articles-vqm/banding-gradient-steps — banding after encoding
- https://support.google.com/youtube/answer/1722171 — YouTube upload settings
- https://en.wikipedia.org/wiki/Piston_motion_equations — crank-slider
- https://en.wikipedia.org/wiki/Connecting_rod — rod ratios
- https://en.wikipedia.org/wiki/W%C3%A4rtsil%C3%A4-Sulzer_RTA96-C ; https://en.wikipedia.org/wiki/Burj_Khalifa ; https://www.guinnessworldrecords.com/world-records/383096-deepest-open-pit-mine ; https://www.mining-technology.com/features/feature-top-ten-deepest-mines-world-south-africa/ ; https://www.nasa.gov/image-detail/amf-aal-4479 ; https://im-mining.com/?p=83589 ; https://www.ausimm.com/publications/conference-proceedings/the-ausimm-proceedings-1956/the-zinc-corporation-limited---broken-hill/ — real scales
- https://www.barbicanliving.co.uk/barbican-story/construction/hammered-and-brushed-finish/ ; https://www.studiomatrx.org/architecture-canon/national-theatre ; https://architectuul.com/architecture/boston-city-hall ; https://www.pci.org/PCI_Docs/Publications/PCI%20Journal/1967/February-1967/Habitat%2067%20-%20Towards%20the%20Development%20of%20a%20Building%20System.pdf ; https://www.specformliners.com/wp-content/uploads/pdfs/guides/elastospec-guide.pdf — concrete references
- https://en.wikipedia.org/wiki/Blame! ; https://carrollnews.org/200840/arts-and-life/niheis-style-is-special/ ; https://gameinformer.com/2019/03/27/the-mesmerizing-art-behind-control ; https://www.alanwake.info/2026/05/1st-may-2026-third-control-resonant.html — composition
- https://www.inverse.com/entertainment/dune-2-greig-fraser-interview ; https://en.wikipedia.org/wiki/Aerial_perspective ; https://en.wikipedia.org/wiki/Visibility — scale and haze

---

<a id="source-index"></a>
## Source index

Every URL cited in the sections above, listed once, in section order. All were accessed on 2026-10-29 and treated as untrusted data. URLs that a section abbreviates (`.../time/capturedeltatime`, `.../DC_Blocker_Frequency_Response.html`, `/filterableprocedurals/`, `/fbm/`, `/morenoise/`) are expanded here with the prefix of the URL they follow. The JOS PASP pages are listed under their common base URL because the section names the pages, not their full URLs. The section that cites each URL is given in brackets.

| URL | Accessed | Used for |
|---|---|---|
| https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus | 2026-10-29 | [urp-shaders] Forward+ custom light loop, `_CLUSTER_LIGHT_LOOP`, InputData requirement, directional pre-loop |
| https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-alpha-to-mask | 2026-10-29 | [urp-shaders] AlphaToMask syntax and values |
| https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-render-state-commands/alpha-to-mask | 2026-10-29 | [urp-shaders] AlphaToMask needs MSAA; unpredictable without it |
| https://docs.unity3d.com/6/Documentation/Manual/urp/shader-stripping.html | 2026-10-29 | [urp-shaders] Strip Unused Variants; `ShaderStrippingReport` in Editor.log |
| https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/built-in/shaders-in-universalrp/shader-stripping/features | 2026-10-29 | [urp-shaders] Shader Build Settings keyword sets; Strip Unused Variants note |
| https://docs.unity.com/en-us/engine/current/script-reference/unityeditor/rendering/editorgraphicssettings/batchrenderergroupshaderstrippingmode | 2026-10-29 | [urp-csharp] BRG stripping getter is read-only |
| https://docs.unity.com/en-us/engine/current/script-reference/unityeditor/rendering/batchrenderergroupstrippingmode | 2026-10-29 | [urp-csharp] BatchRendererGroupStrippingMode member names |
| https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html | 2026-10-29 | [urp-csharp] GRD requirements and setup (search summary) |
| https://docs.unity3d.com/Manual/urp/renderer-features/how-to-fullscreen-blit.html | 2026-10-29 | [urp-csharp] Blit.hlsl / Vert / `_BlitTexture` shader pattern (search summary) |
| https://docs.unity3d.com/kr/current/Manual/urp/renderer-features/write-a-scriptable-render-pass.html | 2026-10-29 | [urp-csharp] Scriptable render pass / blit shader pattern (search summary) |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/rendersettings/ambientprobe | 2026-10-29 | [urp-csharp] ambientProbe is settable; no runtime regeneration documented |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/dynamicgi/updateenvironment | 2026-10-29 | [urp-csharp] UpdateEnvironment is an async, scheduled cubemap update |
| https://discussions.unity.com/t/is-it-possible-to-modify-update-skybox-ambient-light-values-at-runtime/590700 | 2026-10-29 | [urp-csharp] Anecdotal: skybox changes do not recalculate lighting (search summary) |
| https://learn.microsoft.com/en-us/windows/win32/direct3d10/d3d10-graphics-programming-guide-resources-float-rules | 2026-10-29 | [urp-csharp] 11/10-bit float mantissa sizes (B10G11R11 banding) |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshinstanced | 2026-10-29 | [mesh-instancing] RenderMeshInstanced instance limits, struct fields, culling |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderparams | 2026-10-29 | [mesh-instancing] RenderParams field list; null camera = all cameras |
| https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/Graphics/Graphics.cs | 2026-10-29 | [mesh-instancing] Managed RenderMeshInstanced implementation (no splitting, pinning) |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshindirect | 2026-10-29 | [mesh-instancing] Indirect args buffer, shader macros, bounds |
| https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer | 2026-10-29 | [mesh-instancing] GRD requirements |
| https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering | 2026-10-29 | [mesh-instancing] GRD compatibility conditions |
| https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials | 2026-10-29 | [mesh-instancing] SRP Batcher compatibility rules |
| https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing | 2026-10-29 | [mesh-instancing] SRP Batcher vs GPU instancing for MeshRenderers |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/materialpropertyblock/setvectorarray | 2026-10-29 | [mesh-instancing] MPB array length fixed at first set |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/allocatewritablemeshdata | 2026-10-29 | [mesh-instancing] MeshData workflow |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/indexformat | 2026-10-29 | [mesh-instancing] 16/32-bit index rules; changing it clears indices |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/uploadmeshdata | 2026-10-29 | [mesh-instancing] Upload timing and readability |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/setvertexbufferparams | 2026-10-29 | [mesh-instancing] 4-byte vertex attribute rule |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/staticbatchingutility/combine | 2026-10-29 | [mesh-instancing] Static batching copy and lock semantics |
| https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/character-control-section/class-character-controller | 2026-10-29 | [mesh-instancing] CharacterController property guidance |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/charactercontroller/move | 2026-10-29 | [mesh-instancing] CharacterController.Move semantics |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/physics/synctransforms | 2026-10-29 | [mesh-instancing] When to call Physics.SyncTransforms |
| https://docs.unity3d.com/2023.1/Documentation/Manual/class-MeshCollider.html | 2026-10-29 | [mesh-instancing] Convex 255-triangle limit, readable requirement (older version) |
| https://discussions.unity.com/t/character-controller-ignores-transform-position/729515 | 2026-10-29 | [mesh-instancing] Community: CC overwrites direct transform writes |
| https://discussions.unity.com/t/moving-platform-and-charactercontroller-move-not-working-the-player-falls/223755 | 2026-10-29 | [mesh-instancing] Community: moving-platform delta approach |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/capturedeltatime | 2026-10-29 | [capture-player] captureDeltaTime semantics |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/captureframerate | 2026-10-29 | [capture-player] captureFramerate reciprocal and rounding |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screencapture/capturescreenshot | 2026-10-29 | [capture-player] Composed-screen capture, path handling |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screencapture/capturescreenshotastexture | 2026-10-29 | [capture-player] Capture timing and cleanup |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshotintorendertexture | 2026-10-29 | [capture-player] RT capture + AsyncGPUReadback example |
| https://issuetracker.unity.com/issues/4340/screencapturecapturescreenshot-renders-blocky-artifacts-when-super-size-is-greater-than-1 | 2026-10-29 | [capture-player] superSize blocky output, Won't Fix |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/waitforendofframe | 2026-10-29 | [capture-player] End-of-frame timing; not in batch mode |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendering/asyncgpureadback/request | 2026-10-29 | [capture-player] AsyncGPUReadback overloads and format conversion |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadbackrequest/waitforcompletion | 2026-10-29 | [capture-player] WaitForCompletion stall semantics |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/texture2d/readpixels | 2026-10-29 | [capture-player] ReadPixels same-format rule |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexturereadwrite | 2026-10-29 | [capture-player] sRGB/linear render texture rules |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screen/setresolution | 2026-10-29 | [capture-player] SetResolution end-of-frame, closest resolution |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen | 2026-10-29 | [capture-player] Startup precedence, PlayerPrefs, Alt+Enter |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player | 2026-10-29 | [capture-player] Player command-line arguments |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor | 2026-10-29 | [capture-player] Editor batchmode, -quit, -executeMethod |
| https://docs.unity.com/en-us/engine/6000.6/manual/get-started/project-configuration/log-files | 2026-10-29 | [capture-player] Player and Editor log paths |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/quit | 2026-10-29 | [capture-player] Application.Quit exit code |
| https://docs.unity.com/en-us/engine/6000.6/manual/cameras/urp/multiple/user-render-requests | 2026-10-29 | [capture-player] SingleCameraRequest |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/renderpipeline/submitrenderrequest | 2026-10-29 | [capture-player] StandardRequest = full stack, synchronous |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/render | 2026-10-29 | [capture-player] Camera.Render |
| https://docs.unity3d.com/Packages/com.unity.polyspatial.visionos@2.1/manual/RenderTextures.html | 2026-10-29 | [capture-player] Batch mode cameras do not auto-render |
| https://issuetracker.unity.com/issues/24220/errors-are-logged-continuously-in-the-player-when-panelsettings-is-created-at-runtime | 2026-10-29 | [capture-player] Runtime PanelSettings bug (UUM-150415) and fix version |
| https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-render-runtime-ui/uie-runtime-panel-settings | 2026-10-29 | [capture-player] Panel Settings properties |
| https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-work-with-text/uie-get-started-with-text | 2026-10-29 | [capture-player] Advanced Text Generator; no static fonts |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/uielements/panelsettings/themestylesheet | 2026-10-29 | [capture-player] themeStyleSheet semantics |
| https://support.unity.com/hc/en-us/articles/212165803 | 2026-10-29 | [capture-player] Unity Personal can disable the splash in Unity 6 |
| https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/windows/playersettings | 2026-10-29 | [capture-player] Windows Player settings |
| https://github.com/PowerShell/PowerShell/issues/20400 | 2026-10-29 | [capture-player] Start-Process ExitCode caveats (search snippet only) |
| https://docs.unity.com/en-us/engine/6000.6/manual/video/player/clock | 2026-10-29 | [capture-player] Capture clock vs audio DSP clock (search snippet only) |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/onaudiofilterread | 2026-10-29 | [audio] OnAudioFilterRead threading, generator rule, data layout |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset | 2026-10-29 | [audio] AudioSettings.Reset semantics, valid buffer sizes and rates |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/dsptime | 2026-10-29 | [audio] dspTime and pause behaviour |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audioclip/create | 2026-10-29 | [audio] AudioClip.Create overloads, stream semantics, Scriptable Audio Pipeline note |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/time/captureframerate | 2026-10-29 | [audio] Capture timing vs audio |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/time/capturedeltatime | 2026-10-29 | [audio] Capture timing vs audio (cited as `.../time/capturedeltatime`) |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager.md | 2026-10-29 | [audio] Audio Manager: sample rate 0, Disable Unity Audio, Virtualize Effect, Output Suspension |
| https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/filters/class-audio-reverb-filter.md | 2026-10-29 | [audio] Audio Reverb Filter ranges, User preset |
| https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audioreverbfilter.md | 2026-10-29 | [audio] AudioReverbFilter scripting reference, RequireComponent |
| https://docs.unity.com/en-us/engine/6000.6/manual/audio/scriptable-processors/generators | 2026-10-29 | [audio] IAudioGenerator overview (not adopted) |
| https://issuetracker.unity3d.com/issues/onaudiofilterread-not-working-as-expected-for-generator-scripts | 2026-10-29 | [audio] Generator detection in Play() (search snippet only) |
| https://issuetracker.unity3d.com/issues/empty-data-buffer-gets-passed-to-onaudiofilterread-when-using-video-player | 2026-10-29 | [audio] Generator detection in Play() (search snippet only) |
| https://discussions.unity.com/t/onaudiofilterread-not-playing/503615 | 2026-10-29 | [audio] Clipless-source anecdotes |
| https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf | 2026-10-29 | [audio] TPT state-variable filter (primary; not text-extractable) |
| https://forum.juce.com/t/svf-resonant/56889/17 | 2026-10-29 | [audio] SVF text transcription |
| https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html | 2026-10-29 | [audio] DC blocker |
| https://ccrma.stanford.edu/~jos/filters/DC_Blocker_Frequency_Response.html | 2026-10-29 | [audio] DC blocker gain normalisation |
| https://ccrma.stanford.edu/~jos/filters/Two_Pole.html | 2026-10-29 | [audio] Two-pole modal resonator |
| https://ccrma.stanford.edu/~jos/pasp/ | 2026-10-29 | [audio] FDN design: pages First_Order_Delay_Filter_Design, Delay_Line_Damping_Filter_Design, Achieving_Desired_Reverberation_Times, Hadamard_Matrix, Choice_Delay_Lengths, Mode_Density_Requirement |
| https://www.musicdsp.org/en/latest/Filters/76-pink-noise-filter.html | 2026-10-29 | [audio] Paul Kellet pink-noise coefficients (and its pink.txt) |
| https://forum.juce.com/t/pink-noise-generator/40013/2 | 2026-10-29 | [audio] Kellet coefficients transcription |
| https://en.wikipedia.org/wiki/Xorshift | 2026-10-29 | [audio] xorshift32 (Marsaglia, doi:10.18637/jss.v008.i14) |
| https://mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html | 2026-10-29 | [audio] WAV file format |
| https://cacophony.js.org/classes/TruePeakDetector.html | 2026-10-29 | [audio] BS.1770 true-peak structure |
| https://github.com/audionuma/r128x/issues/10 | 2026-10-29 | [audio] BS.1770 true-peak filter details |
| https://www.audiolabs-erlangen.com/resources/2018-IEEE-Modal | 2026-10-29 | [audio] Schlecht FDN modal density research |
| https://audiolabs-erlangen.de/resources/2019-WASPAA-DFM-FDN | 2026-10-29 | [audio] Schlecht FDN density research |
| https://ar5iv.arxiv.org/html/1912.08888 | 2026-10-29 | [audio] Schlecht/Habets echo and modal density |
| https://advances.realtimerendering.com/s2014/wronski/bwronski_volumetric_fog_siggraph2014.pdf | 2026-10-29 | [art-technique] Wronski froxel fog numbers (AC4) |
| https://www.slideshare.net/DICEStudio/physically-based-and-unified-volumetric-rendering-in-frostbite | 2026-10-29 | [art-technique] Hillaire froxels, temporal blend, costs |
| https://advances.realtimerendering.com/s2015/index.html | 2026-10-29 | [art-technique] HZD clouds (Schneider), incl. its "The Real-time Volumetric Cloudscapes of Horizon - Zero Dawn - ARTR.pdf" |
| https://pbr-book.org/3ed-2018/Volume_Scattering/Phase_Functions | 2026-10-29 | [art-technique] Henyey-Greenstein formula and sign convention |
| https://raw.githubusercontent.com/erich666/GraphicsGems/master/gemsii/RayCPhdron.c | 2026-10-29 | [art-technique] Haines ray/convex-polyhedron code |
| https://www.gameenginegems.com/gemsdb/article.php?id=819 | 2026-10-29 | [art-technique] Haines 1991 citation |
| https://pomax.github.io/bezierinfo/legendre-gauss.html | 2026-10-29 | [art-technique] Gauss-Legendre table cross-check |
| http://iquilezles.org/articles/checkerfiltering/ | 2026-10-29 | [art-technique] Box-filtered checker (article snippet has a range pitfall) |
| http://iquilezles.org/articles/filterableprocedurals/ | 2026-10-29 | [art-technique] Filterable procedurals: checker and grid |
| http://iquilezles.org/articles/fbm/ | 2026-10-29 | [art-technique] fBm gain, lacunarity, domain rotation |
| http://iquilezles.org/articles/morenoise/ | 2026-10-29 | [art-technique] Value noise with quintic fade and analytic derivatives |
| https://reedbeta.com/blog/hash-functions-for-gpu-rendering/ | 2026-10-29 | [art-technique] PCG hash |
| https://www.jcgt.org/published/0009/03/02/paper.pdf | 2026-10-29 | [art-technique] Jarzynski & Olano GPU hashes, pcg3d |
| https://pyssv.readthedocs.io/en/latest/_modules/random.glsl.html | 2026-10-29 | [art-technique] Hoskins "Hash without Sine" copy (MIT) |
| https://www.redblobgames.com/grids/hexagons/implementation.html | 2026-10-29 | [art-technique] Hex grid formulas |
| https://blog.playdead.com/articles/inside_presentations/inside_publications.html | 2026-10-29 | [art-technique] INSIDE publications index |
| https://media.githubusercontent.com/media/playdeadgames/publications/master/INSIDE/rendering_inside_gdc2016.pdf | 2026-10-29 | [art-technique] INSIDE banding and dithering slides (Gjoel, GDC 2016) |
| https://www.forasoft.com/learn/video-quality/articles-vqm/banding-gradient-steps | 2026-10-29 | [art-technique] Banding after video encoding |
| https://support.google.com/youtube/answer/1722171 | 2026-10-29 | [art-technique] YouTube upload encoding settings and bitrates |
| https://en.wikipedia.org/wiki/Piston_motion_equations | 2026-10-29 | [art-technique] Crank-slider equations |
| https://en.wikipedia.org/wiki/Connecting_rod | 2026-10-29 | [art-technique] Rod-to-stroke ratios |
| https://en.wikipedia.org/wiki/W%C3%A4rtsil%C3%A4-Sulzer_RTA96-C | 2026-10-29 | [art-technique] RTA96-C engine scale |
| https://en.wikipedia.org/wiki/Burj_Khalifa | 2026-10-29 | [art-technique] Burj Khalifa height |
| https://www.guinnessworldrecords.com/world-records/383096-deepest-open-pit-mine | 2026-10-29 | [art-technique] Bingham Canyon depth |
| https://www.mining-technology.com/features/feature-top-ten-deepest-mines-world-south-africa/ | 2026-10-29 | [art-technique] Deepest mines (Mponeng) |
| https://www.nasa.gov/image-detail/amf-aal-4479 | 2026-10-29 | [art-technique] NASA Ames 40x80 fans |
| https://im-mining.com/?p=83589 | 2026-10-29 | [art-technique] Mine fan sizes (vendor) |
| https://www.ausimm.com/publications/conference-proceedings/the-ausimm-proceedings-1956/the-zinc-corporation-limited---broken-hill/ | 2026-10-29 | [art-technique] Broken Hill 1954 mine fan |
| https://www.barbicanliving.co.uk/barbican-story/construction/hammered-and-brushed-finish/ | 2026-10-29 | [art-technique] Barbican hammered concrete finish |
| https://www.studiomatrx.org/architecture-canon/national-theatre | 2026-10-29 | [art-technique] National Theatre board-marked concrete |
| https://architectuul.com/architecture/boston-city-hall | 2026-10-29 | [art-technique] Boston City Hall construction (wiki) |
| https://www.pci.org/PCI_Docs/Publications/PCI%20Journal/1967/February-1967/Habitat%2067%20-%20Towards%20the%20Development%20of%20a%20Building%20System.pdf | 2026-10-29 | [art-technique] Habitat 67 module sizes |
| https://www.specformliners.com/wp-content/uploads/pdfs/guides/elastospec-guide.pdf | 2026-10-29 | [art-technique] Form-tie hole placement |
| https://en.wikipedia.org/wiki/Blame! | 2026-10-29 | [art-technique] BLAME! megastructure composition |
| https://carrollnews.org/200840/arts-and-life/niheis-style-is-special/ | 2026-10-29 | [art-technique] Nihei's style and scale |
| https://gameinformer.com/2019/03/27/the-mesmerizing-art-behind-control | 2026-10-29 | [art-technique] Control art direction |
| https://www.alanwake.info/2026/05/1st-may-2026-third-control-resonant.html | 2026-10-29 | [art-technique] Remedy on brutalism in Control |
| https://www.inverse.com/entertainment/dune-2-greig-fraser-interview | 2026-10-29 | [art-technique] "The trick is people" scale staging |
| https://en.wikipedia.org/wiki/Aerial_perspective | 2026-10-29 | [art-technique] Aerial perspective |
| https://en.wikipedia.org/wiki/Visibility | 2026-10-29 | [art-technique] Koschmieder visual range |
