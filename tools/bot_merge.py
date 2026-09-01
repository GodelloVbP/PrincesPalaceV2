#!/usr/bin/env python3
"""Merge a balance-bot batch's shards into one summary.json.

A batch is now N Unity processes over disjoint seed ranges (tools/bot.ps1
-Shards N), each writing runs.jsonl / content.json / batch.json into its own
shard-<i>/ directory. This reads all of them and writes the batch's
summary.json exactly per docs/BOT_SUMMARY_SCHEMA.md, then prints the
plain-text table that used to come out of the Unity log.

WHY THIS IS NOT DONE PER SHARD AND ADDED UP. Almost nothing in summary.json
merges. A median of medians is not a median. decisionPressure's denominator
counts positions reached by two or more archetypes on the same seed, which is
only knowable once every archetype's runs are in one place. "This relic was
never offered" is only true if it was never offered in ANY shard.
buildDiversity is over the deepest tenth of a CELL, which spans shards. So
the shards emit facts and this computes every aggregate once, over all of
them.

WHY IT IS NOT ALSO IMPLEMENTED IN C#. It was, until this file existed, and
two implementations of doomedShare is exactly the pair that drifts. The
runner now emits only what it alone can know -- the party's live max HP per
fight, and the swing count already reduced against it -- and every aggregate
lives here.

Stdlib only, same as tools/bot_report.py, and it never reads traces.jsonl:
that file is archival, for digging into a seed a bug row names.

    python tools/bot_merge.py reports/bot/<timestamp>
    python tools/bot_merge.py reports/bot/<timestamp> --out somewhere/summary.json
"""

import argparse
import json
import math
import os
import sys


# ---------------------------------------------------------------- reading


def _read_jsonl(path):
    rows = []
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return rows


def _read_json(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def find_shard_dirs(batch_dir):
    """Every shard directory under a batch, in shard order.

    A single-shard batch writes straight into the batch directory rather than
    a shard-1/ under it, so -Shards 1 keeps producing the same layout it
    always did and an old batch can still be merged.
    """
    shards = sorted(
        (name for name in os.listdir(batch_dir) if name.startswith("shard-")),
        key=lambda name: int(name.split("-", 1)[1]) if name.split("-", 1)[1].isdigit() else 0,
    )
    dirs = [os.path.join(batch_dir, name) for name in shards]
    dirs = [d for d in dirs if os.path.isfile(os.path.join(d, "runs.jsonl"))]

    if not dirs and os.path.isfile(os.path.join(batch_dir, "runs.jsonl")):
        dirs = [batch_dir]

    return dirs


def load_batch(batch_dir):
    dirs = find_shard_dirs(batch_dir)
    if not dirs:
        raise SystemExit(
            "no runs.jsonl under {} (looked in the directory itself and in shard-*/)".format(batch_dir)
        )

    runs = []
    headers = []
    contents = []

    for shard in dirs:
        runs.extend(_read_jsonl(os.path.join(shard, "runs.jsonl")))
        header_path = os.path.join(shard, "batch.json")
        if os.path.isfile(header_path):
            headers.append(_read_json(header_path))
        content_path = os.path.join(shard, "content.json")
        if os.path.isfile(content_path):
            contents.append(_read_json(content_path))

    return runs, headers, contents


# ------------------------------------------------------------- small maths


def share(part, whole):
    return 0.0 if whole <= 0 else float(part) / whole


def percentile(sorted_values, q):
    """Nearest-rank, which is what a depth distribution wants.

    Every value it reports is a step somebody actually reached rather than an
    interpolation between two. Same rule the runner used before this moved.
    """
    if not sorted_values:
        return 0
    index = int(math.ceil(q * len(sorted_values))) - 1
    return sorted_values[min(len(sorted_values) - 1, max(0, index))]


def num(value):
    """Four decimal places, matching what the runner used to write.

    A float that is exactly an integer comes back as an int so summary.json
    reads `"median": 12` rather than `12.0` -- the schema's examples are
    written that way and a diff against an older batch should not light up on
    formatting.
    """
    if value is None:
        return None
    if isinstance(value, float) and (math.isnan(value) or math.isinf(value)):
        return 0
    rounded = round(float(value), 4)
    return int(rounded) if rounded == int(rounded) else rounded


def depth_of(run, cap):
    """A capped run did not die, but it did stop there."""
    return cap if run.get("capped") else run.get("deathStep", 0)


# ------------------------------------------------------------------ cells


def cell_json(runs, cap):
    depths = sorted(depth_of(r, cap) for r in runs)
    fights = [f for r in runs for f in r.get("fights", [])]

    out = {
        "runs": len(runs),
        "depth": {
            "median": num(percentile(depths, 0.5)),
            "p10": num(percentile(depths, 0.10)),
            "p90": num(percentile(depths, 0.90)),
            "mean": num(sum(depths) / float(len(depths))) if depths else 0,
            "cappedShare": num(share(sum(1 for r in runs if r.get("capped")), len(runs))),
        },
        "deathCauses": death_causes(runs),
        "doomedShare": num(doomed_share(runs)),
        "swingShare": num(
            share(sum(r.get("swingTurns", 0) for r in runs),
                  sum(r.get("swingDenomTurns", 0) for r in runs))
        ),
        "fightLength": {
            "byFloor": _mean_by(fights, lambda f: str(f["floor"]), lambda f: f["turns"]),
            "byRoomType": _mean_by(fights, lambda f: f["roomType"], lambda f: f["turns"]),
        },
        "turnOneShare": num(share(sum(1 for f in fights if f["turns"] == 1), len(fights))),
        "steamrollByFloor": _share_by(
            fights, lambda f: str(f["floor"]), lambda f: f["damageTaken"] == 0
        ),
        "consumableUseShare": num(share(sum(1 for f in fights if f.get("usedItem")), len(fights))),
        "potionsWastedMean": num(
            sum(r.get("consumablesLeft", 0) for r in runs) / float(len(runs)) if runs else 0
        ),
        "buildDiversity": build_diversity(runs, cap),
        "itemPickRate": pick_rate(
            ((room.get("offerItemIds", []), room.get("pickedIndex", -1))
             for r in runs for room in r.get("rooms", []))
        ),
        "relicPickRate": pick_rate(
            ((rnd.get("offerIds", []), rnd.get("pickedIndex", -1))
             for r in runs for rnd in r.get("relicRounds", []))
        ),
        "itemEquipRate": equip_rate(r.get("rooms", []) for r in runs),
    }
    return out


def _mean_by(rows, key_of, value_of):
    totals = {}
    counts = {}
    for row in rows:
        key = key_of(row)
        totals[key] = totals.get(key, 0) + value_of(row)
        counts[key] = counts.get(key, 0) + 1
    return {k: num(totals[k] / float(counts[k])) for k in sorted(totals)}


def _share_by(rows, key_of, predicate):
    hits = {}
    counts = {}
    for row in rows:
        key = key_of(row)
        counts[key] = counts.get(key, 0) + 1
        if predicate(row):
            hits[key] = hits.get(key, 0) + 1
    return {k: num(share(hits.get(k, 0), counts[k])) for k in sorted(counts)}


def death_causes(runs):
    """One row per (enemyId, roomType, floor) present in the fight that ended a run.

    A multi-enemy fight contributes one row per enemy standing in it: the
    trace does not record which one landed the killing blow, and inventing an
    answer would be worse than saying "these were present".
    """
    counts = {}
    parts = {}

    for run in runs:
        if run.get("capped"):
            continue
        death_step = run.get("deathStep", 0)
        fight = next((f for f in run.get("fights", []) if f["step"] == death_step), None)
        if fight is None:
            continue

        seen = []
        for enemy_id in fight.get("enemyIds", []):
            if enemy_id in seen:
                continue
            seen.append(enemy_id)
            key = "{}|{}|{}".format(enemy_id, fight["roomType"], fight["floor"])
            counts[key] = counts.get(key, 0) + 1
            parts[key] = (enemy_id, fight["roomType"], fight["floor"])

    ordered = sorted(counts, key=lambda k: (-counts[k], k))
    return [
        {
            "enemyId": parts[k][0],
            "roomType": parts[k][1],
            "floor": parts[k][2],
            "count": counts[k],
        }
        for k in ordered
    ]


def doomed_share(runs):
    """Never above half health at the end of any of the last three fights.

    A run that was never going to make it, as distinct from one that died to
    a spike. Max HP comes off runs.jsonl's per-fight partyMaxHp, read live off
    the encounter by the runner -- it is not a trace field and the schema says
    explicitly not to make it one.
    """
    denominator = 0
    doomed = 0

    for run in runs:
        fights = run.get("fights", [])
        if run.get("capped") or not fights:
            continue

        denominator += 1
        ever_healthy = False

        for fight in fights[max(0, len(fights) - 3):]:
            max_hp = fight.get("partyMaxHp", 0)
            if max_hp <= 0:
                continue
            if fight.get("partyHpOut", 0) / float(max_hp) > 0.5:
                ever_healthy = True
                break

        if not ever_healthy:
            doomed += 1

    return share(doomed, denominator)


def build_diversity(runs, cap):
    """Distinct relic/talent sets among the deepest tenth of the cell's runs.

    Ranked on the same depth figure the distribution uses (a capped run counts
    as the cap) rather than the raw death step, which reads 0 for a capped run
    and would sort the strongest runs to the bottom. The sort is stable, so
    ties keep the order the runs completed in -- which is what makes a
    two-shard batch produce the same answer as a one-shard batch over the same
    seeds.
    """
    if not runs:
        return {"distinctRelicSets": 0, "distinctTalentSets": 0, "distinctGearSets": 0}

    take = max(1, int(math.ceil(len(runs) * 0.10)))
    deepest = sorted(runs, key=lambda r: -depth_of(r, cap))[:take]

    relic_sets = {"+".join(sorted(r.get("relicIds", []))) for r in deepest}
    talent_sets = {"+".join(sorted(r.get("talentIds", []))) for r in deepest}
    # The third axis. Relics and talents are chosen from a handful of options;
    # the paperdoll is eight slots filled out of everything the run was offered,
    # so it is where most of a build's actual variation lives -- and it read as
    # nothing at all until the bot started wearing what it picked up.
    gear_sets = {"+".join(sorted(r.get("gearIds", []))) for r in deepest}
    return {
        "distinctRelicSets": len(relic_sets),
        "distinctTalentSets": len(talent_sets),
        "distinctGearSets": len(gear_sets),
    }


def equip_rate(room_lists):
    """equips / offers, per id that was ever offered.

    The companion to pick_rate, and the pair is the point. pick_rate answers
    "when this is on the table, how often is it taken"; this answers "how often
    does a copy of it end up on somebody". An item with a high pick rate and a
    zero equip rate is a trap: it looks like the best thing in the offer and is
    never worth wearing. Neither number can show that on its own.

    Denominator is OFFERS, not picks, so the two rates are read against the
    same base -- a per-pick rate would divide by a number that is itself a
    policy decision and move when the archetype changed its mind. An id never
    offered is omitted, exactly as pick_rate omits it.
    """
    offered = {}
    equipped = {}

    for rooms in room_lists:
        for room in rooms:
            for item_id in room.get("offerItemIds", []):
                if not item_id:
                    continue
                offered[item_id] = offered.get(item_id, 0) + 1
            for item_id in room.get("equippedItemIds", []):
                if not item_id:
                    continue
                equipped[item_id] = equipped.get(item_id, 0) + 1

    return {k: num(share(equipped.get(k, 0), offered[k])) for k in sorted(offered)}


def pick_rate(offer_rows):
    """picks / offers, per id that was ever offered.

    An id never offered is omitted rather than written as a 0/0 row -- it
    shows up in coverage instead, which is where "this never appeared" belongs.
    """
    offered = {}
    picked = {}

    for ids, index in offer_rows:
        for i, item_id in enumerate(ids):
            if not item_id:
                continue
            offered[item_id] = offered.get(item_id, 0) + 1
            if i == index:
                picked[item_id] = picked.get(item_id, 0) + 1

    return {k: num(share(picked.get(k, 0), offered[k])) for k in sorted(offered)}


# ------------------------------------------------------------ batch-wide


def decision_pressure(runs, archetypes):
    """A choice every archetype makes the same way is dead.

    Denominator is positions reached by two or more archetypes on the same
    seed and profile; a position only one archetype ever sees -- because an
    earlier disagreement diverged the run -- counts neither way.
    """
    if len(archetypes) < 2:
        # With one archetype there is nobody to disagree with, and a zero
        # would read as "every choice is dead" rather than "not measured".
        return {"nodes": None, "offers": None, "relics": None}

    nodes = {}
    offers = {}
    relics = {}

    for run in runs:
        key = "{}|{}".format(run["seed"], run["profile"])

        for room in run.get("rooms", []):
            nodes.setdefault("{}|{}".format(key, room["step"]), []).append(str(room["nodeId"]))

            if room.get("offerItemIds"):
                offers.setdefault(
                    "{}|{}|{}".format(key, room["step"], room["nodeId"]), []
                ).append(str(room.get("pickedIndex", -1)))

        picks = []
        for rnd in run.get("relicRounds", []):
            ids = rnd.get("offerIds", [])
            index = rnd.get("pickedIndex", -1)
            picks.append(ids[index] if 0 <= index < len(ids) else "-")
        relics.setdefault(key, []).append(">".join(picks))

    return {
        "nodes": num(_disagreement(nodes)),
        "offers": num(_disagreement(offers)),
        "relics": num(_disagreement(relics)),
    }


def _disagreement(positions):
    reached = 0
    differed = 0
    for values in positions.values():
        if len(values) < 2:
            continue
        reached += 1
        if len(set(values)) > 1:
            differed += 1
    return share(differed, reached)


def coverage(runs, content):
    """What the batch never touched, differenced against the content database.

    A relic that exists and was never drawn is the finding; a relic that is
    silently absent from the traces is not one anybody can see.
    """
    seen_enemies = {e for r in runs for f in r.get("fights", []) for e in f.get("enemyIds", [])}

    offered_relics = set()
    picked_relics = set()
    for run in runs:
        for rnd in run.get("relicRounds", []):
            ids = rnd.get("offerIds", [])
            offered_relics.update(ids)
            index = rnd.get("pickedIndex", -1)
            if 0 <= index < len(ids):
                picked_relics.add(ids[index])

    offered_items = set()
    picked_items = set()
    for run in runs:
        for room in run.get("rooms", []):
            ids = room.get("offerItemIds", [])
            offered_items.update(ids)
            index = room.get("pickedIndex", -1)
            if 0 <= index < len(ids):
                picked_items.add(ids[index])

    used_skills = {s for r in runs for s in r.get("skillsUsed", [])}

    def missing(all_ids, seen):
        return sorted(i for i in all_ids if i not in seen)

    return {
        "enemiesNeverSeen": missing(content["enemyIds"], seen_enemies),
        "relicsNeverOffered": missing(content["relicIds"], offered_relics),
        "relicsNeverPicked": missing(content["relicIds"], picked_relics),
        "itemsNeverOffered": missing(content["offerableItemIds"], offered_items),
        "itemsNeverPicked": missing(content["offerableItemIds"], picked_items),
        "skillsNeverUsed": missing(content["skillIds"], used_skills),

        # NOT A "never" LIST. This one names which of the ids above are an
        # enemy's rather than a player's, so the report can split them: enemy
        # abilities are authored in skills.json like any other skill, and
        # FightRunner only ever traces the PLAYER's commands, so every enemy
        # ability appears in skillsNeverUsed on every batch that has ever run.
        # That is noise, and it was burying the player-side gaps that are the
        # actual finding.
        "enemyAbilityIds": sorted(content.get("enemyAbilityIds", [])),
    }


def merge_content(contents):
    """The union across shards.

    Identical across shards of one batch in every normal case -- same commit,
    same content -- so the union is a no-op. It is a union rather than "trust
    the first" so a mismatched shard degrades to "everything any shard
    believed exists", which over-reports a coverage gap instead of hiding one.
    """
    keys = ["enemyIds", "relicIds", "offerableItemIds", "skillIds", "enemyAbilityIds"]
    merged = {}
    for key in keys:
        seen = []
        for content in contents:
            for value in content.get(key, []):
                if value not in seen:
                    seen.append(value)
        merged[key] = seen
    return merged


def merge_headers(headers):
    """One batch header out of N shard headers.

    runsPerCell SUMS -- each shard ran its own slice of the seed range, and the
    cell the report shows is all of them. elapsedSeconds is the slowest shard's
    wall clock, not the sum: they ran at the same time, and summing would
    report a batch that took four times as long as the user watched it take.
    """
    if not headers:
        return {}

    first = headers[0]
    return {
        "timestamp": min(h.get("timestamp", "") for h in headers),
        "commitSha": first.get("commitSha", ""),
        "runsPerCell": sum(h.get("runsPerCell", 0) for h in headers),
        "firstSeed": min(h.get("firstSeed", 0) for h in headers),
        "archetypes": first.get("archetypes", []),
        "profiles": first.get("profiles", []),
        "depthCapSteps": first.get("depthCapSteps", 0),
        "elapsedSeconds": num(max(h.get("elapsedSeconds", 0) for h in headers)),
        "shards": len(headers),
    }


# ------------------------------------------------------------------ merge


def merge(runs, headers, contents):
    batch = merge_headers(headers)
    cap = batch.get("depthCapSteps", 0)
    archetypes = batch.get("archetypes", [])
    profiles = batch.get("profiles", [])

    # CELL ORDER, NOT FILE ORDER, for everything that emits a list of rows.
    #
    # Concatenating N shards interleaves the cells: one shard writes every
    # cell for seeds 1..50, the next every cell for 51..100. Grouping by cell
    # puts each cell's runs back in seed order, which is the order a single
    # shard would have produced -- and that is what makes a two-shard bugs[]
    # or mismatches[] identical to a one-shard one rather than merely holding
    # the same rows in a different order.
    cells = []
    ordered = []
    for profile in profiles:
        for archetype in archetypes:
            in_cell = [r for r in runs if r["profile"] == profile and r["archetype"] == archetype]
            if not in_cell:
                continue
            ordered.extend(in_cell)
            cell = {"archetype": archetype, "profile": profile}
            cell.update(cell_json(in_cell, cap))
            cells.append(cell)

    # A run whose profile or archetype the header does not name would
    # otherwise vanish from bugs[] entirely. It should not happen; if it does,
    # the row is still a finding and losing it silently is the worst outcome.
    named = {id(r) for r in ordered}
    ordered.extend(r for r in runs if id(r) not in named)

    gap = []
    for profile in profiles:
        row = {"profile": profile, "archetypes": []}
        for archetype in archetypes:
            in_cell = [r for r in runs if r["profile"] == profile and r["archetype"] == archetype]
            if not in_cell:
                continue
            depths = sorted(depth_of(r, cap) for r in in_cell)
            row["archetypes"].append(
                {"archetype": archetype, "medianDepth": num(percentile(depths, 0.5))}
            )
        gap.append(row)

    bugs = []
    for run in ordered:
        for hit in run.get("bugs", []):
            row = {"invariant": hit["invariant"], "seed": run["seed"],
                   "archetype": run["archetype"], "profile": run["profile"]}
            row.update({k: hit[k] for k in ("step", "nodeId", "detail", "lastActions", "stack")
                        if k in hit})
            bugs.append(row)

    return {
        "batch": batch,
        "cells": cells,
        "decisionPressure": decision_pressure(runs, archetypes),
        "coverage": coverage(runs, merge_content(contents)),
        "archetypeGap": gap,
        "bugs": bugs,
        "determinism": {
            "checked": sum(1 for r in ordered if r.get("replayed")),
            "mismatches": [
                {"seed": r["seed"], "archetype": r["archetype"], "profile": r["profile"]}
                for r in ordered if not r.get("hashMatched", True)
            ],
        },
    }


# ------------------------------------------------------------ plain text


def plain_text(summary):
    """The same numbers, in the shape somebody reads off a terminal.

    This used to be printed by the Unity process. It cannot be any more: with
    four shards there are four processes, and each of them only knows a
    quarter of the batch.
    """
    batch = summary["batch"]
    lines = [
        "",
        "=== BALANCE BOT ===",
        "{} runs/cell x {} archetypes x {} profiles, seeds {}..{}, depth cap {}, {} shard(s), {:.1f}s".format(
            batch.get("runsPerCell", 0),
            len(batch.get("archetypes", [])),
            len(batch.get("profiles", [])),
            batch.get("firstSeed", 0),
            batch.get("firstSeed", 0) + batch.get("runsPerCell", 0) - 1,
            batch.get("depthCapSteps", 0),
            batch.get("shards", 1),
            float(batch.get("elapsedSeconds", 0)),
        ),
        "",
        "profile/archetype              runs  median  p10  p90  capped  doomed  swing",
    ]

    for cell in summary["cells"]:
        depth = cell["depth"]
        lines.append(
            "{:<30}{:>5}{:>8.1f}{:>5.0f}{:>5.0f}{:>8.0%}{:>8.0%}{:>7.0%}".format(
                cell["profile"] + "/" + cell["archetype"],
                cell["runs"],
                float(depth["median"]), float(depth["p10"]), float(depth["p90"]),
                float(depth["cappedShare"]), float(cell["doomedShare"]), float(cell["swingShare"]),
            )
        )

    by_name = {}
    example = {}
    for bug in summary["bugs"]:
        by_name[bug["invariant"]] = by_name.get(bug["invariant"], 0) + 1
        example.setdefault(bug["invariant"], bug)

    lines.append("")
    lines.append("bugs: {} hits across {} invariant(s)".format(
        sum(by_name.values()), len(by_name)))
    for name in sorted(by_name, key=lambda n: (-by_name[n], n)):
        one = example[name]
        lines.append("  {} x{}  (e.g. seed {} {}/{})".format(
            name, by_name[name], one["seed"], one["profile"], one["archetype"]))

    determinism = summary["determinism"]
    mismatches = determinism["mismatches"]
    total_runs = sum(cell["runs"] for cell in summary["cells"])
    lines.append("")
    if not mismatches:
        lines.append("determinism: {}/{} runs replayed, 0 mismatches".format(
            determinism["checked"], total_runs))
    else:
        first = mismatches[0]
        lines.append("determinism: {}/{} runs replayed, {} MISMATCHES (e.g. seed {} {}/{})".format(
            determinism["checked"], total_runs, len(mismatches),
            first["seed"], first["profile"], first["archetype"]))

    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("batch", help="a reports/bot/<timestamp> directory")
    parser.add_argument("--out", default=None,
                        help="where summary.json goes (default: <batch>/summary.json)")
    parser.add_argument("--quiet", action="store_true",
                        help="write the file without printing the plain-text table")
    args = parser.parse_args(argv)

    runs, headers, contents = load_batch(args.batch)
    summary = merge(runs, headers, contents)

    out = args.out or os.path.join(args.batch, "summary.json")

    # NO BOM and a trailing newline, the same two properties the runner's own
    # writer was careful about: json.load refuses a leading BOM outright, and
    # that broke tools/bot_report.py on the very first batch ever written.
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(summary, handle, indent=1, sort_keys=False)
        handle.write("\n")

    if not args.quiet:
        sys.stdout.write(plain_text(summary))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
