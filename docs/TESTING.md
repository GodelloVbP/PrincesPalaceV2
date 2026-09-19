# Testing

Which command to run, the full test/build/preview decision table, and the
flake-triage protocol. Rules, not narrative -- history: `docs/INCIDENTS.md`.

---

## Flake protocol

**Three strikes: the third failure of the same test is a mandatory
root-cause, not another "passed on rerun."**

Triage order, cheapest first:

1. Read the actual failure log (`test-run-<Platform>.log` in the relevant
   TestRunner copy) before theorizing. Classify: wiring (NullReference,
   missing-script, an import/GUID exception) or logic (wrong value/branch,
   no exception at all).
2. Correlate against the triggering condition: only after a fresh
   `-BuildContent`/`-BuildScenes`? Only under a specific test order? Only
   concurrent with the other platform's run?
3. If it's environmental, prove any fix with 3 consecutive green runs under
   the exact provoking condition -- not one.
4. Timebox it (a rough session, not open-ended). If root cause doesn't
   resolve in that window: add the loudest, cheapest assertion that catches
   the *class* of bug suspected (see `Assert-GuidsMatch` in
   `tools/run_tests_parallel.ps1` for the worked example), write up what was
   ruled out and what remains open as a new `AUDIT.md` finding under "Open
   investigations", and move on.

## Focus guard

- Every `tools/` entry point that can spawn a window MUST call
  `Start-FocusGuard` near its own top and `Stop-FocusGuard` in a matching
  `finally` -- `tools/unity_path.ps1`.
- Every `-nographics` launch MUST go through `Start-UnityQuiet`, never
  `Start-Process` on `Unity.exe` directly.
- Mechanism, the two 2026-09-18 fixes, and the `.ExitCode` gotcha:
  `docs/INCIDENTS.md` ("Batchmode Unity steals focus").
- Run `tools/focus_check.ps1` after touching any launch site or either
  guard function.
- A new window-spawning script that skips `Start-FocusGuard` is back to
  pre-2026-09-18 unguarded behavior.

## Test-run decision table

Every Unity launch in `tools/` goes through `Start-UnityQuiet` in
`tools/unity_path.ps1` -- never `Start-Process` on `Unity.exe` directly.

| Situation | Command |
|---|---|
| Iterating on one class/area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 <fuzzy-name or area>` (areas: `combat`, `hub`, `content`, `run`, `ui`, `art`, `rng` -- each a FOLDER under `Tests/EditMode/` and `Tests/PlayMode/`, so a test's area is simply where its file sits; `-List` shows every class with its area, marking each `[D]` or `[U]` for its host) |
| ... and how long that takes | Depends on whether the slice needs Unity. All-`[D]` runs under `dotnet test`, no Editor: ~4s (`test.ps1 wool` was ~12s, is 3.7s). Any `[U]` class boots Unity; any PlayMode class makes the slice PlayMode-bound -- `test.ps1 combat` is ~110s either way (47/122 classes are PlayMode, 93s of the run). The EditMode half of a mixed slice still costs nothing extra |
| Checking the fast host against the slow one | `tools/test.ps1 <slice> -Unity` forces the whole slice through Unity. Use when a dotnet result looks wrong, or after touching `tools/domain-tests`. Both hosts compile the same files, not the same NUnit (Unity ships 3.5, the host pins 3.14) -- `tools/domain-tests/README.md` |
| Iterating across several touched files, not sure which area | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed` -- maps uncommitted changes (tracked + untracked) to the areas/classes they affect, prints the mapping used. A file it cannot map is a hard refusal (exit 2), never a quiet subset. Forces the full suite instead of a slice: every `.asmdef`, anything under `Tests/**/Shared/`, the scenes, `tools/`, and the five `Editor/` generators (`ContentBuilder`, `GenerationRun`, `PipelineBuilder`, `StanceSpriteImporter`, `PreviewRequestWatcher`) |
| Before any commit | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1` (~5m40s: 17s sync, then EditMode and PlayMode concurrently, PlayMode 311s of it). **Does not build the scenes** -- see below. **Refuses to run at all** on any of five: a test file outside an area folder, anything deeper than one folder inside one, a testable file in `Shared/`, two files declaring the same class name, or a test file declaring a class discovery never saw. Each names the file and the class; no bypass flag; the fix is usually a `git mv` |
| Changed the discovery/area code in `tools/test_areas.ps1` | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -List -SelfCheck` (<1s). Runs against `tools/test_areas_fixture/` (ten files broken eight ways on purpose, two controls that must NOT be refused), asserts each of the five refusals fires on its case and does not fire on a neighbouring non-case, plus the exact count of each list. Prints `SELF-CHECK: ok` or names every miss and exits 1 |
| Wrote a test class whose name already exists, or a generic/nested fixture | Rename it. NUnit reports a generic fixture as `Foo<Int32>` and a nested one as `Outer+Inner`; every filter this repo builds (Unity's `-testFilter`, dotnet's `FullyQualifiedName`) is the bare declared name, so none can be selected by any run. All three are refused rather than accommodated |
| Changed `Domain/` or `Tests/EditMode/` and want to know the fast host still compiles | `dotnet build tools/domain-tests` (~2s warm). `tools/githooks/pre-commit` runs this for you when either is staged |
| Changed `ContentData/*.json` or `ContentBuilder.cs` | add `-BuildContent` |
| Just want the content assets rebuilt, no tests | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1` -- ~14s warm, one batchmode Unity against **main**, in place. No runner copies, no sync-back. Refuses while the Editor is open (Unity locks `Library`) and tells you to use the row below; a lockfile with no Unity behind it is cleared as debris |
| ... and the Editor might be open | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Build` -- same work, but checks the lockfile and the process table and picks batchmode or `Editor/PreviewRequestWatcher.cs` for you |
| Wrote an `enemies.json` row and want to SEE the mob | `tools/preview.ps1 -Enemy <id>` builds content, writes `tools/screenshots/preview/<id>_stances.png` (a contact sheet of every pose) and `<id>_turn<n>_<ability>.png` (one frame per kit turn). ~45s. `-Launch` plays the fight in the Editor instead; `-Formation full` fields three copies so a summon has a slot to fail on. The id is validated against `enemies.json` before any Unity boots |
| ... and why the mob does its whole kit in order | `-Launch` and the capture route both hand the session an `EnemyShowcase`: authored abilities in order, one per turn, then the plain swing. An ability whose prerequisite is unmet this turn is named and skipped, not substituted |
| Wrote a `skills.json` row and want to SEE the spell | `tools/preview.ps1 -Spell <id>` builds content, stands up a fight for one cast, writes `tools/screenshots/preview/spell_<id>_{before,impact,after,tail}.png` (plus `_ground.png` for a spell with a ground layer). An elemental spell casts as the first element that draws anything (the log names which); `-Element <DamageType>` casts the one named instead, filenames include the element. The element is validated against that skill's own `elements[]` before any Unity boots. Caster is worked out from content: the character the skill names, else whoever carries the signature resource it spends. Mana/resource filled, ability requirements waived one score at a time, party opens at half health for a heal -- all printed, not done quietly. `-Launch` plays it in the Editor via the buttons a hand would press |
| ... and it says "REFUSED" | Read the line: it names the effect or the id. Seven `SkillEffect`s (`Ward`, `Shatter`, the three Gifts, `Provoke`, `RestorePartyMana`) need a talent tree, an existing ward, or a drained party first, so they are refused rather than approximated. "No character in content carries resource X" means what it says |
| Wrote a `characters.json` row and want to SEE them | `tools/preview.ps1 -Character <id>` fields them alone and writes four pictures: map figure (walk stance), dossier portrait, fight stage at rest, fight stage mid-cast. Turn one casts the first kit row. `-Launch` plays it instead. The file must flag three starters (`startsInSquad`/`squadSlot`); the resolver refuses the build otherwise, naming ids |
| Delivered a combat actor's stance stills | `python tools/slice_actor_sheet.py --sheet ... --actor Enemies/<id> --stances ...` writes the stills, the `StanceManifest.json` `groundLine` entry, and `Art/<Enemies\|Characters>/<id>/recipe.json`. Verify: `--recipe <that path>`, then check `git status --short Assets/_Project/Resources/` is clean -- clean means reproducible; not clean means it belongs in `Art/Sheets/hand_assembled.json`'s `actors` block with its bytes pinned in `HandAssembledArtTests`. Full checklist: `docs/ART_PIPELINE.md` sec. 5a |
| ... and the manifest did not change | Expected. The slicer writes `groundLine` only when the entry is absent or says `groundLineSource: "slicer"`; an `authored` entry is left alone and the delta is printed |
| "the content is stale" from a test | `ContentFreshnessTests` compares a sha256 over `ContentData/*.json` + `Scripts/Domain/Content/**` + `Scripts/Core/Content/**` + `ContentBuilder.cs` + nineteen named Domain files against `Resources/Content/content_stamp.json`. `ContentInputCoverageTests` derives that set by reflection every run and fails naming the file and line to paste when a record starts reaching somewhere new. Editing anything else under `Scripts/Domain/` does not mark content stale. When it is stale, the regenerated **stamp must be committed with the change**; a missing stamp means the last build died partway |
| Changed a screen tree under `Domain/UiKit/Screens/` or wiring in `Editor/SceneBuilder/` | add `-BuildScenes` (~1 min slower). Builds the scenes in the runners, where PlayMode reads them, and copies them back to main and the PlayMode runner. `UiAudit` itself does not need this flag -- it already runs inside the EditMode screen tests, which re-solve layout from the screen tree in code, not from a built scene |
| About to COMMIT regenerated scenes | same flag, `-BuildScenes` -- there is no separate build-only vs. sync-only mode |
| Re-running with nothing changed since the last synced build | `-SkipSync` is legal |
| UI-visible change | also `tools/screenshot.ps1 -Panel <Name>` and actually look at the PNG |
| Change to anything that MOVES (an ambient animator, an intro, a tween) | `tools/screenshot.ps1 -Runtime`. `-Panel`/`-All` render Edit Mode, which never ticks `Update()` -- a static capture cannot show motion, and pure-curve unit tests only prove the formulas vary, not that anything calls them |
| Balance-bot batch (not a correctness gate -- a report to read) | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 200`. Defaults cover all four archetypes and sample the determinism replay at 0.1. Writes `reports/bot/<timestamp>/` (gitignored), merges shards with `tools/bot_merge.py` into `summary.json`, renders `report.html` via `tools/bot_report.py`. Metric contract: `docs/BOT_SUMMARY_SCHEMA.md` |
| ... a big one (thousands of runs) | add `-Shards 4`. N shards means N project copies (`-Bot1`...`-BotN`, created on first use -- that run pays a full asset import, later ones boot in ~20s); Unity refuses two batchmode instances against one copy. Peak ~1.1 GB per shard, so 4 is the ceiling on 16 GB. `-TestRunner2` is deliberately excluded from the sharded path so a batch cannot clobber a concurrent test run |
| ... iterating on the bot itself | `-Shards 1` stays on `-TestRunner2` and skips the copy machinery (default is `min(4, processors/2)`). `-ReplayShare 1` replays every run instead of a tenth; `-InMemorySaves 0` puts the save back on disk (~16x slower), and exists only to prove the fast path did not change a run's hash |
