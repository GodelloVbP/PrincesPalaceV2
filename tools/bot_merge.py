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
        header_path = os.path.join(shard, "batch.json")
        head = _read_json(header_path) if os.path.isfile(header_path) else {}
        if head:
            headers.append(head)

        # THE SHOP POLICY TRAVELS ON THE RUN, not just in the header. A batch
        # directory can hold shards from both modes, and the shop-vs-no-shop
        # comparison pairs runs across them -- so a run has to be able to say
        # which half it belongs to. Under an underscore because it is not a
        # runs.jsonl field: it is copied down from the shard that wrote it,
        # and nothing outside this file reads it.
        policy = head.get("shopPolicy", "WhenOffered")
        for row in _read_jsonl(os.path.join(shard, "runs.jsonl")):
            row["_shopPolicy"] = policy
            runs.append(row)

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


# ------------------------------------------------------------------- shop


# THE FOUR BANDS GOLD FORGONE IS MEASURED IN, per docs/PLAN_SHOP.md 7.1
# point 1. A leg is eight steps, so these are legs 1, 2, 3 and "the rest",
# which is where income stops climbing (2a).
STEP_BANDS = [(1, 8), (9, 16), (17, 24), (25, 40)]

# The steps the arrival-gold table reports at. This replaces 2a's
# "cumulative won gold at depth" table: cumulative winnings are not what a
# player holds when a door opens, and GoldOnArrival is recorded for EVERY
# room precisely so the question can be asked at any depth rather than only
# where a shop happened to generate.
ARRIVAL_STEPS = [4, 8, 12, 16, 24, 32, 40]

SECTION_NAMES = ["gear", "books", "relics"]


def band_of(step):
    for low, high in STEP_BANDS:
        if low <= step <= high:
            return "{}-{}".format(low, high)
    return None


def band_key(low, high):
    return "{}-{}".format(low, high)


def shop_rooms(run):
    return [room for room in run.get("rooms", []) if room.get("roomType") == "Shop"]


def forgone_gold(runs):
    """Median won-fight payout per step band -- what a shop node cost to take.

    OVER WON FIGHTS ONLY. A lost fight pays zero, and including it would drag
    every Fresh band toward 0 at exactly the depths where the trade matters:
    the number is "the fight you did not have", and the fight you did not
    have is one you would probably have won.

    Measured inside the SAME cell, so the comparison is against this
    archetype at this profile rather than against a batch-wide average.
    """
    by_band = {}
    for run in runs:
        for fight in run.get("fights", []):
            if not fight.get("won"):
                continue
            band = band_of(fight.get("step", 0))
            if band is None:
                continue
            by_band.setdefault(band, []).append(fight.get("payoutGold", 0))

    out = {}
    for low, high in STEP_BANDS:
        key = band_key(low, high)
        values = sorted(by_band.get(key, []))
        out[key] = num(percentile(values, 0.5)) if values else None
    return out


def arrival_gold_by_step(runs):
    """p10 / p25 / median gold on arrival, at each of ARRIVAL_STEPS.

    Every room, not only shops. A step nobody in the cell reached reports
    null rather than 0 -- "never got there" and "got there broke" are
    different findings and a zero would read as the second.
    """
    at_step = {}
    for run in runs:
        for room in run.get("rooms", []):
            step = room.get("step", 0)
            if step in ARRIVAL_STEPS:
                at_step.setdefault(step, []).append(room.get("goldOnArrival", 0))

    out = {}
    for step in ARRIVAL_STEPS:
        values = sorted(at_step.get(step, []))
        out[str(step)] = None if not values else {
            "p10": num(percentile(values, 0.10)),
            "p25": num(percentile(values, 0.25)),
            "median": num(percentile(values, 0.5)),
            "n": len(values),
        }
    return out


# Steps this section reads P(learned a first spell) at -- one per boss
# (docs/PLAN_SHOP.md 7.3 Gate 3, the plan's own Phase D numbers 1-3).
SPELL_ACQUISITION_STEPS = [8, 16, 24, 32]


def _rooms_up_to(run, step):
    """Every room this run recorded at or before `step`, in order."""
    return [room for room in run.get("rooms", []) if room.get("step", 0) <= step]


def spell_acquisition_json(runs, cap):
    """Gate 3's own exit numbers (docs/PLAN_SHOP.md 7.3, Phase D 1-3).

    Every RoomTrace carries learnedSpellCountAfterRoom /
    unassignedSpellBookCountAfterRoom now (both bookOnly-inert during Phase
    A/gate 3 -- this measures ACQUISITION, not combat impact, which is the
    whole point of shipping the loop before the flip). Null fields are the
    honest answer for a cell that never reached the step in question, same
    posture arrival_gold_by_step already takes.
    """
    learned_by_step = {}
    for step in SPELL_ACQUISITION_STEPS:
        reached = 0
        learned = 0
        for run in runs:
            # REACHED means the run's own depth got there, not merely "has a
            # room recorded before this step" -- a run that died at step 5
            # has a step-4 room too, and counting it toward "reached step 8"
            # would be exactly the depth-of-8 share reading as depth-of-4.
            if depth_of(run, cap) < step:
                continue

            rooms = _rooms_up_to(run, step)
            if not rooms:
                continue
            reached += 1
            if rooms[-1].get("learnedSpellCountAfterRoom", 0) > 0:
                learned += 1

        learned_by_step[str(step)] = None if reached == 0 else {
            "share": num(share(learned, reached)),
            "n": reached,
        }

    # Slots filled per leg, at the DEEPEST room each run reached -- a leg is
    # eight steps, so this is learnedSpellCount / (depth / 8), floored at one
    # leg so a run that died on step 3 is not divided by a fraction.
    slots_per_leg = []
    for run in runs:
        rooms = run.get("rooms", [])
        if not rooms:
            continue
        depth = depth_of(run, cap)
        legs = max(1.0, depth / 8.0)
        slots_per_leg.append(rooms[-1].get("learnedSpellCountAfterRoom", 0) / legs)

    # Zero books entering leg 2 (step 9) against at least one -- "books" here
    # means ACQUIRED, learned or still unassigned either counts, since a
    # book sitting unassigned still says the loop found something.
    zero_at_leg2 = []
    some_at_leg2 = []
    for run in runs:
        rooms = _rooms_up_to(run, 8)
        if not rooms:
            continue
        last = rooms[-1]
        total = last.get("learnedSpellCountAfterRoom", 0) + last.get("unassignedSpellBookCountAfterRoom", 0)
        (some_at_leg2 if total > 0 else zero_at_leg2).append(depth_of(run, cap))

    # Shop visits that showed an unaffordable book -- the shelf's own
    # roll already excludes a book every fielded character knows, so a
    # shown-but-unaffordable book card is the honest "wanted it, could not
    # pay" reading without needing per-character eligibility replayed here.
    shop_visits_with_unaffordable_book = 0
    total_shop_visits = 0
    for run in runs:
        for room in shop_rooms(run):
            total_shop_visits += 1
            gold_in = room.get("goldOnArrival", 0)
            if any(c.get("kind") == "Book" and c.get("price", 0) > gold_in for c in room.get("shopOffers", [])):
                shop_visits_with_unaffordable_book += 1

    return {
        "learnedFirstSpellByStep": learned_by_step,
        "slotsFilledPerLegMean": num(sum(slots_per_leg) / len(slots_per_leg)) if slots_per_leg else None,
        "depthZeroBooksAtLeg2": num(percentile(sorted(zero_at_leg2), 0.5)) if zero_at_leg2 else None,
        "depthSomeBooksAtLeg2": num(percentile(sorted(some_at_leg2), 0.5)) if some_at_leg2 else None,
        "shopVisitsShowingUnaffordableBookShare": (
            None if total_shop_visits == 0
            else num(share(shop_visits_with_unaffordable_book, total_shop_visits))
        ),
    }


def shop_json(runs):
    """Everything gate 1 asks about a shop visit (docs/PLAN_SHOP.md 7.3).

    Null when the cell visited no shop at all, which is the honest answer for
    a -ShopPolicy Never batch: a block full of zeroes would read as "visits
    that bought nothing" rather than "no visits".
    """
    visits = [(run, room) for run in runs for room in shop_rooms(run)]

    forgone = forgone_gold(runs)
    by_step = arrival_gold_by_step(runs)

    if not visits:
        return {
            "visits": 0,
            "arrivalGold": None,
            "belowCheapestShare": None,
            "goldForgone": forgone,
            "arrivalGoldByStep": by_step,
        }

    arrival = sorted(room.get("goldOnArrival", 0) for _, room in visits)

    below_cheapest = 0
    zero_purchase = 0
    purchases = 0

    # SHOWN CARDS, per section: the denominator is cards still on sale when
    # the shelf was rolled, so a NO OFFER placeholder (the whole book section
    # until gate 3) never counts as an unaffordable card.
    shown = [0, 0, 0]
    affordable = [0, 0, 0]

    rerolls = [0, 0, 0]
    reroll_then_nothing = [0, 0, 0]
    spend = [0, 0, 0]

    for _, room in visits:
        gold_in = room.get("goldOnArrival", 0)
        offers = room.get("shopOffers", [])

        prices = [c.get("price", 0) for c in offers]
        if prices and gold_in < min(prices):
            below_cheapest += 1

        bought = room.get("purchasesBySection", []) or []
        rerolled = room.get("rerollsBySection", []) or []

        total_bought = sum(bought)
        purchases += total_bought
        if total_bought == 0:
            zero_purchase += 1

        for card in offers:
            section = _section_of(card.get("kind", ""))
            if section is None:
                continue
            shown[section] += 1
            if gold_in >= card.get("price", 0):
                affordable[section] += 1
            if card.get("sold"):
                spend[section] += card.get("price", 0)

        for section in range(len(SECTION_NAMES)):
            used = rerolled[section] if section < len(rerolled) else 0
            rerolls[section] += used

            # "PAID TO LOOK AGAIN AND STILL WALKED", counted per VISIT rather
            # than per reroll: a section rerolled twice and then bought from
            # is one satisfied decision, not one satisfied and one wasted.
            if used > 0 and (section >= len(bought) or bought[section] == 0):
                reroll_then_nothing[section] += 1

    n = len(visits)
    spend_total = sum(spend)

    return {
        "visits": n,
        "arrivalGold": {
            "p10": num(percentile(arrival, 0.10)),
            "p25": num(percentile(arrival, 0.25)),
            "median": num(percentile(arrival, 0.5)),
            "p75": num(percentile(arrival, 0.75)),
        },
        "belowCheapestShare": num(share(below_cheapest, n)),
        "goldForgone": forgone,
        "purchasesPerVisit": num(share(purchases, n)),
        "zeroPurchaseShare": num(share(zero_purchase, n)),
        "affordableShareBySection": {
            SECTION_NAMES[i]: (None if shown[i] == 0 else num(share(affordable[i], shown[i])))
            for i in range(len(SECTION_NAMES))
        },
        "rerollsPerVisitBySection": {
            SECTION_NAMES[i]: num(share(rerolls[i], n)) for i in range(len(SECTION_NAMES))
        },
        "rerollThenNoPurchaseShare": {
            SECTION_NAMES[i]: (None if rerolls[i] == 0 else num(share(reroll_then_nothing[i], n)))
            for i in range(len(SECTION_NAMES))
        },
        "spendShareBySection": {
            SECTION_NAMES[i]: (None if spend_total == 0 else num(share(spend[i], spend_total)))
            for i in range(len(SECTION_NAMES))
        },
        "goldOnLeaveMedian": num(percentile(sorted(room.get("goldOnLeave", 0) for _, room in visits), 0.5)),

        # WHAT THE RUN WAS CARRYING WHEN IT ENDED, read off the LAST room's
        # arrival gold. The run snapshot is gone by the time anything could
        # ask -- RunManager.EndRun replaces it -- and the last room the party
        # walked into is the closest honest reading: gold unspent at the
        # moment it stopped mattering.
        "goldAtDeathMedian": num(percentile(
            sorted(run["rooms"][-1].get("goldOnArrival", 0) for run in runs if run.get("rooms")), 0.5)),
        "arrivalGoldByStep": by_step,
    }


def _section_of(kind):
    """ShopEntryKind's name -> ShopStock's section index."""
    if kind == "Gear":
        return 0
    if kind == "Book":
        return 1
    if kind == "Relic":
        return 2
    return None


def shop_vs_no_shop(runs, cap):
    """Paired runs, one per mode, differing in exactly one decision.

    PAIRED BY (seed, archetype, profile) and never by position: the two
    batches play the same seeds, and a run present in one mode and missing
    from the other -- a shard that died, a cell that was not asked for --
    would otherwise shift every comparison after it by one.

    Null when the runs come from only one mode, which is the normal case for
    a single batch. Reporting zeroes there would read as "taking a shop
    changes nothing" rather than as "not measured".
    """
    by_mode = {}
    for run in runs:
        by_mode.setdefault(run.get("_shopPolicy", "WhenOffered"), {})[
            (run.get("seed"), run.get("archetype"), run.get("profile"))] = run

    if len(by_mode) < 2 or "WhenOffered" not in by_mode or "Never" not in by_mode:
        return None

    taken = by_mode["WhenOffered"]
    baseline = by_mode["Never"]
    keys = sorted(set(taken) & set(baseline), key=lambda k: (str(k[2]), str(k[1]), k[0]))
    if not keys:
        return None

    depths = {"WhenOffered": [], "Never": []}
    survived = {"WhenOffered": 0, "Never": 0}
    boss_pairs = 0

    for key in keys:
        pair = {"WhenOffered": taken[key], "Never": baseline[key]}
        for mode, run in pair.items():
            depths[mode].append(depth_of(run, cap))

        # SURVIVAL TO THE NEXT BOSS, MEASURED FROM THE SHOP. A pair with no
        # shop visit on the WhenOffered side has nothing to compare -- the two
        # runs are the same run -- so it counts in neither numerator nor
        # denominator.
        visits = shop_rooms(pair["WhenOffered"])
        if not visits:
            continue

        after = visits[0].get("step", 0)
        boss_pairs += 1
        for mode, run in pair.items():
            if _won_a_boss_after(run, after):
                survived[mode] += 1

    return {
        "pairs": len(keys),
        "pairsWithAShopVisit": boss_pairs,
        "medianDepth": {
            "WhenOffered": num(percentile(sorted(depths["WhenOffered"]), 0.5)),
            "Never": num(percentile(sorted(depths["Never"]), 0.5)),
            "delta": num(percentile(sorted(depths["WhenOffered"]), 0.5)
                         - percentile(sorted(depths["Never"]), 0.5)),
        },
        "survivalToNextBossShare": {
            "WhenOffered": num(share(survived["WhenOffered"], boss_pairs)),
            "Never": num(share(survived["Never"], boss_pairs)),
        },
        "depthVariance": {
            "WhenOffered": num(_variance(depths["WhenOffered"])),
            "Never": num(_variance(depths["Never"])),
        },
    }


def _won_a_boss_after(run, step):
    return any(f.get("roomType") == "Boss" and f.get("won") and f.get("step", 0) > step
               for f in run.get("fights", []))


def _variance(values):
    if len(values) < 2:
        return 0
    mean = sum(values) / float(len(values))
    return sum((v - mean) ** 2 for v in values) / float(len(values))


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
        "shop": shop_json(runs),
        "spellAcquisition": spell_acquisition_json(runs, cap),
        "knells": knells_json(fights),
    }
    return out


KNELL_SEATS = ("front", "middle", "rear")
KNELL_ANSWERS = ("none", "step", "passage")


def knells_json(fights):
    """The seat-sized hits (the Bellwether's Death Knell) this cell's fights saw.

    Per seat as it LANDED: how many landed and how many the target did not
    survive -- the M7 contract is written per seat (front kills, middle is
    survivable, rear is nothing), so pooling seats would hide the only split
    that matters. answeredBy is the share of landed knells the shared
    telegraph answer stepped or passaged away from, which under
    -NoTelegraphAnswer is all "none" by construction. A seat past the rear
    (a party member shoved further back than three) counts as rear, the same
    reading SeatSizedDamageBase makes. Shares are null when nothing landed:
    0.0 would claim "never answered", which no knell happened to test.
    """
    rows = [k for f in fights for k in (f.get("knells") or [])]
    by_seat = {}
    for index, name in enumerate(KNELL_SEATS):
        in_seat = [k for k in rows
                   if min(max(k.get("seat", 0), 0), len(KNELL_SEATS) - 1) == index]
        by_seat[name] = {
            "landed": len(in_seat),
            "deaths": sum(1 for k in in_seat if not k.get("survived", True)),
        }
    answered = {
        name: (num(share(sum(1 for k in rows if k.get("answeredBy", "none") == name), len(rows)))
               if rows else None)
        for name in KNELL_ANSWERS
    }
    return {
        "landed": len(rows),
        "deaths": sum(1 for k in rows if not k.get("survived", True)),
        "bySeat": by_seat,
        "answeredBy": answered,
    }


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

        # A LIST, not a string. One batch is normally one mode, but merging
        # two batch directories to compare them puts both here -- and a
        # header that named only the first would misdescribe half its own
        # runs.
        "shopPolicies": sorted({h.get("shopPolicy", "WhenOffered") for h in headers}),
    }


# ------------------------------------------------------------------ merge


def merge(runs, headers, contents):
    batch = merge_headers(headers)
    cap = batch.get("depthCapSteps", 0)

    # THE SECOND BATCH IS A COMPARISON, NOT A BIGGER SAMPLE. Every cell
    # metric -- depth, doomedShare, the shop block -- is a claim about one
    # mode, and pooling two modes into one median would answer a question
    # nobody asked. So the cells are computed over the FIRST batch's mode
    # only, and shopVsNoShop is the one thing that sees both.
    all_runs = runs
    primary = (headers[0].get("shopPolicy", "WhenOffered") if headers else "WhenOffered")
    if len({r.get("_shopPolicy", "WhenOffered") for r in runs}) > 1:
        runs = [r for r in runs if r.get("_shopPolicy", "WhenOffered") == primary]
        batch["shopPolicy"] = primary
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
        "shopVsNoShop": shop_vs_no_shop(all_runs, cap),
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

    shop_cells = [c for c in summary["cells"] if (c.get("shop") or {}).get("visits")]
    if shop_cells:
        lines.append("")
        lines.append("shop visits                    n   arrive p10/p25/med   <cheapest  buys/visit  0-buy")
        for cell in shop_cells:
            shop = cell["shop"]
            gold = shop["arrivalGold"]
            lines.append(
                "{:<30}{:>4}{:>8.0f}/{:.0f}/{:.0f}{:>11.0%}{:>12.2f}{:>7.0%}".format(
                    cell["profile"] + "/" + cell["archetype"],
                    shop["visits"],
                    float(gold["p10"]), float(gold["p25"]), float(gold["median"]),
                    float(shop["belowCheapestShare"]),
                    float(shop["purchasesPerVisit"]),
                    float(shop["zeroPurchaseShare"]),
                )
            )

    versus = summary.get("shopVsNoShop")
    if versus:
        lines.append("")
        lines.append(
            "shop vs no shop: {} paired runs ({} with a visit), median depth {} vs {} (delta {})".format(
                versus["pairs"], versus["pairsWithAShopVisit"],
                versus["medianDepth"]["WhenOffered"], versus["medianDepth"]["Never"],
                versus["medianDepth"]["delta"]))
        lines.append("  survival to the next boss after the first shop: {:.0%} vs {:.0%}".format(
            float(versus["survivalToNextBossShare"]["WhenOffered"]),
            float(versus["survivalToNextBossShare"]["Never"])))

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
    parser.add_argument("--compare", default=None,
                        help="a second reports/bot/<timestamp> directory, run over the same "
                             "seeds with the other -ShopPolicy mode; its runs are folded in "
                             "so shopVsNoShop can pair them")
    args = parser.parse_args(argv)

    runs, headers, contents = load_batch(args.batch)

    if args.compare:
        # FOLDED IN, NOT MERGED SIDE BY SIDE. Every cell metric would be
        # meaningless over two modes at once, so the second batch contributes
        # to shopVsNoShop and to nothing else -- see merge().
        other_runs, other_headers, other_contents = load_batch(args.compare)
        runs = runs + other_runs
        headers = headers + other_headers
        contents = contents + other_contents

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
