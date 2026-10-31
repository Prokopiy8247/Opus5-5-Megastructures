# 01 — Handwritten URP 17.6 lit shaders that match URP Lit (Unity 6000.6.0f1)

Scope: ForwardLit / ShadowCaster / DepthOnly / DepthNormals for a procedural Unity 6.6 / URP 17.6.0 project.
Direct light comes from URP lights (main + additional, with shadows, Forward+). Ambient (3 colours) and reflection colour are our own.

Abbreviations used below:
- `U` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`
- `C` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`

Tags:
- **VERIFIED-SOURCE**: I read it in the package source for this exact version (path:line given).
- **VERIFIED-DOCS**: confirmed in the official Unity docs (URL given).
- **UNVERIFIED**: reasoning or memory that was not confirmed.

---

## 0. Facts about THIS project that change what a shader must do

| Fact | Consequence | Tag |
|---|---|---|
| `Assets/Settings/PC_Renderer.asset:56` sets `m_RenderingMode: 2`. The enum is `ForwardPlus = 2` (`U/Runtime/UniversalRenderer.cs:11-23`). | The PC path is **Forward+**. A shader without `_CLUSTER_LIGHT_LOOP` gets **zero additional lights**. In Forward+ URP sets `_ADDITIONAL_LIGHTS` to false and `_CLUSTER_LIGHT_LOOP` to true (`U/Runtime/ForwardLights.cs:576-579`). | VERIFIED-SOURCE |
| `PC_RPAsset.asset:58` sets `m_ShadowCascadeCount: 4`. | The keyword `_MAIN_LIGHT_SHADOWS_CASCADE` is active. `_MAIN_LIGHT_SHADOWS` is active only when there is 1 cascade (`U/Runtime/Passes/MainLightShadowCasterPass.cs:322-323`). | VERIFIED-SOURCE |
| `PC_RPAsset.asset:66,69` sets soft shadows on with quality 3 (High). | On Windows standalone only `_SHADOWS_SOFT` is ever enabled. `_LOW/_MEDIUM/_HIGH` are used only on XR-mobile (`U/Runtime/ShadowUtils.cs:662-713`). The quality is read at runtime from `_MainLightShadowParams.y` (`U/ShaderLibrary/Shadows.hlsl:290-308,326-334`). | VERIFIED-SOURCE |
| `PC_Renderer.asset:61-80` has an active `ScreenSpaceAmbientOcclusion` feature with `Source: 1` (DepthNormals, `U/Runtime/RendererFeatures/ScreenSpaceAmbientOcclusion.cs:65-69`) and `AfterOpaque: 0`. | URP renders the **DepthNormals** pass of every opaque object. A custom shader without that pass leaves holes in depth and normals for SSAO and other depth consumers. SSAO only reaches the surface through `_SCREEN_SPACE_OCCLUSION`, so omitting that keyword means no SSAO on our materials (the SSAO pass still costs GPU time). | VERIFIED-SOURCE |
| `PC_RPAsset.asset:85` sets `m_GPUResidentDrawerMode: 1`. `ProjectSettings/GraphicsSettings.asset:49` sets `m_BrgStripping: 2`. | The GPU Resident Drawer is on. Renderers whose shader has no `DOTS_INSTANCING_ON` variant are **excluded from GRD and fall back to the normal SRP Batcher path** (`C/Runtime/GPUDriven/SceneProcessors/WorldProcessor.cs:199-219`). That is valid. The meaning of the enum value `2` is UNVERIFIED (probably "Keep All"). | VERIFIED-SOURCE |
| `PC_RPAsset.asset:76` sets `m_SupportsLightLayers: 1`. | URP enables `_LIGHT_LAYERS` globally (`ForwardLights.cs:619`). If our shader does not declare it, every light affects our objects. | VERIFIED-SOURCE |
| `ProjectSettings/GraphicsSettings.asset:48` sets `m_InstancingStripping: 0`. | Instancing variants are kept only if a material in the build has "Enable GPU Instancing" (enum mapping UNVERIFIED). This matters for runtime-created materials used with `Graphics.RenderMeshInstanced`. | UNVERIFIED |
| `ProjectSettings/ProjectSettings.asset:49` sets `m_ActiveColorSpace: 1`. | The project is in **Linear** colour space. `Shader.SetGlobalColor` does **not** convert sRGB to linear, so pass `color.linear` (https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/setglobalcolor). | VERIFIED-DOCS |
| `universal-config/Runtime/ShaderConfig.cs.hlsl` sets `MAX_VISIBLE_LIGHT_COUNT_DESKTOP (256)` and `USE_DYNAMIC_BRANCH_FOG_KEYWORD (0)`. | On desktop `MAX_LIGHTS_PER_TILE = 256` (`U/ShaderLibrary/Input.hlsl:17-41`). Fog uses `multi_compile_fog` (`U/ShaderLibrary/Fog.hlsl:5-9`). | VERIFIED-SOURCE |

---

## 1. ForwardLit keyword lines in this version (`U/Shaders/Lit.shader:144-186`)

These are verbatim (VERIFIED-SOURCE):

```hlsl
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN   // :144
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS                              // :145
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX                                      // :146
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS                                        // :147
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING                                       // :148
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION                                 // :149
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS                                          // :150
#pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION                                         // :151 (new: URP SSR)
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH // :152
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION                                          // :153
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE                                         // :154 (new)
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3                        // :155
#pragma multi_compile_fragment _ _LIGHT_COOKIES                                                   // :156
#pragma multi_compile _ _LIGHT_LAYERS                                                             // :157
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP                                                       // :158
#include_with_pragmas ".../core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"                    // :163 (Metal/visionOS/PS5 only)
#include_with_pragmas ".../universal/ShaderLibrary/RenderingLayers.hlsl"                         // :164 -> _WRITE_RENDERING_LAYERS
// Unity-defined: LIGHTMAP_SHADOW_MIXING, SHADOWS_SHADOWMASK, DIRLIGHTMAP_COMBINED, LIGHTMAP_ON,
// LIGHTMAP_BICUBIC_SAMPLING, REFLECTION_PROBE_ROTATION, DYNAMICLIGHTMAP_ON, USE_LEGACY_LIGHTMAPS,
// LOD_FADE_CROSSFADE, DEBUG_DISPLAY (:169-178); Fog.hlsl + ProbeVolumeVariants.hlsl (:179-180)
#pragma multi_compile_instancing                     // :184
#pragma instancing_options renderinglayer            // :185
#include_with_pragmas ".../universal/ShaderLibrary/DOTS.hlsl"   // :186
```

- The **Forward+ keyword is `_CLUSTER_LIGHT_LOOP`** (VERIFIED-SOURCE `U/ShaderLibrary/Core.hlsl:13-22`).
  - `_FORWARD_PLUS` has been deprecated since 6.1. It still works through `ForwardPlusKeyword.deprecated.hlsl:12-21` but emits a `#warning`. URP still sets it for backward compatibility (`ForwardLights.cs:579`). **Do not declare it.**
  - Core.hlsl does the following when `_CLUSTER_LIGHT_LOOP` is on: `#define USE_CLUSTER_LIGHT_LOOP 1`, `#define _ADDITIONAL_LIGHTS 1` and `#undef _ADDITIONAL_LIGHTS_VERTEX`.
  - The docs agree: https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/shader-keywords-macros (VERIFIED-DOCS).
- **Soft shadow variants**: `_SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH` all exist. Their static paths are in `Shadows.hlsl:320-337`. On Windows only `_SHADOWS_SOFT` is used (see section 0).

### What a custom shader needs (no SSAO, lightmaps, cookies, decals, probes or URP fog)

| Need | Keyword line | Tag |
|---|---|---|
| Main light shadows, cascaded (plus the 1-cascade and screen-space cases) | `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` | VERIFIED-SOURCE |
| Soft shadows on Windows | `#pragma multi_compile_fragment _ _SHADOWS_SOFT`. Use the full 5-way Lit line only if XR-mobile is ever a target. | VERIFIED-SOURCE |
| Additional lights, Forward+ | `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP` | VERIFIED-SOURCE/DOCS |
| Additional lights, plain Forward (mobile renderer, per-pixel) | `#pragma multi_compile _ _ADDITIONAL_LIGHTS`. Add `_ADDITIONAL_LIGHTS_VERTEX` only if you implement vertex lights. | VERIFIED-SOURCE |
| Additional light shadows (point/spot) | `#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS` | VERIFIED-SOURCE |
| No SSAO / decals / cookies / lightmaps / SH / probe volumes / reflection probes / SSR / screen-space irradiance / URP fog / light layers / debug display | Omit those lines. `Fog.hlsl` and `ProbeVolumeVariants.hlsl` are pulled in only through `#include_with_pragmas`, so do not include them. | VERIFIED-SOURCE |

Omitting a keyword is safe because every consumer is guarded:
- Fog: `MixFogColor` is a no-op when the `FOG_*_KEYWORD_DECLARED` macros are absent (`ShaderVariablesFunctions.hlsl:463-492`).
- SSR: `GetScreenSpaceReflection` returns 0 when `_SCREEN_SPACE_REFLECTION_KEYWORD_DECLARED` is absent (`SampleScreenSpaceReflection.hlsl:31-39`).
- SSAO: `GetScreenSpaceAmbientOcclusion` returns 1 (`AmbientOcclusion.hlsl:28-39`).
- Cookies: guarded by `#if defined(_LIGHT_COOKIES)` (`RealtimeLights.hlsl:133,270`).

The `if (KEYWORD)` style used inside URP for `multi_compile` keywords is resolved at compile time per variant (https://docs.unity3d.com/2023.2/Documentation/Manual/SL-MultipleProgramVariants.html, VERIFIED-DOCS).

---

## 2. Light loop, InputData, main light, shadow coordinates

### `LIGHT_LOOP_BEGIN` / `LIGHT_LOOP_END` (`U/ShaderLibrary/RealtimeLights.hlsl:34-47`, VERIFIED-SOURCE)

```hlsl
#if USE_CLUSTER_LIGHT_LOOP
  #define LIGHT_LOOP_BEGIN(lightCount) { uint lightIndex; \
     ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0); \
     [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) { lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT; CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
  #define LIGHT_LOOP_END } }
#else
  #define LIGHT_LOOP_BEGIN(lightCount) UNROLL_ONELIGHT for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex) {
  #define LIGHT_LOOP_END }
#endif
```

- **A local variable named exactly `inputData` of type `InputData` must be in scope.** The macro reads `inputData.normalizedScreenSpaceUV` and `inputData.positionWS`. The docs say the same (VERIFIED-DOCS, https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus).
- `GetAdditionalLightsCount()` returns **0 under Forward+** (`RealtimeLights.hlsl:292-303`). The count argument is ignored by the cluster iterator.
- **Non-main directional lights are not in the clusters.** They sit first in the light buffer, so they need an extra loop before `LIGHT_LOOP_BEGIN`. Copy it from `U/ShaderLibrary/Lighting.hlsl:356-392`:
  `[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) { CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK ... }`
  `URP_FP_DIRECTIONAL_LIGHTS_COUNT` is `_FPParams0.w` (`Input.hlsl:154`). The main light is excluded from that count (`ForwardLights.cs:312-314`).
- `normalizedScreenSpaceUV` **must** be `GetNormalizedScreenSpaceUV(input.positionCS)` computed from `SV_POSITION` in the fragment shader (`ShaderVariablesFunctions.hlsl:574-584`, which uses `_ScaledScreenParams`). A wrong UV picks the wrong tile and lights pop per tile. `positionWS` drives the z-bin (`Clustering.hlsl:52-53`).

### `InputData` (`U/ShaderLibrary/Input.hlsl:43-94`, VERIFIED-SOURCE)

Fields: `float3 positionWS; float4 positionCS; float3 normalWS; half3 viewDirectionWS; float4 shadowCoord; half fogCoord; half3 vertexLighting; half3 bakedGI; float2 normalizedScreenSpaceUV; half4 shadowMask; half3x3 tangentToWorld;` plus debug-only fields under `DEBUG_DISPLAY`.

Fields this template must fill:

| Field | Value |
|---|---|
| `positionWS` | displaced world position |
| `normalWS` | `NormalizeNormalPerPixel(...)` |
| `viewDirectionWS` | `GetWorldSpaceNormalizeViewDir(positionWS)` (`ShaderVariablesFunctions.hlsl:137-150`) |
| `shadowCoord` | see below |
| `normalizedScreenSpaceUV` | see above |
| `shadowMask` | `half4(1,1,1,1)`. Baked shadow is 1 without `LIGHTMAP_ON/LIGHTMAP_SHADOW_MIXING/SHADOWS_SHADOWMASK` (`Shadows.hlsl:57-59,491-495`). |
| `bakedGI` | our ambient |

Shadow coordinates in Lit, per pixel (`U/Shaders/LitForwardPass.hlsl:103-109`):

```hlsl
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)            inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else                                                  inputData.shadowCoord = float4(0,0,0,0);
```

- `MAIN_LIGHT_CALCULATE_SHADOWS` is defined when one of the `_MAIN_LIGHT_SHADOWS*` keywords is on and `_RECEIVE_SHADOWS_OFF` is off (`Shadows.hlsl:13-25`).
- Computing it per pixel is valid for all 3 main-shadow keywords: `TransformWorldToShadowCoord` handles cascade, single and screen-space (`Shadows.hlsl:360-374`).
- Signature: `float4 TransformWorldToShadowCoord(float3 positionWS)`.

### Main light and additional lights (`RealtimeLights.hlsl`, VERIFIED-SOURCE)

- `Light { half3 direction; half3 color; float distanceAttenuation; half shadowAttenuation; uint layerMask; }` (:12-19).
- `Light GetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)` (:128-139).
  - Use this overload. It calls `MainLightShadow()`, which includes the **shadow-distance fade** (`Shadows.hlsl:486-504`).
  - Pitfall: `GetMainLight(float4 shadowCoord)` (:121-126) uses `MainLightRealtimeShadow` only. It has **no fade**, so shadows cut off hard at the shadow distance.
- In Forward+, `GetMainLight()` uses `distanceAttenuation = 1` (:104-109). In Forward it uses `unity_LightData.z` (the per-object culling-mask result).
- `Light GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)` (:255-276) includes `AdditionalLightShadow`, which is active only with `_ADDITIONAL_LIGHT_SHADOWS`. In cluster mode `i` is the global light index.

### `UniversalFragmentPBR` (`U/ShaderLibrary/Lighting.hlsl:302-404`, VERIFIED-SOURCE)

- Signature: `half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)`.
- Order of work:
  1. `InitializeBRDFData`
  2. `CreateClearCoatBRDFData`
  3. `CalculateShadowMask`
  4. `CreateAmbientOcclusionFactor`
  5. `GetMainLight(inputData, shadowMask, aoFactor)`
  6. `MixRealtimeAndBakedGI`
  7. `GlobalIllumination(...)` (probe/skybox reflections + `bakedGI`)
  8. main light `LightingPhysicallyBased`
  9. Forward+ directional loop + `LIGHT_LOOP`
  10. vertex lights
  11. `CalculateFinalColor` (adds emission)
- `SurfaceData` (`U/ShaderLibrary/SurfaceData.hlsl:5-17`): `half3 albedo, specular; half metallic, smoothness; half3 normalTS, emission; half occlusion, alpha, clearCoatMask, clearCoatSmoothness;`.

### `LightingPhysicallyBased` overloads (`Lighting.hlsl:48-139`)

- Main overload: `half3 LightingPhysicallyBased(BRDFData, BRDFData brdfDataClearCoat, Light light, half3 normalWS, half3 viewDirectionWS, half clearCoatMask, bool specularHighlightsOff)` (:98-101).
- **SURPRISE (VERIFIED-SOURCE `Lighting.hlsl:131-139`):** the overload `LightingPhysicallyBased(BRDFData, half3 lightColor, half3 lightDirectionWS, float lightAttenuation, half3 normalWS, half3 viewDirectionWS, bool specularHighlightsOff)` is **buggy**. It forwards `(light, viewDirectionWS, specularHighlightsOff, specularHighlightsOff)`, which passes the view direction as the normal. **Never call it.** Use the `Light` + clear-coat overload above.

---

## 3. ShadowCaster, DepthOnly, DepthNormals

### ShadowCaster (`U/Shaders/ShadowCasterPass.hlsl:13-47`, `Lit.shader:193-241`, VERIFIED-SOURCE)

- Declare the globals yourself, outside any CBUFFER: `float3 _LightDirection; float3 _LightPosition;` (:13-14).
  - URP sets them in `ShadowUtils.SetupShadowCasterConstantBuffer` (`U/Runtime/ShadowUtils.cs:492-518`): `_LightDirection = -light.forward`, `_LightPosition = light position`, `_ShadowBias = (depthBias, normalBias, lightType, 0)`.
- Keyword: `#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW` (`Lit.shader:234`). It is on for spot/point light shadow maps and selects `normalize(_LightPosition - positionWS)` instead of `_LightDirection`.
- `float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)` (`Shadows.hlsl:542-551`) offsets by `lightDirection * _ShadowBias.x` plus `normalWS * (1 - saturate(N·L)) * _ShadowBias.y` (the normal bias is negative, an inset).
- `float4 ApplyShadowClamping(float4 positionCS)` (`Shadows.hlsl:553-569`):
  - Under `UNITY_REVERSED_Z`: `min(z, w * UNITY_NEAR_CLIP_VALUE)`, otherwise `max(...)`.
  - It is applied only for directional lights through `IsDirectionalLight()`, which reads `_ShadowBias.z == 1` (`Shadows.hlsl:114-133`). This is shadow pancaking.
- Render state: `ZWrite On`, `ZTest LEqual`, `ColorMask 0`, `Cull[_Cull]`. URP adds `SetGlobalDepthBias(1.0, 2.5)` while drawing slices (`ShadowUtils.cs:250-285`), so no `Offset` is needed.
- The main-light shadow pass sets `_WorldSpaceCameraPos` to the real camera (`U/Runtime/Passes/MainLightShadowCasterPass.cs:305`). It is UNVERIFIED for the additional-light pass. The view/projection matrices are the light's (`ShadowUtils.cs:262,279`).
  - **Rule: vertex displacement must not depend on `UNITY_MATRIX_V`/`VP` or `_WorldSpaceCameraPos`.** Use your own global, e.g. `_Ring_CameraPosWS` set from C#.

### DepthOnly (`U/Shaders/DepthOnlyPass.hlsl:37-80`, `Lit.shader:334-376`, VERIFIED-SOURCE)

- Tag `"LightMode"="DepthOnly"`, `ZWrite On`, `ColorMask R`.
- Fragment: `half DepthOnlyFragment(...) : SV_TARGET { return input.positionCS.z; }`.
- Lit uses `TransformObjectToHClip` (:47). Ours uses the displaced world-space path. It must be the same code as ForwardLit.

### DepthNormals (`U/Shaders/LitDepthNormalsPass.hlsl:57-161`, `Lit.shader:379-431`, VERIFIED-SOURCE)

- Tag `"LightMode"="DepthNormals"`. URP also accepts `"DepthNormalsOnly"` (`U/Runtime/Passes/DepthNormalOnlyPass.cs:19-20`).
- `ZWrite On`, no ColorMask.
- Output: `half4(NormalizeNormalPerPixel(normalWS), 0.0)`, a raw world-space normal (:151).
  - `.a` holds smoothness only under `_WRITE_SMOOTHNESS` (for SSR).
  - `_GBUFFER_NORMALS_OCT` is deferred-only (`DeferredLights.cs:368,472`).
  - `_WRITE_RENDERING_LAYERS` is needed only for the rendering-layers texture (decal layers).
  - The minimal pass can omit all three.

### Displacement consistency across passes

- Use one shared function in every pass: `positionWS = TransformObjectToWorld(posOS)` → `Displace(positionWS, normalWS)` → `TransformWorldToHClip(positionWS)`.
- `TransformObjectToHClip(p)` is `mul(VP, mul(M, float4(p,1)))` and `TransformWorldToHClip(ws)` is `mul(VP, float4(ws,1))`; they are mathematically identical (`C/ShaderLibrary/SpaceTransforms.hlsl:79-86,108-118`).
- Depth priming is off here (`PC_Renderer.asset:57`). If it is ever enabled, ForwardLit runs with `ZTest Equal`, so the vertex math must be textually identical. A shared include guarantees that.

---

## 4. Own ambient and reflection instead of SH / probes / skybox

How URP feeds GI (VERIFIED-SOURCE):
- **Ambient enters only through `inputData.bakedGI`.** Without lightmaps it comes from SH `unity_SHAr..unity_SHC` (`AmbientProbe.hlsl:45-58`, `GlobalIllumination.hlsl:46-75`, `LitForwardPass.hlsl:134-153`).
- `unity_AmbientSky/Equator/Ground` are declared in `UnityInput.hlsl:221-223`. They are **overwritten every camera** from `RenderSettings` (`U/Runtime/UniversalRenderPipeline.cs:2471-2491`), and Lit never reads them.
  - With "Gradient" ambient the engine projects the 3 colours into SH (UNVERIFIED engine detail).
  - **Do not SetGlobal `unity_Ambient*`.** Use your own names.
- Reflections: `GlossyEnvironmentReflection(...)` (`GlobalIllumination.hlsl:423-473`). With `_ENVIRONMENTREFLECTIONS_OFF` it returns `_GlossyEnvironmentColor.rgb`.
  - URP sets `_GlossyEnvironmentColor` every camera to the ambient-probe L0 × `RenderSettings.reflectionIntensity` (`UniversalRenderPipeline.cs:2475-2479`), so it is **not ours**.
- `GlobalIllumination()` (:526-568) computes:
  - `reflectVector = reflect(-V, N)`
  - `fresnelTerm = Pow4(1 - saturate(N·V))`
  - `EnvironmentBRDF(brdf, bakedGI, indirectSpecular, fresnelTerm) * occlusion`
  - `EnvironmentBRDF` (`BRDF.hlsl:157-172`) is `indirectDiffuse * brdf.diffuse + indirectSpecular * surfaceReduction * lerp(brdf.specular, brdf.grazingTerm, fresnel)`, with `surfaceReduction = 1/(roughness² + 1)`.

**Recommended (used in the template): mirror `UniversalFragmentPBR` and replace only the GI term.**
- Steps: `InitializeBRDFData(surfaceData, brdfData)` (`BRDF.hlsl:99-102`), then `GetMainLight(shadowCoord, positionWS, shadowMask)`, then our `EnvironmentBRDF(brdf, MyAmbient(N), MyReflection(R, perceptualRoughness), fresnel) * occlusion`, then `LightingPhysicallyBased` for the main light, the Forward+ directional loop and `LIGHT_LOOP`, then `+ emission`.
- This gives a BRDF and direct lighting that are numerically identical to Lit, with fully custom ambient and reflection.
- Also `#define _ENVIRONMENTREFLECTIONS_OFF 1` before the includes. This makes `CLUSTER_HAS_REFLECTION_PROBES` 0 (`Clustering.hlsl:9`), so the cluster iterator skips reflection-probe bookkeeping.

Alternative (hack, UNVERIFIED): `#define _GlossyEnvironmentColor _Ring_ReflectionColor` before the includes, then call `UniversalFragmentPBR` with `bakedGI = MyAmbient` and `_ENVIRONMENTREFLECTIONS_OFF`. It gives only a constant reflection colour and depends on the token renaming the `Input.hlsl:100` declaration. Not recommended.

Trilight ambient (own formula, UNVERIFIED as a "Unity match"):
- `u = dot(N, up)`, `sky = saturate(u)`, `ground = saturate(-u)`, `equator = 1 - sky - ground`.
- On a ringworld, "up" is radial toward the ring axis, not `+Y`. Keep it behind a hook.

---

## 5. SRP Batcher, instancing, DOTS

- SRP Batcher rules (VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials):
  - All built-in engine properties go in a single `UnityPerDraw` cbuffer. Including `Core.hlsl` gives you that (`UnityInput.hlsl:110-175`); never redeclare `unity_ObjectToWorld`.
  - All material properties go in **one** `CBUFFER_START(UnityPerMaterial)`.
  - **The layout must be identical in every pass.** Lit puts it in `LitInput.hlsl:16-35` and includes it in every pass. The note in `LitInput.hlsl:16` reads "Do not ifdef the properties here as SRP batcher can not handle different layouts."
  - Textures and samplers go outside the cbuffer.
  - Global uniforms outside any cbuffer (`_Ring_*`) are fine. URP's own `_MainLightPosition` etc. are globals (`Input.hlsl:117-121`).
  - Render-state-only properties such as `_Cull` need no HLSL declaration.
  - MaterialPropertyBlocks break the SRP Batcher for that renderer (VERIFIED-DOCS, same page).
- `#pragma multi_compile_instancing` with `UNITY_VERTEX_INPUT_INSTANCE_ID` / `UNITY_SETUP_INSTANCE_ID` / `UNITY_TRANSFER_INSTANCE_ID`:
  - Required for `Graphics.RenderMeshInstanced` and "Enable GPU Instancing". The SRP Batcher takes priority otherwise.
  - It is cheap, so keep it. `instancing_options renderinglayer` is optional (per-instance rendering layer).
- `#include_with_pragmas ".../DOTS.hlsl"` adds `multi_compile _ DOTS_INSTANCING_ON` plus `target 4.5 DOTS_INSTANCING_ON` (`U/ShaderLibrary/DOTS.hlsl:4-11`).
  - **Safe to omit.** Renderers then skip the GPU Resident Drawer and use the SRP Batcher (section 0).
  - If you add it, the DOTS-instanced material props need `UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)` (`LitInput.hlsl:40-56`), and `UNITY_SETUP_INSTANCE_ID` is mandatory in every stage.
- **Build-stripping pitfalls for a fully procedural project (UNVERIFIED but standard Unity behaviour):**
  - `shader_feature(_local)` keywords are compiled only if a material in the build uses them. Use `multi_compile` or `#define` for features toggled on runtime-created materials.
  - `Shader.Find("Megastructure/TemplateLit")` in a player needs the shader to be referenced by an asset, listed in Always Included Shaders, or placed under `Resources/`.

---

## 6. TEMPLATE — two files (mirrors Lit's `LitInput.hlsl` + per-pass pragmas)

Why two files: Lit declares the pass pragmas first and then includes the shared input file in each pass (`Lit.shader:188,238,373,428`). A relative `#include` next to the file works; `LitForwardPass.hlsl:4` does `#include "LitInput.hlsl"`.
- Putting `Core.hlsl` + the CBUFFER into an `HLSLINCLUDE` block is the common alternative (Cyanilux, VERIFIED-DOCS https://www.cyanilux.com/tutorials/urp-shader-code/).
- In that alternative, the include is processed textually before the pass `#pragma` lines. Whether keyword macros are visible at that point is UNVERIFIED (keywords are normally passed as compiler defines). The two-file layout avoids the question entirely.

### File A: `Assets/Shaders/Megastructure/TemplateLitCommon.hlsl`

```hlsl
#ifndef MEGASTRUCTURE_TEMPLATE_LIT_COMMON_INCLUDED
#define MEGASTRUCTURE_TEMPLATE_LIT_COMMON_INCLUDED

// We supply our own environment reflection => make URP headers skip probe/SSR paths.
// (GlobalIllumination.hlsl:426,439; Clustering.hlsl:9 CLUSTER_HAS_REFLECTION_PROBES)
#define _ENVIRONMENTREFLECTIONS_OFF 1
#define _SCREENSPACEREFLECTIONS_OFF 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// ---------------------------------------------------------------------------------
// SRP Batcher: exactly ONE UnityPerMaterial, identical in every pass (never #ifdef it).
// ---------------------------------------------------------------------------------
CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _ReflectionTint;
    half  _Metallic;
    half  _Smoothness;
    half  _Occlusion;
CBUFFER_END

// ---------------------------------------------------------------------------------
// Global uniforms (outside any cbuffer). Set from C# with Shader.SetGlobalColor(id, c.linear)
// (SetGlobalColor does NOT convert to linear; project is Linear colour space).
// ---------------------------------------------------------------------------------
float4 _Ring_AmbientSky;        // rgb linear (HDR ok)
float4 _Ring_AmbientEquator;    // rgb linear
float4 _Ring_AmbientGround;     // rgb linear
float4 _Ring_ReflectionColor;   // rgb linear: our "environment" reflection colour
// float4 _Ring_CameraPosWS;    // use THIS (not _WorldSpaceCameraPos) if displacement is camera-relative

// ---------------------------------------------------------------------------------
// HOOK 1: world-space vertex displacement. Called in ForwardLit, ShadowCaster, DepthOnly,
// DepthNormals with identical inputs. Only depend on positionWS/normalWS, material and
// global uniforms. NOT on UNITY_MATRIX_V/VP or _WorldSpaceCameraPos (shadow passes use light matrices).
// If it moves geometry outside mesh bounds, enlarge Renderer/mesh bounds or culling & shadow
// caster culling will drop the object.
// ---------------------------------------------------------------------------------
void Megastructure_DisplaceWS(inout float3 positionWS, inout float3 normalWS)
{
    // placeholder: identity
}

void Megastructure_ObjectToWorldDisplaced(float3 positionOS, float3 normalOS,
                                          out float3 positionWS, out float3 normalWS)
{
    positionWS = TransformObjectToWorld(positionOS);          // SpaceTransforms.hlsl:79
    normalWS   = TransformObjectToWorldNormal(normalOS);      // SpaceTransforms.hlsl:199 (normalized)
    Megastructure_DisplaceWS(positionWS, normalWS);
    normalWS   = normalize(normalWS);
}

// ---------------------------------------------------------------------------------
// HOOK 2: local "up" for ambient (ringworld: radial direction toward the ring axis).
// ---------------------------------------------------------------------------------
half3 Megastructure_UpWS(float3 positionWS)
{
    return half3(0.0h, 1.0h, 0.0h); // placeholder
}

// HOOK 3: own ambient (3-colour trilight), replaces SH/bakedGI.
half3 Megastructure_Ambient(half3 normalWS, float3 positionWS)
{
    half u       = dot(normalWS, Megastructure_UpWS(positionWS));
    half sky     = saturate(u);
    half ground  = saturate(-u);
    half equator = 1.0h - sky - ground;
    return _Ring_AmbientSky.rgb * sky + _Ring_AmbientEquator.rgb * equator + _Ring_AmbientGround.rgb * ground;
}

// HOOK 4: own environment reflection (replaces reflection probes / skybox).
half3 Megastructure_Reflection(half3 reflectVectorWS, half perceptualRoughness, float3 positionWS)
{
    return _Ring_ReflectionColor.rgb * _ReflectionTint.rgb; // placeholder (could be directional/rough-dependent)
}

// HOOK 5: own fog, applied to the final colour (Lit applies MixFog at the same point: LitForwardPass.hlsl:263-264).
half3 Megastructure_ApplyFog(half3 color, float3 positionWS, float2 normalizedScreenSpaceUV)
{
    return color; // placeholder
}

#endif
```

### File B: `Assets/Shaders/Megastructure/TemplateLit.shader`

```hlsl
Shader "Megastructure/TemplateLit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Occlusion("Occlusion", Range(0, 1)) = 1
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        _ReflectionTint("Reflection Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }
        LOD 300

        // ------------------------------------------------------------------ ForwardLit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MegaForwardVert
            #pragma fragment MegaForwardFrag

            // URP keywords (subset of Lit.shader:144-158)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT   // Windows: only _SHADOWS_SOFT is ever set (ShadowUtils.cs:662-713)
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP      // Forward+ (NOT _FORWARD_PLUS)
            #pragma multi_compile_instancing

            #include "TemplateLitCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;   // float, not half: large world coords
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MegaForwardVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS, normalWS;
                Megastructure_ObjectToWorldDisplaced(input.positionOS.xyz, input.normalOS, positionWS, normalWS);

                output.positionWS = positionWS;
                output.normalWS   = normalWS;
                output.uv         = input.texcoord;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            // Mirrors UniversalFragmentPBR (Lighting.hlsl:302-404) with GI replaced by our hooks.
            half3 Megastructure_FragmentPBR(InputData inputData, SurfaceData surfaceData)
            {
                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);                  // BRDF.hlsl:99
                const BRDFData noClearCoat = (BRDFData)0;
                const half4 shadowMask = half4(1, 1, 1, 1);
                const bool specularHighlightsOff = false;

                // Main light: cascades + soft + distance fade (RealtimeLights.hlsl:128, Shadows.hlsl:486)
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);

                // Own GI (same math as GlobalIllumination.hlsl:540-548 / EnvironmentBRDF BRDF.hlsl:167)
                half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
                half  NoV           = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half  fresnelTerm   = Pow4(1.0h - NoV);
                half3 indirectSpecular = Megastructure_Reflection(reflectVector, brdfData.perceptualRoughness, inputData.positionWS);
                half3 color = EnvironmentBRDF(brdfData, inputData.bakedGI, indirectSpecular, fresnelTerm) * surfaceData.occlusion;

                color += LightingPhysicallyBased(brdfData, noClearCoat, mainLight,
                                                 inputData.normalWS, inputData.viewDirectionWS, 0.0h, specularHighlightsOff);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();   // 0 in Forward+ (unused there)

                #if USE_CLUSTER_LIGHT_LOOP
                // Non-main directional lights are not in clusters (Lighting.hlsl:359-375)
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light,
                                                     inputData.normalWS, inputData.viewDirectionWS, 0.0h, specularHighlightsOff);
                }
                #endif

                // Point/spot (Forward+: clustered; Forward: per-object list). Needs 'inputData' in scope.
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light,
                                                     inputData.normalWS, inputData.viewDirectionWS, 0.0h, specularHighlightsOff);
                LIGHT_LOOP_END
            #endif

                color += surfaceData.emission;
            #if REAL_IS_HALF
                color = min(color, HALF_MAX);                       // same guard as Lighting.hlsl:398-400
            #endif
                return color;
            }

            half4 MegaForwardFrag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ---- Surface (replace with procedural material) ----
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo              = _BaseColor.rgb;
                surfaceData.alpha               = 1.0h;
                surfaceData.metallic            = _Metallic;
                surfaceData.specular            = half3(0, 0, 0);   // metallic workflow (no _SPECULAR_SETUP)
                surfaceData.smoothness          = _Smoothness;
                surfaceData.occlusion           = _Occlusion;
                surfaceData.emission            = _EmissionColor.rgb;
                surfaceData.normalTS            = half3(0, 0, 1);
                surfaceData.clearCoatMask       = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                // ---- InputData: variable MUST be named 'inputData' (LIGHT_LOOP_BEGIN, RealtimeLights.hlsl:37) ----
                InputData inputData = (InputData)0;
                inputData.positionWS              = input.positionWS;
                inputData.positionCS              = input.positionCS;
                inputData.normalWS                = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord             = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord             = float4(0, 0, 0, 0);
            #endif
                inputData.shadowMask              = half4(1, 1, 1, 1);
                inputData.bakedGI                 = Megastructure_Ambient(inputData.normalWS, input.positionWS);

                half3 color = Megastructure_FragmentPBR(inputData, surfaceData);
                color = Megastructure_ApplyFog(color, input.positionWS, inputData.normalizedScreenSpaceUV);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ShadowCaster
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
            #pragma vertex MegaShadowVert
            #pragma fragment MegaShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW   // Lit.shader:234
            #pragma multi_compile_instancing

            #include "TemplateLitCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by ShadowUtils.SetupShadowCasterConstantBuffer (ShadowCasterPass.hlsl:13-14)
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings MegaShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS, normalWS;
                Megastructure_ObjectToWorldDisplaced(input.positionOS.xyz, input.normalOS, positionWS, normalWS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)); // Shadows.hlsl:542
                output.positionCS = ApplyShadowClamping(positionCS);                                                // Shadows.hlsl:553
                return output;
            }

            half4 MegaShadowFrag(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthOnly
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MegaDepthOnlyVert
            #pragma fragment MegaDepthOnlyFrag
            #pragma multi_compile_instancing

            #include "TemplateLitCommon.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;     // hook may use it; keeps displacement identical
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings MegaDepthOnlyVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS, normalWS;
                Megastructure_ObjectToWorldDisplaced(input.positionOS.xyz, input.normalOS, positionWS, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half MegaDepthOnlyFrag(DepthVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;                      // DepthOnlyPass.hlsl:79
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthNormals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MegaDepthNormalsVert
            #pragma fragment MegaDepthNormalsFrag
            #pragma multi_compile_instancing

            #include "TemplateLitCommon.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings MegaDepthNormalsVert(DNAttributes input)
            {
                DNVaryings output = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS, normalWS;
                Megastructure_ObjectToWorldDisplaced(input.positionOS.xyz, input.normalOS, positionWS, normalWS);
                output.normalWS   = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            void MegaDepthNormalsFrag(DNVaryings input, out half4 outNormalWS : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);   // LitDepthNormalsPass.hlsl:151
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"   // same as Lit.shader:553
}
```

### C# side (globals)

```csharp
static readonly int kAmbSky   = Shader.PropertyToID("_Ring_AmbientSky");
static readonly int kAmbEq    = Shader.PropertyToID("_Ring_AmbientEquator");
static readonly int kAmbGnd   = Shader.PropertyToID("_Ring_AmbientGround");
static readonly int kReflCol  = Shader.PropertyToID("_Ring_ReflectionColor");
// Linear project + SetGlobalColor does no conversion -> pass .linear (HDR via * intensity)
Shader.SetGlobalColor(kAmbSky,  skySRGB.linear * skyIntensity);
Shader.SetGlobalColor(kAmbEq,   equatorSRGB.linear * eqIntensity);
Shader.SetGlobalColor(kAmbGnd,  groundSRGB.linear * groundIntensity);
Shader.SetGlobalColor(kReflCol, reflSRGB.linear * reflIntensity);
```

---

## 7. Pitfalls checklist

1. Forward+ without `_CLUSTER_LIGHT_LOOP` gives no point or spot lights. Without the extra `URP_FP_DIRECTIONAL_LIGHTS_COUNT` loop, extra directional lights are missing (VERIFIED-SOURCE).
2. The variable must be named `inputData`, and `normalizedScreenSpaceUV` must come from `SV_POSITION` (VERIFIED-SOURCE/DOCS).
3. `GetMainLight(shadowCoord)` has no shadow-distance fade. Use the 3-argument overload (VERIFIED-SOURCE).
4. The 7-argument `LightingPhysicallyBased(lightColor, lightDir, atten, N, V, bool)` overload is buggy (`Lighting.hlsl:131-139`).
5. SSAO is on with DepthNormals source, so a missing DepthNormals pass leaves holes in depth and normals (VERIFIED-SOURCE).
6. Displacement must match in all 4 passes. It must not use camera or view matrices (the shadow pass uses light matrices). Bounds must cover displaced geometry, otherwise frustum and shadow-caster culling drop it (bounds advice UNVERIFIED).
7. If the displaced normal changes, the shadow normal bias uses the displaced normal (template does). Normal maps would also need a displaced tangent (not in the minimal template).
8. With TAA or motion blur, static-in-world displacement is fine (camera motion vectors come from depth). Time-animated displacement needs a MotionVectors pass (`Lit.shader:509-523`, `ObjectMotionVectors.hlsl`). UNVERIFIED for the GRD path.
9. Use `color.linear` for `SetGlobalColor`. Do not write `unity_Ambient*` or `_GlossyEnvironmentColor`, because URP overwrites them per camera.
10. `#pragma target 4.5` is a choice for DX11/DX12/Vulkan Windows. Lit itself uses `target 2.0`.
11. Omit `Fog.hlsl`, so RenderSettings fog is ignored and our fog hook is the only fog (VERIFIED-SOURCE `ShaderVariablesFunctions.hlsl:463-492`).

---

## Sources (accessed 2026-10-29)

- `U/Shaders/Lit.shader`: pass list, exact keyword lines, render states.
- `U/Shaders/LitForwardPass.hlsl`: InputData init, shadowCoord, screen UV, fog placement.
- `U/Shaders/ShadowCasterPass.hlsl`, `U/Shaders/DepthOnlyPass.hlsl`, `U/Shaders/LitDepthNormalsPass.hlsl`: shadow, depth and normals passes.
- `U/Shaders/LitInput.hlsl`: UnityPerMaterial layout and DOTS props.
- `U/ShaderLibrary/Core.hlsl`, `ForwardPlusKeyword.deprecated.hlsl`: `_CLUSTER_LIGHT_LOOP` / `USE_CLUSTER_LIGHT_LOOP`.
- `U/ShaderLibrary/Input.hlsl`, `UnityInput.hlsl`: InputData struct, light globals, FP params, UnityPerDraw, `unity_Ambient*`.
- `U/ShaderLibrary/RealtimeLights.hlsl`, `Clustering.hlsl`: Light struct, LIGHT_LOOP, GetMainLight/GetAdditionalLight, cluster iterator.
- `U/ShaderLibrary/Lighting.hlsl`, `BRDF.hlsl`, `SurfaceData.hlsl`, `GlobalIllumination.hlsl`, `AmbientOcclusion.hlsl`, `SampleScreenSpaceReflection.hlsl`: PBR pipeline, GI, buggy overload.
- `U/ShaderLibrary/Shadows.hlsl`: shadow keywords, soft-shadow sampling, `TransformWorldToShadowCoord`, `ApplyShadowBias`, `ApplyShadowClamping`.
- `U/ShaderLibrary/ShaderVariablesFunctions.hlsl`, `Fog.hlsl`, `DOTS.hlsl`, `RenderingLayers.hlsl`: helpers, fog no-op, DOTS and rendering-layer pragmas.
- `C/ShaderLibrary/SpaceTransforms.hlsl`, `AmbientProbe.hlsl`, `FoveatedRenderingKeywords.hlsl`, `Common.hlsl`: transforms, SH, `Pow4`, `REAL_IS_HALF`.
- `C/Runtime/GPUDriven/SceneProcessors/WorldProcessor.cs`: GRD exclusion of non-DOTS shaders.
- `U/Runtime/ForwardLights.cs`, `ShadowUtils.cs`, `Passes/MainLightShadowCasterPass.cs`, `Passes/DepthNormalOnlyPass.cs`, `UniversalRenderPipeline.cs`, `UniversalRenderer.cs`, `RendererFeatures/ScreenSpaceAmbientOcclusion.cs`, `DeferredLights.cs`: which keywords and globals URP sets at runtime.
- `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/Runtime/ShaderConfig.cs.hlsl`: light count limits, fog keyword mode.
- Project files `Assets/Settings/PC_Renderer.asset`, `PC_RPAsset.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/ProjectSettings.asset`: actual renderer settings.
- https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus (6000.6 manual): Forward+ light loop, `_CLUSTER_LIGHT_LOOP`, inputData requirement, directional loop.
- https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/shader-keywords-macros (6000.6 manual): `_CLUSTER_LIGHT_LOOP` and `LIGHT_LOOP_BEGIN` definitions.
- https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials: SRP Batcher compatibility rules.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/setglobalcolor: no colour-space conversion in SetGlobalColor.
- https://docs.unity3d.com/2023.2/Documentation/Manual/SL-MultipleProgramVariants.html: `if (KEYWORD)` with multi_compile and dynamic_branch.
- https://www.cyanilux.com/tutorials/urp-shader-code/: common HLSLINCLUDE + UnityPerMaterial pattern, pass structure.
