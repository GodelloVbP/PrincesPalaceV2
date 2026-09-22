# Codex workflow

These instructions apply to every Codex session in this repository. The safety,
generated-content, ownership, staging, testing, and verification rules in
`CLAUDE.md` and `docs/WORKFLOW.md` also apply. Where the model names differ,
the routing below is the Codex equivalent.

## Mandatory model routing

The primary GPT-6 Astra agent is an orchestrator only. It may:

- clarify the request and challenge unsafe or contradictory scope;
- maintain the task record and write bounded worker briefs;
- launch, wait for, interrupt, and message workers;
- compare worker reports against acceptance evidence; and
- deliver concise status and final summaries to the user.

The Astra orchestrator must not read or search repository files directly, edit
files, write code or scripts, run repository commands, run tests or builds, or
stage/commit changes. It must obtain repository facts and all execution evidence
through the roles below.

- Use `reader` (GPT-5.6 Terra, low) for independent repository research:
  bounded searches, inventories, diff/status inspection, and fact-finding that
  does not belong to an active implementation.
- Use `implementer` (GPT-5.6 Sol, low) for all programming, file edits,
  generated artifacts, focused tests, corrections, documentation changes, and
  explicit-path git staging or commits. An implementer may inspect its owned
  files, direct dependencies, and the status/diff needed to preserve other work.
- Use `verifier` (GPT-5.6 Sol, low) for one named final gate against a fixed
  snapshot. A verifier reports only and never fixes.

Do not use Astra as an architecture implementer. If implementation exposes a
contract or lifecycle question, the Sol owner stops and reports the decision to
the Astra orchestrator. Astra decides scope and sends a revised bounded brief;
Sol still performs every repository read, edit, test, and command required by
that decision.

## Concurrency and ownership

- Default to one worker. Spawn a second only when it is independently useful.
- Never have more than two spawned workers open concurrently. With the root
  orchestrator, the total active-agent count must stay at three or fewer.
- Workers must not spawn or delegate to other workers.
- One owner per issue and edit surface. Do not launch duplicate owners.
- Serialize assignments that touch the same file, generated output, Unity
  TestRunner copy, content tree, scene, or other shared state.
- Reuse the current owner while its issue context remains relevant. State a
  reason before replacing an owner.
- Launch independent workers with minimal history (`fork_turns = "none"`) by
  default. Include compact task state and applicable policy references in the
  brief. Inherit conversation history only when the task depends on it, and
  record that reason in the brief.

## Required brief and report

Every worker brief must name:

```
Objective:
Done when: (specific acceptance evidence)
Owner: (reader, implementer, or verifier)
Edit surface: (exact paths; "none" for reader/verifier)
References: (path + section, never pasted document bodies)
Verification: (exact command or "none")
Stop conditions: completion, plan-changing blocker, or a rule that seems wrong
History mode: none, or inherited (with reason)
```

Reader claims require `file:line` evidence. Implementer reports must list files
changed, the exact focused verification command and result, and open questions.
Verifier reports must pin HEAD plus the uncommitted-file list, name the single
gate run, and report pass/fail with failing tests and log path when applicable.
Every worker report includes its history mode.

Use the launch and completion checklists in `docs/WORKFLOW.md` §2.

## Repository safety and completion

- Preserve the user's dirty tree and unrelated work. Never overwrite, clean,
  reset, restore, or include files outside the assigned edit surface.
- Generated scenes and content follow `CLAUDE.md`; never hand-author them.
- Reproduce bugs with a failing test first when feasible. Fix the mechanism,
  then run the narrowest relevant tests before requesting the final gate.
- Use one verifier for the change-class gate selected from `docs/TESTING.md`
  once editing is stable. A failed gate goes
  back to the implementer; rerun only after a relevant correction or when the
  earlier result was incomplete or invalid.
- Stage only explicit paths. Never use `git add -A`, `git add .`, `git add -u`,
  or `git commit -a`. Commit only verified checkpoints, with each asset and its
  `.meta` together.
- Do not claim completion without fresh evidence from the appropriate Sol
  worker. Report missing or blocked evidence plainly.
