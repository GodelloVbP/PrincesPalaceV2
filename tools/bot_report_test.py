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
