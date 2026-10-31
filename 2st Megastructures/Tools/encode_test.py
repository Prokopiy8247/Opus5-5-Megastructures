"""Encode test: banding must survive a YouTube-like re-encode (MASTER_PROMPT Part 11.12, 14.7 H5, 14.10 BandScore).

Usage:
  python -I Tools/encode_test.py QA/captures/<run> [--clips enc_dusk,enc_night,enc_exit,enc_swarm] [--crf 23 30]
                                 [--no-vp9] [--frames N] [--every K] [--out DIR] [--no-fail]
  python -I Tools/encode_test.py --folder <PNG sequence folder> --fps 60 [--region x0,y0,x1,y1] ...
  (<run> may also be a run id under QA/captures or "latest")

For every encode clip (the harness writes seqenc_*/frame_00000.png ... plus sequence.json with fps and band_region):
  1. encodes with ffmpeg (on PATH, else the binary of the pip package imageio-ffmpeg) to H.264 (libx264, -preset medium,
     CRF 23 and 30, yuv420p, BT.709 limited range like YouTube uploads) and, when libvpx-vp9 exists, to VP9 at the same
     CRF values (-b:v 0, -deadline good -cpu-used 4);
  2. decodes back to RGB (rawvideo pipe, BT.709) and computes the BandScore of 14.10 in the clip's band region on
     every K-th decoded frame and on the same source PNGs (limit 6 px at 1080p, 12 px at 4K);
  3. writes side-by-side crops of the middle frame at 100 % and 200 % (plain and contrast-enhanced x 6 around the
     crop mean, so plateaus are easy to see) for Part 11.4 inspection.
Pass: the source and every CRF 23 decode within the limit (14.10 names x264 CRF 23); CRF 30 results are reported
(advisory). If no encoder can be found, the report says so and Part 11.12 (4) applies (record it in DEVELOPMENT.md and
use a safety factor of 2 on the 8-bit BandScore limits); the tool then checks the PNGs against the halved limit.
Writes DIR/encode_report.json and DIR/encode_report.md (default DIR = QA/encode/<run>).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # python -I drops the script folder from sys.path
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
import qa_common as q  # noqa: E402

DEFAULT_CLIPS = ["enc_dusk", "enc_dusk30", "enc_night", "enc_exit", "enc_swarm"]


def find_ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe, "PATH"
    try:
        import imageio_ffmpeg  # type: ignore
        return imageio_ffmpeg.get_ffmpeg_exe(), "imageio-ffmpeg"
    except Exception:  # noqa: BLE001
        return None, "none"


def encoders(ffmpeg):
    try:
        out = subprocess.run([ffmpeg, "-hide_banner", "-encoders"], capture_output=True, text=True, timeout=60).stdout
    except Exception:  # noqa: BLE001
        return set()
    names = set()
    for line in out.splitlines():
        parts = line.split()
        if len(parts) >= 2 and len(parts[0]) == 6 and parts[0][0] in "VAS":
            names.add(parts[1])
    return names


def frame_list(folder):
    return sorted(f for f in os.listdir(folder) if f.startswith("frame_") and f.endswith(".png"))


def encode(ffmpeg, folder, fps, nframes, codec, crf, out):
    common = [ffmpeg, "-y", "-hide_banner", "-loglevel", "error", "-framerate", str(fps), "-start_number", "0",
              "-i", os.path.join(folder, "frame_%05d.png"), "-frames:v", str(nframes),
              "-vf", "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p",
              "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv"]
    if codec == "x264":
        cmd = common + ["-c:v", "libx264", "-preset", "medium", "-crf", str(crf), "-pix_fmt", "yuv420p", out]
    else:
        cmd = common + ["-c:v", "libvpx-vp9", "-crf", str(crf), "-b:v", "0", "-deadline", "good", "-cpu-used", "4",
                        "-row-mt", "1", "-pix_fmt", "yuv420p", out]
    t0 = time.time()
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode == 0, (r.stderr or "")[-2000:], time.time() - t0


def decode_frames(ffmpeg, path, w, h):
    """Generator of decoded RGB frames (h x w x 3 uint8)."""
    cmd = [ffmpeg, "-hide_banner", "-loglevel", "error", "-i", path,
           "-vf", "scale=in_color_matrix=bt709:in_range=tv,format=rgb24", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    size = w * h * 3
    try:
        while True:
            buf = p.stdout.read(size)
            if not buf or len(buf) < size:
                break
            yield np.frombuffer(buf, dtype=np.uint8).reshape(h, w, 3)
    finally:
        p.stdout.close()
        p.wait()


def crop_box(rect, w, h, cw, ch):
    x0, y0, x1, y1 = q.rect_px(rect, w, h)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    cw, ch = min(cw, w), min(ch, h)
    left = int(np.clip(cx - cw // 2, 0, w - cw))
    top = int(np.clip(cy - ch // 2, 0, h - ch))
    return left, top, left + cw, top + ch


def enhance(a):
    f = a.astype(np.float32)
    m = f.mean(axis=(0, 1), keepdims=True)
    return np.clip((f - m) * 6.0 + 128.0, 0, 255).astype(np.uint8)


def side_by_side(crops, labels, scale, enh):
    tiles = []
    for c in crops:
        a = enhance(c) if enh else c
        im = Image.fromarray(a)
        if scale != 1:
            im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        tiles.append(im)
    tw, th = tiles[0].size
    sheet = Image.new("RGB", (len(tiles) * (tw + 6) + 6, th + 30), (16, 16, 16))
    dr = ImageDraw.Draw(sheet)
    for i, (t, lab) in enumerate(zip(tiles, labels)):
        sheet.paste(t, (6 + i * (tw + 6), 24))
        dr.text((6 + i * (tw + 6), 5), lab, fill=(230, 230, 230))
    return sheet


def scores_for(frames_iter, rect, h, every, keep_index):
    vals, steps, kept = [], 0, None
    for i, rgb in enumerate(frames_iter):
        if i == keep_index:
            kept = rgb.copy()
        if i % every != 0 and i != keep_index:
            continue
        bs = q.band_score(q.luma(rgb), rect, h)
        vals.append(bs["score"])
        steps += bs["steps"]
    return vals, steps, kept


def summarize(vals, limit):
    if not vals:
        return {"frames_scored": 0, "max": None, "mean": None, "p95": None, "limit": limit, "pass": False}
    v = np.array(vals, dtype=np.float64)
    return {"frames_scored": int(len(v)), "max": float(v.max()), "mean": round(float(v.mean()), 3),
            "p95": float(np.percentile(v, 95)), "limit": limit, "pass": bool(v.max() <= limit)}


def run_clip(ffmpeg, encs, folder, meta, opts, outdir):
    name = meta.get("name") or os.path.basename(folder)
    files = frame_list(folder)
    if opts["frames"]:
        files = files[:opts["frames"]]
    n = len(files)
    res = {"clip": name, "folder": folder, "frames": n, "fps": meta.get("fps", 60), "variants": []}
    if n < 2:
        res["error"] = "fewer than 2 frames"
        return res
    first = q.load_rgb(os.path.join(folder, files[0]))
    h, w = first.shape[:2]
    br = meta.get("band_region") or [0.1, 0.05, 0.9, 0.35]
    rect = tuple(float(x) for x in br)
    limit = q.limit_for(h)
    res.update({"w": w, "h": h, "band_region": list(rect)})
    mid = n // 2
    every = max(1, opts["every"])

    # source PNGs
    def src_iter():
        for f in files:
            yield q.load_rgb(os.path.join(folder, f))
    vals, steps, src_mid = scores_for(src_iter(), rect, h, every, mid)
    lim_src = limit / 2.0 if ffmpeg is None else limit
    srow = summarize(vals, lim_src)
    srow.update({"variant": "png", "steps": steps, "required": True})
    res["variants"].append(srow)
    crops, labels = [src_mid], ["source PNG"]

    if ffmpeg is not None:
        variants = [("x264", c) for c in opts["crf"] if "libx264" in encs]
        if opts["vp9"] and "libvpx-vp9" in encs:
            variants += [("vp9", c) for c in opts["crf"]]
        for codec, crf in variants:
            ext = "mp4" if codec == "x264" else "webm"
            out = os.path.join(outdir, "%s_%s_crf%d.%s" % (name, codec, crf, ext))
            ok, err, secs = encode(ffmpeg, folder, res["fps"], n, codec, crf, out)
            row = {"variant": "%s_crf%d" % (codec, crf), "encode_ok": ok, "encode_seconds": round(secs, 1),
                   "required": crf == 23}
            if not ok:
                row.update({"error": err, "pass": False})
                res["variants"].append(row)
                continue
            size = os.path.getsize(out)
            row["bytes"] = size
            row["kbps"] = round(size * 8 / 1000.0 / max(n / float(res["fps"]), 1e-3), 1)
            vals, steps, dmid = scores_for(decode_frames(ffmpeg, out, w, h), rect, h, every, mid)
            row.update(summarize(vals, limit))
            row["steps"] = steps
            if dmid is not None and src_mid is not None:
                row["mid_mean_abs_diff"] = round(float(np.abs(dmid.astype(np.int16) - src_mid.astype(np.int16)).mean()), 4)
                crops.append(dmid)
                labels.append("%s CRF %d" % (codec, crf))
            res["variants"].append(row)
            if not opts["keep"]:
                try:
                    os.remove(out)
                except OSError:
                    pass

    # crops of the middle frame
    if src_mid is not None:
        cw, ch = (480, 270) if h < 2000 else (960, 540)
        x0, y0, x1, y1 = crop_box(rect, w, h, cw, ch)
        c100 = [c[y0:y1, x0:x1] for c in crops]
        hx0, hy0 = x0 + (x1 - x0) // 4, y0 + (y1 - y0) // 4
        c200 = [c[hy0:hy0 + (y1 - y0) // 2, hx0:hx0 + (x1 - x0) // 2] for c in crops]
        base = os.path.join(outdir, name)
        side_by_side(c100, labels, 1, False).save(base + "_crops_100.png")
        side_by_side(c100, labels, 1, True).save(base + "_crops_100_enhanced.png")
        side_by_side(c200, labels, 2, False).save(base + "_crops_200.png")
        side_by_side(c200, labels, 2, True).save(base + "_crops_200_enhanced.png")
        res["crops"] = [os.path.basename(base) + s for s in ("_crops_100.png", "_crops_100_enhanced.png", "_crops_200.png", "_crops_200_enhanced.png")]
    res["pass"] = all(v.get("pass", False) for v in res["variants"] if v.get("required"))
    return res


def write_md(path, report):
    lines = ["# Encode test - %s" % report.get("run", ""), "",
             "Generated %s by Tools/encode_test.py. ffmpeg: %s (%s); x264 %s; VP9 %s." % (
                 time.strftime("%Y-%m-%d %H:%M:%S"), report.get("ffmpeg") or "not found", report.get("ffmpeg_source"),
                 "yes" if report.get("x264") else "no", "yes" if report.get("vp9") else "no"), ""]
    if not report.get("ffmpeg"):
        lines += ["No encoder was found: Part 11.12 (4) applies (record it in DEVELOPMENT.md); the PNGs were checked against",
                  "half the BandScore limit (safety factor 2).", ""]
    lines += ["| clip | variant | frames scored | BandScore max | mean | limit | steps | kbps | result |", "|---|---|---|---|---|---|---|---|---|"]
    for c in report.get("clips", []):
        if "error" in c:
            lines.append("| %s | - | - | - | - | - | - | - | ERROR %s |" % (c["clip"], c["error"]))
            continue
        for v in c["variants"]:
            lines.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s%s |" % (
                c["clip"], v["variant"], q.fmt(v.get("frames_scored")), q.fmt(v.get("max"), 0), q.fmt(v.get("mean"), 2),
                q.fmt(v.get("limit"), 0), q.fmt(v.get("steps")), q.fmt(v.get("kbps"), 0),
                "pass" if v.get("pass") else "FAIL", "" if v.get("required") else " (advisory)"))
        if c.get("crops"):
            lines.append("")
            lines.append("Crops (%s): %s" % (c["clip"], ", ".join(c["crops"])))
            lines.append("")
    lines.append("")
    lines.append("Overall: %s" % ("pass" if report.get("pass") else "FAIL"))
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run", nargs="?", default=None)
    ap.add_argument("--folder", default=None, help="a PNG sequence folder (frame_00000.png ...) instead of a run")
    ap.add_argument("--fps", type=int, default=60)
    ap.add_argument("--region", default=None, help="band region x0,y0,x1,y1 (fractions) for --folder")
    ap.add_argument("--clips", default=",".join(DEFAULT_CLIPS))
    ap.add_argument("--crf", type=int, nargs="+", default=[23, 30])
    ap.add_argument("--no-vp9", action="store_true")
    ap.add_argument("--frames", type=int, default=0, help="use only the first N frames (0 = all)")
    ap.add_argument("--every", type=int, default=6, help="score every K-th frame")
    ap.add_argument("--keep", action="store_true", help="keep the encoded files")
    ap.add_argument("--out", default=None)
    ap.add_argument("--no-fail", action="store_true")
    a = ap.parse_args()

    ffmpeg, src = find_ffmpeg()
    encs = encoders(ffmpeg) if ffmpeg else set()
    opts = {"crf": a.crf, "vp9": not a.no_vp9, "frames": a.frames, "every": a.every, "keep": a.keep}
    clips = []
    if a.folder:
        folder = os.path.abspath(a.folder)
        meta = {"name": os.path.basename(folder.rstrip("\\/")), "fps": a.fps}
        sj = os.path.join(folder, "sequence.json")
        if os.path.exists(sj):
            with open(sj, "r", encoding="utf-8") as f:
                meta.update(json.load(f))
        if a.region:
            meta["band_region"] = [float(x) for x in a.region.split(",")]
        clips.append((folder, meta))
        run_name = meta["name"]
        outdir = a.out or os.path.join(q.ROOT, "QA", "encode", run_name)
    else:
        run = q.resolve_run(a.run or "latest")
        if not run:
            print("[encode_test] run folder not found: %s" % a.run)
            return 2
        run_name = os.path.basename(run)
        outdir = a.out or os.path.join(q.ROOT, "QA", "encode", run_name)
        for c in [x.strip() for x in a.clips.split(",") if x.strip()]:
            folder = os.path.join(run, "seq" + c)
            sj = os.path.join(folder, "sequence.json")
            if not os.path.exists(sj):
                continue
            with open(sj, "r", encoding="utf-8") as f:
                clips.append((folder, json.load(f)))
    if not clips:
        print("[encode_test] no encode clips found (capture them with -sequence encode)")
        return 2
    os.makedirs(outdir, exist_ok=True)
    t0 = time.time()
    results = [run_clip(ffmpeg, encs, f, m, opts, outdir) for f, m in clips]
    report = {"run": run_name, "ffmpeg": ffmpeg, "ffmpeg_source": src, "x264": "libx264" in encs, "vp9": "libvpx-vp9" in encs,
              "crf": a.crf, "clips": results, "seconds": round(time.time() - t0, 1),
              "pass": all(r.get("pass", False) for r in results)}
    if ffmpeg is None:
        report["note"] = "no encoder found: Part 11.12 (4) safety factor 2 applied to the PNG BandScore limits"
    q.write_json(os.path.join(outdir, "encode_report.json"), report)
    write_md(os.path.join(outdir, "encode_report.md"), report)
    print("[encode_test] %d clips, %s, %.1f s -> %s" % (len(results), "pass" if report["pass"] else "FAIL", report["seconds"], outdir))
    return 0 if (a.no_fail or report["pass"]) else 1


if __name__ == "__main__":
    sys.exit(main())
