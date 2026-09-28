---
paths:
  - "Assets/_Project/Scripts/Domain/UiKit/**"
  - "Assets/_Project/Scripts/Editor/SceneBuilder/**"
  - "Assets/_Project/Scripts/Editor/ProceduralSpriteBaker.cs"
  - "Assets/_Project/Scripts/Core/*Controller*.cs"
---

# UI rules

Screens are trees in `Domain/UiKit/Screens/`, wired in
`Editor/SceneBuilder/ScreenRegistry.cs`; see `CLAUDE.md` rule 1.

## Existing helpers (use before writing one)

- Screen vocabulary: `Ui.Panel/Column/Row/Grid/Label/Button/Sprite/Solid/Space/Modal/Each/Pool`.
  A flow child has no position parameter; spacing and padding are container
  properties.
- `Place.Flow/At/Pin/Stretch/Frac`: the full anchor space.
- `UiStrings` + `UiString`: every user-facing literal. `Ui.Label`/`Ui.Button`
  take no `string`.
- `UiSolver.Solve` / `UiAudit.RunAllFrames`: pure; an EditMode test audits a
  screen at four aspects.
- `UiEmitter`: the only code that calls `new GameObject` for UI (lint-enforced).
- `UiEmitResult` (`Go/Tmp/Button/Image/Rect`, `Attach<T>`) and
  `UiAutoBind.Bind(result, controller, screen)` after each `Attach<T>`: fills
  every `[SerializeField]` whose identifier mirrors a `NodeRef`
  (`UiBindingNames` states the rule).
- `ScreenRegistry.All`: the one list of screens (scenes, audits, screenshot tool).
- Build audits: `UiAudit`, `UiTextFitAudit`, `UiCountAudit`, `UiWiringSweep`.
- `SceneBuilder.LoadSpriteByKey`: the one place a texture is force-imported
  as a Sprite; returns `null` with a warning on a miss.
- Runtime: `ItemIcons.Find/Apply` over `IconEntry[]` (disables the `Image`
  on a miss, sets `preserveAspect`); `CharacterPortraits.For(id)` (runtime
  load, so a miss leaves the placeholder).
- Ambient motion, each a pure static curve, not interchangeable:
  `BeaconPulse` (signal), `LanternFlicker` (flame), `StarTwinkle` (alpha),
  `SlowDrift` (mist), `MoteDrift` (rising motes), `KenBurnsDrift` (pan +
  scale never below 1). Glows sit as a sibling before the element they glow
  behind.

## Wiring

- Controller UI references are `internal` + `[SerializeField]`: `internal`
  lets `PrincesPalace.Editor` assign directly (compile-checked);
  `[SerializeField]` makes it serialize. `InternalsVisibleTo` is granted to
  the Editor assembly only; PlayMode tests drive the UI through scenes and
  public API like a player does.
- Wire by direct assignment or `UiAutoBind`, never reflection over a typed
  field-name string (`SetField`; lint-enforced, and `UiWiringSweep` refuses
  a null serialized reference).
- A MonoBehaviour attached at build time (`Attach<T>`) lives in its own file
  named exactly after the class, or the rebuilt scene carries a missing
  script. Only an actual `-BuildScenes` shows it.

## Layout

- Do not hand-roll bounds checks; `UiAudit` covers overlap, overflow,
  capacity, duplicate names and zero size. Exempt a node with
  `AllowOverlap("reason")`/`AllowOverflow("reason")`.
- An exemption says why some overflow is expected, never how much: give an
  exempt node its own test for the property it must still satisfy.
- A child's `Place` is in its parent's frame; moving a node under a new
  parent means re-deriving its placement.
- Two dimensions sharing a budget, one authored and one the remainder: give
  the remainder a floor, or it starves without any audit firing.

## Sprites

- A generated asset's import settings belong to its generator
  (`ProceduralSpriteBaker`); `LoadSpriteByKey` leaves `Art/Generated/` alone
  beyond the texture-type check.
- `GetPixels` on a tight-mesh sprite returns crop-space coordinates: add
  `textureRect.x/y` before comparing with anything in `sprite.rect` units.
