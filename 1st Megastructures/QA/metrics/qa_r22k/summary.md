# Image metrics - run qa_r22k

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\qa_r22k`  
Generated: 2026-10-30T14:53:21+01:00 by image_metrics.py 1.0.0  
Shots: 14, pass: 9, fail: 5, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1b_doorway | 1 | 1920x1080 | 0.174 [0.05-0.20] | 0.626 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.180 [0.05-0.20] | 0.647 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.157 [0.05-0.20] | 0.576 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down | 2 | 1920x1080 | 0.100 [0.05-0.13] | 0.553 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2b_bridge_down_4k | 2 | 3840x2160 | 0.100 [0.05-0.13] | 0.554 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | PASS |
| KS2b_bridge_down_16x10 | 2 | 1680x1050 | 0.107 [0.05-0.13] | 0.561 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: side_strips (warn: amber_coverage_pct) |
| KS2b_bridge_down_21x9 | 2 | 2560x1080 | 0.085 [0.05-0.13] | 0.531 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.142 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_4k | 3 | 3840x2160 | 0.142 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 24.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_16x10 | 3 | 1680x1050 | 0.146 [0.06-0.15] | 0.420 [<=0.70] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS3_column_up_21x9 | 3 | 2560x1080 | 0.131 [0.06-0.15] | 0.408 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS5c_lift_down | 5 | 1920x1080 | 0.088 [0.06-0.16] | 0.201 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down_16x10 | 5 | 1680x1050 | 0.087 [0.06-0.16] | 0.207 [<=0.80] | 0.00 [<=0.5] | 0 / 11.7 | 0 | 0 | PASS |
| KS5c_lift_down_21x9 | 5 | 2560x1080 | 0.092 [0.06-0.16] | 0.242 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS2b_bridge_down_16x10 (beat02_bridge_KS2b_bridge_down_16x10.png): side_strips
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
- KS3_column_up_4k (beat03_ledge_KS3_column_up_4k.png): pour_lifts
- KS3_column_up_16x10 (beat03_ledge_KS3_column_up_16x10.png): pour_lifts
- KS3_column_up_21x9 (beat03_ledge_KS3_column_up_21x9.png): pour_lifts
