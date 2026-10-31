# Encode test: enc_beat01_sky

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat01_sky (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat01_sky\enc_beat01_sky` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 0.459 | 0.00 | 1 | 0.046 | 0.213 | - | - | PASS |
| x264 crf 23 | cli0 | 6.273 | 0.00 | 20 | 0.627 | 2.524 | 536 | 0.832 | FAIL |
| x264 crf 30 | cli0 | 10.284 | 0.00 | 23 | 1.028 | 4.569 | 186 | 1.082 | FAIL |
| vp9 crf 31 | cli0 | 6.499 | 0.00 | 23 | 0.650 | 2.521 | 224 | 0.718 | FAIL |
| vp9 crf 40 | cli0 | 6.174 | 0.00 | 24 | 0.617 | 3.106 | 127 | 0.794 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 6.2730 | score <= 1 | steps/1000 0.00, run 20, resid rms 0.627 pp 2.524 (source score 0.459), frame 513 |
| x264_crf30[cli0] | FAIL | 10.2840 | score <= 1 | steps/1000 0.00, run 23, resid rms 1.028 pp 4.569 (source score 0.459), frame 0 |
| vp9_crf31[cli0] | FAIL | 6.4990 | score <= 1 | steps/1000 0.00, run 23, resid rms 0.650 pp 2.521 (source score 0.459), frame 513 |
| vp9_crf40[cli0] | FAIL | 6.1740 | score <= 1 | steps/1000 0.00, run 24, resid rms 0.617 pp 3.106 (source score 0.459), frame 513 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat01_sky\enc_beat01_sky\crops\vp9_crf40_cli0_f00342_x8.png`
