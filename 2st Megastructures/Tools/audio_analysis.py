"""Offline audio analysis for The Ring (MASTER_PROMPT 14.3 "Master", 11.9, Part 10 #25).

Reads PCM WAV files (16 / 24 / 32-bit integer, any channel count) with the standard `wave` module and measures,
per file: sample peak, true peak (4x oversampled, 65-tap Blackman-Harris windowed sinc, the same interpolator as
AudioAnalyzer in Assets/_Game/Scripts/Runtime/Audio/AudioOffline.cs), DC per channel, the largest sample-to-sample
step, clicks (third-difference detector: |d3| > 0.005 and > 8 x its centred 10 ms RMS, the C# rule), the RMS of the
first / last 10 ms and the sliding 3 s RMS curve (0.1 s hop). RMS dBFS = 10 log10(mean(x^2)) over all channels
(no +3.01 dB AES17 offset), like the C# side and QA/audio-trims.json.

When QA/audio/excerpts.json (written by the EditMode test AudioTests.OfflineWalkMeetsTheMasterSpec) describes a file,
the hatch sweep window is exempt from the 0.25 step rule and every 3 s window that lies inside the steady part of a
beat (no fade, no deliberate silence S1-S5, no 24 m Rule-P boundary window) is compared with its 14.3 target.

Usage:  python -I Tools/audio_analysis.py [WAV files or folders ...] [--excerpts FILE] [--out FILE] [--selftest]
        default input QA/audio, default output <first folder>/analysis.json
Exit code 1 when a rule fails (true peak > -1 dBFS, |DC| >= 0.001, step > 0.25 outside the exemption, NaN, clicks,
or a valid 3 s window more than +-1.5 dB from its 14.3 target), 0 otherwise.
Requires numpy only.
"""
import json
import math
import os
import sys
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DEFAULT_DIR = os.path.join(ROOT, "QA", "audio")

PEAK_CEILING_DBFS = -1.0
MAX_DC = 0.001
MAX_STEP = 0.25
TOLERANCE_DB = 1.5
HOP_SEC = 0.1
WINDOW_SEC = 3.0
CLICK_ABS = 0.005
CLICK_RATIO = 8.0
CLICK_WINDOW_SEC = 0.010


# ---------------------------------------------------------------------------------------------------------------
# I/O
# ---------------------------------------------------------------------------------------------------------------
def read_wav(path):
    """Returns (float64 array frames x channels in [-1, 1], sample rate)."""
    with wave.open(path, "rb") as w:
        ch, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    if width == 2:
        x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32767.0
    elif width == 3:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        v = np.where(v >= 1 << 23, v - (1 << 24), v)
        x = v.astype(np.float64) / 8388607.0
    elif width == 4:
        x = np.frombuffer(raw, dtype="<i4").astype(np.float64) / 2147483647.0
    elif width == 1:
        x = (np.frombuffer(raw, dtype=np.uint8).astype(np.float64) - 128.0) / 127.0
    else:
        raise ValueError("unsupported sample width %d in %s" % (width, path))
    frames = len(x) // ch
    return x[: frames * ch].reshape(frames, ch), rate


def write_wav16(path, x, rate):
    x = np.clip(np.asarray(x, dtype=np.float64), -1.0, 1.0)
    if x.ndim == 1:
        x = x[:, None]
    q = np.round(x * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(x.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(q.tobytes())


# ---------------------------------------------------------------------------------------------------------------
# metrics
# ---------------------------------------------------------------------------------------------------------------
def db(v, floor=1e-12):
    return 20.0 * math.log10(max(float(v), floor))


def db_power(p, floor=1e-30):
    return 10.0 * math.log10(max(float(p), floor))


def true_peak_phases(L=4, taps_per_phase=16):
    """Polyphase windowed sinc (Blackman-Harris), unity DC gain per phase: research 05 section 9."""
    N = L * taps_per_phase + 1
    c = (N - 1) / 2.0
    k = np.arange(N)
    xs = (k - c) / L
    s = np.sinc(xs)
    a = 2.0 * np.pi * k / (N - 1)
    h = s * (0.35875 - 0.48829 * np.cos(a) + 0.14128 * np.cos(2 * a) - 0.01168 * np.cos(3 * a))
    phases = []
    for p in range(L):
        f = h[p::L].copy()
        f /= f.sum()
        phases.append(f)
    return phases


_PHASES = true_peak_phases()


def true_peak(x):
    """4x oversampled peak (linear) over all channels."""
    m = 0.0
    for ch in range(x.shape[1]):
        col = x[:, ch]
        for f in _PHASES:
            y = np.convolve(col, f, mode="full")
            m = max(m, float(np.max(np.abs(y))) if y.size else 0.0)
    return m


def exempt_mask(frames, rate, windows):
    mask = np.zeros(frames, dtype=bool)
    for a, b in windows or []:
        i0, i1 = max(0, int(a * rate)), min(frames, int(math.ceil(b * rate)) + 1)
        if i1 > i0:
            mask[i0:i1] = True
    return mask


def max_step(x, mask):
    if x.shape[0] < 2:
        return 0.0, 0
    d = np.max(np.abs(np.diff(x, axis=0)), axis=1)
    d[mask[1:]] = 0.0
    i = int(np.argmax(d))
    return float(d[i]), i + 1


def clicks(x, rate, mask):
    """Third-difference click detector (as AudioAnalyzer in C#). Returns (count of click clusters, max ratio, times)."""
    if x.shape[0] < 8:
        return 0, 0.0, []
    d3 = np.max(np.abs(x[3:] - 3.0 * x[2:-1] + 3.0 * x[1:-2] - x[:-3]), axis=1)
    win = max(2, int(round(CLICK_WINDOW_SEC * rate)))
    e = np.concatenate(([0.0], np.cumsum(d3 * d3)))
    n = d3.size
    half = win // 2
    lo = np.clip(np.arange(n) - half, 0, n)
    hi = np.clip(np.arange(n) + half, 0, n)
    cnt = np.maximum(hi - lo, 1)
    rms = np.sqrt(np.maximum((e[hi] - e[lo]) / cnt, 1e-30))
    ratio = d3 / rms
    m = mask[3:] if mask.size >= n + 3 else np.zeros(n, dtype=bool)
    ratio[m] = 0.0
    big = d3 > CLICK_ABS
    rmax = float(np.max(np.where(big, ratio, 0.0))) if n else 0.0
    flagged = np.nonzero(big & (ratio > CLICK_RATIO))[0]
    times, last = [], -10 ** 9
    for i in flagged:
        if i - last > win:
            times.append(round((i + 3) / rate, 4))
        last = i
    return len(times), rmax, times


def hop_energies(x, rate):
    hop = int(round(HOP_SEC * rate))
    nh = x.shape[0] // hop
    if nh == 0:
        return np.zeros(0), hop
    e = np.sum((x[: nh * hop] ** 2).reshape(nh, hop, x.shape[1]), axis=(1, 2))
    return e, hop


def sliding_rms_db(x, rate):
    """Sliding 3 s RMS (dB) at 0.1 s hops; element i covers hops i .. i + 29 (file time i*0.1 .. i*0.1 + 3)."""
    e, hop = hop_energies(x, rate)
    wh = int(round(WINDOW_SEC / HOP_SEC))
    if e.size < wh:
        return np.zeros(0)
    c = np.concatenate(([0.0], np.cumsum(e)))
    s = c[wh:] - c[:-wh]
    return 10.0 * np.log10(s / (wh * hop * x.shape[1]) + 1e-30)


def edge_rms_db(x, rate, last=False):
    n = max(1, int(0.010 * rate))
    seg = x[-n:] if last else x[:n]
    return db_power(np.mean(seg ** 2)) if seg.size else -300.0


# ---------------------------------------------------------------------------------------------------------------
# targets from excerpts.json
# ---------------------------------------------------------------------------------------------------------------
def rms_vs_targets(curve, meta):
    """Compares the sliding RMS with the per-hop targets of excerpts.json; returns stats or None."""
    hops = meta.get("hops") or []
    if not hops or curve.size == 0:
        return None
    ht = np.array([h[0] for h in hops], dtype=np.float64)
    hb = np.array([h[2] for h in hops], dtype=np.int64)
    htarget = np.array([h[3] for h in hops], dtype=np.float64)
    hflags = np.array([h[4] for h in hops], dtype=np.int64)
    devs, beats = [], []
    for i in range(curve.size):
        a, b = i * HOP_SEC, i * HOP_SEC + WINDOW_SEC
        sel = (ht > a) & (ht <= b)
        if np.count_nonzero(sel) < 25:
            continue
        if np.any(hflags[sel] != 0) or np.unique(hb[sel]).size != 1:
            continue
        centre = 0.5 * (a + b)
        j = int(np.argmin(np.abs(ht - centre)))
        devs.append(curve[i] - htarget[j])
        beats.append(int(hb[j]))
    if not devs:
        return {"windows": 0}
    d = np.array(devs)
    return {
        "windows": int(d.size),
        "beat": int(np.bincount(beats).argmax()),
        "meanDevDb": round(float(np.mean(d)), 3),
        "medianDevDb": round(float(np.median(d)), 3),
        "p05DevDb": round(float(np.percentile(d, 5)), 3),
        "p95DevDb": round(float(np.percentile(d, 95)), 3),
        "maxAbsDevDb": round(float(np.max(np.abs(d))), 3),
        "withinTol": round(float(np.mean(np.abs(d) <= TOLERANCE_DB)), 4),
    }


# ---------------------------------------------------------------------------------------------------------------
# driver
# ---------------------------------------------------------------------------------------------------------------
def analyse(path, meta=None):
    x, rate = read_wav(path)
    frames = x.shape[0]
    res = {"file": os.path.basename(path), "sampleRate": rate, "channels": int(x.shape[1]), "seconds": round(frames / float(rate), 3)}
    if frames == 0:
        res["error"] = "empty"
        return res
    finite = np.isfinite(x)
    res["nan"] = int(np.size(x) - np.count_nonzero(finite))
    x = np.where(finite, x, 0.0)
    exempt = (meta or {}).get("stepExempt") or []
    mask = exempt_mask(frames, rate, exempt)
    sp = float(np.max(np.abs(x)))
    tp = true_peak(x)
    res["peakDbfs"] = round(db(sp), 3)
    res["truePeakDbfs"] = round(db(max(tp, sp)), 3)
    res["dc"] = [round(float(v), 7) for v in np.mean(x, axis=0)]
    st, at = max_step(x, mask)
    res["maxStep"] = round(st, 5)
    res["maxStepAt"] = round(at / float(rate), 4)
    res["stepExempt"] = exempt
    nclk, rmax, times = clicks(x, rate, mask)
    res["clicks"] = nclk
    res["clickTimes"] = times[:20]
    res["clickMaxRatio"] = round(rmax, 2)
    res["rmsDb"] = round(db_power(np.mean(x ** 2)), 3)
    res["first10msRmsDb"] = round(max(-300.0, edge_rms_db(x, rate)), 1)
    res["last10msRmsDb"] = round(max(-300.0, edge_rms_db(x, rate, last=True)), 1)
    curve = sliding_rms_db(x, rate)
    if curve.size:
        res["rms3sMinDb"] = round(float(np.min(curve)), 2)
        res["rms3sMedianDb"] = round(float(np.median(curve)), 2)
        res["rms3sMaxDb"] = round(float(np.max(curve)), 2)
        res["rmsCurve1s"] = [[round(i * HOP_SEC + 0.5 * WINDOW_SEC, 1), round(float(curve[i]), 2)] for i in range(0, curve.size, 10)]
    if meta is not None:
        res["note"] = meta.get("note", "")
        res["rmsVsTarget"] = rms_vs_targets(curve, meta)
    fails = []
    if res["nan"] > 0:
        fails.append("NaN")
    if res["truePeakDbfs"] > PEAK_CEILING_DBFS:
        fails.append("true peak %.2f dBFS" % res["truePeakDbfs"])
    if max(abs(v) for v in res["dc"]) >= MAX_DC:
        fails.append("DC")
    if st > MAX_STEP:
        fails.append("step %.3f at %.3f s" % (st, at / float(rate)))
    if nclk > 0:
        fails.append("%d clicks" % nclk)
    tv = res.get("rmsVsTarget")
    if tv and tv.get("windows", 0) > 0 and tv["maxAbsDevDb"] > TOLERANCE_DB:
        fails.append("3 s RMS %.2f dB off target (%.1f %% of %d windows within +-%.1f dB)" % (
            tv["maxAbsDevDb"], 100.0 * tv["withinTol"], tv["windows"], TOLERANCE_DB))
    res["fails"] = fails
    res["pass"] = not fails
    return res


def selftest():
    rate = 48000
    n = np.arange(rate)
    sine = np.sin(0.5 * np.pi * n + 0.25 * np.pi)[:, None]
    tp = db(true_peak(sine))
    ok = abs(db(np.max(np.abs(sine))) + 3.0103) < 0.01 and -0.1 <= tp <= 0.25
    print("selftest true peak: sample %.3f dBFS, true %.3f dBTP -> %s" % (db(np.max(np.abs(sine))), tp, "ok" if ok else "FAIL"))
    # a wind-like background (low-passed noise + a 220 Hz hum) with and without a 0.02 step (a click)
    rng = np.random.default_rng(3)
    brown = np.cumsum(0.0005 * rng.standard_normal(rate))
    brown -= np.mean(brown)
    noise = (brown + 0.05 * np.sin(2.0 * np.pi * 220.0 * n / rate))[:, None]
    stepped = noise.copy()
    stepped[rate // 2:] += 0.02
    c0, _, _ = clicks(noise, rate, np.zeros(rate, dtype=bool))
    c1, _, _ = clicks(stepped, rate, np.zeros(rate, dtype=bool))
    ok2 = c0 == 0 and c1 >= 1
    print("selftest clicks: smooth noise %d, with a 0.02 step %d -> %s" % (c0, c1, "ok" if ok2 else "FAIL"))
    return ok and ok2


def main(argv):
    out, excerpts_path, inputs = None, None, []
    i = 0
    run_self = False
    while i < len(argv):
        a = argv[i]
        if a == "--out" and i + 1 < len(argv):
            out = argv[i + 1]; i += 2; continue
        if a == "--excerpts" and i + 1 < len(argv):
            excerpts_path = argv[i + 1]; i += 2; continue
        if a == "--selftest":
            run_self = True; i += 1; continue
        inputs.append(a); i += 1
    if run_self:
        return 0 if selftest() else 1
    if not inputs:
        inputs = [DEFAULT_DIR]
    files, first_dir = [], None
    for p in inputs:
        if os.path.isdir(p):
            first_dir = first_dir or p
            files += sorted(os.path.join(p, f) for f in os.listdir(p) if f.lower().endswith(".wav"))
        elif os.path.isfile(p):
            first_dir = first_dir or os.path.dirname(os.path.abspath(p))
            files.append(p)
    if not files:
        print("audio_analysis: no WAV files in", inputs)
        return 2
    if excerpts_path is None and first_dir and os.path.isfile(os.path.join(first_dir, "excerpts.json")):
        excerpts_path = os.path.join(first_dir, "excerpts.json")
    metas = {}
    if excerpts_path:
        with open(excerpts_path, "r", encoding="utf-8") as f:
            ex = json.load(f)
        metas = {e["file"]: e for e in ex.get("files", [])}
    results = [analyse(f, metas.get(os.path.basename(f))) for f in files]

    hdr = "%-16s %7s %8s %8s %10s %8s %6s %8s %8s  %-30s %s" % (
        "file", "sec", "peak", "TP", "DC max", "step", "clicks", "rms", "edge", "3 s RMS vs target (mean p05/p95 in)", "verdict")
    print(hdr)
    print("-" * len(hdr))
    for r in results:
        if "error" in r:
            print("%-16s %s" % (r["file"], r["error"]))
            continue
        tv = r.get("rmsVsTarget")
        if tv and tv.get("windows", 0) > 0:
            tvs = "%+5.2f %+5.1f/%+5.1f %3.0f%% (n %d)" % (tv["meanDevDb"], tv["p05DevDb"], tv["p95DevDb"], 100 * tv["withinTol"], tv["windows"])
        else:
            tvs = "-"
        edge = "%.0f/%.0f" % (r["first10msRmsDb"], r["last10msRmsDb"])
        print("%-16s %7.1f %8.2f %8.2f %10.6f %8.4f %6d %8.2f %8s  %-30s %s" % (
            r["file"], r["seconds"], r["peakDbfs"], r["truePeakDbfs"], max(abs(v) for v in r["dc"]), r["maxStep"], r["clicks"],
            r["rmsDb"], edge, tvs, "ok" if r["pass"] else "FAIL: " + ", ".join(r["fails"])))
    allpass = all(r.get("pass", False) for r in results)
    report = {
        "tool": "Tools/audio_analysis.py",
        "rules": {"truePeakDbfs": PEAK_CEILING_DBFS, "maxDc": MAX_DC, "maxStep": MAX_STEP, "rmsToleranceDb": TOLERANCE_DB,
                  "rmsDefinition": "10*log10(mean(x^2)) over all channels, no +3.01 dB offset; sliding 3 s, 0.1 s hop",
                  "clickRule": "|d3| > %.3f and > %.1f x centred %.0f ms RMS of d3" % (CLICK_ABS, CLICK_RATIO, 1000 * CLICK_WINDOW_SEC)},
        "excerpts": excerpts_path,
        "pass": allpass,
        "files": results,
    }
    if out is None:
        out = os.path.join(first_dir or DEFAULT_DIR, "analysis.json")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    print("wrote", out, "-", "all hard rules pass" if allpass else "FAILURES")
    return 0 if allpass else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
