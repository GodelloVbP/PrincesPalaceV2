# sheep_black_ram — Shawn's Black Ram transformation, six key stills

Delivered 2026-09-09. **This is not a roster character.** It is the FORM
Shawn (`Characters/sheep`) wears while his `black_ram_mode` skill is active
— a three-turn transformation (`CharacterId: sheep`, `Effect: 6`,
`ResourceCost: 7`) resolved through the skill's `transform.spritePath`, the
same mechanism any other transform effect points at a different stance
folder. There is no `characters.json` row for `sheep_black_ram` and there
must not be one: nobody picks this actor for a party slot, nobody levels it,
it has no stats of its own. It borrows Shawn's.

## Source

`sheet_poses.png` — 1536x1024, 3x2, copied verbatim from the owner's design
sheet `Assets/_Project/Art/Sheets/shawn_black_ram_sheet-v2.png` (left alone;
the owner decides what happens to the sheet copy). RGB, no alpha channel,
a baked near-white background — same situation the owl, treant and bear
sheets shipped in.

Cells, row-major, to stance:

1. idle — standing neutral, weight settled, arms down.
2. attack — punching straight ahead, front arm fully extended.
3. cast — head-down horn charge, hunched low, weight forward.
4. hurt — flinching, eyes shut, arms drawn in.
5. defeated — lying on the ground, curled, cape pooled.
6. victory — fist raised overhead, weight back on the heels.

The ram faces RIGHT in every cell — confirmed by eye: idle's weight leans
right, attack's punching arm and cast's charging lunge both drive rightward,
victory's raised fist sits on the right side of the read. Matches Shawn's
own `battleSpriteFacing: "Right"`.

## Keying

`white_flood --pocket-max-area 4000`, the same setting the owl, treant and
bear needed for the same reason: the near-white background is baked into
the RGB rather than carried as alpha, and gaps between the cape and the body,
under the raised/extended arms, and around the curled defeated pose each
enclose a background pocket well over the 200px default speck cap.

Checked programmatically after slicing: scanned every delivered still's
opaque pixels for near-white (channel minimum >= 235), near-neutral (channel
spread <= 6) pockets that would mean a checkerboard/background survivor.

| stance | suspect px |
|---|---:|
| idle | 0 |
| attack | 0 |
| cast | 1 |
| hurt | 1 |
| defeated | 0 |
| victory | 1 |

Three stances came back with exactly one suspect pixel each, the rest zero
— single antialiased pixels on an edge, not pockets (a pocket would be a
contiguous cluster inside a limb gap; these are lone pixels). Same read as
the bear's `defeated.png` at delivery. `--pocket-max-area` did not need
raising.

## Sizing

Sliced once at `--delivery-scale 1.0`: idle content height measured 394px.
Target is **1.00x** Shawn's currently delivered idle bbox height — the ram
is Shawn's own body transformed, not a separately-scaled creature, so unlike
the bear (1.10x, a taller frontliner) or the owl (its own hover scale) this
one matches Shawn exactly. Re-measured directly against the committed
`Resources/Characters/sheep/idle.png` alpha bbox rather than trusting any
number in a doc: **350px** (canvas 540x370, bbox y 12-361).

Target = 1.00 x 350 = 350px. Re-sliced at `350/394 = 0.8883248730964467`;
the written `idle.png` now measures exactly 350px, matching Shawn's own
height as the same body wearing a different silhouette should.

## Accepted metrics

Filled area (opaque pixel count) and wool px (dark curly-wool-hued opaque
pixels, isolated by channel ordering R>=G>=B with a 5-25 gap between R and B
and value between 15 and 95 — this excludes the pure-black outline, the tan
tunic, the leather straps, the green cape and the cream horns, all of which
sit outside that band) against idle, plus the slicer's own
`sqrt(largest-component-mass)`:

| stance | filled area / idle | wool px / idle | sqrt(mass) |
|---|---:|---:|---:|
| idle | 1.00 | 1.00 | 267.4 |
| attack | 1.04 | 1.04 | 273.0 |
| cast | 0.90 | 0.92 | 253.8 |
| hurt | 0.88 | 0.93 | 250.7 |
| defeated | 0.68 | 0.83 | 219.7 |
| victory | 1.12 | 1.13 | 283.6 |

All six sit inside or close to the usual 0.90-1.10 band except defeated,
which legitimately loses mass the way every other actor's collapsed pose
does (curled tightly, cape pooled beneath it), and victory, whose fully
raised double-fist pose widens the canvas the same way the bear's roaring
cast did — wool px rises with it in proportion, consistent with a genuine
pose spread rather than a resize.

## Provenance

**Reproducible — `recipe.json`, beside this file.** Replay it with:

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Characters/sheep_black_ram/recipe.json
```

Verified 2026-09-09: the replay reproduces all six committed stills
(sha256-compared before/after) and leaves `Resources/StanceManifest.json`'s
`Characters/sheep_black_ram` entry unchanged, which is what earns this actor
`groundLineSource: "slicer"`. `groundLine` is 8.

## Delivery

```bash
python tools/slice_actor_sheet.py --sheet Assets/_Project/Art/Characters/sheep_black_ram/sheet_poses.png \
    --actor Characters/sheep_black_ram --stances idle,attack,cast,hurt,defeated,victory \
    --grid 3x2 --key white_flood --pocket-max-area 4000 \
    --delivery-scale 0.8883248730964467 --prune
```

Canvas 498x366, `groundLine 8`, idle content height 350px against Shawn's
measured 350px (1.00x, by design — same body, different form).

## Judgements made, not measured

- **`breath: 1.0`** in `StanceManifest.json`'s `Characters/sheep_black_ram`
  entry is copied, not tuned, the same call the bear's entry made: Shawn's
  own entry (`Characters/sheep`) sets no `breath` at all, which means the
  runtime default of 1.0 already applies — this makes that same number
  explicit for the transformed form rather than authoring a new one. No
  `hover` block: the ram stands on the ground like Shawn, unlike the owl.
- **No roster entry, and none should be added.** This actor exists only to
  be pointed at by `black_ram_mode`'s `transform.spritePath` while the skill
  is active; wiring that reference is a separate job (content, not art) and
  is not this delivery's concern.
- **No portrait.** A transform-only form has no dossier plate to show — it
  is never picked in a party screen, never has a card of its own — so unlike
  the bear's stopgap crop, nothing was generated here and nothing should be
  until the game actually needs one.
