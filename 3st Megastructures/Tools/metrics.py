#!/usr/bin/env python3
"""Project metrics for Thread (MASTER_PROMPT Part 15.3).

Measures, from the Claude Code transcripts of THIS project only:
  * total tokens per category (uncached input, cache read, cache write 5m / 1h, output),
    deduplicated by message id (a streamed message is written several times),
  * wall-clock time (first to last timestamp) and active time (wall-clock minus every
    gap longer than 10 minutes without any transcript activity),
  * API-equivalent cost from a pricing file (Tools/pricing.json) whose numbers come from
    the official Anthropic pricing page (URL and access date stored in that file).

Run:  python -I Tools/metrics.py [--pricing Tools/pricing.json] [--json out.json] [--md out.md]
Read-only on the transcript folder. No third-party packages.
"""
import argparse
import glob
import json
import os
import sys
from datetime import datetime, timezone

PROJECTS_ROOT = os.path.join(os.path.expanduser("~"), ".claude", "projects")
FOLDER_GLOB = "*3rd-Megastructures*"
CWD_MARKER = "3rd Megastructures"
GAP_SECONDS = 600.0


def parse_ts(s):
    if not s:
        return None
    try:
        if s.endswith("Z"):
            s = s[:-1] + "+00:00"
        return datetime.fromisoformat(s)
    except Exception:
        return None


def hms(seconds):
    seconds = int(round(seconds))
    h, rem = divmod(seconds, 3600)
    m, s = divmod(rem, 60)
    return f"{h}:{m:02d}:{s:02d}"


def folder_confirmed(folder):
    """Confirm by the cwd field inside the files that the folder belongs to this project."""
    for path in glob.glob(os.path.join(folder, "**", "*.jsonl"), recursive=True):
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                for i, line in enumerate(f):
                    if i > 200:
                        break
                    try:
                        obj = json.loads(line)
                    except Exception:
                        continue
                    cwd = obj.get("cwd")
                    if cwd:
                        return CWD_MARKER in cwd
        except OSError:
            continue
    return False


def collect(root=PROJECTS_ROOT):
    folders = []
    for folder in sorted(glob.glob(os.path.join(root, FOLDER_GLOB))):
        if not os.path.isdir(folder):
            continue
        base = os.path.basename(folder)
        # exclude the sibling game projects and the parent consulting folder by construction
        if "3rd-Megastructures" not in base:
            continue
        if folder_confirmed(folder):
            folders.append(folder)

    files = []
    for folder in folders:
        files.extend(sorted(glob.glob(os.path.join(folder, "**", "*.jsonl"), recursive=True)))

    messages = {}      # dedup key -> usage record
    timestamps = []
    models = {}
    n_main = n_sub = n_wf = 0
    for path in files:
        rel = os.path.relpath(path, PROJECTS_ROOT).replace("\\", "/")
        parts = rel.split("/")
        if "workflows" in parts:
            n_wf += 1
        elif "subagents" in parts:
            n_sub += 1
        else:
            n_main += 1
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except Exception:
                    continue
                cwd = obj.get("cwd")
                if cwd and CWD_MARKER not in cwd:
                    continue
                ts = parse_ts(obj.get("timestamp"))
                if ts is not None:
                    timestamps.append(ts)
                if obj.get("type") != "assistant":
                    continue
                msg = obj.get("message") or {}
                usage = msg.get("usage")
                if not usage:
                    continue
                key = msg.get("id") or obj.get("requestId") or obj.get("uuid")
                cc = usage.get("cache_creation") or {}
                rec = {
                    "model": msg.get("model", "unknown"),
                    "input": int(usage.get("input_tokens") or 0),
                    "cache_read": int(usage.get("cache_read_input_tokens") or 0),
                    "cache_write": int(usage.get("cache_creation_input_tokens") or 0),
                    "cache_write_5m": int(cc.get("ephemeral_5m_input_tokens") or 0),
                    "cache_write_1h": int(cc.get("ephemeral_1h_input_tokens") or 0),
                    "output": int(usage.get("output_tokens") or 0),
                    "file": rel,
                }
                old = messages.get(key)
                # keep the most complete copy of a streamed message (largest output count)
                if old is None or rec["output"] >= old["output"]:
                    messages[key] = rec
    for rec in messages.values():
        models[rec["model"]] = models.get(rec["model"], 0) + 1
    return folders, files, messages, timestamps, models, (n_main, n_sub, n_wf)


def time_stats(timestamps):
    if not timestamps:
        return None
    ts = sorted(timestamps)
    wall = (ts[-1] - ts[0]).total_seconds()
    gaps = []
    for a, b in zip(ts, ts[1:]):
        d = (b - a).total_seconds()
        if d > GAP_SECONDS:
            gaps.append((a, b, d))
    active = wall - sum(g[2] for g in gaps)
    return {"first": ts[0], "last": ts[-1], "wall": wall, "gaps": gaps, "active": active}


def cost(messages, pricing):
    """Cost per category in USD. pricing: per-million-token prices."""
    if not pricing:
        return None
    p = pricing["prices_per_mtok"]
    lc = pricing.get("long_context")  # optional {"threshold_input_tokens": N, "multipliers": {...}}
    totals = {"input": 0.0, "cache_read": 0.0, "cache_write_5m": 0.0, "cache_write_1h": 0.0, "output": 0.0}
    for rec in messages.values():
        mult = {k: 1.0 for k in totals}
        if lc:
            prompt = rec["input"] + rec["cache_read"] + rec["cache_write"]
            if prompt > lc["threshold_input_tokens"]:
                for k, v in lc["multipliers"].items():
                    mult[k] = v
        w5 = rec["cache_write_5m"]
        w1 = rec["cache_write_1h"]
        if w5 + w1 == 0 and rec["cache_write"] > 0:
            w5 = rec["cache_write"]  # no split given: price as 5-minute writes
        totals["input"] += rec["input"] * p["input"] * mult["input"] / 1e6
        totals["cache_read"] += rec["cache_read"] * p["cache_read"] * mult["cache_read"] / 1e6
        totals["cache_write_5m"] += w5 * p["cache_write_5m"] * mult["cache_write_5m"] / 1e6
        totals["cache_write_1h"] += w1 * p["cache_write_1h"] * mult["cache_write_1h"] / 1e6
        totals["output"] += rec["output"] * p["output"] * mult["output"] / 1e6
    totals["total"] = sum(totals.values())
    return totals


def main():
    ap = argparse.ArgumentParser()
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument("--pricing", default=os.path.join(here, "pricing.json"))
    ap.add_argument("--json", default=None)
    ap.add_argument("--md", default=None)
    args = ap.parse_args()

    folders, files, messages, timestamps, models, counts = collect()
    tok = {k: sum(r[k] for r in messages.values())
           for k in ("input", "cache_read", "cache_write", "cache_write_5m", "cache_write_1h", "output")}
    tok["total"] = tok["input"] + tok["cache_read"] + tok["cache_write"] + tok["output"]
    t = time_stats(timestamps)
    pricing = None
    if args.pricing and os.path.exists(args.pricing):
        with open(args.pricing, "r", encoding="utf-8") as f:
            pricing = json.load(f)
    c = cost(messages, pricing)

    rows = []
    rows.append(("Transcript folders", str(len(folders)), "measured: glob *3rd-Megastructures* under ~/.claude/projects, confirmed by cwd field"))
    rows.append(("Transcript files (main / subagent / workflow)", f"{counts[0]} / {counts[1]} / {counts[2]}", "measured: *.jsonl files found recursively"))
    rows.append(("Assistant messages (deduplicated)", f"{len(messages):,}", "measured: unique message.id with usage"))
    rows.append(("Models", ", ".join(f"{k} ({v})" for k, v in sorted(models.items())), "measured: message.model"))
    rows.append(("Uncached input tokens", f"{tok['input']:,}", "measured: sum usage.input_tokens"))
    rows.append(("Cache read tokens", f"{tok['cache_read']:,}", "measured: sum usage.cache_read_input_tokens"))
    rows.append(("Cache write tokens (5 min / 1 h)", f"{tok['cache_write']:,} ({tok['cache_write_5m']:,} / {tok['cache_write_1h']:,})", "measured: usage.cache_creation_input_tokens and its ephemeral split"))
    rows.append(("Output tokens (incl. thinking)", f"{tok['output']:,}", "measured: sum usage.output_tokens"))
    rows.append(("Total tokens", f"{tok['total']:,}", "measured: sum of the four categories"))
    if t:
        rows.append(("Wall-clock time", hms(t["wall"]), f"measured: first {t['first'].isoformat()} to last {t['last'].isoformat()} transcript timestamp"))
        rows.append(("Idle gaps > 10 min", f"{len(t['gaps'])} gaps, {hms(sum(g[2] for g in t['gaps']))}", "measured: consecutive timestamps further apart than 600 s"))
        rows.append(("Active time", hms(t["active"]), "derived: wall-clock minus all gaps > 10 min"))
    if c:
        rows.append(("Cost: uncached input", f"${c['input']:,.2f}", f"estimated: tokens x ${pricing['prices_per_mtok']['input']}/MTok"))
        rows.append(("Cost: cache read", f"${c['cache_read']:,.2f}", f"estimated: tokens x ${pricing['prices_per_mtok']['cache_read']}/MTok"))
        rows.append(("Cost: cache write 5 min", f"${c['cache_write_5m']:,.2f}", f"estimated: tokens x ${pricing['prices_per_mtok']['cache_write_5m']}/MTok"))
        rows.append(("Cost: cache write 1 h", f"${c['cache_write_1h']:,.2f}", f"estimated: tokens x ${pricing['prices_per_mtok']['cache_write_1h']}/MTok"))
        rows.append(("Cost: output", f"${c['output']:,.2f}", f"estimated: tokens x ${pricing['prices_per_mtok']['output']}/MTok"))
        rows.append(("Total API-equivalent cost", f"${c['total']:,.2f}", f"estimated: sum of categories; prices from {pricing.get('source_url','?')} accessed {pricing.get('accessed','?')} for {pricing.get('model','?')}"))
    else:
        rows.append(("Total API-equivalent cost", "unavailable", "no pricing file; cost = in*P_in + cr*P_cr + w5*P_w5 + w1*P_w1 + out*P_out"))

    md = ["| Metric | Value | How obtained |", "|---|---|---|"]
    md += [f"| {a} | {b} | {c_} |" for a, b, c_ in rows]
    if t and t["gaps"]:
        md.append("")
        md.append("Gaps longer than 10 minutes (UTC):")
        for a, b, d in t["gaps"]:
            md.append(f"- {a.isoformat()} -> {b.isoformat()} ({hms(d)})")
    text = "\n".join(md)
    print(text)
    if args.md:
        with open(args.md, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    if args.json:
        out = {"tokens": tok, "messages": len(messages), "files": counts, "folders": folders,
               "models": models, "cost": c,
               "time": None if not t else {"first": t["first"].isoformat(), "last": t["last"].isoformat(),
                                           "wall_s": t["wall"], "active_s": t["active"],
                                           "gaps": [(a.isoformat(), b.isoformat(), d) for a, b, d in t["gaps"]]}}
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(out, f, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main())
