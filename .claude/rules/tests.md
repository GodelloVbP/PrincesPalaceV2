---
paths:
  - "Assets/_Project/Scripts/Tests/**"
---

# Test rules

Commands and gates: `docs/TESTING.md`.

## Writing tests

- Pin formulas with literal expected values (`CLAUDE.md` gotcha 5).
- EditMode is Domain only. Needing `ContentDatabase`, `SaveData`
  or a scene makes it PlayMode.
- A test's area is its folder: `Tests/<Platform>/<Area>/`, areas `Combat
  Hub Content Run Ui Art Rng`, one level deep; helpers without `[Test]` go
  in `Shared/`. A misplaced file refuses the whole run.
- No duplicate, generic or nested fixture class names: no filter can select them.
- `Start()` runs one frame after `SetActive(true)`: `yield return null`
  twice after activating a panel before clicking it.
- `Assert.Ignore` only for a real environment limit (no graphics device),
  never keyed to content shape or run state; assert the precondition instead.
- A lint or audit carries a vacuity guard (`MinimumFilesExpected`, a
  minimum count) so scanning nothing fails.
- Tests reference production methods by name; a rename has test impact.

## Shared scenes in PlayMode

- A fixture loads its scene once per fixture via `SharedScene.Ensure` /
  `EnsureFight`. A test that leaves state its fixture cannot undo calls
  `SharedScene.MarkDirty("reason")`. Per-test resets belong to the fixture.
- Run a touched shared fixture under `PP_SHARED_SCENE_RELOAD=1`
  (required when adding one): passing only when shared means it passes on
  another test's leftovers.
- The hooks in `PlayModeTestProfiler.cs` are load-bearing: they call
  `TestGlobals.ResetEngineClock` before every test and fixture.
- Measure durations on the clock production waits on (`Time.time` for
  `WaitForSeconds`, pinned `captureDeltaTime` when frames matter);
  `realtimeSinceStartup` only for hang guards.
- A test without its own save root gets an emptied `TestSaveSandbox`.
- Contracts: the headers of `Tests/PlayMode/Shared/SharedScene.cs`,
  `TestSaveSandbox.cs`, `UnityEventRegistryPrune.cs`, `NavSceneReuse.cs`.

