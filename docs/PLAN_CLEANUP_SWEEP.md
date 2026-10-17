# Cleanup sweep plan

Status: rev 4. Rev 4 adds architecture candidates (D13, P6) on the owner's ask. Rev 3 came after two review rounds. Reviews:
`docs/handoffs/cleanup_sweep/REVIEW_R{1,2}_{warm,cold}.md`. §12 maps every point
to its change. Rev 3 and rev 4 changes have not been reviewed.

**Owner ask (2026-10-17):** sweep the whole codebase for bugs, inefficiencies,
simplifications and more. Make it efficient. Map out who does what and where.

**Scope.** The 2026-09-24 review (baseline `bc867981`) read 129.5K production
lines and excluded `tools/`. Today there are 218K lines of production code plus
tools, and 11K lines of ContentData JSON. About 79K lines were added since the
baseline; 56 of 90 code slices changed by a quarter or more. Tests (204K lines)
get scans only.

**Base commit:** `c8d0c631`. The one commit since, `8c19867e`, touched only
`.gitattributes` and `.gitignore`, so every line number is the same at HEAD.

## 1. Decisions taken

| # | Decision | Why | Owner can overturn |
|---|---|---|---|
| D1 | **Scripts** run grep-shaped scans (zero tokens, no misses). **Haiku `reader`s** run the three scans that need light judgement. **Sonnet finders** read the code. | Answers the Haiku question. Haiku is cheap, but a grep is cheaper and exact. Haiku never diagnoses (CLAUDE.md); past yield came from cross-seam bugs. | — |
| D2 | One finder read covers all five categories, ARCH included as candidates. | The read is the cost. | — |
| D3 | Scan candidates are tagged to slices and confirmed or refuted by that slice's finder. | No separate triage read. | — |
| D4 | Slices of 3,000 lines or fewer. T1 gets a full read. T2 gets a read of `git diff -W bc867981 c8d0c631`. A `partial` is re-cut once to 2,000 lines or fewer; a second `partial` is filed as a coverage gap. | 5.5K-line slices came back partial in September. | T2 full read: +18 finders. Churn bar 25% → 50%: about −20 finders. |
| D5 | **The fix's place follows its paths.** A fix that touches only `Domain/` and `Tests/EditMode/` code reachable by the dotnet host is a **worktree fix**, done in a sparse worktree with `[D]` tests, in parallel. Any other fix is a **main-tree fix**, done in the main tree by one holder at a time, which gates with Unity itself. | Worktrees run only named `[D]` classes (`test.ps1:567`), and Core, Data and Editor are Unity-only (R2 cold 1). Full extra clones cost about 3.5 GiB plus a fresh Unity import each. | One extra full clone with its own TestRunner doubles main-tree throughput; the owner can approve the disk. |
| D6 | **One main-tree writer at a time.** The orchestrator launches at most one main-tree agent (a holder or the integrator) and starts the next only after the previous one reports. | Kills gate collisions, overlapping integrators, false reds from others' edits, and the promotion stampede (R1 w1/w2, R2 c7). | — |
| D7 | **Sweep working files live outside the repo** in `C:\Games\sweep-2026-10-17\` (`notes/`, `scan/`, `ledger/`, `baseline/`). The plan, `BRIEF_COMMON.md`, `SLICES.md`, the scripts and the reviews are committed before P1. | The main tree stays clean, so the pre-check can pass (R2 cold 2). There is no ledger location ambiguity (R2 cold 12). The folder survives a dead session. | — |
| D8 | Coverage lives in the notes coverage lines, checked by `coverage.py`. `docs/hunt/MANIFEST.md` is not used. | Its revision rule re-opens every row a fix touches; two ledgers would disagree. | — |
| D9 | The sweep's scripts are one-offs, under `docs/handoffs/cleanup_sweep/scripts/`, each with fixtures. | First use. Fold them into `tools/` on a second use. | — |
| D10 | No `-Runtime` captures until the owner says yes. Visual fixes queue for one batch. | Owner rule. | — |
| D11 | Gate infrastructure is frozen until every other lane closes: `test.ps1`, `run_tests_parallel.ps1`, `test_select.ps1`, `test_areas.ps1`, `playmode_shards.ps1`, `runner_sync.ps1`, `unity_lock.ps1`, `unity_path.ps1`, `tools/githooks/`, `tools/domain-tests/`. Then L9b fixes them, followed by a runner-trust check. | Every gate runs through them. | — |
| D13 | **Architecture is found in the sweep, fixed after it.** Finders record ARCH candidates (BRIEF_COMMON) from the same read. After the last lane closes, P6 turns them into the `/improve-codebase-architecture` report. The owner picks a candidate and grills it through. Each chosen candidate gets its own plan and session. | Deepening redesigns a contract (senior, `Escalation: architecture`), which needs the owner. Refactoring mid-sweep would move code under 13 running lanes. Dead code removed first keeps noise out of the report. | — |
| D12 | Bugs are fixed without asking. Design forks go to one owner digest. Comment density is out of scope; false and history comments are in. | Standing owner rules. | "trim density" |

## 2. Contracts

The finding contract lives in `docs/handoffs/cleanup_sweep/BRIEF_COMMON.md`:

- categories and severity;
- intent precedence and the two-source rule;
- states and grades;
- what "dead" means;
- what is out of scope;
- the row format;
- the git rules;
- the sourced Decided list.

That file is the one home for these.

What a fix may change:

- **BUG:** only toward the side the intent sources agree on. Red test first, with
  pinned literals. The red is shown before the fix exists, by the agent that
  writes the fix, with the test's name and assertion message in the run output.
  Worktree fixes use `[D]`; main-tree fixes use `tools/test.ps1 <class>`. Fix and
  test land as one commit, so main never carries a red test. A compile error or
  an exception is not a valid red. If the test cannot go red, the row is graded
  `executed` or `static` and the commit message says so.
- **SIMP:** behaviour preserved, gate green before and after. No public-surface
  change outside the lane. No new abstraction unless it deletes more than it adds.
- **PERF:** simulation results unchanged. The measurement route depends on the
  cost:
  - test time: `test-profile-PlayMode*.csv`;
  - scene loads: `SceneLoadBenchmarkTests` with `PP_BENCHMARK=1`;
  - pure-Domain hot paths: a `[D]` timing comparison;
  - per-frame Unity allocation: no automated route, so those rows stay `static`,
    with the call frequency written out.
- **Never:** balance numbers, art (orphan-asset rows go to the digest, never to a
  deletion), scenes by hand, content beyond fixing a broken row, the Decided
  list.

## 3. Roster

| Role | Type (model) | Count | Where | Writes | Runs |
|---|---|---|---|---|---|
| Orchestrator | main session (Opus) | 1 | main tree | brief suffixes, routing, docs edits of 20 lines or fewer, sweep-dir bookkeeping | scripts |
| Script author | `implementer` (Sonnet) | 1 | main tree, during P0 | `scripts/*.py` + fixtures | `selftest.py` |
| Haiku scanners H5, H6, H8 | `reader` (Haiku) | 3 | read-only | nothing; they return rows, which the orchestrator saves to `scan/` | — |
| Log reader H9/H10 | `verifier` (Haiku) | 1 | read-only | nothing; returns rows | — |
| Finders | `implementer` (Sonnet), read-only by brief | 82 | main tree, read-only | `notes/<slice>.md` in the sweep dir | `[D]` classes, for `executed` |
| Worktree fixers | lane `implementer`; `fixer` (Haiku) for pre-diagnosed one-file batches | up to 1 per lane at a time | `.claude/worktrees/sweep-<lane>` | their branch, `ledger/<lane>.tsv` | named `[D]` classes |
| Main-tree holders | lane `implementer`, fresh per hold, up to 4 rows per hold | 1 at a time (D6) | main tree | commits, `ledger/<lane>.tsv` | `test.ps1 <class>`, then the commit gate |
| Integrator | `implementer` (Sonnet), fresh per batch | 1 at a time (D6) | main tree | cherry-picks, regenerated scenes and content, reverts | the commit gate, full runs, the final run |
| Architecture synthesiser | `implementer` (Sonnet) | 1, in P6 | read-only | the HTML report in the OS temp dir, plus a copy in the sweep dir | — |
| Senior | `senior` (Opus) | as needed | as the row's place | — | — |

The senior's brief needs a line `Escalation: architecture | cross-layer |
two-failed-cycles`.

## 4. Phases and gates

### P0 hygiene, baseline, enablement

| Step | Owner | Gate |
|---|---|---|
| A line endings, `/scratchpad/` ignore | implementer | DONE 8c19867e. Dirty entries went from 76 to 13. |
| B FightRunUpTests double sting | implementer | Rate before/after with run counts, or a bounded no-repro report. |
| C scenes and baseline | integrator, after B | `run_tests_parallel.ps1 -BuildScenes -BuildContent` is green. Scenes are committed if they differ from HEAD. The compile log, `test-profile-PlayMode*.csv` and the EditMode XML are copied to `baseline/`. |
| C2 runner trust | integrator | Only if `git log 438d6fda..HEAD` touches a D11 path: one planted failing `[D]` test and one PlayMode test each turn the gate red, then both are removed. |
| S scripts | script author, in parallel with B/C | `python -I scripts/selftest.py` passes; `python -I scripts/lanes.py --check` prints `0 unowned, 0 double-owned`; `SLICES.md` is regenerated (§5). |
| W worktree proof | integrator | One sparse worktree runs the whole `[D]` suite (`tools/test.ps1 -List`, every `[D]` class, about 10 s). Every failure must be a class matching `$RepoReadPattern` (`test_select.ps1:70`). Those classes form the `main-only` list, and a fix needing one becomes a main-tree fix. Any other failure stops P0 and D5 is re-planned. |
| K commit | orchestrator | Plan, `BRIEF_COMMON`, `SLICES`, scripts and reviews are committed through the docs gate. `git status` shows only the owner-excluded paths. |

**Owner-excluded paths** are never staged and are ignored by every pre-check:
`Assets/_Project/Art/Sheets/Spells/Velvet Shackles/` and `docs/captures/run_up/`.

### P1 scans (all parallel; the script scans take seconds)

Script scans, through `scripts/scan.py`:

| Id | Pattern |
|---|---|
| X1 | Identifiers declared once and referenced nowhere, checking every "dead" form in BRIEF_COMMON. The fixture holds one hit per form. |
| X2 | History or false-comment phrases. |
| X3 | Allocating or lookup calls inside `Update`/`LateUpdate`/`FixedUpdate`/`OnGUI` bodies in `Assets/_Project/Scripts/Core/` and `Assets/_Project/Scripts/Domain/UiKit/`. |
| X4 | Same-named private or static helpers in two or more files (helpers only, not literals). |
| X5 | Unreferenced `tools/` scripts. |
| X6 | Unreferenced `Art/`, `Audio/` and `Resources/` files by path and GUID, plus orphan `.meta` files. Owner digest only. |
| X7 | Layering: `using UnityEngine` in `Domain/`, and asmdef references. |
| X8 | `.ps1` non-ASCII outside comments, or a BOM. |

Haiku scans:

| Id | Scope | Looks for |
|---|---|---|
| H5 | docs | Doc drift, and open AUDIT rows whose code is gone. |
| H6 | tests | Gotcha 5 violations, false `[Ignore]` reasons, tests with no assert, near-duplicates. |
| H8 | ContentData + resolvers | Fields accepted and never read. |
| H9/H10 | baseline logs | Warnings, slowest tests. |

**Gate:** `coverage.py scan` checks that every lane wrote a TSV with rows or a
`0 rows: <pattern>` line. Then `tag.py` gives each row a slice id, or a lane id
when no slice covers it.

### P2 finders (§5)

**Gate:** `coverage.py notes` passes only when every slice reads
`coverage: full`. A zero-finding slice passes with `0 rows`.

After each wave, `git status` must show no finder edits. If one does, the
orchestrator saves the diff to `sweep/stray/` and removes it with
`git apply -R`. That is recoverable, and the finder is named in the record.

### P3 triage (orchestrator)

1. `merge.py` folds the notes into a generated `LEDGER.tsv`.
2. It drops duplicates by symbol and by contract.
3. It re-checks each excerpt at HEAD; stale rows go back to notes.
4. It tags each row's **place** (worktree or main, by D5 and the `main-only`
   list) and its **lane** (`lanes.py`).
5. The orchestrator reads only S1/S2 and owner-fork rows.

Routing:

| Shape | Route |
|---|---|
| Pre-diagnosed, one file | `fixer` (Haiku) |
| Bounded | lane `implementer` |
| Contract or lifecycle redesign, or must land across Domain, Core and Editor at once | `senior` |
| Intent sources disagree | owner digest + AUDIT (L11) |

**Empty case:** a lane with zero rows is closed with one line in the record.

### P4 fixing (§7), P5 integration (§8), P6 architecture (below)

### P6 architecture report (after close-out)

Inputs:
- the ARCH rows from `LEDGER.tsv`, re-checked at HEAD, since sweep fixes may
  already have removed some;
- X7 layering output;
- commit hot spots from `git log` since `bc867981`;
- `codebase-design` vocabulary.

Steps:
1. The orchestrator runs the skill's Explore step with that input. The ARCH rows
   stand in for the organic walk, so there is no second codebase read.
2. One synthesiser reads only the code behind `strong` and `worth-exploring` rows.
3. It writes the report per the skill's `HTML-REPORT.md`: a card per candidate
   with files, problem, solution, benefits, a before/after diagram and a
   strength, and a top recommendation.

**Gate:** the report exists and every `strong` row is either a card or listed
with a reason it was merged or dropped.

**Empty case:** zero ARCH rows. The record says so and no report is written.

The skill has two prerequisites the repo lacks: `CONTEXT.md` does not exist and
no `docs/adr/` exists. The skill creates `CONTEXT.md` lazily during grilling.
The orchestrator then asks the owner, "Which of these would you like to explore?"

## 5. Finder waves

`scripts/slices.py` regenerates `SLICES.md` with:

- full paths and a `wave` column;
- S04, S05 and S09 promoted to T1;
- the extensionless `tools/githooks/pre-commit`;
- content slices C1–C4;
- the misc slice G1.

Every slice is listed there.

| Wave | Content | Finders |
|---|---|---|
| W1 | Domain/Combat (13); Core fight, stage, spell, party and dossier slices, including S04, S05, S09 | 26 |
| W2 | Rest of Core, Domain/UiKit, Rewards, Dungeon, Events, Data, Bot | 30 |
| W3 | Domain/Content, Core/Content, tools (T1), D1–D5. C1–C4 (ContentData JSON, about 3K lines each, read against `docs/CONTENT_SCHEMA.md`). G1 (500 shader lines, `tools/githooks/pre-commit`, 5 asmdefs, ProjectSettings/Packages manifests for X7 drift). | 26 |

Total: 72 T1 + 5 D-groups + 4 content + 1 misc = 82.

Each wave launches one finder first, then the rest once that finder's first
response has begun. That way the shared prefix is already cached.

## 6. Scripts (P0 step S)

All run with `python -I`. Each has a fixture and an empty-input rule, which is
`0 rows` and exit 0.

| Script | Does |
|---|---|
| `slices.py` | Regenerates `SLICES.md` (see §5). |
| `lanes.py` | Maps every tracked file under `Assets/_Project/` (Scripts, ContentData, Scenes, Shaders), every asmdef, `tools/`, `docs/`, `AUDIT.md`, `CLAUDE.md`, `Packages/manifest.json` and `ProjectSettings/` to exactly one lane (§7). `--check` fails on unowned or double-owned paths. Test files are assigned by the production types they reference, using `test_select.ps1`'s type closure: the lane owning most of the referenced types wins; ties and `Tests/*/Shared/` go to L10. |
| `scan.py` | X1–X8. |
| `tag.py` | Gives a scan row its slice id or lane. |
| `coverage.py` | Runs the `scan` and `notes` gates. |
| `merge.py` | Builds the ledger, deduplicates, does the HEAD excerpt check, tags place and lane, and summarises. |
| `selftest.py` | Runs each script against its fixture. |

## 7. Lanes (P4)

Lanes own paths; `lanes.py` is authoritative, and the longest prefix or an
explicit file entry wins. A whole partial class belongs to one lane.

| Lane | Owns |
|---|---|
| L1 combat rules | `Domain/Combat/`, except `Presentation/` and `Session/FightHud*` |
| L2 fight presentation | `Domain/Combat/Presentation/`, `Domain/Combat/Session/FightHud*`, `Domain/Stage/`, and in `Core/`: `Fight*`, `Stage*`, `Spell*`, `Stance*`, `Focus*`, `FrameSequenceLoader`, `ContactCues`, `SquadTrack`, `PreviewFight`, `DamagePopup`, `StatusCues` |
| L3 run loop | in `Core/`: `RunController`, `RunEncounter`, `RunEventContext`, `RunLedger`, `RunManager`, `RunSettlement`, `Map*`, `Reckoning*`, `Reward*`, `Relic*`, `Shop*`, `Event*`, `RoomResolver`, `ItemOfferRoll`, `Hone*`, `RealmOpening`, `RealmPrizes`; and `Domain/Rewards/`, `Dungeon/`, `Events/`, `Economy/`, `Relics/` |
| L4 hub and menus | every other top-level `Core/*.cs` (including `RuntimeNavWiring`, `PcPlateSprites`, `BeaconPulse`, `StaticSwing`); `Domain/Party/`, `Talents/`, `Progression/` |
| L5 screens | `Domain/UiKit/`, `Editor/SceneBuilder/`, `Assets/_Project/Scenes/` (generated) |
| L6 content | `Domain/Content/`, `Core/Content/`, `ContentData/`, `Editor/ContentBuilder.cs` |
| L7 saves | `Data/`, `Core/SaveSystem.cs`, `Core/SaveSlot*` |
| L8 bot | `Core/Bot/`, `Core/RunOrchestrator.Spells.cs`, `Domain/Bot/`, `Editor/Bot/` |
| L9a tools | `tools/`, except the D11 paths |
| L9b gate infra | the D11 paths, after every other lane closes; then C2 |
| L10 shared tests | `Tests/*/Shared/`, tests with tied ownership, and test-only fixes |
| L11 docs | `docs/` (except `docs/handoffs/cleanup_sweep/`, which belongs to the orchestrator), `AUDIT.md`, `CLAUDE.md` |
| L12 foundation | `Domain/` root, `Equipment/`, `Stats/`, `Inventory/`, `Rng/`, `Glossary/`, `Preview/`, `Audio/`, `Ambience/`, `DebugMenu/` |
| L13 editor and project | `Editor/*.cs` except `ContentBuilder.cs`; shaders; asmdefs; `Packages/`; `ProjectSettings/` |

**Edits outside the lane.** A lane edits only its own paths. Owner forks go into
its ledger, and L11 files them. A fix whose root cause sits in another lane goes to
that lane. A needed edit to a test file the lane does not own goes to the owning
lane as a follow-up row.

**Worktree fix loop.** The integrator creates the worktree once:

- `git worktree add --no-checkout .claude/worktrees/sweep-<lane> -b sweep/<lane>`;
- sparse cone: `Assets/_Project/Scripts`, `Assets/_Project/ContentData`, `tools`,
  `docs`.

Per row:

1. Rebase onto `main`. Inside its own worktree a lane may `git rebase`,
   `git rebase --abort` and `git switch -C sweep/<lane> main`; the ban on
   checkout, restore, reset and stash covers the main tree.
2. Red test (§2).
3. Fix.
4. Named `[D]` classes go green.
5. One commit.
6. Append to `ledger/<lane>.tsv`.
7. Report one line.

A fresh lane agent takes over after about 8 rows or about 150K context.

**Main-tree hold.** A fresh lane agent takes up to 4 main-place rows:

1. Pre-check (§8 step 1).
2. Per row: red via `tools/test.ps1 <class>`, then the fix, then green.
3. The commit gate `run_tests_parallel.ps1 -Changed -ChangedPaths <row paths>`,
   plus `-BuildScenes` and `-BuildContent` per §8 step 3.
4. One commit per row.
5. Report and release.

**Cycles.** A row whose gate fails twice for its own reason becomes
`two-failed-cycles` and goes to the senior. If the senior also fails, the row is
filed as unresolved. A red from another commit does not count.

## 8. Integration (P5)

The integrator is a fresh agent per batch, launched under D6. A batch is 4 worktree
commits or fewer. The orchestrator launches one when 4 are waiting, or 30 minutes
after the first.

Per batch:

1. **Pre-check.** `git status --porcelain` shows nothing outside the
   owner-excluded paths.
   - On foreign dirt, wait 10 minutes and check once more. Another session may
     be mid-commit.
   - Then stop and report the paths. The orchestrator tells the owner. No agent
     discards another agent's work.
2. **Cherry-pick in FIFO order.** A conflict means a lane-table bug: stop and
   report. Never resolve one.
3. **Gate once:** `run_tests_parallel.ps1 -Changed -ChangedPaths <batch paths>`.
   - Add `-BuildScenes` when the batch touched `Domain/UiKit/Screens/`,
     `Editor/SceneBuilder/` or a `[SerializeField]` line.
   - Add `-BuildContent` when it touched ContentData or ContentBuilder.
   - Diff `Art/` after a scene build (gotcha 3).
   - Regenerated scenes and content go in their own commit.
   - `test_select.ps1` promotes the gate to a full run when more than 5 commits
     have landed since `.last-full-green`. Holders and integrators share that
     counter, so expect a full run about every sixth main-tree commit. That is the
     owner's cadence; nothing else triggers one.
4. **On red.**
   - Rerun the failing classes once with `tools/test.ps1 <class>`; a flake goes to
     the TESTING.md Flakes rule, not to a revert.
   - Still red: pick the batch commits that touch types the failing class
     references (via `lanes.py`). Revert those, newest first, with `git revert`,
     and rerun the class.
   - Green: the reverted rows go back to their lane with the failure block. The
     lane recreates its branch from main (`git switch -C`) and redoes the work as
     a new commit, never by rebasing the old one, because a rebase silently drops
     it as patch-identical.
   - Still red, a revert conflict, or no attributable commit: stop and report to
     the orchestrator.
   - Never `reset`.
5. **Report** one line per commit (merged or reverted) plus the gate's result
   line.

**Close-out.** Each step is assigned to a role:

1. The orchestrator runs `merge.py` for the final ledger.
2. L11 files the owner forks in AUDIT.
3. L9b runs, then C2.
4. A final integrator runs the full suite with `-BuildScenes -BuildContent`.
5. A visual-fix capture batch runs, if the owner allowed `-Runtime`.
6. The orchestrator writes `docs/SWEEP_2026-10-17.md`:
   - counts by category, severity, state and grade;
   - the full unresolved list, including coverage gaps;
   - the ARCH candidate count by strength, with a pointer to P6;
   - the owner digest: one plain-language question per fork, with options and my
     recommendation;
   - measured cost per role.

## 9. Efficiency

Cost is tool calls × context, and cache reads are 96% of tokens.

1. **Byte-identical prefix.** Every prompt is `BRIEF_COMMON.md` verbatim, then a
   short suffix. One finder launches first, so the prefix is cached before the
   rest start.
2. **ARCH rides the finder read.** It costs about one row per slice and one synthesiser, not a second walk of the codebase.
3. **Scripts before models.** Eight of twelve scans cost zero tokens.
4. **One read, five lenses.** Candidates arrive pre-located.
5. **Small returns.** One line per agent. The orchestrator reads only S1/S2 and
   fork rows.
6. **Parallel where it is safe.** Finders and worktree fixes run in parallel.
   Only main-tree work is serial, and that work is the part Unity must check
   anyway.
7. **Fresh small agents for Unity waits.** Holders and integrators re-pay their
   cache after each Unity run of 3–7 minutes. They start fresh per hold or batch,
   so what they re-pay stays small.
8. **Diff-scoped T2.** 18 slices become 5 D-groups.

Estimate, mostly Sonnet cache reads:

| Role | Planning case (September: ~50 turns, ~120K context) | 2026-09-19 baseline (~85 turns, ~212K context) |
|---|---|---|
| Finders | 82 × 6M ≈ 0.5B | 82 × 18M ≈ 1.5B |
| Holders, integrators, worktree fixers | 40–60 agents × 5–10M ≈ 0.2–0.6B | |
| Scanners | under 2% | |

**Total: about 0.7B to 2.1B tokens.** D4's churn bar is the cheapest knob.

**Wall clock.** Finder waves take hours. Main-tree work is serial at about
10–20 minutes per hold.

## 10. Stop rules

- A finder that cannot read its slice in full reports `partial` and stops.
- The third failure of the same test is a root-cause job (`docs/TESTING.md`
  Flakes).
- Main-tree agents never resolve conflicts, reset, or discard foreign work.
- Agents never spawn agents.

## 11. Out of scope, stated

- Localisation: there is no localisation layer to sweep.
- Balance tuning.
- Art and asset deletion. X6 lists orphans for the owner only.
- Comment density.
- New features.

## 12. Review dispositions

Round 1, cold (c) and warm (w):

| Point | Change |
|---|---|
| Unowned paths c1 w4; nested precedence c1; RunOrchestrator w5; L4 catch-all w6 | §7, `lanes.py --check` |
| Shared-tree collisions c2 w2; promotion stampede w1; Unity queue w20 | D5, D6 |
| Full-run cost of tools and scene commits c3 | Batching; only main-tree agents gate |
| Two coverage stores c4 w17 | D8 |
| `BRIEF_COMMON` c5 w15 | Written, with sources |
| Scripts unowned c6 w16 | §6 |
| ContentData, hook, asmdefs, shaders c7; logs deleted c7 | W3 C1–C4, G1; P0 C |
| SerializeField rebuilds c8 w18 | §8 step 3 |
| Gates and empty cases c9 | §4 |
| `[U]` assertion c10 | §2 |
| T1 promotion c11 w12 | §5 |
| Cost c12 w10; cache claim w11 | §9 |
| Pre-existing dirt c13 | Hygiene A, P0 K, the excluded list |
| Grep-shaped Haiku lanes w8; H4 c15 | D1, X4 |
| Assets, layering, PERF route w9 | X6, X7, §2 |
| Moving target w3 | Base pin, `merge.py` stale check |
| Ledger race w13 | Per-lane ledgers in the sweep dir |
| `.meta` w14 | BRIEF_COMMON |
| Gate infra w7 | D11 |
| Finders hold Write w16 | Post-wave check, §4 P2 |
| Tools excluded in September w19 | Scope paragraph |

Round 2:

| Point | Change |
|---|---|
| Core and Editor untestable in worktrees c1 | D5 place-by-paths; main-tree holds |
| Sparse cone breaks disk-reading `[D]` classes w1 c10 | P0 W runs the full `[D]` suite; `main-only` list |
| Dirty-tree pre-check c2 | D7, P0 K, the foreign-dirt rule |
| Tests unowned w2 c3 | Type-closure ownership, L10 for Shared |
| Two-commit red on main w3 c4 | §2: red shown before the fix, by name; one commit |
| Promotion on 5-commit batches c5; cadence w8 | Batches of 4; cadence stated in §8 step 3 |
| Revert and bisect gaps w4 c6 | §8 step 4: flake rerun, attribution, `switch -C`, stop states; terminal state in §7 |
| Integrator overlap c7 | D6 |
| 5-minute claim; final run unassigned c8 | §9 lever 6, §8 close-out |
| `2>/dev/null` hides failures; rebase conflicts; trailer c9 w6 | BRIEF_COMMON git rules; §7 worktree rules |
| Unowned non-script paths; `Run*` matching `RuntimeNavWiring` c11 w7 | L5, L13, L4, explicit L3 list |
| Ledger location c12 | D7 |
| SLICES stale; count c13 w5 | `slices.py` in P0 S; 82 |
| Base vs HEAD c14 | Base paragraph |
| Dead-form fixtures c15 | X1 |
| "And more" c16 | X6, X7, G1, §11 |
| Decided sources w6 | BRIEF_COMMON fixed |
