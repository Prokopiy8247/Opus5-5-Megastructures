# Image metrics - run global_r23

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r23`  
Generated: 2026-10-30T14:42:54+01:00 by image_metrics.py 1.0.0  
Shots: 9, pass: 4, fail: 5, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.095 [0.05-0.13] | 0.339 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.095 [0.05-0.13] | 0.339 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.343 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: wall_macro_residual |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.092 [0.05-0.13] | 0.332 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2b_bridge_down | 2 | 1920x1080 | 0.100 [0.05-0.13] | 0.553 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.100 [0.05-0.13] | 0.554 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.107 [0.05-0.13] | 0.561 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: side_strips (warn: amber_coverage_pct) |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.085 [0.05-0.13] | 0.531 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.195 [0.05-0.20] | 0.485 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS2a_bridge_ahead_4k (beat02_bridge_KS2a_bridge_ahead_4k.png): wall_macro_residual
- KS2a_bridge_ahead_16x10 (beat02_bridge_KS2a_bridge_ahead_16x10.png): wall_macro_residual
- KS2a_bridge_ahead_21x9 (beat02_bridge_KS2a_bridge_ahead_21x9.png): wall_macro_residual
- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): side_strips
