# Image metrics - run hall_r11

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\hall_r11`  
Generated: 2026-10-30T12:32:36+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 11, fail: 3, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.342 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.094 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.174 [0.08-0.18] | 0.511 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.180 [0.08-0.18] | 0.509 [<=0.80] | 0.02 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.160 [0.08-0.18] | 0.521 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.187 [0.05-0.13] | 0.519 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B4_start | 4 | 1920x1080 | 0.070 [0.08-0.18] | 0.342 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B4_middle | 4 | 1920x1080 | 0.153 [0.08-0.18] | 0.638 [<=0.80] | 0.23 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_end | 4 | 1920x1080 | 0.152 [0.08-0.18] | 0.714 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_back | 4 | 1920x1080 | 0.161 [0.08-0.18] | 0.484 [<=0.80] | 0.01 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_leftup | 4 | 1920x1080 | 0.125 [0.08-0.18] | 0.645 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_rightdown | 4 | 1920x1080 | 0.189 [0.08-0.18] | 0.402 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS4_hall_pistons_16x10 (beat04_hall_KS4_hall_pistons_16x10.png): mean_luma
- B2_start (beat02_bridge_B2_start.png): mean_luma
- B4_start (beat04_hall_B4_start.png): mean_luma
