# Image metrics - run global_r21

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r21`  
Generated: 2026-10-30T14:24:36+01:00 by image_metrics.py 1.0.0  
Shots: 17, pass: 10, fail: 7, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.095 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.092 [0.05-0.13] | 0.347 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.106 [0.05-0.13] | 0.620 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: side_strips |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.106 [0.05-0.13] | 0.620 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: side_strips |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.114 [0.05-0.13] | 0.628 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: side_strips |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.090 [0.05-0.13] | 0.595 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: side_strips |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.355 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.355 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: readable_cells |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.357 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: readable_cells |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.353 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.180 [0.05-0.20] | 0.503 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_base | 7 outside | 1920x1080 | 0.378 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_move10 | 7 outside | 1920x1080 | 0.356 [0.25-0.45] | 0.507 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_yaw2 | 7 outside | 1920x1080 | 0.378 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.421 [0.25-0.45] | 0.600 [<=0.92] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2b_bridge_down (beat02_bridge_KS2b_bridge_down.png): side_strips
- KS2b_bridge_down_4k (beat02_bridge_KS2b_bridge_down_4k.png): side_strips
- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): side_strips
- KS2b_bridge_down_21x9 (beat02_bridge_KS2b_bridge_down_21x9.png): side_strips
- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): readable_cells
- KS7b_honeycomb_4k (beat07_reveal_KS7b_honeycomb_4k.png): readable_cells
- KS7b_honeycomb_16x10 (beat07_reveal_KS7b_honeycomb_16x10.png): readable_cells
