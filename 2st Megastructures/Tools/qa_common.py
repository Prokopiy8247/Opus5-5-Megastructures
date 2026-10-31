"""Shared helpers for The Ring QA tools (MASTER_PROMPT Part 11.3, 14.10).

Run every tool with ``python -I``. Requires numpy and Pillow (pip install numpy pillow).

Conventions (14.1): luma = Rec.709 on the 8-bit sRGB capture (0.2126 R + 0.7152 G + 0.0722 B), yaw 0 = +Z,
+90 = +X, pitch up positive, vFOV vertical. Image rows are top-down here; the capture harness writes report.json
next to the PNGs with the camera of every image (eye, yaw, pitch, vfov, w, h, s). Key-shot poses are read from
report.json, or parsed from Assets/_Game/Scripts/Runtime/QA/KeyShots.cs (the single source of the 14.10 table).
"""
import glob
import json
import math
import os
import re

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
RING_R = 16000.0
CLIP_EXTENT = 8192.0
BEAT_S = [0, 24, 276, 411, 594, 774, 894, 960]
BEAT_NAMES = ["capsule", "valley", "ark", "sunset", "rimwall", "tunnel", "outside"]
STAR_RADIUS_DEG = 1.2
STAR_MASK_DEG = 1.5          # disc plus limb smoothing; the star-disc mask of 14.10


def dir_from_az_el(yaw_deg, el_deg):
    y, e = math.radians(yaw_deg), math.radians(el_deg)
    return np.array([math.sin(y) * math.cos(e), math.sin(e), math.cos(y) * math.cos(e)])


STAR_L = dir_from_az_el(-68.0, 26.0)
STAR_L7 = dir_from_az_el(96.0, 7.0)

# Per-beat thresholds of the 14.10 table: (mean luma min, max, clipped % max, clipped % outside the star max)
BEAT_THRESHOLDS = {
    1: (35, 90, 1.5, None),
    2: (105, 165, 0.4, None),
    3: (85, 150, 0.6, None),
    4: (40, 155, 0.5, None),
    5: (50, 100, 0.3, None),
    6: (6, 38, 1.0, None),
    7: (35, 105, 2.5, 0.8),
}
# Key-shot luma ranges stated in the 14.5 acceptance lists (they replace the beat range for that shot)
KS_LUMA = {"KS01": (35, 90), "KS02": (105, 165), "KS05": (70, 125), "KS07": (50, 100), "KS10": (6, 38)}
KS_CLIP = {"KS15": 3.0}
# Banding regions per beat (fractions x0, y0, x1, y1; y from the top). Beat 7 uses the corona around the star.
BAND_REGIONS = {
    1: (0.10, 0.05, 0.90, 0.35),
    2: (0.10, 0.05, 0.90, 0.35),
    3: (0.10, 0.02, 0.90, 0.20),
    4: (0.10, 0.03, 0.90, 0.40),
    5: (0.10, 0.03, 0.90, 0.35),
    6: (0.25, 0.70, 0.75, 0.98),
    7: (0.10, 0.05, 0.90, 0.35),
}
KS01_PORTHOLE = (0.42, 0.30, 0.58, 0.70)


def beat_of(s):
    for i in range(6, -1, -1):
        if s >= BEAT_S[i]:
            return i + 1
    return 1


# ---------------------------------------------------------------- images
def load_rgb(path):
    im = Image.open(path)
    if im.mode != "RGB":
        im = im.convert("RGB")
    return np.asarray(im, dtype=np.uint8)


def luma(rgb):
    """Rec.709 luma (float32, 0..255) of an 8-bit sRGB image."""
    f = rgb.astype(np.float32)
    return 0.2126 * f[..., 0] + 0.7152 * f[..., 1] + 0.0722 * f[..., 2]


def luma8(rgb):
    return np.clip(np.rint(luma(rgb)), 0, 255).astype(np.uint8)


def percentiles(lum, qs=(0.1, 1, 50, 99, 99.9)):
    return {("p%g" % q).replace(".", "_"): float(np.percentile(lum, q)) for q in qs}


def clipped_mask(rgb):
    return (rgb >= 254).any(axis=-1)


def magenta_mask(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r >= 200) & (b >= 200) & (g <= 60)


def rect_px(rect, w, h):
    x0, y0, x1, y1 = rect
    return (int(round(x0 * w)), int(round(y0 * h)), max(int(round(x1 * w)), int(round(x0 * w)) + 1),
            max(int(round(y1 * h)), int(round(y0 * h)) + 1))


def box1d(a, n):
    """Moving average with window n (same length, edge-normalised)."""
    if n <= 1:
        return a.astype(np.float64)
    k = np.ones(n) / n
    num = np.convolve(a, k, mode="same")
    den = np.convolve(np.ones_like(a, dtype=np.float64), k, mode="same")
    return num / np.maximum(den, 1e-9)


def windows2d(a, k):
    """k x k sliding windows of a 2D array with edge padding (view, no copy of the windows)."""
    p = k // 2
    ap = np.pad(a, p, mode="edge")
    return np.lib.stride_tricks.sliding_window_view(ap, (k, k))


def median5(lum8, strip=96):
    """5 x 5 median of a uint8 image, computed in row strips to bound memory."""
    h, w = lum8.shape
    out = np.empty_like(lum8)
    ap = np.pad(lum8, 2, mode="edge")
    for y in range(0, h, strip):
        y1 = min(h, y + strip)
        win = np.lib.stride_tricks.sliding_window_view(ap[y:y1 + 4], (5, 5)).reshape(y1 - y, w, 25)
        out[y:y1] = np.partition(win, 12, axis=-1)[..., 12]
    return out


def isolated_pixels(lum8):
    """NaN-like pixels (14.10): brighter than 8x the 5x5 median (and >= 48) with no neighbour above half their value
    (a one-pixel spike, not a 3-12 px glint), and black blotches: darker than 1/8 of the median (>= 64) with all
    neighbours at least half the median. Returns (bright, dark, raw_bright)."""
    med = median5(lum8).astype(np.int32)
    l = lum8.astype(np.int32)
    raw = (l > 8 * np.maximum(med, 1)) & (l >= 48)
    lp = np.pad(l, 1, mode="edge")
    h, w = l.shape
    nmax = np.zeros_like(l)
    nmin = np.full_like(l, 255)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            sh = lp[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            nmax = np.maximum(nmax, sh)
            nmin = np.minimum(nmin, sh)
    bright = raw & (nmax * 2 < l)
    dark = (l * 8 < med) & (med >= 64) & (nmin * 2 >= med)
    return int(bright.sum()), int(dark.sum()), int(raw.sum())


# ---------------------------------------------------------------- banding
def band_score(lum, rect, fullres_h, min_cols=128):
    """BandScore of 14.10: average the region over >= 128 columns (strips of at least 128 columns, so curved bands
    stay nearly horizontal within a strip), take the vertical luma profile, and measure the longest run of identical
    values where the expected slope is >= 0.1 level/px. Limit 6 px at 1080p, 12 px at 4K.
    Also counts step edges (a jump >= 1 level where the trend is below 0.5 level/px)."""
    h, w = lum.shape
    x0, y0, x1, y1 = rect_px(rect, w, h)
    sub = lum[y0:y1, x0:x1]
    if sub.shape[0] < 8 or sub.shape[1] < 8:
        return {"score": 0.0, "limit": limit_for(fullres_h), "steps": 0, "cols": int(sub.shape[1]), "gradient_px": 0}
    ncols = sub.shape[1]
    nstrips = max(1, ncols // max(min_cols, ncols // 4))
    edges = np.linspace(0, ncols, nstrips + 1).astype(int)
    best, steps, grad_px = 0, 0, 0
    # expected slope: the profile smoothed twice with a 63 px box (no ripple from 1-level plateaus up to ~20 px wide)
    win = int(min(63, max(3, (sub.shape[0] // 2) * 2 - 1)))
    for i in range(nstrips):
        prof = sub[:, edges[i]:edges[i + 1]].astype(np.float64).mean(axis=1)
        sm = box1d(box1d(prof, win), win)
        slope = np.abs(np.gradient(sm))
        d = np.abs(np.diff(prof))
        same = d < 1e-4
        ok = slope >= 0.1
        grad_px += int(ok.sum())
        run = 0
        for j in range(len(same)):
            if same[j] and ok[j] and ok[j + 1]:
                run += 1
                best = max(best, run + 1)
            else:
                run = 0
        steps += int(((d >= 1.0) & (slope[1:] < 0.5) & (slope[:-1] < 0.5)).sum())
    return {"score": float(best), "limit": limit_for(fullres_h), "steps": steps, "cols": int(ncols), "gradient_px": grad_px}


def limit_for(h):
    return 12.0 if h >= 2000 else 6.0


# ---------------------------------------------------------------- camera maths
def cam_basis(yaw_deg, pitch_deg):
    y, p = math.radians(yaw_deg), math.radians(pitch_deg)
    fwd = np.array([math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)])
    right = np.array([math.cos(y), 0.0, -math.sin(y)])
    up = np.array([-math.sin(y) * math.sin(p), math.cos(p), -math.cos(y) * math.sin(p)])
    return fwd, right, up


def project(cam, pts):
    """World points (N x 3, already bent) to pixel coordinates (u right, v down) for cam = dict(eye, yaw, pitch,
    vfov, w, h). Returns (u, v, z)."""
    pts = np.atleast_2d(np.asarray(pts, dtype=np.float64))
    fwd, right, up = cam_basis(cam["yaw"], cam["pitch"])
    d = pts - np.asarray(cam["eye"], dtype=np.float64)
    z = d @ fwd
    x = d @ right
    y = d @ up
    ty = math.tan(math.radians(cam["vfov"]) / 2.0)
    tx = ty * cam["w"] / cam["h"]
    zz = np.where(z > 1e-6, z, 1e-6)
    u = (0.5 + 0.5 * x / (zz * tx)) * cam["w"]
    v = (0.5 - 0.5 * y / (zz * ty)) * cam["h"]
    return u, v, z


def dir_to_px(cam, direction):
    p = np.asarray(cam["eye"], dtype=np.float64) + np.asarray(direction, dtype=np.float64) * 1000.0
    u, v, z = project(cam, p[None, :])
    return float(u[0]), float(v[0]), float(z[0])


def bend(pts, cam_eye):
    """Numpy twin of RingBend.Bend (14.7 H1), double precision."""
    pts = np.atleast_2d(np.asarray(pts, dtype=np.float64))
    d = pts[:, 2] - cam_eye[2]
    t = d / RING_R
    h = np.clip(pts[:, 1], -50.0, 0.9 * RING_R)
    sh = np.sin(0.5 * t)
    return np.stack([pts[:, 0], h * np.cos(t) + 2.0 * RING_R * sh * sh, cam_eye[2] + (RING_R - h) * np.sin(t)], axis=1)


def px_per_deg(cam):
    return cam["h"] / cam["vfov"]


def star_for(s):
    """The star direction visible at progress s (the key switches to L7 inside the tunnel, 14.2.3)."""
    return STAR_L7 if s >= 834 else STAR_L


def star_mask(cam, s, radius_deg=STAR_MASK_DEG):
    """Boolean mask of the star disc (plus limb margin) if it projects into the frame; None otherwise."""
    u, v, z = dir_to_px(cam, star_for(s))
    if z <= 0:
        return None
    r = radius_deg * px_per_deg(cam)
    if u < -r or v < -r or u > cam["w"] + r or v > cam["h"] + r:
        return None
    yy, xx = np.ogrid[:cam["h"], :cam["w"]]
    return (xx - u) ** 2 + (yy - v) ** 2 <= r * r


# ---------------------------------------------------------------- colour
def srgb_to_lab(rgb):
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124564, 0.3575761, 0.1804375], [0.2126729, 0.7151522, 0.0721750], [0.0193339, 0.1191920, 0.9503041]])
    xyz = lin @ m.T
    xyz = xyz / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 216 / 24389, np.cbrt(xyz), (24389 / 27 * xyz + 16) / 116)
    L = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], axis=-1)


def de76(lab1, lab2):
    return np.sqrt(((np.asarray(lab1) - np.asarray(lab2)) ** 2).sum(axis=-1))


# ---------------------------------------------------------------- names, reports, key shots
NAME_KS = re.compile(r"^ks(\d\d)_([a-z0-9_]+?)_(\d+)x(\d+)\.png$")
NAME_BEAT = re.compile(r"^beat(\d\d)_([a-z]+)_(.+?)(?:_(\d+)x(\d+))?\.png$")
NAME_RES = re.compile(r"_(\d+)x(\d+)\.png$")


def parse_name(fname):
    m = NAME_KS.match(fname)
    if m:
        return {"kind": "ks", "shot": "KS" + m.group(1), "index": int(m.group(1)), "tag": m.group(2),
                "w": int(m.group(3)), "h": int(m.group(4))}
    m = NAME_BEAT.match(fname)
    if m:
        w = int(m.group(4)) if m.group(4) else 1920
        h = int(m.group(5)) if m.group(5) else 1080
        return {"kind": "still", "beat": int(m.group(1)), "name": m.group(2), "tag": m.group(3), "w": w, "h": h}
    m = NAME_RES.search(fname)
    if m:
        return {"kind": "other", "w": int(m.group(1)), "h": int(m.group(2))}
    return {"kind": "other"}


def load_report(run):
    p = os.path.join(run, "report.json")
    if not os.path.exists(p):
        return None
    with open(p, "r", encoding="utf-8") as f:
        return json.load(f)


def report_index(report):
    idx = {}
    if report:
        for r in report.get("images_detail", []):
            idx[r["file"].replace("\\", "/")] = r
    return idx


_KS_CACHE = None


def key_shots():
    """The 14.10 table parsed from KeyShots.cs (the single source): id -> dict(index, tag, s, eye, yaw, pitch, vfov)."""
    global _KS_CACHE
    if _KS_CACHE is not None:
        return _KS_CACHE
    path = os.path.join(ROOT, "Assets", "_Game", "Scripts", "Runtime", "QA", "KeyShots.cs")
    num = r"(-?[\d.]+)f?"
    # K(index, "tag", s, x, y, z, yaw, pitch, vfov[, t[, extra]])
    pat = re.compile(r"K\((\d+),\s*\"(\w+)\",\s*" + r",\s*".join([num] * 7))
    res = {}
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as f:
            for m in pat.finditer(f.read()):
                i = int(m.group(1))
                v = [float(m.group(k)) for k in range(3, 10)]
                res["KS%02d" % i] = {"index": i, "tag": m.group(2), "s": v[0], "eye": [v[1], v[2], v[3]],
                                     "yaw": v[4], "pitch": v[5], "vfov": v[6]}
    _KS_CACHE = res
    return res


def camera_for(fname, rec, w, h):
    """Camera dict for an image: from its report row, else from the key-shot table (key shots only)."""
    if rec and rec.get("eye") is not None and rec.get("vfov"):
        return {"eye": rec["eye"], "yaw": rec["yaw"], "pitch": rec["pitch"], "vfov": rec["vfov"], "w": w, "h": h,
                "s": rec.get("s", 0.0)}
    info = parse_name(fname)
    if info["kind"] == "ks":
        ks = key_shots().get(info["shot"])
        if ks:
            return {"eye": ks["eye"], "yaw": ks["yaw"], "pitch": ks["pitch"], "vfov": ks["vfov"], "w": w, "h": h, "s": ks["s"]}
    return None


def write_json(path, obj):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=1, default=lambda o: float(o) if isinstance(o, (np.floating,)) else int(o) if isinstance(o, (np.integer,)) else str(o))


def list_pngs(folder):
    return sorted(os.path.basename(p) for p in glob.glob(os.path.join(folder, "*.png")))


# ---------------------------------------------------------------- thin-line (aliasing) regions
# Designated thin-line regions per beat (fractions x0, y0, x1, y1; y from the top), for the beat stills and the key
# shots of that beat (14.10 table, "Aliasing region"). They are approximate frame zones where those structures sit in
# the start/mid/end and key-shot poses; refine them after looking at the captures (Part 11.4).
ALIAS_REGIONS = {
    1: {"frame_rim": (0.30, 0.15, 0.70, 0.85), "floor_joints": (0.20, 0.80, 0.80, 1.00)},
    2: {"rails": (0.00, 0.55, 1.00, 1.00), "terrain_edge": (0.10, 0.40, 0.90, 0.60)},
    3: {"window_rows": (0.05, 0.05, 0.60, 0.45), "shadow_edge": (0.40, 0.45, 1.00, 0.75)},
    4: {"square_edges": (0.10, 0.05, 0.70, 0.60), "star_limb": (0.25, 0.30, 0.55, 0.60)},
    5: {"seams": (0.40, 0.05, 1.00, 0.80)},
    6: {"ribs": (0.00, 0.00, 1.00, 1.00), "mouth_edge": (0.40, 0.35, 0.60, 0.65)},
    7: {"panels": (0.30, 0.05, 0.80, 0.55), "rails": (0.00, 0.60, 1.00, 1.00), "hull_ridges": (0.00, 0.55, 1.00, 0.85)},
}


def aliasing_score(lum, rect):
    """Thin-line aliasing helper on one still. 'hf' = mean |Laplacian| / mean |gradient| (high where edges are
    one-pixel stair-steps or speckle), 'jag' = fraction of edge pixels with a diagonal-only continuation (a stair-step
    signature), 'edge_density' = fraction of strong-edge pixels. A relative measure: compare the same region between
    runs and between 1080p and 4K (the temporal shimmer test is in sequence_metrics.py)."""
    h, w = lum.shape
    x0, y0, x1, y1 = rect_px(rect, w, h)
    a = lum[y0:y1, x0:x1].astype(np.float32)
    if a.shape[0] < 5 or a.shape[1] < 5:
        return {"hf": 0.0, "jag": 0.0, "edge_density": 0.0}
    gx = np.abs(np.diff(a, axis=1))[:-1, :]
    gy = np.abs(np.diff(a, axis=0))[:, :-1]
    g = gx + gy
    lap = np.abs(a[1:-1, :-2] + a[1:-1, 2:] + a[:-2, 1:-1] + a[2:, 1:-1] - 4.0 * a[1:-1, 1:-1])
    gm = float(g.mean())
    hf = float(lap.mean()) / max(gm, 1e-3)
    edges = g > 24.0
    dens = float(edges.mean())
    jag = 0.0
    if edges.any():
        e = edges[:-1, :-1]
        right = edges[:-1, 1:]
        down = edges[1:, :-1]
        diag = edges[1:, 1:]
        stair = e & diag & ~right & ~down
        jag = float(stair.sum()) / max(1, int(e.sum()))
    return {"hf": round(hf, 4), "jag": round(jag, 4), "edge_density": round(dens, 4)}


# ---------------------------------------------------------------- blobs (glints) without scipy
def blobs(mask, min_px=3, max_px=12):
    """Connected components (8-neighbour) of a sparse boolean mask, union-find over the set pixels only.
    Returns (count with min_px <= size <= max_px, total components, sizes array)."""
    ys, xs = np.nonzero(mask)
    n = len(ys)
    if n == 0:
        return 0, 0, np.zeros(0, dtype=np.int64)
    w = mask.shape[1]
    lin = ys.astype(np.int64) * w + xs
    parent = list(range(n))

    def find(i):
        root = i
        while parent[root] != root:
            root = parent[root]
        while parent[i] != root:
            parent[i], i = root, parent[i]
        return root

    pos = {int(v): i for i, v in enumerate(lin)}
    for i in range(n):
        y, x = int(ys[i]), int(xs[i])
        for dy, dx in ((0, -1), (-1, -1), (-1, 0), (-1, 1)):
            yy, xx = y + dy, x + dx
            if yy < 0 or xx < 0 or xx >= w:
                continue
            j = pos.get(yy * w + xx)
            if j is not None:
                ri, rj = find(i), find(j)
                if ri != rj:
                    parent[max(ri, rj)] = min(ri, rj)
    roots = np.array([find(i) for i in range(n)])
    _, sizes = np.unique(roots, return_counts=True)
    ok = int(((sizes >= min_px) & (sizes <= max_px)).sum())
    return ok, int(len(sizes)), sizes


# ---------------------------------------------------------------- masks from projected geometry
def polygon_mask(w, h, pts_uv):
    """Boolean mask of a polygon given in pixel coordinates (u right, v down)."""
    from PIL import ImageDraw
    im = Image.new("L", (int(w), int(h)), 0)
    ImageDraw.Draw(im).polygon([(float(u), float(v)) for u, v in pts_uv], fill=255)
    return np.asarray(im) > 127


def disc_mask(cam, centre, normal, radius, n=64):
    """Mask of a world disc (for example the 0.55 m porthole opening) projected through cam (bent like the shaders);
    None when part of it is behind the camera."""
    c = np.asarray(centre, dtype=np.float64)
    nn = np.asarray(normal, dtype=np.float64)
    nn = nn / max(np.linalg.norm(nn), 1e-9)
    a = np.cross([0.0, 1.0, 0.0], nn)
    a = a / max(np.linalg.norm(a), 1e-9)
    b = np.cross(nn, a)
    ang = np.linspace(0, 2 * math.pi, n, endpoint=False)
    ring = c[None, :] + (np.cos(ang)[:, None] * a[None, :] + np.sin(ang)[:, None] * b[None, :]) * radius
    ring = bend(ring, np.asarray(cam["eye"], dtype=np.float64))
    u, v, z = project(cam, ring)
    if (z <= 0).any():
        return None
    return polygon_mask(cam["w"], cam["h"], list(zip(u, v)))


def terrain_edge_points(cam, n=12, extent=CLIP_EXTENT):
    """Pixel positions of the terrain-clipmap edge (square half-extent 8 192 m around the camera, bent) at n azimuths
    spread over the central 80 % of the horizontal field of view. Returns a list of (u, v)."""
    eye = np.asarray(cam["eye"], dtype=np.float64)
    ty = math.tan(math.radians(cam["vfov"]) / 2.0)
    hfov = math.degrees(2.0 * math.atan(ty * cam["w"] / cam["h"]))
    out = []
    for k in range(n):
        off = (k / (n - 1) - 0.5) * 0.8 * hfov
        phi = math.radians(cam["yaw"] + off)
        sx, cz = math.sin(phi), math.cos(phi)
        dist = extent / max(abs(sx), abs(cz), 1e-6)
        p = np.array([[eye[0] + sx * dist, 0.0, eye[2] + cz * dist]])
        u, v, z = project(cam, bend(p, eye))
        if z[0] > 0:
            out.append((float(u[0]), float(v[0])))
    return out


SEAM_PATCH_MAX_STD = 2.0    # luma std of a 5 x 5 seam patch above which it straddles an occluder edge (not the seam)


def horizon_pairs(rgb, cam, gap=4, half=2, n=12, mask=None, max_std=SEAM_PATCH_MAX_STD):
    """Seam pairs across the terrain edge (Part 10 #13, 14.5.2 (4)): the mean colour of a (2 half + 1)^2 patch `gap` px
    below the projected edge (fogged terrain) against the same patch `gap` px above it (sky / arch); CIE76 dE each.
    n edge samples over the central 80 % of the horizontal FOV. A pair is dropped (not evaluable, never a pass or a
    failure) when either patch leaves the frame, leaves `mask` (for example the KS01 porthole opening), or has a luma
    standard deviation above max_std (the patch straddles an occluder edge: a rib, a frame, a rail, a silhouette).
    Returns (pairs, dropped) where dropped counts the reasons."""
    h, w = rgb.shape[:2]
    pairs = []
    dropped = {"outside_frame": 0, "outside_mask": 0, "occluder_edge": 0}
    lum = luma(rgb)
    for u, v in terrain_edge_points(cam, n=n):
        iu, iv = int(round(u)), int(round(v))
        lo, hi = iv + gap, iv - gap
        if iu - half < 0 or iu + half >= w or hi - half < 0 or lo + half >= h:
            dropped["outside_frame"] += 1
            continue
        ys, xs = slice(lo - half, lo + half + 1), slice(iu - half, iu + half + 1)
        ya = slice(hi - half, hi + half + 1)
        if mask is not None and not (mask[ys, xs].all() and mask[ya, xs].all()):
            dropped["outside_mask"] += 1
            continue
        sb, sa = float(lum[ys, xs].std()), float(lum[ya, xs].std())
        if sb > max_std or sa > max_std:
            dropped["occluder_edge"] += 1
            continue
        below = rgb[ys, xs].reshape(-1, 3).astype(np.float64).mean(axis=0)
        above = rgb[ya, xs].reshape(-1, 3).astype(np.float64).mean(axis=0)
        pairs.append({"u": iu, "v": iv, "below": [round(float(x), 1) for x in below], "above": [round(float(x), 1) for x in above],
                      "std_below": round(sb, 3), "std_above": round(sa, 3),
                      "de76": round(float(de76(srgb_to_lab(below), srgb_to_lab(above))), 3)})
    return pairs, dropped


def pixel_elevation(cam):
    """Elevation (degrees, up positive) of the view ray through every pixel centre: an h x w float32 array for the
    pinhole camera of project() (the sky dome is direction based, so no bend is involved)."""
    fwd, right, up = cam_basis(cam["yaw"], cam["pitch"])
    w, h = int(cam["w"]), int(cam["h"])
    ty = math.tan(math.radians(cam["vfov"]) / 2.0)
    tx = ty * w / h
    x = ((np.arange(w, dtype=np.float64) + 0.5) / w * 2.0 - 1.0) * tx
    y = (1.0 - (np.arange(h, dtype=np.float64) + 0.5) / h * 2.0) * ty
    dy = fwd[1] + y[:, None] * up[1] + x[None, :] * right[1]
    norm = np.sqrt(1.0 + x[None, :] ** 2 + y[:, None] ** 2)
    return np.degrees(np.arcsin(np.clip(dy / norm, -1.0, 1.0))).astype(np.float32)


def downsample2(a):
    """2 x 2 box downsample (the 4 x supersampled reference of the shimmer test)."""
    a = np.asarray(a, dtype=np.float32)
    h2, w2 = a.shape[0] // 2, a.shape[1] // 2
    a = a[:h2 * 2, :w2 * 2]
    return a.reshape(h2, 2, w2, 2, *a.shape[2:]).mean(axis=(1, 3))


# ---------------------------------------------------------------- runs
def captures_root():
    return os.path.join(ROOT, "QA", "captures")


def list_runs():
    """Run folders under QA/captures, oldest first (by report.json or folder modification time)."""
    root = captures_root()
    if not os.path.isdir(root):
        return []
    runs = [os.path.join(root, d) for d in os.listdir(root) if os.path.isdir(os.path.join(root, d))]

    def stamp(p):
        r = os.path.join(p, "report.json")
        return os.path.getmtime(r) if os.path.exists(r) else os.path.getmtime(p)

    return sorted(runs, key=stamp)


def resolve_run(arg):
    """A run folder from an argument: a path, a run id under QA/captures, or 'latest'."""
    if arg in (None, "", "latest"):
        runs = list_runs()
        return runs[-1] if runs else None
    if os.path.isdir(arg):
        return os.path.abspath(arg)
    p = os.path.join(captures_root(), arg)
    return p if os.path.isdir(p) else None


def previous_run(run):
    """The newest other run folder that is not newer than `run` (for the regression diff), or None."""
    a = os.path.abspath(run)
    runs = [r for r in list_runs() if os.path.abspath(r) != a]
    older = [r for r in runs if os.path.getmtime(r) <= os.path.getmtime(a)]
    return older[-1] if older else None


def fmt(v, nd=2):
    if v is None:
        return "-"
    if isinstance(v, bool):
        return "yes" if v else "no"
    if isinstance(v, float):
        if v != v:
            return "nan"
        return ("%." + str(nd) + "f") % v
    return str(v)
