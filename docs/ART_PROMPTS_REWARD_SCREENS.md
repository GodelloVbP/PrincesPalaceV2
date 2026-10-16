# Art prompts: reward screens

For `docs/PLAN_REWARD_SCREENS.md` §5. Each block below is complete: paste
the prompt text into ChatGPT as is, attach the files listed under it, and
save the result under the filename given. Claude handles the rest (keying,
slicing, scaling, wiring).

Paths are relative to the repo root, `C:\Games\Prince's Palace-v2\`. Save
results to `Assets/_Project/Art/UI/Reckoning/`.

| Sheet | Output file | When |
|---|---|---|
| A1 Caches | `reward_caches.png` | **now** |
| A2 Plinths | `reward_plinths.png` | **wait**: after the R2 ghost gate (a capture of the five-card row with ghosts must read first; the plan's §6) |
| A3 Card back | `reward_card_back.png` | **now** |
| A4 Glyphs | `reward_glyphs.png` | **now** |
| A5 Chain overlay | `reward_chain.png` | **now** |

The run-up (C0) needs no new art.

If a sheet comes back with cells merged, shapes that run into each other or
the wrong number of objects, regenerate it one object at a time: use the
same prompt and replace the grid sentence with "One object, centred, on the
same background." Name those `reward_caches_1.png`, `reward_caches_2.png`
and so on, in reading order.

---

## A1. Caches: generate now

Output: `reward_caches.png`. Keyed green.

Attach:
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png` (outline weight and cel shading)
- `Assets/_Project/Art/Backgrounds/Processed/map_chip_gear.png` (the icon set these sit beside)

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached references: flat cel shading with bold dark outlines, a restrained
grey-violet palette with warm gold accents, worn and handmade, tender rather
than grim. No gore, nothing modern. No text, letters, numbers, UI, frames or
watermark.

A sheet of 6 objects in a 3x2 grid of equal square cells, highest
resolution you can make, each object centred in its cell with generous
empty margin. They are a proud cat's offerings, wrapped for the souls he
sends to fight.

Row 1, gear, left to right: a small bundle wrapped in frayed violet cloth,
tied with one cord, a single paw print pressed into the knot; the same
bundle larger, tied with two cords and closed with a wax seal; the same
bundle largest, old iron bands over the cloth, closed with a seal shaped
like a small crown.

Row 2, relics, left to right: a small dented tin box with one clasp; the
same tin with brass corners and a wax seal; the same box in dark iron with
a crown-shaped seal.

Every object has a thin dark seam along its opening with nothing drawn in
it. Bold silhouettes that read at 96 pixels.

Flat solid pure green #00FF00 background, no shadow on it, no gradient, no
green anywhere inside the objects.
```

---

## A2. Plinths: wait for the R2 ghost gate

Output: `reward_plinths.png`. Keyed magenta (the moss is green).

Do not generate yet. The plan first checks, on placeholder discs, that a
five-card row with the worn item drawn behind each new item still reads.
If it does not, the size of the band or the ghost changes, and this prompt
changes with it.

Attach:
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`
- `Assets/_Project/Art/Backgrounds/Processed/map_chip_gear.png`
- `Assets/_Project/Art/Items/Relics/Processed/amassing_star.png` (object scale: an item stands on these)

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached references: flat cel shading with bold dark outlines, a restrained
grey-violet palette with warm gold accents, worn and handmade, tender rather
than grim. No gore, nothing modern. No text, letters, numbers, UI, frames or
watermark.

A sheet of 4 low resting places for an object, in a 4x1 row of equal
square cells, highest resolution you can make, seen from slightly above,
each with a flat top surface, centred with generous margin. Each has a
DIFFERENT outline so they can be told apart in greyscale:
1. a bare square flagstone;
2. a rough folded cloth on a round stone, a band of moss along its edge;
3. a cloth embroidered with blue thread on a taller hexagonal stone;
4. a cloth woven with gold thread on a wide scalloped stone, a small lit
   votive candle at one side.
All four the same width, increasing in height from 1 to 4. Across the
front of each stone, a smooth flat band left completely plain and wide
enough for two short numbers (the game engraves them there).

Flat solid pure magenta #FF00FF background, no shadow on it, no gradient,
no magenta anywhere inside the objects.
```

---

## A3. Card back: generate now

Output: `reward_card_back.png`. Keyed green.

Attach:
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached reference: flat cel shading with bold dark outlines, a restrained
grey-violet palette with warm gold accents, worn and handmade, tender rather
than grim. No gore, nothing modern. No text, letters, numbers, UI, frames or
watermark.

One upright card back in 2:3 portrait proportion, centred, highest
resolution you can make: dark violet cloth with a pale rubbing of a cat's
paw print in the middle and a worn gold thread edge. Plain and quiet; it is
seen for a fraction of a second.

Flat solid pure green #00FF00 background around the card, no shadow on it,
no green anywhere on the card.
```

---

## A4. Glyphs: generate now

Output: `reward_glyphs.png`. Keyed magenta (the leaf is green).

Damage, defence and mana already exist and are not in this sheet.

Attach:
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`
- `Assets/_Project/Resources/Icons/Ability/defense_physical.png` (the icon set these join: match its weight and size)
- `Assets/_Project/Resources/Icons/Ability/resource_mana.png`

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached references: flat cel shading with bold dark outlines, a restrained
grey-violet palette with warm gold accents, worn and handmade. No text,
letters, numbers, UI, frames or watermark.

A sheet of 10 small icons in a 5x2 grid of equal square cells, highest
resolution you can make, each centred with generous margin. Very bold and
simple, so each one reads at 24 pixels, with the same outline weight as
the attached icons.

Row 1, left to right: a red heart; a single feather; a whetstone with a
small spark; a stack of three gold coins; a small wooden door standing
ajar.

Row 2, left to right: a cloth satchel; a cracked red heart; a cracked
shield; a green leaf; a small skull.

Flat solid pure magenta #FF00FF background, no shadow on it, no gradient,
no magenta anywhere inside the icons.
```

---

## A5. Chain overlay: generate now

Output: `reward_chain.png`. Keyed green.

Attach:
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached reference: flat cel shading with bold dark outlines, a restrained
grey-violet palette. No text, letters, numbers, UI, frames or watermark.

One short loop of dark iron chain lying diagonally from lower left to upper
right, centred, simple, with a few links, highest resolution you can make.
It is laid over an item to show the item cannot be used yet.

Flat solid pure green #00FF00 background, no shadow on it, no green
anywhere on the chain.
```

---

## Round 2: regenerations after review

The first sheets were reviewed at game size. A3, A5 and A1's cloth row are
kept as they are. These four replace the parts that missed the brief.

### A1b. Relic tins: generate now

Output: `reward_caches_relic.png`. Keyed green. Replaces row 2 of
`reward_caches.png`, which read as stone blocks and a treasure chest.

Attach:
- `Assets/_Project/Art/UI/Reckoning/reward_caches.png` (match row 1's style, outline and scale)
- `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached references exactly: the same flat cel shading, bold dark outlines
and scale as the cloth bundles in the top row of the first attached sheet.
Worn and handmade, tender rather than grim. No text, letters, numbers, UI,
frames or watermark.

A sheet of 3 objects in a 3x1 row of equal square cells, highest resolution
you can make, each centred with generous empty margin. They are small boxes
a proud cat has kept his treasures in, NOT treasure chests: low, flat,
rounded tins like old sweet or tobacco tins, wider than they are tall.
Left to right:
1. a small dented tin, scuffed thin metal with a soft blue-grey sheen, one
   small clasp;
2. the same tin larger, with brass corner caps and a red wax seal with a
   paw print across the lid's edge;
3. the same tin largest, blackened iron with worn gold trim, a red wax seal
   shaped like a small crown across the lid's edge.
Each tin has a clearly visible thin dark gap between the lid and the base,
all the way round, with nothing drawn inside it. No domed lids, no straps,
no chest shapes. Bold silhouettes that read at 96 pixels.

Flat solid pure green #00FF00 background, no shadow on it, no gradient, no
green anywhere inside the objects.
```

### A1c. Cloth bundles with a seam (optional)

Output: `reward_caches_gear.png`. Keyed green. Only if you want the light to
leak from an opening instead of from the knot; otherwise the kept row 1
stays.

Attach:
- `Assets/_Project/Art/UI/Reckoning/reward_caches.png` (redraw row 1 only)

```
Redraw the three cloth bundles from the top row of the attached sheet,
exactly the same style, colours, sizes, cords, seals and iron bands, in a
3x1 row of equal square cells, highest resolution you can make, each
centred with generous margin. One change only: each bundle has a visible
thin dark gap where two folds of cloth meet across its front, as if it
could be opened there, with nothing drawn inside the gap. No text, letters,
numbers, UI, frames or watermark.

Flat solid pure green #00FF00 background, no shadow on it, no gradient, no
green anywhere inside the objects.
```

### A4b. Icon fixes and the three stat icons: generate now

Output: `reward_glyphs_2.png`. Keyed magenta. Replaces the satchel and the
whetstone in `reward_glyphs.png`, and adds damage, defence and mana in the
same style, so the stat row no longer mixes flat disc icons with painted
ones.

Attach:
- `Assets/_Project/Art/UI/Reckoning/reward_glyphs.png` (match these icons' style, outline weight and size exactly)

```
2D game icons matching the attached sheet exactly: the same chunky flat cel
shading, the same bold dark outline weight, the same size in the cell. No
text, letters, numbers, UI, frames or watermark.

A sheet of 5 small icons in a 5x1 row of equal square cells, highest
resolution you can make, each centred with generous margin. Very bold and
simple, so each reads at 24 pixels:
1. a small round glass potion flask with a cork, filled with red liquid;
2. a sharpening stone: a curved grey stone held at an angle, with a large
   bright gold spark flying off its edge;
3. a short sword pointing up, steel blade, wrapped grip;
4. a round wooden shield with an iron rim and a boss in the centre, whole
   and uncracked;
5. a single blue droplet with a small highlight.

Flat solid pure magenta #FF00FF background, no shadow on it, no gradient,
no magenta anywhere inside the icons.
```

---

## Checks before Claude wires them

- Every sheet has the number of cells it asks for, one object per cell, and
  nothing crosses into a neighbouring cell.
- Plinths and glyphs: turned to greyscale at their game size (plinths
  ~120 px wide, glyphs 24 px), every shape is still distinct.
- No text or numbers anywhere: the game adds every number itself.
