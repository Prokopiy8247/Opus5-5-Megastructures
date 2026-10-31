# Image metrics - run beams_r03

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\beams_r03`  
Generated: 2026-10-30T10:54:20+01:00 by image_metrics.py 1.0.0  
Shots: 17, pass: 13, fail: 4, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.151 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.151 [0.06-0.16] | 0.472 [<=0.80] | 0.01 [<=0.5] | 16 / 24.0 | 0 | 0 | PASS |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.156 [0.06-0.16] | 0.475 [<=0.80] | 0.00 [<=0.5] | 10 / 11.7 | 0 | 0 | PASS |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.139 [0.06-0.16] | 0.472 [<=0.80] | 0.00 [<=0.5] | 13 / 12.0 | 0 | 0 | FAIL: banding:shaft_fog_bottom |
| KS6_gantry_fan | 6 | 1920x1080 | 0.204 [0.09-0.22] | 0.522 [<=0.95] | 0.08 [<=1.0] | 9 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.204 [0.09-0.22] | 0.523 [<=0.95] | 0.08 [<=1.0] | 12 / 24.0 | 0 | 0 | PASS |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.213 [0.09-0.22] | 0.522 [<=0.95] | 0.09 [<=1.0] | 10 / 11.7 | 0 | 0 | PASS |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.179 [0.09-0.22] | 0.522 [<=0.95] | 0.06 [<=1.0] | 10 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.275 [0.25-0.45] | 0.377 [<=0.92] | 0.00 [<=1.0] | 14 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.270 [0.25-0.45] | 0.374 [<=0.92] | 0.00 [<=1.0] | 16 / 11.7 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.293 [0.25-0.45] | 0.377 [<=0.92] | 0.00 [<=1.0] | 16 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| B6_start | 6 | 1920x1080 | 0.184 [0.09-0.22] | 0.522 [<=0.95] | 0.08 [<=1.0] | 9 / 12.0 | 0 | 0 | PASS |
| B6_middle | 6 | 1920x1080 | 0.119 [0.09-0.22] | 0.497 [<=0.95] | 0.08 [<=1.0] | 12 / 12.0 | 0 | 0 | PASS |
| B6_end | 6 | 1920x1080 | 0.139 [0.09-0.22] | 0.551 [<=0.95] | 0.18 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| B6_freelook_back | 6 | 1920x1080 | 0.176 [0.09-0.22] | 0.384 [<=0.95] | 0.06 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| B6_freelook_leftup | 6 | 1920x1080 | 0.224 [0.09-0.22] | 0.423 [<=0.95] | 0.00 [<=1.0] | 8 / 12.0 | 0 | 0 | PASS |
| B6_freelook_rightdown | 6 | 1920x1080 | 0.094 [0.09-0.22] | 0.292 [<=0.95] | 0.01 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |

## Failures

- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): banding:shaft_fog_bottom
- KS7d_look_back (beat07_reveal_KS7d_look_back.png): banding:sky_haze_top40
- KS7d_look_back_16x10 (beat07_reveal_KS7d_look_back_16x10.png): banding:sky_haze_top40
- KS7d_look_back_21x9 (beat07_reveal_KS7d_look_back_21x9.png): banding:sky_haze_top40
