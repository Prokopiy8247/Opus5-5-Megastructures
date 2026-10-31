"""Regression diff between two capture runs (Part 11.3 "regression diff against the previous run").

Usage:
    python -I Tools/qa/compare.py QA/captures/<old-run> QA/captures/<new-run> [--out QA/compare/<old>_vs_<new>]
           [--threshold 2] [--gain 8] [--fail-mean 0] [--resize]

Shots are matched by FILE (path relative to the run folder), not by tag: every key shot is captured at several
resolutions under the same tag (11.2), so a tag alone would let one resolution overwrite the others. Files come from
report.json (or, without a report, from the PNGs in the folder). Per matched file: mean absolute difference (8-bit
levels, averaged over R, G, B), p99 and max of the per-pixel difference (max over channels), percentage of pixels
that changed by more than --threshold levels, the mean-luma change, and a heatmap PNG (per-pixel difference x --gain
mapped to a black-blue-magenta-yellow-white ramp, with the new image dimmed underneath). Files present in only one
run, size mismatches and report duplicates (one file referenced by several shots, one tag + size in several files)
are listed. With --fail-mean M the exit code is 1 when any file's mean abs difference exceeds M levels (default
0 = report only).

Outputs: <out>/compare.json, <out>/summary.md, <out>/heat_<file stem>.png.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from typing import Any, Dict, List, Optional, Tuple  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402


def index_run(folder: str) -> Tuple[Dict[str, Dict[str, Any]], Dict[str, List[Dict[str, Any]]]]:
    """(match key -> {key, tag, path}, duplicates). The key is the file path relative to the run folder."""
    rep = qa.load_report(folder)
    out: Dict[str, Dict[str, Any]] = {}
    for s in rep["shots"]:
        f = s.get("file") or ""
        if not f:
            continue
        p = f if os.path.isabs(f) else os.path.join(folder, f)
        if not os.path.isfile(p):
            continue
        key = qa.shot_key(p, folder)
        mk = qa.match_key(key)
        if mk in out:          # second reference to the same file: listed under duplicates
            continue
        out[mk] = {"key": key, "tag": str(s.get("tag") or os.path.splitext(os.path.basename(p))[0]), "path": p}
    if not out:
        for p in qa.list_pngs(folder):
            key = qa.shot_key(p, folder)
            out[qa.match_key(key)] = {"key": key, "tag": os.path.splitext(os.path.basename(p))[0], "path": p}
    dups = qa.report_duplicates(rep["shots"], folder) if rep.get("_has_report") else {"files": [], "tag_sizes": []}
    return out, dups


def heatmap(a: np.ndarray, b: np.ndarray, d: np.ndarray, gain: float) -> Image.Image:
    """Heatmap of the per-pixel difference (max over channels) on top of the dimmed new image, plus A | B strip."""
    t = d.astype(np.float32) * gain / 255.0
    heat = qa.heat_colour(t)
    base = (b.astype(np.float32) * 0.25).astype(np.uint8)
    over = np.where((d > 0)[..., None], heat, base)
    h, w = d.shape
    tw = max(1, w // 4)
    th = max(1, h // 4)
    thumbs = Image.new("RGB", (w, th), (14, 15, 18))
    thumbs.paste(Image.fromarray(a).resize((tw, th), Image.Resampling.LANCZOS), (0, 0))
    thumbs.paste(Image.fromarray(b).resize((tw, th), Image.Resampling.LANCZOS), (tw + 8, 0))
    out = Image.new("RGB", (w, h + th + 8), (14, 15, 18))
    out.paste(Image.fromarray(over), (0, 0))
    out.paste(thumbs, (0, h + 8))
    return out


def _dup_lines(label: str, dups: Dict[str, List[Dict[str, Any]]]) -> List[str]:
    lines = []
    for d in dups.get("files", []):
        lines.append("- %s: file `%s` is referenced by %d shots (%s)" % (label, d["file"], len(d["shots"]), ", ".join(
            "%s %sx%s" % (x["tag"], x["width"], x["height"]) for x in d["shots"])))
    for d in dups.get("tag_sizes", []):
        lines.append("- %s: tag %s at %s appears in %d files (%s)" % (label, d["tag"], d["size"], len(d["files"]),
                                                                     ", ".join(d["files"])))
    return lines


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run_a", help="older / reference run folder")
    ap.add_argument("run_b", help="newer run folder")
    ap.add_argument("--out")
    ap.add_argument("--threshold", type=float, default=2.0, help="levels for the 'changed pixels' percentage")
    ap.add_argument("--gain", type=float, default=8.0, help="heatmap gain")
    ap.add_argument("--fail-mean", type=float, default=0.0, help="exit 1 when a mean abs diff exceeds this (levels)")
    ap.add_argument("--resize", action="store_true", help="resize B to A when sizes differ (else the file is skipped)")
    ap.add_argument("--no-heatmaps", action="store_true")
    args = ap.parse_args(argv)
    A, B = os.path.abspath(args.run_a), os.path.abspath(args.run_b)
    for f in (A, B):
        if not os.path.isdir(f):
            qa.eprint("not a folder: %s" % f)
            return 2
    (ia, dup_a), (ib, dup_b) = index_run(A), index_run(B)
    for label, dups in (("A", dup_a), ("B", dup_b)):
        for line in _dup_lines(label, dups):
            qa.eprint("warning " + line[2:])
    name = re.sub(r"[^A-Za-z0-9_.-]+", "_", "%s_vs_%s" % (os.path.basename(A), os.path.basename(B)))
    out_dir = qa.ensure_dir(os.path.abspath(args.out) if args.out else os.path.join(qa.QA_DIR, "compare", name))
    keys = [k for k in ia if k in ib]
    keys.sort(key=lambda k: qa.natural_key(ia[k]["key"]))
    res: List[Dict[str, Any]] = []
    heat_names: Dict[str, int] = {}
    for k in keys:
        ea, eb = ia[k], ib[k]
        a = qa.load_rgb8(ea["path"])
        b = qa.load_rgb8(eb["path"])
        entry: Dict[str, Any] = {"key": ea["key"], "tag": ea["tag"], "a": ea["path"], "b": eb["path"],
                                 "size_a": [a.shape[1], a.shape[0]], "size_b": [b.shape[1], b.shape[0]]}
        if eb["tag"] != ea["tag"]:
            entry["tag_b"] = eb["tag"]
        if a.shape != b.shape:
            if not args.resize:
                entry["error"] = "size mismatch"
                res.append(entry)
                continue
            b = np.asarray(Image.fromarray(b).resize((a.shape[1], a.shape[0]), Image.Resampling.LANCZOS))
            entry["resized"] = True
        d3 = np.abs(a.astype(np.int16) - b.astype(np.int16))
        d = d3.max(axis=2)
        ya, yb = float(qa.luma(a).mean()), float(qa.luma(b).mean())
        entry.update({
            "mean_abs_levels": float(d3.mean()), "p99_levels": float(np.percentile(d, 99)), "max_levels": int(d.max()),
            "changed_pct": float((d > args.threshold).mean() * 100.0), "mean_luma_a": ya, "mean_luma_b": yb,
            "mean_luma_delta": yb - ya,
        })
        if not args.no_heatmaps:
            stem = re.sub(r"[^A-Za-z0-9_.-]+", "_", os.path.splitext(ea["key"])[0])
            n = heat_names.get(stem.lower(), 0)      # subfolders can sanitise to the same stem
            heat_names[stem.lower()] = n + 1
            hp = os.path.join(out_dir, "heat_%s%s.png" % (stem, "" if n == 0 else "_%d" % (n + 1)))
            heatmap(a, b, d, args.gain).save(hp)
            entry["heatmap"] = hp
        res.append(entry)
        print("%-60s mean %.3f  max %3d  changed %.2f%%" % (ea["key"][-60:], entry["mean_abs_levels"],
                                                            entry["max_levels"], entry["changed_pct"]))
    only_a = sorted((ia[k]["key"] for k in ia if k not in ib), key=qa.natural_key)
    only_b = sorted((ib[k]["key"] for k in ib if k not in ia), key=qa.natural_key)
    fails = [r["key"] for r in res if args.fail_mean > 0 and r.get("mean_abs_levels", 0.0) > args.fail_mean]
    result = {"run_a": A, "run_b": B, "generated": qa.now_iso(), "tool": "compare.py", "tool_version": qa.TOOL_VERSION,
              "matched_by": "file path relative to the run folder", "threshold_levels": args.threshold,
              "matched": len(res), "only_in_a": only_a, "only_in_b": only_b,
              "duplicates_a": dup_a, "duplicates_b": dup_b, "shots": res,
              "fail_mean_levels": args.fail_mean, "failures": fails}
    qa.write_json(os.path.join(out_dir, "compare.json"), result)
    rows = [[r["key"], r["tag"], "%dx%d" % tuple(r["size_a"]),
             "%.3f" % r["mean_abs_levels"] if "mean_abs_levels" in r else "-",
             "%.0f" % r["p99_levels"] if "p99_levels" in r else "-", r.get("max_levels", "-"),
             "%.2f" % r["changed_pct"] if "changed_pct" in r else "-",
             "%+.4f" % r["mean_luma_delta"] if "mean_luma_delta" in r else "-",
             r.get("error", "") + ((" tag in B: " + r["tag_b"]) if r.get("tag_b") else "")] for r in res]
    rows.sort(key=lambda row: -float(row[3]) if row[3] != "-" else 1e9)
    md = ["# Capture regression diff", "", "A (reference): `%s`  " % A, "B (new): `%s`  " % B,
          "Matched by file (one tag exists at several resolutions). Matched: %d; only in A: %d; only in B: %d" % (
              len(res), len(only_a), len(only_b)), "",
          qa.md_table(["File", "Tag", "Size A", "Mean abs (levels)", "p99", "Max", "Changed > %g %%" % args.threshold,
                       "d mean luma", "Note"], rows), ""]
    if only_a:
        md += ["Only in A: " + ", ".join(only_a), ""]
    if only_b:
        md += ["Only in B: " + ", ".join(only_b), ""]
    dl = _dup_lines("A", dup_a) + _dup_lines("B", dup_b)
    if dl:
        md += ["## Duplicates in report.json", ""] + dl + [""]
    qa.write_text(os.path.join(out_dir, "summary.md"), "\n".join(md) + "\n")
    print("compare: %d matched, %d only in A, %d only in B, %d duplicate entries -> %s" % (
        len(res), len(only_a), len(only_b), len(dl), out_dir))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
