"""Frame-sequence QA metrics (Part 11.6, 14.10 frame sequences, 10.6 popping, 10.12 exposure jumps, 14.7.2 noise).

Usage:
    python -I Tools/qa/sequence_metrics.py QA/sequences/<name> [--fps 30] [--out QA/seqmetrics/<name>]
           [--luma-region x0,y0,x1,y1] [--diff-region x0,y0,x1,y1]
           [--std thin=x0,y0,x1,y1 ...] [--std-limit 6] [--std-stat mean|p95|max]
           [--static deck=x0,y0,x1,y1 ...] [--static-limit 1] [--static-stat max|p99|mean]
           [--sigma 4] [--spike-window 30] [--diff-floor 0.002]
           [--luma-step-limit 0.02] [--ev-rate-limit 0.35] [--ev-window 0.5]
           [--judge spikes,luma_step,ev_rate,std,static] [--plot] [--start N] [--end N] [--step N]

Regions: x0,y0,x1,y1 as fractions 0..1 of the frame, px:x0,y0,x1,y1 in pixels, circle:cx,cy,r or circle_px:cx,cy,r.
A region smaller than 8 x 8 px inside the frame fails its check with an error (luma / diff region: exit code 2).

Frames: every PNG of the folder in natural order (frame_2 before frame_10).
Per frame: mean luma (Rec. 709 of sRGB, in --luma-region), mean linear luminance and EV = log2(mean linear),
frame-to-frame mean absolute difference (RGB 0-1, in --diff-region), luma step, EV rate.

Checks (all judged unless --judge restricts them):
* spikes    - 4-sigma rule: diff[i] > mean + sigma * std of the diffs within +/- spike-window frames (excluding i) AND
              diff[i] - mean > diff-floor (keeps a perfectly static sequence, whose std is ~0, from flagging noise).
* luma_step - |mean luma[i] - mean luma[i-1]| <= 0.02 per 30 fps frame (the limit scales with 30 / fps).
* ev_rate   - |EV[i] - EV[i-k]| / (k / fps) <= 0.35 EV/s with k = ev-window * fps frames. EV is computed from the
              display-referred frame (sRGB decoded to linear), a proxy for the exposure blend, documented as such.
* std       - temporal standard deviation per pixel of luma (8-bit levels) inside each --std rectangle (thin-line,
              facade, beam or nadir masks); the --std-stat over the mask must be <= --std-limit levels (6/255 per
              14.10; use --std-limit 1 for the abyss nadir of 14.7.2).
* static    - frame-to-frame per-pixel absolute change (8-bit levels, max over channels) inside each --static
              rectangle must stay <= --static-limit (1 level: "deck difference at most 1 level"); the change against
              the first frame is reported too.

Outputs: <out>/sequence.csv, <out>/sequence.json, <out>/summary.md and with --plot <out>/plot.png.
Exit code 0 = all judged checks pass, 1 = a judged check fails, 2 = bad input.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import csv  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from typing import Any, Dict, List, Optional, Tuple  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

ALL_CHECKS = ("spikes", "luma_step", "ev_rate", "std", "static")


class RegionStd:
    """Streaming per-pixel temporal mean / std (Welford, float64) of luma in 8-bit levels inside one box."""

    def __init__(self, name: str, shape: Any):
        self.name, self.shape = name, shape
        self.box: Optional[Tuple[int, int, int, int]] = None
        self.mask: Optional[np.ndarray] = None
        self.n = 0
        self.mean: Optional[np.ndarray] = None
        self.m2: Optional[np.ndarray] = None
        self.error: Optional[str] = None

    def add(self, rgb8: np.ndarray) -> None:
        if self.box is None:
            h, w = rgb8.shape[:2]
            self.box, self.mask, self.error = qa.resolve_region(self.shape, w, h)
        if self.error:
            return
        x0, y0, x1, y1 = self.box
        v = qa.luma(rgb8[y0:y1, x0:x1]).astype(np.float64) * 255.0
        self.n += 1
        if self.mean is None:
            self.mean = v.copy()
            self.m2 = np.zeros_like(v)
            return
        d = v - self.mean
        self.mean += d / self.n
        self.m2 += d * (v - self.mean)

    def result(self, stat: str, limit: float) -> Dict[str, Any]:
        if self.error:
            return {"name": self.name, "region": self.shape, "frames": self.n, "value": None, "pass": False,
                    "error": self.error}
        if self.mean is None or self.n < 2:
            return {"name": self.name, "frames": self.n, "pass": False, "error": "fewer than 2 frames"}
        sd = np.sqrt(self.m2 / (self.n - 1))
        vals = sd[self.mask] if self.mask is not None else sd.ravel()
        if vals.size == 0:
            return {"name": self.name, "frames": self.n, "pass": False, "error": "empty region"}
        stats = {"mean": float(vals.mean()), "p95": float(np.percentile(vals, 95)), "max": float(vals.max())}
        flat = np.argmax(np.where(self.mask, sd, -1) if self.mask is not None else sd)
        wy, wx = np.unravel_index(flat, sd.shape)
        judged = stats[stat]
        return {"name": self.name, "region": self.shape, "box_px": list(self.box), "frames": self.n,
                "std_levels": stats, "stat": stat, "value": judged, "limit_levels": limit,
                "worst_px": [int(self.box[0] + wx), int(self.box[1] + wy)], "pass": bool(judged <= limit)}


class RegionStatic:
    """Frame-to-frame and versus-first-frame per-pixel change (8-bit levels) inside one box."""

    def __init__(self, name: str, shape: Any):
        self.name, self.shape = name, shape
        self.box: Optional[Tuple[int, int, int, int]] = None
        self.mask: Optional[np.ndarray] = None
        self.first: Optional[np.ndarray] = None
        self.prev: Optional[np.ndarray] = None
        self.f2f_max: List[float] = []
        self.f2f_p99: List[float] = []
        self.f2f_mean: List[float] = []
        self.first_max = 0.0
        self.first_mean_max = 0.0
        self.error: Optional[str] = None

    def _vals(self, a: np.ndarray) -> np.ndarray:
        return a[self.mask] if self.mask is not None else a.reshape(-1, a.shape[-1])

    def add(self, rgb8: np.ndarray) -> None:
        if self.box is None:
            h, w = rgb8.shape[:2]
            self.box, self.mask, self.error = qa.resolve_region(self.shape, w, h)
        if self.error:
            return
        x0, y0, x1, y1 = self.box
        cur = rgb8[y0:y1, x0:x1].astype(np.int16)
        if self.first is None:
            self.first = cur
        else:
            d0 = self._vals(np.abs(cur - self.first)).max(axis=-1)
            if d0.size:
                self.first_max = max(self.first_max, float(d0.max()))
                self.first_mean_max = max(self.first_mean_max, float(d0.mean()))
        if self.prev is not None:
            d = self._vals(np.abs(cur - self.prev)).max(axis=-1)
            if d.size:
                self.f2f_max.append(float(d.max()))
                self.f2f_p99.append(float(np.percentile(d, 99)))
                self.f2f_mean.append(float(d.mean()))
        self.prev = cur

    def result(self, stat: str, limit: float) -> Dict[str, Any]:
        if self.error:
            return {"name": self.name, "region": self.shape, "value": None, "pass": False, "error": self.error}
        if not self.f2f_max:
            return {"name": self.name, "pass": False, "error": "fewer than 2 frames"}
        series = {"max": self.f2f_max, "p99": self.f2f_p99, "mean": self.f2f_mean}[stat]
        worst = int(np.argmax(series))
        value = float(series[worst])
        return {"name": self.name, "region": self.shape, "box_px": list(self.box), "stat": stat, "value": value,
                "worst_frame": worst + 1, "limit_levels": limit,
                "frame_to_frame_levels": {"max": max(self.f2f_max), "p99_max": max(self.f2f_p99),
                                          "mean_max": max(self.f2f_mean)},
                "vs_first_frame_levels": {"max": self.first_max, "mean_max": self.first_mean_max},
                "pass": bool(value <= limit)}


def named_rect(text: str, index: int, prefix: str) -> Tuple[str, Any]:
    """'name=x0,y0,x1,y1' (or name=px:...) -> (name, shape); unnamed regions become prefix0, prefix1, ..."""
    nm, rect = qa.parse_rect_arg(text)
    return (nm or "%s%d" % (prefix, index)), rect


def detect_spikes(diff: np.ndarray, sigma: float, window: int, floor: float) -> List[Dict[str, Any]]:
    """4-sigma rule on the frame-difference series (index i = change from frame i-1 to frame i; diff[0] unused)."""
    spikes = []
    n = len(diff)
    for i in range(1, n):
        lo, hi = max(1, i - window), min(n, i + window + 1)
        neigh = np.concatenate([diff[lo:i], diff[i + 1:hi]])
        if neigh.size < 3:
            continue
        mu, sd = float(neigh.mean()), float(neigh.std())
        thr = mu + sigma * sd
        if diff[i] > thr and (diff[i] - mu) > floor:
            spikes.append({"frame": i, "diff": float(diff[i]), "local_mean": mu, "local_std": sd,
                           "z": float((diff[i] - mu) / sd) if sd > 0 else None})
    return spikes


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder")
    ap.add_argument("--fps", type=float, default=30.0)
    ap.add_argument("--out")
    ap.add_argument("--name")
    ap.add_argument("--luma-region", help="x0,y0,x1,y1 (fractions) or px:... for the mean-luma series (default full)")
    ap.add_argument("--diff-region", help="x0,y0,x1,y1 (fractions) or px:... for the difference series (default full)")
    ap.add_argument("--std", action="append", default=[],
                    help="name=x0,y0,x1,y1 (fractions) or name=px:x0,y0,x1,y1 temporal std mask (repeatable)")
    ap.add_argument("--std-limit", type=float, default=6.0, help="levels (14.10: 6)")
    ap.add_argument("--std-stat", choices=["mean", "p95", "max"], default="mean")
    ap.add_argument("--static", action="append", default=[],
                    help="name=x0,y0,x1,y1 (fractions) or name=px:x0,y0,x1,y1 static region (repeatable)")
    ap.add_argument("--static-limit", type=float, default=1.0, help="levels (14.10: deck difference 1)")
    ap.add_argument("--static-stat", choices=["max", "p99", "mean"], default="max")
    ap.add_argument("--sigma", type=float, default=4.0)
    ap.add_argument("--spike-window", type=int, default=30, help="frames on each side for the local statistics")
    ap.add_argument("--diff-floor", type=float, default=0.002, help="minimum excess over the local mean (0-1)")
    ap.add_argument("--luma-step-limit", type=float, default=0.02, help="per 30 fps frame")
    ap.add_argument("--ev-rate-limit", type=float, default=0.35, help="EV per second")
    ap.add_argument("--ev-window", type=float, default=0.5, help="seconds")
    ap.add_argument("--judge", default=",".join(ALL_CHECKS), help="comma list of judged checks")
    ap.add_argument("--plot", action="store_true")
    ap.add_argument("--start", type=int, default=0)
    ap.add_argument("--end", type=int, default=None)
    ap.add_argument("--step", type=int, default=1)
    ap.add_argument("--no-fail-exit", action="store_true")
    args = ap.parse_args(argv)

    folder = os.path.abspath(args.folder)
    if not os.path.isdir(folder):
        qa.eprint("not a folder: %s" % folder)
        return 2
    files = qa.list_pngs(folder)[args.start:args.end:max(1, args.step)]
    if len(files) < 2:
        qa.eprint("need at least 2 PNG frames in %s" % folder)
        return 2
    fps = args.fps / max(1, args.step)
    judge = set(s.strip() for s in args.judge.split(",") if s.strip())
    name = args.name or os.path.basename(folder)
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", name)
    out_dir = os.path.abspath(args.out) if args.out else os.path.join(qa.QA_DIR, "seqmetrics", safe)

    # --luma-region csv: a per-frame region from the harness's frames.csv (region_x0..y1 fractions; the gallery doorway
    # aperture of the t12_ev sequences, R1-07). Frames without a region use the whole frame.
    csv_regions: Optional[Dict[str, List[float]]] = None
    if args.luma_region and args.luma_region.strip().lower() == "csv":
        csv_regions = {}
        fc = os.path.join(folder, "frames.csv")
        if not os.path.isfile(fc):
            qa.eprint("--luma-region csv needs %s" % fc)
            return 2
        with open(fc, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                try:
                    r = [float(row["region_x0"]), float(row["region_y0"]), float(row["region_x1"]), float(row["region_y1"])]
                except (KeyError, TypeError, ValueError):
                    continue
                csv_regions[os.path.basename(row.get("file") or "")] = r
        args.luma_region = None
    try:
        luma_shape = qa.parse_rect_arg(args.luma_region)[1] if args.luma_region else [0.0, 0.0, 1.0, 1.0]
        diff_shape = qa.parse_rect_arg(args.diff_region)[1] if args.diff_region else [0.0, 0.0, 1.0, 1.0]
        stds = [RegionStd(*named_rect(t, i, "std")) for i, t in enumerate(args.std)]
        statics = [RegionStatic(*named_rect(t, i, "static")) for i, t in enumerate(args.static)]
    except ValueError as e:
        qa.eprint("bad region argument: %s" % e)
        return 2

    n = len(files)
    mean_luma = np.zeros(n)
    mean_lin = np.zeros(n)
    diff = np.zeros(n)
    size0: Optional[Tuple[int, int]] = None
    size_errors = []
    prev: Optional[np.ndarray] = None
    lbox = dbox = None
    lmask = dmask = None
    region_frames = 0
    # R2-01: with --luma-region csv the frames with a region (the doorway aperture) and the frames without one (the whole
    # frame, d >= 59.3) are two segments: luma_step and ev_rate are never computed across the change of region
    seg = np.zeros(n, dtype=np.int8)
    for i, p in enumerate(files):
        rgb8 = qa.load_rgb8(p)
        h, w = rgb8.shape[:2]
        if size0 is None:
            size0 = (w, h)
            lbox, lmask, lerr = qa.resolve_region(luma_shape, w, h)
            dbox, dmask, derr = qa.resolve_region(diff_shape, w, h)
            if lerr or derr:
                qa.eprint("bad --luma-region / --diff-region for %dx%d frames: %s" % (w, h, lerr or derr))
                return 2
        elif (w, h) != size0:
            # a frame of the wrong size is a capture defect: report it, hold the previous values in the series
            size_errors.append({"frame": i, "file": os.path.basename(p), "size": [w, h]})
            mean_luma[i], mean_lin[i] = mean_luma[i - 1], mean_lin[i - 1]
            continue
        x0, y0, x1, y1 = lbox
        fmask = lmask
        if csv_regions is not None:
            reg = csv_regions.get(os.path.basename(p))
            if reg is not None:
                rb, rm, rerr = qa.resolve_region(reg, w, h)
                if not rerr:
                    (x0, y0, x1, y1), fmask = rb, rm
                    region_frames += 1
                    seg[i] = 1
        sub = rgb8[y0:y1, x0:x1]
        Y = qa.luma(sub)
        lin = qa.srgb_to_linear(qa.to_float(sub)) @ qa.REC709
        if fmask is not None:
            Y, lin = Y[fmask], lin[fmask]
        mean_luma[i] = float(Y.mean())
        mean_lin[i] = float(lin.mean())
        x0, y0, x1, y1 = dbox
        cur = rgb8[y0:y1, x0:x1]
        if prev is not None:
            dd = np.abs(cur.astype(np.int16) - prev.astype(np.int16)).astype(np.float32) * (1.0 / 255.0)
            diff[i] = float(dd[dmask].mean() if dmask is not None else dd.mean())
        prev = cur
        for r in stds:
            r.add(rgb8)
        for r in statics:
            r.add(rgb8)
        if (i + 1) % 100 == 0:
            print("  %d / %d frames" % (i + 1, n))

    ev = np.log2(np.maximum(mean_lin, 1e-6))
    step = np.zeros(n)
    step[1:] = np.abs(np.diff(mean_luma))
    k = max(1, int(round(args.ev_window * fps)))
    ev_rate = np.zeros(n)
    if n > k:
        ev_rate[k:] = np.abs(ev[k:] - ev[:-k]) / (k / fps)
    ev_inst = np.zeros(n)
    ev_inst[1:] = np.abs(np.diff(ev)) * fps
    seg_breaks: List[int] = []
    if csv_regions is not None:
        seg_breaks = [int(i) for i in range(1, n) if seg[i] != seg[i - 1]]
        same1 = np.ones(n, dtype=bool)
        same1[1:] = seg[1:] == seg[:-1]
        step[~same1] = 0.0
        ev_inst[~same1] = 0.0
        if n > k:
            # a window that spans a segment change is not judged (the segments are runs, so equal ends with no break
            # between them means the same segment)
            for i in range(k, n):
                if any(i - k < b <= i for b in seg_breaks):
                    ev_rate[i] = 0.0
    step_limit = args.luma_step_limit * (30.0 / fps)

    spikes = detect_spikes(diff, args.sigma, args.spike_window, args.diff_floor)
    spike_set = set(s["frame"] for s in spikes)
    for s in spikes:
        s["file"] = os.path.basename(files[s["frame"]])
    std_res = [r.result(args.std_stat, args.std_limit) for r in stds]
    static_res = [r.result(args.static_stat, args.static_limit) for r in statics]

    checks: Dict[str, Dict[str, Any]] = {
        "spikes": {"value": len(spikes), "limit": 0, "pass": not spikes},
        "luma_step": {"value": float(step.max()), "limit": step_limit, "pass": bool(step.max() <= step_limit),
                      "worst_frame": int(step.argmax())},
        "ev_rate": {"value": float(ev_rate.max()), "limit": args.ev_rate_limit,
                    "pass": bool(ev_rate.max() <= args.ev_rate_limit), "worst_frame": int(ev_rate.argmax()),
                    "window_frames": k, "instant_max": float(ev_inst.max())},
        "std": {"value": max((r.get("value", 0.0) or 0.0) for r in std_res) if std_res else None,
                "limit": args.std_limit, "pass": all(r["pass"] for r in std_res)},
        "static": {"value": max((r.get("value", 0.0) or 0.0) for r in static_res) if static_res else None,
                   "limit": args.static_limit, "pass": all(r["pass"] for r in static_res)},
    }
    if size_errors:
        checks["frame_size"] = {"value": len(size_errors), "limit": 0, "pass": False}
        judge.add("frame_size")
    for kname, c in checks.items():
        c["judged"] = kname in judge
    failures = [kname for kname, c in checks.items() if c["judged"] and not c["pass"]]

    qa.ensure_dir(out_dir)
    with open(os.path.join(out_dir, "sequence.csv"), "w", newline="", encoding="utf-8") as f:
        wr = csv.writer(f)
        wr.writerow(["frame", "file", "mean_luma", "mean_linear", "ev", "diff", "luma_step", "ev_rate_window",
                     "spike"])
        for i in range(n):
            wr.writerow([i, os.path.basename(files[i]), "%.6f" % mean_luma[i], "%.6f" % mean_lin[i], "%.4f" % ev[i],
                         "%.6f" % diff[i], "%.6f" % step[i], "%.4f" % ev_rate[i], 1 if i in spike_set else 0])
    diffs = diff[1:]
    result = {
        "name": name, "source": folder, "generated": qa.now_iso(), "tool": "sequence_metrics.py",
        "tool_version": qa.TOOL_VERSION, "frames": n, "fps": fps, "size": list(size0) if size0 else None,
        "duration_s": n / fps,
        "settings": {"luma_region": "frames.csv per frame (%d of %d frames had a region)" % (region_frames, n)
                     if csv_regions is not None else luma_shape,
                     "segment_breaks": seg_breaks, "diff_region": diff_shape, "sigma": args.sigma,
                     "spike_window": args.spike_window, "diff_floor": args.diff_floor,
                     "luma_step_limit_per_frame": step_limit, "ev_rate_limit": args.ev_rate_limit,
                     "ev_window_s": args.ev_window, "std_stat": args.std_stat, "static_stat": args.static_stat},
        "series_stats": {"mean_luma": {"min": float(mean_luma.min()), "max": float(mean_luma.max()),
                                       "mean": float(mean_luma.mean())},
                         "diff": {"mean": float(diffs.mean()), "std": float(diffs.std()), "max": float(diffs.max()),
                                  "max_frame": int(np.argmax(diffs)) + 1}},
        "spikes": spikes, "std_regions": std_res, "static_regions": static_res, "size_errors": size_errors,
        "checks": checks, "failures": failures, "pass": not failures,
        "per_frame": {"mean_luma": mean_luma.tolist(), "ev": ev.tolist(), "diff": diff.tolist()},
    }
    qa.write_json(os.path.join(out_dir, "sequence.json"), result)

    rows = [[kname, c["value"] if not isinstance(c["value"], float) else "%.5g" % c["value"],
             "%.5g" % c["limit"] if isinstance(c["limit"], float) else c["limit"],
             "yes" if c["judged"] else "no", "PASS" if c["pass"] else "FAIL"] for kname, c in checks.items()]
    md = ["# Sequence metrics - %s" % name, "", "Source: `%s`  " % folder,
          "Frames: %d at %.3g fps (%.2f s), size %s  " % (n, fps, n / fps, "x".join(map(str, size0 or []))),
          "Generated: %s" % result["generated"], "", qa.md_table(["Check", "Value", "Limit", "Judged", "Result"], rows),
          ""]
    if spikes:
        md += ["## Spikes (%.1f sigma)" % args.sigma, "",
               qa.md_table(["Frame", "File", "Diff", "Local mean", "Local std", "z"],
                           [[s["frame"], s["file"], "%.5f" % s["diff"], "%.5f" % s["local_mean"],
                             "%.5f" % s["local_std"], "%.1f" % s["z"] if s["z"] is not None else "inf"]
                            for s in spikes]), ""]
    for r in std_res + static_res:
        md.append("- region `%s`: %s" % (r["name"], ", ".join("%s=%s" % (kk, vv) for kk, vv in r.items()
                                                                 if kk not in ("name", "region"))))
    md.append("")
    md.append("Overall: **%s**" % ("PASS" if not failures else "FAIL (" + ", ".join(failures) + ")"))
    qa.write_text(os.path.join(out_dir, "summary.md"), "\n".join(md) + "\n")

    if args.plot:
        p1 = qa.line_plot([("mean luma", mean_luma.tolist(), (120, 190, 255))], title="%s - mean luma" % name)
        p2 = qa.line_plot([("EV (display-referred)", ev.tolist(), (250, 200, 80))], title="EV")
        p3 = qa.line_plot([("frame diff", [None] + diff[1:].tolist(), (240, 120, 200))],   # diff[0] is undefined
                          title="frame-to-frame mean abs diff",
                          markers=sorted(spike_set))
        sheet = Image.new("RGB", (p1.width, p1.height + p2.height + p3.height))
        sheet.paste(p1, (0, 0))
        sheet.paste(p2, (0, p1.height))
        sheet.paste(p3, (0, p1.height + p2.height))
        sheet.save(os.path.join(out_dir, "plot.png"))

    print("sequence_metrics: %s, %d frames, %s -> %s" % (name, n, "PASS" if not failures else
                                                           "FAIL " + ",".join(failures), out_dir))
    return 0 if (not failures or args.no_fail_exit) else 1


if __name__ == "__main__":
    sys.exit(main())
