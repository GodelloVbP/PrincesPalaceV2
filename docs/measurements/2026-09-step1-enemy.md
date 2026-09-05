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
