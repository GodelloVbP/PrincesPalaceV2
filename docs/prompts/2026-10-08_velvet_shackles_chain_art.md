# Spell art batch 2026-10-08 (2): Velvet Shackles as real shackles

Owner 2026-10-08, after r2: the ribbon read as "a cute bowtie", not a shackle.
Direction A chosen: gold shackles lined with dark crimson velvet, a chain lash,
the cuffs clamp the legs. Replaces the ribbon lash, the wrap and the padded cuff
from `2026-10-08_velvet_shackles_art.md`. The gold glint particle stays.

Upload as rendering style with every prompt:
`Assets/_Project/Art/Enemies/Giant_rat_sheet.png`,
`docs/spell_art/reference/censer_hanging.png`,
`docs/spell_art/reference/censer_flame_loop.png`. Also attach the approved frame
named per piece (`docs/prompts/2026-10-08_shackles_refs/`): it shows where and
how big the piece sits; the drawing replaces what is there.

## Style block (paste first)

Flat 2D game cel art matching the references: bold, uniform, dark outlines; flat
cel shading with two or three discrete tones per colour area; a small palette;
minimal internal texture. Clean game sprite art, not illustration. No painterly
gradients, no rim light, no painted glow or bloom, no soft airbrush, no
micro-texture. The game adds glow, flicker and darkness at runtime, so the art
stays flat. Solid #00FF00 background, no gradient, vignette, floor or shadow
plate. No green or yellow-green anywhere in the subject. No text, numerals,
frame, border, UI or watermark.

Sheets: a grid of equal cells. Every cell has the same pivot at the same pixel
position. Nothing crosses a cell edge. The drawing changes between cells; the
canvas, scale and pivot never do.

## 1. Chain lash (delivery)

Attach: `2026-10-08_shackles_refs/shackles_chain_lash_approved.png`.

```
Piece: Velvet Shackles: a gold chain cracked like a whip, an open manacle at its tip
Beat it serves: delivery 0.02-0.22 s, flying from Shawn's hand to the target's legs
Filename: velvet_shackles_chain_lash.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_chain_lash.png
Canvas: 4096x512   Grid: 4x1 of 1024x512 (never touching a cell edge)
Pivot: the open manacle at the leading tip, 90% across and 50% down, in every cell; the chain points right
Size in play: about 170 px long, the manacle about 30 px
Drawing: a chain of heavy gold links (two gold tones, dark outline) with one open
  manacle at the leading end, its inside lined with dark crimson velvet. Cell 1
  the chain nearly straight; cell 2 a wave running along it; cell 3 the tip
  whipping up; cell 4 the tip snapping forward with the manacle jaws wide open.
  The trailing end thins to a single link.
Verb: lash, then grab.
Must read as: a shackle thrown on a chain.
Must NOT read as: a ribbon, a snake, a necklace, a rope, a flail with a spiked ball.
Not in the picture: hand, target, glow, sparks, text.
```

Deliver one PNG named exactly `velvet_shackles_chain_lash.png` at 4096x512.

## 2. Shackle pair (payoff)

Attach: `2026-10-08_shackles_refs/shackles_cuffs_approved.png` (the cuffs sit on
the legs where the red ring sits now, much smaller and closed round the legs).

```
Piece: Velvet Shackles: two manacles clamping round two legs, joined by a short chain
Beat it serves: payoff 0.30-0.40 s (snap shut on the cue), resolution 0.45-0.85 s
Filename: velvet_shackles_cuffs.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_cuffs.png
Canvas: 3072x768   Grid: 4x1 of 768x768
Pivot: the middle of the short chain between the cuffs, 50% across and 60% down, in every cell
Size in play: the pair about 110 px wide on a rat's feet, the same pixel size on any body
Drawing: seen from the front and slightly above, the NEAR half of two gold
  manacles (the far half would be behind the leg), side by side with a gap for a
  leg in each, joined by three or four short heavy links. The inside edge of each
  cuff shows a thin band of dark crimson velvet lining. Cell 1 both cuffs open,
  jaws apart; cell 2 closing; cell 3 locked, a small keyhole plate on each;
  cell 4 locked with the connecting chain hanging slack and lower. Two gold tones,
  a darker gold for the inner faces, crimson lining, bold dark outline.
Verb: bind.
Must read as: shackles locked on an animal's legs.
Must NOT read as: bracelets, a belt, a bow, a collar, a bowl, jewellery.
Not in the picture: legs, body, floor, shadow, glow, text.
```

Deliver one PNG named exactly `velvet_shackles_cuffs.png` at 3072x768.

## 3. Broken link scatter (resolution particles)

No frame to attach.

```
Piece: Velvet Shackles: loose gold link and velvet shred
Beat it serves: resolution 0.45-0.90 s, as the shackles fade
Filename: velvet_shackles_links.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_links.png
Canvas: 1024x256   Grid: 4x1 of 256x256
Pivot: the middle of each cell
Size in play: 15 to 35 px particles
Drawing: cell 1 a single gold chain link; cell 2 a link bent open; cell 3 a small
  torn shred of dark crimson velvet; cell 4 a tiny gold keyhole plate. Two flat
  tones each, bold dark outline.
Verb: scatter.
Must read as: bits shed by a shackle letting go.
Must NOT read as: coins, rings, petals, confetti.
Not in the picture: chain, cuffs, glow, sparkle trails.
```

Deliver one PNG named exactly `velvet_shackles_links.png` at 1024x256.
