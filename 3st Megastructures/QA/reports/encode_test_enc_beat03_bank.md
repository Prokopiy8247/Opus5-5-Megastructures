# Encode test: enc_beat03_bank

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat03_bank (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat03_bank\enc_beat03_bank` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 0.715 | 0.00 | 1 | 0.071 | 0.383 | - | - | PASS |
| x264 crf 23 | cli0 | 4.712 | 0.00 | 13 | 0.344 | 2.827 | 1152 | 0.866 | FAIL |
| x264 crf 30 | cli0 | 5.154 | 0.00 | 20 | 0.515 | 2.599 | 575 | 1.226 | FAIL |
| vp9 crf 31 | cli0 | 5.431 | 0.00 | 20 | 0.543 | 2.846 | 409 | 0.736 | FAIL |
| vp9 crf 40 | cli0 | 5.666 | 0.00 | 17 | 0.567 | 2.477 | 222 | 0.824 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 4.7120 | score <= 1 | steps/1000 0.00, run 13, resid rms 0.344 pp 2.827 (source score 0.715), frame 0 |
| x264_crf30[cli0] | FAIL | 5.1540 | score <= 1 | steps/1000 0.00, run 20, resid rms 0.515 pp 2.599 (source score 0.715), frame 257 |
| vp9_crf31[cli0] | FAIL | 5.4310 | score <= 1 | steps/1000 0.00, run 20, resid rms 0.543 pp 2.846 (source score 0.715), frame 86 |
| vp9_crf40[cli0] | FAIL | 5.6660 | score <= 1 | steps/1000 0.00, run 17, resid rms 0.567 pp 2.477 (source score 0.715), frame 86 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat03_bank\enc_beat03_bank\crops\vp9_crf40_cli0_f00342_x8.png`
