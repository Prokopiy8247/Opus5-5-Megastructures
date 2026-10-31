# Encode test: enc_beat02_fog

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat02_fog (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat02_fog\enc_beat02_fog` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 51.527 | 92.42 | 8 | 3.174 | 30.916 | - | - | FAIL |
| x264 crf 23 | cli0 | 53.447 | 79.48 | 14 | 3.280 | 32.068 | 749 | 0.786 | FAIL |
| x264 crf 30 | cli0 | 42.514 | 85.03 | 25 | 2.760 | 22.161 | 265 | 1.017 | FAIL |
| vp9 crf 31 | cli0 | 48.638 | 85.03 | 11 | 3.089 | 29.183 | 294 | 0.686 | FAIL |
| vp9 crf 40 | cli0 | 49.173 | 90.57 | 13 | 3.161 | 29.504 | 149 | 0.768 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 53.4470 | score <= 1 | steps/1000 79.48, run 14, resid rms 3.280 pp 32.068 (source score 51.527), frame 342 |
| x264_crf30[cli0] | FAIL | 42.5140 | score <= 1 | steps/1000 85.03, run 25, resid rms 2.760 pp 22.161 (source score 51.527), frame 86 |
| vp9_crf31[cli0] | FAIL | 48.6380 | score <= 1 | steps/1000 85.03, run 11, resid rms 3.089 pp 29.183 (source score 51.527), frame 599 |
| vp9_crf40[cli0] | FAIL | 49.1730 | score <= 1 | steps/1000 90.57, run 13, resid rms 3.161 pp 29.504 (source score 51.527), frame 342 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat02_fog\enc_beat02_fog\crops\vp9_crf40_cli0_f00342_x8.png`
