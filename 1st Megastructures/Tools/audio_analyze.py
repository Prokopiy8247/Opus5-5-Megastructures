#!/usr/bin/env python3
"""Abyss audio analyser (MASTER_PROMPT Part 10 item 25, 11.9, 14.3, 14.10 Functional).

Reads a WAV file (16/24/32-bit PCM or 32/64-bit IEEE float, any channel count) and reports:
  * sample peak and true peak (4x oversampling, band-limited FFT interpolation per block) in dBFS
  * the RMS curve in 0.4 s windows (power mean over all channels)
  * the largest sample-to-sample step and the DC offset per channel
  * checks against the 14.3 targets:
      - peak <= -3 dBFS, true peak < -1 dBFS, max step <= 0.1, |DC| < 1e-3
      - a full nominal walk (from -render-audio or AbyssScore.RenderWalkToWav, length >= 650 s): every 0.4 s window inside
        each beat's steady span is within +/-3 dB of the beat target; the ending has >= 2.4 s of absolute silence (< 1e-5)
        followed by the 31 Hz tone at about -34 dBFS
      - --zone N (1..8) for a single-zone render: every window within +/-3 dB of that zone's target
Writes a JSON summary (--json, default <wav>.analysis.json) and exits 0 when every check passes, 1 otherwise.

Usage:  python -I Tools/audio_analyze.py <file.wav> [--zone N] [--json out.json] [--csv curve.csv] [--top N]
Needs numpy (pip install numpy); without numpy it falls back to pure Python (slow, true peak via cubic interpolation).
"""
import argparse
import json
import math
import os
import struct
import sys

try:
    import numpy as np
except Exception:  # pragma: no cover - fallback path
    np = None

# ---------------------------------------------------------------------------------------------- 14.3 / 14.4 constants
TARGETS = [-34.0, -38.0, -33.0, -27.0, -29.0, -26.0, -31.0, -35.0]
ZONE_NAMES = ["1 gallery", "2 bridge", "3 ledge", "4 hall", "5 lift", "6 fan", "7 tunnel", "7 outside"]
TOLERANCE_DB = 3.0
WINDOW_S = 0.4
PEAK_MAX_DB = -3.0
TRUE_PEAK_MAX_DB = -1.0
STEP_MAX = 0.1
DC_MAX = 1e-3
SILENCE_ABS = 1e-5

# Nominal walk timeline (14.4 at 1.5 m/s; AbyssConst / AbyssScore): steady spans per zone exclude the transitions.
T_GATE_OPEN = 429.36
T_RIDE_START = 308.36
D_LIFT = 459.54
GOAL_TIME = T_GATE_OPEN + (728.54 + 72.8 - D_LIFT) / 1.5          # 657.23 s
STEADY_SPANS = [
    (4.0, 34.0),                                    # 1 gallery: after the title fade, before the d 52 blend (t 34.7)
    (48.5, 136.0),                                  # 2 bridge: after the d 72 blend, before d 205
    (150.0, 180.0),                                 # 3 ledge: after d 225, before the pulse rise at d 270
    (220.0, 284.0),                                 # 4 hall: inside the hall, before the last 12 m
    (T_RIDE_START + 12.0, T_RIDE_START + 50.0),     # 5 lift: after the departure silence (b), before the fan fades in
    (T_GATE_OPEN + (469.54 - D_LIFT) / 1.5 + 4.0, T_GATE_OPEN + (664.54 - D_LIFT) / 1.5),  # 6 gantry
    (T_GATE_OPEN + (672.54 - D_LIFT) / 1.5 + 2.0, T_GATE_OPEN + (716.0 - D_LIFT) / 1.5),   # 7 tunnel
    (T_GATE_OPEN + (734.54 - D_LIFT) / 1.5 + 2.0, GOAL_TIME + 0.9),                        # 7 outside
]
T_MOUTH = T_GATE_OPEN + (668.54 - D_LIFT) / 1.5    # 568.69 s
SILENCE_A = (41.6, 43.6)                            # (a) HUM gone on the landing / bridge start (d 62.4-65.4)
SILENCE_B = (T_RIDE_START + 2.0, T_RIDE_START + 10.0)  # (b) lift departure: HUM, pulse and whine fade out over 4 s
SILENCE_C = (T_MOUTH + 2.5, T_MOUTH + 5.5)          # (c) machine layers cut at the tunnel mouth
END_SILENCE = (GOAL_TIME + 5.5, GOAL_TIME + 7.95)   # 3 s of absolute silence after the 4 s fade (14.3 d)
END_TONE_TOP = (GOAL_TIME + 10.5, GOAL_TIME + 11.5)  # the 31 Hz tone at its -34 dBFS top (3 s in from goal + 8 s)


# ---------------------------------------------------------------------------------------------- WAV reading
def read_wav(path):
    """Return (samples[frames][channels] as numpy float64 array or list of lists, sample_rate, channels)."""
    with open(path, "rb") as f:
        data = f.read()
    if data[0:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError("not a RIFF/WAVE file")
    pos = 12
    fmt = None
    pcm = None
    while pos + 8 <= len(data):
        cid = data[pos:pos + 4]
        size = struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            tag, ch, rate, _, align, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE and len(body) >= 26:  # WAVE_FORMAT_EXTENSIBLE: sub-format GUID starts with the tag
                tag = struct.unpack("<H", body[24:26])[0]
            fmt = (tag, ch, rate, align, bits)
        elif cid == b"data":
            pcm = body
        pos += 8 + size + (size & 1)
    if fmt is None or pcm is None:
        raise ValueError("missing fmt or data chunk")
    tag, ch, rate, align, bits = fmt
    frames = len(pcm) // align
    pcm = pcm[:frames * align]
    if np is not None:
        if tag == 3 and bits == 32:
            x = np.frombuffer(pcm, dtype="<f4").astype(np.float64)
        elif tag == 3 and bits == 64:
            x = np.frombuffer(pcm, dtype="<f8").astype(np.float64)
        elif tag == 1 and bits == 16:
            x = np.frombuffer(pcm, dtype="<i2").astype(np.float64) / 32768.0
        elif tag == 1 and bits == 32:
            x = np.frombuffer(pcm, dtype="<i4").astype(np.float64) / 2147483648.0
        elif tag == 1 and bits == 24:
            b = np.frombuffer(pcm, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
            v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
            v = np.where(v >= 1 << 23, v - (1 << 24), v)
            x = v.astype(np.float64) / 8388608.0
        else:
            raise ValueError("unsupported format tag %d / %d bits" % (tag, bits))
        return x.reshape(-1, ch), rate, ch
    # pure Python fallback
    if tag == 3 and bits == 32:
        vals = struct.unpack("<%df" % (frames * ch), pcm)
    elif tag == 1 and bits == 16:
        vals = [v / 32768.0 for v in struct.unpack("<%dh" % (frames * ch), pcm)]
    else:
        raise ValueError("pure-Python fallback supports float32 and PCM16 only (install numpy)")
    return [list(vals[i * ch:(i + 1) * ch]) for i in range(frames)], rate, ch


def db(v):
    return 20.0 * math.log10(v) if v > 1e-15 else -300.0


# ---------------------------------------------------------------------------------------------- metrics (numpy)
def true_peak_np(x, block=65536, pad=256):
    """4x band-limited interpolation per channel with overlapping FFT blocks (edges discarded)."""
    peak = float(np.max(np.abs(x))) if x.size else 0.0
    n = x.shape[0]
    for c in range(x.shape[1]):
        s = x[:, c]
        start = 0
        while start < n:
            a = max(0, start - pad)
            b = min(n, start + block + pad)
            seg = s[a:b]
            m = len(seg)
            if m < 8:
                break
            spec = np.fft.rfft(seg)
            up = np.fft.irfft(spec, 4 * m) * 4.0
            lo = (start - a) * 4
            hi = lo + (min(n, start + block) - start) * 4
            peak = max(peak, float(np.max(np.abs(up[lo:hi]))))
            start += block
    return peak


def analyse_np(x, rate):
    res = {}
    res["peak_dbfs"] = db(float(np.max(np.abs(x)))) if x.size else -300.0
    res["true_peak_dbfs"] = db(true_peak_np(x))
    res["max_step"] = float(np.max(np.abs(np.diff(x, axis=0)))) if x.shape[0] > 1 else 0.0
    res["dc"] = [float(v) for v in np.mean(x, axis=0)]
    res["rms_dbfs"] = 10.0 * math.log10(max(float(np.mean(x * x)), 1e-30))
    win = int(round(WINDOW_S * rate))
    count = x.shape[0] // win
    p = np.mean((x[:count * win] ** 2).reshape(count, win * x.shape[1]), axis=1) if count else np.zeros(0)
    curve = 10.0 * np.log10(np.maximum(p, 1e-30))
    return res, curve


def analyse_py(x, rate):
    res = {}
    ch = len(x[0]) if x else 1
    peak = 0.0
    step = 0.0
    sums = [0.0] * ch
    pw = 0.0
    tp = 0.0
    prev = None
    for i, fr in enumerate(x):
        for c, v in enumerate(fr):
            a = abs(v)
            if a > peak:
                peak = a
            sums[c] += v
            pw += v * v
            if prev is not None:
                d = abs(v - prev[c])
                if d > step:
                    step = d
        prev = fr
    # cubic (Catmull-Rom) estimate of the inter-sample peak
    for c in range(ch):
        for i in range(1, len(x) - 2):
            p0, p1, p2, p3 = x[i - 1][c], x[i][c], x[i + 1][c], x[i + 2][c]
            for t in (0.25, 0.5, 0.75):
                v = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
                if abs(v) > tp:
                    tp = abs(v)
    n = max(1, len(x))
    res["peak_dbfs"] = db(peak)
    res["true_peak_dbfs"] = db(max(tp, peak))
    res["max_step"] = step
    res["dc"] = [s / n for s in sums]
    res["rms_dbfs"] = 10.0 * math.log10(max(pw / (n * ch), 1e-30))
    win = int(round(WINDOW_S * rate))
    curve = []
    for k in range(len(x) // win):
        s = 0.0
        for fr in x[k * win:(k + 1) * win]:
            for v in fr:
                s += v * v
        curve.append(10.0 * math.log10(max(s / (win * ch), 1e-30)))
    return res, curve


def span_peak(x, rate, t0, t1):
    a, b = int(t0 * rate), int(t1 * rate)
    if np is not None:
        seg = x[a:b]
        return float(np.max(np.abs(seg))) if seg.size else 0.0
    return max((abs(v) for fr in x[a:b] for v in fr), default=0.0)


def span_rms_db(x, rate, t0, t1):
    a, b = int(t0 * rate), int(t1 * rate)
    if np is not None:
        seg = x[a:b]
        return 10.0 * math.log10(max(float(np.mean(seg * seg)), 1e-30)) if seg.size else -300.0
    vals = [v * v for fr in x[a:b] for v in fr]
    return 10.0 * math.log10(max(sum(vals) / max(1, len(vals)), 1e-30))


def top_events(x, rate, count):
    """Times of the largest |sample| and the largest steps (for locating defects)."""
    if np is None or count <= 0:
        return [], []
    mono = np.max(np.abs(x), axis=1)
    sec = int(rate)
    n = len(mono) // sec
    per_sec = mono[:n * sec].reshape(n, sec).max(axis=1)
    idx = np.argsort(per_sec)[::-1][:count]
    peaks = [{"t": float(i), "peak_dbfs": db(float(per_sec[i]))} for i in sorted(idx)]
    st = np.max(np.abs(np.diff(x, axis=0)), axis=1)
    n2 = len(st) // sec
    ps = st[:n2 * sec].reshape(n2, sec).max(axis=1)
    idx2 = np.argsort(ps)[::-1][:count]
    steps = [{"t": float(i), "step": float(ps[i])} for i in sorted(idx2)]
    return peaks, steps


# ---------------------------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("wav")
    ap.add_argument("--zone", type=int, default=0, help="single-zone render 1..8 (beats 1-6, 7 tunnel, 7 outside)")
    ap.add_argument("--json", default=None)
    ap.add_argument("--csv", default=None, help="write the 0.4 s RMS curve as CSV")
    ap.add_argument("--top", type=int, default=5, help="list the N loudest seconds and largest steps")
    ap.add_argument("--skip", type=float, default=0.0, help="ignore the first S seconds for the window check")
    args = ap.parse_args()

    x, rate, ch = read_wav(args.wav)
    frames = x.shape[0] if np is not None else len(x)
    dur = frames / float(rate)
    res, curve = analyse_np(x, rate) if np is not None else analyse_py(x, rate)
    curve = [float(v) for v in curve]
    summary = {"file": os.path.abspath(args.wav), "sample_rate": rate, "channels": ch, "seconds": dur}
    summary.update(res)
    checks = {}
    checks["peak<=-3dBFS"] = res["peak_dbfs"] <= PEAK_MAX_DB + 1e-6
    checks["true_peak<-1dBFS"] = res["true_peak_dbfs"] < TRUE_PEAK_MAX_DB
    checks["max_step<=0.1"] = res["max_step"] <= STEP_MAX
    checks["dc<1e-3"] = all(abs(v) < DC_MAX for v in res["dc"])

    zones = []
    if args.zone:
        z = args.zone - 1
        tgt = TARGETS[z]
        w = [v for i, v in enumerate(curve) if (i * WINDOW_S) >= args.skip]
        bad = [v for v in w if abs(v - tgt) > TOLERANCE_DB]
        zones.append({"zone": ZONE_NAMES[z], "target_db": tgt, "windows": len(w), "min_db": min(w) if w else None,
                      "max_db": max(w) if w else None, "median_db": sorted(w)[len(w) // 2] if w else None, "bad_windows": len(bad)})
        checks["zone_windows_within_3dB"] = len(bad) == 0 and len(w) > 0
    elif dur >= 650.0:
        allok = True
        for z, (t0, t1) in enumerate(STEADY_SPANS):
            i0, i1 = int(math.ceil(t0 / WINDOW_S)), int(math.floor(t1 / WINDOW_S))
            w = curve[i0:i1]
            tgt = TARGETS[z]
            bad = [(round((i0 + k + 0.5) * WINDOW_S, 1), round(v, 2)) for k, v in enumerate(w) if abs(v - tgt) > TOLERANCE_DB]
            ws = sorted(w)
            zones.append({"zone": ZONE_NAMES[z], "span_s": [round(t0, 2), round(t1, 2)], "target_db": tgt, "windows": len(w),
                          "min_db": ws[0] if ws else None, "max_db": ws[-1] if ws else None,
                          "median_db": ws[len(ws) // 2] if ws else None, "bad_windows": len(bad), "first_bad": bad[:5]})
            allok = allok and not bad and len(w) > 0
        checks["walk_windows_within_3dB"] = allok

        def med(t0, t1):
            w = sorted(curve[int(math.ceil(t0 / WINDOW_S)):int(math.floor(t1 / WINDOW_S))])
            return w[len(w) // 2] if w else 0.0

        def mn(t0, t1):
            w = curve[int(math.ceil(t0 / WINDOW_S)):int(math.floor(t1 / WINDOW_S))]
            return min(w) if w else 0.0
        sa = med(*STEADY_SPANS[0]) - med(*SILENCE_A)
        sb = med(*STEADY_SPANS[4]) - mn(*SILENCE_B)
        sc = med(*STEADY_SPANS[5]) - med(*SILENCE_C)
        summary["silence_depth_db"] = {"a_bridge": round(sa, 2), "b_departure": round(sb, 2), "c_mouth": round(sc, 2)}
        checks["silence_a_present"] = sa >= 3.0
        checks["silence_b_present"] = sb >= 2.0
        checks["silence_c_present"] = sc >= 3.0
        if dur >= END_TONE_TOP[1]:
            sil = span_peak(x, rate, *END_SILENCE)
            tone = span_rms_db(x, rate, *END_TONE_TOP)
            summary["ending_silence_peak"] = sil
            summary["ending_tone_rms_dbfs"] = tone
            checks["ending_silence"] = sil < SILENCE_ABS
            checks["ending_tone_-34dB"] = abs(tone - (-34.0)) <= TOLERANCE_DB
    summary["zones"] = zones
    peaks, steps = top_events(x, rate, args.top)
    summary["loudest_seconds"] = peaks
    summary["largest_steps"] = steps
    summary["checks"] = checks
    summary["pass"] = all(checks.values())

    out = args.json or (args.wav + ".analysis.json")
    with open(out, "w") as f:
        json.dump(summary, f, indent=2)
    if args.csv:
        with open(args.csv, "w") as f:
            f.write("t,rms_db\n")
            for i, v in enumerate(curve):
                f.write("%.2f,%.2f\n" % ((i + 0.5) * WINDOW_S, v))

    print("%s: %.1f s, %d Hz, %d ch" % (args.wav, dur, rate, ch))
    print("  peak %.2f dBFS, true peak %.2f dBFS, max step %.4f, DC %s, RMS %.2f dBFS"
          % (res["peak_dbfs"], res["true_peak_dbfs"], res["max_step"], ", ".join("%.1e" % v for v in res["dc"]), res["rms_dbfs"]))
    for z in zones:
        print("  %-10s target %6.1f  windows %4d  min %7.2f  median %7.2f  max %7.2f  bad %d"
              % (z["zone"], z["target_db"], z["windows"], z["min_db"] or 0, z["median_db"] or 0, z["max_db"] or 0, z["bad_windows"]))
    if "silence_depth_db" in summary:
        print("  deliberate silences (dB below the surrounding level): %s" % summary["silence_depth_db"])
    if "ending_silence_peak" in summary:
        print("  ending: silence peak %.2e, tone RMS %.2f dBFS" % (summary["ending_silence_peak"], summary["ending_tone_rms_dbfs"]))
    if peaks:
        print("  loudest seconds: " + ", ".join("%.0f s %.1f dB" % (p["t"], p["peak_dbfs"]) for p in peaks))
        print("  largest steps:   " + ", ".join("%.0f s %.4f" % (s["t"], s["step"]) for s in steps))
    for k, v in checks.items():
        print("  [%s] %s" % ("PASS" if v else "FAIL", k))
    print("  summary -> %s" % out)
    return 0 if summary["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
