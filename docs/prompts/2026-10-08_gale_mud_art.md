# Spell art batch 2026-10-08: Gale Scythe and Mud Burst

Four pieces. Motion approved by the owner 2026-10-08. Template:
`docs/prompts/TEMPLATE_spell_art.md`. Each piece attaches its approved
stand-in frame (`docs/prompts/2026-10-08_refs/`); the art is checked against
it before wiring.

Upload with every prompt, as rendering style only:
`Assets/_Project/Art/Enemies/Giant_rat_sheet.png`,
`docs/spell_art/reference/censer_hanging.png`,
`docs/spell_art/reference/censer_flame_loop.png`.

## Style block (paste first)

Flat 2D game cel art matching the references: bold, uniform, dark outlines; flat
cel shading with two or three discrete tones per colour area; a small palette;
minimal internal texture. Clean game sprite art, not illustration. No painterly
gradients, no rim light, no painted glow or bloom, no soft airbrush, no
micro-texture. The game adds glow, flicker and darkness at runtime, so the art
stays flat. No text, numerals, frame, border, UI or watermark.

Sheets: a grid of equal cells. Every cell has the same pivot at the same pixel
position. Nothing crosses a cell edge. The drawing changes between cells; the
canvas, scale and pivot never do.

## 1. Gale Scythe: leaf spiral (anticipation)

Attach: `2026-10-08_refs/gale_leaf_spiral_approved.png` (the whirl in Shawn's
raised hand, before the sweep).

```
Piece: Gale Scythe: leaves winding into a tight spiral of wind
Beat it serves: anticipation, 0.0-0.2 s, wind gathers in Shawn's raised hand
Filename: gale_leaf_spiral.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Gale Scythe/gale_leaf_spiral.png
Canvas: 1536x256   Grid: 6x1 of 256x256
Pivot: the spiral's centre at the exact centre of every cell
Size in play: about 150-200 px, a little larger than Shawn's hand and staff head
Drawing: 6 cells, a loop. Five or six small leaves (pale sea-green and
  yellow-green, dark outline) and two thin white wind ribbons wound round the
  centre in a spiral that tightens cell by cell: cell 1 wide and loose, cell 6
  tight and small. Leaves turn as they travel. Flat tones.
Verb: gather
Must read as: wind being wound up, leaves sucked into a point
Must NOT read as: a tornado, a wheel, a green magic orb, a flower
Not in the picture: hand, staff, Shawn, glow, sparkles, ground
```

Background: solid #FF00FF (the leaves are green). No pink or magenta in the
subject. Deliver one PNG named exactly `gale_leaf_spiral.png` at 1536x256.

## 2. Gale Scythe: gust streaks (resolution)

Attach: `2026-10-08_refs/gale_gust_streaks_approved.png` (the frame after the
cut: leaves should be spiralling away here, and today nothing reads).

```
Piece: Gale Scythe: wind streaks trailing off after the cut
Beat it serves: resolution, 0.3-0.65 s, the wind passes and leaves blow away
Filename: gale_gust_streaks.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Gale Scythe/gale_gust_streaks.png
Canvas: 2048x256   Grid: 4x1 of 512x256
Pivot: left end of the streaks, 50% down, at the same pixel in every cell
Size in play: about the width of one rat body, laid across it
Drawing: 4 cells. Three or four thin curved white-and-pale-sea-green wind
  lines running left to right with two or three small leaves tumbling along
  them. Cell 1 long and full; each cell after it the lines break up, thin and
  shorten toward the right; cell 4 only short wisps and one leaf. Flat tones,
  dark outline on the leaves only.
Verb: blow away
Must read as: a gust passing and dying out
Must NOT read as: a second slash, claw marks, speed lines of a moving figure
Not in the picture: blade, bodies, ground, glow
```

Background: solid #FF00FF. No pink or magenta in the subject. Deliver one PNG
named exactly `gale_gust_streaks.png` at 2048x256.

## 3. Mud Burst: puddle (aftermath)

Attach: `2026-10-08_refs/mud_puddle_approved.png` (the splat fading on the
rat; the puddle lies at its feet on the ground line).

```
Piece: Mud Burst: a flat puddle of mud on the ground
Beat it serves: aftermath, 0.4-1.2 s, mud lands at the feet and fades
Filename: mud_puddle.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Mud Burst/mud_puddle.png
Canvas: 1536x512   Grid: one still
Pivot: the pool's centre on a line 60% down the canvas
Size in play: 260-400 px wide, flat under the target's feet
Drawing: a wet brown mud pool seen from a low three-quarter angle, about 3:1
  wide to tall, an irregular lobed edge with a few short splash fingers, two or
  three flat highlight shapes for wetness, a few loose clots around it. Same
  browns as the reference splat (dark umber outline, two mid browns, one pale
  tan highlight). Matte.
Verb: settle
Must read as: mud lying on the ground
Must NOT read as: a hole, a shadow, blood, a round splat seen from the front
Not in the picture: feet, body, grass, glow, stones
```

Background: solid #00FF00, no green or yellow-green in the subject. Deliver one
PNG named exactly `mud_puddle.png` at 1536x512.

## 4. Mud Burst: drip strands (aftermath)

Attach: `2026-10-08_refs/mud_drips_approved.png` (today's stand-in reads as
stains; the drips run down the fur and bark from the splat).

```
Piece: Mud Burst: a strand of mud running down
Beat it serves: aftermath, 0.4-1.2 s, mud runs down the body
Filename: mud_drips.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Mud Burst/mud_drips.png
Canvas: 1024x512   Grid: 4x1 of 256x512
Pivot: the top of the strand, centred, 10% down, the same pixel in every cell
Size in play: 60-120 px tall on a rat's flank or a treant's bark
Drawing: 4 cells, one strand of thick mud sliding down: cell 1 a short blob
  hanging from the top; cell 2 longer, a bead forming at the bottom; cell 3
  long and thinning with the bead heavy; cell 4 the bead dropped off, a thin
  trail left. Mid brown with a darker umber outline and one pale tan
  highlight stripe so it reads on dark fur and on brown bark.
Verb: run down
Must read as: wet mud sliding down a surface
Must NOT read as: blood, chocolate, a worm, a stain
Not in the picture: body, fur, bark, glow
```

Background: solid #00FF00, no green or yellow-green in the subject. Deliver one
PNG named exactly `mud_drips.png` at 1024x512.
