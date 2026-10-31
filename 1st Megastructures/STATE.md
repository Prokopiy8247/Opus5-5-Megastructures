# STATE — Abyss

Run started at 2026-10-29 23:02:14 +01:00 (taken from the operating system clock at the start of Phase 0).

## Current phase
Phase 7 — Final (ended early at the user's request to save budget). Fix round R2 completed by all ten owners; final build, EditMode 385/385, autotest PASS, final key shots `QA/captures/f04_keys` (38/50 metric PASS), 4K set in `QA/final/`. Critic round 3, the full sequence suite and a final perf/soak re-run were not done (see `OPUS_5.5_FINAL_REPORT.md` Known issues).

## Milestones
| Milestone | Commit | Notes |
|---|---|---|
| Phase 0 preflight | 767cd85 | git init, .gitignore, STATE.md |
| Phase 1 research sprint | cb4f9b3, a46e9d3 | `docs/research/`, `docs/RESEARCH.md` |
| Phase 2 foundations | 918cce2 | core, MeshKit, shaders, audio, QA tools, RebuildAll, first player build |
| Phase 3 graybox | 7a2111a | built-player autotest PASS; capture parity PASS; audio PASS |
| Phase 4 beat production | c95511d | Full tier of all 7 beats, reviews + fixes |
| Phase 5 look-dev | f32765d..e0c6b30 | key-light depth falloff, exposure/fog calibration, contrast 0, beams, area look-dev; critic round 1 (41 findings) |
| Fix round R1 | 33bcdb7 | 10 owners; 350/350 EditMode; autotest, bots, perf (1080p 691 fps, 4K 225 fps), 30-min soak PASS |
| Critic round 2 | d0e783d | 37 open (5 S1, 16 S2, 16 S3) |
| R2 checkpoint (resume) | afca26a | partial R2 owner edits, build OK, EditMode 379/383 |
| Fix round R2 + Phase 7 | (final commit) | all R2 owners done; EditMode 385/385; autotest PASS 656.6 s; f04 key shots; QA/final 4K |

## Open defects
Not re-judged by a panel after fix round R2. Open metric items (all readability targets, no visible artefacts): KS2a wall macro, KS3 pour lifts y 6/9, KS7b readable cells 129/150 and amber hue, KS1 21:9 mean luma; R2-16 1-px dark ring at the far tunnel mouth (B6_start). Details: `OPUS_5.5_FINAL_REPORT.md`.

## Ownership table (Part 5.2: two agents never edit the same file)
| Area | Owner (fix rounds) |
|---|---|
| ProjectSettings, Packages, Assets/Settings, `Scripts/Editor/{BatchTools,ProjectSetup,RenderSetup,SceneBuilder}.cs`, scenes, builds, Unity process, root docs, `Tools/*.ps1`, contracts (`AbyssConst`, `AbyssPalette`, `GameState`, `BeatBase`, `MeshBuilder`, `CorridorKit`, `MaterialLibrary`) | Lead |
| `Beats/Beat01_Gallery` | gallery |
| `Beats/Beat02_Bridge`, `Beats/Beat03_Ledge` | ledge |
| `Beats/Beat04_Hall` | hall |
| `Beats/Beat05_Lift` | lift |
| `Beats/Beat06_Fan`, `Shaders/BeamWedge.shader` | fan |
| `Beats/Beat07_Reveal` | reveal |
| `Shaders/**` (except BeamWedge, AbyssLighting, AbyssFog, Sky), `MaterialBuilder`, `ProceduralTextures` | shaders |
| `ProgressTables`, `ProgressDirector`, `KeyLightRig`, `ShaderIds`, `AbyssLighting.hlsl`, `AbyssFog.hlsl`, `Sky.shader`, `Runtime/UI/**` | global |
| `Runtime/Player/**`, `GazeCues`, `PathGuide` | player |
| `Runtime/QA/**`, `Tools/qa/**`, `docs/QA_*.md` | qa |
| `Runtime/Audio/**`, `Tools/audio_analyze.py`, `docs/AUDIO_MIX.md` | audio (done; all checks PASS) |
| `Runtime/Geometry/MeshKit*`, `MeshValidation`, `VertexBake` | meshkit (done) |

## Next actions
If work resumes: critic panel round 3 on f04, fix the open items above, run the full sequence suite (`-shots seq:...`, write frames to a drive with space), perf 1080p/4K and the 30-minute soak on the final build.

## Commands
- Fast compile: `powershell -NoProfile -File Tools\fastcompile.ps1`
- Rebuild + build: `powershell -NoProfile -File Tools\unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows`
- EditMode tests: `powershell -NoProfile -File Tools\unity-queue.ps1 -Name tests -Tests EditMode`
- Look-dev loop (build into its own folder + capture + metrics): `powershell -NoProfile -File Tools\lookdev.ps1 -Slot <name> -Run r01 -Shots look`
- Key shots: `Builds\Abyss\Abyss.exe -screen-fullscreen 0 -capture QA\captures\<run> -shots keys`, then `python -I Tools\qa\image_metrics.py QA\captures\<run>` and `python -I Tools\qa\contact_sheet.py QA\captures\<run>`
- Autotest `-autotest`; bots `-bot all`; perf `-perf-log QA\perf\<run>.csv [-res 3840x2160]`; soak `-soak 1800`; audio `-render-audio QA\audio\walk.wav -quit-after 45` + `python -I Tools\audio_analyze.py QA\audio\walk.wav`
- Metrics (Part 15.3): `python -I Tools\metrics.py --update-report OPUS_5.5_FINAL_REPORT.md`
- Details: `docs/QA_RUNTIME.md`, `docs/QA_TOOLS.md`, `DEVELOPMENT.md`
