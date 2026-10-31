# Image metrics - run player_r13

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\player_r13`  
Generated: 2026-10-30T15:04:11+01:00 by image_metrics.py 1.0.0  
Shots: 7, pass: 6, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2b_bridge_down | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.459 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.459 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.102 [0.05-0.13] | 0.464 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: side_strips |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.082 [0.05-0.13] | 0.437 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down | 5 | 1920x1080 | 0.088 [0.06-0.16] | 0.203 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.087 [0.06-0.16] | 0.207 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.092 [0.06-0.16] | 0.240 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): side_strips
