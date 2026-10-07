# Spell art batch, 2026-10-07

Every piece the spell workflow is waiting on, paste-ready. For each piece: paste the **style block**, then the **piece block**, then its **delivery line**. Save each PNG under its delivery path. I inspect every piece (step 5) before it is wired.

Sources (kept in sync by hand; the NOTES are canonical): `docs/captures/spell_rework/after/court_of_whispers/NOTES.md`, `docs/captures/spell_rework/after/vipers_bite/NOTES.md`, `docs/captures/spell_rework/after/thorn_tithe/NOTES.md`.

## Style block (paste first, every time)

Upload as **rendering style** references only (not subject or palette):
`Assets/_Project/Art/Enemies/Giant_rat_sheet.png`, then
`docs/spell_art/reference/censer_hanging.png` and
`docs/spell_art/reference/censer_flame_loop.png` (approved spell pieces in the
same style). Optionally attach the rejected examples under "Do not make it like
this": `docs/spell_art/reference/rejected_*.png`. Style rules and reference
notes: `docs/spell_art/REFERENCE.md`.

Flat 2D game cel art matching the references: bold, uniform, dark outlines; flat
cel shading with two or three discrete tones per colour area; a small palette;
minimal internal texture. Clean game sprite art, not illustration. No painterly
gradients, no rim light, no painted glow or bloom, no soft airbrush, no
micro-texture. The game adds glow, flicker and darkness at runtime, so the art
stays flat.

Solid #00FF00 background, no gradient, vignette, floor or shadow plate. No green
or yellow-green anywhere in the subject (the keyer removes by hue). **If the
subject contains green or teal (venom), swap the backdrop to solid #FF00FF, drop
the "no green" line, say "no pink or magenta in the subject" instead, and key
the recipe `"keyed": "magenta"`.** No text,
numerals, frame, border, UI or watermark.

**Sheets:** a grid of equal cells. Every cell has the **same pivot at the same
pixel position** (stated per piece). Nothing crosses a cell edge. The drawing
changes between cells; the canvas, scale and pivot never do.

## Court of Whispers (backdrop #00FF00)

### 1. **Leaning courtier** (the payoff drawing; replaces `court_of_whispers_courtier-v2.png`)

```
Piece: Court of Whispers: a hooded courtier bent in to whisper
Beat it serves: payoff and resolution, 0.58-1.4 s (lean)
Filename: court_of_whispers_courtier_lean.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Court of Whispers/court_of_whispers_courtier_lean.png
Canvas: 1024x1024   Grid: one still
Pivot: the hood's opening at 82% across and 22% down; the hem trails to the lower left
Size in play: small, an 80 px box beside the target's head (about a rat's head and shoulders); it must read at that size
Drawing: one tall hooded figure in a long violet court robe with wide sleeves, bent forward from the waist to the
  right, one cupped hand raised beside the hood as if to speak into an ear. The hood is empty and dark: no face, no
  eyes. The hem breaks into a few ragged tongues that trail left and down. Bold dark outline with a thin pale lilac
  outer rim around the whole silhouette, flat cel shading in three LIGHT-to-mid violet tones (lavender to amethyst,
  never near-black) plus one pale lilac for a sleeve edge. A simple, chunky silhouette: few folds, readable as a
  thumbnail.
Verb: whisper.
Must read as: a courtier bending close to speak in someone's ear.
Must NOT read as: a grim reaper, a wraith with a scythe or claws, a mage casting, a ghost drawing life out.
Not in the picture: the target, a second figure, smoke, glow, floor, any weapon.
```

Delivery: one PNG named exactly `court_of_whispers_courtier_lean.png`, at the stated canvas size, on solid #00FF00, no green or yellow-green in the subject.

### 2. **Courtier from behind** (the orbit; replaces `courtier_back.png`)

```
Piece: Court of Whispers: the same courtier walking away, seen from behind
Beat it serves: orbit, 0.00-0.72 s (orbit_back and orbit_front, the second mirrored)
Filename: court_of_whispers_courtier_back.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Court of Whispers/court_of_whispers_courtier_back.png
Canvas: 1024x1024   Grid: one still
Pivot: the hood's centre at 80% across and 12% down; the hem at the bottom
Size in play: small, 52 px (far arc) to 70 px (near arc) boxes at the target's head; it must read at that size
Drawing: the same figure, same robe and sleeves, standing upright and turned three quarters away, the hood facing
  right and a little back toward the viewer, arms hanging in the wide sleeves, the hem trailing left in the same
  ragged tongues. Same outline, pale lilac outer rim and light violet tones as piece 1: it must be recognisably the
  same courtier.
Verb: whisper (the approach to it): processing round someone.
Must read as: one courtier gliding past; the same person as the leaning one.
Must NOT read as: a statue, a ghost with a face, a different character.
Not in the picture: the target, other figures, smoke, glow, floor.
```

Delivery: one PNG named exactly `court_of_whispers_courtier_back.png`, at the stated canvas size, on solid #00FF00, no green or yellow-green in the subject.

### 3. **Smoke and wisps** (the ending, the motes and Shawn's price flicker)

```
Piece: Court of Whispers: smoke curl, thin wisp, motes, flame lick
Beat it serves: resolution 0.95-2.65 s (smoke_a, smoke_b, curl), payoff motes 0.70 s (settle), price flicker 0.88 s (price)
Filename: court_of_whispers_wisps.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Court of Whispers/court_of_whispers_wisps.png
Canvas: 1024x256   Grid: 4x1 of 256x256
Pivot: centre of every cell (50%, 50%); cell 1 and 2 rise from their bottom third
Size in play: cell 1 about a third of a body's width, cell 2 smaller, cell 3 tiny, cell 4 about Shawn's head
Drawing: flat cel shapes, bold dark outline, the courtier's own light violet in two tones and a pale lilac rim, so the
  smoke reads as the robe coming apart (never grey, yellow or white). Cell 1 a single thick curl of smoke with
  one spiral at its head. Cell 2 a thin S-shaped wisp. Cell 3 three small pale teardrop motes, one larger. Cell 4 a
  small flame-like lick of violet, three points, as a candle flame guttering.
Verb: come apart into smoke; flicker.
Must read as: smoke from a snuffed candle; a flicker of cold flame.
Must NOT read as: a cloud, a fire (orange), a ring or halo, a ghost shape.
Not in the picture: figures, glow, floor, background texture.
```

Delivery: one PNG named exactly `court_of_whispers_wisps.png`, at the stated canvas size, on solid #00FF00, no green or yellow-green in the subject.

## Viper's Bite (backdrop #FF00FF, recipe keyed "magenta")

Magenta backdrop: replace the style block's no-green line with "no pink or magenta in the subject".

### 4. **Snake eyes** (the anticipation)

```
Piece: Viper's Bite: a pair of snake eyes
Beat it serves: anticipation, 0.04-0.38 s (eye_a, eye_b)
Filename: vipers_bite_eyes.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Viper's Bite/vipers_bite_eyes.png
Canvas: 512x256   Grid: 2x1 of 256x256
Pivot: the eye's centre at 50% across, 50% down in each cell
Size in play: small, a 32 px box each, at face height beside the head; slit pupils and a pale rim so the pair reads as eyes, not two dots, at that size
Drawing: cell 1 an eye half open (a thin lime lens with a vertical slit), cell 2 the eye fully open (a lime almond with a dark vertical slit pupil
  and a small pale highlight). Bold dark outline, flat cel in two tones of yellow-lime (never near-black fill).
Verb: watch.
Must read as: a snake's eye opening in the dark.
Must NOT read as: a cat's or human eye, a lamp, a round dot, a face.
Not in the picture: head, lid skin, body, glow, floor.
```

Delivery: one PNG named exactly `vipers_bite_eyes.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 5. **Jaws** (the delivery, the payoff and the settle)

```
Piece: Viper's Bite: the jaws closing
Beat it serves: delivery 0.14-0.40 s (jaws), payoff 0.40 s (bite), settle 0.50-0.70 s (closed)
Filename: vipers_bite_jaws.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Viper's Bite/vipers_bite_jaws.png
Canvas: 1536x1024   Grid: 3x2 of 512x512
Pivot: the middle of the gap between the jaws, 50% across and 50% down, in every cell
Size in play: big, a 190-230 px box clamping the whole rat or a treant's face and upper trunk (owner: bigger jaws); the fangs must read on dark fur
Drawing: one pair of snake jaws seen from the side, an upper and a lower half with a gap between them; no head, no eyes, no neck.
  Cell 1 the halves far apart with two small fang tips; cell 2 closer, fangs longer; cell 3 wide open, two long ivory fangs; cell 4 the jaws
  nearly shut with a flat starburst of short lime rays between the fangs; cell 5 shut, the fangs crossed; cell 6 shut, a drop of venom on each fang
  tip. Teal-green scales in two flat tones with a light lime edge, bold dark outline, ivory fangs in two tones.
Verb: bite.
Must read as: a mouthful of fangs closing on something.
Must NOT read as: a snake's head, a cartoon monster, a wolf trap, a flower or butterfly.
Not in the picture: head, body, eyes, target, floor, smoke.
```

Delivery: one PNG named exactly `vipers_bite_jaws.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 6. **Venom drip and mist** (the resolution)

```
Piece: Viper's Bite: venom drips and mist
Beat it serves: resolution 0.55-1.45 s (drip, mist, settle drops)
Filename: vipers_bite_venom.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Viper's Bite/vipers_bite_venom.png
Canvas: 1024x512   Grid: 4x2 of 256x256
Pivot: the top centre of each cell for cells 1-4 (the drip hangs from it); the middle for cells 5-8
Size in play: cells 1-4 about a rat's torso tall (150 px); cells 5-8 small, 15 to 40 px particles
Drawing: cells 1-4 a sheet of venom running down fur: a few thick lime rivulets ending in fat drops, the drops falling lower and thinning
  from cell 1 to cell 4. Cells 5-6 a single round venom drop (big, small). Cells 7-8 a soft curl of green vapour (thick, thin) as flat cel shapes,
  not airbrush. Light lime fill, mid green shade, dark outline.
Verb: seep.
Must read as: venom running off a bitten body, then a little green vapour.
Must NOT read as: slime, paint, blood, tears, rain.
Not in the picture: fur, body, jaws, floor, glow.
```

Delivery: one PNG named exactly `vipers_bite_venom.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 7. **Venom burst** (the detonation; optional restyle of the owner's painted piece to the flat style)

```
Piece: Viper's Bite: the venom burst
Beat it serves: detonation, 0.58 s (pop)
Filename: vipers_bite_burst_flat.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Viper's Bite/vipers_bite_burst_flat.png
Canvas: 1024x1024   Grid: one still
Pivot: the base of the plume, 50% across and 85% down
Size in play: a 200 px box over a rat (the plume is about a rat tall); it must read at that size
Drawing: a thick plume of venom bursting upward from a point, with a ring of round droplets thrown out around it; flat cel in two or three tones of
  LIGHT lime and one mid green (never near-black), bold dark outline, a few pale highlight droplets.
Verb: burst.
Must read as: venom exploding out of a body.
Must NOT read as: an oil splash, a fountain, a tree, an explosion with fire.
Not in the picture: target, jaws, floor, smoke, glow.
```

Delivery: one PNG named exactly `vipers_bite_burst_flat.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

## Thorn Tithe (backdrop #FF00FF, recipe keyed "magenta")

Magenta backdrop: replace the style block's no-green line with "no pink or magenta in the subject".

### 8. **Back vine set** (delivery behind the body)

```
Piece: Thorn Tithe: back vines rising and closing into an arch
Beat it serves: delivery 0.10-0.40 s and payoff 0.40 s (back)
Filename: thorn_tithe_back_vines.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_tithe_back_vines.png
Canvas: 4480x640   Grid: 7x1 of 640x640 (never touching a cell edge)
Pivot: the base line of the soil mounds, 90% down, in every cell; the left foot at 25% across and the right foot at 75% across in every cell
Size in play: the cage is as wide as the target (a rat's body, 290 px box) and about as tall as it is wide, a squat dome, never a tall loop; it must read at that size
Drawing: a bunch of three bramble canes with large white thorns, growing from a small mound of soil on each foot. Cells 1-2 three short canes
  on each foot (a sway pair, the second leaning the other way); cells 3-4 the canes taller, already leaning inward; cells 5-6 the canes long and
  bending over toward the top centre, their tips almost meeting; cell 7 the cage closed: the same canes, now meeting and twisting together at the
  top into a low dome, a mound on each foot. Cell 7 must be the visible continuation of cell 6 (same canes, same feet), not a new drawing. Dark teal canes in two flat tones, white
  thorns in two tones, soil mounds in two browns, bold dark outline on everything.
Verb: cage.
Must read as: thorny canes forcing up out of soil and arching shut.
Must NOT read as: a plant, a tree, a rope, a hoop, a horseshoe, a crown.
Not in the picture: target, floor, shadow, glow, leaves, text.
```

Delivery: one PNG named exactly `thorn_tithe_back_vines.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 9. **Front vine set** (delivery across the body)

```
Piece: Thorn Tithe: front vines rising and crossing
Beat it serves: delivery 0.10-0.40 s and payoff 0.40 s (front)
Filename: thorn_tithe_front_vines.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_tithe_front_vines.png
Canvas: 4480x640   Grid: 7x1 of 640x640
Pivot: exactly the same as the back set: base line 90% down, feet at 25% and 75% across, in every cell
Size in play: the same box as the back set (they are drawn one over the other); shorter than the back arch so the arch's top shows above the crossing
Drawing: the near canes, a bunch of two or three on each foot, growing in the same steps as the back set (cells 1-2 short, 3-4 taller and
  leaning in, 5-6 long and reaching across); cell 7 the same canes crossed over the middle in a low X, a mound on each foot. Cell 7 continues
  cell 6; it is not a new drawing. Same colours and outline as the back set, a little lighter so the front reads nearer.
Verb: cage.
Must read as: the near canes crossing in front of the target.
Must NOT read as: a letter X, a fence, scissors, a gate.
Not in the picture: target, floor, shadow, glow, leaves, text.
```

Delivery: one PNG named exactly `thorn_tithe_front_vines.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 10. **Soil crack and dust** (anticipation)

```
Piece: Thorn Tithe: the soil cracking
Beat it serves: anticipation 0.00-0.20 s (dust, a new `crack` layer)
Filename: thorn_tithe_crack.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_tithe_crack.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: the middle of the crack, 50% across and 70% down, in every cell
Size in play: as wide as the target's feet apart (a 290 px box), flat on the ground
Drawing: a wide, shallow crack in dry soil seen from the side-front: cell 1 two hairline cracks, cell 2 a jagged crack with a lip of soil, cell 3 the
  crack open with clods rising and a brown dust puff, cell 4 the dust puff thinning with clods falling. Two flat browns, dark outline, pale tan dust.
Verb: split.
Must read as: ground cracking and dust jumping up.
Must NOT read as: lava, a fault line, a lightning bolt, a spider web.
Not in the picture: vines, target, glow.
```

Delivery: one PNG named exactly `thorn_tithe_crack.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.

### 11. **Sap, leaves, clods** (payoff and resolution particles)

```
Piece: Thorn Tithe: sap bead, copper leaf, bark chip, soil clod
Beat it serves: payoff 0.40 s (sap), resolution 0.40-0.90 s (leaves), anticipation (clods)
Filename: thorn_tithe_particles_flat.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Thorn Tithe/thorn_tithe_particles_flat.png
Canvas: 1024x256   Grid: 4x1 of 256x256
Pivot: the middle of each cell
Size in play: 15 to 40 px particles
Drawing: cell 1 a round sap bead, light lime with a small pale highlight; cell 2 a dead copper-brown leaf, slightly curled; cell 3 a flake of bark;
  cell 4 a soil clod. Two flat tones each, bold dark outline.
Verb: scatter.
Must read as: bits shaken off by the snap.
Must NOT read as: a gem, a flower petal, a coin, fruit.
Not in the picture: vines, hands, glow, sparkle.
```

Delivery: one PNG named exactly `thorn_tithe_particles_flat.png`, at the stated canvas size, on solid #FF00FF, no pink or magenta in the subject.
