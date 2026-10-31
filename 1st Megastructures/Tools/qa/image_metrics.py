"""Per-image QA metrics for an Abyss capture folder (Part 11.3, thresholds of Part 14.10, nadir of 14.7.2).

Usage (always isolated mode):
    python -I Tools/qa/image_metrics.py QA/captures/<run-id> [--out QA/metrics/<run-id>] [--advisory REGEX]
    python -I Tools/qa/image_metrics.py --single shot.png --beat 2 [--band name=x0,y0,x1,y1 ...]
           [--horizon-fog R --horizon-sky R --horizon-backdrop R] [--horizon-row R --nopost] [--nadir R]
           [--tag KS1b_doorway --doorway R]

Input folder: PNGs plus report.json written by the CaptureHarness:
    {"run": id, "shots": [{"tag", "beat", "file", "width", "height", "pose": {...}, "clock": t,
                           "regions": {optional}, "zone": optional, "nopost": optional, "thresholds": optional}]}
Optional per-shot "regions" (see abyss_qa.py: [x0, y0, x1, y1] fractions clamped to 0..1, {"px": [...]} pixels,
{"circle": [cx, cy, r]}, {"circle_px": [...]}, {"rects": [[...], ...]} a union of fractional rectangles):
"banding" (dict name->shape or list; replaces the beat defaults), "aliasing", "horizon_fog", "horizon_sky",
"horizon_backdrop", "horizon_row", "nadir", "doorway" (KS1b only). A region that is malformed or smaller than
8 x 8 px fails its check with an error. CLI regions: x0,y0,x1,y1 (fractions 0..1), px:x0,y0,x1,y1 or
rects:x0,y0,x1,y1/x0,y0,x1,y1.
Without report.json the beat is inferred from file names (beatNN_<name>_<tag>.png) and no size check is possible.
Shots are identified by file, not tag (one tag exists at several resolutions); duplicate file references and
duplicate (tag, size) pairs are listed in metrics.json and summary.md.

Per image: size vs report, black / white frame, magenta pixel ratio, NaN-like isolated extreme pixels, clipped %
(any channel >= 0.98), Rec. 709 luma mean / p1 / p50 / p99, BandScore in the beat's gradient regions (12 px at 1080p,
24 px at 4K), horizon-colour agreement (when regions are given), the nadir requirement (when a nadir region is given),
the 14.5.1 doorway check for KS1b only (aperture mean luma 0.40-0.60, the calibrated band of lead decision D6 / DEVELOPMENT.md
(spec text 0.65-0.90), under 0.3 % clipped; KS1 cannot see the doorway),
an aliasing proxy (informational), and the beat row of the 14.10 threshold table with pass / fail (the mean-luma band is the
union of both beats' rows inside a 14.6 transition window, by the shot's E; R1-38). BandScore: confirmed bands only (R1-38;
the plain longest run is reported as raw_max_run). 14.5 acceptance checks (R1-29): near-black floor (every image),
KS2b side strips, KS1 far glow, KS5a windows, KS7b amber dots and readable cells; advisory: stipple, KS6 spoke contrast.
Fix round R2: 1-px pure-black runs (every image, R2-16), KS7b amber dot-core saturation (R2-21), KS3 pour lifts on the
projected iso-y curves of C1 (judged, R2-02), KS1 wall board steps and KS2a wall macro residual (R2-02); advisory: amber
coverage by emitter area (R2-24).

Output: <out>/metrics.json and <out>/summary.md. Exit code 0 = every image passes, 1 = at least one failure,
2 = bad input.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from typing import Any, Dict, List, Optional  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402


def _check(checks: Dict[str, Any], name: str, value: Any, limit: Any, ok: bool, advisory: bool = False) -> None:
    checks[name] = {"value": value, "limit": limit, "pass": bool(ok), "advisory": bool(advisory)}


def analyse_image(rgb8, shot: Dict[str, Any], expected_wh=None, band_axis: str = "v", band_channels: str = "rgb",
                  advisory_luma: bool = False, report_nopost: bool = False) -> Dict[str, Any]:
    """All metrics of one image. ``shot`` carries tag / beat / zone / regions / thresholds / nopost."""
    h, w = rgb8.shape[:2]
    tag = str(shot.get("tag", ""))
    beat = shot.get("beat")
    try:
        beat = int(beat) if beat is not None else None
    except (TypeError, ValueError):
        beat = None
    if beat is None:
        beat = qa.infer_beat(tag, str(shot.get("file", "")))
    tkey = qa.threshold_key(beat, shot)
    zone = tkey[1] if tkey else None
    over = shot.get("thresholds") or {}
    adv_list = [str(a) for a in (shot.get("advisory") or [])]

    def is_adv(name: str) -> bool:
        return any(name.startswith(a) for a in adv_list)

    out: Dict[str, Any] = {"tag": tag, "beat": beat, "zone": zone, "file": shot.get("file"), "width": w, "height": h}
    checks: Dict[str, Any] = {}

    # --- size
    if expected_wh and expected_wh[0] and expected_wh[1]:
        ew, eh = int(expected_wh[0]), int(expected_wh[1])
        _check(checks, "size", [w, h], [ew, eh], (w, h) == (ew, eh), is_adv("size"))

    # --- luma and global checks
    Y = qa.luma(rgb8)
    ls = qa.luma_stats(Y)
    out["luma"] = ls
    blk, wht = qa.black_white_frame(Y)
    _check(checks, "black_frame", blk, False, not blk, is_adv("black_frame"))
    _check(checks, "white_frame", wht, False, not wht, is_adv("white_frame"))
    mag = qa.magenta_mask(rgb8)
    mag_n = int(mag.sum())
    out["magenta"] = {"count": mag_n, "ratio": mag_n / float(w * h)}
    _check(checks, "magenta_ratio", mag_n / float(w * h), 0.0, mag_n == 0, is_adv("magenta_ratio"))
    sp = qa.nan_like_specks(rgb8, Y)
    out["specks"] = sp
    _check(checks, "nan_like_pixels", sp["count"], 0, sp["count"] == 0, is_adv("nan_like_pixels"))
    clip = qa.clipped_pct(rgb8)
    out["clipped_pct"] = clip

    # --- beat thresholds (14.10 table); the mean-luma band follows the content in view: inside a 14.6 transition window
    #     (by the shot's E) it is the union of both beats' bands (critique R1-38)
    band_limit = float(over.get("band_limit_px", qa.band_limit_px(h)))
    if tkey is not None:
        th = qa.BEAT_THRESHOLDS[tkey]
        rows = qa.luma_rows_for(shot, tkey)
        mmin = float(over.get("mean_min", min(qa.BEAT_THRESHOLDS[k]["mean"][0] for k in rows)))
        mmax = float(over.get("mean_max", max(qa.BEAT_THRESHOLDS[k]["mean"][1] for k in rows)))
        # lead decision D8 (R2): a shot judged by the content in view (KS5b: beat 6) takes that row's p99 limit as well
        crow = qa.content_luma_row(shot)
        p99m = float(over.get("p99_max", qa.BEAT_THRESHOLDS[crow]["p99_max"] if crow in qa.BEAT_THRESHOLDS else th["p99_max"]))
        clm = float(over.get("clipped_max_pct", th["clipped_max_pct"]))
        out["thresholds"] = {"row": "beat %d%s" % (tkey[0], (" " + tkey[1]) if tkey[1] else ""), "mean": [mmin, mmax],
                             "mean_rows": ["beat %d%s" % (k[0], (" " + k[1]) if k[1] else "") for k in rows],
                             "p99_max": p99m, "clipped_max_pct": clm, "band_limit_px": band_limit,
                             "band_desc": th["band_desc"], "alias_desc": th["alias_desc"]}
        if crow is not None:
            out["thresholds"]["content_row"] = "beat %d (lead decision D8: judged by the content in view; mean and p99)" % crow[0]
        _check(checks, "mean_luma", ls["mean"], [mmin, mmax], mmin <= ls["mean"] <= mmax,
               advisory_luma or is_adv("mean_luma"))
        _check(checks, "p99_luma", ls["p99"], p99m, ls["p99"] <= p99m, advisory_luma or is_adv("p99_luma"))
        _check(checks, "clipped_pct", clip, clm, clip <= clm, advisory_luma or is_adv("clipped_pct"))
    else:
        out["thresholds"] = None

    # --- regions
    regions = shot.get("regions") or {}
    band_regions = qa.normalise_regions(regions.get("banding"), "band") if "banding" in regions else \
        qa.default_band_regions(tkey, shot)
    region_warnings: List[str] = []

    def note(name: str, shape: Any) -> None:
        wmsg = qa.region_warning(shape)
        if wmsg:
            region_warnings.append("%s: %s" % (name, wmsg))

    bres = []
    for name, shape in band_regions.items():
        note("banding:" + name, shape)
        box, m, err = qa.resolve_region(shape, w, h)
        if err:
            r = {"name": name, "region": shape, "box_px": list(box), "max_run": 0, "limit_px": band_limit,
                 "error": err, "pass": False}
        else:
            r = qa.banding_score(rgb8, box, m, axis=band_axis, channels=band_channels, limit_px=band_limit)
            r["name"] = name
            r["region"] = shape
            r["limit_px"] = band_limit
            r["pass"] = bool(r["max_run"] <= band_limit) and not r.get("error")
        bres.append(r)
        # a region error is bad input, not a measurement: it fails even where banding is advisory
        _check(checks, "banding:" + name, None if r.get("error") else r["max_run"], band_limit, r["pass"],
               is_adv("banding") and not r.get("error"))
        if r.get("error"):
            checks["banding:" + name]["error"] = r["error"]
    out["banding"] = bres

    def judged(name: str, value: Any, limit: Any, res: Dict[str, Any], adv_prefix: str) -> None:
        err = res.get("error")
        _check(checks, name, value, limit, res["pass"], is_adv(adv_prefix) and not err)
        if err:
            checks[name]["error"] = err

    for key in ("horizon_fog", "horizon_sky", "horizon_backdrop", "horizon_row", "nadir", "doorway"):
        if key in regions:
            note(key, regions[key])

    # --- 14.5.1 doorway (KS1b only: at KS1 the sloped stair ceiling hides the doorway, so a "doorway" region on any
    #     other shot is reported as not applicable and never judged)
    if qa.is_doorway_shot(shot):
        shape = regions["doorway"] if "doorway" in regions else qa.default_doorway_region(w, h)
        dw = qa.doorway_check(rgb8, shape)
        dw["applies"] = True
        dw["region_source"] = "report" if "doorway" in regions else "default (KS1b pose)"
        out["doorway"] = dw
        err = {"error": dw.get("error")} if dw.get("error") else {}
        judged("doorway_luma", dw.get("mean_luma"), list(qa.DOORWAY_LUMA), dict(err, **{"pass": dw["luma_pass"]}),
               "doorway")
        judged("doorway_clipped_pct", dw.get("clipped_pct"), "<%.1f" % qa.DOORWAY_CLIPPED_MAX_PCT,
               dict(err, **{"pass": dw["clipped_pass"]}), "doorway")
    elif "doorway" in regions:
        out["doorway"] = {"applies": False, "region": regions["doorway"],
                          "note": "the doorway check applies to %s only" % qa.DOORWAY_SHOT}
    hz = qa.horizon_agreement(rgb8, regions)
    out["horizon"] = hz
    if hz is not None:
        judged("horizon_agreement", hz["max_diff_levels"], hz["limit_levels"], hz, "horizon_agreement")
    nopost = bool(shot.get("nopost")) or report_nopost or ("nopost" in tag.lower())
    if "horizon_row" in regions:
        hr = qa.horizon_vs_reference(rgb8, regions["horizon_row"])
        hr["applies"] = bool(nopost and beat == 7)
        out["horizon_row"] = hr
        if hr["applies"]:
            judged("horizon_row_vs_OUT_HORIZ", hr["max_diff_levels"], hr["limit_levels"], hr, "horizon_row")
    if "nadir" in regions:
        nd = qa.nadir_check(rgb8, regions["nadir"])
        out["nadir"] = nd
        judged("nadir", nd.get("mean_luma"), "%.3f+/-%.3f, max<=%.2f, edge<=%.2f" % (
            qa.NADIR_LUMA, qa.NADIR_TOL, qa.NADIR_MAX_PIXEL, qa.NADIR_MAX_EDGE), nd, "nadir")
    # --- 14.5 image acceptance checks (critique R1-29; regions / data written by the harness for the key shots)
    acc: Dict[str, Any] = {}
    nb = qa.near_black_check(rgb8)
    acc["near_black"] = nb
    judged("near_black_pct", nb["pct"], nb["limit_pct"], nb, "near_black")
    # R2-16 / R2-36 (g): 1-px pure-black runs (no 2x2 opening)
    br = qa.black_run_check(rgb8)
    acc["black_runs"] = br
    judged("black_runs", br.get("runs"), "no run >= %d px at max channel <= %d" % (qa.BLACK_RUN_MIN_PX, qa.BLACK_RUN_LEVEL),
           br, "black_runs")
    # R2-24: amber coverage by emitter area (advisory until the lamp-head conflict is arbitrated)
    ac = qa.amber_coverage_check(rgb8)
    acc["amber_coverage"] = ac
    _check(checks, "amber_coverage_pct", ac["pct"], "<=%.1f (advisory)" % qa.AMBER_COVER_MAX_PCT, ac["pass"], True)
    if "side_strips" in regions:
        r = qa.side_strips_check(rgb8, regions["side_strips"])
        acc["side_strips"] = r
        judged("side_strips", r.get("mean_luma_max"), "mean<=%.2f, max<=%.2f, edge<=%.2f" % (
            qa.SIDE_STRIP_MEAN_MAX, qa.SIDE_STRIP_PIXEL_MAX, qa.SIDE_STRIP_EDGE_MAX), r, "side_strips")
    if "far_glow" in regions:
        r = qa.far_glow_check(rgb8, regions["far_glow"])
        acc["far_glow"] = r
        judged("far_glow_p90", r.get("p90_luma"), ">=%.2f" % qa.FAR_GLOW_P90_MIN, r, "far_glow")
    if "window_grid" in regions:
        r = qa.window_grid_check(rgb8, regions["window_grid"])
        acc["window_grid"] = r
        judged("windows", r.get("windows"), ">=%d, cell %g-%g px" % ((qa.WINDOWS_MIN,) + qa.WINDOW_CELL_PX), r, "windows")
    if "amber_dots" in regions:
        r = qa.amber_dots_check(rgb8, regions["amber_dots"])
        acc["amber_dots"] = r
        judged("amber_dots", r.get("dots"), ">=%d" % qa.AMBER_DOTS_MIN, r, "amber_dots")
        # R2-21: the dot cores keep their amber hue (not cream-white)
        r = qa.amber_saturation_check(rgb8, regions["amber_dots"])
        acc["amber_saturation"] = r
        judged("amber_saturation", r.get("core_saturation"), "S>=%.2f, hue %g-%g" % (
            (qa.AMBER_SAT_MIN,) + qa.AMBER_HUE_RANGE), r, "amber_saturation")
    if "wall_board" in regions:
        r = qa.wall_board_check(rgb8, regions["wall_board"])
        acc["wall_board"] = r
        judged("wall_board_steps", r.get("p95_levels"), ">=%.1f levels" % qa.WALL_BOARD_MIN_LEVELS, r, "wall_board")
    if "wall_macro" in regions:
        r = qa.wall_macro_check(rgb8, regions["wall_macro"])
        acc["wall_macro"] = r
        judged("wall_macro_residual", r.get("std_levels"), ">=%.1f levels" % qa.WALL_MACRO_MIN_LEVELS, r, "wall_macro")
    if "cells" in regions:
        r = qa.readable_cells_check(rgb8, regions["cells"])
        acc["readable_cells"] = r
        judged("readable_cells", r.get("readable"), ">=%d" % qa.READABLE_CELLS_MIN, r, "readable_cells")
    # advisory measurements (calibration pending, they only warn): stipple, KS3 pour lifts, KS6 spoke contrast
    st = qa.stipple_check(rgb8)
    acc["stipple"] = st
    _check(checks, "stipple_tiles", st["tiles"], 0, st["pass"], True)
    if "lift_curves" in regions:
        # R2-02 / R2-36 (c): judged on the projected iso-y curves of C1 (the row-mean count below stays as information)
        r = qa.lift_curves_check(rgb8, regions["lift_curves"])
        acc["lift_curves"] = r
        judged("pour_lifts", r.get("lifts"), ">=%d lifts of >=%g levels" % (qa.LIFT_MIN_COUNT, qa.LIFT_MIN_LEVELS), r,
               "pour_lifts")
    if "column_face" in regions:
        r = qa.lift_count_check(rgb8, regions["column_face"])
        acc["lift_count"] = r
        _check(checks, "pour_lifts_rows" if "lift_curves" in regions else "pour_lifts", r.get("lifts"),
               ">=3 (advisory, row means)", r["pass"], True)
    if "fan_ring" in regions:
        r = qa.spoke_contrast_check(rgb8, regions["fan_ring"])
        acc["spoke_contrast"] = r
        _check(checks, "spoke_contrast", r.get("contrast"), ">=0.04 (advisory)", r["pass"], True)
    out["acceptance"] = acc

    if "aliasing" in regions:
        alias = qa.normalise_regions(regions["aliasing"], "alias")
        for name, shape in alias.items():
            note("aliasing:" + name, shape)
        out["aliasing_proxy"] = {name: qa.aliasing_proxy(rgb8, shape) for name, shape in alias.items()}
        for name, ap in out["aliasing_proxy"].items():
            if ap.get("error"):   # informational metric: reported, not judged
                region_warnings.append("aliasing:%s: %s" % (name, ap["error"]))
    out["region_warnings"] = region_warnings

    out["checks"] = checks
    out["failures"] = [k for k, c in checks.items() if not c["pass"] and not c["advisory"]]
    out["warnings"] = [k for k, c in checks.items() if not c["pass"] and c["advisory"]]
    out["pass"] = not out["failures"]
    return out


def summary_markdown(result: Dict[str, Any]) -> str:
    rows = []
    for s in result["shots"]:
        if s.get("error"):
            rows.append([s.get("tag"), s.get("beat"), "-", "-", "-", "-", "-", "-", "-", "ERROR: " + s["error"]])
            continue
        ls = s["luma"]
        band = max((b["max_run"] for b in s["banding"]), default=0)
        blim = s["banding"][0]["limit_px"] if s["banding"] else None
        th = s.get("thresholds") or {}
        mean_rng = th.get("mean")
        rows.append([
            s["tag"], "%s%s" % (s["beat"], (" " + s["zone"]) if s.get("zone") else ""),
            "%dx%d" % (s["width"], s["height"]),
            "%.3f%s" % (ls["mean"], (" [%.2f-%.2f]" % tuple(mean_rng)) if mean_rng else ""),
            "%.3f%s" % (ls["p99"], (" [<=%.2f]" % th["p99_max"]) if th else ""),
            "%.2f%s" % (s["clipped_pct"], (" [<=%.1f]" % th["clipped_max_pct"]) if th else ""),
            ("%d / %.1f" % (band, blim)) if blim else "-",
            s["magenta"]["count"], s["specks"]["count"],
            "PASS" if s["pass"] else "FAIL: " + ", ".join(s["failures"]) +
            ((" (warn: " + ", ".join(s["warnings"]) + ")") if s.get("warnings") else ""),
        ])
    sm = result["summary"]
    head = ["# Image metrics - run %s" % result["run"], "",
            "Source: `%s`  " % result["source"], "Generated: %s by image_metrics.py %s  " % (
                result["generated"], qa.TOOL_VERSION),
            "Shots: %d, pass: %d, fail: %d, errors: %d" % (sm["shots"], sm["pass"], sm["fail"], sm["errors"]), "",
            "Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run "
            "(px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).", ""]
    table = qa.md_table(["Tag", "Beat", "Size", "Mean luma", "p99", "Clipped %", "Band px", "Magenta", "Specks",
                         "Result"], rows)
    tail = ["", "## Failures", ""]
    if sm["failures"]:
        for f in sm["failures"]:
            tail.append("- %s" % f)
    else:
        tail.append("None.")
    errs = [(s, n, c["error"]) for s in result["shots"] for n, c in (s.get("checks") or {}).items() if c.get("error")]
    if errs:
        tail += ["", "## Region errors", ""] + ["- %s %s: %s" % (qa.shot_label(s), n, e) for s, n, e in errs]
    warns = [(s, wmsg) for s in result["shots"] for wmsg in (s.get("region_warnings") or [])]
    if warns:
        tail += ["", "## Region warnings", ""] + ["- %s %s" % (qa.shot_label(s), wmsg) for s, wmsg in warns]
    dup = result.get("duplicates") or {}
    if dup.get("files") or dup.get("tag_sizes"):
        tail += ["", "## Duplicates in report.json", ""]
        for d in dup.get("files", []):
            tail.append("- file `%s` is referenced by %d shots: %s" % (d["file"], len(d["shots"]), ", ".join(
                "%s %sx%s" % (x["tag"], x["width"], x["height"]) for x in d["shots"])))
        for d in dup.get("tag_sizes", []):
            tail.append("- tag %s at %s appears in %d files: %s" % (d["tag"], d["size"], len(d["files"]),
                                                                    ", ".join(d["files"])))
    return "\n".join(head) + table + "\n" + "\n".join(tail) + "\n"


def run_folder(args: argparse.Namespace) -> int:
    folder = os.path.abspath(args.folder)
    if not os.path.isdir(folder):
        qa.eprint("not a folder: %s" % folder)
        return 2
    rep = qa.load_report(folder)
    run_id = str(rep.get("run") or os.path.basename(folder))
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", run_id)
    out_dir = os.path.abspath(args.out) if args.out else os.path.join(qa.QA_DIR, "metrics", safe)
    adv_re = re.compile(args.advisory) if args.advisory else None
    report_nopost = bool(rep.get("nopost"))
    shots_out: List[Dict[str, Any]] = []
    listed = set()
    for shot in rep["shots"]:
        fn = shot.get("file") or ""
        path = fn if os.path.isabs(fn) else os.path.join(folder, fn)
        listed.add(os.path.normcase(os.path.abspath(path)))
        if not os.path.isfile(path):
            shots_out.append({"tag": shot.get("tag"), "beat": shot.get("beat"), "file": fn, "pass": False,
                              "error": "file missing", "failures": ["file_missing"]})
            continue
        try:
            rgb8 = qa.load_rgb8(path)
        except Exception as e:  # unreadable / truncated PNG is itself a defect
            shots_out.append({"tag": shot.get("tag"), "beat": shot.get("beat"), "file": fn, "pass": False,
                              "error": "unreadable: %s" % e, "failures": ["unreadable"]})
            continue
        adv = bool(adv_re and adv_re.search(str(shot.get("tag", ""))))
        r = analyse_image(rgb8, shot, (shot.get("width"), shot.get("height")), args.band_axis, args.band_channels,
                          adv, report_nopost)
        for k in ("pose", "clock", "kind"):
            if k in shot:
                r[k] = shot[k]
        shots_out.append(r)
        if not args.quiet:
            print("%-60s %s" % (qa.shot_label(r)[:60], "PASS" if r["pass"] else "FAIL " + ",".join(r["failures"])))
    unlisted = [os.path.basename(p) for p in qa.list_pngs(folder)
                if os.path.normcase(os.path.abspath(p)) not in listed] if rep.get("_has_report") else []
    failures = []
    for s in shots_out:
        if not s.get("pass"):
            failures.append("%s: %s" % (qa.shot_label(s), ", ".join(s.get("failures") or [])))
    dups = qa.report_duplicates(rep["shots"], folder) if rep.get("_has_report") else {"files": [], "tag_sizes": []}
    for d in dups["files"]:
        qa.eprint("warning: file %s is referenced by %d shots in report.json" % (d["file"], len(d["shots"])))
    for d in dups["tag_sizes"]:
        qa.eprint("warning: tag %s at %s appears in %d files" % (d["tag"], d["size"], len(d["files"])))
    result = {
        "run": run_id, "source": folder, "generated": qa.now_iso(), "tool": "image_metrics.py",
        "tool_version": qa.TOOL_VERSION, "has_report": bool(rep.get("_has_report")),
        "unlisted_pngs": unlisted, "duplicates": dups, "shots": shots_out,
        "summary": {"shots": len(shots_out), "pass": sum(1 for s in shots_out if s.get("pass")),
                    "fail": sum(1 for s in shots_out if not s.get("pass")),
                    "errors": sum(1 for s in shots_out if s.get("error")), "failures": failures},
    }
    qa.write_json(os.path.join(out_dir, "metrics.json"), result)
    qa.write_text(os.path.join(out_dir, "summary.md"), summary_markdown(result))
    print("image_metrics: %d shots, %d pass, %d fail -> %s" % (
        result["summary"]["shots"], result["summary"]["pass"], result["summary"]["fail"], out_dir))
    if not shots_out:
        return 2
    return 0 if (result["summary"]["fail"] == 0 or args.no_fail_exit) else 1


def run_single(args: argparse.Namespace) -> int:
    import json
    path = os.path.abspath(args.single)
    rgb8 = qa.load_rgb8(path)
    regions: Dict[str, Any] = {}
    try:
        if args.band:
            regions["banding"] = {}
            for i, t in enumerate(args.band):
                n, r = qa.parse_rect_arg(t)
                regions["banding"][n or "band%d" % i] = r
        for key in ("horizon_fog", "horizon_sky", "horizon_backdrop", "horizon_row", "nadir", "doorway", "amber_dots"):
            v = getattr(args, key)
            if v:
                regions[key] = qa.parse_rect_arg(v)[1]
        if args.aliasing:
            regions["aliasing"] = {}
            for i, t in enumerate(args.aliasing):
                n, r = qa.parse_rect_arg(t)
                regions["aliasing"][n or "alias%d" % i] = r
    except ValueError as e:
        qa.eprint("bad region argument: %s" % e)
        return 2
    shot = {"tag": args.tag or os.path.splitext(os.path.basename(path))[0], "beat": args.beat, "file": path,
            "regions": regions, "nopost": args.nopost}
    if args.zone:
        shot["zone"] = args.zone
    exp = (args.width, args.height) if args.width and args.height else None
    r = analyse_image(rgb8, shot, exp, args.band_axis, args.band_channels)
    txt = json.dumps(r, indent=2, default=qa._json_default)
    if args.out:
        qa.write_text(os.path.abspath(args.out), txt + "\n")
    print(txt)
    return 0 if r["pass"] or args.no_fail_exit else 1


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder", nargs="?", help="capture folder with PNGs and report.json")
    ap.add_argument("--out", help="output folder (folder mode, default QA/metrics/<run>) or JSON file (--single)")
    ap.add_argument("--band-axis", choices=["v", "h", "both"], default="v", help="banding line direction (spec: v)")
    ap.add_argument("--band-channels", choices=["rgb", "luma"], default="rgb",
                    help="banding planes: max over R,G,B (default) or 8-bit luma")
    ap.add_argument("--advisory", help="regex of tags whose luma / p99 / clipped checks only warn (e.g. free-look)")
    ap.add_argument("--no-fail-exit", action="store_true", help="exit 0 even when checks fail")
    ap.add_argument("--quiet", action="store_true")
    g = ap.add_argument_group("single image mode")
    g.add_argument("--single", help="analyse one PNG and print JSON")
    g.add_argument("--beat", type=int)
    g.add_argument("--zone", choices=["tunnel", "outside"])
    g.add_argument("--tag")
    g.add_argument("--width", type=int)
    g.add_argument("--height", type=int)
    g.add_argument("--band", action="append",
                   help="name=x0,y0,x1,y1 (fractions 0..1) or name=px:x0,y0,x1,y1 banding region (repeatable)")
    g.add_argument("--aliasing", action="append", help="name=x0,y0,x1,y1 or name=px:... thin-line region (repeatable)")
    g.add_argument("--horizon-fog", dest="horizon_fog")
    g.add_argument("--horizon-sky", dest="horizon_sky")
    g.add_argument("--horizon-backdrop", dest="horizon_backdrop")
    g.add_argument("--horizon-row", dest="horizon_row")
    g.add_argument("--nadir", help="x0,y0,x1,y1, px:..., circle:cx,cy,r or circle_px:cx,cy,r")
    g.add_argument("--doorway", help="doorway aperture region (judged only with a KS1b --tag; default: the KS1b pose)")
    g.add_argument("--amber-dots", dest="amber_dots", help="amber-dot region (KS7b lower 66 %%: 0,0.34,1,1): amber_dots and the "
                   "R2-21 dot-core saturation, e.g. on cue_ignition frames")
    g.add_argument("--nopost", action="store_true", help="image was captured with -nopost (horizon row test)")
    args = ap.parse_args(argv)
    if args.single:
        return run_single(args)
    if not args.folder:
        ap.print_usage()
        return 2
    return run_folder(args)


if __name__ == "__main__":
    sys.exit(main())
