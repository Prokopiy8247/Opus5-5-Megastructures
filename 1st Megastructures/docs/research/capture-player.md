## Deterministic capture, player build behaviour, text and CLI (Unity 6000.6.0f1 / URP 17.6.0)

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
