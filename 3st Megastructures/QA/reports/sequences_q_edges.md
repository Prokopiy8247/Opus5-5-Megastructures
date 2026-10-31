# Frame sequences: q_edges

Sequences: 2. Checks: 4 PASS, 0 FAIL, 0 WARN, 0 SKIP.

**Result: PASS**

## Summary

| Sequence | Kind | Frames | Max luma step | Mean diff | Max diff | Max px diff | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| end | transition | 502 | 0.0006 | 0.216 | 1.212 | 220.8 | PASS |  |
| start | transition | 60 | 0.0000 | 0.000 | 0.000 | 0.0 | PASS |  |

## end (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_edges\seq_end`, 502 frames at 30 fps, kind transition

- luma      `▁▁▁▁▁▂▂▂▃▄▅▆▇███████████████████████████████████████████████`
- mean diff `▄▆▇▇▇▇▇███▇▆▆▅▄▃▂▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0006 | < 0.04 | at frame 73 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## start (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\q_edges\seq_start`, 60 frames at 30 fps, kind transition

- luma      `▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`
- mean diff `▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0000 | < 0.04 | at frame 1 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |
