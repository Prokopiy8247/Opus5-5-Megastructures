# Image metrics - run fan_r01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\fan_r01`  
Generated: 2026-10-30T12:30:36+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 7, fail: 7, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.198 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.177 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan | 6 | 1920x1080 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.241 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.209 [0.09-0.22] | 0.482 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.205 [0.09-0.22] | 0.523 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_middle | 6 | 1920x1080 | 0.135 [0.09-0.22] | 0.451 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.107 [0.09-0.22] | 0.327 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.195 [0.09-0.22] | 0.373 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.248 [0.09-0.22] | 0.389 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.111 [0.09-0.22] | 0.303 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- KS6_gantry_fan_4k (beat06_fan_KS6_gantry_fan_4k.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
