# Image metrics - run f01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\f01`  
Generated: 2026-10-30T09:41:10+01:00 by image_metrics.py 1.0.0  
Shots: 50, pass: 24, fail: 26, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.193 [0.10-0.20] | 0.553 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.185 [0.10-0.20] | 0.596 [<=0.90] | 0.00 [<=0.3] | 8 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.215 [0.10-0.20] | 0.476 [<=0.90] | 0.00 [<=0.3] | 8 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.287 [0.10-0.20] | 0.852 [<=0.90] | 0.02 [<=0.3] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.293 [0.10-0.20] | 0.855 [<=0.90] | 0.02 [<=0.3] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.267 [0.10-0.20] | 0.844 [<=0.90] | 0.01 [<=0.3] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.060 [0.05-0.13] | 0.455 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.060 [0.05-0.13] | 0.455 [<=0.70] | 0.01 [<=0.3] | 11 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.062 [0.05-0.13] | 0.456 [<=0.70] | 0.01 [<=0.3] | 11 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.058 [0.05-0.13] | 0.451 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.126 [0.05-0.13] | 0.726 [<=0.70] | 0.00 [<=0.3] | 12 / 12.0 | 0 | 0 | FAIL: p99_luma |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.125 [0.05-0.13] | 0.729 [<=0.70] | 0.00 [<=0.3] | 13 / 24.0 | 0 | 0 | FAIL: p99_luma |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.134 [0.05-0.13] | 0.730 [<=0.70] | 0.00 [<=0.3] | 10 / 11.7 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.109 [0.05-0.13] | 0.715 [<=0.70] | 0.00 [<=0.3] | 13 / 12.0 | 0 | 0 | FAIL: p99_luma, banding:void_bottom40 |
| KS3_column_up | 3 | 1920x1080 | 0.093 [0.06-0.15] | 0.314 [<=0.70] | 0.04 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.092 [0.06-0.15] | 0.314 [<=0.70] | 0.04 [<=0.3] | 8 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.095 [0.06-0.15] | 0.317 [<=0.70] | 0.04 [<=0.3] | 6 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.085 [0.06-0.15] | 0.308 [<=0.70] | 0.03 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.042 [0.08-0.18] | 0.370 [<=0.80] | 0.17 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.042 [0.08-0.18] | 0.352 [<=0.80] | 0.19 [<=0.5] | 10 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.038 [0.08-0.18] | 0.375 [<=0.80] | 0.13 [<=0.5] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5a_lift_city | 5 | 1920x1080 | 0.104 [0.06-0.16] | 0.573 [<=0.80] | 0.08 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.103 [0.06-0.16] | 0.563 [<=0.80] | 0.10 [<=0.5] | 11 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.099 [0.06-0.16] | 0.467 [<=0.80] | 0.08 [<=0.5] | 11 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.118 [0.06-0.16] | 0.622 [<=0.80] | 0.07 [<=0.5] | 15 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_bottom |
| KS5b_lift_fan | 5 | 1920x1080 | 0.320 [0.06-0.16] | 0.843 [<=0.80] | 0.03 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.319 [0.06-0.16] | 0.847 [<=0.80] | 0.04 [<=0.5] | 15 / 24.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.326 [0.06-0.16] | 0.847 [<=0.80] | 0.03 [<=0.5] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.303 [0.06-0.16] | 0.830 [<=0.80] | 0.04 [<=0.5] | 12 / 12.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.127 [0.06-0.16] | 0.513 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.125 [0.06-0.16] | 0.517 [<=0.80] | 0.00 [<=0.5] | 9 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.135 [0.06-0.16] | 0.491 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.507 [0.09-0.22] | 0.929 [<=0.95] | 0.46 [<=1.0] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.507 [0.09-0.22] | 0.933 [<=0.95] | 0.67 [<=1.0] | 11 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.521 [0.09-0.22] | 0.934 [<=0.95] | 0.49 [<=1.0] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.466 [0.09-0.22] | 0.905 [<=0.95] | 0.34 [<=1.0] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.089 [0.04-0.20] | 0.622 [<=0.98] | 0.00 [<=2.0] | 9 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_16x10 | 7 tunnel | 1680x1050 | 0.088 [0.04-0.20] | 0.622 [<=0.98] | 0.00 [<=2.0] | 10 / 11.7 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_21x9 | 7 tunnel | 2560x1080 | 0.096 [0.04-0.20] | 0.621 [<=0.98] | 0.00 [<=2.0] | 8 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.369 [0.25-0.45] | 0.665 [<=0.92] | 0.01 [<=1.0] | 12 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.368 [0.25-0.45] | 0.665 [<=0.92] | 0.00 [<=1.0] | 13 / 24.0 | 0 | 0 | PASS |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.373 [0.25-0.45] | 0.668 [<=0.92] | 0.01 [<=1.0] | 11 / 11.7 | 0 | 0 | PASS |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.361 [0.25-0.45] | 0.654 [<=0.92] | 0.01 [<=1.0] | 9 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.384 [0.25-0.45] | 0.613 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | FAIL: horizon_agreement |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.383 [0.25-0.45] | 0.610 [<=0.92] | 0.01 [<=1.0] | 6 / 24.0 | 0 | 0 | FAIL: horizon_agreement |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.386 [0.25-0.45] | 0.621 [<=0.92] | 0.02 [<=1.0] | 6 / 11.7 | 0 | 0 | FAIL: horizon_agreement |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.380 [0.25-0.45] | 0.599 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | FAIL: horizon_agreement |
| KS7d_look_back | 7 outside | 1920x1080 | 0.280 [0.25-0.45] | 0.377 [<=0.92] | 0.00 [<=1.0] | 14 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.276 [0.25-0.45] | 0.381 [<=0.92] | 0.00 [<=1.0] | 11 / 11.7 | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.297 [0.25-0.45] | 0.374 [<=0.92] | 0.00 [<=1.0] | 14 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
- KS1b_doorway (beat01_gallery_KS1b_doorway.png): mean_luma
- KS1b_doorway_16x10 (beat01_gallery_KS1b_doorway_16x10.png): mean_luma
- KS1b_doorway_21x9 (beat01_gallery_KS1b_doorway_21x9.png): mean_luma
- KS2b_bridge_down (beat02_bridge_KS2b_bridge_down.png): p99_luma
- KS2b_bridge_down_4k (beat02_bridge_KS2b_bridge_down_4k.png): p99_luma
- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): mean_luma, p99_luma
- KS2b_bridge_down_21x9 (beat02_bridge_KS2b_bridge_down_21x9.png): p99_luma, banding:void_bottom40
- KS4_hall_pistons (beat04_hall_KS4_hall_pistons.png): mean_luma
- KS4_hall_pistons_16x10 (beat04_hall_KS4_hall_pistons_16x10.png): mean_luma
- KS4_hall_pistons_21x9 (beat04_hall_KS4_hall_pistons_21x9.png): mean_luma
- KS5a_lift_city_21x9 (beat05_lift_KS5a_lift_city_21x9.png): banding:shaft_fog_bottom
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma, p99_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma, p99_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma, p99_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma, p99_luma
- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- KS6_gantry_fan_4k (beat06_fan_KS6_gantry_fan_4k.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
- KS6_gantry_fan_21x9 (beat06_fan_KS6_gantry_fan_21x9.png): mean_luma
- KS7c_goal_door (beat07_reveal_KS7c_goal_door.png): horizon_agreement
- KS7c_goal_door_4k (beat07_reveal_KS7c_goal_door_4k.png): horizon_agreement
- KS7c_goal_door_16x10 (beat07_reveal_KS7c_goal_door_16x10.png): horizon_agreement
- KS7c_goal_door_21x9 (beat07_reveal_KS7c_goal_door_21x9.png): horizon_agreement
- KS7d_look_back (beat07_reveal_KS7d_look_back.png): banding:sky_haze_top40
- KS7d_look_back_21x9 (beat07_reveal_KS7d_look_back_21x9.png): banding:sky_haze_top40
