# Encode test: enc_beat01_sky_lsb1.5

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat01_sky (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat01_sky\enc_beat01_sky` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 0.333 | 0.00 | 2 | 0.030 | 0.150 | - | - | PASS |
| x264 crf 23 | cli0 | 6.071 | 0.00 | 23 | 0.607 | 2.346 | 484 | 0.919 | FAIL |
| x264 crf 30 | cli0 | 12.953 | 0.00 | 26 | 1.295 | 4.487 | 179 | 1.203 | FAIL |
| vp9 crf 31 | cli0 | 6.020 | 0.00 | 23 | 0.602 | 2.387 | 225 | 0.814 | FAIL |
| vp9 crf 40 | cli0 | 5.989 | 0.00 | 21 | 0.599 | 2.999 | 126 | 0.883 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 6.0710 | score <= 1 | steps/1000 0.00, run 23, resid rms 0.607 pp 2.346 (source score 0.333), frame 0 |
| x264_crf30[cli0] | FAIL | 12.9530 | score <= 1 | steps/1000 0.00, run 26, resid rms 1.295 pp 4.487 (source score 0.333), frame 171 |
| vp9_crf31[cli0] | FAIL | 6.0200 | score <= 1 | steps/1000 0.00, run 23, resid rms 0.602 pp 2.387 (source score 0.333), frame 171 |
| vp9_crf40[cli0] | FAIL | 5.9890 | score <= 1 | steps/1000 0.00, run 21, resid rms 0.599 pp 2.999 (source score 0.333), frame 428 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb1.5\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_x8.png`
