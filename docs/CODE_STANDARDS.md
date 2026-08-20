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
  some time; character portraits go through `ItemIcons` like everything else.
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
- `CharacterTabStrip.Wire` / `.SetVisibleCount` / `.Refresh` — the character-
  switcher tab row's click-wiring, squad-count show/hide, and colour+label
  refresh, shared by CharacterSheetController, RelicsController and
  TalentController. Each screen still owns its own active/inactive colors.

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
- `UiEmitResult` — typed lookups (`Go`/`Tmp`/`Button`/`Image`) plus
  `Attach<T>` for engine dressing whose type Domain cannot name.
- `ScreenRegistry.All` — the one list of screens, driving scene building, the
  audits and the screenshot tool.
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
- **Wire controllers by direct field assignment**, never by reflection over a
  field name. See §4a.

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
  `[SerializeField]` fields, all consts, and any runtime state fields. Parts
  contain only methods (and, where genuinely private to one topic, private
  nested types).
- For a **MonoBehaviour**, the root file's name must stay exactly the class
  name — Unity binds a component to a scene through the MonoScript asset
  whose GUID the scene references, and that GUID is tied to the file
  Unity first imported under that class name. Moving the declaration to a
  different file breaks every scene reference to it.
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
  sweep, not something to fold into a move.

## 5. Test rules

- **Pin formulas with literal expected values**, never recompute the
  expected value by calling the method under test — that makes the test a
  tautology (`return this` would pass it). This project has hit a real flake
  from exactly this shape: `Mathf.RoundToInt` (banker's rounding) and
  `Math.Round`/`MidpointRounding.AwayFromZero` disagree at `.5` after
  float32 precision loss, and a test that recomputed its own expectation
  never caught the divergence because it used the same rounding both times.
- EditMode = Domain-only logic. The moment a test needs `ContentDatabase`,
  `SaveData`, `GameplayManager`, or a scene, it's PlayMode.
- **`Start()` runs one frame after `SetActive(true)`, not synchronously.**
  PlayMode tests must `yield return null;` **twice** after activating a
  panel before clicking its buttons. `OnEnable()` fires synchronously with
  `SetActive(true)`; `Start()` does not, and only fires once per component
  on first activation.
- Tests reference production methods by name — a rename anywhere is a
  refactor with test impact, not a free action.

## 6. Comment voice

- Comments state the **why** — a hidden constraint, a subtle invariant, a
  workaround for a specific bug, something that would surprise a reader.
  Not the what; well-named identifiers already say that.
- Comments travel with their code through a refactor (a moved method keeps
  its comment; a split file's class-level comment stays with the root part).
- No changelog-shaped comments ("changed X to Y", "added for the Z flow").
  That belongs in the commit message, which doesn't rot as the code moves.
