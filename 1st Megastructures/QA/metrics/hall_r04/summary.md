# Image metrics - run hall_r04

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\hall_r04`  
Generated: 2026-10-30T10:35:11+01:00 by image_metrics.py 1.0.0  
Shots: 9, pass: 8, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS4_hall_pistons | 4 | 1920x1080 | 0.108 [0.08-0.18] | 0.626 [<=0.80] | 0.33 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| KS4_hall_pistons_16x10 | 4 | 1680x1050 | 0.111 [0.08-0.18] | 0.647 [<=0.80] | 0.37 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS4_hall_pistons_21x9 | 4 | 2560x1080 | 0.098 [0.08-0.18] | 0.617 [<=0.80] | 0.26 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B4_start | 4 | 1920x1080 | 0.032 [0.08-0.18] | 0.339 [<=0.80] | 0.02 [<=0.5] | 12 / 12.0 | 0 | 1 | FAIL: nan_like_pixels, mean_luma |
| B4_middle | 4 | 1920x1080 | 0.110 [0.08-0.18] | 0.634 [<=0.80] | 0.36 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B4_end | 4 | 1920x1080 | 0.152 [0.08-0.18] | 0.721 [<=0.80] | 0.01 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B4_freelook_back | 4 | 1920x1080 | 0.088 [0.08-0.18] | 0.653 [<=0.80] | 0.21 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B4_freelook_leftup | 4 | 1920x1080 | 0.076 [0.08-0.18] | 0.624 [<=0.80] | 0.19 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B4_freelook_rightdown | 4 | 1920x1080 | 0.144 [0.08-0.18] | 0.387 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |

## Failures

- B4_start (beat04_hall_B4_start.png): nan_like_pixels, mean_luma
