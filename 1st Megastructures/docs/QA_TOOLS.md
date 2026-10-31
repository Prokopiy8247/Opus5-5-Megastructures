# QA analysis tools (Python)

Offline analysis for Part 11 of `MASTER_PROMPT.md` (image metrics 11.3, contact sheets 11.4, frame sequences 11.6,
encode test 11.12), with the thresholds of Part 14.10 and the nadir rule of 14.7.2. Also covers project metrics for
Part 15.3. The tools never touch Unity. They read what the CaptureHarness and the sequence capture write.

## Setup

- Python 3.12 with `python -m pip install pillow numpy imageio-ffmpeg` (analysis tools only, Part 11.12).
- ffmpeg: the one on `PATH` (verified: ffmpeg 9.0.2 with libx264 and libvpx-vp9). Otherwise the binary from
  `imageio-ffmpeg` is used automatically.
- **Always run with `python -I`** (isolated mode). The scripts add their own folder to `sys.path` from `__file__`, so
  they work from any working directory. Shared code lives in `Tools/qa/abyss_qa.py`.
- **No bytecode in `Tools/`.** `-I` does not imply `-B`, so every entry script (the six tools, `selftest.py` and
  `Tools/metrics.py`) sets `sys.dont_write_bytecode = True` before it imports `abyss_qa`. Run the tools as scripts;
  do not import them from another Python process (the importer would cache the imported file). Do not use
  `python -m py_compile` on them either (it always writes a `.pyc`). The self-test fails if a `.pyc` appears.
- Self-test of every tool on synthetic data: `python -I Tools/qa/selftest.py` (about 70 s; `--skip-encode` for 25 s).
  It writes to `QA/tmp/selftest/`, which is gitignored and deleted on success unless `--keep` is given.

## Conventions

| Item | Definition |
|---|---|
| Luma | Rec. 709 of the **sRGB-encoded** values, 0-1 (`0.2126 R' + 0.7152 G' + 0.0722 B'`). Check: #070B10 gives 0.0412, the 0.041 of 14.7.2. |
| Clipped | Any channel >= 0.98 (8-bit value >= 250), in % of pixels. |
| Magenta | min(R, B) >= 128, G <= min(R, B) / 4, abs(R - B) <= 30 % of max(R, B). Catches the error shader after post-processing. |
| NaN-like pixel | Pure white (all >= 250), pure black (all <= 5) or magenta pixel whose luma differs by more than 0.4 from **every** one of its 8 neighbours. Real lights have AA edges or bloom. |
| Black / white frame | Black: p99 luma <= 2/255. White: p1 luma >= 0.95. |
| BandScore | Along each vertical line of a gradient region where one channel changes by more than 24 levels (max - min), runs of identical 8-bit values in that channel (runs of 0 or 255 are clamping). Limit 12 px at 1080p and 24 px at 2160p (scaled linearly with height). **Refined in fix round R1 (critique R1-38; the limits are unchanged):** a run over the limit counts as a band only when (a) no edge (a change above 6 levels across 4 px of the 5-line average) lies within 8 px of it, (b) the smoothed values just before and after it (4-12 px out) differ by at least 1 level with the run value between them (a step of a monotone gradient, not a plateau at a local extremum such as a flat panel, a rib face or a stripe), with a local slope under 1 level per 4 px, and (c) at least 5 adjacent lines carry such a run at a common row. `max_run` is the longest confirmed band (judged); the plain 14.10 measurement stays as `raw_max_run` (information) with the line-run p50 / p99 and `lines_over_limit`. On f02 the rule clears all 7 banding FAILs that critic B showed to be artefacts (raw runs 12-15 px, no confirmed band) and still fails a posterised gradient. |
| Regions | `[x0, y0, x1, y1]` or `{"rect": [...]}` as fractions of the frame, origin top-left, clamped to 0..1 (an overshoot such as 1.02 stays a fraction). Pixels only when explicit: `{"px": [x0, y0, x1, y1]}`. Circle: `{"circle": [cx, cy, r]}` (r is a fraction of the height) or `{"circle_px": [cx, cy, r]}`; the centre may lie outside the frame. Union of fractional rectangles: `{"rects": [[x0, y0, x1, y1], ...]}` (bounding box plus mask; the harness samples the horizon over both symmetric side ranges this way). A region that is malformed or smaller than 8 x 8 px (64 px) inside the frame is an error: its check fails with an `error` field, never passes as empty, and an `advisory` entry does not soften it. A `rects` union is judged on its bounding box (at least 8 x 8 px) and its pixel count (at least 64). Fractional values beyond -0.5..1.5 add a `region_warnings` note ("probably pixels"). |
| CLI regions | `[name=]x0,y0,x1,y1` (fractions, must lie in 0..1, otherwise exit 2), `[name=]px:x0,y0,x1,y1`, `[name=]circle:cx,cy,r`, `[name=]circle_px:cx,cy,r`, `[name=]rects:x0,y0,x1,y1/x0,y0,x1,y1` (fractions). |
| Shot identity | The file path relative to the capture folder, never the tag alone: each key shot exists at 1920x1080, 3840x2160, 1680x1050 and 2560x1080 under one tag (11.2). image_metrics, contact_sheet and compare list duplicates: one file referenced by several shots (a resolution overwrote another) and one (tag, size) in several files. |
| Exit codes | 0 = pass, 1 = a judged check fails, 2 = bad input, 3 = no video encoder (encode test only). |

### report.json (written by the CaptureHarness)

```json
{"run": "<id>", "nopost": false,
 "shots": [{"tag": "KS2a_bridge_ahead", "beat": 2, "file": "beat02_bridge_KS2a_bridge_ahead.png",
            "width": 1920, "height": 1080, "pose": {"pos": [0, -1.3, 120], "yaw": 0, "pitch": -10, "fov": 66},
            "clock": 12.5,
            "zone": "tunnel|outside (beat 7 only, optional)", "nopost": false, "kind": "keyshot|still|freelook",
            "regions": {"banding": {"fog": [0, 0, 1, 0.3]}, "aliasing": {"rails": [0.1, 0.5, 0.9, 0.6]},
                        "horizon_fog": {"rects": [[0.02, 0.345, 0.25, 0.357], [0.75, 0.345, 0.98, 0.357]]},
                        "horizon_sky": {"rects": [...]}, "horizon_backdrop": {"rects": [...]},
                        "horizon_row": {"rects": [...]}, "nadir": {"circle": [0.5, 0.5, 0.35]},
                        "doorway": [0.449, 0.467, 0.551, 0.766]},
            "thresholds": {"mean_min": 0.05, "mean_max": 0.13, "p99_max": 0.7, "clipped_max_pct": 0.3,
                           "band_limit_px": 12},
            "advisory": ["mean_luma"]}]}
```

Only `tag`, `file`, `width` and `height` are needed. Every resolution of a key shot needs its own `file` name (for
example a `_3840x2160` suffix); the harness naming is up to the harness, the tools only require it to be unique and
stable between runs. `beat` falls back to the file name (`beatNN_...`) or the
`KSn` tag. Beat 7 uses the tunnel row for `KS7a`, a pose with z > 0, or a tag containing "tunnel", and the outside
row otherwise. `regions.banding` replaces the beat defaults below. `thresholds` overrides the 14.10 row for that one
shot. That is the calibration clause, so log every override in `DEVELOPMENT.md`. `advisory` lists check-name
prefixes that only warn. Without `report.json` the folder is scanned and no size check is possible.

### 14.10 thresholds and default gradient regions (built into `abyss_qa.py`)

| Beat | Mean luma | p99 max | Clipped max | Default banding regions (fractions) |
|---|---|---|---|---|
| 1 | 0.10-0.20 | 0.90 | 0.3 % | doorway glow `[0.30, 0.10, 0.70, 0.90]` (KS1b; every other beat-1 shot: the same rectangle as `gallery_centre`) |
| 2 | 0.05-0.13 | 0.70 | 0.3 % | upper 30 % fog `[0, 0, 1, 0.30]`, lower 40 % void `[0, 0.60, 1, 1]` |
| 3 | 0.06-0.15 | 0.70 | 0.3 % | upper 25 % column fog `[0, 0, 1, 0.25]` |
| 4 | 0.08-0.18 | 0.80 | 0.5 % | far hall haze `[0.25, 0.20, 0.75, 0.60]` |
| 5 | 0.06-0.16 | 0.80 | 0.5 % | shaft fog `[0, 0, 1, 0.35]` and `[0, 0.65, 1, 1]` |
| 6 | 0.09-0.22 | 0.95 | 1.0 % | beam region `[0.20, 0, 0.80, 0.60]` |
| 7 tunnel | 0.04-0.20 | 0.98 | 2.0 % | mouth glow `[0.35, 0.30, 0.65, 0.70]` |
| 7 outside | 0.25-0.45 | 0.92 | 1.0 % | sky and haze, top 40 % `[0, 0, 1, 0.40]` |

The spec names the regions, not their exact place in the frame. The rectangles above are the tool's interpretation.
The harness should send exact per-shot regions wherever it knows the subject's screen bounds (KS7d sends none: no
gradient is in frame).

**Mean-luma band by the content in view (R1-38).** When the shot's extended progress `e` (report) lies inside a 14.6
transition window (E 52-72, 205-225, 285-310, 420-440, ride 110 to the beat-6 start (E 569.54-590.54: the arrival, the
gate and the bottom dock, so B5_end, a view of the fan and the gantry, is judged against beats 5 and 6; R2-36 f), and the
6-to-7 window d 640-668.54 after the ride), the frame shows a blend of two beats' looks and the mean-luma band is the union
of both rows (`thresholds.mean_rows`); p99 and clipped stay those of the shot's beat. **KS5b (lead decision D8 of fix
round R2, R2-13):** KS5b frames the fan ring and its backlit bore, a beat-6 subject, so all its variants are judged on the
beat-6 row alone (mean 0.09-0.22, p99 <= 0.95; `abyss_qa.CONTENT_LUMA_ROWS`, `thresholds.content_row`); its clipping limit
and banding regions stay those of beat 5.

### 14.5 image acceptance checks (critique R1-29; regions from the harness)

| Check | Shot | Rule |
|---|---|---|
| `near_black_pct` | every image | 14.2 floor: share of pixels whose largest channel is <= 6/255, counted only where the dark area is at least 2 x 2 px (1-px crevices are ignored): 0 %. |
| `side_strips` | KS2b | each 30 % side strip: mean luma <= 0.06, no pixel above 0.12, no horizontal edge above 0.03 after a 3x3 blur (14.5.2). |
| `far_glow_p90` | KS1 | 90th-percentile luma of the projected stair-head region >= 0.25 (the far glow that replaces the hidden doorway, R1-16). |
| `windows` | KS5a | >= 1 000 windows with cells of 8-20 px (14.5.5): compact dark or bright blobs (band-pass extrema with a clear 11 x 11 border) that form a grid (two others 7-22 px away); cell = median nearest-neighbour distance. Other content (bolts, grating) also yields such blobs, so the check runs on KS5a only. |
| `amber_dots` | KS7b | >= 40 groups of warm pixels (R - B 16 levels above the 9x9 local mean, luma +0.015, R > G > B) in the lower 66 % (14.5.7). |
| `readable_cells` | KS7b | >= 150 cells with a projected row height >= 3 px and a mean luma difference >= 0.03 between 5 interior points and 6 web-ring points on the web centreline (14.5.7). R2-04: every point is sampled by bilinear interpolation at its exact projected position; the nearest-pixel count stays as `readable_nearest` (information). |
| `black_runs` | every image | R2-16 / R2-36 (g): no connected (8-neighbour) run of at least 8 px whose largest channel is <= 2/255, without the 2x2 opening of `near_black_pct` (a 1-px black outline fails). Validation: f03 B6_start fails (the tunnel-mouth ring: runs of 14 and 8 px at (966, 537) and (963, 546)); f03 B5_end and KS6 pass. |
| `amber_saturation` | KS7b | R2-21: the amber-dot cores (brightest 10 % of the `amber_dots` pixels) have median HSV saturation >= 0.45 and median hue 28-38 degrees (AMBER #FFB45A: hue 32.7, S 0.65). Validation: f03 cores (226-233, 204-214, 167-191), S 0.18-0.27, cream-white at 4x zoom; lead_b01 cores (190-203, 148-157, 137-143), hue 10, S 0.24, peach-salmon at 4x zoom: both fail, as seen. |
| `pour_lifts` | KS3 | R2-02 / R2-36 (c), judged: along each projected iso-y curve of C1 (`lift_curves`, the 3 m joints), the luma profile across the curve (+/-16 px, detrended by a line through its outer 6-16 px) averaged over the curve's points; a joint reads when the largest deviation within +/-4 px reaches 4 levels; at least 3 joints above the y 0 collar must read. The old row-mean count stays as the advisory `pour_lifts_rows`. Validation (lead_b01, curves drawn over a x12 high-pass): the y -3 and y 3 curves lie on visible joints (9.4 and 5.7 levels); at the KS3 pose the y 6 and y 9 joints enter the frame only within about 30 px of C1's silhouette (24 and 4 points, 1.3 and 0.9 levels), so 1 lift reads (fail). |
| `wall_board_steps` | KS1 | R2-02: the left gallery wall at 1-2 m (`wall_board`): 8 x 8 px block means minus their 9-block moving average down the wall; the 95th percentile of the absolute residual must reach 3 levels (board rows). Validation (fix round R2): f03 0.92 and lead_b01 1.79 levels, where the wall shows thin panel joints but no board-to-board tone (critique R2-02); a +/-5 % board tone injected into the lead_b01 wall gives 3.1-4.4 levels (rows 24-64 px, pass), +/-3 % 2.3-3.0, +/-2 % 2.0-2.6 (fail). |
| `wall_macro_residual` | KS2a | R2-02: the right shaft wall between the rib lines (`wall_macro`, two rectangles): 8 x 8 px block means minus a least-squares plane; the residual std of each rectangle must reach 2 levels (macro noise). Validation: KS2a f03 0.58-0.74, lead_b01 0.64-0.74 (no visible macro variation, R2-02); the KS7d Face (x 0.02-0.38, y 0.62-0.95), which the critics read as panelled concrete, gives 3.5-3.6. |
| `amber_coverage_pct` | every image | advisory (R2-24): warm-emissive pixels by emitter area (hue 12-55, S >= 0.12, V >= 0.6, lamp cores included) against the 14.2 cap of 0.3 % of the frame, until the lamp-head conflict of R2-24 is arbitrated. |
| `stipple_tiles` | every image | advisory: 16 x 16 tiles with at least 5 isolated 1-px luma maxima and 5 minima beyond 0.02 (screen-door dither between tiers, R1-13); aliased thin diagonals also trigger it. |
| `pour_lifts_rows` | KS3 | advisory: horizontal lines of at least 4 levels in the detrended row profile of the column face (the R1-08 detector; named `pour_lifts` for shots without `lift_curves`). |
| `spoke_contrast` | KS6 | advisory: detrended angular luma profile of the annulus 1.05-1.35 x the projected housing radius (upper half); top 10 % minus bottom 10 % (target >= 0.04, R1-10). |

**Doorway (14.5.1 acceptance, KS1b only).** "Doorway luma 0.65-0.90 with under 0.3 % clipped", calibrated to **0.40-0.60**
(DEVELOPMENT.md gallery row "KS1b doorway luma" and lead decision D6 of fix round R2: a 0.8 doorway that becomes the shaft
view within d 52-62 is a 2.6 EV drop in 6.7 s, 0.39 EV/s, above the 0.35 EV/s of 14.6): the mean luma of the
clear doorway aperture (`regions.doorway`) must lie in 0.40-0.60, and under 0.3 % of the aperture's pixels may be
clipped (strictly below; the frame-level 0.3 % of the beat row is judged as well). Checks `doorway_luma` and
`doorway_clipped_pct`. They apply to KS1b shots only (base tag, tag or file name token `KS1b`): at KS1 the sloped stair
ceiling hides the doorway, so a `doorway` region on any other shot is reported with `"applies": false` and never judged.
Without a `doorway` region a KS1b shot uses the aperture computed from the KS1b pose (eye (0, -1.3, 55), FOV 62,
opening 1.2 x 2.0 between z 59.2 and 60, inset 5 % per side) at the image aspect.

**Horizon samples (KS7b, KS7c).** The harness writes `horizon_row`, `horizon_sky`, `horizon_fog` and
`horizon_backdrop` as `rects` unions over the same two side ranges, columns 0.02-0.25 and 0.75-0.98: a centred sample
would contain the KS7c goal door and its lamp (columns 0.477-0.523) and would sit under the centre of the vignette
(about +2 levels against the sides). Rows: sky just above the horizon, fog just below it (above the far edge of the
tier-2 backdrop), backdrop just below that edge (see `docs/QA_RUNTIME.md`).

## 1. image_metrics.py: per-image checks (11.3, 14.10, 14.7.2)

```
python -I Tools/qa/image_metrics.py QA/captures/<run-id> [--out QA/metrics/<run-id>] [--advisory "freelook|_look"]
       [--band-axis v|h|both] [--band-channels rgb|luma] [--no-fail-exit] [--quiet]
```
Per image it checks the size against the report, black / white frame, magenta ratio (must be 0), NaN-like pixels
(must be 0), clipped %, luma mean / p1 / p50 / p99, and BandScore per gradient region. It also checks horizon-colour
agreement (fog / sky / backdrop within 3 levels) when `horizon_*` regions are given. With `-nopost` captures of beat 7
(shot or report `nopost`, or "nopost" in the tag), the `horizon_row` region must equal OUT_HORIZ #8496A5 within 2
levels. With a `nadir` region it applies 14.7.2: mean luma 0.041 +/- 0.008, no pixel above 0.06, and no horizontal
edge above 0.03 after a 3x3 blur. KS1b shots get the doorway check (aperture mean luma 0.40-0.60, the calibrated band,
under 0.3 % clipped; never for KS1 or any other shot). Each `aliasing` region gets a static Laplacian proxy (information only; the judged
test is the temporal one in `sequence_metrics.py`). Mean, p99 and clipped are judged against the beat row of 14.10.
`--advisory REGEX` turns those three into warnings for matching tags, for example free-look stills.
Output: `metrics.json` (every number, the worst band positions, speck coordinates, region errors and warnings,
`duplicates`) and `summary.md`. Failures are labelled `tag (file)`.

Single image, for quick checks and the horizon helper:
```
python -I Tools/qa/image_metrics.py --single shot.png --beat 7 --zone outside \
       --horizon-sky rects:0.02,0.307,0.25,0.327/0.75,0.307,0.98,0.327 \
       --horizon-fog rects:0.02,0.345,0.25,0.357/0.75,0.345,0.98,0.357 \
       --horizon-backdrop rects:0.02,0.3694,0.0775,0.3747/0.1925,0.3649,0.25,0.369/0.75,0.3649,0.8075,0.369/0.9225,0.3694,0.98,0.3747 \
       --horizon-row rects:0.02,0.337,0.25,0.347/0.75,0.337,0.98,0.347 --nopost [--nadir 0.3,0.3,0.7,0.7] [--band fog=0,0,1,0.4]
python -I Tools/qa/image_metrics.py --single ks1b.png --beat 1 --tag KS1b_doorway [--doorway 0.449,0.467,0.551,0.766]
```
(The rows above are KS7b values at 1920x1080; the backdrop shows only the outermost and innermost of the 4 strips per
side. Take the exact regions from the shot's `report.json` entry.)

## 2. contact_sheet.py: contact sheet for the lead (11.4)

```
python -I Tools/qa/contact_sheet.py QA/captures/<run-id> [--metrics QA/metrics/<run-id>/metrics.json]
       [--out QA/contact/<run-id>.jpg] [--cols 4] [--tile-width 480] [--per-page 24] [--format jpg|png]
```
Tiles keep their aspect ratio and are labelled with tag, beat, size, mean luma and p99. With `--metrics`, each tile
gets a green or red bar and the failing check names. Tiles are matched to `metrics.json` by file (relative path,
then a unique basename), never by tag. A file referenced twice in `report.json` gets one tile, and duplicates are
printed and counted in the header. Beyond `--per-page`, the sheet splits into `<out>_p01`, `_p02`, and so on. Open the sheet with the Read tool. Look at the key shots themselves at full size as well.

## 3. sequence_metrics.py: frame sequences (11.6, 14.10)

```
python -I Tools/qa/sequence_metrics.py QA/sequences/<name> --fps 30 [--plot]
       [--luma-region R] [--diff-region R]
       [--std thin=R --std facade=R ...] [--std-limit 6] [--std-stat mean|p95|max]
       [--static deck=R ...] [--static-limit 1] [--static-stat max|p99|mean]
       [--sigma 4] [--spike-window 30] [--diff-floor 0.002]
       [--luma-step-limit 0.02] [--ev-rate-limit 0.35] [--ev-window 0.5]
       [--judge spikes,luma_step,ev_rate,std,static] [--start N --end N --step N]
```
Per frame: mean luma, mean linear luminance, EV = log2(mean linear), frame-to-frame mean absolute difference, luma
step, and EV rate over a 0.5 s window.

| Check | Rule | Spec |
|---|---|---|
| spikes | diff[i] > mean + 4 sigma of the diffs within +/- 30 frames (excluding i) and diff[i] - mean > 0.002. The floor stops a perfectly static sequence from flagging noise. | 10.6, 14.6, 14.10 (d) |
| luma_step | abs(mean luma[i] - mean luma[i-1]) <= 0.02 per 30 fps frame (scaled by 30 / fps) | 14.10 tunnel mouth, 14.5.7 |
| ev_rate | abs(EV[i] - EV[i-k]) / (k / fps) <= 0.35 EV/s, k = 0.5 s. EV comes from the display-referred frame (a proxy for the exposure blend). | 14.6, 14.5.7 |
| std | per-pixel temporal std of luma (levels) inside each mask, statistic <= limit | 14.10 aliasing (6/255 over 30 frames with 0.02 m steps), facade (6/255, clock frozen), 14.7.2 nadir (`--std-limit 1`) |
| static | frame-to-frame per-pixel change (levels, max over channels) inside each mask <= limit. The change against the first frame is also reported. | 14.10 "deck difference at most 1 level" |

Use `--judge` to judge only what a sequence is meant to prove. For example, a transition walk uses
`--judge spikes,luma_step,ev_rate`, and a 100 s static sequence uses `--judge spikes,std,static`.
`--luma-region csv` takes the mean-luma region of every frame from the sequence's `frames.csv` (`region_x0..y1`; frames
without one use the whole frame): the t12_ev sequences (R1-07) judge the luma seen through the gallery doorway this way.
R2-01: frames with a region and frames without one are two segments (the harness stops writing the aperture at d 59.3,
where it would grow to the full frame); `luma_step` and `ev_rate` are never computed across the change, and the
`settings.segment_breaks` list names the first frame of each new segment.
Output: `sequence.csv`, `sequence.json`, `summary.md`, and `plot.png` with `--plot`.

## 4. encode_test.py: banding after YouTube-style compression (11.12, 14.10)

```
python -I Tools/qa/encode_test.py QA/sequences/<KS2a_seq> --fps 60 --beat 2 [--band fog=0,0,1,0.3 ...]
       [--x264-crf 20,23,30] [--preset medium] [--vp9 auto|on|off] [--vp9-crf 31] [--gate x264:20]
       [--limit-px 24] [--stride 1] [--max-frames N] [--png-every 60] [--keep-video]
```
The frames are piped once into every encoder: libx264 at each CRF (preset medium, yuv420p) and libvpx-vp9 at
constant quality when available. RGB to YUV uses BT.709 limited range with accurate swscale rounding, and the streams
are tagged BT.709. Each encode is then decoded back to RGB. Decoded PNGs are saved every `--png-every` frames, plus
the worst frame. The BandScore is computed on the source (still rule) and on every decoded frame, inside the gradient
regions. **Verdict (recalibrated, DEVELOPMENT.md "Encode test and dither decision"; `abyss_qa.encode_band_runs`):** in a
qualifying line (change > 24 levels) of a decoded frame, a run of identical values (R, G and B separately) longer than
24 px at 1080p counts as a band only if (a) it ends in a step of at least 2 levels to the neighbouring plateau (median of
the 4 decoded pixels beyond the boundary, so a one-pixel ringing overshoot is no step) that the source does not have (the
5-line source average changes by under 2 levels across the boundary, and no source edge above 6 levels lies within 8 px:
ringing around an object edge is not a band), or (b) it is longer than max(24 px, 1.5 / local slope), the slope (levels
per pixel) being a least-squares fit of the dithered source frame (5-line average) over the run +/- 48 px, the window cut
at source edges: longer than the plateau the true gradient itself implies. No band = pass. Reported per encode:
`band_max_run` (judged), `band_runs` (by step / by length), `frames_with_bands`, and the raw longest run `raw_max_run`
(information, the old rule). `--plain-rule` judges every run over the limit (comparison only). The `--gate` encodes set
the exit code; the default is the 14.10 rule, x264 CRF 20. CRF 23 / 30 and VP9 (11.12) are reported with the same rule.
`crops_worst.png` shows the source and each encode at the worst location at 100 %, at 200 %, and at 200 % with the
contrast stretched x8 around the local mean, which makes step edges obvious.
For 3840x2160 sequences, use `--stride 2` or `--stride 4` to keep the run time reasonable.
If no encoder exists, it exits 3 and `encode.json` records the 11.12 fallback (halve the BandScore thresholds and log
it in `DEVELOPMENT.md`).

## 5. compare.py: regression diff between runs (11.3)

```
python -I Tools/qa/compare.py QA/captures/<old> QA/captures/<new> [--out QA/compare/<old>_vs_<new>]
       [--threshold 2] [--gain 8] [--fail-mean 0] [--resize] [--no-heatmaps]
```
Shots are matched by file (path relative to the run folder), so every resolution of a key shot is compared with
its own counterpart. For each file it reports the tag, the mean absolute difference (levels), the p99 and max
per-pixel difference, the % of pixels changed by more than the threshold, and the change in mean luma. It also writes
`heat_<file stem>.png` (the difference x gain over the dimmed new frame, with A | B thumbnails underneath). Files
missing from one run, size mismatches and `report.json` duplicates (`duplicates_a` / `duplicates_b`) are listed.
`compare.json` holds one entry per file under `shots`. `--fail-mean M` returns exit code 1 when any tag differs by more than
M levels on average.

## 6. metrics.py: time, tokens and cost (Part 15.3)

```
python -I Tools/metrics.py [--update-report OPUS_5.5_FINAL_REPORT.md] [--json QA/metrics/project_metrics.json]
       [--since anchor|<ISO>] [--gap-minutes 10]
```
- **Transcripts.** Folders under `C:\Users\<user>\.claude\projects` whose name contains `1st-Megastructures`. All
  `*.jsonl` files are read recursively: main sessions, `<session>/subagents/**` and workflow agents and journals. A
  file counts only when every `cwd` in it is the project root. Foreign-cwd files are excluded and listed. Sibling
  projects and the parent `W--YouTube-Megastructures` folder never match and are listed as excluded. Access is
  read-only.
- **Tokens.** Uses `message.usage` of assistant records, deduplicated by `message.id`, then `requestId`, then uuid.
  A streamed message appears once per content block with identical usage; on the real transcripts, 1 297 records
  reduced to 648 messages. Categories: uncached input, cache read, 5-minute and 1-hour cache writes (from
  `usage.cache_creation`), and output (thinking is shown separately and is included in output). `<synthetic>`
  records are skipped.
- **Time.** Wall-clock runs from the first to the last timestamp over all records. Every gap longer than 10 minutes is
  listed. Active time = wall-clock minus the gaps. The `Run started at` line of `STATE.md` gives an extra row.
- **Cost.** Rates come from `Tools/pricing.json`, fetched from the official page
  <https://platform.claude.com/docs/en/about-claude/pricing> on 2026-10-29. Claude Opus 5.5: $4 input, $5 5-minute
  cache write, $8 1-hour cache write, $0.20 cache read, $20 output, all per MTok. There is no long-context surcharge.
  Fast mode ($8 / $40), the `inference_geo: us` x1.1 multiplier and web searches ($10 / 1000) are applied only when
  the usage says so.
- **Output.** A `Metric | Value | How obtained` table, the gap list, the cost formula and notes, including what cannot
  be counted. `--update-report` replaces the `## Project metrics` section, up to the next `## ` heading, or appends it.

## Measured findings to keep in mind

- **The dither kind decides the BandScore.** Measured on a beat-2-like dark fog gradient at 1080p (64 to 2 levels):
  - IGN dither, 1.0 LSB: max run 13 px in the gentle lower 40 % (fails 12 px).
  - IGN dither, 1.5 LSB: 3 px. IGN 2.0 LSB: 2 px. Triangular IGN (two samples): 7 px.
  - White noise fails at any amplitude: 1 LSB rectangular, TPDF +/-1 and 1.5 LSB all give 17-24 px (raw rule; the
    R1-38 confirmed-band rule keeps only runs shared by 5 adjacent lines, which white noise yields rarely).

  Use an IGN or blue-noise pattern at about 1.5 LSB, never white noise.
- **A static 1.5 LSB IGN dither did not survive x264** on a static synthetic 720p60 gradient. Max run 24 px at
  CRF 20, against a 16 px limit at 720p. The encoder flattened the dither at about 90 kbps (see the self-test crops).
  The real sequences will decide (camera motion raises the bitrate). If they fail too, apply the remedies of 11.12
  step 4 in order: temporal dither in capture / cinematic mode, then a higher amplitude.
- **ffmpeg's default RGB to YUV path is biased.** Without `accurate_rnd+full_chroma_int+bitexact`, a lossless round
  trip darkens dark frames by 1.0-1.4 levels. With the flags the bias is +0.2 / +0.1 / 0.0. `encode_test.py` uses the
  accurate path.
