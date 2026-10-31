#!/usr/bin/env python3
"""selftest: synthetic end-to-end test of the Thread QA tools (images, sequences, encode, audio, run_all).

Usage:  python -I Tools/qa/selftest.py [--work QA/tmp/selftest] [--no-encode] [--no-4k]

Generates synthetic stills, sequences and WAVs under QA/tmp/selftest (gitignored), runs every tool on them and
asserts the expected verdicts: a smooth gradient without dither must FAIL the banding score and the same gradient
with a 1 LSB TPDF dither must PASS; a 2 px antialiased line must measure about 2 px; deliberate defects (black,
white, magenta, NaN-like pixel, wrong size, popping sequence, aliased line, DC, click, 0 dBFS peak) must be
caught; a 997 Hz 0 dBFS sine on one channel must read -3.01 LUFS.  Exit code 1 if any assertion fails.
"""
import argparse
import json
import math
import os
import shutil
import struct
import sys
import time
import wave

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import numpy as np  # noqa: E402

import qa_common as qc  # noqa: E402

RNG = np.random.default_rng(20261029)
RESULTS = []


def expect(name, cond, detail=""):
    RESULTS.append((name, bool(cond), detail))
    print("  %s  %s  %s" % ("ok  " if cond else "FAIL", name, detail), flush=True)


# --------------------------------------------------------------------------------------------- synthesis helpers
def enc(lin):
    lin = np.clip(lin, 0.0, 1.0)
    return np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)


def dec(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.04045, c / 12.92, np.power((c + 0.055) / 1.055, 2.4))


def tpdf(shape, amp=1.0):
    return (RNG.random(shape) - RNG.random(shape)) * amp


def quantise(codes, amp):
    """Float sRGB codes (0..255) -> uint8 with monochrome TPDF dither of +-amp LSB."""
    n = tpdf(codes.shape[:2] + (1,), amp) if amp > 0 else 0.0
    return np.clip(np.floor(codes + n + 0.5), 0, 255).astype(np.uint8)


def gradient(H, W, top, bot):
    t = np.linspace(0.0, 1.0, H)[:, None, None]
    return np.broadcast_to(np.array(top, float) * (1 - t) + np.array(bot, float) * t, (H, W, 3)).copy()


def coverage_line(W, H, x_of_y, width, aliased=False):
    """Per-pixel coverage of a vertical band [x - w/2, x + w/2] (exact box area per pixel, or 0/1 when aliased)."""
    ys = np.arange(H) + 0.5
    xc = x_of_y(ys)[:, None]
    i = np.arange(W)[None, :].astype(np.float64)
    a, b = xc - width / 2.0, xc + width / 2.0
    if aliased:
        cx = i + 0.5
        return ((cx >= a) & (cx < b)).astype(np.float64)
    return np.clip(np.minimum(b, i + 1) - np.maximum(a, i), 0.0, 1.0)


def _herm(p0, p1, m0, m1, u):
    u2 = u * u
    u3 = u2 * u
    return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1


def sky_lin(dir_y, h, m, z, stop_m_deg=25.0):
    """C1 sky gradient in t = sqrt(dirY) (the DEVELOPMENT.md D-008 form), linear colour per channel."""
    tm = math.sqrt(math.sin(math.radians(stop_m_deg)))
    t = np.sqrt(np.clip(dir_y, 0.0, 1.0))[..., None]
    sl = z - h
    a = np.clip(_herm(h, m, 0.0, sl * tm, t / tm), np.minimum(h, m), np.maximum(h, m))
    b = np.clip(_herm(m, z, sl * (1 - tm), 0.0, (t - tm) / (1 - tm)), np.minimum(m, z), np.maximum(m, z))
    return np.where(t < tm, a, b)


def kf1_image(W, H, dither=1.0):
    """Synthetic beat01_salt_KF1: sky gradient over elevation, fogged ground meeting the horizon colour, a 2 px
    (at 1080p) thread at x ~ 1115/1920, the sun disc at azimuth -35 / elevation 24 with a clipped ring inside 1.2 deg."""
    cam = qc.Cam(W, H, 62.0, 0.0, 12.0, (0.0, 1.7, 0.0))
    uu, vv = np.meshgrid(np.arange(W) + 0.5, np.arange(H) + 0.5)
    d = cam.cam_dir_px(uu, vv)
    fwd, right, up = cam.basis()
    wd = d[..., 0:1] * right + d[..., 1:2] * up + d[..., 2:3] * fwd
    wd /= np.linalg.norm(wd, axis=-1, keepdims=True)
    el = np.degrees(np.arcsin(np.clip(wd[..., 1], -1, 1)))
    hzc, m25, zen = np.array([243, 228, 208.]), np.array([191, 217, 227.]), np.array([111, 175, 211.])
    sky = enc(sky_lin(wd[..., 1], dec(hzc / 255.0), dec(m25 / 255.0), dec(zen / 255.0))) * 255.0
    dist = 1.7 / np.tan(np.radians(np.maximum(-el, 1e-4)))
    fog = (1.0 - np.exp(-3.912 * dist / 1600.0))[..., None]
    ground_lin = dec(np.array([228, 218, 198.]) / 255.0) * (1 - fog) + dec(hzc / 255.0) * fog
    codes = np.where((el >= 0)[..., None], sky, enc(ground_lin) * 255.0)
    # thread (linear-light coverage blend, haze capped at 0.74 -> visible contrast)
    sc = H / 1080.0
    cov = coverage_line(W, H, lambda y: (1116.8 + 0.01 * (y / sc - 540.0)) * sc, 2.0 * sc)
    cov *= (el > 1.0)
    thread = dec(np.array([40, 90, 100.]) / 255.0) * 0.5 + dec(hzc / 255.0) * 0.5
    lin = dec(codes / 255.0)
    lin = lin * (1 - cov[..., None]) + thread * cov[..., None]
    codes = enc(lin) * 255.0
    # sun disc (0.9 deg, below 0.98) with a clipped ring 0.7..1.1 deg (inside the 1.2 deg exclusion)
    sdir = cam.azel_to_cam(-35.0, 24.0)
    dn = d / np.linalg.norm(d, axis=-1, keepdims=True)
    ang = np.degrees(np.arccos(np.clip(dn.dot(sdir / np.linalg.norm(sdir)), -1, 1)))
    codes = np.where((ang < 0.9)[..., None], np.array([246, 238, 222.]), codes)
    ring = (ang > 0.95) & (ang < 1.1)
    codes = np.where(ring[..., None], 255.0, codes)
    out = quantise(codes, dither)
    out[ring] = 255
    return out


def write_wav_float(path, x, fs):
    x = np.asarray(x, dtype="<f4")
    ch = x.shape[1]
    data = x.tobytes()
    fmt = struct.pack("<HHIIHHH", 3, ch, fs, fs * ch * 4, ch * 4, 32, 0)
    fact = struct.pack("<I", x.shape[0])
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 4 + 8 + len(fmt) + 8 + len(fact) + 8 + len(data)) + b"WAVE")
        f.write(b"fmt " + struct.pack("<I", len(fmt)) + fmt)
        f.write(b"fact" + struct.pack("<I", len(fact)) + fact)
        f.write(b"data" + struct.pack("<I", len(data)) + data)


def write_wav_pcm16(path, x, fs):
    q = np.clip(np.round(np.asarray(x) * 32768.0), -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(q.shape[1])
        w.setsampwidth(2)
        w.setframerate(fs)
        w.writeframes(q.tobytes())


def write_wav_pcm24(path, x, fs):
    q = np.clip(np.round(np.asarray(x) * 8388608.0), -8388608, 8388607).astype(np.int32).reshape(-1)
    b = np.stack([(q & 0xFF), (q >> 8) & 0xFF, (q >> 16) & 0xFF], axis=1).astype(np.uint8).tobytes()
    ch = x.shape[1]
    fmt = struct.pack("<HHIIHH", 1, ch, fs, fs * ch * 3, ch * 3, 24)
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 4 + 8 + len(fmt) + 8 + len(b)) + b"WAVE")
        f.write(b"fmt " + struct.pack("<I", len(fmt)) + fmt)
        f.write(b"data" + struct.pack("<I", len(b)) + b)


def get_check(res_img, name):
    for c in res_img["checks"]:
        if c["name"] == name:
            return c
    return None


# --------------------------------------------------------------------------------------------- tests
def test_banding():
    print("[banding metric]")
    for label, H, top, bot in (("day 1080p", 403, (243, 228, 208), (191, 217, 227)),
                               ("day 4K", 806, (243, 228, 208), (191, 217, 227)),
                               ("night", 700, (10, 15, 38), (4, 5, 13))):
        g = gradient(H, 256, top, bot)
        b0 = qc.band_score(quantise(g, 0.0).astype(np.float64))
        b1 = qc.band_score(quantise(g, 1.0).astype(np.float64))
        expect("%s undithered fails" % label, b0["pass"] is False, "score %.2f resid %.3f" % (b0["score"], b0["resid_rms"]))
        expect("%s 1 LSB TPDF passes" % label, b1["pass"] is True, "score %.2f resid %.3f" % (b1["score"], b1["resid_rms"]))
        expect("%s scores clearly apart" % label, b0["score"] > 2.0 * b1["score"], "%.2f vs %.2f" % (b0["score"], b1["score"]))
    # horizontal gradient + mask splitting
    g = np.transpose(gradient(640, 300, (243, 228, 208), (191, 217, 227)), (1, 0, 2)).copy()
    m = np.zeros(g.shape[:2], dtype=bool)
    m[100:140, 200:260] = True
    bh = qc.band_score(quantise(g, 1.0).astype(np.float64), None, "auto", m)
    expect("auto direction + mask (dithered passes)", bh["pass"] is True and bh["direction"] == "h",
           "dir %s strips %d" % (bh["direction"], bh["n_strips"]))


def test_line_probe():
    print("[thin-line probe]")
    W, H = 256, 300
    bg = gradient(H, W, (200, 200, 205), (190, 195, 205))
    for width, lo, hi in ((2.0, 1.85, 2.15), (1.75, 1.7, 2.05), (3.3, 3.15, 3.45)):
        cov = coverage_line(W, H, lambda y: 128.3 + 0.013 * y, width)
        lin = dec(bg / 255.0) * (1 - cov[..., None]) + dec(np.array([60, 90, 100.]) / 255.0) * cov[..., None]
        img = quantise(enc(lin) * 255.0, 1.0)
        r = qc.line_probe(qc.luma_linear(img), 130, 20, 64, 256)
        ok = r.get("found") and lo <= r["fwhm_px"] <= hi
        expect("AA line %.2f px width" % width, ok, "fwhm %.3f eq %.3f" % (r.get("fwhm_px", -1), r.get("eq_width_px", -1)))
        ev = qc.evaluate_line(r, {"name": "t"}, 1080)
        bad = [c["name"] for c in ev if c["status"] == "FAIL"]
        expect("AA line %.2f px clean (monotone, no stair-step, stable energy)" % width, not bad,
               "monotone %.2f stair %.3f ecv %.3f %s" % (r["monotone_rows"], r["centroid_resid_max_px"], r["energy_var"], bad))
    cov = coverage_line(W, H, lambda y: 128.3 + 0.013 * y, 2.0, aliased=True)
    lin = dec(bg / 255.0) * (1 - cov[..., None]) + dec(np.array([60, 90, 100.]) / 255.0) * cov[..., None]
    img = quantise(enc(lin) * 255.0, 1.0)
    r = qc.line_probe(qc.luma_linear(img), 130, 20, 64, 256)
    ev = {c["name"]: c["status"] for c in qc.evaluate_line(r, {"name": "t"}, 1080)}
    expect("aliased line caught (stair-step)", ev.get("line[t] stair-step") == "FAIL",
           "stair %.3f px" % r.get("centroid_resid_max_px", -1))


def make_capture(root, with_4k):
    cap = qc.ensure_dir(os.path.join(root, "cap_run"))
    shots = []
    print("[synthesis] stills", flush=True)
    img = kf1_image(1920, 1080, 1.0)
    qc.save_png(os.path.join(cap, "beat01_salt_KF1.png"), img)
    shots.append({"tag": "beat01_salt_KF1", "file": "beat01_salt_KF1.png", "width": 1920, "height": 1080, "s": 0,
                  "pose": {"x": 0, "y": 1.7, "z": 0, "yaw": 0, "pitch": 12, "fov": 62}})
    qc.save_png(os.path.join(cap, "beat01_salt_KF1_nodither.png"), kf1_image(1920, 1080, 0.0))
    shots.append({"tag": "beat01_salt_KF1_nodither", "file": "beat01_salt_KF1_nodither.png", "width": 1920,
                  "height": 1080, "s": 0, "pose": {"yaw": 0, "pitch": 12, "fov": 62}})
    qc.save_png(os.path.join(cap, "beat01_salt_KF1_window.png"), img)  # parity companion
    if with_4k:
        qc.save_png(os.path.join(cap, "beat01_salt_KF1_3840x2160.png"), kf1_image(3840, 2160, 1.0))
        shots.append({"tag": "beat01_salt_KF1_3840x2160", "file": "beat01_salt_KF1_3840x2160.png", "width": 3840,
                      "height": 2160, "s": 0, "pose": {"yaw": 0, "pitch": 12, "fov": 62}})
    qc.save_png(os.path.join(cap, "beat02_avenue_mid_black.png"), np.zeros((1080, 1920, 3), np.uint8))
    shots.append({"tag": "beat02_avenue_mid_black", "file": "beat02_avenue_mid_black.png", "width": 1920, "height": 1080})
    qc.save_png(os.path.join(cap, "beat03_stairs_end_white.png"), np.full((1080, 1920, 3), 255, np.uint8))
    shots.append({"tag": "beat03_stairs_end_white", "file": "beat03_stairs_end_white.png", "width": 1920, "height": 1080})
    g = quantise(gradient(1080, 1920, (170, 165, 160), (120, 125, 130)), 1.0)
    g[500:510, 900:910] = (255, 0, 255)
    g[200, 300] = (0, 0, 0)                      # isolated NaN-like black pixel
    qc.save_png(os.path.join(cap, "beat04_tower_mid_defects.png"), g)
    shots.append({"tag": "beat04_tower_mid_defects", "file": "beat04_tower_mid_defects.png", "width": 1920, "height": 1080})
    g2 = quantise(gradient(720, 1280, (150, 160, 170), (100, 110, 130)), 1.0)
    qc.save_png(os.path.join(cap, "beat05_spiral_start.png"), g2)
    shots.append({"tag": "beat05_spiral_start", "file": "beat05_spiral_start.png", "width": 1920, "height": 1080,
                  "subjects": [{"name": "bead", "box": [100, 100, 600, 500]},
                               {"name": "clipped_box", "box": [-50, 100, 400, 500]}],
                  "regions": {"stats": [{"name": "all", "rect": [0, 0, 1, 1], "units": "frac", "stdMax": 0.5}]}})
    with open(os.path.join(cap, "report.json"), "w") as f:
        json.dump({"run": "selftest", "build": "synthetic", "shots": shots}, f, indent=1)

    print("[synthesis] sequences", flush=True)
    base = gradient(256, 320, (180, 182, 186), (150, 160, 170))
    sd = qc.ensure_dir(os.path.join(cap, "seq_b1_static_frozen"))
    f0 = quantise(base, 1.0)
    for i in range(10):
        qc.save_png(os.path.join(sd, "frame_%05d.png" % i), f0)
    json.dump({"kind": "static", "fps": 30, "freezeDither": True}, open(os.path.join(sd, "sequence.json"), "w"))
    sd = qc.ensure_dir(os.path.join(cap, "seq_b1_static_temporal"))
    for i in range(12):
        qc.save_png(os.path.join(sd, "frame_%05d.png" % i), quantise(base, 1.0))
    json.dump({"kind": "static", "fps": 30, "freezeDither": False}, open(os.path.join(sd, "sequence.json"), "w"))
    sd = qc.ensure_dir(os.path.join(cap, "seq_b1_static_claims_frozen"))
    for i in range(12):
        qc.save_png(os.path.join(sd, "frame_%05d.png" % i), quantise(base, 1.0))
    json.dump({"kind": "static", "fps": 30, "freezeDither": True}, open(os.path.join(sd, "sequence.json"), "w"))
    sd = qc.ensure_dir(os.path.join(cap, "seq_b2_b3_pop"))
    for i in range(40):
        k = 1.0 + 0.002 * i + (0.12 if i == 25 else 0.0)
        qc.save_png(os.path.join(sd, "frame_%05d.png" % i), quantise(np.clip(base * k, 0, 255), 1.0))
    json.dump({"kind": "transition", "fps": 30, "s0": 228, "s1": 252}, open(os.path.join(sd, "sequence.json"), "w"))
    for name, aliased in (("seq_b5_pan_rail", False), ("seq_b5_pan_rail_aliased", True)):
        sd = qc.ensure_dir(os.path.join(cap, name))
        bgl = dec(gradient(300, 256, (200, 200, 205), (190, 195, 205)) / 255.0)
        for i in range(30):
            cov = coverage_line(256, 300, lambda y: 120.2 + 0.1 * i + 0.0 * y, 2.0, aliased)
            lin = bgl * (1 - cov[..., None]) + dec(np.array([40, 40, 50.]) / 255.0) * cov[..., None]
            qc.save_png(os.path.join(sd, "frame_%05d.png" % i), quantise(enc(lin) * 255.0, 1.0))
        json.dump({"kind": "pan", "fps": 30, "lines": [{"name": "rail", "x": 121, "tol": 16, "y0": 20, "h": 256}],
                   "stability": [{"name": "vp", "rect": [20, 20, 84, 84]}]}, open(os.path.join(sd, "sequence.json"), "w"))
    sd = qc.ensure_dir(os.path.join(cap, "seq_b6_sky_encode"))
    night = gradient(360, 640, (10, 15, 38), (4, 5, 13))
    for i in range(30):
        qc.save_png(os.path.join(sd, "frame_%05d.png" % i), quantise(night, 1.0))
    json.dump({"kind": "encode", "encode": True, "fps": 30,
               "banding": [{"name": "night_sky", "rect": [0, 0, 640, 360], "dir": "v"}]},
              open(os.path.join(sd, "sequence.json"), "w"))
    return cap


def make_audio(root):
    print("[synthesis] audio", flush=True)
    ad = qc.ensure_dir(os.path.join(root, "audio"))
    sp = qc.spec()
    fs, dur = 48000, 60.0
    n = int(fs * dur)
    t = np.arange(n) / float(fs)
    s = t * (sp.beat_boundaries[-1] / dur)
    beat = np.searchsorted(np.array(sp.beat_boundaries[1:]), s, side="right") + 1
    beat = np.clip(beat, 1, 7)
    tgt = np.array(sp.target_rms_db)[beat - 1]
    tgt = np.where((s >= sp.swell[0]) & (s <= sp.swell[1]), -12.0, tgt)   # loud swell must be excluded in beat 4
    amp = np.sqrt(2.0) * 10 ** (tgt / 20.0)
    x = amp * np.sin(2 * np.pi * 220.0 * t)
    write_wav_float(os.path.join(ad, "render_mix.wav"), np.stack([x, x], axis=1), fs)
    json.dump({"s": [float(v) for v in np.arange(0, dur + 1) * (sp.beat_boundaries[-1] / dur)], "dt": 1.0},
              open(os.path.join(ad, "render_mix.json"), "w"))
    t2 = np.arange(fs * 10) / float(fs)
    l = (32767.0 / 32768.0) * np.sin(2 * np.pi * 997.0 * t2)
    write_wav_pcm16(os.path.join(ad, "lufs_997_left.wav"), np.stack([l, np.zeros_like(l)], axis=1), fs)
    t3 = np.arange(fs * 5) / float(fs)
    y = 0.05 * np.sin(2 * np.pi * 110.0 * t3) + 0.01
    y[fs * 2] = 0.5
    write_wav_float(os.path.join(ad, "dc_click.wav"), np.stack([y, y], axis=1), fs)
    t4 = np.arange(fs * 2) / float(fs)
    z = np.sqrt(2.0) * 0.1 * np.sin(2 * np.pi * 440.0 * t4)
    write_wav_pcm24(os.path.join(ad, "tone_pcm24.wav"), np.stack([z, z], axis=1), fs)
    w = np.sqrt(2.0) * np.where(s >= sp.wind_off_s, 1e-4, 1e-3) * np.sin(2 * np.pi * 260.0 * t)
    write_wav_float(os.path.join(ad, "stem_wind.wav"), np.stack([w, w], axis=1), fs)
    json.dump({"s": [float(v) for v in np.arange(0, dur + 1) * (sp.beat_boundaries[-1] / dur)], "stem": "wind"},
              open(os.path.join(ad, "stem_wind.json"), "w"))
    return ad


def main(argv=None):
    ap = argparse.ArgumentParser(prog="selftest.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("--work", default=os.path.join(qc.ROOT, "QA", "tmp", "selftest"))
    ap.add_argument("--no-encode", action="store_true")
    ap.add_argument("--no-4k", action="store_true")
    a = ap.parse_args(argv)
    t0 = time.time()
    root = os.path.abspath(a.work)
    if os.path.isdir(root):
        shutil.rmtree(root)
    qc.ensure_dir(root)
    rep = qc.ensure_dir(os.path.join(root, "reports"))
    print("scene constants: %s %s" % (qc.spec().source, qc.spec().warnings))
    test_banding()
    test_line_probe()
    cap = make_capture(root, not a.no_4k)
    ad = make_audio(root)

    import image_metrics
    import contact_sheet
    import sequences
    import encode_test
    import audio_analysis
    import run_all

    print("[image_metrics]", flush=True)
    r = image_metrics.run(cap, None, qc.DEFAULT_REGIONS, "selftest", rep, 1, False, None, "display", True)
    js = qc.read_json(r["json"])
    imgs = {i["tag"]: i for i in js["images"]}
    k = imgs["beat01_salt_KF1"]
    for name in ("size", "black frame", "white frame", "magenta", "NaN-like pixels", "clipped highlights", "luma p1",
                 "horizon[default]", "banding[sky_above_bank]", "line[thread] width@1080p", "line[thread] contrast",
                 "line[thread] monotone 3-level edge", "line[thread] stair-step", "core[sun_core] colour",
                 "window parity"):
        c = get_check(k, name)
        expect("KF1 %s PASS" % name, c is not None and c["status"] == "PASS",
               "%s %s" % (c and c["status"], c and qc.fmt(c["value"], 4)))
    ml = get_check(k, "mean luma")
    print("  info KF1 mean luma %s (%s)" % (ml and ml["value"], ml and ml["status"]))
    expect("KF1 clip exclusion used", k["metrics"]["clip_excluded_px"] > 0, str(k["metrics"]["clip_excluded_px"]))
    c = get_check(imgs["beat01_salt_KF1_nodither"], "banding[sky_above_bank]")
    expect("KF1 undithered banding FAIL", c is not None and c["status"] == "FAIL", c and qc.fmt(c["value"]))
    if not a.no_4k:
        k4 = imgs["beat01_salt_KF1_3840x2160"]
        for name in ("banding[sky_above_bank]", "line[thread] width@1080p", "horizon[default]", "clipped highlights"):
            c = get_check(k4, name)
            expect("KF1 4K %s PASS" % name, c is not None and c["status"] == "PASS",
                   "%s %s" % (c and c["status"], c and qc.fmt(c["value"], 4)))
    expect("black frame caught", get_check(imgs["beat02_avenue_mid_black"], "black frame")["status"] == "FAIL")
    expect("white frame caught", get_check(imgs["beat03_stairs_end_white"], "white frame")["status"] == "FAIL")
    expect("white clipping caught", get_check(imgs["beat03_stairs_end_white"], "clipped highlights")["status"] == "FAIL")
    d = imgs["beat04_tower_mid_defects"]
    expect("magenta caught", get_check(d, "magenta")["status"] == "FAIL", str(d["metrics"]["magenta_like"]))
    expect("NaN-like pixel caught (exactly 1)", d["metrics"]["nan_like_dark"] == 1 and d["metrics"]["nan_like_bright"] == 0,
           "%s / %s" % (d["metrics"]["nan_like_dark"], d["metrics"]["nan_like_bright"]))
    sp5 = imgs["beat05_spiral_start"]
    expect("wrong size caught", get_check(sp5, "size")["status"] == "FAIL")
    expect("subject inside margin PASS", get_check(sp5, "subject[bead] margin")["status"] == "PASS")
    expect("subject outside margin FAIL", get_check(sp5, "subject[clipped_box] margin")["status"] == "FAIL")
    expect("report.json stats region used", get_check(sp5, "stats[all] std") is not None)
    expect("image_metrics overall FAIL (defects present)", r["status"] == "FAIL")

    print("[contact_sheet]", flush=True)
    rc = contact_sheet.run(cap, os.path.join(root, "contact"), 4, 2048, 2048, r["json"], True, 5, "selftest")
    from PIL import Image
    ok = bool(rc["sheets"]) and all(Image.open(p).size[0] <= 2048 for p in rc["sheets"])
    expect("contact sheets written, <= 2048 px wide", ok, "%d sheets, %d seq sheets" % (len(rc["sheets"]), len(rc["seq_sheets"])))

    print("[sequences]", flush=True)
    rs = sequences.run([cap], "selftest", rep, True)
    sj = {q["name"]: q for q in qc.read_json(rs["json"])["sequences"]}
    st = lambda seq, name: next((c["status"] for c in sj[seq]["checks"] if c["name"] == name), None)  # noqa: E731
    expect("static frozen PASS", st("b1_static_frozen", "static mean diff") == "PASS")
    md_t = sj["b1_static_temporal"]["summary"]["mean_diff"]
    expect("static temporal (TPDF +-1) PASS at 0.8", st("b1_static_temporal", "static mean diff") == "PASS", "%.3f" % md_t)
    expect("static noisy but declared frozen FAIL at 0.3", st("b1_static_claims_frozen", "static mean diff") == "FAIL")
    expect("pop: luma step caught", st("b2_b3_pop", "luma step/frame") == "FAIL")
    expect("pop: diff spike caught", st("b2_b3_pop", "diff spikes") == "FAIL", str(sj["b2_b3_pop"].get("spikes")))
    expect("AA pan: jitter PASS", st("b5_pan_rail", "line[rail] centroid jitter") == "PASS",
           qc.fmt(sj["b5_pan_rail"]["lines"]["rail"]["jitter_max_px"], 4))
    expect("AA pan: speed 0.1 px/frame", abs(sj["b5_pan_rail"]["lines"]["rail"]["pan_px_per_frame"] - 0.1) < 0.01,
           qc.fmt(sj["b5_pan_rail"]["lines"]["rail"]["pan_px_per_frame"], 4))
    expect("aliased pan: jitter FAIL", st("b5_pan_rail_aliased", "line[rail] centroid jitter") == "FAIL",
           qc.fmt(sj["b5_pan_rail_aliased"]["lines"]["rail"]["jitter_max_px"], 4))
    expect("stability crop PASS on dithered static background", st("b5_pan_rail", "stability[vp]") == "PASS")

    if not a.no_encode:
        print("[encode_test]", flush=True)
        eargs = encode_test.parser().parse_args([cap, "--run", "selftest", "--out-dir", rep, "--work-dir",
                                                 os.path.join(root, "encode"), "--sample", "4", "--quiet"])
        re_ = encode_test.run([cap], eargs)
        ej = qc.read_json(re_["json"])
        seqs = ej["sequences"]
        expect("encode_test ran one sequence", len(seqs) == 1, str([q["name"] for q in seqs]))
        if seqs:
            v = seqs[0].get("variants", [])
            expect("encode variants decoded with full frame count", v and all(x.get("decoded_frames") == 30 for x in v),
                   str([(x["codec"], x["crf"], x.get("decoded_frames"), qc.fmt(x.get("mean_abs_error"))) for x in v]))
            expect("review crops written", v and all(len(x.get("crops", [])) == 3 and all(os.path.isfile(p) for p in x["crops"])
                                                     for x in v))
            src = seqs[0]["source"]["night_sky"]
            expect("encode source (dithered) passes", src and src["pass"], qc.fmt(src and src["score"]))
            for x in v:
                dsc = (x.get("decoded") or {}).get("night_sky")
                print("  info %s crf %d decoded score %s (%s)" % (x["codec"], x["crf"], qc.fmt(dsc and dsc["score"]),
                                                                 "PASS" if dsc and dsc["pass"] else "FAIL"))

    print("[audio_analysis]", flush=True)
    ra = audio_analysis.run([ad], "selftest", rep, 1.0, 10.0, True)
    aj = qc.read_json(ra["json"])
    files = {os.path.basename(f["file"]): f for f in aj["files"]}
    fc = lambda f, name: next((c for c in files[f]["checks"] if c["name"] == name), None)  # noqa: E731
    mix = files["render_mix.wav"]
    for name in ("sample peak", "DC offset", "sample step", "NaN/Inf samples", "not silent", "peak outside swell"):
        c = fc("render_mix.wav", name)
        expect("mix %s PASS" % name, c is not None and c["status"] == "PASS", c and qc.fmt(c["value"], 4))
    beats = {b["beat"]: b for b in aj["beats"]}
    for bi in range(1, 8):
        b = beats.get(bi)
        expect("beat %d RMS PASS" % bi, b is not None and b["status"] == "PASS",
               "%s vs %s" % (b and b["rms_dbfs"], b and b["target"]))
    expect("beat 4 swell excluded (exact target)", beats.get(4) and abs(beats[4]["rms_dbfs"] - beats[4]["target"]) < 0.2,
           str(beats.get(4) and beats[4]["rms_dbfs"]))
    lu = files["lufs_997_left.wav"]["lufs_integrated"]
    expect("997 Hz 0 dBFS left only = -3.01 LUFS", lu is not None and abs(lu + 3.01) < 0.05, qc.fmt(lu, 3))
    expect("0 dBFS peak caught", fc("lufs_997_left.wav", "sample peak")["status"] == "FAIL")
    expect("DC caught", fc("dc_click.wav", "DC offset")["status"] == "FAIL", qc.fmt(fc("dc_click.wav", "DC offset")["value"], 5))
    expect("click caught", fc("dc_click.wav", "sample step")["status"] == "FAIL", qc.fmt(fc("dc_click.wav", "sample step")["value"], 3))
    pk24 = files["tone_pcm24.wav"]["sample_peak_dbfs"]
    expect("24-bit reader (-17 dBFS peak)", abs(pk24 - 20 * math.log10(math.sqrt(2) * 0.1)) < 0.05, qc.fmt(pk24, 3))
    c = fc("stem_wind.wav", "wind off from s %g" % qc.spec().wind_off_s)
    expect("wind stem off check", c is not None and c["status"] == "PASS", c and qc.fmt(c["value"], 2))
    expect("audio overall FAIL (defects present)", ra["status"] == "FAIL")

    print("[run_all]", flush=True)
    rc = run_all.main([cap, "--audio", ad, "--run", "selftest_all", "--out-dir", rep, "--encode",
                       "none" if a.no_encode else "auto", "--quiet"])
    expect("run_all exit code 1 (defects present)", rc == 1)
    expect("summary written", os.path.isfile(os.path.join(rep, "summary_selftest_all.md")))

    n_bad = sum(1 for _, ok, _ in RESULTS if not ok)
    print("\nselftest: %d assertions, %d failed, %.1f s; outputs in %s" % (len(RESULTS), n_bad, time.time() - t0, root))
    for name, ok, det in RESULTS:
        if not ok:
            print("  FAILED: %s  %s" % (name, det))
    return 1 if n_bad else 0


if __name__ == "__main__":
    sys.exit(main())
