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

## Gale Scythe: the blade that crosses the line

Plan 3.4, the one frame that carries meaning. Stand-in: `Resources/Spells/gale_scythe`
f5 (a whorl, rat-sized), stretched over the span by `place: formation`,
`align: span`. The verb is *reap*: one blade of wind crosses the whole line, then
every enemy is cut at once.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Gale Scythe/gale_scythe_blade.png`, sliced as
`Spells/gale_scythe_blade/f0`. It is laid on the line between the front and
the last enemy by the span layer (box 3:1, wide) and carried across it by
`drift`; the render fits the drawing into that box, so the drawing's own
proportion matters.

Style matches the existing wind frames and the Blackglass Spear stills:
Hades-inspired painterly fantasy, bold sculpted brush planes, rich material
rendering, a clean readable silhouette. Pale sea-green and white wind, a few
dark-gold leaf flecks caught in it; no other hue.

Canvas 3072x1024 (3:1), solid #00FF00 background, no gradient or vignette. No
green or yellow-green in the blade, its wisps or its glow beyond a pale
sea-green that stays well away from #00FF00 (the keyer removes by hue); the
white core stays opaque.

- One long crescent blade, lying almost flat, spanning 90% of the canvas width
  and about 25% of its height, thick at its centre and tapering to needle tips,
  its bright edge on the leading (right) side and a soft feathered trailing
  edge, curved slightly like a sickle moon on its side.
- Wind streaks trailing behind it to the left, parallel to the blade, in three
  or four thin wisps of decreasing length. A handful of small leaf flecks in the
  wisps.
- No whorl, no tornado, no spiral, no ring and no ground plane.
- No character, enemy or weapon, no scythe handle, no hand, no skull.
- No text, numerals, frame, border, UI or watermark.

Must read as: a single cutting stroke of wind passing sideways through a line
of enemies. Must NOT read as: a tornado, a fan or a slash mark made by a claw.

Owner: what to change from the blade curve or the wisps: ______

## Velvet Shackles: the cuff pulled tight

Plan 3.5. Stand-ins today: `Resources/Spells/velvet_shackles` (f0-f4 ribbons, f5-f6 the
held cuff) and a procedural gold ring (`tools/make_spell_fx.py shackle_cinch_ring`). The
verb is *bind*; what a painted piece fixes is the cinch itself, which the stand-ins fake
with an oversized ring snapped inward.

Target: one short sheet of 4 frames in a 2x2 grid, raw file
`Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_cinch.png`, sliced as
`Spells/velvet_shackles_cinch/f0..f3`, played at 25 fps on the cue. Frame 0: the ribbon
loops hanging slack round an empty space, frame 1: the loops pulled half closed with the
chain links taut, frame 2: fully closed, ribbon creased and pinched at the clasp, frame 3:
the same, a faint rebound shimmer. All four frames share one centre, one scale and one
silhouette outline so the crop does not jump.

Style matches the existing velvet frames: Hades-inspired painterly fantasy, deep crimson
velvet with a gold-thread edge and gold chain links; no other hue.

Canvas 2048x2048 (each cell 1024x1024), solid #00FF00 background, no gradient or vignette.
No green in the velvet or the gold (keyer removes by hue); dark interiors stay opaque.

- One ring of ribbon with a gold chain clasp, seen from slightly above, empty in the
  middle so the enemy shows through.
- No hand, no body, no animal, no floor, shadow or ground plane, no spikes.
- No text, numerals, frame, border, UI or watermark.

Must read as: a restraint being pulled tight round something. Must NOT read as: a halo,
a ring portal, a wedding ring or a bracelet.

Owner: what to change from the tightness or the ribbon material: ______

## Viper's Bite: the strike

Plan 3.6. Stand-ins today: `Resources/Spells/vipers_bite` (jaws f0-f4, the snap f5, drip
f6-f11) and the venom particles (f2 splash crown). The verb is *strike*; what a painted
piece fixes is the lunge and the second payoff's shape, which are the old drawings
repositioned.

Target: two stills, raw files in `Assets/_Project/Art/Sheets/Spells/Viper's Bite/`:
`vipers_bite_lunge.png` (the snake's head mid-strike, side view, mouth wide, fangs
forward, body curling out of frame at the left) and `vipers_bite_burst.png` (venom
bursting upward out of a bitten body: a crown of green droplets and a thick vertical
plume). Sliced as `Spells/vipers_bite_lunge/f0` and `Spells/vipers_bite_burst/f0`. The
lunge replaces the descending jaws (f0-f4); the burst is the `pop` layer on the
detonation.

Style matches the existing viper frames: Hades-inspired painterly fantasy, bold sculpted
planes, glossy venom, ivory fangs. Acid green and near-black green shadow, ivory; no other
hue.

Canvas 2048x2048 each, solid #00FF00 background, no gradient or vignette. The keyer removes
by hue, so the venom must be a deep emerald-teal that stays well away from #00FF00, with an
opaque dark interior.

- Lunge: one snake head, three-quarter side view, open mouth, two curved fangs dripping, a
  single trailing curve of neck. Nothing else.
- Burst: symmetrical crown of droplets around a central plume, no body, no skin.
- No character, no rat or other prey, no floor, shadow or ground plane, no skull.
- No text, numerals, frame, border, UI or watermark.

Must read as: a snake strike, then venom erupting. Must NOT read as: a dragon, a monster
mouth, a skull, an explosion or a splash of water.

Owner: what to change from the snake's species or the venom colour: ______

## Ashen Reckoning: the poison catches fire

Plan 3.7. Stand-ins today: `Resources/Spells/ashen_reckoning` f3 (the green swirl) held on
the Poison packet and f5-f11 (a fireball around a rock, then ash) on the Fire packet, 0.05 s
apart. The verb is *ignite*; what a painted piece fixes is the catch itself, which is two
unrelated drawings back to back.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_catch.png`, sliced as
`Spells/ashen_reckoning_catch/f0`. It replaces the two accents with one drawing on the cue.

Style matches the existing ashen frames: Hades-inspired painterly fantasy, bold sculpted
flame planes, rich material rendering. Toxic green flame at the core turning to orange at the
tips, black ash flecks; no other hue.

Canvas 2048x2048, solid #00FF00 background, no gradient or vignette. The keyer removes by
hue, so the green flame must be a deep teal-emerald well away from #00FF00 with a bright
white-yellow core that stays opaque.

- One radial burst of flame, the inner half toxic green, the outer half orange, tongues
  spiralling outward as if turning, a handful of ash flecks and three small drops of green
  fire.
- No rock, no coal, no skull, no creature, no ring, no floor or ground plane.
- No text, numerals, frame, border, UI or watermark.

Must read as: poison vapour igniting. Must NOT read as: a fireball, a nuke, a magic circle
or a lava burst.

Owner: what to change from the green or the flame shape: ______

## Gilded Aegis: the shield locks shut

Plan 3.8. Stand-in today: `Resources/Spells/gilded_aegis` f0-f4 (four shards closing) and f5
(the lock) held on the cue, with the procedural `aegis_mote` as the delivery. The verb is
*encase*; what a painted piece fixes is the lock itself, which today is one frame of an animated
sheet with loose shards still in it and the ally's face hidden behind it.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Gilded Aegis/gilded_aegis_lock.png`, sliced as
`Spells/gilded_aegis_lock/f0`, drawn on the cue in place of f5 at `fit: target`. Layer fields
only; no sheet.

Style matches the existing aegis frames: Hades-inspired painterly fantasy, bold sculpted brush
planes, rich material rendering. Burnished gold and warm white light, a faint cream fleece
texture in the shield's rim; no other hue.

Square canvas 1536x1536, solid #00FF00 background, no gradient or vignette. No green or
yellow-green anywhere in the subject or its glow; the gold must lean orange, never lime.

- One closed ward, centred, filling about 80% of the canvas, at least 8% margin on every side:
  a tall rounded shield outline built of interlocking gold plates, with a small crown at its
  top and a thin halo ring around it.
- The centre is open and clear: the outline and its glow occupy the outer quarter only, so the
  ally's body and face stay visible through it.
- The plates sit flush, seams closed: it reads as finished and locked, not assembling.
- A soft gold glow on the outer edge only, a few gilt leaves caught in the rim.
- No loose shards, no figure, no hand, no floor or ground plane, no runes or circle on the
  ground.
- No text, numerals, frame, border, UI or watermark.

Must read as: a golden shield closed around my ally. Must NOT read as: a mirror, a portal, a
cage, or a solid gold plate hiding the body.

Owner: what to change from the shield shape or the open centre: ______

## Borrowed Moment: the step taken early

Plan 3.9. Stand-in today: `Resources/Spells/borrowed_moment` (the hourglass turning, f5 the cyan
clock-hand starburst on the cue, f6-f11 the glass breaking into rising sand). The verb is
*quicken*. The weak point is that "faster" lives only in the 40 px turn-order chip and the log
line; nothing on the ally's body says speed.

The best fix is a runtime afterimage (the ally's own sprite, doubled and offset forward, cyan
tinted, fading): it needs no art and fits every body. That is an engine feature, not a prompt.
Until it exists, this still is the body-independent piece that carries the same idea.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Borrowed Moment/borrowed_moment_quicken.png`, sliced as
`Spells/borrowed_moment_quicken/f0`, drawn on the ally on the cue at `fit: target` and slid
forward with `drift`.

Style matches the existing borrowed_moment frames: Hades-inspired painterly fantasy, bold
sculpted brush planes. Cold cyan and white light, pale gold sand grains; no other hue.

Square canvas 1536x1536, solid #00FF00 background, no gradient or vignette. No green or
yellow-green anywhere; the cyan must lean blue, never teal-green.

- Three nested chevron-shaped arcs of cyan light, stacked left to right and pointing right, the
  front one brightest and sharp, the two behind fainter and softer, like after-images of a
  forward step.
- Fine streams of glowing sand trailing back to the left from each arc, thinning to nothing.
- One small clock hand, pale gold, laid along the front arc and pointing right.
- The middle third of the canvas stays mostly clear so the body shows through.
- No figure, no hourglass (the sheet already has it), no clock face, no numerals, no floor.
- No text, frame, border, UI or watermark.

Must read as: time sped up, a step taken early. Must NOT read as: an arrow attack, a dash
strike, or a wind spell.

Owner: what to change from the chevrons or the sand: ______

## Censer of Embers: the embers catch

Plan 3.10. Stand-in today: `Resources/Spells/censer_of_embers` f0-f4 (the censer swinging on its
chain) and f5 (a column of flame) held on the cue, plus six procedural flame licks. The verb is
*smolder*. The weak point: the payoff is a column drawn in front of the body, hiding the
treant's face for about 0.2 s, and f5 still carries a smoke wisp. The NOTES' "body burning at
the edges" cannot be one still (bodies differ); the lingering burn on the body itself is the
coal glow's job (plan 3.11, built). This piece is the catch at the body's base.

Target: one still, raw file
`Assets/_Project/Art/Sheets/Spells/Censer of Embers/censer_of_embers_catch.png`, sliced as
`Spells/censer_of_embers_catch/f0`, drawn on the cue at the target's feet (`fit: target`) in
place of f5.

Style matches the existing censer frames: Hades-inspired painterly fantasy, bold sculpted flame
planes, rich material rendering. Deep red, orange and gold flame, dark coal shapes; no other hue.

Wide canvas 2048x1024, solid #00FF00 background, no gradient or vignette. No green or
yellow-green anywhere in the flame, the coals or the glow.

- A low crescent of flame tongues rising from a scatter of glowing coals, spread across the
  full width like a ring seen from the front at ground level, the tongues tallest at the two
  ends and lowest in the middle, so the centre stays open for the body.
- Flame height at most 40% of the canvas; the top half is empty except a few rising sparks.
- Bright gold where flame meets coal, darkening to red at the tips.
- No smoke, no column, no censer, no chain, no floor plane or ground texture, no figure.
- No text, numerals, frame, border, UI or watermark.

Must read as: the embers caught and the target is set alight. Must NOT read as: a wall of fire,
an explosion, or a campfire.

Owner: what to change from the crescent or the flame height: ______
