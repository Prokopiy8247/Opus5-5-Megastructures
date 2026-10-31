# Image metrics - run fan_r11

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\fan_r11`  
Generated: 2026-10-30T14:41:19+01:00 by image_metrics.py 1.0.0  
Shots: 21, pass: 15, fail: 6, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.201 [0.06-0.16] | 0.726 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.201 [0.06-0.16] | 0.728 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.210 [0.06-0.16] | 0.728 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.176 [0.06-0.16] | 0.722 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan | 6 | 1920x1080 | 0.214 [0.09-0.22] | 0.526 [<=0.95] | 0.06 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.215 [0.09-0.22] | 0.527 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.223 [0.09-0.22] | 0.527 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma (warn: stipple_tiles) |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.192 [0.09-0.22] | 0.523 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.290 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.285 [0.25-0.45] | 0.383 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.307 [0.25-0.45] | 0.386 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.189 [0.09-0.22] | 0.520 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: black_runs (warn: stipple_tiles) |
| B6_middle | 6 | 1920x1080 | 0.136 [0.09-0.22] | 0.531 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.106 [0.04-0.22] | 0.326 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.187 [0.09-0.22] | 0.371 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.242 [0.09-0.22] | 0.479 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.111 [0.09-0.22] | 0.303 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p0 | 6 | 1920x1080 | 0.173 [0.09-0.22] | 0.369 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p1 | 6 | 1920x1080 | 0.166 [0.09-0.22] | 0.338 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p2 | 6 | 1920x1080 | 0.182 [0.09-0.22] | 0.346 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p3 | 6 | 1920x1080 | 0.192 [0.09-0.22] | 0.370 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
- B6_start (beat06_fan_B6_start.png): black_runs
