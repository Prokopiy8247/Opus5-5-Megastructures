# Frame sequences: seq02

Sequences: 9. Checks: 18 PASS, 1 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## Summary

| Sequence | Kind | Frames | Max luma step | Mean diff | Max diff | Max px diff | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| b12 | transition | 601 | 0.0001 | 0.109 | 0.149 | 81.1 | PASS |  |
| b23 | transition | 801 | 0.0036 | 2.328 | 4.867 | 62.2 | PASS |  |
| b34 | transition | 1661 | 0.0040 | 1.862 | 3.720 | 75.8 | PASS |  |
| b45 | transition | 601 | 0.0029 | 1.378 | 3.211 | 198.1 | PASS |  |
| b56 | transition | 801 | 0.0117 | 1.861 | 5.745 | 205.9 | PASS |  |
| b67 | transition | 601 | 0.0023 | 1.111 | 2.060 | 226.8 | PASS |  |
| bankexit | transition | 301 | 0.0006 | 0.155 | 0.243 | 75.8 | PASS |  |
| deck | transition | 801 | 0.0117 | 1.870 | 5.745 | 205.9 | PASS |  |
| swap | swap | 201 | 0.0029 | 2.630 | 3.535 | 48.4 | FAIL | T-SWAP consecutive diff |

## b12 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b12`, 601 frames at 30 fps, kind transition

- luma      `███████████████▇▇▇▇▇▇▆▆▆▆▅▅▅▄▄▄▃▃▃▃▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▃▃▃▃`
- mean diff `▂▂▂▃▂▃▃▃▂▂▃▃▁▁▂▃▃▂▃▄▄▃▃▅▅▃▃▄▅▄▄▅▅▄▄▅▅▅▄▅▅▄▅▅▆▆▄▆▇▇▆▆▇▇▆▆▇██▇`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0001 | < 0.04 | at frame 309 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b23 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b23`, 801 frames at 30 fps, kind transition

- luma      `███████████▇▇▇▇▆▅▄▃▃▂▂▁▁▁▂▂▂▂▃▃▃▃▃▄▄▄▄▄▄▄▄▄▄▄▃▃▃▃▃▃▃▃▃▃▃▄▃▃▃`
- mean diff `▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▃▃▄▄▄▄▄████▇▇▇▇▇▇█▇▆▆▆▆▆▆▆▆▆▆▇▆▆▅▅▅▅▅▅▅▅▆▆▅▅▅`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0036 | < 0.04 | at frame 431 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b34 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b34`, 1661 frames at 30 fps, kind transition

- luma      `▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▅▄▄▄▄▄▄▆████▇▆▆▅▄▃▃▂▂▂▂▂▁▁▁▁`
- mean diff `█▆▆▆▆▇█▆▆▆▆█▆▆▆▆▇█▆▆▆▆▇█▆▆▆▆▇█▆▆▇▇▇█▇▇▇▇▆▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0040 | < 0.04 | at frame 1122 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b45 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b45`, 601 frames at 30 fps, kind transition

- luma      `▇▇▇▇▇███████████▇▆▆▆▅▅▅▄▄▃▃▂▂▁▁▂▂▁▁▃▃▃▂▃▃▃▂▃▂▂▂▄▄▄▃▄▄▄▃▄▄▃▃▄`
- mean diff `▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▂▂▂▂▂▂▃▃▃▄▅▆▆▅▅▆▆▅▆▇▇▄▆██▄▆██▅▆██▄▆██▄▆██▄▆██`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0029 | < 0.04 | at frame 474 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b56 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b56`, 801 frames at 30 fps, kind transition

- luma      `▄▅▅▄▅▄▄▅▄▄▄▄▄▅▄▄▄▄▆█████████████████▇█▇▇▇▆▄▂▂▂▂▂▁▂▁▁▁▁▁▁▁▁▁▁`
- mean diff `▄▂▃▄▂▃▄▂▃▄▂▃▄▂▃▄▂▃▆▄▁▂▁▁▂▁▁▂▁▁▂▁▁▂▂▂▃▂▂▃▂▇█▃▃▄▃▃▄▃▃▄▃▃▄▃▃▄▃▃`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0117 | < 0.04 | at frame 563 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b67 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_b67`, 601 frames at 30 fps, kind transition

- luma      `▇███▇██▇▆▇▇▇▇▇▇▇▇▇▇▇▅▅▄▄▃▃▃▂▁▂▂▁▁▂▂▂▂▃▃▃▃▄▃▃▂▃▃▂▂▃▃▂▂▂▂▂▂▂▂▂`
- mean diff `██▂▆█▇▂▆█▇▂▆█▇▂▆█▇▂▆██▂▅▆▆▁▅▆▅▁▄▆▅▁▄▆▅▂▅▆▅▂▄▅▅▁▃▅▄▁▄▅▄▁▄▅▅▂▄`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0023 | < 0.04 | at frame 15 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## bankexit (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_bankexit`, 301 frames at 30 fps, kind transition

- luma      `██▇▇▆▆▆▅▅▅▅▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▃▃▃▃▃▃▃▂▂▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`
- mean diff `██▇▇▇▆▆▆▅▅▆▆▆▆▆▇▅▄▃▂▂▂▂▂▁▁▁▁▁▁▁▁▁▂▂▁▁▁▁▂▁▁▁▁▁▁▁▂▂▂▂▂▂▂▂▂▂▃▃▃`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0006 | < 0.04 | at frame 1 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## deck (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_deck`, 801 frames at 30 fps, kind transition

- luma      `▄▅▄▄▅▄▄▅▄▄▄▄▆▇████████████████▇█▇▇▇▆▄▂▂▂▂▂▁▂▂▁▂▁▁▁▁▁▁▁▁▁▁▁▁▁`
- mean diff `▄▂▃▄▂▃▄▂▃▄▂▃▆▄▁▂▁▁▂▁▁▂▁▁▂▁▁▂▂▂▃▂▂▃▂▇█▃▃▄▃▃▄▃▃▄▃▃▄▃▃▄▃▃▄▃▃▄▃▃`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0117 | < 0.04 | at frame 483 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## swap (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq02\seq_swap`, 201 frames at 30 fps, kind swap

- luma      `▄▂▃▄▁▄▁▃▄▂▄▁▄▂▃▄▁▄▁▄▂▂▄▁▄▂▃▄▂▄▁▄▂▃▅▃▆▄▇█▂▄▁▄▂▃▄▁▄▁▃▄▂▄▁▄▂▃▄▂`
- mean diff `▄▂▄▁▂▄▂▄▂▃▄▂▄▂▄▁▂▄▂▄▂▃▄▂▄▂▄▁▂▄▂▅▃▅▅▄▆▅█▃▃▄▂▄▂▃▄▂▄▂▄▁▂▄▂▄▂▃▄▂`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0029 | < 0.01 | at frame 131 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |
| T-SWAP consecutive diff | FAIL | 3.5346 | < 1.5 grey (every pair) |  |
