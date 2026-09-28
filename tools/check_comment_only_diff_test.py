#!/usr/bin/env python3
"""Tests for tools/check_comment_only_diff.py. Stdlib unittest.

    python3 tools/check_comment_only_diff_test.py

Runs the checker's own main() against a real git repo in a temp directory,
committing an "old" version of Foo.cs and then staging a "new" version -
the same shape check_comment_only_diff.py is meant to be pointed at (a
comment-sweep commit's diff). This exercises the real git-show plumbing
that block-comment classification depends on, not just the parser.
"""

import os
import subprocess
import sys
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "check_comment_only_diff.py")


class CheckerRepoTestCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.repo = self.tmp.name
        self._git("init", "-q")
        self._git("config", "user.email", "t@example.com")
        self._git("config", "user.name", "t")

    def tearDown(self):
        self.tmp.cleanup()

    def _git(self, *args):
        subprocess.run(["git"] + list(args), cwd=self.repo, check=True, capture_output=True)

    def _write(self, name, content):
        with open(os.path.join(self.repo, name), "w") as fh:
            fh.write(content)

    def run_guard(self, *args):
        result = subprocess.run(
            [sys.executable, SCRIPT] + list(args),
            cwd=self.repo,
            capture_output=True,
            text=True,
        )
        return result.returncode, result.stdout

    def check_cached(self, old_content, new_content, filename="Foo.cs"):
        """Commit old_content, stage new_content, run the guard --cached."""
        self._write(filename, old_content)
        self._git("add", filename)
        self._git("commit", "-q", "-m", "init")

        self._write(filename, new_content)
        self._git("add", filename)

        return self.run_guard("--cached")


class CommentOnlyChangesPassTests(CheckerRepoTestCase):
    def test_a_reworded_line_comment_passes(self):
        code, out = self.check_cached(
            "class Foo {\n    // old wording explaining why.\n}\n",
            "class Foo {\n    // new wording explaining why.\n}\n",
        )
        self.assertEqual(0, code, out)

    def test_blank_line_removal_passes(self):
        code, out = self.check_cached(
            "class Foo {\n\n    int x = 1;\n}\n",
            "class Foo {\n    int x = 1;\n}\n",
        )
        self.assertEqual(0, code, out)

    def test_block_comment_interior_edit_passes(self):
        # The interior line sits far enough into the block that a -U0 diff
        # carries no /* in its own hunk - this is exactly the case the
        # whole-file classification exists for.
        old = "class Foo {\n/*\n   old interior line\n   more context\n*/\n    int x = 1;\n}\n"
        new = "class Foo {\n/*\n   new interior line\n   more context\n*/\n    int x = 1;\n}\n"
        code, out = self.check_cached(old, new)
        self.assertEqual(0, code, out)


class CodeChangesFailTests(CheckerRepoTestCase):
    def test_a_code_token_change_fails(self):
        code, out = self.check_cached(
            "class Foo {\n    var xs = new List<string>();\n}\n",
            "class Foo {\n    var xs = new List<string>\n}\n",
        )
        self.assertEqual(1, code)
        self.assertIn("Foo.cs", out)

    def test_a_trailing_comment_edit_on_a_code_line_fails(self):
        # Deliberately conservative: the line still has code on it (x = 1;),
        # so it fails even though only the trailing comment text changed.
        code, out = self.check_cached(
            "class Foo {\n    x = 1; // old reason\n}\n",
            "class Foo {\n    x = 1; // new reason\n}\n",
        )
        self.assertEqual(1, code)
        self.assertIn("Foo.cs", out)


class ScopeTests(CheckerRepoTestCase):
    def test_non_cs_files_are_ignored(self):
        code, out = self.check_cached(
            "old code line\n", "new code line\n", filename="Foo.txt"
        )
        self.assertEqual(0, code, out)

    def test_working_tree_mode_without_cached(self):
        self._write("Foo.cs", "class Foo {\n    // old\n}\n")
        self._git("add", "Foo.cs")
        self._git("commit", "-q", "-m", "init")

        self._write("Foo.cs", "class Foo {\n    // new\n}\n")
        # Not staged - exercises working tree vs HEAD, the default mode.
        code, out = self.run_guard()
        self.assertEqual(0, code, out)

    def test_a_newly_added_file_is_checked_on_its_own(self):
        self._write("Foo.cs", "class Foo {\n}\n")
        self._git("add", "Foo.cs")
        self._git("commit", "-q", "-m", "init")

        self._write("Bar.cs", "class Bar {\n    int y = broken\n}\n")
        self._git("add", "Bar.cs")

        code, out = self.run_guard("--cached")
        self.assertEqual(1, code)
        self.assertIn("Bar.cs", out)


if __name__ == "__main__":
    unittest.main(verbosity=2)
