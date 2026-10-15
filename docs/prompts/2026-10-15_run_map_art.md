# Run map art batch 2026-10-15: the Hush, vertical

Five prompts for the realm map, which runs top to bottom. This file replaces the
asset list in `docs/RUN_MAP_ART_BRIEF.md`, which was written for the old
left-to-right map. That brief's world section (§2–3) and its "Not wanted" list
(§9) still apply.

Drop the results in `Art/Incoming/run_map/` under the filenames given below.
Claude handles the rest: keying, scaling, making the strip seamless, and wiring
it in.

Attach these to every prompt as references:
`Assets/_Project/Art/Backgrounds/forest_map_background.png` (house map style)
and `Assets/_Project/Art/Enemies/Giant_rat_sheet.png` (how the creatures on top
are drawn).

## Style block (paste first, every time)

```
2D game art for a dark-fantasy roguelike map, matching the attached references:
top-down overhead view, clean shapes with dark outlines, soft painted shading,
a restrained palette. Melancholic and uncanny, tender rather than grim. No gore,
no horror, nothing modern or clinical. No text, letters, numbers, UI, frames,
borders or watermark anywhere in the image.
```

## 1. The Hush, as found (backdrop strip)

Filename: `realm_hush_found.png`

```
A tall portrait image, highest resolution you can make, seen straight from
above: a forest where colour, sound and wind have stopped. Trees stand too
still, flat mist lies on the ground, light comes from nowhere in particular.
Everything leans toward one cold grey-violet, with a few small things (a
mushroom, a leaf cluster, a flower) still holding a trace of colour, as if they
forgot to fade.

Layout, important: the CENTRAL 75% of the width is open forest floor - calm,
even, mid-value ground (moss, mist, fallen leaves, roots) with no paths, no
trails, no clearings, no buildings, no bright spots and no strong shapes. Game
pieces are placed on top of it. The dense trees and all the interesting detail
go in the left and right edge bands.

The image must tile vertically: the top edge continues seamlessly into the
bottom edge, with nothing big cut off by either edge. No horizon, no sky, no
vanishing point - it is ground seen from directly above, the same all the way
down.
```

## 2. The Hush, as eased (same strip, healed)

Filename: `realm_hush_eased.png`. Attach the chosen `realm_hush_found.png` as
the image to edit.

```
Edit the attached image. Keep every tree, root, rock and the whole composition
exactly where it is - same framing, same size, pixel-aligned. Change only light
and colour: warmth returns at the edges of things, a low golden light filters
through, the mist thins and drifts, faint green and amber come back into the
leaves and moss. Still quiet, but the quiet of rest instead of absence. Not
bright, not triumphant, no sunbeams blasting down.
```

## 3. The heart (boss landmark at the bottom of the map)

Filename: `realm_hush_heart.png`

```
A wide landscape image seen from directly above, the same forest and the same
grey-violet palette as the attached strip: the place the whole realm leads to,
where the stillness is thickest. A great hollow dead tree whose roots spread
out into a dark, perfectly still pool. Roots reach outward toward the image
edges so it can sit at the bottom of the forest strip. The centre (where the
boss stands, drawn by the game) is a calm dark area with no detail. The top
edge fades into plain forest floor so it blends into the strip above it.
```

## 4. Room medallions (one sheet)

Filename: `map_medallions.png`

```
A sheet of 9 game map icons in a 3x3 grid of equal square cells, each icon
centred in its cell with generous empty margin, on a flat solid pure green
#00FF00 background (no shadows, no gradient on the green, no green inside the
icons). Painted badge medallions in one shared set style: carved dark wood and
old tarnished metal rims, a simple emblem in the middle, bold readable
silhouettes. Each one must have a DIFFERENT outer shape so they read in
greyscale at 48 pixels:

Row 1: FIGHT - round medallion, crossed claws emblem, the calmest of the set.
       ELITE - hexagonal faceted medallion, horned skull emblem, heavier rim.
       BOSS - large gate/arch-shaped medallion, a dark crown over a keyhole,
       clearly the biggest and most ominous.
Row 2: REST - peaked tent/flame shape, a small campfire emblem, warmest colours.
       EVENT - rounded-square medallion, an open eye inside a crescent moon.
       SHOP - square medallion with corner studs, a coin purse emblem.
Row 3: TREASURE - small diamond medallion, a closed chest emblem.
       CACHE - shield shape, a buried strongbox half-sunk in roots.
       ENTRY - small plain circle, a soft pale glow emblem (where the souls
       arrive).
```

## 5. Reward chips (one sheet, optional)

Filename: `map_chips.png`

```
A sheet of 5 tiny game icons in a 5x1 row of equal square cells, centred, on a
flat solid pure green #00FF00 background. Same painted style as the
medallions, very bold and simple so each reads at 20 pixels: a stack of gold
coins; a round red potion flask; a sword (gear); a glowing amulet on a cord
(relic); a closed spell book with a clasp.
```

## Checks before Claude wires them

- Strip: nothing path-like or clearing-like in the middle 75%; the top and bottom
  edges meet with no big object cut off. Claude fixes small seams.
- Eased: an overlay at 50% shows no shifted trees. If ChatGPT moved things,
  regenerate. A shift cannot be fixed afterwards.
- Medallions: all 9 shapes are still distinct when turned to greyscale at 48 px.
