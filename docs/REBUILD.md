# Rebuild: Prince's Palace v2

Decided 2026-08-09. This document is the porting checklist and the reason
the rebuild exists. It supersedes the in-place migration roadmap.

## Why rebuild rather than migrate

The in-place plan (URP migration, then TMP) would have rewritten most of the
UI layer anyway — the canvas restructure and `Text` → `TMP_Text` together
touch essentially every UI file. Doing that *in place* means fighting 1403
tests at every step while every legacy decision stays baked in around you.

The thing that makes a rebuild cheap here is a decision made long ago:
**`Domain/` has `noEngineReferences: true` on its asmdef.** The actual game —
combat math, dungeon generation, stats, rewards, RNG — has zero Unity
dependency, enforced by the compiler rather than by good intentions. It moves
across untouched.

## What ports free vs what gets rebuilt

| Ports across essentially untouched | Rebuilt deliberately |
|---|---|
| `Scripts/Domain/` — 90 files, engine-free | `Editor/SceneBuilder*` — 7,608 lines of uGUI construction |
| `Tests/EditMode/` — 44 files, **789 tests**, Domain-only by rule | `Tests/PlayMode/` — 615 tests, scene-coupled |
| `ContentData/*.json` — all game content | `Core/` MonoBehaviours (~18,700 lines, mostly thin over Domain) |
| `tools/` — the whole art + test pipeline and its manifests | |
| `Art/`, `Resources/` (non-generated) — every asset | |
| `docs/`, `AUDIT.md` — the institutional memory | |

## Framework: uGUI again, on URP + TMP from day one

**Not UI Toolkit**, despite it fitting the build-everything-from-code style
better on paper. Measured against the three stated requirements:

- **"Able to do everything."** UI Toolkit's ceiling is higher for layout and
  data but *lower for custom rendering* — it uses its own batched renderer,
  so you cannot hang a Material on a `VisualElement` the way you can on an
  `Image`. `UIEffect` and `UIParticle` do not work with it at all.
- **"Dynamic."** This game's identity is custom-rendered: lantern flicker,
  additive glows, ember particles, a parallax shader. That is uGUI's
  strength and UI Toolkit's soft spot.
- **"Builds out at scale."** UI Toolkit would win this one — but the
  framework was never the bottleneck. `AUDIT.md` #29 (94 sites duplicating a
  rect preamble), "systems worth building" #2 (11 near-identical strip loops,
  two line-for-line identical), and #35 (116 loose string literals) are all
  *construction layer* problems. The rebuild fixes them there.

Same framework, radically better construction layer underneath.

## The scar-tissue checklist

**The rebuild's real risk is not the code — it is silently re-learning what
`AUDIT.md` already paid for.** Every item below is a bug this project has
already shipped or narrowly avoided. Nothing here is theoretical.

### Rendering / uGUI
- [ ] **`Image.Type.Sliced` produced a completely invisible button on every
      screen**, compiling and running with zero errors. Production uses
      `Type.Simple` and per-size procedural rounded rects instead of 9-slice.
      **Now genuinely re-testable** via `screenshot.ps1 -Runtime` — this was
      only ever unverifiable because Editor Play Mode entry hung.
- [ ] **`CanvasScaler` must be `ScaleWithScreenSize` + `ScreenMatchMode.Expand`.**
      The default (`MatchWidthOrHeight`, match 0) matches WIDTH only, which let
      the canvas grow to 1200 units tall at 16:10 and desynchronised stretched
      art from fixed-offset text.
- [ ] **A nested `Canvas` ignores `sortingOrder` unless `overrideSorting` is
      set.** `BarkCanvas`'s 500 is inert in v1 and renders on top by call-order
      accident. Set both, always.
- [ ] **Default text colour is black and unreadable on dark panels** — the
      delete-confirmation label shipped invisible (AUDIT P0 #4). Decide text
      colour per surface, not per widget.
- [ ] **A beacon/glow must be a SIBLING built before the element it sits
      behind**, never a child — uGUI draws later siblings on top, so a child
      paints over its own parent.
- [ ] **A `Graphic` with no sprite renders as a solid white quad**, not as
      nothing. Carry `WhiteQuadTests`' sweep over.

### Content / assets
- [ ] **Do NOT carry `[CreateAssetMenu]` onto the `*Definition` types.**
      There are **8** of them in v1 (`CLAUDE.md` says five — it undercounts).
      They invite authoring into `Resources/Content`, which `ContentBuilder`
      deletes wholesale with no prompt. Replace the comment-based guard with a
      reflection test that fails if the attribute reappears.
- [ ] **`Resources.LoadAll` returns filename-alphabetical order, not
      authoring order.** Every content type needs an explicit `sortOrder`
      sorted for in `ContentDatabase`.
- [ ] **A PNG imported with Unity's default `textureType` returns `null` from
      `Resources.Load<Sprite>`** — no error, no warning. The golem's Boulder
      Slam shipped playing nothing for weeks. Every runtime-loaded folder needs
      an import postprocessor.
- [ ] **Two art-path conventions coexist** (AUDIT #32): `iconPath` is
      `Assets/`-relative and baked at build time; `spritePath`/`vfxPath`/
      `sfxPath` are `Resources/`-relative and loaded at runtime. Getting it
      wrong fails silently. Unify or validate all of them in v2.

### Layout
- [ ] **Count-driven layouts collide with fixed neighbours** (AUDIT #6, #7).
      The count came from content while spacing and neighbour positions stayed
      hardcoded, so adding one entry silently overlapped something. Every
      content-count-driven layout must assert its own bounds at build time.

### Tests / tooling
- [ ] **Never recompute a production formula to build a test's expected
      value** — `Mathf.RoundToInt` (banker's) and `Math.Round`
      (`AwayFromZero`) disagree at `.5` after float32 loss. Pin literals.
- [ ] **`Start()` runs one frame after `SetActive(true)`, not synchronously.**
      PlayMode tests must `yield return null` twice after activating a panel.
- [ ] **Static state outlives a scene and leaks between tests** (AUDIT #23).
      Anything static needs an explicit reset hook.
- [ ] **Play Mode: the EDITOR entering Play Mode from a batch
      `-executeMethod` call hangs. The PlayMode TEST RUNNER has always
      worked.** That over-generalisation cost months of blind visual work.
- [ ] **The suite runs `-nographics`** — nothing can render inside the normal
      gate. Capture tests must guard on `SystemInfo.graphicsDeviceType`.
- [ ] Carry over `test_areas.ps1`'s orphan gate: the suite refuses to run if
      a test class matches no area. There is no bypass flag, deliberately.

### Process
- [ ] **New files need their `.meta` committed with them** or the GUID
      regenerates on the next fresh `Library` rebuild and orphans every
      reference.
- [ ] **`LoadSprite` silently flips a texture's importer settings** and
      generates a fresh `.meta`. Diff `Art/` after a scene build, not just
      `Scenes/` and `Resources/`.
- [ ] **Never `git add -A`.** Two sessions have shared this tree; one such
      commit already destroyed ~58 files of another session's work.

## Milestones

- **M1 — Scaffold.** `C:\Games\Prince's Palace-v2`: five asmdefs, manifest
  with URP + Input System + UIEffect + UIParticle + test-framework, URP Asset
  and Renderer 2D generated from code (never hand-authored). Tuned
  `ProjectSettings` ported from v1, cruft stripped.
- **M2 — Domain green.** `Domain/` + `Tests/EditMode/` ported verbatim; all
  **789 EditMode tests passing**. This is the milestone that proves the
  strategy: the actual game runs on the new foundation.
- **M3 — Content + art.** `ContentData/`, `tools/`, `Art/`, `Resources/`,
  `docs/`. `ContentBuilder` ported so `ContentDatabase` loads and validates.
- **M4 — Construction layer.** The widget library that fixes #29, #2 and #35
  by construction. TMP-based from the start.
- **M5 — Main Menu.** First screen on the new stack, carrying the ambience
  work forward plus the additive glows and bloom the old stack could not reach.
- **M6 — Hub.** The Divine Principality on the new stack, five buildings and
  two corner buttons as one audited tree.
- **M7 — Save slots.** The Play and Options panels made real against
  `SaveSystem`, with the delete-then-reopen path covered.
- **M8 — The fight, Domain half.** `FightSession` and its six partials,
  `DamagePipeline`, `VictoryRewards`: the ~34% of v1's `FightController` that
  was never about Unity, now **1,028 EditMode tests** with first-ever coverage
  of the damage funnel's composition order, enemy AI, rider ordering and the
  fourteen-effect skill dispatch.
- **M9 — The fight, screen tree.** `FightScreen` builds, solves and audits
  clean at four canvas aspects with no scene in existence; the Fight scene
  emits and passes E1–E4. The first `Ui.Pool` call sites anywhere.
- **M10+** — the fight's runtime view (HUD refresh, menu machine, beat
  playback), then the remaining screens.

## Rules that carry over unchanged

Scenes and content are **generated, never hand-authored**. Commit messages
explain reasoning and tradeoffs. Commit only test-passing checkpoints. Stage
by explicit path.
