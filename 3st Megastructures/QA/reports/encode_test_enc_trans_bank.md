# Encode test: enc_trans_bank

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_trans_bank (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_trans_bank\enc_trans_bank` 1920x1080, 60 fps, 1800 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 73.081 | 146.16 | 29 | 3.592 | 33.577 | - | - | FAIL |
| x264 crf 23 | cli0 | 70.452 | 140.90 | 30 | 3.371 | 30.449 | 1078 | 0.846 | FAIL |
| x264 crf 30 | cli0 | 62.566 | 125.13 | 27 | 2.861 | 26.590 | 539 | 1.141 | FAIL |
| vp9 crf 31 | cli0 | 74.132 | 148.26 | 30 | 3.802 | 36.448 | 362 | 0.714 | FAIL |
| vp9 crf 40 | cli0 | 73.081 | 146.16 | 28 | 3.687 | 32.286 | 201 | 0.792 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 70.4520 | score <= 1 | steps/1000 140.90, run 30, resid rms 3.371 pp 30.449 (source score 73.081), frame 1799 |
| x264_crf30[cli0] | FAIL | 62.5660 | score <= 1 | steps/1000 125.13, run 27, resid rms 2.861 pp 26.590 (source score 73.081), frame 1799 |
| vp9_crf31[cli0] | FAIL | 74.1320 | score <= 1 | steps/1000 148.26, run 30, resid rms 3.802 pp 36.448 (source score 73.081), frame 1799 |
| vp9_crf40[cli0] | FAIL | 73.0810 | score <= 1 | steps/1000 146.16, run 28, resid rms 3.687 pp 32.286 (source score 73.081), frame 1799 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf23_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf23_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf23_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf30_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf30_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\x264_crf30_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf31_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf31_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf31_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf40_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf40_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_bank\enc_trans_bank\crops\vp9_crf40_cli0_f01028_x8.png`
