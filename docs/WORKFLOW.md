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
  or near its limit: it first writes a state record (findings, decisions,
  files changed, verification, next step) and the new agent starts from it;
  (c) the work moves to another agent type or model (e.g. escalation to
  `senior`): the new brief references the predecessor's report or state
  record.
- A state record goes to the scratchpad if its reader runs in this session;
  one that must outlive the session is committed under
  `docs/handoffs/<topic>/` (snake_case folder, `README.md` first).
- Review is never done by the agent that produced the work: the reviewer
  launches fresh under (a).
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

## Plans

A `docs/PLAN_*.md` is final after two review rounds: v1, review, v2, review,
v3.

- Each round has two reviewers launched in parallel, both `implementer` with
  an empty edit surface: one with context (the research or audit inputs and
  the previous round's reviews), one without (plan and repo only, reason (a)).
- The author is continued, not relaunched, and revises after each round. The
  final plan carries a short review-disposition table: finding, accepted or
  rejected, why.
- A plan that redesigns a contract is authored by `senior` (`architecture`);
  any other plan by `implementer`.
- Every plan meets the structure requirement below; both reviewers check it.

Structure requirement: the final plan specifies the simplest structure that
delivers the design, and shows it:

1. One home per behaviour: every rule, formula, data shape and pipeline
   exists once. Name each shared entry point and every caller.
2. Extend before adding: name the existing system each piece extends
   (path); a new system states why extending fails.
3. Delete what it replaces: list the classes, files, branches and tests
   removed. No old and new path coexist after the phase that replaces them.
4. Data over code: content and tuning live in data. Show the path for the
   most common future additions as files touched, before vs after; the
   target is data only.
5. No speculative structure: share an abstraction only when it has two or
   more callers today; no hooks or options for features nobody has asked
   for. Where the model doesn't fit a case, change the model; no flags or
   special cases.
6. Efficient: fewest moving parts and least code for the same behaviour.
   State runtime cost only for per-turn or per-frame paths.
7. Shippable phases: every phase leaves the game working and tests green.

## Haiku lanes

`reader`, `verifier` and `fixer` run on Haiku 5.5; `fixer` is a trial. The
orchestrator logs in `docs/measurements/haiku_lanes.md`:

- Fixer trial: every launch, one row: date, brief one-liner, served model
  (launches before a session restart still run the old frontmatter model),
  outcome (done / correctly stopped / wrongly stopped / wrong fix),
  correction cycles, out-of-scope edits (y/n), gate result, which agent
  caught the mistake. The orchestrator reviews every fixer diff before
  commit.
- Reader/verifier incidents: date, agent, what went wrong, caught by, cost.

Exit and rollback:

- `fixer`, after 10 launches: keep Haiku if no wrong fix reached a commit
  and correction cycles are at most one per three launches; otherwise revert
  to Sonnet 5.5.
- `verifier`: revert to Sonnet on the first false PASS that reaches a commit.
- `reader`: revert to Sonnet after two misses that caused rework.

A revert changes the agent file, the `route_agents.py` pin, its tests and
the `CLAUDE.md` row.

## Verification runs

One verifier runs the selected gate once, in one foreground call, and
reports the state it verified (HEAD sha + uncommitted files), the gate's
exit code and its final summary line, quoted verbatim; the orchestrator
checks that quote before committing. Rerun only
when gate-relevant files changed, a failure was fixed, or the result was
invalid. No progress polling.

The owner reviews the `judgment` escalation list in `tools/usage_baseline.py`
output with each usage report.

Shell searches name a root (`Assets/_Project`, `docs`, `tools`) and exclude
`Library`, `Temp`, `.claude/worktrees`, `reports`, `tools/screenshots`.

## Parallel sessions

Two sessions can share one working tree and the TestRunner copies.

- One branch per session. Nothing uncommitted at session end (except a file
  an active agent is still editing, see Commits): a WIP commit costs
  nothing, an untracked file is unrecoverable.
- Runner copies are claimed by the tools themselves; `waiting for <runner>
  -- held by <tool>` is another session, not a hang.
- Freeze protocol: announce a multi-hundred-line refactor first; others
  commit or stash, then pause edits to that file and all test runs until it
  lands.
- Work vanished? Check `git reflog` and the other branch's tip first.

## Commits

- Never commit a file an active agent is editing; commit once it reports.
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
