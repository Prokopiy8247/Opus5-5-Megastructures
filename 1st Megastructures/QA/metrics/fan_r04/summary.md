# Image metrics - run fan_r04

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\fan_r04`  
Generated: 2026-10-30T12:57:39+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 11, fail: 3, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.167 [0.06-0.16] | 0.471 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.167 [0.06-0.16] | 0.471 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.172 [0.06-0.16] | 0.471 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma (warn: stipple_tiles) |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.153 [0.06-0.16] | 0.471 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.211 [0.09-0.22] | 0.490 [<=0.95] | 0.07 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.212 [0.09-0.22] | 0.490 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.219 [0.09-0.22] | 0.490 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | PASS |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.190 [0.09-0.22] | 0.489 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.188 [0.09-0.22] | 0.490 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_middle | 6 | 1920x1080 | 0.133 [0.09-0.22] | 0.451 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.107 [0.04-0.22] | 0.327 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.187 [0.09-0.22] | 0.372 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.241 [0.09-0.22] | 0.388 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.111 [0.09-0.22] | 0.303 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
