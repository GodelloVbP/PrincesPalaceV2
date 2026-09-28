# Prince's Palace — v2. THIS is the live project.

> ## One Prince's Palace under `C:\Games\`, and this is it
>
> v1 was moved to `C:\Games\Backup Princes palace\Prince's Palace`. v1 is kept
> for reading history and migration only — never edit it.
>
> If you are ever in a Prince's Palace tree and unsure which:
>
> ```bash
> test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
> ```

No shared code with any other game in `C:\Games\`. Catan, Godot, or a
milestone name like "M137" means the wrong project's context — stop.

Unity 6 (6000.5.7f1), URP with a Renderer 2D, C#, turn-based roguelike dungeon crawler.
uGUI with TextMeshPro (`TMP_Text`/`Button`/`Image`); Hades-style painterly art direction.

## How work happens (Claude Code routing)

Opus 5.5 at medium effort (the main session) orchestrates only: reads files,
runs `git status`, writes briefs, launches agents, judges reports. The
orchestrator NEVER edits, runs tests, or writes scripts directly.

Fable is not used on this project for now (owner, 2026-09-23); do not switch
a session or agent to it.

Five agent types, each pinned to one model, plus the built-in
`claude-code-guide`. No other `subagent_type`, no model override, no
`Workflow` tool — enforced by `tools/githooks/route_agents.py`. Owner
decision, 2026-09-24: `implementer` moved to Opus 5.5 at medium effort and
`reader` moved to Sonnet 5. The only Opus this project allows is exactly
`claude-opus-5-5` — no other Opus, including a bare `opus` alias, is
routed. Also 2026-09-24: easy fixes run on Sonnet 5 (`fixer`), harder
ones stay on Opus 5.5.

**Triage before every implementation launch:** is this doable as a bounded
change, or does it go deep architectural? Trivial and already diagnosed
(brief carries file:line + excerpt + failure) → `fixer`. Doable → `implementer`. Deep
architectural → `senior`, with a stated reason — every Opus launch is
hook-checked, not a per-call judgment call.

| Type | Model | Use when |
|---|---|---|
| `reader` | Sonnet 5 | Locate files and extract facts. Returns file paths, line numbers, and relevant excerpts. Never interprets or diagnoses. |
| `fixer` | Sonnet 5 | A trivial, pre-diagnosed fix: brief carries file:line + excerpt + failure scenario, one file or one system with known tests. Stops and reports back if the fix is unclear, reaches beyond the brief, or is architectural. Tests via the dotnet `[D]` loop or `tools/test.ps1 <area>` only — never `run_tests_parallel.ps1`. |
| `implementer` | Opus 5.5, medium effort | The default for every implementation. Fix or build based on a brief. If a reader ran first, the brief includes reader's findings (file paths, line numbers, excerpts) — implementer acts on those without re-reading the tree. If no reader ran, implementer diagnoses, implements, tests, corrects. |
| `verifier` | Sonnet 5 | Run one named gate once, report pass/fail. |
| `senior` | Opus 5.5, medium effort | Deep architectural work only: brief must carry `Escalation:` with one of three criteria; hook-enforced. |

**Handoff pattern:** Reader outputs `file.cs:123-145` + excerpt. Implementer's brief includes that exact location and excerpt, so implementer reads only what's necessary. This avoids re-reading the codebase and maximizes cache reuse.

Workers cannot launch agents. `senior` REQUIRES a brief with a line
`Escalation: <criterion>` naming exactly one of:

- `architecture` (by triage) — redesigns a contract or lifecycle.
- `cross-layer` (by triage) — must land atomically across Domain + Core +
  Editor/scene generation, with ordering, serialization or lifecycle risk.
- `two-failed-cycles` (fallback) — an implementer already failed two
  correction cycles on this same issue.

scoped to only that portion, or it refuses — see `docs/WORKFLOW.md` §6.
`unknown-cause` is removed; there is no generic "difficult" criterion.
Max three active agents
at once. One owner per issue — no duplicates, no replacement launch without
a stated reason. Details: `docs/WORKFLOW.md`.

---

## The two rules that matter most

### 1. Scenes are generated, never hand-authored

`Assets/_Project/Scripts/Editor/SceneBuilder/SceneBuilder.cs` is the only
thing that writes scenes. Never hand-edit a scene and save it in the Editor.
It knows nothing about layout: it builds camera/Volume/canvas/EventSystem,
then hands each screen's tree to `UiEmitter`. To change UI, change the
screen's tree in `Domain/UiKit/Screens/` and its wiring in
`Editor/SceneBuilder/ScreenRegistry.cs`. A full regeneration reassigns every
`fileID` — a thousand-line diff is normal. `UiAudit` re-solves every screen
at four canvas aspects at BUILD time and refuses overlaps, overflow,
duplicate names, zero-sized graphics; every exemption states a reason
(`AllowOverlap("...")` / `AllowOverflow("...")`).

### 2. Content is generated, never hand-edited

`Assets/_Project/Scripts/Editor/ContentBuilder.cs` generates every
ScriptableObject under `Assets/_Project/Resources/Content/`. Regeneration is
destructive — it deletes the whole tree first. Never hand-author an asset
there. `Raw*Entry` fields, defaults, and the enum names they parse against:
`docs/CONTENT_SCHEMA.md` (generated from the types; cannot drift).

---

## Verification

- Iterate: `tools/test.ps1 <area>` or `tools/test.ps1 -Changed`.
- Select the commit gate by change class in `docs/TESTING.md`. Game/runtime,
  editor, content, generated artifact, test, and dependency changes use
  `tools/run_tests_parallel.ps1 -Changed`: only the tests the change can
  affect, promoted to the full run by itself (full-suite paths, an unmapped
  file, `-BuildScenes`, or 5 commits / 24h since the last full green) and
  saying why. Agent/workflow prose or configuration alone uses its
  documented focused policy gate and an independent verifier.
- The full run (`tools/run_tests_parallel.ps1`, no flag) before any release
  or merge the owner names.
- Add `-BuildScenes` when a `[SerializeField]` or a screen tree changed, and
  again before committing it; `-BuildContent` likewise for content.
- Commit only test-passing checkpoints.
- `.ps1` files MUST be pure ASCII, no BOM — PS 5.1 reads a BOM-less file as
  Windows-1252 and a UTF-8 em-dash breaks the parser. Enforced by
  `tools/githooks/pre-commit`.

Which command, timings, full decision table: `docs/TESTING.md`.

## Never `git add -A`

Stage by explicit path, always. Never `git add -A`/`.`/`-u`, never
`git commit -a` — two sessions can share one working tree, and a broad stage
can sweep another session's uncommitted work into a commit. Enforced by
`tools/githooks/deny_broad_staging.py`.

---

## Five gotchas

1. Sync rebuilt scenes and `Resources/Content/` back to main immediately
   after a TestRunner build, before any main→TestRunner robocopy, or stale
   files clobber what was just built (`-BuildContent -BuildScenes` does this).
2. New files need their `.meta` copied back too, same pass — miss it and the
   GUID regenerates on next `Library` rebuild, orphaning every reference.
   Enforced at commit by `tools/githooks/pre-commit`.
3. Diff `Art/` after a scene build too — `LoadSprite()` silently flips
   importer settings and writes a fresh `.meta`.
4. New content types declare ordering — `ContentDatabase.LoadOrdered<T>`
   requires `IOrderedContent`; a lint bans `Resources.LoadAll` elsewhere.
5. Never let a test recompute a production formula for its own expected
   value — that's a tautology. Pin formula tests with literal values.

---

## Where things are

| Need | Go to |
|---|---|
| Workflow, assignments, ownership, verification rules | `docs/WORKFLOW.md` |
| Which test command | `docs/TESTING.md` |
| Screen/system → file (Grep it, never read whole) | `docs/CODE_MAP.md` |
| Code rules | `docs/CODE_STANDARDS.md` |
| Art pipeline | `docs/ART_PIPELINE.md` |
| Content fields | `docs/CONTENT_SCHEMA.md` |
| Open debt | `AUDIT.md` (open only; struck: `docs/AUDIT_STRUCK_ARCHIVE.md`; v1: `docs/AUDIT_V1_ARCHIVE.md`) |
| System map | `architecture_audit.md` Part I |
| Why a rule exists | `docs/INCIDENTS.md` |
| Shipped plans, handoffs | `docs/archive/README.md` |
| Handoff shape | `docs/HANDOFF_TEMPLATE.md` |

## Conventions

- Challenge the ask, then state three lines: Change / Don't touch / Done when.
- Commit messages explain reasoning and tradeoffs, not just the change.
- Fix the class of bug, not the instance, where possible.
- Graceful degradation on missing content is the house style.
- Build the model, not the patch — check it still fits before extending it;
  no flag/slot/special case in place of that. `docs/CODE_STANDARDS.md` §10.
- `ui-ugui` skill: overridden by rule 1 above — never edit a scene/prefab
  directly, edit `Domain/UiKit/Screens/` and `ScreenRegistry.cs` instead.
- `anti-ui-slop` skill: inert (no UIZZE MCP); its "never report missing
  evidence" policy is not followed — missing evidence gets reported.
- `game-ui-ux` skill: its Inspector-setup examples (CanvasScaler, anchors,
  Navigation) are overridden by rule 1 — apply its principles through
  `Domain/UiKit/Screens/` + `ScreenRegistry.cs`; `UiAudit` already enforces
  multi-aspect layout.
- `game-feel` skill: UI feedback (tweens, pops, transitions) applies;
  feedback never blocks input or touches simulation state — no global
  `Time.timeScale` hit-stop.
- `refactoring` skill: its "green baseline, small behaviour-preserving
  steps" rule applies with this project's gates (`docs/TESTING.md`), not
  its TypeScript/mutation-testing tooling; per-commit approval is replaced
  by the orchestrator's review.
- `save-systems` skill: a checklist only (examples are Godot);
  `Core/SaveSystem.cs` is the save path.
