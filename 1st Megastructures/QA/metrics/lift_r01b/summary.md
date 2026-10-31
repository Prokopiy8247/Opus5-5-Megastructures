# Image metrics - run lift_r01b

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\lift_r01b`  
Generated: 2026-10-30T12:29:59+01:00 by image_metrics.py 1.0.0  
Shots: 7, pass: 4, fail: 3, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS6_gantry_fan | 6 | 1920x1080 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.233 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.241 [0.09-0.22] | 0.482 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.209 [0.09-0.22] | 0.482 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B1_end | 1 | 1920x1080 | 0.167 [0.10-0.20] | 0.567 [<=0.90] | 0.24 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B5_middle | 5 | 1920x1080 | 0.109 [0.06-0.16] | 0.391 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B5_freelook_back | 5 | 1920x1080 | 0.103 [0.06-0.16] | 0.250 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- KS6_gantry_fan_4k (beat06_fan_KS6_gantry_fan_4k.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
