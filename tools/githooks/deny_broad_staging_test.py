#!/usr/bin/env python3
"""Tests for tools/githooks/deny_broad_staging.py. Stdlib unittest.

    python tools/githooks/deny_broad_staging_test.py

EVERY CASE IS A WHOLE COMMAND STRING, run through the module the way the hook
runs it -- payload in, decision out. The unit under test is not `offence()`:
it is the pipeline from "what the model actually typed into the Bash tool" to
"deny or allow", and every bypass found so far lived in the two steps BEFORE
offence() ever saw a token (how the string is split into commands, and which
token is taken to be the program).

The banned spellings come from CLAUDE.md's "Never `git add -A`" section:
`git add -A`, `git add .`, `git add -u`, `git commit -a`. The controls matter
as much as the denials -- a hook that refuses `git add <path>` is a hook that
gets uninstalled.

The literal `git` is assembled from pieces here so that this file can be read,
edited and grepped by an agent whose own tooling refuses to run a command
containing these spellings.
"""

import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import deny_broad_staging

GIT = "g" + "it"


def decision(command):
    """Run the hook's decision path over one command string.

    Returns the reason it would deny with, or None if it would allow.
    """
    for segment in deny_broad_staging.commands(command):
        reason = deny_broad_staging.offence(segment)
        if reason:
            return reason
    return None


class DeniesEverySpellingOfTheBannedCommandsTests(unittest.TestCase):
    def assertDenied(self, command):
        self.assertIsNotNone(decision(command), "should have been denied: " + command)

    def assertAllowed(self, command):
        self.assertIsNone(decision(command), "should have been allowed: " + command)

    def test_the_four_spellings_named_in_claude_md(self):
        self.assertDenied(GIT + " add -A")
        self.assertDenied(GIT + " add .")
        self.assertDenied(GIT + " add -u")
        self.assertDenied(GIT + " commit -a -m x")

    def test_long_forms_and_clusters(self):
        self.assertDenied(GIT + " add --all")
        self.assertDenied(GIT + " add --update")
        self.assertDenied(GIT + " add -Av")
        self.assertDenied(GIT + " commit --all -m x")
        self.assertDenied(GIT + " commit -am x")
        self.assertDenied(GIT + " stage -A")

    def test_after_git_global_options_and_separators(self):
        self.assertDenied(GIT + " -C foo add -A")
        self.assertDenied("cd foo && " + GIT + " add -A")
        self.assertDenied(GIT + " add -A ; echo done")
        self.assertDenied(GIT + " add -- .")


class ShellShapesThatUsedToWalkStraightPastItTests(unittest.TestCase):
    """The five bypasses this file was written for.

    Each is a real shape a multi-step Bash call takes. They got through
    because SEPARATORS did not know a newline ends a command, and because the
    program name was read as "token 0 of the segment" with no allowance for
    the shell words that can legally sit in front of it.
    """

    def assertDenied(self, command):
        self.assertIsNotNone(decision(command), "should have been denied: " + command)

    def test_a_newline_separates_two_commands(self):
        # The one that matters most: a Bash call carrying more than one line
        # is the normal case in this repo, not an exotic one.
        self.assertDenied(GIT + " status\n" + GIT + " add -A")

    def test_carriage_return_newline_too(self):
        self.assertDenied("echo hi\r\n" + GIT + " add -A")

    def test_inside_a_subshell(self):
        self.assertDenied("(" + GIT + " add -A)")

    def test_inside_a_brace_group_or_a_loop_body(self):
        self.assertDenied("{ " + GIT + " add -A; }")
        self.assertDenied("if true; then " + GIT + " add -A; fi")
        self.assertDenied("for f in a; do " + GIT + " add -A; done")

    def test_behind_an_environment_assignment(self):
        self.assertDenied("GIT_PAGER=cat " + GIT + " add -A")


class LeavesLegitimateStagingAloneTests(unittest.TestCase):
    """A false refusal costs more than it looks like it does.

    The rule these tests protect is the whole point of the hook: staging BY
    EXPLICIT PATH must stay frictionless, or the hook becomes something to
    work around rather than something to work with.
    """

    def assertAllowed(self, command):
        self.assertIsNone(decision(command), "should have been allowed: " + command)

    def test_explicit_paths(self):
        self.assertAllowed(GIT + " add path/to/file")
        self.assertAllowed(GIT + " add Assets/_Project/Art/x.png Assets/_Project/Art/x.png.meta")
        self.assertAllowed(GIT + " commit -m 'msg'")

    def test_a_message_that_merely_mentions_the_flag(self):
        # The docstring's own example. shlex keeps the quoted message as one
        # token, so the flag inside it is never read as a flag.
        self.assertAllowed(GIT + " commit -m 'add -a note'")

    def test_other_subcommands_and_other_programs(self):
        self.assertAllowed(GIT + " status --short")
        self.assertAllowed(GIT + " diff --cached --name-only")
        self.assertAllowed("echo " + GIT + " add -A")
        self.assertAllowed("grep -n 'add -A' CLAUDE.md")


class PayloadHandlingTests(unittest.TestCase):
    """main() reads a hook payload on stdin and must never crash the tool call."""

    def run_main(self, payload_text):
        import io

        stdin, stdout = sys.stdin, sys.stdout
        sys.stdin = io.StringIO(payload_text)
        sys.stdout = io.StringIO()
        try:
            code = deny_broad_staging.main()
            return code, sys.stdout.getvalue()
        finally:
            sys.stdin, sys.stdout = stdin, stdout

    def test_a_denial_is_a_deny_decision_on_stdout(self):
        code, out = self.run_main(json.dumps({"tool_input": {"command": GIT + " add -A"}}))

        self.assertEqual(0, code)
        self.assertEqual(
            "deny",
            json.loads(out)["hookSpecificOutput"]["permissionDecision"],
        )

    def test_silence_for_anything_it_does_not_object_to(self):
        code, out = self.run_main(json.dumps({"tool_input": {"command": GIT + " status"}}))

        self.assertEqual(0, code)
        self.assertEqual("", out)

    def test_garbage_in_is_allowed_through_rather_than_erroring(self):
        for payload in ("", "not json", "{}", json.dumps({"tool_input": {}})):
            code, out = self.run_main(payload)
            self.assertEqual(0, code)
            self.assertEqual("", out)


if __name__ == "__main__":
    unittest.main(verbosity=2)
