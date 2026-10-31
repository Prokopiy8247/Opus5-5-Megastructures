# Image metrics - run bridge_r03

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\bridge_r03`  
Generated: 2026-10-30T10:53:18+01:00 by image_metrics.py 1.0.0  
Shots: 12, pass: 11, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.091 [0.05-0.13] | 0.335 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.091 [0.05-0.13] | 0.336 [<=0.70] | 0.01 [<=0.3] | 12 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.092 [0.05-0.13] | 0.339 [<=0.70] | 0.01 [<=0.3] | 10 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.089 [0.05-0.13] | 0.327 [<=0.70] | 0.01 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.107 [0.05-0.13] | 0.597 [<=0.70] | 0.00 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.106 [0.05-0.13] | 0.600 [<=0.70] | 0.00 [<=0.3] | 11 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.114 [0.05-0.13] | 0.604 [<=0.70] | 0.00 [<=0.3] | 13 / 11.7 | 0 | 0 | FAIL: banding:void_bottom40 |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.093 [0.05-0.13] | 0.582 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.130 [0.06-0.15] | 0.442 [<=0.70] | 0.00 [<=0.3] | 8 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.130 [0.06-0.15] | 0.442 [<=0.70] | 0.00 [<=0.3] | 7 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.133 [0.06-0.15] | 0.446 [<=0.70] | 0.00 [<=0.3] | 8 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.123 [0.06-0.15] | 0.428 [<=0.70] | 0.00 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): banding:void_bottom40
