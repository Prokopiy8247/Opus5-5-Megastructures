#!/usr/bin/env python3
"""audio_analysis: offline checks of ThreadDsp renders and -audio-dump files (MASTER_PROMPT 11.9, 14.3.3, 14.10.3).

Usage:
  python -I Tools/qa/audio_analysis.py <file.wav | folder> [more ...] [--run <id>] [--out-dir QA/reports]
         [--window 1.0] [--chunk-seconds 10]

Input: WAV files (16/24/32-bit PCM or 32-bit IEEE float, any channel count and rate; WAVE_FORMAT_EXTENSIBLE ok),
each with an optional JSON sidecar <name>.json / <name>.wav.json / <name>_s.json:
  {"s": [plan distance at t = t0 + i*dt], "dt": 1.0, "t0": 0.0}   s per second of the render (beat assignment)
  {"beat": N}                                                     the whole file is beat N (also "beatNN" in the name)
  {"stem": "mix" | "wind" | ...}                                  default "mix"
  {"footsteps": [onset seconds]}                                  excluded from the sample-step check (+-10/300 ms)
Per file (streamed in chunks, any length): sample peak (dBFS), DC per channel, largest sample-to-sample step, NaN /
Inf count, RMS per --window seconds, max 400 ms RMS, BS.1770-4 K-weighted integrated loudness (400 ms blocks, 75 %
overlap, -70 LUFS absolute and -10 LU relative gates; momentary and short-term maxima).  Per beat (all files
together, 100 ms blocks assigned by s at the block centre, beat boundaries from ThreadConstants): RMS against the
14.3.3 totals +-3 dB, excluding s 268-302 in beat 3 (inhale) and s 330-345 in beat 4 (swell).
Checks: peak below -1.5 dBFS, |DC| below 0.001, steps at most 0.25 FS, no NaN, a mix is not silent, peak outside the
swell window at most -9 dBFS ("momentary peak", needs s), wind stem below -70 dBFS RMS from s = 752; integrated
loudness about -30 LUFS is reported (INFO).  Writes QA/reports/audio_<run>.json and .md; exit code 1 on any FAIL.
"""
import argparse
import math
import os
import re
import struct
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import numpy as np  # noqa: E402

import qa_common as qc  # noqa: E402
from qa_common import PASS, FAIL, SKIP, INFO, WARN, TH, check  # noqa: E402


# --------------------------------------------------------------------------------------------- WAV reading
class Wav(object):
    def __init__(self, path):
        self.path = path
        with open(path, "rb") as f:
            hdr = f.read(12)
            if len(hdr) < 12 or hdr[0:4] not in (b"RIFF", b"RF64") or hdr[8:12] != b"WAVE":
                raise ValueError("not a RIFF/WAVE file")
            fmt, data_off, data_size = None, None, None
            fsize = os.path.getsize(path)
            pos = 12
            while pos + 8 <= fsize:
                f.seek(pos)
                cid = f.read(4)
                size = struct.unpack("<I", f.read(4))[0]
                if cid == b"fmt ":
                    body = f.read(min(size, 64))
                    tag, ch, fs, _, block, bits = struct.unpack_from("<HHIIHH", body, 0)
                    if tag == 0xFFFE and size >= 26:
                        tag = struct.unpack_from("<H", body, 24)[0]
                    fmt = (tag, ch, fs, bits, block)
                elif cid == b"data":
                    data_off = pos + 8
                    data_size = size
                    if size in (0, 0xFFFFFFFF) or data_off + size > fsize:  # unfinalised / streamed writer
                        data_size = fsize - data_off
                    break
                pos += 8 + size + (size & 1)
        if fmt is None or data_off is None:
            raise ValueError("missing fmt or data chunk")
        self.tag, self.ch, self.fs, self.bits, self.block = fmt
        if (self.tag, self.bits) not in ((1, 16), (1, 24), (1, 32), (3, 32)):
            raise ValueError("unsupported sample format tag %d / %d bit" % (self.tag, self.bits))
        self.bps = self.bits // 8
        self.block = self.block or self.bps * self.ch
        self.data_off = data_off
        self.frames = data_size // self.block
        self.format = {1: "PCM", 3: "float"}[self.tag] + "%d" % self.bits

    def read(self, start, count):
        """Frames [start, start+count) as float64 (count, ch) in full-scale units."""
        count = max(0, min(count, self.frames - start))
        with open(self.path, "rb") as f:
            f.seek(self.data_off + start * self.block)
            raw = f.read(count * self.block)
        if self.tag == 3:
            x = np.frombuffer(raw, dtype="<f4").astype(np.float64)
        elif self.bits == 16:
            x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
        elif self.bits == 32:
            x = np.frombuffer(raw, dtype="<i4").astype(np.float64) / 2147483648.0
        else:
            b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
            v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
            v = (v ^ 0x800000) - 0x800000
            x = v.astype(np.float64) / 8388608.0
        return x.reshape(-1, self.ch)


# --------------------------------------------------------------------------------------------- K-weighting
def k_weighting(fs):
    """BS.1770 pre-filter (shelf) and RLB high-pass for any rate (libebur128 formulas; R5 snippet D)."""
    f0, G, Q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    K = math.tan(math.pi * f0 / fs)
    Vh = 10 ** (G / 20)
    Vb = Vh ** 0.4996667741545416
    a0 = 1 + K / Q + K * K
    shelf = ([(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0],
             [1.0, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0])
    f0, Q = 38.13547087602444, 0.5003270373238773
    K = math.tan(math.pi * f0 / fs)
    a0 = 1 + K / Q + K * K
    hp = ([1.0, -2.0, 1.0], [1.0, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0])
    return shelf, hp


def biquad_impulse(b, a, n):
    h = np.zeros(n)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(n):
        x = 1.0 if i == 0 else 0.0
        y = b[0] * x + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
        x2, x1, y2, y1 = x1, x, y1, y
        h[i] = y
    return h


def k_impulse(fs, n=1 << 15):
    (b1, a1), (b2, a2) = k_weighting(fs)
    return np.convolve(biquad_impulse(b1, a1, n), biquad_impulse(b2, a2, n))[:n]


def lk(z):
    return -0.691 + 10.0 * np.log10(np.maximum(z, 1e-30))


def db(v):
    return 20.0 * math.log10(max(float(v), 1e-12))


def db_pow(p):
    return 10.0 * math.log10(max(float(p), 1e-24))


def integrated_lufs(sub):
    """sub: per-100 ms channel-summed K-weighted mean squares.  Returns (integrated, momentary max, short max)."""
    sub = np.asarray(sub, dtype=np.float64)
    if sub.size < 4:
        return float("-inf"), float("-inf"), float("-inf")
    blocks = np.convolve(sub, np.ones(4) / 4.0, mode="valid")
    L = lk(blocks)
    g = blocks[L > -70.0]
    mm = float(L.max())
    st = float(lk(np.convolve(sub, np.ones(30) / 30.0, mode="valid")).max()) if sub.size >= 30 else float("nan")
    if g.size == 0:
        return float("-inf"), mm, st
    rel = lk(g.mean()) - 10.0
    g2 = blocks[(L > -70.0) & (L > rel)]
    return (float(lk(g2.mean())) if g2.size else float("-inf")), mm, st


# --------------------------------------------------------------------------------------------- sidecar
def find_sidecar(path):
    base = os.path.splitext(path)[0]
    for c in (base + ".json", path + ".json", base + "_s.json"):
        if os.path.isfile(c):
            return c
    return None


def s_of_time(side, t):
    """Plan distance at times t from a sidecar, or None."""
    if not side:
        return None
    s = side.get("s", side.get("sPerSecond", side.get("s_per_second")))
    if isinstance(s, list) and len(s) >= 1:
        dt, t0 = float(side.get("dt", 1.0)), float(side.get("t0", 0.0))
        ts = t0 + dt * np.arange(len(s))
        return np.interp(t, ts, np.asarray(s, dtype=np.float64))
    segs = side.get("segments")
    if isinstance(segs, list) and segs:
        out = np.full(np.shape(t), np.nan)
        for g in segs:
            m = (t >= g["t0"]) & (t <= g["t1"])
            out[m] = g["s0"] + (t[m] - g["t0"]) * (g["s1"] - g["s0"]) / max(g["t1"] - g["t0"], 1e-9)
        return out
    return None


# --------------------------------------------------------------------------------------------- analysis
def analyse_wav(path, chunk_seconds=10.0, window_s=1.0):
    w = Wav(path)
    sp = qc.spec()
    side_path = find_sidecar(path)
    side = qc.read_json(side_path, {}) if side_path else {}
    side = side if isinstance(side, dict) else {}
    stem = (side.get("stem") or ("wind" if re.search(r"wind", os.path.basename(path), re.I) else "mix")).lower()
    fs, ch = w.fs, w.ch
    hop = fs // 10
    warn = []
    if fs % 10:
        warn.append("sample rate %d not divisible by 10: 100 ms blocks are %d samples" % (fs, hop))
    chunk = hop * max(1, int(round(chunk_seconds * 10)))
    h = k_impulse(fs)
    m = len(h)
    nfft = 1 << int(math.ceil(math.log2(chunk + m - 1)))
    Hf = np.fft.rfft(h, nfft)
    carry = np.zeros((m - 1, ch))
    foot = np.asarray(side.get("footsteps", []) or [], dtype=np.float64)
    peak, peak_at = 0.0, 0
    dc = np.zeros(ch)
    max_step, step_at, n_over = 0.0, 0, 0
    nan_count = 0
    last = None
    ms_blocks, pk_blocks, k_blocks = [], [], []
    tail = np.zeros((0, ch))   # samples carried to complete a 100 ms block
    ktail = np.zeros((0, ch))
    for start in range(0, w.frames, chunk):
        x = w.read(start, chunk)
        bad = ~np.isfinite(x)
        if bad.any():
            nan_count += int(bad.sum())
            x = np.where(bad, 0.0, x)
        a = np.abs(x)
        i = int(np.argmax(a.max(axis=1))) if x.shape[0] else 0
        if x.shape[0] and a[i].max() > peak:
            peak, peak_at = float(a[i].max()), start + i
        dc += x.sum(axis=0)
        xx = np.concatenate([last, x]) if last is not None else x
        if xx.shape[0] > 1:
            st = np.abs(np.diff(xx, axis=0)).max(axis=1)
            base = start - (1 if last is not None else 0)
            if foot.size:
                tt = (base + 1 + np.arange(st.size)) / float(fs)
                near = np.zeros(st.size, dtype=bool)
                for f0 in foot[(foot >= tt[0] - 0.30) & (foot <= tt[-1] + 0.01)]:
                    near |= (tt >= f0 - 0.01) & (tt <= f0 + 0.30)
                st = np.where(near, 0.0, st)
            j = int(np.argmax(st))
            if st[j] > max_step:
                max_step, step_at = float(st[j]), base + 1 + j
            n_over += int((st > TH["audio_step_max"]).sum())
        last = x[-1:].copy()
        # K-weighting by FFT overlap-add (exact FIR of the truncated impulse response)
        y = np.fft.irfft(np.fft.rfft(x, nfft, axis=0) * Hf[:, None], nfft, axis=0)[:x.shape[0] + m - 1]
        y[:m - 1] += carry
        carry = y[x.shape[0]:x.shape[0] + m - 1].copy()
        if carry.shape[0] < m - 1:
            carry = np.concatenate([carry, np.zeros((m - 1 - carry.shape[0], ch))])
        yk = y[:x.shape[0]]
        xb = np.concatenate([tail, x])
        kb = np.concatenate([ktail, yk])
        nb = xb.shape[0] // hop
        if nb:
            blk = xb[:nb * hop].reshape(nb, hop, ch)
            ms_blocks.append((blk ** 2).mean(axis=(1, 2)))
            pk_blocks.append(np.abs(blk).max(axis=(1, 2)))
            kblk = kb[:nb * hop].reshape(nb, hop, ch)
            k_blocks.append((kblk ** 2).mean(axis=1).sum(axis=1))
        tail, ktail = xb[nb * hop:], kb[nb * hop:]
    ms = np.concatenate(ms_blocks) if ms_blocks else np.zeros(0)
    pk = np.concatenate(pk_blocks) if pk_blocks else np.zeros(0)
    kb = np.concatenate(k_blocks) if k_blocks else np.zeros(0)
    nblk = ms.size
    t_c = (np.arange(nblk) + 0.5) * hop / float(fs)
    s_c = s_of_time(side, t_c) if nblk else None
    file_beat = side.get("beat")
    if file_beat is None:
        mb = re.search(r"beat0*([1-7])", os.path.basename(path), re.I)
        file_beat = int(mb.group(1)) if mb else None
    beats = np.zeros(nblk, dtype=np.int64)
    excluded = np.zeros(nblk, dtype=bool)
    b = sp.beat_boundaries
    if s_c is not None:
        for k in range(len(b) - 1):
            beats[(s_c >= b[k]) & (s_c < b[k + 1])] = k + 1
        excluded |= (beats == 3) & (s_c >= sp.inhale[0]) & (s_c <= sp.inhale[1])
        excluded |= (beats == 4) & (s_c >= sp.swell[0]) & (s_c <= sp.swell[1])
    elif file_beat:
        beats[:] = int(file_beat)
        if int(file_beat) in (3, 4):
            warn.append("beat %d without s sidecar: the inhale/swell window cannot be excluded" % int(file_beat))
    wn = max(1, int(round(window_s * 10)))
    nw = nblk // wn
    rms_win = [round(db_pow(v), 2) for v in ms[:nw * wn].reshape(nw, wn).mean(axis=1)] if nw else []
    m400 = np.convolve(ms, np.ones(4) / 4.0, mode="valid") if nblk >= 4 else ms
    integ, mmax, stmax = integrated_lufs(kb)
    res = {"file": path, "sidecar": side_path, "stem": stem, "format": w.format, "fs": fs, "channels": ch,
           "seconds": w.frames / float(fs), "frames": w.frames, "nan_or_inf": nan_count,
           "sample_peak": peak, "sample_peak_dbfs": db(peak), "sample_peak_at_s": peak_at / float(fs),
           "dc_per_channel": [float(v) for v in dc / max(w.frames, 1)],
           "max_step": max_step, "max_step_at_s": step_at / float(fs), "steps_over_limit": n_over,
           "rms_dbfs": db_pow(ms.mean()) if nblk else None,
           "max_400ms_rms_dbfs": db_pow(m400.max()) if m400.size else None,
           "rms_per_window_dbfs": rms_win, "window_s": wn / 10.0,
           "lufs_integrated": integ, "lufs_momentary_max": mmax, "lufs_short_term_max": stmax,
           "warnings": warn, "_ms": ms, "_pk": pk, "_kb": kb, "_beats": beats, "_excl": excluded, "_s": s_c,
           "has_s": s_c is not None}
    return res


def file_checks(r):
    C = []
    C.append(check("NaN/Inf samples", PASS if r["nan_or_inf"] == 0 else FAIL, r["nan_or_inf"], "0"))
    pkl = TH["audio_peak_max_dbfs"]
    C.append(check("sample peak", PASS if r["sample_peak_dbfs"] < pkl else FAIL, round(r["sample_peak_dbfs"], 2),
                   "< %g dBFS" % pkl, "at %.3f s" % r["sample_peak_at_s"]))
    dcm = max(abs(v) for v in r["dc_per_channel"]) if r["dc_per_channel"] else 0.0
    C.append(check("DC offset", PASS if dcm < TH["audio_dc_max"] else FAIL, round(dcm, 6), "< %g" % TH["audio_dc_max"]))
    C.append(check("sample step", PASS if r["max_step"] <= TH["audio_step_max"] else FAIL, round(r["max_step"], 4),
                   "<= %g FS" % TH["audio_step_max"], "at %.4f s, %d over" % (r["max_step_at_s"], r["steps_over_limit"])))
    if r["stem"] == "mix":
        rd = r["rms_dbfs"]
        C.append(check("not silent", PASS if rd is not None and rd > -80.0 else FAIL, qc.fmt(rd, 2), "> -80 dBFS RMS"))
    if r["has_s"] and r["stem"] == "mix":
        sp = qc.spec()
        s, pk = r["_s"], r["_pk"]
        out = ~((s >= sp.swell[0]) & (s <= sp.swell[1]))
        if out.any():
            mp = db(pk[out].max())
            lim = TH["audio_momentary_peak_max_dbfs"]
            C.append(check("peak outside swell", PASS if mp <= lim else FAIL, round(mp, 2), "<= %g dBFS" % lim,
                           "14.3.3 momentary peak (sample peak per 100 ms block outside s %g-%g)" % sp.swell))
        inside = ~out
        if inside.any():
            C.append(check("swell peak", INFO, round(db(pk[inside].max()), 2), "about -6 dBFS", "14.3.6 item 3"))
    if r["stem"] == "wind" and r["has_s"]:
        sp = qc.spec()
        m = r["_s"] >= sp.wind_off_s
        if m.any():
            v = db_pow(r["_ms"][m].mean())
            C.append(check("wind off from s %g" % sp.wind_off_s, PASS if v < TH["wind_off_max_dbfs"] else FAIL,
                           round(v, 2), "< %g dBFS" % TH["wind_off_max_dbfs"]))
    lu = r["lufs_integrated"]
    note = "target about %g LUFS (quiet by design; not gated)" % TH["audio_lufs_target"]
    C.append(check("integrated loudness", INFO, qc.fmt(lu, 2), "~%g LUFS" % TH["audio_lufs_target"], note))
    return C


def beat_table(results):
    sp = qc.spec()
    rows, C = [], []
    acc = {k: [] for k in range(1, len(sp.beat_boundaries))}
    for r in results:
        if r["stem"] != "mix":
            continue
        for k in acc:
            m = (r["_beats"] == k) & ~r["_excl"]
            if m.any():
                acc[k].append(r["_ms"][m])
    for k in sorted(acc):
        name = sp.beat_names[k - 1] if k - 1 < len(sp.beat_names) else str(k)
        tgt = sp.target_rms_db[k - 1] if k - 1 < len(sp.target_rms_db) else None
        if not acc[k] or tgt is None:
            C.append(check("beat %d RMS" % k, SKIP, None, None, "no blocks (no s sidecar / beat tag)"))
            rows.append([k, name, "-", tgt, "-", "-", SKIP])
            continue
        v = np.concatenate(acc[k])
        rd = db_pow(v.mean())
        dev = rd - tgt
        st = PASS if abs(dev) <= TH["audio_rms_tol_db"] else FAIL
        excl = {3: "inhale s %g-%g excluded" % sp.inhale, 4: "swell s %g-%g excluded" % sp.swell}.get(k, "")
        C.append(check("beat %d RMS" % k, st, round(rd, 2), "%g +- %g dB" % (tgt, TH["audio_rms_tol_db"]), excl))
        rows.append([k, name, round(rd, 2), tgt, "%+.2f" % dev, "%.1f s" % (v.size / 10.0), st])
    return rows, C


def build_markdown(run, results, beat_rows, beat_checks, combined_lufs, errors):
    allc = [c for r in results for c in r["checks"]] + beat_checks
    cnt = qc.count_status(allc)
    L = ["# Audio analysis: %s" % run, "",
         "Files: %d. Checks: %d PASS, %d FAIL, %d SKIP, %d INFO. Scene constants: %s." % (
             len(results), cnt[PASS], cnt[FAIL] + len(errors), cnt[SKIP], cnt[INFO], qc.spec().source), "",
         "**Result: %s**" % (FAIL if (cnt[FAIL] or errors) else (PASS if results else SKIP)), ""]
    if errors:
        L += ["## Unreadable files", ""] + ["- %s" % e for e in errors] + [""]
    L += ["## RMS per beat (mix files, 14.3.3 totals +-3 dB)", ""]
    L.append(qc.md_table(["Beat", "Name", "RMS dBFS", "Target", "Delta dB", "Measured", "Result"], beat_rows))
    if combined_lufs is not None:
        L += ["Integrated loudness over all mix files: %s LUFS (target about -30, INFO)." % qc.fmt(combined_lufs, 2), ""]
    L += ["## Files", ""]
    rows = []
    for r in results:
        rows.append([os.path.basename(r["file"]), r["stem"], "%s %d Hz x%d" % (r["format"], r["fs"], r["channels"]),
                     qc.fmt(r["seconds"], 1), qc.fmt(r["sample_peak_dbfs"], 2), qc.fmt(max(abs(v) for v in r["dc_per_channel"]), 6),
                     qc.fmt(r["max_step"], 4), qc.fmt(r["rms_dbfs"], 2), qc.fmt(r["lufs_integrated"], 2), r["status"]])
    L.append(qc.md_table(["File", "Stem", "Format", "Seconds", "Peak dBFS", "|DC|", "Max step", "RMS dBFS", "LUFS",
                          "Result"], rows))
    from sequences import sparkline  # local import: shared helper
    for r in results:
        L += ["### %s (%s)" % (os.path.basename(r["file"]), r["status"]), "",
              "RMS per %g s: `%s`" % (r["window_s"], sparkline(r["rms_per_window_dbfs"])), ""]
        for wmsg in r.get("warnings", []):
            L.append("- WARN: %s" % wmsg)
        L.append(qc.md_table(["Check", "Status", "Value", "Threshold", "Note"],
                             [[c["name"], c["status"], qc.fmt(c["value"], 4), c["threshold"] or "", c["note"] or ""]
                              for c in r["checks"]]))
    L += ["## Method notes", "",
          "- RMS in dBFS = 20 log10(rms of all samples of all channels) (a full-scale sine reads -3.01 dBFS).",
          "- LUFS sums the channel powers after K-weighting, so a centred signal reads about +3 dB above its "
          "per-channel RMS and the 36.7/55 Hz drone loses 3-6 dB (R5 section 3): the two scales differ by design.",
          ""]
    return "\n".join(L)


def collect(paths):
    out = []
    for p in paths:
        if os.path.isdir(p):
            for name in sorted(os.listdir(p)):
                if name.lower().endswith(".wav"):
                    out.append(os.path.join(p, name))
        elif os.path.isfile(p):
            out.append(p)
    return out


def run(paths, run_name=None, out_dir=qc.REPORTS_DIR, window_s=1.0, chunk_seconds=10.0, quiet=False):
    files = collect(paths)
    if run_name is None:
        run_name = qc.run_id_for(paths[0]) if paths else "audio"
    results, errors = [], []
    for f in files:
        if not quiet:
            print("[audio_analysis] %s" % f, flush=True)
        try:
            r = analyse_wav(f, chunk_seconds, window_s)
        except Exception as e:
            errors.append("%s: %s: %s" % (f, type(e).__name__, e))
            continue
        r["checks"] = file_checks(r)
        r["status"] = qc.worst_status(r["checks"])
        results.append(r)
    beat_rows, beat_checks = beat_table(results)
    mix_k = [r["_kb"] for r in results if r["stem"] == "mix" and r["_kb"].size]
    combined = integrated_lufs(np.concatenate(mix_k))[0] if len(mix_k) > 1 else None
    allc = [c for r in results for c in r["checks"]] + beat_checks
    status = FAIL if (errors or any(c["status"] == FAIL for c in allc)) else (PASS if results else SKIP)
    js = os.path.join(out_dir, "audio_%s.json" % run_name)
    md = os.path.join(out_dir, "audio_%s.md" % run_name)
    pub = [{k: v for k, v in r.items() if not k.startswith("_")} for r in results]
    qc.write_json(js, {"run": run_name, "status": status, "files": pub, "errors": errors,
                       "beats": [dict(zip(["beat", "name", "rms_dbfs", "target", "delta", "measured", "status"], row))
                                 for row in beat_rows], "beat_checks": beat_checks, "lufs_combined": combined})
    qc.write_text(md, build_markdown(run_name, results, beat_rows, beat_checks, combined, errors))
    fails = ["%s: %s (%s)" % (os.path.basename(r["file"]), c["name"], qc.fmt(c["value"], 4)) for r in results
             for c in r["checks"] if c["status"] == FAIL]
    fails += ["%s (%s)" % (c["name"], qc.fmt(c["value"], 2)) for c in beat_checks if c["status"] == FAIL]
    fails += errors
    return {"tool": "audio_analysis", "status": status, "json": js, "md": md, "fails": fails,
            "counts": qc.count_status(allc), "n": len(results)}


def main(argv=None):
    ap = argparse.ArgumentParser(prog="audio_analysis.py", description=__doc__.split("\n\n")[0],
                                 formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    ap.add_argument("paths", nargs="+", help="WAV files and/or folders with WAV files")
    ap.add_argument("--run", help="run id (default: folder name)")
    ap.add_argument("--out-dir", default=qc.REPORTS_DIR)
    ap.add_argument("--window", type=float, default=1.0, help="RMS window in seconds (default 1)")
    ap.add_argument("--chunk-seconds", type=float, default=10.0, help="streaming chunk length (default 10 s)")
    ap.add_argument("--quiet", action="store_true")
    a = ap.parse_args(argv)
    files = collect(a.paths)
    if not files:
        print("no WAV file found in %s" % ", ".join(a.paths), file=sys.stderr)
        return 1
    r = run(a.paths, a.run, a.out_dir, a.window, a.chunk_seconds, a.quiet)
    print("[audio_analysis] %s: %d files, %d FAIL -> %s" % (r["status"], r["n"], len(r["fails"]), r["md"]))
    for f in r["fails"][:30]:
        print("  FAIL " + f)
    return 1 if r["status"] == FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
