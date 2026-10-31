# Image metrics - run fan_r12

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\fan_r12`  
Generated: 2026-10-30T14:55:45+01:00 by image_metrics.py 1.0.0  
Shots: 21, pass: 15, fail: 6, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.200 [0.06-0.16] | 0.726 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.200 [0.06-0.16] | 0.726 [<=0.80] | 0.00 [<=0.5] | 0 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.210 [0.06-0.16] | 0.726 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.176 [0.06-0.16] | 0.722 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan | 6 | 1920x1080 | 0.214 [0.09-0.22] | 0.503 [<=0.95] | 0.06 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.214 [0.09-0.22] | 0.504 [<=0.95] | 0.07 [<=1.0] | 0 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.221 [0.09-0.22] | 0.504 [<=0.95] | 0.07 [<=1.0] | 0 / 11.7 | 0 | 0 | FAIL: mean_luma (warn: stipple_tiles) |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.192 [0.09-0.22] | 0.500 [<=0.95] | 0.05 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.292 [0.25-0.45] | 0.496 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.288 [0.25-0.45] | 0.503 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.309 [0.25-0.45] | 0.394 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| B6_start | 6 | 1920x1080 | 0.188 [0.09-0.22] | 0.500 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: black_runs (warn: stipple_tiles) |
| B6_middle | 6 | 1920x1080 | 0.134 [0.09-0.22] | 0.508 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.106 [0.04-0.22] | 0.319 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.186 [0.09-0.22] | 0.367 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.241 [0.09-0.22] | 0.457 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.110 [0.09-0.22] | 0.300 [<=0.95] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p0 | 6 | 1920x1080 | 0.172 [0.09-0.22] | 0.365 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p1 | 6 | 1920x1080 | 0.165 [0.09-0.22] | 0.334 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p2 | 6 | 1920x1080 | 0.181 [0.09-0.22] | 0.342 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p3 | 6 | 1920x1080 | 0.191 [0.09-0.22] | 0.367 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
- B6_start (beat06_fan_B6_start.png): black_runs
