#!/usr/bin/env python3
"""Render a balance-bot batch's summary.json into one self-contained HTML
report, with a delta column against a previous batch.

Usage:
    python tools/bot_report.py <batch_dir> [--previous <batch_dir>] [--out report.html]

Standard library only. See docs/BOT_SUMMARY_SCHEMA.md for the summary.json
contract this reads. This script never parses traces.jsonl -- every metric
here is precomputed by the runner into summary.json; this file only renders
and diffs it.
"""

import argparse
import html
import json
import os
import re
import statistics
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Loading and previous-batch discovery
# ---------------------------------------------------------------------------


def load_summary(batch_dir):
    path = Path(batch_dir) / "summary.json"
    # utf-8-sig, not utf-8: it reads a plain UTF-8 file identically and also
    # tolerates a leading BOM. The runner writes without one now, but the
    # previous-batch lookup below reads OLD directories -- including any
    # written before that was true -- and json.load refuses a BOM outright,
    # so a single stale batch on disk would break every report from here on.
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)


def find_previous_batch(batch_dir):
    """Newest sibling directory under batch_dir's parent, with a summary.json,
    whose name sorts before batch_dir's own name. Batch directories are named
    reports/bot/<yyyyMMdd-HHmmss>/, which sorts lexicographically in time
    order, so string comparison is enough -- no timestamp parsing needed.
    """
    batch_dir = Path(batch_dir).resolve()
    parent = batch_dir.parent
    if not parent.is_dir():
        return None

    candidates = []
    for child in parent.iterdir():
        if not child.is_dir():
            continue
        if child.name >= batch_dir.name:
            continue
        if not (child / "summary.json").is_file():
            continue
        candidates.append(child)

    if not candidates:
        return None
    return max(candidates, key=lambda p: p.name)


# ---------------------------------------------------------------------------
# Small numeric helpers
# ---------------------------------------------------------------------------


def cell_key(cell):
    return (cell["archetype"], cell["profile"])


def index_cells(summary):
    return {cell_key(c): c for c in summary.get("cells", [])}


def fmt_num(value):
    if value is None:
        return "-"
    if isinstance(value, bool):
        return str(value)
    if isinstance(value, float):
        if value == int(value):
            return str(int(value))
        return "{:.3g}".format(value)
    return str(value)


# direction: "high" (bigger is better), "low" (smaller is better), None (no verdict)
def delta_html(old, new, direction=None):
    if old is None:
        return ""
    if isinstance(old, bool) or isinstance(new, bool):
        return ""
    try:
        d = new - old
    except TypeError:
        return ""

    if abs(d) < 1e-9:
        sign = "0"
        cls = "delta-neutral"
    else:
        sign = "{:+.3g}".format(d)
        if direction == "high":
            cls = "delta-better" if d > 0 else "delta-worse"
        elif direction == "low":
            cls = "delta-better" if d < 0 else "delta-worse"
        else:
            cls = "delta-neutral"

    return '<span class="delta {}">{}</span>'.format(cls, html.escape(sign))


# ---------------------------------------------------------------------------
# Plain-text stdout summary
# ---------------------------------------------------------------------------


def render_text_summary(summary):
    lines = []
    batch = summary["batch"]
    lines.append(
        "Batch {} ({}) -- {} cell(s), {}s".format(
            batch.get("timestamp", "?"),
            batch.get("commitSha", "?"),
            len(summary.get("cells", [])),
            fmt_num(batch.get("elapsedSeconds")),
        )
    )
    for cell in summary.get("cells", []):
        depth = cell.get("depth", {})
        lines.append(
            "  {} / {}: depth median={} (n={})".format(
                cell["archetype"], cell["profile"], fmt_num(depth.get("median")), cell.get("runs")
            )
        )

    bug_count = len(summary.get("bugs", []))
    lines.append("Bugs: {}".format(bug_count))

    det = summary.get("determinism", {})
    mismatches = det.get("mismatches", [])
    checked = det.get("checked", 0)
    if mismatches:
        lines.append("Determinism: {} mismatch(es) out of {} checked".format(len(mismatches), checked))
    else:
        lines.append("Determinism: clean ({} checked)".format(checked))

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# HTML rendering
# ---------------------------------------------------------------------------

CSS = """
body { font-family: -apple-system, Segoe UI, sans-serif; margin: 0; padding: 24px; background: #0f1115; color: #e6e8eb; }
h1, h2, h3 { color: #fff; }
h1 { font-size: 20px; margin-bottom: 4px; }
.subtitle { color: #9aa4b2; margin-bottom: 24px; }
table { border-collapse: collapse; margin: 8px 0 24px 0; width: 100%; max-width: 900px; }
th, td { border: 1px solid #2a2f3a; padding: 4px 10px; text-align: right; font-variant-numeric: tabular-nums; }
th:first-child, td:first-child { text-align: left; }
th { background: #1a1e26; cursor: pointer; user-select: none; }
th.sorted-asc::after { content: " \\25B2"; }
th.sorted-desc::after { content: " \\25BC"; }
tr:nth-child(even) td { background: #14171d; }
.delta-better { color: #57d38c; }
.delta-worse { color: #e06060; }
.delta-neutral { color: #9aa4b2; }
.cell-header { margin-top: 32px; }
.bar-row text { fill: #e6e8eb; font-size: 12px; }
.coverage-list { columns: 3; }
.coverage-list li { margin-bottom: 2px; }
.bug-row-invariant { color: #e06060; font-weight: 600; }
code { background: #1a1e26; padding: 1px 4px; border-radius: 3px; }
.empty { color: #6b7480; font-style: italic; }
"""

JS = """
function sortTable(table, col, numeric) {
  var tbody = table.tBodies[0];
  var rows = Array.prototype.slice.call(tbody.rows);
  var th = table.tHead.rows[0].cells[col];
  var asc = th.classList.contains('sorted-asc') ? false : true;
  rows.sort(function(a, b) {
    var av = a.cells[col].getAttribute('data-sort') || a.cells[col].innerText;
    var bv = b.cells[col].getAttribute('data-sort') || b.cells[col].innerText;
    if (numeric) { av = parseFloat(av) || 0; bv = parseFloat(bv) || 0; return asc ? av - bv : bv - av; }
    return asc ? av.localeCompare(bv) : bv.localeCompare(av);
  });
  for (var i = 0; i < table.tHead.rows[0].cells.length; i++) {
    table.tHead.rows[0].cells[i].classList.remove('sorted-asc', 'sorted-desc');
  }
  th.classList.add(asc ? 'sorted-asc' : 'sorted-desc');
  rows.forEach(function(r) { tbody.appendChild(r); });
}
document.querySelectorAll('table.sortable').forEach(function(table) {
  var headers = table.tHead.rows[0].cells;
  for (var i = 0; i < headers.length; i++) {
    (function(i) {
      headers[i].addEventListener('click', function() {
        sortTable(table, i, headers[i].getAttribute('data-numeric') === '1');
      });
    })(i);
  }
});
"""


def esc(v):
    return html.escape(str(v))


# One color per archetype, assigned by first-seen order across every
# profile in archetypeGap so the SAME archetype reads as the SAME color
# in every profile's row group -- this is the ONE thing that makes a
# four-(or more-)archetype batch scannable at a glance; with a fixed
# single color (the pre-Phase-6 version), the two extra bars per profile
# read as noise rather than as "which archetype is this". Six entries
# covers the plan's four (RandomLegal/GreedyAggressive/GreedyDefensive/
# Lookahead2) with headroom before it wraps.
BAR_PALETTE = ["#4a7fd6", "#57d38c", "#e0a355", "#c26be0", "#e06060", "#4fd6c0"]


def _archetype_color_map(gap):
    order = []
    for profile_entry in gap:
        for a in profile_entry["archetypes"]:
            if a["archetype"] not in order:
                order.append(a["archetype"])
    return {name: BAR_PALETTE[i % len(BAR_PALETTE)] for i, name in enumerate(order)}


def render_archetype_gap_svg(summary):
    gap = summary.get("archetypeGap", [])
    if not gap:
        return '<p class="empty">No archetype gap data (single-archetype batch).</p>'

    colors = _archetype_color_map(gap)

    bar_h = 22
    gap_h = 6
    label_w = 220
    chart_w = 500
    rows = []
    for profile_entry in gap:
        rows.append(("__profile__", profile_entry["profile"], None))
        for a in profile_entry["archetypes"]:
            rows.append(("bar", a["archetype"], a["medianDepth"]))

    max_depth = max([r[2] for r in rows if r[0] == "bar"] + [1])
    total_h = sum(bar_h + gap_h if r[0] == "bar" else 26 for r in rows) + 10

    svg = ['<svg viewBox="0 0 {} {}" xmlns="http://www.w3.org/2000/svg" font-family="sans-serif">'.format(
        label_w + chart_w + 60, total_h
    )]
    y = 10
    for kind, label, value in rows:
        if kind == "__profile__":
            svg.append('<text x="0" y="{}" font-size="13" font-weight="bold" fill="#e6e8eb">{}</text>'.format(y + 14, esc(label)))
            y += 26
            continue
        w = 0 if max_depth == 0 else (value / max_depth) * chart_w
        svg.append('<g class="bar-row">')
        svg.append('<text x="0" y="{}" font-size="12">{}</text>'.format(y + bar_h * 0.7, esc(label)))
        svg.append('<rect x="{}" y="{}" width="{:.1f}" height="{}" fill="{}" rx="2"/>'.format(
            label_w, y, w, bar_h, colors.get(label, BAR_PALETTE[0])
        ))
        svg.append('<text x="{}" y="{}" font-size="12">{}</text>'.format(
            label_w + w + 8, y + bar_h * 0.7, fmt_num(value)
        ))
        svg.append('</g>')
        y += bar_h + gap_h
    svg.append('</svg>')
    return "".join(svg)


NUMERIC_SIMPLE_FIELDS = [
    ("runs", "runs", None),
    ("doomedShare", "doomed share", None),
    ("swingShare", "swing share", None),
    ("turnOneShare", "turn-1 share", None),
    ("consumableUseShare", "consumable use share", None),
    ("potionsWastedMean", "potions wasted (mean)", None),
]

DEPTH_FIELDS = [
    ("median", "median", "high"),
    ("p10", "p10", "high"),
    ("p90", "p90", "high"),
    ("mean", "mean", "high"),
    ("cappedShare", "capped share", None),
]


def render_cell_table(cell, prev_cell):
    rows = []

    depth = cell.get("depth", {})
    prev_depth = (prev_cell or {}).get("depth", {})
    for key, label, direction in DEPTH_FIELDS:
        old = prev_depth.get(key) if prev_cell else None
        new = depth.get(key)
        rows.append((label, fmt_num(new), delta_html(old, new, direction) if prev_cell else ""))

    for key, label, direction in NUMERIC_SIMPLE_FIELDS:
        old = prev_cell.get(key) if prev_cell else None
        new = cell.get(key)
        rows.append((label, fmt_num(new), delta_html(old, new, direction) if prev_cell else ""))

    diversity = cell.get("buildDiversity", {})
    prev_diversity = (prev_cell or {}).get("buildDiversity", {})
    for key, label in [("distinctRelicSets", "distinct relic sets"), ("distinctTalentSets", "distinct talent sets")]:
        old = prev_diversity.get(key) if prev_cell else None
        new = diversity.get(key)
        rows.append((label, fmt_num(new), delta_html(old, new) if prev_cell else ""))

    out = ['<table class="sortable"><thead><tr><th>metric</th><th data-numeric="1">value</th><th>delta</th></tr></thead><tbody>']
    for label, value, delta in rows:
        out.append("<tr><td>{}</td><td>{}</td><td>{}</td></tr>".format(esc(label), esc(value), delta))
    out.append("</tbody></table>")
    return "".join(out)


def render_keyed_table(title, current, previous, better=None):
    keys = sorted(set(current.keys()) | set((previous or {}).keys()))
    if not keys:
        return '<h3>{}</h3><p class="empty">none</p>'.format(esc(title))
    out = ["<h3>{}</h3>".format(esc(title))]
    out.append('<table class="sortable"><thead><tr><th>key</th><th data-numeric="1">value</th><th>delta</th></tr></thead><tbody>')
    for k in keys:
        new = current.get(k)
        old = (previous or {}).get(k)
        d = delta_html(old, new, better) if previous is not None else ""
        out.append("<tr><td>{}</td><td>{}</td><td>{}</td></tr>".format(esc(k), fmt_num(new), d))
    out.append("</tbody></table>")
    return "".join(out)


def render_death_causes(cell):
    rows = cell.get("deathCauses", [])
    if not rows:
        return '<h3>death causes</h3><p class="empty">none</p>'
    rows = sorted(rows, key=lambda r: -r["count"])
    out = ['<h3>death causes</h3><table class="sortable"><thead><tr><th>enemy</th><th>room type</th><th data-numeric="1">floor</th><th data-numeric="1">count</th></tr></thead><tbody>']
    for r in rows:
        out.append(
            "<tr><td>{}</td><td>{}</td><td>{}</td><td>{}</td></tr>".format(
                esc(r["enemyId"]), esc(r["roomType"]), esc(r["floor"]), esc(r["count"])
            )
        )
    out.append("</tbody></table>")
    return "".join(out)


# Item ids for a set piece are generated as "<set>_<piece>_p<tier>"
# (ItemSetEntryResolver.BuildPiece: `$"{setId}_{pieceId}_p{tier}"`) --
# eleven real ids (p0..p10, maxTier defaults to 10) per authored piece.
# A coverage list of raw ids buries the one gap worth reading ("this
# WHOLE PIECE never drops") under ten-plus near-duplicate rows. This
# folds every "<base>_pN" id sharing a base back into one row, so the
# report reads "leather_torso (p4..p10)" instead of seven separate
# <li> lines.
_PLUS_TIER_ID = re.compile(r"^(.*)_p(\d+)$")


def _fold_plus_tiers(ids):
    bases = {}
    standalone = []
    for item_id in ids:
        m = _PLUS_TIER_ID.match(item_id)
        if m:
            bases.setdefault(m.group(1), []).append(int(m.group(2)))
        else:
            standalone.append(item_id)

    rows = list(standalone)
    for base, tiers in bases.items():
        rows.append("{} ({})".format(base, _tier_ranges(sorted(set(tiers)))))
    return sorted(rows)


def _tier_ranges(tiers):
    """[4,5,6,7,8,9,10] -> 'p4..p10'; [1,3] -> 'p1, p3'."""
    ranges = []
    start = prev = tiers[0]
    for t in tiers[1:]:
        if t == prev + 1:
            prev = t
            continue
        ranges.append((start, prev))
        start = prev = t
    ranges.append((start, prev))
    return ", ".join("p{}".format(a) if a == b else "p{}..p{}".format(a, b) for a, b in ranges)


# coverage.skillsNeverUsed holds BOTH player skill ids and enemy ability
# ids (docs/BOT_SUMMARY_SCHEMA.md: "never the target of a TurnTrace.Action
# of the form 'Skill:...'", which TraceLabel writes for an enemy's own
# skill turns identically to a player's -- see Domain/Bot/FightRunner.cs's
# TraceLabel). Splitting them needs a second list naming which ids are
# enemy-side; look for one under a name that says so rather than a single
# hardcoded key, since the concurrent schema work on this same phase may
# land it under any of a few reasonable spellings.
def _find_enemy_ability_key(coverage):
    for key in coverage.keys():
        if key == "skillsNeverUsed":
            continue
        lowered = key.lower()
        if "enemy" in lowered and ("abilit" in lowered or "skill" in lowered):
            return key
    return None


def render_coverage(coverage):
    out = ["<h2>coverage</h2>"]
    plain_labels = [
        ("enemiesNeverSeen", "enemies never seen"),
        ("relicsNeverOffered", "relics never offered"),
        ("relicsNeverPicked", "relics never picked"),
    ]
    for key, label in plain_labels:
        items = coverage.get(key, [])
        out.append("<h3>{}</h3>".format(esc(label)))
        if not items:
            out.append('<p class="empty">none</p>')
        else:
            out.append('<ul class="coverage-list">' + "".join("<li><code>{}</code></li>".format(esc(i)) for i in items) + "</ul>")

    for key, label in [("itemsNeverOffered", "items never offered"), ("itemsNeverPicked", "items never picked")]:
        items = _fold_plus_tiers(coverage.get(key, []))
        out.append("<h3>{}</h3>".format(esc(label)))
        if not items:
            out.append('<p class="empty">none</p>')
        else:
            out.append('<ul class="coverage-list">' + "".join("<li><code>{}</code></li>".format(esc(i)) for i in items) + "</ul>")

    skills = coverage.get("skillsNeverUsed", [])
    enemy_key = _find_enemy_ability_key(coverage)
    out.append("<h3>skills never used</h3>")
    if not skills:
        out.append('<p class="empty">none</p>')
    elif enemy_key:
        enemy_ids = set(coverage.get(enemy_key, []))
        player_skills = sorted(s for s in skills if s not in enemy_ids)
        enemy_skills = sorted(s for s in skills if s in enemy_ids)
        out.append("<p><em>split against <code>{}</code>.</em></p>".format(esc(enemy_key)))
        for sub_label, sub_items in [("player skills", player_skills), ("enemy abilities", enemy_skills)]:
            out.append("<h4>{}</h4>".format(esc(sub_label)))
            if not sub_items:
                out.append('<p class="empty">none</p>')
            else:
                out.append('<ul class="coverage-list">' + "".join("<li><code>{}</code></li>".format(esc(i)) for i in sub_items) + "</ul>")
    else:
        # No way to tell player skills from enemy abilities apart yet --
        # collapsed under <details> rather than dumped flat, per this
        # phase's brief, so a long combined list does not read as one
        # more full-height coverage section.
        out.append(
            '<details><summary>{} never used</summary>'.format(len(skills))
            + '<ul class="coverage-list">'
            + "".join("<li><code>{}</code></li>".format(esc(i)) for i in sorted(skills))
            + "</ul></details>"
        )

    return "".join(out)


def render_bugs(bugs):
    out = ["<h2>bugs ({})</h2>".format(len(bugs))]
    if not bugs:
        out.append('<p class="empty">none</p>')
        return "".join(out)
    out.append(
        '<table class="sortable"><thead><tr>'
        "<th>invariant</th><th data-numeric=\"1\">seed</th><th>archetype</th><th>profile</th>"
        '<th data-numeric="1">step</th><th data-numeric="1">node</th><th>detail</th><th>last actions</th>'
        "</tr></thead><tbody>"
    )
    for b in bugs:
        out.append(
            "<tr><td class=\"bug-row-invariant\">{}</td><td>{}</td><td>{}</td><td>{}</td>"
            "<td>{}</td><td>{}</td><td>{}</td><td>{}</td></tr>".format(
                esc(b["invariant"]),
                esc(b["seed"]),
                esc(b["archetype"]),
                esc(b["profile"]),
                esc(b["step"]),
                esc(b.get("nodeId", "")),
                esc(b["detail"]),
                esc(", ".join(b.get("lastActions", []))),
            )
        )
    out.append("</tbody></table>")
    return "".join(out)


def render_determinism(det):
    checked = det.get("checked", 0)
    mismatches = det.get("mismatches", [])
    out = ["<h2>determinism</h2>"]
    if not mismatches:
        out.append("<p>clean -- {} run(s) checked, 0 mismatches.</p>".format(checked))
    else:
        out.append("<p><strong>{} mismatch(es)</strong> out of {} checked:</p>".format(len(mismatches), checked))
        out.append('<table class="sortable"><thead><tr><th data-numeric="1">seed</th><th>archetype</th><th>profile</th></tr></thead><tbody>')
        for m in mismatches:
            out.append("<tr><td>{}</td><td>{}</td><td>{}</td></tr>".format(esc(m["seed"]), esc(m["archetype"]), esc(m["profile"])))
        out.append("</tbody></table>")
    return "".join(out)


def render_html(summary, previous, batch_dir, previous_dir):
    batch = summary["batch"]
    cells = index_cells(summary)
    prev_cells = index_cells(previous) if previous else {}

    parts = []
    parts.append("<title>Balance bot report -- {}</title>".format(esc(batch.get("timestamp", ""))))
    parts.append("<style>{}</style>".format(CSS))
    parts.append("<h1>Balance bot report</h1>")
    parts.append(
        '<div class="subtitle">{} &middot; commit {} &middot; {} runs/cell &middot; seeds from {} '
        "&middot; archetypes: {} &middot; profiles: {} &middot; depth cap {} steps &middot; {}s elapsed</div>".format(
            esc(batch.get("timestamp", "?")),
            esc(batch.get("commitSha", "?")),
            esc(batch.get("runsPerCell", "?")),
            esc(batch.get("firstSeed", "?")),
            esc(", ".join(batch.get("archetypes", []))),
            esc(", ".join(batch.get("profiles", []))),
            esc(batch.get("depthCapSteps", "?")),
            esc(fmt_num(batch.get("elapsedSeconds"))),
        )
    )
    if previous:
        parts.append("<p>Compared against <code>{}</code>.</p>".format(esc(str(previous_dir))))
    else:
        parts.append('<p class="empty">No previous batch found -- deltas blank.</p>')

    parts.append("<h2>archetype gap (median depth)</h2>")
    parts.append(render_archetype_gap_svg(summary))

    dp = summary.get("decisionPressure", {})
    if dp:
        parts.append("<h2>decision pressure</h2>")
        parts.append(render_keyed_table("", dp, (previous or {}).get("decisionPressure") if previous else None))

    for key, cell in cells.items():
        prev_cell = prev_cells.get(key)
        parts.append('<h2 class="cell-header">{} / {}</h2>'.format(esc(key[0]), esc(key[1])))
        parts.append(render_cell_table(cell, prev_cell))
        parts.append(render_death_causes(cell))
        parts.append(
            render_keyed_table(
                "fight length by floor",
                cell.get("fightLength", {}).get("byFloor", {}),
                (prev_cell or {}).get("fightLength", {}).get("byFloor", {}) if prev_cell else None,
            )
        )
        parts.append(
            render_keyed_table(
                "fight length by room type",
                cell.get("fightLength", {}).get("byRoomType", {}),
                (prev_cell or {}).get("fightLength", {}).get("byRoomType", {}) if prev_cell else None,
            )
        )
        parts.append(
            render_keyed_table(
                "steamroll share by floor",
                cell.get("steamrollByFloor", {}),
                (prev_cell or {}).get("steamrollByFloor", {}) if prev_cell else None,
            )
        )
        parts.append(
            render_keyed_table(
                "item pick rate",
                cell.get("itemPickRate", {}),
                (prev_cell or {}).get("itemPickRate", {}) if prev_cell else None,
            )
        )
        parts.append(
            render_keyed_table(
                "relic pick rate",
                cell.get("relicPickRate", {}),
                (prev_cell or {}).get("relicPickRate", {}) if prev_cell else None,
            )
        )

    parts.append(render_coverage(summary.get("coverage", {})))
    parts.append(render_bugs(summary.get("bugs", [])))
    parts.append(render_determinism(summary.get("determinism", {})))

    parts.append("<script>{}</script>".format(JS))

    return "\n".join(parts)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def main(argv=None):
    parser = argparse.ArgumentParser(description="Render a balance-bot batch summary to HTML.")
    parser.add_argument("batch_dir", help="Directory holding this batch's summary.json (and traces.jsonl).")
    parser.add_argument("--previous", default=None, help="Previous batch directory to diff against. Auto-discovered if omitted.")
    parser.add_argument("--out", default="report.html", help="Output HTML path (default: report.html in the current directory).")
    args = parser.parse_args(argv)

    batch_dir = Path(args.batch_dir)
    summary = load_summary(batch_dir)

    previous_dir = Path(args.previous) if args.previous else find_previous_batch(batch_dir)
    previous = load_summary(previous_dir) if previous_dir else None

    text = render_text_summary(summary)
    print(text)

    out_html = render_html(summary, previous, batch_dir, previous_dir)
    out_path = Path(args.out)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(out_html)

    print("\nReport written to {}".format(out_path.resolve()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
