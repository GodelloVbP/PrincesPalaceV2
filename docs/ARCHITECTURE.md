# Architecture

Layers, generated artifacts, state, randomness, error posture. File lookup:
`docs/CODE_MAP.md`; coding rules: `docs/CODE_STANDARDS.md`. Paths are relative
to `Assets/_Project/Scripts/` unless they start with `Assets/`, `tools/`, `docs/`.

## Assemblies and dependency direction

```
PrincesPalace.Domain        Domain/            noEngineReferences: true, no references
      ^
PrincesPalace.Core          Scripts/ root      + Domain, UnityEngine.UI, TextMeshPro, URP
      ^                     (Core/, Data/)     MonoBehaviours, ScriptableObjects, save
PrincesPalace.Editor        Editor/            + Core, Domain; Editor-only
                                               scene/content/pipeline generators, importers

PrincesPalace.Domain.Tests  Tests/EditMode/    -> Domain only
PrincesPalace.PlayModeTests Tests/PlayMode/    -> Core + Domain
```

- **Domain cannot see UnityEngine.** Anything that should be testable without a
  scene (rules, layout, models) goes in Domain; a `using UnityEngine` there does
  not compile.
- **The Core asmdef sits at the `Scripts/` root**, so any folder without its own
  asmdef is Core (`Data/` is Core). Never add an asmdef to a folder holding
  partial parts of an existing class.
- **`InternalsVisibleTo` is granted to `PrincesPalace.Editor` only**
  (`Core/AssemblyInfo.cs`). `ScreenRegistry` assigns controllers' `internal`
  fields directly, so a wiring typo is a compile error; PlayMode tests cannot
  reach controller internals and must drive the UI.
- **The Editor assembly is in the global namespace**, uniformly. Keep it so:
  partial parts of a namespace-less class must stay namespace-less.

## Generated artifacts

Outputs, not sources. A hand edit is destroyed by the next build.

| Artifact | Generator | Source of truth | Behaviour |
|---|---|---|---|
| `Assets/_Project/Scenes/*.unity` | `Editor/SceneBuilder/SceneBuilder.cs` (`BuildAllScenes`) | `ScreenRegistry.All` + `Domain/UiKit/Screens/` | Rewrites every scene; every `fileID` changes |
| `Assets/_Project/Resources/Content/**` | `Editor/ContentBuilder.cs` | `Assets/_Project/ContentData/*.json` | Deletes the tree first |
| `docs/CONTENT_SCHEMA.md` | `tools/content_schema.ps1` | `Raw*Entry` types and `[ContentDoc]` | Regenerated whole |
| `Assets/_Project/Rendering/*.asset` | `Editor/PipelineBuilder.cs` | the builder | URP asset, Renderer 2D, volume profile |
| TMP font/material assets | `Editor/SceneBuilder/TmpBootstrap.cs` (+`.Typography`) | `Domain/UiKit/Typography.cs` | Per role |
| Sliced/keyed art | `tools/*.py` + `Editor/*ImportPostprocessor.cs` | source art under `Assets/_Project/Art/` | Per file; recipes replay |
| `tools/screenshots/**` | `Editor/SceneBuilder/ScreenshotTool.cs` via `tools/screenshot.ps1` | built scenes | Per file |

Batchmode entry: `Editor/GenerationRun.cs` (each builder logs a `BUILD-COMPLETE` sentinel).

The content chain:

```
ContentData/*.json -> Domain/Content/*EntryResolver -> Resolved* records
  (hand-authored)     (validates, reports every error    (pure, testable)
                       in one pass)
                              |
                              v
                     Editor/ContentBuilder -> Resources/Content/*.asset
                     (wipes, then writes     (*Definition ScriptableObjects,
                      via internal SetData)    Data getter over a Resolved*)
                              |
                              v
                     Core/Content/ContentDatabase -> the game
                     (static lazy cache; LoadOrdered<T> sorts by IOrderedContent)
```

`Resources.LoadAll` returns filename order, so every content type implements
`IOrderedContent`; `ContentDatabase.LoadOrdered` is the only `LoadAll` caller
(lint: `ContentLoadingLintTests`). Audio config is the exception to the chain:
`Core/SoundController.cs` self-bootstraps before any scene and reads its tables
(`Core/AudioLevels.cs`, `Domain/Audio/MusicLayerResolver.cs`) through its own
Resources loaders.

**Adding a content type:** JSON file → `Raw*Entry` (with `[ContentDoc]`) →
`*EntryResolver` (collect all errors) → `Resolved*` → `*Definition` implementing
`IOrderedContent` with `internal SetData` → `ContentBuilder` writer and folder →
`ContentDatabase` list via `LoadOrdered` → `ContentDatabase.Validation`
cross-checks (pure rules in `Domain/Content/CatalogueCrossChecks.cs`) → tests →
`tools/content_schema.ps1`.

## Where state lives

No DI container, no `GameObject.Find` graph. Global state is static fields of
four kinds:

| Kind | Examples | Rule |
|---|---|---|
| Caches | `ContentDatabase`'s lists, `StanceManifestLoader`, `RunManager`'s map | Rebuildable, keyed, each with a `Reset()` seam |
| Session | `SaveSlotManager` (active slot), `RunManager` (map seed/start) | Thin accessors over the save, never a second copy |
| Player settings | `Core/GameSettings.cs` (PlayerPrefs) | Player-facing, not test escape hatches |
| Test seams | `SaveSystem.RootOverride`, `Navigation.LoadOverride`/`QuitOverride` | Each test restores what it touched (lint: `GlobalStateLintTests`) |

- **Profile + run live in the save.** `Data/SaveData.cs` (profile, versioned:
  `Migrate()` for breaking changes by version range, `Reconcile()` for additive
  ones) holds the active run as `Data/RunSnapshot.cs`. `Core/SaveSystem.cs` is
  the only save path; `Core/SaveSlotManager.cs` owns slots.
- **A run is not a MonoBehaviour.** `Core/RunManager.cs` is static; the run
  outlives every scene. The map is regenerated from the run seed
  (`Domain/Dungeon/DescentMap.cs`), never serialised.
- **A fight is a session object.** `Domain/Combat/Session/FightSession` owns
  one fight; `Core/FightController` holds references, paints session queries,
  forwards input. Settlement into the run goes through
  `Core/Bot/RunOrchestrator` (`SettleFight`), the same code the bot calls.
- **Singletons:** `Core/SoundController.cs` (`DontDestroyOnLoad`); boot-time
  statics use `[RuntimeInitializeOnLoadMethod]` (`GameSettings`, `CursorController`, …).

## Determinism and RNG

- Gameplay randomness is `Domain/Rng/SeededRandom.cs` (SplitMix64), opened per
  purpose through `Domain/Rng/RngStreams.cs`: `RngStreams.Open(runSeed, stream,
  keys...)`. Streams are numbered constants; a new stream takes a new number
  and existing numbers never change (that would change every seeded run).
- Domain cannot see `UnityEngine.Random`; Domain code that randomises takes the
  randomness injected as a `Func`.
- `UnityEngine.Random` in Core is allowed only where the result is not part of
  the simulation: cosmetic motion (`StarTwinkle`, `SlowDrift`, `EmberFlare`,
  `StageShake`), voice take choice (`CharacterVoice`), and the post-fight offer
  roll in `FightController.Input` (must not shift a replay's beats). Apply that
  test to any new use.

## Error posture

| Layer | Posture |
|---|---|
| Domain | Throws (`ArgumentException`/`InvalidOperationException`) on programmer error. |
| Core | Degrades on missing content/art: disable the `Image`, log a warning, keep the scene. |
| Editor builds | Throw loudly with the fix in the message (e.g. `BuildAllScenes` refuses without TMP essentials). |
| Content resolvers | Collect every error in a file, refuse the build naming type, id and field. |

No layer returns a plausible wrong answer (a zero, an empty list, a default
struct) that a caller cannot tell from a real one.

## Where the architecture is enforced

| Tier | Mechanism |
|---|---|
| Compile | `noEngineReferences`; `internal` fields assigned by `ScreenRegistry`; `Ui.Label` takes `UiString` only; `IOrderedContent` |
| Scene build | `UiAudit` (four aspects), `UiTextFitAudit`, `UiCountAudit`, `UiWiringSweep`, `UiNavControlsAudit`, `UiBindingAudit` |
| Source lint (tests) | `UiKitLintTests`, `ContentLoadingLintTests`, `ContentOwnershipLintTests`, `GlobalStateLintTests`, `ButtonFallbackLintTests`, `ThemedButtonAspectLintTests`, `UiCountAuditCoverageLintTests`, `PreviewEnvironmentLintTests`. A lint that scans files asserts a minimum scanned count, so a moved path cannot make it pass vacuously |
| Harness | `tools/run_tests_parallel.ps1`: area-folder refusal, stale-scene guard (globs `*.unity`), GUID consistency, `BUILD-COMPLETE` sentinel |
| Commit | `tools/githooks/pre-commit`, `tools/githooks/deny_broad_staging.py` |

## Known structural risks

- **`ContentDatabase` is globally reachable from all of Core.** Nothing but
  convention stops content concepts leaking across Core; keep content reads in
  the controllers and ops classes that need them.
- **Verification maps can drift** (`tools/test_areas.ps1`'s `$PathAreas`, audit
  exemptions, lint scopes). Each has a guard (UNMAPPED refusal, mandatory
  reasons, vacuity minimums); a new map needs one too.
