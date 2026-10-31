# Image metrics: run08a

Capture folder: `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a`  
Scene constants: ThreadConstants.cs  
Images: 16, failing images: 6. Checks: 104 PASS, 8 FAIL, 0 WARN, 12 SKIP, 8 INFO.

**Result: FAIL**

## Input warnings

- beat03_stairs_KF3.png is not listed in report.json
- beat03_stairs_KF3_1610.png is not listed in report.json
- beat03_stairs_KF3_219.png is not listed in report.json
- beat03_stairs_KF3_4k.png is not listed in report.json
- beat03_stairs_KF3_s305.png is not listed in report.json
- beat03_stairs_KF3_s305_4k.png is not listed in report.json
- beat03_stairs_end.png is not listed in report.json
- beat03_stairs_look_back.png is not listed in report.json
- beat03_stairs_look_l.png is not listed in report.json
- beat03_stairs_look_r.png is not listed in report.json
- beat03_stairs_mid.png is not listed in report.json
- beat03_stairs_start.png is not listed in report.json

## Summary

| Image | Beat | Size | Mean luma | p1 | Clipped % | Band score | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| beat03_stairs_KF3 | 3 | 1920x1080 | 0.756 | 0.614 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_1610 | 3 | 1680x1050 | 0.756 | 0.614 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_219 | 3 | 2560x1080 | 0.757 | 0.614 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_4k | 3 | 3840x2160 | 0.756 | 0.614 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_s305 | 3 | 1920x1080 | 0.749 | 0.605 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_KF3_s305_4k | 3 | 3840x2160 | 0.749 | 0.605 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_end | 3 | 1920x1080 | 0.775 | 0.580 | 0.0000 | - | PASS |  |
| beat03_stairs_look_back | 3 | 1920x1080 | 0.874 | 0.832 | 0.0000 | - | PASS |  |
| beat03_stairs_look_l | 3 | 1920x1080 | 0.818 | 0.620 | 0.0000 | - | PASS |  |
| beat03_stairs_look_r | 3 | 1920x1080 | 0.818 | 0.620 | 0.0000 | - | PASS |  |
| beat03_stairs_mid | 3 | 1920x1080 | 0.743 | 0.598 | 0.0000 | - | PASS |  |
| beat03_stairs_start | 3 | 1920x1080 | 0.734 | 0.582 | 0.0000 | - | PASS |  |
| beat07_top_KF7b | 7 | 1920x1080 | 0.272 | 0.066 | 0.0000 | - | PASS |  |
| beat07_top_KF7b_1610 | 7 | 1680x1050 | 0.285 | 0.070 | 0.0000 | - | PASS |  |
| beat07_top_KF7b_219 | 7 | 2560x1080 | 0.236 | 0.065 | 0.0000 | - | PASS |  |
| beat07_top_KF7b_4k | 7 | 3840x2160 | 0.271 | 0.066 | 0.0000 | - | PASS |  |

## Key-shot coverage (14.10.2: every key shot at 1920x1080 and 3840x2160)

| Key shot | Requested sizes found | Status |
|---|---|---|
| beat01_salt_KF1 | - | INFO |
| beat02_avenue_KF2a | - | INFO |
| beat02_avenue_KF2b | - | INFO |
| beat02_avenue_KF2c | - | INFO |
| beat03_stairs_KF3 | 0x0 | INFO |
| beat04_tower_KF4a | - | INFO |
| beat04_tower_KF4b | - | INFO |
| beat05_spiral_KF5a | - | INFO |
| beat05_spiral_KF5b | - | INFO |
| beat05_spiral_KF5c | - | INFO |
| beat05_spiral_KF5d | - | INFO |
| beat06_clouds_KF6a | - | INFO |
| beat06_clouds_KF6a2 | - | INFO |
| beat06_clouds_KF6b | - | INFO |
| beat07_top_KF7a | - | INFO |
| beat07_top_KF7b | 1680x1050, 1920x1080, 2560x1080, 3840x2160 | PASS |
| beat07_top_KF7c | - | INFO |

## Details

### beat03_stairs_KF3 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.6138 | >= 0.25 | display |
| mean luma | FAIL | 0.7563 | 0.8..0.95 | display |

### beat03_stairs_KF3_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1680x1050 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2002 px excluded |
| luma p1 | PASS | 0.6138 | >= 0.25 | display |
| mean luma | FAIL | 0.7564 | 0.8..0.95 | display |

### beat03_stairs_KF3_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 2560x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.6140 | >= 0.25 | display |
| mean luma | FAIL | 0.7569 | 0.8..0.95 | display |

### beat03_stairs_KF3_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 3840x2160 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8452 px excluded |
| luma p1 | PASS | 0.6138 | >= 0.25 | display |
| mean luma | FAIL | 0.7563 | 0.8..0.95 | display |

### beat03_stairs_KF3_s305 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3_s305.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6051 | >= 0.25 | display |
| mean luma | INFO | 0.7493 |  | display |
| stats[centre_60x60] std | FAIL | 0.0834 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.0971 | < 0.07 |  |

### beat03_stairs_KF3_s305_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_KF3_s305_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 3840x2160 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6051 | >= 0.25 | display |
| mean luma | INFO | 0.7493 |  | display |
| stats[centre_60x60] std | FAIL | 0.0835 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.0971 | < 0.07 |  |

### beat03_stairs_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5798 | >= 0.25 | display |
| mean luma | INFO | 0.7753 |  | display |

### beat03_stairs_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.8318 | >= 0.25 | display |
| mean luma | INFO | 0.8744 |  | display |

### beat03_stairs_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6202 | >= 0.25 | display |
| mean luma | INFO | 0.8184 |  | display |

### beat03_stairs_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6202 | >= 0.25 | display |
| mean luma | INFO | 0.8181 |  | display |

### beat03_stairs_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5981 | >= 0.25 | display |
| mean luma | INFO | 0.7428 |  | display |

### beat03_stairs_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat03_stairs_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5823 | >= 0.25 | display |
| mean luma | INFO | 0.7343 |  | display |

### beat07_top_KF7b (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat07_top_KF7b.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5979 px excluded |
| luma p1 | PASS | 0.0662 | >= 0.01 | display |
| mean luma | PASS | 0.2715 | 0.08..0.3 | display |

### beat07_top_KF7b_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat07_top_KF7b_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1680x1050 | 1680x1050 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5654 px excluded |
| luma p1 | PASS | 0.0695 | >= 0.01 | display |
| mean luma | PASS | 0.2851 | 0.08..0.3 | display |

### beat07_top_KF7b_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat07_top_KF7b_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 2560x1080 | 2560x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 5979 px excluded |
| luma p1 | PASS | 0.0648 | >= 0.01 | display |
| mean luma | PASS | 0.2358 | 0.08..0.3 | display |

### beat07_top_KF7b_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run08a\beat07_top_KF7b_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 3840x2160 | 3840x2160 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode clipped), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.15 % | any channel at 255; 23915 px excluded |
| luma p1 | PASS | 0.0662 | >= 0.01 | display |
| mean luma | PASS | 0.2712 | 0.08..0.3 | display |

## Method notes

- Luma: Rec.709 weights; 'display' on the 8-bit encoded values / 255, 'linear' on the exact sRGB-decoded values. The 14.10.3 gates (p1, expected mean ranges, region std) use display luma unless --luma-scale linear.
- Clipped: any channel at code 255 (>= 0.999), percentage of all pixels outside the exclusion discs.
- NaN-like: display luma more than 0.30 above the max / 0.30 below the min of the 24 neighbours (isolated); beats 6-7 count bright outliers only when they also clip (stars).
- Banding (14.10.3 + R6 6.2): 64-px strips, 9-px box, steps > 0.6 code per 1000 px <= 2; longest run (< 0.05 code) <= 6 px only where |slope| >= 0.06 code/px; residual vs a 129-px quadratic trend RMS <= 0.10 and p-p <= 0.60 code.  Score = worst ratio to its limit (<= 1 passes).
- Thin line: crop 64 x 256 on linear luminance, flank-fitted background, FWHM per row (reported at 1080p scale), monotone 3-level edge on >= 95 % of rows, centroid residual <= 0.25 px, energy variation <= 10 %.
