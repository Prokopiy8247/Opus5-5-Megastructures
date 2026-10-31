# Image metrics - run global_r05

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\global_r05`  
Generated: 2026-10-30T10:10:03+01:00 by image_metrics.py 1.0.0  
Shots: 41, pass: 29, fail: 12, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.091 [0.05-0.13] | 0.382 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_4k | 2 | 3840x2160 | 0.091 [0.05-0.13] | 0.385 [<=0.70] | 0.01 [<=0.3] | 11 / 24.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead_16x10 | 2 | 1680x1050 | 0.092 [0.05-0.13] | 0.385 [<=0.70] | 0.01 [<=0.3] | 10 / 11.7 | 0 | 0 | PASS |
| KS2a_bridge_ahead_21x9 | 2 | 2560x1080 | 0.089 [0.05-0.13] | 0.381 [<=0.70] | 0.01 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.116 [0.05-0.13] | 0.633 [<=0.70] | 0.00 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.114 [0.05-0.13] | 0.634 [<=0.70] | 0.00 [<=0.3] | 11 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.123 [0.05-0.13] | 0.637 [<=0.70] | 0.00 [<=0.3] | 13 / 11.7 | 0 | 0 | FAIL: banding:void_bottom40 |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.099 [0.05-0.13] | 0.620 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.120 [0.06-0.15] | 0.287 [<=0.70] | 0.03 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| KS3_column_up_4k | 3 | 3840x2160 | 0.119 [0.06-0.15] | 0.286 [<=0.70] | 0.03 [<=0.3] | 6 / 24.0 | 0 | 0 | PASS |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.121 [0.06-0.15] | 0.289 [<=0.70] | 0.04 [<=0.3] | 6 / 11.7 | 0 | 0 | PASS |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.115 [0.06-0.15] | 0.281 [<=0.70] | 0.03 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.125 [0.06-0.16] | 0.550 [<=0.80] | 0.00 [<=0.5] | 13 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_bottom |
| KS5a_lift_city_4k | 5 | 3840x2160 | 0.124 [0.06-0.16] | 0.542 [<=0.80] | 0.00 [<=0.5] | 11 / 24.0 | 0 | 0 | PASS |
| KS5a_lift_city_16x10 | 5 | 1680x1050 | 0.120 [0.06-0.16] | 0.455 [<=0.80] | 0.00 [<=0.5] | 15 / 11.7 | 0 | 0 | FAIL: banding:shaft_fog_bottom |
| KS5a_lift_city_21x9 | 5 | 2560x1080 | 0.138 [0.06-0.16] | 0.595 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.311 [0.06-0.16] | 0.800 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.310 [0.06-0.16] | 0.803 [<=0.80] | 0.01 [<=0.5] | 15 / 24.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.316 [0.06-0.16] | 0.802 [<=0.80] | 0.00 [<=0.5] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma, p99_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.296 [0.06-0.16] | 0.785 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5c_lift_down | 5 | 1920x1080 | 0.093 [0.06-0.16] | 0.490 [<=0.80] | 0.00 [<=0.5] | 9 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.090 [0.06-0.16] | 0.494 [<=0.80] | 0.00 [<=0.5] | 9 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.103 [0.06-0.16] | 0.468 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.161 [0.05-0.13] | 0.520 [<=0.70] | 0.02 [<=0.3] | 8 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B2_middle | 2 | 1920x1080 | 0.097 [0.05-0.13] | 0.398 [<=0.70] | 0.05 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| B2_end | 2 | 1920x1080 | 0.093 [0.05-0.13] | 0.279 [<=0.70] | 0.11 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| B2_freelook_back | 2 | 1920x1080 | 0.139 [0.05-0.13] | 0.242 [<=0.70] | 0.00 [<=0.3] | 14 / 12.0 | 0 | 0 | FAIL: banding:fog_top30, banding:void_bottom40 (warn: mean_luma) |
| B2_freelook_leftup | 2 | 1920x1080 | 0.132 [0.05-0.13] | 0.191 [<=0.70] | 0.00 [<=0.3] | 12 / 12.0 | 0 | 0 | PASS |
| B2_freelook_rightdown | 2 | 1920x1080 | 0.078 [0.05-0.13] | 0.226 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B3_start | 3 | 1920x1080 | 0.094 [0.06-0.15] | 0.271 [<=0.70] | 0.06 [<=0.3] | 5 / 12.0 | 0 | 0 | PASS |
| B3_middle | 3 | 1920x1080 | 0.144 [0.06-0.15] | 0.290 [<=0.70] | 0.04 [<=0.3] | 6 / 12.0 | 0 | 0 | PASS |
| B3_end | 3 | 1920x1080 | 0.044 [0.06-0.15] | 0.117 [<=0.70] | 0.02 [<=0.3] | 8 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B3_freelook_back | 3 | 1920x1080 | 0.204 [0.06-0.15] | 0.995 [<=0.70] | 1.19 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B3_freelook_leftup | 3 | 1920x1080 | 0.147 [0.06-0.15] | 0.310 [<=0.70] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| B3_freelook_rightdown | 3 | 1920x1080 | 0.183 [0.06-0.15] | 0.329 [<=0.70] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| B5_start | 5 | 1920x1080 | 0.159 [0.06-0.16] | 0.686 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_middle | 5 | 1920x1080 | 0.105 [0.06-0.16] | 0.413 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B5_end | 5 | 1920x1080 | 0.424 [0.06-0.16] | 0.922 [<=0.80] | 0.36 [<=0.5] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma, p99_luma |
| B5_freelook_back | 5 | 1920x1080 | 0.106 [0.06-0.16] | 0.486 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| B5_freelook_leftup | 5 | 1920x1080 | 0.195 [0.06-0.16] | 0.740 [<=0.80] | 0.00 [<=0.5] | 10 / 12.0 | 0 | 0 | PASS |
| B5_freelook_rightdown | 5 | 1920x1080 | 0.073 [0.06-0.16] | 0.406 [<=0.80] | 0.04 [<=0.5] | 14 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_top |

## Failures

- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): banding:void_bottom40
- KS5a_lift_city (beat05_lift_KS5a_lift_city.png): banding:shaft_fog_bottom
- KS5a_lift_city_16x10 (beat05_lift_KS5a_lift_city_16x10.png): banding:shaft_fog_bottom
- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma, p99_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma, p99_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
- B2_start (beat02_bridge_B2_start.png): mean_luma
- B2_freelook_back (beat02_bridge_B2_freelook_back.png): banding:fog_top30, banding:void_bottom40
- B3_end (beat03_ledge_B3_end.png): mean_luma
- B5_end (beat05_lift_B5_end.png): mean_luma, p99_luma
- B5_freelook_rightdown (beat05_lift_B5_freelook_rightdown.png): banding:shaft_fog_top
