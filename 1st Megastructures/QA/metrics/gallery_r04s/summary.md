# Image metrics - run gallery_r04s

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\gallery_r04s`  
Generated: 2026-10-30T10:37:08+01:00 by image_metrics.py 1.0.0  
Shots: 12, pass: 11, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.143 [0.10-0.20] | 0.444 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.136 [0.10-0.20] | 0.488 [<=0.90] | 0.00 [<=0.3] | 8 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.161 [0.10-0.20] | 0.375 [<=0.90] | 0.00 [<=0.3] | 9 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway | 1 | 1920x1080 | 0.163 [0.10-0.20] | 0.772 [<=0.90] | 0.02 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.170 [0.10-0.20] | 0.776 [<=0.90] | 0.02 [<=0.3] | 11 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.145 [0.10-0.20] | 0.764 [<=0.90] | 0.01 [<=0.3] | 11 / 12.0 | 0 | 0 | PASS |
| B1_start | 1 | 1920x1080 | 0.142 [0.10-0.20] | 0.428 [<=0.90] | 0.00 [<=0.3] | 12 / 12.0 | 0 | 0 | PASS |
| B1_middle | 1 | 1920x1080 | 0.098 [0.10-0.20] | 0.399 [<=0.90] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | FAIL: mean_luma |
| B1_end | 1 | 1920x1080 | 0.183 [0.10-0.20] | 0.593 [<=0.90] | 0.24 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| B1_freelook_back | 1 | 1920x1080 | 0.104 [0.10-0.20] | 0.399 [<=0.90] | 0.00 [<=0.3] | 10 / 12.0 | 0 | 0 | PASS |
| B1_freelook_leftup | 1 | 1920x1080 | 0.041 [0.10-0.20] | 0.177 [<=0.90] | 0.00 [<=0.3] | 7 / 12.0 | 0 | 0 | PASS |
| B1_freelook_rightdown | 1 | 1920x1080 | 0.046 [0.10-0.20] | 0.077 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- B1_middle (beat01_gallery_B1_middle.png): mean_luma
