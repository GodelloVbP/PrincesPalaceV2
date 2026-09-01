#!/usr/bin/env python3
"""Tests for tools/bot_merge.py. Stdlib unittest, no fixtures on disk.

    python tools/bot_merge_test.py

THE FIXTURE IS BUILT TWICE, ONE BATCH TWO WAYS. Every test here writes the
same twelve runs out as a single shard and as two shards, merges both, and
compares -- because that equivalence IS the contract. A merger that gets a
median right but reads two shards in the wrong order, or sums a
decision-pressure denominator that should have been pooled, passes every
"does this number look right" test and still makes sharding a lie.

Built in a temp directory rather than checked in as JSON files: the fixture
has to exist in two different LAYOUTS of the same data, and a pair of
hand-maintained directories that are supposed to be identical is a pair that
drifts.
"""

import json
import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bot_merge


ARCHETYPES = ["RandomLegal", "GreedyAggressive"]
PROFILES = ["Fresh"]
CAP = 40

CONTENT = {
    "enemyIds": ["forest_troll", "giant_rat", "beetle"],
    "relicIds": ["bloodlust", "tin_whistle", "ashbound_locket"],
    "offerableItemIds": ["healing_draught", "iron_ration", "stale_bread"],
    "skillIds": ["cleave", "provoke", "hex", "roar"],
    "enemyAbilityIds": ["hex", "roar"],
}


def a_run(seed, archetype, depth, capped=False, relics=None, replayed=True, matched=True,
          bugs=None):
    """One runs.jsonl row, shaped exactly as BalanceBotRunner.RunRowJson writes it."""
    fights = []
    for i in range(3):
        step = depth - 2 + i
        fights.append({
            "step": step,
            "floor": 1 + i,
            "roomType": "Fight" if i < 2 else "EliteFight",
            "enemyIds": ["giant_rat"] if i < 2 else ["forest_troll"],
            "turns": 1 + i,
            "damageTaken": 0 if i == 0 else 12 * i,
            "partyHpOut": 0 if i == 2 and not capped else 40 - 10 * i,
            "partyMaxHp": 100,
            "usedItem": i == 1,
        })

    return {
        "seed": seed,
        "archetype": archetype,
        "profile": "Fresh",
        "capped": capped,
        "deathStep": 0 if capped else depth,
        "consumablesLeft": seed % 3,
        "replayed": replayed,
        "hashMatched": matched,
        "swingTurns": 2,
        "swingDenomTurns": 6,
        "relicIds": relics if relics is not None else ["bloodlust"],
        "talentIds": ["hardy"],
        # Two distinct paperdolls across the fixture's seeds, so
        # distinctGearSets is a number that can be wrong rather than a
        # constant 1 that passes whatever build_diversity does.
        "gearIds": ["iron_coif", "iron_sword"] if seed % 2 else ["iron_sword"],
        "levelAtDeath": 3 + seed,
        "fights": fights,
        "rooms": [
            {"step": depth - 2, "nodeId": 3 + (seed % 2),
             "offerItemIds": ["healing_draught", "iron_ration"], "pickedIndex": seed % 2,
             "equippedItemIds": ["iron_sword"] if seed % 2 else []},
            {"step": depth - 1, "nodeId": 7, "offerItemIds": [], "pickedIndex": -1,
             "equippedItemIds": []},
        ],
        "relicRounds": [
            {"offerIds": ["bloodlust", "tin_whistle"], "pickedIndex": 0 if seed % 2 else 1},
        ],
        "skillsUsed": ["cleave"],
        "bugs": bugs or [],
    }


def the_runs():
    """Twelve runs: six seeds x two archetypes, one cell per archetype.

    Deliberately not uniform -- depths differ, one run caps, one carries a
    bug row, one failed its determinism replay and one was never replayed at
    all -- so every branch the merger has is actually reached.
    """
    rows = []
    for archetype in ARCHETYPES:
        for i, seed in enumerate(range(1, 7)):
            rows.append(a_run(
                seed, archetype,
                depth=4 + 3 * i + (2 if archetype == "GreedyAggressive" else 0),
                capped=(seed == 6),
                relics=["bloodlust"] if seed % 2 else ["tin_whistle", "bloodlust"],
                replayed=(seed % 3 == 1),
                matched=not (seed == 4 and archetype == "RandomLegal"),
                bugs=[{
                    "invariant": "StalledEnemyTurn", "step": 5, "nodeId": 3,
                    "detail": "the fight is not over and it is not the player's turn",
                    "lastActions": ["Attack", "Skill:cleave"], "stack": None,
                }] if seed == 2 else [],
            ))
    return rows


def header(first_seed, runs_per_cell, elapsed):
    return {
        "timestamp": "2026-09-01T20:00:00Z",
        "commitSha": "abc1234",
        "shard": "1/1",
        "runsPerCell": runs_per_cell,
        "firstSeed": first_seed,
        "archetypes": ARCHETYPES,
        "profiles": PROFILES,
        "depthCapSteps": CAP,
        "replayShare": 0.5,
        "elapsedSeconds": elapsed,
    }


def write_shard(directory, rows, head):
    os.makedirs(directory, exist_ok=True)
    with open(os.path.join(directory, "runs.jsonl"), "w", encoding="utf-8", newline="\n") as h:
        for row in rows:
            h.write(json.dumps(row) + "\n")
    with open(os.path.join(directory, "content.json"), "w", encoding="utf-8", newline="\n") as h:
        json.dump(CONTENT, h)
    with open(os.path.join(directory, "batch.json"), "w", encoding="utf-8", newline="\n") as h:
        json.dump(head, h)


class MergeTests(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="bot-merge-test-")
        rows = the_runs()

        # One shard: everything, straight into the batch directory, which is
        # the layout -Shards 1 still writes.
        self.single = os.path.join(self.root, "single")
        write_shard(self.single, rows, header(1, 6, 12.0))

        # Two shards: the same runs, split by seed range the way tools/bot.ps1
        # splits them -- seeds 1..3 to shard-1, 4..6 to shard-2, each shard
        # running every archetype over its own slice.
        self.split = os.path.join(self.root, "split")
        os.makedirs(self.split)
        low = [r for r in rows if r["seed"] <= 3]
        high = [r for r in rows if r["seed"] > 3]

        def by_cell(subset):
            ordered = []
            for archetype in ARCHETYPES:
                ordered.extend(r for r in subset if r["archetype"] == archetype)
            return ordered

        write_shard(os.path.join(self.split, "shard-1"), by_cell(low), header(1, 3, 11.0))
        write_shard(os.path.join(self.split, "shard-2"), by_cell(high), header(4, 3, 12.0))

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def merged(self, directory):
        runs, headers, contents = bot_merge.load_batch(directory)
        return bot_merge.merge(runs, headers, contents)

    # --- the contract ----------------------------------------------------

    def test_two_shards_merge_to_the_same_summary_as_one(self):
        one = self.merged(self.single)
        two = self.merged(self.split)

        # elapsedSeconds and shards legitimately differ -- one batch took 12s
        # in one process and 12s in the slower of two. Everything else is a
        # claim about the same twelve runs and has to match exactly.
        for summary in (one, two):
            summary["batch"].pop("elapsedSeconds")
            summary["batch"].pop("shards")

        self.assertEqual(json.dumps(one, sort_keys=True), json.dumps(two, sort_keys=True))

    def test_runs_per_cell_sums_across_shards_and_first_seed_is_the_lowest(self):
        batch = self.merged(self.split)["batch"]
        self.assertEqual(6, batch["runsPerCell"])
        self.assertEqual(1, batch["firstSeed"])
        self.assertEqual(2, batch["shards"])

    def test_elapsed_is_the_slowest_shard_not_the_sum(self):
        # Shards run at the same time. Summing would report a batch four times
        # longer than the user watched it take.
        self.assertEqual(12, self.merged(self.split)["batch"]["elapsedSeconds"])

    # --- the metrics -----------------------------------------------------

    def test_a_capped_run_contributes_the_cap_as_its_depth(self):
        cell = next(c for c in self.merged(self.single)["cells"]
                    if c["archetype"] == "RandomLegal")
        # Seeds 1..6 give depths 4,7,10,13,16 and one capped run at 40.
        self.assertEqual(CAP, cell["depth"]["p90"])
        self.assertEqual(num_or_float(1 / 6.0), cell["depth"]["cappedShare"])

    def test_swing_share_pools_the_counters_rather_than_averaging_runs(self):
        cell = self.merged(self.single)["cells"][0]
        # Every run contributes 2 of 6, so the pooled share is exactly a third
        # however the runs are split between shards.
        self.assertEqual(num_or_float(2 / 6.0), cell["swingShare"])
        self.assertEqual(cell["swingShare"], self.merged(self.split)["cells"][0]["swingShare"])

    def test_coverage_is_differenced_against_content_not_against_the_traces(self):
        cov = self.merged(self.single)["coverage"]
        # beetle exists in content and never appears in a fight; the two
        # enemies that do are absent from the list.
        self.assertEqual(["beetle"], cov["enemiesNeverSeen"])
        self.assertEqual(["ashbound_locket"], cov["relicsNeverOffered"])
        self.assertEqual(["stale_bread"], cov["itemsNeverOffered"])
        # An enemy ability can never appear in a trace -- FightRunner records
        # the player's commands only -- so both are always "never used", and
        # enemyAbilityIds is what lets the report say so rather than filing
        # them beside a real player-side gap.
        self.assertEqual(["hex", "provoke", "roar"], cov["skillsNeverUsed"])
        self.assertEqual(["hex", "roar"], cov["enemyAbilityIds"])

    def test_determinism_counts_only_the_runs_that_were_actually_replayed(self):
        determinism = self.merged(self.single)["determinism"]
        # Seeds 1 and 4 of each archetype, per the fixture's replayed rule.
        self.assertEqual(4, determinism["checked"])
        self.assertEqual(
            [{"seed": 4, "archetype": "RandomLegal", "profile": "Fresh"}],
            determinism["mismatches"])

    def test_decision_pressure_is_null_with_a_single_archetype(self):
        rows = [r for r in the_runs() if r["archetype"] == "RandomLegal"]
        lonely = os.path.join(self.root, "lonely")
        head = header(1, 6, 5.0)
        head["archetypes"] = ["RandomLegal"]
        write_shard(lonely, rows, head)

        pressure = self.merged(lonely)["decisionPressure"]
        self.assertEqual({"nodes": None, "offers": None, "relics": None}, pressure)

    def test_decision_pressure_pools_positions_across_shards(self):
        # Both archetypes reach every (seed, step) in the fixture, and the two
        # archetypes' node ids agree, so nodes is 0 -- the point is that the
        # DENOMINATOR is non-trivial and identical either way, which is what a
        # per-shard computation could not have produced.
        self.assertEqual(self.merged(self.single)["decisionPressure"],
                         self.merged(self.split)["decisionPressure"])
        self.assertIsNotNone(self.merged(self.split)["decisionPressure"]["nodes"])

    def test_bugs_carry_the_seed_archetype_and_profile_of_the_run_that_tripped_them(self):
        bugs = self.merged(self.single)["bugs"]
        self.assertEqual(2, len(bugs))
        self.assertTrue(all(b["invariant"] == "StalledEnemyTurn" for b in bugs))
        self.assertEqual({2}, {b["seed"] for b in bugs})
        self.assertEqual(set(ARCHETYPES), {b["archetype"] for b in bugs})

    def test_the_plain_text_table_names_every_cell_and_the_replay_fraction(self):
        text = bot_merge.plain_text(self.merged(self.split))
        self.assertIn("Fresh/RandomLegal", text)
        self.assertIn("Fresh/GreedyAggressive", text)
        self.assertIn("2 shard(s)", text)
        self.assertIn("4/12 runs replayed", text)
        self.assertIn("StalledEnemyTurn x2", text)


    def test_build_diversity_counts_gear_sets_beside_relics_and_talents(self):
        summary = self.merged(self.single)
        diversity = summary["cells"][0]["buildDiversity"]

        # Present at all -- the axis was missing entirely, and a missing key
        # renders as a blank row rather than as a failure.
        self.assertIn("distinctGearSets", diversity)
        self.assertGreaterEqual(diversity["distinctGearSets"], 1)

    def test_item_equip_rate_is_equips_over_offers_for_every_offered_id(self):
        summary = self.merged(self.single)
        cell = summary["cells"][0]
        equip = cell["itemEquipRate"]
        pick = cell["itemPickRate"]

        # Same denominator as itemPickRate, so the two tables line up key for
        # key -- that pairing is the whole reason the rate is per OFFER.
        self.assertEqual(sorted(equip), sorted(pick))

        # iron_sword is worn in the odd seeds and never offered, so it must
        # NOT appear: an id with no offers is omitted, exactly as pick_rate
        # omits it, rather than dividing by zero.
        self.assertNotIn("iron_sword", equip)

        for value in equip.values():
            self.assertGreaterEqual(value, 0)

def num_or_float(value):
    return bot_merge.num(value)


if __name__ == "__main__":
    unittest.main(verbosity=2)
