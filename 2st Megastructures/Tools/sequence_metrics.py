"""Frame-sequence metrics (MASTER_PROMPT Part 11.6, 14.10 "Frame sequences", Part 10 #1-3, #6, #12, #15).

Usage:
  python -I Tools/sequence_metrics.py QA/captures/<run> [NAME ...] [--jobs N] [--toggle-level 2] [--no-fail]
  (<run> may also be a run id under QA/captures or "latest"; NAME = sequence names without the "seq" prefix)

Reads every <run>/seqNAME/sequence.json written by the CaptureHarness (-sequence) and the frames frame_00000.png ...
Per sequence:
  - per-frame mean luma and its step (14.10: <= 1.2 levels per frame at 30 fps; flashes only where 14.5 allows),
  - frame-difference series (mean |luma difference| between consecutive frames) and spikes (pops, Part 10 #6),
  - activation-frame difference: at every frame of a set change, beat change or BeatDirector activation log line
    (sequence.json "events"), the frame difference in excess of the median of its neighbours (14.10: <= 0.5 level),
  - "nonincreasing1.5" (s 411-516: mean luma non-increasing within 1.5 levels), "monotone1.5" (s 860-900),
    "clip12" (clipped < 12 % per frame),
  - static cameras ("static"): per-pixel temporal std, the 99.9th percentile and maximum of the per-pixel
    frame-to-frame change, and pixels whose frame-to-frame difference flips sign with |d| > --toggle-level more than
    10 times (14.5.2 (5)); beat 7 also the frame-to-frame brightness ratio of bright pixels (14.5.7 (3): <= 1.6),
  - "shimmer2.0" pairs (pan_X at 1080p and pan_X_ss at 2 x 2 supersampling): the per-pixel temporal std averaged over
    the frame and the beat's thin-line regions minus the same for the supersampled reference (<= 2.0 levels).
No grass / water / mote masks exist yet, so the static thresholds use the 99.9th percentile (the maxima are reported).
Writes <run>/sequence_metrics.json, <run>/sequence_metrics.md and <run>/seqNAME/luma.png (mean-luma and step plot).
"""
import argparse
import json
import math
import os
import re
import sys
import time
from concurrent.futures import ProcessPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # python -I drops the script folder from sys.path
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
import qa_common as q  # noqa: E402

STATIC_LIMITS = {1: 3.0, 2: 2.0, 3: 2.0, 4: 2.0, 5: 2.0, 6: 2.0, 7: 2.0}   # levels per frame (14.5 acceptance lists)
TOGGLE_MAX = 10


def parse_checks(s):
    out = {}
    for tok in (s or "").split(","):
        tok = tok.strip()
        if not tok:
            continue
        m = re.match(r"^([a-z_]+?)([\d.]+)?$", tok)
        if m:
            out[m.group(1)] = float(m.group(2)) if m.group(2) else None
    return out


def frame_files(folder):
    return sorted(f for f in os.listdir(folder) if f.startswith("frame_") and f.endswith(".png"))


def load_lum(path, down=1):
    rgb = q.load_rgb(path)
    clip = 100.0 * float(q.clipped_mask(rgb).mean())
    lum = q.luma(rgb)
    if down == 2:
        lum = q.downsample2(lum)
    return lum, clip


def excess_at(diffs, i, rad=5):
    """Frame difference at i minus the median of its neighbours (i - rad .. i + rad, i and i + 1 excluded)."""
    n = len(diffs)
    if i <= 0 or i >= n:
        return None
    nb = [diffs[j] for j in range(max(1, i - rad), min(n, i + rad + 1)) if j != i and j != i + 1]
    if not nb:
        return None
    return float(diffs[i] - np.median(nb))


def plot(path, means, steps, limit):
    W, H, pad = 1000, 320, 30
    im = Image.new("RGB", (W, H), (20, 20, 24))
    dr = ImageDraw.Draw(im)
    n = len(means)
    if n < 2:
        im.save(path)
        return
    lo, hi = min(means), max(means)
    if hi - lo < 1:
        hi, lo = hi + 0.5, lo - 0.5

    def xy(i, v, a, b, top, bot):
        return pad + (W - 2 * pad) * i / (n - 1), bot - (bot - top) * (v - a) / max(b - a, 1e-6)

    dr.line([xy(i, v, lo, hi, pad, H * 0.6) for i, v in enumerate(means)], fill=(120, 200, 255), width=2)
    smax = max(limit * 1.5, max(abs(x) for x in steps) if steps else 1.0)
    dr.line([(pad, H * 0.8), (W - pad, H * 0.8)], fill=(80, 80, 80))
    for lim in (limit, -limit):
        yl = H * 0.8 - (H * 0.15) * lim / smax
        dr.line([(pad, yl), (W - pad, yl)], fill=(140, 60, 60))
    dr.line([(pad + (W - 2 * pad) * (i + 1) / (n - 1), H * 0.8 - (H * 0.15) * v / smax) for i, v in enumerate(steps)], fill=(255, 200, 90), width=1)
    dr.text((pad, 6), "mean luma %.1f .. %.1f (blue); step per frame (orange), limit +-%.1f (red)" % (lo, hi, limit), fill=(220, 220, 220))
    im.save(path)


def analyze(args):
    folder, opts = args
    t0 = time.time()
    with open(os.path.join(folder, "sequence.json"), "r", encoding="utf-8") as f:
        meta = json.load(f)
    name = meta.get("name", os.path.basename(folder)[3:])
    checks = parse_checks(meta.get("checks", ""))
    files = frame_files(folder)
    res = {"name": name, "folder": os.path.basename(folder), "frames": len(files), "expected_frames": meta.get("frames"),
           "fps": meta.get("fps"), "static": meta.get("static"), "checks_requested": meta.get("checks", ""), "results": []}
    if not files:
        res["error"] = "no frames"
        return res
    scale = int(meta.get("scale", 1) or 1)
    down = 2 if scale == 2 else 1
    recs = {r.get("i"): r for r in meta.get("records", [])}
    beats = [recs[i].get("beat") for i in sorted(recs)] if recs else []
    beat = max(set(beats), key=beats.count) if beats else None

    means, clips, diffs = [], [], [0.0]
    prev = None
    acc = acc2 = maxchg = toggles = prevsign = ratio_max = None
    is_static = bool(meta.get("static")) or "static" in checks or "shimmer" in checks
    for i, fn in enumerate(files):
        lum, clip = load_lum(os.path.join(folder, fn), down)
        means.append(float(lum.mean()))
        clips.append(clip)
        if prev is not None:
            d = lum - prev
            diffs.append(float(np.abs(d).mean()))
            if is_static:
                ad = np.abs(d)
                maxchg = ad if maxchg is None else np.maximum(maxchg, ad)
                sign = np.where(d > opts["toggle_level"], 1, np.where(d < -opts["toggle_level"], -1, 0)).astype(np.int8)
                if prevsign is not None:
                    flip = (sign != 0) & (prevsign != 0) & (sign != prevsign)
                    toggles = flip.astype(np.int16) if toggles is None else toggles + flip
                prevsign = np.where(sign != 0, sign, prevsign) if prevsign is not None else sign
                if beat == 7:
                    bright = (prev > 64) & (lum > 64)
                    if bright.any():
                        r = np.maximum(lum[bright] / np.maximum(prev[bright], 1e-3), prev[bright] / np.maximum(lum[bright], 1e-3))
                        rv = float(np.percentile(r, 99.9))
                        ratio_max = rv if ratio_max is None else max(ratio_max, rv)
        if is_static:
            l64 = lum.astype(np.float64)
            acc = l64 if acc is None else acc + l64
            acc2 = l64 * l64 if acc2 is None else acc2 + l64 * l64
        prev = lum
    n = len(files)
    steps = [means[i + 1] - means[i] for i in range(n - 1)]
    res["mean_luma"] = [round(x, 3) for x in means]
    res["clipped_pct"] = [round(x, 4) for x in clips]
    res["frame_diff"] = [round(x, 4) for x in diffs]
    res["max_step"] = round(max((abs(x) for x in steps), default=0.0), 4)
    res["max_step_at"] = int(np.argmax(np.abs(steps))) + 1 if steps else 0
    if n != (meta.get("frames") or n):
        res["results"].append({"name": "frame_count", "pass": False, "value": n, "expected": meta.get("frames")})

    # frame-difference spikes (pops): above median + 6 MAD and above 1 level
    if len(diffs) > 8:
        dd = np.array(diffs[1:])
        med = float(np.median(dd))
        mad = float(np.median(np.abs(dd - med))) + 1e-6
        sp = [int(i + 1) for i in np.nonzero((dd > med + 6 * mad) & (dd > 1.0))[0]]
        res["diff_spikes"] = sp[:50]
        res["diff_median"] = round(med, 4)

    lim = checks.get("step", None)
    if "step" in checks:
        lim = lim or 1.2
        res["results"].append({"name": "step", "pass": res["max_step"] <= lim, "value": res["max_step"], "expected": "<= %g levels/frame" % lim,
                               "at_frame": res["max_step_at"]})
    for key in ("activation", "swap"):
        if key in checks:
            lv = checks[key] or 0.5
            evs = [e for e in meta.get("events", []) if (key == "activation") or e.get("type") == "set"]
            worst, rows = None, []
            for e in evs:
                ex = excess_at(diffs, int(e.get("i", 0)))
                if ex is None:
                    continue
                rows.append({"i": e.get("i"), "type": e.get("type"), "detail": e.get("detail"), "excess": round(ex, 4)})
                worst = ex if worst is None else max(worst, ex)
            res[key + "_events"] = rows
            res["results"].append({"name": key, "pass": worst is None or worst <= lv, "value": None if worst is None else round(worst, 4),
                                   "expected": "<= %g level (%d events)" % (lv, len(rows))})
    if "nonincreasing" in checks:
        tol = checks["nonincreasing"] or 1.5
        run_min, worst = means[0], 0.0
        for v in means:
            worst = max(worst, v - run_min)
            run_min = min(run_min, v)
        res["results"].append({"name": "nonincreasing", "pass": worst <= tol, "value": round(worst, 3), "expected": "rise above the running minimum <= %g" % tol})
    if "monotone" in checks:
        tol = checks["monotone"] or 1.5
        up = max((max(means[:i + 1]) - v for i, v in enumerate(means)), default=0.0)       # drops while rising
        dn = max((v - min(means[:i + 1]) for i, v in enumerate(means)), default=0.0)       # rises while falling
        worst = min(up, dn)
        res["results"].append({"name": "monotone", "pass": worst <= tol, "value": round(worst, 3), "expected": "monotone within +-%g" % tol,
                               "direction": "rising" if up <= dn else "falling"})
    if "clip" in checks:
        lv = checks["clip"] or 12.0
        mx = max(clips)
        res["results"].append({"name": "clip", "pass": mx < lv, "value": round(mx, 3), "expected": "< %g %% per frame" % lv})

    if is_static and acc is not None and n >= 2:
        mean = acc / n
        var = np.maximum(acc2 / n - mean * mean, 0.0)
        std = np.sqrt(var)
        res["temporal_std_mean"] = round(float(std.mean()), 4)
        res["temporal_std_p999"] = round(float(np.percentile(std, 99.9)), 4)
        if maxchg is not None:
            res["change_p999"] = round(float(np.percentile(maxchg, 99.9)), 3)
            res["change_max"] = round(float(maxchg.max()), 3)
        if toggles is not None:
            res["toggle_pixels_gt10"] = int((toggles > TOGGLE_MAX).sum())
            res["toggle_max"] = int(toggles.max())
        if "static" in checks:
            lv = STATIC_LIMITS.get(beat or 2, 2.0)
            v = res.get("change_p999", 0.0)
            res["results"].append({"name": "static_change", "pass": v <= lv, "value": v, "expected": "p99.9 of the per-pixel change <= %g levels (max %s)" % (lv, res.get("change_max"))})
            tp = res.get("toggle_pixels_gt10", 0)
            res["results"].append({"name": "static_toggles", "pass": tp <= max(1, int(1e-4 * mean.size)), "value": tp,
                                   "expected": "pixels toggling > 10 times <= 0.01 %% of the frame (toggle max %s)" % res.get("toggle_max")})
            if beat == 7 and ratio_max is not None:
                res["results"].append({"name": "panel_ratio", "pass": ratio_max <= 1.6, "value": round(ratio_max, 3), "expected": "p99.9 bright-pixel ratio <= 1.6"})
        if "shimmer" in checks:
            regions = {"frame": (0.0, 0.0, 1.0, 1.0)}
            regions.update(q.ALIAS_REGIONS.get(beat or 0, {}))
            res["shimmer_regions"] = {}
            h, w = std.shape
            for rn, rect in regions.items():
                x0, y0, x1, y1 = q.rect_px(rect, w, h)
                res["shimmer_regions"][rn] = round(float(std[y0:y1, x0:x1].mean()), 4)
    res["beat"] = beat
    res["seconds"] = round(time.time() - t0, 1)
    if opts.get("plot", True):
        try:
            plot(os.path.join(folder, "luma.png"), means, steps, lim or 1.2)
        except Exception as e:  # noqa: BLE001
            res["plot_error"] = str(e)
    return res


def pair_shimmer(results, limit_default=2.0):
    by = {r["name"]: r for r in results}
    for r in results:
        if "shimmer_regions" not in r or r["name"].endswith("_ss"):
            continue
        ref = by.get(r["name"] + "_ss")
        lim = parse_checks(r.get("checks_requested", "")).get("shimmer") or limit_default
        if ref is None or "shimmer_regions" not in ref:
            r["results"].append({"name": "shimmer", "pass": False, "value": None, "expected": "pair %s_ss missing" % r["name"]})
            continue
        deltas = {k: round(v - ref["shimmer_regions"].get(k, 0.0), 4) for k, v in r["shimmer_regions"].items()}
        r["shimmer_delta"] = deltas
        worst = max(deltas.values())
        r["results"].append({"name": "shimmer", "pass": worst <= lim, "value": worst,
                             "expected": "temporal std - 4x supersampled <= %g levels (%s)" % (lim, ", ".join("%s %.2f" % kv for kv in deltas.items()))})


def write_md(path, run, results):
    lines = ["# Frame-sequence metrics - %s" % os.path.basename(run), "",
             "Generated %s by Tools/sequence_metrics.py." % time.strftime("%Y-%m-%d %H:%M:%S"), "",
             "| sequence | frames | beat | max step | diff median | spikes | std mean | toggles>10 | result |",
             "|---|---|---|---|---|---|---|---|---|"]
    for r in results:
        if "error" in r:
            lines.append("| %s | 0 | - | - | - | - | - | - | ERROR %s |" % (r["name"], r["error"]))
            continue
        fails = [c["name"] for c in r["results"] if not c["pass"]]
        lines.append("| %s | %d | %s | %s | %s | %s | %s | %s | %s |" % (
            r["name"], r["frames"], q.fmt(r.get("beat")), q.fmt(r.get("max_step"), 3), q.fmt(r.get("diff_median"), 3),
            len(r.get("diff_spikes", [])), q.fmt(r.get("temporal_std_mean"), 3), q.fmt(r.get("toggle_pixels_gt10")),
            "ok" if not fails else "FAIL: " + ", ".join(fails)))
    lines += ["", "Failed checks:", ""]
    anyf = False
    for r in results:
        for c in r.get("results", []):
            if not c["pass"]:
                anyf = True
                lines.append("- %s: %s = %s (expected %s)" % (r["name"], c["name"], c["value"], c["expected"]))
    if not anyf:
        lines.append("- none")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run", nargs="?", default="latest")
    ap.add_argument("names", nargs="*")
    ap.add_argument("--jobs", type=int, default=max(1, min(4, (os.cpu_count() or 2) - 1)))
    ap.add_argument("--toggle-level", type=float, default=2.0)
    ap.add_argument("--no-plot", action="store_true")
    ap.add_argument("--no-fail", action="store_true")
    a = ap.parse_args()
    run = q.resolve_run(a.run)
    if not run:
        print("[sequence_metrics] run folder not found: %s" % a.run)
        return 2
    folders = sorted(os.path.join(run, d) for d in os.listdir(run)
                     if d.startswith("seq") and os.path.isfile(os.path.join(run, d, "sequence.json")))
    if a.names:
        want = set(a.names)
        folders = [f for f in folders if os.path.basename(f)[3:] in want]
        # keep shimmer references with their pans
        for nme in list(want):
            p = os.path.join(run, "seq" + nme + "_ss")
            if os.path.isfile(os.path.join(p, "sequence.json")) and p not in folders:
                folders.append(p)
    if not folders:
        print("[sequence_metrics] no sequences in %s" % run)
        return 2
    opts = {"toggle_level": a.toggle_level, "plot": not a.no_plot}
    t0 = time.time()
    jobs = [(f, opts) for f in folders]
    if a.jobs > 1 and len(jobs) > 1:
        with ProcessPoolExecutor(max_workers=a.jobs) as ex:
            results = list(ex.map(analyze, jobs))
    else:
        results = [analyze(j) for j in jobs]
    pair_shimmer(results)
    nfail = sum(1 for r in results for c in r.get("results", []) if not c["pass"]) + sum(1 for r in results if "error" in r)
    q.write_json(os.path.join(run, "sequence_metrics.json"), {"run": os.path.basename(run), "sequences": results,
                                                              "failed_checks": nfail, "seconds": round(time.time() - t0, 1)})
    write_md(os.path.join(run, "sequence_metrics.md"), run, results)
    print("[sequence_metrics] %s: %d sequences, %d failed checks, %.1f s" % (os.path.basename(run), len(results), nfail, time.time() - t0))
    return 0 if (a.no_fail or nfail == 0) else 1


if __name__ == "__main__":
    sys.exit(main())
