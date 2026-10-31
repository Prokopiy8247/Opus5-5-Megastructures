# Image metrics - run shaders_r04

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\shaders_r04`  
Generated: 2026-10-30T13:00:53+01:00 by image_metrics.py 1.0.0  
Shots: 29, pass: 25, fail: 4, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.095 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.351 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.092 [0.05-0.13] | 0.347 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.105 [0.05-0.13] | 0.620 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.105 [0.05-0.13] | 0.620 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.112 [0.05-0.13] | 0.628 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.088 [0.05-0.13] | 0.595 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.142 [0.06-0.15] | 0.412 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.142 [0.06-0.15] | 0.412 [<=0.70] | 0.02 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.146 [0.06-0.15] | 0.416 [<=0.70] | 0.02 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.131 [0.06-0.15] | 0.400 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.167 [0.08-0.18] | 0.495 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.173 [0.08-0.18] | 0.494 [<=0.80] | 0.02 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.154 [0.08-0.18] | 0.509 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.355 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: readable_cells (warn: stipple_tiles) |
| KS7b_honeycomb_4k | 7 outside | 3840x2160 | 0.355 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: readable_cells |
| KS7b_honeycomb_16x10 | 7 outside | 1680x1050 | 0.357 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: readable_cells |
| KS7b_honeycomb_21x9 | 7 outside | 2560x1080 | 0.353 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: readable_cells (warn: stipple_tiles) |
| KS7d_look_back | 7 outside | 1920x1080 | 0.290 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.285 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.307 [0.25-0.45] | 0.387 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| B3_freelook_back | 3 | 1920x1080 | 0.183 [0.06-0.15] | 0.763 [<=0.70] | 0.59 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B5_freelook_rightdown | 5 | 1920x1080 | 0.074 [0.06-0.16] | 0.367 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.188 [0.09-0.22] | 0.490 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_base | 7 outside | 1920x1080 | 0.378 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_move10 | 7 outside | 1920x1080 | 0.356 [0.25-0.45] | 0.507 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_yaw2 | 7 outside | 1920x1080 | 0.378 [0.25-0.45] | 0.500 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.421 [0.25-0.45] | 0.600 [<=0.92] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): readable_cells
- KS7b_honeycomb_4k (beat07_reveal_KS7b_honeycomb_4k.png): readable_cells
- KS7b_honeycomb_16x10 (beat07_reveal_KS7b_honeycomb_16x10.png): readable_cells
- KS7b_honeycomb_21x9 (beat07_reveal_KS7b_honeycomb_21x9.png): readable_cells
