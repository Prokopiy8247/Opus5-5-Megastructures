# Encode test: enc_beat05_sky

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat05_sky (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat05_sky\enc_beat05_sky` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 60.134 | 35.75 | 26 | 4.279 | 36.080 | - | - | FAIL |
| x264 crf 23 | cli0 | 59.099 | 34.70 | 20 | 4.233 | 35.459 | 209 | 0.691 | FAIL |
| x264 crf 30 | cli0 | 60.947 | 35.75 | 23 | 4.371 | 36.568 | 96 | 0.867 | FAIL |
| vp9 crf 31 | cli0 | 59.203 | 36.80 | 21 | 4.217 | 35.522 | 88 | 0.622 | FAIL |
| vp9 crf 40 | cli0 | 59.896 | 36.80 | 21 | 4.263 | 35.937 | 58 | 0.668 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 59.0990 | score <= 1 | steps/1000 34.70, run 20, resid rms 4.233 pp 35.459 (source score 60.134), frame 513 |
| x264_crf30[cli0] | FAIL | 60.9470 | score <= 1 | steps/1000 35.75, run 23, resid rms 4.371 pp 36.568 (source score 60.134), frame 513 |
| vp9_crf31[cli0] | FAIL | 59.2030 | score <= 1 | steps/1000 36.80, run 21, resid rms 4.217 pp 35.522 (source score 60.134), frame 513 |
| vp9_crf40[cli0] | FAIL | 59.8960 | score <= 1 | steps/1000 36.80, run 21, resid rms 4.263 pp 35.937 (source score 60.134), frame 513 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat05_sky\enc_beat05_sky\crops\vp9_crf40_cli0_f00342_x8.png`
