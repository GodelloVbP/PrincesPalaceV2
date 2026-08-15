# Workflow

How to work on this project efficiently and safely — session rituals, the
rules that keep parallel sessions from destroying each other's work, and the
decision tables that replace re-deriving the same answer every session.

This document itself has an update rule: see the index at the bottom. If you
change how the project is worked on, this file is where that goes.

---

## 1. Session start ritual

Two reads, then work:

1. `CLAUDE.md` — the two golden rules (scenes and content are generated,
   never hand-edited) and the five gotchas. Everything here assumes you've
   read it. Its first block is the v1/v2 check; do not skip it.
2. `docs/CODE_MAP.md` — screens/systems to files. Finding the right file by
   name beats grepping a 4,000-line file window by window.

Then, before touching anything:

- `git status --short` — know which changes in the tree are yours before you
  stage or reset anything.
- `git config core.hooksPath` — must print `tools/githooks`. It is local
  config, so it does not travel with a clone or a fresh checkout, and the
  pre-commit gate is silently absent until it is set:
  ```bash
  git config core.hooksPath tools/githooks
  ```
- Check `Temp\UnityLockfile` in both `C:\Games\Prince's Palace-v2-TestRunner`
  and `-v2-TestRunner2` before running tests — if it's locked, another session
  is mid-run against the same isolated copies. (Those paths said
  `Prince's Palace-TestRunner` until 2026-08-15, which are v1's directories and
  do not exist here — the same wrong-tree confusion that once made a v2 test
  run build into v1.)

## 2. State the intent before non-trivial work

Before anything bigger than a one-line fix, write three lines and let the user
veto them. Ten seconds of reading beats an hour of the wrong work.

> **Change:** the frame around the slot grid, not the slot cells.
> **Don't touch:** cell size, the armour art.
> **Done when:** `test.ps1 ui` green, and `screenshot.ps1 -Panel Inventory`
> shows a wider frame with the cells unmoved.

Each line does a specific job, and they are not interchangeable:

- **Change** names the *layer*. A container and its contents can both get
  "bigger" from one instruction, and picking the wrong one costs a full redo.
  This has happened more than once.
- **Don't touch** is where a misreading becomes visible. If the wrong layer was
  understood, this line contradicts the first one obviously enough to catch.
- **Done when** is the one most often missing and the one that does the most
  work. It forces the acceptance check to exist *before* the code does, and it
  is what makes "is this finished" a question with an answer rather than a
  judgement call.

This is the tier below a design gate (§5), not a replacement for it. Anything
with real scope still gets a handoff doc.

Two more briefing patterns worth keeping in mind, both earned by rework:

- **For behaviour and AI changes, write trigger → condition → outcome.** The
  clearest spec this project has had was implemented correctly first try
  because nothing was left to interpret.
- **Describe the symptom, not the theory of the cause.** "Still doesn't fit
  perfectly" invites guessing at why and burns rounds. "The left edge overlaps
  by about this much" points straight at the fix. Diagnosing the cause is the
  implementer's job; reporting precisely what was seen is the more useful half.

## 3. Efficiency practices

These are why the project got split up and documented in the first place —
treat them as standing rules, not suggestions:

- **Delegate multi-file research to an Explore agent.** A question that
  touches more than 2-3 files burns far less of the main session's context
  when a subagent reads the files and returns a distilled report, versus
  reading them all inline.
- **Read small files whole, not windows of big ones.** Post-split,
  `SceneBuilder.Talents.cs` or `FightController.Beats.cs` are each small
  enough to read in one call. Don't grep-and-window a file you can just read.
- **Targeted tests in the inner loop, full suite only at commit gates.**
  `tools/test.ps1 <area>` or `tools/test.ps1 -Changed` beats a ~90-100s full
  run every edit-compile-check cycle. See the decision table below.
- **Screenshot QA only for UI-visible changes.** Don't burn a Unity batch
  launch confirming a change that has no visual surface.
- **Decision tables over re-derivation.** The test-run table and the doc
  update-rules index below exist so nobody re-figures out "which command do
  I run" or "which doc do I update" from scratch every session.

## 4. Parallel-session rules

Two Claude sessions have run against this repo at once before. They share
**one working tree** and **one pair of TestRunner copies**. One session ran
`git add -A` and swept ~58 files of the other session's uncommitted work into
its own branch commit, then `git checkout main` — deleting all of it from
disk. Full account: `docs/INCIDENTS.md`.

Rules, non-negotiable:

- **Stage by explicit path, always.** Never `git add -A`, never `git add .`,
  never `git add -u`, never `git commit -a`. If you don't know exactly what a
  broad add would pick up, run `git status` first and read it. A PreToolUse
  hook now refuses these commands outright
  (`tools/githooks/deny_broad_staging.py`), but the hook is a backstop for the
  rule, not a substitute for knowing it.
- **One dedicated branch per session.** Don't both work on `master` or the same
  feature branch at once.
- **Nothing uncommitted at session end.** A WIP commit on your own session
  branch costs nothing, and it is the only reason the incident above was
  recoverable at all. Long-lived uncommitted work is what a broad add eats.
- **Check the TestRunner lock before test runs** (see §1). A concurrent
  `run_tests.ps1`/`run_tests_parallel.ps1` from another session clobbers your
  in-flight run, and Unity itself refuses a second batchmode instance against
  a project already open.
- **Freeze protocol for shared-file refactors.** Before starting a
  multi-hundred-line change to a file another session might also touch,
  announce it, and other sessions commit-or-stash and pause work on that file
  *and all test runs* until the refactor lands — same day, not left open
  overnight.
- **If work vanishes and `git status` is unexpectedly clean**, check
  `git reflog` and the other branch's tip before assuming it's gone.

## 5. Feature lifecycle

1. **Design gate.** For anything with real scope (a new screen, a rebalance,
   a system rework), get the user to confirm shape/economy *before* writing
   code. Don't guess at numbers a design decision should set. Smaller work
   gets the three-line intent header instead (§2).
2. **Handoff.** New/major-rework screens get a handoff doc in
   `docs/handoffs/<slug>/` — see `docs/HANDOFF_TEMPLATE.md` for the shape.
3. **Build.**
4. **Gap audit.** Section-by-section: what the handoff asked for vs. what got
   built. `GAP_AUDIT.md` in the same handoff folder — see the template.
5. **Verify.** Tests (targeted, then full before commit) + screenshot for
   anything UI-visible.
6. **Land.** Commit, explicit paths.
7. **Update `docs/CODE_MAP.md`** if a screen, system, or file moved or was
   added.

## 6. Bug protocol

- Reproduce with a test first when it's feasible to.
- Fix the mechanism, not the symptom, where the two differ.
- Pin the fix with literals where a formula's involved — see
  `docs/CODE_STANDARDS.md` §5 on why.
- If the bug closes an `AUDIT.md` finding, strike it in the *same* commit —
  see `AUDIT.md`'s own "how this register works" preamble.

## 7. Flake protocol

**Three strikes: the third failure of the same test is a mandatory
root-cause, not another "passed on rerun."** A flake that's shrugged off
twice and investigated the third time is still cheaper than one that's never
investigated at all.

Triage order, cheapest first:

1. Read the actual failure log (`test-run-<Platform>.log` in the relevant
   TestRunner copy) before theorizing. Classify: does it smell like
   **wiring** (NullReference, missing-script, an import/GUID-shaped
   exception) or **logic** (wrong value, wrong branch, no exception at all)?
2. Correlate against the triggering condition. Does it only happen after a
   fresh `-BuildContent`/`-BuildScenes`? Only under a specific test order?
   Only concurrently with the other platform's run?
3. If it's environmental, prove any fix with **3 consecutive runs green**
   under the exact provoking condition — not one.
4. **Timebox it** (a rough session, not open-ended). If root cause doesn't
   resolve in that window: add the loudest, cheapest assertion that would
   catch the *class* of bug you suspect (see `Assert-GuidsMatch` in
   `tools/run_tests_parallel.ps1` for the worked example), write up what was
   ruled out and what remains open as a new `AUDIT.md` finding under "Open
   investigations", and move on. Don't let one flake block everything else.

## 8. Test-run decision table

| Situation | Command |
|---|---|
| Iterating on one class/area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 <fuzzy-name or area>` (~12s for a single class, more for a broad area; areas: `combat`, `hub`, `content`, `run`, `ui`, `art`, `rng`, defined in `tools/test_areas.ps1`; `-List` shows everything) |
| Iterating across several touched files, not sure which area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed` — maps uncommitted changes (tracked + untracked) to the areas/classes they affect and runs just that, printing the mapping it used |
| Before any commit | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1` (~90-100s). **Refuses to run at all** if any test class matches no area, or if a `[Test]`/`[UnityTest]` file's class was never discovered — both point at `tools/test_areas.ps1` and name the class/file; there is no bypass flag, fix the pattern |
| Changed `ContentData/*.json` or `ContentBuilder.cs` | add `-BuildContent` |
| Changed `SceneBuilder.cs` or anything scene-affecting | add `-BuildScenes` (auto-syncs regenerated scenes+content back to main — this is not optional bookkeeping, skipping it means the next run silently reverts what you just built) |
| Re-running with nothing changed since the last synced build | `-SkipSync` is legal |
| UI-visible change | also `tools/screenshot.ps1 -Panel <Name>` and actually look at the PNG |
| Change to anything that MOVES (an ambient animator, an intro, a tween) | `tools/screenshot.ps1 -Runtime`. `-Panel`/`-All` render Edit Mode, which never ticks `Update()` — a static capture cannot show motion at all, and the pure-curve unit tests only prove the formulas vary, not that anything calls them |

## 9. Commit conventions

- Messages explain **reasoning and tradeoffs**, not just the change — the
  git log is the project's real history, deliberately, because chat context
  doesn't survive between sessions.
- `.cs`/`.png`/etc. files and their `.meta` are always committed together.
  Never one without the other (a missing `.meta` regenerates a GUID on the
  next fresh Library rebuild and orphans every reference to it). Enforced by
  `tools/githooks/pre-commit`, which refuses a commit that would leave an
  asset without its `.meta` or a `.meta` without its asset.
- Regenerated scenes are committed with whichever change caused the
  regeneration, not separately.
- New-file sequence: create the file → `run_tests_parallel.ps1 -BuildScenes`
  (this is what actually generates its `.meta` and syncs it back to main,
  across both TestRunner copies) → commit both.

## 10. Enforced rules

Rules that stopped being documentation and became something that fails. When
you change one of these, change its enforcement in the same commit — a rule
whose check has drifted is worse than one with no check, because it reads as
covered.

| Rule | Enforced by | Fails at |
|---|---|---|
| Layout has no overlaps/overflow/zero-sized graphics at four aspects | `UiAudit` | scene build |
| No serialized UI reference left null | `UiWiringSweep` | scene build |
| No `SetField`-style reflection wiring | lint test | test run |
| Every test class maps to an area | `run_tests_parallel.ps1` | test run, no bypass |
| Asset and `.meta` commit together | `tools/githooks/pre-commit` | commit |
| Staged `.ps1` is ASCII outside comments | `tools/githooks/pre-commit` | commit |
| No `git add -A/-u/.`, no `git commit -a` | `tools/githooks/deny_broad_staging.py` | before the command runs |

The two git hooks need `git config core.hooksPath tools/githooks` (§1) and, for
the staging hook, `.claude/settings.json` — which Claude Code only picks up at
session start, so a session running when it was added must restart before it
takes effect.

## 11. Doc update-rules index

The enforcement spine — if you did the left column, do the right column in
the same commit:

| When you… | Touch… |
|---|---|
| Add/rename a screen, system, or partial-class part file | `docs/CODE_MAP.md` |
| Add an art kit, or change a keying/delivery convention | `docs/ART_PIPELINE.md` |
| Fix an `AUDIT.md` finding | `AUDIT.md` (strike it, cite the commit) |
| Add a panel to `SceneBuilder` | `ScreenshotTool.cs`'s `KnownPanels` table + `screenshot.ps1`'s usage text |
| Change test-tooling behavior or its timing characteristics | that tool's own header comment |
| Finish (or partially close) a handoff's implementation | its `GAP_AUDIT.md` verdicts |
| Change a workflow rule, ritual, or convention | this file |
| Change a rule that a hook enforces | that hook's script, plus §10 above |
| Lose time to something avoidable | `docs/INCIDENTS.md`, with what now prevents it |
