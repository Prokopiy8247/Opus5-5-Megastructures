# Image metrics: run03

Capture folder: `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03`  
Scene constants: ThreadConstants.cs  
Images: 80, failing images: 49. Checks: 599 PASS, 67 FAIL, 0 WARN, 16 SKIP, 4 INFO.

**Result: FAIL**

## Summary

| Image | Beat | Size | Mean luma | p1 | Clipped % | Band score | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| beat01_salt_KF1 | 1 | 1920x1080 | 0.763 | 0.524 | 0.0000 | 37.084 | FAIL | banding[sky_above_bank], line[thread] width@1080p, line[thread] monotone 3-level edge, line[thread] stair-step, line[thread] energy variation |
| beat01_salt_KF1_1610 | 1 | 1680x1050 | 0.757 | 0.523 | 0.0000 | - | PASS |  |
| beat01_salt_KF1_219 | 1 | 2560x1080 | 0.775 | 0.527 | 0.0000 | - | PASS |  |
| beat01_salt_KF1_4k | 1 | 3840x2160 | 0.763 | 0.524 | 0.0000 | 102.794 | FAIL | banding[sky_above_bank], line[thread] width@1080p, line[thread] monotone 3-level edge |
| beat02_avenue_KF2a | 2 | 1920x1080 | 0.538 | 0.380 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2a_1610 | 2 | 1680x1050 | 0.546 | 0.380 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2a_219 | 2 | 2560x1080 | 0.526 | 0.381 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2a_4k | 2 | 3840x2160 | 0.538 | 0.380 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2b | 2 | 1920x1080 | 0.687 | 0.245 | 0.0000 | - | FAIL | luma p1 |
| beat02_avenue_KF2b_1610 | 2 | 1680x1050 | 0.702 | 0.239 | 0.0000 | - | FAIL | luma p1 |
| beat02_avenue_KF2b_219 | 2 | 2560x1080 | 0.646 | 0.356 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2b_4k | 2 | 3840x2160 | 0.687 | 0.244 | 0.0000 | - | FAIL | luma p1 |
| beat02_avenue_KF2c | 2 | 1920x1080 | 0.657 | 0.405 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_1610 | 2 | 1680x1050 | 0.658 | 0.404 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_219 | 2 | 2560x1080 | 0.651 | 0.406 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_4k | 2 | 3840x2160 | 0.657 | 0.405 | 0.0000 | - | PASS |  |
| beat03_stairs_KF3 | 3 | 1920x1080 | 0.733 | 0.565 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_1610 | 3 | 1680x1050 | 0.733 | 0.565 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_219 | 3 | 2560x1080 | 0.735 | 0.568 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_4k | 3 | 3840x2160 | 0.733 | 0.565 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_s305 | 3 | 1920x1080 | 0.726 | 0.556 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_KF3_s305_4k | 3 | 3840x2160 | 0.726 | 0.556 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat04_tower_KF4a | 4 | 1920x1080 | 0.672 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_1610 | 4 | 1680x1050 | 0.658 | 0.399 | 0.0000 | - | FAIL | NaN-like pixels |
| beat04_tower_KF4a_219 | 4 | 2560x1080 | 0.703 | 0.400 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_4k | 4 | 3840x2160 | 0.672 | 0.399 | 0.0000 | - | FAIL | NaN-like pixels |
| beat04_tower_KF4b | 4 | 1920x1080 | 0.563 | 0.304 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_1610 | 4 | 1680x1050 | 0.545 | 0.303 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_219 | 4 | 2560x1080 | 0.621 | 0.306 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_4k | 4 | 3840x2160 | 0.563 | 0.303 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide | 4 | 1920x1080 | 0.570 | 0.302 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_1610 | 4 | 1680x1050 | 0.551 | 0.301 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_219 | 4 | 2560x1080 | 0.628 | 0.305 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_4k | 4 | 3840x2160 | 0.570 | 0.302 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5a | 5 | 1920x1080 | 0.785 | 0.353 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5a_1610 | 5 | 1680x1050 | 0.788 | 0.357 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5a_219 | 5 | 2560x1080 | 0.777 | 0.220 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5a_4k | 5 | 3840x2160 | 0.785 | 0.353 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5b | 5 | 1920x1080 | 0.434 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_1610 | 5 | 1680x1050 | 0.420 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_219 | 5 | 2560x1080 | 0.506 | 0.259 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_4k | 5 | 3840x2160 | 0.434 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c | 5 | 1920x1080 | 0.752 | 0.361 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5c_1610 | 5 | 1680x1050 | 0.753 | 0.362 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5c_219 | 5 | 2560x1080 | 0.744 | 0.359 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c_4k | 5 | 3840x2160 | 0.752 | 0.361 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5d | 5 | 1920x1080 | 0.629 | 0.197 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_1610 | 5 | 1680x1050 | 0.630 | 0.206 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_219 | 5 | 2560x1080 | 0.626 | 0.259 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_4k | 5 | 3840x2160 | 0.629 | 0.193 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6a | 6 | 1920x1080 | 0.617 | 0.392 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2 | 6 | 1920x1080 | 0.547 | 0.138 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_1610 | 6 | 1680x1050 | 0.550 | 0.107 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_219 | 6 | 2560x1080 | 0.539 | 0.155 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_4k | 6 | 3840x2160 | 0.548 | 0.138 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_1610 | 6 | 1680x1050 | 0.620 | 0.392 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_219 | 6 | 2560x1080 | 0.611 | 0.143 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_4k | 6 | 3840x2160 | 0.617 | 0.392 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6b | 6 | 1920x1080 | 0.396 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6b_1610 | 6 | 1680x1050 | 0.405 | 0.054 | 0.0000 | - | FAIL | mean luma |
| beat06_clouds_KF6b_219 | 6 | 2560x1080 | 0.359 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6b_4k | 6 | 3840x2160 | 0.396 | 0.054 | 0.0000 | - | PASS |  |
| beat07_top_KF7a | 7 | 1920x1080 | 0.330 | 0.053 | 0.0000 | - | FAIL | NaN-like pixels, mean luma |
| beat07_top_KF7a_1610 | 7 | 1680x1050 | 0.330 | 0.053 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7a_219 | 7 | 2560x1080 | 0.325 | 0.054 | 0.0000 | - | FAIL | NaN-like pixels, mean luma |
| beat07_top_KF7a_4k | 7 | 3840x2160 | 0.330 | 0.053 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7a_s750 | 7 | 1920x1080 | 0.354 | 0.054 | 0.0000 | - | PASS |  |
| beat07_top_KF7a_s750_4k | 7 | 3840x2160 | 0.355 | 0.054 | 0.0000 | - | PASS |  |
| beat07_top_KF7b | 7 | 1920x1080 | 0.422 | 0.115 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7b_1610 | 7 | 1680x1050 | 0.441 | 0.103 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7b_219 | 7 | 2560x1080 | 0.383 | 0.085 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7b_4k | 7 | 3840x2160 | 0.422 | 0.115 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c | 7 | 1920x1080 | 0.621 | 0.337 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_1610 | 7 | 1680x1050 | 0.624 | 0.345 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_219 | 7 | 2560x1080 | 0.596 | 0.141 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_4k | 7 | 3840x2160 | 0.622 | 0.336 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide | 7 | 1920x1080 | 0.615 | 0.283 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_1610 | 7 | 1680x1050 | 0.618 | 0.295 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_219 | 7 | 2560x1080 | 0.586 | 0.138 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_4k | 7 | 3840x2160 | 0.616 | 0.283 | 0.0000 | - | FAIL | mean luma |

## Key-shot coverage (14.10.2: every key shot at 1920x1080 and 3840x2160)

| Key shot | Requested sizes found | Status |
|---|---|---|
| beat01_salt_KF1 | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat02_avenue_KF2a | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat02_avenue_KF2b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat02_avenue_KF2c | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat03_stairs_KF3 | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat04_tower_KF4a | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat04_tower_KF4b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat05_spiral_KF5a | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat05_spiral_KF5b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat05_spiral_KF5c | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat05_spiral_KF5d | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat06_clouds_KF6a | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat06_clouds_KF6a2 | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat06_clouds_KF6b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat07_top_KF7a | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat07_top_KF7b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat07_top_KF7c | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |

## Details

### beat01_salt_KF1 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat01_salt_KF1.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9445 | < 0.98 | max pixel 0.949, 648 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2044 px excluded |
| luma p1 | PASS | 0.5236 | >= 0.25 | display |
| mean luma | PASS | 0.7630 | 0.7..0.85 | display |
| horizon[default] | SKIP | 731.0000 |  | sky band not uniform (spread 75.0 codes): objects on the horizon |
| banding[sky_above_bank] | FAIL | 37.0840 | score <= 1 | steps/1000 74.17, run 28 px, resid rms 2.489 / pp 21.714, 16 strips |
| line[thread] width@1080p | FAIL | 10.7450 | 1.75..2.6 | native 10.74 px, eq 10.19 px |
| line[thread] contrast | PASS | 0.2169 | >= 0.08 | 3-px column vs columns +-10 px, linear |
| line[thread] coverage | PASS | 1.0000 | >= 0.5 |  |
| line[thread] monotone 3-level edge | FAIL | 0.0000 | >= 0.95 of rows |  |
| line[thread] stair-step | FAIL | 2.6600 | <= 0.25 px | 5-row smoothed centroid residual vs a quadratic fit (raw rms 0.886 px) |
| line[thread] energy variation | FAIL | 0.2492 | <= 0.1 |  |

### beat01_salt_KF1_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat01_salt_KF1_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9445 | < 0.98 | max pixel 0.949, 609 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1937 px excluded |
| luma p1 | PASS | 0.5230 | >= 0.25 | display |
| mean luma | PASS | 0.7567 | 0.7..0.85 | display |
| horizon[default] | SKIP | 710.7000 |  | sky band not uniform (spread 74.0 codes): objects on the horizon |

### beat01_salt_KF1_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat01_salt_KF1_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9445 | < 0.98 | max pixel 0.949, 648 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2044 px excluded |
| luma p1 | PASS | 0.5272 | >= 0.25 | display |
| mean luma | PASS | 0.7747 | 0.7..0.85 | display |
| horizon[default] | SKIP | 731.0000 |  | sky band not uniform (spread 43.0 codes): objects on the horizon |

### beat01_salt_KF1_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat01_salt_KF1_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9445 | < 0.98 | max pixel 0.949, 2582 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8182 px excluded |
| luma p1 | PASS | 0.5236 | >= 0.25 | display |
| mean luma | PASS | 0.7629 | 0.7..0.85 | display |
| horizon[default] | SKIP | 1462.1000 |  | sky band not uniform (spread 75.0 codes): objects on the horizon |
| banding[sky_above_bank] | FAIL | 102.7940 | score <= 1 | steps/1000 48.04, run 28 px, resid rms 6.132 / pp 61.676, 32 strips |
| line[thread] width@1080p | FAIL | 9.4330 | 1.75..2.6 | native 18.87 px, eq 16.29 px |
| line[thread] contrast | PASS | 0.0856 | >= 0.08 | 3-px column vs columns +-10 px, linear |
| line[thread] coverage | PASS | 1.0000 | >= 0.5 |  |
| line[thread] monotone 3-level edge | FAIL | 0.0000 | >= 0.95 of rows |  |
| line[thread] stair-step | PASS | 0.1510 | <= 0.25 px | 5-row smoothed centroid residual vs a quadratic fit (raw rms 0.098 px) |
| line[thread] energy variation | PASS | 0.0138 | <= 0.1 |  |

### beat02_avenue_KF2a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1720 px excluded |
| luma p1 | PASS | 0.3805 | >= 0.25 | display |
| mean luma | FAIL | 0.5378 | 0.55..0.8 | display |
| horizon[default] | SKIP | 854.5000 |  | sky band not uniform (spread 84.0 codes): objects on the horizon |

### beat02_avenue_KF2a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1632 px excluded |
| luma p1 | PASS | 0.3799 | >= 0.25 | display |
| mean luma | FAIL | 0.5464 | 0.55..0.8 | display |
| horizon[default] | SKIP | 830.8000 |  | sky band not uniform (spread 85.0 codes): objects on the horizon |

### beat02_avenue_KF2a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1720 px excluded |
| luma p1 | PASS | 0.3813 | >= 0.25 | display |
| mean luma | FAIL | 0.5256 | 0.55..0.8 | display |
| horizon[default] | SKIP | 854.5000 |  | sky band not uniform (spread 93.0 codes): objects on the horizon |

### beat02_avenue_KF2a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 6891 px excluded |
| luma p1 | PASS | 0.3805 | >= 0.25 | display |
| mean luma | FAIL | 0.5376 | 0.55..0.8 | display |
| horizon[default] | SKIP | 1709.1000 |  | sky band not uniform (spread 84.5 codes): objects on the horizon |

### beat02_avenue_KF2b (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | FAIL | 0.2450 | >= 0.25 | display |
| mean luma | PASS | 0.6870 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | FAIL | 0.2389 | >= 0.25 | display |
| mean luma | PASS | 0.7021 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3558 | >= 0.25 | display |
| mean luma | PASS | 0.6465 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | FAIL | 0.2445 | >= 0.25 | display |
| mean luma | PASS | 0.6870 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2c (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4050 | >= 0.25 | display |
| mean luma | PASS | 0.6572 | 0.4..0.75 | display |
| horizon[default] | SKIP | 630.8000 |  | sky band not uniform (spread 53.0 codes): objects on the horizon |

### beat02_avenue_KF2c_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4045 | >= 0.25 | display |
| mean luma | PASS | 0.6583 | 0.4..0.75 | display |
| horizon[default] | SKIP | 613.3000 |  | sky band not uniform (spread 54.0 codes): objects on the horizon |

### beat02_avenue_KF2c_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4064 | >= 0.25 | display |
| mean luma | PASS | 0.6511 | 0.4..0.75 | display |
| horizon[default] | SKIP | 630.8000 |  | sky band not uniform (spread 60.0 codes): objects on the horizon |

### beat02_avenue_KF2c_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat02_avenue_KF2c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4050 | >= 0.25 | display |
| mean luma | PASS | 0.6569 | 0.4..0.75 | display |
| horizon[default] | SKIP | 1261.7000 |  | sky band not uniform (spread 53.5 codes): objects on the horizon |

### beat03_stairs_KF3 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.5654 | >= 0.25 | display |
| mean luma | FAIL | 0.7331 | 0.8..0.95 | display |

### beat03_stairs_KF3_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2002 px excluded |
| luma p1 | PASS | 0.5646 | >= 0.25 | display |
| mean luma | FAIL | 0.7326 | 0.8..0.95 | display |

### beat03_stairs_KF3_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.5682 | >= 0.25 | display |
| mean luma | FAIL | 0.7353 | 0.8..0.95 | display |

### beat03_stairs_KF3_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8452 px excluded |
| luma p1 | PASS | 0.5654 | >= 0.25 | display |
| mean luma | FAIL | 0.7331 | 0.8..0.95 | display |

### beat03_stairs_KF3_s305 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3_s305.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2093 px excluded |
| luma p1 | PASS | 0.5564 | >= 0.25 | display |
| mean luma | INFO | 0.7260 |  | display |
| stats[centre_60x60] std | FAIL | 0.0991 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.1155 | < 0.07 |  |

### beat03_stairs_KF3_s305_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat03_stairs_KF3_s305_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8380 px excluded |
| luma p1 | PASS | 0.5564 | >= 0.25 | display |
| mean luma | INFO | 0.7259 |  | display |
| stats[centre_60x60] std | FAIL | 0.0992 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.1155 | < 0.07 |  |

### beat04_tower_KF4a (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6719 | 0.45..0.75 | display |

### beat04_tower_KF4a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | FAIL | 2 | 0 | bright 0 (mode all), dark 2, first at [[208, 653], [146, 655]] |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3990 | >= 0.25 | display |
| mean luma | PASS | 0.6577 | 0.45..0.75 | display |

### beat04_tower_KF4a_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4002 | >= 0.25 | display |
| mean luma | PASS | 0.7032 | 0.45..0.75 | display |

### beat04_tower_KF4a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | FAIL | 3 | 0 | bright 0 (mode all), dark 3, first at [[496, 1327], [494, 1341], [492, 1355]] |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6718 | 0.45..0.75 | display |

### beat04_tower_KF4b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3037 | >= 0.25 | display |
| mean luma | PASS | 0.5632 | 0.3..0.65 | display |

### beat04_tower_KF4b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3025 | >= 0.25 | display |
| mean luma | PASS | 0.5445 | 0.3..0.65 | display |

### beat04_tower_KF4b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3062 | >= 0.25 | display |
| mean luma | PASS | 0.6212 | 0.3..0.65 | display |

### beat04_tower_KF4b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3034 | >= 0.25 | display |
| mean luma | PASS | 0.5631 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_wide.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3020 | >= 0.25 | display |
| mean luma | PASS | 0.5701 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_wide_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3012 | >= 0.25 | display |
| mean luma | PASS | 0.5510 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_wide_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3050 | >= 0.25 | display |
| mean luma | PASS | 0.6282 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat04_tower_KF4b_wide_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3020 | >= 0.25 | display |
| mean luma | PASS | 0.5701 | 0.3..0.65 | display |

### beat05_spiral_KF5a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3535 | >= 0.05 | display |
| mean luma | FAIL | 0.7850 | 0.4..0.75 | display |

### beat05_spiral_KF5a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3571 | >= 0.05 | display |
| mean luma | FAIL | 0.7878 | 0.4..0.75 | display |

### beat05_spiral_KF5a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2205 | >= 0.05 | display |
| mean luma | FAIL | 0.7772 | 0.4..0.75 | display |

### beat05_spiral_KF5a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3535 | >= 0.05 | display |
| mean luma | FAIL | 0.7849 | 0.4..0.75 | display |

### beat05_spiral_KF5b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2650 | >= 0.05 | display |
| mean luma | PASS | 0.4341 | 0.35..0.7 | display |

### beat05_spiral_KF5b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2647 | >= 0.05 | display |
| mean luma | PASS | 0.4204 | 0.35..0.7 | display |

### beat05_spiral_KF5b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2594 | >= 0.05 | display |
| mean luma | PASS | 0.5061 | 0.35..0.7 | display |

### beat05_spiral_KF5b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2650 | >= 0.05 | display |
| mean luma | PASS | 0.4341 | 0.35..0.7 | display |

### beat05_spiral_KF5c (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3613 | >= 0.05 | display |
| mean luma | FAIL | 0.7516 | 0.4..0.75 | display |

### beat05_spiral_KF5c_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3622 | >= 0.05 | display |
| mean luma | FAIL | 0.7532 | 0.4..0.75 | display |

### beat05_spiral_KF5c_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3585 | >= 0.05 | display |
| mean luma | PASS | 0.7441 | 0.4..0.75 | display |

### beat05_spiral_KF5c_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3613 | >= 0.05 | display |
| mean luma | FAIL | 0.7515 | 0.4..0.75 | display |

### beat05_spiral_KF5d (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5d.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1972 | >= 0.05 | display |
| mean luma | PASS | 0.6291 | 0.4..0.75 | display |

### beat05_spiral_KF5d_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5d_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2059 | >= 0.05 | display |
| mean luma | PASS | 0.6305 | 0.4..0.75 | display |

### beat05_spiral_KF5d_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5d_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2588 | >= 0.05 | display |
| mean luma | PASS | 0.6263 | 0.4..0.75 | display |

### beat05_spiral_KF5d_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat05_spiral_KF5d_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1933 | >= 0.05 | display |
| mean luma | PASS | 0.6290 | 0.4..0.75 | display |

### beat06_clouds_KF6a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3918 | >= 0.05 | display |
| mean luma | FAIL | 0.6167 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1490 | <= 0.02 | row ~473.7, 461 cols: above [185.2, 153.5, 133.0] vs below [203.2, 180.5, 171.0] |

### beat06_clouds_KF6a2 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a2.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1384 | >= 0.05 | display |
| mean luma | FAIL | 0.5475 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1157 | <= 0.02 | row ~515.8, 461 cols: above [118.5, 102.0, 106.5] vs below [145.8, 131.5, 135.0] |

### beat06_clouds_KF6a2_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a2_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1073 | >= 0.05 | display |
| mean luma | FAIL | 0.5500 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1170 | <= 0.02 | row ~498.4, 404 cols: above [118.8, 102.3, 106.8] vs below [146.5, 132.1, 135.8] |

### beat06_clouds_KF6a2_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a2_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1547 | >= 0.05 | display |
| mean luma | FAIL | 0.5389 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1098 | <= 0.02 | row ~527.7, 615 cols: above [117.0, 101.0, 105.5] vs below [143.2, 129.0, 132.8] |

### beat06_clouds_KF6a2_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a2_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1384 | >= 0.05 | display |
| mean luma | FAIL | 0.5477 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1152 | <= 0.02 | row ~1031.6, 922 cols: above [118.4, 102.0, 106.5] vs below [145.9, 131.4, 135.1] |

### beat06_clouds_KF6a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3918 | >= 0.05 | display |
| mean luma | FAIL | 0.6197 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1500 | <= 0.02 | row ~458.9, 404 cols: above [185.8, 154.0, 133.5] vs below [204.0, 181.2, 171.8] |

### beat06_clouds_KF6a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1429 | >= 0.05 | display |
| mean luma | FAIL | 0.6106 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1451 | <= 0.02 | row ~480.2, 615 cols: above [183.0, 151.8, 131.5] vs below [200.8, 178.0, 168.5] |

### beat06_clouds_KF6a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3918 | >= 0.05 | display |
| mean luma | FAIL | 0.6167 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.1510 | <= 0.02 | row ~947.4, 922 cols: above [185.2, 153.5, 133.1] vs below [204.6, 182.5, 171.6] |

### beat06_clouds_KF6b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.3962 | 0.15..0.4 | display |

### beat06_clouds_KF6b_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.05 | display |
| mean luma | FAIL | 0.4049 | 0.15..0.4 | display |

### beat06_clouds_KF6b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.3594 | 0.15..0.4 | display |

### beat06_clouds_KF6b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat06_clouds_KF6b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.3960 | 0.15..0.4 | display |

### beat07_top_KF7a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | FAIL | 1 | 0 | bright 0 (mode clipped), dark 1, first at [[1330, 333]] |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8855 px excluded |
| luma p1 | PASS | 0.0530 | >= 0.01 | display |
| mean luma | FAIL | 0.3302 | 0.08..0.3 | display |

### beat07_top_KF7a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8363 px excluded |
| luma p1 | PASS | 0.0528 | >= 0.01 | display |
| mean luma | FAIL | 0.3296 | 0.08..0.3 | display |

### beat07_top_KF7a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | FAIL | 1 | 0 | bright 0 (mode clipped), dark 1, first at [[1650, 333]] |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8855 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.01 | display |
| mean luma | FAIL | 0.3253 | 0.08..0.3 | display |

### beat07_top_KF7a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 35404 px excluded |
| luma p1 | PASS | 0.0530 | >= 0.01 | display |
| mean luma | FAIL | 0.3303 | 0.08..0.3 | display |

### beat07_top_KF7a_s750 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a_s750.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.01 | display |
| mean luma | INFO | 0.3541 |  | display |

### beat07_top_KF7a_s750_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7a_s750_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.01 | display |
| mean luma | INFO | 0.3553 |  | display |

### beat07_top_KF7b (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5980 px excluded |
| luma p1 | PASS | 0.1149 | >= 0.01 | display |
| mean luma | FAIL | 0.4222 | 0.08..0.3 | display |

### beat07_top_KF7b_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5650 px excluded |
| luma p1 | PASS | 0.1034 | >= 0.01 | display |
| mean luma | FAIL | 0.4412 | 0.08..0.3 | display |

### beat07_top_KF7b_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5980 px excluded |
| luma p1 | PASS | 0.0849 | >= 0.01 | display |
| mean luma | FAIL | 0.3826 | 0.08..0.3 | display |

### beat07_top_KF7b_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 23915 px excluded |
| luma p1 | PASS | 0.1149 | >= 0.01 | display |
| mean luma | FAIL | 0.4220 | 0.08..0.3 | display |

### beat07_top_KF7c (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3367 | >= 0.01 | display |
| mean luma | FAIL | 0.6206 | 0.08..0.35 | display |

### beat07_top_KF7c_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3445 | >= 0.01 | display |
| mean luma | FAIL | 0.6238 | 0.08..0.35 | display |

### beat07_top_KF7c_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1413 | >= 0.01 | display |
| mean luma | FAIL | 0.5959 | 0.08..0.35 | display |

### beat07_top_KF7c_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3364 | >= 0.01 | display |
| mean luma | FAIL | 0.6217 | 0.08..0.35 | display |

### beat07_top_KF7c_wide (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_wide.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2835 | >= 0.01 | display |
| mean luma | FAIL | 0.6149 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_wide_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2952 | >= 0.01 | display |
| mean luma | FAIL | 0.6179 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_wide_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1383 | >= 0.01 | display |
| mean luma | FAIL | 0.5856 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run03\beat07_top_KF7c_wide_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2835 | >= 0.01 | display |
| mean luma | FAIL | 0.6164 | 0.08..0.35 | display |

## Method notes

- Luma: Rec.709 weights; 'display' on the 8-bit encoded values / 255, 'linear' on the exact sRGB-decoded values. The 14.10.3 gates (p1, expected mean ranges, region std) use display luma unless --luma-scale linear.
- Clipped: any channel at code 255 (>= 0.999), percentage of all pixels outside the exclusion discs.
- NaN-like: display luma more than 0.30 above the max / 0.30 below the min of the 24 neighbours (isolated); beats 6-7 count bright outliers only when they also clip (stars).
- Banding (14.10.3 + R6 6.2): 64-px strips, 9-px box, steps > 0.6 code per 1000 px <= 2; longest run (< 0.05 code) <= 6 px only where |slope| >= 0.06 code/px; residual vs a 129-px quadratic trend RMS <= 0.10 and p-p <= 0.60 code.  Score = worst ratio to its limit (<= 1 passes).
- Thin line: crop 64 x 256 on linear luminance, flank-fitted background, FWHM per row (reported at 1080p scale), monotone 3-level edge on >= 95 % of rows, centroid residual <= 0.25 px, energy variation <= 10 %.
