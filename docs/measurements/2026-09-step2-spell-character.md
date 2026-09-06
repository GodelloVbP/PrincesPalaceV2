# Step 2 repeated: the spell and character exercises, 2026-09-06

The Step 0 baseline's exercises 2 and 3 (`docs/measurements/2026-09-authoring-baseline.md`)
run again against the same inputs, with the same columns, after Step 2 landed
`preview.ps1 -Spell` and `-Character`.

Two differences from Step 0's method, both deliberate and both stated because they
change what the numbers mean:

- **In MAIN, not in `-TestRunner2`.** The whole point of the preview loop is that an
  author runs it where they are working; measuring it in an isolated copy would measure
  something nobody does. The scratch content rows were removed afterwards and the tree
  verified clean (below).
- **The commands are the deliverable.** Step 0 hand-assembled batchmode argument lists
  from `screenshot.ps1`; this runs `tools/preview.ps1` and nothing else.

Categories: edit | command | wait | error | lookup

---

## Exercise 2 -- spell previewed

Same inputs as the baseline: one `skills.json` row, `baseline_flare`, `characterId`
sheep, `unlockLevel` 1, `DamageSingle`, `manaCost` 6, reusing `Spells/frost_flare`'s
frames with **different timing** (`seconds` 0.9, `impactFrame` 2 against frost_flare's
own 0.52 / 5).

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-06T01:45:28+02:00 | edit | `ContentData/skills.json` | one row, pasted. No other file touched -- no test edit, no capture-class edit, no manifest |
| 01:45:33 | error | `preview.ps1 -Spell baseline_flare` | **TOOL BUG 1.** "No skill with id 'baseline_flare' in @{_readme=...}" followed by the entire contents of skills.json where the filename should have been. `Resolve-ContentId` took a `[string]$File` parameter and assigned the parsed JSON to a local called `$file` -- PowerShell variable names are case-insensitive, so that is ONE variable, and the id lookup then read the wrong shape and refused every id. Fixed and committed (`0bd9a9f`) |
| 01:46:10 -> 01:46:53 | wait | `preview.ps1 -Spell baseline_flare` | **43.0s wall**, correct pictures. Content build 17.5s of it (ContentBuilder 4.9s), then the mirror into `-TestRunner` and the graphics run |
| 01:46:53 | error | same run's console | **TOOL BUG 2.** Three of the four `PreviewCaptureTests` captures failed with "PP_PREVIEW_* is empty" -- `preview.ps1` filters the fixture by CLASS, so every mode runs and exactly one has its variable set. Three red tests, three stack traces and a non-zero exit sat beside three perfectly correct PNGs. They ignore by name now (`a9f6d19`) |
| 01:47:29 -> 01:47:50 | wait | `preview.ps1 -Spell baseline_flare -NoBuild` | **21.0s wall**, clean output: one `Passed`, three `Skipped` naming the mode each was not asked for, then the three file paths |
| 01:47:55 | lookup | viewed `spell_baseline_flare_impact.png` | **CORRECT first look.** Shawn mid-cast, the frost_flare sequence playing ON the Giant Rat rather than beside it, "Shawn uses Baseline Flare on Giant Rat for 7 damage!" in the log, MP 32/38 (the 6 it costs, spent), Wool 10/10 with the preview's own note -- "Shawn's Wool is as full as it will get" -- printed into the fight log where the author reads it |
| | | **E2 time to first correct preview** | **43s** for the command that produced them (`01:46:10 -> 01:46:53`), against the baseline's **4m31s**. Raw wall clock including finding and fixing both tool bugs: 2m27s (`01:45:28 -> 01:47:55`) |

### Code edits

**Zero demanded by the content.** The baseline needed one
(`SpellCastCaptureTests` names its spells literally, so a new spell could not be
photographed without adding a line). That file reads the spell list from content now, so
the same row is photographed by the verification suite as well without being named
anywhere.

Two code edits DID happen, and neither was asked for by the row: both are fixes to the
Step 2 tooling, found by running it against real content for the first time. They are
committed and cannot recur. Counting them as exercise cost would be counting the cost of
writing the tool, not of using it -- but the honest version of "zero code edits" is
"zero after the tool worked at all", and the tool did not work at all on its first real
invocation.

### What the picture is and is not evidence of

The baseline recorded a caveat here worth carrying forward: its strip sampled at
FRACTIONS of the spell's own authored duration, so a spell differing from its neighbour
only in `seconds`/`impactFrame` produced a strip that read almost identically by eye.
This capture is aimed at the impact instant instead --
`FightController.ImpactDelayFor`, the same function `FightBeatPlayer` waits out, decides
which sample is `_impact.png` -- and the observed damage-popup frame is logged beside the
scheduled one, so the two can disagree visibly. That is a better instrument for a timing
edit than the fraction strip was, and it is still three stills: it shows WHERE the impact
frame lands, not what the 0.9s felt like.

---

## Exercise 3 -- playable character

Same inputs as the baseline: `baseline_owl2` on the owl's spread (4/6/8/20/16/6 = 60),
reusing the owl's `portraitPath` and `battleSpritePath`, plus one selectable skill
`baseline_owl2_bolt`.

One input that is no longer needed: the baseline inserted the row **at roster position 3,
before owl**, because `SaveData.CreateNew` took the first three rows of the file. E3's
own Step 4 item removed that rule -- the file flags its three starters
(`startsInSquad`/`squadSlot`) -- and `-Character` fields the named character regardless,
so the row was simply appended.

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-06T01:48:12+02:00 | edit | `ContentData/characters.json` + `skills.json` | two rows, one each. No test edit, no capture-class hunt |
| 01:48:17 -> 01:48:59 | wait | `preview.ps1 -Character baseline_owl2` | **42.0s wall**, green FIRST TRY. Content build 17.3s of it. Wrote `character_baseline_owl2_{map,dossier,fight_idle,fight_cast}.png` |
| 01:49:10 | lookup | viewed `..._map.png` | **CORRECT.** The owl walker on the real Descent map at the START node, at its own aspect ratio, not Shawn |
| 01:49:20 | lookup | viewed `..._fight_cast.png` | **CORRECT.** "Baseline Odette", LV1 SUPPORT, owl art on the stage against the three-mob line-up, MP 45/50 -- the 5 the bolt costs, spent -- and a `-16` on the Giant Rat |
| 01:49:28 | lookup | viewed `..._dossier.png` | **PARTLY CORRECT, and this is the finding.** The dossier renders her name, level, all six attributes and every derived stat. The portrait plate is EMPTY -- see below |
| | | **E3 time to first correct preview** | **42s**, one command, first try, against the baseline's **5m24s** |

### Code edits

**Zero.** The baseline needed two: `SaveDataSquadOfThreeTests` (its `CollectionAssert`
arrays named the three real ids, so inserting a character failed an unrelated test) and
`PartyFormationCaptureTests` (keyed on the DISPLAY NAME `"Odette"`, which is the field an
author is most likely to change). Both were dealt with in Step 2: the first reads content,
the second was not touched by this exercise at all because `-Character` does not need it
-- the baseline's ~2 minutes lost to picking the wrong capture class by name is simply
gone, since the author names the character rather than the fixture.

### The finding: a new character has no dossier portrait

`portraitPath` is, in `RawCharacterEntry`'s own words, "baked into the scene at build
time". `SceneBuilder` bakes it; the dossier does not load it at runtime. So a character
authored after the last scene build has a correct name, correct attributes and an empty
portrait plate, and **no amount of content rebuilding fixes it** -- it needs
`run_tests_parallel.ps1 -BuildScenes`, which is a ~1 minute step this loop deliberately
does not take.

Not a defect in `-Character`: the capture is showing the truth, which is that the
portrait is not there yet. It IS a gap in the claim "a character can be seen without a
code edit or a manual prerequisite" -- there is a manual prerequisite, and it is a scene
build. Worth recording rather than smoothing over, because the picture looks like a
missing portrait asset and the cause is a stale scene.

---

## Side by side

| | Step 0 baseline (2026-09-05) | Step 2 (2026-09-06) |
|---|---|---|
| E2 spell -- time to first correct preview | **4m31s** | **43s** (21s with `-NoBuild`) |
| E2 -- code edits demanded by the row | 1 (`SpellCastCaptureTests` literal list) | **0** |
| E2 -- content rebuild cycles | 2 (78.5s each) | 1 (17.5s) |
| E2 -- unrelated failures to diagnose | 1 (the AUDIT #61 `SpellVfxTests` flake, +29.6s rerun and a lookup to learn it was not mine) | 0 |
| E3 character -- time to first correct preview | **5m24s** | **42s** |
| E3 -- code edits demanded by the row | 2 (`SaveDataSquadOfThreeTests`, `PartyFormationCaptureTests`) | **0** |
| E3 -- time lost picking a capture class | ~2 min (three candidates by name, only one fields a real squad) | 0 |
| E3 -- roster-position constraint | had to displace a top-three character | none |
| Commands an author has to assemble | batchmode argv, hand-built from `screenshot.ps1` | `tools/preview.ps1 -Spell <id>` / `-Character <id>` |
| Content step, warm | 75-97s (in `-TestRunner2`) | 17.3-17.5s (in main, `build_content.ps1`) |

The content step's collapse from ~76s to ~17s is Step 1's, not Step 2's -- it is here
because the exercise time is dominated by it and the comparison would be unreadable
without saying so.

## Cleanup (verified 2026-09-06T01:50)

- `ContentData/skills.json` and `ContentData/characters.json` restored from the copies
  taken before the exercise; `git diff` on both is empty.
- Content rebuilt from the restored files, so `Resources/Content` holds no `baseline_*`
  asset and `content_stamp.json` matches the committed inputs.
- `tools/screenshots/` is gitignored, so the exercise's PNGs are not in the tree either
  way; they are left in place as the evidence for the readings above.
- `git status` carries only the paths that were already there at session start, plus this
  session's own commits.

---

## The E3 caveat, closed (2026-09-06T03:11-03:14)

The finding above -- "a new character has no dossier portrait" -- is fixed and the
exercise was repeated to see it. `portraitPath` is Resources-relative now and the
dossier loads it through `CharacterPortraits.For(characterId)` at the moment it
draws; `ScreenRegistry` bakes no `IconEntry[]` of faces into the scene any more.

Same inputs as before: `baseline_owl2` on the owl's spread, one skill row,
appended rather than inserted. The one authored difference is that
`portraitPath` reads `"Portraits/sheep"` instead of an `Assets/` path, which is
the change.

| ISO timestamp | cat | command / file | what happened |
|---|---|---|---|
| 2026-09-06T03:11:18+02:00 | edit | `ContentData/characters.json` + `skills.json` | two rows, one each |
| 03:11:24 -> 03:12:47 | wait | `preview.ps1 -Character baseline_owl2` | **83.0s wall**, green first try. Content build 17.0s of it; the extra over the run below is the runner copy importing the relocated 2 MB portrait for the first time |
| 03:13:07 -> 03:13:55 | wait | same command again | **48.0s wall**, warm |
| 03:13:58 | lookup | viewed `character_baseline_owl2_dossier.png` | **CORRECT, and this is the point.** Shawn's painted face on the plate above "Baseline Odette", Level 1, WIS 20 / INT 16 -- **against scenes last built before this character existed**. No `-BuildScenes`, no scene in the process at all on the content side |

### Side by side, E3 only

| | Step 0 baseline (2026-09-05) | Step 2 (2026-09-06 01:48) | Step 2 + runtime portraits (2026-09-06 03:11) |
|---|---|---|---|
| time to first correct preview | 5m24s | 42s | **48s warm** (83s on the first run after the art moved) |
| code edits demanded by the row | 2 | 0 | **0** |
| dossier portrait | n/a | **empty plate** | **the portrait** |
| manual prerequisite | roster position | a scene build for the portrait | **none** |

The 48s is not an improvement on 42s and was not meant to be: this closed a
correctness gap, not a timing one. What changed is the last row -- Step 2's claim
that "a character can be seen without a code edit or a manual prerequisite" was
true except for the portrait, and it is now true without the exception.

### What is NOT closed

The portrait plate degrades to the armour stand when a character authors no
`portraitPath`, unchanged and deliberate. And the three characters in content all
name the same file: `placeholder_brawler` and `owl` wear Shawn's face because
theirs have not been drawn. That is a content gap and it looks exactly like a bug
in this picture, which is worth saying out loud beside a screenshot of Odette
wearing a sheep's head.

### Cleanup (verified 2026-09-06T03:14)

- Both JSON files restored from the copies taken before the exercise; `git diff`
  on `skills.json` is empty and on `characters.json` shows only this session's
  own `portraitPath` change.
- Content rebuilt from the restored files: no `baseline_*` asset under
  `Resources/Content`, and `content_stamp.json` matches the committed inputs.
