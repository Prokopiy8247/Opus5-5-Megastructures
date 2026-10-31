# The Ring

> **▶ Как запустить / How to run:** [HOW_TO_RUN.md](HOW_TO_RUN.md) — Windows: `PLAY_WINDOWS.bat`, macOS: `PLAY_MAC.command` (RU / EN).


A quiet first-person walk across the inside of a ringworld, where the land does not fall away to a horizon but curves up into the
sky. You leave a small docking capsule, cross a valley and a river bridge under the far side of the ring, watch a colossal ark ship
drag its shadow over the valley, see evening arrive as giant shadow squares cross the star, walk along the foot of a smooth rim wall
that rises beyond sight, pass through a tunnel in that wall and step out onto a platform facing a star inside a glittering Dyson swarm.

Everything is procedural: C# geometry, handwritten URP HLSL shaders, code-generated audio. No imported assets.
Built with Unity 6000.6.0f1, URP 17.6.0, Input System 1.20.0.

## Run
`PLAY_WINDOWS.bat` (Windows 10/11, DX12; the game is in `Game\Windows`) or `PLAY_MAC.command` (macOS, Intel and Apple silicon; `Game/Mac/The Ring.app`). Step by step: [HOW_TO_RUN.md](HOW_TO_RUN.md). The walk takes about 10:40 at the default speed (about 11:30 in cinematic autowalk).

## Controls
| Input | Action |
|---|---|
| W A S D / arrows, left stick | walk |
| Mouse, right stick | look |
| Shift, right shoulder | brisk walk (2.5 m/s instead of 1.5 m/s) |
| C | toggle the cinematic autowalk (any input cancels it) |
| Esc, Start | pause (Resume, Restart, Quit) |
| F11 or Alt+Enter | toggle fullscreen |

## Recording the video
Start `"The Ring.exe" -cinematic` (or press C at any time): the camera walks the path on its own, slows down at the reveals, holds the
eight key views (49 s in total) and frames each beat with a scripted gaze. Record the window at 60 fps; the game runs at the monitor refresh
rate with VSync. For 4K stills see `QA/final/`.

## Command-line flags
| Flag | Meaning |
|---|---|
| `-cinematic` | start in the cinematic autowalk |
| `-beat N` / `-progress X` | start at beat N (1..7) or at progress X metres along the path |
| `-nofade` | skip the title fade |
| `-res WxH` | window resolution |
| `-autotest` | QA: autowalk start to goal at 1.5 m/s, asserts, exit code 0 on success |
| `-walltest` | QA: wall-hugging bot along both lane edges |
| `-capture DIR -shots all|keys|stills|KS02,KS06` | QA: deterministic captures through RenderTextures, then quit |
| `-sequence NAME` | QA: 30/60 fps virtual-time frame sequence |
| `-perf-log FILE` / `-perf` | QA: performance log (VSync off) |
| `-soak S` | QA: soak test for S seconds |
| `-seed N`, `-quit-after S`, `-set N`, `-audio-dump FILE` | determinism and debug helpers |

## Building from source
All scenes, settings, materials and generated shader includes are produced by editor scripts:
```
powershell -NoProfile -File Tools/unity-queue.ps1 -Name rebuild -Method Megastructure.EditorTools.BatchTools.RebuildAll
powershell -NoProfile -File Tools/unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows
```
Menu equivalents: `Tools/Megastructure/Rebuild All`, `Tools/Megastructure/Build Windows`. See `DEVELOPMENT.md` for architecture and QA commands.
