# Workflow

Ownership, briefs, parallel sessions and commits. Routing and the
`Escalation:` criteria live in `CLAUDE.md`; gate selection in
`docs/TESTING.md`.

## Setup

`git config core.hooksPath` must print `tools/githooks` (set it if not). Hooks and
`.claude/agents/` load at session start; restart to pick up a change.

## Ownership

- One owner per issue: diagnosis, implementation, focused tests,
  correction. No duplicate owner.
- Context reuse is mandatory. An existing agent (running or resumable)
  that holds overlapping context (same files, same system, or its own
  earlier output) is continued with SendMessage; launching a context-free
  agent for that work is not allowed.
- Launch fresh only when: (a) independence is the purpose (a context-free
  reviewer or second opinion); (b) the existing agent's context is obsolete
  or near its limit: it first writes a state record to the scratchpad
  (findings, decisions, files changed, verification, next step) and the new
  agent starts from it; (c) the work moves to another agent type or model
  (e.g. escalation to `senior`): the new brief references the predecessor's
  report or state record.
- A fresh agent never sees the conversation. A brief pastes no chat
  history; context reaches it only by reference (path to a report or state
  record), and the brief says why it is needed.
- At most three active agents. Never two owners on one file at once;
  serialize overlapping edits and combine tightly coupled issues.
- After two failed correction cycles, reassess cause and scope, then allow a
  third attempt or relaunch as `senior` (reason (c)) with the fitting
  `Escalation:`. The orchestrator may escalate earlier with
  `Escalation: judgment - <reason>`.
- A worker that thinks a rule is wrong says so in its report and follows it;
  the orchestrator raises it with the user.
- Ask the user only for decisions that are irreversible, preference-only and
  unanswerable from the code. Otherwise state the assumption and proceed.

## Briefs

A one-line fix needs no brief. Anything bigger uses this template:

```
Objective: (behaviour changes as trigger -> condition -> outcome; bugs as the symptom, not a theory of the cause)
Done when: (acceptance evidence: test names, screenshot condition, or file state)
Owner: (agent type)
Context: continues <agent> | fresh because (a|b|c): <why>
Edit surface: (paths the owner may change; empty for read-only work)
Don't touch: (optional)
Checked for existing systems: (what was searched, what it hooks into)
References: (path + section, never pasted text)
Verification: (exact command)
Stop conditions: completion, a plan-changing blocker, or a rule that seems wrong.
```

A novel screen or major rework gets a design spec first: layout at
1920x1080, every interactive state, interaction rules, out-of-scope. A
prototype outranks any table transcribed from it. Before claiming it built,
audit the build against the spec section by section.

## Fixer trial

`fixer` runs on Haiku 5.5 as a trial. The orchestrator reviews every fixer
diff before commit and logs every launch in
`docs/measurements/fixer_haiku_trial.md`, one row each: date, brief
one-liner, outcome (done / correctly stopped / wrongly stopped / wrong fix),
correction cycles, out-of-scope edits (y/n), gate result, which agent caught
the mistake.

Exit after 10 launches: keep Haiku if no wrong fix reached a commit and
correction cycles are at most one per three launches; otherwise revert
`fixer` to Sonnet 5.5 (agent file, `route_agents.py` pin, tests, `CLAUDE.md`
row).

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
