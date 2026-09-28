#!/usr/bin/env python3
"""Tests for tools/githooks/comment_history.py. Stdlib unittest.

    python3 tools/githooks/comment_history_test.py

Each test builds a small unified diff fragment the way
`git diff --cached -U0 -- '*.cs'` would emit it, and runs it through
find_violations() the way the hook does.
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import comment_history

HEADER = "diff --git a/Foo.cs b/Foo.cs\n--- a/Foo.cs\n+++ b/Foo.cs\n"


def violations(hunk_body, filename="Foo.cs"):
    diff = (
        "diff --git a/{f} b/{f}\n--- a/{f}\n+++ b/{f}\n".format(f=filename)
        + hunk_body
    )
    return list(comment_history.find_violations(diff))


class FlagsAnAddedCommentWithAnIsoDateTests(unittest.TestCase):
    def test_a_plain_comment_with_a_date_is_flagged(self):
        v = violations("@@ -1,0 +5 @@\n+    // Changed on 2026-09-28 because it moved.\n")
        self.assertEqual(1, len(v))
        self.assertEqual("Foo.cs", v[0][0])
        self.assertEqual(5, v[0][1])

    def test_a_doc_comment_with_a_date_is_flagged(self):
        v = violations("@@ -1,0 +9 @@\n+/// Reworked 2026-09-28 for the new pipeline.\n")
        self.assertEqual(1, len(v))
        self.assertEqual(9, v[0][1])


class IgnoresWhatItShouldTests(unittest.TestCase):
    def test_removed_lines_are_never_flagged(self):
        v = violations("@@ -5 +5,0 @@\n-    // Changed on 2026-09-28 because it moved.\n")
        self.assertEqual([], v)

    def test_a_date_inside_a_string_literal_is_not_a_comment(self):
        v = violations('@@ -1,0 +3 @@\n+    var s = "2026-09-28"; // fine\n')
        self.assertEqual([], v)

    def test_a_double_slash_inside_a_string_before_the_real_comment(self):
        v = violations('@@ -1,0 +3 @@\n+    var s = "a // b 2026-01-01";\n')
        self.assertEqual([], v)

    def test_non_cs_hunks_are_ignored(self):
        v = violations("@@ -1,0 +5 @@\n+    // Changed on 2026-09-28 because it moved.\n", filename="Foo.txt")
        self.assertEqual([], v)

    def test_a_clean_diff_passes(self):
        v = violations("@@ -1,0 +5 @@\n+    // Explains why this exists, present tense.\n")
        self.assertEqual([], v)

    def test_an_existing_unchanged_line_is_never_flagged(self):
        # Context lines (no leading +/-) never appear under -U0, but the
        # parser must not misread one as added if it ever sees one.
        v = violations("@@ -5,1 +5,1 @@\n     // Changed on 2026-09-28 because it moved.\n")
        self.assertEqual([], v)


class MainExitCodeTests(unittest.TestCase):
    def run_main(self, diff_text):
        import io

        stdin, stdout = sys.stdin, sys.stdout
        sys.stdin = io.StringIO(diff_text)
        sys.stdout = io.StringIO()
        try:
            code = comment_history.main()
            return code, sys.stdout.getvalue()
        finally:
            sys.stdin, sys.stdout = stdin, stdout

    def test_exit_1_on_violation(self):
        diff = HEADER + "@@ -1,0 +5 @@\n+    // Changed on 2026-09-28 because it moved.\n"
        code, out = self.run_main(diff)
        self.assertEqual(1, code)
        self.assertIn("Foo.cs:5:", out)

    def test_exit_0_on_clean_diff(self):
        diff = HEADER + "@@ -1,0 +5 @@\n+    // Explains why this exists.\n"
        code, out = self.run_main(diff)
        self.assertEqual(0, code)
        self.assertEqual("", out)


if __name__ == "__main__":
    unittest.main(verbosity=2)
