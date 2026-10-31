# Image metrics - run global_r31

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r31`  
Generated: 2026-10-30T16:24:05+01:00 by image_metrics.py 1.0.0  
Shots: 13, pass: 5, fail: 8, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.304 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.304 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.305 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.092 [0.05-0.13] | 0.297 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2b_bridge_down | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.451 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.451 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.102 [0.05-0.13] | 0.456 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: side_strips |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.082 [0.05-0.13] | 0.429 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.143 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_4k | 3 | 3840x2160 | 0.143 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.146 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.132 [0.06-0.15] | 0.404 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| B2_start | 2 | 1920x1080 | 0.190 [0.05-0.20] | 0.476 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS2a_bridge_ahead_4k (beat02_bridge_KS2a_bridge_ahead_4k.png): wall_macro_residual
- KS2a_bridge_ahead_21x9 (beat02_bridge_KS2a_bridge_ahead_21x9.png): wall_macro_residual
- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): side_strips
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
- KS3_column_up_4k (beat03_ledge_KS3_column_up_4k.png): pour_lifts
- KS3_column_up_16x10 (beat03_ledge_KS3_column_up_16x10.png): pour_lifts
- KS3_column_up_21x9 (beat03_ledge_KS3_column_up_21x9.png): pour_lifts
