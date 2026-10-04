# Spell rework prompts

Designer prompts, 2026-10-03, for `docs/PLAN_SPELL_REWORK.md`. Same shape as
`docs/prompts/2026-10-02_petting_zoo_and_frost_flare.md`: composition and
invariants only. Stills only. Prompt text only; no art is generated here.

## Kinship relic icon

Plan 3.12 (owner O3). `kinship` in `ContentData/relics.json` has an empty
`iconPath`: "The first hit Shawn takes each fight is negated." Rare, Shawn's
(`bearer: sheep`), not draftable; the Petting Zoo grants it on the tenth pet
(`docs/PLAN_PETTING_ZOO.md`). The icon is the trust a flock gives back: a hand
-made keepsake, not armour.

Target: a 256x256 RGBA PNG like every other relic icon
(`Assets/_Project/Art/Items/Relics/Processed/<id>.png`, delivered size 256,
`tools/key_green_screen.py` `default_delivery_size`). It shows on the Relics
screen, the fight HUD chip and the reward track at small sizes, so the shape
must read as one silhouette at 64 px.

Pipeline: generate on flat green, drop the raw file as
`Assets/_Project/Art/Items/Relics/kinship.png` (not in `Processed/`), run
`py tools/key_green_screen.py`, then set `"iconPath"` in `relics.json` to the
keyed file and rebuild content and scenes (`Assets/_Project/Art/Items/Relics/README.md`).
Do not hand a real-alpha file to the keyer: it reads black under transparency
as background.

Style matches the existing relic set (see `Processed/ice_fingernail.png`,
`phoenix_egg.png`): one painted object, Hades-inspired painterly fantasy, bold
sculpted brush planes, rich material rendering, clean readable silhouette, soft
rim light from the upper left. Not flat clip art, not a photo, not a sticker
with a white outline.

Square canvas, solid #00FF00 background, no gradient or vignette in it. No
green or yellow-green anywhere in the subject, its shadow or its glow (the
keyer removes by hue).

- One object, centred, filling about 70% of the canvas, at least 12% margin on
  every side. A soft contact shadow under it, dark brown, never green, never
  touching the edge.
- The object: a hand-braided wool wristband lying in a loose ring, three
  strands of undyed cream wool and one dark brown strand twisted through, the
  loose ends knotted and left short and slightly frayed. Tucked into the knot, a
  single small brass bead. Cream, not yellow: warm off-white wool with
  light-brown shadows.
- Three-quarter view from slightly above, so the ring's opening shows and the
  braid's twist reads.
- A faint warm amber glow hugging the braid's upper edge, as if a hand had just
  let go of it. Subtle: it must not read as a magical aura or a fire.
- No sheep, animal, hand, face or character. No bell (that is the Bellwether's
  relic). No shield or armour shapes (the effect is protection, the object is
  affection).
- No text, numerals, frame, border, UI, watermark or drop-shadow plate.

Owner: what to change from the object or its glow: ______

## Court of Whispers: the leaning courtier

Plan 3.1, the one frame that carries meaning. `Resources/Spells/court_of_whispers`
(12 frames, three figures per frame, all rising together) is the stand-in. The
verb is *haunt*: hooded courtiers lean in around each enemy to whisper, then
draw back. The current frames cannot lean, because the three figures are fixed
in one drawing and the layers can only slide and mirror them.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Court of Whispers/court_of_whispers_courtier.png`,
sliced as one recipe (`Spells/court_of_whispers_courtier/f0`) and drawn by two
`sprite` layers per enemy: one mirrored, both sliding in on `drift` (see
`SpellLayer.drift`). Layer fields only; no sheet.

Style matches the existing court frames and the Blackglass Spear stills:
Hades-inspired painterly fantasy, bold sculpted brush planes, rich material
rendering, a clean readable silhouette, soft rim light. Violet and deep indigo
cloth, a cold pale-lilac rim, no other hue. Not flat clip art, not a photo.

Square canvas 1536x1536, solid #00FF00 background, no gradient, vignette or
floor in it. No green or yellow-green anywhere in the subject or its glow (the
keyer removes by hue; the dark hood interior stays opaque because the key is
the green, never luminance).

- One figure, centred, filling about 75% of the canvas height, at least 10%
  margin on every side. Full length, three-quarter view, facing the right.
- Hooded courtier in long layered robes that dissolve into wisps below the knee,
  so it can hover without feet or a floor. The hood is an empty dark void with
  no face and no eyes; this is a presence, not a character.
- Posed leaning forward from the waist toward the right edge, head bowed and
  turned a little, one sleeve raised with the hand cupped beside where an ear
  would be. The lean is the whole point: the spine is a clear diagonal, about 25
  degrees off vertical, and the silhouette reads as "bending close to speak".
- The robe's trailing wisps curl back to the left, away from the lean, so the
  motion reads forward.
- One soft violet glow near the cupped hand, small, no larger than the hood.
- No ring, circle, rune, floor shadow or ground plane (the stage supplies them).
  No second figure. No crown, weapon, skull, bones, tentacles or cult robe
  detail; it must not read as a necromancer, a ghost costume or a Death figure.
- No text, numerals, frame, border, UI or watermark.

Must read as: someone whispering into an ear. Must NOT read as: a generic curse
circle, a wraith attacking, or a reaper.

Owner: what to change from the lean or the hood: ______

## Thorn Tithe: vines rising

Plan 3.2, the one frame run that cannot be faked. `Resources/Spells/thorn_tithe`
(12 frames: a ring of vines rising, a sap burst, the cage sinking) is the
stand-in, and its rise is a slide of a nearly finished drawing. The verb is
*sprout*: green thorned vines thrust out of the ground and thicken as they
climb.

Target: a 4-frame sheet, one recipe (`Spells/thorn_tithe_growth/f0..f3`), laid
out 2x2 on one image, each cell 768x768. Raw file
`Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_tithe_growth.png`, sliced by
`tools/slice_spell_sheet.py`. The cells are one clump of vines at four moments;
the cell grid is the only layout.

Style matches the existing thorn frames and the Blackglass Spear stills:
Hades-inspired painterly fantasy, bold sculpted brush planes, rich material
rendering, a clean readable silhouette, soft rim light. Vines are saturated
cool green with dark green shadows and pale yellow-green thorns; the old
frames are brown on a brown floor, so green is the fix.

Solid #00FF00 sheet background, no gradient or vignette. The keyer removes by
hue, so keep the figure off the key: the vines are a blue-leaning emerald (hue
near #1F8F5F) with near-black shadows, never yellow-green or lime, and no glow
that approaches #00FF00.

- Cell 1: three short pointed shoots breaking the ground, soil crumbs flying,
  a hand's height.
- Cell 2: five vines, a third of the cell height, thorns forming along them.
- Cell 3: the vines at full height, thick, thorns large and sharp, tips curling
  inward toward the cell's centre; the vines occupy 80% of the cell height.
- Cell 4: the same clump a beat later, tips touching at the top as a cage;
  one bead of sap at a thorn tip.
- The base of every cell sits on the same line, 8% above the bottom edge, a
  low mound of soil and torn roots (not a floor plane or shadow).
- No ring, rune, circle, character, animal or body in the cell; the stage
  supplies the enemy. No leaves falling (the emitter supplies them).
- No text, numerals, frame, border, UI or watermark.

Must read as: thorny vines growing up out of the ground and caging what
stands there. Must NOT read as: a bramble wreath, a tree, a bear trap, or a
generic nature sparkle.

Owner: what to change from the vine colour or the thorn size: ______

## Mud Burst: the glob and the splat

Plan 3.3. Stand-ins: `Resources/Spells/mud_glob` (procedural, a flat lump) and
`Resources/Spells/mud_burst_splat` (the old gold burst frames under a brown
`tint`). The verb is *splatter*. Matte and wet, never glowing: a first-time
viewer must say "mud", not "fire".

Target: two stills, each a square 1536x1536 canvas on solid #00FF00, raw files
`Assets/_Project/Art/Sheets/Spells/Mud Burst/mud_glob.png` and
`.../mud_splat.png`, one recipe each (`Spells/mud_glob/f0`,
`Spells/mud_splat/f0`). Layers keep the timing; the splat is held and faded by
layer fields, so no sheet.

Style matches the existing frames and the Blackglass Spear stills:
Hades-inspired painterly fantasy, bold sculpted brush planes, rich material
rendering, a clean readable silhouette. Saturated-but-dark earth brown (hue
near #5A3F2A), a wet rim highlight in a lighter brown, never gold, orange or
yellow, and no green anywhere (the keyer removes by hue).

- `mud_glob`: one heavy, wet glob of mud thrown to the right, about 45% of the
  canvas width, tapering into a short stringy tail and three separate drips
  trailing to the left. Slightly lumpy, one bulge forward. Depth read by two
  tones of brown and a thin dark rim. No dirt specks flying off it.
- `mud_splat`: the glob caught the instant it lands on something flat to its
  left: a round dense core about 35% of the canvas, with thick radial
  tendrils ending in round drops, thin flat spatter around them, and two
  drops mid-fall. Wet and heavy, not an explosion: no spikes, no shards, no
  rock chunks, no debris cloud.
- No character, animal, floor, shadow, ring or ground plane. No text,
  numerals, frame, border, UI or watermark.

Must read as: a thrown handful of wet mud hitting something. Must NOT read
as: fire, lava, an explosion, blood, or a boulder.

Owner: what to change from the splat shape or the brown: ______
