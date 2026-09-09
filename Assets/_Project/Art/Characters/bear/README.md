# Bjorn — the bear veteran, nine key stills

Delivered 2026-09-07. The third party member with battle art after Shawn and
Odette, replacing `placeholder_brawler`. STR/CON Tank, and the owner's
favourite bear design — see `output/bear-veteran-v4/prompt.txt` for the full
accepted prompt; this file only summarises it.

## Source

`sheet_poses.png` — 1536x1024, 3x2, copied verbatim from
`Assets/_Project/Art/Sheets/bear_veteran_sheet-v4.png` (the fourth generation;
v2/v3 beside it in `Art/Sheets/` are earlier attempts, left alone — the owner
decides what happens to those copies). The brief was a style-transfer pass:
redraw the owner's favourite bear design (image 1) in the flat, spare,
strong-outline rendering Odette and Shawn's sheets established (images 2 and
3), keeping the bear's own silhouette, copper pauldron/bracers, red scarf and
paw-emblem tabard, facial scar and paw-emblem hammer untouched. The prompt
also asked for "transparent background with real alpha, no painted
checkerboard" and got the opposite — the delivered file is RGB with no alpha
channel at all, a baked light checkerboard (~247-253 grey/white) standing in
for it, same as the owl's and the treant's sheets before it.

Cells, row-major: idle (standing, hammer down), attack (hammer swing), cast
(roaring, paw raised), hurt (flinch, arm up), defeated (lying down), victory
(hammer raised overhead) — the six stances `FightSession.Stances` drives for
a party member. The bear faces RIGHT in every cell (nose/attack direction
both point right — confirmed by eye against the sheet), matching
`battleSpriteFacing: "Right"`.

## Keying

`white_flood --pocket-max-area 4000`, same setting the owl and treant needed
and for the same reason: the checkerboard is baked into the RGB rather than
carried as alpha, and the cape, the gap between the raised arm and the body,
and the space under the hammer head each enclose a checkerboard pocket well
over the 200px default speck cap. Checked programmatically after slicing —
scanned every delivered still's opaque pixels for near-white
(RGB >= 235), near-neutral (channel spread <= 6) pockets that would mean a
checkerboard survivor: five of six stances came back with zero suspect
pixels, `defeated.png` with exactly one (a single antialiased highlight
pixel on the armor rim, not a pocket). `pocket-max-area` did not need
raising.

## Sizing

Sliced once at `--delivery-scale 1.0`: idle content height measured 446px.
Target is 1.10x Shawn's currently delivered idle bbox height — re-measured
directly rather than trusting the sheep README's stated 349px, which reads
**350px** off the committed `Resources/Characters/sheep/idle.png` alpha
bbox (a 1px gap from the README, immaterial to the target). Target = 1.10 x
350 = 385px. Re-sliced at `385/446 = 0.8632286995515696`; the written
`idle.png` now measures exactly 385px, a head taller than the sheep, as a
STR/CON frontliner should.

**Ceiling check.** `Domain/Stage/FightStageAnchors.cs`'s ceiling comment
names the golem as the tallest actor on record at 384px above its own
manifest ground line, with room to spare under the enemy plate (head lands
around y=72-90 against a plate edge at 96-108). The bear's 385px is 1px over
that reference point — inside the same margin the golem already clears by
12-36px, not a new binding constraint — so the delivery scale was not
dropped to 1.0. `FightScreenTests` and `PartyFormationCaptureTests` both
pass with the bear's art on disk (see the delivery commit's test run).

## Accepted metrics

Filled area (opaque pixel count) and fur px (brown-fur-hued opaque pixels,
isolated by hue/saturation from the copper armor, red cape and cream trim)
against idle, plus the slicer's own `sqrt(largest-component-mass)`:

| stance | filled area / idle | fur px / idle | sqrt(mass) |
|---|---:|---:|---:|
| idle | 1.00 | 1.00 | 285.3 |
| attack | 0.95 | 1.06 | 278.1 |
| cast | 1.21 | 1.26 | 302.1 |
| hurt | 0.94 | 1.05 | 277.1 |
| defeated | 0.77 | 0.86 | 249.7 |
| victory | 0.93 | 1.12 | 275.8 |

Cast sits over the usual 0.90-1.10 band on filled area — it is the roaring
pose's fully outstretched raised arm and open stance widening the canvas,
not a body redraw at a different scale; fur px rises with it in proportion,
which is what a genuine pose spread looks like rather than a resize. Defeated
legitimately loses mass, same as every other actor's collapsed pose.

## Provenance

**Reproducible — `recipe.json`, beside this file.** Replay it with:

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Characters/bear/recipe.json
```

Verified 2026-09-07: the replay reproduces all six committed stills and
`Resources/StanceManifest.json`'s `Characters/bear` entry byte for byte
(sha256-compared before/after), which is what earns this actor
`groundLineSource: "slicer"`. `groundLine` is 74.

## Judgements made, not measured

- **`breath: 1.0`** in `StanceManifest.json`'s `Characters/bear` entry is
  copied, not tuned. Shawn's own entry (`Characters/sheep`) carries no
  `breath` override at all, which means the runtime default of 1.0 applies
  to him — this makes that same number explicit for the bear rather than
  authoring a new one. No `hover` block: the bear stands on the ground like
  Shawn, unlike the owl.
- **Display name "Bjorn"** is the owner's call and is provisional, same
  status Odette's stats/skill kit carried at her own delivery.
- **Portrait was borrowed, now stopgapped.** `portraitPath` used to stay
  `Portraits/sheep` verbatim from `placeholder_brawler`, showing Shawn's
  neutral portrait for Bjorn too. See the Portrait section below for what
  replaced it.
- **Skill ids kept as `placeholder_brawler_*`.** Renaming them would touch
  saves and tests for no visible gain today, the same call Odette's delivery
  made for her `placeholder_caster_*` skills. A design pass on stats, skill
  ids and a STR-scaled kit is a separate job from seating the bear in his
  own art.

## Delivery

```bash
python tools/slice_actor_sheet.py --sheet Assets/_Project/Art/Characters/bear/sheet_poses.png \
    --actor Characters/bear --stances idle,attack,cast,hurt,defeated,victory \
    --key white_flood --pocket-max-area 4000 --delivery-scale 0.8632286995515696 --prune
```

Canvas 486x467, `groundLine 74`, idle content height 385px against the sheep's
measured 350px (1.10x, by design).

## Portrait

No painted portrait exists for Bjorn yet. Until one is commissioned and run
through `tools/remove_portrait_backgrounds.py` (the keyer for painted
portrait sheets — this is not that), the dossier plate shows a stopgap crop
of his own idle stance still, made by `tools/portrait_from_stance.py`:

```bash
python tools/portrait_from_stance.py --still Assets/_Project/Resources/Characters/bear/idle.png \
    --out Assets/_Project/Resources/Portraits/bear.png --height-fraction 0.20
```

The bear faces right with his head sitting right of his own torso/hammer
centre, so centring on the full-figure bbox (as an earlier version of this
tool did) put the muzzle outside the frame; the crop is centred on the
HEAD BAND's own alpha extent instead — the top slice of the figure's alpha
bbox, its horizontal span measured on its own. The band's width (plus a
small margin) sets the crop width, and the reference portrait's aspect sets
the crop height from that, so the band is always whole and centred. Nothing
grows to keep the rest of the pose in frame: below the band the crop is
left to clip at the left/right edges, which is why the delivered plate cuts
into the left pauldron and drops the arm bracer and hammer.

Bjorn's build is stout enough that the tool's own default `--height-fraction`
(0.45) puts the band's bottom row already past the shoulder pauldrons' widest
point — at that width, matching the portrait's aspect makes the crop tall
enough to reach the hammer head and boots. `--height-fraction 0.20` keeps
the band to head, ears, scar and scarf, ending just as the round shoulder
guard comes into view; checked by hand against 0.25-0.35 first, where the
hammer's handle starts entering the frame at the bottom-left corner from
about 0.28 up.

Crop box (source px, against `idle.png`'s 486x467 canvas): `(132, 0) -
(334, 250)`, size 202x250. Native crop 202x250, upscaled 5x with LANCZOS to
1010x1250 (the largest whole-integer multiple that doesn't overshoot
`Portraits/sheep.png`'s 1122x1402 — see the tool's header for why an
integer factor beats resampling straight to the target's exact pixel
size). **This is soft** — a 5x upscale off a ~200px-wide native crop is a
real quality loss next to a painted portrait — and that is accepted "for
now." The plate shows both ears, the whole muzzle, the scar and the scarf,
whole and centred, and the top of the near shoulder pauldron; the hammer
and both feet fall entirely outside the frame **by design** — this crop
trades full-figure coverage for a face that actually reads at dossier size
(~107px), which the previous near-full-body crop did not.
`characters.json`'s `portraitPath` for `bear` is `Portraits/bear`; replace
this file and stop pointing at it the moment a painted portrait lands.

## Second sheet: the Slam kit (2026-09-09)

The owner drew a second sheet for Bjorn's Slam skill:
`Assets/_Project/Art/Sheets/bjorn_combat_sheet-v1.png`, 1254x1254, 2x2 grid,
RGB with a baked WHITE background (no checkerboard this time, unlike
`sheet_poses.png`'s ~247-253 grey/white checker). Copied verbatim into this
folder as `sheet_slam.png`.

Three of its four cells become three additional stances of this same actor.
Cell 2 is disregarded entirely, by the owner's own call:

| cell | pose | stance |
|---|---|---|
| 1 (top-left) | running forward, hammer trailing low | `rush` |
| 2 (top-right) | -- | disregarded, not sliced |
| 3 (bottom-left) | hammer held high overhead | `overhead` |
| 4 (bottom-right) | hammer brought down in the slam | `slam` |

Bjorn faces RIGHT in every used cell, confirmed by eye against the sheet,
matching `battleSpriteFacing: "Right"` and the first sheet's own convention.

### Why this needed a tool change, not a second composite sheet

`slice_actor_sheet.py`'s one-shared-canvas invariant is per ACTOR, not per
sheet: every stance an actor ships has to sit on one canvas at one scale with
one ground line (`ActorArtAssertions.AssertOneCanvasSize`,
`StanceManifestValidationTests`). The slicer used to size and anchor its
canvas from a single sheet's cells only, so slicing the new sheet as a
separate run would have produced a SECOND canvas and a second scale for the
same actor — exactly the failure mode this file's own §4a in
`docs/ART_PIPELINE.md` warns about. Hand-pasting the three new cells into a
composite sheet alongside the old six was the other obvious option and was
rejected for the same reason the spell slicer's own multi-source `sources`
convention exists: a hand-assembled source is not reproducible from anything
committed, and this actor already carries a `recipe.json` earning it
`groundLineSource: "slicer"` — silently downgrading it to an
unreproducible composite would have thrown that away.

`tools/slice_actor_sheet.py` now accepts more than one `--sheet`, each with
its own `--grid`/`--stances`/`--key`/`--pocket-max-area`/`--delivery-scale`,
gathered into one flat list of named pieces before the canvas is ever sized
— see the tool's own module docstring, "More than one sheet, one actor". A
one-sheet actor (every other recipe committed today: owl, treant, the ram)
replays through the exact same code at n=1, unchanged; this is proven by
`tools/slice_actor_sheet_test.py` and by replaying all four existing recipes
during this change (byte-identical against the committed art in every case).

### Scale derivation — the numbers

A second AI generation of the same character is never drawn at the first
generation's pixel size, so the new sheet needed its own `--delivery-scale`,
derived from a feature that should be a constant absolute size regardless of
pose or generation: the hammer head. Its copper trim shares a hue with the
shaft/armor, so instead of the trim or an axis-aligned bbox (which shifts
with the head's rotation from pose to pose), the isolated feature was the
head's own CENTRAL DARK-CHARCOAL FACE — the one part of the head with a
color (neutral, low-saturation grey) nothing else on the bear shares — masked
by hue/saturation, holes from the specular highlight closed with a small
morphological dilation, then measured two ways: its PCA-oriented long/short
axis extents, and its raw pixel area.

Measured on the ORIGINAL, undelivered sheets (native pixels, before any
`--delivery-scale`):

| sheet | cell | long axis | short axis | geometric mean | area (px) |
|---|---|---:|---:|---:|---:|
| `sheet_poses.png` | attack | 139.7 | 130.3 | 134.9 | 5520 |
| `sheet_poses.png` | idle | 138.3 | 142.4 | 140.3 | 4416 |
| `bjorn_combat_sheet-v1.png` | overhead | 156.8 | 145.8 | 151.2 | 7183 |
| `bjorn_combat_sheet-v1.png` | slam | 156.8 | 171.7 | 164.1 | 7406 |
| `bjorn_combat_sheet-v1.png` | rush | 154.0 | 168.5 | 161.1 | 6085 |

Old-sheet average (attack, idle): geometric-mean 137.6. New-sheet average
(overhead, slam, rush): geometric-mean 158.8. Axis-based ratio
137.6 / 158.8 = 0.8665; the area-based cross-check
(`sqrt(old_area / new_area)`) gives 0.8492 — about 2% apart, which is the
manual crop-box-margin noise in an by-eye-cropped measurement, not a real
disagreement. Averaging the two: ratio ~0.858.

    new_delivery_scale = old_delivery_scale x ratio
                        = 0.8632286995515696 x 0.858
                        ~= 0.74

**0.74 was used exactly**, then checked two more ways before touching real
art: (1) a side-by-side composite of the committed `idle.png`/`attack.png`
next to a SCRATCH-sliced `rush`/`overhead`/`slam` at 0.74 (never against
`Resources/Characters/bear` first — that is real, delivered art, and this
tool is destructive) showed consistent body/head/boot proportions across all
five figures by eye; (2) `sqrt(LCC-mass)` for the three new stances at 0.74
came out 303.6 / 312.6 / 289.4 — squarely inside the 285-312 band the
existing `cast` stance (302.1) already occupies, which is the right
comparison, since `rush`/`overhead`/`slam` are all wide, extended-limb poses
like `cast` rather than close-in poses like `idle`/`attack`. No further
adjustment was made.

### Canvas, ground line

Canvas grew from **486x467 to 512x536** (width +26, height +69) — mostly
upward, to fit the `overhead` pose's raised hammer, plus a little wider for
`rush`'s forward lunge and `slam`'s extended swing reaching further from
centre than any of the original six poses. **`groundLine` is unchanged at
74.** The six original stances' alpha-bbox crops were sha256-compared
against the previously committed PNGs and are pixel-identical — the canvas
resize repositions them (further from the top-left origin; the anchor point
moves when the shared canvas grows) but changes not one opaque pixel of
their own art.

`rush`'s pose has the back leg lifted mid-stride, which is exactly the shape
of pose the `ground_band` anchor exists to get right (see the tool's own
"Anchoring" section) — its largest-connected-component + ground-band
measurement correctly picked the PLANTED front foot as the ground contact,
not the raised back one. Checked, not assumed: all nine stances landed
within 0px of each other (`--max-ground-spread`'s default is 6), so no
`--nudge` was needed anywhere in this delivery.

### Ceiling check

`overhead`'s topmost opaque pixel sits **454px above its own groundLine**.
This file's own Sizing section above (written 2026-09-07) compared the
bear's height to "384px, golem" — `Domain/Stage/FightStageAnchors.cs`'s own
2026-09-08 re-measurement note says that 384/golem figure "was never
measured off the art" and is superseded: the current documented tallest
actor on record is **forest_warden at 483px** above its own manifest ground
line, with the stage band closing by 66 units at that figure (not the 18
units the stale note claimed). Y is shared between the enemy and party sides
(only X differs, per `FightStageAnchors.cs`'s C4 note), so this comparison
applies directly to a party member too.

Bjorn's 454px sits under the current 483px reference, with less headroom
than most of the roster but not a new tallest actor. **Not shrunk to fit** —
`overhead` is a momentary wind-up pose, per this job's own brief, and the
owner decides whether that margin is comfortable enough as delivered.

### Provenance

**Still reproducible.** `recipe.json` gained a `"sheets"` array (two
sources) alongside the `"argv"` replay actually reads — see the tool's
module docstring for why `"argv"` alone is what a replay needs, in both the
one-sheet and multi-sheet shape. Verified 2026-09-09: replaying the recipe
reproduces all nine committed stills byte for byte (sha256-compared), and
the four other single-sheet recipes committed elsewhere (owl, treant, the
ram) were replayed unchanged during this same check, into scratch, never
against their own delivered art.

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Characters/bear/recipe.json
```

`toolSha256` in this recipe now differs from owl/treant/the ram's own
recipes, because the tool itself changed to support more than one sheet. No
test in `Assets/_Project/Scripts/Tests` compares `toolSha256` values (checked
by grep before this delivery) — `load_recipe`'s own version-mismatch NOTE is
the only place that number is read back, and it degrades gracefully (a
printed warning, not a failure) — so those three recipes were left
untouched rather than rewritten for a hash nothing enforces.
