# Workflow

Session rituals, assignment/ownership, verification gates, the
enforcement/doc-update index. Rules, not narrative — history: `docs/INCIDENTS.md`.

Update rule: see §11.

---

## 1. Session start

1. Read `CLAUDE.md`.
2. `git status --short`.
3. `git config core.hooksPath` MUST print `tools/githooks`
   (`git config core.hooksPath tools/githooks` if not).
4. Check `Temp\UnityLockfile` in both `C:\Games\Prince's Palace-v2-TestRunner`
   and `-v2-TestRunner2` before tests — locked means another session is
   mid-run against the same copies.

`docs/CODE_MAP.md` is searched, never read whole.

## 2. Assignments

**Step one of every implementation launch: triage.** Is this doable as a
bounded change, or does it go deep architectural? Doable goes to
`implementer`; deep architectural goes to `senior` with a stated
`Escalation:` reason (§6).

Say the reservation when it forms — a reaffirmed request is a decision.

User-facing intent header, before anything bigger than a one-line fix:

> **Change:** the frame around the slot grid, not the slot cells.
> **Don't touch:** cell size, the armour art.
> **Done when:** `test.ps1 ui` green, `screenshot.ps1 -Panel Inventory` shows
> a wider frame with cells unmoved.

Two briefing patterns:
- Behaviour/AI changes: trigger -> condition -> outcome.
- Bug reports: describe the symptom, not the theory of the cause.

Worker brief, always this shape:

```
Objective:
Done when: (acceptance evidence -- test names, screenshot condition, or file state)
Owner: (agent type)
Edit surface: (paths the owner may change)
References: (path + section -- never pasted text)
Verification: (exact command)
Stop conditions: report at completion, at a blocker that changes the plan,
  or when a rule seems wrong.
History mode: none, or inherited (with reason)
```

Independent workers start with minimal history (`fork_turns = "none"`) by
default. The brief carries compact current state plus the policy references
needed for the assignment. Inherit history only when the work depends on prior
conversation that cannot be represented compactly; state why in the brief.

Launch checklist: one bounded objective; one owner and exact edit surface;
compact current state; path-and-section references; selected verification from
`docs/TESTING.md`; stop conditions; history mode. Completion checklist: changed
files or read evidence; exact command and result; acceptance evidence; preserved
unrelated work; open questions; history mode.

The orchestrator keeps a task record per session: acceptance, owner, edit surface,
dependencies, status. Combine tightly coupled issues into one assignment.
Serialize overlapping edits — never two owners on one file at once. Reuse
an owner while its context on the issue is still relevant.

## 3. Ownership and context renewal

The owner does diagnosis, implementation, focused tests, and correction for
its issue. A reader performs independent research. An implementer may read its
owned files, direct dependencies, and status/diffs needed to preserve shared
work; it does not need a separate reader for those implementation-local facts.
When its context is largely obsolete at a natural checkpoint it
writes a state record to the scratchpad (findings, decisions, files
changed, verification results, next action); the orchestrator ends the assignment and
starts a fresh owner from the record.

No fixed-turn restarts. The threshold comes from measurement
(`tools/usage_baseline.py`; 2026-09-19 baseline: median worker 64 turns at
148K context/turn, p90 184 turns at 262K), not a guess.

After two failed correction cycles on one issue, the orchestrator reassesses cause and
scope, then either allows a third attempt or relaunches as `senior` with
`Escalation: two-failed-cycles` — or `architecture`/`cross-layer` if the
reassessment found that instead (§6).

## 4. Worker context discipline

The primary cost lever (measured 2026-09-19: cache-read is 96% of all
tokens; the average worker turn carries ~212K tokens of context):

- `Grep -n` before reading any file over 30 KB, then `Read` with
  `offset`/`limit` for just the range needed.
- Tests and builds write logs to disk; read back only the failing lines.
- Review authored changes by file and responsibility, never by paging a
  large diff.
- Check generated changes (scenes, content, `.meta`) through their audits
  (`UiAudit`, `UiWiringSweep`, the pre-commit hook) — never by eye.
- Reuse recorded findings (`AUDIT.md`, prior state records) before
  re-investigating something already known.
- Briefs reference a path + section, never paste the text.
- Report once: at completion, or at a plan-changing blocker. Include history
  mode, files changed or `file:line` evidence, exact verification and result,
  acceptance evidence, unrelated-work preservation, and open questions.
- Screenshots only when the brief names a visual acceptance condition —
  smallest set that proves it, conclusion stated in the report.
- Shell searches (`find`/`grep`/`rg` in Bash) MUST name a root
  (`Assets/_Project`, `docs`, `tools`) and exclude `Library`, `Temp`,
  `.claude/worktrees`, `reports`, `tools/screenshots`. Grep/Glob already
  honour `.git/info/exclude`.

## 5. Verification

The implementer runs focused tests while editing. Once editing is stable,
one verifier runs the selected change-class gate exactly once, in one
foreground call, and reports the state it verified (HEAD sha + uncommitted file
list). `docs/TESTING.md` is the canonical gate matrix. Game/runtime/editor,
content, generated artifact, test, and dependency changes require the full
Unity gate. Agent/workflow prose or configuration alone uses the focused policy
gate and does not run Unity. Prefer a fresh verifier when the owner's context
is already large.

Rerun the gate WHEN: files relevant to the selected gate changed after its
snapshot; a failure was fixed; the earlier result was incomplete or invalid.
Hook-specific triggers still apply. Not a reason: time passed, reassurance.

`-BuildScenes`/`-BuildContent` only on their documented triggers
(`docs/TESTING.md`). No progress polling — block on the gate with a long
timeout.

## 6. Escalation

Owner decision, 2026-09-24: `implementer` moved to Opus 5.5 at medium
effort and `reader` moved to Sonnet 5. `implementer` and `senior` now run
the same model and effort, so the per-token cost rationale that used to
justify keeping Sonnet as the default implementer no longer applies —
what still separates the two is `senior`'s `Escalation:` gate, a process
control that forces a stated reason before an architecture- or
cross-layer-shaped change proceeds, not a cost control. Triage before
every implementation launch (§2) still decides `implementer` vs `senior`:
doable as a bounded change goes to `implementer`; deep architectural goes
to `senior`, with the brief carrying a line `Escalation: <criterion>`
naming one of exactly three criteria. `tools/githooks/route_agents.py`
checks that line is present and well-formed before the launch is allowed,
and refuses any Opus model string that is not exactly `claude-opus-5-5`
("no Opus 5" — the bare `opus` alias is not routed).

Opus gate — the three criteria, also in `senior.md` and `CLAUDE.md`:

- `architecture` (by triage) — the change redesigns a contract or
  lifecycle (brief names the change, the affected contracts/lifecycles,
  and the concrete failure risks).
- `cross-layer` (by triage) — one change that must land atomically across
  Domain + Core + Editor/scene generation with ordering, serialization or
  lifecycle risk, where splitting it across separate implementer owners would break it.
- `two-failed-cycles` (fallback) — an implementer failed two
  correction cycles on this same issue (brief names what each attempt did
  and why it failed).

`unknown-cause` is removed: there is no generic "difficult" criterion.

A worker that believes a rule is wrong says so in its report and continues
under the rule. The orchestrator raises it with the user; nobody deviates silently.

Ask the user only for decisions that are irreversible, preference-only, and
unanswerable from the code. Otherwise assume, state the assumption, proceed.

## 7. Parallel sessions

Two sessions can share one working tree and one TestRunner pair.

- Stage by explicit path, always — never `git add -A`/`.`/`-u`, never
  `git commit -a`. Enforced by `tools/githooks/deny_broad_staging.py`.
- One dedicated branch per session.
- Nothing uncommitted at session end — a WIP commit costs nothing.
- Check the TestRunner lock before test runs (§1).
- Freeze protocol: announce a multi-hundred-line refactor before starting;
  other sessions commit-or-stash and pause work on that file and all test
  runs until it lands, same day.
- Work vanished, `git status` unexpectedly clean? Check `git reflog` and
  the other branch's tip before assuming it's gone.

## 8. Lifecycle and bugs

Feature lifecycle: design gate for real scope (confirm shape/economy before
code; smaller work uses the intent header, §2) -> risk-scaled handoff when required
(`docs/handoffs/<slug>/`, shape: `docs/HANDOFF_TEMPLATE.md`) -> build ->
gap audit (`GAP_AUDIT.md`, same folder) -> verify (§5, plus screenshot for
UI-visible changes) -> land -> update `docs/CODE_MAP.md` if a screen,
system, or file moved.

Bug protocol: reproduce with a test first when feasible; fix the mechanism,
not the symptom; pin formula fixes with literal values (`docs/CODE_STANDARDS.md`
§8); if it closes an `AUDIT.md` finding, strike it in the same commit.

## 9. Commit conventions

- Messages explain reasoning and tradeoffs — the git log is real history.
- An asset and its `.meta` commit together, always. Enforced by
  `tools/githooks/pre-commit`.
- Regenerated scenes commit with the change that caused the regeneration.
- New-file sequence: create it -> `run_tests_parallel.ps1 -BuildScenes`
  (generates its `.meta`, syncs to main and both TestRunner copies) ->
  commit both together.

## 10. Enforced rules

| Rule | Enforced by | Fails at |
|---|---|---|
| Layout has no overlaps/overflow/zero-sized graphics at four aspects | `UiAudit` | scene build |
| No serialized UI reference left null | `UiWiringSweep` | scene build |
| No `SetField`-style wiring | lint test | test run |
| Every test class maps to an area | `run_tests_parallel.ps1` | test run, no bypass |
| Asset and `.meta` commit together | `tools/githooks/pre-commit` | commit |
| Staged `.ps1` is ASCII outside comments | `tools/githooks/pre-commit` | commit |
| `tools/domain-tests` builds after `Domain/`/`Tests/EditMode/` changes | `tools/githooks/pre-commit` | commit |
| No `git add -A/-u/.`, no `git commit -a` | `tools/githooks/deny_broad_staging.py` | before the command runs |
| Agent launches use the four project types with their pinned models; `Workflow` denied | `tools/githooks/route_agents.py` | before the command runs |

The two git hooks need `git config core.hooksPath tools/githooks` (§1). The
two PreToolUse hooks (`deny_broad_staging.py`, `route_agents.py`) and the
`.claude/agents/` definitions load at session start — a running session
must restart to pick up a change to either.

## 11. Doc update index

| When you… | Touch… |
|---|---|
| Add/rename a screen, system, or part file | `docs/CODE_MAP.md` |
| Add an art kit, or change a keying/delivery convention | `docs/ART_PIPELINE.md` |
| Fix an `AUDIT.md` finding | `AUDIT.md` (strike it, cite the commit) |
| Add a panel to `SceneBuilder` | `ScreenshotTool.cs`'s `KnownPanels` + `screenshot.ps1` usage text |
| Change a test command or its timing | `docs/TESTING.md` |
| Change test-tooling behavior otherwise | that tool's own header comment |
| Exclude a test file from the dotnet host (or back) | `tools/domain-tests/README.md`'s exclusion table |
| Add/change a `Raw*Entry` field | `[ContentDoc]` on it; run `tools/content_schema.ps1`; commit `docs/CONTENT_SCHEMA.md` |
| Change what a balance batch writes, or a metric means | `docs/BOT_SUMMARY_SCHEMA.md` |
| Finish/partially close a handoff's implementation | its `GAP_AUDIT.md` verdicts |
| A shipped plan or handoff lands | move it into `docs/archive/` |
| Change an agent's role or model | `.claude/agents/<name>.md` and the `route_agents.py` pin, same commit |
| Change a workflow rule, ritual, or convention | this file |
| Change a rule that a hook enforces | that hook's script, plus §10 |
| Lose time to something avoidable | `docs/INCIDENTS.md`, with what now prevents it |
