#!/usr/bin/env python3
"""Tests for the senior-escalation audit in tools/usage_baseline.py.

    python tools/usage_baseline_test.py
"""

import json
import os
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import usage_baseline


def agent_record(tool_id, subagent_type, prompt):
    return {
        "type": "assistant",
        "timestamp": usage_baseline.NOW.isoformat(),
        "message": {
            "content": [{
                "type": "tool_use",
                "id": tool_id,
                "name": "Agent",
                "input": {"subagent_type": subagent_type, "prompt": prompt},
            }],
        },
    }


class ParseEscalationTests(unittest.TestCase):
    def test_bare_criterion(self):
        self.assertEqual(("architecture", ""), usage_baseline.parse_escalation("x\nEscalation: architecture\ny"))

    def test_judgment_reason_for_each_separator(self):
        for sep in (" - ", " — ", ": "):
            self.assertEqual(
                ("judgment", "needs a design call"),
                usage_baseline.parse_escalation("Escalation: judgment" + sep + "needs a design call"),
            )

    def test_missing_line(self):
        self.assertEqual(("(none)", ""), usage_baseline.parse_escalation("no line here"))


class SeniorReportTests(unittest.TestCase):
    def test_launches_grouped_and_judgment_reasons_listed_verbatim(self):
        records = [
            agent_record("t1", "senior", "Escalation: architecture"),
            agent_record("t2", "senior", "Escalation: judgment - touches save format and bot"),
            agent_record("t3", "senior", "Escalation: judgment: second reason"),
            agent_record("t4", "implementer", "Escalation: judgment - not senior, ignored"),
        ]
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "session-1.jsonl")
            with open(path, "w", encoding="utf-8") as fh:
                for r in records:
                    fh.write(json.dumps(r) + "\n")
            stats = usage_baseline.Stats()
            usage_baseline.process_file(path, "main", stats, datetime.now(timezone.utc) - timedelta(days=1))
        text = "\n".join(usage_baseline.senior_escalation_lines(stats.senior_launches))
        self.assertIn("| judgment | 2 |", text)
        self.assertIn("| architecture | 1 |", text)
        self.assertIn("touches save format and bot", text)
        self.assertIn("second reason", text)
        self.assertNotIn("not senior", text)

    def test_no_launches(self):
        self.assertIn("No `senior` launches", "\n".join(usage_baseline.senior_escalation_lines([])))


if __name__ == "__main__":
    unittest.main(verbosity=2)
