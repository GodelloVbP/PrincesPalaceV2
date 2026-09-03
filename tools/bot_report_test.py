#!/usr/bin/env python3
"""Tests for tools/bot_report.py against the fixtures in
tools/bot_report_fixtures/. Standard library only.

Run: python tools/bot_report_test.py
"""

import io
import re
import sys
import unittest
from contextlib import redirect_stdout
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import bot_report  # noqa: E402

FIXTURES = Path(__file__).resolve().parent / "bot_report_fixtures"
BATCH_A = FIXTURES / "batch_a"
BATCH_B = FIXTURES / "batch_b"


class TextSummaryTests(unittest.TestCase):
    def test_batch_a_literal_numbers(self):
        summary = bot_report.load_summary(BATCH_A)
        text = bot_report.render_text_summary(summary)
        self.assertIn("RandomLegal / Fresh: depth median=10 (n=3)", text)
        self.assertIn("GreedyAggressive / Fresh: depth median=14 (n=3)", text)
        self.assertIn("Bugs: 0", text)
        self.assertIn("Determinism: clean (6 checked)", text)

    def test_batch_b_literal_numbers(self):
        summary = bot_report.load_summary(BATCH_B)
        text = bot_report.render_text_summary(summary)
        self.assertIn("RandomLegal / Fresh: depth median=12 (n=3)", text)
        self.assertIn("GreedyAggressive / Fresh: depth median=19 (n=3)", text)
        self.assertIn("Bugs: 1", text)
        self.assertIn("Determinism: 1 mismatch(es) out of 6 checked", text)


class PreviousBatchDiscoveryTests(unittest.TestCase):
    def test_finds_batch_a_as_previous_of_batch_b(self):
        found = bot_report.find_previous_batch(BATCH_B)
        self.assertEqual(found, BATCH_A)

    def test_batch_a_has_no_older_sibling(self):
        found = bot_report.find_previous_batch(BATCH_A)
        self.assertIsNone(found)


class DeltaHtmlTests(unittest.TestCase):
    def test_higher_is_better(self):
        out = bot_report.delta_html(10, 12, "high")
        self.assertIn("delta-better", out)
        self.assertIn("+2", out)

    def test_higher_is_worse_when_direction_low(self):
        out = bot_report.delta_html(10, 12, "low")
        self.assertIn("delta-worse", out)

    def test_no_direction_is_neutral(self):
        out = bot_report.delta_html(0.3, 0.4, None)
        self.assertIn("delta-neutral", out)

    def test_no_previous_value_is_blank(self):
        self.assertEqual(bot_report.delta_html(None, 12, "high"), "")

    def test_zero_delta(self):
        out = bot_report.delta_html(5, 5, "high")
        self.assertIn("delta-neutral", out)
        self.assertIn(">0<", out)


class HtmlRenderTests(unittest.TestCase):
    def setUp(self):
        self.summary_a = bot_report.load_summary(BATCH_A)
        self.summary_b = bot_report.load_summary(BATCH_B)

    def test_render_with_previous_includes_delta_column(self):
        out = bot_report.render_html(self.summary_b, self.summary_a, BATCH_B, BATCH_A)
        self.assertIn("delta", out)
        self.assertIn("delta-better", out)  # depth median improved 10->12 and 14->19
        self.assertIn("Compared against", out)

    def test_render_without_previous_has_no_delta_but_still_valid(self):
        out = bot_report.render_html(self.summary_a, None, BATCH_A, None)
        self.assertIn("No previous batch found", out)
        self.assertIn("RandomLegal", out)

    def test_bug_row_rendered(self):
        out = bot_report.render_html(self.summary_b, self.summary_a, BATCH_B, BATCH_A)
        self.assertIn("HpOutOfRange", out)
        self.assertIn("bugs (1)", out)

    def test_determinism_mismatch_rendered(self):
        out = bot_report.render_html(self.summary_b, self.summary_a, BATCH_B, BATCH_A)
        self.assertIn("1 mismatch(es)", out)

    def test_coverage_rendered(self):
        out = bot_report.render_html(self.summary_a, None, BATCH_A, None)
        self.assertIn("ashbound_locket", out)
        self.assertIn("cave_spider", out)

    def test_archetype_gap_svg_present(self):
        out = bot_report.render_html(self.summary_b, self.summary_a, BATCH_B, BATCH_A)
        self.assertIn("<svg", out)
        self.assertIn("RandomLegal", out)
        self.assertIn("GreedyAggressive", out)

    def test_title_and_style_present(self):
        out = bot_report.render_html(self.summary_a, None, BATCH_A, None)
        self.assertIn("<title>", out)
        self.assertIn("<style>", out)


class FoldPlusTiersTests(unittest.TestCase):
    def test_contiguous_run_collapses_to_a_range(self):
        rows = bot_report._fold_plus_tiers(["leather_torso_p4", "leather_torso_p5", "leather_torso_p6"])
        self.assertEqual(rows, ["leather_torso (p4..p6)"])

    def test_gap_in_tiers_is_two_ranges(self):
        rows = bot_report._fold_plus_tiers(["leather_torso_p4", "leather_torso_p6", "leather_torso_p7"])
        self.assertEqual(rows, ["leather_torso (p4, p6..p7)"])

    def test_single_tier_has_no_range_dots(self):
        rows = bot_report._fold_plus_tiers(["steel_helmet_p9"])
        self.assertEqual(rows, ["steel_helmet (p9)"])

    def test_a_non_plus_tier_id_passes_through_unchanged(self):
        rows = bot_report._fold_plus_tiers(["stale_bread"])
        self.assertEqual(rows, ["stale_bread"])

    def test_mixed_ids_sort_together(self):
        rows = bot_report._fold_plus_tiers(["stale_bread", "leather_torso_p4", "leather_torso_p5"])
        self.assertEqual(rows, ["leather_torso (p4..p5)", "stale_bread"])


class SkillsNeverUsedSplitTests(unittest.TestCase):
    def test_batch_b_folds_item_plus_tiers_in_the_rendered_report(self):
        summary = bot_report.load_summary(BATCH_B)
        out = bot_report.render_coverage(summary["coverage"])
        self.assertIn("leather_torso (p4, p6..p10)", out)
        self.assertIn("steel_helmet (p9..p10)", out)
        # The individual plus-tier ids must NOT also appear as their own rows.
        self.assertNotIn("leather_torso_p4<", out)

    def test_batch_b_splits_skills_never_used_by_enemyAbilityIds(self):
        summary = bot_report.load_summary(BATCH_B)
        out = bot_report.render_coverage(summary["coverage"])
        self.assertIn("player skills", out)
        self.assertIn("enemy abilities", out)
        self.assertIn("provoke", out)
        self.assertIn("shatter", out)
        self.assertIn("hex", out)
        self.assertIn("roar", out)
        self.assertIn("enemyAbilityIds", out)

    def test_batch_a_has_no_enemy_ability_key_and_collapses_under_details(self):
        summary = bot_report.load_summary(BATCH_A)
        out = bot_report.render_coverage(summary["coverage"])
        # batch_a's skillsNeverUsed is empty, so this exercises the "none"
        # branch rather than <details> -- assert the flat-list fallback
        # path is reachable by feeding a non-empty list with no enemy key.
        out2 = bot_report.render_coverage({"skillsNeverUsed": ["provoke", "hex"]})
        self.assertIn("<details>", out2)
        self.assertIn("provoke", out2)
        self.assertNotIn("player skills", out2)
        self.assertIsInstance(out, str)


class ArchetypeGapFourArchetypesTests(unittest.TestCase):
    def test_color_map_assigns_a_distinct_color_per_archetype(self):
        gap = [{"profile": "Fresh", "archetypes": [
            {"archetype": "RandomLegal", "medianDepth": 12},
            {"archetype": "GreedyAggressive", "medianDepth": 19},
            {"archetype": "GreedyDefensive", "medianDepth": 16},
            {"archetype": "Lookahead2", "medianDepth": 24},
        ]}]
        colors = bot_report._archetype_color_map(gap)
        self.assertEqual(len(colors), 4)
        self.assertEqual(len(set(colors.values())), 4, "all four archetypes must get distinct colors")

    def test_batch_b_gap_svg_renders_all_four_archetypes_with_distinct_fills(self):
        summary = bot_report.load_summary(BATCH_B)
        svg = bot_report.render_archetype_gap_svg(summary)
        for name in ["RandomLegal", "GreedyAggressive", "GreedyDefensive", "Lookahead2"]:
            self.assertIn(name, svg)

        fills = re.findall(r'<rect [^>]*fill="(#[0-9a-fA-F]{6})"', svg)
        self.assertEqual(len(fills), 4, "one bar per archetype")
        self.assertEqual(len(set(fills)), 4, "all four archetype bars must get distinct colors")


class TracesJsonlFixtureTests(unittest.TestCase):
    """These fixtures aren't parsed by bot_report.py itself, but they are
    part of the documented batch-directory contract (BOT_SUMMARY_SCHEMA.md),
    so a malformed one here would be a silent lie about what a real batch
    looks like."""

    def test_batch_a_traces_are_valid_json_lines(self):
        self._assert_valid_traces(BATCH_A / "traces.jsonl", expect_count=3)

    def test_batch_b_traces_are_valid_json_lines(self):
        self._assert_valid_traces(BATCH_B / "traces.jsonl", expect_count=3)

    def _assert_valid_traces(self, path, expect_count):
        import json

        lines = [l for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]
        self.assertEqual(len(lines), expect_count)
        required = {"Seed", "Archetype", "Profile", "Fights", "Rooms", "DeathStep", "DeathCause", "Capped"}
        for line in lines:
            obj = json.loads(line)
            self.assertTrue(required.issubset(obj.keys()), obj.keys())
            for fight in obj["Fights"]:
                self.assertIn("TurnTraces", fight)
                for turn in fight["TurnTraces"]:
                    self.assertIn("PartyHpAfter", turn)


class ShopSectionTests(unittest.TestCase):
    """The gate-1 shop rows, rendered off a summary built here rather than off
    a checked-in fixture: the two batch_* fixtures predate the shop and are
    the record of what an older summary.json looked like, which is a property
    worth keeping rather than editing away."""

    def summary(self, shop, versus=None):
        return {
            "batch": {"timestamp": "2026-09-03T00:00:00Z", "commitSha": "abc1234",
                      "runsPerCell": 10, "firstSeed": 1, "archetypes": ["RandomLegal"],
                      "profiles": ["Fresh"], "depthCapSteps": 40, "elapsedSeconds": 1,
                      "shopPolicies": ["WhenOffered"]},
            "cells": [{"archetype": "RandomLegal", "profile": "Fresh", "runs": 10,
                       "depth": {"median": 8, "p10": 4, "p90": 20, "mean": 9, "cappedShare": 0},
                       "deathCauses": [], "doomedShare": 0, "swingShare": 0,
                       "fightLength": {"byFloor": {}, "byRoomType": {}},
                       "turnOneShare": 0, "steamrollByFloor": {}, "consumableUseShare": 0,
                       "potionsWastedMean": 0,
                       "buildDiversity": {"distinctRelicSets": 1, "distinctTalentSets": 1,
                                          "distinctGearSets": 1},
                       "itemPickRate": {}, "relicPickRate": {}, "itemEquipRate": {},
                       "shop": shop}],
            "decisionPressure": {"nodes": None, "offers": None, "relics": None},
            "shopVsNoShop": versus,
            "coverage": {"enemiesNeverSeen": [], "relicsNeverOffered": [],
                         "relicsNeverPicked": [], "itemsNeverOffered": [],
                         "itemsNeverPicked": [], "skillsNeverUsed": [], "enemyAbilityIds": []},
            "archetypeGap": [{"profile": "Fresh",
                              "archetypes": [{"archetype": "RandomLegal", "medianDepth": 8}]}],
            "bugs": [],
            "determinism": {"checked": 1, "mismatches": []},
        }

    A_SHOP = {
        "visits": 4,
        "arrivalGold": {"p10": 12, "p25": 20, "median": 44, "p75": 91},
        "belowCheapestShare": 0.25,
        "goldForgone": {"1-8": 16, "9-16": 22, "17-24": None, "25-40": None},
        "purchasesPerVisit": 0.5,
        "zeroPurchaseShare": 0.75,
        "affordableShareBySection": {"gear": 0.4, "books": None, "relics": 0.05},
        "rerollsPerVisitBySection": {"gear": 0.25, "books": 0, "relics": 0},
        "rerollThenNoPurchaseShare": {"gear": 0.25, "books": None, "relics": None},
        "spendShareBySection": {"gear": 1, "books": None, "relics": 0},
        "goldOnLeaveMedian": 20,
        "goldAtDeathMedian": 31,
        "arrivalGoldByStep": {
            "4": {"p10": 8, "p25": 14, "median": 30, "n": 10},
            "8": None, "12": None, "16": None, "24": None, "32": None, "40": None,
        },
    }

    def test_the_shop_block_renders_its_numbers(self):
        out = bot_report.render_html(self.summary(self.A_SHOP), None, "x", None)
        self.assertIn("arrived below the cheapest card", out)
        self.assertIn("visits that bought nothing", out)
        self.assertIn("gold forgone", out)
        self.assertIn("share of shown cards affordable on arrival", out)
        self.assertIn("gold on arrival, by step", out)

    def test_a_step_nobody_reached_reads_as_not_reached(self):
        out = bot_report.render_html(self.summary(self.A_SHOP), None, "x", None)
        self.assertIn("not reached", out)

    def test_a_cell_with_no_visits_says_so_instead_of_showing_zeroes(self):
        empty = {"visits": 0, "arrivalGold": None, "belowCheapestShare": None,
                 "goldForgone": {"1-8": 16}, "arrivalGoldByStep": {}}
        out = bot_report.render_html(self.summary(empty), None, "x", None)
        self.assertIn("no shop visits in this cell", out)
        self.assertNotIn("visits that bought nothing", out)

    def test_shop_vs_no_shop_is_absent_without_both_modes(self):
        out = bot_report.render_html(self.summary(self.A_SHOP), None, "x", None)
        self.assertNotIn("shop vs no shop", out)

    def test_shop_vs_no_shop_renders_the_paired_comparison(self):
        versus = {
            "pairs": 100, "pairsWithAShopVisit": 61,
            "medianDepth": {"WhenOffered": 12, "Never": 14, "delta": -2},
            "survivalToNextBossShare": {"WhenOffered": 0.4, "Never": 0.5},
            "depthVariance": {"WhenOffered": 30, "Never": 26},
        }
        summary = self.summary(self.A_SHOP, versus)
        out = bot_report.render_html(summary, None, "x", None)
        self.assertIn("shop vs no shop (matched seeds)", out)
        self.assertIn("100 paired runs", out)
        self.assertIn("survived to a boss after the first shop", out)

        text = bot_report.render_text_summary(summary)
        self.assertIn("Shop policy: WhenOffered", text)
        self.assertIn("Shop vs no shop: median depth 12 vs 14 over 100 paired runs", text)
        self.assertIn("shop visits=4", text)


class CliSmokeTest(unittest.TestCase):
    def test_main_runs_end_to_end(self):
        import tempfile
        import os

        with tempfile.TemporaryDirectory() as tmp:
            out_path = os.path.join(tmp, "report.html")
            buf = io.StringIO()
            with redirect_stdout(buf):
                rc = bot_report.main([str(BATCH_B), "--previous", str(BATCH_A), "--out", out_path])
            self.assertEqual(rc, 0)
            self.assertTrue(os.path.isfile(out_path))
            stdout = buf.getvalue()
            self.assertIn("Bugs: 1", stdout)
            with open(out_path, "r", encoding="utf-8") as f:
                content = f.read()
            self.assertIn("delta-better", content)


if __name__ == "__main__":
    unittest.main()
