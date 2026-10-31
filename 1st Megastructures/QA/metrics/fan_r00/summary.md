# Image metrics - run fan_r00

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\fan_r00`  
Generated: 2026-10-30T11:55:22+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 13, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.151 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 9 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.150 [0.06-0.16] | 0.472 [<=0.80] | 0.01 [<=0.5] | 10 / 24.0 | 0 | 0 | PASS |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.156 [0.06-0.16] | 0.475 [<=0.80] | 0.00 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.139 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 8 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.216 [0.09-0.22] | 0.498 [<=0.95] | 0.07 [<=1.0] | 7 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.216 [0.09-0.22] | 0.498 [<=0.95] | 0.07 [<=1.0] | 7 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.224 [0.09-0.22] | 0.498 [<=0.95] | 0.08 [<=1.0] | 7 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.195 [0.09-0.22] | 0.498 [<=0.95] | 0.05 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.199 [0.09-0.22] | 0.502 [<=0.95] | 0.05 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| B6_middle | 6 | 1920x1080 | 0.141 [0.09-0.22] | 0.481 [<=0.95] | 0.03 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.160 [0.09-0.22] | 0.547 [<=0.95] | 0.16 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.192 [0.09-0.22] | 0.381 [<=0.95] | 0.04 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.234 [0.09-0.22] | 0.412 [<=0.95] | 0.00 [<=1.0] | 6 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.119 [0.09-0.22] | 0.300 [<=0.95] | 0.00 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |

## Failures

- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
