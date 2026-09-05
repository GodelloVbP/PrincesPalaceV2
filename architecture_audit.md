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
class exists that no area pattern matches, rather than warning: a warning in a
120-second run is a warning nobody reads.

## 7. Coupling: where the gravity is

Most-referenced declared types, counted as the number of *other* production
files that name them:

| Refs | Type | Home |
|---:|---|---|
| 49 | `FightController` | `Core/` (5 partial files, 2,216 lines) |
| 45 | `ContentDatabase` | `Core/Content/` (3 partial files, 1,532 lines) |
| 37 | `Character` | `Data/` |
| 37 | `SceneBuilder` | `Editor/SceneBuilder/` |
| 36 | `AbilityScoreBlock`, `UiVec` | `Domain/Stats`, `Domain/UiKit` |
| 35 | `UiStrings` | `Domain/UiKit` |
| 31 | `StatBlock`, `CombatantState` | `Domain/Stats`, `Domain/Combat` |

`FightController` at 49 looks like a god object and is not one. Its own header
states what is *not* in it — no damage funnel, no enemy AI, no turn riders, no
skill dispatch, no talent rules, no reward assembly — all of which resolved into
Domain's `FightSession` (4,010 lines across 20 files). The controller holds
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

### F1 — HIGH. **Fixed.** The stale-scene guard checked four scenes; there are five.

`tools/run_tests_parallel.ps1:229` hardcodes
`MainMenu.unity`, `Hub.unity`, `Fight.unity`, `Map.unity`. **`Talents.unity` is
missing** — 62,040 lines, a full `TalentController` wiring, generated by the same
`BuildAllScenes` pass as the other four.

What makes this the top finding is what the guard is *for*. Its own comment:
"A sync that silently leaves an old scene behind produces dozens of
NullReferenceExceptions from serialized fields that 'should' be wired, and the
cause looks nothing like the symptom — it is the same trap CLAUDE.md's gotcha #1
is about. Cheap to check, so check." The Talents scene is precisely a scene whose
staleness produces that cascade, and it is the one the guard does not see. The
`robocopy /MIR` that does the actual sync covers the whole directory, so this is
a hole in the *verification*, not in the copy — the run proceeds believing it
checked.

The deeper point is that this is a restated list. `ScreenRegistry` holds five
`*Scene` constants and `SceneBuilder.BuildAllScenes` derives the scene set from
`ScreenRegistry.All.GroupBy(s => s.ScenePath)`. The single source exists. And
`ScreenRegistry`'s own header describes v1's second copy of the screen list as a
documented "nothing keeps these in sync" hazard — the third copy now lives in
PowerShell and has already diverged.

*Fixed:* the guard enumerates `Assets\_Project\Scenes\*.unity` from disk rather
than naming scenes, so a sixth scene is covered the day it exists. A vacuity
guard was added with it — an empty glob now aborts the run instead of passing by
finding nothing to check, matching the shape `UiKitLintTests` uses.

### F2 — HIGH (documentation). **Fixed.** `CLAUDE.md`'s hazard box described v1.

Two errors in the file every session is instructed to read first:

- The box states "all five `*Definition` types carry `[CreateAssetMenu]`, which
  invites authoring into the folder `ContentBuilder` wipes." There are **nine**
  `*Definition` types, and **none of them carries the attribute** — verified
  across all nine files and against `git log -S`, which shows the string entering
  at the initial commit and never changing, consistent with it existing only in
  comments. Three production comments still defend against the hazard
  (`ContentDatabase.Validation.cs:18` and `:271`,
  `CharacterEntryResolver.cs:27`).
- Gotcha #1 says to copy "the two `.unity` files" back after a scene build.
  There are **five**.

Neither misleads toward data loss, and the second is harmless because
`run_tests_parallel.ps1` does the copy. But a stale statement in `CLAUDE.md` is
expensive out of proportion to its size: it is the one file read with the
assumption that it is current, and F1 shows the "how many scenes are there"
question already has a wrong answer somewhere that matters.

*Fixed:* both corrected. The hazard box is kept rather than deleted — it now
records that the attribute is absent and why the box survives, since production
comments still defend against it and a reader meeting those needs to know what
they guard. Gotcha #1 says "every `.unity` file" and points at
`ScreenRegistry.All`'s distinct `ScenePath`s rather than writing the count down
again.

### F3 — MEDIUM. **Fixed.** `ContentBuilder`'s talent-grid comment contradicted the resolver.

[ContentBuilder.cs:27-30](Assets/_Project/Scripts/Editor/ContentBuilder.cs:27):
"the authorable limits (column 0-11, row 0-5, matching what SceneBuilder can
actually lay out) are enforced by `TalentEntryResolver`."

`TalentEntryResolver` enforces **column 0-2** (`TalentPage.PathCount - 1`) and
**row 0-20** (`TalentSkeleton.SlotCount - 1`). The comment is wrong in both
bounds and has them transposed relative to the real shape — it describes a wide
shallow grid where the tree is narrow and deep. It sits in the generator, which
is where someone adding content looks first.

*Fixed:* the numbers are replaced by a reference to the two constants that own
them, phrased as a prohibition against writing them out again and citing what the
previous wording claimed. §12's rule applied to the exact class of comment that
produced the finding.

### F4 — MEDIUM. **Fixed.** `ContentDatabase.TalentColumns` / `TalentRows` had no callers.

[ContentDatabase.cs:170-178](Assets/_Project/Scripts/Core/Content/ContentDatabase.cs:170).
Both derive grid dimensions from authored talents (`_talents.Max(t => t.column) + 1`).
A project-wide grep finds **zero production call sites** — the only references
are two comments, one of which is F3.

Worse than unused: [ContentDatabase.cs:411](Assets/_Project/Scripts/Core/Content/ContentDatabase.cs:411)
states "`TalentController` binds buttons by `row * TalentColumns + column`", and
that is the stated justification for why the `OrbCost` lookup is legitimate
rather than coincidental. `TalentController` does no such thing — it binds via
`TalentScreen.OrbIndex(path, slot)` and `tree.Set(talent.column, talent.row, …)`.

This is exactly the `BagView.CellCount` shape `b407dbd` buried: a live-looking
accessor that reads as the authority on the tree's dimensions while the real
authority is `TalentPage.PathCount` / `TalentSkeleton.SlotCount` in Domain. A
future session sizing something off `TalentColumns` would get a number derived
from content that nothing else in the game consults.

*Partly fixed:* the `:411` comment now names the binding that exists —
`tree.Set(talent.column, talent.row, …)`, bounded by `TalentPage.PathCount` and
`TalentSkeleton.SlotCount`.

Both properties are then deleted, with a note in their place saying why
"derived from content" reads like the better design and is not: the skeleton is
a fixed graph that content is authored *against*, so a `max()` over what happens
to be authored describes the content, not the tree. Content filling fewer rows
than the skeleton has would have made this pair quietly disagree with every
other reader. Recorded rather than removed silently for that reason — the idea
is attractive enough to be re-added by someone acting in good faith.

### F5 — MEDIUM. **Fixed.** A placement decision justified by a false premise.

[ContentDatabase.cs:414-416](Assets/_Project/Scripts/Core/Content/ContentDatabase.cs:414):
"Lives here in Core rather than in the Domain resolver because `TalentSkeleton`
is a Core type and Domain cannot see it."

`TalentSkeleton` is at
`Assets/_Project/Scripts/Domain/Talents/TalentSkeleton.cs`. It is a Domain type,
and `TalentEntryResolver` — in Domain — already reads
`Talents.TalentSkeleton.SlotCount`. The stated reason for `OrbCost` living in
Core does not hold.

Whether it *should* move is a separate question with a real answer either way:
`OrbCost` takes a `TalentDefinition`, which is a Core ScriptableObject, so it may
well belong in Core regardless. But the recorded reason is false, and a false
reason is worse than no reason — the next session reads it, believes the
constraint, and shapes its own work around a wall that is not there.

*Fixed:* the comment now gives the real reason — `OrbCost` takes a
`TalentDefinition`, a Core ScriptableObject Domain cannot see — and states in
one clause that `TalentSkeleton` is Domain, so the next reader is not sent at the
same wall. The function stays where it is; the real constraint holds.

### F6 — MEDIUM. **Fixed, and one of the nine was already doing it.**

Twenty `Assert.Ignore` calls; eleven guard the headless environment and are
correct. The other nine are conditional on content or run state:

```
PlayMode/DossierXpBarTests.cs:86        "the dossier's root node was renamed; update this test"
PlayMode/EquipmentReachesCombatTests.cs:187  content has fewer than two spell tiers
PlayMode/FightPlayableTests.cs:172      this character's attack is a single flat frame
PlayMode/GlossaryTests.cs:167           no locked relic on the first page
PlayMode/MapFlowTests.cs:228 and :273   this leg offers no fight / only fights
PlayMode/RelicsReachCombatTests.cs:106  content has only one character
PlayMode/RelicsReachCombatTests.cs:129  no relic carries a numeric modifier yet
PlayMode/RelicsReachCombatTests.cs:135  that relic does not touch attack
```

Each is honest about its condition, and the harness prints skips. But the failure
mode is that a content change turns coverage off *without turning anything red*.

**Correction to this finding as first written.** It said `MapFlowTests`' two were
"keyed on a randomly generated leg, so the same commit can cover or not cover the
path from run to run." That is wrong: `OpenTheMap` calls
`RunManager.StartRun(4242)`, so the leg is identical every run. Those two skips
were *deterministic* — they either always fired or never did, and a green suite
gave no way to tell which. Worth correcting rather than quietly restating,
because the wrong version made them sound like flakes to be tolerated when they
were in fact permanent, silent holes.

`DossierXpBarTests.cs:86` is the sharpest: a test that ignores itself when the
node it inspects is renamed is a test that cannot fail for the reason it exists.
`RelicsReachCombatTests.cs:129`/`:135` is the most costly: it takes whichever
relic sorts first with *any* modifier and then skips if that one happens not to
touch attack — so whether it ran depended on content ordering, and its own
comment says it exists because "RelicModifiers.Apply had ZERO callers when it was
written". A silent skip is precisely how that returns.

*Fixed:* all nine now assert. Eight passed on the first run, which is the
expected result — they were guarding against fixtures that have not been thin for
a long time. **The ninth failed, and it had never run.**

`GlossaryTests.ALockedRelicShowsItsNameButWithholdsItsBody` scanned page one for
a locked relic and ignored itself when it found none. There are thirteen relics,
`GlossaryCatalog.RowsPerPage` is ten, and exactly one relic is gated today —
`forest_wardens_tooth`, behind `first_forest_boss` — sitting at index **10**. It
was the first row of page *two*, one turn past everything the test looked at, so
its three real assertions (a locked entry keeps its name, shows "NOT YET FOUND"
instead of its body, and explains itself with "Unlocked by:") had never executed
once. It now pages through the whole category, with the page count derived from
`ContentDatabase.Relics.Count` rather than bounded by a literal, since one relic
past a page boundary is exactly how it went blind.

Two things worth keeping from how that surfaced. The skip was **not** protecting
against a thin fixture, which is what every one of these looks like — it was
reporting green on the only test covering the locked-entry plate. And it would
have gone on doing so however many locked relics were added, as long as none
landed in the first ten. A conditional skip does not decay gracefully; it decays
invisibly.

`RelicsReachCombatTests` changed shape rather than just changing `Ignore` to
`Assert`: it now selects the relic **by the stat it touches** instead of taking
whichever sorted first with any modifier and skipping if that one missed. Two
relics carry modifiers and only one is an attack relic, so whether that test ran
was decided by content ordering — on a test whose own comment says it exists
because `RelicModifiers.Apply` once had zero callers.

### F7 — LOW/MEDIUM. **Fixed.** A feature flag with no writer.

`RequirementCurve.GearRequirementsEnabled = false`
([RequirementCurve.cs:45](Assets/_Project/Scripts/Domain/Stats/RequirementCurve.cs:45))
is a `public static bool` that nothing in the project ever assigns — not
production, not tests, not the debug menu. Its 15-line header is excellent and
explains exactly why gear requirements are hidden rather than deleted, and why
the flag is scoped to gear rather than done with `Percent = 0`.

The gap is between what the header promises — "flipping this back on restores the
whole system untouched" — and what is testable. Nothing exercises the `true`
branch, so "restores the whole system untouched" is an untested claim about a
code path that has been dark for however long. `RequirementCurve.Percent`, its
sibling, has six tests that set it.

*Fixed:* three tests, not one. Off demands nothing however the item was
authored; on demands exactly what was authored; and on still composes with
`Percent` rather than shadowing it — the last because turning gear back on with
the difficulty knob already moved is the state a settings screen would actually
arrive in, and it is the interaction most likely to be assumed rather than
checked. The `[TearDown]` now restores both knobs instead of the one this file
used to touch.

Still not exposed anywhere: flipping it on remains a source edit. That half is a
design call and stays open.

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

### F10 — **Upgraded to MEDIUM on inspection. Fixed.**

As first written this said "six exemptions describe one missing concept" and
proposed an exclusive-pages container. Both halves needed correcting, in
opposite directions.

**Most of the exemption list was not doing anything.** Of 67 `AllowOverlap` call
sites, **38 sat on nodes that already carried `.AsDecor()`** — and A1 exempts
decoration twice over, returning outright inside a decor subtree and skipping any
pair where either side is decor. Those 38 waived nothing. They still *read* as
the mechanism, which is how this audit's first pass counted them among the
audit's blind spots and reasoned about waivers that did not exist.

**And the list was longer than any search could show.** `Ui.Rim` took a `reason`
parameter, threaded it to all four edges, and spent it on exactly that inert
pair — so five call sites produced **20 more** dead allowances that no grep for
`.AsDecor().AllowOverlap(...)` could find, because the pair only ever existed
inside the helper. They were found by A7 (below) on its first build, not by
reading.

*Fixed:* all 59 deleted — 39 inline, plus the `reason` parameter removed from
`Ui.Rim` entirely, since an argument whose only consumer is a no-op is the defect
rather than the call. Eleven of the reason strings survive as comments, the ones
stating a mechanism the code does not: the hit flash being the sprite's
silhouette redrawn white, the forest tile repeating every 1726 against a
1920 width, the earned XP segment drawn on top of the before segment. The rest
restated a `Ui.Solid` named `...Fill`, marked `AsDecor`, as "the fill is what the
card's contents stand on".

**A7 `InertOverlapAllowance` now makes the class unwritable.** It lives in
`UiAudit.Walk` rather than in the `AllowOverlap` setter because `Decor` is
inherited by a whole subtree: a setter can only see its own node, and the
inherited case is the one it would miss. Three tests pin it, including one
asserting it stays quiet on an ordinary clickable node — without that it is a
rule against `AllowOverlap` itself.

**The first word is built.** `Ui.Exclusive(...)` marks a set of alternatives —
tab pages, wizard phases, menu panes — with a token rather than a name, so two
groups cannot collide by both being called "panes". It marks nodes without
wrapping them, so the emitted hierarchy is unchanged and the controllers that
find these by name are untouched. A1 exempts a pair sharing a group and nothing
else; **A8** checks the half the prose never verified, that at most one member
starts active. "Exactly one is ever active" was written in six screens and
tested in none, and forgetting `.Inactive()` on the second page draws both at
once, which reads as a rendering fault long before anyone suspects the
declaration.

**What narrowing them surfaced is the argument for doing it.** Five of the eight
newly-reported overlaps turned out to be a fact the tree already encodes: a bare
`Ui.Panel` emits no `Image` at all — `UiEmitter` gives one a Graphic only when it
has a colour — so it can neither hide a sibling nor take its clicks, which is
precisely the harm A1's own message describes. A1 now skips a pair when the
**later** sibling emits no graphic. Directional, and that matters: if the
invisible one is first and the drawn one is second, the drawn one really is
sitting on top of whatever the frame contains. This also retires the
"draws nothing and has no graphic to intercept a click" cluster, which was
telling the audit something readable off the tree.

The other three were a genuine layering question nobody had been asked in years:
`ReckoningContinueButton` is a drawn button declared after the pages, so it sits
over whatever they hold at the bottom of the phase. Intended — it is the only way
out of the summary and must be reachable from every tab — so the exemption moved
onto that one button instead of the three full-size pages. Same waiver, a
fraction of the blast radius.

**The second word is built.** `Ui.Layered(...)` says these nodes are one
widget, stacked on purpose. Symmetric, because for the audit's purposes it is —
which layer is on top is already decided by declaration order, and an
`Over`/`Under` pair would be two names for one relationship with one of them
eventually disagreeing with the code. A test asserts a layer stack and a page
set are different groups and still collide, without which the two words would
quietly collapse into one loose one.

**Only two of the eight "attachment" sites needed it.** The initiative badge
(ring, portrait, and the initial printed on it as the no-art fallback) and the
mana bar (fill and cost preview). **The other five were already inert** — their
partner carries `.AsDecor()`, and A1 skips any pair where either side is
decoration, so those allowances could never fire. The same class A7 catches, one
step removed: A7 sees an allowance ON a decor node; these were allowances on a
node whose PARTNER is decor, which no rule detects and only reading finds.

That ratio is the finding's own lesson turned on itself. This register counted
eight sites needing a new API and there were two. **Count what a waiver actually
waives before designing around it** — the same mistake, in the same finding, that
the 38-inert discovery had already made once.

**Production `AllowOverlap` is 21 sites, 20 of them live, down from 67 with 29
live** when this audit opened. What remains is genuinely per-node: a modal
covering its siblings by definition, depth-stacked actors on a receding floor,
constellation glows reaching their neighbours, the Continue button that swaps
footprints with the verb column.

**One site deliberately not migrated.** `FightScreen`'s Continue button swaps
footprints with the verb column, and `BuildVerbColumn` returns several nodes that
are alternatives to Continue but *not* to each other. A flat mutual-alternatives
set cannot say that, and stretching the word to fit would make it mean "these
sometimes do not coexist", which is loose enough to be worth nothing.

*The general lesson, which is why this was upgraded:* an exemption list reads as
a design backlog, and two thirds of this one was fiction. Count what a rule
actually waives before reasoning about it, and prefer a rule that runs over a
sweep that reads — the sweep only ever sees the shapes you thought of.

### F11 — LOW. **Fixed by another session**, in `64a3dc2`.

The comment read "the built scene always has SlotCount-derived `EdgesPerPath * 3`
edges total" — that `3` being `TalentPage.PathCount`, one file away. `64a3dc2`
("Describe the talent tree once, and let its four tables follow") rewrote
`TalentSkeleton` to derive `Depth`, `DxSlot`, `Kind` and `Parents` from a row
description in a static constructor, and the restatement went with it.

Worth recording as a data point rather than just a strike: two sessions found the
same class of defect in the same file on the same day, independently, from
different directions. That is what a defect *class* looks like when it is real
rather than an artefact of one reader's taste — and it is the argument for F8's
mechanisation being worth more than its false-positive rate suggests.

### F12 — LOW. **Fixed**, by a different mechanism than the one recommended.

`SaveSystem.RootOverride`, `Navigation.LoadOverride`/`QuitOverride`,
`FightBeatPlayer.BeatSpeedMultiplier`, `RequirementCurve.Percent`,
`ContentDatabase`'s nine caches, `RunManager`'s three fields and
`SaveSlotManager`'s three all persist for the life of the test process. Each test
file restores what it touched — `Navigation.Reset()` and `RunManager.ResetForTests()`
exist and are used, `RequirementCurveTests` resets `Percent` in `[SetUp]` — but
the discipline is per-file and unenforced. A new PlayMode test that sets
`BeatSpeedMultiplier = 60f` and forgets the teardown makes every later test in
that process run at 60×.

*Fixed, and the recommendation above was wrong in its second half.*
`TestGlobals.ResetAll()` exists and calls every seam. **The shared `[SetUp]` does
not**, and must not: an assembly-level NUnit action's ordering against a
fixture's own `[SetUp]` decides whether the reset lands before or *after* the
fixture prepares itself, and most of these fixtures set
`SaveSystem.RootOverride` to a throwaway directory there. A reset arriving after
that would null it and point the whole test at the player's real
`persistentDataPath` — a recommendation that would have written to the user's
machine.

The enforcement is a source lint instead — `GlobalStateLintTests`, T3 on the same
ladder — which fails when a test file flips one of six globals and never restores
it. It catches the mistake when it is *written* rather than when it fires, and it
cannot reorder anything. It carries the two guards §12 asks of a lint: a minimum
file count, and a second test asserting every rule in its table still matches
something, so a broken regex cannot retire a global's protection silently.

Three teardowns were converted to call `ResetAll()`, both so the helper has real
callers and so the list of globals lives in one place rather than being restated
per fixture. The lint found **zero** offenders on the day it was written, which is
the argument for adding it then: a rule the code already obeys costs nothing and
never has to be paid off.

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

### F14 — MEDIUM. **Fixed.** `sortOrder` was the one step in the content pipeline with no catch.

Adding a content type is nine or ten touch points: the JSON, a `Raw*Entry`, a
`*EntryResolver`, a `Resolved*` record, a `*Definition`, a `ContentBuilder`
folder and writer, a `ContentDatabase` list and getter, a `ValidateContent` arm,
and tests. Eight of them fail loudly when missed. One does not.

`Resources.LoadAll` returns filename-alphabetical order, not authoring order, so
every content type carries an explicit `sortOrder` sorted for in
`ContentDatabase`. Nothing enforces that a new type has one. The failure is the
worst available shape: content loads, nothing throws, and the order is *plausible
but wrong* -- talents in a tree, spell tiers by level, items in a shop. It is
gotcha #4 in `CLAUDE.md`, which is to say it is currently enforced by whoever
remembers reading that file.

*Fixed:* `IOrderedContent { int SortOrder { get; } }`, with
`ContentDatabase.LoadOrdered<T>` constrained on it, so a content type that has
not said how it is ordered does not compile. `ContentLoadingLintTests` keeps
`Resources.LoadAll` from being called anywhere else, the T2/T3 pair that makes
the constraint worth having — the same arrangement as `UiEmitter` owning
`new GameObject`.

**A PROPERTY, NOT A REQUIRED FIELD, and this finding was wrong about that.** It
said "every content type needs a `sortOrder`", repeating gotcha #4. That was
never literally true: seven types order by an authored `sortOrder`, but a spell
tier orders by `level` and a talent by its position in the grid. Forcing all
nine to an authored int would have given two of them a second key that could
only disagree with the one they are really sorted by. Nine inline `.OrderBy(...)`
chains in `EnsureLoaded` are what hid it — written out nine times, the two
exceptions read as ordinary lines; collected into one helper, they have to be
declared on the types themselves, where a reader looks for them.

**The real gap was not the one recorded here.** It was that the ordering rule
already existed *twice*. `ContentDatabase.Initialize` — the test-injection path
that bypasses `Resources` — held its own complete copy: eight `OrderBy` chains
including the spell-tier special case, with nothing tying them to the runtime
path. A test's fixture could have been ordered differently from the game's
content and nothing would have said so, which makes it a test of something else.
Found by the compiler, not by reading, when deleting the old private `Sorted`
helper broke its second caller. `Ordered<T>` is now shared by both paths.

*Left open, deliberately:* `SpellTierDefinition.sortOrder` is written by
`ContentBuilder` and read by nothing — `SpellTierEntryResolver` stamps it as the
JSON authoring index and only then sorts by level, so the two agree exactly as
long as `spells.json` happens to be authored in level order. Deleting a
serialized field means regenerating the whole content tree, which does not
belong in the same change as the loader. Noted at the declaration.

### F15 — LOW. **Fixed.** Controllers maintained a two-array invariant Unity does not require.

`string[] iconIds` alongside `Sprite[] iconSprites`, in `CharacterDossierController`,
`GlossaryController`, `ReckoningController`, `RelicDraftController` and others,
wired at five sites in `ScreenRegistry` as two `.Select().ToArray()` calls over
the same collection. Same length, same order, forever, by hand.

`ItemIcons`' header states the constraint that produced it, and it is true as far
as it goes:

> Two parallel arrays rather than a dictionary, because a scene serialises arrays
> and does not serialise dictionaries.

Dictionary versus array is a real constraint. But the conclusion skipped the
option between them: Unity **does** serialise an array of a `[Serializable]`
struct. One `IconEntry[] { string Id; Sprite Sprite; }` keeps everything the
comment argues for -- it serialises, the registry binds it at build time, and
`UiWiringSweep` still sees it -- while making the pairing structural instead of a
standing invariant across six files.

*Fixed:* `IconEntry` — a `[Serializable]` struct of one id and its sprite.
Five field pairs across four controllers become five single arrays, and the five
wiring sites become one `.Select(... => new IconEntry(...))` each. "Same length,
same order" is a property of the type now rather than a rule five files keep.

**The change nearly cost a check, and that was the real work.** `UiWiringSweep`
(E3) walks array elements flagging null `ObjectReference`s — which is how a
missing icon sprite becomes a build failure. A struct element is not an object
reference, so `icons[i].Sprite == null` would have quietly stopped being checked
while `SceneBuilder.LoadSpriteByKey` goes on returning null on a miss *by
design*. The sweep recurses into serializable struct elements now, so the check
the pair had survived the change that removed the pair, and E3 is stricter
everywhere as a side effect.

**A test was retired rather than migrated.** `RaggedArraysDoNotThrow` held that
a `string[]` longer than its `Sprite[]` returns null instead of throwing — a
state `IconEntry` makes unrepresentable. It is recorded in the class header
rather than deleted quietly, since a vanishing test usually means coverage was
lost and here it means the case was. What replaced it covers what still exists:
an entry carrying an id and no art.

This one had to rebuild and commit the scenes, because the serialized field
shape genuinely changed — 143,062 lines out and 143,071 back, and the scenes now
carry `icons:` where they carried `iconIds:`/`iconSprites:`.

Two stale documents turned up on the way, both the same class as F2 and F3 and
both found incidentally: `SceneBuilder`'s own comment still counted
"iconSprites arrays", and `CODE_STANDARDS.md`'s helper registry still listed
**`PortraitIcons`, a type that does not exist** — portraits have gone through
`ItemIcons` for some time. Both corrected.

### F16 — LOW. **Fixed**, and not the way this finding proposed.

`FightScreenTests`, `HubScreenTests`, `GlossaryScreenTests`, `ReckoningScreenTests`,
`DefeatScreenTests`, `DebugMenuScreenTests`, `ConstellationScreenTests` and
`RelicDraftScreenTests` each walk their own tree asserting that any
`AllowOverlapReason` is longer than 20 characters -- a per-screen copy of one
rule, with per-screen wording of the same message.

Two things follow. A screen added without that block is simply not covered, and
nothing says so; and until F10 was fixed, every copy was policing the prose
quality of fields that mostly waived nothing.

*The recommendation above does not work.* `ScreenRegistry` lives in the
**Editor** assembly and EditMode tests reference **Domain only** -- which is why
each of the eight built its own tree in the first place. A lint over
`ScreenRegistry.All` cannot be written from where these tests live, and one
written in PlayMode would have the same blind spot as the eight copies, in one
file instead of eight.

*Fixed* by moving the rule to where the reason is SET. `UiNode.Require` already
threw on an empty reason; it now also refuses one at or under
`MinimumReasonLength`. That covers every screen that exists and every screen that
will, it fails at the declaration rather than inside a test named after some
other subject, and it needs no registry at all.

20 is kept as the threshold -- not because it measures anything, but because it
is the number eight test classes each settled on independently, and the shortest
reason actually in the tree is 39 characters, so it has room. Two tests replace
the eight: a label like `"by design"` is refused, and a real sentence is
accepted. The second is the one that matters, since a rule that only ever
refuses is indistinguishable from a broken one.

*The general lesson:* "consolidate the duplicated check" was the right
instinct and the wrong altitude. Eight copies of a rule are a signal that the
rule is in the wrong layer, not that it needs a ninth home -- and the assembly
boundary that made the lint impossible is the same one that should have
suggested the API as the place for it.

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
| Test | `Tests/EditMode/NewScreenTests.cs` | `run_tests_parallel` area-coverage refusal |
| Area pattern | `tools/test_areas.ps1` | the same refusal — it aborts the run |

Strong. Six of seven steps have a mechanical catch. Layout correctness is free
(`UiAudit` re-solves at four aspects). **The gap is F1's:** if the screen brings a
new *scene*, the stale-scene guard will not know about it.

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
   of #1: `test_areas.ps1`'s patterns, the audits' exemption list, the lint's
   scoping. All three currently have guards (the area-coverage refusal, greppable
   reasons, the vacuity assertions), which is why this is fifth and not first.
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
