"""Project metrics for The Ring (MASTER_PROMPT Part 15.3): total time, tokens and API-equivalent cost.

Reads Claude Code transcripts (read-only) under %USERPROFILE%\\.claude\\projects, keeps only folders whose name
contains "2nd-Megastructures" and whose records carry a cwd inside this project, deduplicates assistant messages by
message id (falling back to requestId), sums usage per category, measures wall-clock and active time (gaps > 10 min
removed) and prices the usage with Tools/pricing.json (official prices fetched from the web, URL and date recorded there).

Usage:  python -I Tools/metrics.py [--json out.json] [--report OPUS_5.5_FINAL_REPORT.md]
"""
import datetime as dt
import glob
import json
import os
import sys

PROJECTS = os.path.join(os.path.expanduser("~"), ".claude", "projects")
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PRICING = os.path.join(ROOT, "Tools", "pricing.json")
CWD_MARK = "2nd megastructures\\2nd megastructures"
GAP_S = 600.0


def parse_ts(s):
    if not s:
        return None
    try:
        return dt.datetime.fromisoformat(s.replace("Z", "+00:00"))
    except ValueError:
        return None


def fmt_hms(sec):
    sec = int(round(sec))
    return "%d:%02d:%02d" % (sec // 3600, (sec % 3600) // 60, sec % 60)


def collect():
    folders = [f for f in glob.glob(os.path.join(PROJECTS, "*")) if os.path.isdir(f) and "2nd-Megastructures" in os.path.basename(f)]
    files = []
    for f in folders:
        files += glob.glob(os.path.join(f, "**", "*.jsonl"), recursive=True)
    msgs = {}
    stamps = []
    models = {}
    used_files = []
    sessions, subagents = set(), 0
    for path in sorted(files):
        cwd_ok = None
        recs = []
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    o = json.loads(line)
                except json.JSONDecodeError:
                    continue
                cwd = o.get("cwd")
                if cwd:
                    cwd_ok = CWD_MARK in cwd.lower().replace("/", "\\") or (cwd_ok is True)
                recs.append(o)
        if cwd_ok is not True:
            continue
        used_files.append(path)
        rel = os.path.relpath(path, PROJECTS)
        if os.sep + "subagents" + os.sep in rel:
            subagents += 1
        else:
            sessions.add(os.path.splitext(os.path.basename(path))[0])
        for o in recs:
            t = parse_ts(o.get("timestamp"))
            if t:
                stamps.append(t)
            if o.get("type") != "assistant":
                continue
            m = o.get("message") or {}
            u = m.get("usage")
            if not u:
                continue
            key = m.get("id") or o.get("requestId") or o.get("uuid")
            model = m.get("model") or o.get("requestedModel") or "unknown"
            cc = u.get("cache_creation") or {}
            rec = {
                "model": model,
                "input": int(u.get("input_tokens") or 0),
                "cache_read": int(u.get("cache_read_input_tokens") or 0),
                "cache_write": int(u.get("cache_creation_input_tokens") or 0),
                "cache_write_5m": int(cc.get("ephemeral_5m_input_tokens") or 0),
                "cache_write_1h": int(cc.get("ephemeral_1h_input_tokens") or 0),
                "output": int(u.get("output_tokens") or 0),
            }
            prev = msgs.get(key)
            # streamed messages repeat the usage; keep the record with the largest output (final usage)
            if prev is None or rec["output"] >= prev["output"]:
                msgs[key] = rec
    for r in msgs.values():
        models[r["model"]] = models.get(r["model"], 0) + 1
    return msgs, stamps, models, used_files, sessions, subagents


def main():
    args = sys.argv[1:]
    msgs, stamps, models, used, sessions, subagents = collect()
    tot = {k: 0 for k in ("input", "cache_read", "cache_write", "cache_write_5m", "cache_write_1h", "output")}
    for r in msgs.values():
        for k in tot:
            tot[k] += r[k]
    unsplit = tot["cache_write"] - tot["cache_write_5m"] - tot["cache_write_1h"]
    total_tokens = tot["input"] + tot["cache_read"] + tot["cache_write"] + tot["output"]
    stamps.sort()
    anchor = None
    sp = os.path.join(ROOT, "STATE.md")
    if os.path.exists(sp):
        import re as _re
        m = _re.search(r"Run started at (\S+)", open(sp, "r", encoding="utf-8").read())
        if m:
            anchor = parse_ts(m.group(1))
    wall = (stamps[-1] - stamps[0]).total_seconds() if stamps else 0.0
    wall_anchor = (stamps[-1] - anchor).total_seconds() if (stamps and anchor) else None
    gaps = []
    for a, b in zip(stamps, stamps[1:]):
        d = (b - a).total_seconds()
        if d > GAP_S:
            gaps.append((a, b, d))
    active = wall - sum(g[2] for g in gaps)

    price = None
    if os.path.exists(PRICING):
        with open(PRICING, "r", encoding="utf-8") as f:
            price = json.load(f)
    cost = None
    lines_cost = []
    if price:
        p = price["per_mtok_usd"]
        c_in = tot["input"] * p["input"] / 1e6
        c_cr = tot["cache_read"] * p["cache_read"] / 1e6
        c_w5 = (tot["cache_write_5m"] + max(unsplit, 0)) * p["cache_write_5m"] / 1e6
        c_w1 = tot["cache_write_1h"] * p["cache_write_1h"] / 1e6
        c_out = tot["output"] * p["output"] / 1e6
        cost = c_in + c_cr + c_w5 + c_w1 + c_out
        lines_cost = [
            ("Cost: uncached input", "$%.2f" % c_in, "%d x $%.2f / MTok" % (tot["input"], p["input"])),
            ("Cost: cache read", "$%.2f" % c_cr, "%d x $%.2f / MTok" % (tot["cache_read"], p["cache_read"])),
            ("Cost: cache write 5 min", "$%.2f" % c_w5, "%d x $%.2f / MTok" % (tot["cache_write_5m"] + max(unsplit, 0), p["cache_write_5m"])),
            ("Cost: cache write 1 h", "$%.2f" % c_w1, "%d x $%.2f / MTok" % (tot["cache_write_1h"], p["cache_write_1h"])),
            ("Cost: output (incl. thinking)", "$%.2f" % c_out, "%d x $%.2f / MTok" % (tot["output"], p["output"])),
            ("**Total API-equivalent cost**", "**$%.2f**" % cost, "sum of the five lines; prices for %s from %s (accessed %s)%s" % (
                price.get("model"), price.get("url"), price.get("accessed"), ("; " + price["note"]) if price.get("note") else "")),
        ]

    pre = [t for t in stamps if anchor and t < anchor]
    rows = []
    if wall_anchor is not None:
        gaps_a = [g for g in gaps if g[0] >= anchor]
        rows.append(("Wall-clock time since the run anchor", fmt_hms(wall_anchor), "measured: 'Run started at' in STATE.md (%s) to the last transcript timestamp" % anchor.isoformat()))
        rows.append(("Active time since the run anchor", fmt_hms(wall_anchor - sum(g[2] for g in gaps_a)), "measured: minus %d gaps > 10 min after the anchor" % len(gaps_a)))
    rows += [
        ("Wall-clock time (all records)", fmt_hms(wall), "measured: first to last transcript timestamp (%s -> %s UTC)" % (
            stamps[0].strftime("%Y-%m-%d %H:%M:%S") if stamps else "-", stamps[-1].strftime("%Y-%m-%d %H:%M:%S") if stamps else "-")),
        ("Active time (all records)", fmt_hms(active), "measured: wall-clock minus %d gaps > 10 min without any transcript record (%s); %d records predate the anchor (session opened earlier, request interrupted)" % (
            len(gaps), fmt_hms(sum(g[2] for g in gaps)), len(pre))),
        ("Uncached input tokens", "{:,}".format(tot["input"]), "measured: sum of usage.input_tokens"),
        ("Cache read tokens", "{:,}".format(tot["cache_read"]), "measured: sum of usage.cache_read_input_tokens"),
        ("Cache write tokens (5 min)", "{:,}".format(tot["cache_write_5m"] + max(unsplit, 0)), "measured: usage.cache_creation.ephemeral_5m_input_tokens (+ unsplit writes)"),
        ("Cache write tokens (1 h)", "{:,}".format(tot["cache_write_1h"]), "measured: usage.cache_creation.ephemeral_1h_input_tokens"),
        ("Output tokens (incl. thinking)", "{:,}".format(tot["output"]), "measured: sum of usage.output_tokens"),
        ("**Total tokens**", "**{:,}**".format(total_tokens), "measured: sum of the categories above"),
        ("Sessions / subagent transcripts / messages", "%d / %d / %d" % (len(sessions), subagents, len(msgs)),
         "measured: %d transcript files, assistant messages deduplicated by message id" % len(used)),
        ("Models", ", ".join("%s (%d)" % kv for kv in sorted(models.items())), "measured: message.model"),
    ]
    rows += lines_cost
    if not price:
        rows.append(("Total API-equivalent cost", "unavailable", "Tools/pricing.json missing; cost = input*p_in + cache_read*p_cr + w5*p_w5 + w1*p_w1 + output*p_out"))

    out = ["| Metric | Value | How obtained |", "|---|---|---|"]
    for r in rows:
        out.append("| %s | %s | %s |" % r)
    table = "\n".join(out)
    gap_lines = ["- %s -> %s UTC: %s" % (a.strftime("%Y-%m-%d %H:%M:%S"), b.strftime("%H:%M:%S"), fmt_hms(d)) for a, b, d in gaps]
    print(table)
    print()
    print("Gaps > 10 min:" if gap_lines else "Gaps > 10 min: none")
    for g in gap_lines:
        print(g)

    if "--json" in args:
        jp = args[args.index("--json") + 1]
        with open(jp, "w", encoding="utf-8") as f:
            json.dump({"tokens": tot, "total_tokens": total_tokens, "wall_s": wall, "active_s": active, "gaps": [(a.isoformat(), b.isoformat(), d) for a, b, d in gaps],
                       "cost_usd": cost, "sessions": len(sessions), "subagents": subagents, "messages": len(msgs), "models": models}, f, indent=2)
    if "--report" in args:
        rp = args[args.index("--report") + 1]
        txt = open(rp, "r", encoding="utf-8").read() if os.path.exists(rp) else ""
        a, b = "<!-- METRICS:BEGIN -->", "<!-- METRICS:END -->"
        block = a + "\n" + table + "\n\n" + ("Gaps > 10 min:\n" + "\n".join(gap_lines) if gap_lines else "Gaps > 10 min: none") + "\n" + b
        if a in txt and b in txt:
            txt = txt[: txt.index(a)] + block + txt[txt.index(b) + len(b):]
        else:
            txt = txt.rstrip() + "\n\n## Project metrics\n\n" + block + "\n"
        with open(rp, "w", encoding="utf-8") as f:
            f.write(txt)


if __name__ == "__main__":
    main()
