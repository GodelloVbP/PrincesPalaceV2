# Authoring baseline, 2026-09-05

Measured, not estimated. Every line below carries an observed ISO timestamp.
All exercise edits and all Unity runs happened in `C:\Games\Prince's Palace-v2-TestRunner2`.
Main tree touched only for this file.

Categories: edit | command | wait | error | lookup

## Log

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-05T23:10:18+02:00 | command | `ls TestRunner/Temp/UnityLockfile`, `ls TestRunner2/Temp/UnityLockfile` | both absent -- no stale lock, no Unity.exe check needed |
| 2026-09-05T23:10:27+02:00 | lookup | CLAUDE.md (full) | the two generated-artifact rules, the five gotchas |
| 2026-09-05T23:10:58 -> 23:11:13 | command | Sync-Runner replica -> TestRunner2 (robocopy /MIR Assets, Packages, ProjectSettings, docs + Repair-Metas + productName rewrite) | 14.57s total. robocopy Assets 0.12s (tree already mirrored, so this is NOT a true cold copy); Repair-Metas' per-.meta Get-FileHash pass is 14.35s of it, and it runs whether or not anything moved |
| 2026-09-05T23:11 - 23:13 | lookup | tools/test.ps1, tools/run_tests_parallel.ps1 (Sync-Runner, GenerationRun phase), tools/screenshot.ps1, tools/graphics_tests.ps1 | to reproduce the batchmode arg lists against TestRunner2 by hand |
| 2026-09-05T23:11 - 23:13 | lookup | docs/WORKFLOW.md S8, docs/ART_PIPELINE.md S4/S4a/S5/S5b, docs/CONTENT_SCHEMA.md (characters/enemies/skills/SpellPresentation), the 3 _readme strings | the schema and the rules |
| 2026-09-05T23:12:43+02:00 | lookup | ContentData/characters.json `_readme` | WHERE THE 60 IS: "THE SIX ABILITY SCORES MUST TOTAL EXACTLY 60" is in the _readme prose only. CONTENT_SCHEMA.md says "must total exactly the resolver's budget" per field and never names the number |
| 2026-09-05T23:13:10+02:00 | edit | cp Resources/Enemies/treant/{6 png,6 png.meta} -> Resources/Enemies/baseline_treant/ (TestRunner2) | 6 stills copied |
| 2026-09-05T23:13:2x | lookup | Editor/StanceSpriteImporter.cs | confirms a NEW png under Resources/Enemies gets Sprite/alphaIsTransparency/isReadable automatically on import -- no manual importer step. But nothing in it or in CLAUDE.md gotcha #2 says what to do with a COPIED .meta |
| 2026-09-05T23:13:36+02:00 | edit | TestRunner2 ContentData/enemies.json + skills.json + Resources/StanceManifest.json | three files, one new row each: baseline_treant / baseline_treant_slam / Enemies/baseline_treant groundLine 8 |
| 2026-09-05T23:13:52 -> 23:15:29 | wait | gen2.ps1 (Unity -batchmode -nographics -executeMethod GenerationRun.RunAll -ppSteps content -projectPath TestRunner2) COLD | 97.1s wall. Marks: ProceduralSpriteBaker 1.7s, PipelineBuilder 0.2s, ContentBuilder 66.3s, total 68.2s. All four BUILD-COMPLETE sentinels present; no [ContentBuilder] error |
| 2026-09-05T23:15:29 | error | duplicate GUIDs from the copied .meta files | Unity logged `GUID [4bffed79...] for asset 'Assets/_Project/Resources/Enemies/baseline_treant/idle.png' conflicts with:` / `Assigning a new guid.` x6. NAMES THE FILE, does not name the file it conflicts WITH (the line after "conflicts with:" is blank). Non-fatal -- Unity silently rewrote the copies' .meta. On main this is the gotcha-#2 hazard in reverse: gotcha #2 says always carry the .meta, and for a duplicated asset carrying it is exactly wrong |
| 2026-09-05T23:15:48 -> 23:17:04 | wait | same command, WARM (nothing changed) | 75.4s wall; ContentBuilder 64.8s. Cold-vs-warm delta is ~22s of Unity boot + importing the 6 new pngs. ContentBuilder itself does NOT get cheaper: it deletes and regenerates the whole content tree every run, so a one-row JSON edit costs the same ~65s as a rewrite of every file |
| 2026-09-05T23:17:49 -> 23:18:11 | wait | pm2.ps1 -Pattern ".*\.(EnemyFightableTests\|EnemyStanceCaptureTests)\..*" (Unity -batchmode -nographics -runTests -testPlatform PlayMode, TestRunner2, NO sync) | 21.2s wall, 5.8s of tests. Total 6 / Passed 5 / Failed 0 / Skipped 1. Green FIRST TRY. Skipped = CaptureBothKitsOnTheStage self-ignoring for want of a graphics device |
| 2026-09-05T23:17:49 | lookup | PlayMode/Combat/EnemyFightableTests.cs, PlayMode/Art/EnemyStanceCaptureTests.cs | which assertions actually cover a new mob. EnemyFightableTests sweeps ContentDatabase.Enemies, so it picked up baseline_treant with no edit; EnemyStanceCaptureTests.EveryStanceEveryEnemyCanReachResolvesToFrames likewise resolves every ability's stance. Only the CAPTURE is hard-coded |
| 2026-09-05T23:18:22+02:00 | edit (CODE) | TestRunner2 .../Tests/PlayMode/Art/EnemyStanceCaptureTests.cs | 3 substitutions: `e.id == "treant"` -> `"baseline_treant"`, its assert message, and the output filename. Required because CaptureBothKitsOnTheStage names its two enemies literally; there is no id parameter and no env var |
| 2026-09-05T23:18:30 -> 23:18:56 | wait | pm2.ps1 -Pattern PrincesPalace.PlayModeTests.EnemyStanceCaptureTests -Graphics (i.e. -batchmode WITHOUT -nographics, screenshot.ps1's -Runtime shape) | 25.7s wall. 5/5 passed, nothing skipped. Wrote tools/screenshots/runtime/Enemies_beetle_baseline_treant.png |
| 2026-09-05T23:19:10+02:00 | lookup | viewed the PNG | CORRECT first look: "Baseline Treant" nameplate, treant art on the stage, feet on the ground, red target glow. E1 time to first correct preview = 23:13:10 -> 23:19:10 = 6m00s |
| | | **FRICTION** | tools/screenshot.ps1 and tools/graphics_tests.ps1 both hardcode `-TestRunner` (not TestRunner2) as their target AND sync main over it first. Neither can be pointed at TestRunner2, so the capture command had to be hand-assembled from screenshot.ps1's argument list. -SkipSync exists on screenshot.ps1 but the path is still the wrong copy |

### Exercise 2 -- spell previewed

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-05T23:19:35 | lookup | ContentData/skills.json (frost_flare), ls Resources/Spells/, PlayMode/Combat/SpellVfxTests.cs | real folder name is `Spells/frost_flare` (9 frames f0..f8); frost_flare's own timing is seconds 0.52 / impactFrame 5. Six of SpellVfxTests' 36 tests sweep ContentDatabase.Skills generically, so a new row is covered with no test edit |
| 2026-09-05T23:19:59+02:00 | edit | TestRunner2 ContentData/skills.json | one row: baseline_flare, characterId sheep, unlockLevel 1, DamageSingle, manaCost 6, vfx.path Spells/frost_flare, seconds 0.9, impactFrame 2 (both deliberately unlike frost_flare's) |
| 2026-09-05T23:20:09 -> 23:21:28 | wait | gen2.ps1 (content), cycle 2 | 78.5s wall; ContentBuilder 66.8s. Clean, 4 sentinels, baseline_flare.asset generated |
| 2026-09-05T23:21:34 -> 23:22:03 | wait | pm2.ps1 SpellVfxTests | 28.8s. Total 36 / Passed 35 / FAILED 1 |
| 2026-09-05T23:22:03 | error | SpellVfxTests.AFullFormationGetsThreeEruptionsAndExactlyOneFault -- "the fault was never drawn / Expected: True / But was: False" | DOES NOT NAME ANY CONTENT ID, and nothing in the message says it is unrelated to the row just authored |
| 2026-09-05T23:22:11 -> 23:22:41 | wait | same command again | 29.6s, 36/36 green. Confirmed the KNOWN FLAKE: AUDIT.md #61 "SpellVfxTests flakes between runs on an identical tree -- cause not found", and the HEAD commit is about exactly that. Cost: one 29.6s rerun plus the lookup to find out it was not mine |
| 2026-09-05T23:22:53+02:00 | edit (CODE) | TestRunner2 .../Tests/PlayMode/Combat/SpellCastCaptureTests.cs | added one Shoot(fight, canvas, hero, foe, "baseline_flare", "baseline") line. The class names its spells literally (mud_burst / frost_flare / lightning_bolt) with no parameter |
| 2026-09-05T23:22:54 -> 23:23:50 | wait | pm2.ps1 SpellCastCaptureTests -Graphics | 56.2s wall, 37.2s of test. 1/1 passed, wrote SpellCast_baseline_0..7.png |
| 2026-09-05T23:24:30+02:00 | lookup | viewed SpellCast_baseline_2.png against SpellCast_frost_2.png | correct: the effect plays over the Giant Rat, placed on it rather than beside it. E2 time to first correct preview = 23:19:59 -> 23:24:30 = 4m31s |
|  |  | **CAVEAT ON THE PICTURE** | the strip samples at FRACTIONS of the spell's own authored duration, so a spell differing from its neighbour only in seconds/impactFrame produces a strip that reads almost identically by eye (byte-different at all 8 indices, but not visibly so). The capture proves the spell plays; it is weak evidence that a timing edit landed where intended |

### Exercise 3 -- playable character

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-05T23:24:20 | lookup | Data/SaveData.cs ActiveSquad() and CreateNew() | CreateNew takes the first EffectiveMaxSquadSize() (3) of the roster in FILE ORDER, so a character appended at the end of characters.json is in the roster and never in the squad. To be fielded it has to displace someone in the top three |
| 2026-09-05T23:24:20 | lookup | CharacterEntryResolver: `public const int AbilityScoreBudget = 60;` | the 60 is a named constant in code plus one sentence of _readme prose; CONTENT_SCHEMA.md's per-field text says only "the resolver's budget" and never the number |
| 2026-09-05T23:24:46+02:00 | edit | TestRunner2 ContentData/characters.json + skills.json | baseline_owl2 inserted as roster slot 3 (before owl) on the owl's spread (4/6/8/20/16/6 = 60), reusing the owl's portraitPath and battleSpritePath; one selectable skill baseline_owl2_bolt |
| 2026-09-05T23:24:54 -> 23:26:11 | wait | gen2.ps1 (content), cycle 3 | 76.9s wall; ContentBuilder 65.4s. Clean, 4 sentinels |
| 2026-09-05T23:26:26 -> 23:26:52 | wait | pm2.ps1 SaveDataSquadOfThreeTests+MapFlowTests+PartyStageTests | 25.4s. 17 total, 15 passed, 2 FAILED |
| 2026-09-05T23:26:52 | error | SaveDataSquadOfThreeTests.CreateNew_WithTheSwitchOn_FieldsAllThreeCharactersInFileOrder and .DossierPaging_... | `Expected: "owl" / But was: "baseline_owl2"`. NAMES THE ID, in both. Cause is the hard-coded roster array, not the content |
| 2026-09-05T23:27:02+02:00 | edit (CODE) | TestRunner2 .../Tests/PlayMode/Run/SaveDataSquadOfThreeTests.cs | 2 substitutions, both literal "owl" -> "baseline_owl2" inside CollectionAssert arrays |
| 2026-09-05T23:27:02 -> 23:27:30 | wait | same test slice again | 27.5s, 17/17 green |
| 2026-09-05T23:27:52 -> 23:28:16 | wait | pm2.ps1 MapCaptureTests+FightMenuCaptureTests -Graphics | 23.8s, 3/3 passed -- but WRONG PICTURES: the map walker draws only the party leader (Shawn), and FightMenuCaptureTests fields a solo-Shawn fixture. Neither can show a new character. ~2 minutes lost to picking the wrong capture class, and nothing in the file names says which class fields the real squad |
| 2026-09-05T23:28:56 -> 23:29:14 | wait | pm2.ps1 PartyFormationCaptureTests -Graphics | 17.7s, FAILED: "no party slot holds a combatant named Odette / Expected: not null / But was: null". Names the string it wanted, not the id that displaced her |
| 2026-09-05T23:29:27+02:00 | edit (CODE) | TestRunner2 .../Tests/PlayMode/Combat/PartyFormationCaptureTests.cs | `s.CombatantName == "Odette"` -> `"Baseline Odette"`. A capture keyed on a DISPLAY NAME, which is the field an author is most likely to change |
| 2026-09-05T23:29:27 -> 23:29:51 | wait | same, -Graphics | 23.6s, 1/1 passed, wrote 4 frames + slots.json to tools/screenshots/runtime/party_formation/unlabelled |
| 2026-09-05T23:30:10+02:00 | lookup | viewed f3.png | CORRECT: the owl stands in the party formation in a real fight beside the two sheep, art loaded rather than a fallback plate. E3 time to first correct preview = 23:24:46 -> 23:30:10 = 5m24s |

### The two whole-pipeline timings

| ISO timestamp | what | measured |
|---|---|---|
| 23:13:52 -> 23:15:29 | content step COLD (first GenerationRun after the sync, 6 new pngs to import) | **97.1s wall.** gen.log: `[GenerationRun] ContentBuilder: 66.3s (total 68.2s)`; ProceduralSpriteBaker 1.7s, PipelineBuilder 0.2s. The ~29s the marks do not account for is Unity boot + import + shutdown |
| 23:15:48 -> 23:17:04 | content step WARM (immediately after, nothing changed) | **75.4s wall.** gen.log: `[GenerationRun] ContentBuilder: 64.8s (total 66.6s)`. Two later cycles agreed: 78.5s / 66.8s and 76.9s / 65.4s |
| 23:30:50 -> 23:35:17 | `tools/run_tests_parallel.ps1` (PLAIN -- see below) | **267s / 4m27s.** Its own stamps: `[15.5s +15.5s] sync -> EditMode`, `[26.2s +10.8s] sync -> PlayMode`; then EditMode 2723 tests in 15.3s and PlayMode 763 tests (741 passed, 22 skipped for want of a graphics device) in 222.2s, concurrently. All green |

**Why `-BuildContent` was NOT run.** run_tests_parallel.ps1:401-465 puts the entire
generated-asset write-back inside `if ($BuildContent -or $BuildScenesHere)`, and its first
line mirrors the runner's `Resources\Content` over main's with /MIR. That mutates main's
generated content tree, which contradicts this task's own "the main tree is edited only to
save the log file" -- in a working tree already carrying 118 uncommitted paths from another
session. Two comments in that same file disagree about how bad that is (near line 404:
content is "stable across a rebuild"; the Assert-GuidsMatch header: "EVERY content asset
gets a freshly Unity-assigned guid on EVERY -BuildContent run"), which is itself a reason
not to find out on a dirty tree. The plain run above is the same gate minus that write-back;
add one content generation (75-97s, serial, ahead of the tests) for the -BuildContent
figure: roughly 345-365s.

## Cleanup (verified 23:35)

`run_tests_parallel.ps1` mirrors main into both runners as its first act, so the plain run
above was itself the cleanup. Verified afterwards:

- `diff -r main/ContentData TestRunner2/ContentData` -- identical
- `diff main/Resources/StanceManifest.json TestRunner2/...` -- identical
- `Resources/Enemies/` in TestRunner2 -- no `baseline_treant` folder
- all four edited `.cs` files -- byte-identical to main
- `Resources/Content/{Skills,Enemies}` -- no `baseline_*` assets
- main's `git status` -- 118 paths, all pre-existing at session start; nothing under
  `Resources/Content`, nothing new under `Art/`. The only addition is this file.

Nothing was committed.

## Rules that had to be looked up because nothing enforces or announces them

1. **The ability-score budget is 60.** Only in `characters.json`'s `_readme` prose and as
   `CharacterEntryResolver.AbilityScoreBudget`. `docs/CONTENT_SCHEMA.md`, the generated
   per-field reference an author would reach for first, says "the resolver's budget".
2. **A duplicated asset must NOT keep its `.meta`.** CLAUDE.md gotcha #2 says the opposite
   in every other situation. Unity repaired it silently here; nothing in the docs covers it.
3. **`StanceManifest.json` lives in `Resources/`, not `ContentData/`**, and an actor with
   no entry silently stands on its canvas bottom. Found in ART_PIPELINE.md 4a.
4. **Roster order is squad order.** A character appended to the end of `characters.json` is
   in the roster and never fielded. Only discoverable by reading `SaveData.CreateNew()`.
5. **Which capture class fields the real squad.** Three candidates by name (MapCaptureTests,
   FightMenuCaptureTests, PartyFormationCaptureTests); only the third does, and only its
   source says so.

## Values typed in two places

- `baseline_treant` -- `enemies.json` `id`, `enemies.json` `spritePath` (as
  `Enemies/baseline_treant`), `skills.json` `characterId`, `StanceManifest.json`
  `spritePath`, and the folder name on disk. Five places, no cross-check at build time
  except the stance-resolution sweep.
- `trunk_slam` -- the skill's `stance` string and the PNG filename inside the folder.
- `baseline_owl2` -- `characters.json` `id`, `skills.json` `characterId`, and (because the
  assertion is hard-coded) two arrays in `SaveDataSquadOfThreeTests`.
- `"Baseline Odette"` -- `characters.json` `displayName` and a string literal in
  `PartyFormationCaptureTests`.
