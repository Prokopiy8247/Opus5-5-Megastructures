# Image metrics - run lead_b01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\lead_b01`  
Generated: 2026-10-30T15:44:20+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 9, fail: 5, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.198 [0.10-0.20] | 0.495 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_board_steps |
| KS1b_doorway | 1 | 1920x1080 | 0.175 [0.05-0.20] | 0.576 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.309 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2b_bridge_down | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.459 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.142 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS4_hall_pistons | 4 | 1920x1080 | 0.168 [0.08-0.18] | 0.498 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.128 [0.06-0.16] | 0.401 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.200 [0.06-0.16] | 0.726 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.088 [0.06-0.16] | 0.203 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.213 [0.09-0.22] | 0.491 [<=0.95] | 0.06 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.095 [0.04-0.20] | 0.617 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.400 [0.25-0.45] | 0.527 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.409 [0.25-0.45] | 0.527 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.292 [0.25-0.45] | 0.460 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |

## Failures

- KS1_gallery_start (beat01_gallery_KS1_gallery_start.png): wall_board_steps
- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): amber_saturation, readable_cells
