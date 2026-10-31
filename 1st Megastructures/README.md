# Abyss

> **▶ Играть / Play:** Windows — `PLAY_WINDOWS.bat`, Mac — `PLAY_MAC.command`.
> Пошаговая инструкция / step-by-step guide: **[HOW_TO_RUN.md](HOW_TO_RUN.md)** (русский + English).

Игра целиком сделана нейросетью Claude Opus 5.5 по одному промпту (`MASTER_PROMPT.md`).
The whole game was made by Claude Opus 5.5 from a single prompt (`MASTER_PROMPT.md`).

A first-person walk down through a vertical brutalist megastructure: a railless bridge over a bottomless shaft, giant columns,
slow pistons the size of buildings, a house-sized lift past a vertical city, a cyclopean fan hurling beams of light, and a final
balcony from which the shaft is revealed to be one of thousands of identical shafts of a honeycomb that reaches the horizon.
About 11 minutes. No combat, no HUD — made to be recorded.

Everything is procedural (C# geometry, handwritten URP HLSL shaders, code-generated textures and audio). Unity 6000.6.0f1, URP 17.6.

## Run
Ready-made builds are in `Game/`: `Game/Windows/Abyss.exe` (Windows 10/11, 64-bit) and `Game/Mac/Abyss.app` (macOS 11+, Intel and
Apple silicon); the `PLAY_*` files next to this README start them. The game starts black, fades in with the title, and ends on
black after the goal door. A build from source goes to `Builds/` (see below).

## Controls
| Input | Action |
|---|---|
| W A S D / left stick | walk |
| Mouse / right stick | look |
| Shift / shoulder button | brisk walk |
| C | toggle the cinematic autowalk (any input cancels it) |
| Esc | pause overlay (Resume, Restart, Quit) |
| Alt+Enter or F11 | toggle fullscreen |

## Recording the video
Start `Abyss.exe -cinematic` (optionally `-res 3840x2160`) and record the window: the autowalk follows the staged gaze cues of every
beat, slows down at the reveals and holds on the balcony. For a perfectly synchronous soundtrack render the score offline:
`Abyss.exe -render-audio score.wav` (48 kHz float WAV of the whole nominal walk).

## Command-line flags
| Flag | Effect |
|---|---|
| `-cinematic` | start the cinematic autowalk |
| `-autotest` | QA: autowalk start to goal, assert timing/beats/errors, exit 0/1 |
| `-beat N` / `-d D` / `-progress X` | start at beat N / path distance D metres / fraction X of the path |
| `-res WxH` | window resolution |
| `-seed N` | deterministic seed |
| `-nofade` | skip the title fade |
| `-capture <dir> -shots <list>` | QA captures (see `docs/QA_RUNTIME.md`) |
| `-perf-log <file>`, `-soak S`, `-bot wallhug|raygrid` | QA performance, soak and collider bots |
| `-freeze-clock`, `-clock T`, `-tier full|simplified|graybox`, `-nopost`, `-dither static|temporal|off`, `-dither-amp LSB` | QA and look-development switches |
| `-render-audio <wav>`, `-audio-dump <wav> <s>` | offline score render / dump of the live audio callback |
| `-quit-after S` | quit after S seconds |

## Rebuild from source
Open the project in Unity 6000.6.0f1 and run `Tools > Megastructure > Rebuild All`, or in batch:
```
powershell -NoProfile -File Tools\unity-queue.ps1 -Name rebuild -Method Megastructure.EditorTools.BatchTools.RebuildAll
powershell -NoProfile -File Tools\unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows
powershell -NoProfile -File Tools\unity-queue.ps1 -Name build_mac -Method Megastructure.EditorTools.BatchTools.BuildMac
```
Engineering notes: `DEVELOPMENT.md`; integration contracts: `docs/CONTRACTS.md`; QA: `QA/` and `docs/QA_*.md`.
