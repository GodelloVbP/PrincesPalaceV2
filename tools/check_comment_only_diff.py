#!/usr/bin/env python3
"""Guard that a diff over *.cs touches comments and blank lines only.

Written for comment-sweep agents: a pass whose whole job is rewording or
removing comments (see tools/githooks/comment_history.py and CLAUDE.md's
"How to work") must never also slip in a code change. A regex-driven sweep
has already nearly stripped the `()` off a call while "cleaning up" the
comment beside it -- this is the mechanical check that catches that class
of mistake before it is committed.

Every changed line (added or removed) in a *.cs file must, after optional
leading/trailing whitespace, be one of:

  * blank
  * a `//` line comment (the whole line, after stripping whitespace)
  * a line lying inside a `/* ... */` block comment (opening line, interior
    lines, and the closing line all count)

A line that has ANY code on it fails, including a trailing `// why` comment
on an otherwise-code line -- deliberately conservative, because this guard
exists so the sweep never has to be trusted to draw that line itself.

WHY A LINE'S BLOCK-COMMENT STATUS IS READ FROM THE WHOLE FILE, NOT THE DIFF.
A `git diff -U0` hunk carries no context, so an interior line of a `/* ... */`
block can appear in a hunk with no `/*` anywhere in the diff to say so. This
reads each side's line ranges straight out of the hunk headers (which -U0
gives exactly, one changed line per line number, no guessing), then classifies
every line of the OLD blob and every line of the NEW blob by scanning each
file whole, so nesting state is always correct regardless of where a hunk
happens to start.

Usage:

    python3 tools/check_comment_only_diff.py                  # working tree vs HEAD
    python3 tools/check_comment_only_diff.py --cached          # index vs HEAD
    python3 tools/check_comment_only_diff.py <rev>..<rev>      # a revision range

Exit 0 if every changed *.cs line is a comment or blank; exit 1 and print
each offending file:line otherwise.
"""

import argparse
import re
import subprocess
import sys

LINE_COMMENT_RE = re.compile(r"^//")
HUNK_RE = re.compile(r"^@@ -(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?")

WORKTREE = "WORKTREE"
INDEX = "INDEX"


def is_comment_or_blank(text, in_block):
    """Classify one line's content.

    Returns (ok, in_block_after): ok is True if the line is blank, a `//`
    line comment, or part of a `/* ... */` block (including the line that
    opens it and the one that closes it).
    """
    stripped = text.strip()

    if in_block:
        if "*/" in stripped:
            return True, False
        return True, True

    if stripped == "":
        return True, False

    if LINE_COMMENT_RE.match(stripped):
        return True, False

    if stripped.startswith("/*"):
        after_open = stripped[2:]
        return True, "*/" not in after_open

    return False, False


def classify_all(lines):
    """Return [(ok, text), ...] for a whole file's lines, in order."""
    result = []
    in_block = False
    for text in lines:
        ok, in_block = is_comment_or_blank(text, in_block)
        result.append((ok, text))
    return result


def parse_diff_hunks(diff_text):
    """Yield (old_file, new_file, old_start, old_count, new_start, new_count).

    old_file/new_file are None for a side that does not exist (added or
    deleted file). Paths have their a/ b/ prefix stripped.
    """
    old_file = None
    new_file = None
    for raw in diff_text.splitlines():
        if raw.startswith("--- "):
            path = raw[4:].strip()
            old_file = None if path == "/dev/null" else _strip_prefix(path)
        elif raw.startswith("+++ "):
            path = raw[4:].strip()
            new_file = None if path == "/dev/null" else _strip_prefix(path)
        elif raw.startswith("@@"):
            m = HUNK_RE.match(raw)
            if not m:
                continue
            old_start = int(m.group(1))
            old_count = int(m.group(2)) if m.group(2) is not None else 1
            new_start = int(m.group(3))
            new_count = int(m.group(4)) if m.group(4) is not None else 1
            yield (old_file, new_file, old_start, old_count, new_start, new_count)


def _strip_prefix(path):
    return path[2:] if path.startswith(("a/", "b/")) else path


def blob_lines(rev, path):
    """Lines of `path` as it exists at `rev` (a commit-ish, INDEX, or WORKTREE).

    None if the file does not exist there (added/deleted).
    """
    if rev == WORKTREE:
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                return fh.read().splitlines()
        except OSError:
            return None

    spec = ":{}".format(path) if rev == INDEX else "{}:{}".format(rev, path)
    result = subprocess.run(
        ["git", "show", spec], capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        return None
    return result.stdout.splitlines()


def find_offenders(diff_text, old_spec, new_spec):
    """Yield (file, line_number, side, line_text) for each non-comment change."""
    old_cache = {}
    new_cache = {}

    for old_file, new_file, old_start, old_count, new_start, new_count in parse_diff_hunks(diff_text):
        if old_count > 0 and old_file and old_file.endswith(".cs"):
            if old_file not in old_cache:
                lines = blob_lines(old_spec, old_file)
                old_cache[old_file] = classify_all(lines) if lines is not None else None
            classified = old_cache[old_file]
            if classified is not None:
                for lineno in range(old_start, old_start + old_count):
                    idx = lineno - 1
                    if 0 <= idx < len(classified):
                        ok, text = classified[idx]
                        if not ok:
                            yield (old_file, lineno, "-", text)

        if new_count > 0 and new_file and new_file.endswith(".cs"):
            if new_file not in new_cache:
                lines = blob_lines(new_spec, new_file)
                new_cache[new_file] = classify_all(lines) if lines is not None else None
            classified = new_cache[new_file]
            if classified is not None:
                for lineno in range(new_start, new_start + new_count):
                    idx = lineno - 1
                    if 0 <= idx < len(classified):
                        ok, text = classified[idx]
                        if not ok:
                            yield (new_file, lineno, "+", text)


def resolve_specs(cached, rev_range):
    """Return (old_spec, new_spec) for blob_lines(), given the CLI mode."""
    if cached:
        return ("HEAD", INDEX)
    if rev_range:
        if ".." not in rev_range:
            raise ValueError("expected a rev..rev range, got: " + rev_range)
        old_rev, new_rev = rev_range.split("..", 1)
        new_rev = new_rev.lstrip(".")
        if not old_rev or not new_rev:
            raise ValueError("expected a rev..rev range, got: " + rev_range)
        return (old_rev, new_rev)
    return ("HEAD", WORKTREE)


def diff_for(cached, rev_range):
    if cached:
        cmd = ["git", "diff", "--cached", "--diff-filter=ACMR", "-U0", "--", "*.cs"]
    elif rev_range:
        cmd = ["git", "diff", rev_range, "--diff-filter=ACMR", "-U0", "--", "*.cs"]
    else:
        cmd = ["git", "diff", "--diff-filter=ACMR", "-U0", "--", "*.cs"]
    result = subprocess.run(cmd, capture_output=True, text=True, check=False)
    if result.returncode not in (0, 1):
        sys.stderr.write(result.stderr)
        sys.exit(2)
    return result.stdout


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument(
        "range",
        nargs="?",
        default=None,
        help="A revision range (rev..rev). Omit for working tree vs HEAD.",
    )
    parser.add_argument(
        "--cached",
        action="store_true",
        help="Compare the index against HEAD instead of the working tree.",
    )
    args = parser.parse_args(argv)

    if args.cached and args.range:
        parser.error("--cached and a revision range are mutually exclusive")

    try:
        old_spec, new_spec = resolve_specs(args.cached, args.range)
    except ValueError as exc:
        parser.error(str(exc))

    diff_text = diff_for(args.cached, args.range)
    offenders = list(find_offenders(diff_text, old_spec, new_spec))

    for path, lineno, side, content in offenders:
        loc = "{}:{}".format(path, lineno if lineno is not None else "?")
        print("{} {}{}".format(loc, side, content))

    return 1 if offenders else 0


if __name__ == "__main__":
    sys.exit(main())
