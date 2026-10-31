"""Encode test: banding must survive YouTube (Part 11.12, 14.10 "Encode test", defect 10.37).

Usage:
    python -I Tools/qa/encode_test.py QA/sequences/<name> [--fps 60] [--name KS2a] [--out QA/encode/<name>]
           [--beat 2 [--zone tunnel|outside]] [--band fog=0,0,1,0.3 ...]
           [--x264-crf 20,23,30] [--preset medium] [--vp9 auto|on|off] [--vp9-crf 31]
           [--gate x264:20] [--limit-px 24] [--stride 1] [--max-frames N] [--png-every 60] [--crop 256]
           [--ffmpeg PATH] [--keep-video]

What it does:
1. Reads the PNG sequence (natural order) and pipes the frames (rgb24) into ffmpeg: H.264 (libx264, -preset medium,
   yuv420p, one encode per CRF, default 20 / 23 / 30) and VP9 (libvpx-vp9, constant quality, if the encoder exists).
   RGB -> YUV uses BT.709 limited range (what YouTube delivers) and the streams are tagged BT.709.
2. Decodes every encode back to RGB (BT.709) and saves decoded PNGs (every --png-every frames plus the worst frame).
3. Runs the BandScore of 14.10 on the source frames and on every decoded frame (every --stride frames) inside the
   gradient regions (--band regions, else the beat's default regions, else the whole frame). --band takes
   name=x0,y0,x1,y1 (fractions 0..1) or name=px:x0,y0,x1,y1; a region under 8 x 8 px is bad input (exit 2).
4. Pass / fail (recalibrated, DEVELOPMENT.md "Encode test and dither decision"): in a qualifying line (total change
   > 24 levels) of a decoded frame, a run of identical 8-bit values longer than --limit-px (24 px at 1080p, scaled
   with the frame height) counts as a band only if it ends in a step of >= 2 levels that the source frame does not
   have, or if it is longer than max(limit, 1.5 / local slope), the local slope (levels per pixel) being measured on
   the dithered source frame over the run +/- 48 px (abyss_qa.encode_band_runs). No band = pass. The raw longest
   run is reported as information; --plain-rule judges every run over the limit (the old rule, for comparison).
   The encodes listed in --gate decide the exit code (default x264:20, the 14.10 rule); the others (11.12 CRF 23 /
   30, VP9) are reported with the same rule.
5. Writes side-by-side crops of the worst gradient location: source and every encode at 100 %, 200 % and 200 % with
   the contrast stretched x8 around the local mean (makes step edges obvious): <out>/crops_worst.png.

Outputs: <out>/encode.json, <out>/summary.md, <out>/crops_worst.png, <out>/<encode>/decoded_*.png.
Exit code: 0 = gated encodes pass, 1 = a gated encode fails, 2 = bad input, 3 = no encoder available (the 11.12
fallback then applies: halve the 8-bit BandScore thresholds and record it in DEVELOPMENT.md).
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
import subprocess  # noqa: E402
from typing import Any, Dict, List, Optional, Tuple  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

# Accurate swscale paths: the default (fast) RGB <-> YUV conversion of ffmpeg darkens dark frames by 1.0-1.4 levels
# per channel (measured on a lossless -qp 0 round trip: bias -1.37/-1.24/-1.05, with these flags +0.21/+0.06/-0.02).
# A recorder and the YouTube player convert accurately, so the encoder's own losses are isolated.
SWS = ":flags=accurate_rnd+full_chroma_int+bitexact"
ENC_VF = "scale=out_color_matrix=bt709:out_range=tv" + SWS + ",format=yuv420p"
DEC_VF = "scale=in_color_matrix=bt709:in_range=tv" + SWS + ",format=rgb24"
COLOR_TAGS = ["-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv"]

# The recalibrated encode gate (DEVELOPMENT.md "Encode test and dither decision"; abyss_qa.encode_band_runs).
ENCODE_RULE = ("a decoded run over the limit counts as a band only if it ends in a step of >= %d levels that the "
               "source does not have (source change across the boundary < %g levels), or if it is longer than "
               "max(limit, %g / local slope), the slope being a least-squares fit on the dithered source over the "
               "run +/- %d px (cut at source edges > %g levels); the raw longest run is reported as information"
               % (qa.ENCODE_STEP_LEVELS, qa.ENCODE_SRC_EDGE_LEVELS, qa.ENCODE_PLATEAU_FACTOR,
                  qa.ENCODE_SLOPE_HALF_PX, qa.BAND_EDGE_LEVELS))


class Worst:
    """Tracks the worst banding run seen over frames and regions (and the diagnostic low-contrast run)."""

    def __init__(self) -> None:
        self.run = 0
        self.frame = -1
        self.info: Dict[str, Any] = {}
        self.diag_run = 0
        self.diag_info: Dict[str, Any] = {}
        self.frames_scored = 0
        self.qual_lines_min: Optional[int] = None
        self.qual_lines_sum = 0
        self.lines_over_limit_max = 0   # most qualifying lines above the limit in one frame and region

    def update(self, frame: int, region: str, r: Dict[str, Any], diag: Optional[Dict[str, Any]]) -> None:
        q = int(r["qualifying_lines"])
        self.qual_lines_min = q if self.qual_lines_min is None else min(self.qual_lines_min, q)
        self.qual_lines_sum += q
        self.lines_over_limit_max = max(self.lines_over_limit_max, int(r.get("lines_over_limit") or 0))
        if r["max_run"] > self.run:
            self.run = int(r["max_run"])
            self.frame = frame
            self.info = {"region": region, "x": r["x"], "y": r["y"], "channel": r["channel"], "value": r["value"],
                         "axis": r["axis"]}
        if diag is not None and diag["max_run"] > self.diag_run:
            self.diag_run = int(diag["max_run"])
            self.diag_info = {"frame": frame, "region": region, "x": diag["x"], "y": diag["y"],
                              "channel": diag["channel"]}

    def as_dict(self, n_regions: int) -> Dict[str, Any]:
        calls = max(1, self.frames_scored * max(1, n_regions))
        return {"max_run": self.run, "frame": self.frame, "where": self.info, "frames_scored": self.frames_scored,
                "qualifying_lines_min": self.qual_lines_min, "qualifying_lines_mean": self.qual_lines_sum / calls,
                "lines_over_limit_max": self.lines_over_limit_max,
                "diag_low_contrast_max_run": self.diag_run, "diag_where": self.diag_info}


def score_frame(rgb8: np.ndarray, regions: Dict[str, Any], worst: Worst, frame: int, diag_change: int,
                limit: float) -> None:
    h, w = rgb8.shape[:2]
    for name, shape in regions.items():
        box, m = qa.region_mask(shape, w, h)
        r = qa.banding_score(rgb8, box, m, top_n=1, limit_px=limit)
        d = qa.banding_score(rgb8, box, m, change_levels=diag_change, top_n=1, limit_px=limit) \
            if diag_change > 0 else None
        worst.update(frame, name, r, d)
    worst.frames_scored += 1


class EncWorst:
    """Decoded-frame bands under the recalibrated encode rule (abyss_qa.encode_band_runs): the judged longest band
    run, the raw longest run (information), counts."""

    def __init__(self) -> None:
        self.band = 0
        self.band_frame = -1
        self.band_where: Dict[str, Any] = {}
        self.raw = 0
        self.raw_frame = -1
        self.raw_where: Dict[str, Any] = {}
        self.band_runs = 0
        self.by_step = 0
        self.by_length = 0
        self.candidates = 0
        self.frames_scored = 0
        self.frames_with_bands = 0

    def add(self, frame: int, region: str, r: Dict[str, Any]) -> bool:
        self.band_runs += int(r["band_runs"])
        self.by_step += int(r["band_by_step"])
        self.by_length += int(r["band_by_length"])
        self.candidates += int(r["candidates"])
        if r["raw_max_run"] > self.raw:
            self.raw, self.raw_frame = int(r["raw_max_run"]), frame
            self.raw_where = dict(r.get("raw_where") or {}, region=region)
        if r["band_max_run"] > self.band:
            self.band, self.band_frame = int(r["band_max_run"]), frame
            self.band_where = dict(r.get("where") or {}, region=region)
        return r["band_runs"] > 0

    def as_dict(self) -> Dict[str, Any]:
        return {"max_run": self.band, "frame": self.band_frame if self.band > 0 else self.raw_frame,
                "where": self.band_where if self.band > 0 else self.raw_where,
                "band_max_run": self.band, "band_frame": self.band_frame, "band_where": self.band_where,
                "band_runs": self.band_runs, "band_runs_by_step": self.by_step,
                "band_runs_by_length": self.by_length, "frames_with_bands": self.frames_with_bands,
                "raw_max_run": self.raw, "raw_frame": self.raw_frame, "raw_where": self.raw_where,
                "runs_over_limit": self.candidates, "frames_scored": self.frames_scored}


def score_decoded(dec: np.ndarray, src: Optional[np.ndarray], regions: Dict[str, Any], worst: EncWorst, frame: int,
                  limit: float) -> None:
    h, w = dec.shape[:2]
    banded = False
    for name, shape in regions.items():
        box, m = qa.region_mask(shape, w, h)
        r = qa.encode_band_runs(dec, src, box, m, limit)
        banded = worst.add(frame, name, r) or banded
    worst.frames_scored += 1
    if banded:
        worst.frames_with_bands += 1


def available_encoders(ffmpeg: str) -> Tuple[bool, bool]:
    try:
        out = subprocess.run([ffmpeg, "-hide_banner", "-encoders"], capture_output=True, text=True, timeout=60).stdout
    except Exception:
        return False, False
    return (" libx264 " in out), (" libvpx-vp9 " in out)


def encoder_cmd(ffmpeg: str, kind: str, crf: int, preset: str, w: int, h: int, fps: float, out: str) -> List[str]:
    base = [ffmpeg, "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
            "-s", "%dx%d" % (w, h), "-framerate", "%g" % fps, "-i", "-", "-vf", ENC_VF]
    if kind == "x264":
        return base + ["-c:v", "libx264", "-preset", preset, "-crf", str(crf), "-pix_fmt", "yuv420p"] + COLOR_TAGS + \
            ["-movflags", "+faststart", out]
    return base + ["-c:v", "libvpx-vp9", "-crf", str(crf), "-b:v", "0", "-deadline", "good", "-cpu-used", "4",
                   "-row-mt", "1", "-pix_fmt", "yuv420p"] + COLOR_TAGS + [out]


def decode_iter(ffmpeg: str, video: str, w: int, h: int, log_path: str, vf_prefix: str = ""):
    """Yield decoded RGB frames (uint8 H x W x 3) of a video, BT.709 limited range -> full-range RGB."""
    vf = (vf_prefix + "," if vf_prefix else "") + DEC_VF
    cmd = [ffmpeg, "-hide_banner", "-loglevel", "error", "-i", video, "-vf", vf, "-f", "rawvideo", "-pix_fmt",
           "rgb24", "-"]
    frame_bytes = w * h * 3
    with open(log_path, "ab") as log:
        p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=log)
        try:
            while True:
                buf = p.stdout.read(frame_bytes)
                if not buf or len(buf) < frame_bytes:
                    break
                yield np.frombuffer(buf, dtype=np.uint8).reshape(h, w, 3)
        finally:
            p.stdout.close()
            if p.poll() is None:      # stopped early (extract_frame): do not wait for the rest of the stream
                p.kill()
            p.wait()


def extract_frame(ffmpeg: str, video: str, index: int, w: int, h: int, log_path: str) -> Optional[np.ndarray]:
    for f in decode_iter(ffmpeg, video, w, h, log_path, vf_prefix="select=eq(n\\,%d)" % index):
        return f.copy()
    return None


def crop_panel(rgb8: np.ndarray, cx: int, cy: int, c: int) -> List[Image.Image]:
    """Three views of the location: 100 % (c x c), 200 % (c/2 x c/2 nearest x2), 200 % contrast-stretched x8."""
    h, w = rgb8.shape[:2]

    def crop(size: int) -> np.ndarray:
        x0 = int(np.clip(cx - size // 2, 0, max(0, w - size)))
        y0 = int(np.clip(cy - size // 2, 0, max(0, h - size)))
        return rgb8[y0:y0 + size, x0:x0 + size]

    a = crop(c)
    b = crop(c // 2)
    b2 = np.repeat(np.repeat(b, 2, axis=0), 2, axis=1)
    m = b2.astype(np.float32).mean(axis=(0, 1), keepdims=True)
    s = np.clip(np.rint((b2.astype(np.float32) - m) * 8.0 + 128.0), 0, 255).astype(np.uint8)
    return [Image.fromarray(np.ascontiguousarray(a)), Image.fromarray(np.ascontiguousarray(b2)),
            Image.fromarray(np.ascontiguousarray(s))]


def build_crops(rows: List[Tuple[str, Optional[np.ndarray]]], cx: int, cy: int, c: int, title: str) -> Image.Image:
    label_w, head_h, gap = 230, 46, 6
    W = label_w + 3 * (c + gap) + gap
    H = head_h + len(rows) * (c + gap) + gap
    sheet = Image.new("RGB", (W, H), (14, 15, 18))
    d = ImageDraw.Draw(sheet)
    f = qa.font(14)
    d.text((gap, 6), title, fill=(230, 230, 230), font=f)
    for j, t in enumerate(["100 %", "200 %", "200 %, contrast x8"]):
        d.text((label_w + gap + j * (c + gap), 26), t, fill=(170, 170, 170), font=f)
    for i, (label, img) in enumerate(rows):
        y = head_h + gap + i * (c + gap)
        d.text((gap, y + 4), label, fill=(225, 228, 232), font=f)
        if img is None:
            d.text((label_w + gap, y + 4), "(frame unavailable)", fill=(210, 60, 60), font=f)
            continue
        for j, pnl in enumerate(crop_panel(img, cx, cy, c)):
            sheet.paste(pnl.resize((c, c), Image.Resampling.NEAREST) if pnl.size != (c, c) else pnl,
                        (label_w + gap + j * (c + gap), y))
    return sheet


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder")
    ap.add_argument("--fps", type=float, default=60.0)
    ap.add_argument("--name")
    ap.add_argument("--out")
    ap.add_argument("--beat", type=int)
    ap.add_argument("--zone", choices=["tunnel", "outside"])
    ap.add_argument("--band", action="append", default=[],
                    help="name=x0,y0,x1,y1 (fractions) or name=px:x0,y0,x1,y1 gradient region (repeatable)")
    ap.add_argument("--x264-crf", default="20,23,30")
    ap.add_argument("--preset", default="medium")
    ap.add_argument("--vp9", choices=["auto", "on", "off"], default="auto")
    ap.add_argument("--vp9-crf", type=int, default=31)
    ap.add_argument("--gate", default="x264:20", help="comma list of encodes that decide pass / fail")
    ap.add_argument("--limit-px", type=float, default=qa.ENCODE_BAND_LIMIT_PX_1080, help="at 1080p")
    ap.add_argument("--diag-change", type=int, default=8,
                    help="also report runs in lines with a change above this many levels (info only, 0 = off)")
    ap.add_argument("--stride", type=int, default=1, help="score every Nth frame")
    ap.add_argument("--max-frames", type=int)
    ap.add_argument("--png-every", type=int, default=60, help="save every Nth decoded frame as PNG (0 = none)")
    ap.add_argument("--crop", type=int, default=256, help="100 %% crop size at 1080p (scaled with height)")
    ap.add_argument("--ffmpeg")
    ap.add_argument("--keep-video", action="store_true", help="keep the encoded .mp4 / .webm files")
    ap.add_argument("--plain-rule", action="store_true",
                    help="judge every decoded run over the limit (the pre-calibration rule; for comparison only)")
    args = ap.parse_args(argv)

    folder = os.path.abspath(args.folder)
    files = qa.list_pngs(folder) if os.path.isdir(folder) else []
    if args.max_frames:
        files = files[:args.max_frames]
    if len(files) < 2:
        qa.eprint("need a folder with at least 2 PNG frames: %s" % folder)
        return 2
    name = args.name or os.path.basename(folder)
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", name)
    out_dir = qa.ensure_dir(os.path.abspath(args.out) if args.out else os.path.join(qa.QA_DIR, "encode", safe))
    log_path = os.path.join(out_dir, "ffmpeg.log")
    if os.path.exists(log_path):
        os.remove(log_path)

    first = qa.load_rgb8(files[0])
    h, w = first.shape[:2]
    if w % 2 or h % 2:
        qa.eprint("yuv420p needs even dimensions, got %dx%d" % (w, h))
        return 2
    limit = args.limit_px * h / 1080.0
    still_limit = qa.band_limit_px(h)

    # gradient regions
    regions: Dict[str, Any] = {}
    try:
        for i, t in enumerate(args.band):
            n, r = qa.parse_rect_arg(t)
            regions[n or "band%d" % i] = r
    except ValueError as e:
        qa.eprint("bad --band argument: %s" % e)
        return 2
    if not regions and args.beat:
        shot = {"tag": name, "zone": args.zone} if args.zone else {"tag": name}
        key = qa.threshold_key(args.beat, shot)
        regions = dict(qa.DEFAULT_BAND_REGIONS.get(key, {})) if key else {}
    if not regions:
        regions = {"full_frame": [0.0, 0.0, 1.0, 1.0]}
    bad = [(rn, qa.resolve_region(shape, w, h)[2]) for rn, shape in regions.items()]
    bad = [(rn, err) for rn, err in bad if err]
    if bad:
        for rn, err in bad:
            qa.eprint("gradient region %s unusable on %dx%d frames: %s" % (rn, w, h, err))
        return 2

    ffmpeg = args.ffmpeg or qa.find_ffmpeg()
    has264, hasvp9 = available_encoders(ffmpeg) if ffmpeg else (False, False)
    encodes: List[Tuple[str, int]] = []
    if has264:
        encodes += [("x264", int(c)) for c in args.x264_crf.split(",") if c.strip()]
    if args.vp9 == "on" or (args.vp9 == "auto" and hasvp9):
        if hasvp9:
            encodes.append(("vp9", args.vp9_crf))
    gates = set(g.strip() for g in args.gate.split(",") if g.strip())

    result: Dict[str, Any] = {
        "name": name, "source": folder, "generated": qa.now_iso(), "tool": "encode_test.py",
        "tool_version": qa.TOOL_VERSION, "frames": len(files), "fps": args.fps, "size": [w, h],
        "regions": regions, "limit_px": limit, "limit_px_1080": args.limit_px, "still_limit_px": still_limit,
        "ffmpeg": ffmpeg, "encoders_available": {"libx264": has264, "libvpx-vp9": hasvp9}, "gates": sorted(gates),
        "settings": {"preset": args.preset, "x264_crf": args.x264_crf, "vp9_crf": args.vp9_crf,
                     "stride": args.stride, "rgb_to_yuv": "BT.709 limited range, yuv420p"},
    }
    if not encodes:
        result["error"] = "no encoder available (ffmpeg with libx264 / libvpx-vp9 not found)"
        result["fallback"] = "Part 11.12 step 4: record in DEVELOPMENT.md and apply a safety factor of 2 to the " \
                             "8-bit BandScore thresholds (6 px at 1080p for stills)."
        result["pass"] = False
        qa.write_json(os.path.join(out_dir, "encode.json"), result)
        print("encode_test: no encoder available -> %s" % out_dir)
        return 3

    # ---- pass 1: read source frames once, score them, feed every encoder
    procs = []
    for kind, crf in encodes:
        label = "%s_crf%d" % (kind, crf)
        video = os.path.join(out_dir, label + (".mp4" if kind == "x264" else ".webm"))
        log = open(log_path, "ab")
        p = subprocess.Popen(encoder_cmd(ffmpeg, kind, crf, args.preset, w, h, args.fps, video),
                             stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=log)
        procs.append({"kind": kind, "crf": crf, "label": label, "video": video, "proc": p, "log": log, "error": None})
    src_worst = Worst()
    for i, path in enumerate(files):
        rgb8 = first if i == 0 else qa.load_rgb8(path)
        if rgb8.shape[:2] != (h, w):
            for e in procs:
                e["proc"].kill()
            qa.eprint("frame %s has size %dx%d, expected %dx%d" % (path, rgb8.shape[1], rgb8.shape[0], w, h))
            return 2
        if i % max(1, args.stride) == 0:
            score_frame(rgb8, regions, src_worst, i, args.diag_change, limit)
        data = np.ascontiguousarray(rgb8).tobytes()
        for e in procs:
            if e["error"]:
                continue
            try:
                e["proc"].stdin.write(data)
            except (BrokenPipeError, OSError) as ex:
                e["error"] = "encoder pipe closed at frame %d: %s" % (i, ex)
        if (i + 1) % 120 == 0:
            print("  encoded %d / %d frames" % (i + 1, len(files)))
    for e in procs:
        try:
            e["proc"].stdin.close()
        except OSError:
            pass
        rc = e["proc"].wait()
        e["log"].close()
        if rc != 0 and not e["error"]:
            e["error"] = "ffmpeg exited with %d (see ffmpeg.log)" % rc
    result["source_banding"] = src_worst.as_dict(len(regions))
    result["source_banding"]["pass_still_limit"] = bool(src_worst.run <= still_limit)
    result["source_banding"]["pass_encode_limit"] = bool(src_worst.run <= limit)

    # ---- pass 2: decode each encode, score, save sample PNGs
    enc_results = []
    for e in procs:
        er: Dict[str, Any] = {"encode": e["label"], "codec": e["kind"], "crf": e["crf"],
                              "gated": e["label"].replace("_crf", ":") in gates or e["label"] in gates}
        if e["error"]:
            er.update({"error": e["error"], "pass": False})
            enc_results.append(er)
            continue
        er["video_bytes"] = os.path.getsize(e["video"]) if os.path.exists(e["video"]) else 0
        er["bitrate_kbps"] = er["video_bytes"] * 8.0 / 1000.0 / (len(files) / args.fps)
        wst = EncWorst()
        dec_dir = qa.ensure_dir(os.path.join(out_dir, e["label"]))
        n_dec = 0
        for i, fr in enumerate(decode_iter(ffmpeg, e["video"], w, h, log_path)):
            n_dec += 1
            if i % max(1, args.stride) == 0:
                src = qa.load_rgb8(files[i]) if (i < len(files) and not args.plain_rule) else None
                score_decoded(fr, src, regions, wst, i, limit)
            if args.png_every and i % args.png_every == 0:
                Image.fromarray(fr).save(os.path.join(dec_dir, "decoded_%05d.png" % i))
        er["decoded_frames"] = n_dec
        er.update(wst.as_dict())
        f_worst = er["frame"]
        if f_worst >= 0:
            fr = extract_frame(ffmpeg, e["video"], f_worst, w, h, log_path)
            if fr is not None:
                Image.fromarray(fr).save(os.path.join(dec_dir, "worst_%05d.png" % f_worst))
        er["limit_px"] = limit
        er["rule"] = "plain (every run over the limit)" if args.plain_rule else ENCODE_RULE
        er["pass"] = bool(n_dec >= len(files) - 1 and wst.band == 0)
        if n_dec < len(files) - 1:
            er["error"] = "decoded %d of %d frames" % (n_dec, len(files))
        enc_results.append(er)
        print("  %-12s band run %3d px, %d band runs in %d frames (raw max run %3d px, info; limit %.1f) %s" % (
            e["label"], wst.band, wst.band_runs, wst.frames_with_bands, wst.raw, limit,
            "PASS" if er["pass"] else "FAIL"))
    result["encodes"] = enc_results

    # ---- crops of the worst location (prefer a failing gated encode, else the overall worst)
    cands = [r for r in enc_results if r.get("frame", -1) >= 0]
    pick = None
    failing_gated = [r for r in cands if r["gated"] and not r["pass"]]
    if failing_gated:
        pick = max(failing_gated, key=lambda r: r["max_run"])
    elif cands:
        pick = max(cands, key=lambda r: (r["max_run"], r.get("raw_max_run", 0)))
    if pick is not None and (pick.get("where") or {}).get("x") is not None:
        f_idx = pick["frame"]
        run_len = int(pick["max_run"]) if pick["max_run"] > 0 else int(pick.get("raw_max_run", 0))
        what = ("band run %d px (%s)" % (run_len, pick["where"].get("reason"))) if pick["max_run"] > 0 else \
            ("no band; raw longest run %d px" % run_len)
        cx = int(pick["where"]["x"])
        cy = int(pick["where"]["y"]) + run_len // 2
        c = max(32, int(round(args.crop * h / 1080.0)) // 2 * 2)
        rows: List[Tuple[str, Optional[np.ndarray]]] = [("source  frame %d" % f_idx, qa.load_rgb8(files[f_idx]))]
        for e in procs:
            img = None if e["error"] else extract_frame(ffmpeg, e["video"], f_idx, w, h, log_path)
            rows.append((e["label"], img))
        title = "%s: %s (%s) at x=%d y=%d frame %d, region %s" % (
            name, what, pick["encode"], cx, int(pick["where"]["y"]), f_idx, pick["where"].get("region"))
        crops = build_crops(rows, cx, cy, c, title)
        crops.save(os.path.join(out_dir, "crops_worst.png"))
        result["crops"] = {"file": os.path.join(out_dir, "crops_worst.png"), "frame": f_idx, "center": [cx, cy],
                           "size": c, "from": pick["encode"]}

    if not args.keep_video:
        for e in procs:
            try:
                os.remove(e["video"])
            except OSError:
                pass
    gated = [r for r in enc_results if r["gated"]]
    result["pass"] = bool(gated) and all(r["pass"] for r in gated)
    if not gated:
        result["note"] = "no gated encode ran (check --gate against the encodes)"
    qa.write_json(os.path.join(out_dir, "encode.json"), result)

    rows_md = [["source (PNG)", "-", "%d" % src_worst.run, "%d (still rule, R1-38)" % src_worst.run,
                "-", "%.1f / %.1f" % (still_limit, limit), "-", "still %s / encode %s" % (
                    "PASS" if result["source_banding"]["pass_still_limit"] else "FAIL",
                    "PASS" if result["source_banding"]["pass_encode_limit"] else "FAIL")]]
    for r in enc_results:
        rows_md.append([r["encode"], "yes" if r["gated"] else "no", r.get("band_max_run", "-"),
                        r.get("raw_max_run", "-"),
                        "%s (%s step / %s length) in %s frames" % (r.get("band_runs", "-"), r.get("band_runs_by_step", "-"),
                                                                   r.get("band_runs_by_length", "-"),
                                                                   r.get("frames_with_bands", "-")),
                        "%.1f" % limit, "%.0f" % r["bitrate_kbps"] if "bitrate_kbps" in r else "-",
                        ("PASS" if r["pass"] else "FAIL") + ((" (" + r["error"] + ")") if r.get("error") else "")])
    md = ["# Encode test - %s" % name, "", "Source: `%s` (%d frames, %dx%d, %g fps)  " % (folder, len(files), w, h,
                                                                                         args.fps),
          "Regions: %s  " % ", ".join("%s=%s" % (k, v) for k, v in regions.items()),
          "Rule (14.10, recalibrated per DEVELOPMENT.md): in a qualifying line (change > 24 levels) of a decoded frame, "
          "%s. Limit %.1f px; gated encodes: %s." % (
              "every run over the limit counts (plain rule)" if args.plain_rule else ENCODE_RULE, limit,
              ", ".join(sorted(gates))), "",
          qa.md_table(["Encode", "Gated", "Band run px (judged)", "Raw max run px (info)", "Band runs",
                       "Limit px", "kbps", "Result"], rows_md), "",
          "Crops: `crops_worst.png` (100 %, 200 %, 200 % contrast x8)." if "crops" in result else "",
          "", "Overall: **%s**" % ("PASS" if result["pass"] else "FAIL")]
    qa.write_text(os.path.join(out_dir, "summary.md"), "\n".join(md) + "\n")
    print("encode_test: %s -> %s" % ("PASS" if result["pass"] else "FAIL", out_dir))
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
