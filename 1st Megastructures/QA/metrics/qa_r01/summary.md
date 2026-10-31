# Image metrics - run qa_r01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\qa_r01`  
Generated: 2026-10-30T12:30:38+01:00 by image_metrics.py 1.0.0  
Shots: 57, pass: 45, fail: 12, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.196 [0.10-0.20] | 0.496 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.189 [0.10-0.20] | 0.535 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.214 [0.10-0.20] | 0.430 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.174 [0.10-0.20] | 0.614 [<=0.90] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: doorway_luma |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.180 [0.10-0.20] | 0.651 [<=0.90] | 0.02 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: doorway_luma |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.157 [0.10-0.20] | 0.568 [<=0.90] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: doorway_luma |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.342 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.094 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.105 [0.05-0.13] | 0.618 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.105 [0.05-0.13] | 0.619 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.112 [0.05-0.13] | 0.624 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.090 [0.05-0.13] | 0.590 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.150 [0.06-0.15] | 0.497 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.150 [0.06-0.15] | 0.497 [<=0.70] | 0.01 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.154 [0.06-0.15] | 0.501 [<=0.70] | 0.01 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.137 [0.06-0.15] | 0.485 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.166 [0.08-0.18] | 0.501 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.171 [0.08-0.18] | 0.498 [<=0.80] | 0.02 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.154 [0.08-0.18] | 0.513 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.141 [0.06-0.16] | 0.432 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.141 [0.06-0.16] | 0.428 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.142 [0.06-0.16] | 0.428 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.138 [0.06-0.16] | 0.432 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.198 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.177 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.101 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.101 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.102 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.241 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.209 [0.09-0.22] | 0.482 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.094 [0.04-0.20] | 0.611 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_16x10 | 7 tunnel | 1680x1050 | 0.095 [0.04-0.20] | 0.611 [<=0.98] | 0.00 [<=2.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_21x9 | 7 tunnel | 2560x1080 | 0.094 [0.04-0.20] | 0.607 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.358 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.357 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.360 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.355 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.379 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.379 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.380 [0.25-0.45] | 0.502 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.378 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.289 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.285 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.306 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| cue_freight_t30 | 5 | 1920x1080 | 0.128 [0.06-0.16] | 0.298 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_freight_t31 | 5 | 1920x1080 | 0.127 [0.06-0.16] | 0.297 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p0 | 6 | 1920x1080 | 0.178 [0.09-0.22] | 0.371 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p1 | 6 | 1920x1080 | 0.174 [0.09-0.22] | 0.343 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p2 | 6 | 1920x1080 | 0.189 [0.09-0.22] | 0.358 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p3 | 6 | 1920x1080 | 0.196 [0.09-0.22] | 0.374 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.424 [0.25-0.45] | 0.600 [<=0.92] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
- KS1b_doorway (beat01_gallery_KS1b_doorway.png): doorway_luma
- KS1b_doorway_16x10 (beat01_gallery_KS1b_doorway_16x10.png): doorway_luma
- KS1b_doorway_21x9 (beat01_gallery_KS1b_doorway_21x9.png): doorway_luma
- KS3_column_up_16x10 (beat03_ledge_KS3_column_up_16x10.png): mean_luma
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- KS6_gantry_fan_4k (beat06_fan_KS6_gantry_fan_4k.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
