# FEATURE MATRIX — The Ring

Beat-by-beat status against MASTER_PROMPT Part 14. Tier: **Full** (as specified), **Simplified**, **Graybox** (Part 13 ladder).
Deviations that are deliberate decisions are ADRs in `DEVELOPMENT.md`.

| Beat | s | Tier | Status and honest notes |
|---|---|---|---|
| 1 Capsule, gangway | 0-24 | Full | Ceramic capsule (2.6 x 2.5 x 11 m clear), 11 ribs, porthole with PaleGold ring, sliding hatch (s 6.5, 2 s), gangway, landing pad, motes, 2700 K strips. Interior tones reworked in wave 2 (was khaki). |
| 2 Valley, bridge (money shot 1) | 24-276 | Full | Clipmap terrain (9 levels), river, cos-profile bridge with rails and lamps, belvedere, aqueduct, grass and flower indirect draws, cloud shadows. ADR-012: lighter valley fog (sigma 1e-4) and a haze-layer arch so the land visibly climbs into the sky; arch geography reworked (rectilinear farm districts, forests, lakes, rivers, towns and villages on jittered grids, no road graticule). Terrain no longer forms a trench along the path. |
| 3 The ark (money shot 2) | 276-411 | Full | 3 km hull on its track with window rows, spars, engines, analytic 3 000 x 620 m shadow, footbridge arch. Fog thins with height (ADR-012), so the hull can read dark. Squares no longer fade in during KS04 (ADR-013). |
| 4 Sunset, night, underpass (money shot 3) | 411-594 | Full | Shadow squares A/B/C crossing the star (opaque silhouettes), V(u) LUT, sky day -> dusk -> night, terminator band with the lit arch overhead, glittering city lights on the night side, levee with terraces and lamps, sealed underpass and swap at s 572 (ADR-009). Sky light dims with the eclipse (V^3) so the sunset reads as a sunset. Night white balance halved (ADR-013). |
| 5 Rim wall | 594-774 | Full | 12 km wall with seams, door with the tunnel profile, plinth, apron, bollards, climber light, far-rim sky rule. Wall reworked in wave 2 (was a flat blue plane). |
| 6 Wall tunnel | 774-894 | Full | 120 m tunnel, 20 ribs, amber strips and markers, mouth spill, EV ramps; the outside deck is sunlit through the mouth (key L7 from s 834, ADR-013). |
| 7 Platform, swarm, beacon (money shot 4) | 894-960 | Full (ADR-011 geometry) | 12 m pier instead of 30 m and hull 200 m below instead of 600 m (ADR-011: with the spec numbers the hull was never visible); 24 000-panel swarm in three bands with glints, outer face, beacon with pulse and ping, ending line. |

## Systems
| System | Status |
|---|---|
| Player, corridor colliders, safety volume, cinematic autowalk with holds and gaze | done; autotest passes in the player |
| Shared atmosphere (one fog model, sky, arch haze, aerial perspective) | done (ADR-012) |
| Final static dither pass, camera dithering off, HDR 64-bit, MSAA 4x, reversed float depth D32 | done |
| Procedural audio (wind, river, ark, dusk pad, night bells, tunnel, star drone, beacon, footsteps, limiter) | done; offline renders analysed (QA/audio) |
| UI Toolkit title, hint, pause overlay, ending line | done |
| QA harness (captures, sequences, metrics, autotest, wall test, perf log) | built; see the report for what was run in the final session |
