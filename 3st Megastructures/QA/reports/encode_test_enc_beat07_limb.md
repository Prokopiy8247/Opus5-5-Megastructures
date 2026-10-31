# Encode test: enc_beat07_limb

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 4 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat07_limb (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat07_limb\enc_beat07_limb` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 26.820 | 53.64 | 8 | 1.034 | 6.349 | - | - | FAIL |
| x264 crf 23 | cli0 | 26.820 | 53.64 | 19 | 1.047 | 6.653 | 1553 | 1.237 | FAIL |
| x264 crf 30 | cli0 | 26.820 | 53.64 | 26 | 1.071 | 7.047 | 808 | 1.671 | FAIL |
| vp9 crf 31 | cli0 | 26.820 | 53.64 | 23 | 1.074 | 7.089 | 1837 | 1.031 | FAIL |
| vp9 crf 40 | cli0 | 28.736 | 57.47 | 26 | 1.168 | 7.401 | 934 | 1.157 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 26.8200 | score <= 1 | steps/1000 53.64, run 19, resid rms 1.047 pp 6.653 (source score 26.820), frame 0 |
| x264_crf30[cli0] | FAIL | 26.8200 | score <= 1 | steps/1000 53.64, run 26, resid rms 1.071 pp 7.047 (source score 26.820), frame 257 |
| vp9_crf31[cli0] | FAIL | 26.8200 | score <= 1 | steps/1000 53.64, run 23, resid rms 1.074 pp 7.089 (source score 26.820), frame 0 |
| vp9_crf40[cli0] | FAIL | 28.7360 | score <= 1 | steps/1000 57.47, run 26, resid rms 1.168 pp 7.401 (source score 26.820), frame 0 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat07_limb\enc_beat07_limb\crops\vp9_crf40_cli0_f00342_x8.png`
