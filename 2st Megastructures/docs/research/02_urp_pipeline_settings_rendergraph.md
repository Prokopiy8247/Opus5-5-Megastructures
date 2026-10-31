# 02 - URP 17.6 pipeline asset / renderer / camera settings from editor C#, plus a Render Graph full-screen pass

Scope: Unity 6000.6.0f1, URP 17.6.0, SRP Core 17.6.0. Ground truth is the package source in this project.

Path abbreviations:
- `URP/` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184/`
- `CORE/` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e/`

Tags: **VS** = VERIFIED-SOURCE (path:line), **VD** = VERIFIED-DOCS (URL), **UV** = UNVERIFIED.

---

## 0. Version facts that change how you write code (read first)

1. **Render Graph only. Compatibility Mode no longer exists.** `RenderGraphSettings.enableRenderCompatibilityMode => false` is hard-coded and marked obsolete "#from(6000.4)". Any `ScriptableRenderPass` must implement `RecordRenderGraph(RenderGraph, ContextContainer)`. `Execute()`/`OnCameraSetup()` are not called. **VS** `URP/Runtime/Settings/RenderGraphSettings.cs:41-66`
2. **`ENABLE_UPSCALER_FRAMEWORK` is NOT defined** in this project. The compiler response file only has `UNITY_6000_6_OR_NEWER` etc. So `upscalingFilter` (enum `UpscalingFilterSelection`) is the active API, and `upscalerName` returns `""`. **VS** `Library/Bee/artifacts/1900b0aE.dag/Unity.RenderPipelines.Universal.Runtime.rsp` (grep). Code paths: `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:1137-1158`
3. **`supportsDynamicBatching` is `[Obsolete(..., true)]`**, so using it is a compile error. Only the serialized field `m_SupportsDynamicBatching` remains, "retained for serialized data compatibility". **VS** `UniversalRenderPipelineAsset.cs:557-558, 1573-1578`
4. **Several setters are `internal`.** You must write them through `SerializedObject`: main/additional light rendering mode, main/additional shadows supported, soft shadows, soft shadow quality, LOD cross-fade, reflection probe toggles. See section 1.
5. **The existing project assets are not in a state you want.** `Assets/Settings/PC_RPAsset.asset` (quality level "PC", which is the Standalone default) has:
   - `m_GPUResidentDrawerMode: 1`, meaning GRD is **ON**. Must become 0.
   - `m_HDRColorBufferPrecision: 0` (32-bit).
   - `m_MSAA: 1` (off).
   - `m_VolumeProfile`, the quality volume profile, set to `SampleSceneProfile`. That profile carries Bloom, Vignette and Tonemapping(ACES) overrides, and they apply in every scene.

   `GraphicsSettings.m_CustomRenderPipeline` is `{fileID: 0}`, so there is no default RP. `PC_Renderer.asset` is already `m_RenderingMode: 2` (Forward+) with an SSAO feature. **VS** `Assets/Settings/PC_RPAsset.asset`, `ProjectSettings/GraphicsSettings.asset:40`, `Assets/Settings/PC_Renderer.asset:36-60`
6. **Post-processing variant stripping is ON in this project.** It is set by `m_StripUnusedPostProcessingVariants: 1` (`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset:22,29`). The build keeps `_BLOOM_LQ/_BLOOM_HQ`, `_TONEMAP_NEUTRAL/_ACES` and `_FILM_GRAIN` only if some **VolumeProfile asset under `Assets/`** has that component `IsActive()`. For example, Bloom needs `intensity > 0`, and the HQ or LQ variant follows the asset's `highQualityFiltering`. Tonemapping needs `mode != None`. A bloom that is created only at runtime, or that is authored at intensity 0 and animated up, is **missing in the built player only**. **VS** `URP/Editor/ShaderBuildPreprocessor.cs:302-305, 418-480`; `URP/Editor/ShaderScriptableStripper.cs:296-329`. The switch is `GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>().stripUnusedPostProcessingVariants` (get/set, editor). **VS** `URP/Runtime/Settings/URPShaderStrippingSetting.cs:57-85`

---

## 1. Settings: exact names

### 1a. `UniversalRenderPipelineAsset` (namespace `UnityEngine.Rendering.Universal`)
Source: `URP/Runtime/Data/UniversalRenderPipelineAsset.cs`. Serialized fields are at lines 431-628 and properties at lines 1043-1820. All entries are **VS**.

| Setting | Public API | Serialized field (for `SerializedObject`) | Notes |
|---|---|---|---|
| HDR on | `supportsHDR {get;set}` (1087) | `m_SupportsHDR` | Effective HDR = `camera.allowHDR && asset.supportsHDR` (`UniversalRenderPipeline.cs:1772`) |
| HDR precision 64-bit | `hdrColorBufferPrecision {get;set}` (1096), enum `HDRColorBufferPrecision._64Bits` (166-175) | `m_HDRColorBufferPrecision` (0=_32Bits, 1=_64Bits) | `_64Bits` gives `R16G16B16A16_SFloat`. `_32Bits` gives `B10G11R11_UFloatPack32` (blue/yellow banding risk). `UniversalRenderPipelineCore.cs:1477-1492` |
| MSAA 4x | `msaaSampleCount {get;set}` int (1106). Pass 4, which maps to `MsaaQuality._4x` | `m_MSAA` (1=Disabled, 2, 4, 8) | The pipeline constructor copies it to `QualitySettings.antiAliasing` and calls `Screen.SetMSAASamples` (`UniversalRenderPipeline.cs:368-376`). Also needs `camera.allowMSAA` (`UniversalRenderPipelineCore.cs:1548-1565`) |
| Render scale | `renderScale {get;set}` (1115), clamped to [0.1, 3.0] (`UniversalRenderPipeline.cs:188-199`) | `m_RenderScale` | Any value within 0.05 of 1 is treated as 1 (`UniversalRenderPipeline.cs:1789-1791`) |
| Upscaling filter | `upscalingFilter` (`UpscalingFilterSelection.Auto/Linear/Point/FSR/STP`) | `m_UpscalingFilter` | Active API, since the upscaler framework is off |
| Shadow distance | `shadowDistance {get;set}` (1472) | `m_ShadowDistance` | URP uses `min(asset.shadowDistance, camera.farClipPlane)`. `QualitySettings.shadowDistance` is ignored (`UniversalRenderPipeline.cs:1865`) |
| Cascades | `shadowCascadeCount {get;set}` (1481), **throws** outside 1..4 | `m_ShadowCascadeCount` | |
| Splits | `cascade2Split` float, `cascade3Split` Vector2, `cascade4Split` Vector3 (1498-1522); `cascadeBorder` (1527) | `m_Cascade2Split`, `m_Cascade3Split`, `m_Cascade4Split`, `m_CascadeBorder` | Splits are fractions of `shadowDistance` |
| Bias | `shadowDepthBias`, `shadowNormalBias` (1536-1549), clamped to [0, 10] | `m_ShadowDepthBias`, `m_ShadowNormalBias` | |
| Main light shadowmap 4096 | `mainLightShadowmapResolution {get;set}` int (1329). Pass 4096 (`ShadowResolution._4096`) | `m_MainLightShadowmapResolution` | |
| Main light shadows on | `supportsMainLightShadows` (**internal set**, 1314) | `m_MainLightShadowsSupported` **and** editor-only `m_AnyShadowsSupported` (541) | `m_AnyShadowsSupported` drives `_SHADOWS_SOFT` stripping. Set both |
| Main light per-pixel | `mainLightRenderingMode` (**internal set**, 1305) | `m_MainLightRenderingMode`: write `.intValue = 1` (PerPixel=1, PerVertex=2, Disabled=0; values are not sequential) | |
| Additional lights per-pixel | `additionalLightsRenderingMode` (**internal set**, 1339) | `m_AdditionalLightsRenderingMode`: `.intValue = 1` | |
| Additional light shadows | `supportsAdditionalLightShadows` (**internal set**) | `m_AdditionalLightShadowsSupported` | |
| Per-object light limit | `maxAdditionalLightsCount {get;set}` (1348), clamped to 0..8 | `m_AdditionalLightsPerObjectLimit` | Ignored by Forward+ |
| Soft shadows | `supportsSoftShadows` (**internal set**, 1554) | `m_SoftShadowsSupported` | If false, `_SHADOWS_SOFT` is stripped (540-546). Each light also needs `Light.shadows = LightShadows.Soft` |
| Soft shadow quality | `softShadowQuality` (**internal** property, 1563) | `m_SoftShadowQuality`: enum `SoftShadowQuality` UsePipelineSettings=0, Low=1, Medium=2, **High=3** (41-61) | Per light: `UniversalAdditionalLightData.softShadowQuality` (`UniversalAdditionalLightData.cs:165`), default UsePipelineSettings |
| Depth texture | `supportsCameraDepthTexture {get;set}` (1046) | `m_RequireDepthTexture` | Each camera can override |
| Opaque texture | `supportsCameraOpaqueTexture {get;set}` (1055); `opaqueDownsampling` get-only | `m_RequireOpaqueTexture`, `m_OpaqueDownsampling` | |
| SRP Batcher | `useSRPBatcher {get;set}` (1625) | `m_UseSRPBatcher` | Applied in the pipeline constructor through `GraphicsSettings.useScriptableRenderPipelineBatching` (`UniversalRenderPipeline.cs:368`) |
| Dynamic batching | **obsolete with error** | `m_SupportsDynamicBatching` (558) | Leave it false |
| GPU Resident Drawer | `gpuResidentDrawerMode {get;set}` (1722), enum `UnityEngine.Rendering.GPUResidentDrawerMode {Disabled, InstancedDrawing}` (`CORE/Runtime/GPUDriven/IGPUResidentRenderPipeline.cs:9-19`) | `m_GPUResidentDrawerMode` (597) | Set to `Disabled`. The setter calls `OnValidate()`. GRD also forces reflection-probe blending on (1428-1435) |
| LOD cross-fade | `enableLODCrossFade` **get-only** (1124); `lodCrossFadeDitheringType` get-only | `m_EnableLODCrossFade` (474), `m_LODCrossFadeDitheringType` (`BayerMatrix=0, BlueNoise=1, Stencil=2`) | If false, `LOD_FADE_CROSSFADE` is stripped at build (471-474). Custom shaders need `#pragma multi_compile _ LOD_FADE_CROSSFADE` and `LODFadeCrossFade(positionCS)` from `Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl` (`URP/ShaderLibrary/LODCrossFade.hlsl:15`) |
| Color grading mode | `colorGradingMode {get;set}` (`LowDynamicRange` / `HighDynamicRange`), `colorGradingLutSize` (16..65) | `m_ColorGradingMode`, `m_ColorGradingLutSize` | |
| Fast sRGB approximation | get-only `useFastSRGBLinearConversion` | `m_UseFastSRGBLinearConversion` | Keep false |
| Quality volume profile | `volumeProfile {get;set}` (1615) | `m_VolumeProfile` | In this project it currently points at SampleSceneProfile. Set it to null or to your own profile |
| Volume update mode | get-only `volumeFrameworkUpdateMode` | `m_VolumeFrameworkUpdateMode` (EveryFrame=0, ViaScripting=1) | |
| Light layers / rendering layers | `useRenderingLayers` (internal set) | `m_SupportsLightLayers` | |
| Renderer list | `rendererDataList` (ReadOnlySpan) | `m_RendererDataList` (array), `m_DefaultRendererIndex` | |

### 1b. `UniversalRendererData` (renderer asset)
Source: `URP/Runtime/UniversalRendererData.cs`. Every public setter calls `SetDirty()`, so the renderer is rebuilt. All entries are **VS**.

- `renderingMode {get;set}` (237). Enum `RenderingMode { Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3 }` (`URP/Runtime/UniversalRenderer.cs:11-22`). Field `m_RenderingMode`.
- `depthPrimingMode {get;set}` (250). Enum `DepthPrimingMode { Disabled, Auto, Forced }` (`UniversalRenderer.cs:28-36`). Field `m_DepthPrimingMode`. Priming is **automatically off when MSAA > 1**, when deferred is used, and on WebGL (`URP/Runtime/UniversalRendererRenderGraph.cs:1605-1647`).
- `copyDepthMode {get;set}` (263). `CopyDepthMode { AfterOpaques, AfterTransparents, ForcePrepass }`.
- `depthAttachmentFormat {get;set}` (276) and `depthTextureFormat {get;set}` (305). Enum `DepthFormat { Default, Depth_16, Depth_24, Depth_32, Depth_16_Stencil_8, Depth_24_Stencil_8, Depth_32_Stencil_8 }` (59-102). Fields `m_DepthAttachmentFormat`, `m_DepthTextureFormat`.
  - **`Default` resolves through `CoreUtils.GetDefaultDepthStencilFormat()`.** That returns `D24_UNorm_S8_UInt` only on Switch, Switch2, EmbeddedLinux, QNX and Android. **On Windows DX11/DX12/Vulkan it is `GraphicsFormat.D32_SFloat_S8_UInt`.** **VS** `URP/Runtime/UniversalRenderer.cs:211-212`, `CORE/Runtime/Utilities/CoreUtils.cs:1944-1951`. Depth-only default: `D32_SFloat` (`CoreUtils.cs:1958-1965`).
- `intermediateTextureMode` (`IntermediateTextureMode.Auto/Always`), `tileOnlyMode` (keep false), `prepassLayerMask`, `opaqueLayerMask`, `transparentLayerMask`, `shadowTransparentReceive`.
- `public PostProcessData postProcessData` (134). **If it is null, URP post-processing is completely disabled for that renderer** (`postProcessEnabled = data.postProcessData != null`, `UniversalRenderer.cs:318`). `CreateInstance<UniversalRendererData>()` leaves it null.
- Read-only: `usesClusterLightLoop` (Forward+ or Deferred+), which GRD requires.

### 1c. Reversed Z
This is an engine/API property, not a URP setting. On D3D11, core sets `#define UNITY_REVERSED_Z 1`, `UNITY_NEAR_CLIP_VALUE (1.0)` and `UNITY_RAW_FAR_CLIP_VALUE (0.0)`. **VS** `CORE/ShaderLibrary/API/D3D11.hlsl:4-7`. C#: `SystemInfo.usesReversedZBuffer` (**VS**, `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml`). D3D12 shares the D3D11 shader API path: **UV**, because the docs list `SHADER_API_D3D11` for D3D11 only. Always branch on `UNITY_REVERSED_Z`, never on the API.

### 1d. Camera: `UniversalAdditionalCameraData`
Source: `URP/Runtime/UniversalAdditionalCameraData.cs`. Get it with `camera.GetUniversalAdditionalCameraData()`, which adds the component if it is missing (183). All entries are **VS**.

- `renderPostProcessing {get;set}` (838). **Default false** (field `m_RenderPostProcessing`, 501). A camera created by script shows no post-processing until this is set (`UniversalRenderPipeline.cs:1884`).
- `antialiasing {get;set}` (848). Enum `AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }` (43-68). There are no members named FXAA, SMAA or TAA.
- `antialiasingQuality` (`AntialiasingQuality { Low, Medium, High }`) for SMAA. `ref taaSettings` (`TemporalAA.Settings`: `quality`, `baseBlendFactor`, `jitterScale`, `mipBias`, `varianceClampScale`, `contrastAdaptiveSharpening`).
- **TAA is silently disabled when MSAA > 1**, and also with camera stacking, dynamic resolution, or post-processing off (`URP/Runtime/FrameData/UniversalCameraData.cs:459-469`).
- `dithering {get;set}` (919, `m_Dithering`, default false). `stopNaN` (910). `allowHDROutput` (964), which you should keep false for SDR video. `renderShadows`, `requiresDepthTexture` / `requiresColorTexture` (setters write `CameraOverrideOption.On/Off`, 666-703), `volumeLayerMask`, `volumeTrigger`, `renderType`, `cameraStack`.
- `UnityEngine.Camera` itself: `allowHDR`, `allowMSAA`, `allowDynamicResolution` (all three must agree with the asset).
- The camera has no depth-format fields. Depth formats live on the renderer data (1b).

---

## 2. Create assets and assign the pipeline from script (editor)

Facts:
- `public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null)` is `#if UNITY_EDITOR`. It runs `CreateInstance`, puts `rendererData` into `m_RendererDataList[0]`, sets `m_ConservativeEnclosingSphere = true`, and calls `ResourceReloader.ReloadAllNullIn`. **It does not create a sub-asset or a file. You must `AssetDatabase.CreateAsset` both objects yourself.** **VS** `UniversalRenderPipelineAsset.cs:693-707`
- The menu path sets `rendererData.postProcessData = PostProcessData.GetDefaultPostProcessData()`. That method is internal; the public equivalent is `AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset")`. **VS** `UniversalRenderPipelineAsset.cs:740-750`, `URP/Runtime/Data/PostProcessData.cs:41-45`
- `GraphicsSettings.defaultRenderPipeline` and `QualitySettings.renderPipeline` (current level only) both have setters. Core tests write both. **VS** `CORE/Editor/RenderGraph/RenderGraphTestsCore.cs:139-140`. Also available: `QualitySettings.count`, `SetQualityLevel(int,bool)`, `GetRenderPipelineAssetAt(int)`. **VS** CoreModule.xml. The per-level YAML key is `m_QualitySettings[i].customRenderPipeline` (**VS** `ProjectSettings/QualitySettings.asset:51,105`).
- To add a renderer feature, copy what the editor does: `CreateInstance` the feature, set `name`, OR in `HideFlags.HideInHierarchy`, `AssetDatabase.AddObjectToAsset(feature, rendererData)`, `TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId)`, append to `m_RendererFeatures`, append `localId` to `m_RendererFeatureMap`, then `ApplyModifiedProperties` and save. **VS** `URP/Editor/ScriptableRendererDataEditor.cs:270-307`. Both lists are `[SerializeField] internal` (`URP/Runtime/ScriptableRendererData.cs:40-41`). Public read access: `rendererFeatures` (50) and `TryGetRendererFeature<T>` (124). If the map size does not match, `ValidateRendererFeatures()` re-links by localId (149-216).
- `ScriptableRendererData.SetDirty()` is `public new void SetDirty()`. It only sets `isInvalidated`, which rebuilds the renderer. It does **not** mark the asset dirty for saving, so also call `EditorUtility.SetDirty`. **VS** `ScriptableRendererData.cs:59-62`
- Shader keyword prefiltering (`m_Prefiltering*` fields) is recomputed for every URP asset of the build target in the build preprocessor. You do not need to touch it. **VS** `URP/Editor/ShaderBuildPreprocessor.cs:541-597`

Skeleton (editor-only file):
```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RingUrpSetup
{
    const string Dir = "Assets/_Game/Settings/Rendering"; // folder must exist
    public static void Run(Material finalPassMat)
    {
        // Renderer data
        var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
        rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset"); // REQUIRED for post FX
        rd.renderingMode         = RenderingMode.ForwardPlus;
        rd.depthPrimingMode      = DepthPrimingMode.Disabled;   // ignored anyway with MSAA
        rd.copyDepthMode         = CopyDepthMode.AfterOpaques;
        rd.depthAttachmentFormat = DepthFormat.Depth_32_Stencil_8; // == Default on Windows
        rd.depthTextureFormat    = DepthFormat.Depth_32_Stencil_8;
        AssetDatabase.CreateAsset(rd, Dir + "/Ring_Renderer.asset");

        // Pipeline asset
        var rp = UniversalRenderPipelineAsset.Create(rd);
        AssetDatabase.CreateAsset(rp, Dir + "/Ring_URP.asset");
        rp.supportsHDR = true;
        rp.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
        rp.msaaSampleCount = 4;
        rp.renderScale = 1f;
        rp.shadowDistance = 300f;
        rp.shadowCascadeCount = 4;                       // throws outside 1..4
        rp.cascade4Split = new Vector3(0.04f, 0.12f, 0.35f);
        rp.cascadeBorder = 0.1f;
        rp.mainLightShadowmapResolution = 4096;
        rp.supportsCameraDepthTexture = true;
        rp.supportsCameraOpaqueTexture = false;
        rp.useSRPBatcher = true;
        rp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
        rp.volumeProfile = null;                          // or your quality-level profile

        var so = new SerializedObject(rp);                // internal setters -> serialized fields
        so.FindProperty("m_MainLightRenderingMode").intValue        = (int)LightRenderingMode.PerPixel;
        so.FindProperty("m_MainLightShadowsSupported").boolValue    = true;
        so.FindProperty("m_AnyShadowsSupported").boolValue          = true;  // editor-only, strip control
        so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        so.FindProperty("m_SoftShadowsSupported").boolValue         = true;
        so.FindProperty("m_SoftShadowQuality").intValue             = (int)SoftShadowQuality.High;
        so.FindProperty("m_SupportsDynamicBatching").boolValue      = false;
        so.FindProperty("m_EnableLODCrossFade").boolValue           = true;
        so.FindProperty("m_LODCrossFadeDitheringType").intValue     = (int)LODCrossFadeDitheringType.BlueNoise;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);

        // Full-screen pass feature (see section 3)
        var fs = AddFeature<FullScreenPassRendererFeature>(rd, "RingFinalPass");
        fs.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        fs.passMaterial = finalPassMat;   // must be a saved .mat asset (also pulls the shader into the build)
        fs.passIndex = 0;
        fs.fetchColorBuffer = true;
        fs.requirements = ScriptableRenderPassInput.None;
        fs.bindDepthStencilAttachment = false;
        EditorUtility.SetDirty(fs);

        // Assign everywhere (default + every quality level)
        GraphicsSettings.defaultRenderPipeline = rp;
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.count; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = rp; }
        QualitySettings.SetQualityLevel(cur, false);
        AssetDatabase.SaveAssets();
    }

    static T AddFeature<T>(UniversalRendererData rd, string name) where T : ScriptableRendererFeature
    {
        var f = ScriptableObject.CreateInstance<T>();
        f.name = name;
        f.hideFlags |= HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(f, rd);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out _, out long localId);
        var so = new SerializedObject(rd);
        var list = so.FindProperty("m_RendererFeatures");
        var map  = so.FindProperty("m_RendererFeatureMap");
        list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = f;
        map.arraySize++;  map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        rd.SetDirty();                 // rebuild renderer
        EditorUtility.SetDirty(rd);    // persist
        return f;
    }
}
#endif
```
Notes on the skeleton:
- For enum `SerializedProperty`, use `.intValue` to write the underlying value. `.enumValueIndex` is the declaration index, which is wrong for `LightRenderingMode` (PerPixel=1 is declared after PerVertex=2). **UV** (standard Unity behaviour)
- Alternative: edit `PC_RPAsset` / `PC_Renderer` in place the same way, and set `m_GPUResidentDrawerMode` to 0. Remember that the Mobile quality level uses a different asset.

---

## 3. `FullScreenPassRendererFeature` (Render Graph) and the full-screen shader

**VS** `URP/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs`
- `public enum InjectionPoint { BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents, BeforeRenderingPostProcessing = ..., AfterRenderingPostProcessing = ... }` (18-34). Event values: BeforeRenderingPostProcessing=550, AfterRenderingPostProcessing=600, AfterRendering=1000 (`URP/Runtime/Passes/ScriptableRenderPass.cs:153-163`).
- Public fields (39-68): `injectionPoint` (default AfterRenderingPostProcessing), `fetchColorBuffer` (default true), `requirements` (`ScriptableRenderPassInput` flags None=0, Depth=1, Normal=2, Color=4, Motion=8; Color means `_CameraOpaqueTexture`, not the current color), `passMaterial`, `passIndex`, `bindDepthStencilAttachment` (default false).
- The feature is **Render Graph native**: it only has `RecordRenderGraph`. With `fetchColorBuffer`, it does `renderGraph.AddBlitPass(cameraColor -> "_CameraColorFullScreenPass" copy)`. It then draws a raster pass into `resourcesData.activeColorTexture` with `cmd.DrawProcedural(identity, mat, passIndex, Triangles, 3)`. The MPB holds `_BlitTexture` (the copy) and `_BlitScaleBias`. `requiresIntermediateTexture = fetchColorBuffer` (125, 165-262).
- It skips Preview and Reflection cameras, and skips when `passIndex` is out of range (99-111).
- The MPB is cleared every frame and only sets `_BlitTexture` and `_BlitScaleBias`. Feed your own per-frame parameters with `Shader.SetGlobalFloat/Vector`. Do not use `passMaterial.SetX` at runtime: in the Editor that dirties the `.mat` asset on disk. **VS** for the MPB contents (153-163); the advice is a recommendation.
- Docs: "Fetch Color Buffer... binds it to `_BlitTexture`". The "Pass" index field is hidden behind Advanced Properties in the inspector. **VD** https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass

Shader-side facts. Source: `CORE/Runtime/Utilities/Blit.hlsl`, include path `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`. All **VS**.
- It declares `TEXTURE2D_X(_BlitTexture)` (or `_FLOAT` with `USE_FULL_PRECISION_BLIT_TEXTURE`). It also declares `float4 _BlitScaleBias, _BlitScaleBiasRt, _BlitTexture_TexelSize; float _BlitMipLevel;` (11-25).
- `struct Attributes { uint vertexID : SV_VertexID; ... }`, `struct Varyings { float4 positionCS : SV_POSITION; float2 texcoord : TEXCOORD0; ... }` (27-38).
- `Varyings Vert(Attributes)` is a full-screen triangle with `texcoord = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv)`, which is `uv * _BlitScaleBias.xy + _BlitScaleBias.zw` (40-53; `CORE/ShaderLibrary/DynamicScaling.hlsl:14`).
- Samplers come from `GlobalSamplers.hlsl`: `sampler_PointClamp`, `sampler_LinearClamp`, also `sampler_LinearRepeat` and `sampler_PointRepeat` (`CORE/ShaderLibrary/GlobalSamplers.hlsl:7-8`). Ready-made fragments: `FragNearest`, `FragBilinear`.
- The FullScreen pass does **not** set `_BlitTexture_TexelSize`. **UV** whether Unity auto-fills it from the MPB. Use `_ScreenSize` {w,h,1/w,1/h} (`URP/ShaderLibrary/UnityInput.hlsl:298`) or `_ScaledScreenParams` (`URP/ShaderLibrary/Input.hlsl:108`).

Minimal shader. The pattern matches `URP/Samples~/URPRenderGraphSamples/BlitWithMaterial/BlitWithMaterial.shader` and `URP/Shaders/Utils/Blit.shader`.
```hlsl
Shader "Ring/FinalPass"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "RingFinalPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"   // first (XR/globals)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _RingFinalParams; // set with Shader.SetGlobalVector
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                // c.rgb is LINEAR, already tonemapped (0..1) when injected AfterRenderingPostProcessing
                return c;
            }
            ENDHLSL
        }
    }
}
```

Custom pass alternative (Render Graph). The public utility is `renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, mat, pass), passName)` from `UnityEngine.Rendering.RenderGraphModule.Util`. **VS** `CORE/Runtime/RenderGraph/RenderGraphUtilsBlit.cs:528-560, 928`. It sets `_BlitTexture`, `_BlitScaleBias`, `_BlitMipLevel` and `_BlitTexArraySlice`. Inside your own raster pass, use `Blitter.BlitTexture(RasterCommandBuffer, RTHandle src, Vector4 scaleBias, Material, int pass)` (`CORE/Runtime/Utilities/Blitter.cs:793`) with `scaleBias = new Vector4(1,1,0,0)` between intermediates. Full sample: `URP/Samples~/URPRenderGraphSamples/BlitWithMaterial/BlitAndSwapColorRendererFeature.cs`. It sets `requiresIntermediateTexture = true`, uses `frameData.Get<UniversalResourceData>()`, `renderGraph.GetTextureDesc(resourceData.activeColorTexture)` and `CreateTexture`, then `AddBlitPass`, then `resourceData.cameraColor = destination`.

### 3b. What happens after `AfterRenderingPostProcessing`: dithering, quantisation and sRGB
All **VS**:
- If any pass sits at `AfterRenderingPostProcessing`, UberPost does **not** write to the backbuffer (`isTargetBackbuffer` requires `!hasPassesAfterPostProcessing`). It writes to `renderGraph.CreateTexture(sourceTexture)`, which has **the same format as the camera colour**, so `R16G16B16A16_SFloat` with 64-bit HDR. **VS** `UniversalRendererRenderGraph.cs:1404,1435-1456`; `URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs:122-148`
- **Camera dithering and film grain run inside UberPost, before your pass**, whenever there is no FinalPost pass. FinalPost runs only for FXAA, FSR1 upscaling, or TAA with CAS sharpening > 0 (`applyFinalPostProcessing`, `UniversalRendererRenderGraph.cs:1397-1400`; `m_IsFinalPass = !hasFinalPass`, `PostProcess.cs:208,239`; `UberPostProcessPass.cs:203-207`). With FXAA on, dithering moves to `FinalPostProcessPass`, which runs **after** your pass (`PostProcess.cs:387-391`).
- Dither details: triangular blue noise of ±1/255 **in sRGB space**: `GetSRGBToLinear(GetLinearToSRGB(c) + noise/255)` (`URP/Shaders/PostProcessing/Common.hlsl:188-206`). The texture index cycles every frame, so the noise is animated (`PostProcess.cs:112-127`).
- So if your pass re-grades the colour, the earlier dither gets distorted. Either keep the camera `dithering` flag and keep your pass tone-neutral, or turn the flag off and dither inside your pass with your own ±0.5 LSB noise in sRGB space.
- After your pass, `FinalBlitPass` (`AfterRendering + offset`) copies `cameraColor` to the backbuffer with `Blitter.BlitTexture` using CoreBlit passes `BilinearDebugDraw` / `NearestDebugDraw` (chosen by the source filterMode; the intermediate is Bilinear). It applies **no dither, no tonemap and no grading**. The only optional addition is the `_LINEAR_TO_SRGB_CONVERSION` keyword when `cameraData.requireSrgbConversion` is set. **VS** `URP/Runtime/Passes/FinalBlitPass.cs:99-119,196-245`; `URP/Shaders/Utils/CoreBlit.shader:330-363`. Quantisation to 8 bits happens only at the backbuffer write.
- **Where linear-to-sRGB happens:** `requireSrgbConversion = targetTexture == null && Display.main.requiresSrgbBlitToBackbuffer` (`UniversalCameraData.cs:276-288`). On Windows DX11/DX12 in Linear colour space, the backbuffer is written through an sRGB view, so **hardware encodes on write**. That is the FinalBlit when you have a pass after post, or UberPost when it writes straight to the backbuffer. The DX specifics are **UV**; the docs say the flag is "True when doing a blit to the back buffer requires manual color space conversion" (**VD** https://docs.unity3d.com/ScriptReference/Display-requiresSrgbBlitToBackbuffer.html). For a `camera.targetTexture`, the shader never converts: an `*_SRGB` RT is hardware-encoded on write, and a `UNorm` RT stores linear values.
- **Capture pitfall:** with `camera.targetTexture` set, **URP's intermediate colour buffer inherits the target texture's format** ("incorrect but kept for backward compatibility"). Rendering into an `R8G8B8A8_SRGB` RT therefore runs the **whole HDR chain in 8-bit**: bloom input is clamped and banding gets worse. The target RT must also have a depth-stencil format, or you get a warning. **VS** `URP/Runtime/UniversalRenderPipelineCore.cs:1529-1546,1584-1606`; `UniversalRendererRenderGraph.cs:398-416`. For frame captures, use a backbuffer capture (`ScreenCapture`) or an `R16G16B16A16_SFloat` target RT followed by your own sRGB encode.

---

## 4. Volume overrides (class and field names in 17.6)
Namespace `UnityEngine.Rendering.Universal`. All are `VolumeComponent, IPostProcessComponent`. All **VS** `URP/Runtime/Overrides/*.cs`.
- `Tonemapping`: `mode` (`TonemappingModeParameter`, enum `TonemappingMode { None, Neutral, ACES }`, default None). HDR-output-only extras: `neutralHDRRangeReductionMode`, `acesPreset`, `hueShiftAmount`, `detectPaperWhite`, `paperWhite`, `detectBrightnessLimits`, `minNits`, `maxNits` (Tonemapping.cs:8-26,186-231). In LDR grading mode UberPost enables `_TONEMAP_NEUTRAL`; in HDR grading mode tonemapping is baked into the LUT (`UberPostProcessPass.cs:257-269`).
- `Bloom`: `threshold` (MinFloat 0.9, "gamma-space"), `intensity` (MinFloat 0; **IsActive = intensity > 0**), `scatter` (Clamped 0.7, 0..1), `clamp` (MinFloat 65472), `tint`, `highQualityFiltering` (Bool), **new** `filter` (`BloomFilterMode { Gaussian, Dual, Kawase }`), `downscale` (`BloomDownscaleMode { Half, Quarter }`), `maxIterations` (Clamped 6, 2..8), `dirtTexture`, `dirtIntensity` (Bloom.cs:24-44,176-245).
- `ColorAdjustments`: `postExposure` (Float, EV), `contrast` (-100..100), `colorFilter` (HDR colour), `hueShift` (-180..180), `saturation` (-100..100) (ColorAdjustments.cs:114-138).
- `WhiteBalance`: `temperature` (-100..100), `tint` (-100..100). IsActive means either is nonzero (WhiteBalance.cs:102-114).
- `Vignette`: `color`, `center` (Vector2), `intensity` (0..1, IsActive > 0), `smoothness` (0.01..1, default 0.2), `rounded` (Vignette.cs:115-145).

Creating a profile asset in the editor. `UnityEditor.Rendering.VolumeProfileFactory` is **VS** at `CORE/Editor/Volume/VolumeProfileFactory.cs:52-117`:
```csharp
var p  = VolumeProfileFactory.CreateVolumeProfileAtPath("Assets/_Game/Settings/Rendering/Ring_Volume.asset"); // CreateAsset + SaveAssets
var tm = VolumeProfileFactory.CreateVolumeComponent<Tonemapping>(p, overrides:false, saveAsset:false);      // profile.Add<T>() + hideFlags + AddObjectToAsset
tm.mode.Override(TonemappingMode.Neutral);                          // Override() == overrideState=true + value
var b  = VolumeProfileFactory.CreateVolumeComponent<Bloom>(p, false, false);
b.threshold.Override(1.0f); b.intensity.Override(0.25f); b.scatter.Override(0.6f);
b.clamp.Override(500f); b.highQualityFiltering.Override(true);      // intensity>0 in the ASSET keeps the bloom variant in builds
var ca = VolumeProfileFactory.CreateVolumeComponent<ColorAdjustments>(p, false, false);
ca.postExposure.Override(0f); ca.contrast.Override(0f); ca.saturation.Override(0f);
var wb = VolumeProfileFactory.CreateVolumeComponent<WhiteBalance>(p, false, false); wb.temperature.Override(0f);
var vg = VolumeProfileFactory.CreateVolumeComponent<Vignette>(p, false, false);
vg.intensity.Override(0.18f); vg.smoothness.Override(0.45f);
foreach (var c in p.components) EditorUtility.SetDirty(c);
EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
```
- The underlying API is `VolumeProfile.Add<T>(bool overrides = false)`. It **throws if the component already exists**, so call `TryGet<T>(out T)` first. It calls `SetAllOverridesTo(overrides)` (`CORE/Runtime/Volume/VolumeProfile.cs:92-123,231`). `VolumeParameter<T>.overrideState`, `.value` and `Override(T)` are at `CORE/Runtime/Volume/VolumeParameter.cs:60,183,235-239`.
- For a raw `profile.Add<T>()` on a persistent profile, you must call `AssetDatabase.AddObjectToAsset(component, profile)`. Otherwise the reference is lost on save. **VS** `CORE/Editor/Volume/VolumeComponentListEditor.cs:509-518`

Driving values cheaply at runtime:
- Put a scene `Volume` (`isGlobal = true`, `priority`, `weight` 0..1, `sharedProfile = asset`) on a GameObject. **VS** `CORE/Runtime/Volume/Volume.cs:23-77`
- Once, in `Awake`, take `var prof = volume.profile;`. This instantiates a per-volume copy, so the asset is never modified in Editor play mode. Then `prof.TryGet(out m_Bloom)` and so on, and cache the references.
- Every frame, assign `m_Bloom.intensity.value = x;` and `m_Exposure.postExposure.value = ev;`. This allocates nothing. `overrideState` must already be true, either from the asset or from `Override()` once.
- `VolumeManager.Update` already re-blends every frame whenever any volume exists (`requiresReset = true`, `CORE/Runtime/Volume/VolumeManager.cs:727-745,769-830`), as long as the camera or asset volume update mode is EveryFrame. With `ViaScripting`, call `camera.UpdateVolumeStack()` (`UniversalAdditionalCameraData.cs:242-257`). Cross-fading two profiles with two global volumes' `weight` is also free.
- **Do not edit the Default profile or the Quality (`asset.volumeProfile`) profile at runtime.** Their values are cached. If you must, call `VolumeManager.instance.OnVolumeProfileChanged(profile)`, which is expensive. **VS** `VolumeManager.cs:359-368`; **VD** https://docs.unity3d.com/6000.4/Documentation/Manual/urp/volumes-troubleshooting.html (via search).
- The project's Default Volume Profile (`Assets/Settings/DefaultVolumeProfile.asset`) is the "all components, all overrides" defaults file. That is normal. The quality profile `SampleSceneProfile` (Bloom 1.0 / Vignette 0.2 / ACES) is the one to remove or replace.

---

## Sources (accessed 2026-10-29)
- `URP/Runtime/Data/UniversalRenderPipelineAsset.cs`: asset enums, fields, properties, `Create()`, obsolete/internal setters, keyword-filter attributes.
- `URP/Runtime/Data/PostProcessData.cs`: default PostProcessData path.
- `URP/Runtime/UniversalRendererData.cs`, `URP/Runtime/ScriptableRendererData.cs`, `URP/Editor/ScriptableRendererDataEditor.cs`: renderer settings, DepthFormat, feature list/map, AddComponent recipe.
- `URP/Runtime/UniversalRenderer.cs`, `URP/Runtime/UniversalRendererRenderGraph.cs`: RenderingMode/DepthPrimingMode enums, depth format resolution, priming conditions, post/after-post/final-blit ordering, intermediate allocation.
- `URP/Runtime/UniversalRenderPipeline.cs`, `URP/Runtime/UniversalRenderPipelineCore.cs`, `URP/Runtime/FrameData/UniversalCameraData.cs`: HDR/MSAA/postProcessEnabled/dithering resolution, HDR format choice, targetTexture format inheritance, TAA conditions, requireSrgbConversion, limits.
- `URP/Runtime/UniversalAdditionalCameraData.cs`: camera enums and properties.
- `URP/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs` (+ `.migration.cs`): feature fields, RG implementation.
- `URP/Runtime/PostProcess.cs`, `URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs`, `URP/Runtime/Passes/FinalBlitPass.cs`, `URP/Shaders/PostProcessing/Common.hlsl`, `URP/Shaders/PostProcessing/UberPost.shader`, `URP/Shaders/Utils/CoreBlit.shader`, `URP/Shaders/Utils/Blit.shader`: dithering placement, final blit behaviour, sRGB keyword.
- `URP/Runtime/Settings/RenderGraphSettings.cs`, `URP/Runtime/Settings/URPShaderStrippingSetting.cs`, `URP/Editor/ShaderBuildPreprocessor.cs`, `URP/Editor/ShaderScriptableStripper.cs`: Render Graph only; post-processing variant stripping from Volume assets; prefiltering update at build.
- `URP/Runtime/Overrides/{Tonemapping,Bloom,ColorAdjustments,WhiteBalance,Vignette}.cs`: volume field names.
- `URP/Samples~/URPRenderGraphSamples/BlitWithMaterial/*`: canonical RG pass and shader.
- `URP/ShaderLibrary/{LODCrossFade,UnityInput,Input}.hlsl`: crossfade helper, screen-size uniforms.
- `CORE/Runtime/Utilities/{Blit.hlsl,Blitter.cs,CoreUtils.cs}`, `CORE/ShaderLibrary/{Common,DynamicScaling,GlobalSamplers}.hlsl`, `CORE/ShaderLibrary/API/D3D11.hlsl`: blit include, samplers, default depth formats, reversed Z.
- `CORE/Runtime/RenderGraph/RenderGraphUtilsBlit.cs`: AddBlitPass / BlitMaterialParameters.
- `CORE/Runtime/Volume/{Volume,VolumeProfile,VolumeParameter,VolumeManager}.cs`, `CORE/Editor/Volume/{VolumeProfileFactory,VolumeComponentListEditor}.cs`: Volume APIs.
- `CORE/Editor/RenderGraph/RenderGraphTestsCore.cs`: proof that `GraphicsSettings.defaultRenderPipeline` / `QualitySettings.renderPipeline` setters work.
- `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml`, `.../Managed/UnityEditor.xml`: GraphicsSettings/QualitySettings/SystemInfo/Display/PlayerSettings member existence.
- `Library/Bee/artifacts/1900b0aE.dag/Unity.RenderPipelines.Universal.Runtime.rsp`: active scripting defines.
- Project files `Assets/Settings/{PC_RPAsset,PC_Renderer,SampleSceneProfile,DefaultVolumeProfile,UniversalRenderPipelineGlobalSettings}.asset`, `ProjectSettings/{GraphicsSettings,QualitySettings}.asset`: current project state.
- https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass: Full Screen Pass properties, `_BlitTexture` binding, hidden Pass field.
- https://docs.unity3d.com/ScriptReference/Display-requiresSrgbBlitToBackbuffer.html: meaning of the sRGB blit flag.
- https://docs.unity3d.com/6000.4/Documentation/Manual/urp/volumes-troubleshooting.html (seen via web search summary): default/quality profile runtime edits need `OnVolumeProfileChanged`.
- https://docs.unity3d.com/Manual/urp/renderer-features/how-to-fullscreen-blit.html and https://docs.unity3d.com/kr/6000.0/Manual/urp/renderer-features/write-a-scriptable-render-pass.html (via web search summary): Core.hlsl followed by Blit.hlsl include order, `Vert`/`Varyings`, `sampler_LinearClamp` usage.
- https://discussions.unity.com/t/toggle-renderfeatures-or-change-renderers-from-script/771443 (via web search summary): the renderer feature list is meant to be static at build time; toggle features with `SetActive`.
