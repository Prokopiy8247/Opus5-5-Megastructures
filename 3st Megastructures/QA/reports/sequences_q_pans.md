# Frame sequences: q_pans

Sequences: 9. Checks: 18 PASS, 0 FAIL, 0 WARN, 0 SKIP.

**Result: PASS**

## Summary

| Sequence | Kind | Frames | Max luma step | Mean diff | Max diff | Max px diff | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| moire_far | transition | 60 | 0.0001 | 0.482 | 0.527 | 114.3 | PASS |  |
| moire_mid | transition | 60 | 0.0003 | 0.211 | 0.220 | 81.9 | PASS |  |
| moire_near | transition | 60 | 0.0000 | 0.086 | 0.086 | 14.6 | PASS |  |
| pan_beat05_spiral_down | pan | 5 | 0.0000 | 0.025 | 0.026 | 86.2 | PASS |  |
| pan_beat05_spiral_up | pan | 5 | 0.0000 | 0.023 | 0.024 | 51.6 | PASS |  |
| pan_beat06_clouds_down | pan | 5 | 0.0000 | 0.038 | 0.041 | 136.3 | PASS |  |
| pan_beat06_clouds_up | pan | 5 | 0.0000 | 0.070 | 0.070 | 92.2 | PASS |  |
| pan_beat07_top_down | pan | 5 | 0.0000 | 0.017 | 0.018 | 86.9 | PASS |  |
| pan_beat07_top_up | pan | 5 | 0.0000 | 0.073 | 0.075 | 142.3 | PASS |  |

## moire_far (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_moire_far`, 60 frames at 30 fps, kind transition

- luma      `▁▁▁▂▂▂▂▃▃▃▃▃▄▄▄▄▅▅▅▅▅▆▆▆▆▆▆▆▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇███`
- mean diff `▂▂▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▂▂▂▂▃▃▃▃▃▄▄▄▄▄▄▄▄▅▅▅▅▅▅▅▆▆▆▆▇▆▇▇▇██`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0001 | < 0.04 | at frame 9 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## moire_mid (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_moire_mid`, 60 frames at 30 fps, kind transition

- luma      `▁▁▂▂▂▂▃▃▃▃▄▄▄▅▅▅▅▅▆▆▆▆▆▆▇▇▇▇▇▇▇▇▇███████████████████▇▇▇▇▇▇▇▇`
- mean diff `▇▇▇▇███▇▇▆▆▅▅▅▄▄▄▅▄▃▃▂▂▂▃▄▃▃▃▇▇▃▃▂▂▂▂▁▁▂▃▃▃▃▃▂▃▃▃▄▅▅▅▅▄▃▂▂▂`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0003 | < 0.04 | at frame 1 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## moire_near (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_moire_near`, 60 frames at 30 fps, kind transition

- luma      `██▇▇▆▆▆▅▅▅▄▄▄▃▃▃▂▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▃▃▃▄▄▄▅▅▅▆▆▆▇▇▇`
- mean diff `▇██▇██▇████▇▇██▇▇▇█▇▇▆▆▅▆▅▆▆▇▁▁▇▆▆▆▆▅▇▅▆▇▇▇▇▇██▇▇█▇█▇██▇▇█▇`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 2 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat05_spiral_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat05_spiral_down`, 5 frames at 30 fps, kind pan

- luma      `▁▁▁▁▁`
- mean diff `▆▁▆█`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 2 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat05_spiral_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat05_spiral_up`, 5 frames at 30 fps, kind pan

- luma      `▁▁▁██`
- mean diff `▁▇█▆`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 3 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat06_clouds_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat06_clouds_down`, 5 frames at 30 fps, kind pan

- luma      `▁▅█▅▅`
- mean diff `▃█▁▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 2 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat06_clouds_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat06_clouds_up`, 5 frames at 30 fps, kind pan

- luma      `▁███▁`
- mean diff `▅▁▅█`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 4 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat07_top_down (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat07_top_down`, 5 frames at 30 fps, kind pan

- luma      `▁▁█▁▁`
- mean diff `▅█▇▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 2 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## pan_beat07_top_up (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_pans\seq_pan_beat07_top_up`, 5 frames at 30 fps, kind pan

- luma      `▁██▁█`
- mean diff `▅▂█▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 3 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |
