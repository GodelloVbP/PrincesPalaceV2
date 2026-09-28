# Prince's Palace

Unity 6 (6000.5.7f1), URP with a Renderer 2D, C#: a turn-based roguelike
dungeon crawler. uGUI with TextMeshPro; Hades-style painterly art.
v1 lives at `C:\Games\Backup Princes palace\Prince's Palace` — read-only.
Unsure which tree you are in? `test -d Assets/_Project/Scripts/Domain/UiKit`
succeeds only here. Catan, Godot, or "M137" means another project — stop.

## Two generation rules

1. **Scenes are generated.** Only `Editor/SceneBuilder/SceneBuilder.cs`
   writes scenes; each screen's tree is emitted by `UiEmitter`. To change UI,
   edit the tree in `Domain/UiKit/Screens/` and its wiring in
   `Editor/SceneBuilder/ScreenRegistry.cs`. Never hand-edit a scene. A full
   regeneration reassigns every `fileID`; a thousand-line diff is normal.
2. **Content is generated.** `Editor/ContentBuilder.cs` writes every asset
   under `Resources/Content/`, deleting the tree first. Edit
   `ContentData/*.json`; fields and enum names: `docs/CONTENT_SCHEMA.md`.

## How to work

- Read the whole task first, then take the smallest route that fits. The
  main session may make small, low-risk edits itself (docs, one-liners,
  roughly under 20 lines; never scene, content or runtime code) and commit
  them. Anything larger goes through the routing table below.
- Before building, search for an existing system to hook into
  (`docs/CODE_MAP.md`, Grep) and extend it rather than add a parallel one.
  Say in the brief or commit what you checked.
- Build the model, not the patch: no flag, slot or special case where the
  model no longer fits. Fix the class of bug, not the instance.
- Comments say why the code is this way, in present tense. History, dates,
  owner quotes, "used to", phase names go in the commit message. Delete a
  comment the moment it stops being true; fix stale ones in lines you touch.
- One home per fact: link a rule from another doc, never restate it.
- Be concise in code, comments, docs and reports.
- Verify with evidence (the command and its output) before claiming done.

## Agent routing

Triage every implementation launch. Trivial and already diagnosed (brief
carries file:line + excerpt + failure) → `fixer`. Doable as a bounded change
→ `implementer`. Deep architectural → `senior` with an `Escalation:` line.

| Type | Model | Use when |
|---|---|---|
| `reader` | Sonnet 5 | Locate files, extract facts: paths, line numbers, excerpts. Never diagnoses. |
| `fixer` | Sonnet 5 | Pre-diagnosed fix in one file or system with known tests. Stops if unclear, wider than the brief, or architectural. Tests via the dotnet `[D]` loop or `tools/test.ps1 <area>` only. |
| `implementer` | Opus 5.5, medium | The default implementation owner: diagnose, implement, test, correct. Acts on a reader's excerpts without re-reading the tree. |
| `verifier` | Sonnet 5 | Run one named gate once, report pass/fail. |
| `senior` | Opus 5.5, medium | Deep architectural work only. |

`senior`'s brief must carry a line `Escalation: <criterion>` naming one of:
`architecture` (redesigns a contract or lifecycle), `cross-layer` (must land
atomically across Domain + Core + Editor/scene generation, with ordering,
serialization or lifecycle risk), or `two-failed-cycles` (an implementer
already failed two correction cycles on this issue). There is no generic
"difficult" criterion. `claude-code-guide` is also allowed. No other
`subagent_type`, no per-call model override, no `Workflow` tool, no Fable,
and no Opus but exactly `claude-opus-5-5`: `tools/githooks/route_agents.py`
refuses the rest.

## Verification

`docs/TESTING.md` is canonical for which gate a change needs.

- Iterate: `tools/test.ps1 <area>` or `tools/test.ps1 -Changed`.
- Commit gate for code, content, generated artifacts, tests or
  dependencies: `tools/run_tests_parallel.ps1 -Changed` (promotes itself to
  the full run when needed, and says why). Docs/agent config alone: the
  focused policy gate in `docs/TESTING.md`.
- Full run (`tools/run_tests_parallel.ps1`) before any release or merge the
  owner names.
- Add `-BuildScenes` when a `[SerializeField]` or a screen tree changed, and
  again before committing it; `-BuildContent` likewise for content.
- Commit only test-passing checkpoints.
- `.ps1` files are pure ASCII outside comments, no BOM (PS 5.1 misparses
  UTF-8). Enforced by `tools/githooks/pre-commit`.
- Staging: explicit paths only, never `git add -A`/`.`/`-u`/a directory or
  `git commit -a`; sessions share one tree. Enforced by
  `tools/githooks/deny_broad_staging.py`.

## Five gotchas

1. After a TestRunner build, sync rebuilt scenes and `Resources/Content/`
   back to main before any main→TestRunner copy (`-BuildScenes`/`-BuildContent` do this).
2. A new file's `.meta` commits with it, or its GUID regenerates and orphans
   every reference. Enforced by `tools/githooks/pre-commit`.
3. Diff `Art/` after a scene build: `LoadSprite()` flips importer settings
   and writes fresh `.meta` files.
4. Content types implement `IOrderedContent` and load through
   `ContentDatabase.LoadOrdered<T>`; a lint bans `Resources.LoadAll` elsewhere.
5. A test never recomputes a production formula for its expected value; pin
   literals.

## Where things are

| Need | Go to |
|---|---|
| Ownership, briefs, parallel sessions, commits | `docs/WORKFLOW.md` |
| Which test command | `docs/TESTING.md` |
| Screen/system → file (Grep it, never read whole) | `docs/CODE_MAP.md` |
| System map | `docs/ARCHITECTURE.md` |
| Code rules | `docs/CODE_STANDARDS.md`, plus `.claude/rules/` (load by path) |
| Art pipeline | `docs/ART_PIPELINE.md` |
| Content fields | `docs/CONTENT_SCHEMA.md` |
| Open debt | `AUDIT.md` |

## Skill overrides

- `ui-ugui`, `game-ui-ux`: never edit a scene, prefab or Inspector setup;
  apply them through `Domain/UiKit/Screens/` + `ScreenRegistry.cs`.
- `anti-ui-slop`: inert (no UIZZE MCP); missing evidence is reported.
- `save-systems`: checklist only (Godot examples); `Core/SaveSystem.cs` saves.
- `game-feel`: UI feedback applies; it never blocks input or touches
  simulation state — no global `Time.timeScale` hit-stop.
- `refactoring`: green baseline and small steps with this project's gates;
  the orchestrator's review replaces per-commit approval.
