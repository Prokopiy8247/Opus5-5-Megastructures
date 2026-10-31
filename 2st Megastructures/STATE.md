# STATE — The Ring

Run started at 2026-10-29T23:01:18+01:00 (local OS time, Europe/CET, taken with `date -Iseconds` at the first command of the run)

## Current phase
Phase 7 - Final (shortened at the user's request because of their usage budget). Wave 2 of beat production is integrated: all four
builders finished, final build and final key shots done (QA/final). The full Phase 6 verification (critic panel, sequences, perf,
soak, encode test, wall bot, EditMode re-run) was not run in session 2; see OPUS_5.5_FINAL_REPORT.md "Known limitations".

## Milestones
| Milestone | Commit | Notes |
|---|---|---|
| Phase 0 preflight | 240e261 | git init, .gitignore, Tools/unity-queue.ps1, Tools/fastcheck.py, Tools/metrics.py |
| Phase 1 research + contracts | 3f6c562 | 9 research notes, core C#/HLSL contracts, sky + ceramic shaders |
| Phase 2 builder wave | a8d02d8 | 11 modules (editor, runtime, audio, UI, QA, rendering, terrain, beats 1-7), reviewed + fixed; RebuildAll OK, 13 shaders compile (114 variants), player build OK (105 s, 99 MB) |
| Phase 3 first full pass | f1ae569 | arch geography, light calibration 0.45; autotest passes in the player (goal 636.79 s, exit 0); run04 captures + 3-lens panel |

## Ownership, production wave 2 (one owner per file; critics and researchers are read-only)
| Path | Owner |
|---|---|
| `Shaders/Include/**`, `RingSky/RingCeramic/RingTerrain/RingEmissive/RingAdditive/RingFinalDither.shader`, `Scripts/Runtime/Core/**`, `Directors/**`, `Player/**`, `Terrain/**` (C#), `Rendering/**`, `UI/**`, `Audio/**`, `QA/**`, `Scripts/Editor/Batch/**`, `MaterialDefs/RenderingMaterialDefs.cs`, tests, Tools, docs, scenes, ProjectSettings, builds, git | lead |
| `Beats/Beat01_Capsule/**`, `Beats/Beat02_Valley/**`, `RingWater.shader`, `RingGrass.shader`, `MaterialDefs/Beat12MaterialDefs.cs`, `MaterialDefs/TerrainMaterialDefs.cs` (water, grass, flower entries) | B12 |
| `Beats/Beat03_Ark/**`, `Beats/Beat04_Sunset/**`, `RingArk.shader`, `MaterialDefs/Beat34MaterialDefs.cs` | B34 |
| `Beats/Beat05_Wall/**`, `Beats/Beat06_Tunnel/**`, `RingWall.shader`, `RingTunnel.shader`, `MaterialDefs/Beat56MaterialDefs.cs` | B56 |
| `Beats/Beat07_Outside/**`, `RingHull/RingSwarmPanel/RingOuterFace.shader`, `MaterialDefs/Beat7MaterialDefs.cs` | B7 |
Builder rules: `docs/BUILDER_BRIEF.md`. Unity batch calls and player runs share one mutex (`Tools/unity-queue.ps1`, `Tools/run-player.ps1`).

## Open defects (run04 panel; details in QA/critique-run04-raw.json)
| ID | Severity | Owner | Description |
|---|---|---|---|
| D004 | S1 | lead | Money shot 1: haze band 6-19 deg hides the land curving up (ADR-012 fog/haze rework, verify) |
| D005 | S1/S2 | lead | Arch reads as a map: road graticule, towns on the lattice, camouflage fields (geography rework, verify) |
| D006 | S1 | lead | Night arch: city lights as a lattice of white lines; lit arch overexposed cyan (rework, verify) |
| D007 | S1 | lead | Trench along the path hides the midground and the KS07 door (RingHeight flattening within 60 m, verify) |
| D008 | S2 | lead | Shadow squares translucent and too light; fade in during KS04 (ADR-013, verify) |
| D009 | S1 | B34 | Ark reads as a pale translucent blimp (hull luma 155-200, target 25-120) |
| D010 | S1 | B56 | Rim wall reads as flat royal-blue sky: no gradient, seams or structure |
| D011 | S1 | B7 | Hull never visible (ADR-011 pier), swarm reads as an atom logo, outer face overexposed |
| D012 | S2/S3 | B12/B34 | Capsule khaki, river pool-like, bald meadow, levee/footbridge graybox, lamps cold |
| D013 | S2 | lead | Deck unlit through the tunnel mouth (ADR-013 key window, verify) |

## Next actions (if work resumes)
1. EditMode tests and the wall bot in the player; sequences (`-sequence boundaries,long,statics`) and `Tools/sequence_metrics.py`.
2. A full 5-lens critic panel on `-shots all` (docs/CRITIC_BRIEF.md); fix the open items listed in the final report.
3. Perf log, soak, encode test (`Tools/encode_test.py`).

## Commands
- Fast compile: `python -I Tools/fastcheck.py all`
- Unity batch (serialised): `powershell -NoProfile -File Tools/unity-queue.ps1 -Name <log> -Method <Class.Method>`
- Rebuild all: `Tools/unity-queue.ps1 -Name rebuild -Method Megastructure.EditorTools.BatchTools.RebuildAll`
- Build player: `Tools/unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows`
- Capture: `"Builds/The Ring/The Ring.exe" -capture QA/captures/<run> -shots all`
- Metrics: `python -I Tools/metrics.py`

## Part 14.0 digest (verbatim copy)
Use: after a compaction re-read Part 2, this digest and the section of the beat in progress only; re-read Part 14 in full at each milestone. Copy this digest verbatim into `STATE.md`. The numbers here are copies of the sections below; if they ever differ, the section wins and the digest is corrected.

- **Game.** Ringworld, R = 16 000 m. A 960 m walk (+Z, then +X) through 7 beats: bright golden hour, white ceramic, wind and silence; serenity turns into vertigo. Money shots: (1) beat 2, the land curves up and the far side of the ring hangs overhead; (2) beat 3, the cloud shadow is a ship; (3) beat 4, shadow squares cross the star, night, the lit arch; (4) beat 7, a star inside a glittering swarm.
- **Timeline** (progress s in metres; durations at 1.5 m/s):

| Beat | s range | Duration | Key shots |
|---|---|---|---|
| 1 Capsule | 0-24 | 16 s | KS00, KS01 |
| 2 Valley, bridge | 24-276 | 168 s | KS02, KS03 |
| 3 The ark | 276-411 | 90 s | KS04 |
| 4 Sunset, night, underpass | 411-594 | 122 s | KS05, KS06 |
| 5 Rim wall | 594-774 | 120 s | KS07, KS08, KS09 |
| 6 Wall tunnel | 774-894 | 80 s | KS10, KS11 |
| 7 Outside platform | 894-960 | 44 s | KS12-KS15 |

- **Constants.** Eye 1.7 m; walk 1.5, brisk 2.5 m/s; player radius 0.35; star az -68 / el 26, L = (-0.833, 0.438, 0.337), 2.4 degrees; near 0.05 m, far 15 000 / 300 (beat 6) / 60 000 m; fog sigma 2.5e-4 (beats 1-3), 2.5e-4 -> 3.6e-4 (4), 5e-4 (5), 4e-3 (6), 0 (7); vFOV 60, 64, 66, 64, 66, 60, 60 -> 66; EV slew limit 0.30 EV/s; waypoints (0,0) (0,24) (0,276) (0,411) (0,558) (0,576) (18,576) (100,576) (100,660) (114,660) (234,660) (300,660); door and tunnel profile 2.6 x 3.0, wall x 114..234, platform 30 x 72, beacon (301, 660).
- **Key shots** (id: s, eye, yaw / pitch, vFOV): KS01 5.1, (-0.88, 2.6, 5.07), -45/+25, 60; KS02 150, (0, 4.7, 150), 0/+14, 64; KS03 150, same eye, 6/+38, 64; KS04 364, (0, 1.7, 364), -12/+14, 66; KS05 442, (0, 1.7, 442), -58/+20, 64; KS06 505, (0, 1.7, 505), 0/+28, 64; KS07 606, (30, 1.7, 576), 80/+16, 66; KS08 700, (100, 1.7, 600), 20/+30, 66; KS09 752, (100, 1.7, 652), 60/+2, 66; KS10 800, (140, 1.7, 660), 90/0, 60; KS11 888, (228, 1.7, 660), 90/0, 60; KS12 912, (252, 1.7, 660), 92/+9, 66; KS13 912, same eye, 90/-28, 66; KS14 956, (296, 1.7, 660), 90/+3, 66; KS15 900, (240, 1.7, 660), 270/+20, 61.3.
- **Cut order** (14.8): 1 ark; 2 beat-2 detail and cloud shadows; 3 wall-door detail; 3b climber, footbridge arch, tunnel spill; 4 capsule extras; 5 swarm to 12 000, gantries; 6 tunnel markers; 7 squares B and C, city lights. Never cut: bend, shared atmosphere, QA, the cores of the money shots.
- **Hard problems** (14.7): H1 curved world with the stable form `B.y = h cos t + 2 R sin^2(t/2)` and inflated bounds; H2 arch sky with `ArchHaze(e)`, never `RingFog`; H3 depth D32 float, near 0.05, star as a sky element; H4 one atmosphere, EV slew 0.30 EV/s; H5 banding: analytic gradients, RGBA16F, one static final dither pass, camera dithering off; H6 swarm in 3 indirect draws with analytic star occlusion; H7 clipmap and stable shadows; H8 time and progress phases, capture mode equals progress; H9 star, bloom and NaN guards; H10 shared lighting (Forward+, bent normal, rotated key, sky-colour reflections).
- **Rules that are easy to forget.** Every parameter crossfades over 24 m around each beat boundary unless a row gives its own window (Rule P, 14.6); every set is pre-warmed during the load fade and swaps never create anything (14.6); no visible swap, ever: sealed, dark or fogged; 4 capture-critique-fix cycles per beat, then demote to the next fallback tier (14.8); the key-shot table (14.10) is the only source of capture poses and vFOV.
