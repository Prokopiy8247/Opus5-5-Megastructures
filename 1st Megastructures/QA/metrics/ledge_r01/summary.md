# Image metrics - run ledge_r01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\ledge_r01`  
Generated: 2026-10-30T12:31:34+01:00 by image_metrics.py 1.0.0  
Shots: 20, pass: 17, fail: 3, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.342 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.094 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.150 [0.06-0.15] | 0.497 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.150 [0.06-0.15] | 0.497 [<=0.70] | 0.01 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.154 [0.06-0.15] | 0.501 [<=0.70] | 0.01 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.137 [0.06-0.15] | 0.485 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.187 [0.05-0.13] | 0.519 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B2_middle | 2 | 1920x1080 | 0.108 [0.05-0.13] | 0.370 [<=0.70] | 0.04 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_end | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.347 [<=0.70] | 0.11 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_back | 2 | 1920x1080 | 0.153 [0.05-0.13] | 0.273 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_leftup | 2 | 1920x1080 | 0.141 [0.05-0.13] | 0.230 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_rightdown | 2 | 1920x1080 | 0.088 [0.05-0.13] | 0.246 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_start | 3 | 1920x1080 | 0.086 [0.06-0.15] | 0.259 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_middle | 3 | 1920x1080 | 0.162 [0.06-0.15] | 0.354 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B3_end | 3 | 1920x1080 | 0.067 [0.06-0.15] | 0.329 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_freelook_back | 3 | 1920x1080 | 0.184 [0.06-0.15] | 0.708 [<=0.70] | 0.48 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_freelook_leftup | 3 | 1920x1080 | 0.115 [0.06-0.15] | 0.493 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_freelook_rightdown | 3 | 1920x1080 | 0.188 [0.06-0.15] | 0.324 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS3_column_up_16x10 (beat03_ledge_KS3_column_up_16x10.png): mean_luma
- B2_start (beat02_bridge_B2_start.png): mean_luma
- B3_middle (beat03_ledge_B3_middle.png): mean_luma
