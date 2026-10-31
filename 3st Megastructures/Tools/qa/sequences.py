#!/usr/bin/env python3
"""sequences: frame-sequence tests of Thread captures (MASTER_PROMPT 11.6, 14.10.1 T-SWAP / T-BANK, 14.10.3).

Usage:
  python -I Tools/qa/sequences.py QA/captures/<run-id>            (every seq_* folder inside)
  python -I Tools/qa/sequences.py QA/captures/<run-id>/seq_<name> [more folders] [--run <id>] [--out-dir QA/reports]

Per sequence (frame_NNNNN.png + optional sequence.json, see qa_common): the mean absolute difference between
consecutive frames in 8-bit grey levels (Rec.709 display luma x 255) and its per-pixel maximum, the frame-mean
luminance series (display 0..1), and the checks
  - luma step per frame below 0.04 (14.10.3), 0.01 for T-SWAP, or sequence.json "lumaStepMax";
  - difference spikes: no frame difference above 3 x the running median (31-frame window, floor 0.25 grey levels);
  - static clips: mean difference below 0.3 grey levels with frozen dither (freezeDither true), 0.8 with temporal
    dither, 1.2 where D > 0.5; no pixel difference above 6 grey levels (14.5.2 acceptance 5);
  - T-SWAP (kind "swap", or s inside 279..291): every consecutive difference below 1.5 grey levels;
  - thin lines ("lines"): centroid jitter (residual of a linear fit over frames, a 0.1 px/frame pan is allowed)
    below 0.1 px and frame-to-frame energy variation below 10 percent;
  - "stability" crops: no pixel deviates more than 12 percent from the mean of 5 consecutive frames.
Writes QA/reports/sequences_<run>.json and .md.  Exit code 1 if any check FAILs.
"""
import argparse
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import numpy as np  # noqa: E402

import qa_common as qc  # noqa: E402
from qa_common import PASS, FAIL, SKIP, INFO, WARN, TH, check  # noqa: E402

_BLOCKS = u"▁▂▃▄▅▆▇█"


def sparkline(v, width=60):
    v = np.asarray([x for x in v if x is not None and np.isfinite(x)], dtype=np.float64)
    if v.size == 0:
        return ""
    if v.size > width:
        edges = np.linspace(0, v.size, width + 1).astype(int)
        v = np.array([v[a:b].max() for a, b in zip(edges[:-1], edges[1:]) if b > a])
    lo, hi = v.min(), v.max()
    if hi - lo < 1e-12:
        return _BLOCKS[0] * len(v)
    return "".join(_BLOCKS[int(round((x - lo) / (hi - lo) * 7))] for x in v)


def running_median(x, win):
    n = len(x)
    h = win // 2
    out = np.empty(n)
    for i in range(n):
        out[i] = np.median(x[max(0, i - h):min(n, i + h + 1)])
    return out


def _kind(seq):
    m = seq["meta"]
    k = (m.get("kind") or "").lower()
    if k:
        return k
    n = seq["name"].lower()
    for key in ("static", "swap", "pan", "encode", "ride"):
        if key in n:
            return key
    s = m.get("s")
    s0, s1 = m.get("s0"), m.get("s1")
    if isinstance(s, list) and s:
        s0, s1 = min(s), max(s)
    if s0 is not None and s1 is not None and 279.0 <= float(s0) and float(s1) <= 291.0:
        return "swap"
    return "transition"


def _darkness(meta):
    if "dAbove05" in meta:
        return 1.0 if meta["dAbove05"] else 0.0
    d = meta.get("darkness", meta.get("D"))
    if isinstance(d, list):
        d = max(d) if d else None
    try:
        return float(d) if d is not None else None
    except (TypeError, ValueError):
        return None


def _resolve_item(it, W, H):
    it = dict(it)
    it.setdefault("_ref", it.get("ref"))
    it.setdefault("_units", it.get("units", None if it.get("ref") else "px"))
    return it


def analyse_sequence(seq, th=TH, quiet=False):
    meta, frames = seq["meta"], seq["frames"]
    kind = _kind(seq)
    res = {"name": seq["name"], "dir": seq["dir"], "kind": kind, "fps": seq["fps"], "n_frames": len(frames),
           "checks": [], "series": {}}
    C = res["checks"]
    if len(frames) < 2:
        C.append(check("frames", FAIL if not frames else SKIP, len(frames), ">= 2"))
        res["status"] = qc.worst_status(C)
        return res
    lines = [_resolve_item(l, 0, 0) for l in meta.get("lines", []) or []]
    stabs = [_resolve_item(s, 0, 0) for s in meta.get("stability", []) or []]
    luma, luma_lin, mdiff, xdiff = [], [], [], []
    line_ser = [[] for _ in lines]
    stab_crops = [[] for _ in stabs]
    prev = None
    size = None
    for k, (idx, path) in enumerate(frames):
        try:
            rgb8, _ = qc.load_rgb8(path)
        except Exception as e:
            C.append(check("frame %d readable" % idx, FAIL, None, None, str(e)))
            prev = None
            continue
        H, W = rgb8.shape[:2]
        if size is None:
            size = (W, H)
        elif (W, H) != size:
            C.append(check("frame %d size" % idx, FAIL, "%dx%d" % (W, H), "%dx%d" % size))
            continue
        g = qc.grey_levels(rgb8)
        luma.append(float(g.mean() / 255.0))
        Ll = qc.luma_linear(rgb8)
        luma_lin.append(float(Ll.mean()))
        if prev is not None:
            d = np.abs(g - prev)
            mdiff.append(float(d.mean()))
            xdiff.append(float(d.max()))
        prev = g
        for j, ln in enumerate(lines):
            x, y0, w, h, tol = qc.line_spec_resolved(ln, W, H)
            lp = qc.line_lum(Ll, ln)
            if k == 0 or "_xc" not in ln:
                ln["_xc"] = qc.locate_line(lp, x, y0, h, tol)
            r = qc.line_probe(lp, ln["_xc"], y0, w, h)
            line_ser[j].append(r if r.get("found") else None)
            # follow a slow pan: re-centre the (integer) crop when the line drifts 8 px off its centre; the
            # centroid is in absolute image coordinates, so the series stays continuous
            if r.get("found") and abs(r["centroid_mean"] - ln["_xc"]) > 8.0:
                ln["_xc"] = float(np.floor(r["centroid_mean"])) + 0.5
        for j, st in enumerate(stabs):
            x0, y0, x1, y1 = qc.item_rect(st, "rect", W, H)
            stab_crops[j].append(g[y0:y1, x0:x1].astype(np.float32) / 255.0)
        if not quiet and k % 100 == 0:
            print("[sequences] %s frame %d/%d" % (seq["name"], k + 1, len(frames)), flush=True)
    res["size"] = list(size) if size else None
    luma = np.array(luma)
    steps = np.abs(np.diff(luma)) if luma.size > 1 else np.zeros(0)
    mdiff, xdiff = np.array(mdiff), np.array(xdiff)
    res["series"] = {"luma": [round(v, 5) for v in luma], "luma_linear": [round(v, 5) for v in luma_lin],
                     "mean_abs_diff": [round(v, 4) for v in mdiff], "max_abs_diff": [round(v, 2) for v in xdiff]}
    res["summary"] = {"luma_min": float(luma.min()), "luma_max": float(luma.max()),
                      "max_luma_step": float(steps.max()) if steps.size else 0.0,
                      "max_luma_step_frame": int(np.argmax(steps)) + 1 if steps.size else None,
                      "mean_diff": float(mdiff.mean()) if mdiff.size else 0.0,
                      "max_diff": float(mdiff.max()) if mdiff.size else 0.0,
                      "max_pixel_diff": float(xdiff.max()) if xdiff.size else 0.0}
    S = res["summary"]

    # luma steps
    lim = float(meta.get("lumaStepMax", th["swap_luma_step_max"] if kind == "swap" else th["seq_luma_step_max"]))
    C.append(check("luma step/frame", PASS if S["max_luma_step"] < lim else FAIL, round(S["max_luma_step"], 5),
                   "< %g" % lim, "at frame %s" % S["max_luma_step_frame"]))
    # spikes
    if mdiff.size >= 3:
        ratio = float(meta.get("spikeRatio", th["spike_ratio"]))
        floor = float(meta.get("spikeFloor", th["spike_floor_grey"]))
        med = running_median(mdiff, int(th["spike_window"]))
        sp = np.flatnonzero((mdiff > ratio * med) & (mdiff > floor))
        res["spikes"] = [int(i) + 1 for i in sp[:50]]
        C.append(check("diff spikes", PASS if sp.size == 0 else FAIL, int(sp.size), "0 above %gx running median" % ratio,
                       ("frames %s" % res["spikes"][:10]) if sp.size else "floor %g grey levels" % floor))
    # static clip
    if kind == "static":
        fd = meta.get("freezeDither")
        D = _darkness(meta)
        if fd:
            slim, why = th["static_frozen_max"], "frozen dither"
        elif D is not None and D > 0.5:
            slim, why = th["static_temporal_dark_max"], "temporal dither, D > 0.5"
        else:
            slim, why = th["static_temporal_max"], "temporal dither" + ("" if fd is not None else " (freezeDither not declared)")
        slim = float(meta.get("meanDiffMax", slim))
        C.append(check("static mean diff", PASS if S["mean_diff"] < slim else FAIL, round(S["mean_diff"], 4),
                       "< %g grey" % slim, why))
        plim = float(meta.get("maxPixelDiff", th["static_max_pixel_diff"]))
        C.append(check("static shimmer (max pixel diff)", PASS if S["max_pixel_diff"] <= plim else FAIL,
                       round(S["max_pixel_diff"], 2), "<= %g grey" % plim))
    elif kind == "swap":
        slim = float(meta.get("meanDiffMax", th["swap_mean_diff_max"]))
        C.append(check("T-SWAP consecutive diff", PASS if S["max_diff"] < slim else FAIL, round(S["max_diff"], 4),
                       "< %g grey (every pair)" % slim))
    elif "meanDiffMax" in meta:
        slim = float(meta["meanDiffMax"])
        C.append(check("consecutive diff", PASS if S["max_diff"] < slim else FAIL, round(S["max_diff"], 4),
                       "< %g grey (every pair)" % slim))
    if "maxPixelDiff" in meta and kind != "static":
        plim = float(meta["maxPixelDiff"])
        C.append(check("max pixel diff", PASS if S["max_pixel_diff"] <= plim else FAIL, round(S["max_pixel_diff"], 2),
                       "<= %g grey" % plim))
    # thin lines
    res["lines"] = {}
    for j, ln in enumerate(lines):
        name = ln.get("name", "line%d" % j)
        ser = line_ser[j]
        ok = np.array([r is not None for r in ser])
        if ok.sum() < 3:
            C.append(check("line[%s] found" % name, FAIL, int(ok.sum()), ">= 3 frames"))
            continue
        n = np.flatnonzero(ok).astype(np.float64)
        cen = np.array([r["centroid_mean"] for r in ser if r is not None])
        en = np.array([r["energy_mean"] for r in ser if r is not None])
        fw = np.array([r["fwhm_px"] for r in ser if r is not None])
        fit = np.polyfit(n, cen, 1)
        resid = cen - np.polyval(fit, n)
        jit = float(np.abs(resid).max())
        ecv = float(np.std(en) / max(abs(en.mean()), 1e-12))
        res["lines"][name] = {"pan_px_per_frame": float(fit[0]), "jitter_max_px": jit,
                              "jitter_rms_px": float(np.sqrt(np.mean(resid ** 2))), "energy_cv": ecv,
                              "fwhm_median": float(np.median(fw)), "frames_found": int(ok.sum()),
                              "centroid": [round(v, 4) for v in cen]}
        C.append(check("line[%s] centroid jitter" % name, PASS if jit < th["line_jitter_max_px"] else FAIL, round(jit, 4),
                       "< %g px" % th["line_jitter_max_px"], "pan %.4f px/frame" % fit[0]))
        C.append(check("line[%s] energy variation" % name, PASS if ecv < th["line_energy_var_max"] else FAIL,
                       round(ecv, 4), "< %g" % th["line_energy_var_max"]))
        if ok.sum() < len(ser):
            C.append(check("line[%s] found in every frame" % name, FAIL, int(ok.sum()), len(ser)))
    # stability crops
    res["stability"] = {}
    for j, st in enumerate(stabs):
        name = st.get("name", "crop%d" % j)
        crops = stab_crops[j]
        win = int(st.get("window", th["stability_window"]))
        tol = float(st.get("tol", th["stability_tol"]))
        floor = float(st.get("floor", th["stability_floor"]))
        if len(crops) < win or crops[0].size == 0:
            C.append(check("stability[%s]" % name, SKIP, len(crops), ">= %d frames" % win))
            continue
        stack = np.stack(crops)
        worst, worst_n = 0.0, None
        for a in range(0, len(crops) - win + 1):
            blk = stack[a:a + win]
            m = blk.mean(axis=0)
            dev = (np.abs(blk - m[None]) / np.maximum(m, floor)[None]).max()
            if dev > worst:
                worst, worst_n = float(dev), a
        res["stability"][name] = {"max_rel_dev": worst, "window_start": worst_n}
        C.append(check("stability[%s]" % name, PASS if worst <= tol else FAIL, round(worst, 4), "<= %g" % tol,
                       "5-frame mean, floor %g" % floor))
    res["status"] = qc.worst_status(C)
    return res


def build_markdown(run, results):
    allc = [c for r in results for c in r["checks"]]
    cnt = qc.count_status(allc)
    L = ["# Frame sequences: %s" % run, "",
         "Sequences: %d. Checks: %d PASS, %d FAIL, %d WARN, %d SKIP." % (len(results), cnt[PASS], cnt[FAIL],
                                                                       cnt[WARN], cnt[SKIP]), "",
         "**Result: %s**" % (FAIL if cnt[FAIL] else (PASS if results else SKIP)), "", "## Summary", ""]
    rows = []
    for r in results:
        S = r.get("summary", {})
        rows.append([r["name"], r["kind"], r["n_frames"], qc.fmt(S.get("max_luma_step"), 4), qc.fmt(S.get("mean_diff")),
                     qc.fmt(S.get("max_diff")), qc.fmt(S.get("max_pixel_diff"), 1), r["status"],
                     ", ".join(c["name"] for c in r["checks"] if c["status"] == FAIL)])
    L.append(qc.md_table(["Sequence", "Kind", "Frames", "Max luma step", "Mean diff", "Max diff", "Max px diff",
                          "Result", "Failed checks"], rows))
    for r in results:
        L += ["## %s (%s)" % (r["name"], r["status"]), "", "`%s`, %d frames at %g fps, kind %s" % (
            r["dir"], r["n_frames"], r["fps"], r["kind"]), ""]
        if r.get("series"):
            L += ["- luma      `%s`" % sparkline(r["series"].get("luma", [])),
                  "- mean diff `%s`" % sparkline(r["series"].get("mean_abs_diff", [])), ""]
        L.append(qc.md_table(["Check", "Status", "Value", "Threshold", "Note"],
                             [[c["name"], c["status"], qc.fmt(c["value"], 4), c["threshold"] or "", c["note"] or ""]
                              for c in r["checks"]]))
    return "\n".join(L)


def run(paths, run_name=None, out_dir=qc.REPORTS_DIR, quiet=False):
    seq_dirs = qc.resolve_sequence_dirs(paths)
    if run_name is None:
        base = paths[0] if paths else "."
        b = os.path.abspath(base)
        run_name = qc.run_id_for(os.path.dirname(b) if os.path.basename(b).lower().startswith("seq_") else b)
    results = [analyse_sequence(qc.load_sequence(d), TH, quiet) for d in seq_dirs]
    allc = [c for r in results for c in r["checks"]]
    status = FAIL if any(c["status"] == FAIL for c in allc) else (PASS if results else SKIP)
    js = os.path.join(out_dir, "sequences_%s.json" % run_name)
    md = os.path.join(out_dir, "sequences_%s.md" % run_name)
    qc.write_json(js, {"run": run_name, "status": status, "sequences": results})
    qc.write_text(md, build_markdown(run_name, results))
    fails = ["%s: %s (%s)" % (r["name"], c["name"], qc.fmt(c["value"], 4)) for r in results for c in r["checks"]
             if c["status"] == FAIL]
    return {"tool": "sequences", "status": status, "json": js, "md": md, "fails": fails,
            "counts": qc.count_status(allc), "n": len(results)}


def main(argv=None):
    ap = argparse.ArgumentParser(prog="sequences.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("paths", nargs="+", help="capture folder(s) with seq_* subfolders, or sequence folders")
    ap.add_argument("--run", help="run id (default: capture folder name)")
    ap.add_argument("--out-dir", default=qc.REPORTS_DIR)
    ap.add_argument("--quiet", action="store_true")
    a = ap.parse_args(argv)
    missing = [p for p in a.paths if not os.path.isdir(p)]
    if missing:
        print("not found: %s" % ", ".join(missing), file=sys.stderr)
        return 1
    r = run(a.paths, a.run, a.out_dir, a.quiet)
    print("[sequences] %s: %d sequences, %d FAIL checks -> %s" % (r["status"], r["n"], len(r["fails"]), r["md"]))
    for f in r["fails"][:30]:
        print("  FAIL " + f)
    return 1 if r["status"] == FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
