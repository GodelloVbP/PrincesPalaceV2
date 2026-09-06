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

## 2. Challenge the ask, then state the intent

### Line zero: is this the right thing to build?

Every other section here assumes the task is already agreed. This one does not.
Before writing the intent header, answer whether the request actually helps —
and say so if it does not.

The rule fires at one identifiable moment: **when you notice something is wrong
and are about to conclude it is fine.** That is the moment to say it. Deciding
on the author's behalf that a defect does not matter is the failure mode, and
it is a quiet one — nothing in the output looks wrong, so it never gets caught.

How to do it without stalling the work:

- **Say the concern in a sentence or two, then keep building.** Deliver the
  whole thing under a stated assumption. Do not stop and wait unless proceeding
  either way would be unsafe or would waste the work if the guess is wrong.
- **A reaffirmed request is a decision.** If the concern is raised and the
  author repeats the instruction, that settles it — build the full thing, say
  so plainly, and stop relitigating.
- **Never endorse an artifact while holding a reservation about it.** A handoff
  that contradicts itself, a `GAP_AUDIT.md` verdict softened to "close enough",
  a generated asset that violates its own brief — name the deviation in the
  same breath as the verdict, not two messages later after being asked twice.
  A recorded instance: an armour-stand asset whose brief said "no internal
  detail competing with the slot cells" shipped with pauldrons and joint seams
  landing exactly on the slot cells, and was called "exactly it".

What this is *not*: contrarianism, or a licence to relitigate settled design.
Several `AUDIT.md` findings are deliberate decisions belonging to the author;
the register records them rather than fixing them for exactly this reason.

Worth being clear about where the failures actually come from, because it
decides who should be deciding what. Most of this project's expensive mistakes
were **context** failures, not judgement ones — a session could not know it was
in the wrong checkout, could not know which layer "bigger" meant. The author
holds that information. The remaining kind are **candor** failures, where the
implementer knew and did not say. So the split that works is not "the
implementer decides more": it is *state the assumption in one line and keep
going*, plus *say the reservation at the moment it forms*. Handing more
unprompted authority to the side that lacks the context makes the first class
of error worse, because a confident wrong answer runs longer before anyone
sees it.

### The three lines

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
  `docs/CODE_STANDARDS.md` §8 on why.
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
| Iterating on one class/area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 <fuzzy-name or area>` (areas: `combat`, `hub`, `content`, `run`, `ui`, `art`, `rng` — each one a FOLDER under `Tests/EditMode/` and `Tests/PlayMode/`, so a test's area is simply where its file sits; `-List` shows every class with its area, marking each `[D]` or `[U]` for its host) |
| … and how long that takes | Depends entirely on whether the slice needs Unity. A slice that is all `[D]` classes runs under `dotnet test` with no editor at all: **~4s** (`test.ps1 wool` was ~12s, is 3.7s). A slice with any `[U]` class still boots Unity for those, and any slice touching PlayMode is PlayMode-bound — `test.ps1 combat` is ~110s either way, because 47 of its 122 classes are PlayMode and that is 93s of the run. The EditMode half of a mixed slice is still routed to dotnet, so it costs nothing extra |
| Checking the fast host against the slow one | `tools/test.ps1 <slice> -Unity` forces the whole slice through Unity. Use it when a dotnet result looks wrong, or after touching `tools/domain-tests`. Both hosts compile the same files, but not with the same NUnit (Unity ships 3.5, the host pins 3.14) — see `tools/domain-tests/README.md` |

| Iterating across several touched files, not sure which area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed` — maps uncommitted changes (tracked + untracked) to the areas/classes they affect and runs just that, printing the mapping it used |
| Before any commit | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1` (~4 min: 39s sync, then EditMode and PlayMode concurrently, PlayMode 181s of it). Unchanged by the dotnet host, deliberately — it is the only thing that compiles and runs everything under the NUnit and BCL Unity actually ships, which is what makes it the gate. Skipping its EditMode platform would save nothing anyway: PlayMode runs concurrently and dominates the wall clock. **Does not build the scenes** — see below. **Refuses to run at all** if a test file sits outside an area folder (or a testable file sits in `Shared/`), or if a `[Test]`/`[UnityTest]` file's class was never discovered — both name the file; there is no bypass flag, and the fix for the first is a `git mv`. This replaced a class-name-regex area table whose upkeep was 44 of its 47 commits in 90 days; a folder cannot drift the way a regex can |
| Changed `Domain/` or `Tests/EditMode/` and want to know the fast host still compiles | `dotnet build tools/domain-tests` (~2s warm). `tools/githooks/pre-commit` runs this for you when either is staged |
| Changed `ContentData/*.json` or `ContentBuilder.cs` | add `-BuildContent` |
| Just want the content assets rebuilt, no tests | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1` — ~14s warm, one batchmode Unity against **main**, in place. No runner copies, no sync-back to get wrong. Refuses while the Editor is open (Unity locks `Library` exclusively) and tells you to use the route below; a lockfile with no Unity behind it is recognised as debris and cleared |
| … and the Editor might be open | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Build` — same work, but it looks at the lockfile *and the process table* and picks: batchmode when nothing holds the project, `Editor/PreviewRequestWatcher.cs` when the Editor does. You never have to know which |
| Wrote an `enemies.json` row and want to SEE the mob | `tools/preview.ps1 -Enemy <id>` builds content, then writes `tools/screenshots/preview/<id>_stances.png` (a contact sheet of every pose it can reach) and `<id>_turn<n>_<ability>.png` (one frame per turn of its kit). ~45s. Add `-Launch` to play the fight in the Editor instead of photographing it, and `-Formation full` to field three copies so a summon has a slot to fail on. The id is validated against `enemies.json` before any Unity boots, and an unknown one prints the list |
| … and why the mob does its whole kit in order | `-Launch` and the capture route both hand the session an `EnemyShowcase`: authored abilities in order, one per turn, then the plain swing. Weighted randomness is right for playing and useless for looking. An ability whose prerequisite is unmet **this turn** is named and skipped rather than substituted |
| Wrote a `skills.json` row and want to SEE the spell | `tools/preview.ps1 -Spell <id>` builds content, then stands up a fight whose only purpose is that one cast and writes `tools/screenshots/preview/spell_<id>_{before,impact,after}.png` (plus `_ground.png` when the spell authors a ground layer). Who casts it is worked out from content, not asked: the character the skill names, else whoever carries the signature resource it spends. Mana and that resource are filled, ability requirements are waived one score at a time, the party opens at half health for a heal, and every one of those is printed rather than done quietly. `-Launch` plays it in the Editor instead, casting on turn one through the buttons a hand would press |
| … and it says "REFUSED" | Read the line: it names the effect or the id. Seven `SkillEffect`s have a stage arrangement a preview can build honestly; `Ward`, `Shatter`, the three Gifts, `Provoke` and `RestorePartyMana` need a talent tree, an existing ward or a drained party first, so they are refused rather than approximated. "No character in content carries resource X" is the other one, and it means what it says |
| Wrote a `characters.json` row and want to SEE them | `tools/preview.ps1 -Character <id>` fields them alone and writes four pictures: the map figure (walk stance), the dossier portrait, the fight stage at rest and the fight stage mid-cast — three different drawings in three different places, each with its own way of being wrong. Turn one casts the first row on their own kit. `-Launch` plays it instead. Remember the file also has to flag three starters (`startsInSquad`/`squadSlot`); the resolver refuses the build otherwise, naming ids |
| Delivered a combat actor's stance stills | `python tools/slice_actor_sheet.py --sheet ... --actor Enemies/<id> --stances ...` writes the stills, the `StanceManifest.json` `groundLine` entry and `Art/<Enemies\|Characters>/<id>/recipe.json`. Then **verify the recipe**: `--recipe <that path>` and check `git status --short Assets/_Project/Resources/` is clean. Clean means it replays byte-identical and the actor is reproducible; not clean means it belongs in `Art/Sheets/hand_assembled.json`'s `actors` block with its bytes pinned in `HandAssembledArtTests` instead. `docs/ART_PIPELINE.md` §5a is the full checklist |
| … and the manifest did not change | Expected. The slicer writes `groundLine` only when the entry is absent or says `groundLineSource: "slicer"`; an `authored` entry is left alone and the delta is printed. Two ground lines on this roster are deliberate overrides — the golem's slam erupts below its feet, Shawn's idle plants a staff below his |
| "the content is stale" from a test | `ContentFreshnessTests` compares a sha256 over `ContentData/*.json` + `Scripts/Domain/Content/**` + `Scripts/Core/Content/**` + `ContentBuilder.cs` + **sixteen named Domain files** against `Resources/Content/content_stamp.json`. The sixteen are the ones declaring a type a `Raw*`/`Resolved*` record reaches through a public field (`DamageType`, `SkillEffect`, `StatBlock`, …); `ContentInputCoverageTests` derives that set by reflection every run and fails **naming the file and the line to paste** when a record starts reaching somewhere new, so the list cannot drift. Editing anything else under `Scripts/Domain/` — a combat formula, a map generator — no longer marks content stale, which is what used to redden the 4s dotnet loop for work that cannot change an asset. When it *is* stale the regenerated **stamp has to be committed with the change**. A missing stamp means the last build died partway (a resolver error writes none) |
| Changed a screen tree under `Domain/UiKit/Screens/` or wiring in `Editor/SceneBuilder/` | add `-BuildScenes` (~1 min slower). It builds the scenes in the runners, where PlayMode reads them, AND copies them back to main and into the PlayMode runner — needed both to trust a PlayMode run against your change and before committing it. Without it, PlayMode loads whatever scenes are already on disk, not yours. UiAudit itself does not need this flag: it already runs inside the EditMode screen tests (`FightScreenTests`, `MapScreenTests`, `HubScreenTests`, etc.), which re-solve layout from the screen tree in code, not from a built scene |
| About to COMMIT regenerated scenes | same flag, `-BuildScenes` — there is no separate build-only vs. sync-only mode |
| Re-running with nothing changed since the last synced build | `-SkipSync` is legal |
| UI-visible change | also `tools/screenshot.ps1 -Panel <Name>` and actually look at the PNG |
| Change to anything that MOVES (an ambient animator, an intro, a tween) | `tools/screenshot.ps1 -Runtime`. `-Panel`/`-All` render Edit Mode, which never ticks `Update()` — a static capture cannot show motion at all, and the pure-curve unit tests only prove the formulas vary, not that anything calls them |
| Balance-bot batch (not a correctness gate — a report to read) | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 200`. Defaults now cover all four archetypes and sample the determinism replay at 0.1. Writes `reports/bot/<timestamp>/` (gitignored), merges the shards with `tools/bot_merge.py` into one `summary.json`, then renders `report.html` via `tools/bot_report.py`. See `docs/PLAN_BALANCE_BOT.md`, `docs/BOT_SUMMARY_SCHEMA.md` |
| … a big one (thousands of runs) | add `-Shards 4`. N shards means N project copies (`-Bot1`…`-BotN`, created on first use — that run pays a full asset import, later ones boot in ~20s); Unity refuses two batchmode instances against one copy. Peak ~1.1 GB per shard, so 4 is the ceiling on 16 GB. `-TestRunner2` is deliberately excluded from the sharded path so a batch cannot clobber a concurrent test run |
| … iterating on the bot itself | `-Shards 1` stays on `-TestRunner2` and skips the copy machinery entirely (the default is `min(4, processors/2)`). `-ReplayShare 1` replays every run instead of a tenth; `-InMemorySaves 0` puts the save back on disk, which is ~16x slower and exists only to prove the fast path did not change a run's hash |

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
  across both TestRunner copies) → commit both. The `.meta` sweep is not gated
  on the flag, but the scenes are, and a new screen file that changes them
  needs both syncing together.

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
| `tools/domain-tests` still builds when `Domain/`/`Tests/EditMode/` changes | `tools/githooks/pre-commit` | commit |
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
| Change test-tooling behavior or its timing characteristics | that tool's own header comment, plus §8's table if the command line moved |
| Exclude a test file from the dotnet host (or add one back) | `tools/domain-tests/README.md`'s exclusion table, with the reason — `tools/test.ps1 -List` reads the split off the csproj and needs nothing |
| Add or change a `Raw*Entry` field | `[ContentDoc]` on it; run `tools/content_schema.ps1`; commit `docs/CONTENT_SCHEMA.md` |
| Change what a balance batch writes, or what a metric means | `docs/BOT_SUMMARY_SCHEMA.md` (it is the contract between `BalanceBotRunner`, `tools/bot_merge.py` and `tools/bot_report.py`) |
| Finish (or partially close) a handoff's implementation | its `GAP_AUDIT.md` verdicts |
| A `PLAN_*`/`HANDOVER_*` doc's last phase lands | move it to `docs/archive/` in that commit |
| A handoff's feature lands | move its `docs/handoffs/<slug>/` dir to `docs/handoffs/archive/<slug>/` |
| Change a workflow rule, ritual, or convention | this file |
| Change a rule that a hook enforces | that hook's script, plus §10 above |
| Lose time to something avoidable | `docs/INCIDENTS.md`, with what now prevents it |
