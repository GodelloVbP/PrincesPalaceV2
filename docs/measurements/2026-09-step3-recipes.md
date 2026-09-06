# Step 3 (recipes and metadata), 2026-09-06

Measured, not estimated. Every line carries an observed ISO timestamp.

**What this repeats.** The **art half** of exercise E1 in
`2026-09-authoring-baseline.md` and nothing else: get a new mob's stances into
`Resources/`, get it into content, and look at it. The baseline's E1 also
covered the test-editing and capture friction that Step 1 and Step 2 removed;
those are not re-measured here, and the two totals below are therefore not
directly comparable as whole-exercise numbers. What IS comparable is the art
half, because it is the same work against the same source sheet.

**One difference in method, stated so nothing is read as a saving it is not.**
The baseline ran in `-TestRunner2`; this ran in **main**, because
`preview.ps1` builds in place and that is the workflow now. The scratch actor
and both content rows were removed afterwards and the tree verified clean --
see Cleanup.

## The three manual art steps this replaces

From the baseline log, in its own words:

1. `cp Resources/Enemies/treant/{6 png,6 png.meta} -> baseline_treant/` --
   which produced six `GUID [...] conflicts with:` errors from Unity, because
   CLAUDE.md gotcha #2 says always carry the `.meta` and for a DUPLICATED
   asset carrying it is exactly wrong.
2. A `Resources/StanceManifest.json` row, hand-written, with a `groundLine`
   read off a terminal.
3. The lookup that made both necessary: `Editor/StanceSpriteImporter.cs`, to
   find out what happens to a copied `.meta`.

All three are gone. One slicer invocation writes the stills (freshly, from the
design sheet -- nothing is copied, so there is no `.meta` to get wrong), the
manifest row, and a `recipe.json`.

## Log

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-06T02:29:23+02:00 | command | `ls TestRunner/Temp/UnityLockfile`, `ls TestRunner2/Temp/UnityLockfile` | both absent. Main held a lockfile with no Unity.exe behind it; `preview.ps1` recognised it as debris and cleared it, unprompted |
| 02:29:23 -> 02:33:38 | wait | `run_tests_parallel.ps1 -BuildContent` | **255s / 4m15s.** EditMode 2765/2765 in 10.2s; PlayMode 774 total, 748 passed, 26 skipped (no graphics device), 0 failed, 187.7s. Green FIRST TRY -- the AUDIT #61 flake did not appear. Its own stamps: sync 14.3s, GenerationRun 19.3s, sync-back 1.1s, Repair-Metas 10.1s, Assert-GuidsMatch 3.1s |
| 02:33:38 | lookup | `git status --short Assets/_Project/Resources/Content` | 0 paths. The `-BuildContent` write-back left main's generated tree byte-identical, so the concern the baseline raised about running it on a dirty tree did not materialise here |
| **02:34:17** | command | `slice_actor_sheet.py --sheet Art/Enemies/treant/sheet_poses.png --actor Enemies/demo_treant --stances idle,attack,trunk_slam,cast,hurt,defeated --key white_flood --pocket-max-area 2000 --delivery-scale 1.05` | **6.0s.** Wrote six stills, `StanceManifest.json: added Enemies/demo_treant groundLine 8 (groundLineSource slicer)`, and `Art/Enemies/demo_treant/recipe.json`. No `.meta` copied, so no GUID conflict. Nothing typed twice |
| 02:34:23 | | | (the arguments came from `Art/Enemies/treant/README.md` + its `recipe.json`; the `--pocket-max-area 2000` that used to need guessing is in the recipe now) |
| 02:34:46 | edit | `ContentData/enemies.json`, `ContentData/skills.json` | two rows: `demo_treant` with one ability, `demo_treant_slam` on the treant's `trunk_slam` stance. Scripted, under 1s. **No `StanceManifest.json` edit** -- that is the step that disappeared |
| 02:34:54 -> 02:35:42 | wait | `tools/preview.ps1 -Enemy demo_treant` | **48.1s.** Cleared the stale lockfile, took the batchmode route, built content in 24.2s (`ContentBuilder: 4.7s`), synced main into the runner, ran the capture fixture: CaptureShowcaseTurns and CaptureStanceSheets passed, CaptureCharacter and CaptureSpellCast self-skipped (their env vars unset). Wrote three PNGs |
| 02:35:50 | lookup | viewed `demo_treant_stances.png` and `demo_treant_turn1_demo_slam.png` | **CORRECT first look.** Contact sheet: five stances on one canvas, feet level. Fight frame: "Demo Treant" nameplate and health bar, art on the stage, feet on the ground, red target glow, and the log line "Demo Treant uses Demo Slam on Shawn for 8 damage" -- the authored ability fired on turn 1 |
| 02:35:59 -> 02:36:20 | command | cleanup (below) | |

## The comparison

| | baseline E1 (2026-09-05) | this run (2026-09-06) |
|---|---|---|
| art step | `cp` 6 PNGs + 6 `.meta`s, hand-write a `StanceManifest.json` row, one lookup to find out what a copied `.meta` does | **one command, 6.0s** |
| errors along the way | 6x `GUID [...] conflicts with:` -- non-fatal, silently repaired by Unity, and the line after "conflicts with:" was blank so it did not name what it conflicted WITH | none |
| files edited by hand | 3 (`enemies.json`, `skills.json`, `StanceManifest.json`) | **2** (`enemies.json`, `skills.json`) |
| content build | 97.1s cold / 75.4s warm, run by hand as a reconstructed batchmode command line | 24.2s, inside `preview.ps1`, route chosen for you |
| capture | required a **code edit** to `EnemyStanceCaptureTests.cs` (three substitutions -- the class named its two enemies literally) plus a hand-assembled `-Graphics` command, because `screenshot.ps1`/`graphics_tests.ps1` both hardcode `-TestRunner` | one flag, no code edit |
| **first command -> correct picture** | **6m00s** (23:13:10 -> 23:19:10) | **1m33s** (02:34:17 -> 02:35:50) |

Most of that gap belongs to Step 1's preview work rather than to Step 3. **The
part Step 3 owns is the first row and the third**: the art step went from a
copy-with-a-known-hazard plus a hand-copied number to one command that leaves
nothing to type, and the `StanceManifest.json` edit stopped existing. The
baseline's own "values typed in two places" list named `Enemies/baseline_treant`
in five places including `StanceManifest.json`; it is four now.

## What the recipes bought, measured separately

| check | result |
|---|---|
| `slice_actor_sheet.py --recipe Art/Characters/owl/recipe.json` | six committed stills reproduced **byte-identical** (`git status` clean under `Resources/`) |
| `slice_actor_sheet.py --recipe Art/Enemies/treant/recipe.json` | six committed stills reproduced **byte-identical** |
| reconstructing the treant's run from its README alone | needed three attempts -- the README said "a raised `pocket_max_area`"; 2000 and 4000 reproduce, 1600 does not (5/6 frames). This is the measurement that says prose is not a recipe |
| `slice_spell_sheet.py` (all five recipes) | 43 of 44 frames byte-identical. `lightning_bolt/f5` differs in 906 px of 262144 at full channel range, deterministically across two runs, so the drift is in the committed file. Recorded in its recipe's `_notes`, art left alone |
| a no-op slicer run against an `authored` entry | prints the delta and leaves `StanceManifest.json` closed; `git diff` empty |

## Cleanup (verified 02:36:20)

- `Resources/Enemies/demo_treant/` and `Art/Enemies/demo_treant/` deleted with
  their `.meta`s.
- `ContentData/enemies.json`, `ContentData/skills.json` and
  `Resources/StanceManifest.json` restored with `git checkout --`.
- `tools/build_content.ps1` re-run (15.5s warm) to take `demo_treant.asset` and
  `demo_treant_slam.asset` back out of `Resources/Content/` and re-stamp it.
- `git status --short Assets/_Project/Resources/ Assets/_Project/ContentData/`
  -- **empty**.
- The three preview PNGs are under `tools/screenshots/`, which `.gitignore`
  covers.

Nothing from this exercise was committed.
