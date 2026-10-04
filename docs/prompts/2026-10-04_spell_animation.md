# Spell animation prompts

Designer prompts, 2026-10-04, for `docs/PLAN_SPELL_ANIMATION.md`. Prompt text
only; no art is generated here.

## Shared style block (paste into every prompt)

Upload `Assets/_Project/Art/Enemies/Giant_rat_sheet.png` as the **rendering
style** reference only: do not copy its subject or palette.

Flat 2D game cel art matching the reference: bold, uniform, dark outlines; flat
cel shading with two or three discrete tones per colour area; a small palette;
minimal internal texture. Clean game sprite art, not illustration. No painterly
gradients, no rim light, no painted glow or bloom, no soft airbrush, no
micro-texture. The game adds glow, flicker and darkness at runtime, so the art
stays flat.

Solid #00FF00 background, no gradient, vignette, floor or shadow plate. No green
or yellow-green anywhere in the subject (the keyer removes by hue). No text,
numerals, frame, border, UI or watermark.

**Sheets:** a 2x2 or 1x4 grid of equal cells. Every cell has the **same pivot
at the same pixel position** (stated per prompt). Nothing crosses a cell edge.
The drawing changes between cells; the canvas, scale and pivot never do.

## Censer of Embers: the censer on its chain

Plan A2 (pilot). It swings like a heavy bell around the chain's top, then bursts
into embers (the burst is particles, not this still).

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Censer of Embers/censer_hanging.png`, square
1536x1536.

- A heavy brass censer hanging straight down on a short chain. The chain runs
  from the top-centre of the canvas (the pivot: exactly at the top edge, centre)
  down to the censer. The censer fills the lower half.
- Pierced lid with a few small openings; a dull orange coal light shows through
  them as a flat colour (no glow).
- Three thin smoke curls rising from the lid, drawn as flat grey-violet shapes
  with outlines.
- Hanging perfectly vertical and centred: the game swings it.
- No hand, no priest, no altar, no fire around it.

Must read as: a temple censer hanging, heavy. Must NOT read as: a lantern, a
bell, a bomb.

Owner: what to change: ______

## Censer of Embers: living flame loop

Plan A2. The fire at the target's feet after the burst. It loops, so cell 4
must lead back into cell 1.

Target: one 2x2 sheet, raw file
`Assets/_Project/Art/Sheets/Spells/Censer of Embers/censer_flame_loop.png`,
2048x1024 (four 1024x512 cells, read left to right, top to bottom).

- Pivot: the base line of the fire sits on the same horizontal line, 85% down
  each cell, centred. Every cell's flames start from that line.
- A low, wide band of fire on scattered coals, spanning about 80% of the cell
  width, flames at most 40% of the cell height. The centre is lower than the
  ends, so a body can stand in it.
- Four poses of the same fire: the tongues lean, split and re-form between
  cells, as if the fire is alive. Same coals in every cell.
- Flame colours in flat tones: deep red outside, orange, a small yellow core.
  Coals dark brown with orange cracks.
- No smoke, no censer, no figure, no floor texture.

Must read as: a living fire licking up. Must NOT read as: a wall of fire, an
explosion.

Owner: what to change: ______

## Thorn Tithe: back vines

Plan A5. The cage closes around the body: these vines draw **behind** the
target. They pair with the front vines below; draw both from the same scene.

Target: one 1x4 sheet, raw file
`Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_back_vines.png`,
4096x1024 (four 1024x1024 cells, left to right).

- Pivot: the roots sit on one ground line, 90% down each cell, centred, in
  exactly the same spot in every cell.
- An empty middle the width of a body (about 40% of the cell), so a creature
  fits inside.
- Cell 1: short thorny shoots breaking the soil. Cell 2: vines up to half
  height. Cell 3: vines full height, starting to arch inward. Cell 4: the far
  vines arched over the top, meeting above the middle.
- Dark teal-blue vines (not green) with pale bone-white thorns; brown soil
  clods at the roots.
- These are the vines on the far side: draw them a little darker than the front
  set.

## Thorn Tithe: front vines

Same scene and same cells as the back vines; these draw **in front of** the
target.

Target: one 1x4 sheet, raw file
`Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_front_vines.png`,
4096x1024, identical pivot and middle gap.

- Fewer vines than the back set, on the near side: two or three, lower, so the
  body inside stays visible between them.
- Cell 1: shoots. Cell 2: half height. Cell 3: full height. Cell 4: curled
  inward, tips crossing the middle at chest height, closing the cage.
- Same colours as the back set, one tone lighter.

Must read as (both sets together): thorns closing a cage around the body. Must
NOT read as: a bush, a wreath, a wall.

Owner: what to change: ______

## Velvet Shackles: ribbon strand

Plan A5. One strand of ribbon. The game sends several of them around the target
(behind it, then in front).

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Velvet Shackles/shackle_ribbon.png`,
2048x512.

- One long velvet ribbon running left to right in a loose S-curve, filling the
  width, about a fifth of the height thick.
- Deep crimson with one darker tone in the folds, a thin gold edge stitch.
- Both ends cut cleanly at the canvas edges' 5% margin. No knot, no cuff.

## Velvet Shackles: the cuff

Plan A5. A small cuff that cinches at the target's feet. It is sized to legs,
not to the whole body.

Target: one 1x3 sheet, raw file
`Assets/_Project/Art/Sheets/Spells/Velvet Shackles/shackle_cuff.png`,
3072x1024 (three 1024x1024 cells).

- Pivot: the cuff's centre at the same point in each cell, 60% down, centred.
- Cell 1: an open velvet band, loose. Cell 2: half closed. Cell 3: closed and
  creased tight, with a small gold clasp.
- Crimson velvet, gold clasp; the cuff is short and wide, like an ankle
  shackle made of cloth.
- About 50% of the cell width. No chain trailing off, no leg, no body.

Must read as: tied down at the ankles. Must NOT read as: a scarf, a flower, a
collar.

Owner: what to change: ______

## Court of Whispers: courtier from behind (optional)

Plan A4. Used when a courtier passes behind the enemy on its orbit. Without
it, the front-view courtier is reused, smaller and dimmer.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Court of Whispers/courtier_back.png`,
1536x1536.

- The same hooded courtier as `court_of_whispers_courtier-v2.png`, now seen
  from behind and slightly to the side: hood, back of the robe, the raised
  sleeve just visible.
- Leaning forward to the right, same 25-degree lean.
- Robes trailing into wisps below the knee; no feet.
- Violet and indigo in flat tones, outlined.

Owner: what to change: ______

## Not commissioned

- **Viper's Bite:** no head (owner). The jaws closing on the target come back
  from the old sheet.
- **Gale, Mud, Ashen, Aegis, Borrowed:** existing art; the work is motion and
  endings.
- **Later, under the cel-style rule:** today's painted stills (Ashen catch,
  Aegis lock, Viper burst, Mud glob and splat, Gale blade, Borrowed chevrons,
  courtier v2) are painterly. Redraw them in the rat style only after the
  pilots show what the style looks like in motion.
