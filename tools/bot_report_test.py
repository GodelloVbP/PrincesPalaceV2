#!/usr/bin/env python3
"""Tests for tools/bot_report.py against the fixtures in
tools/bot_report_fixtures/. Standard library only.

Run: python tools/bot_report_test.py
"""

import io
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
