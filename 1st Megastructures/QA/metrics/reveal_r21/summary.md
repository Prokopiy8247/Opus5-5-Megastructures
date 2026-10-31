# Image metrics - run reveal_r21

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\reveal_r21`  
Generated: 2026-10-30T14:59:28+01:00 by image_metrics.py 1.0.0  
Shots: 27, pass: 22, fail: 5, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.095 [0.04-0.20] | 0.617 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_16x10 | 7 tunnel | 1680x1050 | 0.097 [0.04-0.20] | 0.618 [<=0.98] | 0.00 [<=2.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_21x9 | 7 tunnel | 2560x1080 | 0.096 [0.04-0.20] | 0.614 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.410 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.410 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.414 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.402 [0.25-0.45] | 0.558 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells (warn: stipple_tiles) |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.416 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.416 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.418 [0.25-0.45] | 0.562 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.413 [0.25-0.45] | 0.558 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.292 [0.25-0.45] | 0.496 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.288 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.309 [0.25-0.45] | 0.394 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| B7_start | 7 tunnel | 1920x1080 | 0.186 [0.04-0.20] | 0.718 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| B7_middle | 7 outside | 1920x1080 | 0.461 [0.25-0.45] | 0.561 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B7_end | 7 outside | 1920x1080 | 0.306 [0.25-0.45] | 0.489 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B7_freelook_back | 7 outside | 1920x1080 | 0.368 [0.25-0.45] | 0.523 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B7_freelook_leftup | 7 outside | 1920x1080 | 0.365 [0.25-0.45] | 0.482 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B7_freelook_rightdown | 7 outside | 1920x1080 | 0.473 [0.25-0.45] | 0.538 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_freight_t30 | 5 | 1920x1080 | 0.118 [0.06-0.16] | 0.298 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_freight_t31 | 5 | 1920x1080 | 0.118 [0.06-0.16] | 0.297 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p0 | 6 | 1920x1080 | 0.173 [0.09-0.22] | 0.365 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p1 | 6 | 1920x1080 | 0.165 [0.09-0.22] | 0.334 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p2 | 6 | 1920x1080 | 0.181 [0.09-0.22] | 0.342 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p3 | 6 | 1920x1080 | 0.191 [0.09-0.22] | 0.367 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.488 [0.25-0.45] | 0.677 [<=0.92] | 0.01 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): amber_saturation, readable_cells
- KS7b_honeycomb_4k (beat07_reveal_KS7b_honeycomb_4k.png): amber_saturation, readable_cells
- KS7b_honeycomb_16x10 (beat07_reveal_KS7b_honeycomb_16x10.png): amber_saturation, readable_cells
- KS7b_honeycomb_21x9 (beat07_reveal_KS7b_honeycomb_21x9.png): amber_saturation, readable_cells
- B7_middle (beat07_reveal_B7_middle.png): mean_luma
