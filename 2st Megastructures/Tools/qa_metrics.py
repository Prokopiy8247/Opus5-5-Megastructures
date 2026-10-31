"""Automated image metrics for a capture run (MASTER_PROMPT Part 11.3, thresholds of 14.10, acceptance numbers of 14.5).

Usage:
  python -I Tools/qa_metrics.py QA/captures/<run> [--prev <run or folder>] [--no-prev] [--jobs N] [--no-isolated]
                                                  [--only PATTERN] [--no-fail]
  (<run> may also be a run id under QA/captures or "latest")

Per image (every PNG at the top level of the run folder; frame sequences are handled by sequence_metrics.py):
  size check (against report.json and the file name), black / white frame, magenta ratio, NaN-like isolated pixels
  (> 8 x the 5 x 5 median, 14.10 limit 0), clipped-highlight % (any channel >= 254; star-disc mask excluded), luma
  mean and percentiles, BandScore in the beat's banding region (14.10 definition, limit 6 px at 1080p, 12 px at 4K),
  aliasing helpers on the beat's thin-line regions, terrain-edge seam pairs (fog vs sky, CIE76 dE <= 2.0; open-sky
  shots of s 30-558 and KS01 through the porthole only, occluder-edge pairs dropped, "not evaluable" below 8 pairs or
  12 for KS02), the KS03 arch contrast against its no-haze variant (14.5.2 (8), ratio >= 0.6 at 36-44 deg), the
  per-beat thresholds of 14.10 (mean luma, clipped) for key shots and start / mid / end stills, key-shot acceptance
  checks that need no scene knowledge (KS01 porthole, KS02 blue upper third, KS06 bands, KS12 glints, star size),
  and a regression diff against the previous run (mean / p99 absolute difference, share of pixels changed > 8 levels).
Writes <run>/metrics.json and <run>/metrics.md. Exit code 0 when every check passes, 1 otherwise (--no-fail: always 0).
"""
import argparse
import fnmatch
import math
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # python -I drops the script folder from sys.path
import numpy as np  # noqa: E402
import qa_common as q  # noqa: E402

PORTHOLE_CENTRE = (-1.46, 2.98, 5.65)
PORTHOLE_R = 0.275
STILL_TAGS_WITH_THRESHOLDS = ("start", "mid", "end")
# terrain-edge seam: open-sky progress range (outdoor beats 2-4: past the capsule ramp at s 24 + 6 m, before the
# underpass at s 558) and the minimum number of surviving pairs for a verdict
SEAM_S0, SEAM_S1 = 30.0, 558.0
SEAM_MIN_PAIRS = 8
# 14.5.2 (8): arch luma std in KS03 around 40 deg elevation >= 0.6 of the render with hz forced to 0
HAZE_BAND_DEG = (36.0, 44.0)
HAZE_MIN_RATIO = 0.6
HAZE_CHANGED_LEVELS = 3        # a pixel the haze override moved by at least this much (any channel) is sky-dome arch
HAZE_MIN_ARCH_SHARE = 0.02     # below this share of the band the arch is not evaluable
VARIANT_MIN_DIFF = 0.5         # a debug variant closer than this to its base (mean abs, levels) shows no override effect


def thresholds_for(beat, s, shot):
    lo, hi, clip, clip_out = q.BEAT_THRESHOLDS[beat]
    if beat == 6 and s is not None and s >= 885.0:     # beat 6: last 6 s (9 m at 1.5 m/s) 30-110
        lo, hi = 30, 110
    if shot in q.KS_LUMA:
        lo, hi = q.KS_LUMA[shot]
    if shot in q.KS_CLIP:
        clip = q.KS_CLIP[shot]
    if shot == "KS11":                                  # T6: the tunnel mouth stays <= 12 % clipped at s 888
        clip = 12.0
    return lo, hi, clip, clip_out


def corona_rect(cam, s):
    """Banding region of beat 7: the corona above the outside star (2-10 deg above the disc, +-6 deg wide)."""
    u, v, z = q.dir_to_px(cam, q.star_for(s))
    if z <= 0:
        return None
    ppd = q.px_per_deg(cam)
    x0, x1 = (u - 6 * ppd) / cam["w"], (u + 6 * ppd) / cam["w"]
    y0, y1 = (v - 10 * ppd) / cam["h"], (v - 2 * ppd) / cam["h"]
    x0, x1, y0, y1 = max(0.0, x0), min(1.0, x1), max(0.0, y0), min(1.0, y1)
    if x1 - x0 < 0.05 or y1 - y0 < 0.03:
        return None
    return (x0, y0, x1, y1)


def check(results, name, ok, value, expected):
    results.append({"name": name, "pass": bool(ok), "value": value, "expected": expected})


def analyze(args):
    path, fname, rec, prev_path, opts = args
    t0 = time.time()
    out = {"file": fname}
    checks = []
    try:
        rgb = q.load_rgb(path)
    except Exception as e:  # noqa: BLE001
        out["error"] = "cannot read: %s" % e
        out["checks"] = [{"name": "readable", "pass": False, "value": str(e), "expected": "PNG"}]
        return out
    h, w = rgb.shape[:2]
    info = q.parse_name(fname)
    exp_w = (rec or {}).get("w") or info.get("w")
    exp_h = (rec or {}).get("h") or info.get("h")
    out.update({"w": w, "h": h, "kind": (rec or {}).get("kind") or info.get("kind"), "shot": (rec or {}).get("shot") or info.get("shot")})
    if exp_w and exp_h:
        check(checks, "size", w == exp_w and h == exp_h, "%dx%d" % (w, h), "%dx%d" % (exp_w, exp_h))
    s = (rec or {}).get("s")
    if s is None and info["kind"] == "ks":
        ks = q.key_shots().get(info["shot"])
        s = ks["s"] if ks else None
    beat = (rec or {}).get("beat") or info.get("beat") or (q.beat_of(s) if s is not None else None)
    out["beat"], out["s"] = beat, s
    shot = out["shot"] if out["kind"] in ("ks", "ks_variant") else None
    cam = q.camera_for(fname, rec, w, h)

    lum = q.luma(rgb)
    lum8 = np.clip(np.rint(lum), 0, 255).astype(np.uint8)
    mean = float(lum.mean())
    pct = q.percentiles(lum)
    out["mean_luma"] = round(mean, 3)
    out["percentiles"] = {k: round(v, 2) for k, v in pct.items()}
    out["mean_rgb"] = [round(float(x), 2) for x in rgb.reshape(-1, 3).mean(axis=0)]

    black = mean < 2.0 or pct["p99_9"] <= 5.0
    white = pct["p0_1"] >= 250.0
    mag = float(q.magenta_mask(rgb).mean())
    out.update({"black": black, "white": white, "magenta_ratio": mag})
    check(checks, "not_black", not black, round(mean, 2), "mean >= 2 and p99.9 > 5")
    check(checks, "not_white", not white, pct["p0_1"], "p0.1 < 250")
    check(checks, "magenta", mag == 0.0, mag, "0")

    clipped = q.clipped_mask(rgb)
    clip_pct = 100.0 * float(clipped.mean())
    smask = q.star_mask(cam, s if s is not None else 0.0) if cam is not None else None
    clip_out_pct = 100.0 * float((clipped & ~smask).mean()) if smask is not None else clip_pct
    out.update({"clipped_pct": round(clip_pct, 4), "clipped_outside_star_pct": round(clip_out_pct, 4), "star_in_frame": smask is not None})

    if not opts["no_isolated"]:
        bright, dark, raw = q.isolated_pixels(lum8)
        if smask is not None:   # the star disc is not an isolated pixel; recount outside it
            l2 = lum8.copy()
            l2[smask] = 0
            bright, dark, raw = q.isolated_pixels(l2)
        out["isolated"] = {"bright": bright, "dark": dark, "raw_bright": raw}
        check(checks, "isolated_pixels", bright == 0 and dark == 0, bright + dark, "0 (> 8 x the 5 x 5 median)")

    # per-beat thresholds (key shots and start / mid / end stills; free-looks and debug variants get defect checks only)
    tag = (rec or {}).get("tag") or info.get("tag") or ""
    applies = beat in q.BEAT_THRESHOLDS and (out["kind"] == "ks" or (out["kind"] == "still" and tag in STILL_TAGS_WITH_THRESHOLDS))
    out["thresholds_apply"] = bool(applies)
    if applies:
        lo, hi, cmax, cout = thresholds_for(beat, s, shot)
        check(checks, "mean_luma_beat%d" % beat, lo <= mean <= hi, round(mean, 2), "%g-%g" % (lo, hi))
        use = clip_out_pct if (smask is not None and beat != 7) else clip_pct
        check(checks, "clipped_beat%d" % beat, use <= cmax, round(use, 3), "<= %g %%" % cmax)
        if cout is not None and smask is not None:
            check(checks, "clipped_outside_star", clip_out_pct <= cout, round(clip_out_pct, 3), "<= %g %%" % cout)

    # banding
    if beat in q.BAND_REGIONS and out["kind"] in ("ks", "still", "extra", "defect"):
        rect = q.BAND_REGIONS[beat]
        region = "beat%d" % beat
        if beat == 7 and cam is not None:
            cr = corona_rect(cam, s if s is not None else 912.0)
            if cr is not None:
                rect, region = cr, "corona"
        if shot == "KS01" and cam is not None:
            pm = q.disc_mask(cam, PORTHOLE_CENTRE, q.dir_from_az_el(-45.0, 25.0), PORTHOLE_R * 0.8)
            if pm is not None and pm.any():
                ys, xs = np.nonzero(pm)
                rect, region = (xs.min() / w, ys.min() / h, (xs.max() + 1) / w, (ys.max() + 1) / h), "porthole"
        bs = q.band_score(lum, rect, h)
        bs["region"], bs["rect"] = region, [round(x, 4) for x in rect]
        out["band"] = bs
        check(checks, "band_score", bs["score"] <= bs["limit"], bs["score"], "<= %g px (%s)" % (bs["limit"], region))

    # aliasing helpers (informational)
    if beat in q.ALIAS_REGIONS:
        out["aliasing"] = {name: q.aliasing_score(lum, rect) for name, rect in q.ALIAS_REGIONS[beat].items()}

    # terrain-edge seam (fog vs sky, Part 10 #13, 14.5.2 (4)): only where the edge is open sky. Outdoor beats 2-4
    # (s 30-558: past the capsule ramp, before the underpass) for key shots and beat stills, plus KS01 through the
    # porthole opening only. Pairs whose patches straddle an occluder edge are dropped; with fewer than SEAM_MIN_PAIRS
    # survivors (12 for KS02, its acceptance count) the seam is reported "not evaluable" instead of passing or failing.
    seam = None
    if cam is not None and s is not None:
        if shot == "KS01":
            pm = q.disc_mask(cam, PORTHOLE_CENTRE, q.dir_from_az_el(-45.0, 25.0), PORTHOLE_R)
            if pm is not None and pm.any():
                seam = q.horizon_pairs(rgb, cam, n=64, mask=pm) + (SEAM_MIN_PAIRS, "porthole")
        elif out["kind"] in ("ks", "still") and SEAM_S0 <= s < SEAM_S1:
            seam = q.horizon_pairs(rgb, cam, n=16) + (12 if shot == "KS02" else SEAM_MIN_PAIRS, "open sky")
    if seam is not None:
        pairs, dropped, need, where = seam
        hz = {"pairs": pairs, "dropped": dropped, "required": need, "where": where, "evaluable": len(pairs) >= need}
        if pairs:
            hz["max_de76"] = max(p["de76"] for p in pairs)
            hz["mean_de76"] = round(float(np.mean([p["de76"] for p in pairs])), 3)
        out["horizon"] = hz
        if hz["evaluable"]:
            check(checks, "horizon_seam", hz["max_de76"] <= 2.0, round(hz["max_de76"], 3), "dE76 <= 2.0 (%d pairs, %s)" % (len(pairs), where))
        else:
            hz["note"] = "not evaluable: %d of %d required pairs survived (dropped %s)" % (len(pairs), need, dropped)

    # key-shot acceptance checks of 14.5 that need no scene knowledge
    if shot == "KS01" and cam is not None:
        pm = q.disc_mask(cam, PORTHOLE_CENTRE, q.dir_from_az_el(-45.0, 25.0), PORTHOLE_R)
        if pm is not None and pm.sum() > 50:
            pl = float(lum[pm].mean())
            px = rgb[pm].astype(np.int32)
            blue = float((px[:, 2] > px[:, 0] + 15).mean())
            out["porthole"] = {"pixels": int(pm.sum()), "mean_luma": round(pl, 2), "blue_dominant": round(blue, 3)}
            check(checks, "ks01_porthole_luma", 140 <= pl <= 225, round(pl, 2), "140-225")
            check(checks, "ks01_porthole_blue", blue >= 0.5, round(blue, 3), ">= 50 % B > R + 15")
    if shot == "KS02":
        top = rgb[: h // 3].reshape(-1, 3).astype(np.int32)
        blue = float((top[:, 2] > top[:, 0] + 15).mean())
        out["upper_third_blue"] = round(blue, 3)
        check(checks, "ks02_upper_third_blue", blue >= 0.6, round(blue, 3), ">= 60 %")
    if shot == "KS06":
        top = float(lum[: int(0.4 * h)].mean())
        low = float(lum[int(0.7 * h):].mean())
        out["ks06_bands"] = {"top40": round(top, 2), "low30": round(low, 2)}
        check(checks, "ks06_lit_arch", 150 <= top <= 230, round(top, 2), "top 40 % 150-230")
        check(checks, "ks06_levee_path", 20 <= low <= 70, round(low, 2), "lower 30 % 20-70")
    if shot == "KS07":
        hi_px = float((lum8 > 235).mean()) * 100.0
        out["ks07_over235_pct"] = round(hi_px, 4)
    if shot in ("KS12", "KS14") and cam is not None:
        u, v, z = q.dir_to_px(cam, q.STAR_L7)
        if z > 0 and 0 <= u < w and 0 <= v < h:
            ppd = q.px_per_deg(cam)
            r = int(3 * ppd)
            y0, y1, x0, x1 = max(0, int(v) - r), min(h, int(v) + r), max(0, int(u) - r), min(w, int(u) + r)
            core = lum8[y0:y1, x0:x1] >= 250
            d = 2.0 * math.sqrt(float(core.sum()) / math.pi)
            exp = 2.4 * ppd
            out["star_diameter_px"] = {"measured_core": round(d, 2), "expected": round(exp, 2)}
            if h >= 2000:
                check(checks, "star_diameter", abs(d - exp) <= max(2.0, 0.03 * exp) or d >= exp, round(d, 2), "%.1f +- 2 px (core >= 250; bloom widens it)" % exp)
    if shot == "KS12" and h >= 2000 and cam is not None:
        sm = q.star_mask(cam, 912.0, radius_deg=3.0)
        mx = lum8 > 200
        # local maxima > 200 grouped into blobs of 3-12 px outside the disc and its bloom core
        if sm is not None:
            mx &= ~sm
        nb, ntot, _ = q.blobs(mx, 3, 12)
        out["ks12_glints"] = {"blobs_3_12px": nb, "components": ntot}
        check(checks, "ks12_glint_blobs", nb >= 300, nb, ">= 300 blobs (luma > 200, 3-12 px) outside the disc")

    # regression diff against the previous run
    if prev_path and os.path.exists(prev_path):
        try:
            prev = q.load_rgb(prev_path)
            if prev.shape == rgb.shape:
                d = np.abs(rgb.astype(np.int16) - prev.astype(np.int16)).max(axis=-1)
                out["regression"] = {"mean_abs": round(float(d.mean()), 4), "p99_abs": float(np.percentile(d, 99)),
                                     "changed_gt8_pct": round(100.0 * float((d > 8).mean()), 4)}
            else:
                out["regression"] = {"note": "size changed %s -> %s" % (prev.shape[:2], rgb.shape[:2])}
        except Exception as e:  # noqa: BLE001
            out["regression"] = {"note": "previous image unreadable: %s" % e}

    out["checks"] = checks
    out["pass"] = all(c["pass"] for c in checks)
    out["seconds"] = round(time.time() - t0, 2)
    return out


def ks03_haze_contrast(run, files, all_files, idx):
    """14.5.2 (8) / 14.7 H2: in KS03 the luma standard deviation of the arch around 40 deg elevation must be >= 0.6 of
    the same render with hz forced to 0 (ks03_arch_up_nohaze_WxH.png, written by the capture harness next to the base).
    Band: pixels whose view ray lies at 36-44 deg elevation. Arch pixels: those the override moved by >= 3 levels in any
    channel (the override acts on the sky dome only; foreground geometry is identical in both renders). A variant whose
    mean absolute difference from its base is below 0.5 level fails (the override had no effect)."""
    out = []
    for f in files:
        info = q.parse_name(f)
        if info.get("kind") != "ks" or info.get("shot") != "KS03" or not info.get("tag", "").endswith("_nohaze"):
            continue
        base = f.replace("_nohaze_", "_")
        r = {"file": f, "base": base, "band_deg": list(HAZE_BAND_DEG), "min_ratio": HAZE_MIN_RATIO, "evaluable": False, "pass": None}
        out.append(r)
        if base not in all_files:
            r["note"] = "base key shot %s is not in this run" % base
            continue
        try:
            a = q.load_rgb(os.path.join(run, base))
            b = q.load_rgb(os.path.join(run, f))
        except Exception as e:  # noqa: BLE001
            r["note"] = "unreadable: %s" % e
            continue
        if a.shape != b.shape:
            r["note"] = "size mismatch %s vs %s" % (a.shape[:2], b.shape[:2])
            continue
        h, w = a.shape[:2]
        cam = q.camera_for(base, idx.get(base), w, h)
        if cam is None:
            r["note"] = "no camera for %s" % base
            continue
        diff = np.abs(a.astype(np.int16) - b.astype(np.int16))
        mean_abs = float(diff.mean())
        r["mean_abs_diff"] = round(mean_abs, 4)
        r["override_effective"] = mean_abs >= VARIANT_MIN_DIFF
        if not r["override_effective"]:
            r.update({"evaluable": True, "pass": False, "note": "the no-haze override had no effect (mean abs diff %.3f < %g)" % (mean_abs, VARIANT_MIN_DIFF)})
            continue
        el = q.pixel_elevation(cam)
        band = (el >= HAZE_BAND_DEG[0]) & (el <= HAZE_BAND_DEG[1])
        arch = band & (diff.max(axis=-1) >= HAZE_CHANGED_LEVELS)
        nb, na = int(band.sum()), int(arch.sum())
        r.update({"band_px": nb, "arch_px": na})
        if nb == 0 or na < HAZE_MIN_ARCH_SHARE * nb:
            r["note"] = "not evaluable: %d arch pixels in a band of %d" % (na, nb)
            continue
        sa = float(q.luma(a)[arch].std())
        sb = float(q.luma(b)[arch].std())
        r.update({"std_hazed": round(sa, 3), "std_unhazed": round(sb, 3)})
        if sb <= 1e-3:
            r["note"] = "not evaluable: the unhazed arch band is flat"
            continue
        ratio = sa / sb
        r.update({"ratio": round(ratio, 4), "evaluable": True, "pass": ratio >= HAZE_MIN_RATIO})
    return out


def orientation_check(run):
    p = os.path.join(run, "_orientation_selftest.png")
    if not os.path.exists(p):
        return None
    a = q.load_rgb(p)
    tl = a[0, 0].tolist()
    return {"top_left": tl, "pass": tl[0] > 200 and tl[1] < 60 and tl[2] < 60}


def write_md(path, run, res, summary, report):
    lines = ["# QA metrics - %s" % os.path.basename(run), ""]
    lines.append("Generated %s by Tools/qa_metrics.py. %d images, %d failing, %d checks failed." %
                 (time.strftime("%Y-%m-%d %H:%M:%S"), summary["images"], summary["failing_images"], summary["failed_checks"]))
    if report:
        lines.append("")
        lines.append("Player report: exit code %s, %s images, gpu %s, api %s, hdr %s, depth %s, msaa %s, hook %s." % (
            report.get("exit_code"), report.get("images"), report.get("gpu"), report.get("api"), report.get("hdr_format"),
            report.get("depth_format"), report.get("msaa"), report.get("before_render_hook")))
        par = report.get("parity")
        if par:
            lines.append("Window-vs-RT parity: best %s diff %s (limit 1.0 level) -> %s." % (par.get("best"), q.fmt(par.get("best_diff"), 3), "pass" if par.get("pass") else "FAIL"))
    if summary.get("orientation"):
        lines.append("Orientation self-test: top-left %s -> %s." % (summary["orientation"]["top_left"], "pass" if summary["orientation"]["pass"] else "FAIL"))
    if summary.get("composition_failures"):
        lines.append("Composition failures (16:9 / 16:10 / 21:9, 3 %% margin): %s." % ", ".join(summary["composition_failures"]))
    for hz in summary.get("ks03_haze_contrast") or []:
        if hz.get("evaluable"):
            lines.append("KS03 arch contrast vs hz = 0 (%s, 14.5.2 (8)): ratio %s (>= %g), diff from base %s -> %s." % (
                hz["file"], q.fmt(hz.get("ratio"), 3), HAZE_MIN_RATIO, q.fmt(hz.get("mean_abs_diff"), 3),
                "pass" if hz.get("pass") else "FAIL" + (" (%s)" % hz["note"] if hz.get("note") else "")))
        else:
            lines.append("KS03 arch contrast vs hz = 0 (%s): not evaluable - %s." % (hz["file"], hz.get("note", "")))
    unevaluable = [r["file"] for r in res if r.get("horizon") and not r["horizon"].get("evaluable")]
    if unevaluable:
        lines.append("Terrain-edge seam not evaluable (too few open-sky pairs): %s." % ", ".join(unevaluable))
    lines += ["", "| file | beat | mean | p99.9 | clip % | band px | iso | seam dE | reg mean | result |",
              "|---|---|---|---|---|---|---|---|---|---|"]
    for r in res:
        if "error" in r:
            lines.append("| %s | - | - | - | - | - | - | - | - | ERROR %s |" % (r["file"], r["error"]))
            continue
        fails = [c["name"] for c in r["checks"] if not c["pass"]]
        iso = r.get("isolated", {})
        lines.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
            r["file"], q.fmt(r.get("beat")), q.fmt(r["mean_luma"], 1), q.fmt(r["percentiles"]["p99_9"], 0),
            q.fmt(r["clipped_pct"], 3), q.fmt(r.get("band", {}).get("score"), 0),
            q.fmt((iso.get("bright", 0) + iso.get("dark", 0)) if iso else None),
            q.fmt(r.get("horizon", {}).get("max_de76"), 2), q.fmt(r.get("regression", {}).get("mean_abs"), 2),
            "ok" if not fails else "FAIL: " + ", ".join(fails)))
    lines.append("")
    lines.append("Failed checks in detail:")
    lines.append("")
    any_fail = False
    for r in res:
        for c in r.get("checks", []):
            if not c["pass"]:
                any_fail = True
                lines.append("- %s: %s = %s (expected %s)" % (r["file"], c["name"], c["value"], c["expected"]))
    if not any_fail:
        lines.append("- none")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run", nargs="?", default="latest")
    ap.add_argument("--prev", default=None, help="previous run for the regression diff (default: the newest older run)")
    ap.add_argument("--no-prev", action="store_true")
    ap.add_argument("--jobs", type=int, default=max(1, min(6, (os.cpu_count() or 2) - 1)))
    ap.add_argument("--no-isolated", action="store_true", help="skip the 5 x 5 median scan (faster)")
    ap.add_argument("--only", default=None, help="fnmatch pattern of file names to analyse")
    ap.add_argument("--no-fail", action="store_true")
    a = ap.parse_args()

    run = q.resolve_run(a.run)
    if not run:
        print("[qa_metrics] run folder not found: %s" % a.run)
        return 2
    report = q.load_report(run)
    idx = q.report_index(report)
    prev = None
    if not a.no_prev:
        prev = q.resolve_run(a.prev) if a.prev else q.previous_run(run)
    all_files = [f for f in q.list_pngs(run) if not f.startswith("_")]
    files = list(all_files)
    if a.only:
        files = [f for f in files if fnmatch.fnmatch(f, a.only)]
    if not files:
        print("[qa_metrics] no PNG in %s" % run)
        return 2
    opts = {"no_isolated": a.no_isolated}
    jobs = [(os.path.join(run, f), f, idx.get(f), os.path.join(prev, f) if prev else None, opts) for f in files]
    t0 = time.time()
    if a.jobs > 1 and len(jobs) > 1:
        with ProcessPoolExecutor(max_workers=a.jobs) as ex:
            res = list(ex.map(analyze, jobs, chunksize=1))
    else:
        res = [analyze(j) for j in jobs]

    failed_checks = sum(1 for r in res for c in r.get("checks", []) if not c["pass"])
    failing = sum(1 for r in res if not r.get("pass", False))
    comp_fail = []
    if report:
        for k, v in (report.get("composition_ok") or {}).items():
            if not v:
                comp_fail.append(k)
    # images listed in report.json that are missing on disk
    missing = [f for f in idx if "/" not in f and not os.path.exists(os.path.join(run, f))]
    haze = ks03_haze_contrast(run, files, all_files, idx)
    summary = {"run": os.path.basename(run), "images": len(res), "failing_images": failing, "failed_checks": failed_checks,
               "previous_run": os.path.basename(prev) if prev else None, "seconds": round(time.time() - t0, 1),
               "orientation": orientation_check(run), "composition_failures": comp_fail, "missing_files": missing,
               "player_exit_code": report.get("exit_code") if report else None, "ks03_haze_contrast": haze}
    if summary["orientation"] and not summary["orientation"]["pass"]:
        failed_checks += 1
    if missing:
        failed_checks += len(missing)
    failed_checks += sum(1 for r in haze if r.get("evaluable") and r.get("pass") is False)
    summary["failed_checks"] = failed_checks
    out = {"summary": summary, "images": res}
    q.write_json(os.path.join(run, "metrics.json"), out)
    write_md(os.path.join(run, "metrics.md"), run, res, summary, report)
    print("[qa_metrics] %s: %d images, %d failing, %d failed checks, %.1f s -> metrics.json / metrics.md" %
          (summary["run"], len(res), failing, failed_checks, summary["seconds"]))
    return 0 if (a.no_fail or failed_checks == 0) else 1


if __name__ == "__main__":
    sys.exit(main())
