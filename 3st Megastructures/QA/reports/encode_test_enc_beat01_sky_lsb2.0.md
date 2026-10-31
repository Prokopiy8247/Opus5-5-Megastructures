# Encode test: enc_beat01_sky_lsb2.0

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat01_sky (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat01_sky\enc_beat01_sky` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 0.333 | 0.00 | 1 | 0.033 | 0.178 | - | - | PASS |
| x264 crf 23 | cli0 | 6.536 | 0.00 | 26 | 0.654 | 2.455 | 488 | 1.039 | FAIL |
| x264 crf 30 | cli0 | 13.618 | 0.00 | 28 | 1.362 | 4.927 | 178 | 1.337 | FAIL |
| vp9 crf 31 | cli0 | 5.862 | 0.00 | 23 | 0.586 | 2.209 | 236 | 0.931 | FAIL |
| vp9 crf 40 | cli0 | 6.382 | 0.00 | 18 | 0.638 | 3.338 | 128 | 0.993 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 6.5360 | score <= 1 | steps/1000 0.00, run 26, resid rms 0.654 pp 2.455 (source score 0.333), frame 0 |
| x264_crf30[cli0] | FAIL | 13.6180 | score <= 1 | steps/1000 0.00, run 28, resid rms 1.362 pp 4.927 (source score 0.333), frame 86 |
| vp9_crf31[cli0] | FAIL | 5.8620 | score <= 1 | steps/1000 0.00, run 23, resid rms 0.586 pp 2.209 (source score 0.333), frame 0 |
| vp9_crf40[cli0] | FAIL | 6.3820 | score <= 1 | steps/1000 0.00, run 18, resid rms 0.638 pp 3.338 (source score 0.333), frame 171 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky_lsb2.0\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_x8.png`
