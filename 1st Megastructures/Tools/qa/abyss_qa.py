"""Shared helpers for the Abyss QA analysis tools (Part 11.3, 11.6, 11.12 and 14.10 of MASTER_PROMPT.md).

Every QA script in this folder is run with ``python -I`` (isolated mode), which does NOT put the script's own folder
on ``sys.path``. Each script therefore adds this folder explicitly from ``__file__`` before importing this module::

    import os, sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import abyss_qa as qa

Conventions used by all tools
-----------------------------
* Images are loaded as 8-bit sRGB (``uint8`` H x W x 3). "Luma" is Rec. 709 applied to the *sRGB-encoded* values in
  0..1 (``0.2126 R' + 0.7152 G' + 0.0722 B'``), exactly as 14.10 defines it. The palette check of 14.7.2 confirms the
  convention: #070B10 has luma 0.0412, the "0.041" of the abyss nadir requirement.
* "Clipped" = any channel >= 0.98 (8-bit value >= 250).
* Regions are rectangles ``[x0, y0, x1, y1]`` (or ``{"rect": [...]}``) as fractions of the frame (origin top-left,
  y downward), clamped to 0..1. Pixels are only ever explicit: ``{"px": [x0, y0, x1, y1]}``. A circle is
  ``{"circle": [cx, cy, r]}`` with cx, cy as fractions of width / height and r as a fraction of the frame height, or
  ``{"circle_px": [cx, cy, r]}`` in pixels. A union of fractional rectangles is ``{"rects": [[x0, y0, x1, y1], ...]}``
  (the harness samples the horizon over both symmetric side ranges this way). On the command line: ``x0,y0,x1,y1``
  (fractions, must lie in 0..1), ``px:x0,y0,x1,y1``, ``circle:cx,cy,r``, ``circle_px:cx,cy,r``,
  ``rects:x0,y0,x1,y1/x0,y0,x1,y1``. A region that is malformed or smaller than
  8 x 8 px inside the frame is an error and fails its check (``resolve_region``); it never passes as empty.
* Shots are identified by their file (path relative to the capture folder), not by tag: every key shot is captured at
  several resolutions with the same tag (11.2). ``shot_key`` / ``report_duplicates`` implement this.
* Every entry script sets ``sys.dont_write_bytecode = True`` before importing this module, so no ``__pycache__`` is
  ever written into ``Tools/``.
* Banding (BandScore, 14.10): along a vertical line inside a flagged gradient region whose total change (max - min of
  that channel along the line) exceeds 24 levels, the longest run of identical 8-bit values must not exceed 12 px at
  1080p (24 px at 2160p; the limit scales linearly with the frame height). Runs of 0 or 255 are clamping, not banding,
  and are excluded (clipping has its own metric).
"""

from __future__ import annotations

import datetime as _dt
import json
import math
import os
import re
import sys
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple

import numpy as np
from PIL import Image, ImageDraw, ImageFont

TOOL_VERSION = "1.0.0"

# ----------------------------------------------------------------------------------------------------------------------
# Paths
# ----------------------------------------------------------------------------------------------------------------------
QA_TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(os.path.dirname(QA_TOOLS_DIR))
QA_DIR = os.path.join(PROJECT_ROOT, "QA")

# ----------------------------------------------------------------------------------------------------------------------
# Spec constants (14.2 palette, 14.5 beat names, 14.10 thresholds, 14.7.2 nadir)
# ----------------------------------------------------------------------------------------------------------------------
BEAT_NAMES = {1: "gallery", 2: "bridge", 3: "ledge", 4: "hall", 5: "lift", 6: "fan", 7: "reveal"}

OUT_HORIZ_RGB = (0x84, 0x96, 0xA5)      # AbyssPalette.OUT_HORIZ (#8496A5)
FOG_DOWN_RGB = (0x07, 0x0B, 0x10)       # 14.7.2: the abyss nadir reads as #070B10, luma 0.041

# Per-beat image thresholds of 14.10. Keys: (beat, zone). zone is None except for beat 7 ("tunnel" / "outside").
# mean = [min, max] of mean luma; p99_max = maximum 99th-percentile luma; clipped_max_pct = maximum % of clipped pixels.
BEAT_THRESHOLDS: Dict[Tuple[int, Optional[str]], Dict[str, Any]] = {
    (1, None): {"mean": (0.10, 0.20), "p99_max": 0.90, "clipped_max_pct": 0.3, "band_desc": "doorway glow",
                "alias_desc": "pipe, tray lines"},
    (2, None): {"mean": (0.05, 0.13), "p99_max": 0.70, "clipped_max_pct": 0.3,
                "band_desc": "upper 30 % fog, lower 40 % void", "alias_desc": "bridge edges, truss, posts"},
    (3, None): {"mean": (0.06, 0.15), "p99_max": 0.70, "clipped_max_pct": 0.3, "band_desc": "upper 25 % column fog",
                "alias_desc": "kerb, lamps"},
    (4, None): {"mean": (0.08, 0.18), "p99_max": 0.80, "clipped_max_pct": 0.5, "band_desc": "far hall haze",
                "alias_desc": "rails, truss, grating"},
    (5, None): {"mean": (0.06, 0.16), "p99_max": 0.80, "clipped_max_pct": 0.5, "band_desc": "shaft fog",
                "alias_desc": "window grid, rails"},
    (6, None): {"mean": (0.09, 0.22), "p99_max": 0.95, "clipped_max_pct": 1.0, "band_desc": "beam region",
                "alias_desc": "portal frames, rails"},
    (7, "tunnel"): {"mean": (0.04, 0.20), "p99_max": 0.98, "clipped_max_pct": 2.0, "band_desc": "mouth glow",
                    "alias_desc": "tunnel edges"},
    (7, "outside"): {"mean": (0.25, 0.45), "p99_max": 0.92, "clipped_max_pct": 1.0,
                     "band_desc": "sky and haze, top 40 %", "alias_desc": "lattice rows, door frame"},
}

# Default banding (gradient) regions per beat, fractions of the frame [x0, y0, x1, y1]. A shot in report.json can
# replace them with its own "regions": {"banding": ...}. The descriptive names follow the 14.10 table. Beat 1: the
# doorway glow is the KS1b region only (default_band_regions); every other beat-1 shot gets the same rectangle as
# "gallery_centre", because at KS1 the sloped stair ceiling hides the doorway.
DEFAULT_BAND_REGIONS: Dict[Tuple[int, Optional[str]], Dict[str, List[float]]] = {
    (1, None): {"doorway_glow": [0.30, 0.10, 0.70, 0.90]},
    (2, None): {"upper30_fog": [0.0, 0.0, 1.0, 0.30], "lower40_void": [0.0, 0.60, 1.0, 1.0]},
    (3, None): {"upper25_column_fog": [0.0, 0.0, 1.0, 0.25]},
    (4, None): {"far_hall_haze": [0.25, 0.20, 0.75, 0.60]},
    (5, None): {"shaft_fog_upper": [0.0, 0.0, 1.0, 0.35], "shaft_fog_lower": [0.0, 0.65, 1.0, 1.0]},
    (6, None): {"beam_region": [0.20, 0.0, 0.80, 0.60]},
    (7, "tunnel"): {"mouth_glow": [0.35, 0.30, 0.65, 0.70]},
    (7, "outside"): {"sky_haze_top40": [0.0, 0.0, 1.0, 0.40]},
}

BAND_CHANGE_LEVELS = 24          # a line qualifies when its total change exceeds this many 8-bit levels
BAND_LIMIT_PX_1080 = 12          # still images: longest identical run at 1080p
ENCODE_BAND_LIMIT_PX_1080 = 24   # decoded video frames (14.10 encode test): longest run at 1080p
HORIZON_AGREE_LEVELS = 3         # fog / sky / backdrop horizon colours within 3 levels
HORIZON_NOPOST_LEVELS = 2        # with -nopost the beat-7 horizon row equals OUT_HORIZ within 2 levels
CLIP_LEVEL_8BIT = 250            # 0.98 * 255 = 249.9 -> any channel >= 250 is clipped

# 14.7.2 nadir: luma 0.041 +/- 0.008, no pixel above 0.06, no horizontal gradient edge above 0.03
NADIR_LUMA = 0.041
NADIR_TOL = 0.008
NADIR_MAX_PIXEL = 0.06
NADIR_MAX_EDGE = 0.03

# 14.5.1 acceptance: "doorway luma 0.65-0.90 with under 0.3 % clipped". Only the KS1b pose sees the doorway (at KS1 the
# sloped stair ceiling hides it, a verified geometric fact), so the check is evaluated for KS1b shots only.
# Calibrated band (14.10 calibration clause; DEVELOPMENT.md gallery row "KS1b doorway luma" and lead decision D6 of fix
# round R2): 0.40-0.60. A 0.8 doorway that becomes the shaft view (about 0.13) within d 52-62 is a 2.6 EV drop in 6.7 s,
# 0.39 EV/s, above the 0.35 EV/s limit of 14.6. The clipping limit is unchanged.
DOORWAY_LUMA_SPEC = (0.65, 0.90)     # the 14.5.1 text, kept for reports
DOORWAY_LUMA = (0.40, 0.60)          # mean luma of the clear doorway aperture (calibrated, D6)
DOORWAY_CLIPPED_MAX_PCT = 0.3        # clipped pixels inside the aperture, strictly under this %
DOORWAY_SHOT = "KS1b"
# KS1b pose and doorway geometry (KeyShots.cs, AbyssConst) for the default aperture when a shot carries no "doorway" region
KS1B_EYE = (0.0, -1.3, 55.0)
KS1B_VFOV = 62.0
DOORWAY_W, DOORWAY_H, DOORWAY_FLOOR_Y = 1.2, 2.0, -3.0
DOORWAY_Z_OUT, DOORWAY_DEPTH = 60.0, 0.8

REC709 = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)


# ----------------------------------------------------------------------------------------------------------------------
# Generic helpers
# ----------------------------------------------------------------------------------------------------------------------
def now_iso() -> str:
    return _dt.datetime.now().astimezone().isoformat(timespec="seconds")


def natural_key(s: str) -> List[Any]:
    """Sort key that orders 'frame_2' before 'frame_10'."""
    return [int(t) if t.isdigit() else t.lower() for t in re.split(r"(\d+)", s)]


def list_pngs(folder: str, recursive: bool = False) -> List[str]:
    out: List[str] = []
    if recursive:
        for base, _dirs, files in os.walk(folder):
            for f in files:
                if f.lower().endswith(".png"):
                    out.append(os.path.join(base, f))
    else:
        for f in os.listdir(folder):
            if f.lower().endswith(".png"):
                out.append(os.path.join(folder, f))
    out.sort(key=lambda p: natural_key(os.path.relpath(p, folder)))
    return out


def ensure_dir(path: str) -> str:
    os.makedirs(path, exist_ok=True)
    return path


def _json_default(o: Any) -> Any:
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, (np.floating,)):
        v = float(o)
        return v if math.isfinite(v) else None
    if isinstance(o, (np.bool_,)):
        return bool(o)
    if isinstance(o, np.ndarray):
        return o.tolist()
    if isinstance(o, tuple):
        return list(o)
    raise TypeError("not JSON serialisable: %r" % type(o))


def write_json(path: str, data: Any) -> None:
    ensure_dir(os.path.dirname(os.path.abspath(path)))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, default=_json_default)
        f.write("\n")


def write_text(path: str, text: str) -> None:
    ensure_dir(os.path.dirname(os.path.abspath(path)))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def md_table(headers: Sequence[str], rows: Iterable[Sequence[Any]]) -> str:
    def cell(v: Any) -> str:
        return str(v).replace("|", "\\|").replace("\n", " ")
    lines = ["| " + " | ".join(cell(h) for h in headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for r in rows:
        lines.append("| " + " | ".join(cell(v) for v in r) + " |")
    return "\n".join(lines)


def fmt(v: Optional[float], digits: int = 3) -> str:
    if v is None:
        return "-"
    return ("%." + str(digits) + "f") % v


def parse_rect_arg(text: str) -> Tuple[str, Any]:
    """Parse a CLI region, optionally prefixed by 'name=': 'x0,y0,x1,y1' (fractions 0..1), 'px:x0,y0,x1,y1'
    (pixels), 'circle:cx,cy,r' (fractions, r of the height), 'circle_px:cx,cy,r' or
    'rects:x0,y0,x1,y1/x0,y0,x1,y1/...' (a union of fractional rectangles). Returns (name, shape).
    Fractions outside 0..1 raise ValueError: pixels must be written with 'px:', so a pixel rectangle typed without
    the prefix is an input error instead of being clamped into a different region."""
    name = ""
    if "=" in text:
        name, text = text.split("=", 1)
    body = text.strip()
    if body.lower().startswith("rects:"):
        rects = []
        for part in body[6:].split("/"):
            _n, r = parse_rect_arg(part)
            if not isinstance(r, list):
                raise ValueError("region %r: rects: takes fractional rectangles x0,y0,x1,y1 separated by '/'" % text)
            rects.append(r)
        return name.strip(), {"rects": rects}
    kind = "rect"
    for prefix in ("circle_px:", "circle:", "px:"):
        if body.lower().startswith(prefix):
            kind, body = prefix[:-1], body[len(prefix):]
            break
    try:
        vals = [float(v) for v in body.replace(";", ",").split(",") if v.strip()]
    except ValueError:
        raise ValueError("region %r: not a list of numbers" % text) from None
    n = 3 if kind.startswith("circle") else 4
    if len(vals) != n or not all(math.isfinite(v) for v in vals):
        raise ValueError("region %r needs %d finite numbers (%s)" % (
            text, n, "cx,cy,r" if n == 3 else "x0,y0,x1,y1"))
    if kind in ("rect", "circle") and any(v < 0.0 or v > 1.0 for v in vals):
        raise ValueError("region %r: fractions must lie within 0..1; write %s:%s for pixels" % (
            text, "circle_px" if kind == "circle" else "px", body))
    if kind == "rect":
        return name.strip(), vals
    return name.strip(), {kind: vals}


# ----------------------------------------------------------------------------------------------------------------------
# Image I/O and colour
# ----------------------------------------------------------------------------------------------------------------------
def load_rgb8(path: str) -> np.ndarray:
    """Load any PNG as 8-bit RGB (H x W x 3 uint8). 16-bit images are rounded to 8 bit; alpha is dropped."""
    with Image.open(path) as im:
        im.load()
        if im.mode in ("I;16", "I;16B", "I;16L", "I"):
            a = np.asarray(im, dtype=np.float64)
            scale = 255.0 / 65535.0 if a.max() > 255 else 1.0
            g = np.clip(np.rint(a * scale), 0, 255).astype(np.uint8)
            return np.repeat(g[:, :, None], 3, axis=2)
        if im.mode == "RGB":
            return np.asarray(im, dtype=np.uint8).copy()
        if im.mode in ("RGBA", "LA", "P", "L", "CMYK", "YCbCr", "1"):
            return np.asarray(im.convert("RGB"), dtype=np.uint8).copy()
        return np.asarray(im.convert("RGB"), dtype=np.uint8).copy()


def image_size(path: str) -> Tuple[int, int]:
    with Image.open(path) as im:
        return im.size  # (w, h)


def to_float(rgb8: np.ndarray) -> np.ndarray:
    return rgb8.astype(np.float32) * (1.0 / 255.0)


def luma(rgb: np.ndarray) -> np.ndarray:
    """Rec. 709 luma of sRGB-encoded values. Accepts uint8 (0..255) or float (0..1); returns float32 0..1."""
    if rgb.dtype == np.uint8:
        return (rgb.astype(np.float32) @ REC709) * (1.0 / 255.0)
    return rgb.astype(np.float32) @ REC709


def srgb_to_linear(v: np.ndarray) -> np.ndarray:
    v = v.astype(np.float32)
    return np.where(v <= 0.04045, v / 12.92, np.power((v + 0.055) / 1.055, 2.4)).astype(np.float32)


def hex_to_rgb(h: str) -> Tuple[int, int, int]:
    h = h.strip().lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


# ----------------------------------------------------------------------------------------------------------------------
# Regions
# ----------------------------------------------------------------------------------------------------------------------
MIN_REGION_PX = 8          # a judged region smaller than 8 x 8 px (or 64 px in all) is an error, never a pass
FAR_OUTSIDE_FRACTION = 0.5  # fractional values beyond -0.5 .. 1.5 are flagged as "probably pixels" (still clamped)
REGION_KEYS = ("rect", "px", "circle", "circle_px", "rects")


def _numbers(v: Any, n: int, what: str) -> List[float]:
    if isinstance(v, (str, bytes)) or not isinstance(v, (list, tuple)):
        raise ValueError("%s needs a list of %d numbers, got %r" % (what, n, v))
    vals = [float(x) for x in v]
    if len(vals) != n:
        raise ValueError("%s needs %d numbers, got %d" % (what, n, len(vals)))
    if not all(math.isfinite(x) for x in vals):
        raise ValueError("%s has a non-finite value: %r" % (what, v))
    return vals


def _clamp01(v: float) -> float:
    return 0.0 if v < 0.0 else (1.0 if v > 1.0 else v)


def rect_to_px(rect: Sequence[float], w: int, h: int, pixels: bool = False) -> Tuple[int, int, int, int]:
    """[x0, y0, x1, y1] -> clamped integer pixel box (x1, y1 exclusive). The values are fractions of the frame,
    clamped to 0..1, unless ``pixels`` is True (the explicit {"px": [...]} form). There is no guessing from the
    magnitude: a fraction that overshoots slightly (1.02) stays a fraction."""
    x0, y0, x1, y1 = _numbers(rect, 4, "rectangle")
    if pixels:
        return clamp_box_px(x0, y0, x1, y1, w, h)
    x0, y0, x1, y1 = _clamp01(x0), _clamp01(y0), _clamp01(x1), _clamp01(y1)
    return clamp_box_px(x0 * w, y0 * h, x1 * w, y1 * h, w, h)


def clamp_box_px(px0: float, py0: float, px1: float, py1: float, w: int, h: int) -> Tuple[int, int, int, int]:
    """Pixel coordinates -> ordered, rounded, frame-clamped integer box (x1, y1 exclusive)."""
    a = int(round(min(px0, px1)))
    b = int(round(min(py0, py1)))
    c = int(round(max(px0, px1)))
    d = int(round(max(py0, py1)))
    a, c = max(0, min(w, a)), max(0, min(w, c))
    b, d = max(0, min(h, b)), max(0, min(h, d))
    return a, b, c, d


def region_mask(shape: Any, w: int, h: int) -> Tuple[Tuple[int, int, int, int], Optional[np.ndarray]]:
    """Return (bounding box, optional boolean mask inside the box) for a region. Forms:
    ``[x0, y0, x1, y1]`` or ``{"rect": [...]}`` (fractions, clamped to 0..1), ``{"px": [x0, y0, x1, y1]}`` (pixels),
    ``{"circle": [cx, cy, r]}`` (fractions; r of the frame height), ``{"circle_px": [cx, cy, r]}`` (pixels) and
    ``{"rects": [[x0, y0, x1, y1], ...]}`` (union of fractional rectangles: bounding box plus mask).
    A circle keeps its centre (it may lie outside the frame); only its part inside the frame is used.
    Raises ValueError for a malformed shape. Use ``resolve_region`` to also reject regions that are too small."""
    if isinstance(shape, dict) and "rects" in shape:
        parts = shape["rects"]
        if isinstance(parts, (str, bytes)) or not isinstance(parts, (list, tuple)) or not parts:
            raise ValueError("rects needs a non-empty list of rectangles, got %r" % (parts,))
        boxes = [rect_to_px(r, w, h) for r in parts]
        boxes = [b for b in boxes if b[2] > b[0] and b[3] > b[1]]
        if not boxes:
            return (0, 0, 0, 0), np.zeros((0, 0), dtype=bool)
        x0, y0 = min(b[0] for b in boxes), min(b[1] for b in boxes)
        x1, y1 = max(b[2] for b in boxes), max(b[3] for b in boxes)
        m = np.zeros((y1 - y0, x1 - x0), dtype=bool)
        for a, b, c, d in boxes:
            m[b - y0:d - y0, a - x0:c - x0] = True
        return (x0, y0, x1, y1), m
    if isinstance(shape, dict) and ("circle" in shape or "circle_px" in shape):
        if "circle_px" in shape:
            pcx, pcy, pr = _numbers(shape["circle_px"], 3, "circle_px")
        else:
            cx, cy, r = _numbers(shape["circle"], 3, "circle")
            pcx, pcy, pr = cx * w, cy * h, r * h
        pr = max(0.0, pr)
        box = clamp_box_px(pcx - pr, pcy - pr, pcx + pr, pcy + pr, w, h)
        x0, y0, x1, y1 = box
        yy, xx = np.mgrid[y0:y1, x0:x1]
        m = (xx + 0.5 - pcx) ** 2 + (yy + 0.5 - pcy) ** 2 <= pr * pr
        return box, m
    if isinstance(shape, dict) and "px" in shape:
        return rect_to_px(shape["px"], w, h, pixels=True), None
    if isinstance(shape, dict) and "rect" in shape:
        return rect_to_px(shape["rect"], w, h), None
    if isinstance(shape, dict):
        raise ValueError("region dict needs one of %s, got keys %s" % ("/".join(REGION_KEYS), sorted(shape)))
    return rect_to_px(shape, w, h), None


def region_warning(shape: Any) -> Optional[str]:
    """A note for a fractional region with values far outside 0..1 (they are clamped, never reread as pixels)."""
    try:
        if isinstance(shape, dict) and "rects" in shape:
            vals = [v for r in shape["rects"] for v in _numbers(r, 4, "rectangle")]
        elif isinstance(shape, dict) and "circle" in shape:
            vals = _numbers(shape["circle"], 3, "circle")[:2]
        elif isinstance(shape, dict) and "rect" in shape:
            vals = _numbers(shape["rect"], 4, "rectangle")
        elif isinstance(shape, dict):
            return None
        else:
            vals = _numbers(shape, 4, "rectangle")
    except (TypeError, ValueError):
        return None
    lo, hi = -FAR_OUTSIDE_FRACTION, 1.0 + FAR_OUTSIDE_FRACTION
    if any(v < lo or v > hi for v in vals):
        return ("fractional region %s has values far outside 0..1; they were clamped to the frame. Pixel "
                "coordinates must be written as {\"px\": [x0, y0, x1, y1]}" % (shape,))
    return None


def region_size_error(box: Tuple[int, int, int, int], mask: Optional[np.ndarray],
                      min_px: int = MIN_REGION_PX) -> Optional[str]:
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    if bw < min_px or bh < min_px:
        return "region is %dx%d px inside the frame, below the %dx%d px minimum" % (bw, bh, min_px, min_px)
    n = int(mask.sum()) if mask is not None else bw * bh
    if n < min_px * min_px:
        return "region covers %d px inside the frame, below the %d px minimum" % (n, min_px * min_px)
    return None


def resolve_region(shape: Any, w: int, h: int, min_px: int = MIN_REGION_PX
                   ) -> Tuple[Tuple[int, int, int, int], Optional[np.ndarray], Optional[str]]:
    """region_mask plus validation: (box, mask, error). ``error`` is None when the region is usable; otherwise the
    caller must report it and fail the check (an empty or tiny region must never pass a judged check)."""
    try:
        box, m = region_mask(shape, w, h)
    except (TypeError, ValueError, KeyError) as e:
        return (0, 0, 0, 0), None, "bad region %r: %s" % (shape, e)
    err = region_size_error(box, m, min_px)
    if err:
        note = region_warning(shape)
        err = "region %r: %s" % (shape, err) + (("; " + note) if note else "")
    return box, m, err


def normalise_regions(spec: Any, prefix: str = "region") -> Dict[str, Any]:
    """Accept a dict name->shape, a list of shapes, or a single shape; return dict name->shape."""
    if spec is None:
        return {}
    if isinstance(spec, dict) and any(k in spec for k in REGION_KEYS):
        return {prefix: spec}
    if isinstance(spec, dict):
        return dict(spec)
    if isinstance(spec, (list, tuple)) and len(spec) == 4 and all(isinstance(v, (int, float)) for v in spec):
        return {prefix: list(spec)}
    out = {}
    for i, s in enumerate(spec):
        out["%s%d" % (prefix, i)] = s
    return out


# ----------------------------------------------------------------------------------------------------------------------
# Beat / zone / threshold resolution
# ----------------------------------------------------------------------------------------------------------------------
_BEAT_FILE_RE = re.compile(r"beat0?(\d)", re.IGNORECASE)
_KS_RE = re.compile(r"KS(\d)([a-z]?)", re.IGNORECASE)


def infer_beat(tag: str, filename: str = "") -> Optional[int]:
    for s in (filename, tag):
        m = _BEAT_FILE_RE.search(os.path.basename(s or ""))
        if m:
            b = int(m.group(1))
            if 1 <= b <= 7:
                return b
    m = _KS_RE.search(tag or "") or _KS_RE.search(os.path.basename(filename or ""))
    if m:
        b = int(m.group(1))
        if 1 <= b <= 7:
            return b
    return None


def pose_z(pose: Any) -> Optional[float]:
    if not isinstance(pose, dict):
        return None
    for key in ("pos", "position", "eye", "p"):
        v = pose.get(key)
        if isinstance(v, (list, tuple)) and len(v) >= 3:
            return float(v[2])
        if isinstance(v, dict) and "z" in v:
            return float(v["z"])
    if "z" in pose:
        return float(pose["z"])
    return None


def beat7_zone(shot: Dict[str, Any]) -> str:
    """Beat 7 has two threshold rows. Explicit shot["zone"] wins; then the key-shot tag; then pose z (the mouth is at
    z = 0, the tunnel at z > 0); then the word 'tunnel' in the tag; default 'outside'."""
    z = shot.get("zone")
    if isinstance(z, str) and z.lower() in ("tunnel", "outside"):
        return z.lower()
    tag = str(shot.get("tag", ""))
    m = _KS_RE.search(tag)
    if m and m.group(1) == "7" and m.group(2):
        return "tunnel" if m.group(2).lower() == "a" else "outside"
    pz = pose_z(shot.get("pose"))
    if pz is not None:
        return "tunnel" if pz > 0.0 else "outside"
    if "tunnel" in tag.lower() or "tunnel" in str(shot.get("file", "")).lower():
        return "tunnel"
    return "outside"


def threshold_key(beat: Optional[int], shot: Dict[str, Any]) -> Optional[Tuple[int, Optional[str]]]:
    if beat is None or beat < 1 or beat > 7:
        return None
    if beat == 7:
        return (7, beat7_zone(shot))
    return (beat, None)


def band_limit_px(height: int, base_1080: float = BAND_LIMIT_PX_1080) -> float:
    """Banding run limit scaled with the frame height (12 px at 1080p -> 24 px at 2160p)."""
    return base_1080 * (float(height) / 1080.0)


_DOORWAY_SHOT_RE = re.compile(r"(?:^|_)" + DOORWAY_SHOT + r"(?:_|$)", re.IGNORECASE)


def is_doorway_shot(shot: Dict[str, Any]) -> bool:
    """True for a KS1b shot (any resolution): its base tag, tag or file name contains the token 'KS1b' ('KS1' and
    'KS1_gallery_start' do not match). Only KS1b sees the gallery doorway; KS1's view of it is blocked by the sloped
    stair ceiling, so the doorway checks never apply there."""
    for key in ("base_tag", "tag"):
        v = shot.get(key)
        if isinstance(v, str) and v:
            return bool(_DOORWAY_SHOT_RE.search(v))
    stem = os.path.splitext(os.path.basename(str(shot.get("file") or "")))[0]
    return bool(_DOORWAY_SHOT_RE.search(stem))


def default_band_regions(tkey: Optional[Tuple[int, Optional[str]]], shot: Dict[str, Any]) -> Dict[str, Any]:
    """The 14.10 default gradient regions of a shot without its own "banding" regions. Beat 1: the doorway glow is
    KS1b's; any other beat-1 shot keeps the same rectangle under the name "gallery_centre"."""
    if not tkey:
        return {}
    regs = dict(DEFAULT_BAND_REGIONS.get(tkey, {}))
    if tkey == (1, None) and not is_doorway_shot(shot) and "doorway_glow" in regs:
        regs = {"gallery_centre": regs["doorway_glow"]}
    return regs


def _project(p: Sequence[float], eye: Sequence[float], vfov: float, aspect: float) -> Tuple[float, float]:
    """Pinhole projection for a camera at eye looking +Z (yaw 0, pitch 0): (u, v) fractions, top-left origin."""
    t = math.tan(math.radians(0.5 * vfov))
    x, y, z = p[0] - eye[0], p[1] - eye[1], max(1e-6, p[2] - eye[2])
    return 0.5 + 0.5 * x / (z * t * aspect), 0.5 - 0.5 * y / (z * t)


def default_doorway_region(width: int, height: int) -> List[float]:
    """Clear aperture of the doorway as seen from the KS1b pose at the image aspect (same rule as KeyShots.cs): the
    intersection of the projected inner and outer planes of the 1.2 x 2.0 opening, inset by 5 % of its size per side."""
    aspect = float(width) / float(max(1, height))
    hw = 0.5 * DOORWAY_W
    rects = []
    for z in (DOORWAY_Z_OUT - DOORWAY_DEPTH, DOORWAY_Z_OUT):
        u0, v0 = _project((-hw, DOORWAY_FLOOR_Y + DOORWAY_H, z), KS1B_EYE, KS1B_VFOV, aspect)
        u1, v1 = _project((hw, DOORWAY_FLOOR_Y, z), KS1B_EYE, KS1B_VFOV, aspect)
        rects.append((u0, v0, u1, v1))
    l, t = max(r[0] for r in rects), max(r[1] for r in rects)
    r_, b = min(r[2] for r in rects), min(r[3] for r in rects)
    ix, iy = 0.05 * (r_ - l), 0.05 * (b - t)
    return [_clamp01(l + ix), _clamp01(t + iy), _clamp01(r_ - ix), _clamp01(b - iy)]


# ----------------------------------------------------------------------------------------------------------------------
# Metrics
# ----------------------------------------------------------------------------------------------------------------------
def luma_stats(Y: np.ndarray, mask: Optional[np.ndarray] = None) -> Dict[str, float]:
    v = Y[mask] if mask is not None else Y.ravel()
    if v.size == 0:
        return {"mean": float("nan"), "p1": float("nan"), "p50": float("nan"), "p99": float("nan"),
                "min": float("nan"), "max": float("nan")}
    p1, p50, p99 = np.percentile(v, [1.0, 50.0, 99.0])
    return {"mean": float(v.mean()), "p1": float(p1), "p50": float(p50), "p99": float(p99),
            "min": float(v.min()), "max": float(v.max())}


def clipped_pct(rgb8: np.ndarray) -> float:
    return float((rgb8 >= CLIP_LEVEL_8BIT).any(axis=2).mean() * 100.0)


def magenta_mask(rgb8: np.ndarray) -> np.ndarray:
    """Pixels that look like Unity's error shader (1,0,1) after post: strong equal R and B, almost no G."""
    f = rgb8.astype(np.int16)
    r, g, b = f[:, :, 0], f[:, :, 1], f[:, :, 2]
    mn = np.minimum(r, b)
    mx = np.maximum(r, b)
    return (mn >= 128) & (g * 4 <= mn) & (np.abs(r - b) * 10 <= mx * 3)


def black_white_frame(Y: np.ndarray) -> Tuple[bool, bool]:
    """Black frame: 99 % of the pixels at most 2 levels. White frame: 99 % of the pixels at least 0.95."""
    p1, p99 = np.percentile(Y, [1.0, 99.0])
    return bool(p99 <= 2.0 / 255.0), bool(p1 >= 0.95)


def _shifted_views(a: np.ndarray) -> List[np.ndarray]:
    p = np.pad(a, 1, mode="edge")
    h, w = a.shape
    out = []
    for dy in (0, 1, 2):
        for dx in (0, 1, 2):
            if dy == 1 and dx == 1:
                continue
            out.append(p[dy:dy + h, dx:dx + w])
    return out


def nan_like_specks(rgb8: np.ndarray, Y: Optional[np.ndarray] = None, isolation: float = 0.4,
                    max_samples: int = 20) -> Dict[str, Any]:
    """Isolated extreme pixels (NaN / Inf symptoms, 10.11): pure white (all channels >= 250), pure black (all <= 5)
    or magenta pixels whose luma differs by more than ``isolation`` from EVERY one of their 8 neighbours. Real lights
    have anti-aliased edges and bloom halos, so a lone extreme pixel with no neighbour within 0.4 is not content."""
    if Y is None:
        Y = luma(rgb8)
    white = (rgb8 >= 250).all(axis=2)
    black = (rgb8 <= 5).all(axis=2)
    mag = magenta_mask(rgb8)
    extreme = white | black | mag
    if not extreme.any():
        return {"count": 0, "white": 0, "black": 0, "magenta": 0, "samples": []}
    mind = np.full(Y.shape, np.inf, dtype=np.float32)
    for v in _shifted_views(Y):
        np.minimum(mind, np.abs(Y - v), out=mind)
    specks = extreme & (mind > isolation)
    ys, xs = np.nonzero(specks)
    samples = [[int(x), int(y)] for y, x in zip(ys[:max_samples], xs[:max_samples])]
    return {"count": int(specks.sum()), "white": int((specks & white).sum()), "black": int((specks & black).sum()),
            "magenta": int((specks & mag).sum()), "samples": samples}


def longest_runs(A: np.ndarray, exclude_clamp: bool = True) -> Tuple[np.ndarray, np.ndarray]:
    """For a 2-D uint8 array, per column: (longest run length of identical values along axis 0, end row index of
    that run). Runs of 0 or 255 are excluded when ``exclude_clamp`` (each such pixel counts as a run of 1)."""
    h = A.shape[0]
    if h < 2:
        return np.ones(A.shape[1], dtype=np.int32), np.zeros(A.shape[1], dtype=np.int32)
    eq = A[1:] == A[:-1]
    if exclude_clamp:
        eq &= (A[1:] != 0) & (A[1:] != 255)
    idx = np.arange(1, h, dtype=np.int32)[:, None]
    last_reset = np.maximum.accumulate(np.where(eq, 0, idx), axis=0)
    run = idx - last_reset           # identical neighbours ending at row k+1
    end = run.argmax(axis=0)
    best = run[end, np.arange(A.shape[1])] + 1
    return best.astype(np.int32), (end + 1).astype(np.int32)


def _plane_runs(P: np.ndarray, M: Optional[np.ndarray]) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """Per column of plane P (uint8): (total change, longest identical run, end row of that run, its value).
    Pixels outside the optional mask M get an alternating negative sentinel so they never form runs and are ignored
    by the range."""
    cols = np.arange(P.shape[1])
    if M is None:
        rng = P.max(axis=0).astype(np.int16) - P.min(axis=0).astype(np.int16)
        best, endrow = longest_runs(P)
        return rng, best, endrow, P[endrow, cols].astype(np.int16)
    P16 = P.astype(np.int16)
    A = np.where(M, P16, (-1 - (np.arange(P.shape[0])[:, None] % 2)).astype(np.int16))
    hi = np.where(M, P16, np.int16(-1)).max(axis=0)
    lo = np.where(M, P16, np.int16(999)).min(axis=0)
    rng = np.where(hi >= 0, hi - lo, 0)
    h = A.shape[0]
    if h < 2:
        return rng, np.ones(P.shape[1], np.int32), np.zeros(P.shape[1], np.int32), A[0]
    eq = (A[1:] == A[:-1]) & (A[1:] > 0) & (A[1:] != 255)     # > 0 drops both the clamp value 0 and the sentinels
    idx = np.arange(1, h, dtype=np.int32)[:, None]
    last = np.maximum.accumulate(np.where(eq, 0, idx), axis=0)
    run = idx - last
    end = run.argmax(axis=0)
    best = (run[end, cols] + 1).astype(np.int32)
    endrow = (end + 1).astype(np.int32)
    return rng, best, endrow, A[endrow, cols]


# --- BandScore refinement (critique R1-38; the 12 px / 24 px limits are unchanged) ------------------------------------
# The raw rule (longest identical run on any vertical line whose total change exceeds 24 levels) also counts flat
# plateaus that merely share a line with an unrelated hard edge (a flat window panel, a rib face, a deck stripe), and
# single lines where a dither happened to repeat. A run over the limit now counts as a band only when it sits inside a
# monotone gentle gradient segment and is confirmed by neighbouring lines:
#   * no edge (change above BAND_EDGE_LEVELS across 4 px of the 5-line average) within BAND_EDGE_GUARD_PX of the run;
#   * the smoothed values just before and just after the run differ by at least BAND_MIN_CONTEXT_STEP levels and the run
#     value lies between them (a step of a gradient, not a plateau at a local maximum or minimum), with a local slope
#     under BAND_MAX_SLOPE levels per pixel (1 level per 4 px);
#   * at least BAND_CONFIRM_LINES adjacent lines carry such a run over the limit at a common row.
# The raw longest run stays in the result as ``raw_max_run`` (information).
BAND_EDGE_LEVELS = 6.0
BAND_EDGE_GUARD_PX = 8
BAND_MAX_SLOPE = 0.25
BAND_MIN_CONTEXT_STEP = 1.0
BAND_CONFIRM_LINES = 5
BAND_CONTEXT_NEAR, BAND_CONTEXT_FAR = 4, 12

# --- Encode-test band rule (DEVELOPMENT.md "Encode test and dither decision", calibration clause of 14.10) -------------
# A decoded run longer than the encode limit counts as a band only if it ends in a step of at least ENCODE_STEP_LEVELS
# levels that the source does not have (source change across the same boundary under ENCODE_SRC_EDGE_LEVELS: a step the
# encoder made, not an object edge), or if it is longer than max(limit, ENCODE_PLATEAU_FACTOR / local slope), the local
# slope (levels per pixel) being a least-squares fit on the dithered source frame over the run +/- ENCODE_SLOPE_HALF_PX,
# cut at source edges (BAND_EDGE_LEVELS), i.e. longer than the plateau the true gradient itself implies.
ENCODE_STEP_LEVELS = 2
ENCODE_SRC_EDGE_LEVELS = 2.0
ENCODE_SLOPE_HALF_PX = 48
ENCODE_PLATEAU_FACTOR = 1.5


def _avg5_lines(P: np.ndarray) -> np.ndarray:
    """Average of each line with its two neighbours on either side (lines are the columns of P), edge-clamped."""
    w = P.shape[1]
    pad = np.pad(P.astype(np.float64), ((0, 0), (2, 2)), mode="edge")
    c = np.concatenate([np.zeros((P.shape[0], 1)), np.cumsum(pad, axis=1)], axis=1)
    return (c[:, 5:5 + w] - c[:, 0:w]) / 5.0


def _prefix_rows(A: np.ndarray) -> np.ndarray:
    """Prefix sums along the rows with a leading zero row: S[k] = sum of A[0:k]."""
    return np.concatenate([np.zeros((1, A.shape[1])), np.cumsum(A, axis=0, dtype=np.float64)], axis=0)


def _edge_rows(S5: np.ndarray, levels: float) -> np.ndarray:
    """Boolean map: an edge is centred at row y when |S5[y + 2] - S5[y - 2]| exceeds ``levels``."""
    E = np.zeros(S5.shape, dtype=bool)
    if S5.shape[0] > 4:
        E[2:-2] = np.abs(S5[4:] - S5[:-4]) > levels
    return E


def _run_table(A: np.ndarray) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """For a 2-D int array (runs along axis 0; values <= 0 and 255 never form runs): per pixel the start row of its
    run, the run length up to that pixel, and whether the run ends at this row."""
    h, w = A.shape
    start = np.zeros((h, w), dtype=np.int32)
    endflag = np.ones((h, w), dtype=bool)
    if h > 1:
        eq = (A[1:] == A[:-1]) & (A[1:] > 0) & (A[1:] != 255)
        idx = np.arange(1, h, dtype=np.int32)[:, None]
        start[1:] = np.maximum.accumulate(np.where(eq, 0, idx), axis=0)
        endflag[:-1] = ~eq
    length = np.arange(h, dtype=np.int32)[:, None] - start + 1
    return start, length, endflag


def _hlen(B: np.ndarray) -> np.ndarray:
    """Length of the horizontal (axis 1) run of True values each True pixel belongs to (0 where False)."""
    idx = np.arange(B.shape[1], dtype=np.int32)[None, :]
    left = idx - np.maximum.accumulate(np.where(B, -1, idx), axis=1)          # Trues ending at x
    Br = B[:, ::-1]
    right = (idx - np.maximum.accumulate(np.where(Br, -1, idx), axis=1))[:, ::-1]   # Trues starting at x
    return np.where(B, left + right - 1, 0)


def _window_mean(C: np.ndarray, lo: np.ndarray, hi: np.ndarray, cols: np.ndarray) -> np.ndarray:
    """Mean of rows [lo, hi) of the column ``cols`` from the prefix sums C (NaN where the window is empty)."""
    n = hi - lo
    s = C[np.maximum(hi, lo), cols] - C[lo, cols]
    return np.where(n > 0, s / np.maximum(n, 1), np.nan)


def _plane_bands(P: np.ndarray, M: Optional[np.ndarray], change_levels: int, limit: float
                 ) -> Dict[str, Any]:
    """Raw and confirmed band runs of one plane (runs along axis 0, lines = columns). See BAND_* above."""
    h, w = P.shape
    P16 = P.astype(np.int16)
    if M is None:
        A = P16
        rng = P.max(axis=0).astype(np.int16) - P.min(axis=0).astype(np.int16)
    else:
        A = np.where(M, P16, (-1 - (np.arange(h)[:, None] % 2)).astype(np.int16))
        hi_ = np.where(M, P16, np.int16(-1)).max(axis=0)
        lo_ = np.where(M, P16, np.int16(999)).min(axis=0)
        rng = np.where(hi_ >= 0, hi_ - lo_, 0)
    q = rng > change_levels
    start, length, endflag = _run_table(A)
    valid = (A > 0) & (A != 255)
    ends = endflag & valid & q[None, :]
    # raw per-line longest run (the plain 14.10 rule, kept as information)
    lens = np.where(ends, length, 0)
    raw_best = lens.max(axis=0)
    raw_row = lens.argmax(axis=0)
    out: Dict[str, Any] = {"q": q, "raw_best": raw_best, "raw_end": raw_row, "A": A, "cands": []}
    cand = ends & (length > limit)
    ks, xs = np.nonzero(cand)
    if ks.size == 0:
        return out
    ss = start[ks, xs]
    L = length[ks, xs]
    v = A[ks, xs].astype(np.float64)
    S5 = _avg5_lines(P)
    C0 = _prefix_rows(S5)
    E = _edge_rows(S5, BAND_EDGE_LEVELS)
    CE = _prefix_rows(E.astype(np.float64))
    g0 = np.clip(ss - BAND_EDGE_GUARD_PX, 0, h)
    g1 = np.clip(ks + 1 + BAND_EDGE_GUARD_PX, 0, h)
    no_edge = (CE[g1, xs] - CE[g0, xs]) == 0
    a = _window_mean(C0, np.clip(ss - BAND_CONTEXT_FAR, 0, h), np.clip(ss - BAND_CONTEXT_NEAR, 0, h), xs)
    b = _window_mean(C0, np.clip(ks + 1 + BAND_CONTEXT_NEAR, 0, h), np.clip(ks + 1 + BAND_CONTEXT_FAR, 0, h), xs)
    a = np.where(np.isnan(a), v, a)
    b = np.where(np.isnan(b), v, b)
    lo_c, hi_c = np.minimum(a, b), np.maximum(a, b)
    step = hi_c - lo_c
    monotone = (step >= BAND_MIN_CONTEXT_STEP) & (v >= lo_c - 0.5) & (v <= hi_c + 0.5)
    gentle = step / (L + 2.0 * BAND_CONTEXT_FAR) < BAND_MAX_SLOPE
    keep = no_edge & monotone & gentle
    # confirmation: at least BAND_CONFIRM_LINES adjacent lines with a kept run over the limit at a common row
    if keep.any():
        paint = np.zeros((h, w), dtype=bool)
        for s_, k_, x_ in zip(ss[keep], ks[keep], xs[keep]):
            paint[s_:k_ + 1, x_] = True
        hl = _hlen(paint)
        conf = np.zeros(ks.size, dtype=bool)
        for i in np.nonzero(keep)[0]:
            conf[i] = hl[ss[i]:ks[i] + 1, xs[i]].max() >= BAND_CONFIRM_LINES
    else:
        conf = np.zeros(ks.size, dtype=bool)
    out["cands"] = [(int(L[i]), int(xs[i]), int(ss[i]), int(v[i]), bool(keep[i]), bool(conf[i]))
                    for i in range(ks.size)]
    out["n_kept"] = int(keep.sum())
    out["n_confirmed"] = int(conf.sum())
    return out


def banding_score(rgb8: np.ndarray, box: Tuple[int, int, int, int], mask: Optional[np.ndarray] = None,
                  change_levels: int = BAND_CHANGE_LEVELS, axis: str = "v", channels: str = "rgb",
                  top_n: int = 5, limit_px: Optional[float] = None) -> Dict[str, Any]:
    """BandScore of 14.10 inside a box (optionally a mask inside it), refined per critique R1-38 (see BAND_* above).

    ``max_run`` is the longest *confirmed* band run (0 when none), judged against the limit (12 px at 1080p); the plain
    14.10 measurement (the longest identical run over all qualifying lines) is ``raw_max_run`` with its position under
    ``raw``. ``axis`` 'v' (vertical lines, the spec), 'h' (horizontal) or 'both'. ``channels`` 'rgb' (each channel
    separately; the default) or 'luma' (8-bit rounded Rec. 709 luma). ``limit_px`` defaults to the 1080p limit scaled
    with the frame height. Diagnostics: ``line_run_p50`` / ``line_run_p99`` (raw per-line longest runs over
    qualifying lines), ``lines_over_limit`` (raw), ``candidates`` (runs over the limit), ``kept`` (inside a monotone
    gentle gradient segment away from edges) and ``confirmed_runs``."""
    x0, y0, x1, y1 = box
    sub = rgb8[y0:y1, x0:x1]
    limit = float(limit_px) if limit_px is not None else band_limit_px(rgb8.shape[0])
    res: Dict[str, Any] = {"box_px": [x0, y0, x1, y1], "max_run": 0, "x": None, "y": None, "channel": None,
                           "axis": None, "value": None, "qualifying_lines": 0, "total_lines": 0, "worst": [],
                           "raw_max_run": 0, "raw": None, "line_run_p50": None, "line_run_p99": None,
                           "lines_over_limit": None, "candidates": 0, "kept": 0, "confirmed_runs": 0,
                           "rule": "R1-38: monotone gradient segment, %g px from edges > %g levels, slope < %g, "
                                   "confirmed by %d adjacent lines" % (BAND_EDGE_GUARD_PX, BAND_EDGE_LEVELS,
                                                                       BAND_MAX_SLOPE, BAND_CONFIRM_LINES)}
    line_maxima: List[np.ndarray] = []
    if sub.size == 0 or sub.shape[0] < 2 or sub.shape[1] < 1:
        res["error"] = "empty region (box %s)" % ([x0, y0, x1, y1],)   # callers must fail, never pass, on this
        return res
    if channels == "luma":
        planes = [("Y", np.clip(np.rint(luma(sub) * 255.0), 0, 255).astype(np.uint8))]
    else:
        planes = [("R", sub[:, :, 0]), ("G", sub[:, :, 1]), ("B", sub[:, :, 2])]
    axes = ["v", "h"] if axis == "both" else [axis]
    conf: List[Tuple[int, int, int, str, str, int]] = []   # (run, x, y_start, channel, axis, value)
    raw: List[Tuple[int, int, int, str, str, int]] = []

    def pos(ax: str, line: int, along: int) -> Tuple[int, int]:
        return (x0 + line, y0 + along) if ax == "v" else (x0 + along, y0 + line)

    for ax in axes:
        for cname, P in planes:
            M = mask
            if ax == "h":
                P = P.T
                M = None if mask is None else mask.T
            r = _plane_bands(P, M, change_levels, limit)
            q = r["q"]
            res["total_lines"] += int(P.shape[1])
            res["qualifying_lines"] += int(q.sum())
            if not q.any():
                continue
            best = r["raw_best"]
            line_maxima.append(best[q])
            li = int(np.argmax(np.where(q, best, 0)))
            if best[li] > 0:
                px, py = pos(ax, li, int(r["raw_end"][li]) - int(best[li]) + 1)
                raw.append((int(best[li]), px, py, cname, ax, int(r["A"][int(r["raw_end"][li]), li])))
            res["candidates"] += len(r["cands"])
            res["kept"] += int(r.get("n_kept", 0))
            res["confirmed_runs"] += int(r.get("n_confirmed", 0))
            for (L, x, s, v, _k, c) in r["cands"]:
                if c:
                    px, py = pos(ax, x, s)
                    conf.append((L, px, py, cname, ax, v))
    if line_maxima:
        lm = np.concatenate(line_maxima)
        p50, p99 = np.percentile(lm, [50.0, 99.0])
        res["line_run_p50"], res["line_run_p99"] = float(p50), float(p99)
        res["lines_over_limit"] = int((lm > limit).sum())
    if raw:
        raw.sort(key=lambda c: -c[0])
        rr = raw[0]
        res["raw_max_run"] = rr[0]
        res["raw"] = {"run": rr[0], "x": rr[1], "y": rr[2], "channel": rr[3], "axis": rr[4], "value": rr[5]}
    if conf:
        conf.sort(key=lambda c: -c[0])
        c0 = conf[0]
        res.update({"max_run": c0[0], "x": c0[1], "y": c0[2], "channel": c0[3], "axis": c0[4], "value": c0[5]})
        res["worst"] = [{"run": c[0], "x": c[1], "y": c[2], "channel": c[3], "axis": c[4], "value": c[5]}
                        for c in conf[:top_n]]
    return res


def _neighbour_step(A: np.ndarray, rows: np.ndarray, cols: np.ndarray, v: np.ndarray, direction: int) -> np.ndarray:
    """|level of the neighbouring plateau - v| beyond a run boundary: the median of up to 4 valid pixels (value >= 0)
    beyond ``rows`` in ``direction`` (-1 above the run start, +1 below the run end); 0 where there is none."""
    h = A.shape[0]
    vals = np.full((rows.size, 4), np.nan)
    for k in range(4):
        r = rows + direction * (k + 1)
        ok = (r >= 0) & (r < h)
        a = A[np.clip(r, 0, h - 1), cols].astype(np.float64)
        vals[:, k] = np.where(ok & (a >= 0), a, np.nan)
    with np.errstate(all="ignore"):
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", RuntimeWarning)
            med = np.nanmedian(vals, axis=1)
    return np.where(np.isnan(med), 0.0, np.abs(med - v))


def encode_band_runs(dec8: np.ndarray, src8: Optional[np.ndarray], box: Tuple[int, int, int, int],
                     mask: Optional[np.ndarray], limit_px: float, change_levels: int = BAND_CHANGE_LEVELS
                     ) -> Dict[str, Any]:
    """Encode-test bands of one decoded frame against its dithered source frame (ENCODE_* rule above), vertical lines,
    R, G and B separately. Returns ``band_max_run`` (longest run that counts as a band, 0 = none; judged), its
    position and reason, ``band_runs`` (count), ``raw_max_run`` (longest run over the limit or not, information) and
    ``candidates`` (runs over the limit). Without a source frame every run over the limit counts (the plain rule)."""
    x0, y0, x1, y1 = box
    res: Dict[str, Any] = {"box_px": [x0, y0, x1, y1], "band_max_run": 0, "band_runs": 0, "band_by_step": 0,
                           "band_by_length": 0, "raw_max_run": 0, "candidates": 0, "where": None, "raw_where": None,
                           "qualifying_lines": 0}
    D3 = dec8[y0:y1, x0:x1]
    S3 = src8[y0:y1, x0:x1] if src8 is not None else None
    if D3.size == 0 or D3.shape[0] < 2:
        res["error"] = "empty region (box %s)" % ([x0, y0, x1, y1],)
        return res
    h, w = D3.shape[:2]
    best_band: Optional[Tuple[int, int, int, str, str]] = None
    for ci, cname in enumerate("RGB"):
        P = D3[:, :, ci]
        P16 = P.astype(np.int16)
        if mask is None:
            A = P16
            rng = P.max(axis=0).astype(np.int16) - P.min(axis=0).astype(np.int16)
        else:
            A = np.where(mask, P16, (-1 - (np.arange(h)[:, None] % 2)).astype(np.int16))
            hi_ = np.where(mask, P16, np.int16(-1)).max(axis=0)
            lo_ = np.where(mask, P16, np.int16(999)).min(axis=0)
            rng = np.where(hi_ >= 0, hi_ - lo_, 0)
        q = rng > change_levels
        res["qualifying_lines"] += int(q.sum())
        if not q.any():
            continue
        start, length, endflag = _run_table(A)
        valid = (A > 0) & (A != 255)
        ends = endflag & valid & q[None, :]
        lens = np.where(ends, length, 0)
        if lens.max() > res["raw_max_run"]:
            k, x = np.unravel_index(int(np.argmax(lens)), lens.shape)
            res["raw_max_run"] = int(lens[k, x])
            res["raw_where"] = {"x": x0 + int(x), "y": y0 + int(start[k, x]), "channel": cname}
        cand = ends & (length > limit_px)
        ks, xs = np.nonzero(cand)
        if ks.size == 0:
            continue
        res["candidates"] += int(ks.size)
        ss = start[ks, xs]
        L = length[ks, xs].astype(np.float64)
        v = A[ks, xs].astype(np.int32)
        # boundary steps in the decoded frame: the run value against the level of the neighbouring plateau (median of the
        # up to 4 valid pixels beyond the boundary, so a one-pixel ringing overshoot is not a step); 0 where unknown
        st = _neighbour_step(A, ss, xs, v, -1)
        sb = _neighbour_step(A, ks, xs, v, +1)
        if S3 is None:
            band = np.ones(ks.size, dtype=bool)
            by_step = np.zeros(ks.size, dtype=bool)
        else:
            S5 = _avg5_lines(S3[:, :, ci])
            C0 = _prefix_rows(S5)
            yy = np.arange(h, dtype=np.float64)[:, None]
            C1 = _prefix_rows(S5 * yy)
            E = _edge_rows(S5, BAND_EDGE_LEVELS)
            rows = np.arange(h, dtype=np.int32)[:, None]
            prev_e = np.maximum.accumulate(np.where(E, rows, -10 ** 6), axis=0)
            next_e = np.minimum.accumulate(np.where(E, rows, 10 ** 6)[::-1], axis=0)[::-1]
            # source change across each boundary: mean of 4 rows after minus 4 rows before (5-line average)

            def src_change(bd: np.ndarray) -> np.ndarray:
                after = _window_mean(C0, bd, np.clip(bd + 4, 0, h), xs)
                before = _window_mean(C0, np.clip(bd - 4, 0, h), bd, xs)
                return np.abs(np.nan_to_num(after - before, nan=99.0))

            ct = src_change(np.clip(ss, 0, h))
            cb = src_change(np.clip(ks + 1, 0, h))
            # a boundary within BAND_EDGE_GUARD_PX of a source edge (> BAND_EDGE_LEVELS) is the encoder's ringing around an
            # object edge, not a band contour (the same guard as the still BandScore, R1-38)
            CE = _prefix_rows(E.astype(np.float64))

            def near_edge(bd: np.ndarray) -> np.ndarray:
                a0 = np.clip(bd - BAND_EDGE_GUARD_PX, 0, h)
                a1 = np.clip(bd + BAND_EDGE_GUARD_PX, 0, h)
                return (CE[a1, xs] - CE[a0, xs]) > 0

            et = near_edge(np.clip(ss, 0, h))
            eb = near_edge(np.clip(ks + 1, 0, h))
            by_step = ((st >= ENCODE_STEP_LEVELS) & (ct < ENCODE_SRC_EDGE_LEVELS) & ~et) | \
                      ((sb >= ENCODE_STEP_LEVELS) & (cb < ENCODE_SRC_EDGE_LEVELS) & ~eb)
            # local slope: least squares over the run +/- 48 px, the window cut at source edges outside the run
            lo = np.clip(ss - ENCODE_SLOPE_HALF_PX, 0, h)
            hi = np.clip(ks + 1 + ENCODE_SLOPE_HALF_PX, 0, h)
            pe = np.where(ss > 0, prev_e[np.maximum(ss - 1, 0), xs], -10 ** 6)
            ne = np.where(ks + 1 < h, next_e[np.minimum(ks + 1, h - 1), xs], 10 ** 6)
            lo = np.where(pe >= lo, pe + 3, lo)
            hi = np.where(ne < hi, ne - 2, hi)
            lo = np.minimum(lo, ss)
            hi = np.maximum(hi, ks + 1)
            n = (hi - lo).astype(np.float64)
            sp = C0[hi, xs] - C0[lo, xs]
            syp = C1[hi, xs] - C1[lo, xs]
            lo_f, hi_f = lo.astype(np.float64), hi.astype(np.float64)
            sy = (lo_f + hi_f - 1.0) * n / 2.0

            def f2(m: np.ndarray) -> np.ndarray:
                return (m - 1.0) * m * (2.0 * m - 1.0) / 6.0

            sy2 = f2(hi_f) - f2(lo_f)
            den = n * sy2 - sy * sy
            slope = np.abs(np.where(den > 0, (n * syp - sy * sp) / np.where(den > 0, den, 1.0), 0.0))
            plateau = ENCODE_PLATEAU_FACTOR / np.maximum(slope, 1e-9)
            by_len = L > np.maximum(limit_px, plateau)
            band = by_step | by_len
            res["band_by_length"] += int(by_len.sum())
        res["band_by_step"] += int(by_step.sum())
        res["band_runs"] += int(band.sum())
        if band.any():
            i = int(np.argmax(np.where(band, L, 0)))
            if best_band is None or L[i] > best_band[0]:
                reason = "step" if by_step[i] else "length"
                best_band = (int(L[i]), x0 + int(xs[i]), y0 + int(ss[i]), cname, reason)
    if best_band is not None:
        res["band_max_run"] = best_band[0]
        res["where"] = {"x": best_band[1], "y": best_band[2], "channel": best_band[3], "reason": best_band[4]}
    return res


def mean_rgb8(rgb8: np.ndarray, box: Tuple[int, int, int, int], mask: Optional[np.ndarray] = None) -> List[float]:
    x0, y0, x1, y1 = box
    sub = rgb8[y0:y1, x0:x1].reshape(-1, 3) if mask is None else rgb8[y0:y1, x0:x1][mask]
    if sub.size == 0:
        return [float("nan")] * 3
    m = sub.astype(np.float64).mean(axis=0)
    return [float(m[0]), float(m[1]), float(m[2])]


def horizon_agreement(rgb8: np.ndarray, regions: Dict[str, Any], tol_levels: float = HORIZON_AGREE_LEVELS
                      ) -> Optional[Dict[str, Any]]:
    """Fog vs sky vs backdrop horizon colour agreement (10.13, 14.10). ``regions`` may hold 'horizon_fog',
    'horizon_sky', 'horizon_backdrop' (any two or more). Mean 8-bit sRGB colour per region; the largest per-channel
    difference over all pairs must be within ``tol_levels``. A region that is malformed or smaller than
    MIN_REGION_PX fails the check with an error (its mean would be NaN, which compares as "no difference")."""
    h, w = rgb8.shape[:2]
    keys = [k for k in ("horizon_fog", "horizon_sky", "horizon_backdrop") if k in regions]
    if len(keys) < 2:
        return None
    cols: Dict[str, List[float]] = {}
    errors: Dict[str, str] = {}
    for key in keys:
        box, m, err = resolve_region(regions[key], w, h)
        if err:
            errors[key[8:]] = err
        else:
            cols[key[8:]] = mean_rgb8(rgb8, box, m)
    if errors:
        return {"colours": cols, "errors": errors, "error": "; ".join("%s: %s" % kv for kv in errors.items()),
                "max_diff_levels": None, "worst_pair": None, "limit_levels": tol_levels, "pass": False}
    names = list(cols)
    worst = 0.0
    pair = None
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            d = max(abs(a - b) for a, b in zip(cols[names[i]], cols[names[j]]))
            if d > worst:
                worst, pair = d, (names[i], names[j])
    return {"colours": cols, "max_diff_levels": worst, "worst_pair": pair, "limit_levels": tol_levels,
            "pass": bool(worst <= tol_levels)}


def horizon_vs_reference(rgb8: np.ndarray, shape: Any, ref_rgb: Sequence[int] = OUT_HORIZ_RGB,
                         tol_levels: float = HORIZON_NOPOST_LEVELS) -> Dict[str, Any]:
    """-nopost beat-7 check: the mean colour of the horizon-row region equals the reference within tol levels."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "mean_rgb": None, "reference_rgb": list(ref_rgb), "max_diff_levels": None,
                "limit_levels": tol_levels, "pass": False}
    c = mean_rgb8(rgb8, box, m)
    d = max(abs(a - b) for a, b in zip(c, ref_rgb))
    return {"mean_rgb": c, "reference_rgb": list(ref_rgb), "max_diff_levels": d, "limit_levels": tol_levels,
            "pass": bool(d <= tol_levels)}


def box_blur(a: np.ndarray, r: int) -> np.ndarray:
    """Separable box blur of radius r (edge-clamped)."""
    if r <= 0:
        return a.astype(np.float32)
    k = 2 * r + 1
    p = np.pad(a.astype(np.float64), r, mode="edge")
    c = np.cumsum(p, axis=0)
    c = np.vstack([np.zeros((1, c.shape[1])), c])
    v = (c[k:] - c[:-k]) / k
    c = np.cumsum(v, axis=1)
    c = np.hstack([np.zeros((c.shape[0], 1)), c])
    return ((c[:, k:] - c[:, :-k]) / k).astype(np.float32)


def nadir_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """14.7.2: within 25 degrees of the nadir (region supplied by the harness) the abyss reads as FOG_DOWN:
    mean luma 0.041 +/- 0.008, no pixel above 0.06, no horizontal gradient edge above 0.03 (largest step between
    vertically adjacent pixels after a 3x3 box blur that removes the 1-1.5 LSB dither)."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"pass": False, "error": "nadir " + err, "mean_luma": None}
    x0, y0, x1, y1 = box
    Y = luma(rgb8[y0:y1, x0:x1])
    vals = Y[m] if m is not None else Y.ravel()
    B = box_blur(Y, 1)
    edge = np.abs(B[1:] - B[:-1])
    if m is not None:
        edge = edge[(m[1:] & m[:-1])]
    max_edge = float(edge.max()) if edge.size else 0.0
    mean = float(vals.mean())
    mx = float(vals.max())
    ok = abs(mean - NADIR_LUMA) <= NADIR_TOL and mx <= NADIR_MAX_PIXEL and max_edge <= NADIR_MAX_EDGE
    return {"mean_luma": mean, "target": NADIR_LUMA, "tol": NADIR_TOL, "max_pixel_luma": mx,
            "max_pixel_limit": NADIR_MAX_PIXEL, "max_horizontal_edge": max_edge, "edge_limit": NADIR_MAX_EDGE,
            "pass": bool(ok)}


def doorway_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """14.5.1 acceptance for KS1b (callers gate with ``is_doorway_shot``): the clear doorway aperture has mean luma
    0.40-0.60 (the calibrated band, lead decision D6; spec text 0.65-0.90) and under 0.3 % clipped pixels (any channel
    >= 0.98) inside it. A malformed or tiny region fails."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    res: Dict[str, Any] = {"region": shape, "box_px": list(box), "luma_range": list(DOORWAY_LUMA),
                           "luma_range_spec": list(DOORWAY_LUMA_SPEC),
                           "calibration": "DEVELOPMENT.md gallery row 'KS1b doorway luma' / lead decision D6 (R2)",
                           "clipped_max_pct": DOORWAY_CLIPPED_MAX_PCT}
    if err:
        res.update({"error": "doorway " + err, "mean_luma": None, "clipped_pct": None, "luma_pass": False,
                    "clipped_pass": False, "pass": False})
        return res
    x0, y0, x1, y1 = box
    sub = rgb8[y0:y1, x0:x1]
    px = sub.reshape(-1, 3) if m is None else sub[m]
    st = luma_stats(luma(px))
    clip = float((px >= CLIP_LEVEL_8BIT).any(axis=1).mean() * 100.0)
    lp = bool(DOORWAY_LUMA[0] <= st["mean"] <= DOORWAY_LUMA[1])
    cp = bool(clip < DOORWAY_CLIPPED_MAX_PCT)
    res.update({"mean_luma": st["mean"], "p1_luma": st["p1"], "p50_luma": st["p50"], "p99_luma": st["p99"],
                "clipped_pct": clip, "pixels": int(px.shape[0]), "luma_pass": lp, "clipped_pass": cp,
                "pass": lp and cp})
    return res


def aliasing_proxy(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """Static proxy for thin-line aliasing (informational only; the judged test is temporal, see
    sequence_metrics.py): mean absolute 4-neighbour Laplacian of luma in the region, in 8-bit levels."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"laplacian_mean_levels": None, "error": err}
    x0, y0, x1, y1 = box
    Y = luma(rgb8[y0:y1, x0:x1]) * 255.0
    L = np.abs(4 * Y[1:-1, 1:-1] - Y[:-2, 1:-1] - Y[2:, 1:-1] - Y[1:-1, :-2] - Y[1:-1, 2:])
    if m is not None:
        L = L[m[1:-1, 1:-1]]
    return {"laplacian_mean_levels": float(L.mean()) if L.size else None,
            "laplacian_p99_levels": float(np.percentile(L, 99)) if L.size else None}


# ----------------------------------------------------------------------------------------------------------------------
# 14.5 image acceptance checks (critique R1-29). Regions and data come from the harness (KeyShots.WriteAcceptanceRegions).
# ----------------------------------------------------------------------------------------------------------------------
SIDE_STRIP_MEAN_MAX = 0.06       # 14.5.2 KS2b: each 30 % side strip beyond the deck: mean luma at most 0.06,
SIDE_STRIP_PIXEL_MAX = 0.12      # no pixel above 0.12,
SIDE_STRIP_EDGE_MAX = 0.03       # no horizontal edge above 0.03 (3x3 blur removes the dither)
NEAR_BLACK_LEVEL = 6             # 14.2: nothing darker than linear 0.002 = sRGB 6.6/255 (max channel <= 6 is crushed)
NEAR_BLACK_MAX_PCT = 0.0         # outside 1-px crevices (a 2x2 opening removes structures thinner than 2 px)
WINDOWS_MIN = 1000               # 14.5.5 KS5a: at least 1 000 windows ...
WINDOW_CELL_PX = (8.0, 20.0)     # ... with cells of 8-20 px at 1080p
AMBER_DOTS_MIN = 40              # 14.5.7 KS7b: at least 40 amber dots (lower 66 %)
READABLE_CELLS_MIN = 150         # 14.5.7 KS7b: at least 150 readable cells in the lower 66 %
CELL_MIN_ROW_PX = 3.0            # readable: projected row height >= 3 px (1080p) ...
CELL_MIN_DIFF = 0.03             # ... and |interior - web ring| mean luma >= 0.03, sampled at 5 points
FAR_GLOW_P90_MIN = 0.25          # R1-16 KS1: the stair-head glow reads at about 0.25-0.40 (90th percentile of the region)
STIPPLE_LEVEL = 0.02             # advisory: isolated 1-px extrema of both signs (screen-door dither between two surfaces)
STIPPLE_TILE_MIN = 5             # ... at least this many of each sign in one 16 x 16 tile


def _components(M: np.ndarray, connectivity8: bool = True) -> List[Tuple[int, int, int]]:
    """Connected components of a sparse boolean mask: list of (size, y, x of a member). Pure Python on the True pixels
    (used for small masks: amber dots, changed blobs)."""
    pts = set(zip(*[a.tolist() for a in np.nonzero(M)]))
    nb = ((-1, 0), (1, 0), (0, -1), (0, 1)) + (((-1, -1), (-1, 1), (1, -1), (1, 1)) if connectivity8 else ())
    out = []
    while pts:
        y0, x0 = pts.pop()
        st = [(y0, x0)]
        n = 1
        while st:
            y, x = st.pop()
            for dy, dx in nb:
                q = (y + dy, x + dx)
                if q in pts:
                    pts.remove(q)
                    st.append(q)
                    n += 1
        out.append((n, y0, x0))
    return out


def side_strips_check(rgb8: np.ndarray, strips: Any) -> Dict[str, Any]:
    """14.5.2 KS2b acceptance: in each side strip (dict name -> region) mean luma <= 0.06, no pixel above 0.12 and no
    horizontal edge (vertical neighbour step after a 3x3 box blur) above 0.03."""
    h, w = rgb8.shape[:2]
    res: Dict[str, Any] = {"limits": {"mean_max": SIDE_STRIP_MEAN_MAX, "pixel_max": SIDE_STRIP_PIXEL_MAX,
                                      "edge_max": SIDE_STRIP_EDGE_MAX}, "strips": {}, "pass": True}
    for name, shape in normalise_regions(strips, "strip").items():
        box, m, err = resolve_region(shape, w, h)
        if err:
            res["strips"][name] = {"error": err, "pass": False}
            res["pass"] = False
            res["error"] = err
            continue
        x0, y0, x1, y1 = box
        Y = luma(rgb8[y0:y1, x0:x1])
        B = box_blur(Y, 1)
        edge = float(np.abs(B[1:] - B[:-1]).max()) if B.shape[0] > 1 else 0.0
        st = {"mean_luma": float(Y.mean()), "max_luma": float(Y.max()), "max_edge": edge,
              "pixels_above_max": int((Y > SIDE_STRIP_PIXEL_MAX).sum())}
        st["pass"] = bool(st["mean_luma"] <= SIDE_STRIP_MEAN_MAX and st["max_luma"] <= SIDE_STRIP_PIXEL_MAX
                          and edge <= SIDE_STRIP_EDGE_MAX)
        res["strips"][name] = st
        res["pass"] = res["pass"] and st["pass"]
    if not res["strips"]:
        res.update({"pass": False, "error": "no side strip regions"})
    vals = [s for s in res["strips"].values() if "mean_luma" in s]
    res["mean_luma_max"] = max((s["mean_luma"] for s in vals), default=None)
    res["max_luma"] = max((s["max_luma"] for s in vals), default=None)
    res["max_edge"] = max((s["max_edge"] for s in vals), default=None)
    return res


def near_black_check(rgb8: np.ndarray) -> Dict[str, Any]:
    """14.2 floor (critique R1-14 / R1-29): share of pixels whose largest channel is <= 6/255, counted only where the
    dark area is at least 2 x 2 px (a 2x2 opening removes 1-px crevices). Judged: 0 %."""
    dark = (rgb8.max(axis=2) <= NEAR_BLACK_LEVEL)
    core = dark[:-1, :-1] & dark[1:, :-1] & dark[:-1, 1:] & dark[1:, 1:]   # 2x2 erosion (top-left anchored)
    opened = np.zeros_like(dark)
    opened[:-1, :-1] |= core
    opened[1:, :-1] |= core
    opened[:-1, 1:] |= core
    opened[1:, 1:] |= core
    n = int(opened.sum())
    pct = 100.0 * n / float(dark.size)
    res: Dict[str, Any] = {"level": NEAR_BLACK_LEVEL, "pct": pct, "pixels": n, "raw_pct": 100.0 * float(dark.mean()),
                           "limit_pct": NEAR_BLACK_MAX_PCT, "pass": bool(pct <= NEAR_BLACK_MAX_PCT)}
    if n:
        ys, xs = np.nonzero(opened)
        res["bbox_px"] = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    return res


def window_grid_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """14.5.5 KS5a: at least 1 000 windows with cells of 8-20 px (1080p). Windows are compact dark (unlit) or bright
    (lit) blobs: local extrema of a band-pass luma (3x3 minus 11x11 box blur, beyond 0.010) within an 11 x 11 px
    neighbourhood whose border stays clear (at most 6 border pixels beyond half the threshold), counted only where they
    form a grid (at least two other windows 7-22 px away). The cell size is the median nearest-neighbour distance.
    Scaled with the frame height. Note: other content (bolts, grating) also yields such blobs; the check applies to KS5a."""
    from numpy.lib.stride_tricks import sliding_window_view as swv
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "windows": None}
    x0, y0, x1, y1 = box
    Y = luma(rgb8[y0:y1, x0:x1]).astype(np.float64)
    s = max(1, int(round(h / 1080.0)))
    T, R = 0.010, 5 * s
    D = (box_blur(Y, s) - box_blur(Y, 5 * s)).astype(np.float32)
    P = np.pad(D, R, mode="edge")
    V = swv(P, (2 * R + 1, 2 * R + 1))
    cents = []
    for sign in (-1.0, 1.0):
        ext = V.min(axis=(2, 3)) if sign < 0 else V.max(axis=(2, 3))
        ys, xs = np.nonzero((D == ext) & (sign * D > T))
        for y, x in zip(ys.tolist(), xs.tolist()):
            win = P[y:y + 2 * R + 1, x:x + 2 * R + 1]
            border = np.concatenate([win[0], win[-1], win[1:-1, 0], win[1:-1, -1]])
            if int((sign * border > 0.5 * T).sum()) <= 6 * s:
                cents.append((y, x))
    c = np.array(cents, dtype=np.float64) / s
    grid = 0
    nn: List[float] = []
    for i in range(len(c)):
        d = np.sqrt(((c - c[i]) ** 2).sum(axis=1))
        sel = (d >= 7.0) & (d <= 22.0)
        if int(sel.sum()) >= 2:
            grid += 1
            nn.append(float(d[sel].min()))
    cell = float(np.median(nn)) if nn else None
    ok = grid >= WINDOWS_MIN and cell is not None and WINDOW_CELL_PX[0] <= cell <= WINDOW_CELL_PX[1]
    return {"windows": grid, "blobs": int(len(c)), "cell_px_1080": cell, "min_windows": WINDOWS_MIN,
            "cell_range_px": list(WINDOW_CELL_PX), "pass": bool(ok)}


def amber_dots_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """14.5.7 KS7b: at least 40 amber dots in the lower 66 %. An amber pixel is warmer than its surroundings (R - B at
    least 16 levels above its 9x9 local mean), brighter than them (luma +0.015) and ordered R > G > B; dots are the
    8-connected groups of such pixels."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "dots": None}
    x0, y0, x1, y1 = box
    sub = rgb8[y0:y1, x0:x1]
    f = sub.astype(np.float32)
    s = max(1, int(round(h / 1080.0)))
    RB = f[:, :, 0] - f[:, :, 2]
    Y = luma(sub).astype(np.float32)
    M = (RB - box_blur(RB, 4 * s) >= 16.0) & (Y - box_blur(Y, 4 * s) >= 0.015) & (f[:, :, 0] > f[:, :, 1]) & \
        (f[:, :, 1] > f[:, :, 2])
    comps = _components(M)
    return {"dots": len(comps), "amber_pixels": int(M.sum()), "min_dots": AMBER_DOTS_MIN,
            "pass": bool(len(comps) >= AMBER_DOTS_MIN)}


def readable_cells_check(rgb8: np.ndarray, cells: Any) -> Dict[str, Any]:
    """14.5.7 KS7b: at least 150 readable cells. Each harness cell row is [cu, cv, row_height, 5 interior points, 6 web
    points] in frame fractions; readable = row height >= 3 px (scaled from 1080p) and |mean interior luma - mean web luma|
    >= 0.03, each point sampled where it projects (14.5.7: "sampled at 5 points"; a 3x3 mean would mix the sub-pixel web of
    the 3-10 px cells with the hole). The web points lie on the web's centreline (apothem 365 of the 350-380 web); R2-04:
    every point is sampled by bilinear interpolation at its exact sub-pixel position, not snapped to the nearest pixel."""
    h, w = rgb8.shape[:2]
    if not isinstance(cells, (list, tuple)):
        return {"error": "no cells", "pass": False, "readable": None}
    Y = luma(rgb8)
    min_row = CELL_MIN_ROW_PX * h / 1080.0

    def at(u: float, v: float) -> Optional[float]:
        # R2-04: bilinear at the exact projected point (pixel centres at +0.5), so a web point on its projected centreline
        # is not snapped onto the neighbouring hole pixel of a 1-2 px web
        fx, fy = u * w - 0.5, v * h - 0.5
        if not (-0.5 <= fx <= w - 0.5 and -0.5 <= fy <= h - 0.5):
            return None
        x0 = min(max(int(math.floor(fx)), 0), w - 1)
        y0 = min(max(int(math.floor(fy)), 0), h - 1)
        x1, y1 = min(x0 + 1, w - 1), min(y0 + 1, h - 1)
        ax, ay = min(max(fx - x0, 0.0), 1.0), min(max(fy - y0, 0.0), 1.0)
        top = (1.0 - ax) * float(Y[y0, x0]) + ax * float(Y[y0, x1])
        bot = (1.0 - ax) * float(Y[y1, x0]) + ax * float(Y[y1, x1])
        return (1.0 - ay) * top + ay * bot

    def nearest(u: float, v: float) -> Optional[float]:
        x, y = int(u * w), int(v * h)
        if 0 <= x < w and 0 <= y < h:
            return float(Y[y, x])
        return None

    def count(sample) -> Tuple[int, List[float]]:
        n = 0
        dl: List[float] = []
        for c in cells:
            if not isinstance(c, (list, tuple)) or len(c) < 25 or c[2] * h < min_row:
                continue
            ins = [sample(c[3 + 2 * k], c[4 + 2 * k]) for k in range(5)]
            web = [sample(c[13 + 2 * k], c[14 + 2 * k]) for k in range(6)]
            ins = [v for v in ins if v is not None]
            web = [v for v in web if v is not None]
            if len(ins) < 3 or len(web) < 3:
                continue
            d = abs(float(np.mean(ins)) - float(np.mean(web)))
            dl.append(d)
            if d >= CELL_MIN_DIFF:
                n += 1
        return n, dl

    tall = sum(1 for c in cells if isinstance(c, (list, tuple)) and len(c) >= 25 and c[2] * h >= min_row)
    readable, diffs = count(at)
    readable_nn, diffs_nn = count(nearest)
    return {"cells": len(cells), "tall_enough": tall, "readable": readable, "min_readable": READABLE_CELLS_MIN,
            "median_diff": float(np.median(diffs)) if diffs else None, "min_row_px": min_row,
            "min_diff": CELL_MIN_DIFF, "sampling": "bilinear at the projected point (R2-04)",
            "readable_nearest": readable_nn, "median_diff_nearest": float(np.median(diffs_nn)) if diffs_nn else None,
            "pass": bool(readable >= READABLE_CELLS_MIN)}


def far_glow_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """R1-16 KS1 far glow: the 90th-percentile luma of the projected stair-head region must reach 0.25."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "p90_luma": None}
    x0, y0, x1, y1 = box
    Y = luma(rgb8[y0:y1, x0:x1])
    p90 = float(np.percentile(Y, 90.0))
    return {"p90_luma": p90, "mean_luma": float(Y.mean()), "min_p90": FAR_GLOW_P90_MIN, "pass": bool(p90 >= FAR_GLOW_P90_MIN)}


def stipple_check(rgb8: np.ndarray) -> Dict[str, Any]:
    """Advisory screen-door stipple detector (critique R1-13 / R1-29): 16 x 16 tiles holding at least 5 isolated 1-px
    luma maxima and 5 isolated minima (a pixel more than 0.02 above / below all 4 neighbours), the signature of a
    dithered discard between two surfaces of different tone. Aliased thin diagonals trigger it too, so it only warns."""
    Y = luma(rgb8).astype(np.float32)
    c = Y[1:-1, 1:-1]
    hi = np.ones(c.shape, dtype=bool)
    lo = np.ones(c.shape, dtype=bool)
    for v in (Y[:-2, 1:-1], Y[2:, 1:-1], Y[1:-1, :-2], Y[1:-1, 2:]):
        hi &= c > v + STIPPLE_LEVEL
        lo &= c < v - STIPPLE_LEVEL
    H = np.zeros(Y.shape, dtype=bool)
    L = np.zeros(Y.shape, dtype=bool)
    H[1:-1, 1:-1] = hi
    L[1:-1, 1:-1] = lo
    t = 16
    h, w = Y.shape
    th, tw = h // t, w // t
    ch = H[:th * t, :tw * t].reshape(th, t, tw, t).sum(axis=(1, 3))
    cl = L[:th * t, :tw * t].reshape(th, t, tw, t).sum(axis=(1, 3))
    flagged = np.argwhere(np.minimum(ch, cl) >= STIPPLE_TILE_MIN)
    return {"tiles": int(len(flagged)), "tiles_px": [[int(x) * t, int(y) * t] for y, x in flagged[:20]],
            "pass": bool(len(flagged) == 0)}


def lift_count_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """Advisory KS3 pour-lift count (14.5.3 / R1-08): horizontal lines in the column-face region, as peaks or troughs of
    the detrended row profile (mean luma per row minus a 41-row moving average) of at least 4 levels, 12 px apart."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "lifts": None}
    x0, y0, x1, y1 = box
    prof = luma(rgb8[y0:y1, x0:x1]).mean(axis=1) * 255.0
    k = 41
    pad = np.pad(prof, k // 2, mode="edge")
    trend = np.convolve(pad, np.ones(k) / k, mode="valid")
    d = prof - trend
    lines = []
    for i in range(2, len(d) - 2):
        if abs(d[i]) >= 4.0 and abs(d[i]) == max(abs(d[i - 2:i + 3])) and (not lines or i - lines[-1] >= 12):
            lines.append(i)
    return {"lifts": len(lines), "rows_px": [y0 + i for i in lines], "min_contrast_levels": 4.0,
            "pass": bool(len(lines) >= 3)}


def spoke_contrast_check(rgb8: np.ndarray, ring: Any) -> Dict[str, Any]:
    """Advisory KS6 spoke-vs-gap contrast (R1-10): around the projected fan centre, the angular luma profile of the
    annulus 1.05-1.35 x the projected housing radius (upper half, where the beams cross the fog), detrended by a 31-bin
    moving average; contrast = mean of the top 10 % minus the bottom 10 % of the detrended profile (target >= 0.04)."""
    h, w = rgb8.shape[:2]
    try:
        cu, cv = float(ring["centre"][0]), float(ring["centre"][1])
        r = float(ring["ring_radius"])
    except (TypeError, KeyError, ValueError, IndexError):
        return {"error": "bad fan_ring %r" % (ring,), "pass": False, "contrast": None}
    Y = luma(rgb8)
    cx, cy, R = cu * w, cv * h, r * h
    n = 360
    prof = np.full(n, np.nan)
    for i in range(n):
        a = math.pi * (i + 0.5) / n                  # 0..pi: the upper half (screen y up)
        vals = []
        for rr in np.linspace(1.05 * R, 1.35 * R, 12):
            x, y = int(cx + rr * math.cos(a)), int(cy - rr * math.sin(a))
            if 0 <= x < w and 0 <= y < h:
                vals.append(float(Y[y, x]))
        if vals:
            prof[i] = float(np.mean(vals))
    ok = ~np.isnan(prof)
    if ok.sum() < 60:
        return {"error": "fan annulus mostly out of frame", "pass": False, "contrast": None}
    p = prof[ok]
    k = 31
    trend = np.convolve(np.pad(p, k // 2, mode="edge"), np.ones(k) / k, mode="valid")
    d = np.sort(p - trend)
    q = max(1, len(d) // 10)
    contrast = float(d[-q:].mean() - d[:q].mean())
    return {"contrast": contrast, "target": 0.04, "bins": int(ok.sum()), "pass": bool(contrast >= 0.04)}


# ----------------------------------------------------------------------------------------------------------------------
# Fix round R2 checks (critique R2-02, R2-16, R2-21, R2-24, R2-36)
# ----------------------------------------------------------------------------------------------------------------------
BLACK_RUN_LEVEL = 2              # R2-16 / R2-36 (g): pure black = largest channel <= 2/255 ...
BLACK_RUN_MIN_PX = 8             # ... in a connected (8-neighbour) run of at least 8 px fails, without the 2x2 opening
AMBER_SAT_MIN = 0.45             # R2-21: amber dot cores (brightest 10 % of the warm dot pixels) HSV saturation >= 0.45 ...
AMBER_HUE_RANGE = (28.0, 38.0)   # ... at hue 28-38 degrees (AMBER #FFB45A: hue 32.7, S 0.65)
AMBER_CORE_FRACTION = 0.10
AMBER_COVER_HUE = (12.0, 55.0)   # R2-24: warm-emissive pixels by emitter area: hue 12-55, S >= 0.12, V >= 0.6 ...
AMBER_COVER_SAT = 0.12
AMBER_COVER_VAL = 0.6
AMBER_COVER_MAX_PCT = 0.3        # ... against the 14.2 cap of 0.3 % of the frame (advisory until R2-24 is arbitrated)
LIFT_MIN_LEVELS = 4.0            # R2-02: a pour-lift joint reads when it stands out by >= 4 levels at 1080 ...
LIFT_MIN_COUNT = 3               # ... and at least 3 lifts read (y 3, 6, 9; DEVELOPMENT shaders row 'R1-08 KS3 lift count')
WALL_BOARD_MIN_LEVELS = 3.0      # R2-02: KS1 gallery wall board steps of at least 3 levels (p95 of the block residual)
WALL_MACRO_MIN_LEVELS = 2.0      # R2-02: KS2a shaft-wall macro residual of at least 2 levels (std after a plane fit)


def black_run_check(rgb8: np.ndarray) -> Dict[str, Any]:
    """R2-16 / R2-36 (g): pure black (largest channel <= 2) in a connected 8-neighbour run of at least 8 px fails. Unlike
    near_black_check there is no 2x2 opening, so a 1-px black outline (a crack showing the camera clear colour) is caught."""
    dark = rgb8.max(axis=2) <= BLACK_RUN_LEVEL
    n = int(dark.sum())
    res: Dict[str, Any] = {"level": BLACK_RUN_LEVEL, "min_run_px": BLACK_RUN_MIN_PX, "black_pixels": n}
    if n == 0:
        res.update({"runs": 0, "largest_run_px": 0, "pass": True})
        return res
    if n > 200000:   # a crushed frame: no need to label every pixel
        res.update({"runs": None, "largest_run_px": n, "pass": False, "note": "more than 200 000 black pixels"})
        return res
    allc = sorted(_components(dark, True), reverse=True)
    comps = [c for c in allc if c[0] >= BLACK_RUN_MIN_PX]
    res.update({"runs": len(comps), "largest_run_px": int(allc[0][0]) if allc else 0,
                "runs_px": [[int(x), int(y), int(s)] for s, y, x in comps[:20]], "pass": not comps})
    return res


def _hsv(rgb: np.ndarray) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Hue (degrees 0-360), saturation and value (0-1) of N x 3 sRGB 8-bit pixels."""
    f = rgb.astype(np.float32) / 255.0
    mx = f.max(axis=1)
    mn = f.min(axis=1)
    c = mx - mn
    s = np.where(mx > 0, c / np.maximum(mx, 1e-6), 0.0)
    r, g, b = f[:, 0], f[:, 1], f[:, 2]
    cz = np.maximum(c, 1e-6)
    hr = ((g - b) / cz) % 6.0
    hg = (b - r) / cz + 2.0
    hb = (r - g) / cz + 4.0
    hue = np.where(mx == r, hr, np.where(mx == g, hg, hb)) * 60.0
    hue = np.where(c > 0, hue, 0.0)
    return hue, s, mx


def amber_mask(rgb8: np.ndarray, scale: int = 1) -> np.ndarray:
    """The amber-dot pixel mask of amber_dots_check (warmer and brighter than the 9x9 local mean, R > G > B)."""
    f = rgb8.astype(np.float32)
    RB = f[:, :, 0] - f[:, :, 2]
    Y = luma(rgb8).astype(np.float32)
    return (RB - box_blur(RB, 4 * scale) >= 16.0) & (Y - box_blur(Y, 4 * scale) >= 0.015) & (f[:, :, 0] > f[:, :, 1]) & \
        (f[:, :, 1] > f[:, :, 2])


def amber_saturation_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """R2-21: the amber lamp dots (KS7b lower 66 %) must read amber, not cream: the brightest 10 % of the amber-dot pixels
    (the dot cores) have median HSV saturation >= 0.45 and median hue 28-38 degrees (AMBER #FFB45A: 32.7, S 0.65)."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "core_saturation": None}
    x0, y0, x1, y1 = box
    sub = rgb8[y0:y1, x0:x1]
    s = max(1, int(round(h / 1080.0)))
    M = amber_mask(sub, s)
    px = sub[M]
    res: Dict[str, Any] = {"amber_pixels": int(px.shape[0]), "min_saturation": AMBER_SAT_MIN,
                           "hue_range": list(AMBER_HUE_RANGE), "core_fraction": AMBER_CORE_FRACTION}
    if px.shape[0] < 10:
        res.update({"error": "fewer than 10 amber-dot pixels", "core_saturation": None, "pass": False})
        return res
    Yp = luma(px)
    k = max(1, int(round(AMBER_CORE_FRACTION * px.shape[0])))
    core = px[np.argsort(Yp)[-k:]]
    hue, sat, val = _hsv(core)
    med_rgb = [float(v) for v in np.median(core.astype(np.float32), axis=0)]
    res.update({"core_pixels": int(k), "core_saturation": float(np.median(sat)), "core_hue": float(np.median(hue)),
                "core_value": float(np.median(val)), "core_rgb_median": med_rgb})
    res["pass"] = bool(res["core_saturation"] >= AMBER_SAT_MIN and AMBER_HUE_RANGE[0] <= res["core_hue"] <= AMBER_HUE_RANGE[1])
    return res


def amber_coverage_check(rgb8: np.ndarray) -> Dict[str, Any]:
    """R2-24: amber coverage by emitter area (not by saturation): the share of warm-emissive pixels (hue 12-55, S >= 0.12,
    V >= 0.6; lamp cores included) against the 14.2 cap of 0.3 % of the frame. Advisory until the lamp-head conflict of
    R2-24 is arbitrated."""
    hue, sat, val = _hsv(rgb8.reshape(-1, 3))
    M = (hue >= AMBER_COVER_HUE[0]) & (hue <= AMBER_COVER_HUE[1]) & (sat >= AMBER_COVER_SAT) & (val >= AMBER_COVER_VAL)
    pct = 100.0 * float(M.mean())
    return {"pct": pct, "pixels": int(M.sum()), "limit_pct": AMBER_COVER_MAX_PCT,
            "rule": "hue 12-55, S >= 0.12, V >= 0.6", "pass": bool(pct <= AMBER_COVER_MAX_PCT)}


def lift_curves_check(rgb8: np.ndarray, curves: Any) -> Dict[str, Any]:
    """R2-02 KS3 pour lifts along their projected iso-y curves on C1 (harness "lift_curves": [y, u0, v0, u1, v1, ...]).
    At every curve point the vertical luma profile +/-16 px (scaled from 1080p) is detrended by a line through its outer
    parts (6-16 px out); the profiles of a curve are averaged and the joint's contrast is the largest |deviation| within
    +/-4 px of the curve, in levels. A lift reads at >= 4 levels; at least 3 must read (joints above the collar band)."""
    h, w = rgb8.shape[:2]
    if not isinstance(curves, (list, tuple)) or not curves:
        return {"error": "no lift curves", "pass": False, "lifts": None}
    s = h / 1080.0
    H = max(8, int(round(16 * s)))
    inner = max(2, int(round(4 * s)))
    outer0 = max(inner + 1, int(round(6 * s)))
    Y = luma(rgb8).astype(np.float64) * 255.0
    offs = np.arange(-H, H + 1)
    fit = np.abs(offs) >= outer0
    joints = []
    for c in curves:
        if not isinstance(c, (list, tuple)) or len(c) < 1 + 2 * 4:
            continue
        y_world = float(c[0])
        profs = []
        for k in range(1, len(c) - 1, 2):
            x = int(c[k] * w)
            yc = int(c[k + 1] * h)
            if not (0 <= x < w and H <= yc < h - H):
                continue
            p = Y[yc - H:yc + H + 1, x]
            a, b = np.polyfit(offs[fit], p[fit], 1)
            profs.append(p - (a * offs + b))
        if len(profs) < 4:
            continue
        mp = np.mean(profs, axis=0)
        core = mp[np.abs(offs) <= inner]
        contrast = float(np.max(np.abs(core)))
        joints.append({"y": y_world, "points": len(profs), "contrast_levels": contrast,
                       "peak": float(core[np.argmax(np.abs(core))]), "reads": bool(contrast >= LIFT_MIN_LEVELS)})
    above = [jn for jn in joints if jn["y"] > 0]
    reads = [jn["y"] for jn in above if jn["reads"]]
    return {"lifts": len(reads), "lift_y": reads, "joints": joints, "min_contrast_levels": LIFT_MIN_LEVELS,
            "min_lifts": LIFT_MIN_COUNT, "method": "iso-y curves of C1 (R2-02)", "pass": bool(len(reads) >= LIFT_MIN_COUNT)}


def _block_means(Y: np.ndarray, b: int) -> np.ndarray:
    hh, ww = (Y.shape[0] // b) * b, (Y.shape[1] // b) * b
    if hh == 0 or ww == 0:
        return np.zeros((0, 0))
    return Y[:hh, :ww].reshape(hh // b, b, ww // b, b).mean(axis=(1, 3))


def wall_board_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """R2-02 KS1: the gallery wall at 1-2 m shows its board rows. Block means of 8 x 8 px (scaled; the dither averages out)
    minus their 9-block moving average down the wall; the 95th percentile of |residual| must reach 3 levels (a +/-5 %
    board tone at 44-79 levels gives 3-4 level steps)."""
    h, w = rgb8.shape[:2]
    box, m, err = resolve_region(shape, w, h)
    if err:
        return {"error": err, "pass": False, "p95_levels": None}
    x0, y0, x1, y1 = box
    Y = luma(rgb8[y0:y1, x0:x1]).astype(np.float64) * 255.0
    b = max(4, int(round(8 * h / 1080.0)))
    B = _block_means(Y, b)
    if B.shape[0] < 12:
        return {"error": "region too small for the block profile", "pass": False, "p95_levels": None}
    T = box_blur(B, 4)
    R = (B - T)[4:-4, :] if B.shape[0] > 12 else B - T
    p95 = float(np.percentile(np.abs(R), 95))
    return {"p95_levels": p95, "std_levels": float(R.std()), "block_px": b, "min_levels": WALL_BOARD_MIN_LEVELS,
            "pass": bool(p95 >= WALL_BOARD_MIN_LEVELS)}


def wall_macro_check(rgb8: np.ndarray, shape: Any) -> Dict[str, Any]:
    """R2-02 KS2a: the shaft wall at 100-300 m carries macro noise. Per rectangle: block means of 8 x 8 px (scaled), minus a
    least-squares plane; the residual std (levels) of the worst rectangle must reach 2 levels."""
    h, w = rgb8.shape[:2]
    rects = shape.get("rects") if isinstance(shape, dict) and "rects" in shape else [shape]
    b = max(4, int(round(8 * h / 1080.0)))
    per = []
    for r in rects:
        box, m, err = resolve_region(r, w, h)
        if err:
            return {"error": err, "pass": False, "std_levels": None}
        x0, y0, x1, y1 = box
        B = _block_means(luma(rgb8[y0:y1, x0:x1]).astype(np.float64) * 255.0, b)
        if B.size < 12:
            return {"error": "region too small", "pass": False, "std_levels": None}
        yy, xx = np.mgrid[0:B.shape[0], 0:B.shape[1]]
        A = np.stack([np.ones(B.size), xx.ravel(), yy.ravel()], axis=1)
        coef, *_ = np.linalg.lstsq(A, B.ravel(), rcond=None)
        per.append(float((B.ravel() - A @ coef).std()))
    worst = min(per) if per else None
    return {"std_levels": worst, "per_rect_std_levels": per, "block_px": b, "min_levels": WALL_MACRO_MIN_LEVELS,
            "pass": bool(worst is not None and worst >= WALL_MACRO_MIN_LEVELS)}


# Luma bands by the content in view (critique R1-38): inside a 14.6 transition window the frame shows a blend of two
# beats' looks, so the mean-luma band is the union of both rows. Windows on extended progress E (d before the ride,
# D_LIFT + ride seconds during it, d + 121 after it).
D_LIFT = 459.54
BEAT6_START_D = 469.54   # AbyssConst.BEAT_START_D[5]: the gantry starts 10 m past the bottom dock
# R2-36 (f): the 5 -> 6 window runs from ride second 110 to the beat-6 start (E = d + 121 after the ride), so the bottom
# dock (B5_end at d 467.54, a view of the fan and the gantry) is judged by the content in view, beat 5 or 6
E_TRANSITIONS: List[Tuple[float, float, int, int]] = [
    (52.0, 72.0, 1, 2), (205.0, 225.0, 2, 3), (285.0, 310.0, 3, 4), (420.0, 440.0, 4, 5),
    (D_LIFT + 110.0, BEAT6_START_D + 121.0, 5, 6), (640.0 + 121.0, 668.54 + 121.0, 6, 7),
]


# Lead decision D8 of fix round R2 (critique R2-13, R2-36 f; DEVELOPMENT.md 'Lead arbitrations for fix round R2'): a key
# shot whose subject belongs to another beat is judged by the content in view (R1-38). KS5b (captured in beat 5 from the
# lift) frames the fan ring and its backlit bore, a beat-6 subject: its mean-luma band and p99 limit are the beat-6 row
# (0.09-0.22, p99 <= 0.95). The clipping limit and the banding regions stay those of the capture beat.
CONTENT_LUMA_ROWS: Dict[str, Tuple[int, Optional[str]]] = {"KS5b": (6, None)}


def content_luma_row(shot: Dict[str, Any]) -> Optional[Tuple[int, Optional[str]]]:
    """The threshold row of the content in view for a shot listed in CONTENT_LUMA_ROWS (by the token in its base tag, tag or
    file name, any resolution), else None."""
    names = [shot.get("base_tag"), shot.get("tag"),
             os.path.splitext(os.path.basename(str(shot.get("file") or "")))[0]]
    for v in names:
        if not isinstance(v, str) or not v:
            continue
        for tok, row in CONTENT_LUMA_ROWS.items():
            if re.search(r"(?:^|_)" + re.escape(tok) + r"(?:_|$)", v, re.IGNORECASE):
                return row
        return None
    return None


def luma_rows_for(shot: Dict[str, Any], tkey: Optional[Tuple[int, Optional[str]]]) -> List[Tuple[int, Optional[str]]]:
    """Threshold rows whose mean-luma band applies to a shot: its own row, plus the neighbouring beat's row when the
    shot's E lies inside a 14.6 transition window (beat 7 uses the shot's zone). A shot in CONTENT_LUMA_ROWS (lead decision
    D8: KS5b) uses the row of its content alone."""
    if tkey is None:
        return []
    crow = content_luma_row(shot)
    if crow is not None and crow in BEAT_THRESHOLDS:
        return [crow]
    rows = [tkey]
    try:
        e = float(shot.get("e"))
    except (TypeError, ValueError):
        return rows
    for e0, e1, a, b in E_TRANSITIONS:
        if e0 <= e <= e1:
            for beat in (a, b):
                k = (7, tkey[1] if tkey[0] == 7 else "tunnel") if beat == 7 else (beat, None)
                if k in BEAT_THRESHOLDS and k not in rows:
                    rows.append(k)
    return rows


# ----------------------------------------------------------------------------------------------------------------------
# Capture report
# ----------------------------------------------------------------------------------------------------------------------
def load_report(folder: str) -> Dict[str, Any]:
    """Load report.json of a capture folder. If it is missing, synthesise one from the PNG file names
    (beatNN_<name>_<tag>.png)."""
    path = os.path.join(folder, "report.json")
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8-sig") as f:
            rep = json.load(f)
        rep.setdefault("run", os.path.basename(os.path.normpath(folder)))
        rep.setdefault("shots", [])
        rep["_has_report"] = True
        return rep
    shots = []
    for p in list_pngs(folder):
        fn = os.path.basename(p)
        stem = os.path.splitext(fn)[0]
        m = re.match(r"beat0?(\d)_([a-z0-9]+)_(.+)$", stem, re.IGNORECASE)
        tag = m.group(3) if m else stem
        shots.append({"tag": tag, "beat": infer_beat(tag, fn), "file": fn})
    return {"run": os.path.basename(os.path.normpath(folder)), "shots": shots, "_has_report": False}


def shot_key(path: str, folder: str) -> str:
    """Identity of a shot: its file path relative to the capture folder, with forward slashes (the basename when the
    file lies outside the folder). Tags are not unique: a key shot exists at 1920x1080, 3840x2160, 1680x1050 and
    2560x1080 under the same tag."""
    p = path if os.path.isabs(path) else os.path.join(folder, path)
    try:
        rel = os.path.relpath(os.path.abspath(p), os.path.abspath(folder))
    except ValueError:            # another drive on Windows
        rel = os.path.basename(p)
    if rel == os.pardir or rel.startswith(os.pardir + os.sep):
        rel = os.path.basename(p)
    return rel.replace("\\", "/")


def match_key(key: str) -> str:
    """Case-normalised form of a shot key for matching (file names are case-insensitive on Windows)."""
    return os.path.normcase(key).replace("\\", "/")


def shot_label(shot: Dict[str, Any]) -> str:
    """Human label that stays unique across resolutions: 'tag (file)'."""
    tag = str(shot.get("tag") or "")
    f = os.path.basename(str(shot.get("file") or ""))
    return "%s (%s)" % (tag, f) if f and tag else (tag or f or "?")


def report_duplicates(shots: Sequence[Dict[str, Any]], folder: str) -> Dict[str, List[Dict[str, Any]]]:
    """Duplicates in a capture report:
    * ``files``: one file referenced by several shots (e.g. a 4K capture overwrote the 1080p one because the harness
      named both by tag only);
    * ``tag_sizes``: one (tag, width x height) pair that appears in several files."""
    by_file: Dict[str, Dict[str, Any]] = {}
    by_tag_size: Dict[Tuple[str, str], List[str]] = {}
    for s in shots:
        f = str(s.get("file") or "")
        if not f:
            continue
        key = shot_key(f, folder)
        tag = str(s.get("tag") or "")
        ent = by_file.setdefault(match_key(key), {"file": key, "shots": []})
        ent["shots"].append({"tag": tag, "width": s.get("width"), "height": s.get("height")})
        if s.get("width") and s.get("height"):
            size = "%sx%s" % (s.get("width"), s.get("height"))
            files = by_tag_size.setdefault((tag, size), [])
            if key not in files:
                files.append(key)
    return {"files": [e for e in by_file.values() if len(e["shots"]) > 1],
            "tag_sizes": [{"tag": t, "size": sz, "files": v} for (t, sz), v in by_tag_size.items() if len(v) > 1]}


# ----------------------------------------------------------------------------------------------------------------------
# Drawing helpers (Pillow)
# ----------------------------------------------------------------------------------------------------------------------
def font(size: int = 14) -> ImageFont.ImageFont:
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # very old Pillow
        return ImageFont.load_default()


def text_size(draw: ImageDraw.ImageDraw, text: str, fnt: ImageFont.ImageFont) -> Tuple[int, int]:
    b = draw.textbbox((0, 0), text, font=fnt)
    return b[2] - b[0], b[3] - b[1]


def heat_colour(t: np.ndarray) -> np.ndarray:
    """Map 0..1 to a black -> blue -> magenta -> yellow -> white ramp (uint8 H x W x 3)."""
    t = np.clip(t.astype(np.float32), 0.0, 1.0)
    stops = np.array([[0, 0, 0], [20, 40, 160], [190, 40, 150], [250, 200, 40], [255, 255, 255]], dtype=np.float32)
    pos = t * (len(stops) - 1)
    i = np.clip(np.floor(pos).astype(np.int32), 0, len(stops) - 2)
    f = (pos - i)[..., None]
    c = stops[i] * (1 - f) + stops[i + 1] * f
    return np.clip(np.rint(c), 0, 255).astype(np.uint8)


def line_plot(series: List[Tuple[str, Sequence[float], Tuple[int, int, int]]], width: int = 1400,
              height: int = 300, title: str = "", markers: Optional[Sequence[int]] = None,
              hlines: Optional[List[Tuple[float, Tuple[int, int, int]]]] = None) -> Image.Image:
    """Tiny dependency-free line plot (Pillow). Each series = (label, values, colour). Markers = x indices."""
    img = Image.new("RGB", (width, height), (18, 20, 24))
    d = ImageDraw.Draw(img)
    fnt = font(13)
    ml, mr, mt, mb = 70, 16, 26, 22
    vals = [v for _, s, _ in series for v in s if v is not None and math.isfinite(v)]
    if hlines:
        vals += [v for v, _ in hlines]
    n = max((len(s) for _, s, _ in series), default=0)
    if not vals or n < 2:
        d.text((ml, mt), title + " (no data)", fill=(220, 220, 220), font=fnt)
        return img
    lo, hi = min(vals), max(vals)
    if hi - lo < 1e-9:
        hi = lo + 1e-3
    pad = (hi - lo) * 0.06
    lo, hi = lo - pad, hi + pad
    pw, ph = width - ml - mr, height - mt - mb

    def X(i: float) -> float:
        return ml + pw * i / max(1, n - 1)

    def Yv(v: float) -> float:
        return mt + ph * (1.0 - (v - lo) / (hi - lo))

    d.rectangle([ml, mt, ml + pw, mt + ph], outline=(70, 74, 80))
    for k in range(5):
        v = lo + (hi - lo) * k / 4
        y = Yv(v)
        d.line([ml, y, ml + pw, y], fill=(40, 44, 50))
        d.text((4, y - 7), "%.4g" % v, fill=(160, 160, 160), font=fnt)
    if hlines:
        for v, col in hlines:
            d.line([ml, Yv(v), ml + pw, Yv(v)], fill=col)
    if markers:
        for i in markers:
            d.line([X(i), mt, X(i), mt + ph], fill=(200, 60, 60))
    for label, s, col in series:
        pts = [(X(i), Yv(v)) for i, v in enumerate(s) if v is not None and math.isfinite(v)]
        if len(pts) >= 2:
            d.line(pts, fill=col, width=1)
    x = ml
    d.text((ml, 4), title, fill=(230, 230, 230), font=fnt)
    x = ml + text_size(d, title, fnt)[0] + 20
    for label, _s, col in series:
        d.rectangle([x, 8, x + 10, 18], fill=col)
        d.text((x + 14, 4), label, fill=(210, 210, 210), font=fnt)
        x += 24 + text_size(d, label, fnt)[0]
    d.text((ml, height - 18), "frame 0 .. %d" % (n - 1), fill=(150, 150, 150), font=fnt)
    return img


def find_ffmpeg() -> Optional[str]:
    """ffmpeg on PATH, else the binary shipped by the imageio-ffmpeg pip package (allowed analysis tool, 11.12)."""
    import shutil
    p = shutil.which("ffmpeg")
    if p:
        return p
    try:
        import imageio_ffmpeg  # type: ignore
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def eprint(*a: Any) -> None:
    print(*a, file=sys.stderr)
