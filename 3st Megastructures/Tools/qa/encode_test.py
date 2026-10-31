#!/usr/bin/env python3
"""encode_test: does the banding fix survive YouTube-like encoding? (MASTER_PROMPT 11.12, R6 section 5)

Usage:
  python -I Tools/qa/encode_test.py QA/captures/<run-id>               (seq_* folders with "encode": true or
                                                                         kind "encode"; --all-seqs for every one)
  python -I Tools/qa/encode_test.py QA/captures/<run-id>/seq_<name> [--region x0,y0,x1,y1[,v|h|auto]]
         [--codecs x264,vp9] [--crf-x264 23,30] [--crf-vp9 31,40] [--fps 60] [--sample 8] [--max-frames N]
         [--vp9-cpu-used 2] [--run <id>] [--out-dir QA/reports] [--work-dir QA/encode/<run>] [--keep-video]

For each PNG sequence: encode with ffmpeg to H.264 (libx264 -preset medium, CRF 23 and 30, yuv420p, BT.709 TV
range, exact R6 5.2 command lines) and VP9 (libvpx-vp9 -b:v 0, CRF 31 and 40) when the encoder exists, decode back
to full-range RGB, compute the BandScore (14.10.3 + R6 6.2) on the declared gradient regions of --sample evenly
spaced decoded frames and of the same source frames, and write side-by-side crops (source | decoded) at 100 %,
200 % (nearest) and with x8 local contrast for visual review.  Regions: sequence.json "banding" (image pixels unless
"ref"/"units"), --region, or a regions-file entry whose "match" fits "seq_<name>".  Without any ffmpeg the source
frames are scored with the 8-bit thresholds tightened by the safety factor 2 of 11.12 step 4.
Writes QA/reports/encode_test_<run>.json and .md; crops and decoded frames under the work folder (gitignored).
Exit code 1 if a decoded (or, without an encoder, a source) region fails.
"""
import argparse
import math
import os
import shutil
import subprocess
import sys
import time

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import numpy as np  # noqa: E402

import qa_common as qc  # noqa: E402
from qa_common import PASS, FAIL, SKIP, INFO, WARN, TH, check  # noqa: E402

VF_ENC = "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p"
VF_DEC = "scale=in_color_matrix=bt709:in_range=tv:out_range=pc:flags=accurate_rnd+full_chroma_int,format=rgb24"
TAGS = ["-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv"]


def find_ffmpeg():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    try:  # allowed analysis-tool fallback (11.12 step 2)
        import imageio_ffmpeg  # type: ignore
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def encoders(ffmpeg):
    try:
        out = subprocess.run([ffmpeg, "-hide_banner", "-encoders"], capture_output=True, text=True, timeout=60).stdout
    except Exception:
        return set()
    have = set()
    for name in ("libx264", "libvpx-vp9"):
        if (" " + name + " ") in out:
            have.add(name)
    return have


def encode_cmd(ffmpeg, seq, codec, crf, out, fps, n_frames, cpu_used):
    cmd = [ffmpeg, "-hide_banner", "-y", "-framerate", "%g" % fps, "-start_number", str(seq["start"]),
           "-i", seq["pattern"], "-frames:v", str(n_frames), "-vf", VF_ENC]
    if codec == "x264":
        cmd += ["-c:v", "libx264", "-preset", "medium", "-crf", str(crf), "-pix_fmt", "yuv420p"] + TAGS + \
               ["-movflags", "+faststart", out]
    else:
        cmd += ["-c:v", "libvpx-vp9", "-crf", str(crf), "-b:v", "0", "-deadline", "good", "-cpu-used", str(cpu_used),
                "-row-mt", "1", "-pix_fmt", "yuv420p"] + TAGS + [out]
    return cmd


def decode_selected(ffmpeg, video, W, H, wanted, log):
    """Decode to rgb24 through a pipe; returns ({frame_index: H x W x 3 uint8}, decoded frame count)."""
    cmd = [ffmpeg, "-hide_banner", "-v", "error", "-i", video, "-vf", VF_DEC, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    nbytes = W * H * 3
    got, n = {}, 0
    with open(log, "ab") as lf:
        p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=lf)
        buf = bytearray(nbytes)
        mv = memoryview(buf)
        while True:
            filled = 0
            while filled < nbytes:
                k = p.stdout.readinto(mv[filled:])
                if not k:
                    break
                filled += k
            if filled < nbytes:
                break
            if n in wanted:
                got[n] = np.frombuffer(bytes(buf), dtype=np.uint8).reshape(H, W, 3).copy()
            n += 1
        p.stdout.close()
        p.wait()
    return got, n


def box_blur(a, r):
    """Separable box blur (radius r) of an H x W x C float array, edge-clamped."""
    out = a.astype(np.float64)
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r + 1, r)
        p = np.pad(out, pad, mode="edge")
        c = np.cumsum(p, axis=axis)
        n = out.shape[axis]
        hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
        lo = np.take(c, np.arange(0, n), axis=axis)
        out = (hi - lo) / float(2 * r + 1)
    return out


def review_crops(src, dec, rect, out_prefix):
    """Side-by-side source | decoded crops: 100 %, 200 % (nearest) and x8 local contrast."""
    H, W = src.shape[:2]
    x0, y0, x1, y1 = rect
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    cw, ch = min(512, x1 - x0, W), min(384, y1 - y0, H)
    a0, b0 = max(0, min(W - cw, cx - cw // 2)), max(0, min(H - ch, cy - ch // 2))
    s, d = src[b0:b0 + ch, a0:a0 + cw], dec[b0:b0 + ch, a0:a0 + cw]
    sep = np.full((ch, 6, 3), 255, dtype=np.uint8)
    paths = [qc.save_png(out_prefix + "_100.png", np.concatenate([s, sep, d], axis=1))]
    hw, hh = cw // 2, ch // 2
    sz = lambda im: np.repeat(np.repeat(im[(ch - hh) // 2:(ch - hh) // 2 + hh, (cw - hw) // 2:(cw - hw) // 2 + hw], 2, 0), 2, 1)  # noqa: E731
    s2, d2 = sz(s), sz(d)
    sep2 = np.full((s2.shape[0], 6, 3), 255, dtype=np.uint8)
    paths.append(qc.save_png(out_prefix + "_200.png", np.concatenate([s2, sep2, d2], axis=1)))
    amp = lambda im: np.clip((im.astype(np.float64) - box_blur(im, 15)) * 8.0 + 128.0, 0, 255).astype(np.uint8)  # noqa: E731
    paths.append(qc.save_png(out_prefix + "_x8.png", np.concatenate([amp(s), sep, amp(d)], axis=1)))
    return paths


def _scaled_th(factor):
    t = dict(TH)
    if factor != 1.0:
        for k in ("band_steps_per_1000_max", "band_run_max_px", "band_resid_rms_max", "band_resid_pp_max"):
            t[k] = TH[k] / factor
    return t


def score_frames(frames, regions, th):
    """Worst BandScore per region over the given {index: rgb8} frames."""
    out = {}
    for name, rect, d, it in regions:
        worst = None
        for idx, img in sorted(frames.items()):
            b = qc.band_score(img, rect, d, None, th, it)
            if b.get("pass") is None:
                continue
            b = {k: v for k, v in b.items() if k != "strips"}
            b["frame"] = idx
            if worst is None or b["score"] > worst["score"]:
                worst = b
        out[name] = worst
    return out


def seq_regions(seq, cli_regions, file_entries, W, H):
    items = []
    for b in seq["meta"].get("banding", []) or []:
        it = dict(b)
        it.setdefault("_ref", it.get("ref"))
        it.setdefault("_units", it.get("units", None if it.get("ref") else "px"))
        items.append(it)
    for i, r in enumerate(cli_regions):
        items.append({"name": "cli%d" % i, "rect": r[:4], "dir": r[4] if len(r) > 4 else "v", "_units": "px"})
    reg = qc.regions_for("seq_" + seq["name"], W, H, file_entries, None)
    items += reg["banding"]
    return qc.band_regions_resolved(items, W, H)


def analyse(seq, args, ffmpeg, have, file_entries, work):
    res = {"name": seq["name"], "dir": seq["dir"], "checks": [], "variants": []}
    C = res["checks"]
    frames = seq["frames"]
    if len(frames) < 2:
        C.append(check("frames", FAIL, len(frames), ">= 2"))
        res["status"] = FAIL
        return res
    idx0 = [i for i, _ in frames]
    contiguous = idx0 == list(range(idx0[0], idx0[0] + len(idx0)))
    n = len(frames)
    if not contiguous:
        k = 1
        while k < n and idx0[k] == idx0[0] + k:
            k += 1
        n = k
        C.append(check("contiguous frames", WARN, n, len(frames), "gap in numbering: only the first %d encoded" % n))
    if args.max_frames:
        n = min(n, args.max_frames)
    first, _ = qc.load_rgb8(frames[0][1])
    H, W = first.shape[:2]
    if W % 2 or H % 2:
        C.append(check("even size", FAIL, "%dx%d" % (W, H), "even", "yuv420p needs even dimensions"))
        res["status"] = FAIL
        return res
    fps = float(args.fps or seq["meta"].get("fps") or 60.0)
    res.update({"size": [W, H], "fps": fps, "frames_encoded": n})
    regions = seq_regions(seq, args.region or [], file_entries, W, H)
    res["regions"] = [{"name": r[0], "rect": r[1], "dir": r[2]} for r in regions]
    k = max(1, min(args.sample, n))
    sel = sorted(set(int(round(v)) for v in np.linspace(0, n - 1, k)))
    src = {}
    for j in sel:
        src[j], _ = qc.load_rgb8(frames[j][1])
    if not regions:
        C.append(check("regions", WARN, 0, ">= 1", "no gradient region declared: fidelity and crops only"))
    factor = 1.0 if ffmpeg and have else 2.0
    th = _scaled_th(factor)
    src_scores = score_frames(src, regions, th)
    res["source"] = src_scores
    seq_work = qc.ensure_dir(os.path.join(work, seq["name"]))
    mid = sel[len(sel) // 2]
    if not ffmpeg or not have:
        for name, sc in src_scores.items():
            if sc is None:
                C.append(check("source[%s]" % name, SKIP, None, None, "region too small"))
                continue
            C.append(check("source[%s] (no encoder, x2 safety)" % name, PASS if sc["pass"] else FAIL,
                           round(sc["score"], 3), "score <= 1 at half thresholds"))
        res["status"] = qc.worst_status(C)
        return res
    variants = []
    if "x264" in args.codecs and "libx264" in have:
        variants += [("x264", c) for c in args.crf_x264]
    if "vp9" in args.codecs and "libvpx-vp9" in have:
        variants += [("vp9", c) for c in args.crf_vp9]
    for codec in args.codecs:
        lib = "libx264" if codec == "x264" else "libvpx-vp9"
        if lib not in have:
            C.append(check("encoder %s" % codec, WARN, None, None, "%s not available in ffmpeg" % lib))
    for codec, crf in variants:
        tag = "%s_crf%d" % (codec, crf)
        ext = ".mp4" if codec == "x264" else ".webm"
        video = os.path.join(seq_work, tag + ext)
        log = os.path.join(seq_work, tag + ".log")
        t0 = time.time()
        cmd = encode_cmd(ffmpeg, seq, codec, crf, video, fps, n, args.vp9_cpu_used)
        with open(log, "wb") as lf:
            lf.write((" ".join('"%s"' % c if " " in c else c for c in cmd) + "\n").encode("utf-8"))
            lf.flush()
            rc = subprocess.run(cmd, stdout=lf, stderr=lf).returncode
        enc_s = time.time() - t0
        v = {"codec": codec, "crf": crf, "video": video, "encode_s": round(enc_s, 1)}
        if rc != 0 or not os.path.isfile(video):
            C.append(check("encode %s" % tag, FAIL, rc, 0, "see %s" % log))
            res["variants"].append(v)
            continue
        v["kbps"] = round(os.path.getsize(video) * 8.0 / 1000.0 / max(n / fps, 1e-6), 1)
        dec, nd = decode_selected(ffmpeg, video, W, H, set(sel), log)
        v["decoded_frames"] = nd
        if nd != n:
            C.append(check("decode %s frame count" % tag, FAIL, nd, n))
        for j, img in dec.items():
            qc.save_png(os.path.join(seq_work, tag, "frame_%05d.png" % j), img)
        maes = [float(np.abs(dec[j].astype(np.int16) - src[j].astype(np.int16)).mean()) for j in sel if j in dec]
        v["mean_abs_error"] = float(np.mean(maes)) if maes else None
        scores = score_frames(dec, regions, th)
        v["decoded"] = scores
        crops = []
        if mid in dec:
            rects = [r[1] for r in regions if r[1] is not None] or [(W // 4, H // 4, 3 * W // 4, 3 * H // 4)]
            names = [r[0] for r in regions if r[1] is not None] or ["centre"]
            for nm, rc_ in zip(names, rects):
                crops += review_crops(src[mid], dec[mid], rc_, os.path.join(seq_work, "crops", "%s_%s_f%05d" % (tag, nm, mid)))
        v["crops"] = crops
        for name, sc in scores.items():
            s0 = src_scores.get(name)
            if sc is None:
                C.append(check("%s[%s]" % (tag, name), SKIP, None, None, "region too small"))
                continue
            note = "steps/1000 %.2f, run %d, resid rms %s pp %s (source score %s), frame %d" % (
                sc["steps_per_1000"], sc["longest_run_px"], qc.fmt(sc["resid_rms"]), qc.fmt(sc["resid_pp"]),
                qc.fmt(s0["score"] if s0 else None), sc["frame"])
            C.append(check("%s[%s]" % (tag, name), PASS if sc["pass"] else FAIL, round(sc["score"], 3),
                           "score <= 1", note))
        res["variants"].append(v)
        if not args.keep_video and os.path.isfile(video):
            v["video_kept"] = False
            os.remove(video)
    res["status"] = qc.worst_status(C)
    return res


def build_markdown(run, results, ffmpeg, have):
    allc = [c for r in results for c in r["checks"]]
    cnt = qc.count_status(allc)
    L = ["# Encode test: %s" % run, "",
         "ffmpeg: `%s`, encoders: %s" % (ffmpeg or "not found", ", ".join(sorted(have)) or "none"), "",
         "Sequences: %d. Checks: %d PASS, %d FAIL, %d WARN, %d SKIP." % (len(results), cnt[PASS], cnt[FAIL], cnt[WARN],
                                                                       cnt[SKIP]), "",
         "**Result: %s**" % (FAIL if cnt[FAIL] else (PASS if results else SKIP)), ""]
    if not ffmpeg or not have:
        L += ["No encoder could be obtained: source frames scored with the 8-bit BandScore thresholds halved "
              "(safety factor 2, MASTER_PROMPT 11.12 step 4). Record this in DEVELOPMENT.md.", ""]
    for r in results:
        L += ["## %s (%s)" % (r["name"], r.get("status")), "", "`%s` %s, %s fps, %s frames encoded" % (
            r["dir"], "x".join(str(v) for v in r.get("size", [])), qc.fmt(r.get("fps"), 0), r.get("frames_encoded")), ""]
        rows = []
        for name, sc in (r.get("source") or {}).items():
            rows.append(["source", name, qc.fmt(sc and sc["score"]), qc.fmt(sc and sc["steps_per_1000"], 2),
                         sc and sc["longest_run_px"], qc.fmt(sc and sc["resid_rms"]), qc.fmt(sc and sc["resid_pp"]),
                         "-", "-", "PASS" if sc and sc["pass"] else ("FAIL" if sc else "-")])
        for v in r.get("variants", []):
            for name, sc in (v.get("decoded") or {}).items():
                rows.append(["%s crf %d" % (v["codec"], v["crf"]), name, qc.fmt(sc and sc["score"]),
                             qc.fmt(sc and sc["steps_per_1000"], 2), sc and sc["longest_run_px"],
                             qc.fmt(sc and sc["resid_rms"]), qc.fmt(sc and sc["resid_pp"]), qc.fmt(v.get("kbps"), 0),
                             qc.fmt(v.get("mean_abs_error")), "PASS" if sc and sc["pass"] else ("FAIL" if sc else "-")])
        if rows:
            L.append(qc.md_table(["Stream", "Region", "Score", "Steps/1000", "Run px", "Resid RMS", "Resid p-p",
                                  "kbit/s", "MAE codes", "Result"], rows))
        L.append(qc.md_table(["Check", "Status", "Value", "Threshold", "Note"],
                             [[c["name"], c["status"], qc.fmt(c["value"], 4), c["threshold"] or "", c["note"] or ""]
                              for c in r["checks"]]))
        crops = [p for v in r.get("variants", []) for p in v.get("crops", [])]
        if crops:
            L += ["Review crops (source | decoded; _100, _200 nearest, _x8 local contrast):", ""]
            L += ["- `%s`" % p for p in crops]
            L.append("")
    return "\n".join(L)


def _csv_ints(s):
    return [int(v) for v in s.split(",") if v.strip()]


def _region(s):
    p = s.split(",")
    if len(p) not in (4, 5):
        raise argparse.ArgumentTypeError("region must be x0,y0,x1,y1[,dir]")
    return [float(v) for v in p[:4]] + ([p[4].strip()] if len(p) == 5 else [])


def run(paths, args):
    seq_dirs = []
    for p in paths:
        p = os.path.abspath(p)
        if qc.is_sequence_dir(p):
            seq_dirs.append(p)          # named explicitly: always encoded
        elif os.path.isdir(p):
            for d in qc.find_sequences(p):
                m = qc.read_json(os.path.join(d, "sequence.json"), {}) or {}
                if args.all_seqs or m.get("encode") or (m.get("kind") or "").lower() == "encode":
                    seq_dirs.append(d)
    b = os.path.abspath(paths[0]) if paths else os.path.abspath(".")
    run_name = args.run or qc.run_id_for(os.path.dirname(b) if os.path.basename(b).lower().startswith("seq_") else b)
    work = qc.ensure_dir(args.work_dir or os.path.join(qc.ROOT, "QA", "encode", run_name))
    ffmpeg = find_ffmpeg()
    have = encoders(ffmpeg) if ffmpeg else set()
    entries = [] if args.regions in (None, "", "none") else qc.load_regions(args.regions)
    results = []
    for d in seq_dirs:
        seq = qc.load_sequence(d)
        if not args.quiet:
            print("[encode_test] %s (%d frames)" % (seq["name"], len(seq["frames"])), flush=True)
        try:
            results.append(analyse(seq, args, ffmpeg, have, entries, work))
        except Exception as e:
            results.append({"name": seq["name"], "dir": d, "status": FAIL, "variants": [],
                            "checks": [check("analysis", FAIL, None, None, "%s: %s" % (type(e).__name__, e))]})
    allc = [c for r in results for c in r["checks"]]
    status = FAIL if any(c["status"] == FAIL for c in allc) else (PASS if results else SKIP)
    js = os.path.join(args.out_dir, "encode_test_%s.json" % run_name)
    md = os.path.join(args.out_dir, "encode_test_%s.md" % run_name)
    qc.write_json(js, {"run": run_name, "status": status, "ffmpeg": ffmpeg, "encoders": sorted(have),
                       "safety_factor": 1.0 if (ffmpeg and have) else 2.0, "sequences": results})
    qc.write_text(md, build_markdown(run_name, results, ffmpeg, have))
    fails = ["%s: %s (%s)" % (r["name"], c["name"], qc.fmt(c["value"], 4)) for r in results for c in r["checks"]
             if c["status"] == FAIL]
    return {"tool": "encode_test", "status": status, "json": js, "md": md, "fails": fails,
            "counts": qc.count_status(allc), "n": len(results)}


def parser():
    ap = argparse.ArgumentParser(prog="encode_test.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("paths", nargs="+", help="sequence folder(s) or capture folder(s) with seq_* subfolders")
    ap.add_argument("--codecs", type=lambda s: [c.strip() for c in s.split(",") if c.strip()], default=["x264", "vp9"])
    ap.add_argument("--crf-x264", type=_csv_ints, default=[23, 30])
    ap.add_argument("--crf-vp9", type=_csv_ints, default=[31, 40])
    ap.add_argument("--fps", type=float, help="frame rate (default sequence.json fps, else 60)")
    ap.add_argument("--sample", type=int, default=8, help="decoded frames analysed per stream (default 8)")
    ap.add_argument("--max-frames", type=int, help="encode at most this many frames")
    ap.add_argument("--vp9-cpu-used", type=int, default=2)
    ap.add_argument("--region", type=_region, action="append", help="x0,y0,x1,y1[,dir] in image pixels (repeatable)")
    ap.add_argument("--regions", default=qc.DEFAULT_REGIONS, help="regions JSON ('none' to disable)")
    ap.add_argument("--all-seqs", action="store_true", help="encode every seq_* folder of a capture folder")
    ap.add_argument("--run", help="run id")
    ap.add_argument("--out-dir", default=qc.REPORTS_DIR)
    ap.add_argument("--work-dir", help="videos, decoded frames and crops (default QA/encode/<run>)")
    ap.add_argument("--keep-video", action="store_true", help="keep the encoded files")
    ap.add_argument("--quiet", action="store_true")
    return ap


def main(argv=None):
    a = parser().parse_args(argv)
    missing = [p for p in a.paths if not os.path.isdir(p)]
    if missing:
        print("not found: %s" % ", ".join(missing), file=sys.stderr)
        return 1
    r = run(a.paths, a)
    print("[encode_test] %s: %d sequences, %d FAIL checks -> %s" % (r["status"], r["n"], len(r["fails"]), r["md"]))
    for f in r["fails"][:30]:
        print("  FAIL " + f)
    return 1 if r["status"] == FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
