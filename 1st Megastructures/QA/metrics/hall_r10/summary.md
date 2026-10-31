# Image metrics - run hall_r10

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\hall_r10`  
Generated: 2026-10-30T12:27:53+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 12, fail: 2, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.342 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.094 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons | 4 | 1920x1080 | 0.166 [0.08-0.18] | 0.501 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.171 [0.08-0.18] | 0.498 [<=0.80] | 0.02 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.154 [0.08-0.18] | 0.513 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.187 [0.05-0.13] | 0.519 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B4_start | 4 | 1920x1080 | 0.069 [0.08-0.18] | 0.330 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B4_middle | 4 | 1920x1080 | 0.150 [0.08-0.18] | 0.637 [<=0.80] | 0.23 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_end | 4 | 1920x1080 | 0.152 [0.08-0.18] | 0.714 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_back | 4 | 1920x1080 | 0.138 [0.08-0.18] | 0.423 [<=0.80] | 0.01 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_leftup | 4 | 1920x1080 | 0.127 [0.08-0.18] | 0.645 [<=0.80] | 0.02 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B4_freelook_rightdown | 4 | 1920x1080 | 0.175 [0.08-0.18] | 0.402 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- B2_start (beat02_bridge_B2_start.png): mean_luma
- B4_start (beat04_hall_B4_start.png): mean_luma
