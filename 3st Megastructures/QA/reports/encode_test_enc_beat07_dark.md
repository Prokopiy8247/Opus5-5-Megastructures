# Encode test: enc_beat07_dark

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat07_dark (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat07_dark\enc_beat07_dark` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 344.013 | 215.56 | 28 | 22.522 | 206.408 | - | - | FAIL |
| x264 crf 23 | cli0 | 343.141 | 207.15 | 29 | 22.560 | 205.884 | 808 | 0.884 | FAIL |
| x264 crf 30 | cli0 | 343.390 | 205.05 | 27 | 22.354 | 206.034 | 386 | 1.196 | FAIL |
| vp9 crf 31 | cli0 | 345.787 | 208.20 | 29 | 22.596 | 207.472 | 628 | 0.798 | FAIL |
| vp9 crf 40 | cli0 | 346.210 | 205.05 | 29 | 22.648 | 207.726 | 356 | 0.859 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 343.1410 | score <= 1 | steps/1000 207.15, run 29, resid rms 22.560 pp 205.884 (source score 344.013), frame 171 |
| x264_crf30[cli0] | FAIL | 343.3900 | score <= 1 | steps/1000 205.05, run 27, resid rms 22.354 pp 206.034 (source score 344.013), frame 86 |
| vp9_crf31[cli0] | FAIL | 345.7870 | score <= 1 | steps/1000 208.20, run 29, resid rms 22.596 pp 207.472 (source score 344.013), frame 171 |
| vp9_crf40[cli0] | FAIL | 346.2100 | score <= 1 | steps/1000 205.05, run 29, resid rms 22.648 pp 207.726 (source score 344.013), frame 86 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_dark\enc_beat07_dark\crops\vp9_crf40_cli0_f00342_x8.png`
