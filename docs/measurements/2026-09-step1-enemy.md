# Step 1 measurements, 2026-09-05

Same shape as `2026-09-authoring-baseline.md`, which is the "before" this is
compared against. Every timing below is an observed wall clock, not an estimate.

All Unity runs in this file used the isolated copy
`C:\Games\Prince's Palace-v2-TestRunner2` unless a row says otherwise, with the
argument list `run_tests_parallel.ps1` uses for its generation phase:

```
-batchmode -nographics -silent-crashes -projectPath <copy>
-executeMethod GenerationRun.RunAll -logFile <copy>\gen.log -ppSteps content -quit
```

---

## 1a-0: batching the content step's asset imports

`ContentBuilder.BuildDefaultContent` called `AssetDatabase.CreateAsset` once per
asset (~950 calls for 192 authored entries, once the weapon and armour-set
families are expanded) with nothing bracketing them, so the AssetDatabase
imported each asset the moment it was written. The fix is two lines plus a
`try/finally`: `AssetDatabase.StartAssetEditing()` around the `Build*` calls and
`StopAssetEditing()` in the `finally`, leaving the existing `SaveAssets()` /
`Refresh()` to do the single import.

Measured back to back on one tree, nothing else changed between the runs:

| run | wall clock | `[GenerationRun] ContentBuilder` |
|---|---|---|
| before, cold (right after a main -> TestRunner2 mirror) | 62.0s | **52.4s** |
| before, warm (immediately after, nothing changed) | 59.4s | **50.8s** |
| after, cold (right after a main -> TestRunner2 mirror) | 18.2s | **4.2s** |
| after, warm (immediately after, nothing changed) | 13.1s | **4.1s** |

The plan's non-goal ("no incremental content generation unless Step 0 measures
the content step alone above 20s warm") is therefore settled the cheap way: the
warm step is 4.1s and the whole batchmode boot is 13.1s, so no incremental
generation is needed and none was written.

The new `[ContentTiming]` marks say where the remaining 4.1s goes, so the next
person reads it rather than bisecting for it:

```
[ContentTiming] folders: 1.2s (total 1.2s)      <- RecreateFolder + 10 EnsureFolder
[ContentTiming] write assets: 2.9s (total 4.0s) <- every resolver + every CreateAsset
[ContentTiming] import: 0.0s (total 4.1s)       <- SaveAssets + Refresh
[ContentTiming] validate: 0.0s (total 4.1s)     <- ContentDatabase.Reset + ValidateContent
```

Neither of the two costs the brief named as the next suspects turned out to
matter: `RecreateFolder` + `Refresh` is 1.2s and the per-type `Resources.LoadAll`
in `ContentDatabase.ValidateContent` rounds to 0.0s. The entire 46.7s that
disappeared was per-asset import.

**Output unchanged.** After the batched run, TestRunner2's
`Assets/_Project/Resources/Content` holds the same 811 `.asset` files as main's
committed tree and `diff -r -x '*.meta'` between the two is empty. The
`BUILD-COMPLETE: ContentBuilder` sentinel is reachable only past
`ContentDatabase.ValidateContent`, so the batched build also validated clean.

The timing lines are deliberately prefixed `[ContentTiming]`, not
`[ContentBuilder]`: that second string is `run_tests_parallel.ps1`'s
generation-failure grep, and a timing line wearing it would fail every build it
measured.

---

## The end-to-end demonstration

The Step 0 baseline's Exercise 1 (`2026-09-authoring-baseline.md`) repeated with
the same inputs and the Step 1 command. Same mob, same art, same three edited
files -- six treant stills copied to `Resources/Enemies/baseline_treant/`
(without their `.meta`, per the baseline log's own finding), one `enemies.json`
row, one `skills.json` row, one `StanceManifest.json` entry.

Unlike the baseline, every edit and every run below happened in **main**. That is
itself part of the result: the baseline had to be performed in an isolated copy
because the only way to regenerate content was `run_tests_parallel.ps1
-BuildContent`, which mirrors the runner's whole content tree over main's. The
new route builds in place.

| ISO timestamp | what | measured |
|---|---|---|
| 2026-09-06T00:45:25 -> 00:46:06 | `preview.ps1 -Enemy baseline_treant`, **cold** (no Editor on the project) | **41s.** Content built by batchmode (16.1s wall, `ContentBuilder` 5.2s), main mirrored into the runner copy, `PreviewCaptureTests` run with a graphics device, three PNGs copied back |
| 2026-09-06T00:47:45 -> 00:47:51 | `preview.ps1 -Build`, **warm** (Editor open on main, watcher route) | **6s** |
| 2026-09-06T00:47:57 -> 00:48:23 | `preview.ps1 -Enemy baseline_treant`, **warm** (Editor open on main) | **26s.** The build is 6s of it; the remaining 20s is the runner mirror plus a Unity boot with a graphics device for the capture |
| (TestRunner2, earlier) | `preview.ps1 -Enemy <id> -Launch`, warm | **1s** to the ok result; the Editor is in Play mode against the mob a beat later |

What came out, and it was correct on the first look both times:

```
tools/screenshots/preview/baseline_treant_stances.png
tools/screenshots/preview/baseline_treant_turn1_trunk_slam.png
tools/screenshots/preview/baseline_treant_turn2_attack.png
```

The stage shot reads "Baseline Treant uses Trunk Slam on Shawn for 10 damage!",
with the nameplate, the art and the ground line all right. The turn filenames are
the showcase's own record of what the mob did: its one authored ability, then the
plain swing.

### Beside the baseline

| | baseline, 2026-09-05 | Step 1, 2026-09-06 |
|---|---|---|
| time to first correct preview (mob) | **6m00s** (23:13:10 -> 23:19:10) | **41s** cold, **26s** warm |
| content step, warm | 75.4s wall / `ContentBuilder` 64.8s | 13-16s wall / `ContentBuilder` 4-5s (6s through an open Editor) |
| C# files the author had to edit | **1** (`EnemyStanceCaptureTests.cs`, three substitutions, because the capture named its two enemies literally) | **0** |
| where the work happened | an isolated copy, because the only rebuild route mirrored a whole content tree over main | main, in place |
| what the author had to know | which of three capture classes fields the real squad; that a copied `.meta` must be dropped; that a batchmode arg list had to be hand-assembled from `screenshot.ps1` | the mob's id |
| art coverage for a new mob | the stance sweep already covered it; the canvas and manifest rules covered three hand-listed kits and not the new one | all three cover every enemy |

The two numbers that did not move are worth naming as well as the ones that did.
The **capture** is 20-25s of the 26-41s and almost all of that is one Unity boot
with a graphics device against the runner copy -- the pictures, not the content,
are now the cost. And a mob with **no** art still needs art; nothing here draws
anything.

### What this measurement does not cover

`-Launch` was timed on the TestRunner2 copy, not on main. Driving main's Editor
into Play mode unattended risks a modal save-changes prompt with nobody at the
keyboard, and the route is the same code either way.

## The freshness rule's price, stated once

`ContentInputHash` covers the whole of `Scripts/Domain/`, deliberately (see its
own header). The consequence, met four times while building this and worth
knowing before it is met a fifth: **any edit under `Scripts/Domain/` marks the
content stale**, so the fast `dotnet test` loop goes red until `build_content.ps1`
has run, and the regenerated `content_stamp.json` has to be committed with the
change. That is 14s and one extra staged file per Domain edit.

It is the correct trade as the plan drew it -- a hash that misses an input
reports fresh over a tree built by a different builder, which is worse than
noise. But it is a real tax on the loop this plan exists to speed up, and
narrowing the hashed set (to `Domain/Content/` plus the enums a record actually
references) is the obvious lever if it starts to bite. That would be a decision
to take deliberately, not a bug to fix quietly.
