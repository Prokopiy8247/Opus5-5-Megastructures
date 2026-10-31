#!/usr/bin/env python3
"""image_metrics: per-still automated image metrics of a Thread capture folder (MASTER_PROMPT 11.3, 14.10.3).

Usage:
  python -I Tools/qa/image_metrics.py QA/captures/<run-id> [--previous QA/captures/<old-run>]
         [--regions Tools/qa/regions_default.json|none] [--run <id>] [--out-dir QA/reports] [--jobs 4]
         [--require-keyshots] [--regress-max 2.0] [--luma-scale display|linear]

Per image (PNG listed in report.json, or every *.png of the folder): size vs report.json, black / white frame,
magenta ratio (strict: must be 0), NaN-like isolated extreme pixels, clipped highlights (any channel at code 255,
with exclusion discs in degrees around projected points), luma mean and 1/50/99th percentiles (Rec.709 luma on
the display values and on the sRGB-decoded linear values; the 14.10.3 gates use display luma unless
--luma-scale linear), horizon colour agreement, banding score over declared regions, thin-line probe on declared
lines, region statistics, subject framing, window/RenderTexture parity (X.png vs X_window.png) and a regression
diff against a previous capture folder.
Writes QA/reports/image_metrics_<run>.json and .md.  Exit code 1 if any check FAILs, 0 otherwise.
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


# --------------------------------------------------------------------------------------------- pixel tests
def nan_like_pixels(rgb8, dark_t, bright_t, bright_mode="all"):
    """Isolated extreme pixels: display luma more than bright_t above the MAX (or dark_t below the MIN) of all
    24 other pixels of its 5 x 5 neighbourhood.  Thin lines and object corners are never isolated, so they do not
    count; a NaN (black after saturate) or Inf / firefly (white) single pixel does.  bright_mode 'clipped' only
    counts bright outliers that also reach code >= 254 (used where stars are legitimate point lights)."""
    L = qc.luma_display(rgb8)
    H, W = L.shape
    mode = "reflect" if (H > 2 and W > 2) else "edge"
    P = np.pad(L, 2, mode=mode)
    nmax = np.full((H, W), -np.inf, dtype=np.float32)
    nmin = np.full((H, W), np.inf, dtype=np.float32)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            if dy == 0 and dx == 0:
                continue
            sl = P[2 + dy:2 + dy + H, 2 + dx:2 + dx + W]
            np.maximum(nmax, sl, out=nmax)
            np.minimum(nmin, sl, out=nmin)
    bright = (L - nmax) > bright_t
    if bright_mode == "clipped":
        bright &= rgb8.max(axis=2) >= 254
    dark = (nmin - L) > dark_t
    ys, xs = np.nonzero(bright | dark)
    pos = [[int(x), int(y)] for y, x in list(zip(ys, xs))[:20]]
    return int(bright.sum()), int(dark.sum()), pos


def magenta_pixels(rgb8):
    """Error-shader magenta.  'like' tolerates the grade and dither: R, B >= 140, G <= 0.45 min(R, B),
    |R - B| <= 0.35 max(R, B) (no palette token or blend of two tokens comes close)."""
    a = rgb8.astype(np.int32)
    R, G, B = a[..., 0], a[..., 1], a[..., 2]
    strict = (R == 255) & (G == 0) & (B == 255)
    mn, mx = np.minimum(R, B), np.maximum(R, B)
    like = (R >= 140) & (B >= 140) & (G * 100 <= 45 * mn) & (np.abs(R - B) * 100 <= 35 * mx)
    return int(strict.sum()), int(like.sum())


# --------------------------------------------------------------------------------------------- one image
def analyse_image(shot, region_entries, opts):
    tag, path = shot["tag"], shot["file"]
    res = {"tag": tag, "file": path, "beat": shot["beat"], "s": shot["s"], "keyshot": shot["keyshot"],
           "checks": [], "metrics": {}}
    C = res["checks"]
    try:
        rgb8, info = qc.load_rgb8(path)
    except Exception as e:  # unreadable or missing
        C.append(check("readable", FAIL, None, None, "%s: %s" % (type(e).__name__, e)))
        res["status"] = FAIL
        return res
    H, W = rgb8.shape[:2]
    res["size"] = [W, H]
    beat = shot["beat"]
    M = res["metrics"]

    # size
    ew, eh = shot["exp_w"], shot["exp_h"]
    if ew and eh:
        C.append(check("size", PASS if (W, H) == (ew, eh) else FAIL, "%dx%d" % (W, H), "%dx%d" % (ew, eh)))
    else:
        C.append(check("size", SKIP, "%dx%d" % (W, H), None, "no requested size in report.json"))
    if info.get("alpha_min") is not None and info["alpha_min"] < 255:
        C.append(check("alpha", WARN, info["alpha_min"], 255, "PNG alpha below 255 (capture should be opaque)"))

    # black / white frames
    mx, mn = rgb8.max(axis=2), rgb8.min(axis=2)
    black = float((mx <= TH["black_code"]).mean())
    white = float((mn >= TH["white_code"]).mean())
    M["black_frac"], M["white_frac"] = black, white
    C.append(check("black frame", FAIL if black >= TH["flat_frac"] else PASS, round(black, 4),
                   "< %g of pixels <= code %d" % (TH["flat_frac"], TH["black_code"])))
    C.append(check("white frame", FAIL if white >= TH["flat_frac"] else PASS, round(white, 4),
                   "< %g of pixels >= code %d" % (TH["flat_frac"], TH["white_code"])))

    # magenta
    strict, like = magenta_pixels(rgb8)
    M["magenta_strict"], M["magenta_like"] = strict, like
    C.append(check("magenta", FAIL if like > 0 else PASS, like, "0 px",
                   "ratio %.3g (exact 255,0,255: %d px)" % (like / float(W * H), strict)))

    # regions (file + shot) and camera
    reg = qc.regions_for(tag, W, H, region_entries, shot.get("regions"))
    ropt = reg["options"]
    cam = qc.cam_for(shot, W, H)

    # NaN-like
    bmode = ropt.get("nanBrightMode", "clipped" if (beat or 0) >= 6 else "all")
    nb, nd, pos = nan_like_pixels(rgb8, float(ropt.get("nanDarkT", TH["nan_dark_t"])),
                                  float(ropt.get("nanBrightT", TH["nan_bright_t"])), bmode)
    M["nan_like_bright"], M["nan_like_dark"], M["nan_like_pos"] = nb, nd, pos
    C.append(check("NaN-like pixels", FAIL if nb + nd > 0 else PASS, nb + nd, "0",
                   "bright %d (mode %s), dark %d%s" % (nb, bmode, nd, (", first at " + qc.fmt(pos[:5])) if pos else "")))

    # exclusion discs
    clip_mask = np.zeros((H, W), dtype=bool)
    band_mask = np.zeros((H, W), dtype=bool)
    for ex in reg["exclude"]:
        name = ex.get("name", "exclude")
        d = qc.exclusion_dir(cam, ex, beat)
        if d is None:
            if ex.get("required", "coreMax" in ex):
                C.append(check("exclude[%s]" % name, WARN, None, None, "cannot place the disc (no fov/pose)"))
            continue
        clip_mask |= cam.angle_mask(d, float(ex.get("clipDeg", 1.2)))
        band_mask |= cam.angle_mask(d, float(ex.get("bandDeg", ex.get("clipDeg", 1.2))))
        if "coreMax" in ex:
            core = cam.angle_mask(d, 0.75 * float(ex.get("coreDeg", 0.9)))
            if core.any():
                cm = rgb8[core].astype(np.float64)
                mean_max = float(cm.mean(axis=0).max() / 255.0)
                C.append(check("core[%s] colour" % name, PASS if mean_max < float(ex["coreMax"]) else FAIL,
                               round(mean_max, 4), "< %g" % float(ex["coreMax"]),
                               "max pixel %.3f, %d px" % (cm.max() / 255.0, int(core.sum()))))
            else:
                C.append(check("core[%s] colour" % name, WARN, None, None, "disc centre outside the frame"))

    # clipped highlights
    clip_any = (rgb8 >= TH["clip_code"]).any(axis=2)
    clip_all = (rgb8 >= TH["clip_code"]).all(axis=2)
    n_excl = int(clip_mask.sum())
    cpct = 100.0 * float((clip_any & ~clip_mask).sum()) / float(W * H)
    M["clipped_pct"], M["clipped_all_pct"] = cpct, 100.0 * float((clip_all & ~clip_mask).sum()) / float(W * H)
    M["clip_excluded_px"] = n_excl
    if beat in TH["clip_pct_max"]:
        lim = TH["clip_pct_max"][beat]
        C.append(check("clipped highlights", PASS if cpct <= lim else FAIL, round(cpct, 4), "<= %g %%" % lim,
                       "any channel at 255; %d px excluded" % n_excl))
    else:
        C.append(check("clipped highlights", INFO, round(cpct, 4), None, "beat unknown"))

    # luminance
    Ld = qc.luma_display(rgb8)
    Ll = qc.luma_linear(rgb8)
    pd = np.percentile(Ld, [1, 50, 99])
    pl = np.percentile(Ll, [1, 50, 99])
    M["luma_display"] = {"mean": float(Ld.mean()), "p1": float(pd[0]), "p50": float(pd[1]), "p99": float(pd[2]),
                         "std": float(Ld.std())}
    M["luma_linear"] = {"mean": float(Ll.mean()), "p1": float(pl[0]), "p50": float(pl[1]), "p99": float(pl[2]),
                        "std": float(Ll.std())}
    G = M["luma_display"] if opts.luma_scale == "display" else M["luma_linear"]
    if beat in TH["p1_min"]:
        lim = TH["p1_min"][beat]
        C.append(check("luma p1", PASS if G["p1"] >= lim else FAIL, round(G["p1"], 4), ">= %g" % lim, opts.luma_scale))
    lr = shot.get("luma_range")
    if lr and lr[0] is not None and lr[1] is not None:
        C.append(check("mean luma", PASS if lr[0] <= G["mean"] <= lr[1] else FAIL, round(G["mean"], 4),
                       "%g..%g" % (lr[0], lr[1]), opts.luma_scale))
    else:
        C.append(check("mean luma", INFO, round(G["mean"], 4), None, opts.luma_scale))

    # horizon agreement: explicit items (rect pair a/b, or a constant-elevation line with dipDeg), else the
    # default for beats 1-2 (elevation 0, central 30 percent of the columns, 1 px gap, 2 px bands at 1080p)
    hitems = list(reg["horizon"])
    if not hitems and beat in (1, 2) and ropt.get("horizonDefault", True):
        hitems = [{"name": "default", "dipDeg": 0.0, "cols": [0.35, 0.65], "_default": True}]
    for i, hz in enumerate(hitems):
        name = hz.get("name", "horizon%d" % i)
        tol = float(hz.get("tol", TH["horizon_tol"]))
        if "a" in hz and "b" in hz:
            ra, rb = qc.item_rect(hz, "a", W, H), qc.item_rect(hz, "b", W, H)
            if ra[2] <= ra[0] or ra[3] <= ra[1] or rb[2] <= rb[0] or rb[3] <= rb[1]:
                C.append(check("horizon[%s]" % name, SKIP, None, None, "region outside the frame"))
                continue
            ca = np.median(rgb8[ra[1]:ra[3], ra[0]:ra[2]].reshape(-1, 3).astype(np.float64), axis=0)
            cb = np.median(rgb8[rb[1]:rb[3], rb[0]:rb[2]].reshape(-1, 3).astype(np.float64), axis=0)
            dmax = float(np.abs(ca - cb).max() / 255.0)
            C.append(check("horizon[%s]" % name, PASS if dmax <= tol else FAIL, round(dmax, 4), "<= %g" % tol,
                           "a %s vs b %s" % (qc.fmt(list(ca), 1), qc.fmt(list(cb), 1))))
            continue
        row = shot.get("horizon_row") if hz.get("_default") else None
        hr = qc.horizon_compare(rgb8, cam, hz, row)
        if hr is None:
            C.append(check("horizon[%s]" % name, SKIP, None, None, "line not in frame or no pitch/fov"))
        elif hz.get("_default") and hr["spread_sky"] > 4.0:
            C.append(check("horizon[%s]" % name, SKIP, round(hr["row_mid"], 1), None,
                           "sky band not uniform (spread %.1f codes): objects on the horizon" % hr["spread_sky"]))
        else:
            C.append(check("horizon[%s]" % name, PASS if hr["diff"] <= tol else FAIL, round(hr["diff"], 4),
                           "<= %g" % tol, "row ~%.1f, %d cols: above %s vs below %s" % (
                               hr["row_mid"], hr["n_cols"], qc.fmt(list(hr["sky"]), 1), qc.fmt(list(hr["ground"]), 1))))

    # banding
    worst_band = None
    for name, rect, d, it in qc.band_regions_resolved(reg["banding"], W, H, cam):
        if rect is not None and (rect[2] - rect[0] < 8 or rect[3] - rect[1] < 60):
            C.append(check("banding[%s]" % name, SKIP, None, None, "region %s too small" % (rect,)))
            continue
        b = qc.band_score(rgb8, rect, d, band_mask, TH, it)
        M.setdefault("banding", {})[name] = {k: v for k, v in b.items() if k != "strips"}
        M["banding"][name]["strips"] = [{k: s[k] for k in ("strip", "rows", "steps_per_1000", "longest_run_px",
                                                          "resid_rms", "resid_pp")} for s in b["strips"]]
        if b.get("pass") is None:
            C.append(check("banding[%s]" % name, SKIP, None, None, b.get("note")))
            continue
        worst_band = b["score"] if worst_band is None else max(worst_band, b["score"])
        C.append(check("banding[%s]" % name, PASS if b["pass"] else FAIL, round(b["score"], 3), "score <= 1",
                       "steps/1000 %.2f, run %d px, resid rms %s / pp %s, %d strips" % (
                           b["steps_per_1000"], b["longest_run_px"], qc.fmt(b["resid_rms"]), qc.fmt(b["resid_pp"]),
                           b["n_strips"])))
    M["band_score_worst"] = worst_band

    # thin lines
    for i, ln in enumerate(reg["lines"]):
        name = ln.get("name", "line%d" % i)
        x, y0, w, h, tol = qc.line_spec_resolved(ln, W, H)
        lp = qc.line_lum(Ll, ln)
        xc = qc.locate_line(lp, x, y0, h, tol)
        r = qc.line_probe(lp, xc, y0, w, h)
        r["expected_x"], r["located_x"] = x, xc
        M.setdefault("lines", {})[name] = r
        for c in qc.evaluate_line(r, ln, H):
            C.append(c)

    # region statistics
    for i, st in enumerate(reg["stats"]):
        name = st.get("name", "stat%d" % i)
        x0, y0, x1, y1 = qc.item_rect(st, "rect", W, H)
        if x1 <= x0 or y1 <= y0:
            C.append(check("stats[%s]" % name, SKIP, None, None, "empty region"))
            continue
        src = Ld if st.get("scale", opts.luma_scale) == "display" else Ll
        crop = src[y0:y1, x0:x1]
        mu, sd = float(crop.mean()), float(crop.std())
        M.setdefault("stats", {})[name] = {"mean": mu, "std": sd, "rect": [x0, y0, x1, y1]}
        if "stdMax" in st:
            C.append(check("stats[%s] std" % name, PASS if sd < float(st["stdMax"]) else FAIL, round(sd, 4),
                           "< %g" % float(st["stdMax"])))
        if "meanMin" in st or "meanMax" in st:
            lo, hi = float(st.get("meanMin", -1e9)), float(st.get("meanMax", 1e9))
            C.append(check("stats[%s] mean" % name, PASS if lo <= mu <= hi else FAIL, round(mu, 4), "%g..%g" % (lo, hi)))
        if "meanNear" in st:
            v, rel = float(st["meanNear"]), float(st.get("meanRelTol", 0.12))
            C.append(check("stats[%s] mean~" % name, PASS if abs(mu - v) <= rel * v else FAIL, round(mu, 4),
                           "%g +- %g %%" % (v, 100 * rel)))

    # subject framing
    for sub in shot.get("subjects", []):
        name = sub.get("name", "subject")
        if sub.get("visible") is False or "box" not in sub:
            continue
        x0, y0, x1, y1 = [float(v) for v in sub["box"]]
        bw, bh = max(x1 - x0, 1e-6), max(y1 - y0, 1e-6)
        crop_frac = float(sub.get("maxCropFrac", 0.0))
        if crop_frac > 0:
            cw = (max(0.0, -x0) + max(0.0, x1 - W)) / bw
            ch = (max(0.0, -y0) + max(0.0, y1 - H)) / bh
            ok = cw <= crop_frac and ch <= crop_frac
            C.append(check("subject[%s] crop" % name, PASS if ok else FAIL, round(max(cw, ch), 4), "<= %g" % crop_frac))
        else:
            m = float(sub.get("margin", 0.03))
            slack = min(x0 - m * W, y0 - m * H, (1 - m) * W - x1, (1 - m) * H - y1)
            C.append(check("subject[%s] margin" % name, PASS if slack >= 0 else FAIL, round(slack, 1),
                           ">= 0 px (margin %g)" % m, "box %s" % qc.fmt([x0, y0, x1, y1], 1)))
        if "minWidthFrac" in sub:
            wf = (min(x1, W) - max(x0, 0.0)) / float(W)
            C.append(check("subject[%s] width" % name, PASS if wf >= float(sub["minWidthFrac"]) else FAIL,
                           round(wf, 4), ">= %g" % float(sub["minWidthFrac"])))

    # window / RenderTexture parity (11.2)
    wpath = os.path.splitext(path)[0] + "_window.png"
    if os.path.isfile(wpath):
        try:
            wrgb, _ = qc.load_rgb8(wpath)
            if wrgb.shape == rgb8.shape:
                mad = float(np.abs(wrgb.astype(np.int16) - rgb8.astype(np.int16)).mean())
                C.append(check("window parity", PASS if mad <= 1.0 else FAIL, round(mad, 4), "<= 1 code mean",
                               os.path.basename(wpath)))
            else:
                C.append(check("window parity", FAIL, "%s" % (wrgb.shape[:2],), "%s" % (rgb8.shape[:2],), "size differs"))
        except Exception as e:
            C.append(check("window parity", WARN, None, None, str(e)))

    # regression
    prev = opts.prev_map.get(tag) if opts.prev_map else None
    if prev:
        try:
            prgb, _ = qc.load_rgb8(prev)
            if prgb.shape == rgb8.shape:
                diff = np.abs(prgb.astype(np.int16) - rgb8.astype(np.int16))
                mad = float(diff.mean())
                M["regression"] = {"previous": prev, "mean_abs": mad, "frac_gt8": float((diff.max(axis=2) > 8).mean())}
                if opts.regress_max is not None:
                    C.append(check("regression", PASS if mad <= opts.regress_max else FAIL, round(mad, 3),
                                   "<= %g" % opts.regress_max))
                else:
                    C.append(check("regression", INFO, round(mad, 3), None, "mean abs diff (codes) vs previous run"))
            else:
                C.append(check("regression", INFO, None, None, "size differs from previous run"))
        except Exception as e:
            C.append(check("regression", WARN, None, None, str(e)))
    res["status"] = qc.worst_status(C)
    return res


class _Opts(object):
    def __init__(self, luma_scale, prev_map, regress_max):
        self.luma_scale, self.prev_map, self.regress_max = luma_scale, prev_map, regress_max


def _worker(args):
    shot, entries, o = args
    return analyse_image(shot, entries, _Opts(*o))


# --------------------------------------------------------------------------------------------- report
def keyshot_coverage(shots):
    sp = qc.spec()
    cov = {}
    for kt in sp.keyshots:
        cov[kt] = sorted({"%dx%d" % (s["exp_w"] or 0, s["exp_h"] or 0) for s in shots if s["keyshot"] == kt})
    return cov


def build_markdown(run, cap_dir, results, cov, warnings, require_keyshots, cov_fail):
    sp = qc.spec()
    allc = [c for r in results for c in r["checks"]]
    cnt = qc.count_status(allc)
    n_fail_img = sum(1 for r in results if r["status"] == FAIL)
    L = ["# Image metrics: %s" % run, "",
         "Capture folder: `%s`  " % cap_dir,
         "Scene constants: %s  " % sp.source,
         "Images: %d, failing images: %d. Checks: %d PASS, %d FAIL, %d WARN, %d SKIP, %d INFO." % (
             len(results), n_fail_img, cnt[PASS], cnt[FAIL], cnt[WARN], cnt[SKIP], cnt[INFO]),
         "", "**Result: %s**" % (FAIL if (cnt[FAIL] or cov_fail) else PASS), ""]
    if warnings:
        L += ["## Input warnings", ""] + ["- %s" % w for w in warnings[:50]] + [""]
    L += ["## Summary", ""]
    rows = []
    for r in results:
        m = r["metrics"]
        ld = m.get("luma_display", {})
        fails = [c["name"] for c in r["checks"] if c["status"] == FAIL]
        rows.append([r["tag"], r["beat"] or "-", "%dx%d" % tuple(r["size"]) if r.get("size") else "-",
                     qc.fmt(ld.get("mean")), qc.fmt(ld.get("p1")), qc.fmt(m.get("clipped_pct"), 4),
                     qc.fmt(m.get("band_score_worst")), r["status"], ", ".join(fails)])
    L.append(qc.md_table(["Image", "Beat", "Size", "Mean luma", "p1", "Clipped %", "Band score", "Result",
                          "Failed checks"], rows))
    L += ["## Key-shot coverage (14.10.2: every key shot at 1920x1080 and 3840x2160)", ""]
    crow = []
    for kt, res_list in cov.items():
        have = set(res_list)
        ok = "1920x1080" in have and "3840x2160" in have
        st = PASS if ok else (FAIL if require_keyshots else INFO)
        crow.append([kt, ", ".join(res_list) or "-", st])
    L.append(qc.md_table(["Key shot", "Requested sizes found", "Status"], crow))
    L += ["## Details", ""]
    for r in results:
        L += ["### %s (%s)" % (r["tag"], r["status"]), "", "`%s`" % r["file"], ""]
        L.append(qc.md_table(["Check", "Status", "Value", "Threshold", "Note"],
                             [[c["name"], c["status"], qc.fmt(c["value"], 4), c["threshold"] or "", c["note"] or ""]
                              for c in r["checks"]]))
    L += ["## Method notes", "",
          "- Luma: Rec.709 weights; 'display' on the 8-bit encoded values / 255, 'linear' on the exact sRGB-decoded "
          "values. The 14.10.3 gates (p1, expected mean ranges, region std) use display luma unless --luma-scale linear.",
          "- Clipped: any channel at code 255 (>= 0.999), percentage of all pixels outside the exclusion discs.",
          "- NaN-like: display luma more than %.2f above the max / %.2f below the min of the 24 neighbours "
          "(isolated); beats 6-7 count bright outliers only when they also clip (stars)." % (TH["nan_bright_t"], TH["nan_dark_t"]),
          "- Banding (14.10.3 + R6 6.2): 64-px strips, 9-px box, steps > 0.6 code per 1000 px <= 2; longest run "
          "(< 0.05 code) <= 6 px only where |slope| >= 0.06 code/px; residual vs a 129-px quadratic trend RMS <= 0.10 "
          "and p-p <= 0.60 code.  Score = worst ratio to its limit (<= 1 passes).",
          "- Thin line: crop 64 x 256 on linear luminance, flank-fitted background, FWHM per row (reported at 1080p "
          "scale), monotone 3-level edge on >= 95 % of rows, centroid residual <= 0.25 px, energy variation <= 10 %.",
          ""]
    return "\n".join(L)


def run(cap_dir, previous=None, regions=qc.DEFAULT_REGIONS, run_name=None, out_dir=qc.REPORTS_DIR, jobs=1,
        require_keyshots=False, regress_max=None, luma_scale="display", quiet=False):
    cap_dir = os.path.abspath(cap_dir)
    run_name = qc.run_id_for(cap_dir, run_name)
    shots, meta, warnings = qc.load_capture(cap_dir)
    shots = [s for s in shots if not s["tag"].endswith("_window") and not s["file"].lower().endswith("_window.png")]
    sp = qc.spec()
    warnings = list(sp.warnings) + warnings
    entries = [] if (regions in (None, "", "none")) else qc.load_regions(regions)
    if regions not in (None, "", "none") and not entries:
        warnings.append("regions file %s missing or empty" % regions)
    prev_map = {}
    if previous:
        pshots, _, _ = qc.load_capture(os.path.abspath(previous))
        for p in pshots:
            if os.path.isfile(p["file"]):
                prev_map[p["tag"]] = p["file"]
    o = (luma_scale, prev_map, regress_max)
    results = []
    if jobs and jobs > 1 and len(shots) > 1:
        try:
            from concurrent.futures import ProcessPoolExecutor
            with ProcessPoolExecutor(max_workers=jobs) as ex:
                results = list(ex.map(_worker, [(s, entries, o) for s in shots]))
        except Exception as e:  # fall back to serial
            warnings.append("parallel run failed (%s); serial fallback" % e)
            results = []
    if not results:
        for i, s in enumerate(shots):
            if not quiet:
                print("[image_metrics] %d/%d %s" % (i + 1, len(shots), s["tag"]), flush=True)
            results.append(analyse_image(s, entries, _Opts(*o)))
    results.sort(key=lambda r: r["tag"])
    cov = keyshot_coverage(shots)
    cov_fail = require_keyshots and any(not ("1920x1080" in v and "3840x2160" in v) for v in cov.values())
    if not shots:
        warnings.append("no PNG found in %s" % cap_dir)
    allc = [c for r in results for c in r["checks"]]
    status = FAIL if (any(c["status"] == FAIL for c in allc) or cov_fail) else (PASS if results else SKIP)
    js = os.path.join(out_dir, "image_metrics_%s.json" % run_name)
    md = os.path.join(out_dir, "image_metrics_%s.md" % run_name)
    qc.write_json(js, {"run": run_name, "capture_dir": cap_dir, "status": status, "meta": meta,
                       "spec_source": sp.source, "warnings": warnings, "keyshot_coverage": cov, "images": results})
    qc.write_text(md, build_markdown(run_name, cap_dir, results, cov, warnings, require_keyshots, cov_fail))
    fails = ["%s: %s (%s)" % (r["tag"], c["name"], qc.fmt(c["value"], 4)) for r in results for c in r["checks"]
             if c["status"] == FAIL]
    return {"tool": "image_metrics", "status": status, "json": js, "md": md, "fails": fails,
            "counts": qc.count_status(allc), "n": len(results)}


def main(argv=None):
    ap = argparse.ArgumentParser(prog="image_metrics.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("capture_dir", help="capture folder with *.png and report.json")
    ap.add_argument("--previous", help="previous capture folder for the regression diff")
    ap.add_argument("--regions", default=qc.DEFAULT_REGIONS, help="regions JSON (default: %(default)s; 'none' to disable)")
    ap.add_argument("--run", help="run id (default: capture folder name)")
    ap.add_argument("--out-dir", default=qc.REPORTS_DIR, help="report folder (default: %(default)s)")
    ap.add_argument("--jobs", type=int, default=1, help="parallel worker processes (default 1)")
    ap.add_argument("--require-keyshots", action="store_true", help="FAIL if a key shot lacks 1080p or 4K")
    ap.add_argument("--regress-max", type=float, help="FAIL if the mean abs diff vs --previous exceeds this (codes)")
    ap.add_argument("--luma-scale", choices=("display", "linear"), default="display",
                    help="luma scale of the 14.10.3 gates (default display)")
    ap.add_argument("--quiet", action="store_true")
    a = ap.parse_args(argv)
    if not os.path.isdir(a.capture_dir):
        print("capture folder not found: %s" % a.capture_dir, file=sys.stderr)
        return 1
    r = run(a.capture_dir, a.previous, a.regions, a.run, a.out_dir, a.jobs, a.require_keyshots, a.regress_max,
            a.luma_scale, a.quiet)
    print("[image_metrics] %s: %d images, %d FAIL checks -> %s" % (r["status"], r["n"], len(r["fails"]), r["md"]))
    for f in r["fails"][:30]:
        print("  FAIL " + f)
    return 1 if r["status"] == FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
