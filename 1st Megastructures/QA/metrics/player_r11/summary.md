# Image metrics - run player_r11

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\player_r11`  
Generated: 2026-10-30T14:36:46+01:00 by image_metrics.py 1.0.0  
Shots: 11, pass: 4, fail: 7, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5c_lift_down | 5 | 1920x1080 | 0.074 [0.06-0.16] | 0.491 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: near_black_pct |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.074 [0.06-0.16] | 0.492 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: near_black_pct, black_runs |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.077 [0.06-0.16] | 0.487 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: near_black_pct |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.292 [0.25-0.45] | 0.493 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.292 [0.25-0.45] | 0.493 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: amber_saturation, readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.289 [0.25-0.45] | 0.493 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: amber_saturation, readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.304 [0.25-0.45] | 0.493 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: amber_saturation, readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_px_base | 7 outside | 1920x1080 | 0.352 [0.25-0.45] | 0.494 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_move10 | 7 outside | 1920x1080 | 0.352 [0.25-0.45] | 0.494 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_yaw2 | 7 outside | 1920x1080 | 0.352 [0.25-0.45] | 0.494 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.348 [0.25-0.45] | 0.581 [<=0.92] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5c_lift_down (beat05_lift_KS5c_lift_down.png): near_black_pct
- KS5c_lift_down_16x10 (beat05_lift_KS5c_lift_down_16x10.png): near_black_pct, black_runs
- KS5c_lift_down_21x9 (beat05_lift_KS5c_lift_down_21x9.png): near_black_pct
- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): amber_saturation, readable_cells
- KS7b_honeycomb_4k (beat07_reveal_KS7b_honeycomb_4k.png): amber_saturation, readable_cells
- KS7b_honeycomb_16x10 (beat07_reveal_KS7b_honeycomb_16x10.png): amber_saturation, readable_cells
- KS7b_honeycomb_21x9 (beat07_reveal_KS7b_honeycomb_21x9.png): amber_saturation, readable_cells
