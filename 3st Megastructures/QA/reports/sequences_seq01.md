# Frame sequences: seq01

Sequences: 9. Checks: 16 PASS, 3 FAIL, 0 WARN, 0 SKIP.

**Result: FAIL**

## Summary

| Sequence | Kind | Frames | Max luma step | Mean diff | Max diff | Max px diff | Result | Failed checks |
|---|---|---|---|---|---|---|---|---|
| b12 | transition | 601 | 0.0001 | 0.525 | 0.562 | 81.1 | PASS |  |
| b23 | transition | 801 | 0.0041 | 2.981 | 5.670 | 69.1 | PASS |  |
| b34 | transition | 1661 | 0.0044 | 2.514 | 4.653 | 75.9 | PASS |  |
| b45 | transition | 601 | 0.0029 | 1.804 | 3.635 | 199.3 | PASS |  |
| b56 | transition | 801 | 0.0303 | 2.368 | 10.649 | 207.0 | FAIL | diff spikes |
| b67 | transition | 601 | 0.0019 | 1.718 | 2.654 | 226.8 | PASS |  |
| bankexit | transition | 301 | 0.0005 | 0.557 | 0.612 | 76.0 | PASS |  |
| deck | transition | 801 | 0.0303 | 2.397 | 10.649 | 207.0 | FAIL | diff spikes |
| swap | swap | 201 | 0.0034 | 3.411 | 4.454 | 56.2 | FAIL | T-SWAP consecutive diff |

## b12 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b12`, 601 frames at 30 fps, kind transition

- luma      `███████████████▇▇▇▇▇▇▆▆▆▆▅▅▅▄▄▄▃▃▃▃▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▂▃▃▃`
- mean diff `▂▃▃▃▃▂▄▄▃▃▃▃▁▁▂▃▃▁▃▄▄▃▁▄▄▃▃▄▅▃▄▄▄▃▃▅▅▄▄▅▅▄▅▅▅▅▄▅▇▇▆▆▇▇▆▅▇██▇`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0001 | < 0.04 | at frame 309 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b23 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b23`, 801 frames at 30 fps, kind transition

- luma      `█████████▇▇▇▇▇▆▆▅▄▃▂▂▂▁▁▁▂▂▂▂▂▂▃▃▃▃▃▃▃▃▄▄▃▃▃▃▃▃▃▂▂▂▃▃▃▃▃▃▃▃▃`
- mean diff `▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▃▄▄▄▄▄████▇▇▇▇▇██▇▇▇▆▆▆▆▆▆▆▇▇▆▆▆▆▅▅▅▅▅▆▆▆▅▅▅`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0041 | < 0.04 | at frame 431 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b34 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b34`, 1661 frames at 30 fps, kind transition

- luma      `▄▃▃▃▃▃▄▃▃▃▃▄▃▃▃▃▃▄▃▃▃▃▃▄▃▃▃▃▃▄▃▃▃▃▃▃▃▃▃▅███▇▇▆▅▄▄▃▂▁▁▂▂▁▁▁▁▁`
- mean diff `█▆▆▆▆▇█▆▆▆▆█▆▆▆▆▇█▆▆▆▆▇█▆▆▆▆▇█▇▇▇▇▇█▇▇▇▇▆▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0044 | < 0.04 | at frame 1122 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b45 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b45`, 601 frames at 30 fps, kind transition

- luma      `▇▇▇▇▇███████████▇▆▆▆▅▅▅▄▄▃▃▂▂▁▁▂▂▁▁▃▃▃▂▃▃▃▂▃▂▂▂▄▄▄▃▄▄▄▃▄▄▃▃▄`
- mean diff `▁▁▁▁▁▁▁▁▁▁▁▂▂▂▂▂▂▂▂▂▂▃▃▃▄▅▆▆▄▅▆▆▅▆▇▇▄▆██▄▆██▅▆██▄▆██▄▆██▄▆██`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0029 | < 0.04 | at frame 474 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## b56 (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b56`, 801 frames at 30 fps, kind transition

- luma      `▄▅▄▄▅▄▄▅▄▄▄▄▄▄▄▄▄▄▆███████████████████████▇▂▂▂▂▂▁▂▁▁▁▁▁▁▁▁▁▁`
- mean diff `▂▁▂▂▁▂▂▁▂▂▁▂▂▁▂▂▁▂▃▂▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▂█▂▂▃▂▂▃▂▂▃▂▂▃▂▂▃▂▂`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0303 | < 0.04 | at frame 570 |
| diff spikes | FAIL | 4 | 0 above 3x running median | frames [568, 569, 570, 571] |

## b67 (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_b67`, 601 frames at 30 fps, kind transition

- luma      `▇██▇▆▇▇▆▆▆▆▆▆▇▇▇▆▇▇▆▅▄▄▃▃▃▃▂▁▂▂▁▁▂▂▂▂▃▃▃▃▄▄▃▂▃▃▃▂▃▃▃▂▃▃▂▂▂▂▂`
- mean diff `██▂▆▇▆▂▆▇▅▂▅▇▆▂▆▇▆▂▆▇▇▂▅▆▅▁▄▆▅▁▅▆▅▁▄▆▅▂▅▆▅▂▄▅▅▁▄▅▄▁▄▅▅▂▄▅▅▂▄`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0019 | < 0.04 | at frame 15 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## bankexit (PASS)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_bankexit`, 301 frames at 30 fps, kind transition

- luma      `█▇▇▆▅▅▄▄▄▃▃▃▃▃▃▃▃▃▃▃▃▃▃▄▄▄▄▄▄▄▄▃▃▃▃▃▂▂▂▂▂▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁`
- mean diff `█▇▇▇▇▇▇▇▇▇█▇████▆▄▂▁▂▁▂▁▁▂▁▂▁▂▁▂▂▂▂▂▂▂▃▃▃▃▃▃▄▄▃▄▄▄▄▄▅▅▅▅▅▆▆▆`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0005 | < 0.04 | at frame 1 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |

## deck (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_deck`, 801 frames at 30 fps, kind transition

- luma      `▄▅▄▄▅▄▄▅▄▄▄▄▆▇██████████████████████▇▂▂▂▂▂▁▂▂▁▂▁▁▁▁▁▁▁▁▁▁▁▁▁`
- mean diff `▂▁▂▂▁▂▂▁▂▂▁▂▃▂▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▁▂▁▂█▂▂▃▂▂▃▂▂▃▂▂▃▂▂▃▂▂▃▂▂▃▂▂`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0303 | < 0.04 | at frame 490 |
| diff spikes | FAIL | 4 | 0 above 3x running median | frames [488, 489, 490, 491] |

## swap (FAIL)

`W:\YouTube\Megastructures\3rd Megastructures\3rd Megastructures\QA\captures\seq01\seq_swap`, 201 frames at 30 fps, kind swap

- luma      `▄▂▃▄▁▄▁▃▄▂▄▁▄▂▃▄▁▄▁▄▂▂▄▁▄▂▃▄▂▄▁▄▂▃▅▃▆▄▇█▂▄▁▄▂▃▄▁▄▁▃▄▂▄▁▄▂▃▄▂`
- mean diff `▄▁▄▁▂▄▂▄▂▄▄▂▄▂▄▁▂▄▂▄▂▄▄▂▄▂▄▁▂▄▂▅▃▅▅▄▆▅█▃▃▄▂▄▁▄▄▂▄▂▄▁▂▄▂▄▁▄▄▂`

| Check | Status | Value | Threshold | Note |
|---|---|---|---|---|
| luma step/frame | PASS | 0.0034 | < 0.01 | at frame 131 |
| diff spikes | PASS | 0 | 0 above 3x running median | floor 0.25 grey levels |
| T-SWAP consecutive diff | FAIL | 4.4540 | < 1.5 grey (every pair) |  |
