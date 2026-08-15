"""Refuse broad git staging commands before they run.

Wired as a PreToolUse hook on the Bash and PowerShell tools; see
.claude/settings.json. Reads the hook payload on stdin, writes a deny decision
on stdout when the command would stage more than it names, and stays silent
otherwise.

Why this exists as code rather than a line in CLAUDE.md: this repo is sometimes
worked on by two sessions sharing one working tree. `git add -A` has already
swept about 58 files of another session's uncommitted work into the wrong
branch's commit, which a following `git checkout` then deleted from disk. It
was recoverable only because it had landed in a commit at all. The rule was
written down afterwards, and a written rule is exactly as strong as whoever
happens to be reading it.

The command is tokenised rather than regex-matched. A regex over the raw string
blocks `git commit -m "add -a note"`, because the flag it is looking for is
sitting inside a quoted message. Shell quoting is the whole difficulty here, so
the shell's own lexer does the work.
"""

import json
import shlex
import sys

# Arguments to `git add` that stage by sweep instead of by name. `-u` earns its
# place next to `-A`: it stages every tracked modification, which is precisely
# how the other session's 58 files were picked up - they were edits to files
# already in the index, not new ones.
SWEEPING_ADD_ARGS = {
    "-A", "--all", "--no-ignore-removal",
    "-u", "--update",
    ".", "./", ":/", ":",
}

SEPARATORS = {"&&", "||", ";", "|", "&"}

# Global options that take a separate value, so the subcommand is one token
# further along than it looks: `git -C some/dir add -A`.
GIT_GLOBALS_WITH_VALUE = {"-C", "-c", "--git-dir", "--work-tree", "--namespace"}

ADVICE = (
    "Stage by explicit path instead: `git add <path> [<path>...]`.\n"
    "See CLAUDE.md 'Never git add -A' and docs/WORKFLOW.md section 3."
)


def tokenise(command):
    """Split a shell command into tokens, keeping separators as their own.

    Returns None if the command does not lex - an unbalanced quote, most
    likely. The caller treats that as 'cannot tell' and allows it through.
    """
    try:
        lexer = shlex.shlex(command, posix=True, punctuation_chars=True)
        lexer.whitespace_split = True
        return list(lexer)
    except ValueError:
        return None


def segments(tokens):
    """Yield each separator-delimited run of tokens as its own command."""
    current = []
    for token in tokens:
        if token in SEPARATORS:
            if current:
                yield current
            current = []
        else:
            current.append(token)
    if current:
        yield current


def offence(segment):
    """Return a reason string if this one command stages broadly, else None."""
    if not segment:
        return None

    # Accept `git`, `git.exe`, and an absolute path to either.
    head = segment[0].replace("\\", "/").rsplit("/", 1)[-1].lower()
    if head not in ("git", "git.exe"):
        return None

    # Walk past git's own global options to reach the subcommand.
    i = 1
    while i < len(segment) and segment[i].startswith("-"):
        if segment[i] in GIT_GLOBALS_WITH_VALUE:
            i += 2
        else:
            i += 1
    if i >= len(segment):
        return None

    subcommand = segment[i]
    args = segment[i + 1:]

    if subcommand in ("add", "stage"):
        for arg in args:
            if arg in SWEEPING_ADD_ARGS:
                return (
                    "`git {} {}` stages every change in the tree, including "
                    "any belonging to another session sharing this working "
                    "tree.".format(subcommand, arg)
                )
            # Clustered short flags: -Av, -uf.
            if (
                len(arg) > 1
                and arg[0] == "-"
                and arg[1] != "-"
                and ("A" in arg[1:] or "u" in arg[1:])
            ):
                return (
                    "`git {} {}` bundles a sweeping flag (-A/-u) into a short "
                    "flag cluster.".format(subcommand, arg)
                )

    if subcommand == "commit":
        for arg in args:
            if arg == "--all":
                return "`git commit --all` stages every tracked modification."
            if (
                len(arg) > 1
                and arg[0] == "-"
                and arg[1] != "-"
                and "a" in arg[1:]
            ):
                return (
                    "`git commit {}` carries -a, which stages every tracked "
                    "modification before committing.".format(arg)
                )

    return None


def main():
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    command = (payload.get("tool_input") or {}).get("command")
    if not isinstance(command, str) or not command.strip():
        return 0

    tokens = tokenise(command)
    if tokens is None:
        return 0

    for segment in segments(tokens):
        reason = offence(segment)
        if reason:
            json.dump(
                {
                    "hookSpecificOutput": {
                        "hookEventName": "PreToolUse",
                        "permissionDecision": "deny",
                        "permissionDecisionReason": reason + "\n\n" + ADVICE,
                    }
                },
                sys.stdout,
            )
            return 0

    return 0


if __name__ == "__main__":
    sys.exit(main())
