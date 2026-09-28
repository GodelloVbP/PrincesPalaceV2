#!/usr/bin/env python3
"""Flag an ISO date inside a comment on an ADDED line of staged C#.

Reads a unified diff on stdin (the pre-commit hook feeds it
`git diff --cached -U0 -- '*.cs'`) and looks only at ADDED lines - lines
whose diff marker is a single leading '+' ('+++' file headers do not
count. An existing comment is never flagged, only a new one: history and
dates belong in the commit message, not in code, and a line already in
the tree passed that bar when it was added.

Only an ISO date (20[0-9][0-9]-[0-9][0-9]-[0-9][0-9]) trips this. Fuzzy
words like "recently" or "was" are deliberately not matched - they
false-positive on ordinary present-tense prose explaining why, which is
exactly what CLAUDE.md's "How to work" asks a comment to do.

Finding the comment, not just any '//' substring, needs one heuristic:
a '//' preceded by an odd number of double quotes on its line sits inside
a string literal, not in comment position, so it is skipped and the scan
resumes after it. This is deliberately simple (no escape handling, no
real tokeniser) - see CLAUDE.md's own note on this class of check, and
the pre-commit script's PowerShell-ASCII check, which takes the same
trade-off for the same reason.

Exit code is 1 if any violation is found, 0 otherwise. Usable standalone:

    git diff --cached -U0 -- '*.cs' | python3 tools/githooks/comment_history.py
"""

import re
import sys

DATE_RE = re.compile(r"20[0-9][0-9]-[0-9][0-9]-[0-9][0-9]")

RULE = (
    "dates/history belong in the commit message; comments say why, "
    "present tense - see CLAUDE.md How to work"
)


def comment_text(line):
    """Return the text after the first '//' that is not inside a string.

    None if the line has no such '//'. The heuristic: a '//' preceded by
    an odd number of double quotes on the line is inside a string literal
    (an odd count means an unclosed quote reaches this point), so it is
    skipped and the search continues past it.
    """
    start = 0
    while True:
        idx = line.find("//", start)
        if idx == -1:
            return None
        if line[:idx].count('"') % 2 == 0:
            return line[idx + 2:]
        start = idx + 2


def find_violations(diff_text):
    """Yield (file, line_number, line_text) for each offending added line."""
    current_file = None
    new_lineno = None

    for raw in diff_text.splitlines():
        if raw.startswith("+++ "):
            path = raw[4:].strip()
            if path == "/dev/null":
                current_file = None
            else:
                current_file = path[2:] if path.startswith(("a/", "b/")) else path
            continue

        if raw.startswith("@@"):
            match = re.search(r"\+(\d+)", raw)
            new_lineno = int(match.group(1)) if match else None
            continue

        if raw.startswith("+++") or raw.startswith("---"):
            continue

        if raw.startswith("+"):
            if new_lineno is None:
                continue
            content = raw[1:]
            is_cs = current_file is not None and current_file.endswith(".cs")
            if is_cs:
                text = comment_text(content)
                if text is not None and DATE_RE.search(text):
                    yield (current_file, new_lineno, content)
            new_lineno += 1
        elif raw.startswith("-"):
            continue
        else:
            if new_lineno is not None:
                new_lineno += 1


def main():
    diff_text = sys.stdin.read()
    violations = list(find_violations(diff_text))
    for path, lineno, content in violations:
        print("{}:{}: {}".format(path, lineno, RULE))
    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
