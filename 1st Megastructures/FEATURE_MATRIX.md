# FEATURE MATRIX — Abyss

Honest beat-by-beat status. Tier = Full / Simplified / Graybox (Part 13 fallback ladder). Updated at the end of the run (2026-10-30).

| Beat | Content | Tier | Status | Notes |
|---|---|---|---|---|
| 1 Service gallery | loft shell, stairs, fixtures waking ahead, pressure door, wall fan, doorway spill, depth-wrapped doorway veil | Full | done | Through the doorway at KS1b only C1's fogged face is visible (C1 fills the opening; logged arbitration); walls and ribs appear from the threshold on |
| 2 Railless bridge (money shot) | shaft shell + ribs/belts with lit chamfers, columns C1-C4, 1.2 x 150 m bridge + truss, outrigger lamps, hoist block (x 140), falling bolt from the right-wall belt (D3), far-layer floods | Full | done | KS2b strips pass at all aspects; KS2a wall macro texture below the automated 2-level gate (shaders arbitration) |
| 3 Column ledge | C1 collar ledge, lamps, hatch, apron, cold spots | Full | done | Only the y 3 pour lift reads clearly in KS3 (the y 6 / y 9 lifts fall on C1's silhouette at the spec pose); logged |
| 4 Machine hall | hull with load paths and lit door, trusses, catwalk with lit grating, four crank-slider assemblies, vibration | Full | done | |
| 5 Lift and vertical city (money shot) | 20 m platform, 120 s ride, city districts on three faces (918 modules / 44 064 windows), blocks on concrete cantilevers, freight lights incl. the walk-on group, rail marker lamps | Full | done | KS5c look-down at pitch -38 (D1); lift deck surface detail below the 2-level gate |
| 6 Fan gantry | 300 m fan, 12 blades, contre-jour bore card, analytic beams and blade-shadow prisms, gantry, lighthouse patches | Full | done | |
| 7 Tunnel, balcony, honeycomb, door (money shot) | tunnel, Face, balcony, pier, door with amber sliver, honeycomb tiers 0/1/2 with tube air and ring lines, lamp ignition (9 s sweep), ending line | Full | done | KS7b: 129 readable cells against the 150 of 14.10 (far 4-10 km rows merge into haze) |

| System | Status | Notes |
|---|---|---|
| Player, path, lift kinematics, autowalk, gaze cues (bolt tracking, H7 pan, pier vertigo look, door stop) | done | PathTests pass; autotest PASS (656.6 s, lift 308.0-428.0 s, 0.04 mm offset) |
| Progress/fog/exposure/key-light direction, ring-chamfer key share, UI title/pause/ending | done | ending line uses the title typography |
| Shaders (Surface recipes, Grating, Facade, Emissive, Glow, BeamWedge, Lattice, Sky, FinalDither) | done | static 2.0 LSB TPDF final dither |
| Procedural audio (engine, FDN reverb, layers, footsteps, offline render) | done | all audio checks PASS (built-player render) |
| QA: Python metrics/encode/sequence tools, Tools/metrics.py | done | |
| QA: runtime capture harness, autotest, bots, perf, soak | done | full end-of-run sequence suite and critic round 3 were not run (run ended early at the user's request; see OPUS_5.5_FINAL_REPORT.md) |
