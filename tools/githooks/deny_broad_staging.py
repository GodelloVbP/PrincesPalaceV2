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

TWO THINGS SIT BETWEEN THE COMMAND STRING AND THE CHECK, and both of them have
been where a bypass lived rather than in the check itself:

  * WHERE ONE COMMAND ENDS. `&&`, `||`, `;` and `|` were the whole list, and a
    NEWLINE was not on it -- shlex treats it as ordinary whitespace, so
    `git status\ngit add -A` lexed to one run of tokens whose subcommand was
    `status`. A Bash call in this repo carrying several lines is the normal
    case, so that was not an exotic hole. Every line is therefore ALSO lexed
    on its own; a line that does not lex by itself (the middle of a multi-line
    quoted string, a heredoc body) is skipped rather than guessed at.
  * WHICH TOKEN IS THE PROGRAM. It was token 0, flatly, so anything the shell
    lets you put in front of a command hid it: `(`, `{`, the `then`/`do` of a
    compound statement, an environment assignment, or an ordinary Unix
    wrapper program that execs its trailing arguments as a child process
    (`env`, `nice`, `time`, `sudo`, `command`). Those are stripped now. `time`
    is a Git-Bash/MSYS shell reserved word, not only a program, so timing a
    slow stage over a large tree was a realistic accidental bypass on this
    repo's own documented shell, not merely an adversarial one.

Both are tested in deny_broad_staging_test.py, one case per shape.

A THIRD GAP, in the check itself rather than before it: SWEEPING_ADD_ARGS only
enumerated the four spellings CLAUDE.md names (-A/--all/-u/--update and the
"." family). `git add <some directory>` was never checked at all, even though
the stated danger -- sweeping another session's uncommitted work -- applies
just as much to a broad subtree as to the whole tree, only scoped smaller.
is_directory_arg() closes that: a trailing separator is a directory by syntax
alone, and anything else is checked against the real filesystem so a real
extensionless filename is never mistaken for one.
"""

import json
import os
import re
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

SEPARATORS = {"&&", "||", ";", "|", "&", "\n"}

# Shell words that can legally precede a command without being the command:
# the openers of a subshell or brace group, the keywords that introduce the
# body of a compound statement, `!`, and the ordinary wrapper programs that
# take the real command as trailing arguments and exec it (env/nice/time/
# sudo/command -- see the module docstring). An environment assignment
# (`GIT_PAGER=cat git ...`) is matched by ASSIGNMENT below rather than listed.
COMMAND_PREFIXES = {
    "(", "{", "!", "then", "do", "else", "elif",
    "env", "nice", "time", "sudo", "command",
}

ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")

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


def commands(command):
    """Yield every token run in the string that could be a command.

    Two passes over the same text, because neither alone is right. The whole
    string lexed at once is what handles a quoted message containing a
    newline; each line lexed on its own is what handles the far commoner case
    of a multi-line Bash call, which the first pass runs together into one
    nonsense command. A line that does not lex by itself -- a heredoc body,
    the middle of a multi-line string -- yields nothing rather than a guess.

    Duplicate segments across the two passes cost one extra `offence()` call
    on a handful of tokens, which is not worth de-duplicating.
    """
    tokens = tokenise(command)
    if tokens is not None:
        for segment in segments(tokens):
            yield segment

    lines = command.splitlines()
    if len(lines) < 2:
        return

    for line in lines:
        tokens = tokenise(line)
        if tokens is None:
            continue
        for segment in segments(tokens):
            yield segment


def is_directory_arg(arg):
    """True if `arg` names a directory rather than a file.

    A trailing separator is always a directory by syntax alone -- `foo/`
    means "the directory named foo" whether or not it currently exists.
    Anything else is checked against the real filesystem (os.path.isdir),
    not guessed at from spelling, so a real extensionless filename is never
    mistaken for one.
    """
    if arg.endswith("/") or arg.endswith("\\"):
        return True
    return os.path.isdir(arg)


def offence(segment):
    """Return a reason string if this one command stages broadly, else None."""
    # `(`, `{`, `then`, `FOO=bar` -- shell words that sit in front of the
    # command without being it. Stripped before the program name is read, or
    # `(git add -A)` reads as a program called `(`.
    while segment and (segment[0] in COMMAND_PREFIXES or ASSIGNMENT.match(segment[0])):
        segment = segment[1:]

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

        # Second pass, after the exact-spelling check above: any remaining
        # argument that is not a flag and names a directory sweeps everything
        # under it -- the same hazard the four named spellings cover, just
        # scoped to one subtree instead of the whole tree.
        past_separator = False
        for arg in args:
            if arg == "--" and not past_separator:
                past_separator = True
                continue
            if not past_separator and arg.startswith("-"):
                continue
            if is_directory_arg(arg):
                return (
                    "`git {} {}` stages a whole directory, including any "
                    "change belonging to another session sharing this "
                    "working tree.".format(subcommand, arg)
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

    for segment in commands(command):
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
