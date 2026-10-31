# Encode test: enc_beat06_sky

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat06_sky (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat06_sky\enc_beat06_sky` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 42.587 | 85.17 | 9 | 1.298 | 10.543 | - | - | FAIL |
| x264 crf 23 | cli0 | 41.009 | 82.02 | 22 | 1.307 | 10.635 | 1377 | 0.843 | FAIL |
| x264 crf 30 | cli0 | 41.535 | 83.07 | 28 | 1.324 | 11.617 | 646 | 1.077 | FAIL |
| vp9 crf 31 | cli0 | 41.535 | 83.07 | 23 | 1.300 | 10.154 | 532 | 0.764 | FAIL |
| vp9 crf 40 | cli0 | 43.113 | 86.23 | 25 | 1.295 | 10.123 | 251 | 0.814 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 41.0090 | score <= 1 | steps/1000 82.02, run 22, resid rms 1.307 pp 10.635 (source score 42.587), frame 257 |
| x264_crf30[cli0] | FAIL | 41.5350 | score <= 1 | steps/1000 83.07, run 28, resid rms 1.324 pp 11.617 (source score 42.587), frame 257 |
| vp9_crf31[cli0] | FAIL | 41.5350 | score <= 1 | steps/1000 83.07, run 23, resid rms 1.300 pp 10.154 (source score 42.587), frame 257 |
| vp9_crf40[cli0] | FAIL | 43.1130 | score <= 1 | steps/1000 86.23, run 25, resid rms 1.295 pp 10.123 (source score 42.587), frame 257 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sky\enc_beat06_sky\crops\vp9_crf40_cli0_f00342_x8.png`
