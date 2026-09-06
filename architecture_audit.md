# Architecture Audit

Written for the implementer, not the author. Every session starts with no memory
of the last one, so the arguments are spelled out rather than assumed.

**Scope.** The whole of `Assets/_Project/Scripts`, audited at commit `b407dbd`
and re-verified at `c39db7e` (2026-08-20): 457 C# files, ~83,000 lines, five
assemblies, plus the generation and verification tooling under `tools/`.

> **A parallel session moved HEAD three commits during this pass.** `23c97e5`,
> `64a3dc2` and `c39db7e` landed between the audit and its write-up, and
> `64a3dc2` independently fixed F11 by rewriting `TalentSkeleton` to derive its
> four tables from a row description. Every finding below was re-checked against
> `c39db7e` before this document was finalised. Anything measured — the method
> counts, the exemption clusters — carries the commit it was measured at. Three things are in here — a map of the
system as it actually is (Part I), the rules for adding to it (Part II), and a
register of what this pass found wrong (Part III), followed by cost-to-extend
tables (Part IV) and a risk register (Part V).

**Method.** Every claim below is verified against the tree — file:line, a
measured count, or a commit. Nothing is inferred from what the documentation
says, because a recurring finding of this audit is that in four places the
documentation and the code disagree, and the code won.

**Standing relative to the other documents.** `CLAUDE.md` holds the two rules
that destroy work when broken. `docs/CODE_STANDARDS.md` holds the conventions —
layering, the helper registry, partial-class rules, and (as of 2026-09-03) the
function/value/state/test/comment rules this document used to restate — and is
the reference for "how do I do X here". `AUDIT.md` is the debt register, native
to this tree from #37 onward (its v1 findings #1–36 are archived at
`docs/AUDIT_V1_ARCHIVE.md`). This document is about the architecture itself and
about code that does not exist yet. Where it overlaps the others it defers to
them and says so.

---

## Verdict

**This is a well-architected codebase, and the audit's findings are subtle
rather than structural.** That has to be said first, because an audit that
manufactures alarm is worth nothing. The layering is real and enforced by
assembly definitions rather than by convention; the generated-artifact pipeline
has a single owner per artifact; determinism is handled properly; the test suite
is large (≈1,970 cases) and, unusually, contains lint tests that enforce
architecture at the source level. Most codebases this size have a god object and
a circular dependency. This one has neither.

**The structural risk is in a place most audits do not look: the comments.**
This project deliberately keeps its design record in code comments and commit
messages, on the stated grounds that chat context does not survive. That
decision is correct and is the reason a fresh session can be productive here at
all. But it has a failure mode with no check on it — *a comment cannot be
compiled, tested, or linted, so when the code moves the record silently stops
being true.* Four of this pass's thirteen findings are exactly that: a stale
comment that still reads as authoritative. One of them is in `CLAUDE.md`, the
one file every session is told to read first, and it describes a hazard from a
codebase that is no longer on disk.

**The second risk is the same class one layer out: the project's safety nets are
themselves restated in places that cannot see the source of truth.**
`ScreenRegistry`'s own header describes v1's hand-maintained second copy of the
screen list as a documented "nothing keeps these in sync" hazard. A third copy
of that list now exists in `tools/run_tests_parallel.ps1`, it has already
diverged, and what it guards is the exact failure `CLAUDE.md`'s gotcha #1 warns
about. That is finding F1 — since fixed, along with the four stale comments and
F4, F6, F7 and F12, in the working tree accompanying this document. Thirteen of the
seventeen findings are closed; F8, F9 and F13 are open on stated grounds, and F11 was
closed by another session mid-audit.

One correction belongs here rather than buried in F10: **this audit's own count
of the UiKit exemption list was wrong by two thirds.** 38 of 67 `AllowOverlap`
calls waived nothing at all -- they sat on nodes already exempt as decoration --
and a further 20 existed only inside `Ui.Rim`, where no search for the pattern
could see them. Reasoning about a safety net means counting what it actually
catches, not what its call sites say.

Neither risk is a design flaw. Both are the ordinary cost of a codebase that
records its reasoning, and both have the same fix, which is the fix Part II §9
is entirely about: **derive it, or assert it — never restate it.**

---

# Part I — The system as it is

## 1. Assemblies, and the direction of dependency

Five `.asmdef` files, and the reference graph is acyclic and shallow:

```
PrincesPalace.Domain          noEngineReferences: true, references: []
        ^                     pure C#, no UnityEngine: 190 files, 31,360 lines
        |
PrincesPalace.Core            + UnityEngine.UI, TextMeshPro, URP
        ^                     MonoBehaviours, ScriptableObjects, save
        |
PrincesPalace.Editor          + Core, Domain, Editor-only
                              SceneBuilder, ContentBuilder, importers

PrincesPalace.Domain.Tests    -> Domain only          (EditMode, 1,584 cases)
PrincesPalace.PlayModeTests    -> Core + Domain        (PlayMode, 384 cases)
```

Three properties of this are load-bearing and worth internalising:

**`noEngineReferences: true` on Domain is the whole architecture in one flag.**
It is not a style preference — it is what makes 1,584 EditMode cases able to run
in seconds without a scene, and it is unbypassable. A `using UnityEngine` in
Domain does not compile. Every "should this be pure?" argument in this codebase
has already been settled by that flag.

**`InternalsVisibleTo` is granted to the Editor assembly only**
(`Core/AssemblyInfo.cs`). This is what lets `ScreenRegistry` assign controller
fields directly, making a wiring typo a compile error. It is deliberately *not*
granted to the PlayMode tests, so a test cannot reach into controller state to
make itself pass — it has to drive the UI like a player.

**The Core asmdef sits at `Scripts/` root**, so anything under `Scripts/` not
covered by a nested asmdef lands in Core. `Data/` is Core. A new top-level
folder is Core unless you give it its own asmdef — and per
`CODE_STANDARDS.md` §4, never add an asmdef to a folder holding partial parts of
an existing class.

Measured distribution:

| Area | Files | Lines |
|---|---:|---:|
| `Core/` (excl. `Content/`) | 82 | 14,596 |
| `Core/Content/` | 14 | 2,265 |
| `Data/` | 4 | 816 |
| `Domain/UiKit/` + `Screens/` | 50 | 11,269 |
| `Domain/Combat/` + `Session/` | 39 | 7,325 |
| `Domain/Content/` | 41 | 5,580 |
| `Editor/` + `SceneBuilder/` | 16 | 4,124 |
| `Tests/EditMode` | 100 | 22,231 |
| `Tests/PlayMode` | 52 | 11,113 |

Largest areas only — Domain's remaining folders (`Stats`, `Talents`, `Rng`,
`Ambience`, `Dungeon`, `Economy`, `Equipment`, `Relics`, `Rewards`, `Audio`,
`Glossary`, `Inventory`, `Stage`, `DebugMenu`) hold the balance of its 190 files.

Test code is 40% of the tree. That ratio is the reason the refactors in the last
twenty commits were safe to make.

## 2. The generated artifacts, and their single owners

Four kinds of file in this repository are outputs, not sources. Editing one by
hand is work that gets destroyed:

| Artifact | Sole owner | Source of truth | Destructive? |
|---|---|---|---|
| `Assets/_Project/Scenes/*.unity` (5) | `SceneBuilder.BuildAllScenes` | `ScreenRegistry.All` + `Domain/UiKit/Screens/` | rewrites all |
| `Assets/_Project/Resources/Content/**` | `ContentBuilder` | `Assets/_Project/ContentData/*.json` (10) | deletes the tree first |
| Sliced sprite sheets, keyed art | `tools/*.py` + import postprocessors | source art under `Art/` | per-file |
| `tools/screenshots/**` | `ScreenshotTool` / `screenshot.ps1` | the built scenes | per-file |

The content chain in full, because it is four hops and no single file shows all
of them:

```
ContentData/*.json  ->  Domain/Content/*EntryResolver  ->  Resolved* records
   (hand-authored)      (validates, collects ALL errors,     (pure, testable)
                         assigns sortOrder)
                                  |
                                  v
                        Editor/ContentBuilder  ->  Resources/Content/*.asset
                        (Editor-only, wipes)        (ScriptableObjects)
                                  |
                                  v
                        Core/Content/ContentDatabase  ->  the game
                        (static cache, Resources.LoadAll, sorted by sortOrder)
```

Two facts about this chain that cost time when unknown. `Resources.LoadAll`
returns filename-alphabetical order, not authoring order, which is why every
content type carries an explicit `sortOrder` sorted for in `ContentDatabase` —
a new content type needs that from day one. And the resolvers report *every*
problem in a file in one pass rather than the first
(`SkillEntryResolver.cs:26`), so a hand-edited JSON file gets fixed once rather
than once per typo.

Scene sizes, for calibration on what "a scene diff" means here: Map 113,041
lines, Fight 94,448, Hub 83,804, Talents 62,040, MainMenu 16,767 — 370,100 total.
A full regeneration reassigns every `fileID`, so a no-op rebuild is a
370,000-line diff that means nothing. This is why `run_tests_parallel.ps1`
builds scenes on every run but only syncs them back on `-BuildScenes`.

## 3. Where state lives

There is no DI container and no `GameObject.Find` graph. Global state is held in
static fields, and there are **114 static declarations in production**, of which
roughly 30 are genuinely mutable — the rest are computed properties. They fall
into four kinds, and the kind determines how dangerous each one is:

**Caches (safe — rebuildable, keyed).** `ContentDatabase._characters` and its
eight siblings, `ProceduralSprites`'s five sprites, `StanceManifestLoader._cached`,
`SceneBuilder._uiFont`. Each has a reset seam (`ContentDatabase.Reset()`,
`AudioLevels`' seam, `StanceManifestLoader`'s). `RunManager._map` is the model
of the class: the map is *regenerated from the seed* rather than serialised, and
the cache is keyed by the seed so a different run cannot read a stale one.

**Session state (the interesting one).** `SaveSlotManager._cached` / `_cachedSlot`
/ `_slot`, `RunManager._mapSeed` / `_mapStartStep`. `RunManager`'s header states
the design explicitly and it is a good decision: the run outlives every scene it
passes through, the alternative is a `DontDestroyOnLoad` singleton whose lifetime
nothing can test, and **the state itself lives in the save** — so `RunManager` is
a thin accessor over `SaveSystem` rather than a second copy that could disagree
with disk.

**Tuning knobs that are public and mutable.** `FightBeatPlayer.BeatSpeedMultiplier`,
`RequirementCurve.Percent`, `RequirementCurve.GearRequirementsEnabled`. Each is
documented as "player-facing-setting-shaped, not a test escape hatch". Two of the
three currently have no production writer (see F7).

**Test seams.** `SaveSystem.RootOverride`, `Navigation.LoadOverride` /
`QuitOverride`. `Navigation.Reset()` exists to clear both. There is no global
"reset every static" — each test file restores what it touched (see F12).

The one true singleton is `SoundController._instance` with `DontDestroyOnLoad`,
which is the right shape for an audio source that must survive a scene load.

Save shape: `SaveData` with `CurrentVersion = 3`, a `Migrate()` gated on the
version the save *came from* expressed as ranges (`version < 2`, `version < 3`)
rather than equality, plus a `Reconcile()` for purely additive changes that do
not move the version. The distinction between the two — invalidating data versus
adding a field — is the reason the version field exists, and it is documented at
`SaveData.cs:14-33`. This is better save handling than most shipped games have.

## 4. Determinism

Gameplay randomness runs through `SeededRandom` (SplitMix64) in
`Domain/Rng/`, so a run reproduces from its seed. Domain cannot see
`UnityEngine.Random` at all — the assembly flag makes that structural rather
than a rule — so anything Domain randomises takes the randomness *injected as a
`Func`* (`ItemOffer`, `RarityTable`, `ItemOfferRoll.Roll`).

`UnityEngine.Random` survives in exactly three production places, and all three
are defensible: `CharacterVoice` picking a take, `StarTwinkle` picking a period,
and `FightController.Input.RollOffers` — the last carrying an explicit argument
for why (`FightController.Input.cs:281`): the loot offer is not part of the
fight's simulation and must not shift the beats a replay would produce. That is
the correct test to apply to any new use.

## 5. Error posture — two postures, only one of them written down

Measured: **66 `throw new`, 31 `Debug.LogError`, 14 `Debug.LogWarning`** in
production. The split is not arbitrary, and the pattern is consistent enough to
be an invariant:

- **Domain throws.** `CombatEncounter`, `TurnOrder`, `SkillResolution`,
  `FightSession` throw `ArgumentException` / `InvalidOperationException` on
  states that are programmer error — an encounter with no combatants, `Advance()`
  before `Start()`, an effect the resolver has no case for. Domain is a pure
  library and a caller that misuses it should find out immediately.
- **Core degrades.** Missing art disables the `Image` (`ItemIcons.Apply` —
  a sprite-less `Image` renders as a solid white quad, so this is a bug class,
  not a nicety). `SceneBuilder.LoadSpriteByKey` returns `null` with a warning
  rather than throwing, because a missing piece of art should cost you that
  piece of art and not the scene.
- **Editor build steps throw loudly**, with the fix in the message —
  `SceneBuilder.BuildAllScenes` refuses to start if TMP's essential resources
  are not imported, because the alternative is a bare `NullReferenceException`
  from inside TextMeshPro.

`CLAUDE.md` records the middle line ("graceful degradation on missing content is
the house style") but not the first or third. All three are the rule. The line
that must not be crossed in any layer is returning a *plausible wrong answer* —
a zero, an empty list, a default struct the caller cannot distinguish from a
real one.

## 6. The verification stack

Five tiers, running at different moments. This is the most unusual part of the
architecture and the part most worth preserving:

| Tier | Mechanism | Catches |
|---|---|---|
| Compile | `noEngineReferences`, `internal` + direct field assignment, `Ui.Label` taking `UiString` with no `string` overload | layer violations, wiring typos, bare UI literals |
| Build (per screen) | `UiAudit.RunAllFrames`, `UiTextFitAudit` (E1), `UiCountAudit` (E4), `UiWiringSweep` (E3) | overlap, overflow, duplicate names, zero-sized graphics, text that does not fit, declared-vs-bound count drift, null wiring |
| Source lint | `UiKitLintTests` (8 rules) | `new GameObject` outside `UiEmitter`, the stringly-typed `SetField` idiom, legacy `UnityEngine.UI.Text`, a screen restating a palette colour |
| Harness | `run_tests_parallel.ps1` — area-coverage refusal, stale-scene guard, GUID consistency assertion, `BUILD-COMPLETE` sentinel | a test class no area matches, a stale scene, a partial build reported as green |
| Commit | `tools/githooks/pre-commit`, `deny_broad_staging.py` | an asset committed without its `.meta`, `git add -A` sweeping a parallel session's work |

Two design notes inside this stack are worth copying into any new check.
`UiKitLintTests` guards against *vacuity* — `MinimumFilesExpected = 40`,
`Assert.Greater(palette.Count, 30)` — because a lint that scans nothing passes
everything, and a path change that silently turns every rule into a no-op still
reports green. And `run_tests_parallel.ps1` refuses to run at all while a test
file sits outside its area folder, rather than warning: a warning in a
120-second run is a warning nobody reads. (That gate used to match class
NAMES against per-area regexes, which cost one commit per new test class;
areas are folders now, so there is nothing left to keep in sync.)

## 7. Coupling: where the gravity is

Most-referenced declared types, counted as the number of *other* production
files that name them:

| Refs | Type | Home |
|---:|---|---|
| 49 | `FightController` | `Core/` (≈4,500 lines across 5 files, as of `b03fa207` — a precise count in prose drifts every time the class is touched, so this is deliberately approximate) |
| 45 | `ContentDatabase` | `Core/Content/` (≈1,900 lines across 3 files, as of `b03fa207` — same reasoning) |
| 37 | `Character` | `Data/` |
| 37 | `SceneBuilder` | `Editor/SceneBuilder/` |
| 36 | `AbilityScoreBlock`, `UiVec` | `Domain/Stats`, `Domain/UiKit` |
| 35 | `UiStrings` | `Domain/UiKit` |
| 31 | `StatBlock`, `CombatantState` | `Domain/Stats`, `Domain/Combat` |

`FightController` at 49 looks like a god object and is not one. Its own header
states what is *not* in it — no damage funnel, no enemy AI, no turn riders, no
skill dispatch, no talent rules, no reward assembly — all of which resolved into
Domain's `FightSession` (≈6,000 lines across 14 files, as of `f93bbee8` — a
precise count in prose drifts every time the class is touched, so this is
deliberately approximate). The controller holds
references, paints them from session queries, and forwards clicks. That is why
v1's 7,073-line ten-partial controller did not need reproducing. The 49 is a
*fan-in of view components*, which is the correct shape for a screen this size.

`ContentDatabase` at 45 is the genuine hub, and it is a static class holding nine
lazily-loaded lists plus derived queries. That is a deliberate trade — no scene
wiring needed to reach content — and the cost is that content is globally
reachable from anywhere in Core, with `EnsureLoaded()` on every property getter
as the price.

---

# Part II — How to write code here (moved to `docs/CODE_STANDARDS.md`)

**Moved 2026-09-03.** This section (functions, values, state/lifetime, test
rules, comment voice) restated `docs/CODE_STANDARDS.md` at greater length
rather than deferring to it, contrary to the "standing relative to the other
documents" note above — confirmed by comparing them side by side: this
section's old Tests and Comments subsections tracked `CODE_STANDARDS.md`'s
test rules and comment voice sections point for point, and its placement
procedure restated its layering section.

The reasoning that added something new — the derived/declared-once/restated
grades for values, the cache-vs-state rules for statics, the T1/T2/T3 tier
ladder for which mechanism kills a class of bug, the narrowest-type and
inputs-struct rules for function signatures — is now in `CODE_STANDARDS.md`
§5–9, in full, not summarised. Read it there. What's unique to *this* document
is unchanged below: Part III's findings, Part IV's cost-to-extend tables, and
Part V's risk register.

---

# Part III — Findings

Verified against `b407dbd`, 2026-08-20. Recorded rather than fixed, per house
practice — several are design calls belonging to the author. Severity is about
what the finding costs when it bites, not how ugly it looks.

### F1 — HIGH. Fixed: the stale-scene guard globs `*.unity` off disk instead of naming four scenes, so a fifth (`Talents.unity`) or a future sixth is covered automatically. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F2 — HIGH (documentation). Fixed: `CLAUDE.md`'s hazard box corrected — nine `*Definition` types, none carrying `[CreateAssetMenu]`, and five `.unity` files, not two. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F3 — MEDIUM. Fixed: `ContentBuilder`'s talent-grid comment now points at the two constants that own the real bounds instead of restating wrong numbers. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F4 — MEDIUM. Fixed: `ContentDatabase.TalentColumns`/`TalentRows` (no callers, and a false justification comment) deleted; the real binding renamed in place. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F5 — MEDIUM. Fixed: `OrbCost`'s placement comment now states the true reason (`TalentDefinition` is a Core ScriptableObject) instead of a false one about `TalentSkeleton`. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F6 — MEDIUM. Fixed: all nine content/run-state-conditional `Assert.Ignore` calls now assert; one (`GlossaryTests`) had never actually run and was blind past page one. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F7 — LOW/MEDIUM. Fixed: `RequirementCurve.GearRequirementsEnabled` gained three tests covering both branches and their interaction with `Percent`. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F8 — LOW/MEDIUM. Colours have a single-home lint; numbers do not.

`5fd09d6` mechanised the palette rule, and its argument applies verbatim to
capacities and sizes, which have no equivalent check. A lint over raw *values*
would be noise: across production `const int`/`const float` declarations the
value `1` backs 29 different names, `10` backs 23, `3` backs 20 — mostly
coincidence rather than a shared concept.

The narrower rule worth considering is over **named capacities**: any constant
whose name ends in `Count`, `Slots`, `Size` or `Capacity`, flagged when two in
different namespaces hold the same value. Note before writing it that
`SaveSystem.SlotCount = 5` and `TalentSkeleton.SlotCount = 21` coexist
legitimately, so the rule keys on a value collision, not a name collision. F1,
F3 and F4 are all instances the rule would not have caught (two are comments, one
is a PowerShell list), which is worth weighing: the numeric restatements this
audit actually found live *outside* C# constants.

*Recommendation:* lower priority than it looks. The higher-value mechanisation is
a check that the scene list in `run_tests_parallel.ps1` matches
`ScreenRegistry`'s — that is F1, and it is one assertion.

### F9 — LOW. `ValidateContent` is the one procedure where §8.1's test fails.

360 lines at
[ContentDatabase.Validation.cs:39](Assets/_Project/Scripts/Core/Content/ContentDatabase.Validation.cs:39),
the longest in the project by half again, growing one arm per content type — a
different reason to change per paragraph, which is the definition of the seam
being in the wrong place. Its local `CheckId` closure is the right instinct
already applied once.

The shape this suggests is a list of per-type validators each returning its own
`List<string>`, which would also let each be tested alone. **Not proposed as
work:** the function is correct, well commented, and the author may reasonably
prefer one readable pass over content to a dispatch table.

### F10 — Upgraded to MEDIUM on inspection. Fixed: of 67 `AllowOverlap` sites, 59 waived nothing (`.AsDecor()` on one side) and were deleted; `Ui.Exclusive`/`Ui.Layered` plus audit checks A7/A8 now cover the two real concepts. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F11 — LOW. Fixed by another session, in `64a3dc2`: `TalentSkeleton` now derives its tables from one row description instead of restating `EdgesPerPath * 3`. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F12 — LOW. Fixed, by a different mechanism than recommended: `GlobalStateLintTests` (a source lint, not a shared `[SetUp]`, which would have reordered fixture setup unsafely) catches a test that flips one of six globals and never restores it. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F13 — INFORMATIONAL. The Editor assembly lives in the global namespace.

Seventeen production files have no `namespace` declaration, and sixteen of them
are all of `Editor/` — `SceneBuilder`, `ScreenRegistry`, `UiEmitter`,
`ContentBuilder`, `PipelineBuilder`, the audits, the importers. The seventeenth
is `Core/AssemblyInfo.cs`, which is correct.

This is uniform, so it is a convention rather than a slip, and
`CODE_STANDARDS.md` §4 already notes that a partial part of a namespace-less
class stays namespace-less. The residual risk is name collision with any package
that also publishes a global-namespace `SceneBuilder` or `PipelineBuilder` —
low, and Editor-only. Recorded so a future session does not "fix" it file by
file, which would break every partial-class pairing.

### F14 — MEDIUM. Fixed: `IOrderedContent` makes a content type that has not said how it is ordered fail to compile; `ContentLoadingLintTests` keeps `Resources.LoadAll` from being called anywhere else. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F15 — LOW. Fixed: the `string[] iconIds` / `Sprite[] iconSprites` parallel-array pairs became one `IconEntry[]` `[Serializable]` struct array each, across five controllers. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F16 — LOW. Fixed, not the way this finding proposed: `UiNode.Require` refuses a too-short `AllowOverlapReason` at declaration, replacing eight per-screen test copies of the same 20-character check. Full write-up in `docs/archive/ARCHITECTURE_AUDIT_FIXED_FINDINGS.md`

### F17 — HIGH. The test suite repaired a state the game left broken.

Found by a player report, not by this audit, and it is the sharpest instance of
the pattern the audit kept circling.

`FightSession.Begin()` opens a fight — grants turn one, lets any monster faster
than the party take its opening swing, telegraphs what comes next. It had **36
call sites and every one was in `Tests/`**. Nothing in the game called it, so a
fight that opened on a monster's turn had nothing to resolve that turn and
deadlocked. Full diagnosis in `AUDIT.md` #46; the architectural point is why it
survived so long.

**The suite could not see it, because the suite was the thing supplying the
missing call.** Every fight test constructed a session and called `Begin()`
itself, so all of them exercised a correctly-opened fight that no player could
reach. `FightAfterTheEliteTests` was written for this exact report, its header
complains that the old tests entered "by a door the player never uses", and it
fixed that for `FightBootstrap` — while still calling `Begin()` by hand. Its
commit ends "WHAT I HAVE NOT DONE IS REPRODUCE IT."

`FightController.Bind` was doing the same repair for the telegraph half, and its
comment said so plainly: *"FightSession.Begin() is where this belongs and it has
NO production caller."* The missing caller was written down, in production code,
next to a workaround for one of its symptoms — and read as a note about scope
rather than as the bug.

**The rule this earns**, and it belongs beside §11's existing test rules:

> **A test may not do for production what production must do for itself.** If a
> fixture calls a method to get the object into a workable state, either the
> game calls it too or the test is describing a state the game cannot reach.
> Setup that constructs inputs is fine; setup that *operates* the subject is a
> claim about the production path, and an unchecked one.

**The lint this finding first proposed does not survive measurement**, and the
numbers are worth keeping so nobody proposes it again. "Production methods
called only from `Tests/`" flags **109** methods — overwhelmingly pure functions
tested directly (`AbilityDerivation.Modifier`, `DifficultyCurve.HealthMultiplier`)
and deliberate seams (`SaveSlotManager.Forget`, 67 test calls). Narrowed to void
INSTANCE methods — operations on a subject rather than functions returning a
value, which is what distinguishes `Begin` — it flags **17**, and most of those
are `HandleEscape`, `Press`, `RestoreDefaults`, `OnPointerEnter`: handlers
production invokes through uGUI wiring rather than a call site. A rule needing a
fourteen-entry allowlist on its first day is the shape `UiKitLintTests` warns
about: narrowed to make it pass, and meaning nothing afterwards.

**What was built instead is a second watchdog**, at the symptom rather than at
the cause. `bffe4c5` added `RescueAStrandedTurn`, which guards `_isBusy` — and
`_isBusy` is only ever set by the player acting, which is precisely what this bug
prevented. It watched the one flag that could not move, which is why it sat
silent through the whole session that reported this.

`RescueAStalledEnemyTurn` guards the state the player actually experiences: not
their turn, nothing playing, fight not over. However that is reached — a missing
`Begin`, an enemy turn resolving into nothing, a future status effect that skips
an actor without advancing — it means the same thing and recovers the same way,
by resolving the enemy turns that are owed rather than nudging the turn back,
since skipping the monsters' round would be a different bug. It logs once, at
error level, naming the state.

And it is pinned by a test that puts a controller into exactly the broken state —
a session bound without `Begin`, on an encounter a monster opens — and watches it
come back, with `LogAssert.Expect` making the error part of the contract. A
rescue nobody has watched work is worth nothing, and this one is expected to find
nothing forever now that the door is fixed.

### Cross-references, not re-counted here

`AUDIT.md` #1–#36 (now archived at `docs/AUDIT_V1_ARCHIVE.md`) were written
against v1 and each needs re-verifying against this tree before it means
anything; #32 was re-confirmed live at `EnemyEntryResolver.cs:173` on
2026-08-17. This pass did not re-verify them.

---

# Part IV — What it costs to extend

The most honest measure of an architecture is how many files a routine change
touches, and whether the compiler or the build catches you when you miss one.

### Add a screen

| Step | File | Missed → caught by |
|---|---|---|
| Declare the tree | `Domain/UiKit/Screens/NewScreen.cs` | — |
| Add strings | `Domain/UiKit/UiStrings.cs` | compile error (`Ui.Label` takes no `string`) |
| Register + wire | `Editor/SceneBuilder/ScreenRegistry.cs` | screen simply absent |
| Controller | `Core/NewController.cs`, `internal` + `[SerializeField]` | compile error on typo; `UiWiringSweep` on null |
| Declare counts | `CountBindings` in the registry entry | `UiCountAudit` (E4) |
| Test | `Tests/EditMode/Ui/NewScreenTests.cs` | `run_tests_parallel` structural refusal if it is not in an area folder |

Strong. All seven steps have a mechanical catch. Layout correctness is free
(`UiAudit` re-solves at four aspects). **F1's gap here is fixed:**
`tools/run_tests_parallel.ps1`'s stale-scene guard globs `*.unity` off disk
rather than naming scenes, so a new scene is covered the day it exists.

### Add a content type

Nine or ten touch points: the JSON, a `Raw*Entry`, a `*EntryResolver` (with
collected errors and a `sortOrder`), a `Resolved*` record, a `*Definition`
ScriptableObject, a `ContentBuilder` folder + writer, a `ContentDatabase` list +
`EnsureLoaded` getter, a `ValidateContent` arm, and tests. Nothing enforces that
the `sortOrder` field exists — miss it and `Resources.LoadAll` returns
alphabetical order that looks plausible. That is gotcha #4 in `CLAUDE.md` and it
is the one step in this chain with no mechanical catch.

### Add a stat, a talent, a colour

Adding a **stat**: `Domain/Stats` + `AbilityDerivation` + the dossier's rows +
`ValidateContent`'s budget check. Pure Domain arithmetic, EditMode-testable,
cheap.

Adding a **talent**: content-only — `talents.json`, bounded by
`TalentEntryResolver` against the skeleton. Cost is priced from tree *position*
rather than authored per node, so a redesign does not need a re-price. This is
the best-factored extension point in the project.

Adding a **colour**: one token in `FightHudPalette`, referenced everywhere else.
The lint catches a restatement inside `Domain/UiKit/`. Outside that folder it
does not — deliberately, and the reasoning is recorded.

### Widen the talent tree — the audit's worked example

Change `TalentSkeleton.SlotCount` or `TalentPage.PathCount` and: the resolver
follows (F-fix, `b407dbd`), `TalentScreen.OrbCount` follows,
`ConstellationLayout`'s width and height follow, `TalentTree`'s array follows.
Then the stale statements: `ContentBuilder`'s comment still says 0-11/0-5 (F3),
`TalentSkeleton.cs:86`'s comment still says ×3 (F11), and
`ContentDatabase.TalentColumns` still offers a second, content-derived answer
nobody consults (F4). **The code moves as one; the record does not.** That is
this audit's thesis in one change.

---

# Part V — Risk register

Ordered by expected cost, not likelihood.

1. **A stale scene tested as fresh (F1).** The guard exists precisely because the
   symptom — dozens of `NullReferenceException`s from fields that "should" be
   wired — looks nothing like the cause. One of five scenes is outside it.
2. **A stale comment believed (F2–F5, F11).** Cheap individually, expensive in
   aggregate, because this codebase's comments are unusually authoritative and
   are therefore trusted. F5 is the worst shape: a false constraint that shapes
   future work around a wall that is not there.
3. **Silent coverage loss (F6). ~~Nine tests can turn themselves off on a
   content change.~~ Closed** — and it was not hypothetical: one of the nine had
   never run. This stays at the top of the register in spirit rather than in
   fact, because the class is not gone, only its current instances. The next
   `Assert.Ignore` written on a content condition restores it, which is why the
   entry is struck rather than deleted.
4. **Static leakage between tests (F12). ~~Held by per-file discipline.~~
   Closed** by a lint rather than a runtime hook, so the failure is now caught
   when the missing teardown is written. `AUDIT.md`'s Bloodlust investigation
   burned four rounds on a suspected variant of this, which is what the cost
   would have been.
5. **Verification drifting out of sync with what it verifies.** The general form
   of #1: `test_areas.ps1`'s `$PathAreas` production-path map, the audits'
   exemption list, the lint's scoping. All three currently have guards (the
   UNMAPPED refusal, greppable reasons, the vacuity assertions), which is why
   this is fifth and not first. The area table itself is no longer on this
   list: areas became folders, and a folder cannot drift.
6. **`ContentDatabase` as a global reach.** Not a live defect. Worth watching: any
   Core file can touch all content from anywhere, so the layer that stops content
   concepts leaking is convention plus `EnsureLoaded`, not the compiler.

---

# Checklists

**Before writing new code**

- Which assembly? Run `CODE_STANDARDS.md` §1's four questions. If an EditMode
  test could not call it, know why.
- Does a helper already exist? `CODE_STANDARDS.md` §2 is the registry; the
  second copy-paste of a pattern is the signal to promote it there.
- What is the acceptance check? Write it before the code — `docs/WORKFLOW.md`
  §2's "Done when" line is the one most often missing and the one that does
  the most work.

**Before writing a number**

- Grep it. If it exists, reference it.
- Can it be derived? Derive it. Can it be measured at runtime? Measure it.
- If it stays fixed: value, reason, and a pin that fails when content outgrows it.
- Never put it in a comment as well as in the code.

**Before committing**

- `run_tests_parallel.ps1` — the full ~120s run; it builds the scenes, which is
  the only way `UiAudit` sees what was actually emitted.
- Add `-BuildScenes` / `-BuildContent` only when you intend to commit those
  artifacts.
- Stage by explicit path. Never `git add -A` — a hook refuses it, and it has
  already destroyed a parallel session's work once.
- Does the commit message explain the *reasoning*, not just the change? The git
  log is this project's real history, deliberately.

**If you do only three things**

1. **Grep the value before you type it**, and derive it from what it depends on
   rather than restating it. `CODE_STANDARDS.md` §6 is one rule wearing three
   coats, and Part III is mostly instances of breaking it.
2. **Draw the function boundary at a reason to change**, then check the name
   survives without an "and". `CODE_STANDARDS.md` §5.
3. **When you fix an instance, spend one sentence on which tier kills the class**
   — a type that refuses it, one path that owns it, or a lint with a vacuity
   guard. The colour tokens were fixed by hand once and grew straight back.
   `CODE_STANDARDS.md` §9.
