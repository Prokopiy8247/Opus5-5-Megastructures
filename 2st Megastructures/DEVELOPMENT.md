# DEVELOPMENT — The Ring

Engineering notes: architecture, contracts between modules, commands, decisions (ADRs), blockers.
The specification is `MASTER_PROMPT.md` (Part 14 = this game). This file records how it is implemented.

## Versions (verified 2026-10-29)
- Unity 6000.6.0f1 (f7f8ed4d1e24) at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`, Windows standalone support installed.
- URP 17.6.0 (`Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184`), Core RP 17.6.0, Input System 1.20.0,
  Test Framework 1.8.0, UGUI 2.6.0, Timeline 6.6.0, AI Navigation 2.0.14, Burst 2.0.0 and Collections 6.6.0 present as dependencies.
  `com.unity.mathematics` is not installed: plain C# maths is used.
- GPU NVIDIA GeForce RTX 5070 (driver 617.42, 12 GB). git 2.51.1, Python 3.12.10, Node 24.20.0, dotnet SDK 10.0.401, ffmpeg on PATH (WinGet).
- `activeInputHandler: 1` was already set in `ProjectSettings/ProjectSettings.asset` before the first launch by this run.

## Commands
| Purpose | Command |
|---|---|
| Fast C# compile check (no Unity, safe in parallel) | `python -I Tools/fastcheck.py all` |
| Any Unity batch call (serialised by a named mutex) | `powershell -NoProfile -File Tools/unity-queue.ps1 -Name <log> -Method <Class.Method> [-ExtraArgs ...]` |
| Rebuild everything (settings, URP assets, materials, generated HLSL, scene) | `Tools/unity-queue.ps1 -Name rebuild -Method Megastructure.EditorTools.BatchTools.RebuildAll` |
| Compile + shader gate | `Tools/unity-queue.ps1 -Name gate -Method Megastructure.EditorTools.BatchTools.CompileAndShaderGate` |
| Player build | `Tools/unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows` |
| EditMode tests | `Tools/unity-queue.ps1 -Name tests -ExtraArgs "-runTests","-testPlatform","EditMode","-testResults","<root>\QA\tests-editmode.xml"` (no `-quit` with `-runTests`: use `-NoQuit`) |
| Key-shot capture in the player | `Builds\The Ring\The Ring.exe -capture QA\captures\<run> -shots all -logFile QA\captures\<run>\player.log` |
| Image metrics | `python -I Tools/qa_metrics.py QA/captures/<run>` |
| Project metrics (Part 15.3) | `python -I Tools/metrics.py` |

## Architecture overview
Everything visible is generated. The scene `Assets/_Game/Scenes/Main.unity` (written by `BatchTools.RebuildAll`) only contains the
bootstrap objects; all geometry is generated at load behind the black fade by the beat builders (Part 7.4, load <= 15 s).

```
Main.unity
  [Game]        GameBootstrap (refs RingAssets), QualityBootstrap, CliArgs, ProgressDirector, BeatDirector, AtmosphereDriver,
                PerfMonitor, CaptureHarness, WorldBuilder
                + UIDocument (RingPanel) + UiDirector
  [Player]      CharacterController + PlayerController + AutoWalker
     Camera     Camera + UniversalAdditionalCameraData (dithering off, post on) + CameraRig + AudioListener
     Audio      AudioSource (no clip) + AudioDirector (OnAudioFilterRead generator) + AudioReverbFilter   (research 05: not on the listener)
  [Lights]      Star (directional key, shadows), ArchFill (directional), RimFill (directional)
  [Volume]      global Volume (RingVolume profile: Tonemapping Neutral, Bloom, ColorAdjustments, WhiteBalance, Vignette)
generated at load: Sky dome, terrain clipmap, sets FarValley / NearValley / NearPlain / Tunnel / Outside, corridor colliders
```

### Assemblies and folders (ownership: see STATE.md)
| Assembly | Folders | Notes |
|---|---|---|
| `Megastructure.Runtime` | `Assets/_Game/Scripts/Runtime/**`, `Assets/_Game/Beats/**` (via `Megastructure.Beats.asmref`) | namespace `Megastructure` (beats: `Megastructure.Beats`) |
| `Megastructure.Editor` | `Assets/_Game/Scripts/Editor/**` | namespace `Megastructure.EditorTools`; batch entry `Megastructure.EditorTools.BatchTools` |
| `Megastructure.Tests.EditMode` | `Assets/_Game/Scripts/Tests/EditMode/**` | NUnit, Editor only |
| `Megastructure.Tests.PlayMode` | `Assets/_Game/Scripts/Tests/PlayMode/**` | |

### Core contracts (owner: lead) — `Scripts/Runtime/Core`
- `RingConstants` — every number of Part 14.12. Never hard-code a spec number elsewhere; add it here.
- `RingPalette` — palette tokens (sRGB hex -> linear). `RingPalette.Lin("CeramicWhite")`. Generates `Shaders/Include/RingPalette.hlsl` (`PAL_<Token>`).
- `RingMath` — smoothstep/smootherstep (HLSL semantics), `Smax` (H8), `Ramp`, PCG hash, `ValueNoise`, critically damped followers, slew, `DiscVisible`.
- `RingBend` — C# twin of `RingBend.hlsl`; `InflateBounds`, `Rise`, `Rotate`.
- `RingWorld` — path polyline (`Cumulative`, `PointAt`, `DirAt`, `Project`), `PathDist` (14 segments), river (`River`, `RiverDist`, `RiverCarve`),
  `Height(x, z, set)` (twin of `RingHeight` in HLSL), `LeveeMask`. Generates `Shaders/Include/RingWorldData.hlsl`.
- `RingState` — the per-frame world state (single writer: `ProgressDirector`; read by everyone).
- `RingSchedule` — pure functions of (s, u, ...) returning the parameter targets of 14.2.3-14.2.5 and 14.6 (Rule P).

### Shared HLSL (owner: lead) — `Assets/_Game/Shaders/Include`
- `RingCommon.hlsl` — global uniform contract (below), hashes, value noise, IGN, analytic filtered lines/grids.
- `RingBend.hlsl` — `RingBendPosition`, `RingBendRotate`, `RingBendTheta`, `RingBendPN`.
- `RingAtmosphere.hlsl` — `RingSkyAtmosphere(dir)`, `RingSkyColor(dir)` (reflections), `RingFogHeadingColor`, `RingFog(col, bentPosWS, worldY)`,
  `RingArchHaze(e)`, star frame, square SDF, star disc.
- `RingGround.hlsl` — `RingHeight(p, set)`, `RingPathDist`, `RingRiverDist`, `RingGroundAlbedo(arc, x, fp)`.
- `RingSkyArch.hlsl` — the sky dome (`RingSkyDome(dir)`), arch, clouds, outside sky.
- `RingLighting.hlsl` — shared lit model (H10): URP main + additional lights under Forward+, bent key vector, Trilight ambient from globals,
  reflections from `RingSkyColor`, ship/cloud shadow on the key, then `RingFog`.
- `RingLitPass.hlsl` — the standard vertex/fragment structure for every lit shader (ForwardLit, ShadowCaster, DepthOnly, DepthNormals) with hooks.
- Generated: `RingPalette.hlsl`, `RingWorldData.hlsl` (never edit by hand).

### Global shader uniform contract (written only by `AtmosphereDriver`)
| Uniform | Content |
|---|---|
| `_RingCam` | xyz camera position (flat world), w = vertical pixel angle (rad/px). Set per camera in `RenderPipelineManager.beginCameraRendering` (capture cameras too) |
| `_RingTime` | x animation time (s, wrapped at 7200), y time mod 60, z capture flag, w progress s |
| `_RingSet` | x set (0 valley, 1 plain, 2 tunnel, 3 outside), y outside-sky weight, z far-rim rule on/off, w wall x (114) |
| `_SkyParams` | x sky state 0..2, y N(u), z star visible fraction V, w arch night lift 0.5 N |
| `_SunsetParams` | x sunset u, y squares haze alpha, z terminator half-width (deg), w squares B/C on |
| `_StarDir`, `_StarDirOut` | xyz toward the inside star L / outside star L7, w angular radius (rad) |
| `_FogParams` | x sigma, y valley-mist k, z desaturation 0.25, w space-darkening weight (beat 7) |
| `_FogTint` | rgb override fog colour, a weight (tunnel fog, space) |
| `_AmbSky/_AmbEquator/_AmbGround` | Trilight ambient, already multiplied by the beat multiplier |
| `_ShipShadow` | x, y centre (x, z), z strength, w felt-cue key dip |
| `_CloudShadowA/B` | x, y centre (x, z), z strength |
| `_ArkParams` | x x_c, y window level, z engine level, w roll |
| `_Lamps` | x bollard lamp level, y city level, z beacon pulse 0..1, w tunnel mouth spill |
| `_Wind` | x gust 0..1 (`_Gust`), y wind phase time |
| `_Swarm` | x glint scale g(s), y swarm time |
| `_KeyLightDir/_KeyLightColor` | the key vector toward the light and its colour x intensity (for analytic terms) |

### Runtime systems (names follow Part 7.3)
`GameBootstrap` (boot coroutine: build world, pre-warm sets, warm shaders, fade in) · `QualityBootstrap` · `CliArgs` · `WorldBuilder` (runs every
`IBeatBuilder` with a `BuildContext`) · `PlayerController` · `CameraRig` (Hor+ FOV, near/far, camera-inside-collider push-out) · `PathGuide`
(lane table, corridor colliders, safety volume, last safe point) · `AutoWalker` (fillets, speed profile, cinematic holds, `GazeSchedule`) ·
`ProgressDirector` (s, time branches, followers -> `RingState`) · `BeatDirector` (sets with hysteresis, events once and in order, activation log) ·
`AtmosphereDriver` (schedule -> slew -> globals, lights, ambient, volume, camera) · `AudioDirector` + `RingAudioEngine` (Unity-free DSP) ·
`FadeController`/`UiDirector` (UI Toolkit) · `CaptureHarness` · `PerfMonitor` · `MeshKit` · `InstancedField` · `RingAssets` (material library).

### Colour rule (all code)
Palette values are linear. `MaterialFactory` gamma-encodes plain (non-[HDR]) Color material properties so shaders receive the exact linear value;
[HDR] colours and vectors are written unchanged. At runtime set linear values with `SetVector`/`SetGlobalVector` (never `SetColor` on a plain
Color property). Light colours are given as sRGB (`RingPalette.HexToSrgb` or `linear.gamma`). Optional global `_RingDitherOff` (1 = dither off,
QA encode A/B only). The terrain shaders share `Assets/_Game/Scripts/Runtime/Terrain/RingTerrainShared.hlsl` (owner BG).

## Decisions (ADRs)
- **ADR-001 Generate at load.** Geometry is generated at load (behind black) instead of being stored as mesh assets; the editor build script writes
  only settings, materials, generated HLSL and the bootstrap scene. Keeps the repository small and the build reproducible (Part 7.1, 7.4).
- **ADR-002 One terrain clipmap with per-quad vertices.** Every level is a full 128 x 128 quad grid with 4 vertices per quad; the vertex shader
  collapses quads that lie inside the finer level (exactly, because the fine boundary always lies on coarse grid lines) and geomorphs the outer 15 %.
  Avoids L-shaped fill strips and T-junction cracks; 2 m skirts on every level edge.
- **ADR-003 Sky dome at the far plane.** The dome is a camera-centred sphere whose clip z is forced to the far plane (reversed Z aware); queue
  Background, ZWrite off. Capture cameras get it for free because the vertex shader uses `_WorldSpaceCameraPos`.
- **ADR-004 Seam-free arch.** All arch patterns are periodic in arc with the circumference (cells 168 x 598.4 m, town cells 60, river wavelengths
  2 pi R / n, clouds through a circle mapping), and the arch footprint is analytic (t x pixel angle / cos incidence) instead of ddx/ddy, so the
  antipode (straight up) has no seam and no 1 px line.
- **ADR-005 Arch inscatter.** The far side gets its own aerial inscatter `lerp(inscatter, ground, exp(-t / 46 km))` before `ArchHaze`, so the
  upper sky reads blue (KS02 acceptance (3)) while `ArchHaze` alone controls the contrast measured by acceptance (8).
- **ADR-006 River carve profile.** Bed -3.2 m within 15 m, bank 1:3, waterline (y -2.2) at 18 m half-width (= 36 m river), 0 at 24.6 m; the river
  ribbon is 40 m wide so its edges are always under the bank. The river centre line is extended along its end tangents to x = +-8 200 m.
- **ADR-007 Beat 6 far plane (arbitration, Part 1.6.2).** Far = 300 m in the tunnel contradicts the geometry: through the door (looking back) the
  terrain clipmap and the plain are visible beyond 300 m, and through the mouth the hull (to 6 km) is visible from s ~886. A far plane at 300 m would
  cut them with a visible line or pop. Depth precision does not need it (reversed float depth: ~0.7 mm at 12 km). Decision: far 15 000 m in beats 1-6,
  60 000 m from s 880 (nothing beyond 15 km is visible from inside the tunnel, so the switch is invisible).
- **ADR-008 Swarm independent of the far plane.** Swarm panels are computed in true space (28-52 km) and remapped radially about the camera to
  150-250 m in the vertex shader (direction preserved, so angular size and screen position are exact; depth order preserved). Nothing else lies in
  those directions between 250 m and 52 km (the hull horizon is 11.4 deg below the eye line, band 3 reaches at most 9.7 deg below).
  Superseded in part by ADR-011: the hull now rises to 6.6 deg below the eye line, so the remap range moves beyond the hull horizon.
- **ADR-009 Sealed swap at s 572.** The `NearValley -> NearPlain` swap (`_Set` 0 -> 1) fires at s 572 in the first underpass leg instead of the
  corner s 576 (the exit portal is hidden behind the corner there; at 576 the portal was already in view) and reverts below s 568 (hysteresis).
- **ADR-010 Shadow distance 220 m from the start.** The beat-1 row asks for 30 m; switching to 220 m at s 12 popped every valley shadow at once.
  220 m is used from s 0 (the capsule interior is small, the extra cascades cost little), 90 m outside from s 834.
- **ADR-011 Beat-7 pier and hull depth (arbitration, Part 1.6.2).** From a 1.7 m eye the hull, 11.4 deg below the horizon with the specified 600 m
  drop (`acos(30 000 / 30 600)`), is visible only within `1.7 / tan 11.4 = 8.4 m` of a deck edge. On the 30 m deck the walker is 15 m from the side
  edges and up to 54 m from the front edge, so the "underside of the world far below" never appears (KS12/KS13 showed only deck tiles; critics run04,
  S1). Decision: the deck is a 12 m pier (z 654..666, x 234..306 unchanged, lane 10.3 m, rail colliders at z 660 +- 5.5) and the hull top lies 200 m
  below it: dip `acos(30 000 / 30 200) = 6.6 deg`, horizon `sqrt(2 R h + h^2) = 3 470 m`. The hull is visible beyond both rails wherever the edge is
  within `1.7 / tan 6.6 = 14.7 m` (azimuth >= 24 deg off the walk), i.e. in the lower corners of KS12 and both lower sides of KS13. The swarm remap
  range moves to 9-14 km (beyond the new hull horizon, inside the 15 km far plane of the tunnel).
- **ADR-012 Lighter valley fog, haze-layer arch model (arbitration, Part 1.6.2).** With sigma 2.5e-4 the land is 63 % fogged at 4 km (7 deg) and
  98.5 % at the 8 km terrain edge; in KS02 a featureless haze band ran from 6 to 19 deg elevation and the land never visibly curved up into the sky
  (critics run04, S1, money shot 1). Decisions: (a) beats 1-3 use sigma 1.0e-4 (with the 0.8 valley mist the terrain edge is 72 % fogged, 4 km 27 %),
  beat 4 ramps to 3.6e-4 at night, beats 5-7 unchanged; (b) the fog thins with height above the floor (scale height 2 km, beats 1-4), so the ark at
  1.8 km altitude is ~14 % fogged instead of 59 % and can read dark (its luma target 25-120 was unreachable with 59 % of a fog colour of luma ~190);
  (c) a distance desaturation (Part 8.4 aerial perspective, `1 - exp(-d / 600 m)`, x 0.35) makes saturation fall layer by layer from the midground;
  (d) the arch keeps the shape of 14.7 H2 through a haze-layer model instead of the fixed elevation curve: a ray of elevation e leaves the near-floor
  haze layer after `2 079 m / sin e` (2 079 = 8 192 sin 14.67, so the layer limit meets the terrain edge exactly), giving hz 0.72 / 0.51 / 0.29 /
  0.19 / 0.09 at 14.7 / 20 / 30 / 40 / 90 deg in the valley (monotone, hz(40) <= 0.2). Below the arch the dome uses the current fog of the true
  distance clamped at the terrain-edge distance of that heading, blended into the layer haze over 14.7-19.7 deg, so the dome meets the fogged clipmap
  edge in every direction and, in the night beats (sigma 3.6e-4 / 5e-4), the seam is fully fogged as in the original spec. (e) Terrain and arch
  share one ground-lighting model (`RingGroundLightNow/Day`): inside the squares' night band both use the current rig, outside it both use daylight
  (the far side stays sunlit), so the clipmap edge cannot show a lighting seam at dusk. The sunlit arch at night is scaled by 0.40 (exposure
  compensation for the night EV +1.6; acceptance 14.5.4 (3) lit arch luma 150-230).
- **ADR-013 Night grade and fills.** The night white balance (-16 / -12) is halved to -8 / -6 and the night fills (arch fill, rim wash) use the
  midpoint of RimFill and ArchFill: with the original values the warm lamps rendered cold white and the wall and meadow saturated royal blue
  (critics run04). The shadow squares fade in over u -0.30..0 (s 379.5-411) instead of -0.6..-0.2: as translucent panes they upstaged the ark in its
  own key shot KS04 (s 364). The outside key (L7, masked to x >= 233) rises over s 834-866 so the deck seen through the mouth is sunlit at the exit.

## Research-driven decisions (sprint of 2026-10-29, details in docs/research)
- Forward+ keyword in URP 17.6 is `_CLUSTER_LIGHT_LOOP` (`_FORWARD_PLUS` is deprecated); non-main directional lights need their own loop over
  `URP_FP_DIRECTIONAL_LIGHTS_COUNT`; `LIGHT_LOOP_BEGIN` needs a local `inputData`; use `GetMainLight(shadowCoord, positionWS, shadowMask)` (fade);
  never the 7-argument `LightingPhysicallyBased` overload (buggy). Only `_SHADOWS_SOFT` is used on Windows. (01)
- Our own ambient/reflection: mirror `UniversalFragmentPBR` with `EnvironmentBRDF(brdf, ourAmbient, ourReflection, fresnel)`; never write
  `unity_Ambient*` / `_GlossyEnvironmentColor` (URP overwrites them per camera). `_ENVIRONMENTREFLECTIONS_OFF` defined before the includes. (01)
- Render Graph only in 17.6; `FullScreenPassRendererFeature` is RG-native; post-processing needs `postProcessData` on a renderer created from
  script; the saved `RingVolume.asset` is mandatory (post-processing variant stripping keeps only effects active in saved profiles). (02, 09)
- Camera dithering, film grain run in UberPost before an AfterRenderingPostProcessing pass; with dithering off our pass is the only dither. (02)
- Default depth format on DX11/12 is `D32_SFloat_S8_UInt`; set `depthAttachmentFormat = Depth_32_Stencil_8` explicitly; reversed Z on. (02, 09)
- GPU Resident Drawer culls with CPU bounding spheres: OFF (bent geometry). `Mesh.AllocateWritableMeshData` (not Acquire); `GetInstanceID`
  is obsolete-as-error in 6000.6. Indirect draws: compute world positions in the shader, explicit `worldBounds`, `new RenderParams(material)`
  with shadows set explicitly; `UnityIndirect.cginc` lives in `Editor/Data/Resources/CGIncludes`. (03)
- Captures: `RenderPipeline.StandardRequest` into an HDR RT (`R16G16B16A16_SFloat`, msaaSamples 4; the RT format becomes URP's internal colour
  format and the asset MSAA is ignored for RT targets), then an sRGB blit and `AsyncGPUReadback`. `Time.captureDeltaTime` for sequences. (04)
- Audio: the OnAudioFilterRead generator must be the first filter on an AudioSource with no clip, not on the AudioListener GameObject;
  `Play()` after adding components; set 48 kHz in AudioManager; `reflectionsLevel` default -10 000 mB mutes the wall echo. (05)
- UI Toolkit in the player: PanelSettings must be an editor-created asset with a `.tss` theme (`@import url("unity-theme://default")`), the UI
  shaders go into Always Included Shaders; `letterSpacing` accepts px/% only (0.12 em -> 8.04 px at 67 px); keep UIDocument enabled. (06)
- Build: Mono only installed; `NamedBuildTarget.Standalone`; runInBackground must be set (template had it off). (05, 06)

## Blockers and decisions
- 2026-10-30 04:00 Session usage limit interrupted the builder wave (10 of 17 agents). Recovery: committed the partial output, resumed the same
  workflow from its run id (finished agents replayed from cache) with a resume note for the interrupted builders; fixers now run only for S1/S2.
- 2026-10-30 04:10 First Unity compile failed: the editor asmdef lacked `Unity.RenderPipelines.GPUDriven.Runtime` (GPUResidentDrawerMode).
  Added the reference; `Tools/fastcheck.py` now honours asmdef references so this class of error is caught without Unity.
