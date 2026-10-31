# Image metrics - run f02_beats

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\f02_beats`  
Generated: 2026-10-30T11:04:40+01:00 by image_metrics.py 1.0.0  
Shots: 42, pass: 34, fail: 8, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| B1_start | 1 | 1920x1080 | 0.191 [0.10-0.20] | 0.484 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B1_middle | 1 | 1920x1080 | 0.145 [0.10-0.20] | 0.453 [<=0.90] | 0.00 [<=0.3] | 12 / 12.0 | 0 | 0 | PASS |
| B1_end | 1 | 1920x1080 | 0.183 [0.10-0.20] | 0.581 [<=0.90] | 0.24 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B1_freelook_back | 1 | 1920x1080 | 0.151 [0.10-0.20] | 0.456 [<=0.90] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| B1_freelook_leftup | 1 | 1920x1080 | 0.084 [0.10-0.20] | 0.228 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B1_freelook_rightdown | 1 | 1920x1080 | 0.096 [0.10-0.20] | 0.127 [<=0.90] | 0.00 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.164 [0.05-0.13] | 0.562 [<=0.70] | 0.02 [<=0.3] | 9 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B2_middle | 2 | 1920x1080 | 0.100 [0.05-0.13] | 0.375 [<=0.70] | 0.04 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| B2_end | 2 | 1920x1080 | 0.101 [0.05-0.13] | 0.336 [<=0.70] | 0.11 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B2_freelook_back | 2 | 1920x1080 | 0.138 [0.05-0.13] | 0.242 [<=0.70] | 0.00 [<=0.3] | 15 / 12.0 | 0 | 0 | FAIL: banding:fog_top30, banding:void_bottom40 (warn: mean_luma) |
| B2_freelook_leftup | 2 | 1920x1080 | 0.132 [0.05-0.13] | 0.192 [<=0.70] | 0.00 [<=0.3] | 13 / 12.0 | 0 | 0 | FAIL: banding:void_bottom40 (warn: mean_luma) |
| B2_freelook_rightdown | 2 | 1920x1080 | 0.078 [0.05-0.13] | 0.226 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B3_start | 3 | 1920x1080 | 0.098 [0.06-0.15] | 0.263 [<=0.70] | 0.00 [<=0.3] | 5 / 12.0 | 0 | 0 | PASS |
| B3_middle | 3 | 1920x1080 | 0.147 [0.06-0.15] | 0.324 [<=0.70] | 0.00 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| B3_end | 3 | 1920x1080 | 0.070 [0.06-0.15] | 0.320 [<=0.70] | 0.01 [<=0.3] | 8 / 12.0 | 0 | 0 | PASS |
| B3_freelook_back | 3 | 1920x1080 | 0.190 [0.06-0.15] | 0.575 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B3_freelook_leftup | 3 | 1920x1080 | 0.160 [0.06-0.15] | 0.426 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| B3_freelook_rightdown | 3 | 1920x1080 | 0.185 [0.06-0.15] | 0.330 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B4_start | 4 | 1920x1080 | 0.065 [0.08-0.18] | 0.342 [<=0.80] | 0.01 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B4_middle | 4 | 1920x1080 | 0.147 [0.08-0.18] | 0.615 [<=0.80] | 0.18 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B4_end | 4 | 1920x1080 | 0.154 [0.08-0.18] | 0.725 [<=0.80] | 0.01 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B4_freelook_back | 4 | 1920x1080 | 0.124 [0.08-0.18] | 0.635 [<=0.80] | 0.02 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B4_freelook_leftup | 4 | 1920x1080 | 0.115 [0.08-0.18] | 0.609 [<=0.80] | 0.13 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B4_freelook_rightdown | 4 | 1920x1080 | 0.178 [0.08-0.18] | 0.402 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_start | 5 | 1920x1080 | 0.160 [0.06-0.16] | 0.702 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B5_middle | 5 | 1920x1080 | 0.103 [0.06-0.16] | 0.419 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_end | 5 | 1920x1080 | 0.196 [0.06-0.16] | 0.502 [<=0.80] | 0.02 [<=0.5] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B5_freelook_back | 5 | 1920x1080 | 0.098 [0.06-0.16] | 0.277 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_freelook_leftup | 5 | 1920x1080 | 0.166 [0.06-0.16] | 0.468 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_freelook_rightdown | 5 | 1920x1080 | 0.071 [0.06-0.16] | 0.409 [<=0.80] | 0.04 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.199 [0.09-0.22] | 0.502 [<=0.95] | 0.04 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| B6_middle | 6 | 1920x1080 | 0.141 [0.09-0.22] | 0.480 [<=0.95] | 0.03 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.160 [0.09-0.22] | 0.546 [<=0.95] | 0.16 [<=1.0] | 12 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.192 [0.09-0.22] | 0.381 [<=0.95] | 0.04 [<=1.0] | 12 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.234 [0.09-0.22] | 0.412 [<=0.95] | 0.00 [<=1.0] | 9 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.119 [0.09-0.22] | 0.300 [<=0.95] | 0.00 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| B7_start | 7 tunnel | 1920x1080 | 0.331 [0.04-0.20] | 0.953 [<=0.98] | 1.13 [<=2.0] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B7_middle | 7 outside | 1920x1080 | 0.417 [0.25-0.45] | 0.506 [<=0.92] | 0.00 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| B7_end | 7 outside | 1920x1080 | 0.395 [0.25-0.45] | 0.897 [<=0.92] | 1.01 [<=1.0] | 10 / 12.0 | 0 | 0 | FAIL: clipped_pct |
| B7_freelook_back | 7 outside | 1920x1080 | 0.317 [0.25-0.45] | 0.382 [<=0.92] | 0.00 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| B7_freelook_leftup | 7 outside | 1920x1080 | 0.375 [0.25-0.45] | 0.482 [<=0.92] | 0.00 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| B7_freelook_rightdown | 7 outside | 1920x1080 | 0.342 [0.25-0.45] | 0.482 [<=0.92] | 0.00 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |

## Failures

- B2_start (beat02_bridge_B2_start.png): mean_luma
- B2_freelook_back (beat02_bridge_B2_freelook_back.png): banding:fog_top30, banding:void_bottom40
- B2_freelook_leftup (beat02_bridge_B2_freelook_leftup.png): banding:void_bottom40
- B4_start (beat04_hall_B4_start.png): mean_luma
- B5_start (beat05_lift_B5_start.png): mean_luma
- B5_end (beat05_lift_B5_end.png): mean_luma
- B7_start (beat07_reveal_B7_start.png): mean_luma
- B7_end (beat07_reveal_B7_end.png): clipped_pct
