# RESEARCH — Thread

Research sprint of Phase 1 (MASTER_PROMPT Part 4), run as 8 parallel read-only researchers on 2026-10-29.
Each topic has a detailed note in `docs/research/` with verified facts (package source file:line), code snippets, pitfalls and sources.
Decisions that came out of the research are recorded in `DEVELOPMENT.md`.

## R1 Render Graph grade pass, bloom, HDR precision, URP asset scripting
Detailed note: [`docs/research/R1_render_graph_grade.md`](research/R1_render_graph_grade.md)

Key findings:
1. **Render Graph is the only path.** `ScriptableRenderPass.Execute/Configure/OnCameraSetup` do not exist in 17.6. Implement `RecordRenderGraph` only.
2. **Use Option B.** Turn URP post-processing off on the gameplay camera and run our own bloom and ThreadGrade in one renderer feature at `AfterRenderingPostProcessing`. With URP post on, URP always runs a LUT pass (LDR mode: `saturate` plus an 8-bit LUT) and dithers *before* any after-post pass. Either one rules out banding-free output.
3. **Set `hdrColorBufferPrecision = _64Bits`** (camera colour becomes `R16G16B16A16_SFloat`). The default `_32Bits` gives `B10G11R11_UFloatPack32`. Its 5-bit blue mantissa is coarser than one 8-bit sRGB step in the midtones, which shows up as blue/yellow banding in skies.
4. **Write the final result into a fresh `R8G8B8A8_SRGB` RG texture.** Dither (TPDF, in sRGB code space) right before this write, then set `resourceData.cameraColor = out`. URP's FinalBlit then makes an exact 8-bit copy to the screen and handles y-flip, viewport, `requiresSrgbBlitToBackbuffer`, Recorder capture and the overlay UI. Writing straight to `backBufferColor` (B2) is shown as an option, but it needs manual y-flip and sRGB handling and breaks Camera Capture.
5. **Use MSAA 4x from the asset** (`msaaSampleCount = 4`), with FXAA/SMAA/TAA off (they only exist inside URP post). The MSAA camera target resolves automatically when we `UseTexture` it.

Sources (accessed 2026-10-29):
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

## R2 Handwritten URP 17.6 shaders (Lit passes, keywords, skybox, depth, stripping)
Detailed note: [`docs/research/R2_urp_lit_shader.md`](research/R2_urp_lit_shader.md)

Key findings:
| Topic | Decision |
|---|---|
| Forward+ keyword | Use **`_CLUSTER_LIGHT_LOOP`**. `_FORWARD_PLUS` is deprecated: it still works, but it emits a `#warning`. **Declare it even with only one directional light** (see 1.3, which is a real bug). |
| Main-light shadows | Copy Lit's exact lines: `multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` and `multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`. URP stripping removes the unused variants by keyword name. |
| Shadow lookup | Compute the shadow coordinate per pixel the way Lit does (`REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR` / `MAIN_LIGHT_CALCULATE_SHADOWS`). Call **`GetMainLight(shadowCoord, positionWS, half4(1,1,1,1))`**: unlike `GetMainLight(shadowCoord)`, it applies the shadow-distance fade, so there is no hard cutoff line. |
| Passes per opaque shader | `ForwardLit` (UniversalForward), `ShadowCaster`, `DepthOnly`, `DepthNormals`. All four are mandatory. Without DepthNormals or DepthOnly, an object disappears from `_CameraDepthTexture` and from SSAO in this project (see 3.2). |
| Material data | Declare one `CBUFFER_START(UnityPerMaterial)` in a SubShader-level `HLSLINCLUDE`, identical for every pass. Keep globals outside it and **out of the `Properties` block**. |
| Instancing | Add `#pragma multi_compile_instancing` and `#include_with_pragmas ".../DOTS.hlsl"` to every pass. The project has GPU Resident Drawer on (`PC_RPAsset.asset:85 m_GPUResidentDrawerMode: 1`) and BRG variants are kept (`GraphicsSettings.asset:49 m_BrgStripping: 2` = `KeepAll`, verified with Cecil on `UnityEditor.CoreModule.dll`). |
| Fog | Do not use Unity fog: no `Fog.hlsl` include and no `multi_compile_fog`. Fog comes from our own global uniforms instead. |
| Skybox | Build the ray from the **object-space position of the sky mesh**, as `mul((float3x3)M, positionOS)`, and normalize it per pixel. Use `Cull Off ZWrite Off`, keep the default ZTest, and draw sun and planets analytically with `fwidth` anti-aliasing. |
| Soft depth fade | Depth texture is ON (`PC_RPAsset.asset:22`) and the renderer's Depth Texture Mode is **After Opaques** (`PC_Renderer.asset:58 m_CopyDepthMode: 0`). Keep both. |
| Variants in build | Keep serialized `Shader` references in the boot scene, and also add the shaders to Always Included. Use **no `shader_feature`** on runtime-created materials. |

Sources (accessed 2026-10-29):
| URL | Used for |
|---|---|
| https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus | Unity 6.6 manual: `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`, and that `LIGHT_LOOP_BEGIN` requires an `InputData inputData` with `normalizedScreenSpaceUV` |
| https://issuetracker-mig.prd.it.unity3d.com/issues/getmainlight-returns-incorrect-distanceattenuation-value-for-a-custom-shader-when-urp-forward-plus-rendering-path-is-used | Confirms the Forward+ `distanceAttenuation`=0 black-shader issue ("By Design") |
| https://raw.githubusercontent.com/TwoTailsGames/Unity-Built-in-Shaders/master/DefaultResourcesExtra/Skybox-Procedural.shader | Built-in skybox tags, `Cull Off ZWrite Off`, eye ray from `mul((float3x3)unity_ObjectToWorld, v.vertex.xyz)` (MIT) |
| https://aras-p.info/blog/2019/02/01/Infinite-sky-shader-for-Unity/ | Pushing sky depth to the far plane under reversed Z (`z = 1e-9`) |
| https://discussions.unity.com/t/why-cant-the-unfilled-area-of-the-_cameradepthtexture-be-1/690778 | Reversed Z on D3D, and ZTest LEqual auto-flipped to GEqual (community answer) |
| https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-writing-shader-programs-hlsl/sl-platform-differences | Depth direction table (DirectX/Metal/Vulkan 1→0), `SystemInfo.usesReversedZBuffer`, `UNITY_REVERSED_Z` |
| https://docs.unity3d.com/550/Documentation/Manual/UpgradeGuide55.html | Reversed-Z introduction; `LinearEyeDepth` helpers handle it |
| https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/in-universalrp-srp-batcher | SRP Batcher: `UnityPerMaterial` / `UnityPerDraw` requirements |
| https://docs.unity3d.com/2023.2/Documentation/Manual/SRPBatcher.html | A material property outside `UnityPerMaterial` makes the shader incompatible; no MaterialPropertyBlocks |
| https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html (search summary) and https://docs.unity3d.com/Manual/gpu-instancing-enable.html (search summary) | The SRP Batcher takes priority over GPU instancing |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/setglobalvector | Globals are used only when the material does not define the property (not in Properties) |
| https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings | Always Included Shaders include all variants (even shader_feature); Instancing Variants strip modes; preloading |
| https://docs.unity3d.com/Manual/shader-variant-stripping.html (search summary) | Unused shader_feature variants are stripped; Always Included as the fix |
| https://docs.unity3d.com/ScriptReference/Shader.Find.html (search summary) | Ways to get a shader into a build (scene reference / Always Included / Resources) |
| https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-alpha-to-mask | AlphaToMask On/Off semantics |
| https://docs.unity3d.com/ScriptReference/Rendering.BatchRendererGroupStrippingMode.html (search summary) | BRG stripping modes; numeric values verified locally with Unity.Cecil (KeepAll = 2) |
| https://docs.unity3d.com/Manual/dots-instancing-shaders-declare.html (search summary) | DOTS instancing blocks; URP defines BuiltinPropertyMetadata for every shader |
| https://www.cyanilux.com/tutorials/urp-shader-code/ | Community confirmation: a shared HLSLINCLUDE `UnityPerMaterial` across passes, no `UsePass` for the shadow pass, do not include LitInput.hlsl |
Local ground truth that supersedes the web where they differ:
- URP 17.6 / Core 17.6 package source (file:line references above);
- `UnityEngine.CoreModule.xml` / `UnityEditor.CoreModule.dll` in `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine`;
- project settings assets: `Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset`, `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset`, `ProjectSettings.asset`.

## R3 Procedural meshes, instancing, sweeps and lathes
Detailed note: [`docs/research/R3_meshes_instancing.md`](research/R3_meshes_instancing.md)

Key findings:
1. **The API is `Mesh.AllocateWritableMeshData`, not `AcquireWritableMeshData`.** No method named `AcquireWritableMeshData` exists in 6000.6 (the IL lists only `AllocateWritableMeshData(int | Mesh | Mesh[] | List<Mesh>)`). `AcquireReadOnlyMeshData` is the read-only counterpart.
2. **Unique geometry uses static `MeshRenderer` GameObjects with the SRP Batcher.** This covers the cable LOD0/1/2 chunks, walkway, colossi, stairs, terrace and beads. Use no `MaterialPropertyBlock`s; per-colour materials or vertex colour instead.
3. **Repeated props use `Graphics.RenderMeshInstanced`, chunked at ≤ 500 instances per call with explicit per-chunk `worldBounds`.** This covers rail posts (~470), window dots (≤ 1296) and bollards (28). Chunks are spatially sorted. Fallback ladder step: bake posts/bollards into the walkway/stair chunk meshes, which costs ~50 k triangles.
4. **Generate meshes at runtime during the opening fade.** The generators are deterministic pure functions, and the cable LOD0 costs ~25 ms of plain C#. Do not ship baked `.asset` meshes; keep an optional Editor "bake for inspection" menu only.
5. **Turn the GPU Resident Drawer OFF.** It is currently **ON** in `Assets\Settings\PC_RPAsset.asset:85` (`m_GPUResidentDrawerMode: 1`). The spec says it is not needed (MASTER_PROMPT.md line 880), and handwritten ThreadFlat without `DOTS_INSTANCING_ON` is excluded from it anyway.
6. **Burst, Mathematics and Jobs are usable without touching `manifest.json`.** In 6000.6 they are engine modules, and Collections is a resolved builtin dependency. They are not required: use them only if a generator measures > 100 ms.
7. **Do not use rotation-minimising frames for the helix walkway deck.** On this helix the RMF rolls ~87° relative to "radially level" over one turn. Use the analytic cylindrical frame. Use RMF (double reflection) only for round tubes and free-form sweeps.

Sources (accessed 2026-10-29):
- Unity Scripting API, Graphics.RenderMeshInstanced (6000.3): https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/graphics/rendermeshinstanced. Used for the 1023/511 limit, the enableInstancing exception, worldBounds auto-computation and the instance-struct fields.
- Unity Scripting API, Mesh.AllocateWritableMeshData: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/allocatewritablemeshdata. Used for disposal semantics and the official sample (RecalculateNormals/Bounds after apply).
- Unity Scripting API, Mesh.SetVertexBufferParams: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/setvertexbufferparams. Used for minimal validation and the maxGraphicsBufferSize exception.
- Unity Scripting API, VertexAttributeDescriptor: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendering/vertexattributedescriptor. Used for attribute order, the 4-byte rule, 4 streams and the recommended streams.
- Unity Scripting API, Mesh.indexFormat: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/indexformat. Used for the 16/32-bit rule, the clear-on-change behaviour and the 0xFFFF caveat.
- Unity Scripting API, AssetDatabase.CreateAsset: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/assetdatabase/createasset. Used for overwrite behaviour, the extension requirement and the no-import-time rule.
- Unity Manual, GPU instancing (6000.3): https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing. Used for SRP Batcher vs instancing for GameObjects and RenderMeshInstanced support.
- Unity Manual, GPU Resident Drawer (URP, 6000.0): https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer. Used for requirements, Keep All and longer build times.
- UnityCsReference, Graphics.cs (GitHub master): https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/Graphics/Graphics.cs. Cross-check of the RenderMeshInstanced checks and RenderParams defaults; differences vs 6000.6 IL are noted (rendererShaderUserValue).
- Unity-Technologies/MeshApiExamples: https://github.com/Unity-Technologies/MeshApiExamples. Used for C# vs Burst vs compute generation timings (300 k-tri noise ball: 2.7 s C#, 22 ms multithreaded Burst).
- W. Wang, B. Jüttler, D. Zheng, Y. Liu, "Computation of Rotation Minimizing Frames", ACM TOG 27(1), 2008, doi:10.1145/1330511.1330513; listing https://www.microsoft.com/en-us/research/?p=344486 and https://hub.hku.hk/handle/10722/152386. Used for the double reflection method (section 3.5).
- Unity Discussions, "GPU instancing count limit": https://forum.unity.com/threads/gpu-instancing-count-limit.591397/. Used as corroboration that callers must chunk; no automatic splitting is documented.

## R4 Deterministic capture, CLI, batchmode, profiling
Detailed note: [`docs/research/R4_capture_cli.md`](research/R4_capture_cli.md)

Key findings:
1. **Render path:** `RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest{ destination = rt })`. It renders the whole URP camera stack synchronously, including the volume update, post-processing and every renderer feature. Do not use `UniversalRenderPipeline.SingleCameraRequest`, which skips the volume update and the camera stack. Use `Camera.Render()` only to force the pipeline to be created.
2. **Capture RT format:** use the **same HDR format URP uses for the screen**. On this project that is `B10G11R11_UFloatPack32`, with MSAA set on the RT itself (`msaaSamples = 4`) and a depth-stencil buffer. Then `Graphics.Blit` into an `R8G8B8A8_SRGB` RT and read it back with `AsyncGPUReadback.Request(srgbRT, 0, GraphicsFormat.R8G8B8A8_SRGB)` followed by `WaitForCompletion()`. Write the bytes with `ImageConversion.EncodeArrayToPNG`. The bytes come back sRGB-encoded, so the PNG matches the backbuffer. Never render straight into an 8-bit RT: URP would then run the whole frame in 8 bits and HDR, bloom and tonemapping would differ (§1.2).
3. **Determinism:** set `Time.captureDeltaTime = 1/fps`, vSync 0 and `targetFrameRate = -1`. Every visual state must be a pure function of an explicit walk time `t`, which a seek hook sets. Use a custom shader-time global, not `_Time` or `unscaledTime`. Use fixed RNG seeds. Do not use TAA (§1.3); 4x MSAA is better for flat colour anyway.
4. **Editor batch captures:** run `Unity.exe -batchmode -projectPath … -logFile … -executeMethod ThreadGame.EditorTools.CaptureBatch.Run -thread-capture <dir>` with **no `-quit` and no `-nographics`**. The method opens the scene and sets `EditorApplication.isPlaying = true`. A runtime `CaptureDriver`, bootstrapped by `[RuntimeInitializeOnLoadMethod]` when it sees `-thread-capture` in `Environment.GetCommandLineArgs()`, takes the shots with `yield return null` (never `WaitForEndOfFrame`) and ends with `EditorApplication.Exit(code)`. An `[InitializeOnLoad]` watchdog calls `Exit(3)` on timeout. This is the same pattern the Unity Test Framework uses for batch PlayMode runs (§1.7).
5. **Built-player captures:** run the player windowed and **not** in `-batchmode`: `Thread.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "<abs>" -thread-capture "<abs dir>" -thread-shots a@4,b@60`. The same driver writes 4K PNGs through the RT path. It also writes a window-resolution backbuffer capture (`WaitForEndOfFrame` + `ScreenCapture.CaptureScreenshotAsTexture`) and an RT-vs-backbuffer self-test, then calls `Application.Quit(exitCode)`. Launch it from PowerShell with `Start-Process -PassThru` and `$p.WaitForExit()`, then read `$p.ExitCode`.
6. **Perf:** use `ProfilerRecorder` handles looked up by name ("Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "GC Allocated In Frame", "CPU Main Thread Frame Time", "GPU Frame Time"; frame times are in ns). Add `FrameTimingManager` (`gpuFrameTime` in ms, 4-frame delay), which needs `PlayerSettings.enableFrameTimingStats = true` (currently 0). Measure in frames with **no** capture request.

Sources (accessed 2026-10-29):
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

## R5 Procedural audio (OnAudioFilterRead, DSP blocks, FDN, analysis)
Detailed note: [`docs/research/R5_audio.md`](research/R5_audio.md)

Key findings:
1. **Engine wiring**: keep the spec's pattern (`ThreadSynth` with `OnAudioFilterRead` on the AudioListener GameObject, adding its layers into `data` and then running the master chain over the whole buffer, so footsteps also get reverb and the limiter). Add a **heartbeat** (`Interlocked.Increment` per callback). If it reads 0 after 0.5 s of unscaled time, fall back to the documented generator pattern: a dedicated GameObject with an `AudioSource` (no clip, `playOnAwake`, `Play()`) plus `ThreadSynth`. Log which path ran.
2. **Filters**: use the **Simper/Cytomic trapezoidal SVF** everywhere. The spec's Chamberlin `f = 2 sin(pi fc/fs)` is **unstable at 48 kHz above about 8.3 kHz at Q 0.707**: the 14 kHz master low-pass of L5/beat 4 blows up to infinity (measured). If Chamberlin is kept, clamp `f < sqrt(q^2+4) - q`.
3. **Limiter ceiling**: `0.89*tanh(x/0.89)` has a -1.01 dBFS asymptote, not -1.5. Use **0.84** (-1.51 dBFS) to meet 14.3.3's "sample peak below -1.5 dBFS".
4. **Sample rate is not guaranteed to be 48 kHz**: `ProjectSettings/AudioManager.asset` has `m_SampleRate: 0`, which means the device default (44.1 kHz on many machines). Compute every coefficient from `AudioSettings.outputSampleRate` and rebuild on `OnAudioConfigurationChanged`. Offline renders use 48 kHz.
5. **Reverb**: 8-line FDN, Householder (`y_i = x_i - 0.25*sum`), base primes 1427 to 3539 samples (48 kHz) times a fixed **size factor 2.2** (re-primed at init; 0.894 s of total delay, enough modal density for RT60 up to about 6 s). Per-line `g_i = 10^(-3 M_i/(RT60 fs))` and a unity-DC one-pole damping filter. Pre-delay changes use a 1 s crossfade between two taps (never move a read pointer).
6. **Parameter smoothing**: 20 ms one-pole per sample for gains and cutoffs. Slow (8 s) reverb blends run at control rate (every 32 samples) in **double**, because a float one-pole with a 1.7 s tau stalls about 0.5 % short of its target.
7. **Analysis**: compute peak, DC, step, RMS per beat and BS.1770 LUFS **in C#** inside the EditMode test (fast, no dependencies). The numpy script is the cross-check. **scipy is not installed and `python` (3.12) has no numpy; use `py -3.10 -I`.**
8. **Player proof**: `-audio-dump` arms a preallocated linear capture written from `OnAudioFilterRead`, and the main thread writes a float32 WAV once it is full. Set `Application.runInBackground = true` in capture/autoplay mode (the project has `runInBackground: 0`, so the player pauses when it is unfocused).

Sources (accessed 2026-10-29):
| # | URL | Used for |
|---|---|---|
| S1 | https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/onaudiofilterread.md | callback semantics, threading, generator rule, interleaving |
| S2 | https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/onaudioconfigurationchanged | device/sample-rate change handling |
| S3 | https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Modules/Audio/Public/ScriptBindings/Audio.bindings.cs | PlayOneShot (extern helper, no managed alloc), AudioClip.Create/SetData validation |
| S4 | https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player | -batchmode / -nographics wording (no audio statement) |
| S5 | https://issuetracker.unity3d.com/issues/onaudiofilterread-does-not-affect-signal-on-audiolistener-after-enable | UUM-116871 fix versions (via search summary) |
| S6 | https://issuetracker-mig.prd.it.unity3d.com/issues/onaudiofilterread-component-as-generator-does-not-stop-when-calling-audiosource-dot-stop ; https://issuetracker.unity3d.com/issues/crash-adding-audiolistener-component-with-onaudiofilterread-crashes-the-editor ; https://issuetracker.unity.com/issues/8769/onaudiofilterread-not-working-as-expected-for-generator-scripts | UUM-117120, UUM-61733 (one AudioSource or AudioListener per filter), UUM-58426 (fixed 2023.2/2023.3) |
| S7 | https://discussions.unity.com/t/unity-5-3-seems-to-break-batchmode-nographics-on-headless-linux/610316 ; https://docs.unity3d.com/Manual/desktop-headless-mode.html | headless audio init attempts; headless mode covers graphics only |
| S8 | https://forum.unity3d.com/threads/discrepancy-between-audio-dsptime-and-state-currdsptick-in-native-plugin.461617/ | dspTime freezes in background without Run In Background |
| S9 | https://discussions.unity.com/t/using-a-script-with-onaudiofilterread-as-an-audiosource/526148 | generator on a clip-less AudioSource; listener-on-same-object behaviour (anecdotal) |
| S10 | https://www.firstpr.com.au/dsp/pink-noise/ | Kellet refined and economy coefficients, ±0.05 dB claim |
| S11 | https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf ; https://forum.juce.com/t/svf-resonant/56889 ; https://ccrma.stanford.edu/~jatin/chowdsp/chowdsp_utils/classchowdsp_1_1StateVariableFilter.html | Simper trapezoidal SVF equations (g, k, a1-a3, ic1eq/ic2eq) |
| S12 | https://ccrma.stanford.edu/~jos/pasp/Achieving_Desired_Reverberation_Times.html ; https://ccrma.stanford.edu/~jos/pasp/First_Order_Delay_Filter_Design.html ; https://ccrma.stanford.edu/~jos/pasp/Mode_Density_Requirement.html | RT60 to per-line gain, Jot damping filter, 0.15 t60 fs mode density |
| S13 | https://itu.int/dms_pubrec/itu-r/rec/bs/R-REC-BS.1770-1-200709-S!!PDF-F.pdf ; https://forum.juce.com/t/iir-filter-coefficients-for-lufs-calculation/59111 ; https://cacophony.js.org/variables/K_WEIGHTING_STAGE1_48K.html | BS.1770 K-weighting 48 kHz table, gating constants |
| S14 | https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html | WAV float: fmt size 18, cbSize, fact chunk |
| S15 | https://docs.python.org/3/library/wave.html | stdlib wave float support only from 3.15 |
| S16 | https://mcvuk.com/development-news/procedural-audio-with-unity/ ; https://github.com/pixlpa/unity-synth-experiments | community generator patterns (AudioSource + OnAudioFilterRead) |
| Local | `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml` (lines cited above); `ProjectSettings/AudioManager.asset`; `ProjectSettings/ProjectSettings.asset` | ground-truth API docs for 6000.6.0f1 and project audio settings |

## R6 Banding mitigation, blue noise, TPDF dither, encode test
Detailed note: [`docs/research/R6_banding_dither.md`](research/R6_banding_dither.md)

Key findings:
1. **Use exactly one dither, in ThreadGrade, as the last operation.** Turn URP Camera Dithering off (`UniversalAdditionalCameraData.dithering = false`). URP's own dither is fixed at ±1 LSB TPDF, uses 16x16 textures and a `Time.frameCount` random offset, and runs in Uber or FinalPost. That conflicts with spec 14.7.1 (64x64 void-and-cluster tile, R2 offset, `-freeze-dither`, x1.5 when D > 0.5).
2. **HDR precision must be 64-bit.** Both project RP assets currently use `m_HDRColorBufferPrecision: 0` (= `_32Bits`, R11G11B10) at `Assets/Settings/PC_RPAsset.asset:27`. Set it to 1 (`_64Bits`, R16G16B16A16_SFloat).
   - In R11G11B10, one representable step in a bright sky is up to **2.6 sRGB codes in blue and 1.3 in red/green**. No ±1 LSB dither can hide that.
   - In FP16 the step is at most 0.08 LSB (section 2.3).
3. **URP post-processing stays on (for bloom), so the Uber pass always colour-grades.** UberPost.shader:271-274 says "Color grading is always enabled when post-processing/uber is active".
   - In LDR grading mode (project default, `m_ColorGradingMode: 0`), Uber clamps HDR to [0,1] (`ApplyTonemap` returns `saturate`, Common.hlsl:101-111). ThreadGrade at AfterRenderingPostProcessing would then receive clipped colour.
   - In HDR grading mode, the identity LogC LUT at size 32 adds a smooth scallop error of up to **1.29 LSB** (sim). At size 64 or 65 the error is 0.30 LSB.
   - So: either disable URP post and do bloom inside ThreadGrade (cleanest), or use HDR grading with `colorGradingLutSize = 64`.
4. **Dither math: add TPDF noise in sRGB-encoded space, then convert back to linear exactly.** The 8-bit quantiser is uniform in sRGB space. This is the same as URP's linear-space branch (Common.hlsl:202): `out = SRGBToLinear(LinearToSRGB(c) + n/255)` with the exact Core functions. Use the same noise value for R, G and B (monochrome), because luma is what survives 4:2:0 encoding.
5. **Noise source.** A 64x64 void-and-cluster rank map (sigma 1.5, toroidal), stored as `u = (rank+0.5)/4096` in an RFloat point/repeat texture.
   - Remap: `t = 2u-1; n = sign(t)*(1-sqrt(1-|t|))`. This is URP's own remap (Common.hlsl:190-192). It is monotonic, so the blue-noise ordering is kept.
   - Each frame, offset the tile by an integer R2 texel offset. `-freeze-dither` sets the frame index to 0.

Sources (accessed 2026-10-29):
| URL | Used for |
|---|---|
| Local URP source `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` (file:line in section 1) | ApplyDithering, Uber/FinalPost order, dither textures, HDR precision, grading clamp/LUT |
| Local Core source `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e` (Color.hlsl, Common.hlsl, Random.hlsl, API/D3D11.hlsl, Runtime/Utilities/Blit.hlsl) | exact sRGB functions, FastSign, IGN, LOAD_TEXTURE2D, Blit Varyings |
| `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml` | Texture2D ctor/SetPixelData, RFloat, Display.requiresSrgbBlitToBackbuffer |
| http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare | Jimenez 2014 talk, SIGGRAPH Advances, IGN used for shadow filtering |
| https://blog.demofox.org/2022/01/01/interleaved-gradient-noise-a-different-kind-of-low-discrepancy-sequence/ | IGN formula, 3x3 low-discrepancy property, 5.588238 px/frame offset |
| https://blog.playdead.com/articles/inside_presentations/inside_publications.html | Playdead talk list (GDC 2016 rendering talk, "Banding in Games" 2014) |
| https://github.com/playdeadgames/publications/tree/master/INSIDE | slide file names (PDFs are Git LFS, over the fetch limit) |
| https://gdcvault.com/play/1023002/Low-Complexity-High-Fidelity-INSIDE | talk abstract (dithering against banding), speakers |
| https://lists.blender.org/pipermail/bf-blender-cvs/2020-February/135857.html (via search snippet) | Blender commit citing Gjøl "Banding in Games" slide 27; Shadertoy 4t2SDh triangle remap |
| https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/ | triangular vs uniform, blue noise vs white vs IGN, -1..1 remap note, clipping comment |
| https://blog.demofox.org/2019/06/25/generating-blue-noise-textures-with-void-and-cluster/ | void-and-cluster steps, energy LUT optimisation, sigma 1.5 vs 1.9, 3-sigma truncation warning |
| https://patents.google.com/patent/US5912745 (and the search result citations) | Ulichney 1993 citation, SPIE 1913 pp. 332-343; Gaussian sigma 1.0-1.5 |
| https://momentsingraphics.de/BlueNoise.html | Peters' blue-noise textures (sigma 1.9; random texture and offset per frame) |
| https://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/ (TLS failed; constants via https://scrapbox.io/0b5vr/R2) | R2 definition, g, a1, a2, 0.5 seed |
| https://blog.demofox.org/2017/11/03/animating-noise-for-integration-over-time-2-uniform-over-time/ | value-offset animation degrades the blue-noise spectrum |
| https://support.google.com/youtube/answer/1722171 | YouTube upload specs (H.264 High, 4:2:0, BT.709, bitrates, 8-bit conversion) |
| https://developers.google.com/media/vp9/settings/vod | VP9 CRF per resolution (31 @1080p, 15 @2160p), `-quality good` |
| https://forum.doom9.org/archive/index.php/t-167706.html | x264 banding: ordered dither survives better, 10-bit, aq/psy tweaks |
| Local `ffmpeg -h encoder=libx264 / libvpx-vp9`, `-h filter=scale / zscale` | option names, ranges, defaults (crf, deadline, cpu-used, row-mt, out_color_matrix) |

## R7 Space elevator facts, Earth from orbit, Journey/Manifold Garden, cloud seas, precision
Detailed note: [`docs/research/R7_art_science.md`](research/R7_art_science.md)

Key findings:
1. **Every horizon-dip value in the spec is correct** (14.5.7 text, 14.7.5 table, KF6a, KF6a2). Each one is within 0.012 degrees of `acos(R/(R+A))` with R = 6371 km, which is inside the T-CURV tolerance of 0.05 degrees.
2. **`eps0 = 25 km / sqrt(A(2R+A))` is correct: 0.506 degrees at 600 km.** `sqrt(A(2R+A))` is the tangent (horizon) distance, so eps0 is the angle a 25 km layer subtends at the limb. The curvature-exact value for a 25 km layer is 0.511 degrees. eps0 grows to 31 degrees at s = 500, so the glow must be gated at low altitude (section 2.4).
3. **The cloud shell has a horizon-cut defect at s of about 600 to 640.** The hit test uses the R-based limb, but the shell has radius R + 3 km. At KF6a the sea is cut at a dip of 2.77 degrees, while its own limb is at 2.14 degrees. At that cut, about 24 percent of the surface contrast is still unhazed. Fix: fade to full haze before the cut (section 2.3).
4. **Ray/sphere math:** use the RTG ch. 7 "c/q" form in the angular (mu) parametrisation, with `c/rho` and `mu_h = cos psi_limb` precomputed in double. Take the central angle from `atan2` (not `asin`), and take `sin psi` from `length(d.xz)`, not from `sqrt(1 - mu^2)`. The SRP Core `IntersectRaySphere` is the naive quadratic, so do not use it here (section 5).
5. **Cloud octaves need a variance-preserving combination.** A plain weighted sum of three F1 octaves almost removes the shade tone (0.0 percent of pixels below 0.18). Normalising by the active-weight RMS keeps the tone fractions at about 32 / 46 / 22 percent, independent of how many octaves have faded (section 4.4).
6. **Hash:** use `pcg3d` (JCGT 2020, verified from the paper text) on integer cell ids. Hoskins' "hash without sine" is fine for small coordinates. Compute in cell space (IQ's precision trick), with the integer camera-cell offset split on the CPU in double.
7. **Shading:** the URP calls are verified (`GetMainLight(shadowCoord)`, `TransformWorldToShadowCoord`, the keywords below). Use `centroid` normals under MSAA so the rim does not spike on silhouettes. `rim * (1 - fog)` followed by fog gives a (1 - f)^2 fade; that is intended, so document it. Non-colour `float3` literals need `// pal:ok` or a named `static const`, because T-PAL scans every `float3`/`float4` literal.
---

Sources (accessed 2026-10-29):
| ID | URL | Used for |
|---|---|---|
| S1 | https://en.wikipedia.org/wiki/Space_elevator | history, GEO, taper table, 4,960 km, 30 to 80 MYuri, Edwards 19,800 kg / 207 climbers, release altitudes, hazards |
| S2 | https://www.sciencenews.org/article/ribbon-stars | Edwards ribbon about 1 m wide, thinner than paper, 100,000 km, Pacific anchor, about 190 km/h (via search summary) |
| S3 | https://NSS.org/wp-content/uploads/2017/07/Ad-Astra-Magazine-v15n1f1.pdf | climbers up to 200 km/h, laser power beaming (via search summary) |
| S4 | https://NSS.org/wp-content/uploads/2017/07/2003-Space-Elevator-NIAC-phase2.pdf | Edwards NIAC Phase II Final Report (2003), existence and scope; PDF text not machine-readable here |
| S5 | https://www.isec.org/space-elevator-tether-materials | ISEC tether 100,000 km x 1 m, graphene / hBN / CNT |
| S6 | https://www.isec.org/news/climbertether-interface-of-the-space-elevator-study-now-available- and https://isec.org/space-elevator-newsletter-2024-september-special-edition | opposing-wheel friction climber (2023), Galactic Harbour and Apex Anchor (via search summary) |
| S7 | https://arxiv.org/abs/1804.06453 ; https://arxiv.org/abs/2412.17198 | Popescu and Sun 2018 (high stress ratio plus repair); Gassend exponential tethers (2004/2024) |
| S8 | https://earthobservatory.nasa.gov/images/1094/aurora-from-the-space-shuttle | airglow layer about 100 km, about 30 km thick, 557.7 nm, edge-on only (via search summary) |
| S9 | https://www.nasa.gov/image-detail/gmt064_ehdc1 and the ISS sunset image description (search results) | thin blue line (Rayleigh), orange troposphere band at the terminator |
| S10 | https://earthobservatory.nasa.gov/images/154648/nighttime-over-the-eastern-pacific | green airglow about 130 km, red about 240 km (via search summary) |
| S11 | https://earthref.org/ERR/33081 (Mende et al. 1993, STS-37 limb spectra) | 557.7 / 630 nm emissions above 110 km, Na peak about 92 km (via search summary) |
| S12 | https://www.nasa.gov/image-article/cloudy-earth | 67 percent cloud cover, under 10 percent of ocean skies fully clear |
| S13 | https://modis.gsfc.nasa.gov/sci_team/pubs/abstract.php?id=03751 | King et al. 2013: 67 percent global, 55 percent land, 72 percent ocean |
| S14 | https://en.wikipedia.org/wiki/Journey_(2012_video_game) | "Japanese garden" subtraction, monomyth, awe, music arc |
| S15 | https://www.gdcvault.com/play/1017799/The-Art-of | Nava "The Art of Journey" (GDC 2013) scope |
| S16 | https://gdcvault.com/play/1017742/Sand-Rendering-in | Edwards "Sand Rendering in Journey" (GDC 2013) |
| S17 | https://www.alanzucconi.com/2019/10/08/journey-sand-shader-1/ , -2/ , -4/ | diffuse contrast formula, rim and ocean specular, max combination |
| S18 | https://blog.siggraph.org/?p=11525 | Manifold Garden: SDF AA edges, MSAA advice, world wrapping, "nowhere to hide" |
| S19 | https://history.siggraph.org/?p=77384 | retrospective abstract, keywords, references (Decaudin, Green 2007) |
| S20 | https://www.pcgamer.com/uk/manifold-garden-review | colour per gravity direction (via search summary) |
| S21 | https://en.wikipedia.org/wiki/Manifold_Garden | Tadao Ando, no curved surfaces (via search summary) |
| S22 | https://www.jcgt.org/published/0009/03/02/paper.pdf | pcg3d listing (sec. 6.1) and recommendations (sec. 7), text extracted locally with zlib |
| S23 | https://www.shadertoy.com/view/4djSRW (code copy: https://pyssv.readthedocs.io/en/latest/_modules/random.glsl.html) | Hoskins "Hash without Sine", MIT licence |
| S24 | https://iquilezles.org/articles/smoothvoronoi/ | 3x3 voronoi, cell-space precision trick, smooth voronoi variants |
| S25 | https://iquilezles.org/articles/bandlimiting/ (and https://iquilezles.org/articles/filtering/) | `fcos`, band-limited fBM `smoothstep(1.0, 0.5, w)`, footprint propagation |
| S26 | https://iquilezles.org/articles/warp/ | domain warping `f(p + h(p))` |
| S27 | https://research.nvidia.com/publication/2019-03_precision-improvements-raysphere-intersection | RTG ch. 7 abstract and authors |
| S28 | https://www.realtimerendering.com/raytracinggems/rtg/ | book access (CC BY-NC-ND), errata link |
| S29 | https://github.com/Apress/ray-tracing-gems/blob/master/Ch_07_Precision_Improvements_for_Ray-Sphere_Intersection/sphereIntersect.hlsli | methods 0 to 6, method 6 code quoted above |
| S30 | https://reedbeta.com/blog/hash-functions-for-gpu-rendering | 32-bit `pcg_hash` constants (do not mix them with the pcg3d constants) |
Local ground truth (package source): see the table in 6.1, plus `com.unity.render-pipelines.core@2d66c71e606e/ShaderLibrary/GeometricTools.hlsl:95-114`.

## R8 Thin lines, text, Input System, player settings
Detailed note: [`docs/research/R8_lines_ui_input_settings.md`](research/R8_lines_ui_input_settings.md)

Key findings:
| Topic | Decision |
|---|---|
| Thin lines | Expand the ribbon in clip space. Use `_ScaledScreenParams`, not `_ScreenParams`. Width clamp `w_min = max(1.5, 1.75*H/1080)` px. Energy = `max(true/w_min, 0.35)`. Coverage `saturate(0.5 + halfPx - abs(dPx))`, with `dPx` and `halfPx` passed as **`noperspective`** varyings. Use premultiplied blend for the thread (Transparent queue). Rails use `AlphaToMask On` in the AlphaTest queue (2450), but **only when MSAA > 1**; otherwise fall back to blending. |
| Text | Use **legacy UGUI `Text`**, with `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (verified in the uGUI source). Build one `Text` per glyph, positioned by `CharacterInfo.advance` + tracking (Part 14.11). Canvas: `ScreenSpaceOverlay`, `CanvasScaler.ConstantPixelSize`, sizes in real pixels, rebuilt when the resolution changes. No EventSystem / `StandaloneInputModule`. Avoid UI Toolkit (theme warning, extra .tss asset). Avoid TMP (essentials `.unitypackage` import). |
| Input | Read devices directly: `Keyboard.current`, `Mouse.current`, `Gamepad.current` (null-check all three). `activeInputHandler: 1` is already set (new Input System only). Mouse look = `delta.ReadValue() * degPerCount`, with **no** `Time.deltaTime` and no smoothing. Gamepad look = rate × `unscaledDeltaTime`. |
| Fullscreen | Keep `allowFullscreenSwitch = true`, so the native Alt+Enter works. Handle **only F11** in code (`Screen.SetResolution` with `FullScreenWindow` ↔ `Windowed`), and never handle Alt+Enter in code as well (that would toggle twice). |
| Graphics API | Set `SetUseDefaultGraphicsAPIs(StandaloneWindows64, false)` and `SetGraphicsAPIs(..., { Direct3D11 })`. The Editor uses the first entry in that list (or `-force-d3d11`). Assert `SystemInfo.graphicsDeviceType == Direct3D11` in `-autotest`. |
| Splash | `PlayerSettings.SplashScreen.show = false` is allowed on Personal in Unity 6. Read the value back and log it. |
| Build | `BuildPipeline.BuildPlayer(BuildPlayerOptions)` → check `report.summary.result == BuildResult.Succeeded`, then `EditorApplication.Exit(code)` in batch mode. |

Sources (accessed 2026-10-29):
| URL | Used for |
|---|---|
| https://www.humus.name/index.php?page=3D&ID=89 | Phone-wire AA: clamp to 1 px, fade by ratio, combine with MSAA |
| https://godotshaders.com/shader/the-best-darn-grid-shader-yet-for-godot/ | CC0 port of Golus' pristine grid (derivative, `gridUV`, smoothstep lines) |
| https://lovr.org/docs/v0.19.0/Environment/Grid_(Smooth) | Same algorithm: `drawWidth` clamp, energy `*= saturate(w/drawWidth)`, fade-to-mean term |
| https://bgolus.medium.com/the-best-darn-grid-shader-yet-727f9278b9d8 | Original article (HTTP 403 to the fetcher; algorithm cross-checked via the two ports) |
| https://docs.unity3d.com/2022.3/Documentation/Manual/SL-AlphaToMask.html | AlphaToMask syntax; unpredictable without MSAA |
| https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-struct | `noperspective` / `centroid` / `sample` interpolation modifiers |
| https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/windows/playersettings.md | Fullscreen modes, Default Is Native Resolution, Run/Visible In Background, Allow Fullscreen Switch, flip model, splash, scripting backend |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/playersettings/allowfullscreenswitch | allowFullscreenSwitch description |
| https://discussions.unity.com/t/block-or-override-alt-enter-fullscreen/82728 | Alt+Enter (Windows) / Cmd+F (Mac) governed by the flag (community) |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screen | Screen settings persisted in PlayerPrefs; startup precedence |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player | Player CLI: `-screen-*`, `-window-mode`, `-force-d3d11/12`, `-logFile`, `-monitor` |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor | Editor CLI: `-force-d3d11/12`, `-batchmode`, `-executeMethod`, `-buildTarget`, `-logFile` |
| https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/cross-platform-features/graphics-apis/configure | API list order and fallback; Editor uses the first API in the list |
| https://unity.com/blog/directx-12-improvements-in-unity-6 | DX12 default since 6.1; memory trade-off; DX11 fallback advice; PSO tracing |
| https://support.unity.com/hc/en-us/articles/212165803 | Unity 6 Personal may disable the splash screen |
| https://forum.unity.com/threads/is-frame-delta-accounted-for-in-mouse-current-delta.945542/ | Do not multiply the mouse delta by deltaTime (community) |
| https://discussions.unity.com/t/new-input-system-get-raw-mouse-delta-from-wm_input/857382 | Input System uses WM_INPUT; integer pixel deltas (community, unverified) |
| https://doc.photonengine.com/fusion/current/addons/advanced-kcc/input-smoothing | `Mouse.current.delta` is unprocessed |
| https://discussions.unity.com/t/launching-game-in-windowed-mode-by-default-problem/68708 | Registry/PlayerPrefs screen persistence (community) |


## Pricing source for the final metrics (Part 15.3)
- https://platform.claude.com/docs/en/about-claude/pricing (accessed 2026-10-30; redirected from docs.anthropic.com): Claude Opus 5.5 base input $4/MTok, 5-minute cache writes $5/MTok, 1-hour cache writes $8/MTok, cache hits $0.20/MTok, output $20/MTok; 1M context at standard pricing. Used by Tools/metrics.py via Tools/pricing.json.

## Fix wave 3 techniques (2026-10-30)
- Hue-preserving haze (D-065): mixing in OKLab (Bjorn Ottosson, "A perceptual color space for image processing", https://bottosson.github.io/posts/oklab/) with a slower chroma weight and an exact luminance rescale, so aerial perspective lowers contrast without turning teal and coral grey.
- Puff anti-aliasing (D-076): analytic screen-space gradients instead of quad-level fwidth, which misses jumps between 2x2 quads; a smooth-max union keeps the tone C1 (the same reasoning as analytic SDF anti-aliasing).
- Curved-Earth chord limit for the deck veil (D-067): a ray from height dh reaches a plane above it no flatter than sqrt(2 dh / R), the small-angle form of the horizon dip.
- Footprint filtering of the bank optical depth (D-068): average transmittance over the pixel's depth footprint along the ray, so a ramp that collapses into one row at grazing angles keeps a smooth, multi-pixel foot.
