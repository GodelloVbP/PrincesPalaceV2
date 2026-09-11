# Code Standards

Conventions specific to this codebase — the ones that aren't obvious from
reading any single file, and that get relearned the hard way if they aren't
written down.

## 1. Layering

- **`Domain/`** — engine-free (`noEngineReferences: true` on its asmdef).
  Pure C#, no `UnityEngine` references, unit-tested in isolation. Content-
  layer concepts (`CharacterRole`, anything from `PrincesPalace.Content`)
  **do not belong here** — that's why role-based Skill effects live in
  `FightController` (Core), not pushed down into Domain.
- **`Core/`** — MonoBehaviours, controllers, `Content/` (the `*Definition`
  ScriptableObject types), `Data/` (save-shaped mutable state).
- **`Editor/`** — `SceneBuilder`, `ContentBuilder`, import postprocessors,
  the screenshot tool. Editor-only, never shipped.
- **`Tests/EditMode/`** — can only reference `Domain`. If a test needs
  `ContentDatabase`, `SaveData`, or a scene, it belongs in PlayMode instead.
- **`Tests/PlayMode/`** — references `Core` + `Domain`.

Five asmdefs total, one per folder above (`PrincesPalace.Domain`,
`PrincesPalace.Core`, `PrincesPalace.Editor`, `PrincesPalace.Domain.Tests`,
`PrincesPalace.PlayModeTests`). Reference direction only ever points from
Editor/Tests toward Core toward Domain — never the reverse.

**Deciding where a new piece of code goes,** in order: (1) does it need a
`UnityEngine` type to do its job — not touch one in passing, *need*? If no,
Domain. If yes: (2) does it need a content type (`ContentDatabase`, any
`*Definition`, `CharacterRole`)? Then Core by construction — Domain can't see
them. (3) Does it run only at build time? Editor, never shipped. (4) Is it a
MonoBehaviour concern (lifetime, clock, input) wrapped around arithmetic?
Split it — arithmetic to Domain, wrapper stays. The one-line test that covers
most cases: **could an EditMode test call this?** If no, and the reason isn't
a genuine engine dependency, it's in the wrong assembly.

## 2. Reuse-first helper registry

Check this before writing a new helper — the second copy-paste of a pattern
is the signal to promote it here instead of a third.

**Runtime (`Core/`):**
- `ItemIcons.Find` / `.Apply(Image, IconEntry[] icons, string id)` — the
  lookup-and-assign idiom for art keyed by content id. Disables the `Image` on
  a miss rather than leaving a stale/null sprite (a sprite-less `Image` renders
  as a solid white quad otherwise), and sets `preserveAspect` in the one place
  a new screen cannot forget it.
- `IconEntry` — one id and its sprite, serialized as a single thing. This was
  two parallel arrays (`string[] iconIds` beside `Sprite[] iconSprites`) across
  five controllers and five wiring sites until 2026-08-20; the reasoning that
  produced them ("a scene serialises arrays and does not serialise
  dictionaries") is true and stopped one option short, because Unity serialises
  an array of a `[Serializable]` struct perfectly well.
  `PortraitIcons` is listed here no longer — it does not exist and had not for
  some time.
- `CharacterPortraits.For(characterId)` — the dossier plate, `Resources.Load`ed
  by id off the character's `portraitPath` and cached, misses included. Not an
  `IconEntry[]` and not `ItemIcons`, and the difference is the point: a baked
  array is a photograph of the roster taken at scene-build time, so a character
  authored afterwards showed an empty plate until somebody ran `-BuildScenes`.
  It also has no `Apply(Image, ...)`, because a miss here must leave the tree's
  armour-stand placeholder standing rather than disable the `Image`.
- `RadialGlowImage`, `BeaconPulse` — the soft-glow-behind-an-icon and
  pulsing-highlight primitives (see the Relics, Talent Tree, and reward
  screens for the pattern: beacon built as a sibling *before* the element it
  glows behind, since uGUI draws later siblings on top).
- `SolidCircleImage` — a flat-color circle from a plain `Image`.
- **Ambient motion** — four curves, each a pure `static` so its shape is
  testable without a scene or real time (the seam `SplashController.AlphaAt`
  established). Pick by what the thing *is*, since they are deliberately not
  interchangeable: `BeaconPulse` (one clean cosine — a signal, e.g. an open
  map room; per-instance period/phase so a *field* of them doesn't move in
  lockstep), `LanternFlicker` (three non-harmonic sines — a flame, which must
  not read as periodic), `StarTwinkle` (alpha only, randomised period *and*
  phase), `SlowDrift` (a slow ellipse around the authored position — mist,
  haze, and the pan half of `KenBurnsDrift`), `MoteDrift` (rise + sway +
  fade, looping, staggered by an authored `StartProgress` so a fixed pool
  needs no spawner). `KenBurnsDrift` composes the last one with a scale that
  is guaranteed never to drop below 1 — under a full-bleed background that
  would show bare camera colour down the sides.
**Screen authoring (`Domain/UiKit/`, engine-free):**
- `Ui.Panel/Column/Row/Grid/Label/Button/Sprite/Solid/Space/Modal/Each/Pool` —
  the whole vocabulary. Note what a flow child has no parameter for: a
  position. Spacing and padding are **container** properties, so the
  `y = start - i * pitch` loop that produced v1's shipped collisions has no
  API surface to be written on.
- `Place.Flow / At / Pin / Stretch / Frac` — the full anchor space. v1's
  `NewUiRect` reached 13 of 86 sites because it hardcoded centre anchors and
  therefore could not express stretch or edge pins; a helper you are entitled
  to bypass gets bypassed. There is nothing this cannot say.
- `UiStrings` + `UiString` — every user-facing literal. `Ui.Label`/`Ui.Button`
  take **no `string` overload**, so a bare literal does not compile.
- `UiSolver.Solve` / `UiAudit.RunAllFrames` — pure, so an EditMode test builds
  and audits an entire screen at four canvas aspects in about a millisecond.

**Build-time (`Editor/SceneBuilder/`):**
- `UiEmitter` — the **only** place in the project that calls `new GameObject`
  for UI. The rect preamble exists here exactly once. A lint test enforces it.
- `UiEmitResult` — typed lookups (`Go`/`Tmp`/`Button`/`Image`/`Rect`) plus
  `Attach<T>` for engine dressing whose type Domain cannot name. `UiAutoBind`
  is these same five lookups driven by a controller's own field names.
- `UiAutoBind.Bind(result, controller, screen)` — call it once after each
  `Attach<T>`; it fills every `[SerializeField]` whose identifier mirrors a
  `NodeRef` on the screen (`UiBindingNames` states the rule), and writes
  nothing else. §3 says why it is not the banned `SetField` idiom.
- `ScreenRegistry.All` — the one list of screens, driving scene building, the
  audits and the screenshot tool; its `Wire` steps now carry only what
  `UiAutoBind` cannot bind, and its class header enumerates those four shapes.
- `UiTextFitAudit` (E1), `UiCountAudit` (E4), `UiWiringSweep` (E3) — measured
  text, declared-vs-bound counts, and non-null wiring, all at build time.
- `SceneBuilder.LoadSpriteByKey` — the one place a texture is force-imported
  as a Sprite. Returns `null` with a warning rather than throwing, matching
  the project's graceful-degradation-on-missing-art posture.

## 3. SceneBuilder rules

- **Scenes are generated, never hand-edited.** To change UI, change the
  screen's tree in `Domain/UiKit/Screens/` and its wiring in `ScreenRegistry`
  — not `SceneBuilder`, which knows nothing about layout.
- **Layout bounds are checked for you.** `UiAudit` re-solves every screen at
  four canvas aspects on every build and rejects sibling overlap, escaping
  children, flow overcapacity, duplicate sibling names and zero-sized
  graphics. Do not hand-roll a bounds check; if the audit is wrong for a
  specific node, declare `AllowOverlap("reason")` / `AllowOverflow("reason")`
  so the exemption is greppable.
- **Every content type implements `IOrderedContent`**, and is loaded through
  `ContentDatabase.LoadOrdered<T>` — which is constrained on it, so a type that
  has not said how it is ordered will not compile. `ContentLoadingLintTests`
  keeps `Resources.LoadAll` from being called anywhere else.
  Most types return their authored `sortOrder`; a spell tier returns its
  `level` and a talent its grid position, which is exactly why the interface is
  a property rather than a required field — a second authored int could only
  disagree with the key those two are really sorted by.
- **Wire controllers by direct field assignment or through `UiAutoBind`** —
  never by reflection over a field name *you had to type*. What is banned is
  the `SetField(controller, "playButton", value)` idiom, and the reason is a
  measured one: v1 carried 330 hand-written name strings, each independently
  misspellable, and a misspelling produced no compile error, no runtime error
  and a field that stayed null (`UiKitLintTests
  .TheStringlyTypedSetFieldIdiom_IsNotReImported` keeps that regex out).
  `UiAutoBind` has **zero** name strings: it keys on the controller field's
  OWN identifier, which the compiler already checked and which a rename
  carries with it, and `UiWiringSweep` refuses the build on any serialized
  reference it failed to fill — so a miss cannot ship, and cannot even build.
  Explicit assignment remains the rule for the residual an identifier cannot
  say: the name differs deliberately (`exitLabels` from `ExitButtons`), the
  value comes from a screen-side sub-object, or it is not a `NodeRef` at all.
  See §4a.

### 4a. Controller UI references: `internal` + `[SerializeField]`

Both halves are load-bearing, and dropping either one fails in a way that is
hard to see:

- **`internal`** is what lets `PrincesPalace.Editor` assign the field directly
  (`Core/AssemblyInfo.cs` grants `InternalsVisibleTo`). Direct assignment makes
  a typo or a type mismatch a **compile error at the wiring site**. v1 used
  `SetField(controller, "playButton", value)` at 330 sites, where a typo
  produced no compile error, no runtime error, and a field that simply stayed
  null until something unrelated threw much later.
- **`[SerializeField]` must stay** even though the field is no longer private:
  internal fields do **not** serialize on their own, and a field that does not
  serialize is null in the built player however correctly the builder assigned
  it. `UiWiringSweep` reads the *serialized* view precisely to catch that case.

`InternalsVisibleTo` is granted to the Editor assembly **only**. PlayMode tests
drive the UI through scenes and public API like a player does, so a test can
never quietly reach into controller state to make itself pass.

## 4. Partial-class conventions

(Established by the SceneBuilder and FightController splits — see
`docs/CODE_MAP.md` for the current file list of each.)

- Naming: `ClassName.Topic.cs`, one topic per file.
- The **root file** (same name as the class) owns the class declaration, all
  `[SerializeField]` fields, all consts, and any runtime state fields — for a
  **MonoBehaviour**. Parts contain only methods (and, where genuinely private
  to one topic, private nested types). Reason is the GUID binding in the next
  bullet: Unity's scene reference is anchored to the file that declares the
  class, so everything the scene or the inspector can reach has to live there
  too.
- For a **MonoBehaviour**, the root file's name must stay exactly the class
  name — Unity binds a component to a scene through the MonoScript asset
  whose GUID the scene references, and that GUID is tied to the file
  Unity first imported under that class name. Moving the declaration to a
  different file breaks every scene reference to it.
- For an **engine-free class split by topic** — no MonoBehaviour, nothing for
  a GUID to anchor — the rule above doesn't hold, and shouldn't: each part
  owns the runtime state its own topic needs, declared beside the comment
  that explains the invariant it serves. `FightSession`
  (`Domain/Combat/Session/`) is the worked example: `FightSession.Beats.cs:14-16`
  (`_beats`, `_immediateMessages`, `_recordingBeat`),
  `FightSession.Enemies.cs:31` (`_intents`), `FightSession.Talents.cs:82`
  (`_wardPayoutsThisTurn`, under the paragraph explaining the once-per-turn
  cap). The root file still owns the class declaration, the constructor, and
  state that genuinely spans topics (`_encounter`, `_locks`). Reason:
  locality — the invariant and the field that holds it stay in the same
  file, next to the comment that explains both.
- A part belonging to a namespaced class repeats the same `namespace { }`
  wrapper. A part belonging to a namespace-less class (like `SceneBuilder`)
  stays namespace-less too — they must match exactly.
- **No `static readonly` field initializer may reference a `static readonly`
  field declared in a *different* part file.** C# leaves cross-file static
  initializer order within one partial class unspecified. (Plain `const`
  fields are exempt — those are compile-time constants, not initializers.)
- **Never add an `.asmdef` inside a folder containing partial parts of an
  existing class.** Partial declarations split across assemblies simply
  don't compile, and the compiler error doesn't point at "you added an
  asmdef" as the cause.
- **No renames during a pure-move split.** Tests and other code reference
  methods by name (`SceneBuilder.BuildCombatStage`, `SceneBuilder.CreateButton`,
  etc. — see `docs/CODE_MAP.md` for what's known to be referenced this
  way). A rename is a separate, deliberate change with its own call-site
  sweep, not something to fold into a move — true of any rename, not just
  ones riding along with a partial-class split.

## 5. Functions

- **The unit is a reason to change, not a line count.** A long function is
  fine when it's a *declaration* — one description of one object with no
  independently-meaningful parts (a screen's `Build`, a content type's
  field-copy block in `ContentBuilder`). It's a problem when it's a
  *procedure* that branches and accumulates, where a different paragraph
  changes for a different reason each time
  (`ContentDatabase.Validation.ValidateContent`, 623 lines, one arm per
  content type — the project's own worst offender by this test). The
  question isn't length, it's: **when this changes next, will the whole
  thing change, or one paragraph of it?** Extract-as-reflex produces helpers
  named `HandleRest`/`DoTheOtherPart` — a wrong name costs more than no name.
  If you can't name the extracted piece after what it computes, you found a
  line number, not a seam.
- **Take the narrowest type that answers the question.** A function reading
  `.Count` off a list should take the count, not the list — `FightHudSpec`
  exists because Domain never needed the HUD's icon array, it needed a
  number, and a design decision about the HUD had leaked into combat logic
  through the array's length. This also keeps things EditMode-testable:
  narrow enough is usually engine-free by construction.
- **Past ~4 parameters, take an inputs struct.** Not for the count itself —
  a struct field can't be transposed the way positional args can
  (`new MainMenuInputs(5)` vs. `Build(5, 3, 24, true)`), and a field added
  later isn't a signature break for every caller.
- **A bool parameter is fine when it *is* the state**
  (`OnSlotHover(int index, bool entered)`), refused when it *selects the
  behaviour*. The tell: a call site reading `Refresh(true)`, where the reader
  has to open the callee to learn what `true` meant. That's two functions
  with two names and a shared tail, not one function with a flag.
- **`out` is for the Try-pattern only** (`TryGetValue`, `TryParse`,
  `TryResolveOne(..., out var resolved, out string error)`). An `out`
  returning an unrelated second result should be a tuple or a struct.
- **A default parameter value is a compatibility tool, not a design tool.**
  Right when the default *is* the meaning (`Ui.Label(fontSize = 24)` — most
  labels want body size); wrong when the value is a *decision*, which wants
  a named constant so it isn't quietly living in two homes (the default and
  the one caller that overrides it).
- **Collect every error in one pass; never stop at the first.** Every
  content resolver does this (`SkillEntryResolver.cs:26`) — a hand-edited
  file gets fixed once, not once per typo. Any new validator, audit, or
  lint inherits this: a `List<string>` of problems, empty for "fine."
- **Degrade, but never quietly lie.** Domain throws on programmer error — an
  encounter with no combatants, `Advance()` before `Start()` — because a
  caller misusing a pure library should find out immediately. Core degrades
  on missing content or art (a sprite-less `Image` disables itself rather
  than rendering a white quad) because a missing asset should cost you that
  asset, not the scene. The line that must never be crossed in either
  posture is returning a *plausible wrong answer* — a zero, an empty list,
  a default the caller can't distinguish from a real one.
- **What this project rejects, and why it isn't a universal truth:**
  line-count SRP (kept as a smell above, not a rule); an interface per class
  plus a DI container (the seams here are asmdefs and pure statics —
  `internal` + `[SerializeField]` + direct field assignment, §4a, makes a
  wiring typo a *compile error*, which an `IFightControllerService` wouldn't
  add); null-checking every argument at every level (one guard at the seam
  plus graceful degradation — repeated checks make it ambiguous which layer
  owns the invariant); `GameObject.Find`/`FindObjectOfType` for wiring (zero
  production uses — `ScreenRegistry` binds everything at build time).
  Changing any of these belongs in a commit message that argues it.

## 6. Values — one home per number

The rule underneath "make it dynamically adjustable" isn't *parameterise
everything* — a parameter with exactly one call site is a **worse** constant,
because the value now lives in two places (the default and the call) and
neither is obviously authoritative. The version worth enforcing:

> **Every number has exactly one home, and everything else derives from it.**

A `const` satisfies this. A literal can satisfy this. The question to ask of
a new value isn't "can this be configured," it's **"if a designer changes
this, how many places have to change with it?"** One is correct; more than
one is the defect, whatever mechanism produced it.

Three grades — prefer the first, the second is fine, the third is never
acceptable:

- **Derived** — computed from what it depends on, so it can't disagree.
  `TalentEntryResolver`'s bounds read `TalentPage.PathCount - 1` /
  `TalentSkeleton.SlotCount - 1` rather than the literals `2`/`20`.
- **Declared once** — a design decision with one name, referenced everywhere
  else (`FightHudSpec.StageSlotsPerSide`, `FightHudPalette`'s tokens).
  Correct when the number is a *choice*, not a *consequence*.
- **Restated** — the same value written twice. Worse than an honest magic
  number, because it fails *invisibly*: a restated colour once meant
  "change the hairline" silently produced two different hairlines mid-edit
  before anyone noticed, and a restated bound once meant a widened talent
  tree started rejecting valid content because the resolver's copy of the
  limit was never told.

When the program can measure a value at runtime, measure it instead of
authoring it — a text width, a sprite's bound, a child's measured height
should be read, not typed (a hand-measured tab-width table shipped wrong for
months because nobody re-measured it after the emitter's tracking changed).
A build-time approximation plus a runtime correction is fine *if the
authored figure's own test says it's an approximation* — not a value the
design actually depends on.

Checklist for a new value:
1. Grep it first. If it exists, reference the name — aliasing beats
   renaming (`const string CardRim = FightHudPalette.Hairline;` keeps the
   better local name and removes the local *value*, which is the part that
   rots).
2. If it depends on something, compute it from that thing, not from a copy
   of it.
3. If the program could measure it, measure it. If it can't yet (build
   time), say so at the declaration and in its test.
4. If it's a reserved **capacity** (not a current count — reserving
   `WoolPips = 16` is fine; reading a *current* count off a capacity
   constant is the bug), write the reason and add a pin that fails when
   content outgrows it.
5. Don't add a parameter you have no second caller for. One call site plus a
   default is two homes for one number.

## 7. State, lifetime, and globals

No DI container, no `GameObject.Find` graph — global state lives in static
fields, the right call for a single-player game with no networking. Four
rules, because statics are the one place the compiler stops helping:

- **Cache or state — decide, and name it accordingly.** A cache is
  rebuildable from something else and must be keyed by whatever it was built
  from (`ContentDatabase`'s lists; a run's map keyed by its seed). State is
  authoritative and belongs in the save, with the static as a thin accessor
  over it — never a second copy that can disagree with disk.
- **Every static cache gets a reset seam** (`ContentDatabase.Reset()`,
  `Navigation.Reset()`, and siblings). Without one, the first test to touch
  it poisons every later test in the same process.
- **A public mutable static needs an argument in its header** for why it's
  player-setting-shaped rather than a test escape hatch
  (`FightBeatPlayer.BeatSpeedMultiplier`, `RequirementCurve.Percent`).
  Otherwise make it `internal`.
- **Nothing that outlives a scene may be a MonoBehaviour** unless it's
  genuinely a scene object — a `DontDestroyOnLoad` singleton has a lifetime
  nothing can test. Route run-lifetime state through the save instead.

## 8. Test rules

- **Pin formulas with literal expected values**, never recompute the
  expected value by calling the method under test — that makes the test a
  tautology (`return this` would pass it). This project has hit a real flake
  from exactly this shape: `Mathf.RoundToInt` (banker's rounding) and
  `Math.Round`/`MidpointRounding.AwayFromZero` disagree at `.5` after
  float32 precision loss, and a test that recomputed its own expectation
  never caught the divergence because it used the same rounding both times.
- EditMode = Domain-only logic. The moment a test needs `ContentDatabase`,
  `SaveData`, `GameplayManager`, or a scene, it's PlayMode.
- **A test's area is the folder it sits in.** `Tests/<Platform>/<Area>/`, the
  seven areas being `Combat`, `Hub`, `Content`, `Run`, `Ui`, `Art`, `Rng`,
  with `Shared/` beside them for helpers carrying no `[Test]`. A file left
  directly in `Tests/EditMode` or `Tests/PlayMode` refuses the whole run
  (`tools/run_tests_parallel.ps1`), because a test in no area is invisible to
  every area slice and to `-Changed`. `tools/test_areas.ps1`'s header says
  what belongs where and why; one file, one area.
- **`Start()` runs one frame after `SetActive(true)`, not synchronously.**
  PlayMode tests must `yield return null;` **twice** after activating a
  panel before clicking its buttons. `OnEnable()` fires synchronously with
  `SetActive(true)`; `Start()` does not, and only fires once per component
  on first activation.
- Tests reference production methods by name — a rename anywhere is a
  refactor with test impact, not a free action.
- **`Assert.Ignore` on a condition keyed to content shape or run state is a
  test that can turn itself off.** A skip guarding a genuine environment
  limit (`-nographics`, no graphics device) is legitimate — a pixel test
  with no pixels has nothing to assert. A skip guarding "does content
  currently contain X" is not: content drift silently stops the test from
  covering anything, and a green suite can't tell you which case happened.
  This project has hit both the mild version (a skip that had simply never
  fired because the fixture always satisfied it) and the costly one (a skip
  that had *never once run*, on the only test covering a real code path).
  Prefer asserting the precondition loudly, or building a fixture that
  guarantees it, over skipping past it.
- **A lint or audit needs a vacuity guard.** `MinimumFilesExpected = 40`,
  `Assert.Greater(palette.Count, 30)` — a check that scans nothing passes
  everything, and a path change that silently turns every rule into a
  no-op still reports green.

## 9. Comment voice

- Comments state the **why** — a hidden constraint, a subtle invariant, a
  workaround for a specific bug, something that would surprise a reader.
  Not the what; well-named identifiers already say that.
- Comments travel with their code through a refactor (a moved method keeps
  its comment; a split file's class-level comment stays with the root part).
- No changelog-shaped comments ("changed X to Y", "added for the Z flow").
  That belongs in the commit message, which doesn't rot as the code moves.
- **A comment can't be compiled, tested, or linted — it's a restated value
  in prose**, and §6's rule about restatement applies to it just as much as
  to a duplicated literal. A comment saying "the bounds are 0-2 and 0-20" is
  a second home for those numbers; a comment saying "bounded by
  `TalentSkeleton.SlotCount`" is not, and stays true for free when the
  skeleton changes. Prefer a reference to a restatement in prose as much as
  in code. Where a comment must state a number, an assertion is the
  strongest form available — a test that fails when the claim stops being
  true is a comment that cannot rot.
- **When you fix an instance of a bug, spend one sentence on which tier
  could kill the whole class of it**, in descending order of strength:
  **T1** — the API cannot express the mistake (a type, so the wrong thing
  doesn't compile: `Ui.Label` takes `UiString`, not `string`). **T2** — one
  code path owns the concern, so there's nowhere else to get it wrong
  (`UiEmitter` is the only place calling `new GameObject` for UI). **T3** —
  mechanised discipline as a fallback: a lint over source, with a vacuity
  guard (see above) and its scoping written down as a judgement, since a
  regex only approximates intent. A rule that can't be mechanised at all
  still wants a greppable exemption (`AllowOverlap("reason")`) rather than a
  silent one-off deviation.

## 10. Architecture and long-term maintainability

Owner's standing instruction, 2026-09-08, recorded after the spell player
had grown four spell-specific patches in a month and could still only "shoot
a blob". Prince's Palace is a growing product. Build coherent systems that
support foreseeable variation, rather than narrowly solving each request
through another flag, hardcoded slot, special case or parallel
implementation.

**Before extending a system**, inspect its existing design, its documented
decisions (`docs/`, `AUDIT.md`, `architecture_audit.md`) and the known
upcoming requirements. Decide whether the underlying model still fits. If it
does not, say what the limitation is and recommend a proportionate
architectural change. Never silently reduce the requested experience to fit
an inadequate implementation -- that is the failure that hides, because the
output looks finished.

**Separate reusable behaviour from authored content.** New combinations of
supported behaviour should normally cost a data or configuration change.
New behaviour may cost code, but that code should be local, testable and
compatible with existing content.

**For a meaningful architectural decision:**

- Identify what stays consistent and what varies across the relevant uses.
- Validate the model against at least two materially different known uses,
  where they exist (the spell layers plan uses Water and Cinderfault).
- Explain how the next foreseeable requirement would fit and where its code
  would belong.
- Define ownership, lifecycle, failure handling and compatibility, not only
  the successful path.
- Keep one authoritative implementation of every shared rule.

**SOLID through clear responsibilities, cohesive modules and small
interfaces.** No speculative frameworks, no abstraction without a second
concrete user, no configurability without a demonstrated purpose.
Flexibility is only good when it makes the project easier to read and
extend.

**Evaluate total project cost**: code, art and content authoring,
integration, testing, maintenance and likely rework. The smallest patch
today is not automatically the simplest solution overall.

**Preserve working behaviour** with verification appropriate to the change,
and with an explicit migration when one is needed. Record consequential
design decisions, tradeoffs and deliberate limitations in tracked
documentation (a plan under `docs/`, a finding in `AUDIT.md`, or the commit
message), not in chat.

**Scale this to the task.** A routine change needs no architecture exercise.
Repeated special cases, duplicated logic, or a request that strains the
existing model do require stepping back before the next patch.

Success means the current feature works well, foreseeable variations fit
naturally, and future changes have clear, predictable places to go.
