#!/usr/bin/env python3
"""Tests for tools/githooks/route_agents.py. Stdlib unittest.

    python tools/githooks/route_agents_test.py

Each case is a whole hook payload -- tool_name and tool_input in, allow or
deny out -- run the way the hook itself is invoked: JSON on stdin, a decision
on stdout. The four routed agents are reader (haiku), implementer (sonnet),
verifier (sonnet), and senior (opus); claude-code-guide (haiku or sonnet)
is the one built-in agent this project also allows. Everything else --
general-purpose, Explore, Plan, claude, a missing subagent_type, a model
override on a routed agent, a model naming Fable, isolation: "remote", and
the Workflow tool outright -- must be denied. `senior` additionally requires
a `prompt` with a line `Escalation: <criterion>` naming one of the three
criteria in route_agents.ESCALATION_CRITERIA. `unknown-cause` was removed
2026-09-23 and must be denied like any other made-up criterion.
"""

import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import route_agents


def decision(tool_name, tool_input):
    """Run the hook's decision path over one tool call. Returns the deny
    reason, or None if it would allow."""
    return route_agents.check(tool_name, tool_input)


ESCALATION_PROMPT = "Fix the thing.\nEscalation: architecture\nMore detail.\n"


class AllowsEachRoutedAgentTests(unittest.TestCase):
    def assertAllowed(self, tool_input):
        self.assertIsNone(decision("Agent", tool_input), "should have been allowed: " + repr(tool_input))

    def test_each_type_with_no_model_given(self):
        for subagent_type in ("reader", "implementer", "verifier"):
            self.assertAllowed({"subagent_type": subagent_type})
        self.assertAllowed({"subagent_type": "senior", "prompt": ESCALATION_PROMPT})
        self.assertAllowed({"subagent_type": "claude-code-guide"})

    def test_each_type_with_its_own_pinned_model_named_explicitly(self):
        self.assertAllowed({"subagent_type": "reader", "model": "haiku"})
        self.assertAllowed({"subagent_type": "reader", "model": "claude-haiku-4-5-20251001"})
        self.assertAllowed({"subagent_type": "implementer", "model": "sonnet"})
        self.assertAllowed({"subagent_type": "implementer", "model": "claude-sonnet-5"})
        self.assertAllowed({"subagent_type": "verifier", "model": "sonnet"})
        self.assertAllowed({"subagent_type": "verifier", "model": "claude-sonnet-5"})
        self.assertAllowed({"subagent_type": "senior", "model": "opus", "prompt": ESCALATION_PROMPT})
        self.assertAllowed({"subagent_type": "senior", "model": "claude-opus-5-5", "prompt": ESCALATION_PROMPT})

    def test_claude_code_guide_allows_either_of_its_two_models(self):
        self.assertAllowed({"subagent_type": "claude-code-guide", "model": "haiku"})
        self.assertAllowed({"subagent_type": "claude-code-guide", "model": "sonnet"})

    def test_isolation_worktree_is_not_remote_and_is_left_alone(self):
        self.assertAllowed({"subagent_type": "reader", "isolation": "worktree"})


class SeniorEscalationLineTests(unittest.TestCase):
    """senior additionally requires a prompt line 'Escalation: <criterion>'
    naming one of the three criteria; anything else is denied."""

    def assertAllowed(self, tool_input):
        self.assertIsNone(decision("Agent", tool_input), "should have been allowed: " + repr(tool_input))

    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Agent", tool_input), "should have been denied: " + repr(tool_input))

    def test_each_of_the_three_criteria_is_allowed(self):
        for criterion in route_agents.ESCALATION_CRITERIA:
            self.assertAllowed({
                "subagent_type": "senior",
                "prompt": "Some context.\nEscalation: {}\nMore context.".format(criterion),
            })

    def test_escalation_line_may_carry_trailing_detail(self):
        self.assertAllowed({
            "subagent_type": "senior",
            "prompt": "Escalation: cross-layer -- Domain and Editor must land together.",
        })

    def test_missing_prompt_is_denied(self):
        self.assertDenied({"subagent_type": "senior"})
        self.assertDenied({"subagent_type": "senior", "prompt": ""})
        self.assertDenied({"subagent_type": "senior", "prompt": 7})

    def test_prompt_with_no_escalation_line_is_denied(self):
        self.assertDenied({"subagent_type": "senior", "prompt": "Just fix the bug, it's architectural."})

    def test_made_up_criterion_is_denied(self):
        self.assertDenied({"subagent_type": "senior", "prompt": "Escalation: difficult"})
        self.assertDenied({"subagent_type": "senior", "prompt": "Escalation: important"})

    def test_unknown_cause_is_removed_and_denied(self):
        self.assertDenied({"subagent_type": "senior", "prompt": "Escalation: unknown-cause"})

    def test_criterion_must_be_a_whole_word(self):
        self.assertDenied({"subagent_type": "senior", "prompt": "Escalation: architectureish"})


class DeniesFableModelTests(unittest.TestCase):
    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Agent", tool_input), "should have been denied: " + repr(tool_input))

    def test_fable_model_denied_on_any_routed_agent(self):
        self.assertDenied({"subagent_type": "implementer", "model": "fable"})
        self.assertDenied({"subagent_type": "reader", "model": "Fable-1"})
        self.assertDenied({
            "subagent_type": "senior",
            "model": "fable-opus",
            "prompt": ESCALATION_PROMPT,
        })


class DeniesUnroutedSubagentTypesTests(unittest.TestCase):
    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Agent", tool_input), "should have been denied: " + repr(tool_input))

    def test_general_purpose_and_built_in_planning_agents(self):
        self.assertDenied({"subagent_type": "general-purpose"})
        self.assertDenied({"subagent_type": "Explore"})
        self.assertDenied({"subagent_type": "Plan"})
        self.assertDenied({"subagent_type": "claude"})

    def test_missing_subagent_type(self):
        self.assertDenied({})
        self.assertDenied({"model": "sonnet"})

    def test_empty_or_non_string_subagent_type(self):
        self.assertDenied({"subagent_type": ""})
        self.assertDenied({"subagent_type": None})
        self.assertDenied({"subagent_type": 7})

    def test_a_typo_of_a_real_name(self):
        self.assertDenied({"subagent_type": "Reader"})
        self.assertDenied({"subagent_type": "implementor"})


class FailsClosedOnAMalformedAgentCallTests(unittest.TestCase):
    """Once tool_name is known to be Agent, a missing or malformed
    tool_input is not treated as garbage to wave through -- it is a real
    Agent launch with no readable subagent_type, so it is denied the same
    as any other unrouted call."""

    def test_missing_tool_input_on_an_agent_call(self):
        code, out = PayloadHandlingTests().run_main(json.dumps({"tool_name": "Agent"}))
        self.assertEqual(0, code)
        self.assertEqual(
            "deny",
            json.loads(out)["hookSpecificOutput"]["permissionDecision"],
        )

    def test_non_object_tool_input_on_an_agent_call(self):
        code, out = PayloadHandlingTests().run_main(
            json.dumps({"tool_name": "Agent", "tool_input": "not an object"})
        )
        self.assertEqual(0, code)
        self.assertEqual(
            "deny",
            json.loads(out)["hookSpecificOutput"]["permissionDecision"],
        )


class DeniesModelOverridesOnRoutedAgentsTests(unittest.TestCase):
    """The whole point of a pinned agent is that its model is not a
    per-call decision, so any override -- even to a model another routed
    agent uses -- is refused, not just an override to something exotic."""

    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Agent", tool_input), "should have been denied: " + repr(tool_input))

    def test_reader_may_not_be_upgraded_off_haiku(self):
        self.assertDenied({"subagent_type": "reader", "model": "sonnet"})
        self.assertDenied({"subagent_type": "reader", "model": "opus"})

    def test_implementer_and_verifier_may_not_be_moved_to_opus(self):
        self.assertDenied({"subagent_type": "implementer", "model": "opus"})
        self.assertDenied({"subagent_type": "verifier", "model": "opus"})

    def test_senior_may_not_be_downgraded_off_opus(self):
        self.assertDenied({"subagent_type": "senior", "model": "sonnet", "prompt": ESCALATION_PROMPT})
        self.assertDenied({"subagent_type": "senior", "model": "haiku", "prompt": ESCALATION_PROMPT})

    def test_claude_code_guide_may_not_be_moved_to_opus(self):
        self.assertDenied({"subagent_type": "claude-code-guide", "model": "opus"})


class DeniesRemoteIsolationTests(unittest.TestCase):
    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Agent", tool_input), "should have been denied: " + repr(tool_input))

    def test_remote_isolation_on_an_otherwise_valid_call(self):
        self.assertDenied({"subagent_type": "reader", "isolation": "remote"})
        self.assertDenied({
            "subagent_type": "senior",
            "isolation": "remote",
            "prompt": ESCALATION_PROMPT,
        })


class DeniesWorkflowOutrightTests(unittest.TestCase):
    def assertDenied(self, tool_input):
        self.assertIsNotNone(decision("Workflow", tool_input), "should have been denied: " + repr(tool_input))

    def test_workflow_is_always_denied_regardless_of_input(self):
        self.assertDenied({})
        self.assertDenied({"script": "anything"})


class LeavesUnrelatedToolsAloneTests(unittest.TestCase):
    def assertAllowed(self, tool_name, tool_input):
        self.assertIsNone(decision(tool_name, tool_input), "should have been allowed: " + tool_name)

    def test_ordinary_tools_are_untouched(self):
        self.assertAllowed("Bash", {"command": "git status"})
        self.assertAllowed("Read", {"file_path": "C:/x.py"})
        self.assertAllowed("Edit", {"file_path": "C:/x.py"})


class PayloadHandlingTests(unittest.TestCase):
    """main() reads a hook payload on stdin and must never crash the tool
    call, matching deny_broad_staging.py's posture: bad input is allowed
    through silently rather than raising."""

    def run_main(self, payload_text):
        import io

        stdin, stdout = sys.stdin, sys.stdout
        sys.stdin = io.StringIO(payload_text)
        sys.stdout = io.StringIO()
        try:
            code = route_agents.main()
            return code, sys.stdout.getvalue()
        finally:
            sys.stdin, sys.stdout = stdin, stdout

    def test_a_denial_is_a_deny_decision_on_stdout(self):
        code, out = self.run_main(json.dumps({"tool_name": "Workflow", "tool_input": {}}))

        self.assertEqual(0, code)
        self.assertEqual(
            "deny",
            json.loads(out)["hookSpecificOutput"]["permissionDecision"],
        )

    def test_silence_for_a_routed_agent_call(self):
        code, out = self.run_main(
            json.dumps({"tool_name": "Agent", "tool_input": {"subagent_type": "reader"}})
        )

        self.assertEqual(0, code)
        self.assertEqual("", out)

    def test_garbage_in_is_allowed_through_rather_than_erroring(self):
        for payload in (
            "",
            "not json",
            "{}",
            json.dumps({"tool_input": {"subagent_type": "reader"}}),
            json.dumps(None),
            json.dumps([1, 2, 3]),
        ):
            code, out = self.run_main(payload)
            self.assertEqual(0, code, "payload should not crash main(): " + repr(payload))
            self.assertEqual("", out, "malformed input should be allowed through silently: " + repr(payload))


if __name__ == "__main__":
    unittest.main(verbosity=2)
