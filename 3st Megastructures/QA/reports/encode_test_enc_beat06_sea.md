# Encode test: enc_beat06_sea

ffmpeg: `C:\Users\<user>\AppData\Local\Microsoft\WinGet\Links\ffmpeg.EXE`, encoders: libvpx-vp9, libx264

Sequences: 1. Checks: 0 PASS, 8 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## enc_beat06_sea (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\enc_enc_beat06_sea\enc_beat06_sea` 1920x1080, 60 fps, 600 frames encoded

| Stream | Region | Score | Steps/1000 | Run px | Resid RMS | Resid p-p | kbit/s | MAE codes | Result |
|---|---|---|---|---|---|---|---|---|---|
| source | cli0 | 281.903 | 563.81 | 26 | 16.215 | 95.077 | - | - | FAIL |
| source | cli1 | 5.996 | 7.67 | 2 | 0.477 | 3.598 | - | - | FAIL |
| x264 crf 23 | cli0 | 281.903 | 563.81 | 24 | 16.082 | 92.127 | 1626 | 1.165 | FAIL |
| x264 crf 23 | cli1 | 7.979 | 10.23 | 9 | 0.798 | 4.298 | 1626 | 1.165 | FAIL |
| x264 crf 30 | cli0 | 265.661 | 531.32 | 24 | 16.115 | 92.757 | 769 | 1.677 | FAIL |
| x264 crf 30 | cli1 | 10.280 | 5.12 | 13 | 1.028 | 5.126 | 769 | 1.677 | FAIL |
| vp9 crf 31 | cli0 | 278.422 | 556.84 | 24 | 16.236 | 95.734 | 1354 | 0.969 | FAIL |
| vp9 crf 31 | cli1 | 8.216 | 7.67 | 10 | 0.715 | 4.929 | 1354 | 0.969 | FAIL |
| vp9 crf 40 | cli0 | 278.422 | 556.84 | 25 | 16.219 | 95.159 | 714 | 1.122 | FAIL |
| vp9 crf 40 | cli1 | 8.534 | 7.67 | 11 | 0.707 | 5.120 | 714 | 1.122 | FAIL |

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| x264_crf23[cli0] | FAIL | 281.9030 | score <= 1 | steps/1000 563.81, run 24, resid rms 16.082 pp 92.127 (source score 281.903), frame 428 |
| x264_crf23[cli1] | FAIL | 7.9790 | score <= 1 | steps/1000 10.23, run 9, resid rms 0.798 pp 4.298 (source score 5.996), frame 428 |
| x264_crf30[cli0] | FAIL | 265.6610 | score <= 1 | steps/1000 531.32, run 24, resid rms 16.115 pp 92.757 (source score 281.903), frame 428 |
| x264_crf30[cli1] | FAIL | 10.2800 | score <= 1 | steps/1000 5.12, run 13, resid rms 1.028 pp 5.126 (source score 5.996), frame 0 |
| vp9_crf31[cli0] | FAIL | 278.4220 | score <= 1 | steps/1000 556.84, run 24, resid rms 16.236 pp 95.734 (source score 281.903), frame 428 |
| vp9_crf31[cli1] | FAIL | 8.2160 | score <= 1 | steps/1000 7.67, run 10, resid rms 0.715 pp 4.929 (source score 5.996), frame 599 |
| vp9_crf40[cli0] | FAIL | 278.4220 | score <= 1 | steps/1000 556.84, run 25, resid rms 16.219 pp 95.159 (source score 281.903), frame 428 |
| vp9_crf40[cli1] | FAIL | 8.5340 | score <= 1 | steps/1000 7.67, run 11, resid rms 0.707 pp 5.120 (source score 5.996), frame 599 |

Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):

- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli1_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli1_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf23_cli1_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli1_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli1_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\x264_crf30_cli1_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli1_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli1_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf31_cli1_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli0_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli0_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli0_f00342_x8.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli1_f00342_100.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli1_f00342_200.png`
- `W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\encode\enc_beat06_sea\enc_beat06_sea\crops\vp9_crf40_cli1_f00342_x8.png`
