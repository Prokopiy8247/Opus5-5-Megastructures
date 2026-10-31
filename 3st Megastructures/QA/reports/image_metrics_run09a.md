# Image metrics: run09a

Capture folder: `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a`  
Scene constants: ThreadConstants.cs  
Images: 22, failing images: 7. Checks: 141 PASS, 9 FAIL, 0 WARN, 16 SKIP, 14 INFO.

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
- beat04_tower_KF4a.png is not listed in report.json
- beat04_tower_KF4a_1610.png is not listed in report.json
- beat04_tower_KF4a_219.png is not listed in report.json
- beat04_tower_KF4a_4k.png is not listed in report.json

## Summary

| Image | Beat | Size | Mean luma | p1 | Clipped % | Band score | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| beat03_stairs_KF3 | 3 | 1920x1080 | 0.761 | 0.625 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_1610 | 3 | 1680x1050 | 0.762 | 0.625 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_219 | 3 | 2560x1080 | 0.761 | 0.625 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_4k | 3 | 3840x2160 | 0.761 | 0.625 | 0.0000 | - | FAIL | mean luma |
| beat03_stairs_KF3_s305 | 3 | 1920x1080 | 0.755 | 0.617 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_KF3_s305_4k | 3 | 3840x2160 | 0.755 | 0.617 | 0.0000 | - | FAIL | stats[centre_60x60] std, stats[whole_frame] std |
| beat03_stairs_end | 3 | 1920x1080 | 0.769 | 0.592 | 0.0000 | - | PASS |  |
| beat03_stairs_look_back | 3 | 1920x1080 | 0.874 | 0.829 | 0.0000 | - | PASS |  |
| beat03_stairs_look_l | 3 | 1920x1080 | 0.820 | 0.631 | 0.0000 | - | PASS |  |
| beat03_stairs_look_r | 3 | 1920x1080 | 0.820 | 0.631 | 0.0000 | - | PASS |  |
| beat03_stairs_mid | 3 | 1920x1080 | 0.748 | 0.610 | 0.0000 | - | PASS |  |
| beat03_stairs_start | 3 | 1920x1080 | 0.741 | 0.597 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a | 4 | 1920x1080 | 0.672 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_1610 | 4 | 1680x1050 | 0.658 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_219 | 4 | 2560x1080 | 0.703 | 0.400 | 0.0000 | - | PASS |  |
| beat04_tower_KF4a_4k | 4 | 3840x2160 | 0.672 | 0.399 | 0.0000 | - | PASS |  |
| beat04_tower_end | 4 | 1920x1080 | 0.708 | 0.120 | 0.0000 | - | FAIL | luma p1 |
| beat04_tower_look_back | 4 | 1920x1080 | 0.814 | 0.686 | 0.0000 | - | PASS |  |
| beat04_tower_look_l | 4 | 1920x1080 | 0.790 | 0.686 | 0.0000 | - | PASS |  |
| beat04_tower_look_r | 4 | 1920x1080 | 0.651 | 0.299 | 0.0000 | - | PASS |  |
| beat04_tower_mid | 4 | 1920x1080 | 0.702 | 0.305 | 0.0000 | - | PASS |  |
| beat04_tower_start | 4 | 1920x1080 | 0.768 | 0.682 | 0.0000 | - | PASS |  |

## Key-shot coverage (14.10.2: every key shot at 1920x1080 and 3840x2160)

| Key shot | Requested sizes found | Status |
|---|---|---|
| beat01_salt_KF1 | - | INFO |
| beat02_avenue_KF2a | - | INFO |
| beat02_avenue_KF2b | - | INFO |
| beat02_avenue_KF2c | - | INFO |
| beat03_stairs_KF3 | 0x0 | INFO |
| beat04_tower_KF4a | 0x0 | INFO |
| beat04_tower_KF4b | - | INFO |
| beat05_spiral_KF5a | - | INFO |
| beat05_spiral_KF5b | - | INFO |
| beat05_spiral_KF5c | - | INFO |
| beat05_spiral_KF5d | - | INFO |
| beat06_clouds_KF6a | - | INFO |
| beat06_clouds_KF6a2 | - | INFO |
| beat06_clouds_KF6b | - | INFO |
| beat07_top_KF7a | - | INFO |
| beat07_top_KF7b | - | INFO |
| beat07_top_KF7c | - | INFO |

## Details

### beat03_stairs_KF3 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.6250 | >= 0.25 | display |
| mean luma | FAIL | 0.7613 | 0.8..0.95 | display |

### beat03_stairs_KF3_1610 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1680x1050 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2002 px excluded |
| luma p1 | PASS | 0.6250 | >= 0.25 | display |
| mean luma | FAIL | 0.7615 | 0.8..0.95 | display |

### beat03_stairs_KF3_219 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 2560x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2115 px excluded |
| luma p1 | PASS | 0.6252 | >= 0.25 | display |
| mean luma | FAIL | 0.7615 | 0.8..0.95 | display |

### beat03_stairs_KF3_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 3840x2160 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 8452 px excluded |
| luma p1 | PASS | 0.6250 | >= 0.25 | display |
| mean luma | FAIL | 0.7613 | 0.8..0.95 | display |

### beat03_stairs_KF3_s305 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3_s305.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6171 | >= 0.25 | display |
| mean luma | INFO | 0.7546 |  | display |
| stats[centre_60x60] std | FAIL | 0.0798 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.0929 | < 0.07 |  |

### beat03_stairs_KF3_s305_4k (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_KF3_s305_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 3840x2160 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6171 | >= 0.25 | display |
| mean luma | INFO | 0.7545 |  | display |
| stats[centre_60x60] std | FAIL | 0.0799 | < 0.04 |  |
| stats[whole_frame] std | FAIL | 0.0930 | < 0.07 |  |

### beat03_stairs_end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5919 | >= 0.25 | display |
| mean luma | INFO | 0.7693 |  | display |

### beat03_stairs_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.8293 | >= 0.25 | display |
| mean luma | INFO | 0.8743 |  | display |

### beat03_stairs_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6309 | >= 0.25 | display |
| mean luma | INFO | 0.8204 |  | display |

### beat03_stairs_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6309 | >= 0.25 | display |
| mean luma | INFO | 0.8201 |  | display |

### beat03_stairs_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6098 | >= 0.25 | display |
| mean luma | INFO | 0.7483 |  | display |

### beat03_stairs_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat03_stairs_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.5966 | >= 0.25 | display |
| mean luma | INFO | 0.7415 |  | display |

### beat04_tower_KF4a (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_KF4a.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1920x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6722 | 0.45..0.75 | display |

### beat04_tower_KF4a_1610 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_KF4a_1610.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 1680x1050 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3990 | >= 0.25 | display |
| mean luma | PASS | 0.6584 | 0.45..0.75 | display |

### beat04_tower_KF4a_219 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_KF4a_219.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 2560x1080 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.4002 | >= 0.25 | display |
| mean luma | PASS | 0.7029 | 0.45..0.75 | display |

### beat04_tower_KF4a_4k (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_KF4a_4k.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | SKIP | 3840x2160 |  | no requested size in report.json |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.3993 | >= 0.25 | display |
| mean luma | PASS | 0.6722 | 0.45..0.75 | display |

### beat04_tower_end (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_end.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 2188 px excluded |
| luma p1 | FAIL | 0.1196 | >= 0.25 | display |
| mean luma | INFO | 0.7076 |  | display |

### beat04_tower_look_back (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_look_back.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6865 | >= 0.25 | display |
| mean luma | INFO | 0.8137 |  | display |

### beat04_tower_look_l (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_look_l.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.6865 | >= 0.25 | display |
| mean luma | INFO | 0.7903 |  | display |

### beat04_tower_look_r (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_look_r.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 0 px excluded |
| luma p1 | PASS | 0.2991 | >= 0.25 | display |
| mean luma | INFO | 0.6511 |  | display |

### beat04_tower_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_mid.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1953 px excluded |
| luma p1 | PASS | 0.3048 | >= 0.25 | display |
| mean luma | INFO | 0.7023 |  | display |

### beat04_tower_start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\run09a\beat04_tower_start.png`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| size | PASS | 1920x1080 | 1920x1080 |  |
| black frame | PASS | 0.0000 | < 0.98 of pixels <= code 3 |  |
| white frame | PASS | 0.0000 | < 0.98 of pixels >= code 252 |  |
| magenta | PASS | 0 | 0 px | ratio 0 (exact 255,0,255: 0 px) |
| NaN-like pixels | PASS | 0 | 0 | bright 0 (mode all), dark 0 |
| clipped highlights | PASS | 0.0000 | <= 0.05 % | any channel at 255; 1943 px excluded |
| luma p1 | PASS | 0.6821 | >= 0.25 | display |
| mean luma | INFO | 0.7682 |  | display |

## Method notes

- Luma: Rec.709 weights; 'display' on the 8-bit encoded values / 255, 'linear' on the exact sRGB-decoded values. The 14.10.3 gates (p1, expected mean ranges, region std) use display luma unless --luma-scale linear.
- Clipped: any channel at code 255 (>= 0.999), percentage of all pixels outside the exclusion discs.
- NaN-like: display luma more than 0.30 above the max / 0.30 below the min of the 24 neighbours (isolated); beats 6-7 count bright outliers only when they also clip (stars).
- Banding (14.10.3 + R6 6.2): 64-px strips, 9-px box, steps > 0.6 code per 1000 px <= 2; longest run (< 0.05 code) <= 6 px only where |slope| >= 0.06 code/px; residual vs a 129-px quadratic trend RMS <= 0.10 and p-p <= 0.60 code.  Score = worst ratio to its limit (<= 1 passes).
- Thin line: crop 64 x 256 on linear luminance, flank-fitted background, FWHM per row (reported at 1080p scale), monotone 3-level edge on >= 95 % of rows, centroid residual <= 0.25 px, energy variation <= 10 %.
