# Image metrics - run gallery_r32

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\gallery_r32`  
Generated: 2026-10-30T17:00:54+01:00 by image_metrics.py 1.0.0  
Shots: 8, pass: 7, fail: 1, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.198 [0.10-0.20] | 0.492 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.190 [0.10-0.20] | 0.511 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.217 [0.10-0.20] | 0.438 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.173 [0.05-0.20] | 0.575 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.180 [0.05-0.20] | 0.579 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.157 [0.05-0.20] | 0.560 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_end | 1 | 1920x1080 | 0.161 [0.05-0.20] | 0.571 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.181 [0.05-0.20] | 0.443 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
