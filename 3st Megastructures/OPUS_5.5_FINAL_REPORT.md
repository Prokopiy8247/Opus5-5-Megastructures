# OPUS 5.5 FINAL REPORT — Thread

## What was built
**Thread** is a first-person walk up a space elevator in flat-colour minimalism. It is built for Unity 6000.6.0f1 with URP 17.6 (Forward+, HDR 64-bit, MSAA 4x). The walk is 860 m long: 9 min 33 s at 1.5 m/s, or about 10.3 min in the cinematic autowalk. It has seven beats:

1. a salt flat with a hair-thin line on the horizon;
2. an avenue of eight faceless colossi;
3. stairs into a mist bank;
4. the reveal that the "thread" is a cable 100 m across;
5. a spiral ascent while the land sinks;
6. a sea of clouds under a black sky with the cable's shadow;
7. the top: a banded planet, the curved Earth and a small beacon.

Everything is procedural: C# geometry generated at load, handwritten HLSL shaders (ThreadFlat, ThreadLine, ThreadEmissive, ThreadSky, ThreadGrade), code-generated blue noise and synthesised audio. No external assets are used.

- Build: `Builds/Thread/Thread.exe` (Windows x64, Direct3D 11, about 98 MB).
- Cinematic recording: `Builds/Thread/Thread.exe -cinematic` (see README.md).
- Architecture, commands and the 82 logged decisions (D-001..D-082) are in DEVELOPMENT.md. The beat-by-beat status is in FEATURE_MATRIX.md, and the research log is in docs/RESEARCH.md.

## How it was built
- **Phases 0-2:** preflight, research (8 notes), core contracts, then 12 parallel builders. Each owned its own files; the lead integrated their work.
- **QA loop:**
  - Round 1 (run06): 19 S1 / 42 S2.
  - Fix wave 2 (D-035..D-048), then round 2 (run07): 6 S1 / 39 S2.
  - Beat-3 fixes (D-049..D-053), then fix wave 3 (D-054..D-082). Wave 3 had five fixers: sky, atmosphere, materials, colossi and cinematography. Fixers verified their work offline with mocks and harnesses; the lead rebuilt, captured and tested in Unity.
- **Final build:** fix wave 3, key shots `QA/captures/run10`, 4K finals in `QA/final/`.

## QA evidence (final build unless noted)
| Check | Result | Evidence |
|---|---|---|
| Unity Validate | 37 checks, 0 problems | Logs/w3_Validate.log |
| Player build | succeeded, 0 errors | Logs/w3_BuildWindows2.log |
| EditMode tests | 218 / 218 pass (two expectations moved to D-053 / D-056 first) | QA/reports/tests_final.xml |
| Soak test | PASS: 1800 s real time, 3 full loops, memory growth 0.15 MB managed / 3.31 MB total, 0 errors | QA/reports/soak.json |
| Autotest walk | PASS: 574.75 s (573.3 +- 8), beats 1-7 in order, goal once, 0 errors, corridor coverage 21,388 rays / 0 misses | QA/reports/autotest_walk.json |
| Autotest brisk | PASS: 344.95 s (344 +- 5), 0 errors | QA/reports/autotest_brisk.json |
| Wall bot | PASS: 0.0 mm excess on both edges, 0 errors | QA/reports/wallbot_walk.json |
| Key shots | 80 stills (16:9, 16:10, 21:9, 4K), 0 capture failures | QA/captures/run10, QA/final |
| Performance 1080p (RTX 5070) | 526 FPS avg, 1% low 215, 0.1% low 131, max frame 9.6 ms, 0 frames over 33 ms (target 90 / 60 / 33 ms); GPU 1.63 ms avg | QA/reports/perf_1080.summary.json |
| Performance 4K (earlier build) | 517 FPS avg offscreen, max 16.8 ms | QA/reports/perf_4k.summary.json |
| Transitions, statics, pans (earlier build) | all 14.6 transitions pass the luma-step and spike checks | QA/reports/sequences_*.md |
| Audio (earlier build) | offline T-AUDIO passes; the player dump matches within 0.6 dB | QA/audio |
| Encode test (earlier build) | no step edges added after H.264/VP9; residual sub-criteria fail at low bitrates | QA/reports/encode_summary.md |

## Known issues (honest)
- **No third critic round.** The user asked to finish quickly, so no critic round ran after fix wave 3. Round 2 (QA/critique-run07.md) left 6 S1 and 39 S2. Fix wave 3 addresses all of them in code, and the final key shots show the intended changes. An independent panel has not re-verified them, so Definition of Done item 2 ("zero S1/S2 in the last round") is **not formally met**.
- **KF2b** (looking up the avenue): the pair-2 faces now read, but the lower half of the frame is still a pale bank wash.
- **Beat 5:** the terrace target in KF5a/KF5c is small from the helix (D-070). KF5d cannot show the stair flight (D-071).
- **Beat 7:** "the thin thread meets the Earth" cannot be seen from any walkable pose (D-074). The planet in KF7a is partly behind the support arms.
- **Not re-run on the final build:** frame sequences and the encode test. Their results are from builds before fix wave 3.
- **Over the triangle budget:** 14 k triangles per colossus against 11 k in the spec (D-057). Performance still has a large margin.
- **Fix-wave-3 shaders are not GPU-profiled:** the sky shader costs about 40 % more on sea pixels (D-076).

## Recording the video
Run `Builds/Thread/Thread.exe -cinematic -screen-width 3840 -screen-height 2160 -screen-fullscreen 1` and record at 60 fps. Upload at 1440p or higher so YouTube does not re-band the gradients.

## Project metrics
| Metric | Value | How obtained |
|---|---|---|
| Transcript folders | 1 | measured: glob *3rd-Megastructures* under ~/.claude/projects, confirmed by cwd field |
| Transcript files (main / subagent / workflow) | 2 / 7 / 51 | measured: *.jsonl files found recursively |
| Assistant messages (deduplicated) | 4,403 | measured: unique message.id with usage |
| Models | <synthetic> (14), claude-opus-5-5 (4389) | measured: message.model |
| Uncached input tokens | 8,932 | measured: sum usage.input_tokens |
| Cache read tokens | 1,180,865,437 | measured: sum usage.cache_read_input_tokens |
| Cache write tokens (5 min / 1 h) | 23,914,513 (21,539,315 / 2,375,198) | measured: usage.cache_creation_input_tokens and its ephemeral split |
| Output tokens (incl. thinking) | 7,531,313 | measured: sum usage.output_tokens |
| Total tokens | 1,212,320,195 | measured: sum of the four categories |
| Wall-clock time | 22:06:17 | measured: first 2026-10-29T19:15:02.902000+00:00 to last 2026-10-30T17:21:20.393000+00:00 transcript timestamp |
| Idle gaps > 10 min | 6 gaps, 14:11:17 | measured: consecutive timestamps further apart than 600 s |
| Active time | 7:55:01 | derived: wall-clock minus all gaps > 10 min |
| Cost: uncached input | $0.04 | estimated: tokens x $4.0/MTok |
| Cost: cache read | $236.17 | estimated: tokens x $0.2/MTok |
| Cost: cache write 5 min | $107.70 | estimated: tokens x $5.0/MTok |
| Cost: cache write 1 h | $19.00 | estimated: tokens x $8.0/MTok |
| Cost: output | $150.63 | estimated: tokens x $20.0/MTok |
| Total API-equivalent cost | $513.53 | estimated: sum of categories; prices from https://platform.claude.com/docs/en/about-claude/pricing accessed 2026-10-30 for claude-opus-5-5 |

Gaps longer than 10 minutes (UTC):
- 2026-10-29T19:15:04.181000+00:00 -> 2026-10-29T22:01:08.735000+00:00 (2:46:05)
- 2026-10-29T23:45:13.771000+00:00 -> 2026-10-30T01:41:35.490000+00:00 (1:56:22)
- 2026-10-30T05:41:35.142000+00:00 -> 2026-10-30T13:49:09.604000+00:00 (8:07:34)
- 2026-10-30T14:05:41.070000+00:00 -> 2026-10-30T14:41:09.820000+00:00 (0:35:29)
- 2026-10-30T16:30:52.754000+00:00 -> 2026-10-30T16:47:15.161000+00:00 (0:16:22)
- 2026-10-30T16:49:04.070000+00:00 -> 2026-10-30T17:18:29.138000+00:00 (0:29:25)

Measured with `python -I Tools/metrics.py --json QA/reports/metrics.json --md QA/reports/metrics.md`. It covers both sessions (the first one ended on a usage limit; this session continued on a new account), their subagents and the workflow agents. All of them are confirmed by the `cwd` field.

Notes:
- The two last "idle" gaps (16:30-16:47 and 16:49-17:18 UTC) were the unattended perf, test and 30-min soak runs, so the real working time is somewhat above the active time shown.
- Not counted: the tokens of this final measurement and of the final chat message.
- The cost is an API-equivalent estimate at claude-opus-5-5 list prices, not a subscription bill.
