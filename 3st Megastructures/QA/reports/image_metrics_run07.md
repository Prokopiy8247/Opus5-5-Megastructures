# Image metrics: run07

Capture folder: `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07`  
Scene constants: ThreadConstants.cs  
Images: 128, failing images: 34. Checks: 910 PASS, 44 FAIL, 0 WARN, 76 SKIP, 52 INFO.

**Result: FAIL**

## Input warnings

- beat01_salt_end.png is not listed in report.json
- beat01_salt_look_back.png is not listed in report.json
- beat01_salt_look_l.png is not listed in report.json
- beat01_salt_look_r.png is not listed in report.json
- beat01_salt_mid.png is not listed in report.json
- beat01_salt_start.png is not listed in report.json
- beat02_avenue_end.png is not listed in report.json
- beat02_avenue_look_back.png is not listed in report.json
- beat02_avenue_look_l.png is not listed in report.json
- beat02_avenue_look_r.png is not listed in report.json
- beat02_avenue_mid.png is not listed in report.json
- beat02_avenue_start.png is not listed in report.json
- beat03_stairs_end.png is not listed in report.json
- beat03_stairs_look_back.png is not listed in report.json
- beat03_stairs_look_l.png is not listed in report.json
- beat03_stairs_look_r.png is not listed in report.json
- beat03_stairs_mid.png is not listed in report.json
- beat03_stairs_start.png is not listed in report.json
- beat04_tower_end.png is not listed in report.json
- beat04_tower_look_back.png is not listed in report.json
- beat04_tower_look_l.png is not listed in report.json
- beat04_tower_look_r.png is not listed in report.json
- beat04_tower_mid.png is not listed in report.json
- beat04_tower_start.png is not listed in report.json
- beat05_spiral_end.png is not listed in report.json
- beat05_spiral_look_back.png is not listed in report.json
- beat05_spiral_look_down.png is not listed in report.json
- beat05_spiral_look_l.png is not listed in report.json
- beat05_spiral_look_r.png is not listed in report.json
- beat05_spiral_look_up.png is not listed in report.json
- beat05_spiral_mid.png is not listed in report.json
- beat05_spiral_start.png is not listed in report.json
- beat06_clouds_end.png is not listed in report.json
- beat06_clouds_look_back.png is not listed in report.json
- beat06_clouds_look_down.png is not listed in report.json
- beat06_clouds_look_l.png is not listed in report.json
- beat06_clouds_look_r.png is not listed in report.json
- beat06_clouds_look_up.png is not listed in report.json
- beat06_clouds_mid.png is not listed in report.json
- beat06_clouds_start.png is not listed in report.json
- beat07_top_end.png is not listed in report.json
- beat07_top_look_back.png is not listed in report.json
- beat07_top_look_down.png is not listed in report.json
- beat07_top_look_l.png is not listed in report.json
- beat07_top_look_r.png is not listed in report.json
- beat07_top_look_up.png is not listed in report.json
- beat07_top_mid.png is not listed in report.json
- beat07_top_start.png is not listed in report.json

## Summary

| Image | Beat | Size | Mean luma | p1 | Clipped % | Band score | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| beat01_salt_KF1 | 1 | 1920x1080 | 0.765 | 0.524 | 0.0000 | 0.534 | PASS |  |
| beat01_salt_KF1_1610 | 1 | 1680x1050 | 0.758 | 0.523 | 0.0000 | - | PASS |  |
| beat01_salt_KF1_219 | 1 | 2560x1080 | 0.776 | 0.528 | 0.0000 | - | PASS |  |
| beat01_salt_KF1_4k | 1 | 3840x2160 | 0.765 | 0.524 | 0.0000 | 0.332 | FAIL | line[thread] width@1080p |
| beat01_salt_end | 1 | 1920x1080 | 0.615 | 0.413 | 0.0000 | - | PASS |  |
| beat01_salt_look_back | 1 | 1920x1080 | 0.809 | 0.667 | 0.0000 | - | PASS |  |
| beat01_salt_look_l | 1 | 1920x1080 | 0.826 | 0.669 | 0.0000 | - | PASS |  |
| beat01_salt_look_r | 1 | 1920x1080 | 0.770 | 0.667 | 0.0000 | - | PASS |  |
| beat01_salt_mid | 1 | 1920x1080 | 0.686 | 0.488 | 0.0000 | - | PASS |  |
| beat01_salt_start | 1 | 1920x1080 | 0.780 | 0.537 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2a | 2 | 1920x1080 | 0.547 | 0.381 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2a_1610 | 2 | 1680x1050 | 0.555 | 0.381 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2a_219 | 2 | 2560x1080 | 0.536 | 0.382 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2a_4k | 2 | 3840x2160 | 0.547 | 0.381 | 0.0000 | - | FAIL | mean luma |
| beat02_avenue_KF2b | 2 | 1920x1080 | 0.689 | 0.365 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2b_1610 | 2 | 1680x1050 | 0.704 | 0.366 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2b_219 | 2 | 2560x1080 | 0.648 | 0.360 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2b_4k | 2 | 3840x2160 | 0.688 | 0.365 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c | 2 | 1920x1080 | 0.672 | 0.405 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_1610 | 2 | 1680x1050 | 0.673 | 0.405 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_219 | 2 | 2560x1080 | 0.665 | 0.407 | 0.0000 | - | PASS |  |
| beat02_avenue_KF2c_4k | 2 | 3840x2160 | 0.671 | 0.405 | 0.0000 | - | PASS |  |
| beat02_avenue_end | 2 | 1920x1080 | 0.734 | 0.569 | 0.0000 | - | PASS |  |
| beat02_avenue_look_back | 2 | 1920x1080 | 0.745 | 0.388 | 0.0000 | - | PASS |  |
| beat02_avenue_look_l | 2 | 1920x1080 | 0.590 | 0.366 | 0.0000 | - | PASS |  |
| beat02_avenue_look_r | 2 | 1920x1080 | 0.628 | 0.387 | 0.0000 | - | PASS |  |
| beat02_avenue_mid | 2 | 1920x1080 | 0.651 | 0.372 | 0.0000 | - | PASS |  |
| beat02_avenue_start | 2 | 1920x1080 | 0.610 | 0.397 | 0.0000 | - | PASS |  |
| beat03_stairs_KF3 | 3 | 1920x1080 | 0.745 | 0.593 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_1610 | 3 | 1680x1050 | 0.745 | 0.591 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_219 | 3 | 2560x1080 | 0.747 | 0.593 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_4k | 3 | 3840x2160 | 0.745 | 0.593 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_s305 | 3 | 1920x1080 | 0.738 | 0.582 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_KF3_s305_4k | 3 | 3840x2160 | 0.738 | 0.582 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_end | 3 | 1920x1080 | 0.773 | 0.552 | 0.0000 | - | PASS |  |
| beat03_stairs_look_back | 3 | 1920x1080 | 0.874 | 0.830 | 0.0000 | - | PASS |  |
| beat03_stairs_look_l | 3 | 1920x1080 | 0.813 | 0.600 | 0.0000 | - | PASS |  |
| beat03_stairs_look_r | 3 | 1920x1080 | 0.813 | 0.600 | 0.0000 | - | PASS |  |
| beat03_stairs_mid | 3 | 1920x1080 | 0.731 | 0.575 | 0.0000 | - | PASS |  |
| beat03_stairs_start | 3 | 1920x1080 | 0.716 | 0.551 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a | 4 | 1920x1080 | 0.674 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_1610 | 4 | 1680x1050 | 0.660 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_219 | 4 | 2560x1080 | 0.705 | 0.400 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_4k | 4 | 3840x2160 | 0.674 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b | 4 | 1920x1080 | 0.562 | 0.304 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_1610 | 4 | 1680x1050 | 0.544 | 0.303 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_219 | 4 | 2560x1080 | 0.620 | 0.306 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_4k | 4 | 3840x2160 | 0.562 | 0.303 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide | 4 | 1920x1080 | 0.569 | 0.302 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_1610 | 4 | 1680x1050 | 0.550 | 0.301 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_219 | 4 | 2560x1080 | 0.626 | 0.305 | 0.0000 | - | PASS |  |
| beat04_tower_KF4b_wide_4k | 4 | 3840x2160 | 0.569 | 0.302 | 0.0000 | - | PASS |  |
| beat04_tower_end | 4 | 1920x1080 | 0.708 | 0.120 | 0.0000 | - | FAIL | luma p1 |
| beat04_tower_look_back | 4 | 1920x1080 | 0.814 | 0.686 | 0.0000 | - | PASS |  |
| beat04_tower_look_l | 4 | 1920x1080 | 0.790 | 0.686 | 0.0000 | - | PASS |  |
| beat04_tower_look_r | 4 | 1920x1080 | 0.651 | 0.299 | 0.0000 | - | PASS |  |
| beat04_tower_mid | 4 | 1920x1080 | 0.702 | 0.305 | 0.0000 | - | PASS |  |
| beat04_tower_start | 4 | 1920x1080 | 0.775 | 0.694 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5a | 5 | 1920x1080 | 0.753 | 0.354 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5a_1610 | 5 | 1680x1050 | 0.755 | 0.358 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5a_219 | 5 | 2560x1080 | 0.749 | 0.349 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5a_4k | 5 | 3840x2160 | 0.753 | 0.354 | 0.0000 | - | FAIL | mean luma |
| beat05_spiral_KF5b | 5 | 1920x1080 | 0.438 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_1610 | 5 | 1680x1050 | 0.421 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_219 | 5 | 2560x1080 | 0.510 | 0.259 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5b_4k | 5 | 3840x2160 | 0.438 | 0.265 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c | 5 | 1920x1080 | 0.732 | 0.361 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c_1610 | 5 | 1680x1050 | 0.734 | 0.362 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c_219 | 5 | 2560x1080 | 0.725 | 0.352 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5c_4k | 5 | 3840x2160 | 0.732 | 0.361 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d | 5 | 1920x1080 | 0.585 | 0.197 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_1610 | 5 | 1680x1050 | 0.583 | 0.206 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_219 | 5 | 2560x1080 | 0.590 | 0.259 | 0.0000 | - | PASS |  |
| beat05_spiral_KF5d_4k | 5 | 3840x2160 | 0.585 | 0.193 | 0.0000 | - | PASS |  |
| beat05_spiral_end | 5 | 1920x1080 | 0.744 | 0.123 | 0.0000 | - | PASS |  |
| beat05_spiral_look_back | 5 | 1920x1080 | 0.754 | 0.383 | 0.0000 | - | PASS |  |
| beat05_spiral_look_down | 5 | 1920x1080 | 0.609 | 0.397 | 0.0000 | - | PASS |  |
| beat05_spiral_look_l | 5 | 1920x1080 | 0.813 | 0.360 | 0.0000 | - | PASS |  |
| beat05_spiral_look_r | 5 | 1920x1080 | 0.452 | 0.304 | 0.0000 | - | PASS |  |
| beat05_spiral_look_up | 5 | 1920x1080 | 0.562 | 0.389 | 0.0000 | - | PASS |  |
| beat05_spiral_mid | 5 | 1920x1080 | 0.713 | 0.123 | 0.0000 | - | PASS |  |
| beat05_spiral_start | 5 | 1920x1080 | 0.699 | 0.107 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6a | 6 | 1920x1080 | 0.681 | 0.128 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2 | 6 | 1920x1080 | 0.638 | 0.142 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_1610 | 6 | 1680x1050 | 0.639 | 0.112 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_219 | 6 | 2560x1080 | 0.636 | 0.159 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a2_4k | 6 | 3840x2160 | 0.636 | 0.143 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_1610 | 6 | 1680x1050 | 0.679 | 0.124 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_219 | 6 | 2560x1080 | 0.671 | 0.226 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6a_4k | 6 | 3840x2160 | 0.680 | 0.127 | 0.0000 | - | FAIL | mean luma, horizon[sea_sky_seam_left] |
| beat06_clouds_KF6b | 6 | 1920x1080 | 0.211 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6b_1610 | 6 | 1680x1050 | 0.220 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6b_219 | 6 | 2560x1080 | 0.192 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_KF6b_4k | 6 | 3840x2160 | 0.211 | 0.054 | 0.0000 | - | PASS |  |
| beat06_clouds_end | 6 | 1920x1080 | 0.289 | 0.042 | 0.0000 | - | FAIL | luma p1 |
| beat06_clouds_look_back | 6 | 1920x1080 | 0.534 | 0.207 | 0.0000 | - | PASS |  |
| beat06_clouds_look_down | 6 | 1920x1080 | 0.544 | 0.228 | 0.0000 | - | PASS |  |
| beat06_clouds_look_l | 6 | 1920x1080 | 0.576 | 0.207 | 0.0000 | - | PASS |  |
| beat06_clouds_look_r | 6 | 1920x1080 | 0.342 | 0.229 | 0.0000 | - | PASS |  |
| beat06_clouds_look_up | 6 | 1920x1080 | 0.197 | 0.094 | 0.0000 | - | PASS |  |
| beat06_clouds_mid | 6 | 1920x1080 | 0.476 | 0.191 | 0.0000 | - | PASS |  |
| beat06_clouds_start | 6 | 1920x1080 | 0.850 | 0.476 | 0.0000 | - | PASS |  |
| beat07_top_KF7a | 7 | 1920x1080 | 0.310 | 0.053 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7a_1610 | 7 | 1680x1050 | 0.311 | 0.053 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7a_219 | 7 | 2560x1080 | 0.297 | 0.053 | 0.0000 | - | PASS |  |
| beat07_top_KF7a_4k | 7 | 3840x2160 | 0.310 | 0.053 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7a_s750 | 7 | 1920x1080 | 0.352 | 0.054 | 0.0000 | - | PASS |  |
| beat07_top_KF7a_s750_4k | 7 | 3840x2160 | 0.352 | 0.054 | 0.0000 | - | PASS |  |
| beat07_top_KF7b | 7 | 1920x1080 | 0.269 | 0.066 | 0.0080 | - | PASS |  |
| beat07_top_KF7b_1610 | 7 | 1680x1050 | 0.282 | 0.066 | 0.0088 | - | PASS |  |
| beat07_top_KF7b_219 | 7 | 2560x1080 | 0.234 | 0.065 | 0.0060 | - | PASS |  |
| beat07_top_KF7b_4k | 7 | 3840x2160 | 0.269 | 0.066 | 0.0081 | - | PASS |  |
| beat07_top_KF7c | 7 | 1920x1080 | 0.541 | 0.142 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_1610 | 7 | 1680x1050 | 0.540 | 0.297 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_219 | 7 | 2560x1080 | 0.517 | 0.137 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_4k | 7 | 3840x2160 | 0.541 | 0.142 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide | 7 | 1920x1080 | 0.538 | 0.142 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_1610 | 7 | 1680x1050 | 0.540 | 0.235 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_219 | 7 | 2560x1080 | 0.513 | 0.137 | 0.0000 | - | FAIL | mean luma |
| beat07_top_KF7c_wide_4k | 7 | 3840x2160 | 0.538 | 0.142 | 0.0000 | - | FAIL | mean luma |
| beat07_top_end | 7 | 1920x1080 | 0.248 | 0.058 | 0.0070 | - | PASS |  |
| beat07_top_look_back | 7 | 1920x1080 | 0.193 | 0.042 | 0.0000 | - | PASS |  |
| beat07_top_look_down | 7 | 1920x1080 | 0.510 | 0.212 | 0.0000 | - | PASS |  |
| beat07_top_look_l | 7 | 1920x1080 | 0.190 | 0.042 | 0.0000 | - | PASS |  |
| beat07_top_look_r | 7 | 1920x1080 | 0.334 | 0.087 | 0.0000 | - | PASS |  |
| beat07_top_look_up | 7 | 1920x1080 | 0.174 | 0.017 | 0.0000 | - | PASS |  |
| beat07_top_mid | 7 | 1920x1080 | 0.262 | 0.038 | 0.0000 | - | PASS |  |
| beat07_top_start | 7 | 1920x1080 | 0.273 | 0.038 | 0.0000 | - | PASS |  |

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

### beat01_salt_KF1 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_KF1.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9302 | < 0.98 | max pixel 0.949, 648 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2044 px excluded |
| luma p1 | PASS | 0.5239 | >= 0.25 | display |
| mean luma | PASS | 0.7646 | 0.7..0.85 | display |
| horizon[default] | SKIP | 731.0000 |  | sky band not uniform (spread 75.0 codes): objects on the horizon |
| banding[sky_above_bank] | PASS | 0.5340 | score <= 1 | steps/1000 0.00, run 1 px, resid rms 0.053 / pp 0.197, 7 strips |
| line[thread] width@1080p | PASS | 1.7520 | 1.75..2.6 | native 1.75 px, eq 1.75 px |
| line[thread] contrast | PASS | 0.1352 | >= 0.08 | 3-px column vs columns +-10 px, linear |
| line[thread] coverage | PASS | 1.0000 | >= 0.5 |  |
| line[thread] monotone 3-level edge | PASS | 1.0000 | >= 0.95 of rows |  |
| line[thread] stair-step | PASS | 0.1020 | <= 0.25 px | 5-row smoothed centroid residual vs a quadratic fit (raw rms 0.040 px) |
| line[thread] energy variation | PASS | 0.0271 | <= 0.1 |  |

### beat01_salt_KF1_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_KF1_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9303 | < 0.98 | max pixel 0.949, 609 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1937 px excluded |
| luma p1 | PASS | 0.5233 | >= 0.25 | display |
| mean luma | PASS | 0.7583 | 0.7..0.85 | display |
| horizon[default] | SKIP | 710.7000 |  | sky band not uniform (spread 74.0 codes): objects on the horizon |

### beat01_salt_KF1_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_KF1_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9302 | < 0.98 | max pixel 0.949, 648 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2044 px excluded |
| luma p1 | PASS | 0.5278 | >= 0.25 | display |
| mean luma | PASS | 0.7763 | 0.7..0.85 | display |
| horizon[default] | SKIP | 731.0000 |  | sky band not uniform (spread 43.0 codes): objects on the horizon |

### beat01_salt_KF1_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_KF1_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| core[sun_core] colour | PASS | 0.9299 | < 0.98 | max pixel 0.949, 2582 px |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8182 px excluded |
| luma p1 | PASS | 0.5239 | >= 0.25 | display |
| mean luma | PASS | 0.7645 | 0.7..0.85 | display |
| horizon[default] | SKIP | 1462.1000 |  | sky band not uniform (spread 74.5 codes): objects on the horizon |
| banding[sky_above_bank] | PASS | 0.3320 | score <= 1 | steps/1000 0.00, run 1 px, resid rms 0.033 / pp 0.179, 14 strips |
| line[thread] width@1080p | FAIL | 1.7120 | 1.75..2.6 | native 3.42 px, eq 3.41 px |
| line[thread] contrast | PASS | 0.2284 | >= 0.08 | 3-px column vs columns +-10 px, linear |
| line[thread] coverage | PASS | 1.0000 | >= 0.5 |  |
| line[thread] monotone 3-level edge | PASS | 1.0000 | >= 0.95 of rows |  |
| line[thread] stair-step | PASS | 0.0510 | <= 0.25 px | 5-row smoothed centroid residual vs a quadratic fit (raw rms 0.024 px) |
| line[thread] energy variation | PASS | 0.0190 | <= 0.1 |  |

### beat01_salt_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4135 | >= 0.25 | display |
| mean luma | INFO | 0.6152 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat01_salt_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6666 | >= 0.25 | display |
| mean luma | INFO | 0.8095 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat01_salt_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6695 | >= 0.25 | display |
| mean luma | INFO | 0.8263 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat01_salt_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6666 | >= 0.25 | display |
| mean luma | INFO | 0.7698 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat01_salt_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4877 | >= 0.25 | display |
| mean luma | INFO | 0.6862 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat01_salt_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat01_salt_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5367 | >= 0.25 | display |
| mean luma | INFO | 0.7801 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1720 px excluded |
| luma p1 | PASS | 0.3808 | >= 0.25 | display |
| mean luma | FAIL | 0.5471 | 0.55..0.8 | display |
| horizon[default] | SKIP | 854.5000 |  | sky band not uniform (spread 84.0 codes): objects on the horizon |

### beat02_avenue_KF2a_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1632 px excluded |
| luma p1 | PASS | 0.3807 | >= 0.25 | display |
| mean luma | PASS | 0.5555 | 0.55..0.8 | display |
| horizon[default] | SKIP | 830.8000 |  | sky band not uniform (spread 83.0 codes): objects on the horizon |

### beat02_avenue_KF2a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1720 px excluded |
| luma p1 | PASS | 0.3816 | >= 0.25 | display |
| mean luma | FAIL | 0.5357 | 0.55..0.8 | display |
| horizon[default] | SKIP | 854.5000 |  | sky band not uniform (spread 92.0 codes): objects on the horizon |

### beat02_avenue_KF2a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 6891 px excluded |
| luma p1 | PASS | 0.3808 | >= 0.25 | display |
| mean luma | FAIL | 0.5469 | 0.55..0.8 | display |
| horizon[default] | SKIP | 1709.1000 |  | sky band not uniform (spread 84.4 codes): objects on the horizon |

### beat02_avenue_KF2b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3648 | >= 0.25 | display |
| mean luma | PASS | 0.6885 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3657 | >= 0.25 | display |
| mean luma | PASS | 0.7038 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3600 | >= 0.25 | display |
| mean luma | PASS | 0.6476 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3648 | >= 0.25 | display |
| mean luma | PASS | 0.6885 | 0.45..0.8 | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_KF2c (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4050 | >= 0.25 | display |
| mean luma | PASS | 0.6717 | 0.4..0.75 | display |
| horizon[default] | SKIP | 630.8000 |  | sky band not uniform (spread 31.0 codes): objects on the horizon |

### beat02_avenue_KF2c_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4047 | >= 0.25 | display |
| mean luma | PASS | 0.6729 | 0.4..0.75 | display |
| horizon[default] | SKIP | 613.3000 |  | sky band not uniform (spread 31.0 codes): objects on the horizon |

### beat02_avenue_KF2c_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4067 | >= 0.25 | display |
| mean luma | PASS | 0.6655 | 0.4..0.75 | display |
| horizon[default] | SKIP | 630.8000 |  | sky band not uniform (spread 40.0 codes): objects on the horizon |

### beat02_avenue_KF2c_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_KF2c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4050 | >= 0.25 | display |
| mean luma | PASS | 0.6715 | 0.4..0.75 | display |
| horizon[default] | SKIP | 1261.7000 |  | sky band not uniform (spread 31.0 codes): objects on the horizon |

### beat02_avenue_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5689 | >= 0.25 | display |
| mean luma | INFO | 0.7337 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3877 | >= 0.25 | display |
| mean luma | INFO | 0.7455 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3659 | >= 0.25 | display |
| mean luma | INFO | 0.5900 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3873 | >= 0.25 | display |
| mean luma | INFO | 0.6276 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3720 | >= 0.25 | display |
| mean luma | INFO | 0.6507 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat02_avenue_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat02_avenue_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3969 | >= 0.25 | display |
| mean luma | INFO | 0.6098 |  | display |
| horizon[default] | SKIP | - |  | line not in frame or no pitch/fov |

### beat03_stairs_KF3 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.5931 | >= 0.25 | display |
| mean luma | FAIL | 0.7454 | 0.8..0.95 | display |

### beat03_stairs_KF3_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2002 px excluded |
| luma p1 | PASS | 0.5905 | >= 0.25 | display |
| mean luma | FAIL | 0.7452 | 0.8..0.95 | display |

### beat03_stairs_KF3_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.5933 | >= 0.25 | display |
| mean luma | FAIL | 0.7467 | 0.8..0.95 | display |

### beat03_stairs_KF3_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8452 px excluded |
| luma p1 | PASS | 0.5925 | >= 0.25 | display |
| mean luma | FAIL | 0.7454 | 0.8..0.95 | display |

### beat03_stairs_KF3_s305 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3_s305.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2093 px excluded |
| luma p1 | PASS | 0.5821 | >= 0.25 | display |
| mean luma | INFO | 0.7384 |  | display |
| stats[centre_60x60] std | FAIL | 0.0907 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.1057 | < 0.07 |  |

### beat03_stairs_KF3_s305_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_KF3_s305_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8380 px excluded |
| luma p1 | PASS | 0.5821 | >= 0.25 | display |
| mean luma | INFO | 0.7383 |  | display |
| stats[centre_60x60] std | FAIL | 0.0908 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.1057 | < 0.07 |  |

### beat03_stairs_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5524 | >= 0.25 | display |
| mean luma | INFO | 0.7733 |  | display |

### beat03_stairs_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.8298 | >= 0.25 | display |
| mean luma | INFO | 0.8744 |  | display |

### beat03_stairs_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6001 | >= 0.25 | display |
| mean luma | INFO | 0.8133 |  | display |

### beat03_stairs_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6001 | >= 0.25 | display |
| mean luma | INFO | 0.8130 |  | display |

### beat03_stairs_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5746 | >= 0.25 | display |
| mean luma | INFO | 0.7310 |  | display |

### beat03_stairs_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat03_stairs_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5507 | >= 0.25 | display |
| mean luma | INFO | 0.7161 |  | display |

### beat04_tower_KF4a (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6739 | 0.45..0.75 | display |

### beat04_tower_KF4a_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3990 | >= 0.25 | display |
| mean luma | PASS | 0.6600 | 0.45..0.75 | display |

### beat04_tower_KF4a_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4002 | >= 0.25 | display |
| mean luma | PASS | 0.7047 | 0.45..0.75 | display |

### beat04_tower_KF4a_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6739 | 0.45..0.75 | display |

### beat04_tower_KF4b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3037 | >= 0.25 | display |
| mean luma | PASS | 0.5623 | 0.3..0.65 | display |

### beat04_tower_KF4b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3025 | >= 0.25 | display |
| mean luma | PASS | 0.5437 | 0.3..0.65 | display |

### beat04_tower_KF4b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3062 | >= 0.25 | display |
| mean luma | PASS | 0.6196 | 0.3..0.65 | display |

### beat04_tower_KF4b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3034 | >= 0.25 | display |
| mean luma | PASS | 0.5622 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_wide.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3020 | >= 0.25 | display |
| mean luma | PASS | 0.5692 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_wide_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3012 | >= 0.25 | display |
| mean luma | PASS | 0.5502 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_wide_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3050 | >= 0.25 | display |
| mean luma | PASS | 0.6265 | 0.3..0.65 | display |

### beat04_tower_KF4b_wide_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_KF4b_wide_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3020 | >= 0.25 | display |
| mean luma | PASS | 0.5692 | 0.3..0.65 | display |

### beat04_tower_end (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | FAIL | 0.1196 | >= 0.25 | display |
| mean luma | INFO | 0.7076 |  | display |

### beat04_tower_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6865 | >= 0.25 | display |
| mean luma | INFO | 0.8142 |  | display |

### beat04_tower_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6865 | >= 0.25 | display |
| mean luma | INFO | 0.7904 |  | display |

### beat04_tower_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2991 | >= 0.25 | display |
| mean luma | INFO | 0.6512 |  | display |

### beat04_tower_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3048 | >= 0.25 | display |
| mean luma | INFO | 0.7023 |  | display |

### beat04_tower_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat04_tower_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6944 | >= 0.25 | display |
| mean luma | INFO | 0.7752 |  | display |

### beat05_spiral_KF5a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3543 | >= 0.05 | display |
| mean luma | FAIL | 0.7530 | 0.4..0.75 | display |

### beat05_spiral_KF5a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3582 | >= 0.05 | display |
| mean luma | FAIL | 0.7549 | 0.4..0.75 | display |

### beat05_spiral_KF5a_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3487 | >= 0.05 | display |
| mean luma | PASS | 0.7485 | 0.4..0.75 | display |

### beat05_spiral_KF5a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3543 | >= 0.05 | display |
| mean luma | FAIL | 0.7530 | 0.4..0.75 | display |

### beat05_spiral_KF5b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2650 | >= 0.05 | display |
| mean luma | PASS | 0.4378 | 0.35..0.7 | display |

### beat05_spiral_KF5b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2647 | >= 0.05 | display |
| mean luma | PASS | 0.4213 | 0.35..0.7 | display |

### beat05_spiral_KF5b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2594 | >= 0.05 | display |
| mean luma | PASS | 0.5102 | 0.35..0.7 | display |

### beat05_spiral_KF5b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2650 | >= 0.05 | display |
| mean luma | PASS | 0.4378 | 0.35..0.7 | display |

### beat05_spiral_KF5c (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3613 | >= 0.05 | display |
| mean luma | PASS | 0.7324 | 0.4..0.75 | display |

### beat05_spiral_KF5c_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3622 | >= 0.05 | display |
| mean luma | PASS | 0.7342 | 0.4..0.75 | display |

### beat05_spiral_KF5c_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3521 | >= 0.05 | display |
| mean luma | PASS | 0.7252 | 0.4..0.75 | display |

### beat05_spiral_KF5c_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3613 | >= 0.05 | display |
| mean luma | PASS | 0.7324 | 0.4..0.75 | display |

### beat05_spiral_KF5d (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5d.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1972 | >= 0.05 | display |
| mean luma | PASS | 0.5848 | 0.4..0.75 | display |

### beat05_spiral_KF5d_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5d_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2059 | >= 0.05 | display |
| mean luma | PASS | 0.5828 | 0.4..0.75 | display |

### beat05_spiral_KF5d_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5d_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2588 | >= 0.05 | display |
| mean luma | PASS | 0.5897 | 0.4..0.75 | display |

### beat05_spiral_KF5d_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_KF5d_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1933 | >= 0.05 | display |
| mean luma | PASS | 0.5848 | 0.4..0.75 | display |

### beat05_spiral_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1230 | >= 0.05 | display |
| mean luma | INFO | 0.7436 |  | display |

### beat05_spiral_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3829 | >= 0.05 | display |
| mean luma | INFO | 0.7539 |  | display |

### beat05_spiral_look_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_look_down.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3973 | >= 0.05 | display |
| mean luma | INFO | 0.6093 |  | display |

### beat05_spiral_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3602 | >= 0.05 | display |
| mean luma | INFO | 0.8133 |  | display |

### beat05_spiral_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3044 | >= 0.05 | display |
| mean luma | INFO | 0.4520 |  | display |

### beat05_spiral_look_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_look_up.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3887 | >= 0.05 | display |
| mean luma | INFO | 0.5624 |  | display |

### beat05_spiral_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1227 | >= 0.05 | display |
| mean luma | INFO | 0.7132 |  | display |

### beat05_spiral_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat05_spiral_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1070 | >= 0.05 | display |
| mean luma | INFO | 0.6992 |  | display |

### beat06_clouds_KF6a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1275 | >= 0.05 | display |
| mean luma | FAIL | 0.6814 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0833 | <= 0.02 | row ~354.4, 461 cols: above [235.2, 197.0, 162.2] vs below [236.2, 208.2, 183.5] |

### beat06_clouds_KF6a2 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a2.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1421 | >= 0.05 | display |
| mean luma | FAIL | 0.6384 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0686 | <= 0.02 | row ~515.8, 461 cols: above [209.2, 176.0, 150.8] vs below [216.8, 189.5, 168.2] |

### beat06_clouds_KF6a2_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a2_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1118 | >= 0.05 | display |
| mean luma | FAIL | 0.6394 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0696 | <= 0.02 | row ~498.4, 404 cols: above [210.0, 176.8, 151.2] vs below [217.5, 190.2, 169.0] |

### beat06_clouds_KF6a2_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a2_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1586 | >= 0.05 | display |
| mean luma | FAIL | 0.6365 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0637 | <= 0.02 | row ~527.7, 615 cols: above [206.5, 174.0, 149.5] vs below [214.0, 186.8, 165.8] |

### beat06_clouds_KF6a2_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a2_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1429 | >= 0.05 | display |
| mean luma | FAIL | 0.6355 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0686 | <= 0.02 | row ~1031.6, 922 cols: above [209.2, 176.0, 150.8] vs below [216.8, 189.5, 168.2] |

### beat06_clouds_KF6a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1236 | >= 0.05 | display |
| mean luma | FAIL | 0.6793 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0843 | <= 0.02 | row ~342.9, 404 cols: above [236.0, 197.8, 162.8] vs below [237.0, 209.0, 184.2] |

### beat06_clouds_KF6a_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2258 | >= 0.05 | display |
| mean luma | FAIL | 0.6708 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0804 | <= 0.02 | row ~360.9, 615 cols: above [232.8, 195.0, 160.5] vs below [233.8, 205.8, 181.0] |

### beat06_clouds_KF6a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1272 | >= 0.05 | display |
| mean luma | FAIL | 0.6805 | 0.18..0.45 | display |
| horizon[sea_sky_seam_left] | FAIL | 0.0824 | <= 0.02 | row ~708.8, 922 cols: above [235.2, 197.1, 162.4] vs below [236.2, 208.2, 183.4] |

### beat06_clouds_KF6b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.2113 | 0.15..0.4 | display |

### beat06_clouds_KF6b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.05 | display |
| mean luma | PASS | 0.2203 | 0.15..0.4 | display |

### beat06_clouds_KF6b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.1918 | 0.15..0.4 | display |

### beat06_clouds_KF6b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_KF6b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0544 | >= 0.05 | display |
| mean luma | PASS | 0.2110 | 0.15..0.4 | display |

### beat06_clouds_end (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | FAIL | 0.0421 | >= 0.05 | display |
| mean luma | INFO | 0.2892 |  | display |

### beat06_clouds_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2071 | >= 0.05 | display |
| mean luma | INFO | 0.5344 |  | display |

### beat06_clouds_look_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_look_down.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2275 | >= 0.05 | display |
| mean luma | INFO | 0.5437 |  | display |

### beat06_clouds_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2071 | >= 0.05 | display |
| mean luma | INFO | 0.5758 |  | display |

### beat06_clouds_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2292 | >= 0.05 | display |
| mean luma | INFO | 0.3421 |  | display |

### beat06_clouds_look_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_look_up.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0943 | >= 0.05 | display |
| mean luma | INFO | 0.1968 |  | display |

### beat06_clouds_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1915 | >= 0.05 | display |
| mean luma | INFO | 0.4760 |  | display |

### beat06_clouds_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat06_clouds_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4759 | >= 0.05 | display |
| mean luma | INFO | 0.8498 |  | display |

### beat07_top_KF7a (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8895 px excluded |
| luma p1 | PASS | 0.0528 | >= 0.01 | display |
| mean luma | FAIL | 0.3098 | 0.08..0.3 | display |

### beat07_top_KF7a_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8394 px excluded |
| luma p1 | PASS | 0.0528 | >= 0.01 | display |
| mean luma | FAIL | 0.3109 | 0.08..0.3 | display |

### beat07_top_KF7a_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 8895 px excluded |
| luma p1 | PASS | 0.0530 | >= 0.01 | display |
| mean luma | PASS | 0.2965 | 0.08..0.3 | display |

### beat07_top_KF7a_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 35571 px excluded |
| luma p1 | PASS | 0.0528 | >= 0.01 | display |
| mean luma | FAIL | 0.3098 | 0.08..0.3 | display |

### beat07_top_KF7a_s750 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a_s750.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.01 | display |
| mean luma | INFO | 0.3519 |  | display |

### beat07_top_KF7a_s750_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7a_s750_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0536 | >= 0.01 | display |
| mean luma | INFO | 0.3516 |  | display |

### beat07_top_KF7b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0080 | <= 0.15 % | any channel at 255; 5979 px excluded |
| luma p1 | PASS | 0.0656 | >= 0.01 | display |
| mean luma | PASS | 0.2689 | 0.08..0.3 | display |

### beat07_top_KF7b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0088 | <= 0.15 % | any channel at 255; 5654 px excluded |
| luma p1 | PASS | 0.0664 | >= 0.01 | display |
| mean luma | PASS | 0.2821 | 0.08..0.3 | display |

### beat07_top_KF7b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0060 | <= 0.15 % | any channel at 255; 5979 px excluded |
| luma p1 | PASS | 0.0645 | >= 0.01 | display |
| mean luma | PASS | 0.2338 | 0.08..0.3 | display |

### beat07_top_KF7b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0081 | <= 0.15 % | any channel at 255; 23915 px excluded |
| luma p1 | PASS | 0.0656 | >= 0.01 | display |
| mean luma | PASS | 0.2686 | 0.08..0.3 | display |

### beat07_top_KF7c (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1416 | >= 0.01 | display |
| mean luma | FAIL | 0.5408 | 0.08..0.35 | display |

### beat07_top_KF7c_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2973 | >= 0.01 | display |
| mean luma | FAIL | 0.5399 | 0.08..0.35 | display |

### beat07_top_KF7c_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1374 | >= 0.01 | display |
| mean luma | FAIL | 0.5168 | 0.08..0.35 | display |

### beat07_top_KF7c_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1416 | >= 0.01 | display |
| mean luma | FAIL | 0.5408 | 0.08..0.35 | display |

### beat07_top_KF7c_wide (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_wide.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1416 | >= 0.01 | display |
| mean luma | FAIL | 0.5381 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_wide_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2353 | >= 0.01 | display |
| mean luma | FAIL | 0.5400 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_wide_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1374 | >= 0.01 | display |
| mean luma | FAIL | 0.5130 | 0.08..0.35 | display |

### beat07_top_KF7c_wide_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_KF7c_wide_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.1416 | >= 0.01 | display |
| mean luma | FAIL | 0.5381 | 0.08..0.35 | display |

### beat07_top_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0070 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0583 | >= 0.01 | display |
| mean luma | INFO | 0.2478 |  | display |

### beat07_top_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0424 | >= 0.01 | display |
| mean luma | INFO | 0.1928 |  | display |

### beat07_top_look_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_look_down.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2115 | >= 0.01 | display |
| mean luma | INFO | 0.5100 |  | display |

### beat07_top_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0424 | >= 0.01 | display |
| mean luma | INFO | 0.1896 |  | display |

### beat07_top_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0875 | >= 0.01 | display |
| mean luma | INFO | 0.3335 |  | display |

### beat07_top_look_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_look_up.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0168 | >= 0.01 | display |
| mean luma | INFO | 0.1743 |  | display |

### beat07_top_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0382 | >= 0.01 | display |
| mean luma | INFO | 0.2615 |  | display |

### beat07_top_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run07\beat07_top_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.0382 | >= 0.01 | display |
| mean luma | INFO | 0.2734 |  | display |

## Method notes

- Luma: Rec.709 weights; 'display' on the 8-bit encoded values / 255, 'linear' on the exact sRGB-decoded values. The 14.10.3 gates (p1, expected mean ranges, region std) use display luma unless --luma-scale linear.
- Clipped: any channel at code 255 (>= 0.999), percentage of all pixels outside the exclusion discs.
- NaN-like: display luma more than 0.30 above the max / 0.30 below the min of the 24 neighbours (isolated); beats 6-7 count bright outliers only when they also clip (stars).
- Banding (14.10.3 + R6 6.2): 64-px strips, 9-px box, steps > 0.6 code per 1000 px <= 2; longest run (< 0.05 code) <= 6 px only where |slope| >= 0.06 code/px; residual vs a 129-px quadratic trend RMS <= 0.10 and p-p <= 0.60 code.  Score = worst ratio to its limit (<= 1 passes).
- Thin line: crop 64 x 256 on linear luminance, flank-fitted background, FWHM per row (reported at 1080p scale), monotone 3-level edge on >= 95 % of rows, centroid residual <= 0.25 px, energy variation <= 10 %.
