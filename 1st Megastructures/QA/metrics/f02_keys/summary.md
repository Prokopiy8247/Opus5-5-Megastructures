# Image metrics - run f02_keys

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\f02_keys`  
Generated: 2026-10-30T11:04:16+01:00 by image_metrics.py 1.0.0  
Shots: 50, pass: 43, fail: 7, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.192 [0.10-0.20] | 0.499 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.185 [0.10-0.20] | 0.539 [<=0.90] | 0.00 [<=0.3] | 10 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.210 [0.10-0.20] | 0.433 [<=0.90] | 0.00 [<=0.3] | 8 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.180 [0.10-0.20] | 0.699 [<=0.90] | 0.02 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.186 [0.10-0.20] | 0.700 [<=0.90] | 0.02 [<=0.3] | 10 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.164 [0.10-0.20] | 0.688 [<=0.90] | 0.01 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.091 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.091 [0.05-0.13] | 0.336 [<=0.70] | 0.01 [<=0.3] | 14 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.092 [0.05-0.13] | 0.339 [<=0.70] | 0.01 [<=0.3] | 11 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.089 [0.05-0.13] | 0.327 [<=0.70] | 0.00 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.107 [0.05-0.13] | 0.597 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.106 [0.05-0.13] | 0.600 [<=0.70] | 0.00 [<=0.3] | 10 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.114 [0.05-0.13] | 0.604 [<=0.70] | 0.00 [<=0.3] | 14 / 11.7 | 0 | 0 | FAIL: banding:void_bottom40 |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.093 [0.05-0.13] | 0.582 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.130 [0.06-0.15] | 0.442 [<=0.70] | 0.00 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.130 [0.06-0.15] | 0.442 [<=0.70] | 0.00 [<=0.3] | 7 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.133 [0.06-0.15] | 0.446 [<=0.70] | 0.00 [<=0.3] | 7 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.123 [0.06-0.15] | 0.428 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.146 [0.08-0.18] | 0.610 [<=0.80] | 0.27 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.148 [0.08-0.18] | 0.628 [<=0.80] | 0.29 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.137 [0.08-0.18] | 0.602 [<=0.80] | 0.20 [<=0.5] | 13 / 12.0 | 0 | 0 | FAIL: banding:hall_haze |
| KS5a_lift_city | 5 | 1920x1080 | 0.121 [0.06-0.16] | 0.444 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.121 [0.06-0.16] | 0.444 [<=0.80] | 0.00 [<=0.5] | 13 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.118 [0.06-0.16] | 0.428 [<=0.80] | 0.00 [<=0.5] | 11 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.129 [0.06-0.16] | 0.453 [<=0.80] | 0.00 [<=0.5] | 12 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.151 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.150 [0.06-0.16] | 0.472 [<=0.80] | 0.01 [<=0.5] | 15 / 24.0 | 0 | 0 | PASS |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.156 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 11 / 11.7 | 0 | 0 | PASS |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.139 [0.06-0.16] | 0.471 [<=0.80] | 0.00 [<=0.5] | 14 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_top, banding:shaft_fog_bottom |
| KS5c_lift_down | 5 | 1920x1080 | 0.092 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 9 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.093 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 9 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.095 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.216 [0.09-0.22] | 0.498 [<=0.95] | 0.07 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.216 [0.09-0.22] | 0.498 [<=0.95] | 0.07 [<=1.0] | 11 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.224 [0.09-0.22] | 0.498 [<=0.95] | 0.08 [<=1.0] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.195 [0.09-0.22] | 0.498 [<=0.95] | 0.05 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.113 [0.04-0.20] | 0.614 [<=0.98] | 0.00 [<=2.0] | 8 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_16x10 | 7 tunnel | 1680x1050 | 0.112 [0.04-0.20] | 0.614 [<=0.98] | 0.00 [<=2.0] | 10 / 11.7 | 0 | 0 | PASS |
| KS7a_tunnel_mouth_21x9 | 7 tunnel | 2560x1080 | 0.120 [0.04-0.20] | 0.611 [<=0.98] | 0.00 [<=2.0] | 10 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.381 [0.25-0.45] | 0.506 [<=0.92] | 0.00 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.381 [0.25-0.45] | 0.506 [<=0.92] | 0.00 [<=1.0] | 8 / 24.0 | 0 | 0 | PASS |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.383 [0.25-0.45] | 0.506 [<=0.92] | 0.00 [<=1.0] | 9 / 11.7 | 0 | 0 | PASS |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.380 [0.25-0.45] | 0.506 [<=0.92] | 0.00 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.405 [0.25-0.45] | 0.507 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.404 [0.25-0.45] | 0.507 [<=0.92] | 0.01 [<=1.0] | 6 / 24.0 | 0 | 0 | PASS |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.405 [0.25-0.45] | 0.507 [<=0.92] | 0.01 [<=1.0] | 6 / 11.7 | 0 | 0 | PASS |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.404 [0.25-0.45] | 0.507 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.289 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.284 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | 12 / 11.7 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.306 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | 15 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): banding:void_bottom40
- KS4_hall_pistons_21x9 (beat04_hall_KS4_hall_pistons_21x9.png): banding:hall_haze
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): banding:shaft_fog_top, banding:shaft_fog_bottom
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
- KS7d_look_back_16x10 (beat07_reveal_KS7d_look_back_16x10.png): banding:sky_haze_top40
- KS7d_look_back_21x9 (beat07_reveal_KS7d_look_back_21x9.png): banding:sky_haze_top40
