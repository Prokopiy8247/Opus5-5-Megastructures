# Image metrics - run f04_keys

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\f04_keys`  
Generated: 2026-10-30T17:23:41+01:00 by image_metrics.py 1.0.0  
Shots: 50, pass: 38, fail: 12, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.198 [0.10-0.20] | 0.492 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.190 [0.10-0.20] | 0.511 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.217 [0.10-0.20] | 0.438 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.173 [0.05-0.20] | 0.576 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.180 [0.05-0.20] | 0.580 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.157 [0.05-0.20] | 0.560 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.305 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.095 [0.05-0.13] | 0.305 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.308 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.092 [0.05-0.13] | 0.300 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2b_bridge_down | 2 | 1920x1080 | 0.081 [0.05-0.13] | 0.337 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.081 [0.05-0.13] | 0.338 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.086 [0.05-0.13] | 0.341 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.071 [0.05-0.13] | 0.328 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.143 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_4k | 3 | 3840x2160 | 0.143 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.146 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.132 [0.06-0.15] | 0.404 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS4_hall_pistons | 4 | 1920x1080 | 0.168 [0.08-0.18] | 0.498 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.174 [0.08-0.18] | 0.495 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.155 [0.08-0.18] | 0.512 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.127 [0.06-0.16] | 0.390 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.127 [0.06-0.16] | 0.386 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.128 [0.06-0.16] | 0.386 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.125 [0.06-0.16] | 0.386 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.210 [0.09-0.22] | 0.706 [<=0.95] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.210 [0.09-0.22] | 0.706 [<=0.95] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | PASS |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.220 [0.09-0.22] | 0.709 [<=0.95] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.183 [0.09-0.22] | 0.705 [<=0.95] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down | 5 | 1920x1080 | 0.087 [0.06-0.16] | 0.211 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.087 [0.06-0.16] | 0.216 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.092 [0.06-0.16] | 0.240 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.212 [0.09-0.22] | 0.490 [<=0.95] | 0.06 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.213 [0.09-0.22] | 0.491 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.220 [0.09-0.22] | 0.491 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.190 [0.09-0.22] | 0.487 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.095 [0.04-0.20] | 0.617 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_16x10 | 7 tunnel | 1680x1050 | 0.096 [0.04-0.20] | 0.618 [<=0.98] | 0.00 [<=2.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_21x9 | 7 tunnel | 2560x1080 | 0.095 [0.04-0.20] | 0.614 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.402 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.401 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: amber_saturation (warn: stipple_tiles) |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.405 [0.25-0.45] | 0.535 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.395 [0.25-0.45] | 0.528 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation (warn: stipple_tiles) |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.411 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.410 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.412 [0.25-0.45] | 0.532 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.408 [0.25-0.45] | 0.527 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.290 [0.25-0.45] | 0.460 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.285 [0.25-0.45] | 0.467 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.306 [0.25-0.45] | 0.394 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS2a_bridge_ahead_4k (beat02_bridge_KS2a_bridge_ahead_4k.png): wall_macro_residual
- KS2a_bridge_ahead_21x9 (beat02_bridge_KS2a_bridge_ahead_21x9.png): wall_macro_residual
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
- KS3_column_up_4k (beat03_ledge_KS3_column_up_4k.png): pour_lifts
- KS3_column_up_16x10 (beat03_ledge_KS3_column_up_16x10.png): pour_lifts
- KS3_column_up_21x9 (beat03_ledge_KS3_column_up_21x9.png): pour_lifts
- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): amber_saturation, readable_cells
- KS7b_honeycomb_4k (beat07_reveal_KS7b_honeycomb_4k.png): amber_saturation
- KS7b_honeycomb_16x10 (beat07_reveal_KS7b_honeycomb_16x10.png): amber_saturation, readable_cells
- KS7b_honeycomb_21x9 (beat07_reveal_KS7b_honeycomb_21x9.png): amber_saturation
