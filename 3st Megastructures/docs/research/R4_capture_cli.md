# R4: Deterministic capture, CLI and batchmode (Unity 6000.6.0f1, URP 17.6.0)

Researcher: R4 (read-only). Accessed 2026-10-29. Paths are relative to these roots:
`URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`,
`CORE` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`,
`UTF` = `Library/PackageCache/com.unity.test-framework@7a3849e09bd0`,
`XML` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml`.

## 0. Decisions (TL;DR)

1. **Render path:** `RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest{ destination = rt })`. It renders the whole URP camera stack synchronously, including the volume update, post-processing and every renderer feature. Do not use `UniversalRenderPipeline.SingleCameraRequest`, which skips the volume update and the camera stack. Use `Camera.Render()` only to force the pipeline to be created.
2. **Capture RT format:** use the **same HDR format URP uses for the screen**. On this project that is `B10G11R11_UFloatPack32`, with MSAA set on the RT itself (`msaaSamples = 4`) and a depth-stencil buffer. Then `Graphics.Blit` into an `R8G8B8A8_SRGB` RT and read it back with `AsyncGPUReadback.Request(srgbRT, 0, GraphicsFormat.R8G8B8A8_SRGB)` followed by `WaitForCompletion()`. Write the bytes with `ImageConversion.EncodeArrayToPNG`. The bytes come back sRGB-encoded, so the PNG matches the backbuffer. Never render straight into an 8-bit RT: URP would then run the whole frame in 8 bits and HDR, bloom and tonemapping would differ (§1.2).
3. **Determinism:** set `Time.captureDeltaTime = 1/fps`, vSync 0 and `targetFrameRate = -1`. Every visual state must be a pure function of an explicit walk time `t`, which a seek hook sets. Use a custom shader-time global, not `_Time` or `unscaledTime`. Use fixed RNG seeds. Do not use TAA (§1.3); 4x MSAA is better for flat colour anyway.
4. **Editor batch captures:** run `Unity.exe -batchmode -projectPath … -logFile … -executeMethod ThreadGame.EditorTools.CaptureBatch.Run -thread-capture <dir>` with **no `-quit` and no `-nographics`**. The method opens the scene and sets `EditorApplication.isPlaying = true`. A runtime `CaptureDriver`, bootstrapped by `[RuntimeInitializeOnLoadMethod]` when it sees `-thread-capture` in `Environment.GetCommandLineArgs()`, takes the shots with `yield return null` (never `WaitForEndOfFrame`) and ends with `EditorApplication.Exit(code)`. An `[InitializeOnLoad]` watchdog calls `Exit(3)` on timeout. This is the same pattern the Unity Test Framework uses for batch PlayMode runs (§1.7).
5. **Built-player captures:** run the player windowed and **not** in `-batchmode`: `Thread.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "<abs>" -thread-capture "<abs dir>" -thread-shots a@4,b@60`. The same driver writes 4K PNGs through the RT path. It also writes a window-resolution backbuffer capture (`WaitForEndOfFrame` + `ScreenCapture.CaptureScreenshotAsTexture`) and an RT-vs-backbuffer self-test, then calls `Application.Quit(exitCode)`. Launch it from PowerShell with `Start-Process -PassThru` and `$p.WaitForExit()`, then read `$p.ExitCode`.
6. **Perf:** use `ProfilerRecorder` handles looked up by name ("Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "GC Allocated In Frame", "CPU Main Thread Frame Time", "GPU Frame Time"; frame times are in ns). Add `FrameTimingManager` (`gpuFrameTime` in ms, 4-frame delay), which needs `PlayerSettings.enableFrameTimingStats = true` (currently 0). Measure in frames with **no** capture request.

## 1. Verified facts

### 1.1 Render requests in URP 17.6
- URP supports exactly two request types, `StandardRequest` and `SingleCameraRequest` (`URP/Runtime/UniversalRenderPipeline.cs:675-683`).
- `ProcessRenderRequests` (`:692-821`):
  - It temporarily sets `camera.targetTexture = destination` (`:741`).
  - **StandardRequest** calls the normal `Render(context, new List<Camera>{camera})` (`:745`). That goes through `RenderCameraStack`, so overlay cameras are rendered and `UpdateVolumeFramework` runs (`:1101-1218`).
  - **SingleCameraRequest** calls only `RenderSingleCameraInternal` (`:749-758`), with no `UpdateVolumeFramework` call. It therefore uses whatever `VolumeManager.instance.stack` was left over, and it skips overlay cameras (non-Base cameras get the warning "Only Base cameras can be rendered", `:855-859`).
  - Afterwards it restores the camera and calls `Graphics.SetRenderTarget(originalTarget)` (`:812-815`), so `RenderTexture.active` is reset. Set it again before calling `ReadPixels`.
  - A temporary RT is used only for mip>0 or for non-2D targets (`:733-737`). For a 2D target, rendering goes **directly into the destination**.
- `RenderSingleCamera` is obsolete; Unity recommends SubmitRenderRequest (`:830`). `SingleCameraRequest` fields: `destination`, `mipLevel`, `face`, `slice` (`:2880-2901`).
- Requests are synchronous: "processed sequentially in your script, so there's no callback" (`CORE/Documentation~/User-Render-Requests.md:3-12`). The same doc states that StandardRequest renders "A full stack of cameras" in URP.
- URP's own tests submit `StandardRequest` to `new RenderTexture(128,128, GraphicsFormat.R8G8B8A8_SRGB, CoreUtils.GetDefaultDepthOnlyFormat())` (`URP/Tests/Runtime/RuntimeTests.cs:73-78`).
- The pipeline instance may not exist yet: URP's editor test calls `Camera.main.Render()` *before* checking `RenderPipelineManager.currentPipeline` (`URP/Tests/Editor/NoLeaksOnEnterLeavePlaymode.cs:25-33`).
- `Camera.Render()` + `AsyncGPUReadback.Request` + `AsyncGPUReadback.WaitAllRequests()` works in edit mode under URP (`URP/Tests/Editor/RenderGraphTests.HelperPasses.cs:171-186`).

### 1.2 Camera with targetTexture: format, MSAA, HDR, post, features, UI
- **Color format is inherited from the RT, and HDR is lost with an LDR RT.**
  - `CreateBackbufferInfo` sets `backbufferColor.format = camera.targetTexture.graphicsFormat` (`URP/Runtime/UniversalRenderPipelineCore.cs:1531-1538`).
  - `CreateRenderTextureDescriptor` then sets `desc.graphicsFormat = cameraData.backbufferColor.format`, with the comment "the intermediate color buffer inherits the external texture's format … This is incorrect but kept for backward compatibility" (`:1595-1604`).
  - The intermediate camera color uses that format (`URP/Runtime/UniversalRendererRenderGraph.cs:398-412`).
  - The screen path uses `MakeRenderTextureGraphicsFormat`: `B10G11R11_UFloatPack32` when there is no alpha and precision is `_32Bits`, otherwise `R16G16B16A16_SFloat`, otherwise the LDR default (`:1477-1492`, `:1511-1525`).
- **MSAA comes from the RT, not from the asset:** `msaaSamples = camera.targetTexture.antiAliasing` (`URP/Runtime/UniversalRenderPipelineCore.cs:1555-1564`). The value is clamped by `SystemInfo.GetRenderTextureSupportedMSAASampleCount` (`:1619`).
- **The RT needs depth.** If `depthStencilFormat == None`, URP logs a warning and substitutes a default format (`:1540-1545`).
- **Post-processing does not depend on targetTexture.** `postProcessEnabled = additionalCameraData.renderPostProcessing` (`URP/Runtime/UniversalRenderPipeline.cs:1884`). A camera without `UniversalAdditionalCameraData` gets `false` (`:1896`).
- **Renderer features run for every rendered camera.** `renderer.AddRenderPasses(ref legacyRenderingData)` is called per camera (`:1061`), and the loop skips only inactive features (`URP/Runtime/ScriptableRenderer.cs:1141-1154`). The only filtering is whatever your feature does itself.
- **Offscreen blit:** `isOffscreenRender = targetTexture != null`. An intermediate is used when there is post, a color texture, explicit MSAA resolve, a non-default viewport or color history (`URP/Runtime/UniversalRenderer.cs:832, 846-849`).
- **There is no sRGB-conversion blit for RT targets:** `requireSrgbConversion => targetTexture == null && Display.main.requiresSrgbBlitToBackbuffer` (`URP/Runtime/FrameData/UniversalCameraData.cs:276-288`). An `_SRGB` RT is encoded by the hardware on write. A float RT holds linear, post-tonemapped values.
- **Screen-space overlay UI is NOT drawn into RT captures:** `resolveToScreen => targetTexture == null && …` and `rendersOverlayUI => … && resolveToScreen` (`URP/Runtime/FrameData/UniversalCameraData.cs:307, 384`).
- **Render scale and upscaler also apply to RT cameras:** `scaledWidth = camera.pixelWidth * renderScale` (`URP/Runtime/UniversalRenderPipeline.cs:1533-1536, 1789-1790`).
- `UniversalRenderPipeline.asset` is public (`URP/Runtime/UniversalRenderPipelineCore.cs:1439`). `supportsHDR`, `hdrColorBufferPrecision` and `msaaSampleCount` are public (`URP/Runtime/Data/UniversalRenderPipelineAsset.cs:1087-1106`). `CoreUtils.GetDefaultDepthStencilFormat()` returns `D32_SFloat_S8_UInt` on desktop (`CORE/Runtime/Utilities/CoreUtils.cs:1944-1951`).

### 1.3 Temporal effects and requests
- TAA treats render requests as "redraws of the same frame": `isNewFrame = !isPreview && !isRenderRequest && …` (`URP/Runtime/TemporalAA.cs:420-424`).
- History is **not reset** while `camera.isProcessingRenderRequest` is set, even when its size or format is dirty (`URP/Runtime/History/TaaHistory.cs:110-117`). SSAO and color history have the same guard (`SSAOHistory.cs:174-175`, `ColorHistory.cs:181-182`).
- Consequence: capturing the 1080p game camera at 4K through a request reuses 1080p TAA history. Use no TAA, or a dedicated camera whose size never changes.

### 1.4 Readback and gamma
- `AsyncGPUReadback.Request(Texture, int mip, GraphicsFormat dstFormat, Action<…>)`: "If the target format is different from the format stored on the GPU, the conversion is automatic" (`XML:35883-35901`). Request **the RT's own format** to get raw bytes. `WaitForCompletion` is documented at `XML:36700` and `WaitAllRequests` at `XML:36639`.
- `GetData` throws if the request is pending or disposed (Unity docs). Copy the data (`ToArray()`) immediately.
- sRGB integer formats always store gamma-encoded data. The hardware encodes on write to an `_SRGB` view and decodes on sampling (Microsoft "Converting data for the color space"). So the raw bytes read from an `R8G8B8A8_SRGB` RT **are sRGB-encoded** and can go into a PNG unchanged.
- `ImageConversion.EncodeArrayToPNG(Array, GraphicsFormat, uint w, uint h, uint rowBytes)` exists (`…/UnityEngine.ImageConversionModule.xml:39`). `EncodeNativeArrayToPNG` is at `:81`.
- `Texture2D(int,int,TextureFormat,bool mipChain,bool linear)` exists (`XML:59947`). `ReadPixels(Rect,int,int,bool)` is at `XML:60209`.
- Forum reports disagree about `ReadPixels` and sRGB (Unity forum 784977; a DX12 issue-tracker entry). Use ReadPixels only as a fallback, with `linear:false` for an sRGB RT, and **prove it with the self-test in §3.3**.
- Project colour space is Linear (`ProjectSettings/ProjectSettings.asset:49`, `m_ActiveColorSpace: 1`).

### 1.5 ScreenCapture and WaitForEndOfFrame
- `CaptureScreenshotAsTexture`: "always wait until the frame rendering process is complete … call it from a coroutine that yields on WaitForEndOfFrame". `superSize` gives an N×N larger image (Unity docs).
- `CaptureScreenshot` captures "the final frame presented … not a capture from a specific Camera". Relative paths are resolved against the project folder in the Editor and on desktop players (Unity docs).
- "WaitForEndOfFrame does not run when you run the Editor in batch mode" (Unity docs, WaitForEndOfFrame). Backbuffer capture is therefore **player-only** (or Editor with a visible Game view).
- Do not use `superSize` for 4K. How it interacts with URP post-processing is not verified, so use the RT path instead.

### 1.6 Time.captureDeltaTime / captureFramerate
- If it is non-zero, `Time.time` advances by `captureDeltaTime` (scaled by `timeScale`) each frame "regardless of real time and the duration of a frame". It "does not affect Time.unscaledTime" (Unity docs).
- `captureFramerate` is `1/captureDeltaTime` rounded to an int. Setting it also sets `captureDeltaTime`, and 0 disables both (Unity docs; `XML:61452-61466`). `captureDeltaTimeRational` also exists (`XML:61457`).
- Audio and `realtimeSinceStartup` keep running in real time, so never drive visuals from them.

### 1.7 Editor batchmode
- `-batchmode` suppresses dialogs. It exits with code 1 on script exceptions. "You can't open a project in batch mode while the Editor has the same project open" (Unity editor CLI docs).
- `-nographics` means "Unity doesn't initialize the graphics device", so do not pass it for captures.
- `-executeMethod` methods must be static and live in an Editor folder. Arguments are read with `System.Environment.GetCommandLineArgs`. Errors are reported by throwing (exit 1) or by calling `EditorApplication.Exit(nonZero)` (docs; `EditorApplication.Exit(int)` at `…/UnityEditor.CoreModule.xml:14407`).
- `-quit` "Quit the Unity Editor after other commands have finished executing" and may hang on async code. Without `-quit` the Editor keeps ticking `EditorApplication.update`.
- The UTF batch runner does exactly this:
  - `[InitializeOnLoadMethod]` → `Environment.GetCommandLineArgs` → `EditorApplication.update` callbacks → `EditorApplication.Exit` (`UTF/UnityEditor.TestRunner/CommandLineTest/TestStarter.cs:10-66`).
  - It enters play mode with `EditorApplication.isPlaying = true` (`UTF/UnityEditor.TestRunner/TestRun/Tasks/EnterPlayModeTask.cs:10`).
  - Return codes are `Ok=0`, `Failed=2`, `RunError=3`, `PlatformNotFound=4` (`UTF/UnityEditor.TestRunner/CommandLineTest/Executer.cs:91-97`). The UTF docs confirm that `-runTests -batchmode -testPlatform PlayMode` runs PlayMode tests in the Editor.
- `ShaderUtil.allowAsyncCompilation` can be set at a call site. URP itself sets it to `false` around rendering and restores it afterwards (`URP/Runtime/RendererFeatures/SurfaceCacheGIRendererFeature/SurfaceCacheWorldAdapter.cs:981-984`). `ShaderUtil.anythingCompiling` also exists (`UnityEditor.CoreModule.xml:55722-55731`).
- Project setting **Enter Play Mode Options is ON with option 3** (DisableDomainReload | DisableSceneReload) (`ProjectSettings/EditorSettings.asset:29-30`). Statics and `UnityEngine.Random` state survive play sessions.

### 1.8 Player CLI, logs, exit codes
- Player flags (Unity docs, 6000.3 player CLI page):
  - `-screen-width` / `-screen-height`: "must be an integer from a supported resolution".
  - `-screen-fullscreen 0|1`.
  - `-window-mode exclusive|borderless` (Windows only; there is **no** "windowed" value, use `-screen-fullscreen 0`).
  - `-popupwindow`, `-monitor N`, `-screen-quality <name>`.
  - `-logFile <path>` (`-` writes to stdout; quote paths that contain spaces), `-nolog`, `-timestamps`.
  - `-force-d3d11`, `-force-d3d12`.
  - `-batchmode` ("doesn't display anything or accept user input") and `-nographics`.
- Forum threads report **black or grey RT readbacks and "Forcing GfxDevice: Null" with player `-batchmode`**. Do not use player batchmode for captures.
- Default log: `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\Player.log` (Unity log-files docs). For this project that is `…\LocalLow\DefaultCompany\3rd Megastructures\Player.log` (`ProjectSettings.asset:15-16`). Always pass `-logFile` with an absolute path.
- `Application.Quit(int exitCode)`: "exit code to return when the player application terminates on Windows, Mac and Linux" (`XML:729-734`). The call "is ignored in the Editor" (docs).
- `Screen.SetResolution`: "applied at the end of the current frame"; "If no matching resolution is supported, the closest one is set"; in the Editor it only affects the Game view (docs).
- `Application.runInBackground` can be set at runtime (`XML:486`). The project currently has `runInBackground: 0` (`ProjectSettings.asset:90`), so the capture driver must set it. `fullscreenMode: 1` (FullScreenWindow) is at `:116`. Windows APIs are `[0x02 D3D11, 0x12 D3D12]` (`:541-543`).
- PowerShell `Start-Process` starts processes asynchronously. "When using the Wait parameter, Start-Process waits for the process tree (the process and all its descendants)". `ArgumentList` items that contain spaces need escaped quotes (Microsoft docs).

### 1.9 Profiler counters and FrameTimingManager
- Rendering counters available in **release players**: "SetPass Calls Count", "Draw Calls Count", "Vertices Count" (exact names in the doc example, `ProfilerCategory.Render`). Batches, Triangles and Shadow Casters are also listed as available in release players.
- "GC Allocated In Frame" is a Memory-module statistic. The Unity 6.2 page says the Objects statistics "aren't available in release players", so check `Valid`.
- `ProfilerRecorder` works "in Editor and Player builds, including Release Players". `ProfilerRecorderHandle.GetAvailable(List<>)` and `GetDescription(handle).Name` let you find counters by name (`XML:68049-68061, 68004-68039`). `ProfilerRecorderOptions.Default = SumSamplesInFrame | WrapAroundWhenCapacityReached`, and you must call `Start()` (`XML:69060`).
- Frame-timing counters (CPU Total, CPU Main Thread, CPU Render Thread and GPU Frame Time) are in **nanoseconds**. The doc example uses `ProfilerCategory.Internal, "CPU Main Thread Frame Time"`. "If you use ProfilerRecorder … you don't need to enable the Frame Timing Stats property" (6000.6 manual).
- The `FrameTiming` struct's `gpuFrameTime` is **in ms** (`XML:13631-13635`). FrameTimingManager has a "set delay of four frames" and supports Windows DX11/DX12. It is always active in development builds and off by default in release builds, where `PlayerSettings.enableFrameTimingStats` is needed (`UnityEditor.CoreModule.xml:41232`; project value `ProjectSettings.asset:160` is `0`).

## 2. Recommended architecture

```
CaptureDriver (runtime, DontDestroyOnLoad)  <- created only if "-thread-capture" is on the command line
  ├─ applies: runInBackground, vSync 0, targetFrameRate -1, captureDeltaTime 1/fps, seeds
  ├─ for each shot: CaptureHooks.SeekWalkTime(t) → settle frames → LateUpdate(order 32000):
  │     CaptureUtil.RenderToSrgbBytes(cam, 3840, 2160, msaa 4) → PNG
  │     (player only) WaitForEndOfFrame → backbuffer PNG + RT@window-res vs backbuffer self-test
  ├─ PerfProbe pass (no requests in those frames) → perf.tsv
  └─ Finish(code): Editor-batch → EditorApplication.Exit(code); player → Application.Quit(code)
CaptureBatch (Editor) : -executeMethod entry, opens scene, isPlaying = true, watchdog → Exit(3)
```
Exit codes: `0` ok, `1` Unity exception (batch), `2` some shots failed or self-test failed, `3` timeout or unexpected exit from play mode, `4` null graphics device, `5` bad arguments.

## 3. Snippets (compile-ready for 6000.6 / URP 17.6)

Do not name a namespace `Thread`, because it clashes with `System.Threading.Thread`. These snippets use `ThreadGame`.

### 3.1 Command-line parser (runtime)
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThreadGame.Capture
{
    /// "-key value" / "-flag" pairs from Environment.GetCommandLineArgs() (Editor: Unity.exe args; player: exe args).
    public static class ThreadCli
    {
        static Dictionary<string, string> s_Args;
        static Dictionary<string, string> Args
        {
            get
            {
                if (s_Args != null) return s_Args;
                s_Args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] a = Environment.GetCommandLineArgs();
                for (int i = 1; i < a.Length; i++)
                {
                    if (!a[i].StartsWith("-", StringComparison.Ordinal)) continue;
                    string key = a[i];
                    bool hasValue = i + 1 < a.Length && !a[i + 1].StartsWith("-", StringComparison.Ordinal);
                    s_Args[key] = hasValue ? a[++i] : string.Empty; // note: negative numbers cannot be values
                }
                return s_Args;
            }
        }
        public static bool Has(string key) => Args.ContainsKey(key);
        public static string Get(string key, string fallback = null)
            => Args.TryGetValue(key, out string v) && v.Length > 0 ? v : fallback;
        public static int GetInt(string key, int fallback)
            => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        public static float GetFloat(string key, float fallback)
            => float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
```

### 3.2 RT render → sRGB bytes → PNG (runtime; works in Editor play mode, edit mode and player)
```csharp
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreadGame.Capture
{
    public static class CaptureUtil
    {
        /// Same choice URP makes for the screen path (UniversalRenderPipelineCore.cs:1477-1492).
        public static GraphicsFormat PipelineColorFormat(Camera cam)
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            bool hdr = asset != null && asset.supportsHDR && cam.allowHDR;
            if (!hdr) return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            if (!Graphics.preserveFramebufferAlpha
                && asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits
                && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend))
                return GraphicsFormat.B10G11R11_UFloatPack32;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Blend))
                return GraphicsFormat.R16G16B16A16_SFloat;
            return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
        }

        /// Full URP stack of `cam` at width x height, MSAA `msaa`. Returns RGBA32 sRGB-encoded bytes,
        /// alpha forced to 255, rows in Unity texture order (verify orientation with SelfTest once).
        public static byte[] RenderToSrgbBytes(Camera cam, int width, int height, int msaa)
        {
            if (RenderPipelineManager.currentPipeline == null) cam.Render(); // force pipeline creation

            var desc = new RenderTextureDescriptor(width, height, PipelineColorFormat(cam),
                                                   CoreUtils.GetDefaultDepthStencilFormat(), 0)
            {
                msaaSamples = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(1, msaa)), 1, 8), // URP reads RT.antiAliasing
                useMipMap = false,
                autoGenerateMips = false,
                useDynamicScale = false,
            };
            var target = new RenderTexture(desc) { name = "ThreadCapture_Target" };
            var srgb = new RenderTexture(width, height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None)
                       { name = "ThreadCapture_sRGB" };
            target.Create();
            srgb.Create();
#if UNITY_EDITOR
            bool oldAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
#endif
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(cam, request))
                    throw new System.InvalidOperationException("StandardRequest unsupported by active pipeline");
#if UNITY_EDITOR
                UnityEditor.ShaderUtil.allowAsyncCompilation = false; // no cyan placeholder shaders
#endif
                RenderPipeline.SubmitRenderRequest(cam, request);   // synchronous; restores cam.targetTexture
                Graphics.Blit(target, srgb);                         // MSAA resolve + linear->sRGB encode on write

                byte[] bytes;
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    AsyncGPUReadbackRequest rb = AsyncGPUReadback.Request(srgb, 0, GraphicsFormat.R8G8B8A8_SRGB, null);
                    rb.WaitForCompletion();
                    if (rb.hasError) throw new System.InvalidOperationException("AsyncGPUReadback failed");
                    bytes = rb.GetData<byte>().ToArray();           // copy now; request data is short-lived
                }
                else
                {
                    RenderTexture prev = RenderTexture.active;       // request reset it (UniversalRenderPipeline.cs:814)
                    RenderTexture.active = srgb;
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false); // sRGB tex for sRGB RT
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    tex.Apply(false, false);
                    RenderTexture.active = prev;
                    bytes = tex.GetRawTextureData();
                    DestroySafe(tex);
                }
                for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
                return bytes;
            }
            finally
            {
#if UNITY_EDITOR
                UnityEditor.ShaderUtil.allowAsyncCompilation = oldAsync;
#endif
                target.Release(); srgb.Release();
                DestroySafe(target); DestroySafe(srgb);
            }
        }

        public static void WritePng(byte[] rgbaSrgb, int width, int height, string path)
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            byte[] png = ImageConversion.EncodeArrayToPNG(rgbaSrgb, GraphicsFormat.R8G8B8A8_SRGB,
                                                          (uint)width, (uint)height, 0);
            File.WriteAllBytes(full, png);
        }

        /// Player (or Editor with visible Game view) only: WaitForEndOfFrame never fires in Editor batch mode.
        public static IEnumerator CaptureBackbuffer(string path, System.Action<Texture2D> inspect = null)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                inspect?.Invoke(tex);
                if (path != null) File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            }
            finally { DestroySafe(tex); }
        }

        public static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
```
For frame sequences, cache `target` and `srgb` instead of reallocating them per frame. VRAM at 3840×2160 with 4x MSAA is roughly 0.4 GB for the target plus the same again for URP intermediates.

### 3.3 Gamma and orientation self-test (player, run once per build)
Render the RT at **window resolution** in the same frame as the backbuffer capture and compare. A pass needs mean |Δ| < 0.5 and max |Δ| ≤ 2, with `flipped == false`.
```csharp
// inside CaptureDriver (3.4), using fields m_Out/m_Msaa
IEnumerator SelfTest()
{
    int w = Screen.width, h = Screen.height;
    byte[] rt = null;
    m_PendingAction = () => rt = CaptureUtil.RenderToSrgbBytes(CaptureHooks.GetCamera(), w, h, m_Msaa);
    Color32[] bb = null;
    yield return CaptureUtil.CaptureBackbuffer(null, t => bb = t.GetPixels32()); // same frame as LateUpdate
    if (rt == null || bb == null || bb.Length * 4 != rt.Length) { m_Failures++; yield break; }
    double d0 = MeanAbs(rt, bb, w, h, false), d1 = MeanAbs(rt, bb, w, h, true);
    bool flipped = d1 < d0;
    Debug.Log($"[Capture] selftest meanAbs={System.Math.Min(d0, d1):F3} flipped={flipped}");
    if (System.Math.Min(d0, d1) > 0.5 || flipped) m_Failures++;
}
static double MeanAbs(byte[] a, Color32[] b, int w, int h, bool flipY)
{
    long sum = 0;
    for (int y = 0; y < h; y++)
    for (int x = 0; x < w; x++)
    {
        int ia = (y * w + x) * 4; Color32 c = b[(flipY ? h - 1 - y : y) * w + x];
        sum += System.Math.Abs(a[ia] - c.r) + System.Math.Abs(a[ia + 1] - c.g) + System.Math.Abs(a[ia + 2] - c.b);
    }
    return sum / (3.0 * w * h);
}
```
Screen-space-overlay UI appears only in the backbuffer (§1.2). Disable it during the self-test, or make capture-visible UI Screen Space - Camera or World Space.

### 3.4 Capture driver (runtime)
```csharp
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreadGame.Capture
{
    /// The game registers these (e.g. walk controller Awake). Seek must set ALL visual state from t.
    public static class CaptureHooks
    {
        public static System.Func<Camera> GetCamera = () => Camera.main;
        public static System.Action<float> SeekWalkTime;
    }

    [DefaultExecutionOrder(32000)] // LateUpdate after camera rigs
    public sealed partial class CaptureDriver : MonoBehaviour
    {
        struct Shot { public string name; public float t; }
        readonly List<Shot> m_Shots = new List<Shot>();
        readonly StringBuilder m_Manifest = new StringBuilder("name\tt\tframe\tfile\n");
        string m_Out; int m_W, m_H, m_Msaa, m_Warmup, m_Settle, m_Fps, m_Failures;
        System.Action m_PendingAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!ThreadCli.Has("-thread-capture")) return;
            var go = new GameObject("~CaptureDriver");
            DontDestroyOnLoad(go);
            go.AddComponent<CaptureDriver>();
        }

        void Awake()
        {
            m_Out = Path.GetFullPath(ThreadCli.Get("-thread-capture", "Captures"));
            m_W = ThreadCli.GetInt("-thread-width", 3840);
            m_H = ThreadCli.GetInt("-thread-height", 2160);
            m_Msaa = ThreadCli.GetInt("-thread-msaa", 4);
            m_Warmup = ThreadCli.GetInt("-thread-warmup", 30);
            m_Settle = ThreadCli.GetInt("-thread-settle", 3);
            m_Fps = ThreadCli.GetInt("-thread-fps", 60);
            foreach (string s in ThreadCli.Get("-thread-shots", "default@0").Split(','))
            {
                string[] p = s.Split('@');
                float t = p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 0f;
                m_Shots.Add(new Shot { name = p[0].Trim(), t = t });
            }
            Directory.CreateDirectory(m_Out);
            Application.runInBackground = true;          // project has runInBackground: 0
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = 1f / m_Fps;          // game time decoupled from wall clock
            Random.InitState(ThreadCli.GetInt("-thread-seed", 1234));
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Finish(4, "Null gfx device"); yield break; }
            bool canBackbuffer = !Application.isBatchMode;   // WaitForEndOfFrame is dead in Editor batch
            for (int i = 0; i < m_Warmup; i++) yield return null;
            if (canBackbuffer && !Application.isEditor) yield return SelfTest();
            foreach (Shot shot in m_Shots)
            {
                CaptureHooks.SeekWalkTime?.Invoke(shot.t);
                for (int i = 0; i < m_Settle; i++) yield return null;
                Shot s = shot;
                m_PendingAction = () => CaptureShot(s);       // runs in this frame's LateUpdate
                if (canBackbuffer)
                    yield return CaptureUtil.CaptureBackbuffer(Path.Combine(m_Out, s.name + "_window.png"));
                else
                    yield return null;
            }
            File.WriteAllText(Path.Combine(m_Out, "manifest.tsv"), m_Manifest.ToString());
            Finish(m_Failures == 0 ? 0 : 2, null);
        }

        void LateUpdate()
        {
            if (m_PendingAction == null) return;
            System.Action a = m_PendingAction; m_PendingAction = null;
            try { a(); } catch (System.Exception e) { m_Failures++; Debug.LogException(e); }
        }

        void CaptureShot(Shot shot)
        {
            byte[] px = CaptureUtil.RenderToSrgbBytes(CaptureHooks.GetCamera(), m_W, m_H, m_Msaa);
            string file = Path.Combine(m_Out, shot.name + ".png");
            CaptureUtil.WritePng(px, m_W, m_H, file);
            m_Manifest.Append(shot.name).Append('\t').Append(shot.t.ToString("R", CultureInfo.InvariantCulture))
                      .Append('\t').Append(Time.frameCount).Append('\t').Append(file).Append('\n');
        }

        void Finish(int code, string error)
        {
            if (error != null) Debug.LogError("[Capture] " + error);
            Debug.Log($"[Capture] finished, failures={m_Failures}, exit={code}");
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool("ThreadCapture.Active", false); // else CaptureBatch's EnteredEditMode hook overrides code with 3
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);                       // ignored in Editor; exit code on Windows player
#endif
        }
    }
}
```
Put `SelfTest`/`MeanAbs` from §3.3 in the same class; that is why it is `partial`.

**Sequences:** call `SeekWalkTime(t0)` once. Then let the game run naturally, because `captureDeltaTime` makes frame n land exactly on `t0 + n/fps`. Set `m_PendingAction = () => CaptureShot(name_{n:D5})` on every frame until `t1`.

### 3.5 Editor batch entry (put in an Editor folder or editor asmdef that references the runtime asmdef)
```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ThreadGame.Capture;

namespace ThreadGame.EditorTools
{
    [InitializeOnLoad] // re-runs after every domain reload; command-line args are process-level
    public static class CaptureBatch
    {
        const string kActive = "ThreadCapture.Active", kDeadline = "ThreadCapture.Deadline";

        static CaptureBatch()
        {
            EditorApplication.update += Watchdog;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(kActive, false))
                { Debug.LogError("[CaptureBatch] left play mode before finishing"); EditorApplication.Exit(3); }
            };
        }

        // Unity.exe -batchmode -projectPath <p> -logFile <abs> -executeMethod ThreadGame.EditorTools.CaptureBatch.Run
        //           -thread-capture <absDir> [-thread-scene Assets/_Game/Scenes/Thread.unity] [-thread-timeout 900]
        public static void Run()
        {
            if (!Application.isBatchMode || !ThreadCli.Has("-thread-capture"))
            { Debug.LogError("[CaptureBatch] needs -batchmode and -thread-capture"); EditorApplication.Exit(5); return; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { Debug.LogError("[CaptureBatch] null graphics device (remove -nographics)"); EditorApplication.Exit(4); return; }
            string scene = ThreadCli.Get("-thread-scene", "Assets/_Game/Scenes/Thread.unity");
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            SessionState.SetBool(kActive, true);
            SessionState.SetFloat(kDeadline,
                (float)(EditorApplication.timeSinceStartup + ThreadCli.GetInt("-thread-timeout", 900)));
            EditorApplication.isPlaying = true; // same as UTF EnterPlayModeTask.cs:10; driver bootstraps itself
        }

        static void Watchdog()
        {
            if (!SessionState.GetBool(kActive, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(kDeadline, float.MaxValue))
            { Debug.LogError("[CaptureBatch] timeout"); EditorApplication.Exit(3); }
        }
    }
}
```
**Edit-mode alternative** (no play mode): call `CaptureUtil.RenderToSrgbBytes` directly from `Run()` after building the procedural world with an edit-mode-safe static builder. Volumes update every render in edit mode (`UniversalRenderPipeline.cs:1428-1430`). Use this only if the world can be built without Awake/Start.

### 3.6 Perf probe (player; run in a pass with no capture requests)
```csharp
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace ThreadGame.Capture
{
    public sealed class PerfProbe : System.IDisposable
    {
        public static readonly string[] Names = {
            "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Batches Count",
            "GC Allocated In Frame", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time" };
        readonly Dictionary<string, ProfilerRecorder> m_Rec = new Dictionary<string, ProfilerRecorder>();
        readonly FrameTiming[] m_Ft = new FrameTiming[1];
        long m_LastMono;

        public PerfProbe()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);            // match by name, any category
            foreach (ProfilerRecorderHandle h in handles)
            {
                string n = ProfilerRecorderHandle.GetDescription(h).Name;
                if (System.Array.IndexOf(Names, n) < 0 || m_Rec.ContainsKey(n)) continue;
                var r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
                r.Start();
                m_Rec[n] = r;
            }
            foreach (string n in Names) if (!m_Rec.ContainsKey(n)) Debug.LogWarning("[Perf] counter missing: " + n);
        }

        /// -1 when unavailable. Frame-time counters are ns (divide by 1e6 for ms).
        public long Get(string name) => m_Rec.TryGetValue(name, out ProfilerRecorder r) && r.Valid ? r.LastValue : -1;

        /// Call once per frame. Returns FrameTimingManager GPU ms (4-frame latency) or -1.
        public double SampleGpuMs()
        {
            FrameTimingManager.CaptureFrameTimings();
            return FrameTimingManager.GetLatestTimings(1, m_Ft) > 0 ? m_Ft[0].gpuFrameTime : -1.0;
        }

        /// Release-safe GC fallback: >0 means managed heap grew this frame.
        public long MonoDelta()
        {
            long now = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            long d = now - m_LastMono; m_LastMono = now; return d;
        }

        public void Dispose() { foreach (ProfilerRecorder r in m_Rec.Values) r.Dispose(); m_Rec.Clear(); }
    }
}
```
Dump `ProfilerRecorderHandle.GetAvailable` names to a file once in the built player to confirm the exact strings ("Triangles Count" and "Batches Count" are inferred from the "… Count" naming). Report averages and maxima over at least 300 frames after warm-up. `FrameTimingManager.IsFeatureEnabled()` must return true.

### 3.7 Build-script settings (editor, before `BuildPipeline.BuildPlayer`)
```csharp
PlayerSettings.enableFrameTimingStats = true;   // FrameTimingManager in release (currently 0)
PlayerSettings.usePlayerLog = true;
PlayerSettings.runInBackground = true;          // driver also sets it at runtime
PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; // viewers; CLI overrides for captures
```

### 3.8 Launchers
PowerShell (Windows PowerShell 5.1):
```powershell
$proj = "W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
# Editor batch (project must NOT be open in another Editor instance)
$a = "-batchmode -projectPath `"$proj`" -logFile `"$proj\Logs\capture_editor.log`" " +
     "-executeMethod ThreadGame.EditorTools.CaptureBatch.Run -thread-capture `"$proj\Captures\editor`" -thread-timeout 900"
$p = Start-Process -FilePath $unity -ArgumentList $a -PassThru
$null = $p.Handle; $p.WaitForExit(); "editor exit=$($p.ExitCode)"   # not -Wait: it waits for descendants too
# Built player (windowed, real swapchain, NOT -batchmode)
$exe = "$proj\Build\Windows\Thread.exe"
$a = "-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11 -logFile `"$proj\Logs\capture_player.log`" " +
     "-thread-capture `"$proj\Captures\player`" -thread-shots horizon@4,colossi@60,summit@300 -thread-width 3840 -thread-height 2160"
$p = Start-Process -FilePath $exe -ArgumentList $a -PassThru
$null = $p.Handle; if (-not $p.WaitForExit(900000)) { $p.Kill(); "timeout" } else { "player exit=$($p.ExitCode)" }
```
Git Bash waits for the process and keeps normal quoting: `"/w/…/Thread.exe" -screen-fullscreen 0 … ; echo $?`. Kill only the PID you started; never kill other Unity processes.

## 4. Pitfalls (checklist)
1. **LDR destination equals LDR rendering.** URP's intermediates take the RT's format (§1.2), so an `R8G8B8A8_SRGB` target clips HDR before bloom and tonemapping. Use `PipelineColorFormat`.
2. **MSAA must be on the destination RT.** With `antiAliasing = 1` you get no MSAA at all, whatever the URP asset says (`UniversalRenderPipelineCore.cs:1563`). The asset currently has `m_MSAA: 1` (`Assets/Settings/PC_RPAsset.asset:28`).
3. **The destination needs a depth-stencil format.** Without one URP warns and substitutes a format (`:1541-1545`).
4. **SingleCameraRequest** skips `UpdateVolumeFramework` and overlay cameras. Use **StandardRequest**.
5. **TAA and history:** requests do not reset or advance history (§1.3). Prefer 4x MSAA (plus FXAA if needed). Custom temporal passes must skip history writes when `camera.isProcessingRenderRequest`.
6. **Screen-space overlay UI** is never in RT captures (`UniversalCameraData.cs:307,384`).
7. **Async shader compilation** in the Editor gives cyan placeholders. Set `ShaderUtil.allowAsyncCompilation = false` around the request and wait while `ShaderUtil.anythingCompiling`. In the player, warm up for at least 30 frames.
8. **The pipeline may not exist yet** in batch or edit mode, so call `cam.Render()` once first. `SubmitRenderRequest` also resets `RenderTexture.active`.
9. **Editor batch:** no `WaitForEndOfFrame`, no Game view and unreliable `Screen.width/height`. Custom passes must size from render-graph descriptors or `cameraData.cameraTargetDescriptor`, never from `Screen.*`.
10. **`-quit` with `-executeMethod`** quits before play mode runs. End with `EditorApplication.Exit(code)` and keep the watchdog.
11. **Player `-batchmode`** gives black or grey readbacks or a Null device (forum reports). Use a normal window plus `runInBackground`. Log `SystemInfo.graphicsDeviceType` and fail if it is `Null`.
12. **Windowed size:** `-screen-width/height` must be a supported resolution and `SetResolution` picks the closest supported one. A window larger than the desktop is unreliable (not verified), so get 4K from the RT and not from the window. `-window-mode` has no `windowed` value.
13. **PowerShell:** `& Thread.exe` (a GUI-subsystem exe) does not wait. `-Wait` waits for descendant processes (shader compilers or helpers can hang it). Use `-PassThru`, `$p.Handle` and `WaitForExit()`, and add escaped quotes around paths that contain spaces.
14. **captureDeltaTime does not touch `unscaledTime`, `realtimeSinceStartup` or audio.** The game must not animate from them. Set `fixedDeltaTime` to a divisor of the capture dt for deterministic physics.
15. **Enter Play Mode Options (no domain or scene reload)** is ON. Static state and `Random` state leak across Editor runs. Reset them in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
16. **Resolution-dependent looks:** pixel-unit outline widths, dither and noise tiling and bloom iteration count change between 1080p and 4K. Express widths relative to `_ScreenParams` or the target height.
17. **Render scale and upscaler** also apply to RT cameras. Keep `renderScale = 1` (it is now 1) and `allowDynamicResolution = false`.
18. **A camera with an explicitly set `camera.aspect`** does not follow the RT. Call `ResetAspect()` before capturing.
19. **Perf counters include the capture render.** Measure in frames without requests. "GC Allocated In Frame" may be invalid in release builds, so use the `MonoDelta` fallback. Frame-time counters are in ns; `FrameTiming.gpuFrameTime` is in ms with a 4-frame lag.
20. **Alpha:** an RGBA16F target with `preserveFramebufferAlpha` can carry alpha below 1. The snippet forces 255.
21. **Relative paths** in the player resolve against the process working directory. Always pass absolute `-thread-capture` and `-logFile` paths.
22. **HDR display output** (`allowHDRDisplaySupport: 0`, `ProjectSettings.asset:162`) must stay off, or backbuffer captures become HDR10.

## 5. Sources (accessed 2026-10-29)
| URL | Used for |
|---|---|
| https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player | Player flags: -screen-*, -window-mode values, -logFile, -batchmode/-nographics, -force-d3d11/12 |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor | -batchmode exit code 1, single-instance rule, -executeMethod and GetCommandLineArgs, -quit caveats, -nographics |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/time/capturedeltatime | captureDeltaTime semantics and unscaledTime note |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/captureframerate | captureFramerate ↔ captureDeltaTime |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/waitforendofframe | WaitForEndOfFrame does not run in Editor batch mode |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshotastexture | Must wait for end of frame; superSize |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screencapture/capturescreenshot | Captures the presented frame; path resolution |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/setresolution | Applied at end of frame; closest supported resolution |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/quit | Quit ignored in Editor; exit code |
| https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendering/asyncgpureadbackrequest/getdata | GetData validity rules |
| https://docs.unity3d.com/6000.2/Documentation/Manual/urp/User-Render-Requests.html | URP SingleCameraRequest usage (cross-check) |
| https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/profile-rendering/profiler-rendering | Render counter names; release-player availability; ProfilerRecorder example |
| https://docs.unity3d.com/6000.2/Documentation/Manual/ProfilerMemory.html | "GC Allocated In Frame"; Objects stats not in release players |
| https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html | ProfilerRecorder works in release players; GetAvailable |
| https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/counter-reference | Frame-timing counter names, ns units |
| https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/record-timing-data | ProfilerRecorder "CPU Main Thread Frame Time" example |
| https://docs.unity.cn/6000.6/Documentation/Manual/frame-timing-manager-enable.html | Frame Timing Stats: off in release, always on in dev; ProfilerRecorder alternative |
| https://docs.unity3d.com/6000.2/Documentation/Manual/frame-timing-manager.html | 4-frame delay; DX11/DX12 support |
| https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html | -runTests / -testPlatform PlayMode in batch mode |
| https://docs.unity3d.com/6/Documentation/Manual/log-files.html | Player.log location on Windows |
| https://discussions.unity.com/t/how-to-use-batchmode-command-line-argument-for-standalone-player/858372 | Player -batchmode gives black RT readback (report) |
| https://discussions.unity.com/t/how-to-enable-graphics-device-in-batchmode/177826 | "Forcing GfxDevice: Null" in batchmode (old report) |
| https://discussions.unity.com/t/screenshot-encoding-very-dark-in-linear-color-space-784977/784977 | ReadPixels / linear-flag gamma confusion (why the self-test exists) |
| https://issuetracker-mig.prd.it.unity3d.com/issues/readpixels-behave-differently-on-dx12-when-the-project-is-set-to-linear-color-space | DX11 vs DX12 ReadPixels gamma difference (historic) |
| https://learn.microsoft.com/en-us/windows/desktop/direct3ddxgi/converting-data-color-space | _SRGB integer formats store gamma-encoded data |
| https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/start-process?view=powershell-5.1 | -Wait waits for descendants; ArgumentList quoting; async by default |
