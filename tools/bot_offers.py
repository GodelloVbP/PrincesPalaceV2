#!/usr/bin/env python3
"""Item-offer analysis for a balance-bot batch: which tier, plus and rift
tier show up at which step, at what favor, from which encounter class.

    python tools/bot_offers.py reports/bot/<timestamp>
    python tools/bot_offers.py reports/bot/<timestamp> --out somewhere/offers.html

Reads every shard's runs.jsonl (same directory detection as bot_merge.py),
writes <batch>/offers.html and prints the same tables as plain text. Item
ids are deliberately NOT tabulated -- the question this answers is whether
RarityTable / LootLadder / ModifierTable produce the curve they were tuned
for, and an item id says nothing about that. The per-offer axes come from
rooms[].offers[] (docs/BOT_SUMMARY_SCHEMA.md); a batch written before that
field existed reports "no offer axes" rather than pretending.

Every table is grouped by PROFILE (Fresh/Mid/Late) and POOLS ARCHETYPES:
the roll is a function of step, favor and encounter class, none of which an
archetype controls -- only the PICK is a policy decision, and table 6
(picked vs shown) is where that pooling is a real simplification.

Stdlib only, same as bot_merge.py and bot_report.py.
"""

import argparse
import html
import json
import os
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bot_merge  # noqa: E402  (find_shard_dirs, _read_jsonl)


MAX_STEP = 40
BAND_WIDTH = 4
PLUS_CAP = 4  # +0..+3 shown as-is, +4 and above pooled as "+4+"
CLASSES = ["Normal", "Elite", "Boss"]

# Cited excerpts, for table 8. Kept as data so the HTML and the text render
# the same lines; line numbers were read from the files, not remembered.
FORMULA_REFERENCE = [
    ("Assets/_Project/Scripts/Domain/Rewards/RarityTable.cs:40-47",
     "StepsPerTier = 8; FloorTier(depthStep) = depthStep / 8  (expected tier at a depth)"),
    ("Assets/_Project/Scripts/Domain/Rewards/RarityTable.cs:56-64",
     "TierFloorFor: Normal 0 / Elite 1 / Boss 3  (a boss never drops below tier 3)"),
    ("Assets/_Project/Scripts/Domain/Rewards/RarityTable.cs:99-117",
     "RollTier = clamp(FloorTier(step) + LootLadder.Climb(class, favor), classFloor, maxTier)"),
    ("Assets/_Project/Scripts/Domain/Rewards/RarityTable.cs:125-128",
     "RollPlus = LootLadder.Climb(class, favor)  -- never sees the depth"),
    ("Assets/_Project/Scripts/Domain/Rewards/LootLadder.cs:34-53",
     "MaxRungs 5; NormalStep .22 / EliteStep .30 / BossStep .38; FavorPerPoint .006; MaxStep .55"),
    ("Assets/_Project/Scripts/Domain/Rewards/LootLadder.cs:97-118",
     "Climb: P(rungs >= s) = p^s, five fixed draws, count the leading successes"),
    ("Assets/_Project/Scripts/Domain/Rewards/ModifierTable.cs:26,83-85,103-104",
     "RiftTier ladder: MaxRungs 3; NormalStep .12 / EliteStep .15 / BossStep .18; FavorPerPoint .002; MaxStep .22"),
    ("Assets/_Project/Scripts/Core/ItemOfferRoll.cs:150-167",
     "tier rolled ONCE per offer set; plus / riftTier / modifiers rolled PER item"),
    ("Assets/_Project/Scripts/Domain/Rewards/ItemOffer.cs:74,115-136",
     "ItemOfferTable.Choose: TierSpread 1 around the target, widened until the pool fills the row"),
    ("Assets/_Project/Scripts/Core/Bot/RunOrchestrator.cs:472-474",
     "encounter = session.IsEliteFight ? Elite : Normal  -- IsBossFight is NOT consulted"),
]


# ---------------------------------------------------------------- reading


def load_runs(batch_dir):
    dirs = bot_merge.find_shard_dirs(batch_dir)
    if not dirs:
        raise SystemExit(
            "no runs.jsonl under {} (looked in the directory itself and in shard-*/)".format(batch_dir)
        )
    runs = []
    for shard in dirs:
        runs.extend(bot_merge._read_jsonl(os.path.join(shard, "runs.jsonl")))
    return runs


def offer_rows(runs):
    """One flat row per SHOWN item: (profile, archetype, seed, step, floor,
    favor, class, tier, plus, rift, mods, picked). Rooms with no offers[]
    (no offer made, or a pre-field batch) contribute nothing."""
    rows = []
    for run in runs:
        profile = run.get("profile", "?")
        archetype = run.get("archetype", "?")
        seed = run.get("seed")
        for room in run.get("rooms", []):
            offers = room.get("offers") or []
            if not offers:
                continue
            picked = room.get("pickedIndex", -1)
            for index, offer in enumerate(offers):
                rows.append({
                    "profile": profile,
                    "archetype": archetype,
                    "seed": seed,
                    "step": int(room.get("step", 0)),
                    "floor": int(room.get("floor", 0)),
                    "favor": int(room.get("favor", 0)),
                    "class": room.get("encounterClass") or "?",
                    "tier": int(offer.get("tier", 0)),
                    "plus": int(offer.get("plus", 0)),
                    "rift": int(offer.get("riftTier", 0)),
                    "mods": int(offer.get("modifierCount", 0)),
                    "index": index,
                    "picked": picked == index,
                    "room_picked": picked >= 0,
                })
    return rows


# ---------------------------------------------------------------- helpers


def band_of(step):
    """1-4 -> "1-4", 5-8 -> "5-8", ... Steps outside 1..MAX_STEP fall in the
    nearest band rather than being dropped, so a capped run's last room
    still counts somewhere."""
    step = max(1, min(MAX_STEP, step))
    lo = ((step - 1) // BAND_WIDTH) * BAND_WIDTH + 1
    return "{}-{}".format(lo, lo + BAND_WIDTH - 1)


def bands():
    return [band_of(s) for s in range(1, MAX_STEP + 1, BAND_WIDTH)]


def plus_key(plus):
    return "+{}".format(plus) if plus < PLUS_CAP else "+{}+".format(PLUS_CAP)


def plus_keys():
    return [plus_key(p) for p in range(PLUS_CAP + 1)]


def pct(part, whole):
    return 0.0 if not whole else 100.0 * part / whole


def fmt_cell(count, whole):
    return "{} ({:.1f}%)".format(count, pct(count, whole))


def percentile(values, q):
    if not values:
        return None
    ordered = sorted(values)
    k = (len(ordered) - 1) * q
    lo = int(k)
    hi = min(lo + 1, len(ordered) - 1)
    return ordered[lo] + (ordered[hi] - ordered[lo]) * (k - lo)


def by(rows, key):
    out = {}
    for row in rows:
        out.setdefault(key(row), []).append(row)
    return out


def profiles_of(rows):
    return sorted({r["profile"] for r in rows}, key=lambda p: {"Fresh": 0, "Mid": 1, "Late": 2}.get(p, 9))


def classes_seen(rows):
    seen = {r["class"] for r in rows}
    return [c for c in CLASSES if c in seen] + sorted(seen - set(CLASSES))


# ---------------------------------------------------------------- tables
#
# Each table_* returns (title, header, rows, notes). rows are lists of
# strings. A table is computed per profile by the caller.


def table_per_step(rows):
    header = ["step", "offers shown", "items shown", "picks", "pick rate"]
    out = []
    per_step = by(rows, lambda r: r["step"])
    for step in range(1, MAX_STEP + 1):
        items = per_step.get(step, [])
        offers = {(r["seed"], r["archetype"], r["step"]) for r in items}
        picks = {(r["seed"], r["archetype"], r["step"]) for r in items if r["room_picked"]}
        if not items:
            continue
        out.append([str(step), str(len(offers)), str(len(items)), str(len(picks)),
                    "{:.1f}%".format(pct(len(picks), len(offers)))])
    return ("1. Offers per step", header, out,
            ["'offers shown' is rooms that rolled an offer; 'items shown' is cards on them; "
             "pick rate = rooms where something was taken / rooms with an offer."])


def _dist_table(rows, axis, labels, label_of, title, extra_stats=True):
    """Distribution of `axis` per (band x class): one row per band+class,
    columns per label, plus median / p90 of the raw axis."""
    header = ["band", "class", "n"] + labels + (["median", "p90"] if extra_stats else [])
    out = []
    grouped = by(rows, lambda r: (band_of(r["step"]), r["class"]))
    for band in bands():
        for cls in classes_seen(rows):
            items = grouped.get((band, cls), [])
            if not items:
                continue
            counts = {}
            for r in items:
                counts[label_of(r[axis])] = counts.get(label_of(r[axis]), 0) + 1
            line = [band, cls, str(len(items))] + [fmt_cell(counts.get(l, 0), len(items)) for l in labels]
            if extra_stats:
                vals = [r[axis] for r in items]
                line += ["{:.1f}".format(percentile(vals, 0.5)), "{:.1f}".format(percentile(vals, 0.9))]
            out.append(line)
    # An "all bands" summary per class at the bottom.
    for cls in classes_seen(rows):
        items = [r for r in rows if r["class"] == cls]
        if not items:
            continue
        counts = {}
        for r in items:
            counts[label_of(r[axis])] = counts.get(label_of(r[axis]), 0) + 1
        line = ["ALL", cls, str(len(items))] + [fmt_cell(counts.get(l, 0), len(items)) for l in labels]
        if extra_stats:
            vals = [r[axis] for r in items]
            line += ["{:.1f}".format(percentile(vals, 0.5)), "{:.1f}".format(percentile(vals, 0.9))]
        out.append(line)
    return (title, header, out, [])


def table_tier(rows):
    max_tier = max((r["tier"] for r in rows), default=0)
    labels = ["T{}".format(t) for t in range(max_tier + 1)]
    return _dist_table(rows, "tier", labels, lambda t: "T{}".format(t),
                       "2. Tier distribution per step band x encounter class")


def table_plus(rows):
    return _dist_table(rows, "plus", plus_keys(), plus_key,
                       "3. Plus distribution per step band x encounter class")


def table_rift(rows):
    labels = ["R0", "R1", "R2", "R3"]
    return _dist_table(rows, "rift", labels, lambda r: "R{}".format(min(r, 3)),
                       "4. Rift tier distribution per step band x encounter class")


def favor_groups(rows):
    """Distinct favor values, or bands of them if there are many."""
    values = sorted({r["favor"] for r in rows})
    if len(values) <= 8:
        return [(str(v), lambda f, v=v: f == v) for v in values]
    lo, hi = values[0], values[-1]
    width = max(1, (hi - lo + 1) // 6)
    groups = []
    start = lo
    while start <= hi:
        end = start + width - 1
        groups.append(("{}-{}".format(start, end), lambda f, a=start, b=end: a <= f <= b))
        start = end + 1
    return groups


def table_favor(rows):
    values = sorted({r["favor"] for r in rows})
    max_tier = max((r["tier"] for r in rows), default=0)
    tier_labels = ["T{}".format(t) for t in range(max_tier + 1)]
    header = ["favor", "n"] + tier_labels + plus_keys() + ["med tier", "med plus"]
    out = []
    for label, match in favor_groups(rows):
        items = [r for r in rows if match(r["favor"])]
        if not items:
            continue
        tc, pc = {}, {}
        for r in items:
            tc[r["tier"]] = tc.get(r["tier"], 0) + 1
            pc[plus_key(r["plus"])] = pc.get(plus_key(r["plus"]), 0) + 1
        out.append([label, str(len(items))]
                   + [fmt_cell(tc.get(t, 0), len(items)) for t in range(max_tier + 1)]
                   + [fmt_cell(pc.get(k, 0), len(items)) for k in plus_keys()]
                   + ["{:.1f}".format(percentile([r["tier"] for r in items], 0.5)),
                      "{:.1f}".format(percentile([r["plus"] for r in items], 0.5))])
    notes = ["distinct favor values seen: {}".format(", ".join(str(v) for v in values) or "none")]
    return ("5. Tier / plus by favor", header, out, notes)


def table_picked_vs_shown(rows):
    header = ["tier", "plus", "shown", "shown share", "picked", "picked share", "pick rate"]
    shown = by(rows, lambda r: (r["tier"], r["plus"]))
    picked_rows = [r for r in rows if r["picked"]]
    picked = by(picked_rows, lambda r: (r["tier"], r["plus"]))
    out = []
    for key in sorted(shown):
        s = len(shown[key])
        p = len(picked.get(key, []))
        out.append([str(key[0]), "+{}".format(key[1]), str(s), "{:.1f}%".format(pct(s, len(rows))),
                    str(p), "{:.1f}%".format(pct(p, len(picked_rows))), "{:.1f}%".format(pct(p, s))])
    return ("6. Picked vs shown per (tier, plus)", header, out,
            ["'shown share' and 'picked share' are each out of their own total; a card whose picked share "
             "sits well under its shown share is one the archetypes pass over. Archetypes are POOLED here, "
             "which is the one table where that flattens a real policy difference."])


def table_first_appearance(rows):
    """Per run, the first step at which +1/+2/+3 (and each tier) appears in
    ANY shown card. Runs that never see it are counted, not dropped."""
    per_run = by(rows, lambda r: (r["seed"], r["archetype"]))
    total = len(per_run)
    header = ["threshold", "runs seeing it", "share", "median first step", "p10", "p90"]
    out = []
    max_tier = max((r["tier"] for r in rows), default=0)
    thresholds = [("plus >= {}".format(p), lambda r, p=p: r["plus"] >= p) for p in (1, 2, 3)]
    thresholds += [("tier >= {}".format(t), lambda r, t=t: r["tier"] >= t) for t in range(1, max_tier + 1)]
    for label, test in thresholds:
        firsts = []
        for items in per_run.values():
            hits = [r["step"] for r in items if test(r)]
            if hits:
                firsts.append(min(hits))
        if not firsts:
            out.append([label, "0", "0.0%", "never", "-", "-"])
            continue
        out.append([label, str(len(firsts)), "{:.1f}%".format(pct(len(firsts), total)),
                    "{:.0f}".format(percentile(firsts, 0.5)), "{:.0f}".format(percentile(firsts, 0.1)),
                    "{:.0f}".format(percentile(firsts, 0.9))])
    return ("7. First appearance (per run, across all shown cards)", header, out,
            ["{} runs in this profile made at least one offer; 'share' is out of those. A run that died "
             "before seeing the threshold is not in the median, which is why 'share' sits beside it.".format(total)])


def favor_note(rows):
    per_profile = {p: sorted({r["favor"] for r in items}) for p, items in by(rows, lambda r: r["profile"]).items()}
    if not per_profile:
        return "no offers in the batch."
    if len({tuple(v) for v in per_profile.values()}) == 1:
        return "favor is CONSTANT across profiles: every profile saw exactly {}.".format(
            list(per_profile.values())[0])
    return "favor DIFFERS by profile: " + "; ".join(
        "{} saw {}".format(p, v) for p, v in sorted(per_profile.items()))


def build(rows):
    """Everything the two renderers share: [(profile, [tables])] plus the
    batch-wide notes."""
    sections = []
    for profile in profiles_of(rows):
        items = [r for r in rows if r["profile"] == profile]
        sections.append((profile, [
            table_per_step(items),
            table_tier(items),
            table_plus(items),
            table_rift(items),
            table_favor(items),
            table_picked_vs_shown(items),
            table_first_appearance(items),
        ]))
    notes = [
        "Archetypes are POOLED in every table ({}).".format(
            ", ".join(sorted({r["archetype"] for r in rows})) or "none"),
        "Favor: " + favor_note(rows),
        "Encounter classes seen in the roll: {}.".format(", ".join(classes_seen(rows)) or "none"),
    ]
    return sections, notes


# ---------------------------------------------------------------- rendering


def render_text(sections, notes, batch_dir, total_runs, rows):
    lines = ["Item offers -- {}".format(batch_dir),
             "{} runs, {} shown cards".format(total_runs, len(rows)), ""]
    for note in notes:
        lines.append("* " + note)
    lines.append("")
    if not rows:
        lines.append("no offer axes in this batch (rooms[].offers is missing or empty everywhere) -- "
                     "rerun the batch on a build that writes them.")
        return "\n".join(lines) + "\n"
    for profile, tables in sections:
        lines.append("=" * 78)
        lines.append("PROFILE {}".format(profile))
        lines.append("=" * 78)
        for title, header, body, tnotes in tables:
            lines.append("")
            lines.append(title)
            lines.extend(_text_table(header, body))
            for n in tnotes:
                lines.append("  note: " + n)
    lines.append("")
    lines.append("8. Formula reference (file:line -> what it says)")
    for where, what in FORMULA_REFERENCE:
        lines.append("  {}".format(where))
        lines.append("      {}".format(what))
    return "\n".join(lines) + "\n"


def _text_table(header, body):
    widths = [len(h) for h in header]
    for row in body:
        for i, cell in enumerate(row):
            widths[i] = max(widths[i], len(cell))
    fmt = "  ".join("{:<" + str(w) + "}" for w in widths)
    out = ["  " + fmt.format(*header), "  " + "  ".join("-" * w for w in widths)]
    out += ["  " + fmt.format(*row) for row in body]
    return out


def render_html(sections, notes, batch_dir, total_runs, rows):
    esc = html.escape
    parts = ["<!doctype html><meta charset='utf-8'><title>Item offers</title>",
             "<style>body{font:14px/1.4 system-ui,sans-serif;margin:24px;max-width:1400px}"
             "table{border-collapse:collapse;margin:8px 0 16px;font-size:12px}"
             "th,td{border:1px solid #ccc;padding:2px 6px;text-align:right;white-space:nowrap}"
             "th:first-child,td:first-child,th:nth-child(2),td:nth-child(2){text-align:left}"
             "h2{margin-top:32px;border-bottom:2px solid #444}h3{margin:20px 0 4px}"
             ".note{color:#555;font-size:12px;margin:0 0 8px}code{font-size:12px}</style>",
             "<h1>Item offers -- {}</h1>".format(esc(str(batch_dir))),
             "<p>{} runs, {} shown cards</p>".format(total_runs, len(rows)),
             "<ul>" + "".join("<li>{}</li>".format(esc(n)) for n in notes) + "</ul>"]
    if not rows:
        parts.append("<p><b>No offer axes in this batch</b> (rooms[].offers missing or empty everywhere).</p>")
        return "\n".join(parts)
    for profile, tables in sections:
        parts.append("<h2>Profile {}</h2>".format(esc(profile)))
        for title, header, body, tnotes in tables:
            parts.append("<h3>{}</h3>".format(esc(title)))
            for n in tnotes:
                parts.append("<p class='note'>{}</p>".format(esc(n)))
            parts.append("<table><tr>" + "".join("<th>{}</th>".format(esc(h)) for h in header) + "</tr>")
            for row in body:
                parts.append("<tr>" + "".join("<td>{}</td>".format(esc(c)) for c in row) + "</tr>")
            parts.append("</table>")
    parts.append("<h2>8. Formula reference</h2><table><tr><th>file:line</th><th>what it says</th></tr>")
    for where, what in FORMULA_REFERENCE:
        parts.append("<tr><td><code>{}</code></td><td style='text-align:left'>{}</td></tr>".format(
            esc(where), esc(what)))
    parts.append("</table>")
    return "\n".join(parts)


# ---------------------------------------------------------------- main


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("batch_dir")
    parser.add_argument("--out", default=None, help="HTML path (default <batch>/offers.html)")
    args = parser.parse_args(argv)

    runs = load_runs(args.batch_dir)
    rows = offer_rows(runs)
    sections, notes = build(rows)

    text = render_text(sections, notes, args.batch_dir, len(runs), rows)
    sys.stdout.write(text)

    out = args.out or os.path.join(args.batch_dir, "offers.html")
    with open(out, "w", encoding="utf-8") as handle:
        handle.write(render_html(sections, notes, args.batch_dir, len(runs), rows))
    sys.stdout.write("\nwrote {}\n".format(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
