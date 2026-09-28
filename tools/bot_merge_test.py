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
            # The last of the three is the fight that killed the run, so it
            # pays nothing -- which is what makes goldForgone's "won fights
            # only" rule a rule the fixture can actually break.
            "won": not (i == 2 and not capped),
            "payoutGold": 0 if (i == 2 and not capped) else 20 + 2 * i,
            # The Bellwether's knells (knells_json): every run steps a middle
            # knell in its second fight and survives it; even seeds also take
            # an unanswered front knell in the fight that ends the run.
            "knells": ([{"seat": 1, "survived": True, "answeredBy": "step"}] if i == 1 else [])
                      + ([{"seat": 0, "survived": False, "answeredBy": "none"}]
                         if i == 2 and seed % 2 == 0 else []),
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
            {"step": depth - 2, "nodeId": 3 + (seed % 2), "roomType": "Fight",
             "offerItemIds": ["healing_draught", "iron_ration"], "pickedIndex": seed % 2,
             "equippedItemIds": ["iron_sword"] if seed % 2 else [],
             "goldOnArrival": 40 * seed, "goldSpent": 0, "goldOnLeave": 0,
             "purchasesBySection": [], "rerollsBySection": [],
             "shopOffers": [], "shopChoices": []},
            shop_room(seed, depth - 1),
        ],
        "relicRounds": [
            {"offerIds": ["bloodlust", "tin_whistle"], "pickedIndex": 0 if seed % 2 else 1},
        ],
        "skillsUsed": ["cleave"],
        "bugs": bugs or [],
    }


def shop_room(seed, step):
    """One shop visit, shaped as BalanceBotRunner writes a Shop RoomTrace.

    Deliberately uneven across seeds: the odd seeds arrive with 100 and buy
    the cheap gear card, the even seeds arrive with 10 and can afford nothing
    -- so belowCheapestShare, zeroPurchaseShare and the per-section
    affordability shares are all numbers that can be wrong rather than
    constants. Seed 3 also rerolls the gear shelf and still buys nothing,
    which is the one row rerollThenNoPurchaseShare exists to count.
    """
    rich = seed % 2 == 1
    rerolled = seed == 3
    gold_in = 100 if rich else 10
    bought = rich and not rerolled

    return {
        "step": step,
        "nodeId": 11,
        "roomType": "Shop",
        "offerItemIds": [],
        "pickedIndex": -1,
        "equippedItemIds": [],
        "goldOnArrival": gold_in,
        "goldSpent": 24 if bought else (15 if rerolled else 0),
        "goldOnLeave": gold_in - (24 if bought else (15 if rerolled else 0)),
        "purchasesBySection": [1 if bought else 0, 0, 0],
        "rerollsBySection": [1 if rerolled else 0, 0, 0],
        "shopOffers": [
            {"kind": "Gear", "contentId": "iron_coif", "price": 24, "sold": bought},
            {"kind": "Gear", "contentId": "iron_sword", "price": 44, "sold": False},
            {"kind": "Relic", "contentId": "bloodlust", "price": 190, "sold": False},
        ],
        "shopChoices": (
            [{"kind": "BuyGear", "section": 0, "index": 0, "goldDelta": -24,
              "outcome": "Ok", "refusal": "None"}] if bought else []
        ) + [{"kind": "Leave", "section": -1, "index": -1, "goldDelta": 0,
              "outcome": "Leave", "refusal": "None"}],
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


    # --- the shop block --------------------------------------------------

    def test_the_shop_block_is_identical_across_shard_layouts(self):
        one = self.merged(self.single)["cells"][0]["shop"]
        two = self.merged(self.split)["cells"][0]["shop"]
        self.assertEqual(json.dumps(one, sort_keys=True), json.dumps(two, sort_keys=True))

    def test_arrival_gold_percentiles_come_off_shop_visits_only(self):
        shop = self.merged(self.single)["cells"][0]["shop"]

        # Six visits per cell: three at 100 (odd seeds) and three at 10.
        self.assertEqual(6, shop["visits"])
        self.assertEqual(10, shop["arrivalGold"]["p25"])
        self.assertEqual(10, shop["arrivalGold"]["median"])
        self.assertEqual(100, shop["arrivalGold"]["p75"])

    def test_below_cheapest_and_zero_purchase_are_counted_per_visit(self):
        shop = self.merged(self.single)["cells"][0]["shop"]

        # The three even seeds arrive with 10 against a cheapest card of 24.
        self.assertEqual(num_or_float(3 / 6.0), shop["belowCheapestShare"])
        # Seeds 1 and 5 buy; seed 3 rerolls and buys nothing; the evens cannot.
        self.assertEqual(num_or_float(2 / 6.0), shop["purchasesPerVisit"])
        self.assertEqual(num_or_float(4 / 6.0), shop["zeroPurchaseShare"])

    def test_affordability_is_per_section_and_against_arrival_gold(self):
        shop = self.merged(self.single)["cells"][0]["shop"]

        # Gear: 12 cards shown (two a visit). The three rich visits arrive
        # with 100 and can afford both cards; the three poor ones arrive with
        # 10 and can afford neither.
        self.assertEqual(num_or_float(6 / 12.0), shop["affordableShareBySection"]["gear"])
        # A relic at 190 is never affordable in this fixture.
        self.assertEqual(0, shop["affordableShareBySection"]["relics"])
        # The book shelf shows nothing at all until gate 3, and "no cards" is
        # null rather than a zero that would read as "never affordable".
        self.assertIsNone(shop["affordableShareBySection"]["books"])

    def test_a_reroll_that_bought_nothing_is_counted(self):
        shop = self.merged(self.single)["cells"][0]["shop"]

        self.assertEqual(num_or_float(1 / 6.0), shop["rerollsPerVisitBySection"]["gear"])
        self.assertEqual(num_or_float(1 / 6.0), shop["rerollThenNoPurchaseShare"]["gear"])
        # No reroll of the relic shelf at all -- null, not zero, since "never
        # rerolled" and "rerolled and always bought" are different findings.
        self.assertIsNone(shop["rerollThenNoPurchaseShare"]["relics"])

    def test_spend_share_is_of_gold_actually_spent_on_cards(self):
        shop = self.merged(self.single)["cells"][0]["shop"]
        self.assertEqual(1, shop["spendShareBySection"]["gear"])
        self.assertEqual(0, shop["spendShareBySection"]["relics"])

    def test_gold_forgone_is_the_median_won_fight_payout_in_the_band(self):
        shop = self.merged(self.single)["cells"][0]["shop"]

        # The fixture's won fights pay 20 and 22; the lost one pays 0 and is
        # excluded, which is the whole rule.
        for band, value in shop["goldForgone"].items():
            if value is not None:
                self.assertIn(value, (20, 22), band)

    def test_arrival_gold_by_step_reports_null_for_a_depth_nobody_reached(self):
        by_step = self.merged(self.single)["cells"][0]["shop"]["arrivalGoldByStep"]

        # RandomLegal's deepest fixture run caps at 40 but records rooms only
        # at depth-2 and depth-1, so most of the table is empty -- and empty
        # must read as "not measured", not as "arrived with nothing".
        self.assertIn("40", by_step)
        self.assertTrue(any(v is None for v in by_step.values()))

    # --- shop vs no shop ---------------------------------------------------

    def test_shop_vs_no_shop_is_null_for_a_single_mode_batch(self):
        self.assertIsNone(self.merged(self.single)["shopVsNoShop"])

    def test_shop_vs_no_shop_pairs_the_two_modes_by_seed(self):
        baseline = os.path.join(self.root, "never")
        head = header(1, 6, 12.0)
        head["shopPolicy"] = "Never"

        # The baseline dies two steps shallower on every seed and visits no
        # shop, which is what the mode means.
        rows = []
        for row in the_runs():
            copy = json.loads(json.dumps(row))
            copy["deathStep"] = max(1, copy["deathStep"] - 2)
            copy["rooms"] = [r for r in copy["rooms"] if r["roomType"] != "Shop"]
            rows.append(copy)
        write_shard(baseline, rows, head)

        runs, headers, contents = bot_merge.load_batch(self.single)
        other, other_headers, other_contents = bot_merge.load_batch(baseline)
        summary = bot_merge.merge(runs + other, headers + other_headers,
                                  contents + other_contents)

        versus = summary["shopVsNoShop"]
        self.assertEqual(12, versus["pairs"])
        self.assertEqual(12, versus["pairsWithAShopVisit"])
        self.assertGreater(versus["medianDepth"]["delta"], 0)

        # And the CELLS still describe one mode only -- pooling two modes into
        # one median would answer a question nobody asked.
        self.assertEqual(["Never", "WhenOffered"], summary["batch"]["shopPolicies"])
        self.assertEqual("WhenOffered", summary["batch"]["shopPolicy"])
        self.assertEqual(6, summary["cells"][0]["runs"])

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

    def test_knells_are_counted_per_seat_with_deaths_and_answer_shares(self):
        # Per cell: six middle knells (all survived, all stepped) and three
        # front knells from seeds 2/4/6 (all fatal, none answered).
        for directory in (self.single, self.split):
            knells = self.merged(directory)["cells"][0]["knells"]
            self.assertEqual(9, knells["landed"])
            self.assertEqual(3, knells["deaths"])
            self.assertEqual({"landed": 3, "deaths": 3}, knells["bySeat"]["front"])
            self.assertEqual({"landed": 6, "deaths": 0}, knells["bySeat"]["middle"])
            self.assertEqual({"landed": 0, "deaths": 0}, knells["bySeat"]["rear"])
            self.assertEqual(0.3333, knells["answeredBy"]["none"])
            self.assertEqual(0.6667, knells["answeredBy"]["step"])
            self.assertEqual(0, knells["answeredBy"]["passage"])

    def test_a_cell_with_no_knells_reports_null_shares_not_zero(self):
        knells = bot_merge.knells_json([{"knells": []}, {}])
        self.assertEqual(0, knells["landed"])
        self.assertEqual({"none": None, "step": None, "passage": None}, knells["answeredBy"])
        self.assertEqual({"landed": 0, "deaths": 0}, knells["bySeat"]["middle"])

    def test_a_seat_past_the_rear_counts_as_rear(self):
        knells = bot_merge.knells_json([{"knells": [{"seat": 4, "survived": True, "answeredBy": "passage"}]}])
        self.assertEqual({"landed": 1, "deaths": 0}, knells["bySeat"]["rear"])
        self.assertEqual(1, knells["answeredBy"]["passage"])


def num_or_float(value):
    return bot_merge.num(value)


def _room(step, learned=0, unassigned=0, shop_offers=None, gold_in=0, room_type="Fight"):
    return {
        "step": step,
        "roomType": room_type,
        "learnedSpellCountAfterRoom": learned,
        "unassignedSpellBookCountAfterRoom": unassigned,
        "goldOnArrival": gold_in,
        "shopOffers": shop_offers or [],
    }


def _run(rooms, capped=False, death_step=0):
    return {"capped": capped, "deathStep": death_step, "rooms": rooms}


class SpellAcquisitionTests(unittest.TestCase):
    """Gate 3's own exit numbers (docs/PLAN_SHOP.md 7.3, Phase D 1-3).

    Pure-function tests against bot_merge.spell_acquisition_json directly --
    the shape it reads (rooms[].learnedSpellCountAfterRoom etc.) is simple
    enough not to need a whole batch/shard fixture to exercise honestly.
    """

    def test_learned_first_spell_by_step_counts_runs_that_reached_it(self):
        runs = [
            # Died AT step 8, having just learned a spell there.
            _run([_room(4, learned=0), _room(8, learned=1)], death_step=8),
            # Died at step 8 too, nothing learned.
            _run([_room(4, learned=0), _room(8, learned=0)], death_step=8),
            # Died at step 5 -- never reaches step 8, excluded from that
            # step's denominator entirely rather than counted as "no".
            _run([_room(4, learned=0)], death_step=5),
        ]

        out = bot_merge.spell_acquisition_json(runs, cap=40)

        self.assertEqual(2, out["learnedFirstSpellByStep"]["8"]["n"])
        self.assertEqual(num_or_float(0.5), out["learnedFirstSpellByStep"]["8"]["share"])
        self.assertIsNone(out["learnedFirstSpellByStep"]["16"])

    def test_slots_filled_per_leg_divides_by_legs_reached(self):
        # Depth 16 (capped) = two legs, three spells learned by the end.
        runs = [_run([_room(8, learned=1), _room(16, learned=3)], capped=True)]

        out = bot_merge.spell_acquisition_json(runs, cap=16)

        self.assertEqual(num_or_float(1.5), out["slotsFilledPerLegMean"])

    def test_zero_books_at_leg_2_is_compared_against_at_least_one(self):
        runs = [
            _run([_room(8, learned=0, unassigned=0)], capped=True, death_step=0),
            _run([_room(8, learned=1, unassigned=0)], capped=True, death_step=0),
        ]
        # Depths differ so the two medians can't accidentally agree.
        runs[0]["capped"] = True
        runs[1]["capped"] = False
        runs[1]["deathStep"] = 12

        out = bot_merge.spell_acquisition_json(runs, cap=40)

        self.assertEqual(40, out["depthZeroBooksAtLeg2"])
        self.assertEqual(12, out["depthSomeBooksAtLeg2"])

    def test_shop_visits_showing_an_unaffordable_book_are_shared_against_all_visits(self):
        affordable_book = {"kind": "Book", "price": 10}
        unaffordable_book = {"kind": "Book", "price": 500}
        runs = [
            _run([_room(8, shop_offers=[unaffordable_book], gold_in=50, room_type="Shop")], capped=True),
            _run([_room(8, shop_offers=[affordable_book], gold_in=50, room_type="Shop")], capped=True),
        ]

        out = bot_merge.spell_acquisition_json(runs, cap=40)

        self.assertEqual(num_or_float(0.5), out["shopVisitsShowingUnaffordableBookShare"])

    def test_no_shop_visits_reports_null_rather_than_zero(self):
        runs = [_run([_room(8, room_type="Fight")], capped=True)]

        out = bot_merge.spell_acquisition_json(runs, cap=40)

        self.assertIsNone(out["shopVisitsShowingUnaffordableBookShare"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
