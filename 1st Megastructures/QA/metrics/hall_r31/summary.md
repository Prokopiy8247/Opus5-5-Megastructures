# Image metrics - run hall_r31

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\hall_r31`  
Generated: 2026-10-30T16:46:29+01:00 by image_metrics.py 1.0.0  
Shots: 5, pass: 4, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.305 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS4_hall_pistons | 4 | 1920x1080 | 0.168 [0.08-0.18] | 0.498 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.189 [0.05-0.20] | 0.474 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_middle | 2 | 1920x1080 | 0.107 [0.05-0.13] | 0.334 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B4_middle | 4 | 1920x1080 | 0.144 [0.08-0.18] | 0.564 [<=0.80] | 0.01 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
