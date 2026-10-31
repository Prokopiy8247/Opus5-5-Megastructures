# OPUS 5.5 FINAL REPORT — The Ring

## What was built
**The Ring** is a first-person walk across the inside of a ringworld (R = 16 km), built entirely from code in Unity 6000.6 / URP 17.6:
procedural C# geometry, handwritten HLSL shaders, code-generated audio, no imported assets. The 960 m walk (about 10:40 at 1.5 m/s,
about 11:30 in the cinematic autowalk) runs through seven beats:

| Beat | s (m) | Time at 1.5 m/s | Content |
|---|---|---|---|
| 1 Capsule | 0-24 | 0:00-0:16 | white ceramic docking capsule, porthole view, sliding hatch, gangway |
| 2 Valley and bridge | 24-276 | 0:16-3:04 | the land curves up into the sky; the far side of the ring hangs overhead (money shot 1) |
| 3 The ark | 276-411 | 3:04-4:34 | a 3 km ship crosses the sky and drags its shadow over the valley (money shot 2) |
| 4 Sunset, night, underpass | 411-594 | 4:34-6:36 | shadow squares eclipse the star, night falls, the lit arch and city lights overhead (money shot 3) |
| 5 Rim wall | 594-774 | 6:36-8:36 | a smooth wall rising 12 km, a tiny door |
| 6 Wall tunnel | 774-894 | 8:36-9:56 | dark tube, one growing point of light |
| 7 Outside platform | 894-960 | 9:56-10:40 | a pier into space, the hull 200 m below, a star inside a 24 000-panel Dyson swarm, the beacon (money shot 4) |

Then a 4 s fade to black and the line "Someone built all of this to be home."

## How to run and record
- Run `Builds\The Ring\The Ring.exe` (Windows 10/11, DX12).
- Controls: WASD / arrows / left stick walk; mouse / right stick look; Shift / right shoulder brisk; **C** toggles the cinematic
  autowalk; Esc pause (Resume, Restart, Quit); F11 or Alt+Enter fullscreen.
- **Recording:** start `"The Ring.exe" -cinematic` (or press C at any time). The camera walks the path itself, slows at the reveals,
  holds the eight key views (49 s in total) and frames each beat. Record at 60 fps.
- Useful flags: `-beat N`, `-progress X`, `-nofade`, `-res WxH`, `-autotest`, `-capture DIR -shots keys`, `-walltest`, `-perf-log FILE`,
  `-soak S`, `-seed N` (full list in `README.md`).
- Final 4K key shots for thumbnails: `QA/final/` (16 key shots at 3840x2160 plus a 1080p contact sheet).

## Session history
- Session 1 (2026-10-29/30, commits 240e261..f1ae569): research sprint, core contracts, 11-module builder wave, first full pass,
  first player build, autotest passing, run04 captures and a 3-lens critic panel. Ended by a usage limit.
- Session 2 (2026-10-30, this report): read the spec and the state, addressed the run04 S1/S2 findings with four parallel beat
  builders (B12 capsule/valley, B34 ark/levee, B56 wall/tunnel, B7 platform/swarm) and a lead pass on the shared atmosphere,
  then finished early at the user's request because of their remaining usage budget.

## Main changes in session 2 (details: `DEVELOPMENT.md` ADR-009..013, `FEATURE_MATRIX.md`)
- **Money shot 1 (ADR-012):** lighter valley fog (sigma 1e-4 instead of 2.5e-4) and a haze-layer arch model, so the land visibly climbs
  from the horizon into the sky instead of disappearing into a 13-degree haze band; one lighting model for the far terrain and the
  arch (no seam at the clipmap edge, also at dusk); aerial desaturation by distance; far-side geography reworked (rectilinear farm
  districts, forests, lakes, rivers, towns and villages on jittered grids instead of a road graticule); no trench along the path.
- **Money shot 2:** fog thins with height, the ark now reads as a dark hull (luma 90 vs 162 before) with rim light, spars, marker
  lights, window band and engine glow; the squares no longer fade in during the ark's key shot.
- **Money shot 3:** opaque dark shadow squares; the sky light dims with the eclipse so the sunset reads as a sunset; the night side
  shows glittering clusters of warm and cool city lights instead of a lattice of lines; the lit arch is no longer blown out;
  levee with stone tiers, turf, stair lights and warm lamps; warm bollards (night white balance halved, ADR-013).
- **Beat 5:** the rim wall reads as a colossal built surface (100 m joint grid, foot-to-top gradient, panel tones, warm door glow).
- **Beat 6:** tunnel section markers and a light ladder toward the mouth; the outside deck is sunlit through the mouth.
- **Money shot 4 (ADR-011):** the deck is a 12 m pier and the hull lies 200 m below it, so the underside of the world is visible beyond
  the rails (with the spec's 30 m deck and 600 m drop it was geometrically invisible); swarm reworked into broad soft-point bands with
  glints; structured outer face.
- Tooling: `Tools/run-player.ps1` (player runs under the same mutex as Unity batch calls), `docs/BUILDER_BRIEF.md`, `docs/CRITIC_BRIEF.md`.

## QA evidence (what was actually run)
| Check | Result | Where |
|---|---|---|
| Player build | succeeded, 0 errors, 99.6 MB | `Logs/lead-build06.log` (not in git) |
| Key-shot captures, all 16 shots at 1080p, 4K, 16:10, 21:9 | all render, no magenta, no black frames, clipped <= 0.2 % | `QA/final/`, run `final01` |
| Autotest in the final player build (start to goal at 1.5 m/s, seed 1) | **pass**: goal at 636.74 s (spec 636.8 +- 3), every beat once and in order, 0 log errors, roll 0, exit code 0 | `QA/autotest.json` |
| EditMode tests | session 1: 104/107 passed (1 mesh-validation failure, fixed in `MeshKit` in session 2, not re-run) | `QA/tests/` |
| Offline audio analysis | session 1 renders and trims | `QA/audio/`, `QA/audio-trims.json` |
| Per-builder captures and metrics | 1080p captures per beat with `metrics.md` | `QA/captures/b12-02`, `b34-02`, `b56-02`, `b7-02` (not in git) |

## Known limitations (honest)
- Session 2 ended early at the user's request, so the full verification programme of Parts 11 and 15.1 was **not** completed:
  no full 5-lens critic panel after the fixes, no 30 fps frame-sequence run, no performance log, no 30-minute soak, no encode test,
  no wall-hugging bot run, no EditMode re-run in this session. The harness for all of them exists (`-sequence`, `-perf-log`, `-soak`,
  `-walltest`, `Tools/encode_test.py`, `Tools/sequence_metrics.py`).
- Open visual items: the pavilion P2 in KS05 is lit white instead of a dark silhouette; KS15 and KS08/KS09 sit slightly above the
  beat mean-luma limits; the capsule floor stays neutral grey; the KS01 porthole view is not blue-dominant; the swarm partly reads as
  grey speckle at 1080p (better at 4K); city lights read whiter than CityWarm; the swarm panels use coverage blending instead of
  strictly opaque rendering (H6 wording).
- Deliberate deviations from the spec are recorded as ADR-007..013 in `DEVELOPMENT.md` (far plane in the tunnel, swarm remap, swap at
  s 572, shadow distance, 12 m pier and 200 m hull drop, lighter fog and haze model, night grade).

## Project metrics
<!-- METRICS:BEGIN -->
| Metric | Value | How obtained |
|---|---|---|
| Wall-clock time since the run anchor | 18:39:41 | measured: 'Run started at' in STATE.md (2026-10-29T23:01:18+01:00) to the last transcript timestamp |
| Active time since the run anchor | 6:27:50 | measured: minus 4 gaps > 10 min after the anchor |
| Wall-clock time (all records) | 21:26:51 | measured: first to last transcript timestamp (2026-10-29 19:14:07 -> 2026-10-30 16:40:58 UTC) |
| Active time (all records) | 6:28:07 | measured: wall-clock minus 5 gaps > 10 min without any transcript record (14:58:44); 30 records predate the anchor (session opened earlier, request interrupted) |
| Uncached input tokens | 9,194 | measured: sum of usage.input_tokens |
| Cache read tokens | 1,110,708,467 | measured: sum of usage.cache_read_input_tokens |
| Cache write tokens (5 min) | 32,314,004 | measured: usage.cache_creation.ephemeral_5m_input_tokens (+ unsplit writes) |
| Cache write tokens (1 h) | 2,988,050 | measured: usage.cache_creation.ephemeral_1h_input_tokens |
| Output tokens (incl. thinking) | 9,143,070 | measured: sum of usage.output_tokens |
| **Total tokens** | **1,155,162,785** | measured: sum of the categories above |
| Sessions / subagent transcripts / messages | 2 / 70 / 4558 | measured: 72 transcript files, assistant messages deduplicated by message id |
| Models | <synthetic> (18), claude-opus-5-5 (4540) | measured: message.model |
| Cost: uncached input | $0.04 | 9194 x $4.00 / MTok |
| Cost: cache read | $222.14 | 1110708467 x $0.20 / MTok |
| Cost: cache write 5 min | $161.57 | 32314004 x $5.00 / MTok |
| Cost: cache write 1 h | $23.90 | 2988050 x $8.00 / MTok |
| Cost: output (incl. thinking) | $182.86 | 9143070 x $20.00 / MTok |
| **Total API-equivalent cost** | **$590.51** | sum of the five lines; prices for claude-opus-5-5 (Claude Opus 5.5) from https://platform.claude.com/docs/en/about-claude/pricing (accessed 2026-10-29); standard tier: transcripts show inference_geo global and speed standard; long context is billed at standard rates for Opus 5.5 (pricing page, 'Long context pricing'); fast mode and US-only (1.1x) not used |

Gaps > 10 min:
- 2026-10-29 19:14:08 -> 22:01:01 UTC: 2:46:53
- 2026-10-29 23:41:55 -> 03:04:39 UTC: 3:22:44
- 2026-10-30 05:41:21 -> 13:49:17 UTC: 8:07:55
- 2026-10-30 14:09:09 -> 14:40:19 UTC: 0:31:10
- 2026-10-30 16:30:37 -> 16:40:39 UTC: 0:10:02
<!-- METRICS:END -->
