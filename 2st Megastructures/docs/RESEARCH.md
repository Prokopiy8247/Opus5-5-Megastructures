# RESEARCH — The Ring

Research sprint of Part 4 / 14.9, run 2026-10-29 as nine parallel read-only research agents (workflow `ring-research-sprint`).
Each topic has a full notes file in `docs/research/` with facts tagged VERIFIED-SOURCE (URP 17.6 package source path:line),
VERIFIED-DOCS (URL) or UNVERIFIED. This file is the index and the source log (URL or file, date accessed, what it was used for).
Decisions that came out of the research are recorded in `DEVELOPMENT.md` (ADRs and 'Research-driven decisions').

## Topics

- [01 — Handwritten URP 17.6 lit shaders that match URP Lit (Unity 6000.6.0f1)](research/01_urp_lit_shader_template.md)
- [02 - URP 17.6 pipeline asset / renderer / camera settings from editor C#, plus a Render Graph full-screen pass](research/02_urp_pipeline_settings_rendergraph.md)
- [03 - Procedural meshes, GPU instancing, indirect draws (Unity 6000.6.0f1 / URP 17.6.0)](research/03_meshes_instancing_indirect.md)
- [04 - Deterministic screenshot / frame-sequence capture (Unity 6000.6.0f1, URP 17.6.0, Windows)](research/04_capture_determinism.md)
- [05 Procedural audio (Unity 6000.6.0f1, OnAudioFilterRead)](research/05_procedural_audio.md)
- [06 - Runtime UI Toolkit text, Input System from code, editor build automation (Unity 6000.6.0f1)](research/06_ui_toolkit_input_build.md)
- [07 Rendering techniques: analytic sky, filtered procedurals, hashes, dither, clipmaps, fog, star, water](research/07_rendering_techniques.md)
- [08 - Ringworld facts, art references, exposure adaptation, bloom, video banding](research/08_ringworld_art_exposure.md)
- [09: Depth precision, shader stripping and player-build safety (URP 17.6 / Unity 6000.6.0f1)](research/09_depth_format_shader_build.md)

## Source log (per topic)

### 01 — Handwritten URP 17.6 lit shaders that match URP Lit (Unity 6000.6.0f1)

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

### 02 - URP 17.6 pipeline asset / renderer / camera settings from editor C#, plus a Render Graph full-screen pass

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

### 03 - Procedural meshes, GPU instancing, indirect draws (Unity 6000.6.0f1 / URP 17.6.0)

- `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Resources\CGIncludes\UnityIndirect.cginc` - indirect helper source (struct layouts, per-API instance ID, InitIndirectDrawArgs).
- `...\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll` (+ `.xml`), `UnityEngine.PhysicsModule.dll`, `UnityEditor.CoreModule.dll` - reflection: signatures (`in RenderParams`), RenderParams fields + ctor IL defaults, IndirectDraw*Args layout/size, RenderMesh* managed checks, Mesh/MeshData API, MeshUpdateFlags, MeshColliderCookingOptions, Physics.BakeMesh, Obsolete attributes, GraphicsBuffer.Target, BatchRendererGroupStrippingMode.
- `...\Editor\Data\Documentation\en\ScriptReference\` (offline 6000.6 docs): Graphics.RenderMeshIndirect, Graphics.RenderMeshInstanced, Graphics.RenderMeshPrimitives, Graphics.RenderMesh, Graphics.DrawMesh, RenderParams (+ -ctor/-camera/-worldBounds/...), Mesh-indexFormat, Mesh-bounds, Mesh.RecalculateBounds, Mesh.UploadMeshData, Mesh.MarkDynamic, Mesh.SetIndices, Mesh.SetVertices, Mesh.AllocateWritableMeshData, Mesh.ApplyAndDisposeWritableMeshData, Rendering.MeshUpdateFlags, MeshCollider-cookingOptions, MeshCollider-sharedMesh, MeshColliderCookingOptions.*, Physics.BakeMesh, Renderer-localBounds, Renderer-bounds, Camera.Render, Rendering.RenderPipeline.SubmitRenderRequest - API behaviour.
- `...\Documentation\en\Manual\`: SRPBatcher-Materials, SRPBatcher-Incompatible, GPUInstancing, gpu-instancing-enable, gpu-instancing-troubleshoot, gpu-instancing-birp-shader-modifications (procedural:FunctionName), class-MeshCollider; `Manual\urp\gpu-resident-drawer`, `make-object-compatible-gpu-rendering`, `gpu-culling`, `gpu-resident-drawer-performance` - SRP Batcher / instancing / GRD rules.
- `Library\PackageCache\com.unity.render-pipelines.core@2d66c71e606e\ShaderLibrary\UnityInstancing.hlsl` - instancing keywords, unity_InstanceID, array sizes, matrix macros.
- `...core@2d66c71e606e\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs`, `IGPUResidentRenderPipeline.cs`, `Debug\GPUResidentDrawerDebug.cs`, `SceneProcessors\WorldProcessor.cs`, `Components\DisallowGPUDrivenRendering.cs` - GRD requirements/exclusions.
- `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184\Runtime\Data\UniversalRenderPipelineAsset.cs`, `Runtime\UniversalRenderer.cs`, `Runtime\UniversalRenderPipeline.cs` - GRD support check, RenderingMode enum, camera rendering order / render requests.
- `...universal@8457e85b8184\Shaders\Lit.shader`, `Shaders\ShadowCasterPass.hlsl`, `Shaders\DepthNormalsPass.hlsl`, `ShaderLibrary\DOTS.hlsl`, `ShaderLibrary\UnityInput.hlsl`, `ShaderLibrary\Shadows.hlsl`, `core ShaderLibrary\SpaceTransforms.hlsl` - pass tags, shadow bias functions, DOTS pragma, UnityPerDraw.
- Project files (read only): `Assets\Settings\PC_RPAsset.asset`, `Mobile_RPAsset.asset`, `PC_Renderer.asset`, `ProjectSettings\GraphicsSettings.asset`, `ProjectSettings\QualitySettings.asset` - current GRD/renderer settings.
- https://discussions.unity.com/t/graphics-rendermesh-doesnt-work-in-urp/918319 - default RenderParams renderingLayerMask = 0 pitfall.
- https://zenn.dev/link/comments/5ce0ac65053476 - URP RenderMeshIndirect Lit shader with ShadowCaster/DepthOnly using UnityIndirect.cginc in HLSLPROGRAM; bounds-center offset report.
- https://discussions.unity.com/threads/bounds-parameter-in-drawmeshinstancedindirect-dictates-world-space-position-of-mesh.495313/ and https://discussions.unity.com/t/graphics-drawmeshinstancedindirect-bounds-field-causes-instances-to-move/928981 - bounds center added to world position (community).
- https://discussions.unity.com/t/indirect-procedural-rendering-with-shader-graph/1664601 (+ /12) - Unity staff thread: object space = world space for low-level APIs; procedural:setup keyword behaviour; RenderPrimitivesIndexedIndirect keyword report (6000.2.9).
- Web search summary for "Only 511 instances instead of 1023 are drawn per call when using Graphics.RenderMeshInstanced" (issuetracker page itself returned 502/redirect; details unverified).

### 04 - Deterministic screenshot / frame-sequence capture (Unity 6000.6.0f1, URP 17.6.0, Windows)

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

### 05 Procedural audio (Unity 6000.6.0f1, OnAudioFilterRead)

| Source | Used for |
|---|---|
| `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml` (lines 298, 516, 851, 1211, 1762-1791, 1898-1985, 1986-2065, 2206, 2336-2438) | Exact API names: AudioConfiguration, AudioSettings (GetConfiguration/Reset/OnAudioConfigurationChanged/GetDSPBufferSize/outputSampleRate/dspTime), AudioReverbFilter properties, AudioRenderer, scriptable pipeline types |
| `...\Editor\Data\Documentation\en\ScriptReference\MonoBehaviour.OnAudioFilterRead.html` | Thread, call rate, generator mode, ordering, interleaving, Web restriction, example |
| `...\Documentation\en\ScriptReference\AudioSettings-outputSampleRate.html`, `AudioSettings.GetDSPBufferSize.html`, `AudioSettings-dspTime.html`, `AudioConfiguration-sampleRate.html`, `AudioConfiguration-dspBufferSize.html`, `AudioSettings.Reset.html`, `AudioReverbFilter-reflectionsDelay.html`, `AudioReverbFilter-reverbPreset.html` | Sample rate rule, buffer semantics, Reset example, doc error |
| `...\Documentation\en\Manual\class-AudioReverbFilter.html`, `class-AudioManager.html`, `class-AudioEffect.html`, `audio-filters.html`, `class-AudioListener.html`, `audio-scriptable-processors.html`, `audio-scriptable-processors-generators.html`, `PlayerCommandLineArguments.html`, `EditorCommandLineArguments.html` | Reverb ranges/defaults, project audio settings, effect ordering, listener effects, IAudioGenerator, no audio CLI flag |
| `W:\...\ProjectSettings\AudioManager.asset` (lines 10-19), `ProjectSettings\ProjectSettings.asset` (lines 90, 853) | Current sample rate, DSP buffer, speaker mode, audio disable flag, runInBackground, incremental GC |
| `W:\...\MASTER_PROMPT.md` 14.3 (lines 530-555), 11.9 (line 331) | Spec targets the notes are aligned to |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/onaudiofilterread | Cross-check of the OAFR page (online) |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset | Reset example and valid value lists |
| https://docs.unity.com/en-us/engine/6000.3/manual/audio/reference/filters/class-audio-reverb-filter | Reverb table cross-check |
| https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager | Audio settings cross-check |
| https://issuetracker.unity3d.com/issues/onaudiofilterread-does-not-affect-signal-on-audiolistener-after-enable (UUM-116871; content via search summaries, page is JS-rendered) | Listener filter fix versions; related UUM-117120, UUM-61733 |
| https://issuetracker.unity.com/issues/8659/audio-stuttering-occurs-when-heavy-processing-is-performed-while-onaudiofilterread-is-in-use (UUM-122520) and https://issuetracker-mig.prd.it.unity3d.com/issues/audio-dropouts-are-caused-by-garbage-collector-when-large-amount-of-memory-are-being-allocated (870041) | GC coupling of OAFR, Won't Fix |
| https://issuetracker.unity3d.com/issues/onaudiofilterread-is-never-called-if-added-programmatically-via-addcomponent | AddComponent works since 5.3 |
| https://issuetracker.unity3d.com/issues/empty-data-buffer-gets-passed-to-onaudiofilterread-when-using-video-player (quoted in search results) | Generator detection at AudioSource.Play |
| https://discussions.unity.com/t/onaudiofilterread-not-playing/503615 | Write into `data`, do not reassign |
| https://discussions.unity.com/t/audiosettings-dsptime-and-native-unityaudioeffectstate-currdsptick-sometimes-out-of-sync/671905 | dspTime freezes in background without Run In Background |
| https://www.firstpr.com.au/dsp/pink-noise/ | Kellet refined and economy pink filters, accuracy |
| https://noisehack.com/generate-noise-web-audio-api/ | Pink gain 0.11, brown leaky integrator 0.02/1.02 and gain 3.5 |
| https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf | Trapezoidal SVF nodal equations, k = 1/Q, state update |
| https://www.native-instruments.com/fileadmin/ni_media/downloads/pdf/VAFilterDesign_2.1.0.pdf (404 on fetch) | Zavalishin TPT SVF reference (equations derived, not fetched) |
| https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html | DC blocker equation, R guidance |
| https://signalsmith-audio.co.uk/writing/2022/limiter/ | Look-ahead limiter structure and no-overshoot argument |
| https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html | WAV PCM / IEEE float / fact / extensible rules |
| https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile | Types allowed for `volatile`, limits |
| https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile | Volatile.Read/Write semantics, 64-bit atomicity |
| https://cycling74.com/forums/synthetic-wind | Wind realism heuristics (anecdotal) |
| https://essentia.upf.edu/reference/std_TruePeakDetector.html, https://cacophony.js.org/classes/TruePeakDetector.html, MATLAB loudnessMeter page and Slate Digital bus-clipper article (via search summaries) | True-peak 4x oversampling structure, ITU filter ripple |

### 06 - Runtime UI Toolkit text, Input System from code, editor build automation (Unity 6000.6.0f1)

- `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.UIElementsModule.dll` + `.xml`: PanelSettings/UIDocument/PanelRenderer/FontDefinition/IStyle/Length members, IL of InitializeShaders/Reset/OnEnable/ApplyThemeStyleSheet/ctor, UIR.Shaders constants, warning strings.
- `...\Managed\UnityEngine\UnityEditor.UIElementsModule.dll`: PanelSettingsCreator / ThemeUtility (default .tss path and content).
- `...\Managed\UnityEngine\UnityEngine.TextCoreTextEngineModule.dll`: TextSettings, FontAsset.CreateFontAsset overloads + IL, TextShaderUtilities shader names.
- `...\Managed\UnityEngine\UnityEngine.TextRenderingModule.dll`: Font API.
- `...\Managed\UnityEngine\UnityEngine.CoreModule.dll`: Screen, FullScreenMode, Cursor, GraphicsDeviceType, GraphicsSettings, ShaderVariantCollection, PassType, GraphicsStateCollection.
- `...\Managed\UnityEngine\UnityEditor.CoreModule.dll`: BuildPipeline, BuildPlayerOptions, BuildOptions, BuildPlayerWithProfileOptions, BuildProfile, EditorBuildSettings, PlayerSettings (+SplashScreen), NamedBuildTarget, ScriptingImplementation, EditorSceneManager.
- `...\Editor\Data\Resources\unity_builtin_extra`, `...\Resources\unity editor resources`, `...\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_nondevelopment_mono\Data\Resources\unity default resources`, `...\UnityPlayer.dll`: where UIR/TextCore shaders, the Inter font and LegacyRuntime live; legacy Input exception text.
- `Library\PackageCache\com.unity.inputsystem@7a4e1a2a8194\InputSystem\Runtime\...` (InputAction.cs, InputActionSetupExtensions.cs, Composites/Vector2Composite.cs, Devices/*.cs, Controls/ButtonControl.cs, InputSettings.cs, InputSystem.cs), `...\InputSystem\Editor\Settings\EditorPlayerSettingHelpers.cs`, `...\Tests\TestFixture\*`: Input System API, activeInputHandler values, test fixture.
- `Library\PackageCache\com.unity.test-framework@7a3849e09bd0\UnityEditor.TestRunner\CommandLineTest\{SettingsBuilder,Executer,TestStarter}.cs`: CLI options, -quit warning, exit codes.
- `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184\Runtime\UniversalRenderer.cs:421-422`, `Runtime\Passes\DrawScreenSpaceUIPass.cs`: overlay UI after the final blit.
- Project files: `ProjectSettings\ProjectSettings.asset`, `GraphicsSettings.asset`, `EditorBuildSettings.asset`, `Packages\manifest.json`: current settings.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/uielements/panelsettings: member list cross-check.
- https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-get-started-with-runtime-ui: UIDocument OnEnable lifecycle.
- https://docs.unity.cn/6000.6/Documentation/Manual/UIE-create-ui-document-component.html: UIDocument obsolete -> PanelRenderer, clear-on-enable behaviour.
- https://discussions.unity.com/t/does-uidocument-clear-contents-when-disabled/837983: "by design" clearing (community/staff).
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/playersettings/allowfullscreenswitch: Alt+Enter setting.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/fullscreenmode, .../screen/setresolution, .../unityengine/fullscreenmode: fullscreen semantics, end-of-frame apply.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/buildpipeline/buildplayer: BuildPlayerOptions vs build profiles.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/shadervariantcollection/warmup: DX12/Vulkan warm-up caveat.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor: batchmode/quit/executeMethod/buildTarget/nographics/exit codes, project-lock note.
- https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html: -runTests/-testPlatform/-testResults.
- https://support.unity.com/hc/en-us/articles/212165803: Unity 6 Personal can disable the splash (via search summary).
- https://forum.needle.tools/t/unity-default-text-in-worldspace-canvas-doesnt-export-anymore-with-2022-2-unity-versions/323: LegacyRuntime font context (community).

### 07 Rendering techniques: analytic sky, filtered procedurals, hashes, dither, clipmaps, fog, star, water

- `URP/ShaderLibrary/ShaderVariablesFunctions.hlsl`: fog math, keyword guards, MixFogColor (lines 326-532).
- `URP/ShaderLibrary/Fog.hlsl`: fog keyword declaration (1-9). `URP/Shaders/Lit.shader:179`: include_with_pragmas usage.
- `Library/PackageCache/com.unity.render-pipelines.universal-config@561422b74439/Runtime/ShaderConfig.cs.hlsl:13`: USE_DYNAMIC_BRANCH_FOG_KEYWORD = 0.
- `URP/ShaderLibrary/UnityInput.hlsl:225-226`: unity_FogParams and unity_FogColor.
- `URP/Shaders/PostProcessing/Common.hlsl:188-206`: URP TPDF dither.
- `URP/Runtime/PostProcessUtils.cs:142-214`, `URP/Runtime/PostProcess.cs:112-127`: dither is temporal.
- `URP/Runtime/UniversalAdditionalCameraData.cs:919`: `dithering` property.
- `URP/Shaders/PostProcessing/Bloom.shader:16-118`, `URP/Runtime/Overrides/Bloom.cs:176-239`: bloom prefilter, clamp, no Karis.
- `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:166-174,452,1096`: HDR precision.
- `URP/ShaderLibrary/Particles.hlsl:84-127`: soft-particle and camera fade. `URP/ShaderLibrary/DeclareDepthTexture.hlsl:31-40`: SampleSceneDepth.
- `CORE/ShaderLibrary/Random.hlsl:1-111`: IGN, Jenkins hash, ConstructFloat.
- `CORE/ShaderLibrary/BSDF.hlsl:32-56`: F_Schlick. `CORE/ShaderLibrary/VolumeRendering.hlsl:110-305`: phase functions.
- `CORE/ShaderLibrary/CommonMaterial.hlsl:175-267`: normal/specular filtering. `CORE/ShaderLibrary/Common.hlsl:1206,1388,1692-1698`: depth helpers, SharpenAlpha, float16 clamp.
- https://docs.unity3d.com/6000.2/Documentation/Manual/SL-UnityShaderVariables.html: unity_FogParams definition.
- https://www.jcgt.org/published/0009/03/02/paper.pdf: Jarzynski & Olano 2020 (pcg3d/pcg4d listing, recommendations); text extracted locally.
- https://reedbeta.com/blog/hash-functions-for-gpu-rendering/: 1D pcg constants and recommendation.
- https://rene.ruhr/gfx/gpuhash: pcg3d constants cross-check (search snippet).
- https://iquilezles.org/articles/filterableprocedurals, https://iquilezles.org/articles/checkerfiltering/, https://iquilezles.org/articles/morecheckerfiltering, https://iquilezles.org/articles/bandlimiting/, https://iquilezles.org/articles/fog/: technique existence and structure (search snippets only; the site returned 404 to the fetcher).
- https://git.gay/kaneraZh/lygia/src/tag/1.4.1/math/fcos.hlsl: fcos = cos(x)*smoothstep(TWO_PI,0,fwidth(x)) (search snippet).
- https://community.monogame.net/t/solved-height-distance-based-fog-again/14371: rd.y = 0 division problem in height fog.
- https://raw.githubusercontent.com/blender/blender/v2.93.0/intern/sky/source/sky_model.cpp: Hosek-Wilkie radiance formula and BSD-3 header.
- https://cgg.mff.cuni.cz/projects/SkylightModelling/: HW reference code v1.4a (license not stated on the page).
- https://metadata.ftp-master.debian.org/changelogs/main/o/ospray/stable_copyright, https://ftp.fau.de/cran/web/packages/skymodelr/COPYRIGHTS: HW BSD-3 confirmation.
- https://www.cg.tuwien.ac.at/research/publications/2007/zotti-2007-wscg/zotti-2007-wscg-paper.pdf: Perez/Preetham structure and weaknesses (search summary).
- https://diglib.eg.org/handle/10.1111/cgf14050: Hillaire 2020 bibliographic data and LUT list.
- https://github.com/sebh/UnrealEngineSkyAtmosphere (MIT): RenderSkyCommon.hlsl (sky-view mapping, phase functions), RenderSkyRayMarching.hlsl (multiple scattering, step integration, LUT sizes).
- https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry: clipmap layout, shader steps, alpha form.
- https://www.hhoppe.com/geomclipmap.pdf: transition width n/10, alpha property, zero-area triangles (text extracted locally).
- https://mikejsavage.co.uk/geometry-clipmaps/: simplified tile/trim/seam layout and trim rotation rule.
- https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process: radial blur light shafts.
- https://graphicrants.blogspot.com/2013/12/tone-mapping.html: Karis 1/(1+luma) weighting.
- https://iryoku.com/?p=1707: Jimenez 2014 CoD:AW post-processing (IGN; 2x2 firefly weighting on first downsample).
- https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/: TPDF vs uniform, IGN formula, black/white clipping comment.
- https://en.wikipedia.org/wiki/Dither: TPDF amplitude and noise modulation.
- https://forum.videohelp.com/threads/349125-BANDING-still and https://forum.doom9.org/archive/index.php/t-133546.html: dither vs encoder bitrate (weak, forum).
- https://en.wikipedia.org/wiki/Limb_darkening: limb laws and solar coefficients.
- https://jurnal.umsu.ac.id/index.php/alhisab/article/view/17148, https://www.icranet.org/scuola_lavoro/2023-2024/20042023/Sole_20230420_light.pdf: u ≈ 0.607 at Hα; u(600 nm) = 0.56, u(320 nm) = 0.95 (search summaries).
- https://en.wikipedia.org/wiki/Air_mass_(astronomy): Kasten-Young air mass.
- https://docs.unity3d.com/Packages/com.unity.shadergraph@16.0/manual/Fresnel-Equation-Node.html: dielectric f0 range.

### 08 - Ringworld facts, art references, exposure adaptation, bloom, video banding

- https://en.wikipedia.org/wiki/Ringworld - width 1.6 million km, 99 % g, 3 million Earths, shadow squares exist (no numbers).
- https://academickids.com/encyclopedia/index.php/Ringworld - table: radius 0.95e8 mi, width 997,000 mi, rim 1,000 mi, 770 mi/s, 0.992 g, 30 h day.
- https://larryniven.fandom.com/wiki/Ringworld (search snippets only; fetch returned 402) - corroborates width, rim walls, 770 mi/s, 0.992 g, 9.5e7 mi.
- https://www.dennisantinori.com/ringworldrpg/14109.html - RPG figures (1,239 km/s, 1,000 mi walls); conflicting shadow-square data (not used).
- https://zealotscript.co.uk/2017/10/08/superstructure-of-the-week-ringworld/ - 1,000 mi walls, 99.2 % g.
- https://travellerrpg.com/threads/ideas-for-polities-worlds-and-organisations.12869/page-2 and https://news.povray.org/4360830a%40news.povray.org (search snippets) - conflicting shadow-square numbers, proof that they are unverified.
- https://www.halopedia.org/Installation_04 (search snippet) - Halo ring 10,000 km diameter, 318 km width.
- https://en.wikipedia.org/wiki/Dyson_sphere - Dyson 1960 citation, swarm vs shell, Stapledon.
- https://www.scientificamerican.com/blog/life-unbounded/revisiting-the-dyson-sphere (search snippet) - Dyson's 2003 "swarm" remark.
- https://en.wikipedia.org/wiki/Adaptation_(eye) - human light and dark adaptation time courses.
- https://resources.mpi-inf.mpg.de/tmo/pattanaik00/index.html, https://history.siggraph.org/?p=118253 (search snippets) - Pattanaik 2000 asymmetric adaptation model.
- https://www.mankier.com/1/pfstmo_pattanaik00 - pfstmo implementation options.
- https://dev.epicgames.com/documentation/en-us/unreal-engine/auto-exposure-in-unreal-engine - f-stops/s, linear-then-exponential transition at 1.5 stops, percentiles, metering mask.
- https://docs.unrealengine.com/5.0/en-US/PythonAPI/class/CameraExposureSettings.html (search snippet) - speed_up 3.0 / speed_down 1.0 defaults.
- https://raw.githubusercontent.com/Unity-Technologies/PostProcessing/v2/PostProcessing/Runtime/Effects/AutoExposure.cs and .../Shaders/Builtins/AutoExposure.compute - PPv2 defaults and exp2 adaptation law.
- https://docs.unity3d.com/Packages/com.unity.postprocessing@3.4/manual/Auto-Exposure.html - property semantics.
- https://www.alextardif.com/HistogramLuminance.html - histogram auto exposure, tau 1.1, log range [-10, 2].
- https://bruop.github.io/exposure/ - histogram exposure, temporal blending.
- https://en.wikipedia.org/wiki/Exposure_value - EV100 table, L = 2^(EV-3).
- CIE 88:2004 summaries via search (scribd "Tunnel-Int", inrim x046-PP28.pdf, smartcitiesdive) - tunnel zones, 1:3 steps, black-hole effect.
- https://docs.unity.com/en-us/engine/6000.7/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom.md - URP Bloom property docs (Filter, HQ, Downscale, Max Iterations).
- https://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare/ - CoD:AW bloom, 1/(1+luma) anti-firefly weighting.
- https://support.google.com/youtube/answer/1722171 - YouTube upload encoding settings and bitrates.
- https://slhck.info/video/2017/02/24/crf-guide.html - CRF meaning, ranges, +/-6 rule.
- https://shotstack.io/learn/ffmpegcrf/ - CRF ranges for H.264/H.265/VP9.
- https://bartwronski.com/2016/10/30/dithering-part-three-real-world-2d-quantization-dithering/ - triangular vs blue-noise dithering.
- https://www.forasoft.com/learn/video-encoding/glossary/terms/banding, https://provideocoalition.com/?p=6172, https://forum.videohelp.com/showthread.php?p=2235440 (search snippets) - banding causes, grain workaround, static vs noise dither (anecdotal).
- https://dev.to/masonwritescode/turn-on-av1-film-grain-synthesis-and-measure-what-it-saves-on-your-own-footage-37bb, https://norkin.org/pdf/DCC_2026_FGS_with_debanding.pdf (search snippets) - grain bitrate cost, FGS and banding.
- https://filmdaft.com/how-to-get-better-image-quality-on-your-youtube-videos/, https://clip.dor.gg/en/blog/upload-game-video-without-quality-loss (search snippets) - upload-at-higher-resolution tip (community).
- https://www.siliconera.com/graphic-designers-discuss-creating-shadow-of-the-colossus-majestic-world/, https://glitterberri.com/extended-developer-interview/the-graphic-designers-tell-it-straight-2/ (search snippets) - fog and draw distance for scale.
- https://shootonline.com/?p=47151, https://theasc.com/articles/arrival-cinematography-bradford-young (search snippets) - Blade Runner 2049 and Arrival light/atmosphere notes.
- https://www.naturettl.com/?p=19594, https://www.adobe.com/creativecloud/photography/discover/golden-hour (search snippets) - golden-hour side, front and back light, warm tones.
- https://www.sydneyoperahouse.com/uncovered/uncovered-milly-dent (search snippet; direct fetch 404) and https://www.yourlifechoices.com.au/travel/fun-facts-about-the-sydney-opera-house/ - 1,056,006 Hoganas tiles, glossy and matte, glare avoidance.
- https://www.britannica.com/art/aerial-perspective (search snippet; fetch 403) - aerial perspective definition, Leonardo.
- Local package source: URP/Runtime/Overrides/Bloom.cs; URP/Runtime/Passes/PostProcess/BloomPostProcessPass.cs; URP/Shaders/PostProcessing/Bloom.shader; URP/Shaders/PostProcessing/UberPost.shader; URP/Shaders/PostProcessing/Common.hlsl; URP/Runtime/Passes/PostProcess/UberPostProcessPass.cs; URP/Runtime/PostProcessUtils.cs; URP/Runtime/PostProcess.cs; URP/Runtime/UniversalRenderPipelineCore.cs; URP/Runtime/Data/UniversalRenderPipelineAsset.cs; URP/Runtime/UniversalAdditionalCameraData.cs; URP/Runtime/Overrides/ColorAdjustments.cs; CORE/ShaderLibrary/PhysicalCamera.hlsl; CORE/ShaderLibrary/Color.hlsl; CGINC/UnityCG.cginc - all bloom, exposure, dither and format facts tagged VERIFIED-SOURCE.

### 09: Depth precision, shader stripping and player-build safety (URP 17.6 / Unity 6000.6.0f1)

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

