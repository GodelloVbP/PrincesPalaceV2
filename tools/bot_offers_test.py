#!/usr/bin/env python3
"""Tests for tools/bot_offers.py. Stdlib unittest, fabricated fixture.

    python tools/bot_offers_test.py

Two fake shards, four runs, hand-countable offers -- every expected number
below was counted by hand off the fixture, not recomputed through the
module under test.
"""

import json
import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bot_offers


def room(step, favor, cls, offers, picked, floor=1):
    return {
        "step": step, "floor": floor, "nodeId": step, "roomType": "Fight",
        "offerItemIds": [o[0] for o in offers], "pickedIndex": picked,
        "favor": favor, "encounterClass": cls,
        "offers": [{"itemId": o[0], "tier": o[1], "plus": o[2], "riftTier": o[3], "modifierCount": o[3]}
                   for o in offers],
        "equippedItemIds": [],
    }


def run(seed, archetype, profile, rooms):
    return {"seed": seed, "archetype": archetype, "profile": profile, "capped": False,
            "deathStep": 9, "fights": [], "rooms": rooms, "relicIds": [], "talentIds": []}


# Fresh: two runs. Seed 1 sees offers at steps 2 and 6, seed 2 at step 2 only.
FRESH = [
    run(1, "RandomLegal", "Fresh", [
        room(1, 4, "", [], -1),
        room(2, 4, "Normal", [("a", 0, 0, 0), ("b", 1, 1, 0), ("c", 0, 0, 1)], 1),
        room(6, 4, "Elite", [("d", 1, 0, 0), ("e", 2, 3, 0), ("f", 1, 0, 0)], -1),
    ]),
    run(2, "GreedyAggressive", "Fresh", [
        room(2, 4, "Normal", [("a", 0, 0, 0), ("b", 0, 0, 0), ("c", 1, 0, 0)], 2),
    ]),
]

# Late: two runs, favor 9, one Elite at step 10.
LATE = [
    run(1, "RandomLegal", "Late", [
        room(10, 9, "Elite", [("g", 2, 1, 2), ("h", 3, 0, 0), ("i", 2, 0, 0)], 0, floor=2),
    ]),
    run(2, "GreedyAggressive", "Late", [
        room(3, 9, "Normal", [("j", 0, 0, 0), ("k", 0, 4, 0), ("l", 0, 5, 3)], 1),
    ]),
]


class OffersTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="bot-offers-")
        for i, runs in enumerate((FRESH, LATE), start=1):
            shard = os.path.join(self.tmp, "shard-{}".format(i))
            os.makedirs(shard)
            with open(os.path.join(shard, "runs.jsonl"), "w", encoding="utf-8") as h:
                for r in runs:
                    h.write(json.dumps(r) + "\n")
        self.rows = bot_offers.offer_rows(bot_offers.load_runs(self.tmp))

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def fresh(self):
        return [r for r in self.rows if r["profile"] == "Fresh"]

    def late(self):
        return [r for r in self.rows if r["profile"] == "Late"]

    def find(self, table, *first_cells):
        _, _, body, _ = table
        for row in body:
            if tuple(row[:len(first_cells)]) == first_cells:
                return row
        self.fail("no row starting {} in {}".format(first_cells, body))

    def test_reads_both_shards_and_skips_rooms_without_offers(self):
        self.assertEqual(len(self.rows), 15)
        self.assertEqual(len(self.fresh()), 9)

    def test_per_step_counts_offers_items_and_pick_rate(self):
        t = bot_offers.table_per_step(self.fresh())
        self.assertEqual(self.find(t, "2"), ["2", "2", "6", "2", "100.0%"])
        self.assertEqual(self.find(t, "6"), ["6", "1", "3", "0", "0.0%"])

    def test_tier_distribution_per_band_and_class(self):
        t = bot_offers.table_tier(self.fresh())
        # band 1-4, Normal: six cards, four T0 and two T1.
        row = self.find(t, "1-4", "Normal")
        self.assertEqual(row[2], "6")
        self.assertEqual(row[3], "4 (66.7%)")
        self.assertEqual(row[4], "2 (33.3%)")
        # band 5-8, Elite: T1 x2, T2 x1 -> median 1, p90 1.8
        row = self.find(t, "5-8", "Elite")
        self.assertEqual(row[3:], ["0 (0.0%)", "2 (66.7%)", "1 (33.3%)", "1.0", "1.8"])

    def test_plus_pools_four_and_above(self):
        t = bot_offers.table_plus(self.late())
        row = self.find(t, "1-4", "Normal")
        # +0, +4, +5 -> +0 one, +4+ two
        self.assertEqual(row[3], "1 (33.3%)")
        self.assertEqual(row[7], "2 (66.7%)")

    def test_rift_distribution(self):
        t = bot_offers.table_rift(self.late())
        row = self.find(t, "9-12", "Elite")
        self.assertEqual(row[3:7], ["2 (66.7%)", "0 (0.0%)", "1 (33.3%)", "0 (0.0%)"])

    def test_favor_table_lists_distinct_values(self):
        t = bot_offers.table_favor(self.rows)
        self.assertIn("distinct favor values seen: 4, 9", t[3][0])
        self.assertEqual(self.find(t, "4")[1], "9")
        self.assertEqual(self.find(t, "9")[1], "6")

    def test_picked_vs_shown(self):
        t = bot_offers.table_picked_vs_shown(self.fresh())
        # (tier 1, plus 1): shown once out of 9, picked once out of 2 picks.
        self.assertEqual(self.find(t, "1", "+1"), ["1", "+1", "1", "11.1%", "1", "50.0%", "100.0%"])
        self.assertEqual(self.find(t, "2", "+3")[4:], ["0", "0.0%", "0.0%"])

    def test_first_appearance(self):
        t = bot_offers.table_first_appearance(self.fresh())
        # plus>=1: seed 1 at step 2, seed 2 never -> 1 of 2 runs, median 2.
        self.assertEqual(self.find(t, "plus >= 1"), ["plus >= 1", "1", "50.0%", "2", "2", "2"])
        self.assertEqual(self.find(t, "plus >= 2"), ["plus >= 2", "1", "50.0%", "6", "6", "6"])
        self.assertEqual(self.find(t, "tier >= 2"), ["tier >= 2", "1", "50.0%", "6", "6", "6"])

    def test_favor_note_says_profiles_differ(self):
        self.assertTrue(bot_offers.favor_note(self.rows).startswith("favor DIFFERS"))

    def test_renders_html_and_text_without_error(self):
        sections, notes = bot_offers.build(self.rows)
        text = bot_offers.render_text(sections, notes, self.tmp, 4, self.rows)
        page = bot_offers.render_html(sections, notes, self.tmp, 4, self.rows)
        self.assertIn("PROFILE Late", text)
        self.assertIn("RarityTable.cs", page)


if __name__ == "__main__":
    unittest.main()
