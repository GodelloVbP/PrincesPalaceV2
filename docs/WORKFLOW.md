# Workflow

Ownership, briefs, parallel sessions and commits. Routing and the
`Escalation:` criteria live in `CLAUDE.md`; gate selection in
`docs/TESTING.md`.

## Setup

`git config core.hooksPath` must print `tools/githooks` (set it if not). Hooks and
`.claude/agents/` load at session start; restart to pick up a change.

## Ownership

- One owner per issue: diagnosis, implementation, focused tests,
  correction. No duplicate owner; no replacement launch without a stated
  reason. Reuse an owner while its context on the issue is still relevant.
- At most three active agents. Never two owners on one file at once;
  serialize overlapping edits and combine tightly coupled issues.
- An owner whose context is largely obsolete writes a state record to the
  scratchpad (findings, decisions, files changed, verification, next step);
  a fresh owner starts from it.
- After two failed correction cycles, reassess cause and scope, then allow a
  third attempt or relaunch as `senior` with the fitting `Escalation:`.
- A worker that thinks a rule is wrong says so in its report and follows it;
  the orchestrator raises it with the user.
- Ask the user only for decisions that are irreversible, preference-only and
  unanswerable from the code. Otherwise state the assumption and proceed.

## Briefs

Before anything bigger than a one-line fix, state the intent:
**Change:** / **Don't touch:** / **Done when:** (acceptance evidence).
Behaviour changes are briefed as trigger → condition → outcome; bugs as the
symptom, not a theory of the cause.

```
Objective:
Done when: (test names, screenshot condition, or file state)
Owner: (agent type)
Edit surface: (paths the owner may change)
Checked for existing systems: (what was searched, what it hooks into)
References: (path + section, never pasted text)
Verification: (exact command)
Stop conditions: completion, a plan-changing blocker, or a rule that seems wrong.
```

Workers start without conversation history unless the work depends on it;
the brief then says why.

A novel screen or major rework gets a design spec first: layout at
1920x1080, every interactive state, interaction rules, out-of-scope. A
prototype outranks any table transcribed from it. Before claiming it built,
audit the build against the spec section by section.

## Verification runs

One verifier runs the selected gate once, in one foreground call, and
reports the state it verified (HEAD sha + uncommitted files). Rerun only
when gate-relevant files changed, a failure was fixed, or the result was
invalid. No progress polling.

Shell searches name a root (`Assets/_Project`, `docs`, `tools`) and exclude
`Library`, `Temp`, `.claude/worktrees`, `reports`, `tools/screenshots`.

## Parallel sessions

Two sessions can share one working tree and the TestRunner copies.

- One branch per session. Nothing uncommitted at session end: a WIP commit
  costs nothing, an untracked file is unrecoverable.
- Runner copies are claimed by the tools themselves; `waiting for <runner>
  -- held by <tool>` is another session, not a hang.
- Freeze protocol: announce a multi-hundred-line refactor first; others
  commit or stash, then pause edits to that file and all test runs until it
  lands.
- Work vanished? Check `git reflog` and the other branch's tip first.

## Commits

- Messages give the reasoning and tradeoffs; the git log is the history.
- An asset and its `.meta` commit together; regenerated scenes commit with
  the change that caused them. New file: create it, run
  `tools/run_tests_parallel.ps1 -BuildScenes` (generates and syncs its
  `.meta`), commit both.
- Closing an `AUDIT.md` finding strikes it in the same commit.

## When you change X, also update Y

| Change | Also update |
|---|---|
| Add/rename a screen, system or part file | `docs/CODE_MAP.md` |
| Add an art kit or keying/delivery convention | `docs/ART_PIPELINE.md` |
| A test command or flag `CLAUDE.md` Verification names | `CLAUDE.md` too |
| Exclude a test file from the dotnet host | `tools/domain-tests/README.md` |
| What a balance batch writes | `docs/BOT_SUMMARY_SCHEMA.md` |
| An agent's role or model | `.claude/agents/<name>.md` and the `route_agents.py` pin, same commit |
