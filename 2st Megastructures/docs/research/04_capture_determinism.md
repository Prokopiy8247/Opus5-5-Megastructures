# 04 - Deterministic screenshot / frame-sequence capture (Unity 6000.6.0f1, URP 17.6.0, Windows)

Scope: capturing a URP camera into a RenderTexture of any size (for example 3840x2160 on a smaller monitor) with MSAA 4x, full post-processing and a FullScreenPassRendererFeature at AfterRenderingPostProcessing; readback; time control; player command line; Editor batchmode rendering; ScreenCapture parity.

Path abbreviations:
- `URP/` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184/`
- `CORE/` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e/`
- `XML/` = `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Managed/UnityEngine/`

Tags: VERIFIED-SOURCE (path:line), VERIFIED-DOCS (URL), UNVERIFIED (reasoned or community knowledge; smoke-test it).

---

## 0. Current project facts that matter for capture

- Linear color space: `m_ActiveColorSpace: 1`. VERIFIED-SOURCE `ProjectSettings/ProjectSettings.asset:49`
- Windows graphics APIs: manual list `[Direct3D11 (2), Direct3D12 (18)]`, so DX11 is primary. VERIFIED-SOURCE `ProjectSettings/ProjectSettings.asset:542-544`
- `runInBackground: 0`, so the player pauses when it loses focus. Set `Application.runInBackground = true` in capture mode. VERIFIED-SOURCE `ProjectSettings.asset:90`
- `useHDRDisplay: 0`, so the screen output is SDR. VERIFIED-SOURCE `ProjectSettings.asset:163`
- The default Player.log is at `%USERPROFILE%\AppData\LocalLow\DefaultCompany\2nd Megastructures\Player.log`. The company and product names come from `ProjectSettings.asset:15-16`; the folder pattern is UNVERIFIED.
- PC_RPAsset settings (VERIFIED-SOURCE `Assets/Settings/PC_RPAsset.asset:26-28,85,87,93`):
  - `m_SupportsHDR: 1`
  - `m_HDRColorBufferPrecision: 0` (32 bits, which gives a B10G11R11 intermediate)
  - `m_MSAA: 1` (MSAA is currently OFF; set it to 4 for screen parity)
  - GPU Resident Drawer is on and its occlusion culling is off
  - `m_VolumeFrameworkUpdateMode: 0` (EveryFrame)
- PC_Renderer: `m_RenderingMode: 2` (Forward+), so MSAA is allowed. VERIFIED-SOURCE `Assets/Settings/PC_Renderer.asset:56`
- QualitySettings PC level: `vSyncCount: 0`, `antiAliasing: 0`. VERIFIED-SOURCE `ProjectSettings/QualitySettings.asset:79,86`

---

## 1. Rendering a URP camera into an arbitrary-size RT

### 1.1 Render-request API (ground truth in URP 17.6)

- `UniversalRenderPipeline.IsRenderRequestSupported` accepts exactly `RenderPipeline.StandardRequest` and `UniversalRenderPipeline.SingleCameraRequest`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:675-683`
- `ProcessRenderRequests` does the following. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:692-821`
  - It stores `camera.targetTexture`, sets `camera.targetTexture = destination`, renders, then restores it and calls `Graphics.SetRenderTarget(originalTarget)` (`:714-715, 741, 811-815`).
  - If the destination is a 2D texture at mip 0, it renders directly into it with no temporary RT and no copy (`:733-737`).
  - `StandardRequest` calls `Render(context, new List<Camera>{ camera })` (`:743-745`). This is the full path: `SetHDRState`, `BeginContextRendering`/`EndContextRendering`, per-frame constants, `RenderCameraStack` (overlay stack plus `UpdateVolumeFramework`), and `s_RenderGraph.EndFrame()`.
  - `SingleCameraRequest` wraps only `ContextRenderingScope` and `CameraRenderingScope`, then calls `RenderSingleCameraInternal` (`:749-758`). It does NOT call `UpdateVolumeFramework`, so the volume stack is whatever the previous camera left in `VolumeManager.instance.stack`. It does NOT render overlay cameras. It does NOT run `SetHDRState` or `SetupPerFrameShaderConstants`.
  - `StandardRequest.isPreview = true` sets `cameraType = CameraType.Preview` (`:711, 739-740`). `FullScreenPassRendererFeature` then skips itself (`URP/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs:99-102`). Keep `isPreview = false`.
  - **Verdict: use `RenderPipeline.StandardRequest`.**
- `SingleCameraRequest` fields: `destination`, `mipLevel`, `face`, `slice`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:2880-2901`
- `StandardRequest` fields: `destination`, `mipLevel`, `face`, `slice`, `isPreview`. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/submitrenderrequest (also `XML/UnityEngine.CoreModule.xml:48329-48358`)
- `RenderSingleCamera(ScriptableRenderContext, Camera)` is `[Obsolete]` from 2023.1. Use `SubmitRenderRequest` instead. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:830`
- The request is synchronous: "processed sequentially within your script, and no callback mechanism is involved". Both URP request types are "only applicable to Base Cameras". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/submitrenderrequest
- `RenderPipeline.SupportsRenderRequest(cam, req)` builds the pipeline instance if it does not exist yet, and asserts that the build worked. This matters in Editor batchmode. VERIFIED-DOCS (UnityCsReference master) https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/RenderPipeline/RenderPipeline.cs
- Re-entrancy: both request paths fire `RenderPipelineManager.begin/endContextRendering` (`:593` via `Render`, `:753`). If your capture code subscribes to `endContextRendering` and submits a request from it, guard with a bool or you recurse.
  - URP docs recommend submitting from a `WaitForEndOfFrame` coroutine, or using `endContextRendering` only to know that all cameras finished. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.5/manual/cameras/urp/multiple/user-render-requests
  - A forum report says submitting from render-loop callbacks has been allowed since 6000.0.12f1. UNVERIFIED, from https://discussions.unity.com/t/rendersinglecamera-is-obsolete-but-the-suggested-solution-has-error/898627?page=2
- URP's own runtime tests show the minimal working form. VERIFIED-SOURCE `URP/Tests/Runtime/RuntimeTests.cs:73-78,108-114`:
  `RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = new RenderTexture(128,128, GraphicsFormat.R8G8B8A8_SRGB, CoreUtils.GetDefaultDepthOnlyFormat()) });`
  The camera in that test is a fresh `AddComponent<Camera>()`.

### 1.2 Camera.Render() in URP

- `Camera.Render()` "will use the camera's clear flags, target texture and all other settings". You cannot call it on a camera that is currently rendering. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/render
- The page says nothing about SRP. In practice, Unity routes it into `RenderPipeline.Render(ctx, [camera])` without a deprecation warning, but calling it inside a render callback gives "Recursive rendering is not supported in SRP". UNVERIFIED. Prefer `StandardRequest`: it is documented and supported, and it sets and restores `targetTexture` for you.

### 1.3 Pitfall: a camera without UniversalAdditionalCameraData gets no post-processing

- With `additionalCameraData == null`, URP sets `postProcessEnabled = false`, `antialiasing = None` and `volumeLayerMask = 1`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:1751-1761, 1892-1902`
- `RenderCameraStack` only does `TryGetComponent`; it never adds the component (`:1105`). Always call `cam.GetUniversalAdditionalCameraData()`, which adds it if missing (VERIFIED-SOURCE `URP/Runtime/UniversalAdditionalCameraData.cs:183-191`). Then set:
  - `renderPostProcessing = true`
  - `antialiasing`
  - `volumeLayerMask`
  - `SetRenderer(index)` if not using the default renderer
  - `dithering` and `stopNaN`, matching the main camera
- `Camera.CopyFrom` does not copy the UACD fields. Copy them yourself. UNVERIFIED (standard Unity behaviour).

### 1.4 Pitfall: the RT FORMAT becomes the INTERNAL HDR buffer format

- For a camera with a `targetTexture`:
  - `backbufferColor.format = camera.targetTexture.graphicsFormat` and `msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(targetTexture.descriptor)`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1529-1546`
  - The intermediate camera colour descriptor starts from `camera.targetTexture.descriptor` and uses `desc.graphicsFormat = cameraData.backbufferColor.format`. The code comment admits: "the intermediate color buffer inherits the external texture's format instead of using the configured internal rendering format. This is incorrect but kept for backward compatibility". VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1593-1604`
  - The RG intermediate colour attachment uses that format. VERIFIED-SOURCE `URP/Runtime/UniversalRendererRenderGraph.cs:398-412`
- For the screen path (`targetTexture == null`), the intermediate format comes from `MakeRenderTextureGraphicsFormat(isHdr, precision, needsAlpha)`. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1477-1492, 1511, 1514-1528`:
  - `B10G11R11_UFloatPack32` when HDR is on, precision is not 64 bits, no alpha is needed, and the format is supported.
  - Otherwise `R16G16B16A16_SFloat`.
  - Otherwise `DefaultFormat.LDR`.
- Consequence: if you render into an `R8G8B8A8_SRGB` RT, the whole scene buffer is 8-bit LDR. Values clamp to 1 before bloom and tonemapping, so bloom mostly disappears and highlights look different. The PNG would NOT equal the screen. Community reports confirm the fix is an `R16G16B16A16_SFloat` target:
  - https://issuetracker.unity3d.com/issues/bloom-is-rendered-incorrectly-when-using-camera-dot-render-on-a-rendertexture
  - https://discussions.unity.com/t/hdr-data-is-not-rendered-into-render-texture-when-using-urp/231826
- **Rule: render the camera into an HDR-format RT, then convert to 8-bit sRGB yourself.** For exact screen parity, use the same format the screen intermediate uses. With this project's asset that is B10G11R11. Note that the screen path with a FullScreenPass after post also stores the tonemapped image in a B10G11R11 intermediate before the final blit. VERIFIED-SOURCE:
  - `URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs:138-147` (the uber pass writes to a texture created from the camera colour descriptor when the target is not the backbuffer)
  - `URP/Runtime/UniversalRendererRenderGraph.cs:1404,1435,1464-1465,1489-1497`

```csharp
// Same format choice as UniversalRenderPipeline.MakeRenderTextureGraphicsFormat (internal), for parity with the backbuffer path.
static GraphicsFormat ScreenEquivalentColorFormat(UniversalRenderPipelineAsset a, Camera cam)
{
    bool hdr = cam.allowHDR && a.supportsHDR;
    if (!hdr) return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
    if (a.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits &&
        SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend))
        return GraphicsFormat.B10G11R11_UFloatPack32;   // what the screen path uses here
    return GraphicsFormat.R16G16B16A16_SFloat;          // "quality" choice, finer than the screen near white
}
```

- Give the target RT a depth buffer. Otherwise URP warns: "In the render graph API, the output Render Texture must have a depth buffer..." and substitutes the default format. The RT's `depthStencilFormat` becomes the intermediate depth format. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1540-1545, 1607`. Use `CoreUtils.GetDefaultDepthStencilFormat()`, which returns `D32_SFloat_S8_UInt` on PC. VERIFIED-SOURCE `CORE/Runtime/Utilities/CoreUtils.cs:1944-1951`
- With an RGBA target, `isAlphaOutputEnabled` only takes effect if `allowPostProcessAlphaOutput` is set on the asset; it is off here. RGB is unaffected. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:1703, 2004-2007` and `PC_RPAsset.asset:81`

### 1.5 MSAA on the RT vs the pipeline-asset MSAA

- `GetIntermediateTexturesMSAA` behaves as follows. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipelineCore.cs:1548-1578`
  - If `camera.allowMSAA` is set and the renderer supports MSAA, `msaaSamples` is `asset.msaaSampleCount` when `targetTexture == null`, and **`camera.targetTexture.antiAliasing`** when a targetTexture is set.
  - **The pipeline asset MSAA is ignored for RT targets. The RT itself must be created with `msaaSamples = 4`.** An MSAA-1 RT renders with no MSAA at all.
  - The value is clamped by `SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc)` (`:1619`).
- The intermediate MSAA follows the RT, so the URP output RT is itself MSAA. "To the rest of the system only this 'resolved' surface is visible": Blit, ReadPixels and readback see the resolved surface. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexture/antialiasing
- For screen parity, also set the PC_RPAsset MSAA to 4. It is currently 1.
- `PlatformRequiresExplicitMsaaResolve()` returns `true` in the Editor, and `!supportsMultisampleAutoResolve || !supportsMultisampledBackBuffer` in a player. Intermediate-texture decisions can therefore differ between Editor and Player. VERIFIED-SOURCE `URP/Runtime/UniversalRenderer.cs:793-801, 831`
- An offscreen camera gets an intermediate texture when any of these hold: post-processing, color texture required, explicit MSAA resolve, non-default viewport, or requested color history. VERIFIED-SOURCE `URP/Runtime/UniversalRenderer.cs:846-849`

### 1.6 Where linear to sRGB happens

- `requireSrgbConversion` returns `targetTexture == null && Display.main.requiresSrgbBlitToBackbuffer` (plus an XR branch). VERIFIED-SOURCE `URP/Runtime/FrameData/UniversalCameraData.cs:276-288`
  - The shader keyword `_LINEAR_TO_SRGB_CONVERSION` (`ShaderGlobalKeywords.LinearToSRGBConversion`) is only set from this value: FinalBlit at `URP/Runtime/Passes/FinalBlitPass.cs:141,200`, Uber/Final post at `URP/Runtime/PostProcess.cs:106-109`, and `UniversalRendererRenderGraph.cs:1454`.
  - **For any RT target, URP never encodes sRGB in the shader. It writes linear display values.** The RT format decides what happens:
    - `*_SRGB` 8-bit: the hardware encodes on write. In Linear color space, non-HDR RTs are sRGB by default.
    - `*_UNorm`: stores linear values, so the PNG comes out too dark.
    - Float formats: always linear. "HDR (floating point) formats ... no sRGB conversions are ever performed". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexturereadwrite
- **Conversion step (float RT to 8-bit sRGB):** `GL.sRGBWrite = true; Graphics.Blit(hdrRT, srgbRT /*R8G8B8A8_SRGB, MSAA 1*/);`. The Blit samples the resolved MSAA surface, and the sRGB render target encodes on write.
  - Docs: "In linear color space, set GL.sRGBWrite before using Blit". Blit also changes `RenderTexture.active`.
  - Outside the render loop, Blit to an RT is fine. Blitting to the screen in URP must happen from `RenderPipelineManager.endContextRendering`.
  - VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/blit and `XML/UnityEngine.CoreModule.xml:14659`
- Do NOT rely on AsyncGPUReadback format conversion from float to `R8G8B8A8_SRGB`. The docs say "conversion is automatic" but do not define the sRGB transfer. UNVERIFIED. Convert on the GPU with the Blit above.

### 1.7 Temporal state when the same camera renders twice in one frame

- Motion-vector history updates once per `Time.frameCount` per eye, unless the aspect changes (`m_LastFrameIndex[eye] != frameIndex || aspectChanged`). Rendering the main camera to a window of a different aspect plus a 16:9 4K RT in the same frame resets the history on every render, which breaks camera motion blur. VERIFIED-SOURCE `URP/Runtime/MotionVectors.cs:223, 256-260, 306-307`
- TAA history is resized to the current target in `UpdateTemporalAATargets`, so alternating sizes breaks TAA. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:1005-1006`
- Dithering and film grain:
  - The noise offset is seeded by `Random.InitState(Time.frameCount)`, which is deterministic per frame. VERIFIED-SOURCE `URP/Runtime/PostProcessUtils.cs:202-213`
  - The blue-noise slice index advances on every uber render (`++index`). The image therefore depends on how many renders happened. Defining the scripting symbol `LWRP_DEBUG_STATIC_POSTFX` freezes both (index 0, zero offset). VERIFIED-SOURCE `URP/Runtime/PostProcessUtils.cs:149-154, 204-205`
  - That a Player-Settings define reaches package assemblies is UNVERIFIED but standard. For golden-image tests, either turn dithering off on the capture camera or use that define.
- **Recommendation for sequences (Option B):** in capture mode, set `mainCamera.targetTexture = hdrRT` (4K, MSAA 4, HDR format) for the whole run. The camera then renders exactly once per frame and its histories stay consistent.
  - The window shows nothing. For a preview, `Graphics.Blit(ldrRT, (RenderTexture)null)` from `endContextRendering` (UNVERIFIED that it presents correctly).
  - Set `cam.aspect = (float)w / h` explicitly and call `cam.ResetAspect()` when done. Whether aspect auto-follows the targetTexture is UNVERIFIED.
- **For single screenshots during normal play (Option A):** use `StandardRequest` on a dedicated, disabled capture camera that copies the main camera each frame, so the main camera's histories are untouched. URP checks only cameraType for base cameras, not `enabled` (`UniversalRenderPipeline.cs:621-631, 1101-1109`). Whether the native `SubmitRenderRequest` accepts a disabled camera is UNVERIFIED; smoke-test it.

### 1.8 Alternative hook: CameraCaptureBridge (as used by Unity Recorder)

- API: `UnityEngine.Rendering.CameraCaptureBridge.AddCaptureAction(Camera, Action<RenderTargetIdentifier, CommandBuffer>)` / `RemoveCaptureAction(...)`. VERIFIED-SOURCE `CORE/Runtime/Utilities/CameraCaptureBridge.cs:73-101`
- URP enables it in the pipeline constructor. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:389`
- The action runs after the AfterRenderingPostProcessing passes and before FinalPost (FXAA) and FinalBlit. Its source is the camera colour in the intermediate (linear HDR) format at camera resolution. Using it forces an intermediate texture. VERIFIED-SOURCE `URP/Runtime/UniversalRendererRenderGraph.cs:1401, 1435, 1464-1473` and `URP/Runtime/UniversalRenderer.cs:833,852`
- It is useful for window-resolution capture without a second render. It cannot give 4K on a smaller window unless the camera targets a 4K RT.

---

## 2. Readback

| Method | Facts |
|---|---|
| `Texture2D.ReadPixels(Rect, x, y, recalcMips)` | Reads from `RenderTexture.active`; origin is lower-left. "The render target and the texture must use the same format", and `isReadable` must be true. "usually slow, because the method waits for the GPU to complete all previous work first". `Apply()` only uploads to the GPU, so skip it when you only encode. In URP use it after rendering (`WaitForEndOfFrame` / `endCameraRendering`). VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/texture2d/readpixels |
| `AsyncGPUReadback.Request(Texture, int mip, GraphicsFormat dst, Action<AsyncGPUReadbackRequest>)` and the TextureFormat / region overloads | Non-blocking. "If the target format is different from the format stored on the GPU, the conversion is automatic." Check `hasError`. Don't `ReleaseTemporary` the source until it is done. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadback/request |
| `AsyncGPUReadback.RequestIntoNativeArray<T>(ref NativeArray<T>, Texture, int mip, GraphicsFormat dst, Action<...>)` | Zero-GC with pooled `Allocator.Persistent` arrays. "The NativeArray or underlying memory cannot be Disposed until the request is complete." VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadback/requestintonativearray |
| `AsyncGPUReadbackRequest.WaitForCompletion()` / `AsyncGPUReadback.WaitAllRequests()` | Blocking; "stalls both GPU and CPU". Use for back-pressure and before quitting. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadbackrequest/waitforcompletion and `XML/UnityEngine.CoreModule.xml:36639` |
| `ImageConversion.EncodeArrayToPNG(System.Array, GraphicsFormat, uint w, uint h, uint rowBytes=0)` → `byte[]` | "This method is thread safe", so it can run on worker threads. Output is 8-bit RGB/RGBA, and "PNG data will not contain gamma correction or color profile information". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/imageconversion/encodearraytopng |
| `ImageConversion.EncodeNativeArrayToPNG<T>(NativeArray<T>, GraphicsFormat, uint, uint, uint)` → `NativeArray<byte>` | Persistent allocator, "should only be called from the main thread". Dispose the result. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/imageconversion/encodenativearraytopng |

- **Bytes:** read the `R8G8B8A8_SRGB` RT with dst `GraphicsFormat.R8G8B8A8_SRGB` (same format, raw sRGB bytes), then encode as `GraphicsFormat.R8G8B8A8_UNorm`. That is the docs' encode example format and guarantees pass-through. For ReadPixels, use `new Texture2D(w, h, TextureFormat.RGBA32, false, /*linear*/ false)`, which is `R8G8B8A8_SRGB` in Linear color space and matches the RT. UNVERIFIED in detail; check with a byte-exact test against a known clear colour.
- **Orientation:** a camera-rendered RT read with ReadPixels or AsyncGPUReadback follows Unity's bottom-up texture convention, which matches Texture2D/PNG encoding, so no flip is needed. UNVERIFIED; add an automated test with a red marker in the top-left of the view. `CaptureScreenshotIntoRenderTexture` output is flipped on DX/Metal; see section 6.
- **GC avoidance:**
  - Allocate the RTs and Texture2D once.
  - Pool `NativeArray<byte>` buffers.
  - Copy the readback into pooled `byte[]` and run `EncodeArrayToPNG` + `File.WriteAllBytes` on 1-3 worker threads behind a bounded queue.
  - The only per-frame allocation left is the PNG `byte[]`, on the worker. The project has incremental GC on (`ProjectSettings.asset:853`).
  - Cap the frames in flight: a 3840x2160 RGBA8 frame is 33 MB.

---

## 3. Time control

- `Time.captureDeltaTime` (float): when non-zero, "Time.time increases at an interval of captureDeltaTime (scaled by Time.timeScale) regardless of real time and the duration of a frame". It "does not have any affect on Time.unscaledTime". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/time/capturedeltatime
  - So `Time.deltaTime == captureDeltaTime * timeScale` (inference from the docs). Never animate with `unscaledTime`, `realtimeSinceStartup`, `Stopwatch` or `DateTime`.
- `Time.captureFramerate` (int) "is the equivalent of (1.0 / Time.captureDeltaTime) rounded to the nearest integer". "Setting captureFramerate also sets Time.captureDeltaTime". 0 means off. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/captureframerate
  - Use `Time.captureFramerate = 60` (or 30). For NTSC rates use `Time.captureDeltaTimeRational` (`Unity.IntegerTime.RationalTime`; "Setting it to a value of 0 or invalid restores the normal Unity time management"). VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/capturedeltatimerational
- Real time: the engine does NOT wait. If frames take longer than 1/fps, game time is effectively slowed down ("game time is slowed and the frame updates are issued at precise regular intervals"). VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/scripting/managing-time-and-frame-rate/time-capture-frame-rate.md
  - If frames are faster, it runs faster than real time; the Unity Recorder adds its own "Cap" option to prevent that. VERIFIED-DOCS https://learn.unity.com/tutorial/working-with-unity-recorder.md?language=en
  - For offline PNG capture this is exactly what we want.
- Audio (DSP clock, `OnAudioFilterRead`) keeps running in REAL time, so it is not in sync under capture time. Render procedural audio offline from the same deterministic timeline to WAV, and mux it later. UNVERIFIED (well known; no doc statement found).
- Shader `_Time` follows game time (time since level load), so it is driven by `captureDeltaTime` in Play mode and in the player. UNVERIFIED for edit-mode batchmode; drive your own global such as `Shader.SetGlobalFloat("_RingTime", t)` in edit-mode captures.
- `Application.targetFrameRate`: -1 is the default. "If vSyncCount != 0, then targetFrameRate is ignored". "When vSyncCount = 0 and targetFrameRate = -1, content is rendered unsynchronized as fast as possible". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/targetframerate
  - Capture mode: `QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Time.captureFramerate = fps;`
  - Real-time showcase mode: `vSyncCount = 1`.
- Set the capture rate before the first scene frame (`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`). Render 3-10 warm-up frames before frame 0 so shader first-use, volumes and histories settle. Number frames with your own counter, not `Time.frameCount`.
- FixedUpdate is driven by game time, so it stays deterministic under capture time. Physics steps per rendered frame vary (0, 1 or 2) in a deterministic pattern.

---

## 4. Player command line, logs and exit codes

- Custom args: `System.Environment.GetCommandLineArgs()` (index 0 is the exe; Unity flags stay in the array). The docs confirm this for `-executeMethod`. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor

  ```csharp
  static string Arg(string name, string def = null) {
      var a = System.Environment.GetCommandLineArgs();
      for (int i = 0; i < a.Length - 1; i++)
          if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
      return def;
  }
  static bool Flag(string name) => Array.Exists(System.Environment.GetCommandLineArgs(), s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
  ```

- Player flags (VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player):
  - `-logFile <path>`: "To output to the console, specify `-` ... On Windows ... stdout, which by default is not the console". Quote paths that contain spaces.
  - `-screen-width` / `-screen-height`: "must be an integer from a supported resolution".
  - `-screen-fullscreen 0|1`.
  - `-window-mode exclusive|borderless` (Windows; applies to fullscreen).
  - `-popupwindow` (frameless window).
  - `-monitor N` (1-based).
  - `-force-d3d11` / `-force-d3d12`: pin the API so runs are comparable.
  - `-single-instance`, `-timestamps`, `-nolog`.
  - `-batchmode`: "headless mode ... doesn't display anything or accept user input".
  - `-nographics`: "doesn't initialize a graphics device", so NOTHING renders. Never use it for capture.
- Player `-batchmode` with a GPU: users report all-black RT readbacks (https://discussions.unity.com/t/how-to-use-batchmode-command-line-argument-for-standalone-player/858372). UNVERIFIED either way. **Capture in a normal windowed player** (for example `-screen-fullscreen 0 -screen-width 1280 -screen-height 720` with `runInBackground = true`) and render the 4K frames offscreen. Do not minimise the window (UNVERIFIED whether rendering continues while minimised).
- `Application.Quit(int exitCode)`: "An optional exit code to return when the player application terminates on Windows, Mac and Linux. Defaults to 0." It "is ignored in the Editor". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/quit
  - Before quitting: `AsyncGPUReadback.WaitAllRequests()`, drain the queues, join the writer threads.
- Reading the exit code on Windows. The player is a GUI-subsystem exe, so PowerShell's `&` may not wait for it. UNVERIFIED.

  ```powershell
  $p = Start-Process -FilePath "W:\...\Build\TheRing.exe" -ArgumentList '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -logFile "W:\...\Logs\capture.log" -capture -captureOut "W:\...\Captures\seq01" -captureFps 60 -captureFrames 600' -PassThru -Wait
  exit $p.ExitCode
  ```

---

## 5. Editor batchmode rendering (no `-nographics`)

- `-batchmode` facts. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor
  - It suppresses dialogs. An exception exits with code 1.
  - "You can't open a project in batch mode while the Editor has the same project open".
  - `-executeMethod` needs a static method in an Editor folder. Return errors by throwing or with `EditorApplication.Exit(nonZero)`.
  - `-quit`: "If the Editor is running asynchronous code, then -quit can cause the application to hang". **Omit `-quit` and call `EditorApplication.Exit(code)` yourself** (VERIFIED in `XML/../UnityEditor.xml:16029`).
- Without `-nographics` a D3D device exists, so offscreen rendering to RTs is possible. `RenderPipeline.SupportsRenderRequest` creates the URP instance if needed (section 1.1).
- In edit mode, `UpdateVolumeFramework` always updates (`shouldUpdate |= Application.isPlaying == false`), so post-processing volumes apply. VERIFIED-SOURCE `URP/Runtime/UniversalRenderPipeline.cs:1428-1430`
- Pitfalls:
  - "`WaitForEndOfFrame` does not run when you run the Editor in batch mode, even if ... [ExecuteInEditMode] or [ExecuteAlways]". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/waitforendofframe. Use synchronous `SubmitRenderRequest` in a plain loop, or `EditorApplication.update`.
  - Async shader compilation can draw cyan placeholders. Set `UnityEditor.ShaderUtil.allowAsyncCompilation = false` ("When true, asynchronous Shader compilation is allowed at the current call site") around the renders. Check `ShaderUtil.anythingCompiling`. `EditorSettings.asyncShaderCompilation` covers only the Game and Scene views. VERIFIED-SOURCE `XML/../UnityEditor.xml:70562-70570, 24453`
  - `Time.frameCount` and `Time.time` do not advance inside one synchronous `executeMethod` call. Motion-vector history (keyed by `frameCount`, `MotionVectors.cs:223,258`) goes stale, so camera motion blur and TAA are wrong. The RTHandle pool purge also uses `Time.frameCount` (`UniversalRenderPipeline.cs:649`). Disable motion blur and TAA for edit-mode captures, or capture in Play mode or the player. UNVERIFIED whether frameCount ticks between editor updates in batchmode.
  - `Application.Quit` is ignored in the Editor; use `EditorApplication.Exit`.
  - `MonoBehaviour.Update` does not run in edit mode. Build the procedural content by calling the builders directly.
  - Make sure the intended quality level and RP asset are active: `QualitySettings.SetQualityLevel(i, true)`, `GraphicsSettings.defaultRenderPipeline` and `QualitySettings.renderPipeline`.

Batchmode command:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "W:\YouTube\Megastructures\2nd Megastructures\2nd Megastructures" -executeMethod Ring.EditorTools.BatchCapture.Run -logFile "W:\...\Logs\batchcapture.log" -captureOut "W:\...\Captures\editor" -force-d3d11
```

```csharp
// Assets/Editor/BatchCapture.cs  (sketch)
public static class BatchCapture {
  public static void Run() {
    int code = 1;
    try {
      ShaderUtil.allowAsyncCompilation = false;
      EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single); // or NewScene + procedural build
      var cam = /* find or create */; var ucd = cam.GetUniversalAdditionalCameraData(); ucd.renderPostProcessing = true;
      var hdr = new RenderTexture(new RenderTextureDescriptor(3840, 2160) {
          graphicsFormat = ScreenEquivalentColorFormat(asset, cam), depthStencilFormat = CoreUtils.GetDefaultDepthStencilFormat(),
          msaaSamples = 4, useMipMap = false, autoGenerateMips = false }); hdr.Create();
      var ldr = new RenderTexture(new RenderTextureDescriptor(3840, 2160) {
          graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB, depthStencilFormat = GraphicsFormat.None, msaaSamples = 1 }); ldr.Create();
      var req = new RenderPipeline.StandardRequest { destination = hdr };
      if (!RenderPipeline.SupportsRenderRequest(cam, req)) throw new Exception("StandardRequest unsupported");
      for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(cam, req);   // warm-up
      RenderPipeline.SubmitRenderRequest(cam, req);
      bool s = GL.sRGBWrite; GL.sRGBWrite = true; Graphics.Blit(hdr, ldr); GL.sRGBWrite = s;
      var tex = new Texture2D(3840, 2160, TextureFormat.RGBA32, false, false);
      var prev = RenderTexture.active; RenderTexture.active = ldr;
      tex.ReadPixels(new Rect(0, 0, 3840, 2160), 0, 0, false); RenderTexture.active = prev;
      File.WriteAllBytes(path, ImageConversion.EncodeArrayToPNG(tex.GetRawTextureData(), GraphicsFormat.R8G8B8A8_UNorm, 3840, 2160));
      code = 0;
    } catch (Exception e) { Debug.LogException(e); }
    EditorApplication.Exit(code);
  }
}
```

---

## 6. ScreenCapture and window-vs-RT parity

- `ScreenCapture.CaptureScreenshotIntoRenderTexture(RenderTexture rt)` "captures the game view"; "always wait until the frame rendering process is complete ... a coroutine that yields on WaitForEndOfFrame". The docs example uses `new RenderTexture(Screen.width, Screen.height, 0)`, then `AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, cb)`, then `EncodeArrayToPNG(..., GraphicsFormat.R8G8B8A8_UNorm, ...)`. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshotintorendertexture
- The output is vertically flipped in the Editor and in standalone builds; Unity closed this "by design" (old tracker entry; the page has since been removed): https://issuetracker.unity3d.com/issues/graphics-capturescreenshotintorendertexture-output-is-flipped-in-the-editor. Flip with a Blit (scale (1,-1), offset (0,1)) or flip rows on the CPU. Detect the flip with a marker test instead of hard-coding it.
- `ScreenCapture.CaptureScreenshot(file, superSize)` captures "the final frame presented to the user", including UI composited to the screen. A relative path resolves to the project folder or working directory. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshot. `superSize` behaviour under URP is UNVERIFIED (tile rendering); don't use it for 4K.
- Window-vs-RT parity test:
  - Render the RT path at exactly `Screen.width x Screen.height` with RT MSAA equal to the asset MSAA and the screen-equivalent format (section 1.4).
  - Compare it with `CaptureScreenshotIntoRenderTexture` (after un-flipping).
  - Expect a max per-channel difference of 1-2/255 from dithering (index and offset; see section 1.7), or 0 with `LWRP_DEBUG_STATIC_POSTFX` or dithering off.
  - Screen Space Overlay UI appears only in the window capture. URP draws overlay UI only for the last base camera and not when the target is offscreen; see `UniversalRendererRenderGraph.cs:1501-1538`.

---

## 7. Recommended capture recipe (built Windows player, Option B)

```csharp
// Key steps only; 4K/60 PNG sequence, deterministic.
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void Boot() { if (Flag("-capture")) { Time.captureFramerate = int.Parse(Arg("-captureFps","60"));
    QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true; } }

IEnumerator CaptureLoop() {
  cam.targetTexture = hdrRT; cam.aspect = (float)W / H;          // one render per frame; histories consistent
  for (int w = 0; w < warmup; w++) yield return null;
  for (int i = 0; i < frames; i++) {
    yield return new WaitForEndOfFrame();                          // main camera has rendered into hdrRT
    bool s = GL.sRGBWrite; GL.sRGBWrite = true; Graphics.Blit(hdrRT, ldrRT); GL.sRGBWrite = s;
    var buf = RentNative(W * H * 4);
    var r = AsyncGPUReadback.RequestIntoNativeArray(ref buf, ldrRT, 0, GraphicsFormat.R8G8B8A8_SRGB);
    inflight.Enqueue((r, buf, i));
    while (inflight.Count > 4) { var o = inflight.Peek(); o.r.WaitForCompletion(); DrainOne(); } // back-pressure
    DrainCompleted();   // copy done buffers to pooled byte[], push to encode queue; workers: EncodeArrayToPNG + WriteAllBytes
  }
  AsyncGPUReadback.WaitAllRequests(); DrainAll(); StopWorkersAndJoin();
  cam.targetTexture = null; cam.ResetAspect();
  Application.Quit(errors == 0 ? 0 : 2);
}
```

Determinism checklist:
- Fix every RNG seed.
- Animate only from `Time.time`, `Time.timeAsDouble` or your own frame index.
- Render warm-up frames.
- Set `cam.allowMSAA = true`.
- Make the RT MSAA match the asset MSAA.
- Turn dithering off, or define `LWRP_DEBUG_STATIC_POSTFX`, for golden images.
- Pin the graphics API with `-force-d3d11`.
- Use the same GPU and driver for golden comparisons.
- Audio is rendered offline, not captured.

---

## Sources (all accessed 2026-10-29)

- `URP/Runtime/UniversalRenderPipeline.cs`: request support and processing, the Render/stack/volume paths, the obsolete RenderSingleCamera, camera data init, alpha output, CameraCaptureBridge enable, SingleCameraRequest class, debug-display format override (lines cited above).
- `URP/Runtime/UniversalRenderPipelineCore.cs`: MakeRenderTextureGraphicsFormat, CreateBackbufferInfo, GetIntermediateTexturesMSAA, CreateRenderTextureDescriptor.
- `URP/Runtime/UniversalRendererRenderGraph.cs`: intermediate attachments, post / after-post / capture / final-blit ordering, overlay UI.
- `URP/Runtime/UniversalRenderer.cs`: PlatformRequiresExplicitMsaaResolve, RequiresIntermediateColorTexture.
- `URP/Runtime/FrameData/UniversalCameraData.cs`: requireSrgbConversion.
- `URP/Runtime/Passes/FinalBlitPass.cs`, `URP/Runtime/PostProcess.cs`: the sRGB conversion keyword.
- `URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs`: uber destination and WriteAll.
- `URP/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs`: skips Preview and Reflection cameras.
- `URP/Runtime/PostProcessUtils.cs`: dithering and film-grain randomness, LWRP_DEBUG_STATIC_POSTFX.
- `URP/Runtime/MotionVectors.cs`: motion-vector history keyed by frameCount and aspect.
- `URP/Runtime/UniversalAdditionalCameraData.cs`: GetUniversalAdditionalCameraData.
- `URP/Tests/Runtime/RuntimeTests.cs`: working StandardRequest usage.
- `CORE/Runtime/Utilities/CameraCaptureBridge.cs`, `CORE/Runtime/Utilities/CoreUtils.cs`: capture hook, default depth formats.
- `CORE/Documentation~/User-Render-Requests.md`: StandardRequest renders the full URP camera stack.
- `XML/UnityEngine.CoreModule.xml`, `XML/UnityEngine.ImageConversionModule.xml`, `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Managed/UnityEditor.xml`: API existence and summaries (StandardRequest, GL.sRGBWrite, WaitAllRequests, Encode*ToPNG, ShaderUtil.allowAsyncCompilation, EditorApplication.Exit, RationalTime).
- `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/PC_Renderer.asset`: current project configuration.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/submitrenderrequest: URP request types, base-camera restriction, synchronous behaviour.
- https://docs.unity.com/en-us/engine/6000.5/manual/cameras/urp/multiple/user-render-requests: URP manual pattern (WaitForEndOfFrame / endContextRendering).
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/render: Camera.Render semantics.
- https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/RenderPipeline/RenderPipeline.cs: SupportsRenderRequest builds the pipeline; SubmitRenderRequest forwards to the camera.
- https://discussions.unity.com/t/rendersinglecamera-is-obsolete-but-the-suggested-solution-has-error/898627?page=2: SubmitRenderRequest from render callbacks (forum).
- https://issuetracker.unity3d.com/issues/bloom-is-rendered-incorrectly-when-using-camera-dot-render-on-a-rendertexture and https://discussions.unity.com/t/hdr-data-is-not-rendered-into-render-texture-when-using-urp/231826: the RT format drives the internal buffers; use R16G16B16A16_SFloat.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexturereadwrite: sRGB/linear RT semantics; float formats are always linear.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexture/antialiasing: MSAA resolve visibility.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/blit: GL.sRGBWrite before Blit; URP screen-blit rule; RenderTexture.active side effect.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/texture2d/readpixels: ReadPixels constraints.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadback/request, .../requestintonativearray, .../asyncgpureadbackrequest/waitforcompletion: readback API.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/imageconversion/encodearraytopng and .../encodenativearraytopng: PNG encoding, thread safety.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/time/capturedeltatime, https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/captureframerate, .../time/capturedeltatimerational, https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time.md, https://docs.unity.com/en-us/engine/6000.6/manual/scripting/managing-time-and-frame-rate/time-capture-frame-rate.md: capture time semantics.
- https://learn.unity.com/tutorial/working-with-unity-recorder.md?language=en: the Recorder "Cap" option, meaning capture otherwise runs faster than real time.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/targetframerate: targetFrameRate vs vSyncCount.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/quit: exit code.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player: player flags.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor: batchmode, executeMethod, quit, logFile, exit codes.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/waitforendofframe: not run in Editor batchmode.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshotintorendertexture and .../screencapture/capturescreenshot: screen capture APIs.
- https://issuetracker.unity3d.com/issues/graphics-capturescreenshotintorendertexture-output-is-flipped-in-the-editor (via search summary; the page is now removed): flip is by design.
- https://discussions.unity.com/t/how-to-use-batchmode-command-line-argument-for-standalone-player/858372: black RT readback in player -batchmode (forum, unresolved).
- https://docs.unity3d.com/Manual/AsynchronousShaderCompilation-introduction.html: async compilation and cyan placeholders.
