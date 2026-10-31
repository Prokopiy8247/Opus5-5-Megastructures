"""Contact sheet of a capture run: a grid of labelled thumbnails (MASTER_PROMPT Part 11.4: look at every key shot and at
a contact sheet of all stills).

Usage:
  python -I Tools/contact_sheet.py QA/captures/<run> [--out FILE] [--cols 5] [--thumb 384] [--per-page 40]
                                                    [--only PATTERN] [--all-res]
  (<run> may also be a run id under QA/captures or "latest")

By default only one resolution per shot is shown (1920x1080, or the smallest one present), sorted key shots first,
then beat stills by beat and tag, then extras and defect stills. Labels carry the file name and, when
metrics.json exists (Tools/qa_metrics.py), the mean luma, clipped % and FAIL markers; otherwise the report.json values.
Writes <run>/contact_sheet.png (or contact_sheet_NN.png when there are more images than --per-page).
"""
import argparse
import fnmatch
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # python -I drops the script folder from sys.path
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import qa_common as q  # noqa: E402

RES_RE = re.compile(r"_(\d+)x(\d+)(\.png)$")


def base_key(fname):
    """File name without the resolution suffix (groups the resolutions of one shot)."""
    return RES_RE.sub(r"\3", fname)


def sort_key(fname):
    info = q.parse_name(fname)
    if info["kind"] == "ks":
        return (0, info["index"], fname)
    if info["kind"] == "still":
        order = {"start": 0, "mid": 1, "end": 2}
        return (1, info["beat"], order.get(info["tag"], 3), fname)
    if fname.startswith("parity_"):
        return (3, 0, fname)
    return (2, 0, fname)


def pick(files, all_res):
    if all_res:
        return sorted(files, key=sort_key)
    groups = {}
    for f in files:
        groups.setdefault(base_key(f), []).append(f)
    chosen = []
    for _, fs in groups.items():
        def area(f):
            m = RES_RE.search(f)
            if not m:
                return 1920 * 1080
            w, h = int(m.group(1)), int(m.group(2))
            return 0 if (w, h) == (1920, 1080) else w * h
        chosen.append(sorted(fs, key=area)[0])
    return sorted(chosen, key=sort_key)


def font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:   # Pillow < 10.1
        return ImageFont.load_default()


def label_for(fname, metrics, report_idx):
    m = metrics.get(fname)
    parts = []
    if m and "mean_luma" in m:
        parts.append("L %.0f" % m["mean_luma"])
        parts.append("clip %.2f%%" % m.get("clipped_pct", 0.0))
        if m.get("band"):
            parts.append("band %d" % int(m["band"]["score"]))
        fails = [c["name"] for c in m.get("checks", []) if not c["pass"]]
        if fails:
            parts.append("FAIL " + ",".join(fails[:3]) + ("..." if len(fails) > 3 else ""))
    else:
        r = report_idx.get(fname)
        if r:
            parts.append("L %.0f" % (r.get("mean_luma") or 0.0))
            parts.append("clip %.2f%%" % (r.get("clipped_pct") or 0.0))
            if not r.get("ok", True):
                parts.append("FAIL (player)")
    return " | ".join(parts), bool(m and not m.get("pass", True)) or bool(report_idx.get(fname) and not report_idx[fname].get("ok", True))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run", nargs="?", default="latest")
    ap.add_argument("--out", default=None)
    ap.add_argument("--cols", type=int, default=5)
    ap.add_argument("--thumb", type=int, default=384, help="thumbnail width in px")
    ap.add_argument("--per-page", type=int, default=40)
    ap.add_argument("--only", default=None, help="fnmatch pattern of file names")
    ap.add_argument("--all-res", action="store_true", help="show every resolution, not one per shot")
    a = ap.parse_args()

    run = q.resolve_run(a.run)
    if not run:
        print("[contact_sheet] run folder not found: %s" % a.run)
        return 2
    files = [f for f in q.list_pngs(run) if not f.startswith("_") and not f.startswith("contact_sheet")]
    if a.only:
        files = [f for f in files if fnmatch.fnmatch(f, a.only)]
    files = pick(files, a.all_res)
    if not files:
        print("[contact_sheet] no images in %s" % run)
        return 2
    metrics = {}
    mp = os.path.join(run, "metrics.json")
    if os.path.exists(mp):
        with open(mp, "r", encoding="utf-8") as f:
            for r in json.load(f).get("images", []):
                metrics[r["file"]] = r
    ridx = q.report_index(q.load_report(run))

    tw = a.thumb
    th = int(round(tw * 9 / 16))
    pad, lab = 8, 34
    f_name, f_info = font(13), font(12)
    pages = [files[i:i + a.per_page] for i in range(0, len(files), a.per_page)]
    written = []
    for pi, page in enumerate(pages):
        rows = (len(page) + a.cols - 1) // a.cols
        sheet = Image.new("RGB", (a.cols * (tw + pad) + pad, rows * (th + lab + pad) + pad + 28), (24, 24, 28))
        dr = ImageDraw.Draw(sheet)
        dr.text((pad, 6), "%s  -  page %d/%d  -  %d images" % (os.path.basename(run), pi + 1, len(pages), len(page)), fill=(230, 230, 230), font=f_name)
        for i, fname in enumerate(page):
            cx = pad + (i % a.cols) * (tw + pad)
            cy = 28 + pad + (i // a.cols) * (th + lab + pad)
            try:
                im = Image.open(os.path.join(run, fname)).convert("RGB")
                im.thumbnail((tw, th), Image.LANCZOS)
                ox = cx + (tw - im.width) // 2
                oy = cy + (th - im.height) // 2
                sheet.paste(im, (ox, oy))
            except Exception as e:  # noqa: BLE001
                dr.rectangle([cx, cy, cx + tw, cy + th], outline=(255, 0, 0))
                dr.text((cx + 4, cy + 4), "unreadable: %s" % e, fill=(255, 80, 80), font=f_info)
            info, bad = label_for(fname, metrics, ridx)
            col = (255, 110, 110) if bad else (220, 220, 220)
            if bad:
                dr.rectangle([cx - 2, cy - 2, cx + tw + 1, cy + th + 1], outline=(220, 40, 40), width=2)
            dr.text((cx, cy + th + 3), fname[:60], fill=col, font=f_name)
            dr.text((cx, cy + th + 18), info[:70], fill=(170, 170, 170) if not bad else col, font=f_info)
        if a.out and len(pages) == 1:
            out = a.out
        else:
            out = os.path.join(run, "contact_sheet.png" if len(pages) == 1 else "contact_sheet_%02d.png" % (pi + 1))
            if a.out:
                root, ext = os.path.splitext(a.out)
                out = "%s_%02d%s" % (root, pi + 1, ext or ".png")
        sheet.save(out)
        written.append(out)
    print("[contact_sheet] %d images -> %s" % (len(files), ", ".join(written)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
