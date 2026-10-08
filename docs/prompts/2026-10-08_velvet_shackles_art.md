# Spell art batch 2026-10-08: Velvet Shackles

Motion approved by the owner 2026-10-08 with these decisions: the wrap is drawn (a ribbon bending into a loop round the legs); the lash flies to the legs, not the chest; the hit stays at 0.40 s; the cuff is sized to the legs, smaller on big bodies.
Template: `docs/prompts/TEMPLATE_spell_art.md`. Upload as rendering style with every prompt: `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`, `docs/spell_art/reference/censer_hanging.png`, `docs/spell_art/reference/censer_flame_loop.png`. Also attach the approved frame named per piece (`docs/prompts/2026-10-08_shackles_refs/`) and, for colour, the delivered `Assets/_Project/Art/Sheets/Spells/Velvet Shackles/shackle_cuff.png`.

## Style block (paste first)

Flat 2D game cel art matching the references: bold, uniform, dark outlines; flat cel shading with two or three discrete tones per colour area; a small palette; minimal internal texture. Clean game sprite art, not illustration. No painterly gradients, no rim light, no painted glow or bloom, no soft airbrush, no micro-texture. The game adds glow, flicker and darkness at runtime, so the art stays flat. Solid #00FF00 background, no gradient, vignette, floor or shadow plate. No green or yellow-green anywhere in the subject. No text, numerals, frame, border, UI or watermark.

Sheets: a grid of equal cells. Every cell has the same pivot at the same pixel position. Nothing crosses a cell edge. The drawing changes between cells; the canvas, scale and pivot never do.

## 1. Lash

Attach: `2026-10-08_shackles_refs/shackles_lash_approved.png` (the approved placeholder for this beat).

```
Piece: Velvet Shackles: the lash
Beat it serves: delivery 0.02-0.22 s (lash)
Filename: velvet_shackles_lash.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_lash.png
Canvas: 4096x512   Grid: 4x1 of 1024x512
Pivot: the leading tip, 90% across and 50% down, in every cell; the strip points to the right
Size in play: 170 px long, about 25 px thick
Drawing: the ribbon cracking like a whip: cell 1 straight and rippled, 2 a wave running along it, 3 the tip curling over, 4 the tip flicking. A tapered end on the
  leading side. Crimson, gold stitching, bold dark outline.
Verb: lash. It flies at the target's legs and hands over to the wrap there.
Must read as: a ribbon thrown like a whip.
Must NOT read as: a snake, a banner, a river, a sash.
Not in the picture: hand, target, glow, text.
```

Deliver one PNG named exactly as `Filename`, at the stated canvas, on solid #00FF00, saved to the delivery path.

## 2. Ribbon wrap set (back and front halves)

Attach: `2026-10-08_shackles_refs/shackles_wrap_approved.png` (the approved placeholder for this beat).

```
Piece: Velvet Shackles: ribbon coiling round the legs, back half and front half
Beat it serves: delivery 0.20-0.40 s and resolution 0.45-0.65 s (back, front)
Filename: velvet_shackles_wrap.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_wrap.png
Canvas: 4096x1280   Grid: 4x2 of 1024x640 (never touching a cell edge); the top row is the BACK half (the far side of the loop), the bottom row the
  FRONT half (the near side), the same four moments in the same column
Pivot: the middle of the loop, 50% across and 55% down, in every cell; the ground line at 80% down
Size in play: the loop is about half of a rat's body width at the end (a 240 px box), flat on the legs; it must read at that size
Drawing: one crimson velvet ribbon with a gold stitched edge, seen from slightly above. Column 1 a wide loose spiral of ribbon around empty
  space; column 2 the loop narrower; column 3 the loop closed tight round the ankles, ends crossed; column 4 the loop slack again, wider and drooping.
  The top row draws only the far part of each loop, the bottom row only the near part, so laid one over the other they make one loop.
  Two flat reds, a darker fold, gold dashed stitching, bold dark outline.
Verb: bind.
Must read as: a ribbon winding round and tightening on something thin.
Must NOT read as: a bow, a gift, a belt, a rope, a flower, a sash on the body.
Not in the picture: target, floor, shadow, glow, hands, text.
```

Deliver one PNG named exactly as `Filename`, at the stated canvas, on solid #00FF00, saved to the delivery path.

## 3. Cuff set

Attach: `2026-10-08_shackles_refs/shackles_cuff_approved.png` (the approved placeholder for this beat).

```
Piece: Velvet Shackles: the cuff
Beat it serves: payoff 0.40 s (cuff)
Filename: velvet_shackles_cuff_flat.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_cuff_flat.png
Canvas: 3072x768   Grid: 4x1 of 768x768
Pivot: the middle of the cuff, 50% across and 60% down, in every cell, the same size in every cell (only the opening changes)
Size in play: about 40% of a rat's body width (a 130 px box); sized to the legs, so on big bodies it stays small
Drawing: the padded crimson velvet cuff seen from the front and slightly above, one drawing per cell: 1 open with the gold plates apart, 2 nearly closed,
  3 closed with the clasp, 4 closed and slightly squeezed with one crease. Two flat reds, a darker inside, gold clasp in two tones, bold dark outline.
  The delivered strip (`shackle_cuff.png`) is the target style; step 4 needs its cells on one grid with a fixed pivot.
Verb: bind.
Must read as: a shackle snapping shut.
Must NOT read as: a collar, a bracelet, a scrunchie, a bag.
Not in the picture: target, legs, floor, shadow, glow, text.
```

Deliver one PNG named exactly as `Filename`, at the stated canvas, on solid #00FF00, saved to the delivery path.

## 4. Gold glint, thread, scrap (particles)

Attach: none beyond the style references (replaces the painted particle cells).

```
Piece: Velvet Shackles: gold glint, thread tuft, velvet scrap
Beat it serves: payoff 0.40 s (glint), resolution 0.45-0.90 s (scraps, thread)
Filename: velvet_shackles_particles_flat.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_particles_flat.png
Canvas: 1024x256   Grid: 4x1 of 256x256
Pivot: the middle of each cell
Size in play: 15 to 40 px particles
Drawing: cell 1 a four-point gold glint; 2 a short gold thread tuft; 3 a torn crimson velvet scrap; 4 a small curled red ribbon piece with gold edge. Two flat tones each, bold dark outline.
Verb: scatter.
Must read as: bits shed by a snapped binding.
Must NOT read as: a star, a petal, confetti, a spark.
Not in the picture: ribbon, cuff, hands, glow, sparkle trails.
```

Deliver one PNG named exactly as `Filename`, at the stated canvas, on solid #00FF00, saved to the delivery path.
