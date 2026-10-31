# 09: Depth precision, shader stripping and player-build safety (URP 17.6 / Unity 6000.6.0f1)

Path abbreviations used below:
- `URP/` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184/`
- `CORE/` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e/`
- `UNITY/` = `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Managed/UnityEngine/` (XML API docs that ship with the editor)

Tags: **VERIFIED-SOURCE** (path:line), **VERIFIED-DOCS** (URL), **UNVERIFIED**.

---

## 0. Current project state (read-only snapshot, 2026-10-29)

- Windows graphics APIs: `m_BuildTarget: WindowsStandaloneSupport`, `m_APIs: 0200000012000000i`, `m_Automatic: 0`. That is `[Direct3D11 (2), Direct3D12 (18)]`, so DX11 is primary and DX12 is the fallback. VERIFIED-SOURCE `ProjectSettings/ProjectSettings.asset:542-544`
- The Editor currently runs on DX11 ("Direct3D: Version: Direct3D 11.0 [level 11.1]", RTX 5070). VERIFIED-SOURCE `Logs/Editor.log:114-116`
- Quality levels: "Mobile" (Mobile_RPAsset, Forward) is **excluded for Standalone**. "PC" (PC_RPAsset, `PC_Renderer` `m_RenderingMode: 2` = Forward+) is the Standalone default. So a Windows build includes **only PC_RPAsset (Forward+)**. VERIFIED-SOURCE `ProjectSettings/QualitySettings.asset:61-62,105,119-136`, `Assets/Settings/PC_Renderer.asset:56`, `URP/Runtime/UniversalRenderer.cs:11-23` (Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3)
- PC_RPAsset: HDR on, MSAA off (`m_MSAA: 1`), depth texture on, opaque texture on, main and additional shadows on, soft shadows on, 4 cascades, LOD cross-fade on, GPU Resident Drawer = 1. PC_Renderer has the SSAO feature. No `m_DepthAttachmentFormat` / `m_DepthTextureFormat` keys, so both are `DepthFormat.Default`. VERIFIED-SOURCE `Assets/Settings/PC_RPAsset.asset:19-109`, `Assets/Settings/PC_Renderer.asset:36-60`
- Global stripping settings: `m_StripUnusedVariants: 1`, `m_StripUnusedPostProcessingVariantsAndResources: 1`, `m_StripScreenCoordOverrideVariants: 1`, `m_StripRuntimeDebugShaders: 1`, `m_ExportShaderVariants: 1`. VERIFIED-SOURCE `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset:15-25,319-325,461-466`
- GraphicsSettings: `m_LightmapStripping: 0`, `m_FogStripping: 0` (both Automatic), `m_InstancingStripping: 0` (Strip Unused), `m_BrgStripping: 2`, `m_CustomRenderPipeline: {fileID: 0}` (the pipeline comes only from Quality levels). VERIFIED-SOURCE `ProjectSettings/GraphicsSettings.asset:40,46-58`

---

## 1. Depth and stencil formats, reversed Z, logging the format at runtime

### 1.1 Defaults (Windows DX11 and DX12)
- `CoreUtils.GetDefaultDepthStencilFormat()` returns `GraphicsFormat.D32_SFloat_S8_UInt` on every platform except Switch/Switch2/EmbeddedLinux/QNX/Android, which get `D24_UNorm_S8_UInt`. `GetDefaultDepthOnlyFormat()` returns `D32_SFloat`. `GetDefaultDepthBufferBits()` returns `DepthBits.Depth32`. This is a compile-time `#if`; there is no DX11/DX12 difference. VERIFIED-SOURCE `CORE/Runtime/Utilities/CoreUtils.cs:1944-1979`
- The renderer resolves the formats as follows (VERIFIED-SOURCE `URP/Runtime/UniversalRenderer.cs:211-212`):
  ```csharp
  cameraDepthTextureFormat    = (m_CameraDepthTextureFormat    != DepthFormat.Default) ? (GraphicsFormat)m_CameraDepthTextureFormat    : CoreUtils.GetDefaultDepthStencilFormat();
  cameraDepthAttachmentFormat = (m_CameraDepthAttachmentFormat != DepthFormat.Default) ? (GraphicsFormat)m_CameraDepthAttachmentFormat : CoreUtils.GetDefaultDepthStencilFormat();
  ```
  So with Default on Windows, `_CameraDepthAttachment` is **D32_SFloat_S8_UInt**.
- The intermediate depth attachment is created with `desc.format = cameraDepthAttachmentFormat`. VERIFIED-SOURCE `URP/Runtime/UniversalRendererRenderGraph.cs:1770-1815` (line 1783)
- **The format of `_CameraDepthTexture` depends on how it is produced** (VERIFIED-SOURCE `URP/Runtime/UniversalRendererRenderGraph.cs:663-666,1817-1837`):
  - `depthTextureIsDepthFormat = prepassToCameraDepthTexture || !PlatformAutoDetect.hasRenderToR32F`
  - Prepass straight into the depth texture (no depth priming, not deferred): format is `cameraDepthTextureFormat` (D32_SFloat_S8_UInt by default).
  - Otherwise it is produced by a copy (CopyDepth pass): **`GraphicsFormat.R32_SFloat`, a COLOR texture**. `hasRenderToR32F = SystemInfo.IsFormatSupported(R32_SFloat, GraphicsFormatUsage.Render)`, which is true on DX11/12 (`URP/Runtime/UniversalRenderPipelineCore.cs:1958`).
  - Both paths keep full 32-bit float precision.
  - **Pitfall:** never bind `resourceData.cameraDepthTexture` as a depth attachment. For depth testing use `resourceData.activeDepthTexture`. Unity has a known error of this kind: "Trying to SetRenderAttachmentDepth on a texture that has a color format R32_SFloat" (issue UUM, fixed in 6000.0.18f1). VERIFIED-DOCS https://issuetracker.unity3d.com/issues/invalid-depth-format-when-rendergraph-and-apv-enabled-with-urp
- **Backbuffer depth** (used when URP renders without intermediate textures): `SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil)`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1509-1528`. Its exact value on DX11 is **UNVERIFIED**, so log it.
  - URP creates intermediate color and depth whenever HDR is on, post-processing is on, the render scale is not 1, a depth copy is needed, and so on (`URP/Runtime/UniversalRenderer.cs:809-853`, HDR at line 851; `URP/Runtime/UniversalRendererRenderGraph.cs:328-339`). With this project's settings (HDR, post-processing, depth texture) the camera depth is therefore always the intermediate **D32_SFloat_S8_UInt** attachment. VERIFIED-SOURCE
- Unsupported formats fall back silently. The `UniversalRendererData.depthAttachmentFormat` and `depthTextureFormat` getters return `Default` with the warning "Selected Depth Attachment Format is not supported on this platform, falling back to Default" when `!SystemInfo.IsFormatSupported(fmt, GraphicsFormatUsage.Render)`. Deferred rejects formats without stencil. VERIFIED-SOURCE `URP/Runtime/UniversalRendererData.cs:276-321`
  - The Render Graph Viewer reports Depth_24 and Depth_16_Stencil_8 as D32_SFloat_S8_UInt on Windows. Unity closed this "As Designed" because not all formats exist on all APIs. VERIFIED-DOCS https://issuetracker.unity.com/issues/17357/rendergraph-shows-incorrect-values-for-some-depth-attachment-format-and-depth-texture-format-options

### 1.2 Forcing D32_SFloat_S8_UInt explicitly
`enum DepthFormat { Default, Depth_16 = D16_UNorm, Depth_24 = D24_UNorm, Depth_32 = D32_SFloat, Depth_16_Stencil_8, Depth_24_Stencil_8, Depth_32_Stencil_8 = GraphicsFormat.D32_SFloat_S8_UInt }`. VERIFIED-SOURCE `URP/Runtime/UniversalRendererData.cs:59-102`. The serialized fields are `m_DepthAttachmentFormat` and `m_DepthTextureFormat` (lines 146-147).

Default already gives D32S8 on Windows, so setting it explicitly is belt-and-braces. Use an editor script and set the C# property; do not hand-edit the YAML integer, whose value is UNVERIFIED:
```csharp
// Editor-only
var rd = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
rd.depthAttachmentFormat = UnityEngine.Rendering.Universal.DepthFormat.Depth_32_Stencil_8;
rd.depthTextureFormat    = UnityEngine.Rendering.Universal.DepthFormat.Depth_32_Stencil_8; // or Depth_32 (no stencil needed for the texture)
EditorUtility.SetDirty(rd); AssetDatabase.SaveAssets();
```

### 1.3 Reversed Z
- On DX11/DX12, `SystemInfo.usesReversedZBuffer == true`: near = 1, far = 0. VERIFIED-DOCS https://docs.unity3d.com/ru/2021.1/Manual/SL-PlatformDifferences.html (lists "DirectX 11, DirectX 12, PS4, Xbox One, Metal: Reversed direction") and https://docs.unity3d.com/Manual/SL-PlatformDifferences.html (6.3: DirectX, Metal, Vulkan run 1.0 near to 0.0 far). The XML doc for the property is in `UNITY/UnityEngine.CoreModule.xml` (P:UnityEngine.SystemInfo.usesReversedZBuffer).
- HLSL: `UNITY_REVERSED_Z` is defined. URP's `Core.hlsl` builds `UNITY_Z_0_FAR_FROM_CLIPSPACE` from it. VERIFIED-SOURCE `URP/ShaderLibrary/Core.hlsl:30-40`
  - Use `LinearEyeDepth(raw, _ZBufferParams)` and `Linear01Depth`; they handle reversed Z.
  - When reconstructing world position from raw depth with `ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP)`, pass the raw depth unchanged on DX.
  - Clear and "far" depth = 0 on DX (`#if UNITY_REVERSED_Z ... 0 : 1`).
- Precision: reversed Z with D32 float gives near-uniform relative precision. Near plane 0.1 m with a far plane of hundreds of km is fine for depth. The real limits at ringworld scale are float precision of vertex positions far from the origin, and shadow distance. UNVERIFIED (engineering guidance)

### 1.4 Logging the real depth format at runtime (Render Graph, URP 17)
APIs:
- `RenderGraph.GetTextureDesc(in TextureHandle)` (`CORE/Runtime/RenderGraph/RenderGraph.cs:747`). It **throws ArgumentException for the imported backbuffer** ("does not have a valid descriptor ... system back buffer"; `CORE/Runtime/RenderGraph/RenderGraphResourceRegistry.cs:863-869`).
- `RenderGraph.GetRenderTargetInfo(TextureHandle)` (`RenderGraph.cs:757`) works for imported handles as well (`RenderGraphResourceRegistry.cs:702-776`). Use this one.
- `UniversalResourceData.activeDepthTexture` / `cameraDepth` / `cameraDepthTexture` / `backBufferDepth` (`URP/Runtime/FrameData/UniversalResourceData.cs:61,137,193,113`). Valid only at record time.
```csharp
using UnityEngine;
using UnityEngine.Experimental.Rendering;               // DefaultFormat, GraphicsFormat
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class DepthFormatProbeFeature : ScriptableRendererFeature
{
    sealed class ProbePass : ScriptableRenderPass
    {
        int logged;
        public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
        {
            var cam = frameData.Get<UniversalCameraData>();
            if (cam.cameraType != CameraType.Game || logged > 2) return;
            var res = frameData.Get<UniversalResourceData>();
            string Fmt(TextureHandle h) => h.IsValid() ? rg.GetRenderTargetInfo(h).format.ToString() : "none";
            Debug.Log($"[DepthProbe] api={SystemInfo.graphicsDeviceType} reversedZ={SystemInfo.usesReversedZBuffer} " +
                      $"activeDepth={Fmt(res.activeDepthTexture)} cameraDepth={Fmt(res.cameraDepth)} " +
                      $"depthTex={Fmt(res.cameraDepthTexture)} backbufferDepthDefault={SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil)} " +
                      $"backbufferTarget={res.isActiveTargetBackBuffer}");
            logged++;
        }
    }
    ProbePass m_Pass;
    public override void Create() { m_Pass = new ProbePass { renderPassEvent = RenderPassEvent.AfterRenderingOpaques }; }
    public override void AddRenderPasses(ScriptableRenderer r, ref RenderingData d) => r.EnqueuePass(m_Pass);
}
```
Notes:
- `cameraData.cameraTargetDescriptor.depthStencilFormat` is the *backbuffer-derived* format (`URP/Runtime/UniversalRenderPipelineCore.cs:1607`). It is **not** the intermediate attachment format, which is overridden at `UniversalRendererRenderGraph.cs:1783`. VERIFIED-SOURCE
- `res.cameraDepthTexture` is only valid when something requested the depth texture. UNVERIFIED for edge cases.

---

## 2. URP shader stripping in player builds (what actually happens in 17.6)

### 2.1 Pipeline of filters
1. **Engine prefiltering**, driven by `[ShaderKeywordFilter.*]` attributes on the URP asset. These apply **only to SubShaders tagged `"RenderPipeline"="UniversalPipeline"`**: `[ShaderKeywordFilter.ApplyRulesIfTagsEqual("RenderPipeline", "UniversalPipeline")]` on `UniversalRenderPipelineAsset`. VERIFIED-SOURCE `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:421`; rules in `URP/Runtime/Data/UniversalRenderPipelineAssetPrefiltering.cs:46-206`. The build writes the prefilter values into each included URP asset before the build (`UpdateShaderKeywordPrefiltering`, `ShaderBuildPreprocessor.cs:596-601`), so **the build modifies and saves PC_RPAsset**.
2. **SRP Core `ShaderVariantStripper` (IPreprocessShaders)**:
   - If a SubShader's `RenderPipeline` tag exists and differs from the pipeline's tag (`"UniversalPipeline"`), **all its variants are removed**. VERIFIED-SOURCE `CORE/Editor/ShaderStripping/ShaderPreprocessor.cs:113-121`, tag lookup in `CORE/Editor/ShaderStripping/ShaderExtensions.cs:21-43`
   - A SubShader with no tag is not removed by tag. It still goes through the keyword strippers.
   - A variant is removed only if **all** active strippers agree (`.All(...)`, line 85).
3. **URP `ShaderScriptableStripper.CanRemoveVariant`**. VERIFIED-SOURCE `URP/Editor/ShaderScriptableStripper.cs:1337-1397`
   - **Applies to every shader, custom or built-in, regardless of tag**, because it matches keywords by name: `shader.keywordSpace.FindKeyword(name)` (line 211). It is only active when `UniversalRenderPipeline.asset != null` (line 13).
   - **Feature/pass stripping is skipped entirely when `stripUnusedVariants == false`** (`bool removeInput = strippingData.stripUnusedVariants;`, line 1364). Only volume (post-processing) stripping still runs (lines 1392-1394).
   - A variant is stripped only if it is unused for **every** renderer's feature set in `supportedFeaturesList` (lines 1367-1388).

### 2.2 Settings that drive it
- `URPShaderStrippingSetting` (Graphics settings, URP, "Additional Shader Stripping Settings"). VERIFIED-SOURCE `URP/Runtime/Settings/URPShaderStrippingSetting.cs:57-112`

  | Property | Serialized field | Code default |
  |---|---|---|
  | `stripUnusedPostProcessingVariants` | `m_StripUnusedPostProcessingVariantsAndResources` | false |
  | `stripUnusedVariants` | `m_StripUnusedVariants` | true |
  | `stripScreenCoordOverrideVariants` | `m_StripScreenCoordOverrideVariants` | true |
  | `strip2DUnusedVariants` | `m_Strip2DUnusedVariants` | false |

  Read it in the Editor with `GraphicsSettings.TryGetRenderPipelineSettings<URPShaderStrippingSetting>(out var s)`. The container is removed from players.
- `ShaderStrippingSetting` (Core): `exportShaderVariants` (default true), `shaderVariantLogLevel` (`Disabled|OnlySRPShaders|AllShaders`), `stripRuntimeDebugShaders` (default true). VERIFIED-SOURCE `CORE/Runtime/Settings/ShaderStrippingSetting.cs:8-21,73-114`
  - With export on, the build writes **`Temp/shader-stripping.json`** and `Temp/compute-shader-stripping.json` (variants in/out per shader and pass). VERIFIED-SOURCE `CORE/Editor/ShaderStripping/ShaderStrippingReport.cs:244-268`
- Which URP assets count: `EditorUserBuildSettings.activeBuildTarget.TryGetRenderPipelineAssets(...)` returns Quality levels enabled for the target plus the Graphics default. VERIFIED-SOURCE `URP/Editor/ShaderBuildPreprocessor.cs:533-541`; the docs say the same (VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/built-in/shaders-in-universalrp/shader-stripping/features). The docs also warn: "Avoid including URP assets in your build that use different rendering paths. This can cause Unity to create two sets of variants".
- The build logs "N URP assets included in build" with renderer list. VERIFIED-SOURCE `URP/Editor/BuildProcessors/URPPreprocessBuild.cs:54-73`. The build **fails** if a URP asset or the global settings are not at the latest version (`URPBuildDataValidator.cs:12-58`).

### 2.3 Concrete rules relevant to this project (Forward+ only, shadows on, SSAO on, stripUnusedVariants on)
- **Main light shadows** (`_MAIN_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS_CASCADE`, `_MAIN_LIGHT_SHADOWS_SCREEN`):
  - `UniversalRendererData.stripShadowsOffVariants` is a hard-coded `true` (`[NonSerialized] bool m_StripShadowsOffVariants = true;`). VERIFIED-SOURCE `URP/Runtime/UniversalRendererData.cs:382-397`. So `ShadowsKeepOffVariants` is NOT set (`ShaderBuildPreprocessor.cs:808,870-871`).
  - Prefilter mode becomes `SelectMainLightAndCascades`: keeps `_MAIN_LIGHT_SHADOWS` and `_MAIN_LIGHT_SHADOWS_CASCADE`, **removes the OFF variant** (`ShaderBuildPreprocessor.cs:1222-1239`; `UniversalRenderPipelineAssetPrefiltering.cs:62-70`, comment "off variants are atm always removed when shadows are supported").
  - The scriptable stripper also removes OFF when the pass declares **all three** keywords and all are off (`ShaderStripTool.cs:35-55`; `ShaderScriptableStripper.cs:494-515`). `_MAIN_LIGHT_SHADOWS_SCREEN` variants are stripped unless a ScreenSpaceShadows feature exists.
  - This is safe at runtime because URP keeps the keyword ON with an empty 1x1 shadowmap when there are no casters or the light has no shadows (`SetupForEmptyRendering`, `URP/Runtime/Passes/MainLightShadowCasterPass.cs:170-207,244-250`). VERIFIED-SOURCE
  - Consequence: a custom URP-tagged shader **must not rely on the "no shadow keyword" variant existing**. Declare exactly `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` as Lit does (`URP/Shaders/Lit.shader:144`).
- **Forward+ / cluster keywords**:
  - Forward+ adds `ShaderFeatures.ForwardPlus | AdditionalLightsKeepOffVariants` and **clears `AdditionalLightsPixel | AdditionalLightsVertex`** (`ShaderBuildPreprocessor.cs:834-836,860-865`).
  - Prefilter: `forwardPlusPrefilteringMode = SelectOnly` when no Forward/Deferred renderer is included (`ShaderBuildPreprocessor.cs:1191-1200`). That **removes the OFF variant of `_CLUSTER_LIGHT_LOOP`** (`UniversalRenderPipelineAssetPrefiltering.cs:96-100`). `additionalLightsPrefilteringMode = Remove` **strips `_ADDITIONAL_LIGHTS` and `_ADDITIONAL_LIGHTS_VERTEX`** (`ShaderBuildPreprocessor.cs:1202-1218`).
  - The scriptable stripper also removes a variant where the pass has `_CLUSTER_LIGHT_LOOP` but it is off (`ShaderScriptableStripper.cs:601-615`). It keeps only the additional-lights OFF variant (lines 638-663).
  - At runtime `_CLUSTER_LIGHT_LOOP` is a global keyword (`URP/Runtime/UniversalRenderPipelineCore.cs:917,1067`). In HLSL it sets `USE_CLUSTER_LIGHT_LOOP 1`, `#define _ADDITIONAL_LIGHTS 1` and `#undef _ADDITIONAL_LIGHTS_VERTEX` (`URP/ShaderLibrary/Core.hlsl:13-21`).
  - **Custom lit shaders must declare `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`** and iterate lights with `LIGHT_LOOP_BEGIN(count)` / `LIGHT_LOOP_END` (`URP/ShaderLibrary/RealtimeLights.hlsl:35-46`). Otherwise they get **no additional (point/spot) lights in the player**.
  - `_FORWARD_PLUS` has been deprecated since 6.1 and emits `#warning` (`URP/ShaderLibrary/ForwardPlusKeyword.deprecated.hlsl:4-21`). Do not use it.
- **Soft shadows**: desktop uses `_SHADOWS_SOFT`. The `_LOW/_MEDIUM/_HIGH` variants are stripped unless Quest/XR is targeted (`ShaderBuildPreprocessor.cs:403-408,885-896`; `ShaderScriptableStripper.cs:553-564`). Declare the same line as Lit: `multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`.
- **SSAO**: if every renderer has an active SSAO feature and stripUnusedVariants is on, the `_SCREEN_SPACE_OCCLUSION` prefilter is `SelectOnly` and **the OFF variant is removed** (`ShaderBuildPreprocessor.cs:1268-1278`). **Do not toggle the SSAO feature off at runtime** (e.g. a quality toggle) unless you accept a nearest-variant fallback. UNVERIFIED runtime consequence.
- **Passes removed entirely** (all shaders, by LightMode or pass name):
  - `ShadowCaster` passes, if no URP asset has main or additional shadows (`ShaderScriptableStripper.cs:1159-1168`).
  - `Meta` passes, unless Enlighten realtime GI or Surface Cache (`:1137-1157`).
  - Pass named `"GBuffer"` without deferred (`:483-492`).
  - `"Universal2D"` without a 2D renderer (`:1129-1135`).
  - XR passes (`:1197-1219`).
  - Decal passes by name (`:1170-1195`).
- **Always stripped**: `DEBUG_DISPLAY` (when stripRuntimeDebugShaders is on or in non-diagnostic builds), `SCREEN_COORD_OVERRIDE`, `EDITOR_VISUALIZATION`, `DIRLIGHTMAP_COMBINED` without a lightmap, `_SCREEN_SPACE_IRRADIANCE` (no Surface Cache), foveated rendering, `_MIXED_LIGHTING_SUBTRACTIVE` / `LIGHTMAP_SHADOW_MIXING` / `SHADOWS_SHADOWMASK` when mixed lighting is off, `LOD_FADE_CROSSFADE` if cross-fade is off. VERIFIED-SOURCE `ShaderScriptableStripper.cs:405-481,531-545,856-872,1021-1050`
- **Keyword-name collision pitfall**: because the stripper matches by name, a custom keyword with a URP name (`_LIGHT_COOKIES`, `_LIGHT_LAYERS`, `_SCREEN_SPACE_OCCLUSION`, `_REFLECTION_PROBE_BLENDING`, ...) gets URP's stripping. **Prefix custom keywords** (e.g. `RING_*`). VERIFIED-SOURCE (mechanism); the forum case with `_DISTORTION` is in https://forum.unity.com/threads/my-materials-shader_feature-variants-are-missing-from-build.1391572/

### 2.4 Post-processing stripping, the big trap for a procedural game
With `stripUnusedPostProcessingVariants = true` (this project):
- URP scans **only `VolumeProfile` assets under `Assets/`**: `AssetDatabase.FindAssets("t:VolumeProfile")` with `path.StartsWith("Assets")`. It counts a feature only if the override `IsActive()` is true (e.g. Bloom intensity > 0, Tonemapping mode != None). VERIFIED-SOURCE `URP/Editor/ShaderBuildPreprocessor.cs:418-482`
- Effects of unused features:
  - UberPost keyword variants are stripped (`_BLOOM_*`, `_TONEMAP_ACES/NEUTRAL`, `_DISTORTION`, `_CHROMATIC_ABERRATION`, `_FILM_GRAIN`).
  - The **Bloom, Bokeh/Gaussian DoF, CameraMotionBlur and PaniniProjection shaders lose all variants** if no asset uses them.
  - Film-grain resources are removed.
  - VERIFIED-SOURCE `URP/Editor/ShaderScriptableStripper.cs:296-398`, `URP/Editor/BuildProcessors/GraphicsSettingsStrippers/FilmGrainResourcesStripper.cs`
- So **a `VolumeProfile` created at runtime with `ScriptableObject.CreateInstance` (and overrides added in code) works in the Editor but loses Bloom/Tonemapping etc. in the player**.
- Fixes (pick one):
  - (a) Keep a saved profile asset in `Assets/` with every used override active (values may differ at runtime).
  - (b) Set `URPShaderStrippingSetting.stripUnusedPostProcessingVariants = false`.
  - The docs' wording "Unity checks for Volume Overrides in all scenes" (VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/built-in/shaders-in-universalrp/shader-stripping/features) is less precise than the source, which checks profile assets in the project.

### 2.5 Engine-level (non-URP) stripping that also bites runtime-generated content
These are Graphics settings. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings
- **Fog Modes = Automatic** (project: `m_FogStripping: 0`): "Unity examines the scenes in the build and automatically strips variants that are not used". URP shaders use `#pragma multi_compile_fog` (`URP/ShaderLibrary/Fog.hlsl:5-9`; `USE_DYNAMIC_BRANCH_FOG_KEYWORD (0)` in `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/Runtime/ShaderConfig.cs.hlsl`).
  - **If fog is enabled only from script (`RenderSettings.fog = true`) and the saved scenes have fog off, the player has no fog.**
  - Fix: save fog enabled in the scene with the right mode, or set Fog Modes = Custom and keep the modes you need (`m_FogStripping: 1`, `m_FogKeepLinear/Exp/Exp2`).
- **Instancing Variants = Strip Unused** (project `m_InstancingStripping: 0`): instancing variants are kept only if a material **asset** has Enable Instancing. Runtime-created materials with `enableInstancing = true` (e.g. `Graphics.RenderMeshInstanced`) need Keep All, or a saved material asset with instancing on.
- **Lightmap Modes = Automatic**: the same scene-scan logic. Irrelevant if nothing is baked.
- **BatchRendererGroup Variants** (`m_BrgStripping: 2`). The enum is `UnityEditor.Rendering.BatchRendererGroupStrippingMode { KeepAll, KeepIfEntitiesGraphics, StripAll }`. The mapping of 2 to an enum member is UNVERIFIED; the GPU Resident Drawer needs `DOTS_INSTANCING_ON` kept.
- **shader_feature**: variants not used by any **material asset** in the build are stripped. "If you use a `shader_feature` keyword, don't use the keyword to change which code branch executes at runtime." VERIFIED-DOCS https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-variants/shader-variant-stripping (6000.6). For keywords toggled from C# on runtime materials, **use `multi_compile_local` or `dynamic_branch`**.
- Unity 6000.6 also has **Shader Build Settings** (Graphics settings, or a Build Profile) for per-keyword overrides: `UnityEditor.Rendering.EditorGraphicsSettings.GetShaderBuildSettings()/SetShaderBuildSettings(ShaderBuildSettings)`, `ShaderBuildSettings.KeywordDeclarationOverride { keywords, variantGenerationMode }`, `ShaderVariantGenerationMode { Default, AllVariants, MaterialUsageBasedVariants, SingleVariantWithDynamicBranching }`, `KeywordOverrideInfo(name, keepInBuild)`. VERIFIED-SOURCE `UNITY/UnityEditor.CoreModule.xml`

---

## 3. Making sure shaders and materials are in the player, and pre-warming

### 3.1 Inclusion rules
- "a shader might be not included into the player build if nothing references it. In that case, Shader.Find will work only in the Editor, and will result in the pink error shader in a build." To include a shader, do one of:
  1. reference it from a material used in a scene,
  2. add it to Always Included Shaders,
  3. put it, or something that references it, in a `Resources` folder.

  VERIFIED-DOCS https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/find (6000.6)
- **Always Included Shaders** = "all possible variants in every build". "This setting overrides shader keyword declaration settings ... Unity includes all sets of all keywords, even if you use the 'shader feature' declaration type." VERIFIED-DOCS (class-graphics-settings URL above). It **ignores shader_feature stripping, but the URP scriptable stripper still runs** (UNVERIFIED that URP's IPreprocessShaders also runs on always-included shaders, though IPreprocessShaders runs for all compiled variants). Use it only for small custom shaders.
- Recommended pattern for a procedural game:
  - A `ScriptableObject` "ShaderRegistry" in `Assets/Resources/` with `[SerializeField] Shader[] shaders;` and pre-made `Material` assets (one per keyword combination you use).
  - Or a scene MonoBehaviour that serializes the shader references.
  - Create runtime materials with `new Material(registry.terrainShader)`, never `Shader.Find`.
  - Add to each custom shader `Fallback "Hidden/Universal Render Pipeline/FallbackError"` so that missing variants show up as visible magenta in QA. UNVERIFIED (URP convention).
- **Strict variant matching for QA builds**: `PlayerSettings.strictShaderVariantMatching = true` ("Enable strict shader variant matching in the player"; `UNITY/UnityEditor.CoreModule.xml`).
  - Without it, "Unity might replace missing shader variants with others" (VERIFIED-DOCS shader-variant-stripping URL).
  - With it, the player log gets lines like `Shader <name>, subshader 0, pass 1, stage vertex: variant <KEYWORDS> not found.` (format from issue tracker, VERIFIED-DOCS https://issuetracker-mig.prd.it.unity3d.com/issues/shader-errors-are-logged-in-the-player-log-file-when-strict-shader-variant-matching-is-enabled-and-dynamic-branch-is-used-in-shadowcaster-pass, search snippet only).
  - Grep `Player.log` for `variant .* not found` as a gate.
- `GraphicsSettings.logWhenShaderIsCompiled = true` (dev builds only) logs `Uploaded shader variant to the GPU driver: <shader>, pass: <pass>, stage: <stage>, keywords <...>`. The wording varies by version. VERIFIED-DOCS https://docs.unity3d.com/ScriptReference/Rendering.GraphicsSettings-logWhenShaderIsCompiled.html , https://issuetracker-mig.prd.it.unity3d.com/issues/log-shader-compilation-does-not-give-info-on-which-shader-instance-had-a-variant-compiled

### 3.2 Pre-warming
- **DX11 (primary API here)**: `ShaderVariantCollection.WarmUp()` / `WarmUpProgressively(int)` / `Shader.WarmupAllShaders()` work. Alternatively, list SVCs in Graphics > Shader loading > Preloaded shaders (`m_PreloadedShaders`). VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-prewarm-other . The docs say these methods "aren't recommended on Vulkan, DirectX 12, and Metal because they can create the wrong PSOs due to missing graphics states."
- **GraphicsStateCollection** (PSO tracing and warm-up; Unity 6.0+; the 6.x manuals still label it experimental).
  - Supported on DX12, Vulkan, Metal. On DX11/GL/WebGL it "automatically falls back to shader warm up" (6.1+).
  - **Tracing only works in development builds.**
  - Trace one collection per graphics API and platform.
  - VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-pso-trace-warming/shader-pso-introduction , https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-pso-trace-warming/shader-pso-trace
  - **Surprise in 6000.6**: the type is documented as `UnityEngine.Rendering.GraphicsStateCollection`, not `UnityEngine.Experimental.Rendering` as in 6.0-6.3 script reference URLs. VERIFIED-SOURCE `UNITY/UnityEngine.CoreModule.xml` (T:UnityEngine.Rendering.GraphicsStateCollection). Write `using UnityEngine.Rendering;`.
  - Members (VERIFIED-SOURCE, same XML):
    - Constructors: `GraphicsStateCollection()`, `GraphicsStateCollection(string path)`
    - Tracing: `bool BeginTrace()`, `EndTrace()`, `isTracing`
    - Files: `bool SaveToFile(string)` (writes a `.graphicsstate` file), `bool LoadFromFile(string)`, `bool SendToEditor(string)` (needs PlayerConnection, Development Build + Autoconnect Profiler)
    - Warm-up: `JobHandle WarmUp(JobHandle dependency = default[, bool trackCacheMisses])`, `JobHandle WarmUpProgressively(int count, JobHandle, [bool])`, `isWarmedUp`, `completedWarmupCount`, `cacheMissCollection`
    - Inspection and editing: `AddVariant(...)`, `GetVariants(List<ShaderVariant>)`, `graphicsDeviceType`, `runtimePlatform`, `qualityLevelName`
    - Asset menu: Assets > Create > Shader > Graphics State Collection.
- Recommendation for this project (DX11-first):
  - Warm up during the black or fade-in intro with an SVC, or with a GraphicsStateCollection that falls back to SVC-style warm-up on DX11.
  - Also do a **"render-everything-once" warm-up**: put the camera at each set piece for one frame behind a fade. This creates real PSOs with real states on any API, and it is the most reliable approach for a linear corridor game. UNVERIFIED (engineering practice)
  - Verify in the Profiler: markers `Shader.CreateGPUProgram` and `CreateGraphicsGraphicsPipelineImpl`. VERIFIED-DOCS (6000.3 shader-prewarm page).

---

## 4. Shader compile errors in batchmode, and a "shader compile gate"

### 4.1 Where errors appear
- Editor.log or `-logFile` output:
  - `Shader error in '<Shader/Name>': <message> at line <N> (on d3d11)`
  - or `... at <Assets/path>.shader(<N>) (on d3d11)`
  - followed by `Compiling Subshader: 0, Pass: <PassName>, <Vertex|Fragment> program with <KEYWORDS>` and platform-defines / disabled-keywords lines.
  - Warnings use `Shader warning in '...'`.
  - VERIFIED-DOCS (examples in Unity issue tracker: "Shader error in 'Shader Graphs/Master': syntax error: unexpected token '.'" ... "Compiling Subshader: 0, Pass: GBuffer, Fragment program", https://issuetracker-mig.prd.it.unity3d.com/issues/directx11-errors-are-thrown-and-shader-becomes-broken-when-connecting-the-subgraph-containing-the-sample-texture2darray-node-to-basecolor ; "... at line 3048 (on d3d11)", https://issuetracker.unity3d.com/product/unity/issues/guid/1259511). The exact layout is not otherwise documented; grep `"Shader error in"`.
- **The project's `Logs/` folder contains per-process compiler logs** `Logs/shadercompiler-UnityShaderCompiler.exe-<n>.log`.
  - Each snippet appears as `Cmd: compileSnippet ... file=<path> name=<shader> pass=<pass> ... type=Vertex|Fragment platform=dx11 ...` followed by `RunExternalCompiler: begin name=FXC` and `compileSnippet complete  ok=1 outsize=...`.
  - **`ok=0` marks a failed variant.** Grep it as a secondary gate signal.
  - VERIFIED-SOURCE `Logs/shadercompiler-UnityShaderCompiler.exe-0.log:1-20`
- In `-batchmode`, the console gets minimal output; the log file has everything. `-nographics` disables the device and also output logs unless `-logFile` is passed. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor

### 4.2 APIs (all VERIFIED-SOURCE `UNITY/UnityEditor.CoreModule.xml`)
- `ShaderUtil.GetShaderMessages(Shader)` / `GetShaderMessages(Shader, ShaderCompilerPlatform)` returns `ShaderMessage[]` with fields `message`, `messageDetails`, `file`, `line`, `platform`, `severity` (`UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error|Warning`). Also `GetShaderMessageCount(Shader)` and `ClearShaderMessages(Shader)`.
- `ShaderUtil.ShaderHasError(Shader)` (ignores warnings), `ShaderUtil.ShaderHasWarnings(Shader)`, `ShaderUtil.GetAllShaderInfo()` returns `ShaderInfo { name, hasErrors, hasWarnings, supported }`.
- `ShaderUtil.GetComputeShaderMessages(ComputeShader)` / `GetComputeShaderMessageCount`.
- `ShaderUtil.CompilePass(Material, int pass, bool forceSync)`, `ShaderUtil.IsPassCompiled(Material, int)`, `ShaderUtil.anythingCompiling`, `ShaderUtil.allowAsyncCompilation`.
- **Deterministic per-variant compile**: `ShaderUtil.GetShaderData(shader)` returns `ShaderData`, then `.SubshaderCount`, `.GetSubshader(i)` returns `Subshader` (`.PassCount`, `.GetPass(j)`), then `Pass` (`.Name`, `.IsGrabPass`, `.HasShaderStage(ShaderType)`, `.CompileVariant(ShaderType, string[] keywords, ShaderCompilerPlatform, BuildTarget[, bool forExternalTool])`). It returns `ShaderData.VariantCompileInfo { Success, Messages (ShaderMessage[]), ShaderData (bytecode) ... }`. `Pass.PreprocessVariant(...)` also exists.
- `ShaderCompilerPlatform` has **`D3D11` and `D3D12` as separate members in 6000.6. `D3D` is "marked for deprecation".** Surprise: use `ShaderCompilerPlatform.D3D11` for the DX11 gate and `D3D12` for DX12.
- `ShaderUtil.GetPassKeywords(Shader, ref PassIdentifier[, ShaderType])` returns the local keywords valid for a pass, for building keyword sets. `ShaderUtil.PassHasKeyword(...)` also exists (used by URP: `ShaderScriptableStripper.cs:100`).
- `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport)`.
- `BuildOptions.StrictMode`: "Prevent the build from succeeding if any errors are reported during the build process". `BuildReport.summary.totalErrors`, `BuildResult.Failed`. Combine it with the gate so a shader error during the player build fails the run. UNVERIFIED that every shader compile error counts toward totalErrors; check `summary.result` and grep the log as well.
- Import-time messages may not include every variant's HLSL errors, because variants compile on demand. UNVERIFIED. Hence `CompileVariant` over chosen keyword sets.

### 4.3 Gate skeleton (Editor script, run via `-batchmode -executeMethod`)
```csharp
// Unity.exe -batchmode -projectPath "<proj>" -executeMethod RingShaderGate.Run -logFile "<proj>/Logs/shader_gate.log" -quit
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

public static class RingShaderGate
{
    static readonly string[][] k_KeywordSets =
    {
        new string[0],
        new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_CLUSTER_LIGHT_LOOP", "_SCREEN_SPACE_OCCLUSION", "FOG_EXP2" },
        new[] { "_MAIN_LIGHT_SHADOWS", "_CLUSTER_LIGHT_LOOP", "FOG_LINEAR" },
        // + project-specific RING_* keyword combos
    };

    public static void Run()
    {
        int errors = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) { Debug.LogError($"[ShaderGate] FAIL load {path}"); errors++; continue; }

            foreach (var m in ShaderUtil.GetShaderMessages(shader))
                if (m.severity == ShaderCompilerMessageSeverity.Error)
                { Debug.LogError($"[ShaderGate] FAIL {path}:{m.line} [{m.platform}] {m.message}"); errors++; }
            if (!shader.isSupported) { Debug.LogError($"[ShaderGate] FAIL unsupported {shader.name}"); errors++; }

            var data = ShaderUtil.GetShaderData(shader);
            for (int s = 0; s < data.SubshaderCount; s++)
            {
                var sub = data.GetSubshader(s);
                for (int p = 0; p < sub.PassCount; p++)
                {
                    var pass = sub.GetPass(p);
                    if (pass.IsGrabPass) continue;
                    foreach (var platform in new[] { ShaderCompilerPlatform.D3D11, ShaderCompilerPlatform.D3D12 })
                    foreach (var kws in k_KeywordSets)
                    foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        if (!pass.HasShaderStage(stage)) continue;
                        var r = pass.CompileVariant(stage, kws, platform, BuildTarget.StandaloneWindows64, false);
                        if (r.Success) continue;
                        errors++;
                        foreach (var msg in r.Messages)
                            Debug.LogError($"[ShaderGate] FAIL {shader.name} pass '{pass.Name}' {stage} {platform} [{string.Join(" ", kws)}] line {msg.line}: {msg.message}");
                    }
                }
            }
        }
        Debug.Log($"[ShaderGate] RESULT errors={errors}");
        EditorApplication.Exit(errors == 0 ? 0 : 1);
    }
}
```
Notes:
- Keywords not declared in a pass are ignored by the compiler. UNVERIFIED; harmless either way.
- Keep the gate keyword sets in sync with the variants that survive stripping (section 2.3).
- Also add a **player-side gate**: a development build with `strictShaderVariantMatching = true`, then run the player with `-logFile`. Grep for `Shader error`, `variant .* not found`, `Hidden/InternalErrorShader` and the `[DepthProbe]` line.

---

## 5. DX11 vs DX12 on Unity 6000.6 (Windows standalone, URP)
- Unity 6.1+ makes **DX12 the default for new projects**. DX12 has split graphics jobs and better CPU scaling, but "DX12 multithreading comes with additional graphics memory cost", it "may not be recommended on significantly old GPU models and outdated driver versions", and for legacy devices Unity recommends to "still target DirectX 11 or keep it as a fallback API". VERIFIED-DOCS https://unity.com/blog/directx-12-improvements-in-unity-6
- **Shader compilation**: FXC is the default. If DXC is enabled for DX12 (Build Profile > Graphics Settings > Shader Build Settings > Shader Compiler Backend Selection, or `#pragma use_dxc d3d12` / `#pragma never_use_dxc` per pass), then "If you build for both DX11 and DX12, Unity compiles two sets of shaders by using FXC for DX11 and DXC for DX12", which doubles variant compile time. "DXC isn't compatible with DX11." VERIFIED-DOCS https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-dxc-compiler (6000.6)
- **PSO stutter**: on DX12, first-use stutter comes from PSO creation, and SVC warm-up "can create the wrong PSOs". Use GraphicsStateCollection or a render-once warm-up (section 3.2). On DX11 the driver handles this; SVC warm-up suffices. VERIFIED-DOCS (section 3.2 URLs)
- **Recommendation for a YouTube-capture build**:
  - Keep the current order `[Direct3D11, Direct3D12]` with Auto off. DX11 is what the Editor already runs and is the most battle-tested path for FXC shaders.
  - Smoke-test the built player on DX12 with `-force-d3d12` before release. UNVERIFIED (judgement)
- **Setting APIs from an editor script** (VERIFIED-SOURCE `UNITY/UnityEditor.CoreModule.xml`): `PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget, bool)`, `PlayerSettings.SetGraphicsAPIs(BuildTarget, GraphicsDeviceType[])`, `PlayerSettings.GetGraphicsAPIs(BuildTarget)`.
  ```csharp
  PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
  PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
      new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
  ```
  - Changing the API list for the active target while the Editor is open may trigger an Editor graphics-device switch or restart prompt. In batchmode, set it before building. UNVERIFIED
  - `PlayerSettings.d3d12DeviceFilterListAsset` (allow/deny lists for D3D12 devices) exists. VERIFIED-SOURCE (XML)
- **Command line**:
  - Editor: `-force-d3d11` ("Normally the graphics API depends on Player Settings (typically defaults to D3D11)"), `-force-d3d12`, `-force-vulkan`, `-force-glcore`, `-force-device-index <n>`, `-force-d3d12-debug`, `-force-d3d12-debug-gbv`, `-force-d3d12-nvidia-aftermath`, `-nographics`, `-logFile <path>`. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor
  - Player (`Ring.exe`): `-force-d3d11`, `-force-d3d12`, `-force-d3d11-flip-model`, `-force-d3d11-bitblt-model`, `-logFile <path>`, `-screen-width/-screen-height/-screen-fullscreen`. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player (the D3D12 flag is documented from 6000.1 on)
  - Verify the active API at runtime with `SystemInfo.graphicsDeviceType` / `SystemInfo.graphicsDeviceVersion`, and log both in the `[DepthProbe]` line.

---

## Surprises and pitfalls (version-specific)
1. `stripUnusedVariants=false` disables ALL URP feature and pass stripping (`ShaderScriptableStripper.cs:1364`). `true` (the current setting) removes the main-light-shadow OFF variants, the `_CLUSTER_LIGHT_LOOP` OFF variant and all `_ADDITIONAL_LIGHTS*` variants for a Forward+-only build.
2. Prefiltering applies only to `"RenderPipeline"="UniversalPipeline"`-tagged SubShaders. The scriptable stripper applies to every shader by keyword name. A SubShader tagged with another pipeline is fully stripped.
3. Post-processing stripping scans VolumeProfile **assets in Assets/** only. Runtime-built profiles lose Bloom and Tonemapping in the player.
4. Fog and instancing "automatic/strip unused" stripping kill script-enabled fog and runtime instanced materials.
5. `_CameraDepthTexture` is usually **R32_SFloat (color)** via copy, not a depth format.
6. `ShaderCompilerPlatform.D3D` is deprecated in 6000.6; use `D3D11` and `D3D12`.
7. `GraphicsStateCollection` is in `UnityEngine.Rendering` in 6000.6 per the shipped XML docs. Tracing works only in development builds.
8. The build rewrites prefiltering data into PC_RPAsset and saves it (`ShaderBuildPreprocessor.cs:596-601`). Expect a modified asset after every build.

## Sources (accessed 2026-10-29)
- `URP/Runtime/UniversalRendererData.cs`: DepthFormat enum, format getters and setters, stripShadowsOffVariants.
- `URP/Runtime/UniversalRenderer.cs`: depth format resolution, RenderingMode enum, intermediate texture rules.
- `URP/Runtime/UniversalRendererRenderGraph.cs`: depth attachment and depth texture creation, R32F copy path, intermediate attachments.
- `URP/Runtime/UniversalRenderPipelineCore.cs`: backbuffer depth info, hasRenderToR32F, keyword strings.
- `CORE/Runtime/Utilities/CoreUtils.cs`: GetDefaultDepthStencilFormat / DepthOnly / DepthBufferBits.
- `CORE/Runtime/RenderGraph/RenderGraph.cs`, `RenderGraphResourceRegistry.cs`, `RenderGraphResourceTexture.cs`: GetTextureDesc vs GetRenderTargetInfo behaviour.
- `URP/Runtime/FrameData/UniversalResourceData.cs`: depth texture handles.
- `URP/Runtime/Passes/MainLightShadowCasterPass.cs`: empty shadowmap with keyword kept on.
- `URP/Editor/ShaderBuildPreprocessor.cs`, `URP/Editor/ShaderScriptableStripper.cs`, `URP/Editor/ShaderStripTool.cs`: URP stripping logic.
- `URP/Runtime/Data/UniversalRenderPipelineAsset.cs`, `UniversalRenderPipelineAssetPrefiltering.cs`: ShaderKeywordFilter prefiltering.
- `URP/Runtime/Settings/URPShaderStrippingSetting.cs`, `CORE/Runtime/Settings/ShaderStrippingSetting.cs`: stripping settings.
- `CORE/Editor/ShaderStripping/ShaderPreprocessor.cs`, `ShaderExtensions.cs`, `ShaderStrippingReport.cs`: tag-based stripping and the report/export path.
- `URP/Editor/BuildProcessors/*`: build validation, resource strippers, included-asset log.
- `URP/ShaderLibrary/Core.hlsl`, `Fog.hlsl`, `RealtimeLights.hlsl`, `ForwardPlusKeyword.deprecated.hlsl`, `URP/Shaders/Lit.shader`: keyword declarations, reversed Z, cluster loop.
- `UNITY/UnityEngine.CoreModule.xml`, `UNITY/UnityEditor.CoreModule.xml`: API signatures (GraphicsStateCollection, ShaderVariantCollection, ShaderUtil, ShaderData, ShaderCompilerPlatform, PlayerSettings, BuildOptions, ShaderBuildSettings, SystemInfo).
- Project files `ProjectSettings/ProjectSettings.asset`, `QualitySettings.asset`, `GraphicsSettings.asset`, `Assets/Settings/*.asset`, `Logs/Editor.log`, `Logs/shadercompiler-*.log`: current project configuration and log formats.
- https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-pso-trace-warming/shader-prewarm : GraphicsStateCollection warm-up.
- https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-prewarm-other : SVC and WarmupAllShaders limits on DX12/Vulkan/Metal.
- https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-pso-trace-warming/shader-pso-introduction : PSO API support list, DX11 fallback.
- https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-pso-trace-warming/shader-pso-trace : tracing workflow, development builds only.
- https://unity.com/blog/directx-12-improvements-in-unity-6 : DX12 default in 6.1, memory cost, legacy-device advice.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor : Editor graphics API flags, batchmode, logFile.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player : Player -force-d3d11 / -force-d3d12.
- https://docs.unity3d.com/Manual/SL-PlatformDifferences.html and https://docs.unity3d.com/ru/2021.1/Manual/SL-PlatformDifferences.html : reversed Z per API.
- https://issuetracker.unity.com/issues/17357/rendergraph-shows-incorrect-values-for-some-depth-attachment-format-and-depth-texture-format-options : depth format fallback display ("As Designed").
- https://issuetracker.unity3d.com/issues/invalid-depth-format-when-rendergraph-and-apv-enabled-with-urp : R32_SFloat depth-attachment error.
- https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-variants/shader-variant-stripping : shader_feature runtime warning, strict matching (6000.6).
- https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/built-in/shaders-in-universalrp/shader-stripping/features : URP stripping settings, included assets, mixed render paths warning.
- https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings : Always Included, Preloaded, Instancing, Fog, Lightmap stripping.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/find : shader inclusion rules for Shader.Find (6000.6).
- https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-dxc-compiler : FXC/DXC, dual compile for DX11+DX12 (6000.6).
- https://docs.unity3d.com/ScriptReference/Rendering.GraphicsSettings-logWhenShaderIsCompiled.html and https://issuetracker-mig.prd.it.unity3d.com/issues/log-shader-compilation-does-not-give-info-on-which-shader-instance-had-a-variant-compiled : runtime shader compile log format.
- https://issuetracker-mig.prd.it.unity3d.com/issues/shader-errors-are-logged-in-the-player-log-file-when-strict-shader-variant-matching-is-enabled-and-dynamic-branch-is-used-in-shadowcaster-pass : strict-matching player log line (search snippet; page returned 502).
- https://issuetracker-mig.prd.it.unity3d.com/issues/directx11-errors-are-thrown-and-shader-becomes-broken-when-connecting-the-subgraph-containing-the-sample-texture2darray-node-to-basecolor and https://issuetracker.unity3d.com/product/unity/issues/guid/1259511 : "Shader error in ... (on d3d11)" / "Compiling Subshader" log format (search snippets).
- https://docs.unity3d.com/ja/2023.2/ScriptReference/ShaderKeywordFilter.FilterAttribute.html : filter attributes apply only to RP assets referenced by QualitySettings; empty-keyword rule.
- https://forum.unity.com/threads/my-materials-shader_feature-variants-are-missing-from-build.1391572/ : keyword-name collision anecdote (_DISTORTION).
