# QA runtime (in-game harness)

The C# side of Part 11 / 14.10: captures, composition lint, frame sequences, autotest, bots, perf log, soak and the
first/last-frame check. Code: `Assets/_Game/Scripts/Runtime/QA/` (namespace `Megastructure`), entry point `QaHarness`
on the `Bootstrap` object (the SceneBuilder adds it by type name `Megastructure.QaHarness`), execution order +300.
The Python analysis of the outputs is documented in `docs/QA_TOOLS.md`.

**Inert by default.** Without a QA flag `QaHarness.Awake` disables the component: no Update/LateUpdate, no log hook,
no allocation. QA flags: `-capture`, `-capture-parity`, `-autotest`, `-bot`, `-perf-log`, `-soak`, `-quit-after`,
`-firstlast`.

## Running the built player

`Abyss.exe` is a GUI-subsystem program: PowerShell's `&` does not wait and `$LASTEXITCODE` is unreliable. Always:

```powershell
$root = "W:\YouTube\Megastructures\1st Megastructures\1st Megastructures"
$exe  = "$root\Builds\Abyss\Abyss.exe"
$args = '-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "' + "$root\QA\logs\run.log" + '" -autotest'
$p = Start-Process -FilePath $exe -ArgumentList $args -PassThru; $p.WaitForExit(); $p.ExitCode
```

Relative output paths resolve against the project root (found from `Application.dataPath`: the first ancestor of
`Builds/Abyss/Abyss_Data` with `Assets` and `ProjectSettings`), never against the working directory. JSON reports go to
`-qa-out DIR` (default `QA/`). Exit codes: 0 pass, 1 fail or incomplete. `-quit-after S` (wall-clock seconds since start-up)
aborts any mode; partial reports are written and an unfinished mode exits 1 (a plain `-cinematic -quit-after S` exits 0).

Virtual time: capture, autotest, bots and perf run with `Time.captureDeltaTime = 1/60` and VSync off (the harness sets
`vSyncCount 0` after QualityBootstrap applied the display settings), so they are deterministic and run as fast as the
GPU allows. `-realtime` switches autotest / bots / perf to wall-clock time. Soak is wall-clock unless `-soak-virtual`.

## 1. Captures: `-capture DIR -shots SEL [-res WxH] [-nopost] [-lint-only] [-capture-parity [TAG]]`

```
Abyss.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -capture QA/captures/r01 -shots all
Abyss.exe ... -capture QA/captures/r01_keys -shots keys
Abyss.exe ... -capture QA/captures/look01 -shots look
Abyss.exe ... -capture QA/captures/r01_b3 -shots KS3,B3
Abyss.exe ... -capture QA/captures/nopost -shots KS7b,KS7c -nopost
Abyss.exe ... -capture QA/captures/lint -shots keys -lint-only
Abyss.exe -screen-fullscreen 1 -screen-width 1920 -screen-height 1080 -capture QA/captures/parity -capture-parity
```

Selectors (comma-separated): `all` (keys + beats + parallax + seams + tiers + cues), `keys`, `look`, `beats` (stills +
free-look), `stills`, `freelook`, `parallax`, `seams`, `tiers`, `cues` (R1-40 cue stills; since fix round R2 (R2-36 e) `cues`
and `all` also record, first, the title card (`cue_title/title_2s.png`, `title_4s.png`, window captures with the title
timeline restarted in the fresh post-load state) and, after the stills, the cue sequences `cue_bolt`, `cue_ignition`,
`cue_ending` into the same folder), `tests` (every shot with a C#
analysis: parallax, seams, tiers and cues; this fills `tests_summary`), `4k`, `beatN`, an exact tag (`KS7b_honeycomb_4k`), or
a prefix up to an underscore (`KS2a` selects `KS2a_bridge_ahead` and its variants, `B3` the beat-3 stills; `KS1` does not
match `KS1b`; `KS7b` also selects the cue `KS7b_honeycomb_nopost`).

**Key-shot pose arbitrations (fix round R1, Part 1.6.2; `KeyShots.cs` constants, logged by the lead in `DEVELOPMENT.md`):**
KS4 `(-30, -5.3, 269)`, yaw 274, pitch +8 (spec u 28, 270, +4: the left crank wheel and cylinder B cannot be framed from
u 28; from u -2 the wheel sits upper left and cylinder B on the right; R1-20). KS5a yaw 262, pitch +16 (spec 240, +3;
R1-25). KS5b eye z 260.5 (spec 278: the deck edge hid the lower blade ring; R1-04). KS5c eye z 260.4, yaw 176, pitch -38
(spec 261.0, 180, -55; the walker's -Z rail stand, `WalkPlanner.RAIL_STAND_Z`; R1-05, and lead decision D1 of fix round R2:
from the stand the deck edge lies 50.5 degrees down, so at -55 the lower half was deck; at -38 the edge falls to row about
0.66 and the gantry is 33 % visible, qa_r21). The ride gaze (`GazeCues`) uses the same KS5a and -38 / 176 values.
The piston sequence keeps the 14.10 KS4 spot (u 28) because the camera impulse needs the eye inside the hall.

**Cue stills (`cues`, R1-40)**, kind `cue` (luma checks advisory): `cue_freight_t30` / `cue_freight_t31` (ride 30 s,
platform frozen, clock 1 s apart, yaw 240 at face 210; analysis: changed-pixel blobs of at least 12 levels, at least one
moving light; the difference x 4 is written to `QA/analysis/<run>/test_cue_freight_diff.png`, outside the capture folder
(R2-36 j)), `cue_b6_lookback_p0..p3` (gantry z 160 looking +Z at the
far-wall lighthouse patches, four phases a quarter of the 3.33 s pattern period apart; analysis (R2-28): only the far-wall
pixels of the patch region, eye depth at least 500 m from the shot's depth grab, so the blade shadows sweeping the near
portal frames cannot pass it: mean change against p0 at least 0.3 levels and the patch lift (95th percentile of the
per-pixel luma change) at least the 14.5.6 +0.08; inconclusive without depth), `KS7b_honeycomb_nopost` (KS7b with post-processing off for that shot only:
report `nopost: true`, so `image_metrics.py` judges the horizon row against OUT_HORIZ within 2 levels). The falling bolt,
the honeycomb ignition and the ending are sequences (`seq:cues`, or recorded after the stills by `-shots cues`); the title
card is captured by `-shots cues` (`cue_title/`) and by `-firstlast`. Their verdicts (ending sliver, ignition sweep) are
added to report.json `cue_tests` and `tests_summary`.

**`look` (look-dev loop):** the 14 key shots once each at 1920x1080, without the 4K, 16:10 and 21:9 variants (14 images
instead of 50), with the same poses, clocks, regions and composition lint as `keys`. With `-res WxH` it gives the 14 key
shots at that size. Use it for fast look-dev iterations (`Tools/lookdev.ps1 -Shots look` passes it through) and run
`keys` (or `all`) before signing a beat off: the aspect and 4K variants are part of the 11.2 check.

| Shot set | Tags | Sizes |
|---|---|---|
| 14 key shots of 14.10 | `KS1_gallery_start` ... `KS7d_look_back` + suffix | 1920x1080 (no suffix), `_4k` 3840x2160 where marked (8), `_16x10` 1680x1050, `_21x9` 2560x1080 |
| Per beat (11.2) | `B<n>_start`, `B<n>_middle`, `B<n>_end` (along `PathGuide.WalkPoint`, 2 m inside the beat; beat-5 middle = the deck centre at ride second 60), `B<n>_freelook_back / _leftup (-90, +30) / _rightdown (+90, -30)` from the middle | 1920x1080 |
| Parallax (Part 10.30) | `KS7b_honeycomb_px_base/_move10/_yaw2` (pitch 0), `KS7c_goal_door_px_*` | 1920x1080 |
| Wrap seams (14.10 a) | `seam_Y0`, `seam_Y-1332`, `seam_X0`, `seam_X1332`, `seam_Z0`, `seam_Z-1332`: a temporary 40 x 40 m M_ConcreteBoard wall across the plane, seen from 12 m | 1920x1080 |
| Tier crossover (14.10 b, c) | `tier_facade_near/_far` (150 m from the 210-degree city face at ride second 60), `tier_honeycomb_tier1/_tier2` (KS7b position, frame centre on the lattice 6 km away) | 1920x1080 |

`-res WxH` captures every selected shot once at that size (variants collapse; tag suffix from the size). The window
size does not matter: images are rendered offscreen.

**Clock and state per shot.** Ride shots (KS5a/b/c, beat-5 middle) board the player at the top-dock deck centre, then
`LiftController.SetRideSecondsForCapture(t)` with the lift frozen; beats 6-7 park the lift at the bottom (`SetLeft`);
others dock it at the top. Scene clock = `AbyssConst.NominalTimeAtD(d of the pose)` (ride shots: 308.36 + ride seconds),
frozen. One frame with the capture pose far away (every detail tier starts from the far state, as when approaching),
then `CameraRig.SetCapturePose`, every beat's `PrepareCapture(t)`, `ProgressDirector.SnapAll`, `BeatDirector.SnapBeat`,
3 settle frames (`-capture-settle N`), render.

**Render path** (`QaRenderer`, `docs/research/capture-player.md`): `RenderPipeline.SubmitRenderRequest(StandardRequest)`
from LateUpdate (+300, after the instanced draws) into an `R16G16B16A16_SFloat` RenderTexture with MSAA = the URP asset's
count and `D32_SFloat_S8_UInt` depth; MSAA resolve; `AsyncGPUReadback` (+WaitForCompletion; ReadPixels fallback);
linear to sRGB 8-bit on the CPU (exact piecewise, rounded; NaN/+Inf -> 255); PNG via `Texture2D.EncodeToPNG`. Post-processing
and the final dither are included, overlay UI never is. The camera aspect and `_AbPixelAngle` follow the capture size during
the render. `-capture-flip on` reverses the rows if a platform reads back top-down: run the parity test once per build.

**Parity** (`-capture-parity [TAG]`, default KS2a): in one frame the RenderTexture capture at the window size and
`ScreenCapture.CaptureScreenshotAsTexture` after `WaitForEndOfFrame`. Writes `DIR/parity.json` (mean abs difference in
levels for the rows as read and reversed, which orientation matches, pass at <= 1 level = 1/255), `parity_window.png`,
`parity_rt.png`. Use a 1920x1080 window. Alone (no `-shots`) it captures only the parity pair.

**Outputs.** `DIR/<file>.png` and `DIR/report.json` (rewritten after every shot):

```json
{"run": "<dir name>", "run_stamp": "20261030-101500", "date": "...Z", "build": "player", "editor": false,
 "development": false, "nopost": false, "selector": "all", "complete": true,
 "capture": {"color_format": "R16G16B16A16_SFloat", "msaa": 4, "readback": "async", "flip_rows": false, "dither": "Temporal"},
 "parity": {...},
 "shots": [{"tag": "KS2a_bridge_ahead_4k", "base_tag": "KS2a_bridge_ahead", "kind": "keyshot", "beat": 2,
            "file": "beat02_bridge_KS2a_bridge_ahead_4k.png", "width": 3840, "height": 2160, "aspect": "16:9",
            "variant": "4k", "pose": {"pos": [0, -1.3, 120], "yaw": 0, "pitch": -10, "fov": 66}, "clock": 80.0,
            "d": 120, "e": 120, "game_beat": 2, "nopost": false,
            "regions": {"banding": {"fog_top30": [0, 0, 1, 0.3], "void_bottom40": [0, 0.6, 1, 1]}},
            "lint": {"status": "pass|fail|pass_with_known_conflict|no_subjects", "aspect": 1.777778, "known_conflicts": [],
                     "subjects": [{"name": "C1", "status": "pass", "rect": [x0, y0, x1, y1],
                     "centre": [cx, cy], "size": 0.2, "min_size": 0.1, "behind": false, "centre_ok": true, "size_ok": true,
                     "margin_ok": true, "pass": true, "geometry_pass": true}]},
            "ok": true, "mean_luma": 0.09, "readback": "async", "render_ms": 140, "png_bytes": 9000000}],
 "lint_summary": {"pass": 38, "pass_with_known_conflict": 2, "fail": 0, "no_subjects": 10, "failed_shots": [],
                  "known_conflicts": [{"tag": "KS4_hall_pistons", "subject": "left crank wheel [spec-conflict]",
                                       "status": "pass_with_known_conflict"}]},
 "tests_summary": {"pass": 8, "fail": 0, "inconclusive": 2}, "capture_failures": 0, "log": {...}}
```

- `kind`: `keyshot`, `still`, `freelook`, `parallax`, `seam`, `tier`. Free-look and test shots carry
  `"advisory": ["mean_luma", "p99_luma", "clipped_pct"]`. Beat-7 shots carry `zone` (`tunnel` while z > 0).
- `regions.banding`: the 14.10 rows (the defaults of `abyss_qa.py`), replaced by the projected doorway glow (KS1b only)
  and mouth glow (KS7a). Every other beat-1 shot, KS1 included, gets the centre rectangle `[0.30, 0.10, 0.70, 0.90]` as
  `gallery_centre`: at KS1 the sloped stair ceiling hides the doorway (verified geometry), so a projected doorway region
  there would measure the ceiling.
- `regions.doorway` (KS1b only): the clear doorway aperture (intersection of the projected inner and outer planes of the
  1.2 x 2.0 opening, inset 5 % per side) for the 14.5.1 check "doorway luma with under 0.3 % clipped", which `abyss_qa.py`
  evaluates for KS1b only, against the calibrated band 0.40-0.60 (DEVELOPMENT.md gallery row and lead decision D6; spec text
  0.65-0.90).
- 14.5 acceptance regions (key shots, R1-29; judged by `image_metrics.py`, see docs/QA_TOOLS.md): KS2b `side_strips`
  (`left` `[0, 0, 0.3, 1]`, `right` `[0.7, 0, 1, 1]`), KS1 `far_glow` (the projected stair-head box), KS5a `window_grid`
  (whole frame), KS7b `amber_dots` (`[0, 0.34, 1, 1]`, the lower 66 %) and `cells` (one row per lattice cell 0.5-12 km out
  in the lower 66 %: `[cu, cv, row_height, 5 interior points, 6 web points]` as frame fractions, interior = centre and
  +/-175 m along x and z, web = apothem 365 along the face normals, all on the terrace plane), KS6 `fan_ring`
  (`centre`, `ring_radius` of the projected housing), KS3 `column_face` (`[0.16, 0, 0.57, 0.50]`). KS7d gets
  `"banding": {}` (no gradient region: the frame holds the Face and the fan bore, R1-38). Fix round R2 (R2-02): KS3
  `lift_curves` (one row per pour-lift joint of C1, y = 3k from -3 to 48 without the y 0 collar band: `[y, u0, v0, u1, v1,
  ...]`, the visible, in-frame part of the projected iso-y circle, 4-64 points at least 0.005 apart), KS1 `wall_board`
  (`[0.03, 0.28, 0.10, 0.93]`, the left wall at 1-2 m) and KS2a `wall_macro` (`rects` `[0.75, 0.20, 0.98, 0.27]` and
  `[0.75, 0.45, 0.98, 0.52]`, the right shaft wall between the rib lines).
- Horizon regions (KS7b, KS7c key shots), all as `{"rects": [[x0, y0, x1, y1], ...]}` over the same two symmetric side
  ranges, columns 0.02-0.25 and 0.75-0.98, so the vignette weighs them alike and no centred subject (the KS7c door and
  its lamp at columns 0.477-0.523, the pier) enters them. v = the elevation-0 row (34 % from the top for KS7b):
  `horizon_row` v +/- 0.005 (the `-nopost` OUT_HORIZ test); `horizon_sky` v - 0.035 .. v - 0.015; `horizon_fog`
  v + 0.003 .. v + 0.015, the clear colour below the horizon above the far edge of the camera-following tier-2 backdrop
  (28 km, `HONEY_TIER2`, at `TERRACE_Y`); `horizon_backdrop` the tier-2 backdrop from its far edge (+0.002) to the
  distance where the Exterior fog leaves 0.3 % (20.3 km), as 4 strips per side because a cone of constant elevation
  bends away from the horizon toward the frame sides (`KeyShots.RowAtElevation`).
- `lint` (key shots): every subject of `BeatBase.CollectSubjects(base_tag)` of every built beat plus the QA-owned subjects
  of `KeyShots.CollectQaSubjects` (KS1: `far glow (stair-head ceiling) [qa]`, the sloped ceiling over the stairs, R1-16),
  projected through the capture camera at the shot's aspect (near-plane clipped); 14.10 rule (`geometry_pass`): centre
  inside 5-95 % and size (max of visible height, visible width x aspect, in frame heights) >= `MinScreenFraction`.
  `margin_ok` = the 3 % margin of 11.2 (reported). Fix round R1 (arbitration R1-39 / R1-20): a subject whose projected box
  is larger than the frame in an axis or is cut by the near plane (`oversize`: a rail around the viewer, a 30 m crank wheel
  beside the catwalk) is judged by the centre of its in-frame part (`in_frame_centre`, `in_frame_pass`); `frame_pass` =
  `geometry_pass` or `in_frame_pass`. **Occlusion test (R1-29):** the still is rendered first with a depth grab (a QA render
  pass enqueued from `RenderPipelineManager.beginCameraRendering` blits URP's camera depth texture into an R32 target;
  linearised reversed-Z aware, `QaRenderer.Depth`), then an 8 x 8 grid of rays over the subject's in-frame rectangle is
  tested against the subject's box: a ray is visible when the scene's eye depth is not in front of the box entry
  (1 % + 5 cm tolerance). `visible_fraction` must be at least 0.30 (`visibility_ok`; `visible_centre` = centroid of the
  visible rays). Subject `pass` = `frame_pass` and `visibility_ok`. Without depth (`-lint-only`, or the grab failed)
  `visible_fraction` is "unknown" and only the geometry is judged. The shot's `depth` block reports the grab status and the
  median eye depth of the top and bottom tenth of the rows (orientation check: a raised view has its top rows farther).
  **Known spec conflicts:** a subject whose name ends with `" [spec-conflict]"` (each tag must have a logged arbitration in
  `DEVELOPMENT.md`; critique R1-41 found the KS4 tags unlogged. With the R1 KS4 pose and the in-frame rule the left crank
  wheel and cylinder B pass, and the lint notes that their tags should go) is waived when it fails. R2-36 (b): a conflict
  that DEVELOPMENT.md logs for a subject its beat names without the tag is listed in `CompositionLint.LoggedConflicts`
  (base tag, subject, DEVELOPMENT row; today: KS2b `truss`, ledge row "KS2b truss below") and gets the suffix in the
  report. A waived subject has `status`
  `pass_with_known_conflict`, `waived: true`, `pass: true`
  (`geometry_pass` keeps the plain verdict), shot status `pass_with_known_conflict` when nothing else fails. It is not a
  failure and not in `failed_shots`; the shot's `known_conflicts` and `lint_summary.known_conflicts` list it. A tagged
  subject that passes gets a note to remove the tag.
- `analysis` (test shots, computed in C#): seams `jump`, `neighbour_median`, `excess` (pass <= 0.02 luma); tiers mean luma
  difference in the centre region (facade <= 0.015, honeycomb <= 0.02; `inconclusive` when the two images are identical,
  i.e. the tier hook is not wired); parallax `mean_abs_diff_levels` of the sky band (move 10 m) and the shift-compensated
  difference (turn 2 degrees), pass <= 2 levels.

Python: `python -I Tools/qa/image_metrics.py QA/captures/r01` (reads `tag`, `file`, `width`, `height`, `beat`, `zone`,
`regions`, `thresholds`, `advisory`, `nopost`, `pose`, `clock`, `kind`), `contact_sheet.py QA/captures/r01 --metrics
QA/metrics/r01/metrics.json`, `compare.py QA/captures/r00 QA/captures/r01` (matches by `tag`).

## 2. Frame sequences: `-capture DIR -shots seq:NAMES [-res WxH] [-seq-seconds S] [-seq-fps F] [-seq-static]`

```
Abyss.exe ... -capture QA/sequences/r01 -shots seq:transitions
Abyss.exe ... -capture QA/sequences/r01 -shots seq:ride
Abyss.exe ... -capture QA/sequences/r01 -shots seq:grad -res 3840x2160
Abyss.exe ... -capture QA/sequences/r01 -shots seq:static_b2 -seq-seconds 20
```

`Time.captureDeltaTime = 1/fps` for the whole sequence (pre-roll included). One frame is rendered per game frame, after
all updates. Output `DIR/<name>/frame_00000.png ...`, `frames.csv`, `sequence.json`, plus `DIR/sequences.json`.
Capture mode disables the head bob (CameraRig), so walks show path, gaze, tread dip and hall vibration only.

| Name (group) | What | Default |
|---|---|---|
| `cue_bolt` (cues) | **listed first** (the bolt drops once per run; the t12 walks pass the release). Lead decision D3 (R2-09): the bolt is released from a cold lamp on the right-hand wall belt (about 300 m off the bridge axis) at beat-2 time 15 s, i.e. d = 60 + 15 x 1.5 = 82.5 (`QaSequenceCatalog.BoltReleaseD`). The AutoWalker walks from d 79.5 with its own pose and gaze (the beat-2 gaze time is d-based, so this is the cinematic look, including the 14-22 s pan right to the wall); frames.csv `cue_s` = fall seconds (from the frame where d first reaches 82.5 by walking); `sequence.json` `cue.cue_frames` lists fall seconds 0, 3, 6, 9 and 12 | 16 s at 20 fps, every 2nd written (10 fps) |
| `t12/t23/t34/t45/t67_walk` (transitions) | AutoWalker 1.5 m/s, 25 s centred on the 14.6 trigger (d 62, 215, 297.5, 432, 654) | 30 fps |
| `t12/.../t67_brisk` (transitions) | `QaPathDriver` 2.5 m/s, GazeCues gaze, 25 s centred (t45 stops at the deck centre) | 30 fps |
| `t12_ev_walk`, `t12_ev_brisk` (transitions) | R1-07: d 50 -> 75 at 1.5 / 2.5 m/s; frames.csv `region_*` = the clear doorway aperture of that frame, 10 % inside the jambs (R2-01), written only while d < 59.3 (from there the aperture would grow to the full frame over d 59.3-59.8); frames from d 59.3 on have no region and are judged on the whole frame as a second segment (`sequence_metrics.py` never takes luma_step or ev_rate across the change of region); judge `--judge spikes,luma_step,ev_rate --luma-region csv` (0.35 EV/s, 0.02 per 30 fps frame) | 30 fps |
| `t56_ride` (transitions) | aboard from ride second 96 at the walker's own deck stand (the -Z rail, `WalkPlanner.DeckStand`) with the ride gaze of that second (the D1 look down), so it sees what a run sees: the look down, the walk back to the centre at 109.5-120.5, ride seconds 110-120, arrival, gate at 121, AutoWalker walks off (fix round R2, player note: boarding at 100 on the deck centre swung the look 94 degrees in 2 s onto the deck) | 29 s, 30 fps |
| `tunnel_mouth` (transitions) | AutoWalker from z 40 through the mouth | 30 s, 30 fps |
| `ride` | AutoWalker boards; recording from departure, ride seconds 0..120 (rideU 0..1) | 120 s, 30 fps |
| `pistons` (machines) | player standing at KS4, clock from 2 s before a dead centre (`PORTAL_T + 10 + 15k`) | 20 s, 30 fps |
| `fan_frozen_platform` (machines) | KS5b pose, lift frozen at ride 75, clock running | 20 s, 30 fps |
| `fan_frozen_clock` (machines) | KS5b offset riding with the platform (ride 65-85), scene clock frozen | 20 s, 30 fps |
| `beam_sweep` (machines) | deck-centre eye looking at the fan, ride 50-100 (14.10 e) | 50 s, 30 fps |
| `static_b1..b7` (static) | static camera at KS1, KS2a, KS3, KS4, KS5a, KS6, KS7b, clock running | 100 s, 30 fps |
| `thin_b1..b7` (thin) | same poses, camera stepped 0.02 m right per frame, clock frozen | 30 frames |
| `grad_KS2a`, `grad_KS7b` (grad) | walking through the key pose (1.5 / 1.1 m/s), centred; `-seq-static` holds it | 10 s, 60 fps |
| `grad_KS5a` (grad) | riding through KS5a (ride 55-65) | 10 s, 60 fps |
| `lookback_d90`, `lookback_d380` (lookback) | walking away from the deactivations at 1.5 m/s looking straight back | 20 s, 30 fps |
| `cue_ignition` (cues) | R1-40 / R1-24 / R2-20: the player walks from z -2 through the ignition trigger onto the pier while the camera holds the KS7b pose; `cue_s` = seconds since the ignition (the first simulated frame that sees `_AbLampIgnite.x` set, at most 1/20 s after the trigger; the vector's value itself is the lattice's shifted start since R2-20), `warm_px` = warm pixels of the lower 66 %; `cue_frames` at ignition +0, +0.5, +1.5, +3, +4.5, +6, +12 and the door lamp at +12, +13.5, +14.5 s; judged (`cue.test`, report `cue_tests`): the on-screen sweep (warm-pixel growth from 2 % to 95 % of its final count within 12 s of the ignition) lasts at least 6 s | 26 s at 20 fps, every 2nd written (10 fps) |
| `cue_ending` (cues) | 14.11 / R2-05 / lead decision D2 (trigger at `AbyssConst.DOOR_TRIGGER_Z`): AutoWalker from z -62 to the door; **window** captures (post, dither and overlay UI: the end line); `cue_s` = seconds after the goal; `cue_frames` at goal +0.5, +1, +3 and +7 s (`goal_frames` as before); judged: warm pixels ((R - B) > 0.08, R > 0.12, R >= G >= B) of the lower half at goal +3 s at least 0.1 % of the frame (the amber sliver on the pier) | 24 s at 20 fps, every 2nd written (10 fps) |

Kinds: `WalkPinned` (the player walks with `QaPathDriver` so d-triggered events fire, while the camera holds a fixed pose).
`RecordEvery` N renders and writes every Nth simulated frame only (the walk keeps a 1/20 s step; at 4-5 fps steps the walk
runs at about half speed). `Window` captures the composed window (`ScreenCapture` after `WaitForEndOfFrame`).

`frames.csv`: `frame, file, scene_time, real_time, d, e, beat, lift_phase, ride_s, ride_u, cam_x, cam_y, cam_z, yaw,
pitch, roll, fov, head_dx_mm, head_dy_mm, head_dz_mm, lift_offset_mm, mean_luma, region_x0, region_y0, region_x1,
region_y1, goal_s, cue_s, warm_px, warm_pct` (mean luma = Rec. 709 of the sRGB readback, 0-1; `region_*` only for the
t12_ev sequences; `goal_s` = seconds since the goal once reached; `cue_s` = seconds since the cue reference of a cue
sequence (bolt release, ignition, goal); `warm_px` / `warm_pct` = warm pixels below the cue's row, cue sequences only). `lift_offset_mm` is `LiftController.MaxOffsetError` (14.7.1, <= 0.5 over the ride);
`head_dy_mm` is the camera head-motion offset (hall vibration: the piston impulse, 6 +/- 1 mm). `sequence.json` holds the
metadata and a `suggested` command line (`--fps` = the written frame rate).

Python: `python -I Tools/qa/sequence_metrics.py QA/sequences/r01/t12_walk --fps 30 --judge spikes,luma_step,ev_rate --plot`
(reads only the PNGs, natural order); static: `--judge spikes`; thin: `--judge std --std thin=x0,y0,x1,y1 --std-limit 6`;
ride: `--judge spikes,static --static deck=0.30,0.88,0.70,1.0`; encode test:
`python -I Tools/qa/encode_test.py QA/sequences/r01/grad_KS2a --fps 60 --beat 2`.

## 3. Autotest: `-autotest [-realtime] [-qa-out DIR] [-perf-log FILE] [-firstlast]`

```
Abyss.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile "<root>\QA\logs\autotest.log" -autotest
```

The AutoWalker (non-cinematic, 1.5 m/s; begins by itself with `-autotest`) walks from W0 to the door. Writes
`QA/autotest.json` 3 virtual seconds after the finish; exit 0 when all pass: finish 658.03 +/- 8 s (scene clock), beats
1-7 in `BeatDirector.Fired` once and in order and no backward beat step, ride start 308.36 +/- 1 s and end 428.36 +/- 1 s,
`LiftController.MaxOffsetError` <= 0.5 mm, zero Error/Exception/Assert messages (counted from before the first scene
load), camera never inside a Corridor collider (`Physics.CheckSphere` r 0.05 every frame after CameraRig), zero safety
returns. Also recorded: max camera roll, beat changes, first error messages, real seconds.

## 4. Bots: `-bot wallhug|raygrid|all [-bot-filter TEXT] [-bot-speeds walk|brisk|both] [-realtime]`

- `wallhug` -> `QA/bot_wallhug.json`: 84 runs (both edges of gallery, landing, bridge, ledge, apron, catwalk, dock apron,
  gantry, tunnel, balcony, pier, the balcony front edges, and every width change `x_*`) at 1.5 and 2.5 m/s, pushing 45
  degrees outward. Per run: pass, reason, max lateral vs limit (`HalfWidth - CAPSULE_R` + 6 cm), min feet minus floor
  (fail below -0.5 m), stalls (door frames: push inward for 1 s), safety returns, time, `inward_travel`.
  - **Narrowings** (`QaWallHugDriver`): the bot looks ahead along `PathGuide.HalfWidth`. When a step to a narrower
    half-width that the run must pass (before its end d: landing to bridge d 66, apron to catwalk d 329.54, balcony to pier
    d 742.54, i.e. the `x_landing_bridge`, `x_apron_catwalk`, `x_balcony_pier` runs) lies within 1.75 m ahead
    (pursuit 0.75 + lead 1.0; the balcony front wall stands 0.6 m before the pier step), the outward push becomes a
    pure-pursuit steer toward the new edge (`HalfWidth - CAPSULE_R - 0.10`): lateral command (target - lateral) / 0.75,
    clamped to 45 degrees. A bot that meets the wide region's transverse wall (hall wall, balcony front wall) presses into
    it and slides inward along it to the entrance, then presses outward again past the step. Region runs that end before
    a narrowing (`apron`, `balcony`, `landing`, `dock`) keep hugging their edge to the end; everywhere else the push is the
    plain 45 degrees outward.
  - **Stalls**: under 5 cm of progress in 0.75 s, where progress = d advance + `inward_travel` (lateral metres slid toward
    a narrowing target while outside it), so the slide along a transverse wall is not a stall but a bot that really cannot
    pass still stalls. More than 40 stalls or the timeout (length / (0.45 v) + 15 s) fail the run; the reason then carries
    the lateral, the limit and the narrowing being steered for, plus `fail_lateral` and `fail_narrowing_d`.
- `raygrid` -> `QA/bot_raygrid.json`: 0.25 m grid of downward rays over the whole path except the lift deck; judged where
  the capsule centre can reach (floor within 0.30 m of the true floor under each sample), edge band reported. The floor
  is level across the corridor except from W5 to W8 (d 216-297.54: the W5-W6 straight and the ledge arc), where it is the
  C1 collar surface as a function of the sample's own angle alpha around C1 (`AbyssConst.ARC_CENTRE`; alpha 0 at W6,
  toward -Z): y -3 for alpha <= 0 (the straight's pad side and centreline), the helicoid y = -3 - 4 alpha / pi across the
  whole annulus width for 0..pi, y -7 past pi (`QaBots.ExpectedFloorY`, `LedgeSurfaceY`); ceiling clamps in the gallery and tunnel
  (1.85 m .. visible height + 0.6 m; + stair drop over the stairs); sideways wall check (advisory).
  Every failing point is listed (up to 400 per list; the counts stay complete) so the owners can fix the colliders:

  ```json
  "down": {"failures": 9, "failures_listed": 9,
           "failing_points": [{"d": 216.05, "offset": -2.5, "side": "left", "region": "ledge", "x": -2.5, "y": -3, "z": 216.05,
                               "floor_y": -3, "hit_y": null, "dy": null, "reason": "no collider within 2 m below the floor"}],
           "failure_clusters": [{"region": "ledge", "count": 4, "no_collider": 4, "d_range": [216.05, 216.05],
                                 "offset_range": [-2.5, -1.75], "bounds_min": [-2.5, -3, 216.05], "bounds_max": [-1.75, -3, 216.05]}],
           "edge_band_miss_points": [...]},
  "ceiling": {"failing_points": [{..., "floor_y": 0, "hit_y": 3.4, "dy": 3.4, "expected_clearance": [1.85, 3.1], "collider": "..."}]},
  "sideways_advisory": {"gap_points": [{..., "hit_distance": null, "expected_distance": [2.95, 3.15]}]}
  ```

  `x, y, z` is the probe column on the expected floor (`y` = `floor_y` = the true floor under the sample:
  `PathGuide.FloorPoint(d).y`, on d 216-297.54 the collar surface at the sample's own angle; sideways: the
  expected wall point at 1 m height), `offset` metres right of the centreline, `hit_y` the y of the collider hit (`null` =
  nothing hit), `dy = hit_y - floor_y`, `collider` the name of the hit collider. `failure_clusters` groups adjacent
  judged misses (same region, neighbouring cells) into the world box to cover. The player log gets one warning line per
  cluster (first 8) and per ceiling miss (first 4).

## 5. Perf: `-perf-log FILE [-res WxH] [-cinematic] [-realtime]`

```
Abyss.exe -screen-fullscreen 1 -screen-width 1920 -screen-height 1080 -perf-log QA/perf/r01_1080.csv
Abyss.exe -screen-fullscreen 1 -perf-log QA/perf/r01_2160.csv -res 3840x2160
```

Full autowalk (VSync off, `targetFrameRate -1`), one sample per frame from the end of the load, allocation-free:
`FILE` (CSV: frame, scene_time, d, beat, dt_ms, cpu_ms, gpu_ms, cpu_main_ms, cpu_render_ms, draw_calls, setpass_calls,
batches, triangles, gc_alloc_bytes, system_mb, gfx_mb, after_fade, steady, heap_bytes, gc_collections) and `FILE_summary.json` (avg / 1 % low /
0.1 % low FPS, frame-time percentiles, max frame time and frames over 33 ms after the load fade, GPU / CPU averages,
render counters, GC in steady state, memory, per-beat breakdown, 11.7 targets and `pass`). `-res` larger than the window
renders the camera into a RenderTexture of that size. GPU timing needs Player Settings "Frame Timing Stats".
Exit 1 when a target is missed. `-perf-log` also records alongside `-autotest` and `-soak`.
**Zero GC in non-development players (fix round R2):** the ProfilerRecorder counter "GC Allocated In Frame" exists only in
development players (`gc_alloc_bytes` is -1 otherwise). Every frame therefore also records the managed heap
(`GC.GetTotalMemory(false)`, `heap_bytes`) and the gen-0 collection count (`GC.CollectionCount(0)`, `gc_collections`),
both allocation-free. `gc_steady.method` names the measure used: with the profiler counter, `pass` = no steady frame
allocates; without it, `pass` = no heap growth between consecutive steady frames (`heap_growth_frames` 0) and no gen-0
collection in the steady state (`gen0_collections` 0). The heap counter moves in allocator chunks, so small per-frame
garbage shows as occasional growth steps (`heap_growth_bytes`, `heap_growth_max_step_bytes`) or collections, not on every
frame. The soak (`-soak`) already samples the managed heap (`gc_heap_mb`) and does not use the profiler counter.

## 6. Soak: `-soak S [-cinematic] [-soak-virtual] [-soak-mem-tol-mb MB]`

```
Abyss.exe -screen-fullscreen 1 -logFile "<root>\QA\logs\soak.log" -soak 1800
```

Loops the walk in place (restart before the door trigger at d 798: lift to the top, player to W0, walker restarted) so
the scene clock runs for the whole soak. `QA/soak.json`: memory samples every 10 s and growth after 60 s (limit
max(32 MB, 5 %)), clock drift (scene clock vs a compensated sum of the applied deltas; piston mod 120 s and fan mod 40 s
phases; limit 1 ms; float-accumulator drift and the `_AbTime` float quantum reported), log errors, camera, safety returns.

## 7. First and last frames: `-firstlast [DIR]` (Part 10 item 22)

With `-autotest` (or `-quit-after S`): window captures (post and UI included) of the first 60 frames from start-up,
the first 60 frames after the load (title fade-in) and the last 60 frames before the quit, in
`DIR/first`, `DIR/fadein`, `DIR/last` (default `QA/firstlast/<stamp>`), each a sequence for `sequence_metrics.py`, plus
the title card at 2 s and 4 s of the title timeline (`DIR/title/title_2s.png`, `title_4s.png`; R1-40).

## Hooks the owners add (one line each)

| Hook | Consumer | Line |
|---|---|---|
| `QaHooks.TierOverride` (-1 off, 1 near only, 0 far only) | `DetailTier.Update` | `if (QaHooks.TierOverride >= 0f) c = QaHooks.TierOverride;` before applying `_AbFade` |
| same | Beat 7 honeycomb tier-1 / tier-2 cross-fade | force the tier-2 fade weight to `1 - TierOverride` |
| Frame Timing Stats | build script | `PlayerSettings.enableFrameTimingStats = true` |
