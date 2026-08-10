# Design brief — the Divine Principality (main hub)

> **ARCHIVED 2026-08-01: delivered.** The hub background and all five
> building sprites this brief specifies have landed (`Resources/Hub/*`,
> `Art/Backgrounds/Divine_principality_nebula.png`) and are wired into
> `SceneBuilder.BuildHubPanel`. Kept as historical reference for the
> composition math and art-direction reasoning.

**For:** an art/design pass. Hand this whole file over as the prompt.
**Deliverable:** one background PNG + five foreground asset PNGs, to the specs in §2.
**Not in scope:** any code. The scene is generated from `SceneBuilder.cs`; I wire the
assets up once they exist.

---

## 1. What this screen is

The Divine Principality is the between-runs home base of a turn-based roguelike
dungeon crawler. The player returns here after every run, spends what they hauled
out, and leaves again. It is the only screen that gets richer over the course of a
save file, and that is the whole design problem:

> **The hub must visibly grow.** Progression is shown by things APPEARING on this
> screen, not by numbers going up in a menu.

So the screen is not a menu with a picture behind it. It is a **place**, with
buildings in it, and each building is the way into a sub-screen. New buildings drop
in as the player unlocks them.

Today it is six grey buttons stacked in a column over a throne-room painting. That is
being thrown out entirely.

### Destinations that must be reachable

| Destination | What it is | Needs art? |
|---|---|---|
| **Start Run** | Commit to a dungeon run. One-way. The primary action. | **Yes — the hero asset** |
| **Talents** | Spend talent points on a per-character skill tree. | **Yes** |
| **Principality** | The shop. Spend Gold and Relics on permanent upgrades. | **Yes** |
| **Character Sheet** | Stats, equipment, ability scores. | **Yes** |
| Dialogue Test | Developer tool. Replays the intro scene. | No — stays a plain small button |
| Main Menu | Back out to the title screen. | No — stays a plain small button |

---

## 2. Hard technical constraints

These are not negotiable; they are what the engine can actually display.

- **Unity 6, legacy uGUI.** Every asset is a plain `Image` with a Sprite. No shaders,
  no particle systems, no animation clips. **Anything that should glow, pulse or
  shimmer has to be baked into the PNG.**
- **Reference resolution 1920 × 1080**, `ScaleWithScreenSize` / `Expand`. The
  background is stretched to fill the canvas edge to edge, and on non-16:9 displays
  that stretch is **non-uniform**. Consequences:
  - Keep anything with recognisable geometry (a perfect circle, a readable glyph) out
    of the outer ~8% of the background.
  - Nebula clouds and star fields are ideal for this — they distort invisibly.
- **Foreground assets are separate PNGs with transparent alpha.** They are positioned
  individually, so they must read correctly against any part of the background.
- **PNG, 32-bit, straight alpha.** No baked-in background colour, no white matte
  fringe — these sit over a near-black field and any light halo will show.
- **Deliver at 2× the display size** (listed per asset in §4) so they stay sharp on a
  4K screen. Power-of-two is not required.
- **Hover and press are handled by the engine** — every clickable thing already scales
  to 105% on hover and 95% on press. Do not draw a separate hover frame. If you want a
  brighter "selected" look, deliver an optional `_lit` variant and I will swap it.
- Existing UI font is **Chakra Petch**. Any lettering baked into art should sit
  comfortably next to it — geometric, slightly technical, not calligraphic.

### Where files go

```
Assets/_Project/Art/Backgrounds/Divine_principality_nebula.png   ← the background
Assets/_Project/Art/UI/Hub/gate.png                              ← foreground assets
Assets/_Project/Art/UI/Hub/talents.png
Assets/_Project/Art/UI/Hub/principality.png
Assets/_Project/Art/UI/Hub/character_sheet.png
Assets/_Project/Art/UI/Hub/empty_plot.png
```

---

## 3. Art direction

The game's look is **Hades-style painterly** — visible brush texture, strong rim
light, saturated colour against deep shadow, hand-painted rather than vector-clean.
Existing screens (`Art/Backgrounds/`) are the style anchor: `Fight.png`,
`Divine_principality.png`, `Talents.png`.

### The background: a purple-black nebula

A **large purple and black nebula field, dense with stars.** Deep space, but warm
rather than cold — this is home, not a void.

- **Value range: dark.** The foreground assets are the bright things on this screen.
  If the background competes, the buildings stop reading as clickable.
  Target: background sits in the bottom third of the value range almost everywhere.
- **Purple, but not one purple.** Deep violet through magenta through near-black
  indigo, with the odd warm ember-orange or teal pocket so it does not read as a flat
  gradient. Nebula gas should have visible internal structure — filaments, dust lanes,
  a brighter core somewhere off-centre.
- **Stars at three scales:** a fine dust of tiny ones, a scattered mid layer, and a
  handful of large bright ones with subtle diffraction. Do not distribute them
  evenly — clusters and voids read as depth.
- **Composition matters more than detail.** There are two rules:
  1. **Quiet zones** where the buildings sit (see the map in §4). Low star density,
     low contrast, darker value. The buildings need somewhere to land.
  2. **A brighter region low-centre**, behind the gate, so the primary action has a
     natural halo without needing a drawn one.
- No horizon, no ground plane, no visible platform edges. The buildings float.

### Optional, if it's cheap

A second background layer of foreground nebula wisps with alpha, to sit *over* the
buildings at very low opacity for depth. Nice, not required — flag it if you do it and
I will layer it.

---

## 4. Composition and the asset list

Coordinates are **canvas units from the centre of a 1920×1080 frame**, x→right,
y→up. So the frame spans x ∈ [−960, 960], y ∈ [−540, 540].

> **These numbers were corrected on 2026-07-30 and are now the built, tested layout.**
> The first draft of this table was wrong: measured rather than eyeballed, the gate
> overlapped the character sheet even at rest, and the two rows overlapped by ~68px
> once a building was hovered to 105%. The layout below is what `SceneBuilder` actually
> builds and what `HubCompositionTests` pins. **Draw to these.**

```
        (−960,540)                                        (960,540)
            ┌──────────────────────────────────────────────────┐
            │ [Main Menu]     DIVINE PRINCIPALITY  [1,240 · 7] │   title y=470
            │                    BETWEEN RUNS                  │   plate (690,470)
            │   ╭────────╮                      ╭────────╮     │
            │   │Talents │                      │Princip.│     │   y=200
            │   ╰────────╯                      ╰────────╯     │
            │      (−620)                          (620)       │
            │                                                  │
            │                 ╔══════════╗                    │   gate y=−170
            │ ╭──────╮        ║   GATE   ║        ╭ ─ ─ ─╮     │
            │ │ Sheet│        ║          ║        ╷ empty╷     │   y=−200
            │ ╰──────╯        ╚══════════╝        ╰ ─ ─ ─╯     │
            │   (−500)            (0)               (500)      │
            │                                    [Dlg Test]    │
            └──────────────────────────────────────────────────┘
        (−960,−540)                                      (960,−540)
```

The layout is an **arc opening downward toward the gate** — the eye is led inward and
down to the one action that leaves the screen. An empty plot sits in the arc so the
shape is already legible before anything is unlocked.

| Asset | Position | Display size | Deliver at |
|---|---|---|---|
| `gate.png` | (0, −170) | 560 × 560 | 1120 × 1120 |
| `talents.png` | (−620, 200) | 360 × 360 | 720 × 720 |
| `principality.png` | (620, 200) | 360 × 360 | 720 × 720 |
| `character_sheet.png` | (−500, −200) | 340 × 340 | 680 × 680 |
| `empty_plot.png` | (500, −200) | 340 × 340 | 680 × 680 |

Each asset should be drawn **centred in its square with breathing room** — roughly 10%
padding on every side — so the engine's hover scale-up has somewhere to go and two
neighbours never collide at 105%.

The three constraints that fix these numbers, in the order they bind:

1. The small row has to clear the gate horizontally at 105% hover, which puts it at
   x = ±500 rather than ±440 (a 560px gate is 294 half-wide hovered, a 340px asset
   178.5).
2. The rows have to clear each other vertically at 105%, which is what lifts the upper
   row from y=120 to y=200.
3. The gate stays 560 and the SECONDARY buildings came down to 400 → 360. Shrinking the
   gate instead was tried and rejected: at 500 it is only 25% bigger than Talents and
   stops reading as the primary action, where 560 against 360 is 56% bigger and
   unmistakably the hero. It also sits 30px above the row it anchors, at y=−170.
4. Nothing may leave the 1920×1080 frame, which is the binding limit on the gate —
   hovered it reaches y=−464, with 76px to spare.

Positions are mine to adjust in code, but they are now pinned by a test, so a change is
a deliberate edit in two places rather than a drift. If the composition wants to move,
say so and I will move it; do not distort an asset to fit a number in this table.

**The plot at (500, −200) is the last free position in this arc.** Once something is
built there, taking another needs the composition itself to change — there is no
central slot between the gate and the upper row, and the outer edges are spent.

---

## 5. What the placeholders should look like

These are **placeholders in ambition, not in craft** — simple, single-idea shapes that
read instantly at 400px, but painted in the real style so the screen looks finished
rather than blocked-out. Each one has to be identifiable **with no label on it**,
because the labels are coming off.

**`gate.png` — Start Run.** A freestanding stone archway, floating, no ground beneath
it. Weathered pale stone with gold or violet inlay catching light. The opening is
**not** empty: it holds a dark, deep, faintly swirling void, a little darker than the
nebula behind it, so it reads as a way *through* rather than a hole. Two braziers with
warm flame flank it. This is the biggest, brightest, warmest thing on screen — it
should pull the eye from anywhere.

**`talents.png` — Talents.** A small bare tree floating on a fragment of dark rock,
branches spread wide and root ends trailing into space. Along the branches, seven or
eight **glowing nodes** — some lit warm gold, some still dark — connected by faint
lines, so the shape of a skill tree is legible at a glance. Cool violet bark, warm
light. No leaves; the light is the foliage.

**`principality.png` — the shop.** A merchant's floating stall: a canopy or awning in
deep magenta over a counter, warm lantern light under it, and a few unmistakable goods
on the counter catching the light — a potion bottle, a coin stack, a gem. Cluttered and
warm, the most "inhabited" thing on screen. It should feel like the one place with a
person in it.

**`character_sheet.png` — Character Sheet.** An open book on a floating lectern, pages
lit from within, faint script and a small figure diagram visible on the spread. A
single quill. Cooler and quieter than the other two — this is the contemplative corner.

**`empty_plot.png` — reserved ground.** A flat fragment of dark rock with a **dashed or
dotted outline** of a structure hovering over it in faint violet light, like a
blueprint that has not been built yet. Deliberately unfinished and deliberately dim.
Its whole job is to say *"something goes here later"* — it must read as a promise, not
as a broken asset. Roughly 40% the visual weight of a real building.

---

## 6. Rules for everything added later

Any future building must drop into this screen without a redesign. So:

- **Square canvas, transparent, centred subject, ~10% padding.** One of the three
  sizes in §4 (560 / 400 / 340).
- **Floating.** No ground plane, no cast shadow onto anything, no perspective that
  assumes a specific spot on screen.
- **Lit from the lower centre**, where the gate's braziers are, so a new building's
  light direction agrees with its neighbours wherever it lands.
- **Its own light source.** Every building is internally lit — that is what makes it
  read as clickable against a dark field. A building that only reflects ambient light
  will look disabled.
- **Silhouette first.** It has to be identifiable as a black shape at 30% size. If two
  buildings have similar silhouettes, one of them is wrong.

---

## 7. Deliverables checklist

- [ ] `Divine_principality_nebula.png` — 3840 × 2160, no alpha needed
- [ ] `gate.png` — 1120 × 1120, transparent
- [ ] `talents.png` — 720 × 720, transparent
- [ ] `principality.png` — 720 × 720, transparent
- [ ] `character_sheet.png` — 680 × 680, transparent
- [ ] `empty_plot.png` — 680 × 680, transparent
- [ ] *(optional)* `*_lit.png` variants for hover
- [ ] *(optional)* `nebula_overlay.png` — foreground wisps with alpha

---

## 8. Open questions — resolved

All three were answered by the composition pass and are now built.

1. **Title treatment — live text, painted ornament.** The words stay Chakra Petch at
   (0, 470), with "BETWEEN RUNS" under them at (0, 415). Baking the lettering into the
   background would put it in the non-uniform stretch zone and squash the letterforms
   on 16:10, and would cost localisation. **Paint the ornament instead** — the rule,
   the glow, the flanking marks — into the background, and the words sit on top of it.
2. **Currency readout — yes, built.** A 380 × 90 plate at (690, 470), Gold left,
   Relics right. If you draw it, use the shop's material vocabulary (gold rule,
   magenta gem) so the eye connects the readout to the destination that spends it.
   Deliver as one sprite with the divider baked; the numbers are live text.
3. **Gate animation — animate the braziers, not the void.** Six frames of irregular
   flame flicker at 2–3fps sells "inhabited" far more cheaply than a swirl loop, and as
   the only motion on a still screen it becomes the natural attractor for the primary
   action. A swirling void needs 12+ frames to hide its loop; the braziers do not.
   **Not built yet** — it needs a small looping frame player, since the existing one
   (`SpellVfxPlayer`) plays a sequence once and stops.

### Still open, from reviewing the prototype

- **The value hierarchy is inverted.** The brief says the gate is the biggest,
  brightest, warmest thing on screen. In the prototype the shop's hot-magenta canopy is
  the highest-chroma object and the title is the brightest — the gate wins on size and
  centrality but is in a real fight for the eye. Tone the awning toward the *deep*
  magenta this document actually asked for.
- **The empty plot fails the silhouette test by design**, so its glow is load-bearing.
  It has to stay above roughly 35% of a real building's luminance or it stops reading
  as reserved ground and starts reading as a failed asset load.
