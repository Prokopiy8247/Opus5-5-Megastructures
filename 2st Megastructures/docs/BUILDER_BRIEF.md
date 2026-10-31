# Builder brief (production wave 2, 2026-10-30)

Common rules for every beat builder of The Ring. Your own prompt names your beat, your files and your tasks.

## Project
- Unity 6000.6.0f1, URP 17.6, procedural first-person walk across a ringworld. Root:
  `W:\YouTube\Megastructures\2nd Megastructures\2nd Megastructures` (the path has spaces: quote it everywhere).
- Spec: `MASTER_PROMPT.md`. Read Part 14.0 (digest), 14.2 (look bible: palette, lighting rig, fog, post, materials), your
  beat's section of 14.5, the relevant rows of 14.10 (tests, thresholds) and 14.12 (constants). Part 10 is the defect list.
- Engineering notes: `DEVELOPMENT.md` (architecture, global uniform contract, colour rule: palette values are linear,
  plain Color properties are gamma-encoded by `MaterialFactory`, ADRs). Constants: `Assets/_Game/Scripts/Runtime/Core/RingConstants.cs`.
- Shared shader includes (read-only for you): `Assets/_Game/Shaders/Include/` (`RingLitPass.hlsl` = the standard lit pass
  structure with hooks, `RingLighting.hlsl`, `RingAtmosphere.hlsl` (RingFog), `RingBend.hlsl`, `RingCommon.hlsl` (hashes,
  filtered lines/grids), generated `RingPalette.hlsl` (`PAL_<Token>`, linear)).
- First-pass captures of every beat: `QA/captures/run04/` (key shots `ksNN_*.png`, beat stills `beatNN_*`).
  Critic findings of that run: `QA/critique-run04-raw.json` (three critics; findings carry `beat`, `severity`, text, evidence, fix).

## Ownership and concurrency
- Edit ONLY the files your prompt lists. Read anything. Never edit `Shaders/Include/**`, `Scripts/Runtime/Core/**`,
  `Scripts/Runtime/Directors/**`, `Scripts/Runtime/Player/**`, `Scripts/Editor/Batch/**`, scenes, `ProjectSettings/**`,
  `RingConstants`, or another builder's files. If you need a shared change, put the exact code in your final report.
- Other builders work on other beats and the lead is changing the shared fog, sky, arch, ground colours, terrain height near
  the path, lights, white balance and exposure at the same time. Light levels may shift between your builds: tune your
  materials for the spec's albedos and recipes and for relative contrast inside your subject; report the numbers you measure.
- Never commit to git. Never run Unity or the player except through the two scripts below (they share one machine-wide
  mutex, so builds and player runs of all agents are serialised; a call may wait for others).

## Loop
1. Edit. Run `python -I Tools/fastcheck.py all` (C# compile check, about 6 s) until it prints OK for runtime and editor.
2. Only if you changed a `MaterialDefs` file (new material, new property, new default): rebuild assets
   `powershell -NoProfile -File Tools/unity-queue.ps1 -Name <you>-rebuildNN -Method Megastructure.EditorTools.BatchTools.RebuildAll`
3. Build the player: `powershell -NoProfile -File Tools/unity-queue.ps1 -Name <you>-buildNN -Method Megastructure.EditorTools.BatchTools.BuildWindows`
   (2-3 min plus queue time). Read the printed digest. Shader problems: `grep -n "Shader error\|error:" Logs/<you>-buildNN.log`.
   If the build fails in a file you do not own, another agent is mid-edit: wait about 3 minutes and retry; never touch it.
4. Capture: `powershell -NoProfile -File Tools/run-player.ps1 -Name <you>-capNN -PlayerArgs "-capture","QA\captures\<you>-NN","-shots","<list>","-res","1920x1080"`
   `<list>` is a comma list of key-shot ids (`KS12,KS13`), `beatN` (start/mid/end plus 6 free-look stills), or `t2,t3,t5,t7,h10`
   (see `Assets/_Game/Scripts/Runtime/QA/CapturePlans.cs` `Parse`). Omit `-res` to get every resolution of a key shot (4K too).
   Look at the PNGs with the Read tool (that is the point). Metrics: `python -I Tools/qa_metrics.py QA/captures/<you>-NN --no-prev --no-fail`
   (writes `metrics.md` in the run folder: luma, clipped %, banding, the key-shot acceptance checks it can do).
5. Batch several fixes per build. Budget: about 6 to 8 build-and-capture cycles. Stop earlier when your acceptance list is met.

## Quality rules (Part 8, 10, 14.7)
- Everything procedural: C# meshes (`MeshKit`), handwritten HLSL, procedural textures. No imported assets.
- Shaders: follow the pattern of the existing `Megastructure/Ring*` shaders (SRP Batcher: one `UnityPerMaterial` CBUFFER;
  bend, shared lighting and `RingFog` in every pass; ShadowCaster/DepthOnly/DepthNormals passes kept). Guard every division,
  `pow`, `sqrt`, `normalize`. Filter procedural lines and patterns with `fwidth` or the pixel footprint and fade them out
  before they alias (no shimmer, no moire). No co-planar faces (offset or recess). Bevels on near geometry.
- Keep triangle budgets of 14.5. No per-frame allocations, no mesh generation after load.
- Match the surrounding code: naming, comment density, units in comments, references to the spec section.

## Final report (your last message, plain text)
Changes per file; the final capture folder and the shots in it; measured numbers against your acceptance list; what is
still open and why; exact shared-file changes you need from the lead (if any).
