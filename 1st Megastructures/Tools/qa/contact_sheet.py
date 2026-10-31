"""Contact sheet of every PNG in a capture folder, with tags, for the lead to look at (Part 11.4).

Usage:
    python -I Tools/qa/contact_sheet.py QA/captures/<run-id> [--out QA/contact/<run-id>.jpg] [--cols 4]
           [--tile-width 480] [--per-page 24] [--metrics QA/metrics/<run-id>/metrics.json] [--format jpg|png]
           [--recursive]

Tiles are downscaled with a Lanczos filter, keep their aspect ratio and are labelled with the tag (from report.json
when present, otherwise the file name), the beat and the source size. With --metrics, each tile gets a green (pass) or
red (fail) bar and the failing check names. More images than --per-page produce several pages (<out>_p01, _p02 ...).
Tiles and metrics entries are matched by FILE (path relative to the capture folder), never by tag alone: one key
shot exists at several resolutions under the same tag. A file referenced by several report shots gets one tile; such
duplicates are printed and counted in the sheet header.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import json  # noqa: E402
import math  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from typing import Any, Dict, List, Optional  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

BG = (14, 15, 18)
FG = (225, 228, 232)
DIM = (150, 155, 160)
PASS_C = (60, 170, 90)
FAIL_C = (210, 60, 60)


def collect(folder: str, recursive: bool) -> Dict[str, Any]:
    """{"entries": [{path, key, tag, beat}] in report order first, then any other PNG in natural order,
    "duplicates": report_duplicates of report.json}. One entry per file."""
    entries: List[Dict[str, Any]] = []
    seen = set()
    dups: Dict[str, List[Dict[str, Any]]] = {"files": [], "tag_sizes": []}
    rep_path = os.path.join(folder, "report.json")
    if os.path.isfile(rep_path):
        rep = qa.load_report(folder)
        dups = qa.report_duplicates(rep["shots"], folder)
        for s in rep["shots"]:
            p = s.get("file") or ""
            p = p if os.path.isabs(p) else os.path.join(folder, p)
            if os.path.isfile(p):
                key = qa.shot_key(p, folder)
                if qa.match_key(key) in seen:      # the same file listed again (see duplicates)
                    continue
                seen.add(qa.match_key(key))
                entries.append({"path": p, "key": key, "tag": str(s.get("tag") or os.path.basename(p)),
                                "beat": s.get("beat") or qa.infer_beat(str(s.get("tag", "")), p)})
    for p in qa.list_pngs(folder, recursive):
        key = qa.shot_key(p, folder)
        if qa.match_key(key) in seen:
            continue
        stem = os.path.splitext(os.path.relpath(p, folder))[0]
        entries.append({"path": p, "key": key, "tag": stem, "beat": qa.infer_beat(stem, p)})
    return {"entries": entries, "duplicates": dups}


def load_metrics(path: Optional[str]) -> Optional[Dict[str, Dict[str, Dict[str, Any]]]]:
    """Index of metrics.json shots by file: relative to the metrics' source folder, and by basename (kept only when
    unique, for a metrics file computed on a copy of the folder). Never by tag: a tag is shared by the resolutions of
    one key shot, so a tag lookup would colour a 4K tile with the 1080p verdict."""
    if not path:
        return None
    with open(path, "r", encoding="utf-8") as f:
        m = json.load(f)
    source = str(m.get("source") or os.path.dirname(os.path.abspath(path)))
    by_rel: Dict[str, Dict[str, Any]] = {}
    by_base: Dict[str, Optional[Dict[str, Any]]] = {}
    for s in m.get("shots", []):
        if s.get("file"):
            f = str(s["file"])
            by_rel.setdefault(qa.match_key(qa.shot_key(f, source)), s)
            b = qa.match_key(os.path.basename(f))
            by_base[b] = None if b in by_base else s
    return {"rel": by_rel, "base": {k: v for k, v in by_base.items() if v is not None}}


def make_tile(entry: Dict[str, Any], tile_w: int, tile_h: int, label_h: int,
              met: Optional[Dict[str, Any]]) -> Image.Image:
    tile = Image.new("RGB", (tile_w, tile_h + label_h), BG)
    d = ImageDraw.Draw(tile)
    f1, f2 = qa.font(14), qa.font(12)
    try:
        with Image.open(entry["path"]) as im:
            src_w, src_h = im.size
            im = im.convert("RGB")
            scale = min(tile_w / float(src_w), tile_h / float(src_h))
            tw, th = max(1, int(round(src_w * scale))), max(1, int(round(src_h * scale)))
            thumb = im.resize((tw, th), Image.Resampling.LANCZOS)
        tile.paste(thumb, ((tile_w - tw) // 2, (tile_h - th) // 2))
        size_txt = "%dx%d" % (src_w, src_h)
    except Exception as e:  # keep the sheet going; an unreadable PNG is visible as a red tile
        d.rectangle([0, 0, tile_w - 1, tile_h - 1], fill=(80, 0, 0))
        d.text((8, 8), "unreadable: %s" % e, fill=FG, font=f2)
        size_txt = "?"
    y = tile_h + 3
    tag = entry["tag"]
    while len(tag) > 4 and qa.text_size(d, tag, f1)[0] > tile_w - 8:
        tag = tag[:-2]
    d.text((4, y), tag if tag == entry["tag"] else tag + "...", fill=FG, font=f1)
    info = "beat %s  %s" % (entry["beat"] if entry["beat"] else "?", size_txt)
    if met is not None and not met:
        d.rectangle([0, tile_h, tile_w - 1, tile_h + 2], fill=DIM)
        d.text((4, y + 34), "no metrics entry", fill=DIM, font=f2)
    elif met is not None:
        ok = bool(met.get("pass"))
        col = PASS_C if ok else FAIL_C
        d.rectangle([0, tile_h, tile_w - 1, tile_h + 2], fill=col)
        lum = met.get("luma") or {}
        if "mean" in lum:
            info += "  Y %.3f  p99 %.2f" % (lum["mean"], lum.get("p99", float("nan")))
        if not ok:
            fails = ", ".join(met.get("failures") or [])
            while len(fails) > 4 and qa.text_size(d, fails, f2)[0] > tile_w - 8:
                fails = fails[:-2]
            d.text((4, y + 34), fails, fill=FAIL_C, font=f2)
    d.text((4, y + 18), info, fill=DIM, font=f2)
    return tile


def build_pages(entries: List[Dict[str, Any]], cols: int, tile_w: int, per_page: int,
                metrics: Optional[Dict[str, Dict[str, Dict[str, Any]]]], title: str,
                n_duplicates: int = 0) -> List[Image.Image]:
    tile_h = int(round(tile_w * 9 / 16.0))
    label_h = 54 if metrics else 40
    gap, head_h = 6, 34
    pages = []
    n_pages = max(1, int(math.ceil(len(entries) / float(per_page))))
    for p in range(n_pages):
        chunk = entries[p * per_page:(p + 1) * per_page]
        rows = max(1, int(math.ceil(len(chunk) / float(cols))))
        W = cols * tile_w + (cols + 1) * gap
        H = head_h + rows * (tile_h + label_h) + (rows + 1) * gap
        sheet = Image.new("RGB", (W, H), BG)
        d = ImageDraw.Draw(sheet)
        n_fail = sum(1 for e in chunk if metrics and not _met(metrics, e).get("pass", True))
        head = "%s   page %d/%d   %d images" % (title, p + 1, n_pages, len(chunk))
        if metrics:
            head += "   %d failing" % n_fail
        if n_duplicates:
            head += "   %d duplicate report entries (see console)" % n_duplicates
        d.text((gap, 8), head, fill=FG, font=qa.font(16))
        for i, e in enumerate(chunk):
            r, c = divmod(i, cols)
            x = gap + c * (tile_w + gap)
            y = head_h + gap + r * (tile_h + label_h + gap)
            met = _met(metrics, e) if metrics else None   # None = no metrics given, {} = no entry for this tile
            sheet.paste(make_tile(e, tile_w, tile_h, label_h, met), (x, y))
        pages.append(sheet)
    return pages


def _met(metrics: Dict[str, Dict[str, Dict[str, Any]]], e: Dict[str, Any]) -> Dict[str, Any]:
    """Metrics entry of a tile, matched by file only ({} = no entry)."""
    return (metrics["rel"].get(qa.match_key(e["key"])) or
            metrics["base"].get(qa.match_key(os.path.basename(e["path"]))) or {})


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder")
    ap.add_argument("--out", help="output file (default QA/contact/<folder-name>.<format>)")
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--tile-width", type=int, default=480)
    ap.add_argument("--per-page", type=int, default=24)
    ap.add_argument("--metrics", help="metrics.json from image_metrics.py to colour tiles by pass / fail")
    ap.add_argument("--format", choices=["jpg", "png"], default="jpg")
    ap.add_argument("--recursive", action="store_true")
    args = ap.parse_args(argv)
    folder = os.path.abspath(args.folder)
    if not os.path.isdir(folder):
        qa.eprint("not a folder: %s" % folder)
        return 2
    col = collect(folder, args.recursive)
    entries = col["entries"]
    if not entries:
        qa.eprint("no PNG files in %s" % folder)
        return 2
    dups = col["duplicates"]
    for d in dups["files"]:
        qa.eprint("warning: file %s is referenced by %d shots in report.json (%s); one tile drawn" % (
            d["file"], len(d["shots"]), ", ".join("%s %sx%s" % (x["tag"], x["width"], x["height"])
                                                  for x in d["shots"])))
    for d in dups["tag_sizes"]:
        qa.eprint("warning: tag %s at %s appears in %d files: %s" % (d["tag"], d["size"], len(d["files"]),
                                                                    ", ".join(d["files"])))
    metrics = load_metrics(args.metrics)
    name = re.sub(r"[^A-Za-z0-9_.-]+", "_", os.path.basename(folder))
    out = os.path.abspath(args.out) if args.out else os.path.join(qa.QA_DIR, "contact", name + "." + args.format)
    base, ext = os.path.splitext(out)
    if not ext:
        ext = "." + args.format
    pages = build_pages(entries, max(1, args.cols), max(64, args.tile_width), max(1, args.per_page), metrics,
                        "Contact sheet: " + os.path.basename(folder), len(dups["files"]) + len(dups["tag_sizes"]))
    qa.ensure_dir(os.path.dirname(out))
    written = []
    for i, pg in enumerate(pages):
        path = (base + ext) if len(pages) == 1 else ("%s_p%02d%s" % (base, i + 1, ext))
        if ext.lower() in (".jpg", ".jpeg"):
            pg.save(path, quality=92, subsampling=0)
        else:
            pg.save(path, optimize=True)
        written.append(path)
        print("contact sheet: %s (%dx%d, %d images)" % (path, pg.size[0], pg.size[1],
                                                         min(args.per_page, len(entries) - i * args.per_page)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
