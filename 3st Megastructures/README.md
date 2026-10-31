# Thread

A first-person "corridor" walk up a space elevator, in flat-colour minimalism. You cross a white salt flat toward a hair-thin line on the horizon, pass an avenue of faceless colossi, climb a staircase into a mist bank, and step out onto a terrace where the "thread" turns out to be a cable 100 m across. A spiral walkway climbs the cable while the land drops away, the sky turns black and a sea of clouds opens below; at the top a giant planet hangs overhead, the curved Earth lies far below, and a small beacon waits.

Everything is procedural: C# geometry generated at load, handwritten URP HLSL shaders, code-generated textures (blue noise) and synthesised audio. No imported assets.

- Engine: Unity 6000.6.0f1, URP 17.6.0 (Forward+, HDR 64-bit, MSAA 4x), Input System 1.20.0.
- Walk time: 9 min 33 s at the default speed (860 m at 1.5 m/s); 5 min 44 s brisk.

## Run
**Players:** see [HOW_TO_RUN.md](HOW_TO_RUN.md) (English and Russian). Windows: double-click `PLAY_WINDOWS.bat`; Mac: right-click `PLAY_MAC.command` > Open.

The built games are `Game/Windows/Thread.exe` and `Game/Mac/Thread.app` in the published copy (`Builds/Thread` and `Builds/Mac` after a local build). Rebuild them with (`BuildMac` for the Mac app):

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows
```

## Controls
| Input | Action |
|---|---|
| W A S D / left stick | walk |
| Mouse / right stick | look |
| Shift / shoulder button | hurry (2.5 m/s) |
| C | cinematic autowalk on/off (any input cancels it) |
| Esc | pause (Resume, Restart, Quit) |
| Alt+Enter, F11 | fullscreen / windowed |

## Recording the video
Start the cinematic autowalk from the command line; it follows the gaze cues of every beat and slows down at the reveals (about 10 min 20 s):

```bash
"Builds/Thread/Thread.exe" -cinematic
```

Record at 3840x2160 / 60 fps if the display allows (the game renders at the window resolution; `-screen-width 3840 -screen-height 2160 -screen-fullscreen 1`). Upload at 1440p or higher to keep YouTube from re-banding the gradients.

## Command-line flags
| Flag | Meaning |
|---|---|
| `-cinematic` | start the cinematic autowalk |
| `-beat N` / `-progress X` | start at beat N (1..7) or at plan distance X metres |
| `-nofade` | skip the title fade |
| `-res WxH` | window size |
| `-seed N` | seed of the procedural audio and camera sway |
| `-autotest [-brisk]` | QA: walk start to goal in virtual time, assert, exit code |
| `-wallbot` | QA: wall-hugging bot along both corridor edges |
| `-soak S` | QA: soak test for S seconds |
| `-capture <dir> -capture-set <keyshots,beats,sequences,encode,parity>` | QA: deterministic PNG captures + `report.json` |
| `-freeze-dither`, `-freeze-ambient` | QA: static dither tile / frozen ambient clock |
| `-perf-log <file>` | QA: frame-time CSV with VSync off |
| `-audio-dump <file>` | QA: dump the real audio output to WAV |
| `-quit-after S` | quit after S seconds |

## The seven beats
| # | Beat | Time at 1.5 m/s |
|---|---|---|
| 1 | Salt flat — the hair-thin line | 0:00 – 1:00 |
| 2 | Avenue of colossi | 1:00 – 2:40 |
| 3 | Stairs into haze | 2:40 – 3:33 |
| 4 | The thread is a tower | 3:33 – 4:27 |
| 5 | Spiral ascent | 4:27 – 6:27 |
| 6 | Cloud sea | 6:27 – 8:20 |
| 7 | The top | 8:20 – 9:33, then the ending (14 s) |

## Repository
- `Assets/_Game/` — scripts (Runtime, Editor, Tests), shaders, beats (`Beats/BeatNN_*` builders), generated materials and settings.
- `Tools/` — Unity queue, player runner, fast compile, HLSL generator, metrics, QA analysis (`Tools/qa`).
- `QA/` — reports, critiques, final 4K key shots (`QA/final`).
- `docs/RESEARCH.md` — research log; `DEVELOPMENT.md` — architecture and decisions; `STATE.md` — progress; `FEATURE_MATRIX.md` — beat-by-beat status.

## Space elevator facts (for the video description)
See `docs/research/R7_art_science.md`. The game's dimensions are stylised, not engineering values.
