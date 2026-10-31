# R1: ThreadGrade, a custom grade and bloom pass on Render Graph (URP 17.6.0, Unity 6000.6.0f1)

Path aliases used below:
`URP/` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184/`,
`CORE/` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e/`.
Every API claim below was checked against these sources (file:line). Web sources are listed at the end.

## 0. Decisions in brief

1. **Render Graph is the only path.** `ScriptableRenderPass.Execute/Configure/OnCameraSetup` do not exist in 17.6. Implement `RecordRenderGraph` only.
2. **Use Option B.** Turn URP post-processing off on the gameplay camera and run our own bloom and ThreadGrade in one renderer feature at `AfterRenderingPostProcessing`. With URP post on, URP always runs a LUT pass (LDR mode: `saturate` plus an 8-bit LUT) and dithers *before* any after-post pass. Either one rules out banding-free output.
3. **Set `hdrColorBufferPrecision = _64Bits`** (camera colour becomes `R16G16B16A16_SFloat`). The default `_32Bits` gives `B10G11R11_UFloatPack32`. Its 5-bit blue mantissa is coarser than one 8-bit sRGB step in the midtones, which shows up as blue/yellow banding in skies.
4. **Write the final result into a fresh `R8G8B8A8_SRGB` RG texture.** Dither (TPDF, in sRGB code space) right before this write, then set `resourceData.cameraColor = out`. URP's FinalBlit then makes an exact 8-bit copy to the screen and handles y-flip, viewport, `requiresSrgbBlitToBackbuffer`, Recorder capture and the overlay UI. Writing straight to `backBufferColor` (B2) is shown as an option, but it needs manual y-flip and sRGB handling and breaks Camera Capture.
5. **Use MSAA 4x from the asset** (`msaaSampleCount = 4`), with FXAA/SMAA/TAA off (they only exist inside URP post). The MSAA camera target resolves automatically when we `UseTexture` it.

## 1. Verified facts

### 1.1 Compatibility mode and `Execute()` are removed
- `RenderGraphSettings` is `[Obsolete("These settings are not used. #from(6000.4)")]`, and `enableRenderCompatibilityMode => false` (URP/Runtime/Settings/RenderGraphSettings.cs:42, :66).
- `ScriptableRenderPass` (URP/Runtime/Passes/ScriptableRenderPass.cs:207) is `abstract partial class ScriptableRenderPass : IRenderGraphRecorder`. Its overridable API is `RecordRenderGraph(RenderGraph, ContextContainer)` (:311-314), `OnCameraCleanup` (:306), `ConfigureInput` (:294) and `requiresIntermediateTexture` (:223). No `Execute(ScriptableRenderContext, ref RenderingData)`, `Configure` or `OnCameraSetup` exists anywhere in URP/Runtime (grep returns nothing). The only legacy member left is `FrameCleanup` (URP/Runtime/Deprecated.cs:9-18).
- The Unity 6.4 upgrade guide says: "URP Compatibility Mode is now fully removed for custom render passes", and the `URP_COMPATIBILITY_MODE` define is gone. One dead `#if URP_COMPATIBILITY_MODE` block remains at URP/Runtime/RenderingUtils.cs:851.
- `ScriptableRendererFeature` (URP/Runtime/ScriptableRendererFeature.cs) has `Create()` (:50), `AddRenderPasses(ScriptableRenderer, ref RenderingData)` (:64), `OnCameraPreCull`, `Dispose(bool)` (:122) and `SetActive`. `SetupRenderPasses` no longer exists, and `IntermediateTextureUsage` is obsolete (:23-45).

### 1.2 Frame order around post-processing (`UniversalRenderer.OnAfterRendering`, URP/Runtime/UniversalRendererRenderGraph.cs:1364-1560)
1. `RecordCustomRenderGraphPasses(BeforeRenderingPostProcessing)` (:1381).
2. `applyFinalPostProcessing` is true only when post is on, this is the last camera, and FXAA, FSR1 upscaling or TAA+CAS is used (:1397-1400).
3. `hasPassesAfterPostProcessing` checks whether any enqueued pass has event in [600, 1000) (:1404).
4. If URP post is on: when there is no final post, no after-post pass, no capture and no UI composite, uber writes **directly to the backbuffer** (:1435-1442). Otherwise uber writes to an intermediate (:1443-1452 and below). Then `m_PostProcess.RenderPostProcessing(...)` runs (:1456).
5. `RecordCustomRenderGraphPasses(AfterRenderingPostProcessing)` runs (:1464-1465). **This happens even when URP post is off.**
6. The capture pass runs, then FinalPost (FXAA/FSR) if needed (:1481-1485).
7. **FinalBlit runs only if `!resourceData.isActiveTargetBackBuffer && cameraData.resolveFinalTarget`** (:1489-1497). If our pass calls `SwitchActiveTexturesToBackbuffer()`, FinalBlit is skipped.
8. `AfterRendering` passes run, then URP draws the overlay UI into `activeColorTexture`, which is the backbuffer by then (:1499-1540).

### 1.3 Texture formats (answer to b)
- Camera colour format comes from `MakeRenderTextureGraphicsFormat` (URP/Runtime/UniversalRenderPipelineCore.cs:1477-1491). With HDR and `_32Bits` (and no alpha needed) it is **`B10G11R11_UFloatPack32`**. With `_64Bits` it is **`R16G16B16A16_SFloat`**. Without HDR it is the LDR default.
- The intermediate camera descriptor copies `cameraData.backbufferColor.format` (:1604). For an on-screen camera that is the HDR "proxy" format (:1511-1524). For a camera with a `targetTexture`, it is **that RT's format** (:1527-1533). An 8-bit capture RT therefore makes the whole scene render 8-bit.
- `isHdrEnabled = camera.allowHDR && asset.supportsHDR` (URP/Runtime/UniversalRenderPipeline.cs:1772). The default precision is `_32Bits` (URP/Runtime/Data/UniversalRenderPipelineAsset.cs:452). The enum doc warns that R11G11B10 "can cause a subtle blue/yellow banding" (:166-175).
- **URP post on, pass at `AfterRenderingPostProcessing`:** the uber destination is `renderGraph.CreateTexture(sourceTexture, "_CameraColorAfterPostProcessing")` (URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs:141-147). That is the *same descriptor as camera colour*: **still HDR float (R11G11B10 or RGBA16F), not 8-bit, and it even keeps MSAA** (see the TODO comment at :143-145). Uber then sets `resourceData.cameraColor = destination` (:293-296).
- The values in that texture are not raw HDR. Uber *always* applies colour grading ("Color grading is always enabled", URP/Shaders/PostProcessing/UberPost.shader:271-273):
  - LDR grading: `ApplyTonemap` returns `saturate(input)`, so output is clamped to [0,1] even with tonemapping None (URP/Shaders/PostProcessing/Common.hlsl:101-111). The internal LDR LUT is `R8G8B8A8_UNorm` (URP/Runtime/Passes/ColorGradingLutPass.cs:55).
  - HDR grading: input goes through `saturate(LinearToLogC(x))` and a 32³ LogC LUT stored as RGBA16F (Common.hlsl:121-124; ColorGradingLutPass.cs:42-43).
- `afterPostProcessColor` is `[Obsolete("...has never been implemented. Use cameraColor instead.")]` (URP/Runtime/FrameData/UniversalResourceData.cs:256-263).

### 1.4 Where URP dithers
- The dither texture is chosen when `cameraData.isDitheringEnabled` is set (URP/Runtime/PostProcess.cs:208, :387). `m_UberPass.Setup(..., isFinalPass: !hasFinalPass, ...)` (:239) means **uber dithers whenever there is no FinalPost**. With FXAA/FSR on, FinalPost dithers instead (:390).
- In both shaders, dither comes *after* the optional manual linear→sRGB step and *before* HDR encoding (UberPost.shader:289-301; FinalPost.shader:109-117). `ApplyDithering` adds TPDF-shaped blue noise in sRGB code space and converts back: `GetSRGBToLinear(GetLinearToSRGB(x) + noise/255)` (URP Common.hlsl:188-206).
- When an `AfterRenderingPostProcessing` pass exists, uber dithers into the **float** `_CameraColorAfterPostProcessing`. Our pass then reshapes that noise, and the real 8-bit quantisation happens later in FinalBlit, which **does not dither**. FinalBlit only sets `_LINEAR_TO_SRGB_CONVERSION`, `_ENABLE_ALPHA_OUTPUT` and the HDR overlay keywords (URP/Runtime/Passes/FinalBlitPass.cs:95, :115, :200).

### 1.5 FinalBlit and sRGB with post off
- FinalBlit samples `resourceData.cameraColor` and writes `resourceData.backBufferColor` (FinalBlitPass.cs:151, :240-246). It picks the nearest-sampling shader pass unless the source RT's `filterMode == Bilinear` (:117).
- `cameraData.requireSrgbConversion` (internal) is `targetTexture == null && Display.main.requiresSrgbBlitToBackbuffer` (URP/Runtime/FrameData/UniversalCameraData.cs:276-287). It is false on Windows DX11/DX12, where the swapchain view is sRGB and the GPU encodes on write. When true, FinalBlit enables the global `_LINEAR_TO_SRGB_CONVERSION` (:200).
- FinalBlit does no tonemapping, clamping beyond what the UNORM write does, or dithering. With post off, HDR values above 1 are simply clipped by the write.
- `backBufferColor` has an **internal** setter (UniversalResourceData.cs:102-106). `cameraColor` has a **public** setter (:126-131). `SwitchActiveTexturesToBackbuffer()` is public (:46-50).

### 1.6 MSAA resolve when reading camera colour
- The intermediate camera colour is created with `bindTextureMS = useTileOnlyMode && msaa>1`, so it is **false** on desktop (URP/Runtime/UniversalRendererRenderGraph.cs:1737). A single camera uses a plain RG texture (:1741-1744).
- In the native pass compiler, when a later pass reads an MSAA resource with `bindMS == false` through `UseTexture`, the last writer's store action becomes `Resolve` or `StoreAndResolve` (CORE/Runtime/RenderGraph/Compiler/NativePassCompiler.cs:1536-1600). Our shader samples a normal resolved `Texture2D`. This is a hardware box resolve in linear HDR, so very bright edges alias. Keep HDR contrast at edges moderate.
- Our own render targets must have `msaaSamples = MSAASamples.None`. `CreateTexture(cameraColor)` would inherit MSAA. URP's internal `PostProcessUtils.MakeCompatible` does exactly this reset (URP/Runtime/PostProcessUtils.cs:110-117), but it is internal.
- `requiresIntermediateTexture = true` on our pass forces the intermediate (URP/Runtime/UniversalRenderer.cs:592-601). On-screen cameras with HDR already need it (:851). Offscreen cameras do not (:848-849).

### 1.7 API facts used by the code
- `RenderGraph.AddRasterRenderPass<T>(string, out T, ProfilingSampler)` (CORE/Runtime/RenderGraph/RenderGraph.cs:1061). Pass data and pass objects come from a pool (:1072-1073).
- `CreateTexture(in TextureDesc)` (:682); `CreateTexture(TextureHandle, string, bool)` copies the descriptor (:714); `GetTextureDesc(in TextureHandle)` (:747).
- Builder methods: `UseTexture(in TextureHandle, AccessFlags = Read)`, `AllowPassCulling`, `AllowGlobalStateModification`, `SetRenderAttachment(TextureHandle, int, AccessFlags = Write)`, `SetRenderFunc<T>(BaseRenderFunc<T, RasterGraphContext>)` (CORE/Runtime/RenderGraph/IRenderGraphBuilder.cs:17, :115, :123, :163, :412).
- `AccessFlags.WriteAll = Write | Discard` sets the load action to DontCare (RenderGraph.cs:31-50).
- `RasterGraphContext.GetTextureUVOrigin(in TextureHandle)` is public (RenderGraph.cs:190-199). URP's `RenderingUtils.GetFinalBlitScaleBias` is **internal** (URP/Runtime/RenderingUtils.cs:868-878). Copy its 4 lines (done in the code below).
- `TextureHandle` converts implicitly to `RTHandle`, `Texture`, `RenderTexture` and `RenderTargetIdentifier`, but **only during execution** (CORE/Runtime/RenderGraph/RenderGraphResourceTexture.cs:131-152).
- The `TextureDesc(int w, int h)` constructor sets `msaaSamples = None` and `slices = 1`. Everything else is zero-initialised, so **`filterMode = Point` and `wrapMode = Repeat`**. Set both explicitly (:387-397, plus the field list :250-338).
- The descriptor hash does **not** include `name` (:503-540). Identical descriptors each frame reuse pooled RTHandles, so there is no GPU allocation.
- `Blitter.BlitTexture(RasterCommandBuffer, RTHandle, Vector4 scaleBias, Material, int pass)` (CORE/Runtime/Utilities/Blitter.cs:793). It writes `_BlitTexture` and `_BlitScaleBias` into one static `MaterialPropertyBlock` (:39, :865-870) and calls `DrawProcedural(..., 3 verts, mpb)` (:348-354). Reusing one MPB across many draws is safe because the MPB is copied when the draw is recorded.
- Extra textures set with `material.SetTexture` are **not** copied this way. That is why URP keeps one material per bloom mip (URP/Runtime/Passes/PostProcess/BloomPostProcessPass.cs:256-266). We put every per-draw value in our own MPB, as `FullScreenPassRendererFeature` does (URP/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs:153-162).
- `RenderGraphUtils` lives in namespace `UnityEngine.Rendering.RenderGraphModule.Util` (CORE/Runtime/RenderGraph/RenderGraphUtilsBlit.cs:6).
  - `BlitMaterialParameters(TextureHandle src, TextureHandle dst, Material mat, int pass)`, with optional overloads taking scale/offset and an MPB (:528-560).
  - `AddBlitPass(this RenderGraph, BlitMaterialParameters, string passName, bool returnBuilder=false)` (:928).
  - It records an **unsafe pass** (:1003), so it cannot merge into native render passes. It binds only the source as `_BlitTexture`. Fine for single-input blits; for two-input passes use raster passes.

## 2. Architecture (answer to c)

**Option A: keep URP post (Bloom on, Tonemapping None) and add ThreadGrade.**
- A1, at `BeforeRenderingPostProcessing`: our tonemap runs before URP bloom thresholds the image, so bloom works on LDR. Uber then re-grades, clamps (LDR) and dithers. Wrong order.
- A2, at `AfterRenderingPostProcessing`: the input is still float, but it has already gone through a forced LUT (LDR: clamped to [0,1] plus an 8-bit LUT; HDR: a 32³ LogC LUT) and been dithered *before* our grade. Final quantisation then happens in FinalBlit without dither, and an R11G11B10 intermediate loses part of a 1/255 dither.
- **Rejected.**

**Option B (chosen): `UniversalAdditionalCameraData.renderPostProcessing = false`, with bloom and grade done in our feature.**
- Camera colour stays HDR float (RGBA16F with `_64Bits`) until our pass. No LUT, clamp or early dither happens. Volumes still update with post off (URP/Runtime/UniversalRenderPipeline.cs:1416-1449), so a custom `VolumeComponent` can drive ThreadGrade if needed.
- **B1 (default): write to `cameraColor`.** Create `R8G8B8A8_SRGB` (non-MSAA, Point) and write linear values with the dither already in sRGB code space; the GPU encodes. Then set `resourceData.cameraColor = out`. FinalBlit copies it 1:1 to the backbuffer (an sRGB decode/encode round trip of 8-bit codes is exact). It handles y-flip (`GetTextureUVOrigin`), `requiresSrgbBlitToBackbuffer`, viewport, Recorder `CapturePass`, which reads `activeColorTexture` (URP/Runtime/Passes/CapturePass.cs:43-48), and the overlay UI. Cost: about 0.05 ms for one 1080p 8-bit copy.
- **B2 (optional): write directly.** Set `dst = resourceData.backBufferColor`, use `SetRenderAttachment(dst, 0, AccessFlags.WriteAll)`, then call `resourceData.SwitchActiveTexturesToBackbuffer()`, which makes URP skip FinalBlit. You must:
  1. Flip y when `ctx.GetTextureUVOrigin(src) != ctx.GetTextureUVOrigin(dst)`. The real backbuffer is TopLeft on DX/Vulkan/Metal (URP/Runtime/RenderingUtils.cs:885-899).
  2. Encode sRGB manually when `Display.main.requiresSrgbBlitToBackbuffer`, or for UNorm non-sRGB target textures.
  3. Only do this when `cameraData.resolveFinalTarget && cameraData.isDefaultViewport && cameraData.captureActions == null`.

  Never call `GetTextureDesc(backBufferColor).format`: it returns the HDR proxy format, not the real swapchain format.

## 3. C#: feature and pass (put in `Assets/_Game/Rendering/ThreadGradeFeature.cs`)

```csharp
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Thread Grade")]
public sealed class ThreadGradeFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public float exposureEV = 0f;
        [Range(0.5f, 2f)] public float contrast = 1f;      // log2 contrast around 0.18
        [Range(0f, 2f)]   public float saturation = 1f;
        [Range(0f, 1.5f)] public float vignetteIntensity = 0.3f;
        [Range(0.2f, 5f)] public float vignetteSmoothness = 2f;
        [Range(0.5f, 2f)] public float vignetteRoundness = 1f;
        public Color vignetteColor = Color.black;
        public float bloomThreshold = 1.0f;                 // linear HDR
        [Range(0f, 1f)] public float bloomKnee = 0.5f;
        [Range(0f, 1f)] public float bloomScatter = 0.7f;   // URP mapping: lerp(0.05, 0.95, s)
        public float bloomIntensity = 0.15f;
        public float bloomClamp = 64f;
        [Range(1, 8)] public int bloomMaxLevels = 6;
        [Range(0f, 2f)] public float ditherLsb = 1f;        // TPDF amplitude in 8-bit LSB
        public bool writeBackbufferDirectly = false;        // Option B2
    }

    public Settings settings = new Settings();
    [SerializeField] Shader m_Shader; // assigned by editor setup -> guarantees inclusion in the player build
    public Shader shader { get => m_Shader; set { m_Shader = value; Create(); } }

    ThreadGradePass m_Pass;

    public override void Create()
    {
        m_Pass?.Dispose();
        m_Pass = m_Shader != null ? new ThreadGradePass(m_Shader) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing } : null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null || !m_Pass.isValid) return;
        var t = renderingData.cameraData.cameraType;
        if (t == CameraType.Preview || t == CameraType.Reflection) return;
        m_Pass.settings = settings;
        m_Pass.requiresIntermediateTexture = true;   // forces HDR cameraColor (UniversalRenderer.cs:592-601)
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing) { m_Pass?.Dispose(); m_Pass = null; }
}

sealed class ThreadGradePass : ScriptableRenderPass
{
    const int kMaxLevels = 8;
    const int kPrefilter = 0, kDown = 1, kUp = 2, kGrade = 3;   // shader pass indices

    static readonly int s_BlitTexture   = Shader.PropertyToID("_BlitTexture");
    static readonly int s_BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
    static readonly int s_LowMip        = Shader.PropertyToID("_SourceTexLowMip");
    static readonly int s_BloomTex      = Shader.PropertyToID("_ThreadBloomTex");
    static readonly int s_P0 = Shader.PropertyToID("_ThreadP0"), s_P1 = Shader.PropertyToID("_ThreadP1"),
                        s_P2 = Shader.PropertyToID("_ThreadP2"), s_P3 = Shader.PropertyToID("_ThreadP3");

    static readonly string[] s_DownNames = new string[kMaxLevels], s_UpNames = new string[kMaxLevels];
    static readonly ProfilingSampler s_PrefilterSampler = new ProfilingSampler("ThreadBloom Prefilter");
    static readonly ProfilingSampler s_DownSampler      = new ProfilingSampler("ThreadBloom Down");
    static readonly ProfilingSampler s_UpSampler        = new ProfilingSampler("ThreadBloom Up");
    static readonly ProfilingSampler s_GradeSampler     = new ProfilingSampler("ThreadGrade");

    static ThreadGradePass()
    {
        for (int i = 0; i < kMaxLevels; i++) { s_DownNames[i] = "_ThreadBloomDown" + i; s_UpNames[i] = "_ThreadBloomUp" + i; }
    }

    public ThreadGradeFeature.Settings settings;
    readonly Material m_Material;
    readonly MaterialPropertyBlock m_Mpb = new MaterialPropertyBlock();     // copied at record time per draw
    readonly TextureHandle[] m_Down = new TextureHandle[kMaxLevels];
    readonly TextureHandle[] m_Up   = new TextureHandle[kMaxLevels];

    public bool isValid => m_Material != null;

    public ThreadGradePass(Shader shader)
    {
        profilingSampler = s_GradeSampler;
        m_Material = CoreUtils.CreateEngineMaterial(shader);
    }

    public void Dispose() => CoreUtils.Destroy(m_Material);

    sealed class PassData
    {
        public TextureHandle src, src2, dst;
        public int src2Id, shaderPass;
        public Material material;
        public MaterialPropertyBlock mpb;
        public Vector4 p0, p1, p2, p3;
    }

    static void ExecuteBlit(PassData d, RasterGraphContext ctx)
    {
        // Same math as internal RenderingUtils.GetFinalBlitScaleBias (RenderingUtils.cs:868-878)
        RTHandle srcRt = d.src;
        Vector2 scale = (srcRt != null && srcRt.useScaling)
            ? new Vector2(srcRt.rtHandleProperties.rtHandleScale.x, srcRt.rtHandleProperties.rtHandleScale.y) : Vector2.one;
        bool yflip = ctx.GetTextureUVOrigin(in d.src) != ctx.GetTextureUVOrigin(in d.dst);
        Vector4 scaleBias = yflip ? new Vector4(scale.x, -scale.y, 0f, scale.y) : new Vector4(scale.x, scale.y, 0f, 0f);

        var mpb = d.mpb;
        mpb.Clear();
        mpb.SetTexture(s_BlitTexture, (Texture)d.src);
        if (d.src2.IsValid()) mpb.SetTexture(d.src2Id, (Texture)d.src2);
        mpb.SetVector(s_BlitScaleBias, scaleBias);
        mpb.SetVector(s_P0, d.p0); mpb.SetVector(s_P1, d.p1); mpb.SetVector(s_P2, d.p2); mpb.SetVector(s_P3, d.p3);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, mpb);
    }

    void AddPass(RenderGraph rg, string name, ProfilingSampler sampler, int shaderPass,
                 TextureHandle src, TextureHandle src2, int src2Id, TextureHandle dst,
                 Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3)
    {
        using (var builder = rg.AddRasterRenderPass<PassData>(name, out var d, sampler))
        {
            d.src = src; d.src2 = src2; d.src2Id = src2Id; d.dst = dst;
            d.material = m_Material; d.mpb = m_Mpb; d.shaderPass = shaderPass;
            d.p0 = p0; d.p1 = p1; d.p2 = p2; d.p3 = p3;
            builder.UseTexture(src, AccessFlags.Read);            // MSAA cameraColor is auto-resolved here
            if (src2.IsValid()) builder.UseTexture(src2, AccessFlags.Read);
            builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);   // full-screen: DontCare load
            builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) => ExecuteBlit(data, ctx));
        }
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();
        var s = settings;
        if (s == null || resourceData.isActiveTargetBackBuffer) return; // no intermediate to sample
        if (cameraData.postProcessEnabled) return;   // Option B contract: URP post OFF (else double grade)
        if (cameraData.isHDROutputActive) return;    // SDR grade only

        TextureHandle src = resourceData.cameraColor;              // RGBA16F (with _64Bits), maybe MSAA
        TextureDesc srcDesc = renderGraph.GetTextureDesc(src);

        // ---- bloom pyramid: same sizing rule as URP (BloomPostProcessPass.cs:166-193) ----
        int w = Mathf.Max(1, srcDesc.width >> 1), h = Mathf.Max(1, srcDesc.height >> 1);
        int levels = Mathf.FloorToInt(Mathf.Log(Mathf.Max(w, h), 2f) - 1f);
        levels = Mathf.Clamp(levels, 1, Mathf.Min(s.bloomMaxLevels, kMaxLevels));
        var desc = new TextureDesc(w, h)
        {
            format = GraphicsFormat.R16G16B16A16_SFloat,
            filterMode = FilterMode.Bilinear,          // ctor default would be Point
            wrapMode = TextureWrapMode.Clamp,          // ctor default would be Repeat
            msaaSamples = MSAASamples.None,
            clearBuffer = false,
        };
        for (int i = 0; i < levels; i++)
        {
            desc.width = w; desc.height = h;
            desc.name = s_DownNames[i]; m_Down[i] = renderGraph.CreateTexture(desc);
            if (i < levels - 1) { desc.name = s_UpNames[i]; m_Up[i] = renderGraph.CreateTexture(desc); }
            w = Mathf.Max(1, w >> 1); h = Mathf.Max(1, h >> 1);
        }

        float thr = Mathf.Max(0f, s.bloomThreshold);
        var bp = new Vector4(thr, Mathf.Max(1e-4f, thr * s.bloomKnee), s.bloomClamp, Mathf.Lerp(0.05f, 0.95f, s.bloomScatter));
        var none = TextureHandle.nullHandle;
        AddPass(renderGraph, "ThreadBloomPrefilter", s_PrefilterSampler, kPrefilter, src, none, 0, m_Down[0], bp, default, default, default);
        for (int i = 1; i < levels; i++)
            AddPass(renderGraph, "ThreadBloomDown", s_DownSampler, kDown, m_Down[i - 1], none, 0, m_Down[i], bp, default, default, default);
        TextureHandle bloom = m_Down[levels - 1];
        for (int i = levels - 2; i >= 0; i--)
        {
            AddPass(renderGraph, "ThreadBloomUp", s_UpSampler, kUp, m_Down[i], bloom, s_LowMip, m_Up[i], bp, default, default, default);
            bloom = m_Up[i];
        }

        // ---- grade + output ----
        bool finalTarget = cameraData.resolveFinalTarget;
        bool direct = s.writeBackbufferDirectly && finalTarget && cameraData.isDefaultViewport && cameraData.captureActions == null;
        float manualSrgb = 0f;
        if (direct)
        {
            var tt = cameraData.targetTexture;
            bool manual = tt == null ? Display.main.requiresSrgbBlitToBackbuffer
                                     : (!GraphicsFormatUtility.IsSRGBFormat(tt.graphicsFormat) && GraphicsFormatUtility.IsUNormFormat(tt.graphicsFormat));
            manualSrgb = manual ? 1f : 0f;
        }
        var p0 = new Vector4(Mathf.Pow(2f, s.exposureEV), s.contrast, s.saturation, s.bloomIntensity);
        var p1 = new Vector4(s.vignetteIntensity, s.vignetteSmoothness, s.vignetteRoundness, finalTarget ? s.ditherLsb : 0f);
        var p2 = new Vector4(Time.frameCount & 0xFFFF, manualSrgb, 0f, 0f);
        Color vc = s.vignetteColor.linear;
        var p3 = new Vector4(vc.r, vc.g, vc.b, 1f);

        TextureHandle dst;
        if (direct) dst = resourceData.backBufferColor;
        else
        {
            var od = new TextureDesc(srcDesc.width, srcDesc.height)
            {
                format = finalTarget ? GraphicsFormat.R8G8B8A8_SRGB : srcDesc.format, // 8-bit quantisation happens HERE
                filterMode = FilterMode.Point,     // FinalBlit then uses its nearest pass (FinalBlitPass.cs:117)
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                name = "_ThreadGradeOut",
            };
            dst = renderGraph.CreateTexture(od);
        }
        AddPass(renderGraph, "ThreadGrade", s_GradeSampler, kGrade, src, bloom, s_BloomTex, dst, p0, p1, p2, p3);

        if (direct) resourceData.SwitchActiveTexturesToBackbuffer(); // URP skips FinalBlit (URRG.cs:1489)
        else        resourceData.cameraColor = dst;                  // FinalBlit copies to screen
    }
}
```

How this stays allocation-free per frame:
- Handle arrays, names, samplers and property IDs are allocated once.
- `PassData` is pooled by RG, and the lambdas are `static`, so there are no closures.
- One MPB is reused. It is cleared per draw and copied at record time.
- No LINQ, string concatenation or `Shader.Find` runs per frame.
- RG textures hit the RTHandle pool because the descriptor hash is stable and ignores the name.
- Check with the Profiler (GC Alloc column = 0 B for "ThreadGrade") and the Render Graph Viewer (Window > Analysis).

## 4. HLSL: `Assets/_Game/Rendering/ThreadGrade.shader` (answer to d and e)

```hlsl
Shader "Hidden/Thread/Grade"
{
    HLSLINCLUDE
    #pragma target 4.5
    // Include order mirrors URP's own Bloom.shader:3-11
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    // Blit.hlsl gives: TEXTURE2D_X(_BlitTexture), float4 _BlitScaleBias, _BlitTexture_TexelSize,
    // struct Attributes{vertexID}, struct Varyings{positionCS:SV_POSITION, texcoord:TEXCOORD0}, Varyings Vert(Attributes)
    // and (via GlobalSamplers.hlsl) sampler_LinearClamp / sampler_PointClamp; Color.hlsl -> NeutralTonemap, LinearToSRGB.

    TEXTURE2D_X(_SourceTexLowMip); float4 _SourceTexLowMip_TexelSize;
    TEXTURE2D_X(_ThreadBloomTex);  float4 _ThreadBloomTex_TexelSize;
    float4 _ThreadP0; // bloom passes: x threshold, y knee, z clamp, w scatter | grade: x 2^EV, y contrast, z saturation, w bloom intensity
    float4 _ThreadP1; // grade: x vignette intensity, y smoothness, z roundness, w dither LSB
    float4 _ThreadP2; // grade: x frame index, y manual sRGB encode (0/1)
    float4 _ThreadP3; // grade: rgb vignette colour (linear)

    float3 Fetch(float2 uv) { return min(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb, 65000.0); }
    float  Luma(float3 c)   { return dot(c, float3(0.2126729, 0.7151522, 0.0721750)); }
    float  KarisW(float3 c) { return 1.0 / (1.0 + Luma(c)); }

    // 13-tap 2x downsample (Jimenez, SIGGRAPH 2014). t = SOURCE texel size; covers 6x6 source texels.
    float3 Down13(float2 uv, float2 t, bool karis)
    {
        float3 a = Fetch(uv + t * float2(-2,  2)), b = Fetch(uv + t * float2(0,  2)), c = Fetch(uv + t * float2(2,  2));
        float3 d = Fetch(uv + t * float2(-2,  0)), e = Fetch(uv),                     f = Fetch(uv + t * float2(2,  0));
        float3 g = Fetch(uv + t * float2(-2, -2)), h = Fetch(uv + t * float2(0, -2)), i = Fetch(uv + t * float2(2, -2));
        float3 j = Fetch(uv + t * float2(-1,  1)), k = Fetch(uv + t * float2(1,  1));
        float3 l = Fetch(uv + t * float2(-1, -1)), m = Fetch(uv + t * float2(1, -1));
        if (!karis)
            return e * 0.125 + (a + c + g + i) * 0.03125 + (b + d + f + h) * 0.0625 + (j + k + l + m) * 0.125;
        // first mip only: partial Karis average (suppresses fireflies from the thin bright cable/sun glints)
        float3 b0 = (j + k + l + m) * 0.25, b1 = (a + b + d + e) * 0.25, b2 = (b + c + e + f) * 0.25;
        float3 b3 = (d + e + g + h) * 0.25, b4 = (e + f + h + i) * 0.25;
        float w0 = 0.5 * KarisW(b0), w1 = 0.125 * KarisW(b1), w2 = 0.125 * KarisW(b2), w3 = 0.125 * KarisW(b3), w4 = 0.125 * KarisW(b4);
        return (b0 * w0 + b1 * w1 + b2 * w2 + b3 * w3 + b4 * w4) / (w0 + w1 + w2 + w3 + w4);
    }

    float4 FragPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float3 c = min(Down13(input.texcoord, _BlitTexture_TexelSize.xy, true), _ThreadP0.z);
        // URP soft-knee threshold (URP Bloom.shader:108-115)
        float br = Max3(c.r, c.g, c.b);
        float soft = clamp(br - _ThreadP0.x + _ThreadP0.y, 0.0, 2.0 * _ThreadP0.y);
        soft = soft * soft / (4.0 * _ThreadP0.y + 1e-4);
        c *= max(br - _ThreadP0.x, soft) / max(br, 1e-4);
        return float4(max(c, 0.0), 1.0);
    }

    float4 FragDown(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(Down13(input.texcoord, _BlitTexture_TexelSize.xy, false), 1.0);
    }

    float3 LowTap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_SourceTexLowMip, sampler_LinearClamp, uv, 0).rgb; }

    float4 FragUp(Varyings input) : SV_Target   // _BlitTexture = mipDown[i] (high), _SourceTexLowMip = coarser result
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord, t = _SourceTexLowMip_TexelSize.xy;
        float3 lo = LowTap(uv) * 4.0
                  + (LowTap(uv + float2(-t.x, 0)) + LowTap(uv + float2(t.x, 0)) + LowTap(uv + float2(0, -t.y)) + LowTap(uv + float2(0, t.y))) * 2.0
                  + (LowTap(uv + float2(-t.x, -t.y)) + LowTap(uv + float2(t.x, -t.y)) + LowTap(uv + float2(-t.x, t.y)) + LowTap(uv + float2(t.x, t.y)));
        lo *= 1.0 / 16.0;                                   // 3x3 tent
        float3 hi = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
        return float4(lerp(hi, lo, _ThreadP0.w), 1.0);      // URP "scatter" upsample (Bloom.shader Upsample())
    }

    // PCG3D hash (Jarzynski & Olano, JCGT 2020) -> two 16-bit uniforms per channel -> TPDF on (-1, 1)
    float3 TPDF3(uint2 p, uint frame)
    {
        uint3 v = uint3(p, frame) * 1664525u + 1013904223u;
        v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
        v ^= v >> 16u;
        v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
        float3 u1 = float3(v & 0xFFFFu) * (1.0 / 65536.0);
        float3 u2 = float3(v >> 16u)    * (1.0 / 65536.0);
        return u1 - u2;
    }

    float4 FragGrade(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;   // y-flip (if any) already folded in via _BlitScaleBias
        float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
        // half-res bloom: bicubic like URP UberPost (_BLOOM_HQ) to avoid blocky diamonds
        float3 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_ThreadBloomTex, sampler_LinearClamp), uv,
                                              _ThreadBloomTex_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex).rgb;
        c += bloom * _ThreadP0.w;                 // bloom before exposure (URP order: bloom, then ApplyColorGrading)
        c *= _ThreadP0.x;                         // exposure
        const float kMid = 0.18;
        c = kMid * exp2((log2(max(c, 1e-7)) - log2(kMid)) * _ThreadP0.y);   // contrast (HDR, log2 space)
        float l = Luma(c);
        c = max(l + (c - l) * _ThreadP0.z, 0.0);  // saturation, luma-preserving
        c = NeutralTonemap(c);                    // CORE Color.hlsl:535
        float2 dv = abs(uv - 0.5) * _ThreadP1.x;  dv.x *= _ThreadP1.z;      // URP ApplyVignette form
        float vf = pow(saturate(1.0 - dot(dv, dv)), _ThreadP1.y);
        c *= lerp(_ThreadP3.rgb, (1.0).xxx, vf);
        // TPDF dither in sRGB code space, immediately before the 8-bit write (same domain as URP ApplyDithering)
        float3 s = LinearToSRGB(saturate(c));
        s += TPDF3((uint2)input.positionCS.xy, (uint)_ThreadP2.x) * (_ThreadP1.w / 255.0);
        float3 o = (_ThreadP2.y > 0.5) ? s : SRGBToLinear(s);   // sRGB target: GPU re-encodes linear -> exact codes
        return float4(o, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass { Name "ThreadBloomPrefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL }
        Pass { Name "ThreadBloomDown"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
            ENDHLSL }
        Pass { Name "ThreadBloomUp"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUp
            ENDHLSL }
        Pass { Name "ThreadGrade"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragGrade
            ENDHLSL }
    }
}
```

### 4.1 Exact core HLSL referenced (CORE/ShaderLibrary/Color.hlsl:525-562)
```hlsl
real3 NeutralCurve(float3 x, real a, real b, real c, real d, real e, real f)
{
    return real3(((x * (a * x + c * b) + d * e) / (x * (a * x + b) + d * f)) - e / f);
}
#define TONEMAPPING_CLAMP_MAX 435.18712
real3 NeutralTonemap(real3 x)
{
    x = max((0.0).xxx, x);
    const real a = 0.2;  const real b = 0.29; const real c = 0.24;
    const real d = 0.272; const real e = 0.02; const real f = 0.3;
    const real whiteLevel = 5.3; const real whiteClip = 1.0;
#if REAL_IS_HALF
    x = min(x, TONEMAPPING_CLAMP_MAX);
#endif
    real3 whiteScale = (1.0).xxx / NeutralCurve(whiteLevel, a, b, c, d, e, f);
    x = NeutralCurve(x * whiteScale, a, b, c, d, e, f);
    x *= whiteScale;
    x /= whiteClip.xxx;
    return x;
}
```
- Input 5.3 maps to 1.0. Linear 0.18 maps to about 0.17, so midtones barely change.
- Per-channel curves desaturate bright colours slightly. That suits flat colour.
- `LinearToSRGB` uses `c <= 0.0031308 ? c*12.9232102 : 1.055*pow(c,1/2.4)-0.055`. `SRGBToLinear` is the inverse (Color.hlsl:77-106).

### 4.2 Blit.hlsl contract (CORE/Runtime/Utilities/Blit.hlsl:1-53)
- Include path: `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`. Put URP `Core.hlsl` before it so `TEXTURE2D_X` and XR macros exist.
- `Vert` builds a full-screen triangle from `SV_VertexID` (`GetFullScreenTriangleVertexPosition` / `GetFullScreenTriangleTexCoord`; the latter flips v when `UNITY_UV_STARTS_AT_TOP`, CORE/ShaderLibrary/Common.hlsl:1602-1609). It outputs `texcoord = uv * _BlitScaleBias.xy + _BlitScaleBias.zw` (`DYNAMIC_SCALING_APPLY_SCALEBIAS`, CORE/ShaderLibrary/DynamicScaling.hlsl:14). You must therefore always set `_BlitScaleBias`; the code does, including the y-flip.
- `_BlitTexture` is declared `TEXTURE2D_X` (`TEXTURE2D_X_FLOAT` if `USE_FULL_PRECISION_BLIT_TEXTURE`). Samplers: `sampler_LinearClamp` and `sampler_PointClamp` (CORE/ShaderLibrary/GlobalSamplers.hlsl:7-8).
- `{name}_TexelSize` is filled for textures bound through an MPB. URP's Bloom relies on `_BlitTexture_TexelSize` bound via Blitter's MPB.

## 5. Editor setup (answer to f): `Assets/_Game/Editor/ThreadPipelineSetup.cs`

Public settable properties in 17.6:
- On the asset: `supportsHDR` (UniversalRenderPipelineAsset.cs:1087), `hdrColorBufferPrecision` (:1096), `msaaSampleCount` (int cast to `MsaaQuality`: 1/2/4/8, :1106), `renderScale` (:1115), `mainLightShadowmapResolution` (int cast to `ShadowResolution`: 256..8192, :1329), `shadowDistance` (:1472), `shadowCascadeCount` (throws outside 1..4, :1481-1491), `cascade2Split` / `cascade3Split` / `cascade4Split` (fractions of shadowDistance, :1498-1521), `cascadeBorder`, `shadowDepthBias` and `shadowNormalBias` (:1527-1549).
- On the renderer data: `renderingMode` and `depthPrimingMode` (both call `SetDirty()`, URP/Runtime/UniversalRendererData.cs:237-258), and `intermediateTextureMode` (:340-349).

**Internal setters** that need a `SerializedObject`:
- `supportsMainLightShadows` → `m_MainLightShadowsSupported`; also `m_AnyShadowsSupported`, which is editor-only (:1314-1325, :539-541).
- `supportsSoftShadows` → `m_SoftShadowsSupported` (:1554-1558).
- `softShadowQuality`, which is an internal property → `m_SoftShadowQuality` (`SoftShadowQuality`: UsePipelineSettings=0, Low=1, Medium=2, High=3) (:1563-1567, :41-61).
- `mainLightRenderingMode` → `m_MainLightRenderingMode`.

Enum values: `RenderingMode.Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3` and `DepthPrimingMode.Disabled/Auto/Forced` (URP/Runtime/UniversalRenderer.cs:11-36). The renderer feature list `rendererFeatures` is a public `List<>` getter, but the editor also keeps `m_RendererFeatureMap` (local file IDs) in sync (URP/Editor/ScriptableRendererDataEditor.cs:270-306), so the snippet below mirrors that.

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ThreadPipelineSetup
{
    const string kAsset = "Assets/Settings/PC_RPAsset.asset";   // Standalone default quality = "PC" (QualitySettings.asset)

    [MenuItem("Thread/Setup URP Pipeline")]
    public static void Apply()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(kAsset);
        asset.supportsHDR = true;
        asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;  // RGBA16F camera colour
        asset.msaaSampleCount = 4;
        asset.renderScale = 1f;
        asset.supportsCameraDepthTexture = false;
        asset.supportsCameraOpaqueTexture = false;
        asset.shadowDistance = 400f;
        asset.shadowCascadeCount = 4;
        asset.cascade4Split = new Vector3(0.03f, 0.10f, 0.30f);       // normalised fractions of shadowDistance
        asset.cascadeBorder = 0.1f;
        asset.mainLightShadowmapResolution = 4096;
        asset.shadowDepthBias = 1f; asset.shadowNormalBias = 1f;

        var so = new SerializedObject(asset);                          // internal setters
        so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        so.FindProperty("m_AnyShadowsSupported").boolValue = true;     // editor-only mirror field
        so.FindProperty("m_SoftShadowsSupported").boolValue = true;
        so.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.High;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);

        var rd = (UniversalRendererData)asset.rendererDataList[0];    // ReadOnlySpan<ScriptableRendererData>
        rd.renderingMode = RenderingMode.ForwardPlus;
        rd.depthPrimingMode = DepthPrimingMode.Disabled;               // see pitfalls
        var grade = AddFeature<ThreadGradeFeature>(rd);
        grade.shader = Shader.Find("Hidden/Thread/Grade");             // serialized ref => shader ships in build
        EditorUtility.SetDirty(grade);
        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();

        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.allowHDRDisplaySupport = false;                 // keep SDR swapchain (grade is SDR)
    }

    public static T AddFeature<T>(ScriptableRendererData rd) where T : ScriptableRendererFeature
    {
        foreach (var f in rd.rendererFeatures) if (f is T existing) return existing;
        var feature = ScriptableObject.CreateInstance<T>();
        feature.name = typeof(T).Name;
        feature.hideFlags |= HideFlags.HideInHierarchy;               // as URP's editor does
        AssetDatabase.AddObjectToAsset(feature, rd);                   // sub-asset of the renderer
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
        var so = new SerializedObject(rd);
        var list = so.FindProperty("m_RendererFeatures");
        var map  = so.FindProperty("m_RendererFeatureMap");
        list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;  map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        rd.SetDirty();                         // ScriptableRendererData.SetDirty(): rebuild renderer (ScriptableRendererData.cs:59-62)
        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
        return feature;
    }

    public static void ConfigureCamera(Camera cam)   // Option B camera contract
    {
        cam.allowHDR = true; cam.allowMSAA = true;
        var acd = cam.GetUniversalAdditionalCameraData();
        acd.renderPostProcessing = false;              // UniversalRenderPipeline.cs:1884
        acd.antialiasing = AntialiasingMode.None;      // FXAA/SMAA/TAA live inside URP post
        EditorUtility.SetDirty(cam); EditorUtility.SetDirty(acd);
    }
}
#endif
```

Current project state, read-only:
- `PC_RPAsset.asset:26-28`: HDR on, precision 0 (`_32Bits`), MSAA 1 (off). This needs the change above.
- `PC_Renderer.asset:56-57`: already Forward+ (2), depth priming Disabled.
- `PC_Renderer.asset:36-37`: already contains a **ScreenSpaceAmbientOcclusion** feature. Decide on purpose whether flat shading wants SSAO; it adds grey contact gradients.
- Standalone uses quality level 1 "PC" (`ProjectSettings/QualitySettings.asset` `m_PerPlatformDefaultQuality: Standalone: 1`), and `GraphicsSettings.m_CustomRenderPipeline` is empty. Edit `PC_RPAsset`, or point every quality level at it.

## 6. Pitfalls

1. **Writing `Execute()` gives a silent no-op.** The base `RecordRenderGraph` only logs a warning (ScriptableRenderPass.cs:311-314).
2. **`TextureDesc(w, h)` defaults to Point + Repeat.** Bloom then looks blocky and edges wrap. Always set Bilinear + Clamp.
3. **Never `renderGraph.CreateTexture(resourceData.cameraColor)` for an output.** It inherits MSAA and wastes a full-screen MSAA target. Copy the descriptor and set `msaaSamples = None`.
4. **Material state set in a render func is not snapshotted.** Use an MPB per draw, as here, or one material per draw (BloomPostProcessPass.cs:261-266).
5. **Do not sample `backBufferColor`.** If `isActiveTargetBackBuffer` is true at your event, return early. `requiresIntermediateTexture = true` prevents this for our pass.
6. **Camera `targetTexture` decides the intermediate format** (UniversalRenderPipelineCore.cs:1604). An 8-bit capture RT clamps HDR before the grade. Capture frame sequences in the **built player** with `ScreenCapture.CaptureScreenshotIntoRenderTexture` or `ScreenCapture.CaptureScreenshotAsTexture` after `WaitForEndOfFrame`. Alternatively render to an `R16G16B16A16_SFloat` target, set `writeBackbufferDirectly` so the grade writes into it, then sRGB-encode and round on the CPU.
7. **The R11G11B10 blue channel has a 5-bit mantissa** (about 3.1% steps). One 8-bit sRGB step at mid-grey is about 1.7% in linear, so the default `_32Bits` bands the sky's blue even with dither. RGBA16F steps are about 0.1%. Use `_64Bits`.
8. **Leaving URP post enabled** on the camera makes uber clamp (LDR grading), LUT and dither before us. The pass guards on `cameraData.postProcessEnabled`. In the Scene view, post follows the Scene-view "Post Processing" toggle (`CoreUtils.ArePostProcessesEnabled`). Leave it off so the scene view shows ThreadGrade.
9. **HDR display output** (`PlayerSettings.allowHDRDisplaySupport`) switches the swapchain to 10-bit or FP16 and `isHDROutputActive`. The SDR grade would be wrong there, so keep it off.
10. **Dither domain matters.** Add TPDF noise in sRGB code units (1 LSB = 1/255), then convert back to linear for an sRGB target, exactly as URP's `ApplyDithering` does. Noise added in linear space is far too strong in the darks and too weak in the highlights.
    - Animate the seed with `Time.frameCount`.
    - Use `ditherLsb` 1.0 by default. YouTube re-encoding (8-bit 4:2:0) can still re-band smooth gradients. Consider 1.5 LSB for recordings, and upload at 1440p+ (higher-bitrate VP9/AV1).
11. **MSAA resolve averages linear HDR.** A thin, very bright line next to a dark sky resolves with jaggies or ringing. Keep emissive values near 1–3, and give the hair-thin cable a minimum screen-space width (for example ≥1.5 px in its vertex shader) so it neither shimmers nor drives a flickering bloom. The first bloom mip uses a Karis average for this reason.
12. **Shader inclusion.** `Hidden/` shaders are stripped from players unless referenced. The feature keeps a serialized `Shader` reference that the setup script assigns. `Shader.Find` at runtime in the player returns null otherwise.
13. **Depth priming is silently ignored when MSAA > 1.** `IsDepthPrimingEnabledRenderGraph` requires `msaaSamples == 1`, no deferred lighting, and not WebGL (UniversalRendererRenderGraph.cs:1639-1647). With MSAA 4x, setting Auto or Forced does nothing, so leave it Disabled. TAA is likewise disabled under MSAA and with post off (UniversalCameraData.cs:459-470).
14. **`AddBlitPass` creates an unsafe pass** (RenderGraphUtilsBlit.cs:1003). It is fine for single-input copies, but the raster passes above let the native pass compiler pick DontCare loads (`WriteAll`) and show clean names in the Render Graph Viewer.
15. **Camera stacking is unsupported in this design.** With `resolveFinalTarget == false`, the code writes a float `_ThreadGradeOut` without dither. URP expects persistent A/B attachments for stacks (URRG.cs:1467-1468 TODO), so keep a single Base camera.
16. **`UniversalCameraData.pixelWidth` and `pixelRect` are internal.** Use texture descriptors, `cameraTargetDescriptor` or `cameraData.camera.pixelRect`.

## 7. Sources (accessed 2026-10-29)

| URL | Used for |
|---|---|
| https://docs.unity3d.com/6000.4/Documentation/Manual/UpgradeGuideUnity64.html | Confirms "URP Compatibility Mode is now fully removed for custom render passes" and `URP_COMPATIBILITY_MODE` removed |
| https://docs.unity.cn/6000.6/Documentation/Manual/urp/customize/blit-overview.html | 6000.6 guidance: use `AddBlitPass` / `Blitter`; hand-coded shaders only; avoid `cmd.Blit` / `Graphics.Blit` |
| https://docs.unity3d.com/kr/6000.0/Manual/urp/renderer-features/how-to-fullscreen-blit.html | Official pattern: early-out on `isActiveTargetBackBuffer`, then `resourceData.cameraColor = destination` |
| https://docs.unity.com/en-us/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/effect-list/tonemapping.md | Neutral tonemapper intent (range remap with minimal hue and saturation change) |
| https://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare/ | Jimenez 2014 bloom: 13-tap downsample pyramid and Karis average on the first mip |
| https://gdcvault.com/play/1023002/Low-Complexity-High-Fidelity-INSIDE | Gjøl (Playdead) dithering / banding talk: TPDF dither before quantisation |
| https://lists.blender.org/pipermail/bf-blender-cvs/2020-February/135857.html | Secondary reference that cites Gjøl's "Banding in Games" slide 27 on triangular noise |
| https://www.jcgt.org/published/0009/03/02/paper.pdf | Jarzynski & Olano 2020, "Hash Functions for GPU Rendering" (PCG3D) |
| https://aras-p.info/blog/2025/06/13/Voronoi-Hashing-and-OSL/ | Cross-check of the PCG3D constants (1664525u, 1013904223u) and mixing steps in HLSL form |
