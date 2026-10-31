## URP 17.6 C# configuration and renderer features (research)

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
