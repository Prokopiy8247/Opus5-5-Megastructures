# Encode test: enc_beat04_shade

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat04_shade (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat04_shade\enc_beat04_shade` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 173.085 | 79.43 | 31 | 17.308 | 101.498 | - | - | FAIL |
| x264 crf 23 | cli0 | 170.489 | 87.58 | 27 | 17.049 | 98.073 | 1310 | 0.972 | FAIL |
| x264 crf 30 | cli0 | 168.550 | 95.72 | 29 | 16.855 | 94.511 | 632 | 1.434 | FAIL |
| vp9 crf 31 | cli0 | 172.342 | 81.47 | 28 | 17.234 | 102.933 | 532 | 0.850 | FAIL |
| vp9 crf 40 | cli0 | 173.412 | 97.76 | 29 | 16.941 | 104.047 | 276 | 0.998 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 170.4890 | score <= 1 | steps/1000 87.58, run 27, resid rms 17.049 pp 98.073 (source score 173.085), frame 599 |
| x264_crf30[cli0] | FAIL | 168.5500 | score <= 1 | steps/1000 95.72, run 29, resid rms 16.855 pp 94.511 (source score 173.085), frame 599 |
| vp9_crf31[cli0] | FAIL | 172.3420 | score <= 1 | steps/1000 81.47, run 28, resid rms 17.234 pp 102.933 (source score 173.085), frame 599 |
| vp9_crf40[cli0] | FAIL | 173.4120 | score <= 1 | steps/1000 97.76, run 29, resid rms 16.941 pp 104.047 (source score 173.085), frame 513 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat04_shade\enc_beat04_shade\crops\vp9_crf40_cli0_f00342_x8.png`
