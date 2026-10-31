# Image metrics - run global_r11

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r11`  
Generated: 2026-10-30T12:28:57+01:00 by image_metrics.py 1.0.0  
Shots: 19, pass: 15, fail: 4, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.196 [0.10-0.20] | 0.496 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway | 1 | 1920x1080 | 0.174 [0.10-0.20] | 0.614 [<=0.90] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: doorway_luma |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.105 [0.05-0.13] | 0.618 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.150 [0.06-0.15] | 0.497 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.166 [0.08-0.18] | 0.501 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.141 [0.06-0.16] | 0.432 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.101 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.094 [0.04-0.20] | 0.611 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.358 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.379 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.289 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| B1_middle | 1 | 1920x1080 | 0.156 [0.10-0.20] | 0.457 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.187 [0.05-0.13] | 0.519 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B2_freelook_back | 2 | 1920x1080 | 0.153 [0.05-0.13] | 0.273 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.107 [0.09-0.22] | 0.327 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B7_freelook_leftup | 7 outside | 1920x1080 | 0.365 [0.25-0.45] | 0.482 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS1b_doorway (beat01_gallery_KS1b_doorway.png): doorway_luma
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- B2_start (beat02_bridge_B2_start.png): mean_luma
