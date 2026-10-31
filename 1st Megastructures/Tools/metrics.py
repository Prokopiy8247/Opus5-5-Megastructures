"""Project metrics for Abyss (MASTER_PROMPT.md Part 15.3): total time, total tokens, API-equivalent cost in USD.

Usage (isolated mode, read-only access to the transcripts):
    python -I Tools/metrics.py [--update-report OPUS_5.5_FINAL_REPORT.md] [--json QA/metrics/project_metrics.json]
           [--projects-dir C:\\Users\\<user>\\.claude\\projects] [--match 1st-Megastructures]
           [--project-root <repo>] [--pricing Tools/pricing.json] [--gap-minutes 10] [--state STATE.md]
           [--since anchor|<ISO time>]

Method (exactly Part 15.3):
1. Transcripts: every folder under --projects-dir whose name contains --match ("1st-Megastructures"); every
   ``*.jsonl`` inside it, recursively (main sessions, ``<session>/subagents/**`` subagent transcripts, workflow
   transcripts and journals). A file is included only when every ``cwd`` field in it is the project root (or a folder
   below it); files with a foreign cwd are excluded and listed; files without any cwd (workflow journals) carry no
   usage and are only counted. Sibling projects (2nd / 3rd Megastructures) and the parent consulting folder
   (W--YouTube-Megastructures) never match and are listed as excluded.
2. Tokens: ``message.usage`` of every assistant record, deduplicated by ``message.id`` (fallback ``requestId``, then
   the record uuid), because a streamed message is written once per content block with identical usage. Categories:
   input_tokens (uncached input), cache_read_input_tokens, cache_creation_input_tokens split into 5-minute and 1-hour
   writes from ``usage.cache_creation`` (an unsplit remainder is counted as 5-minute and flagged), output_tokens
   (includes thinking; thinking is also shown separately from output_tokens_details). Synthetic records
   (model "<synthetic>") are not API calls and are skipped.
3. Time: wall-clock = first to last ``timestamp`` over all records of all included files; every gap > --gap-minutes
   without any record is listed; active time = wall-clock - sum(gaps). The run anchor "Run started at ..." from
   STATE.md is reported as an extra row.
4. Cost: per category with Tools/pricing.json (official page, URL and access date recorded there); usage.speed ==
   "fast" uses the fast-mode rows, usage.inference_geo == "us" applies the 1.1x data-residency multiplier, web search
   requests cost $10 / 1000. Opus 5.5 has no long-context surcharge (1M context at standard pricing).
5. Output: a "Metric | Value | How obtained" markdown table on stdout; --update-report replaces the
   "## Project metrics" section of the report (appends it when missing); --json writes every number.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import datetime as dt  # noqa: E402
import json  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402
from typing import Any, Dict, List, Optional, Tuple  # noqa: E402

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(TOOLS_DIR)
DEFAULT_PROJECTS = os.path.join(os.path.expanduser("~"), ".claude", "projects")

CATS = ("input", "cache_read", "cache_write_5m", "cache_write_1h", "output")
CAT_LABEL = {"input": "Uncached input tokens", "cache_read": "Cache read tokens",
             "cache_write_5m": "Cache write tokens (5 min)", "cache_write_1h": "Cache write tokens (1 h)",
             "output": "Output tokens (incl. thinking)"}
CAT_PRICE_KEY = {"input": "base_input", "cache_read": "cache_read", "cache_write_5m": "cache_write_5m",
                 "cache_write_1h": "cache_write_1h", "output": "output"}


# ----------------------------------------------------------------------------------------------------------------------
def parse_ts(s: Any) -> Optional[dt.datetime]:
    if not isinstance(s, str) or len(s) < 19:
        return None
    try:
        t = s.strip().replace("Z", "+00:00")
        d = dt.datetime.fromisoformat(t)
        if d.tzinfo is None:
            d = d.astimezone()
        return d.astimezone(dt.timezone.utc)
    except ValueError:
        return None


def hms(seconds: float) -> str:
    s = int(round(max(0.0, seconds)))
    return "%d:%02d:%02d" % (s // 3600, (s % 3600) // 60, s % 60)


def local(d: Optional[dt.datetime]) -> str:
    return d.astimezone().isoformat(timespec="seconds") if d else "-"


def norm_path(p: str) -> str:
    return os.path.normcase(os.path.normpath(p.strip().rstrip("\\/")))


def cwd_matches(cwd: str, root: str) -> bool:
    c, r = norm_path(cwd), norm_path(root)
    return c == r or c.startswith(r + os.sep)


def read_anchor(state_path: str) -> Optional[dt.datetime]:
    """'Run started at 2026-10-29 23:02:14 +01:00' (or ISO) from STATE.md."""
    if not state_path or not os.path.isfile(state_path):
        return None
    with open(state_path, "r", encoding="utf-8-sig", errors="replace") as f:
        text = f.read()
    m = re.search(r"Run started at\s+(\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2}:\d{2})(?:\.\d+)?\s*(Z|[+-]\d{2}:?\d{2})?", text)
    if not m:
        return None
    tz = m.group(3) or ""
    if tz and tz != "Z" and ":" not in tz:
        tz = tz[:3] + ":" + tz[3:]
    return parse_ts("%sT%s%s" % (m.group(1), m.group(2), tz))


# ----------------------------------------------------------------------------------------------------------------------
class Scan:
    def __init__(self) -> None:
        self.folders_included: List[str] = []
        self.folders_excluded: List[Tuple[str, str]] = []
        self.files_included: List[Dict[str, Any]] = []
        self.files_excluded: List[Tuple[str, str]] = []
        self.timestamps: List[dt.datetime] = []
        self.messages: Dict[str, Dict[str, Any]] = {}
        self.assistant_records = 0
        self.synthetic_records = 0
        self.bad_lines = 0
        self.before_since_records = 0


def classify(rel: str) -> str:
    parts = re.split(r"[\\/]+", rel)
    base = parts[-1].lower()
    if len(parts) == 1:
        return "main_session"
    if base == "journal.jsonl":
        return "workflow_journal"
    if "subagents" in parts:
        return "workflow_subagent" if "workflows" in parts else "subagent"
    return "other"


def scan(projects_dir: str, match: str, root: str, since: Optional[dt.datetime]) -> Scan:
    sc = Scan()
    if not os.path.isdir(projects_dir):
        raise SystemExit("projects dir not found: %s" % projects_dir)
    for name in sorted(os.listdir(projects_dir)):
        full = os.path.join(projects_dir, name)
        if not os.path.isdir(full):
            continue
        if match.lower() not in name.lower():
            if "megastructures" in name.lower():
                sc.folders_excluded.append((name, "name does not contain '%s'" % match))
            continue
        confirmed = 0
        for base, _dirs, files in os.walk(full):
            for fn in sorted(files):
                if not fn.lower().endswith(".jsonl"):
                    continue
                path = os.path.join(base, fn)
                rel = os.path.relpath(path, full)
                recs: List[Dict[str, Any]] = []
                cwds = set()
                with open(path, "r", encoding="utf-8", errors="replace") as f:
                    for line in f:
                        line = line.strip()
                        if not line:
                            continue
                        try:
                            o = json.loads(line)
                        except ValueError:
                            sc.bad_lines += 1      # e.g. a line still being written by a running session
                            continue
                        if not isinstance(o, dict):
                            continue
                        if isinstance(o.get("cwd"), str) and o["cwd"]:
                            cwds.add(o["cwd"])
                        recs.append(o)
                kind = classify(rel)
                has_usage = any(r.get("type") == "assistant" for r in recs)
                if cwds and not all(cwd_matches(c, root) for c in cwds):
                    sc.files_excluded.append((os.path.join(name, rel), "cwd outside project: %s" %
                                              ", ".join(sorted(c for c in cwds if not cwd_matches(c, root)))))
                    continue
                if not cwds and has_usage:
                    sc.files_excluded.append((os.path.join(name, rel), "assistant records without cwd (unconfirmed)"))
                    continue
                if cwds:
                    confirmed += 1
                sc.files_included.append({"file": os.path.join(name, rel), "kind": kind, "records": len(recs),
                                          "confirmed_by_cwd": bool(cwds)})
                for o in recs:
                    t = parse_ts(o.get("timestamp"))
                    if since is not None and t is not None and t < since:
                        sc.before_since_records += 1
                        continue
                    if t is not None:
                        sc.timestamps.append(t)
                    if o.get("type") != "assistant":
                        continue
                    msg = o.get("message") or {}
                    model = msg.get("model") or o.get("requestedModel") or "unknown"
                    usage = msg.get("usage")
                    if model == "<synthetic>" or not isinstance(usage, dict):
                        sc.synthetic_records += 1
                        continue
                    sc.assistant_records += 1
                    key = msg.get("id") or o.get("requestId") or ("%s#%s" % (path, o.get("uuid")))
                    prev = sc.messages.get(key)
                    out_tok = int(usage.get("output_tokens") or 0)
                    if prev is None or out_tok > int(prev["usage"].get("output_tokens") or 0):
                        sc.messages[key] = {"model": model, "usage": usage, "file": rel, "ts": t,
                                            "kind": kind}
        if confirmed:
            sc.folders_included.append(name)
        else:
            sc.folders_excluded.append((name, "no file confirmed by cwd"))
    return sc


def aggregate(sc: Scan) -> Dict[str, Any]:
    buckets: Dict[Tuple[str, str, str], Dict[str, int]] = {}
    totals = {c: 0 for c in CATS}
    extra = {"thinking": 0, "web_search_requests": 0, "web_fetch_requests": 0, "unsplit_cache_write_msgs": 0,
             "unsplit_cache_write_tokens": 0}
    per_kind: Dict[str, int] = {}
    for m in sc.messages.values():
        u = m["usage"]
        speed = str(u.get("speed") or "standard")
        geo = str(u.get("inference_geo") or "global")
        b = buckets.setdefault((m["model"], speed, geo), {c: 0 for c in CATS})
        cc = int(u.get("cache_creation_input_tokens") or 0)
        split = u.get("cache_creation") if isinstance(u.get("cache_creation"), dict) else None
        if split is not None:
            w5 = int(split.get("ephemeral_5m_input_tokens") or 0)
            w1 = int(split.get("ephemeral_1h_input_tokens") or 0)
            rem = cc - (w5 + w1)
            if rem > 0:
                w5 += rem
                extra["unsplit_cache_write_msgs"] += 1
                extra["unsplit_cache_write_tokens"] += rem
        else:
            w5, w1 = cc, 0
            if cc:
                extra["unsplit_cache_write_msgs"] += 1
                extra["unsplit_cache_write_tokens"] += cc
        vals = {"input": int(u.get("input_tokens") or 0), "cache_read": int(u.get("cache_read_input_tokens") or 0),
                "cache_write_5m": w5, "cache_write_1h": w1, "output": int(u.get("output_tokens") or 0)}
        for c in CATS:
            b[c] += vals[c]
            totals[c] += vals[c]
        det = u.get("output_tokens_details") or {}
        extra["thinking"] += int(det.get("thinking_tokens") or 0) if isinstance(det, dict) else 0
        stu = u.get("server_tool_use") or {}
        if isinstance(stu, dict):
            extra["web_search_requests"] += int(stu.get("web_search_requests") or 0)
            extra["web_fetch_requests"] += int(stu.get("web_fetch_requests") or 0)
        per_kind[m["kind"]] = per_kind.get(m["kind"], 0) + 1
    return {"buckets": buckets, "totals": totals, "extra": extra, "messages_per_kind": per_kind}


def cost(agg: Dict[str, Any], pricing: Dict[str, Any]) -> Dict[str, Any]:
    models = pricing.get("models") or {}
    mods = pricing.get("modifiers") or {}
    per_cat = {c: 0.0 for c in CATS}
    missing: List[str] = []
    notes: List[str] = []
    rates_used: Dict[str, Dict[str, float]] = {}
    for (model, speed, geo), b in agg["buckets"].items():
        row = models.get(model)
        if row is None:
            missing.append(model)
            continue
        rates = dict(row)
        if speed == "fast":
            fm = row.get("fast_mode")
            if not fm:
                missing.append("%s (fast mode)" % model)
                continue
            rates.update(fm)
            notes.append("fast-mode rates applied to %s" % model)
        mult = float(mods.get("inference_geo_us_multiplier", 1.1)) if geo == "us" else 1.0
        if mult != 1.0:
            notes.append("inference_geo us x%.2f applied to %s" % (mult, model))
        ok = True
        for c in CATS:
            r = rates.get(CAT_PRICE_KEY[c])
            if r is None:
                ok = False
                break
        if not ok:
            missing.append("%s (null price)" % model)
            continue
        for c in CATS:
            r = float(rates[CAT_PRICE_KEY[c]]) * mult
            per_cat[c] += b[c] / 1e6 * r
            rates_used.setdefault("%s/%s/%s" % (model, speed, geo), {})[c] = r
    ws = agg["extra"]["web_search_requests"] * float(mods.get("web_search_per_1000", 10.0) or 0.0) / 1000.0
    total = sum(per_cat.values()) + ws
    return {"per_category": per_cat, "web_search": ws, "total": total, "missing_models": sorted(set(missing)),
            "notes": sorted(set(notes)), "rates_used": rates_used, "available": not missing}


def gaps_and_active(ts: List[dt.datetime], gap_min: float) -> Dict[str, Any]:
    if not ts:
        return {"first": None, "last": None, "wall_s": 0.0, "gaps": [], "gap_s": 0.0, "active_s": 0.0}
    ts = sorted(ts)
    gaps = []
    for a, b in zip(ts, ts[1:]):
        d = (b - a).total_seconds()
        if d > gap_min * 60.0:
            gaps.append({"from": a, "to": b, "seconds": d})
    wall = (ts[-1] - ts[0]).total_seconds()
    g = sum(x["seconds"] for x in gaps)
    return {"first": ts[0], "last": ts[-1], "wall_s": wall, "gaps": gaps, "gap_s": g, "active_s": wall - g}


# ----------------------------------------------------------------------------------------------------------------------
def build_markdown(sc: Scan, agg: Dict[str, Any], cs: Dict[str, Any], tm: Dict[str, Any], anchor_tm: Optional[Dict],
                   anchor: Optional[dt.datetime], pricing: Dict[str, Any], args: argparse.Namespace) -> str:
    T = agg["totals"]
    total_tokens = sum(T.values())
    kinds: Dict[str, int] = {}
    for f in sc.files_included:
        kinds[f["kind"]] = kinds.get(f["kind"], 0) + 1
    wf_runs = set()
    for f in sc.files_included:
        m = re.search(r"[\\/](wf_[^\\/]+)[\\/]", f["file"])
        if m:
            wf_runs.add(m.group(1))
    models: Dict[str, int] = {}
    for m in sc.messages.values():
        models[m["model"]] = models.get(m["model"], 0) + 1
    src = pricing.get("source") or {}
    url, acc = src.get("url", "?"), src.get("accessed", "?")
    rates = {}
    for k, v in cs["rates_used"].items():
        rates = v
        break

    def r(c: str) -> str:
        return ("$%g/MTok" % rates[c]) if c in rates else "n/a"

    rows: List[List[str]] = []
    rows.append(["Model(s)", ", ".join("%s (%d msgs)" % kv for kv in sorted(models.items())) or "-",
                 "measured: message.model of the deduplicated assistant messages"])
    rows.append(["Transcript folders", ", ".join(sc.folders_included) or "-",
                 "measured: folders under %s whose name contains '%s', confirmed by the cwd field" % (
                     args.projects_dir, args.match)])
    rows.append(["Sessions / subagents / workflows",
                 "%d main session(s), %d subagent transcript(s) (%d in workflows), %d workflow run(s), "
                 "%d journal file(s)" % (kinds.get("main_session", 0),
                                         kinds.get("subagent", 0) + kinds.get("workflow_subagent", 0),
                                         kinds.get("workflow_subagent", 0), len(wf_runs),
                                         kinds.get("workflow_journal", 0)),
                 "measured: included .jsonl files by location (<session>.jsonl, <session>/subagents/**)"])
    rows.append(["Assistant messages", "%d (from %d assistant records)" % (len(sc.messages), sc.assistant_records),
                 "measured: deduplicated by message.id / requestId"])
    for c in CATS:
        extra = ""
        if c == "output":
            extra = " (thinking %s)" % format(agg["extra"]["thinking"], ",")
        rows.append([CAT_LABEL[c], format(T[c], ",") + extra, "measured: sum of usage.%s" % {
            "input": "input_tokens", "cache_read": "cache_read_input_tokens",
            "cache_write_5m": "cache_creation.ephemeral_5m_input_tokens",
            "cache_write_1h": "cache_creation.ephemeral_1h_input_tokens", "output": "output_tokens"}[c]])
    rows.append(["Total tokens", format(total_tokens, ","), "measured: sum of the five categories above"])
    rows.append(["Wall-clock time", "%s (%s to %s)" % (hms(tm["wall_s"]), local(tm["first"]), local(tm["last"])),
                 "measured: first to last timestamp over all included records"])
    rows.append(["Gaps > %g min" % args.gap_minutes, "%d gap(s), %s in total" % (len(tm["gaps"]), hms(tm["gap_s"])),
                 "measured: intervals between consecutive timestamps (all files merged) longer than %g min" %
                 args.gap_minutes])
    rows.append(["Active time", hms(tm["active_s"]), "measured: wall-clock minus the gaps"])
    if anchor is not None and anchor_tm is not None:
        rows.append(["Run anchor to last activity", "%s wall-clock, %s active (from %s)" % (
            hms(anchor_tm["wall_s"]), hms(anchor_tm["active_s"]), local(anchor)),
            "measured: 'Run started at' in STATE.md to the last timestamp; gaps after the anchor removed"])
    for c in CATS:
        rows.append(["Cost: " + CAT_LABEL[c].replace(" tokens", "").replace(" (incl. thinking)", ""),
                     "$%.2f" % cs["per_category"][c],
                     "estimated: %s x %s" % (format(T[c], ","), r(c))])
    if agg["extra"]["web_search_requests"]:
        rows.append(["Cost: web search", "$%.2f" % cs["web_search"],
                     "estimated: %d searches x $10/1000" % agg["extra"]["web_search_requests"]])
    total_txt = ("$%.2f" % cs["total"]) if cs["available"] else \
        "unavailable for %s ($%.2f for the priced part)" % (", ".join(cs["missing_models"]), cs["total"])
    rows.append(["Total API-equivalent cost", total_txt,
                 "estimated: sum of the cost rows; prices from %s (accessed %s)" % (url, acc)])

    lines = ["## Project metrics", "",
             "Generated by `python -I Tools/metrics.py` at %s. API-equivalent estimate at the official list prices "
             "for comparison, not the user's subscription bill." % dt.datetime.now().astimezone().isoformat(
                 timespec="seconds"), "",
             "| Metric | Value | How obtained |", "|---|---|---|"]
    for row in rows:
        lines.append("| " + " | ".join(x.replace("|", "\\|") for x in row) + " |")
    lines += ["", "### Gaps longer than %g minutes" % args.gap_minutes, ""]
    if tm["gaps"]:
        lines += ["| # | From | To | Duration |", "|---|---|---|---|"]
        for i, g in enumerate(tm["gaps"], 1):
            lines.append("| %d | %s | %s | %s |" % (i, local(g["from"]), local(g["to"]), hms(g["seconds"])))
    else:
        lines.append("None.")
    p = cs["per_category"]
    lines += ["", "### Cost formula", "",
              "cost = input x %s + cache read x %s + cache write 5m x %s + cache write 1h x %s + output x %s"
              "%s (per million tokens)" % (r("input"), r("cache_read"), r("cache_write_5m"), r("cache_write_1h"),
                                           r("output"), " + web searches x $10/1000" if cs["web_search"] else ""),
              "", "= $%.2f + $%.2f + $%.2f + $%.2f + $%.2f%s = **$%.2f**" % (
                  p["input"], p["cache_read"], p["cache_write_5m"], p["cache_write_1h"], p["output"],
                  (" + $%.2f" % cs["web_search"]) if cs["web_search"] else "", cs["total"]), ""]
    notes = ["Pricing: %s, accessed %s; Opus 5.5 has no long-context surcharge (1M context at standard pricing)." % (
        url, acc)]
    if cs["notes"]:
        notes.append("Modifiers applied: " + "; ".join(cs["notes"]) + ".")
    if agg["extra"]["unsplit_cache_write_msgs"]:
        notes.append("%d message(s) had cache writes without a 5m/1h split (%s tokens), counted as 5-minute writes "
                     "(assumption)." % (agg["extra"]["unsplit_cache_write_msgs"],
                                        format(agg["extra"]["unsplit_cache_write_tokens"], ",")))
    if sc.bad_lines:
        notes.append("%d unparsable line(s) skipped (lines still being written by running sessions)." % sc.bad_lines)
    if args.since:
        notes.append("Restricted to records at or after %s (--since); %d earlier record(s) ignored." % (
            args.since, sc.before_since_records))
    excl = ["%s (%s)" % e for e in sc.folders_excluded] + ["%s (%s)" % e for e in sc.files_excluded]
    if excl:
        notes.append("Excluded: " + "; ".join(excl) + ".")
    notes.append("Not counted: the tokens of this measurement turn after the scan and of the final chat message; "
                 "backend model calls of the WebFetch summariser and WebSearch tools, which do not appear in the "
                 "transcripts.")
    lines += ["### Notes", ""] + ["- " + n for n in notes] + [""]
    return "\n".join(lines)


def update_report(path: str, section: str) -> None:
    text = ""
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8-sig") as f:
            text = f.read()
    else:
        text = "# OPUS 5.5 Final Report\n\n"
    lines = text.splitlines(keepends=True)
    start = None
    for i, ln in enumerate(lines):
        if re.match(r"^##\s+Project metrics\s*$", ln.strip()):
            start = i
            break
    if start is None:
        new = text.rstrip("\n") + "\n\n" + section.rstrip("\n") + "\n"
    else:
        end = len(lines)
        for j in range(start + 1, len(lines)):
            if re.match(r"^##\s", lines[j]) and not lines[j].startswith("###"):
                end = j
                break
        new = "".join(lines[:start]) + section.rstrip("\n") + "\n" + ("\n" if end < len(lines) else "") + \
            "".join(lines[end:])
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new)


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    default_projects = DEFAULT_PROJECTS if os.path.isdir(DEFAULT_PROJECTS) else \
        os.path.join(os.path.expanduser("~"), ".claude", "projects")
    ap.add_argument("--projects-dir", default=default_projects)
    ap.add_argument("--match", default="1st-Megastructures")
    ap.add_argument("--project-root", default=PROJECT_ROOT, help="cwd that confirms a transcript")
    ap.add_argument("--pricing", default=os.path.join(TOOLS_DIR, "pricing.json"))
    ap.add_argument("--gap-minutes", type=float, default=10.0)
    ap.add_argument("--state", default=os.path.join(PROJECT_ROOT, "STATE.md"))
    ap.add_argument("--since", help="'anchor' (STATE.md run start) or an ISO time: ignore earlier records")
    ap.add_argument("--json", help="write all numbers to this JSON file")
    ap.add_argument("--update-report", help="replace the '## Project metrics' section of this markdown file")
    ap.add_argument("--quiet", action="store_true", help="do not print the table")
    args = ap.parse_args(argv)

    anchor = read_anchor(args.state)
    since = None
    if args.since:
        since = anchor if args.since == "anchor" else parse_ts(args.since)
        if since is None:
            raise SystemExit("cannot resolve --since %r" % args.since)
    with open(args.pricing, "r", encoding="utf-8") as f:
        pricing = json.load(f)
    sc = scan(args.projects_dir, args.match, args.project_root, since)
    agg = aggregate(sc)
    cs = cost(agg, pricing)
    tm = gaps_and_active(sc.timestamps, args.gap_minutes)
    anchor_tm = None
    if anchor is not None:
        after = [t for t in sc.timestamps if t >= anchor]
        anchor_tm = gaps_and_active([anchor] + after, args.gap_minutes) if after else None
    md = build_markdown(sc, agg, cs, tm, anchor_tm, anchor, pricing, args)
    if not args.quiet:
        if hasattr(sys.stdout, "reconfigure"):
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # Windows consoles default to cp1252
        print(md)
    if args.json:
        data = {
            "generated": dt.datetime.now().astimezone().isoformat(timespec="seconds"),
            "projects_dir": args.projects_dir, "match": args.match, "project_root": args.project_root,
            "folders_included": sc.folders_included, "folders_excluded": sc.folders_excluded,
            "files_included": sc.files_included, "files_excluded": sc.files_excluded,
            "assistant_records": sc.assistant_records, "messages": len(sc.messages),
            "messages_per_kind": agg["messages_per_kind"], "tokens": agg["totals"],
            "tokens_total": sum(agg["totals"].values()), "extra": agg["extra"],
            "buckets": [{"model": k[0], "speed": k[1], "inference_geo": k[2], **v} for k, v in agg["buckets"].items()],
            "cost_usd": cs, "pricing_source": pricing.get("source"),
            "time": {"first": local(tm["first"]), "last": local(tm["last"]), "wall": hms(tm["wall_s"]),
                     "wall_s": tm["wall_s"], "gaps": [{"from": local(g["from"]), "to": local(g["to"]),
                                                       "duration": hms(g["seconds"]), "seconds": g["seconds"]}
                                                      for g in tm["gaps"]],
                     "gap_s": tm["gap_s"], "active": hms(tm["active_s"]), "active_s": tm["active_s"],
                     "anchor": local(anchor) if anchor else None,
                     "anchor_wall": hms(anchor_tm["wall_s"]) if anchor_tm else None,
                     "anchor_active": hms(anchor_tm["active_s"]) if anchor_tm else None},
            "bad_lines": sc.bad_lines, "since": args.since,
        }
        os.makedirs(os.path.dirname(os.path.abspath(args.json)), exist_ok=True)
        with open(args.json, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, indent=2, default=str)
            f.write("\n")
    if args.update_report:
        update_report(os.path.abspath(args.update_report), md)
        print("updated '## Project metrics' in %s" % os.path.abspath(args.update_report), file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
