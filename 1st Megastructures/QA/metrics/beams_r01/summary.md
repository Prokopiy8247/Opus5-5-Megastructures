# Image metrics - run beams_r01

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\beams_r01`  
Generated: 2026-10-30T10:30:34+01:00 by image_metrics.py 1.0.0  
Shots: 11, pass: 2, fail: 9, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS5b_lift_fan | 5 | 1920x1080 | 0.181 [0.06-0.16] | 0.567 [<=0.80] | 0.00 [<=0.5] | 11 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_4k | 5 | 3840x2160 | 0.181 [0.06-0.16] | 0.568 [<=0.80] | 0.01 [<=0.5] | 16 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_16x10 | 5 | 1680x1050 | 0.188 [0.06-0.16] | 0.568 [<=0.80] | 0.00 [<=0.5] | 10 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS5b_lift_fan_21x9 | 5 | 2560x1080 | 0.164 [0.06-0.16] | 0.559 [<=0.80] | 0.00 [<=0.5] | 13 / 12.0 | 0 | 0 | FAIL: mean_luma, banding:shaft_fog_bottom |
| KS6_gantry_fan | 6 | 1920x1080 | 0.230 [0.09-0.22] | 0.628 [<=0.95] | 0.08 [<=1.0] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_4k | 6 | 3840x2160 | 0.230 [0.09-0.22] | 0.628 [<=0.95] | 0.08 [<=1.0] | 10 / 24.0 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_16x10 | 6 | 1680x1050 | 0.241 [0.09-0.22] | 0.628 [<=0.95] | 0.09 [<=1.0] | 11 / 11.7 | 0 | 0 | FAIL: mean_luma |
| KS6_gantry_fan_21x9 | 6 | 2560x1080 | 0.201 [0.09-0.22] | 0.624 [<=0.95] | 0.06 [<=1.0] | 11 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back | 7 outside | 1920x1080 | 0.275 [0.25-0.45] | 0.373 [<=0.92] | 0.00 [<=1.0] | 12 / 12.0 | 0 | 0 | PASS |
| KS7d_look_back_16x10 | 7 outside | 1680x1050 | 0.270 [0.25-0.45] | 0.373 [<=0.92] | 0.00 [<=1.0] | 16 / 11.7 | 0 | 0 | FAIL: banding:sky_haze_top40 |
| KS7d_look_back_21x9 | 7 outside | 2560x1080 | 0.293 [0.25-0.45] | 0.374 [<=0.92] | 0.00 [<=1.0] | 16 / 12.0 | 0 | 0 | FAIL: banding:sky_haze_top40 |

## Failures

- KS5b_lift_fan (beat05_lift_KS5b_lift_fan.png): mean_luma
- KS5b_lift_fan_4k (beat05_lift_KS5b_lift_fan_4k.png): mean_luma
- KS5b_lift_fan_16x10 (beat05_lift_KS5b_lift_fan_16x10.png): mean_luma
- KS5b_lift_fan_21x9 (beat05_lift_KS5b_lift_fan_21x9.png): mean_luma, banding:shaft_fog_bottom
- KS6_gantry_fan (beat06_fan_KS6_gantry_fan.png): mean_luma
- KS6_gantry_fan_4k (beat06_fan_KS6_gantry_fan_4k.png): mean_luma
- KS6_gantry_fan_16x10 (beat06_fan_KS6_gantry_fan_16x10.png): mean_luma
- KS7d_look_back_16x10 (beat07_reveal_KS7d_look_back_16x10.png): banding:sky_haze_top40
- KS7d_look_back_21x9 (beat07_reveal_KS7d_look_back_21x9.png): banding:sky_haze_top40
