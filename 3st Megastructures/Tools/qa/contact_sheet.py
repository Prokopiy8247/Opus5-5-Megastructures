#!/usr/bin/env python3
"""contact_sheet: labelled grid contact sheets of every still of a capture folder (MASTER_PROMPT 11.4).

Usage:
  python -I Tools/qa/contact_sheet.py QA/captures/<run-id> [--out <folder>] [--cols 4] [--max-width 2048]
         [--max-height 2048] [--metrics QA/reports/image_metrics_<run>.json] [--sequences] [--seq-every 15]

Stills come from report.json (tag order) or every *.png of the folder.  Thumbnails keep their aspect inside a
16:9 cell, carry "tag  WxH" (and FAIL with a red frame when --metrics marks the image as failing).  Sheets are at
most --max-width px wide (default 2048) and --max-height px tall; several numbered sheets are written when needed:
<out>/contact_<run>_NN.png (default <out> = <capture>/_contact).  --sequences adds one sheet per seq_* folder
with every Nth frame.  Exit code 1 if no sheet could be written although images exist.
"""
import argparse
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import qa_common as qc  # noqa: E402
from qa_common import PASS, FAIL, SKIP  # noqa: E402

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

BG = (24, 24, 26)
LABEL = (225, 225, 225)
FAILC = (230, 40, 40)
GAP = 8


def _font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # Pillow < 10.1
        return ImageFont.load_default()


def make_sheets(items, out_prefix, cols=4, max_w=2048, max_h=2048, title=None):
    """items: [(label, path, failed)].  Returns the list of written sheet paths."""
    if not items:
        return []
    cols = max(1, int(cols))
    cell_w = (max_w - (cols + 1) * GAP) // cols
    cell_h = int(round(cell_w * 9 / 16.0))
    font = _font(max(11, cell_w // 34))
    label_h = font.size + 8 if hasattr(font, "size") else 18
    title_h = (label_h + GAP) if title else 0
    row_h = cell_h + label_h + GAP
    rows_per_sheet = max(1, (max_h - GAP - title_h) // row_h)
    per_sheet = rows_per_sheet * cols
    paths = []
    n_sheets = (len(items) + per_sheet - 1) // per_sheet
    for si in range(n_sheets):
        chunk = items[si * per_sheet:(si + 1) * per_sheet]
        nrows = (len(chunk) + cols - 1) // cols
        H = GAP + title_h + nrows * row_h
        sheet = Image.new("RGB", (max_w, H), BG)
        dr = ImageDraw.Draw(sheet)
        if title:
            dr.text((GAP, GAP), "%s  (sheet %d/%d)" % (title, si + 1, n_sheets), fill=LABEL, font=font)
        for k, (label, path, failed) in enumerate(chunk):
            r, c = divmod(k, cols)
            x = GAP + c * (cell_w + GAP)
            y = GAP + title_h + r * row_h
            try:
                with Image.open(path) as im:
                    im = im.convert("RGB")
                    w0, h0 = im.size
                    s = min(cell_w / float(w0), cell_h / float(h0))
                    tw, th = max(1, int(w0 * s)), max(1, int(h0 * s))
                    th_img = im.resize((tw, th), Image.LANCZOS)
                size_txt = "%dx%d" % (w0, h0)
            except Exception as e:  # unreadable image: draw a placeholder
                th_img, tw, th, size_txt = None, cell_w, cell_h, "unreadable: %s" % type(e).__name__
                failed = True
            ox, oy = x + (cell_w - tw) // 2, y + (cell_h - th) // 2
            if th_img is not None:
                sheet.paste(th_img, (ox, oy))
            if failed:
                dr.rectangle([ox - 3, oy - 3, ox + tw + 2, oy + th + 2], outline=FAILC, width=3)
            txt = "%s  %s%s" % (label, size_txt, "  FAIL" if failed else "")
            while len(txt) > 4 and dr.textlength(txt, font=font) > cell_w:
                txt = txt[:-2]
            dr.text((x, y + cell_h + 3), txt, fill=FAILC if failed else LABEL, font=font)
        p = "%s_%02d.png" % (out_prefix, si + 1)
        qc.ensure_dir(os.path.dirname(p))
        sheet.save(p, optimize=False, compress_level=6)
        paths.append(p)
    return paths


def run(cap_dir, out=None, cols=4, max_w=2048, max_h=2048, metrics=None, sequences=False, seq_every=15,
        run_name=None):
    cap_dir = os.path.abspath(cap_dir)
    run_name = qc.run_id_for(cap_dir, run_name)
    out = out or os.path.join(cap_dir, "_contact")
    shots, _, warnings = qc.load_capture(cap_dir)
    failed = set()
    if metrics:
        mj = qc.read_json(metrics, {}) or {}
        for r in mj.get("images", []):
            if r.get("status") == FAIL:
                failed.add(r.get("tag"))
    shots = [s for s in shots if not s["file"].lower().endswith("_window.png")]
    shots.sort(key=lambda s: s["tag"])
    items = [(s["tag"], s["file"], s["tag"] in failed) for s in shots if os.path.isfile(s["file"])]
    paths = make_sheets(items, os.path.join(out, "contact_%s" % run_name), cols, max_w, max_h,
                        "%s: %d stills" % (run_name, len(items)))
    seq_paths = []
    if sequences:
        for sd in qc.find_sequences(cap_dir):
            seq = qc.load_sequence(sd)
            fr = seq["frames"][::max(1, int(seq_every))]
            it = [("f%05d" % i, p, False) for i, p in fr]
            seq_paths += make_sheets(it, os.path.join(out, "contact_%s_seq_%s" % (run_name, seq["name"])), cols + 2,
                                     max_w, max_h, "%s seq %s every %d" % (run_name, seq["name"], seq_every))
    status = PASS if (paths or not items) else FAIL
    if not items:
        status = SKIP
        warnings.append("no stills found")
    return {"tool": "contact_sheet", "status": status, "sheets": paths, "seq_sheets": seq_paths, "fails": [],
            "warnings": warnings, "n": len(items)}


def main(argv=None):
    ap = argparse.ArgumentParser(prog="contact_sheet.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("capture_dir")
    ap.add_argument("--out", help="output folder (default <capture>/_contact)")
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--max-width", type=int, default=2048)
    ap.add_argument("--max-height", type=int, default=2048)
    ap.add_argument("--metrics", help="image_metrics JSON to mark failing images")
    ap.add_argument("--sequences", action="store_true", help="also one sheet per seq_* folder")
    ap.add_argument("--seq-every", type=int, default=15, help="frame stride of the sequence sheets")
    ap.add_argument("--run", help="run id (default: capture folder name)")
    a = ap.parse_args(argv)
    if not os.path.isdir(a.capture_dir):
        print("capture folder not found: %s" % a.capture_dir, file=sys.stderr)
        return 1
    r = run(a.capture_dir, a.out, a.cols, min(a.max_width, 2048), a.max_height, a.metrics, a.sequences,
            a.seq_every, a.run)
    print("[contact_sheet] %s: %d stills -> %d sheet(s)" % (r["status"], r["n"], len(r["sheets"])))
    for p in r["sheets"] + r["seq_sheets"]:
        print("  " + p)
    return 1 if r["status"] == FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
