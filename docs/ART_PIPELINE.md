# Art Pipeline

How generated art gets from a raw AI output to a game-ready sprite in this
project, and where each kit's pieces actually live.

## 1. Kit registry

| Kit | Source | Keying | Output | Status |
|---|---|---|---|---|
| Hub buildings | `Art/UI/Hub/` | grouped, green `#00FF00` | `Resources/Hub/<building>/f0.png..` | delivered |
| Relic icons | `Art/Items/Relics/` | direct, green `#00FF00` | `Art/Items/Relics/Processed/` | delivered (3/3) |
| Talent Tree kit | `Art/UI/TalentTree/` | direct, green `#00FF00` | `Art/UI/TalentTree/Processed/` | delivered, not yet wired (gated on Phase 7 design confirmation — see `docs/handoffs/talent_tree/`) |
| Portraits | `Art/Portraits/<Character>/` | flood-fill from border (`tools/remove_portrait_backgrounds.py`, `PORTRAITS` manifest) | `Art/Portraits/<Character>/Processed/` | ongoing, per-character — **Sheep does not reproduce, see below** |
| Enemy/item/spell sheets | raw sheet per type | grid slice (`slice_actor_sheet.py` / `slice_item_sheet.py` / `slice_spell_sheet.py`) | one PNG per frame/stance/level, or `{stance}/f0..fN` for an animated stance (see §4) | ongoing |
| Backgrounds | `Art/Backgrounds/` | none — full-frame opaque | same folder | ongoing, one outstanding: `Relics.png` |
| Map icons | raw | flood-fill (`process_map_icons.py`) | `Art/Backgrounds/Processed/` | delivered |
| Six-theme UI kit | `Art/UI/Buttons/` sheets (button plates legacy/3x1/5x1, row 6x1, containers 3x4/9x16/3x2/2x1, flag banners 3x4/9x16), one set per theme (Gold/Crimson/Violet/Blue/Green/Silver) | grid slice (`tools/splice_ui_kit.py`), nominal filenames but measured aspect — see `ButtonPlateArt`/`ContainerArt`'s own headers for the per-shape numbers | `Art/UI/Buttons/Processed/` | delivered |

## 2. Keying conventions

Three distinct techniques, picked by what the asset actually is:

- **Painted objects (icons, hub buildings, kit pieces): flat `#00FF00`
  field, keyed on hue-dominance** (`tools/key_green_screen.py`). Brightness-
  keying (generate on black, key alpha from luminance) is the obvious
  alternative and is **wrong** here — it punches straight through any
  legitimately dark shadow or void the object is supposed to have opaque.
  Green appears nowhere in this project's palette, so keying on how much the
  green channel *dominates* red/blue (not on raw brightness) removes exactly
  the backdrop and nothing else.
- **Pure light/glow effects with no opaque subject: solid black + screen-
  blend at composite time**, no keying at all. A light source has no hard
  edge to cut an alpha mask around; screen-blending a black-background image
  achieves a cleaner result than alpha compositing would, and sidesteps the
  whole keying question. (`activation_line.png` in the Talent Tree kit is
  the example — see the composited previews referenced in
  `docs/handoffs/talent_tree/`.)
- **Full-frame backgrounds: opaque, no keying, no alpha.** 1672×941 is the
  established delivery size for this project's screen backgrounds — match
  existing files in `Art/Backgrounds/` rather than the engine's own
  1920×1080 reference resolution.

### Drawn borders survive an authored alpha

`slice_spell_sheet.py` runs two passes that delete lines a generator drew
and nobody asked for: `erase_drawn_lines` (luminance, sheet-wide) and
`erase_frame_borders` (coverage, per cell).

The second runs on **every** sheet, including one delivered with its own
alpha. That was not always true, and the reasoning for skipping it sounds
right: a frame whose alpha the artist wrote is a frame whose every opaque
pixel was meant. It holds for the luminance pass — which would cheerfully
blank a bright horizon — and it fails for the border pass, because
`mud_blast.png` arrived with a box drawn around every cell *and* with the
alpha to make that box solid. "The artist authored it" was true of the
artefact as well as of the art, and every Mud Burst played a white rectangle
around its target. Frost Flare had produced the same complaint by the other
route.

Safe to run unconditionally because it does not ask what a pixel looks like,
only what its row does: edge to edge, with nothing beside it
(`FRAME_BORDER_ISOLATION`). Real art that spans a frame has neighbours.

## 3. `key_green_screen.py` usage

```
py tools/key_green_screen.py
```

Processes every kit in `KITS` that has files present in its source folder;
a kit with no source directory is skipped, not an error, so kits can be
delivered incrementally.

- **Grouped mode** (Hub only): sources named `name_1.png`, `name_2.png`,
  `name_3.png` become one output with three animation frames
  (`f0.png`, `f1.png`, `f2.png`), resampled to that kit's configured
  delivery size. This is what lets `HubBuildingAnimator` find extra frames
  to cycle at runtime.
- **Direct mode** (everything else): one source file in, one keyed file of
  the same name out, written to a `Processed/` sibling of the source
  folder, **no resampling** — the source resolution IS the delivery
  resolution, since these are baked into the scene once by `LoadSprite`
  (editor-time), never `Resources.Load`ed at runtime the way animated Hub
  buildings are.

The tool also force-corrects each output PNG's `.meta` — `textureType: 8`
(Sprite), `alphaIsTransparency: 1`. This isn't optional: a PNG Unity has
never seen imports as a plain Texture by default, `Resources.Load<Sprite>`
on a plain Texture silently returns `null`, and the affected slot just
renders nothing with no error anywhere. Only a `.meta` that's actually wrong
gets touched, so re-running the tool doesn't force a project-wide reimport.

## 4. Animated actor stances (`{stance}/f0..fN`)

A combat stance (`idle`, `attack`, `cast`, ...) is normally one flat file:
`Resources/Enemies/<id>/<stance>.png`. It can instead be a folder of frames:

```
Resources/Enemies/<id>/<stance>/f0.png
Resources/Enemies/<id>/<stance>/f1.png
...
```

Unlike everything else in this document, this is **not** baked into a scene
or a content asset at build time — `Core/StanceAnimationLibrary.cs` probes
for it at RUNTIME, the same `f{i}`-until-null idiom `SpellVfxPlayer` and
`HubBuildingAnimator` already use for spell VFX and Hub buildings. That means
dropping a new frame in, or adding `f5.png` to a 5-frame stance, needs a
content rebuild (so `ContentDatabase` knows about any new enemy) but **no
scene rebuild** — the frame count is discovered fresh every time the
Resources cache is cold, not fixed at scene-authoring time. A flat single
PNG (today's default for every stance on every enemy) resolves to a
1-frame animation automatically; nothing about an unanimated actor changes.

### 4a. The stance manifest — where the feet are, how it is timed

`Resources/StanceManifest.json` states, per actor, **where the figure's feet
sit inside its own canvas** (`groundLine`, in pixels up from the canvas
bottom), and per multi-frame stance, **how it is timed** (`secondsPerFrame`,
`impactFrame`, `soundFrame`). `StanceManifestLoader` reads it once;
`Domain/Stage/StanceManifest.cs` holds the parsing and defaults.

It exists because all three numbers used to be **inferred at runtime** — the
ground line by scanning alpha, the impact frame as `ceil(frames/2)`, the pace
as one global constant — and five of the eight golem-family stage bugs were
that inference being wrong. The whole frame's lowest opaque pixel is not the
feet: the golem's slam erupts an earth spike ~50px *below* its own, and
Shawn's idle plants a staff ~33px below his, so both were read as the floor
and both figures floated.

Rules worth knowing before you edit either the JSON or the art:

- **`groundLine` is per ACTOR, never per stance.** A character's feet do not
  move relative to their own art — only the effects drawn around them do.
  One value per actor is what makes "the figure cannot jump between poses"
  true by construction.
- **Re-slicing a sheet can invalidate it.** `StanceManifestValidationTests`
  re-measures the art and fails if an authored ground line has drifted more
  than 8px from it, naming the actor and both numbers. That test is the
  entire reason the manifest is an improvement rather than the same guess
  written down somewhere else.
- **Every multi-frame stance needs an entry.** A missing one silently falls
  back to the old midpoint guess, so the validator refuses it. Single-frame
  poses need nothing — timing is meaningless for a still.
- **Effect timing lives elsewhere and must agree.** A monster's `vfxSeconds`
  / `vfxImpactFrame` are in `ContentData/enemies.json`, and nothing derives
  one from the other on purpose (which of the two should move is a
  judgement). `EveryEnemyEffect_LandsWhenItsSwingLands` fails if they drift
  apart, so the golem's rock cannot quietly stop matching its arm.

The file lives in `Resources/` rather than `ContentData/` because it is read
at runtime rather than baked — and deliberately **not** under
`Resources/Content/`, which `ContentBuilder` deletes wholesale.

Two rules the tooling doesn't enforce for you:

- **Slice every stance of one actor — flat and animated, across every
  source sheet it draws from — in a single tool invocation.** The slicer
  sizes its shared canvas from whatever it's given in one pass; two
  separate invocations (even for the same actor) produce two different
  canvases, which makes the actor visibly resize/jump the moment that
  stance shows. `ActorArtAssertions.AssertOneCanvasSize` checks this for
  **every frame** of every stance an actor has authored — not just the four
  combat-driven stances, and not just frame 0 — and both `EnemyStageTests`
  and `PartyStageTests` run it over their own half of the roster.
- **Impact/sound frame timing has no authoring channel yet.**
  `StanceAnimationLibrary` computes a sensible midpoint default for both
  (same fallback `ImpactFraction` already uses for a spell with no
  authored frame). A future per-actor content field could override either
  without any engine change — see `StanceAnimation`'s own header comment
  for why that seam exists.

### 4b. Commissioning stance sheets from an image model

How the Beetle, Treant and Forest Nymph sheets were produced. Two stages, in
a project ChatGPT conversation with the reference images attached — never one
prompt per stance from cold, because nothing then holds the character still
between stances.

**Stage 1 — one 6-pose sheet.** A single 3x2 grid of six *distinct poses*:
idle, attack, whatever ability poses the kit needs, hurt, defeated. This
sheet's only job is to lock the character design. Give abilities that read
differently their own pose (the Nymph's heal and poison hex share nothing but
her silhouette, and one shared "cast" would have looked wrong for both).

**Stage 2 — six animation sheets, one per pose.** Upload the *cut single* from
Stage 1 as the image reference and ask for six frames animating that one pose.
Doing it from the Stage-1 pose rather than from words is what keeps the six
stances recognisably the same creature.

Both stages: transparent background, no text/labels/borders, no ground plane,
no floor or cast shadows, no particles or spell glow (VFX is composited in
engine later — see §5b), identical scale across frames, one facing direction
throughout. State the facing explicitly; a sheet came back with two of six
poses mirrored, which reads in-game as the creature flipping mid-fight.

#### Style: reference the rat, not the most recent sheet

`Art/Enemies/Giant_rat_sheet.png` is the house style — bold uniform dark
outlines, flat cel shading with two or three tones per colour area, small
palette, minimal internal texture. **Attach it as an explicit *rendering
style* reference** (say style only: not its subject, not its grey palette).
Describing the style in words does not hold; the model's default pull is
painterly.

The Treant and Beetle sheets came out painterly and over-rendered — soft
gradients, rim lighting, dense micro-texture — and were kept rather than
redone. They are the two most recent enemy sheets, so anything reaching for
"the most recent example" picks up exactly the wrong style. Do not use
`bog_witch_sheet.png` as a reference either: it is an older artifact with
baked-in captions, a background, floor shadows and glow, all of which current
sheets prohibit.

#### Direct the frames individually, or the sheet comes back static

Describing the *motion* ("a fast raking swipe, wind-up then follow-through")
reliably produces six near-identical copies of one pose. The model collapses
the whole arc into a single drawing. Describing each *frame* — "FRAME 3: full
coil, arm drawn all the way back; FRAME 4: THE STRIKE, full extension, the
biggest pose on the sheet" — forces six distinct drawings.

The difference is not subtle. The Nymph's idle went from 12% to 27% frame-to-
frame change on the same reference image, purely from the rewrite; her attack
from a standing pose repeated six times to 50%.

A static sheet is close to invisible in a thumbnail strip and obvious as a
number, so measure it. `tools/actor_stance_qa.py`'s onion-skin cell is the
committed way to see it (a static stance composites to one clean silhouette
instead of a smear). Rough bands from the sheets that animate correctly:
action ~35-50%, cast ~26-40%, idle ~18-28%.

#### Slicing: cut at the gutters, and reject strays by distance

The generator does **not** lay figures out on an exact grid. Slicing at even
thirds cut straight through bodies — 1635px of ink sat on the horizontal cut
line of the Nymph's defeated sheet, whose real gutter was at y=612 against an
even split of 512, amputating three frames' legs. `sheet_slicing.best_cut`
already solves this: it searches a bounded window for the emptiest row/column
and ties break toward the nominal split. **Use it. Do not hand-roll a slicer**
— this was reinvented from scratch once, badly, before anyone read that
module.

A neighbouring pose's foot or hand routinely pokes over the boundary into the
cell. Those fragments are *big* — a severed foot measured 5-10% of the body —
so a "keep components above N% of the largest" rule keeps them, which is how
four detached feet and a hand survived into a delivered set. Reject on
**distance from the main silhouette**, not size: contamination lands in dead
space (every real stray measured >40px clear), while genuinely detached art —
a loose leaf, a flying hair lock, the Treant's mushrooms — sits against the
body. Note also that erasing everything except the single largest component
is *also* wrong: it eats those mushrooms.

Do **not** reject a component for touching the cell edge. The figure
legitimately stands on the cell floor, and that rule deletes her.

#### Measuring "is it all the same size"

Cross-pose, use `sqrt(opaque area)` — the proxy `actor_stance_qa.py` captions
with and `ActorArtAssertions.AssertOneDrawScale` asserts on. Bounding-box
height is confounded by the pose (a crouch is shorter at identical draw
scale).

Comparing the *same* pose across sheets, area is the confounded one — it
balloons when hair or a cape fans out. The Nymph's six neutral frames read as
6.9% apart by area and 3.2% apart by height, and the height figure was the
true one. A scale "correction" was applied off the area number and had to be
discarded. Two further proxies tried and rejected: flower size (occlusion
changes it, reported 23%) and face width (a bowed head hides the face,
reported 50%).

Cheap guard against fooling yourself: build the comparison contact sheet with
**one** scale factor for every frame and a shared baseline. Fitting each
sprite to its own cell — the obvious way to build a contact sheet — makes
every frame a different display scale, so the image cannot show a size
difference even when one exists.

#### Art/ is not delivery

Everything above produces `Art/Enemies/<actor>/<stance>_frames/f0..fN`, each
frame cropped tight to its own content. That is **not** shippable, for two
reasons, and both are `pad_actor_frames.py`'s job (see its header):

- **One canvas per actor, not per frame.** `FightController.StageVisuals`
  sets `slotRect.sizeDelta = sprite.rect.size` with a bottom-centre pivot,
  and `groundLine` is one authored number for the whole actor. Per-frame
  crops make that number a different lie in every frame, so the creature bobs
  and slides as it animates. Every correctly delivered enemy has a single
  uniform canvas; `forest_warden` has six different ones and its manifest
  entry admits to the pop.
- **`delivery_scale`, because pixel size IS on-screen size.** There is no
  per-enemy scale in content. Both AI kits are generated on the same 512px
  cells, so delivered raw a beetle stands as tall as a golem. Pick the scale
  against the delivered roster's idle content heights — **and measure them,
  do not copy a number out of this file.**

  > **The table that used to live here was stale and caused a real
  > near-miss (2026-08-30).** It read `rat 226, beetle 241, bog_witch 282,
  > golem 284, treant 423, forest_warden 472`. Re-measured against the art
  > actually on disk, four of the six were wrong — bog_witch 313, golem 337,
  > treant 441, forest_warden 483 — by up to 19%. Acting on the stale `rat
  > 226` produced a `delivery_scale` of 0.834 that would have shrunk a
  > correctly-sized rat by a fifth; it was caught only because the number
  > was checked against the actual committed art before being applied
  > (that art measures 288, so the live 12-frame idle at 271–302 was
  > already right). A hardcoded measurement in a doc rots silently the
  > moment any actor is re-sliced, and every re-slice moves these. Measure
  > the target off the art you are matching, in the same command you set
  > the scale in:
  >
  > ```bash
  > python -c "from PIL import Image; im=Image.open(P).convert('RGBA'); b=im.getchannel('A').getbbox(); print(b[3]-b[1])"
  > ```

### `slice_actor_sheet.py`'s `ACTORS` manifest

Driven by a committed per-actor manifest (`ACTORS` in the script), not
one-off shell commands typed at the terminal and never recorded anywhere.
One actor entry lists every source sheet it draws from — each sheet's
grid (or, for an irregularly-spaced sheet like the rat's attack sheet,
explicit row `bands`), a row-major `names` list mapping grid cells to
output subpaths (`"idle"` for a flat stance, `"attack/f0"` for one frame
of an animated stance, `None` to skip a cell), and an `anchor` mode.
Because every sheet an actor draws from lives in one manifest entry, the
tool composites them together in a single pass — the "combine sheets into
one virtual grid image before slicing" workaround this section used to
describe is gone; the manifest *is* that combination.

**"Actor", not "enemy": this covers both sides of the fight stage.** A
party member's stance art (`Resources/Characters/<id>/`) and a monster's
(`Resources/Enemies/<id>/`) are resolved by one runtime path and held to
one set of invariants, so one tool produces both — an entry's `root`
picks the output tree and `source_dir` picks where its sheets live.
Before this, no committed tool wrote `Resources/Characters` at all, which
made Shawn's six battle stances — the most-seen sprite in the game — the
least reproducible art in the project.

- **Never scales an individual pose.** Only one literal `scale` per SHEET
  is allowed (always shrinking the larger sheet down — downsampling
  preserves quality; scaling up doesn't). `sqrt(opaque pixel count)`, not
  bbox height, is the pose-invariant proxy the tool measures by — see the
  script's own module docstring for the full rationale (this replaced an
  earlier pass that scaled by bbox height and made creatures visibly pulse
  size between animation frames).
- **`--suggest-scales <id>` measures and prints per-sheet mass ratios,
  then stops.** Nothing is auto-applied — a human reads the number and
  copies it into the manifest's `scale` field, where it shows up in a
  normal diff. Writing is refused outright if two sheets of one creature
  disagree >10% in measured mass and neither carries an explicit `scale`
  (the guard that would have caught the rat's attack sheet being drawn
  1.306x larger than its base sheet, had it existed at the time).
- **`anchor: "ground_band"` vs `"centroid"`.** `ground_band` (largest
  connected component, intersected with a thin band at the creature's own
  ground line) is the default for new work — it ignores detached debris
  (flying VFX) and raised limbs/tails that would otherwise drag a
  whole-mass centroid sideways. `centroid` is a faithful port of the
  tool's original behaviour, kept only where a creature's shipped art must
  reproduce pixel-identical (bog_witch, the regression control — see the
  comment beside its manifest entry before changing its anchor).
- **Aliases belong in the manifest**, not as manual file copies —
  `"aliases": {"attack": "cast"}` means the output is a byte-for-byte copy,
  preserving whichever shape (flat file or animated frame folder) the
  target has.
- **Reproduce before you improve.** When bringing already-shipped art under
  the manifest, the first run must regenerate it **byte-identical** — that
  is the proof the entry describes what actually produced the art, rather
  than something merely plausible. Accept ugliness to get there: `sheep`
  deliberately uses a plain `grid` even though explicit `bands` would be
  tidier, because a 4px sliver of keying noise at y=450..454 in
  `shawn.png` is swept in by the even grid and is baked into the shipped
  canvas height (366px, not 364px). Bands that exclude it give *better*
  output that is *not what shipped*. Clean it up as a separate, visible
  change if you want to — never as a silent side effect of adding an entry.
- **Run:** `python tools/slice_actor_sheet.py <id> [<id> ...]` or `--all`.
  Refuses to read from anywhere under `Assets/_Project/Resources/` (that
  would compound resample loss against already-processed output — how the
  original bad pass happened). Never adds or removes an output filename by
  itself; `--prune` deletes stray files the manifest no longer produces,
  otherwise they're only listed.

### `slice_spell_sheet.py`'s `VFX` manifest

Same idea as `ACTORS`, for spell/ability effects — and it exists because
every parameter used to come from argv, including the frame names, so how
the three shipped effects were cut survived nowhere. `frost_flare` and
`lightning_bolt` are verified entries: re-running them regenerates the
committed PNGs byte-identical.

**A VFX is NOT held to the actor rules, and must not be.** Two of them
invert:

- **No draw-scale band.** An effect is *supposed* to grow and fade —
  `frost_flare` legitimately spans 0.42×–1.44× of its own median mass.
  Applying the actor rule here would be the same category error as the
  bbox-height pass that broke the enemy art.
- **No baseline re-anchoring.** Where the effect sits inside its cell *is*
  the animation; a frame of sparks gathering at the top has no ground line
  to align to. The slicer cuts a plain fixed grid and preserves each cell
  as drawn.

What does hold: every frame resolves, all frames share one canvas, and no
frame is blank *after* the effect has started. A blank frame is legitimate
only as a lead-in beat (the golem's boulder opens on one); a blank one
mid-sequence means the luminance key ate a cell.

`HAND_ASSEMBLED` records VFX the tool did **not** produce and cannot
reproduce — `golem_boulder` is hand-built (`f2`==`f3` is a held peak,
`f4`/`f5` are a stepped alpha fade-out). Recording "we don't know how this
was made" is the point; a silent gap in the manifest reads as "nothing to
see here." Running the tool against such an id refuses rather than
overwriting.

> **Anything under `Resources/` that is `Resources.Load<Sprite>`ed at
> runtime must be covered by `StanceSpriteImporter`.** A PNG imported with
> Unity's default `textureType` returns `null` from `Resources.Load<Sprite>`
> — no error, no warning. `Resources/Spells/` was missing from that list
> and the golem's Boulder Slam shipped playing *nothing at all* for weeks
> (its frame probe broke on the first null). `Enemies/`, `Characters/` and
> `Spells/` are covered now; `Hub/` is handled separately by
> `key_green_screen.force_sprite_import`. A new runtime-loaded folder needs
> one of the two.

### Reproducibility is recorded, including where it fails

Three of the art tools now carry a committed manifest (`ACTORS`, `VFX`,
`PORTRAITS`) alongside `key_green_screen.py`'s `KITS` and
`slice_item_sheet.py`'s `SHEETS`. Where an entry has been **verified** —
re-running it regenerates the committed PNGs byte-identical — that is the
proof the recipe is the real one rather than a plausible guess.

Where it has *not*, that is recorded too rather than left blank, because a
silent gap reads as "nothing to see here":

- **`golem_boulder`** is in `slice_spell_sheet.py`'s `HAND_ASSEMBLED`. The
  tool refuses to regenerate it.
- **`Sheep`'s portraits** are marked `reproduces: False`. A fresh run
  produces a 1122×1360 crop where the committed `Shawn_neutral.png` is
  1122×1402 — so the shipped art came from different settings, an older
  version of the tool, or a hand edit, and nobody recorded which. Running
  the tool over that folder now **refuses** rather than silently replacing
  all six; `--check` renders to scratch and reports what *would* change
  without writing anything. That hazard was documented in the tool's own
  docstring for months and enforced nowhere.

### Visual QA before shipping an animation

`ScreenshotTool` runs in Editor Edit Mode and cannot capture a stance mid-
animation, so there was no way to actually *see* a new sheet-slicer output
before it shipped. `tools/actor_stance_qa.py --report <Resources/Enemies|Resources/Characters>
[--only <id>]` renders `tools/screenshots/actor_qa/<id>.png` (gitignored,
never committed): every frame of every stance thumbnailed with its canvas,
ground line, anchor line, and a `sqrt(area)/median` caption colour-coded
against the same tolerance band `ActorArtAssertions.AssertOneDrawScale`
asserts (shared by `EnemyStageTests` and `PartyStageTests`); the rightmost
cell of each stance row is an onion-skin composite of all its frames —
drift reads as a smear, a size pulse reads as concentric outlines. Its `--report` mode has no dependency on the slicer, so it can
render today's *committed* art as a "before" picture, turning a re-slice
into a demonstrable before/after rather than an assertion.

#### The redraw ratio

The onion skin shows *that* frames disagree; it cannot show whether the
disagreement is motion. Six silhouettes that differ in their fringes smear
much like six that differ in their pose, which is how the Forest Troll's idle
passed an eyeball check and still played as janky — feet planted to within a
pixel, centre held to within one, every runtime rule satisfied, and the
creature's mass moving 3.9% of its height while 13% of its silhouette was
replaced between adjacent frames.

So each stance row also reports **silhouette churn per step over centroid
travel**, and the run ends with the roster ranked by it. Around 1 means the
drawing changes about as much as the pose does. Measured against the roster
rather than picked: the Rat's attack, a real lunge, scores 0.7; the Beetle's
sealed shell, still and consistently drawn, scores 1.1; every six-frame idle
in the game scores near 3.

Travel is the **centroid's**, not the bounding box's — a box grows when a
branch is redrawn a little wider, and counting that as travel would let a
stance excuse its own churn. Frames are bottom-centre aligned onto a shared
canvas first, matching what the stage does, so a ragged crop is not reported
as movement.

A **looping** stance is held to a tighter bar (amber 1.6, red 2.5) than a
one-shot (2.5 / 4.0), the same asymmetry `StanceTiming.Steady` draws and for
the same reason: an idle that redraws twice as much as it moves is doing
something other than breathing, and an attack that does is an attack. The
per-band figures beside it say where the churn lives — `feet` is the one worth
reading, since a planted foot that gains and loses toes is the most legible
fault in a bad idle and is otherwise buried in a whole-figure average.

`--fail-over <ratio>` turns it into a gate. Off by default, and that is a
statement about the art rather than about the check: every looping stance on
the roster is currently over the red bar, so a gate shipped switched on would
refuse every build until the sheets were re-cut.

#### Do not wire a new looping stance on the QA number alone

The check above measures **shape churn against motion**. It does not measure
**the figure rocking sideways frame to frame**, which is a separate failure
mode with its own separate number, and shipping the Giant Rat's 12-frame idle
without checking it (2026-08-29) is why the rat visibly wobbled in a real
fight the moment someone actually played it, despite the redraw ratio (1.6,
amber but not red) having already been read and reported as "not perfect."
An amber number that gets mentioned in passing is not a gate; a number
nobody looked at twice is not a check.

Before calling a new or re-cut LOOPING stance done, in this order:

1. **Reproduce it, don't eyeball it.** Slice with `slice_actor_sheet.py <id>`,
   then `actor_stance_qa.py --report Resources/Enemies --only <id>` and
   actually open `tools/screenshots/actor_qa/<id>.png` — the onion-skin
   column and the per-frame anchor line are what a "looks fine to me" verdict
   is standing in for if you skip this.
2. **Check the anchor line is a single vertical line, not a smear.** This is
   the side-to-side check the redraw ratio does NOT make. Measure it
   directly if the picture is ambiguous — per-frame opaque bbox centre-x
   across the delivered PNGs (`im.getchannel("A").getbbox()`, average the
   left/right edges) should agree within a couple of px. The rat's idle
   measured a 27.5px spread before correction, 1.0px after — that gap is
   exactly what reads as "wiggly."
3. **If it fails step 2, nudge — don't rescale.** Recentre each outlier frame
   on the roster median with a per-frame `("stance/fN": (dx, 0))` entry in
   the actor's `nudge` table (dx = median_cx − that frame's own cx, rounded
   to an int). This is a horizontal reposition, not a resize, so it cannot
   introduce the pulsing a per-frame `scale` correction would — see the
   rat's own `nudge` entry for the worked example, with the measurement that
   produced every value recorded beside it. Re-slice and re-measure step 2
   after — a nudge is arithmetic, not a guess, and should close the gap
   close to fully.
4. **Read the redraw ratio, and say what it means out loud if it is not
   green.** Amber (1.6-2.5 for a loop) means "will probably still be visible
   as flicker/bob in motion" — say exactly that in the report handed to
   whoever asked for the art, not just the number. Red means don't ship it
   without saying so explicitly and getting a decision. A ratio this checks
   cannot fix (a genuine per-frame silhouette-height swing, distinct from
   the sideways drift step 2-3 fixes) needs new art or a different pipeline
   (see the rig, §9) — not a nudge, and not silence.
5. **Only then** add/update the stance's `StanceManifest.json` entry and
   consider the stance shippable.

## 5. Wiring checklist (new art → visible in-game)

1. Drop raw source file(s) in the kit's source folder, run the keyer.
2. Point the content JSON's `iconPath` (or equivalent field) at the
   `Processed/` file.
3. Rebuild content and/or scenes as needed:
   `run_tests_parallel.ps1 -BuildContent -BuildScenes` — this is also what
   syncs the regenerated `.meta`s back to main (see `docs/WORKFLOW.md` §9).
4. Commit the `.png` **and** its `.meta` together.
5. After a TestRunner build, double-check `Art/` actually diffed back to
   main — `LoadSprite` flips a texture's importer settings and can generate
   a fresh `.meta` for anything newly referenced, and if that diff doesn't
   sync back, the committed scene ends up pointing at a Sprite sub-asset
   that doesn't exist in main's copy of the file.

## 5b. Spell effects (`Resources/Spells/{id}/f0..fN`)

A spell's frames are cut from a sheet by `tools/slice_spell_sheet.py`, and
everything about how one LOOKS is one value in the content — see
`SpellPresentation`. Adding a spell is a manifest entry, a JSON block, and one
command; it should not require touching C#.

### The whole loop

```bash
python tools/slice_spell_sheet.py --new <id> --sheet <file.png>
```

Cuts the sheet, writes a preview, and prints the two blocks to paste: the
manifest entry and the `skills.json` `vfx` block, with `impactFrame` guessed
from the frame carrying the most ink. Then tune against the preview:

```bash
python tools/slice_spell_sheet.py <id> --preview
```

The preview renders the sequence at the speed content actually declares,
dissolve and all, to `tools/screenshots/vfx/{id}.gif`.

### The `vfx` block

| field | default | what it does |
| --- | --- | --- |
| `path` | — | `Resources`-relative folder of `f0..fN`. Empty means no visual. |
| `seconds` | `0.6` | How long the whole sequence takes. |
| `impactFrame` | `3` | Which frame (from 1) the blow lands on. The damage number, the flash and the hit-stop are all timed to it. |
| `anchor` | `target` | Where the art happens — see below. |
| `size` | `380` | Square box the art is fitted into, in reference-frame units. |
| `departFrame` | `0` | `travel` only: which frame (from 1) it leaves the caster on. Holds the wind-up in place instead of letting it drift. |
| `impactX` | unset | Where across its own frame the sheet actually strikes, `0`..`1` from the LEFT. |
| `impactY` | unset | Where up its own frame the sheet actually strikes, `0`..`1` from the BOTTOM. |
| `sfxPath` | — | `Resources` path to the clip that plays on impact. |

Anything left out takes its default. There are no sentinels to remember,
with one exception: `impactX`/`impactY` are unset at `-1`, because `0` is a
legitimate point (the frame's bottom-left corner) and a sentinel that
collides with a real value is a bug waiting for its first author.

### `impactX` / `impactY` — where the sheet hits

**State them, or the view guesses, and the guess is only right for one kind
of art.** Without a point the box's bottom edge goes on the target's ground
line, corrected by the lowest opaque pixel the sheet reaches once it has
landed. That is exactly right for an eruption drawn standing on a floor
(`golem_boulder`), and it shipped two visible bugs for everything else:

- `frost_flare`'s burst sits a sixth of the way up its frame with embers
  falling *below* it, so the measured "floor" was the embers and the strike
  rendered on the Giant Rat's **chest**;
- `mud_burst` is a lance fired flat — its impact is halfway up the frame and
  two thirds of the way **across** it, because the left third is reserved
  for the incoming bolt. Bottom-anchored and centre-aligned it detonated
  above the rat's head and to one side.

Same conclusion the stance manifest reached about actors' feet, for the same
reason: a scan believes whatever it finds, and what it finds is a spray of
sparks. Measured in the same direction, too — `impactY` counts up from the
bottom, like `groundLine`.

How to read one off a sheet: open its `impactFrame` and find the centre of
the burst — not the centroid of the whole drawing, which a bolt's shaft
drags upwards. `tools/screenshots/vfx/{id}_frames.png` is the contact sheet.

Both halves or neither: a partly-stated point is ignored at play time and
**fails the suite**, because silently reverting to the rule the spell was
authored to escape is the least visible way to be wrong.

The point is aligned to whatever the `anchor` aims at — the ground line, or
the body's middle. It is corrected on **both** axes, and the horizontal
correction flips with the sheet's mirroring.

### `anchor`

Where the art happens, which is **not** the same question as who the skill
hits. Two axes — whose body, and where on it — flattened into one word:

| word | meaning | for |
| --- | --- | --- |
| `target` | on the target's ground line | bolts, impacts, eruptions — almost everything |
| `caster` | on the caster's ground line | wind-ups, stomps, transformations |
| `target-centre` | on the target's midpoint | rings, binds, status glints — art drawn around a body |
| `caster-centre` | on the caster's midpoint | self-buff auras, shield bubbles |
| `travel` | flies caster → target, arriving at its ground line | a bolt that buries itself in the floor |
| `travel-centre` | flies caster → target, arriving at its **middle** | anything thrown flat across the stage |

Both `travel` words are mirrored when the caster is on the right.

`travel` versus `travel-centre` is the third axis this word carries, and it
matters more the taller the target: a Giant Rat is 675×306 of canvas, mostly
length, so its ground line and its middle are nowhere near each other.
`mud_burst` arrived at the ground line and detonated above the rat's head
until it was moved to `travel-centre`.

An unrecognised word falls back to `target` at play time and **fails the
suite** — `SpellVfxTests` refuses any anchor that does not parse, so a typo
cannot reach a player as a quietly misplaced explosion.

### Composing frames from cells

The manifest's optional `sequence` builds frames out of the cut cells, so a
six-drawing sheet can become a twenty-six-frame animation without new art:

- `{"from": "f0", "spin": 8}` — eight frames rotating a full turn
- `{"from": "f3", "hold": 4, "scale": (1.0, 1.08)}` — four frames, growing
- `{"from": "f5", "scale": (1.0, 1.04)}` — one frame, resized

Left out entirely, the cells **are** the frames one for one.

### A monster casting it

A monster's abilities are real skills, drawn weighted, in `enemies.json`:

```json
"abilities": [ { "skillId": "mud_burst", "weight": 2 } ],
"attackWeight": 3
```

Weights are **relative**, not probabilities — 2 against 3 is 40%, and adding a
third entry does not require rebalancing the first two. The basic attack is
always in the pool (`attackWeight`, default 1; `0` removes it), because a
monster whose every turn is a special reads as scripted rather than dangerous.

A monster reaches the whole `SkillEffect` vocabulary this way, and every skill
written for a character in future is available to monsters for free. Targeting
is side-relative: `DamageAll` cast by a monster hits your party, `HealParty`
mends its own side.

The telegraph is derived, never authored — its icon from the skill's effect and
status, its scope (`one` / `your whole party` / `itself` / `its allies`) from
the effect alone, and its magnitude from the same formula the resolution runs.
An author who could disagree with any of those could make the telegraph lie.

A skill id that names nothing **fails the content build**. At play time the
adapter would drop it with a warning, and the symptom of that is a boss quietly
easier than authored — which does not look like a bug.

The old `skillName`/`skillPower`/`skillChance` trio still works and still means
what it did; internally it becomes the same two-entry weighted pool. Migrating a
monster is a content decision, not a plumbing one: `"attack x 1.8"` has no
equivalent skill id until somebody writes the skill it should have been.


### Multi-target

An effect that lands on more than one thing draws on each of them. The beat
carries `SplashTargets` and the pool holds one member per stage slot; nothing
in content asks for it, because "this skill hits everything" is already said by
`effect: DamageAll`.

### Art that is NOT reproducible

`Assets/_Project/Art/Sheets/hand_assembled.json` names sequences the slicer must
never write over — hand-cut frames with held duplicates or stepped fades that
re-running the tool does not restore. The slicer refuses them, and
`HandAssembledArtTests` pins their bytes so a clobber from any direction fails
the suite. See `docs/INCIDENTS.md`.


## 6. Per-kit README index

- `Assets/_Project/Art/UI/Hub/README.md`
- `Assets/_Project/Art/Items/Relics/README.md`
- `Assets/_Project/Art/UI/TalentTree/README.md`

Each documents its kit's exact expected filenames and any kit-specific
sizing/tiling requirements.

## 7. Outstanding tracker

- **`Relics.png`** — the Relic screen's background, 1672×941, opaque, no
  keying needed. `SceneBuilder` already looks for this exact filename in
  `Art/Backgrounds/`; dropping the file in is the entire remaining step.
- ~~**Talent Tree kit wiring**~~ — done since Phase 7 (2026-08-01): the art
  (`orb_lit`/`orb_unlit`, `branch_tile_set`, `activation_line`) is wired
  into `SceneBuilder.Talents.cs`/`TalentController`. A later pass (the
  full-bleed/tree-shape rework) added a root crest and angled limbs built
  from the same kit; a dedicated `trunk_base.png`/`branch_fork.png` would
  replace that placeholder without touching anything else, but nothing is
  blocked on it.

## 8. Model and tool licences — read before adding an AI step

This is a **commercial** project, so a non-commercial model anywhere in the art
pipeline is a real problem, not a technicality. The traps below are all cases
where the obvious choice is the wrong one, usually because a permissive badge
on the code repo hides restrictive terms on the *weights*.

### Do NOT use

| Item | Why |
|---|---|
| Depth Anything **V2 Base / Large / Giant** | Weights are **CC-BY-NC**. The GitHub repo is Apache-2.0, which misleads — and most tutorials and ComfyUI workflows default to Large. |
| **BRIA RMBG-1.4 / 2.0**, including `rembg -m bria-rmbg` | **CC-BY-NC**; commercial use needs a paid agreement. `rembg` itself is MIT, but its licence does **not** propagate to the model weights it downloads. |
| **FLUX.1 Fill [dev]** | Non-commercial licence. (`FLUX.1 schnell` is Apache-2.0; the *Fill* variant is not.) |
| **3D Photo Inpainting** (vt-vl-lab) | MIT main code, but bundles EdgeConnect under **CC-BY-NC**. |
| **Apple Depth Pro** | `apple-amlr`; commercial terms unresolved upstream. |
| **Stable Video Diffusion**, **HunyuanVideo** | Revenue-threshold and geographic restrictions respectively. |
| Any GitHub repo with **no LICENSE file** | All rights reserved by default. Several popular 2D-parallax Unity repos are in this state. |

### Safe, and what to reach for

- **Depth maps:** Depth Anything **3** (Apache-2.0), or Depth Anything V2
  **Small** (Apache-2.0), or **Marigold** (Apache-2.0).
- **Inpainting:** **LaMa** via **IOPaint** (both Apache-2.0). Better than SD
  inpainting here because it extends existing texture rather than inventing a
  new subject, which is what keeps a fill stylistically identical to painted art.
- **Cut-out:** `rembg` (MIT) **pinned explicitly** to `birefnet-general` (MIT)
  or `u2net` (Apache-2.0). Never leave the model unpinned.
- **Upscale:** Real-ESRGAN (BSD-3). Prefer the `anime_6B` model for painterly
  work — the default photo model de-noises brush texture away.
- **Video:** Wan 2.2 or LTX (both Apache-2.0), plus FFmpeg to make a loop seamless.
- **Audio:** Sonniss GDC bundle, Kenney (CC0), ChipTone (CC0 output).

**GPL/AGPL authoring tools used offline** — Krita, GIMP, Blender, chaiNNer,
Upscayl, ComfyUI, Audacity, FFmpeg — do not touch this game's licence. The
copyleft attaches to the software, not to the images or audio it produces. Just
never bundle or link their code into the shipped build.

## 9. Skeletal rig pipeline (Unity 2D Animation) — per-creature workflow

The frame-sheet pipeline above (§4) hit a structural ceiling on the Giant
Rat: a 6-frame generated sheet flickers (silhouette churn outruns motion —
every looping stance sits over the QA red bar in §4's "redraw ratio"), can't
hold size/style between generations, and can't produce a real attack (no
articulation, no jaw). Parts cut from ONE approved drawing and re-posed by a
bone rig are consistent by construction, and frame count becomes free —
that is what this pipeline buys, at the cost of an actual rig to author.

**A hybrid, not a replacement.** `RigLibrary.Resolve(folder)` returns null
for any folder with no rig, and the entire §4 frame-sheet Image path stays
byte-for-byte unchanged for those creatures — see
`FightController.StageVisuals.cs`'s `RefreshCombatantSprite`. Only the rat
resolves during the pilot; every other creature's frame-sheet art is
untouched and does not need to migrate. Keep a creature's old `f0..fN`
folders even after it ships a rig — nothing deletes them, and they are the
fallback if the rig is ever pulled.

### What a rig actually is here

- **`Assets/_Project/Resources/Rigs/<Root>/<id>/rig.json`** — bones (a
  tree, root at the feet) and parts (one polygon + rigid single-bone
  weight each, cut from the bind-pose drawing). Authored by
  `tools/rig_actor.py`'s `RIGS` manifest (mirrors `slice_actor_sheet.py`'s
  `ACTORS` pattern) from a green-key bind-pose image the owner supplies —
  see §4's own reject-checklist (tail clear of body, 4 separated legs,
  visible neck, no painterly edges, no mirroring, flat green) for the art
  brief, which applies to a bind pose exactly as it does to a stance frame.
  Parts are a HARD PARTITION of the bind-pose pixels (each pixel owned by
  exactly one part, later `order`-rank wins on a tie), which means a part
  that swings away from its neighbour opens a hole at bind pose's own cut
  line — the neck under a rotating head, most visibly. The body's own
  `backing_px` manifest entry regrows the body texture UNDER a named
  neighbour (via a bounded-radius nearest-fill, not true inpainting) so
  that gap reveals fur instead of the stage background. Either a plain int
  (grows under every neighbour equally) or `{"default": N, "<part>": M}`
  (a bigger radius for one part specifically, clipped to that part's own
  bind footprint so it cannot bleed into an unrelated part sitting nearby)
  — see the rat's own manifest entry for a worked example, sized to the
  attack clip's actual peak rotation rather than guessed.
- **`.../rig.prefab`** — generated by `Editor/Rigging/RigImporter.cs` +
  `RigPrefabBuilder.cs` from `rig.json` + the imported atlas: a bone
  Transform hierarchy (`bones/`, matching rig.json's tree, each bone a
  GameObject named exactly as rig.json names it) and one
  `SpriteRenderer`+`SpriteSkin` per part (`parts/`), bound rigidly to its
  own bone. Destructively regenerated (`RigBuildPilot`/`tools/build_rigs.ps1`),
  never hand-edited, stable GUID via a preserved `.meta`.
- **`.../animations.json`** — per stance, a `durationSeconds`/`impactAt`/
  `soundAt` (seconds — a rig has no frame count to author impact/sound
  AGAINST, unlike `StanceManifest.json`'s frame-indexed timing) and a set
  of per-bone tracks (`{bone, keyframes:[{t, deg, dx, dy}]}`). Rotation is
  the primary vocabulary — every part on a rig built this way binds to
  exactly one bone with weight 1.0, so a swing is a chain of per-bone
  rotations around each bone's own pivot, the same vocabulary any 2-bone
  skeleton animates in elsewhere. `dx`/`dy` are optional (source px, +y
  up; JsonUtility reads an absent field as 0, so every clip authored
  before they existed still parses as rotation-only) and exist for the
  one case rotation-about-a-pivot cannot express: a bone whose pivot sits
  away from the part's own visual mass — the rat's root-at-feet body,
  specifically — can only swing from a rotation, never rise and fall the
  way a chest actually does when it breathes. Keyframes ease with
  smoothstep (`p*p*(3-2p)`, `RigBoneTrack`'s sampler default), not
  linear: velocity is zero AT every keyframe, so a looping clip wraps
  with no seam and a mid-clip keyframe has no mechanical corner, while
  mid-segment velocity still peaks 1.5x the linear rate — a sharp attack/
  hurt impact keyframe keeps its snap. Parsed by
  `Domain/Rig/RawRigManifest.cs` + `RigAnimationResolver.cs` (engine-free,
  EditMode-testable), loaded by `Core/Rig/RigManifestLoader.cs`
  (`Resources.Load` + cache, same posture as `RigLibrary`/
  `StanceManifestLoader`). **This file is the sole timing authority for a
  rig actor** — `StanceManifest.json` is not extended with a rig branch;
  the two manifests stay fully separate rather than one file learning two
  incompatible meanings for the same fields.
- **No jaw bone, no bite.** A rig's bone set is whatever `tools/rig_actor.py`
  cut apart from the bind pose, and there is no obligation to give a
  creature more joints than its silhouette actually shows independently
  moving parts for. The rat's `attack` clip is a whole-head lunge rather
  than a jaw opening, because the bind pose never separated a jaw from
  the head in the first place — decide what a creature's attack CAN be
  from its actual bone tree, not from what a generic "bite" animation
  would need.

### How a beat actually plays a rig

`Core/StancePerformance.cs`'s `IStancePlayback` is the one seam
`FightBeatPlayer` drives regardless of art style: `WindupSeconds`,
`TotalSeconds`, `Windup()`/`FollowThrough()`/`Release()`,
`ResetToRest()`. `FrameStancePlayback` wraps the existing
`StanceStepper`/`FrameHoldCurve` frame-walk unchanged;
`RigStancePlayback` samples a `RigStanceClip` through
`Core/Rig/RigStancePlayer.cs` (the rig twin of `StanceStepper` — same
beat clock, same per-frame `abandon` poll) and applies the result to a
`Core/Rig/RigActor.cs` (a bone-name → Transform map built by walking the
already-instantiated prefab, not baked into it — a bone-mapping change
never needs the destructive prefab rebuild). `FightController.PlaybackFor`
picks one or the other per combatant, mirroring exactly how
`RefreshCombatantSprite` already picks the frame-sheet Image path or the
rig path.

Idle breathing, hit flash and the defeated fade all reach a rig actor
through the same call sites the frame-sheet path already used
(`StepIdleFrame`/`FlashCombatant`/`FadeTheFallen` in
`FightController.StageVisuals.cs`), running ALONGSIDE the existing
uGUI-slot transform breath/flash/fade rather than replacing it — the two
animate entirely different Transforms (a rig's own bones and
SpriteRenderers under its world slot, versus the now-hidden Image's own
RectTransform), so both can run for a rig-resolved combatant at no cost.
A rig actor's own hit flash needed a dedicated shader
(`Resources/Shaders/RigHitFlash.shader` + `.../Materials/RigHitFlash.mat`):
`SpriteRenderer.color` multiplies the texture exactly like `Image.color`
does, so a white tint is the identity and changes nothing — the same
reason `UIHitFlash.shader` exists for the Image path. Its death fade needs
no shader at all, since a fade is pure alpha and the default sprite
material already blends that correctly.

### The workflow, step by step

1. Owner regenerates a bind pose (splayed side-profile, flat `#00FF00`,
   bold-outline cel style — see §4's reject-checklist) → commit under
   `Assets/_Project/Art/Rigs/<id>_bindpose.png`.
2. Add a `RIGS` entry to `tools/rig_actor.py`; `--preview` to sanity-check
   the cuts and bone placement before committing to them.
3. `python tools/rig_actor.py <id>` → `atlas.png` + `rig.json`.
4. Author `animations.json` by hand (copy the rat's as a starting shape;
   edit bone names/timings to match the new rig's own bone tree) — idle
   (looping), attack, hurt, defeated at minimum; more stances as the
   creature's kit needs them.
5. `powershell tools/build_rigs.ps1` (imports + builds the prefab,
   sentinel-gated the same way `ContentBuilder`'s steps are).
6. `powershell tools/test.ps1 combat,art` for the fast slice, then
   `powershell tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigStageTests`
   to confirm the rig actually renders and its bones actually move —
   headless PlayMode cannot see either.
7. `powershell tools/rig_qa.ps1` — samples every clip through a real
   camera+SpriteRenderer harness (`RigCaptureTests`, PlayMode, graphics-
   gated the same way `RigStageTests` is) and assembles a GIF, a contact
   strip and an onion-skin per stance, plus the same churn/redraw metrics
   §4's `actor_stance_qa.py` already gives the frame-sheet roster
   (`--out-dir`/`--no-guides` keep a rig's artifacts out of the frame-sheet
   roster's own report). THE REVIEW SPLIT this is built around: the
   strips, onion skins and metrics are pixels-and-numbers, so Claude can
   read those directly; only a human can judge whether a GIF's TIMING
   reads as the intended motion (breathing, not swaying; a snap, not a
   pop) — that judgment doesn't automate, and one `rig_qa.ps1` run
   produces both halves so nobody has to choose between them. A capture
   that LOOKS wrong in a still is worth confirming against the saved PNG's
   own raw pixel data before trusting it — a thin light line spotted in a
   zoomed crop during the idle rework turned out to be a display-side
   compression artifact with zero corresponding pixels in the file itself.
8. Owner review of a real fight too
   (`tools/screenshot.ps1`'s underlying capture, or just playing it) —
   `rig_qa.ps1` captures clips in isolation and cannot see beat-driven
   slot motion (recoil, punch, travel) layered on top; only a live fight
   shows that.
9. `powershell tools/run_tests_parallel.ps1` before committing.

### Outstanding on the rig pipeline

- **The rat's rig is temporarily disabled (2026-08-29).** It is mid-overhaul
  (a from-scratch hand-skinned replacement was attempted and set aside; the
  checked-in prefab is the older auto-cut-parts one), and the rat shipped a
  new flat 12-frame idle sheet in the meantime
  (`Giant_rat_idle_sheet_12_frame.png`, sliced via `tools/slice_actor_sheet.py`
  into `Resources/Enemies/rat/idle/f0..f11`). `RigLibrary._temporarilyDisabled`
  forces `Resolve("Enemies/rat")` to null, so the rat renders through §4's
  frame-sheet path like every other creature until an entry is removed.
  Nothing under `Resources/Rigs/Enemies/rat/` was touched. `RigStageTests`,
  `RigCaptureTests`, `RigRawSpriteTests` and `RigImportIntegrityTests` are
  `[Ignore]`d for the same reason.
- **Rig visual QA is built (`tools/rig_qa.ps1`, see the workflow above),
  but the redraw-ratio bars it inherited from §4 were calibrated on
  frame-sheet content.** The rat's re-authored idle scores amber (2.1 vs a
  1.6/2.5 green/red split) rather than green, and it isn't yet clear
  whether that is the metric's own denominator (a subtle breathing bob's
  silhouette-centroid travel is small by construction, which inflates
  churn-per-travel even when nothing is actually wrong) or a real, if
  small, inefficiency in how much redraws per step. Worth a second rig's
  idle to see whether amber is where every rig settles or the rat's own
  is an outlier.
- **Shadow and intent-badge placement are not rig-aware.** Both still
  measure the OLD frame-sheet idle art's pixel bounds
  (`ContentCentreFractionForActor`/`ContentTopForActor` in
  `FightController.StageVisuals.cs`) rather than the rig's own bind-pose
  renderer bounds. Harmless for the rat today (nothing visibly wrong has
  been reported), but it is a special case riding on stale art rather than
  the generalised fix a second rig creature would need.
- **`StanceManifest.json` gained no rig branch**, unlike the original plan
  for this pilot. `animations.json` turned out to be a complete, separate
  timing authority on its own — giving `StanceManifest.json` a second,
  differently-shaped meaning for the same fields (`groundLine` forbidden,
  `steady` ignored, frame-indexed `impactFrame` reinterpreted as seconds)
  would have made ONE file express two incompatible schemas rather than
  keeping two small files each fully coherent on their own.
- **`tools/rig_qa.ps1` has a flaky exit-code race with `graphics_tests.ps1`'s
  own results-file check.** The underlying `RigCaptureTests` run can pass
  for real (confirm in `%TEMP%\pp-gfx-results.xml`, or Unity's own default
  `.../TestResults.xml` if the `-testResults` path didn't get honoured —
  seen once against a project path with an apostrophe in it) while
  `rig_qa.ps1` still reports failure and skips GIF assembly. Frames are
  copied back to main either way (that step runs before the exit-code
  check), so the workaround is to confirm the capture actually succeeded
  and then run `rig_clip_qa.py` / `actor_stance_qa.py` by hand over the
  already-copied frames — not to re-run the whole capture. Not root-caused
  or fixed yet.
