# Image metrics - run gallery_r13

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\gallery_r13`  
Generated: 2026-10-30T12:59:22+01:00 by image_metrics.py 1.0.0  
Shots: 18, pass: 14, fail: 4, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.196 [0.10-0.20] | 0.496 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.189 [0.10-0.20] | 0.535 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.215 [0.10-0.20] | 0.430 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.173 [0.05-0.20] | 0.612 [<=0.90] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: doorway_luma |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.179 [0.05-0.20] | 0.648 [<=0.90] | 0.01 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: doorway_luma |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.156 [0.05-0.20] | 0.560 [<=0.90] | 0.01 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: doorway_luma |
| B1_start | 1 | 1920x1080 | 0.195 [0.10-0.20] | 0.483 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_middle | 1 | 1920x1080 | 0.155 [0.10-0.20] | 0.457 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_end | 1 | 1920x1080 | 0.163 [0.05-0.20] | 0.561 [<=0.90] | 0.17 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_back | 1 | 1920x1080 | 0.160 [0.10-0.20] | 0.458 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_leftup | 1 | 1920x1080 | 0.101 [0.10-0.20] | 0.220 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_rightdown | 1 | 1920x1080 | 0.102 [0.10-0.20] | 0.131 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.179 [0.05-0.20] | 0.503 [<=0.70] | 0.02 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_middle | 2 | 1920x1080 | 0.107 [0.05-0.13] | 0.375 [<=0.70] | 0.04 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_end | 2 | 1920x1080 | 0.095 [0.05-0.15] | 0.335 [<=0.70] | 0.11 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_back | 2 | 1920x1080 | 0.160 [0.05-0.13] | 0.281 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_leftup | 2 | 1920x1080 | 0.141 [0.05-0.13] | 0.217 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_freelook_rightdown | 2 | 1920x1080 | 0.088 [0.05-0.13] | 0.254 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
- KS1b_doorway (beat01_gallery_KS1b_doorway.png): doorway_luma
- KS1b_doorway_16x10 (beat01_gallery_KS1b_doorway_16x10.png): doorway_luma
- KS1b_doorway_21x9 (beat01_gallery_KS1b_doorway_21x9.png): doorway_luma
