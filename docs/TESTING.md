# Testing

The canonical gate matrix and the commands. All `.ps1` commands run as
`powershell -NoProfile -ExecutionPolicy Bypass -File tools/<script>.ps1 ...`.
How the tools work inside: each script's header. Rules for writing tests:
`.claude/rules/tests.md`; for editing `tools/`: `.claude/rules/tools.md`.

## Final gate by change class

Pick one row for the whole change set; several apply → the strongest. A gate
result is void once a file relevant to it changes.

| Change class | Final gate |
|---|---|
| Game/runtime/editor code; content or generated artifacts; Unity tests or test tooling; files the game, Editor, build or packages consume | `run_tests_parallel.ps1 -Changed`, plus every applicable `-BuildScenes`/`-BuildContent`, screenshot, focus or schema check below. Full run (no flag) before any release or merge the owner names |
| Agent definitions, orchestration config (`CLAUDE.md`, `AGENTS.md`, `.claude/`, `.codex/agents/`), workflow/prose docs only | Format checks for the touched files, `git diff --check`, `sh tools/githooks/pre-commit`, the hook tests (`python3 tools/githooks/route_agents_test.py`, `deny_broad_staging_test.py`) when hooks or routing changed, diff review against the acceptance list; then one independent verifier. No Unity |

## Iterating

| Situation | Command |
|---|---|
| One class or area | `test.ps1 <fuzzy-name or area>`. Areas: `combat hub content run ui art rng`, one folder each under `Tests/EditMode/` and `Tests/PlayMode/`. `-List` shows every class, marked `[D]` (dotnet host, ~4s) or `[U]` (boots Unity; any PlayMode class makes the slice ~3 min) |
| Several touched files | `test.ps1 -Changed`: maps uncommitted changes to areas; an unmappable file refuses (exit 2) |
| Doubt a dotnet result | `test.ps1 <slice> -Unity` forces Unity (NUnit 3.5 there, 3.14 in `tools/domain-tests`) |
| `Domain/` or `Tests/EditMode/` still compiles on the fast host | `dotnet build tools/domain-tests` (pre-commit runs it when either is staged) |
| Changed `tools/test_areas.ps1` discovery | `test.ps1 -List -SelfCheck` → `SELF-CHECK: ok` |

## The commit gate

- `run_tests_parallel.ps1 -Changed` runs only the classes the change can
  affect (selection rules: `tools/test_select.ps1` header). It prints
  `FULL RUN, not a slice, because:` when it promotes itself: the full-suite
  tier (`tools/`, `Shared/`, `.asmdef`, scenes, generators), an unmapped
  file, a global-state write, `-BuildScenes`, or the safety net (HEAD more
  than 5 commits or 24h past the last green full run in
  `tools/.last-full-green`).
- `-Changed -DryRun` prints the selection and stops; `-ChangedPaths a,b`
  resolves given paths (for checking, not gating).
- The full run (`run_tests_parallel.ps1`, ~3 min, ~6 GB RAM) splits PlayMode
  into `-Shards <n>` (default 3) runner copies. It refuses to start on a
  test file outside an area folder, nested deeper than one folder, a
  testable file in `Shared/`, a duplicate class name, or an undiscovered
  class; the fix is usually a `git mv`. A failing full run prints a
  MAP-GAP REPORT naming commits whose `-Changed` would have skipped it.
- A timing test failing only when sharded: compare with `-Shards 1`.
- `-SkipSync` is legal when nothing changed since the last synced build.

## Build flags

| Changed | Add |
|---|---|
| `ContentData/*.json` or `ContentBuilder.cs` | `-BuildContent` |
| A screen tree (`Domain/UiKit/Screens/`), `Editor/SceneBuilder/`, or a `[SerializeField]` | `-BuildScenes`; again before committing regenerated scenes |

- "Content is stale" from `ContentFreshnessTests`: rebuild and commit the
  regenerated `Resources/Content/content_stamp.json` with the change.
- Content only, no tests: `build_content.ps1` (~14s, main, Editor closed);
  with the Editor possibly open: `preview.ps1 -Build`.

## Seeing it

| Situation | Command |
|---|---|
| UI-visible change | `screenshot.ps1 -Panel <Name>`, then look at the PNG |
| Anything that moves | `screenshot.ps1 -Runtime` (`-Panel` renders Edit Mode, which never ticks `Update()`) |
| A new enemy row | `preview.ps1 -Enemy <id>` → `tools/screenshots/preview/<id>/`; `-Launch` plays it |
| A new skill row | `preview.ps1 -Spell <id> [-Element <DamageType>] [-Versus <ids>]`; a `REFUSED` line names the unsupported effect (`PreviewStage.Supported`) |
| A new character row | `preview.ps1 -Character <id>` |
| Delivered stance stills | `python tools/slice_actor_sheet.py ...`; checklist: `docs/ART_PIPELINE.md` sec. 5a |
| Where PlayMode time goes | `test-profile-PlayMode*.csv` in the runner copy (`PlayModeTestProfiler.cs`); scene loads: `$env:PP_BENCHMARK='1'; test.ps1 SceneLoadBenchmarkTests` |
| Balance report (not a gate) | `bot.ps1 -Runs 200 [-Shards 4]` → `reports/bot/<timestamp>/`; metrics: `docs/BOT_SUMMARY_SCHEMA.md` |

## Flakes

The third failure of the same test is a mandatory root-cause, not another
rerun. Read the runner's `test-run-<Platform>.log` first; classify wiring
(exception) vs logic (wrong value); correlate with the trigger (fresh build,
test order, concurrent run). Prove an environmental fix with 3 green runs
under the provoking condition. Out of time: add the cheapest assertion that
catches the suspected class (`Assert-GuidsMatch` in
`tools/run_tests_parallel.ps1`) and file an `AUDIT.md` open investigation.
