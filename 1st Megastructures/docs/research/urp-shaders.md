## Handwritten URP Lit Shaders: URP 17.6.0 (Unity 6000.6.0f1)

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
