# FEATURE MATRIX — Thread

Tier: F = full (as specified), S = simplified, G = graybox. Status is from the last built player (fix wave 3, captures `QA/captures/run10`); evidence lives in `QA/`. Deviations from the spec are logged as decisions in `DEVELOPMENT.md` (D-0xx).

| Beat | Feature | Tier | Status | Notes |
|---|---|---|---|---|
| 1 Salt flat | polar-grid ground, 4-level crack network (M01) | F | done | crack levels blend with altitude so the sinking plain keeps scale (D-041) |
| 1 | analytic far thread (thread law 14.7.3) | F | done | moved to the decided bearing (D-026); fades inside the bank before the swap (D-049) |
| 1 | mist bank (analytic fog term) | F | done | clears behind the walker (D-036); hue-preserving haze (D-065); soft ground foot (D-068) |
| 2 Avenue | 8 colossi (lathe robes, hoods, sleeves, doors, ledges, bollards) | F | done | heads bow 19-21 deg and look toward the path (D-055); 48-side sleeves with gathered cuffs (D-057); CREAM door frames (D-058); ~14 k triangles per colossus (spec 11 k) |
| 2 | colossus ground shadows (flat meshes) | F | done | fade with distance and view depression, no ruled line under the horizon (D-039, D-059) |
| 3 Stairs | 200 treads, stringers, stripes, bollards, white-out | F | done | 4 m walk between two bollard rows (D-053); the s = 305 std target is unreachable with SAND treads near the eye (D-052) |
| 4 Tower | terrace + mandala, cable LOD0-2 + collars + ribbons, swap in the bank | F | done | KF4b re-posed so the cable has no visible top (D-069); CREAM marker plates give the cable scale (D-062) |
| 5 Spiral | helix deck, rails, posts, arms, ground sink | F | done | rails fade with the posts (D-063); the walkway far below fades out above the deck (D-060); KF5a/c moved to s 468/488 (D-070) |
| 6 Cloud sea | deck pass, analytic sea, cable shadow band, 9 beads + windows + doors | F | done | seamless deck pass (D-067); anti-aliased puffs (D-076); band as a multiplier (D-077); beads at 288 x 48 (D-043) |
| 7 Top | planet, Earth limb, pier + pad, beacon, silence | F | done | screen-round impostors (D-046); crisp three-tone Earth (D-078); beacon pillar 1.6 m (D-051); "thread meets Earth" cannot be seen from a walkable pose (D-074) |
| All | ThreadGrade (bloom, Neutral, vignette, TPDF blue-noise dither) | F | done | |
| All | procedural audio (8 layers, FDN reverb, footsteps) | F | done | player dump matches the offline render within 0.6 dB |
| All | title card, hint, ending, pause overlay | F | done | |
| All | cinematic autowalk with gaze cues | F | done | chained pans and per-cue slow-down (D-054, D-082): about 10.3 min |
| QA | capture harness, autotest, wall bot, soak, perf, Python metrics | F | done | the 30-min soak and a third critic round were not run on the final build (see OPUS_5.5_FINAL_REPORT.md) |
