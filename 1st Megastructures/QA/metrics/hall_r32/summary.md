# Image metrics - run hall_r32

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\hall_r32`  
Generated: 2026-10-30T17:04:35+01:00 by image_metrics.py 1.0.0  
Shots: 6, pass: 4, fail: 2, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.305 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS3_column_up | 3 | 1920x1080 | 0.143 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| B2_start | 2 | 1920x1080 | 0.181 [0.05-0.20] | 0.443 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_middle | 2 | 1920x1080 | 0.104 [0.05-0.13] | 0.321 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_end | 2 | 1920x1080 | 0.082 [0.05-0.15] | 0.270 [<=0.70] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B3_start | 3 | 1920x1080 | 0.084 [0.05-0.15] | 0.250 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
