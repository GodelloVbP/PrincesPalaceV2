#!/usr/bin/env python3
"""
Token-usage baseline for the Claude Code project transcripts under
C:\\Users\\Godel\\.claude\\projects\\C--Games-Prince-s-Palace-v2\\

WHAT THIS MEASURES
  A read-only pass over this project's Claude Code transcripts: token usage
  by model/family, main-session vs subagent split, per-session and
  per-agent-launch cost proxies, Read/Grep call volume and an estimate of
  how many lines those Reads actually returned, which verification/build
  scripts actually got executed (vs merely mentioned in a command), and how
  often assistant records are spaced far enough apart that prompt caching
  would have expired (5 min for subagent transcripts, 1 h for main sessions
  on a subscription plan -- see the per-table caption for the source).

HOW TO RUN
    python tools/usage_baseline.py                  # last 14 days, default report path
    python tools/usage_baseline.py --days 30
    python tools/usage_baseline.py --out reports/usage/custom.md

  --days N     lookback window in days (default 14)
  --out PATH   output path; MUST resolve inside this repo's reports/ folder
               (default: reports/usage/<YYYY-MM-DD>.md, today's date). This
               script refuses to write anywhere else -- reports/ is
               gitignored and is the only place it ever touches.

DEDUP RULE (see also the "Caveats" section emitted at the end of the report)
  Assistant records are SSE-streamed: several JSONL lines can share the same
  requestId (== message.id in every case observed). Within such a group,
  input_tokens / cache_creation_input_tokens / cache_read_input_tokens are
  IDENTICAL on every line (fixed before streaming starts); output_tokens
  grows monotonically and only reaches its final value on the LAST line
  (verified over 1754 sampled multi-line groups: 0 non-monotonic sequences,
  last-line value == max value in 100% of groups). Dedup rule used here:
  group by requestId (fallback message.id, fallback a synthetic per-record
  key so nothing is silently dropped), take input/cache fields from any
  member of the group and output_tokens as the MAX across the group.

STRUCTURE DISCOVERED
  - Main/orchestrator sessions: "<session-uuid>.jsonl" directly under the
    project dir. Every line in these files has isSidechain == false
    (confirmed empirically: 0/72 sampled main files contained an
    isSidechain=true line).
  - Subagent transcripts: "<session-uuid>/subagents/agent-<id>.jsonl", one
    file per Agent-tool launch, plus a sibling
    "agent-<id>.meta.json" recording agentType/description/model/... at
    launch time. Every line in these files has isSidechain == true.

WRITE SCOPE
  This script only ever reads under the transcripts dir above (plus, for
  the Read-range estimate, `os.path.exists`/line-counts of files the
  transcripts themselves reference -- still read-only, never writes to
  them) and only ever WRITES the single markdown report under this repo's
  reports/ directory. It never edits, deletes or writes anything else.
"""

import argparse
import glob
import json
import math
import os
import re
import statistics
import sys
from collections import Counter, defaultdict
from datetime import datetime, timedelta, timezone

PROJECT_DIR = r"C:\Users\Godel\.claude\projects\C--Games-Prince-s-Palace-v2"
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPORTS_ROOT = os.path.join(REPO_ROOT, "reports")

NOW = datetime.now(timezone.utc)

# ---- pricing (per million tokens): input / cache-write / cache-read / output ----
# Source: claude-api skill's model table, cached 2026-06-24. The account this
# runs against is a subscription, not pay-per-token, so every dollar figure
# derived from these rates is a PROXY for relative cost, not a bill.
RATES = {
    "fable":  {"input": 10.0, "cache_write": 12.50, "cache_read": 0.25, "output": 50.0},
    "opus":   {"input": 5.0,  "cache_write": 6.25,  "cache_read": 0.50, "output": 25.0},
    "sonnet": {"input": 2.0,  "cache_write": 2.50,  "cache_read": 0.20, "output": 10.0},
    "haiku":  {"input": 1.0,  "cache_write": 1.25,  "cache_read": 0.10, "output": 5.0},
}

COST_PROXY_NOTE = (
    "_Approximate list-price equivalents; the account is on a subscription so this is a proxy, not a bill._"
)

SCRIPT_NAMES = [
    "run_tests_parallel.ps1",
    "test.ps1",
    "build_content.ps1",
    "preview.ps1",
    "screenshot.ps1",
]
MENTION_MARKERS = [
    "grep ", "select-string", "echo ", "cat ", "get-content", "type ", "sed ", "findstr",
]
NON_EXEC_FLAGS = ["-list", "-selfcheck"]


def classify_family(model):
    if not model:
        return "other"
    m = model.lower()
    if "fable" in m or "mythos" in m:
        return "fable"
    if "opus" in m:
        return "opus"
    if "sonnet" in m:
        return "sonnet"
    if "haiku" in m:
        return "haiku"
    return "other"


def cost_proxy(family, input_tok, cache_write_tok, cache_read_tok, output_tok):
    r = RATES.get(family)
    if r is None:
        return None
    return (
        input_tok * r["input"]
        + cache_write_tok * r["cache_write"]
        + cache_read_tok * r["cache_read"]
        + output_tok * r["output"]
    ) / 1_000_000.0


def parse_ts(ts):
    if not ts:
        return None
    try:
        # handles "...Z" and "...+00:00" forms under py3.11+
        return datetime.fromisoformat(ts.replace("Z", "+00:00"))
    except Exception:
        return None


def new_bucket():
    return {"input": 0, "cache_creation": 0, "cache_read": 0, "output": 0, "records": 0}


def add_bucket(b, input_tok, cache_creation, cache_read, output_tok, n=1):
    b["input"] += input_tok
    b["cache_creation"] += cache_creation
    b["cache_read"] += cache_read
    b["output"] += output_tok
    b["records"] += n


def bucket_total(b):
    return b["input"] + b["cache_creation"] + b["cache_read"] + b["output"]


def percentile(sorted_vals, pct):
    """pct in [0,1]. sorted_vals must already be sorted ascending."""
    if not sorted_vals:
        return 0
    if len(sorted_vals) == 1:
        return sorted_vals[0]
    k = (len(sorted_vals) - 1) * pct
    f = math.floor(k)
    c = math.ceil(k)
    if f == c:
        return sorted_vals[int(k)]
    d0 = sorted_vals[f] * (c - k)
    d1 = sorted_vals[c] * (k - f)
    return d0 + d1


def get_capped_line_count(path, cache, cap=2000):
    """Mirrors the Read tool's default: returns min(actual line count, cap),
    or None if the file can't be opened. Cached since paths repeat heavily."""
    if path in cache:
        return cache[path]
    try:
        n = 0
        with open(path, encoding="utf-8", errors="replace") as f:
            for _ in f:
                n += 1
                if n >= cap:
                    cache[path] = cap
                    return cap
        cache[path] = n
        return n
    except Exception:
        cache[path] = None
        return None


def read_subagent_meta(jsonl_path):
    meta_path = os.path.splitext(jsonl_path)[0] + ".meta.json"
    try:
        with open(meta_path, encoding="utf-8", errors="replace") as f:
            d = json.load(f)
        if isinstance(d, dict):
            return d
    except Exception:
        pass
    return {}


def extract_first_text(content):
    """content is either a string or a list of content blocks. Returns text or ''."""
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        for block in content:
            if isinstance(block, dict) and block.get("type") == "text":
                t = block.get("text")
                if isinstance(t, str) and t.strip():
                    return t
    return ""


def classify_script_command(cmd, stats, day, scope):
    """Distinguish an actual EXECUTION of one of the five tools/*.ps1 scripts
    from a command that merely mentions the name (grep/cat/echo/etc, or a
    -List/-SelfCheck invocation that runs but isn't a real test/build pass)."""
    if not cmd:
        return
    cmd_l = cmd.lower()
    for name in SCRIPT_NAMES:
        name_l = name.lower()
        if name_l not in cmd_l:
            continue
        is_mention_marker = any(m in cmd_l for m in MENTION_MARKERS)
        has_nonexec_flag = any(flag in cmd_l for flag in NON_EXEC_FLAGS)
        invoked = False
        if re.search(r"-file\s+\S*" + re.escape(name_l), cmd_l):
            invoked = True
        if re.search(r"(^|[\s&\"'])\.{0,2}[\\/]?tools[\\/]" + re.escape(name_l), cmd_l):
            invoked = True
        if invoked and not is_mention_marker and not has_nonexec_flag:
            stats.script_exec[name][scope] += 1
            if day:
                stats.script_exec_by_day[day][name][scope] += 1
        else:
            stats.script_mention[name][scope] += 1


class Stats:
    def __init__(self):
        self.malformed_lines = 0
        self.total_lines = 0
        self.assistant_lines_seen = 0
        self.assistant_lines_deduped = 0

        # item 2 (original): per raw model, last-N-days totals
        self.model_totals = defaultdict(new_bucket)

        # item 3 (original): main vs subagent totals
        self.scope_totals = defaultdict(new_bucket)  # 'main' / 'subagent'
        self.subagent_model_totals = defaultdict(new_bucket)  # actual message.model used inside subagent transcripts

        # item 3 (original): Agent tool_use invocations found in MAIN sessions (dedup by (file,id))
        self.agent_tool_model_counts = Counter()  # 'opus'/'sonnet'/'haiku'/'unspecified'/other literal
        self.agent_tool_subagent_type_counts = Counter()
        self.agent_tool_prompt_lens = []  # all prompt char lengths
        self.agent_tool_prompt_lens_by_type = defaultdict(list)
        self.subagent_launches_per_day = Counter()  # date -> count, from Agent tool_use blocks in main sessions

        # item 4 (original): per main-session-file stats, keyed by session uuid
        self.session_stats = {}
        self.session_agent_launches = Counter()  # uuid -> count of Agent tool_use blocks in that main file
        self.session_first_user_msg = {}  # uuid -> label text

        # item 5 (original): Read/Grep/Glob counts by scope, and Read file path frequency
        self.tool_counts_by_scope = defaultdict(Counter)  # scope -> Counter(tool_name)
        self.read_path_counts = Counter()

        # item 6 (original): verification reruns per day (loose substring match, kept as-is)
        self.test_rerun_per_day = Counter()

        # coordinator addendum: day -> family -> total tokens (sum of 4 classes)
        self.day_family_totals = defaultdict(lambda: defaultdict(int))

        # ---- additions ----

        # addition 1: per-subagent-file stats, keyed by jsonl path; rolled up onto
        # the owning main session by parent uuid at report-build time.
        self.subagent_file_stats = {}

        # addition 3: Read line estimate and Grep mode counts
        self.read_line_sum = Counter()       # file_path -> summed estimated lines returned
        self.read_unknown_count = Counter()  # file_path -> # of Read calls we could not estimate
        self.line_count_cache = {}
        self.grep_mode_by_scope = defaultdict(Counter)  # scope -> Counter(output_mode)

        # addition 4: script execution vs mention, per script/day/scope
        self.script_exec = defaultdict(lambda: defaultdict(int))            # name -> scope -> count
        self.script_mention = defaultdict(lambda: defaultdict(int))         # name -> scope -> count
        self.script_exec_by_day = defaultdict(lambda: defaultdict(lambda: defaultdict(int)))  # day -> name -> scope -> count

        # addition 5: cache-TTL gap tracking, per scope/family
        self.cache_ttl_gap_count = defaultdict(lambda: defaultdict(int))   # scope -> family -> count
        self.cache_ttl_gap_tokens = defaultdict(lambda: defaultdict(int))  # scope -> family -> cache_creation tokens

        # bookkeeping / caveats
        self.files_processed = 0
        self.files_skipped_by_mtime = 0
        self.mismatched_sidechain_main = 0
        self.mismatched_sidechain_sub = 0
        self.records_outside_window_skipped = 0
        self.dedup_groups_with_input_variance = 0  # sanity check counter, should stay 0


def list_target_files(window_start):
    """Return (main_files, subagent_files, skipped) filtered by mtime >= window_start (perf pre-filter)."""
    main_files = []
    subagent_files = []
    skipped = 0

    for path in glob.glob(os.path.join(PROJECT_DIR, "*.jsonl")):
        mtime = datetime.fromtimestamp(os.path.getmtime(path), tz=timezone.utc)
        if mtime >= window_start:
            main_files.append(path)
        else:
            skipped += 1

    for path in glob.glob(os.path.join(PROJECT_DIR, "*", "subagents", "*.jsonl")):
        mtime = datetime.fromtimestamp(os.path.getmtime(path), tz=timezone.utc)
        if mtime >= window_start:
            subagent_files.append(path)
        else:
            skipped += 1

    return main_files, subagent_files, skipped


def process_assistant_groups(records_by_reqid):
    """records_by_reqid: dict[key] -> list of (usage_dict, model, stop_reason, timestamp_dt)
    Returns list of (model, input, cache_creation, cache_read, output, timestamp_dt, variance) - one per group."""
    out = []
    for key, items in records_by_reqid.items():
        if not items:
            continue
        input_vals = set()
        cachec_vals = set()
        cacher_vals = set()
        best_output = -1
        best_ts = None
        model = None
        for usage, m, stop_reason, ts in items:
            it = usage.get("input_tokens") or 0
            cc = usage.get("cache_creation_input_tokens") or 0
            cr = usage.get("cache_read_input_tokens") or 0
            ot = usage.get("output_tokens") or 0
            input_vals.add(it)
            cachec_vals.add(cc)
            cacher_vals.add(cr)
            if ot >= best_output:
                best_output = ot
                best_ts = ts
            if model is None:
                model = m
        variance = len(input_vals) > 1 or len(cachec_vals) > 1 or len(cacher_vals) > 1
        it = next(iter(input_vals))
        cc = next(iter(cachec_vals))
        cr = next(iter(cacher_vals))
        ot = max(best_output, 0)
        out.append((model, it, cc, cr, ot, best_ts, variance))
    return out


def process_file(path, scope, stats: Stats, window_start):
    """scope: 'main' or 'subagent'. Single streaming pass; builds per-requestId groups
    for assistant usage, and separately scans tool_use blocks (Agent/Read/Grep/Glob/
    Bash/PowerShell) deduped by tool_use id within this file."""

    reqid_groups = defaultdict(list)  # key -> list of (usage, model, stop_reason, ts)
    seen_tool_ids = set()
    sidechain_true = 0
    sidechain_false = 0
    fallback_counter = 0

    session_uuid = None
    if scope == "main":
        session_uuid = os.path.splitext(os.path.basename(path))[0]
    else:
        # <uuid>/subagents/agent-<id>.jsonl
        session_uuid = os.path.basename(os.path.dirname(os.path.dirname(path)))

    first_msg_checked = 0

    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            stats.total_lines += 1
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except Exception:
                stats.malformed_lines += 1
                continue

            if not isinstance(d, dict):
                stats.malformed_lines += 1
                continue

            is_side = d.get("isSidechain")
            if is_side is True:
                sidechain_true += 1
            elif is_side is False:
                sidechain_false += 1

            ts = parse_ts(d.get("timestamp"))

            rtype = d.get("type")

            if scope == "main" and rtype == "user" and first_msg_checked < 5 and session_uuid not in stats.session_first_user_msg:
                msg = d.get("message") or {}
                text = extract_first_text(msg.get("content"))
                first_msg_checked += 1
                if text.strip():
                    first_line = text.strip().splitlines()[0].strip()
                    stats.session_first_user_msg[session_uuid] = first_line[:80]

            if rtype == "assistant":
                stats.assistant_lines_seen += 1
                msg = d.get("message") or {}
                usage = msg.get("usage")
                model = msg.get("model")
                if usage:
                    rid = d.get("requestId") or msg.get("id")
                    if not rid:
                        fallback_counter += 1
                        rid = ("__fallback__", d.get("uuid"), fallback_counter)
                    reqid_groups[rid].append((usage, model, msg.get("stop_reason"), ts))

                # tool_use blocks (Agent / Read / Grep / Glob / Bash / PowerShell)
                # Dedup by tool_use id unconditionally (correctness of "seen" bookkeeping),
                # but only fold into any windowed aggregate when this record's own timestamp
                # is inside the window -- a file can pass the mtime pre-filter while still
                # containing much older lines (long-lived main sessions).
                in_window = ts is not None and window_start <= ts <= NOW
                content = msg.get("content")
                if isinstance(content, list):
                    for block in content:
                        if not (isinstance(block, dict) and block.get("type") == "tool_use"):
                            continue
                        tid = block.get("id")
                        if tid is not None:
                            if tid in seen_tool_ids:
                                continue
                            seen_tool_ids.add(tid)
                        if not in_window:
                            continue
                        name = block.get("name")
                        inp = block.get("input") or {}

                        if name in ("Read", "Grep", "Glob"):
                            stats.tool_counts_by_scope[scope][name] += 1
                            if name == "Read":
                                fp = inp.get("file_path")
                                if fp:
                                    stats.read_path_counts[fp] += 1
                                    limit = inp.get("limit")
                                    if isinstance(limit, (int, float)) and limit:
                                        stats.read_line_sum[fp] += int(limit)
                                    else:
                                        lc = get_capped_line_count(fp, stats.line_count_cache)
                                        if lc is None:
                                            stats.read_unknown_count[fp] += 1
                                        else:
                                            stats.read_line_sum[fp] += lc
                            elif name == "Grep":
                                mode = inp.get("output_mode") or "files_with_matches"
                                stats.grep_mode_by_scope[scope][mode] += 1

                        elif name in ("Bash", "PowerShell"):
                            cmd = inp.get("command") or ""
                            day = ts.date().isoformat() if ts else None
                            if "run_tests_parallel.ps1" in cmd or "tools/test.ps1" in cmd:
                                if day:
                                    stats.test_rerun_per_day[day] += 1
                            classify_script_command(cmd, stats, day, scope)

                        elif name == "Agent" and scope == "main":
                            model_req = inp.get("model") or "unspecified"
                            subtype = inp.get("subagent_type") or "unspecified"
                            prompt = inp.get("prompt") or ""
                            stats.agent_tool_model_counts[model_req] += 1
                            stats.agent_tool_subagent_type_counts[subtype] += 1
                            stats.agent_tool_prompt_lens.append(len(prompt))
                            stats.agent_tool_prompt_lens_by_type[subtype].append(len(prompt))
                            stats.session_agent_launches[session_uuid] += 1
                            day = ts.date().isoformat() if ts else None
                            if day:
                                stats.subagent_launches_per_day[day] += 1

    if scope == "main":
        stats.mismatched_sidechain_main += sidechain_true
    else:
        stats.mismatched_sidechain_sub += sidechain_false

    # finalize dedup groups for this file, applying the windowed record-level filter
    groups = process_assistant_groups(reqid_groups)
    stats.assistant_lines_deduped += len(groups)

    turns = 0
    peak_context = 0
    context_sum = 0
    cache_read_total = 0
    cache_write_total = 0
    output_total = 0
    all_total = 0
    cost_total = 0.0
    has_unrated = False
    sum_input = sum_cc = sum_cr = sum_out = 0
    kept_for_gap_check = []  # (model, cc, best_ts)

    for model, it, cc, cr, ot, best_ts, variance in groups:
        if variance:
            stats.dedup_groups_with_input_variance += 1
        if best_ts is None or best_ts < window_start or best_ts > NOW:
            stats.records_outside_window_skipped += 1
            continue

        family = classify_family(model)
        cost = cost_proxy(family, it, cc, cr, ot)

        add_bucket(stats.model_totals[model or "unknown"], it, cc, cr, ot)
        add_bucket(stats.scope_totals[scope], it, cc, cr, ot)
        if scope == "subagent":
            add_bucket(stats.subagent_model_totals[model or "unknown"], it, cc, cr, ot)

        day = best_ts.date().isoformat()
        stats.day_family_totals[day][family] += it + cc + cr + ot

        turns += 1
        ctx = it + cc + cr
        peak_context = max(peak_context, ctx)
        context_sum += ctx
        cache_read_total += cr
        cache_write_total += cc
        output_total += ot
        all_total += it + cc + cr + ot
        sum_input += it
        sum_cc += cc
        sum_cr += cr
        sum_out += ot
        if cost is None:
            has_unrated = True
        else:
            cost_total += cost

        kept_for_gap_check.append((model, cc, best_ts))

    if scope == "main":
        stats.session_stats[session_uuid] = {
            "file_label": os.path.basename(path),
            "turns": turns,
            "peak_context": peak_context,
            "cache_read_total": cache_read_total,
            "all_total": all_total,
            "cache_read_ratio": (cache_read_total / all_total) if all_total else 0.0,
            "cost_proxy": cost_total,
            "cost_has_unrated": has_unrated,
            "input": sum_input,
            "cache_creation": sum_cc,
            "cache_read": sum_cr,
            "output": sum_out,
        }
    else:
        meta = read_subagent_meta(path)
        launch_type = meta.get("agentType") or meta.get("subagent_type") or meta.get("type") or "unknown"
        launch_model = meta.get("model") or "unspecified"
        brief_src = meta.get("description") or meta.get("prompt") or ""
        stats.subagent_file_stats[path] = {
            "parent_uuid": session_uuid,
            "launch_type": launch_type,
            "launch_model": launch_model,
            "brief": str(brief_src)[:60],
            "turns": turns,
            "peak_context": peak_context,
            "mean_context": (context_sum / turns) if turns else 0.0,
            "cache_read_total": cache_read_total,
            "cache_write_total": cache_write_total,
            "output_total": output_total,
            "cost_proxy": cost_total,
            "cost_has_unrated": has_unrated,
            "input": sum_input,
            "cache_creation": sum_cc,
            "cache_read": sum_cr,
            "output": sum_out,
        }

    # addition 5: cache-lifetime gaps, computed per file in chronological order
    threshold = timedelta(minutes=5) if scope == "subagent" else timedelta(minutes=60)
    kept_for_gap_check.sort(key=lambda t: t[2])
    prev_ts = None
    for model, cc, best_ts in kept_for_gap_check:
        if prev_ts is not None and (best_ts - prev_ts) > threshold:
            family = classify_family(model)
            stats.cache_ttl_gap_count[scope][family] += 1
            stats.cache_ttl_gap_tokens[scope][family] += cc
        prev_ts = best_ts


def fmt_int(n):
    return f"{n:,}"


def fmt_cost(c):
    if c is None:
        return "n/a"
    return f"${c:,.2f}"


def build_markdown(stats: Stats, main_files, subagent_files, files_skipped_mtime, window_start, days):
    lines = []
    lines.append("# Token-usage baseline")
    lines.append("")
    lines.append(f"Generated {NOW.isoformat()}Z-ish (local run). Window: last {days} days")
    lines.append(f"({window_start.date().isoformat()} to {NOW.date().isoformat()}), "
                  f"files gated by mtime, records gated by their own `timestamp`.")
    lines.append("")
    lines.append(f"Main session files in window: {len(main_files)}. "
                  f"Subagent transcript files in window: {len(subagent_files)}. "
                  f"Files excluded by mtime pre-filter: {files_skipped_mtime}.")
    lines.append("")

    # ---- 1. dedup rule note is in the caveats section at the end ----

    # ---- 2. totals per model, last N days, with cost proxy ----
    lines.append("## Totals per model (last {0} days)".format(days))
    lines.append("")
    lines.append("| Model | Family | Input | Cache-write | Cache-read | Output | Cost proxy |")
    lines.append("|---|---|---:|---:|---:|---:|---:|")
    grand = new_bucket()
    grand_cost = 0.0
    any_unrated = []
    for model, b in sorted(stats.model_totals.items(), key=lambda kv: -bucket_total(kv[1])):
        family = classify_family(model)
        c = cost_proxy(family, b["input"], b["cache_creation"], b["cache_read"], b["output"])
        if c is None:
            any_unrated.append(model)
        else:
            grand_cost += c
        for k in ("input", "cache_creation", "cache_read", "output"):
            grand[k] += b[k]
        lines.append(f"| {model} | {family} | {fmt_int(b['input'])} | {fmt_int(b['cache_creation'])} | "
                      f"{fmt_int(b['cache_read'])} | {fmt_int(b['output'])} | {fmt_cost(c)} |")
    lines.append(f"| **TOTAL** |  | {fmt_int(grand['input'])} | {fmt_int(grand['cache_creation'])} | "
                  f"{fmt_int(grand['cache_read'])} | {fmt_int(grand['output'])} | {fmt_cost(grand_cost)} |")
    lines.append("")
    lines.append(COST_PROXY_NOTE)
    if any_unrated:
        lines.append(f"_Models with no rate bucket (cost shown as n/a, excluded from TOTAL cost): {', '.join(any_unrated)}_")
    lines.append("")

    # ---- coordinator addendum: per-day fable vs non-fable totals ----
    lines.append("## Per-day totals: Fable-family vs non-Fable (all four token classes summed)")
    lines.append("")
    lines.append("_Requested because the account's binding limit is a weekly all-models cap._")
    lines.append("")
    lines.append("| Day | Fable-family tokens | Non-Fable tokens | Total |")
    lines.append("|---|---:|---:|---:|")
    for day in sorted(stats.day_family_totals.keys()):
        fam = stats.day_family_totals[day]
        fable_tok = fam.get("fable", 0)
        non_fable = sum(v for k, v in fam.items() if k != "fable")
        lines.append(f"| {day} | {fmt_int(fable_tok)} | {fmt_int(non_fable)} | {fmt_int(fable_tok+non_fable)} |")
    lines.append("")

    # ---- 3. main vs subagent split ----
    lines.append("## Main-session vs subagent usage")
    lines.append("")
    lines.append("| Scope | Input | Cache-write | Cache-read | Output | Assistant records (deduped) |")
    lines.append("|---|---:|---:|---:|---:|---:|")
    for scope in ("main", "subagent"):
        b = stats.scope_totals.get(scope, new_bucket())
        lines.append(f"| {scope} | {fmt_int(b['input'])} | {fmt_int(b['cache_creation'])} | "
                      f"{fmt_int(b['cache_read'])} | {fmt_int(b['output'])} | {fmt_int(b['records'])} |")
    lines.append("")

    lines.append("### Which model did subagents actually run on (message.model inside subagent transcripts)")
    lines.append("")
    lines.append("| Model | Input | Cache-write | Cache-read | Output |")
    lines.append("|---|---:|---:|---:|---:|")
    for model, b in sorted(stats.subagent_model_totals.items(), key=lambda kv: -bucket_total(kv[1])):
        lines.append(f"| {model} | {fmt_int(b['input'])} | {fmt_int(b['cache_creation'])} | "
                      f"{fmt_int(b['cache_read'])} | {fmt_int(b['output'])} |")
    lines.append("")

    lines.append("### Agent-tool invocations in main sessions, by requested `model` input (dedup by tool_use id)")
    lines.append("")
    lines.append("| Requested model | Count |")
    lines.append("|---|---:|")
    for model, cnt in stats.agent_tool_model_counts.most_common():
        lines.append(f"| {model} | {cnt} |")
    total_agent_calls = sum(stats.agent_tool_model_counts.values())
    lines.append(f"| **TOTAL Agent-tool calls** | {total_agent_calls} |")
    lines.append("")

    lines.append("### Agent-tool invocations by `subagent_type`, with prompt length (chars)")
    lines.append("")
    lines.append("| subagent_type | Count | Mean prompt chars | Median prompt chars |")
    lines.append("|---|---:|---:|---:|")
    for subtype, cnt in stats.agent_tool_subagent_type_counts.most_common():
        lens = stats.agent_tool_prompt_lens_by_type[subtype]
        mean_l = statistics.mean(lens) if lens else 0
        med_l = statistics.median(lens) if lens else 0
        lines.append(f"| {subtype} | {cnt} | {mean_l:,.0f} | {med_l:,.0f} |")
    if stats.agent_tool_prompt_lens:
        lines.append(f"| **ALL** | {len(stats.agent_tool_prompt_lens)} | "
                      f"{statistics.mean(stats.agent_tool_prompt_lens):,.0f} | "
                      f"{statistics.median(stats.agent_tool_prompt_lens):,.0f} |")
    lines.append("")

    lines.append("### Subagent launches per day (Agent tool_use blocks in main sessions, by record timestamp)")
    lines.append("")
    lines.append("| Day | Launches |")
    lines.append("|---|---:|")
    for day in sorted(stats.subagent_launches_per_day.keys()):
        lines.append(f"| {day} | {stats.subagent_launches_per_day[day]} |")
    lines.append("")

    # ---- 4 (original). per-session stats, top 10 by cost proxy ----
    lines.append("## Per main-session stats (last {0} days), top 10 by cost proxy".format(days))
    lines.append("")
    lines.append("| Session file | Assistant turns | Peak context (tokens) | Total cache-read | Cache-read ratio | Cost proxy |")
    lines.append("|---|---:|---:|---:|---:|---:|")
    ranked = sorted(stats.session_stats.items(), key=lambda kv: -kv[1]["cost_proxy"])[:10]
    for uuid, s in ranked:
        unrated = " (some records unrated)" if s["cost_has_unrated"] else ""
        lines.append(f"| {s['file_label']} | {s['turns']} | {fmt_int(s['peak_context'])} | {fmt_int(s['cache_read_total'])} | "
                      f"{s['cache_read_ratio']:.1%} | {fmt_cost(s['cost_proxy'])}{unrated} |")
    lines.append("")
    lines.append(f"_{len(stats.session_stats)} main sessions had at least one in-window assistant record._")
    lines.append("")
    lines.append(COST_PROXY_NOTE)
    lines.append("")

    # ---- 5 (original). Read/Grep/Glob counts + top 15 read paths ----
    lines.append("## Read / Grep / Glob tool_use calls: main vs subagent")
    lines.append("")
    lines.append("| Tool | Main | Subagent | Total |")
    lines.append("|---|---:|---:|---:|")
    for tool in ("Read", "Grep", "Glob"):
        m = stats.tool_counts_by_scope["main"][tool]
        s = stats.tool_counts_by_scope["subagent"][tool]
        lines.append(f"| {tool} | {m} | {s} | {m+s} |")
    lines.append("")

    lines.append("### 15 most frequently Read file paths (main + subagent combined)")
    lines.append("")
    lines.append("| # | File path | Reads |")
    lines.append("|---:|---|---:|")
    for i, (path, cnt) in enumerate(stats.read_path_counts.most_common(15), 1):
        lines.append(f"| {i} | {path} | {cnt} |")
    lines.append("")

    # ---- 6 (original). verification reruns per day (loose match, kept as-is) ----
    lines.append("## Verification reruns per day (Bash/PowerShell command contains `run_tests_parallel.ps1` or `tools/test.ps1`)")
    lines.append("")
    lines.append("| Day | Reruns |")
    lines.append("|---|---:|")
    for day in sorted(stats.test_rerun_per_day.keys()):
        lines.append(f"| {day} | {stats.test_rerun_per_day[day]} |")
    total_reruns = sum(stats.test_rerun_per_day.values())
    lines.append(f"| **TOTAL** | {total_reruns} |")
    lines.append("")

    # =====================================================================
    # ADDITIONS
    # =====================================================================

    # ---- addition 1a: per main session, own stats + Agent launches + label ----
    lines.append("## Addition 1a: per main session (own tokens only, not rolled up)")
    lines.append("")
    lines.append("_Proxy for \"per completed task\" at the orchestrator level -- excludes whatever its subagents spent; see 1b for the rolled-up view._")
    lines.append("")
    lines.append("| Session | Turns | Input | Cache-write | Cache-read | Output | Cost proxy | Agent launches | First user message |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---:|---|")
    session_rows = sorted(stats.session_stats.items(), key=lambda kv: -kv[1]["cost_proxy"])
    for uuid, s in session_rows:
        label = stats.session_first_user_msg.get(uuid, "(no text found)")
        launches = stats.session_agent_launches.get(uuid, 0)
        unrated = " *" if s["cost_has_unrated"] else ""
        lines.append(f"| {s['file_label']} | {s['turns']} | {fmt_int(s['input'])} | {fmt_int(s['cache_creation'])} | "
                      f"{fmt_int(s['cache_read'])} | {fmt_int(s['output'])} | {fmt_cost(s['cost_proxy'])}{unrated} | "
                      f"{launches} | {label} |")
    lines.append("")
    lines.append("_`*` = some records in this session used a model with no rate bucket; cost proxy for those is excluded._")
    lines.append(COST_PROXY_NOTE)
    lines.append("")

    # ---- addition 1b: per main session rolled up with its subagents ----
    lines.append("## Addition 1b: per main session rolled up with its subagents (parent + workers)")
    lines.append("")
    lines.append("_This is the \"per completed task\" proxy: everything spent to produce one orchestrator session's worth of work, including every subagent it launched._")
    lines.append("")
    rolled = defaultdict(lambda: {"turns": 0, "input": 0, "cache_creation": 0, "cache_read": 0, "output": 0, "cost": 0.0, "launches_seen": 0})
    for path, s in stats.subagent_file_stats.items():
        r = rolled[s["parent_uuid"]]
        r["turns"] += s["turns"]
        r["input"] += s["input"]
        r["cache_creation"] += s["cache_creation"]
        r["cache_read"] += s["cache_read"]
        r["output"] += s["output"]
        r["cost"] += s["cost_proxy"]
        r["launches_seen"] += 1
    lines.append("| Session | Turns (incl. subagents) | Input | Cache-write | Cache-read | Output | Cost proxy (incl. subagents) | Agent launches | Subagent transcripts found | First user message |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|")
    combined_rows = []
    for uuid, s in stats.session_stats.items():
        r = rolled.get(uuid, {"turns": 0, "input": 0, "cache_creation": 0, "cache_read": 0, "output": 0, "cost": 0.0, "launches_seen": 0})
        combined_cost = s["cost_proxy"] + r["cost"]
        combined_rows.append((uuid, s, r, combined_cost))
    combined_rows.sort(key=lambda t: -t[3])
    for uuid, s, r, combined_cost in combined_rows:
        label = stats.session_first_user_msg.get(uuid, "(no text found)")
        launches = stats.session_agent_launches.get(uuid, 0)
        lines.append(f"| {s['file_label']} | {s['turns']+r['turns']} | {fmt_int(s['input']+r['input'])} | "
                      f"{fmt_int(s['cache_creation']+r['cache_creation'])} | {fmt_int(s['cache_read']+r['cache_read'])} | "
                      f"{fmt_int(s['output']+r['output'])} | {fmt_cost(combined_cost)} | {launches} | {r['launches_seen']} | {label} |")
    lines.append("")
    lines.append("_\"Agent launches\" (from tool_use blocks) and \"Subagent transcripts found\" (actual .jsonl files under this session's subagents/ dir) "
                  "can differ -- a launch with no matching transcript file, or a transcript outside the lookback window, will show up as a gap._")
    lines.append(COST_PROXY_NOTE)
    lines.append("")

    # ---- addition 2: per Agent launch ----
    lines.append("## Addition 2: per Agent launch (one row per subagent transcript), top 25 by cost proxy")
    lines.append("")
    lines.append("| Type | Model | Turns | Peak context | Mean context/turn | Cache-read | Cache-write | Output | Cost proxy | Brief |")
    lines.append("|---|---|---:|---:|---:|---:|---:|---:|---:|---|")
    launch_rows = sorted(stats.subagent_file_stats.values(), key=lambda s: -s["cost_proxy"])
    for s in launch_rows[:25]:
        unrated = " *" if s["cost_has_unrated"] else ""
        lines.append(f"| {s['launch_type']} | {s['launch_model']} | {s['turns']} | {fmt_int(s['peak_context'])} | "
                      f"{s['mean_context']:,.0f} | {fmt_int(s['cache_read_total'])} | {fmt_int(s['cache_write_total'])} | "
                      f"{fmt_int(s['output_total'])} | {fmt_cost(s['cost_proxy'])}{unrated} | {s['brief']} |")
    lines.append("")
    all_turns = sorted(s["turns"] for s in stats.subagent_file_stats.values())
    all_mean_ctx = sorted(s["mean_context"] for s in stats.subagent_file_stats.values())
    if all_turns:
        lines.append(f"_Distribution across all {len(all_turns)} subagent launches in window -- "
                      f"turns: p50={percentile(all_turns,0.5):.0f}, p90={percentile(all_turns,0.9):.0f}, max={all_turns[-1]:.0f}; "
                      f"mean context/turn: p50={percentile(all_mean_ctx,0.5):,.0f}, p90={percentile(all_mean_ctx,0.9):,.0f}, max={all_mean_ctx[-1]:,.0f}._")
    else:
        lines.append("_No subagent launches found in window._")
    lines.append("")
    lines.append("_`*` = some records in this launch used a model with no rate bucket; cost proxy for those is excluded. "
                  "\"Type\"/\"Model\"/\"Brief\" come from the launch's agent-*.meta.json sidecar; a missing sidecar shows as unknown/unspecified/empty._")
    lines.append(COST_PROXY_NOTE)
    lines.append("")

    # ---- addition 3: Read-range estimate + Grep mode split ----
    lines.append("## Addition 3: Read-range estimate (top 15 by estimated lines returned)")
    lines.append("")
    lines.append("_Estimate per Read call: `limit` if given, else the referenced file's own line count capped at 2000 "
                  "(mirroring the Read tool's default), else unknown if the file couldn't be opened from this machine "
                  "(moved/deleted/inaccessible). Summed per file path across all calls in window._")
    lines.append("")
    lines.append("| # | File path | Reads | Estimated lines returned | Reads with unknown estimate |")
    lines.append("|---:|---|---:|---:|---:|")
    ranked_reads = sorted(stats.read_line_sum.items(), key=lambda kv: -kv[1])[:15]
    for i, (path, total_lines) in enumerate(ranked_reads, 1):
        cnt = stats.read_path_counts.get(path, 0)
        unk = stats.read_unknown_count.get(path, 0)
        lines.append(f"| {i} | {path} | {cnt} | {fmt_int(total_lines)} | {unk} |")
    lines.append("")
    total_unknown = sum(stats.read_unknown_count.values())
    lines.append(f"_Total Read calls with an unknown line estimate (file not found on this machine, no `limit` given): {total_unknown}._")
    lines.append("")

    lines.append("### Grep calls by `output_mode`")
    lines.append("")
    lines.append("| output_mode | Main | Subagent | Total |")
    lines.append("|---|---:|---:|---:|")
    all_modes = set(stats.grep_mode_by_scope["main"]) | set(stats.grep_mode_by_scope["subagent"])
    for mode in sorted(all_modes):
        m = stats.grep_mode_by_scope["main"][mode]
        s = stats.grep_mode_by_scope["subagent"][mode]
        lines.append(f"| {mode} | {m} | {s} | {m+s} |")
    lines.append("")
    lines.append("_`output_mode` defaults to `files_with_matches` when the call omits it; unspecified calls are counted there._")
    lines.append("")

    # ---- addition 4: test/build/preview/screenshot script executions ----
    lines.append("## Addition 4: tools/*.ps1 executions vs mentions, per script")
    lines.append("")
    lines.append("_\"Execution\" = the command invokes the script as its program (`-File tools/<name>` or a direct "
                  "`tools/<name>` / `./tools/<name>` call) and is not a `grep`/`cat`/`echo`/`Get-Content`/etc. lookup "
                  "and does not pass `-List`/`-SelfCheck` (those run the script but don't do a real test/build pass). "
                  "Everything else that names the script counts as a \"mention\", not an execution._")
    lines.append("")
    lines.append("| Script | Executions | ...from main | ...from subagent | Mentions (excluded) |")
    lines.append("|---|---:|---:|---:|---:|")
    total_exec = total_main_exec = total_sub_exec = total_mention = 0
    for name in SCRIPT_NAMES:
        m = stats.script_exec[name]["main"]
        s = stats.script_exec[name]["subagent"]
        mention = stats.script_mention[name]["main"] + stats.script_mention[name]["subagent"]
        lines.append(f"| {name} | {m+s} | {m} | {s} | {mention} |")
        total_exec += m + s
        total_main_exec += m
        total_sub_exec += s
        total_mention += mention
    lines.append(f"| **TOTAL** | {total_exec} | {total_main_exec} | {total_sub_exec} | {total_mention} |")
    lines.append("")

    lines.append("### Executions per day and script")
    lines.append("")
    lines.append("| Day | Script | Main | Subagent | Total |")
    lines.append("|---|---|---:|---:|---:|")
    for day in sorted(stats.script_exec_by_day.keys()):
        for name in SCRIPT_NAMES:
            scopes = stats.script_exec_by_day[day].get(name)
            if not scopes:
                continue
            m = scopes.get("main", 0)
            s = scopes.get("subagent", 0)
            if m + s == 0:
                continue
            lines.append(f"| {day} | {name} | {m} | {s} | {m+s} |")
    lines.append("")

    # ---- addition 5: cache-lifetime gaps ----
    lines.append("## Addition 5: cache-write attributable to long gaps (prompt-cache TTL)")
    lines.append("")
    lines.append("_TTL assumption: subagent transcripts use a 5-minute cache; main sessions on this subscription plan "
                  "use a 1-hour cache (per https://code.claude.com/docs/en/prompt-caching.md). Within each transcript, "
                  "a gap between consecutive assistant records longer than that threshold means the next record's "
                  "cache-write tokens are effectively a forced re-prime, not an incremental add. Counted per transcript, "
                  "grouped by the model family of the record that follows the gap._")
    lines.append("")
    lines.append("| Scope | Threshold | Family | Gaps over threshold | Cache-write tokens on the record after the gap |")
    lines.append("|---|---|---|---:|---:|")
    for scope, threshold_label in (("subagent", ">5 min"), ("main", ">60 min")):
        fams = stats.cache_ttl_gap_count.get(scope, {})
        if not fams:
            lines.append(f"| {scope} | {threshold_label} | (none) | 0 | 0 |")
            continue
        for family in sorted(fams.keys()):
            cnt = stats.cache_ttl_gap_count[scope][family]
            tok = stats.cache_ttl_gap_tokens[scope][family]
            lines.append(f"| {scope} | {threshold_label} | {family} | {cnt} | {fmt_int(tok)} |")
    lines.append("")

    # ---- caveats ----
    lines.append("## Caveats")
    lines.append("")
    lines.append("- **Dedup rule** (established empirically, see script docstring): assistant "
                  "records are SSE-streamed and repeat across lines that share the same "
                  "`requestId` (== `message.id` in every record observed). Within a group, "
                  "`input_tokens`/`cache_creation_input_tokens`/`cache_read_input_tokens` were "
                  "identical on every sampled line (0 groups with variance in this run: "
                  f"{stats.dedup_groups_with_input_variance} flagged). `output_tokens` grows "
                  "monotonically across the group and is taken as the max (== last line) per group.")
    lines.append(f"- Malformed / unparseable JSONL lines skipped: {stats.malformed_lines} "
                  f"out of {stats.total_lines} total lines read.")
    lines.append(f"- Assistant lines seen (raw, pre-dedup, in-window files): {stats.assistant_lines_seen}; "
                  f"deduped to {stats.assistant_lines_deduped} requestId groups; "
                  f"{stats.records_outside_window_skipped} of those groups fell outside the "
                  f"{days}-day record-timestamp window (their file had a qualifying mtime but this "
                  f"particular record predates the window) and were excluded from all totals.")
    lines.append(f"- Structure check: main-session files with an unexpected `isSidechain: true` line: "
                  f"{stats.mismatched_sidechain_main}. Subagent files with an unexpected "
                  f"`isSidechain: false` line: {stats.mismatched_sidechain_sub}. Both should read 0; "
                  f"a nonzero value here means the main-vs-subagent split by file location may not "
                  f"perfectly equal a split by `isSidechain`.")
    lines.append("- Cost proxy: models that don't match fable/mythos/opus/sonnet/haiku in their "
                  "`message.model` string (case-insensitive substring match) have no rate and are "
                  "shown as `n/a`, excluded from the grand-total cost. A `<synthetic>` pseudo-model "
                  "appears in the data with all-zero usage on every record observed (checked ~8 "
                  "samples); it does not affect totals either way.")
    lines.append("- 'Subagent launches per day' and the requested-model / subagent_type breakdown "
                  "come from `Agent` tool_use blocks inside MAIN session assistant messages, deduped "
                  "by tool_use `id` within each file (not from the `subagents/*.meta.json` sidecar "
                  "files, though the two should roughly agree; meta.json was not cross-checked line "
                  "for line against this count).")
    lines.append("- Per-session stats (original item 4) are computed strictly from each top-level main `.jsonl` "
                  "file and do NOT roll up that session's subagent transcripts. Addition 1a repeats that "
                  "own-tokens-only view with more columns; addition 1b adds the subagent-inclusive roll-up.")
    lines.append("- Read/Grep/Glob and Bash/PowerShell tool_use calls are deduped by tool_use `id` "
                  "within each file, since a tool_use block can appear on more than one streamed line "
                  "for the same request (not verified to have differing content across duplicates, but "
                  "id-based dedup is safe regardless).")
    lines.append("- Day bucketing uses the assistant record's own UTC `timestamp`, not file mtime. "
                  "All per-day/per-tool aggregates are gated to the lookback window the same way the "
                  "token totals are -- a file can pass the mtime pre-filter while still containing much "
                  "older lines (e.g. a long-lived main session), so gating only by file mtime would have "
                  "leaked pre-window rows into 'per day' tables.")
    lines.append(f"- Reference 'now' for the {days}-day window is the wall-clock time the script ran "
                  f"({NOW.isoformat()}), not a value read from the data.")
    lines.append("- Addition 1's session-to-subagent roll-up is by directory structure "
                  "(`<session-uuid>/subagents/*.jsonl` under a main file literally named "
                  "`<session-uuid>.jsonl`), not by any explicit parent-id field in the transcripts.")
    lines.append("- Addition 1's 'first user message' is the first non-empty text found in this main "
                  "session file's first few `type: user` records (checked up to 5), first line only, "
                  "truncated to 80 chars; a session whose early user turns carry no plain text (e.g. "
                  "only tool_result/image blocks) reports '(no text found)'.")
    lines.append("- Addition 3's Read-range estimate reads the CURRENT contents of files on this machine, "
                  "not the content that existed when the transcript's Read call actually ran; a file "
                  "edited or deleted since then will estimate against what's there now (or 'unknown' if "
                  "it's gone). This is read-only against those files -- never modified or created.")
    lines.append("- Addition 4's execution-vs-mention split is a heuristic over the raw command string "
                  "(regex + substring checks), not a shell parse; an unusual quoting/escaping style could "
                  "misclassify a call in either direction.")
    lines.append("- Addition 5 counts a gap only between two IN-WINDOW assistant records within the SAME "
                  "transcript file; the very first record in a file (and the first record after the "
                  "lookback window's start cutoff) has no predecessor to compare against and is never "
                  "counted as following a gap, which slightly undercounts gaps at each file's start.")
    lines.append("- This script only ever writes the single markdown file passed via `--out` (default under "
                  "`reports/usage/`), and refuses to run if that path resolves outside this repo's `reports/` "
                  "directory.")
    lines.append("")

    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="Read-only token-usage baseline over this project's Claude Code transcripts.")
    parser.add_argument("--days", type=int, default=14, help="lookback window in days (default 14)")
    parser.add_argument("--out", type=str, default=None, help="output path (default: reports/usage/<YYYY-MM-DD>.md); must resolve inside reports/")
    args = parser.parse_args()

    if args.days <= 0:
        print("--days must be a positive integer", file=sys.stderr)
        sys.exit(1)

    window_start = NOW - timedelta(days=args.days)

    if args.out:
        out_path = os.path.abspath(os.path.join(REPO_ROOT, args.out))
    else:
        out_path = os.path.join(REPORTS_ROOT, "usage", f"{NOW.date().isoformat()}.md")

    reports_root_norm = os.path.normcase(os.path.normpath(REPORTS_ROOT))
    out_norm = os.path.normcase(os.path.normpath(out_path))
    if os.path.commonpath([reports_root_norm, out_norm]) != reports_root_norm:
        print(f"Refusing to write outside reports/: {out_path}", file=sys.stderr)
        sys.exit(1)

    main_files, subagent_files, skipped = list_target_files(window_start)
    stats = Stats()

    for path in main_files:
        process_file(path, "main", stats, window_start)
        stats.files_processed += 1

    for path in subagent_files:
        process_file(path, "subagent", stats, window_start)
        stats.files_processed += 1

    md = build_markdown(stats, main_files, subagent_files, skipped, window_start, args.days)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(md)

    print(f"Wrote {out_path}")
    print(f"Processed {stats.files_processed} files "
          f"({len(main_files)} main + {len(subagent_files)} subagent), "
          f"{stats.malformed_lines} malformed lines skipped.")


if __name__ == "__main__":
    main()
