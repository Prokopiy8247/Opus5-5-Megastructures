# Encode test: enc_trans_deck

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_trans_deck (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_trans_deck\enc_trans_deck` 1920x1080, 60 fps, 1800 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 302.752 | 351.21 | 60 | 26.487 | 181.651 | - | - | FAIL |
| x264 crf 23 | cli0 | 311.116 | 348.05 | 52 | 26.568 | 186.670 | 1468 | 1.095 | FAIL |
| x264 crf 30 | cli0 | 300.069 | 359.62 | 37 | 26.395 | 180.041 | 740 | 1.477 | FAIL |
| vp9 crf 31 | cli0 | 307.149 | 344.90 | 52 | 26.615 | 184.290 | 1482 | 0.896 | FAIL |
| vp9 crf 40 | cli0 | 305.649 | 348.05 | 45 | 26.478 | 183.389 | 772 | 1.002 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 311.1160 | score <= 1 | steps/1000 348.05, run 52, resid rms 26.568 pp 186.670 (source score 302.752), frame 0 |
| x264_crf30[cli0] | FAIL | 300.0690 | score <= 1 | steps/1000 359.62, run 37, resid rms 26.395 pp 180.041 (source score 302.752), frame 0 |
| vp9_crf31[cli0] | FAIL | 307.1490 | score <= 1 | steps/1000 344.90, run 52, resid rms 26.615 pp 184.290 (source score 302.752), frame 0 |
| vp9_crf40[cli0] | FAIL | 305.6490 | score <= 1 | steps/1000 348.05, run 45, resid rms 26.478 pp 183.389 (source score 302.752), frame 0 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf23_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf23_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf23_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf30_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf30_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\x264_crf30_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf31_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf31_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf31_cli0_f01028_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf40_cli0_f01028_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf40_cli0_f01028_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_trans_deck\enc_trans_deck\crops\vp9_crf40_cli0_f01028_x8.png`
