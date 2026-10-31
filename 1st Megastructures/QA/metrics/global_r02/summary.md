# Image metrics - run global_r02

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r02`  
Generated: 2026-10-30T10:02:17+01:00 by image_metrics.py 1.0.0  
Shots: 23, pass: 17, fail: 6, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.098 [0.05-0.13] | 0.412 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.098 [0.05-0.13] | 0.413 [<=0.70] | 0.01 [<=0.3] | 11 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.099 [0.05-0.13] | 0.413 [<=0.70] | 0.01 [<=0.3] | 11 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.096 [0.05-0.13] | 0.409 [<=0.70] | 0.01 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.125 [0.05-0.13] | 0.668 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.124 [0.05-0.13] | 0.669 [<=0.70] | 0.00 [<=0.3] | 10 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.134 [0.05-0.13] | 0.673 [<=0.70] | 0.00 [<=0.3] | 13 / 11.7 | 0 | 0 | FAIL: mean_luma, banding:void_bottom40 |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.107 [0.05-0.13] | 0.656 [<=0.70] | 0.00 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.127 [0.06-0.15] | 0.305 [<=0.70] | 0.04 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.127 [0.06-0.15] | 0.304 [<=0.70] | 0.04 [<=0.3] | 6 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.128 [0.06-0.15] | 0.307 [<=0.70] | 0.04 [<=0.3] | 6 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.121 [0.06-0.15] | 0.298 [<=0.70] | 0.03 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.132 [0.06-0.16] | 0.564 [<=0.80] | 0.01 [<=0.5] | 12 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.132 [0.06-0.16] | 0.555 [<=0.80] | 0.01 [<=0.5] | 13 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.128 [0.06-0.16] | 0.468 [<=0.80] | 0.01 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.146 [0.06-0.16] | 0.610 [<=0.80] | 0.01 [<=0.5] | 15 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_bottom |
| KS5b_lift_fan | 5 | 1920x1080 | 0.322 [0.06-0.16] | 0.812 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.321 [0.06-0.16] | 0.815 [<=0.80] | 0.01 [<=0.5] | 13 / 24.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.327 [0.06-0.16] | 0.815 [<=0.80] | 0.00 [<=0.5] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.307 [0.06-0.16] | 0.800 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.100 [0.06-0.16] | 0.503 [<=0.80] | 0.00 [<=0.5] | 9 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.098 [0.06-0.16] | 0.507 [<=0.80] | 0.00 [<=0.5] | 8 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.111 [0.06-0.16] | 0.482 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): mean_luma, banding:void_bottom40
- KS5a_lift_city_21x9 (beat05_lift_KS5a_lift_city_21x9.png): banding:shaft_fog_bottom
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma, p99_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma, p99_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma, p99_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
