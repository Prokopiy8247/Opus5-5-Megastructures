# Image metrics - run lift_r01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\lift_r01`  
Generated: 2026-10-30T12:23:25+01:00 by image_metrics.py 1.0.0  
Shots: 20, pass: 16, fail: 4, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.096 [0.05-0.13] | 0.340 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.097 [0.05-0.13] | 0.342 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.094 [0.05-0.13] | 0.335 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.105 [0.05-0.13] | 0.618 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.105 [0.05-0.13] | 0.619 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.112 [0.05-0.13] | 0.624 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.090 [0.05-0.13] | 0.590 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.141 [0.06-0.16] | 0.432 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.141 [0.06-0.16] | 0.428 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.142 [0.06-0.16] | 0.428 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.138 [0.06-0.16] | 0.432 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.193 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.198 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.177 [0.06-0.16] | 0.479 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.101 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.101 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.102 [0.06-0.16] | 0.289 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_rightdown | 2 | 1920x1080 | 0.088 [0.05-0.13] | 0.246 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
