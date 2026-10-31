# Image metrics - run reveal_r03

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\reveal_r03`  
Generated: 2026-10-30T10:46:30+01:00 by image_metrics.py 1.0.0  
Shots: 20, pass: 18, fail: 2, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5c_lift_down | 5 | 1920x1080 | 0.093 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.094 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.096 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.364 [0.25-0.45] | 0.501 [<=0.92] | 0.00 [<=1.0] | 7 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.363 [0.25-0.45] | 0.501 [<=0.92] | 0.00 [<=1.0] | 8 / 24.0 | 0 | 0 | PASS |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.366 [0.25-0.45] | 0.501 [<=0.92] | 0.00 [<=1.0] | 9 / 11.7 | 0 | 0 | PASS |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.362 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.389 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_4k | 7 outside | 3840x2160 | 0.388 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 24.0 | 0 | 0 | PASS |
| KS7c_goal_door_16x10 | 7 outside | 1680x1050 | 0.389 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 11.7 | 0 | 0 | PASS |
| KS7c_goal_door_21x9 | 7 outside | 2560x1080 | 0.388 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.275 [0.25-0.45] | 0.373 [<=0.92] | 0.00 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.270 [0.25-0.45] | 0.373 [<=0.92] | 0.00 [<=1.0] | 14 / 11.7 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.293 [0.25-0.45] | 0.374 [<=0.92] | 0.00 [<=1.0] | 13 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7b_honeycomb_px_base | 7 outside | 1920x1080 | 0.391 [0.25-0.45] | 0.505 [<=0.92] | 0.00 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_move10 | 7 outside | 1920x1080 | 0.373 [0.25-0.45] | 0.505 [<=0.92] | 0.00 [<=1.0] | 5 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_yaw2 | 7 outside | 1920x1080 | 0.391 [0.25-0.45] | 0.505 [<=0.92] | 0.00 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_base | 7 outside | 1920x1080 | 0.389 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_move10 | 7 outside | 1920x1080 | 0.370 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 5 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_yaw2 | 7 outside | 1920x1080 | 0.389 [0.25-0.45] | 0.505 [<=0.92] | 0.01 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |

## Failures

- KS7d_look_back_16x10 (beat07_reveal_KS7d_look_back_16x10.png): banding:sky_haze_top40
- KS7d_look_back_21x9 (beat07_reveal_KS7d_look_back_21x9.png): banding:sky_haze_top40
