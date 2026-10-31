# Image metrics - run gallery_r23

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\gallery_r23`  
Generated: 2026-10-30T15:06:12+01:00 by image_metrics.py 1.0.0  
Shots: 13, pass: 10, fail: 3, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.198 [0.10-0.20] | 0.495 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_board_steps |
| KS1_gallery_start_16x10 | 1 | 1680x1050 | 0.191 [0.10-0.20] | 0.512 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | FAIL: wall_board_steps |
| KS1_gallery_start_21x9 | 1 | 2560x1080 | 0.218 [0.10-0.20] | 0.441 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: mean_luma |
| KS1b_doorway | 1 | 1920x1080 | 0.175 [0.05-0.20] | 0.576 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway_16x10 | 1 | 1680x1050 | 0.181 [0.05-0.20] | 0.579 [<=0.90] | 0.00 [<=0.3] | 0 / 11.7 | 0 | 0 | PASS |
| KS1b_doorway_21x9 | 1 | 2560x1080 | 0.158 [0.05-0.20] | 0.560 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_start | 1 | 1920x1080 | 0.195 [0.10-0.20] | 0.476 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_middle | 1 | 1920x1080 | 0.154 [0.10-0.20] | 0.449 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_end | 1 | 1920x1080 | 0.166 [0.05-0.20] | 0.575 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_back | 1 | 1920x1080 | 0.160 [0.10-0.20] | 0.456 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_leftup | 1 | 1920x1080 | 0.100 [0.10-0.20] | 0.220 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B1_freelook_rightdown | 1 | 1920x1080 | 0.104 [0.10-0.20] | 0.132 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| B2_start | 2 | 1920x1080 | 0.194 [0.05-0.20] | 0.485 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |

## Failures

- KS1_gallery_start (beat01_gallery_KS1_gallery_start.png): wall_board_steps
- KS1_gallery_start_16x10 (beat01_gallery_KS1_gallery_start_16x10.png): wall_board_steps
- KS1_gallery_start_21x9 (beat01_gallery_KS1_gallery_start_21x9.png): mean_luma
