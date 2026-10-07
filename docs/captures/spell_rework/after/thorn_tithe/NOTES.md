# Thorn Tithe: state record

Trial 3, spell 3 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), steps 1 and 2 "identity sheet, block with existing assets", round 1.
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3 "Thorn Tithe".
**Status: step 6 done by the worker (2026-10-07; art wired, see "Step 6"); the orchestrator's check is next. Nothing here is owner-reviewed.**
The 2026-10-04 passes (12-frame stand-in, then the painted four-frame growth sheet) are superseded; their GIFs
(`thorn_tithe_rat_1x.gif` etc.) and `spell_feel_1x/`, `spell_feel_1.5x/` stay untracked beside this file.

Captures 2026-10-07 with `tools/gif.ps1 spell thorn_tithe` (`-Vs rat`, `-Vs treant`, `-Vs treant -Speed 1.5`). Phone previews are committed
(`thorn_tithe_r1_*_preview.gif`); the full GIFs (`thorn_tithe_r1_*.gif`, 5-7 MB) are untracked; `r1_*_timing.json` is each capture's
`timing.json`. `tools/spell_feel.ps1` was run once, 1x, treant and rat (`spell_feel_r1_1x/`).

## Identity sheet

Locked by the orchestrator's plan; the owner confirms or edits it with the first GIF.

- **Verb:** cage (thorns grow up out of the ground and close round it).
- **Signature beat:** the cage snaps shut round the body on the cue, with the sap burst, the Thorned badge and a small stage kick.
- **Must read as:** "thorn vines grew out of the soil and caged it", then sank back and shed a few leaves.
- **Must NOT read as:** a vine laid over the body or sliding down onto it; a cage bigger than the body that hides it (the old pass hid the treant); a
  tree or a plant growing; a generic sparkle.
- **Motion reference per moving element** (a real thing, not invented parameters):
  - `back`, `front`: bramble canes forcing up out of soil, fast and in steps, base fixed; they arch over and cross, hold, and draw back down their own
    length (the growth drawings played backwards), so the base never moves;
  - `dust`: soil clods thrown up as the canes break the surface, falling under gravity;
  - `sap`: beads flung from the crossing as the canes close, falling;
  - `leaves`: a few dead leaves shaken loose, tumbling down slowly (drag, spin).
- **Intentional exceptions:** the growth is drawn in two-frame steps (a sway pair per growth stage), as the strips were delivered; the cage closes in one
  frame on the cue (arch and X are the cue frame, f6); the layers cross-fade between drawings (engine behaviour).
- **Mechanics, unchanged:** Thorned for 3 turns, 5 Nature at the start of its turn and after each physical move; cue sound, badge and number on one frame.

## Beat table

Authored seconds on the release clock; hit cue **0.40 s** (was 0.30; the cast sound is re-cut to 0.40 s with `make_spell_cast_cues.py`, pinned by
`MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`). Cue = f22 at 1x, f15 at 1.5x (`timing.json`; authored 0.40 s is f24 minus the first-drawn-wait
frames). Single target (`SingleEnemy`), so the back vines take the one ground renderer (ground band holds 2); no E5 question. "Visible" is the
stage-crop check: layer box = size x fit factor, centred on the body's ground line.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.02-0.30 | Soil thrown up at the feet (anticipation) | `dust` | emitter, ground line | 8 clods, 0.15-0.30 s, falling |
| 0.10-0.40 | Left vine rises from its mound behind the body | `back` f0-f5 | yes, 290 box (fit), base on the ground line | 20 fps, three growth steps of two frames |
| 0.10-0.40 | Right vine rises in front of the body | `front` f0-f5, mirrored | yes, same box and pivot | same clock |
| 0.40 | **Cue.** The cage closes: the arch behind, the X across the body. Sound, number, Thorned badge, stage kick on this frame | `back` f6, `front` f6 | yes, the whole 290 box | one frame, no glow; biggest area change of the spell |
| 0.40 | Sap beads burst from the crossing | `sap` | emitter | 12 beads, 0.2-0.4 s, glow 0.5, falling |
| 0.40-0.60 | Cage held | `back`, `front` f6 | yes | 4 frames |
| 0.40-0.50 | A few leaves shaken loose | `leaves` | emitter | 4 leaves, 0.3-0.4 s life, tumbling down |
| 0.60-0.80 | Thorns sink back down their own length | `back`, `front` f10-f13 | yes | the growth drawings f4, f3, f2, f1 backwards, 20 fps |
| 0.80-0.90 | Last vine thins | `back`, `front` | yes | `fade` 0.1 |

Everything is gone at 0.90 s: 0.50 s after the cue (the residue limit). Stage kick `{strength 0.025, rise 0.03, fall 0.18}` as Censer and Ashen.

## Playback, as seen (my own frame contact sheets, 60 fps; the orchestrator has not yet watched)

Rat 1x (frames cut 500x380 round the body; cue f22):
- **f0-f4.** The rat idles; at f4-f6 small mounds with a first shoot show at both feet, with a few brown clods over the left one.
- **f6-f21.** A vine stands on each side of the rat, the left one behind its front legs, the right one in front of its tail; they grow in three steps
  (short at f6, mid at f10-f14, full height with a curled tip at f18-f21). The rat is fully readable between them. The sway pair shows as a blend
  (cross-fade) at f12 and f15.
- **f22 cue.** The cage closes: a thorn arch behind and an X across the rat, as one shape about 1.6 rat-heights tall and as wide as the rat. The rat's
  head and chest read between the canes; its legs and belly are crossed. The Thorned badge `3` is under the body from f22. Sap beads and the leaves are small and pale; they read as specks.
- **f23-f34.** Cage held, then the canes thin. **f36-f48.** The cage has opened into the two single vines, sinking down their own length; the base mounds
  stay. **f54.** Clear.
- Cage vs body: the cage is as wide as the rat but about 1.6x its height, so the arch stands above the rat. Not hiding it (about a third of the
  rat is crossed by canes), but taller than "sized to the body".

Treant 1x (cue f22): the same beats. The vines are smaller than the tree (fit factor), so the cage reaches to the canopy's lower edge; the X crosses the
trunk and arms, the face stays visible above the crossing, the back arch is behind the trunk and mostly hidden by it, which gives the "around it"
depth. Against brown bark and the green canopy the blue-teal canes with white thorns are the saturated thing on stage and read at once.
f36-f48 the canes sink; clear by f52.

Treant 1.5x: cue f15; growth f0-f14, closed f15-f22, sinking f26-f32, clear by f35; the same shape in two thirds of the time. The dust and the sap
are the first things to go missing at this speed; the cage still reads.

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass | no layer moves (no drift, rotate or scale); the only change is the drawing, and the cage frame is registered on the same base (below) |
| Cue on one frame | pass | `timing.json` `hitFrame` 22 at 1x, 15 at 1.5x; sound, badge and the cage frame together; `back` and `front` are `at: release`, so the cage is not a tail (no `TailScale`) |
| Nothing marked visible leaves the stage | pass | 290 box at ground level on rat and treant; the left vine's tip at f20 reaches the top of the crop but stays on the stage |
| Frame registration (E7) | pass | both recipes share one 490x504 canvas, no per-frame crop; the base line is row 492 in every frame of both sets; the arch's feet sit at +-143 px of the centre and the single vines are registered on those feet (`shift` -143; mirrored for `front`) |

`spell_feel` (1x, runtime capture, `spell_feel_r1_1x/`), no warnings on either body:
- Treant: contact f22 (authored f24, visual f22); peak 5.8 at 0.40 s, which is the cue; hot peak 0.23%; residue 0.37 s after contact, clean at 0.78 s.
- Rat: contact f22 (visual f23); peak 3.5 at 0.40 s, the cue; hot peak 0.08% at f27; residue 0.37 s, clean at 0.75 s.
- The cue is the biggest energy rise on both. No glow on the vines: the rise is the area of the closing cage (the cage frame carries about twice the
  ink of the growth frames).

Advisory:

- **Pop-in:** the cage frame replaces the growth frame in one step (f21 to f22) by design; the mounds of the second foot appear on that frame
  (the right foot of `back`, the left foot of `front`).
- **Frozen holds:** the cage is held 0.2 s (4 frames at 20 fps) with the sap and leaves moving in front of it; nothing else holds.
- **Flash guard:** no glow on the sprites; the sap's glow 0.5 on 12 small beads; the typed Nature hit flash is the shared one.

Gates: dotnet slice `ThornTithePresentationTests`, `MeleeSoundOverrideTests`, `SpellPoolCapacityTests`, `SpellVfxRecipeDriftTests`, `SpellLayerRulesTests`,
`SpellBaselineTimingTests` green (131). The commit gate result is in the report.

## Rubric (worker's score; the orchestrator's check is still due)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ~. Rise, close, hold, sink and leaves are distinct and in order. The soil crack is not drawn (dust only) and the sap
   and leaves are specks, so anticipation and resolution lean on the vines.
2. Weight and easing feel physical: ~. The canes grow in steps with a sway pair, the sink is the growth played backwards; no ease inside a step.
3. One focal point at a time: yes at the cue (the cage closing); during the rise the two vines are the only moving things.
4. The body stays readable at the hit: ~. The rat's head and chest read through the cage; the cage is 1.6 rat-heights tall.
5. The ending decays, not cuts: yes. The canes sink and thin over 0.3 s, clear at 0.5 s after the cue.
6. Distinct from and consistent with the approved spells: yes on identity (green-blue thorns from the ground); the flat cel art matches the rat sheet's
   style, the particles (painted) do not.

Art: the vine strips are flat cel in the rat style (bold dark outline, two or three flat tones, white thorns); the particles are the old painted
atlas cells. Not scored.

## Art record (one row per piece)

| Piece | Accepted prompt | Source file | Recipe | Output | Attempts |
|---|---|---|---|---|---|
| Back vines (stand-in, flat cel) | not recorded (owner's 2026-10-04 evening delivery) | `Art/Sheets/Spells/Thorn Tithe/thorn_back_vines.png` (2172x724, 7 drawings, #00FF00) | `recipes/thorn_tithe_back.json` (new) | `Resources/Spells/thorn_tithe_back` (f0-f5 growth, f6 arch, f7-f9 arch held, f10-f13 sinking) | 0 |
| Front vines (stand-in, flat cel) | not recorded | `thorn_front_vines.png` (same layout) | `recipes/thorn_tithe_front.json` (new) | `Resources/Spells/thorn_tithe_front` (same frame map; f6 is an X) | 0 |
| Clods, sap bead, leaf (stand-in, painted) | n/a | `thorn_tithe_atlas_v1.png` cells 13-16 | `recipes/thorn_tithe_particles.json` (unchanged) | `Resources/Spells/thorn_tithe_particles` (f0 thorn, f1 leaf, f2 bark chip, f3 sap bead) | 0 |
| Painted four-frame growth (superseded, not referenced) | n/a | `thorn_tithe_growth.png` | `recipes/thorn_tithe_growth.json` | `Resources/Spells/thorn_tithe_growth` | pass 2 |
| Painted 12-frame atlas (superseded, not referenced) | n/a | `thorn_tithe_atlas_v1.png`, `_v2.png` cells 1-12 | `recipes/thorn_tithe.json` | `Resources/Spells/thorn_tithe` | pass 1 |

The strips are not on an equal grid: cells 1-6 are single vines centred on mounds at x = 129, 416, 672, 958, 1215, 1502 and cell 7 is a closed shape
456 px wide, so the recipes use explicit rects and the shared canvas. The two superseded folders and recipes stay in the tree unreferenced; delete them
when the owner accepts the new motion.

### Art needed for step 4

**Step 3 passed (owner, 2026-10-07): round 1 motion approved; cue 0.40 s kept. Owner answers folded in: a bunch of canes per
side instead of one, the canes grow INTO the cage (no swap on the cue), and a squatter cage, about as tall as it is wide.**

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`; the style block and its attachments as in its section 1. **The subjects are blue-green, so the
backdrop is solid #FF00FF, the recipes are keyed `"keyed": "magenta"`, and "no pink or magenta in the subject" replaces the no-green line.** At play size
the pieces sit on a dark grey rat and a brown-and-green treant: saturated teal canes with white thorns and a dark outline read on both. The strips
already delivered are close to the target style; what step 4 needs is the same pieces on one grid, with the base on a fixed pivot, and the missing
anticipation and resolution pieces.

1. **Back vine set** (delivery behind the body)
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

2. **Front vine set** (delivery across the body)
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

3. **Soil crack and dust** (anticipation)
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

4. **Sap, leaves, clods** (payoff and resolution particles)
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

### Step 5: art inspection (orchestrator, 2026-10-07)

All four pieces delivered at the right canvas on solid magenta and accepted, with one watch item:
- **Back set:** a bunch of three canes per foot, growing taller and leaning in. Cell 7 is a low dome that continues
  cell 6.
- **Front set:** lighter teal; cells 5-6 bend across; cell 7 is a low X.
- Each drawing fills about half its 640 cell. Register the feet across cells in the recipe.
- **Crack:** hairline crack, then lipped crack, then dust and clods, then thinning. **Watch:** cells 3-4 carry light
  pink specks in the dust, a possible key spill. Check after keying, and drop them with `alpha_floor` or spill
  suppression if they survive.
- **Particles:** a yellow sap bead (lime was asked; yellow is acceptable), a copper leaf, bark and a clod.

## Step 6: art integrated and compared in context (2026-10-07)

Captures `tools/gif.ps1 spell thorn_tithe -Vs rat` and `-Vs treant`, 1x. Copies beside this file: `thorn_tithe_step6_{rat,treant}_1x.gif` (untracked),
`*_preview.gif` (committed), `step6_*_timing.json` (cue f24, freed f54: 0.40 s and 0.90 s). Round 1 timing (cue 0.40 s, 20 fps rise), beat order, the
ground-band back layer, the effects-band front layer and the kick are kept.

### Art record

| Piece | Source | Recipe | Output | Attempts |
|---|---|---|---|---|
| Back canes | `thorn_tithe_back_vines.png` (7x1 of 640) | `recipes/thorn_tithe_back.json` rewritten: hue key (magenta), `alpha_floor` 40, explicit rect 520x553 per cell, no crop | `Resources/Spells/thorn_tithe_back` (f0-f5 growth, f6-f9 dome held, f10-f13 sway frames f3-f0 backwards) | accepted at step 5, wired first try |
| Front canes | `thorn_tithe_front_vines.png` (7x1 of 640) | `recipes/thorn_tithe_front.json` rewritten, same rect and sequence | `Resources/Spells/thorn_tithe_front` (same map; f6 is the X) | accepted at step 5, wired first try |
| Crack | `thorn_tithe_crack.png` (4x1 of 512) | `recipes/thorn_tithe_crack.json` (new): hue key, `alpha_floor` 40, rect 440x240, `cap_blue` 0.83 | `Resources/Spells/thorn_tithe_crack` f0-f3 | one revision: the pink specks (below) |
| Sap, leaf, bark, clod | `thorn_tithe_particles_flat.png` (4x1 of 256) | `recipes/thorn_tithe_particles_flat.json` (new): hue key, `alpha_floor` 40, `content_crop` (213x230), `cap_blue` 0.83 | `Resources/Spells/thorn_tithe_particles_flat` f0 sap, f1 leaf, f2 bark, f3 clod | sizes revised once (specks) |

Both vine sets share one rect (x 60-580, y 34-587 of each cell: the union of both sets' content, feet at 25% and 75%, base row 575 so 12 px above the bottom),
so one canvas, one pivot, no per-frame crop (E7). `thorn_tithe_particles` and the old atlas stay: Spore Cloud and Splinters still use them, so the new
particles are a new folder. The superseded stand-in strips and the growth and 12-frame recipes stay unreferenced.

**The pink specks were real, not spill.** Crack cells 3-4 hold opaque salmon pixels (250,185,185) on the pale dust: tan with magenta mixed in, which the hue
key keeps (little magenta dominance) and `alpha_floor` cannot touch (alpha 255). New optional recipe key `cap_blue` (`tools/slice_spell_sheet.py`, documented
in `docs/ART_PIPELINE.md`) pulls blue down to 0.83 of green on pixels with red well above green and blue at or above green; tan has blue below green,
dark outline has red near green. After it no pixel in the four crack cells is pink.

**`skills.json`:** `back`/`front` size 290 to 341 (the dome is 442 of the 520 px canvas, so 290 wide on screen), `front` no longer mirrored (the set is symmetric),
new `crack` layer (offset 0, 0.2 s at 20 fps, size 300, effects band, `impactY` 0.133 = the cell's 70% pivot), `dust` weights bark and clod, `sap` bead and
`leaves` leaf on the new sheet with sizes about 2.5x (sap 0.3-0.45, leaves 0.4-0.55, clods 0.22-0.34), sap speed 140-340, leaves 60-140, bursts 10 and 5.
`ThornTithePresentationTests` pins size 341, front not mirrored, the crack at offset 0.

### Playback, as seen (60 fps, my own contact sheets, cue f24)

1. **Canes grow into the cage, no swap: yes.** Rat f6-f22: a bunch of three canes stands on each foot (short f6, tall and parting f12, leaning in at f18,
   tips meeting at f22), and f24 closes the same canes into the dome with the X across. The dome frame continues the last growth frame; the only step is
   the tips twisting shut. Back canes are taller than the dome (f4-f5 reach 528 px, the dome 417), so the cage settles down by a fifth on the cue.
2. **Low dome, about as wide as tall: yes.** Rat f24-f34: the dome is as wide as the rat (290 px) and about 270 px tall, a cane height above the
   rat's back. The rat's snout, teeth, ear and back read through it; the eye sits behind one cane. Treant f26: dome and X reach the canopy's lower edge,
   the face and the orange eyes stay readable above the X.
3. **Back arch behind, X in front: yes.** The dome's top shows above the body and behind the ears; the X crosses chest and flank in front. Treant: the arch
   mostly hidden behind the trunk, which gives the depth.
4. **Crack reads, no pink: yes.** Rat f2-f6: hairline cracks at the feet, a lipped crack with clods at f4, a tan dust cloud with clods at f6-f8 as the
   first canes break through. No pink specks after `cap_blue`. The dust cloud at f6-f8 is large and pale against the dark floor; it is the loudest thing
   before the cue.
5. **Sap and leaves read: yes.** f24-f34: a spray of yellow beads from the centre falling past the body, copper leaves at the top of the cage. Both sit
   behind the X's crossing, so they read as small clusters, not a burst.
6. **Clear within 0.5 s of the cue: yes.** Cue f24, canes sinking f36-f50, clear at f54 (`clearedSeconds` 0.900 against 0.40 + 0.5).

Weak, not changed: the cage's first sink frame (f36) is the tall sway pose, so the dome opens to tall canes in one step before they sink; the sap and
leaves overlap the X and are partly hidden by it.

### Worker's rubric (step 6)

1 yes (crack, rise, close, hold, sink, leaves in order). 2 ~ (growth in steps with a sway pair, no ease inside a step). 3 yes (the cage closing on
the cue). 4 ~ (a rat's face reads, its body is crossed by canes). 5 yes (the canes sink and thin over 0.3 s). 6 yes (teal canes on dark fur and brown bark).
Round log: Step 6 (2026-10-07), art swapped for the delivered sheets, `cap_blue` for the crack, particle sizes raised; caught by the worker's frame check.
Measures: owner reviews 0; internal retries 2 (pink specks, particle size); generation attempts per art piece 0 at this step; about 45 min active,
two content builds and two captures at 1x.
Snapshot id: `a68833d8` (step 6 commit); capture runs `20261007T211000Z_7eea78bebcb5` (rat), `20261007T211101Z_e4aca9c0a6d2` (treant).

### Orchestrator's check of step 6 (2026-10-07, step6 full GIFs, rat and treant 1x; frames at 60 fps)

For the owner's review. On the rat it works: the crack shows (f4), canes break out with dust (f8), grow and lean in
(f12-f22), close into a low dome with the X (f24), and sink (f38-f52). Open:
1. **Treant: the canes swamp the body.** During growth (f12-f22) the canes are taller than the treant and leave the
   top of the frame. The dome at f24 covers the trunk and the arms up to the face. Fit-to-target width makes the
   tall growth frames huge on a big body.
2. **The cage re-opens before it sinks.** At f38 the dome snaps back to the tall, open canes (the sink plays the
   growth frames backwards from the tallest pose), which pops on both bodies. The worker flagged the same. Fix
   options: sink from the dome's own frame with a fade, or draw sink frames.
3. The dust burst at f8 is large and pale, the loudest thing before the cue.

### Tuning after step 6 (2026-10-07; `thorn_tithe_tune_{rat,treant}_1x.gif`, strips beside it)

Changed: recipes `thorn_tithe_back`/`_front` sequence now f0-f5 then the dome f6 held for f6-f13 (the backwards sway frames f10-f13 are gone; only
those four PNGs per set changed). Layers `back`/`front`: `seconds` 0.7 to 0.5, `fade` 0.1 to 0.28, so the dome plays to 0.6 s and fades in place,
clear at 0.9 s (cue 0.40 s unchanged, size unchanged).

2. Cage re-opening at f38: **fixed**. Rat and treant, 0.60-0.90 s: the X and arch hold shape and thin out over the body; no tall open canes return.
   It leaves by fading, it does not sink into the soil; sinking would need drawn frames. Items 1 and 3 (treant size, dust) not touched.

## Open owner questions

Each asked once. Status: open, answered (with the answer), or dropped.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | Two vines (one behind on the left, one in front on the right) rise before the cage closes, instead of the plan's five. Fine, or should the art grow a full bunch on each side? | 1 | open |
| 2 | The cue moved 0.30 to 0.40 s so the vines have room to rise (the cast sound is re-cut). Keep, or hold the cue at 0.30 and speed the rise? | 1 | open |
| 3 | The cage is as wide as a rat but 1.6x its height (the strip's proportions). Accept, or ask for a squatter cage in the art? | 1 | open |

## Round log

| Round | What changed | Who caught it (owner / orchestrator / binding check / worker) | Rubric result |
|---|---|---|---|
| Pass 1 (2026-10-04) | The 12-frame painted atlas; a sap-burst star accent | worker | not scored |
| Pass 2 (2026-10-04) | The owner's painted four-frame growth sheet in two layers (`grow`, `back` mirrored at 0.5); cage as f3 of one `grow` layer because an `at: hit` sprite is a tail (`TailScale`, 0.6 of the growth); old accent removed; `back` mirrored (`facing: reverse`); badge lifted with `impactY` 0.36. Found: the cage was bigger than the treant and hid it, and slid down from half size instead of rising | worker; the owner's brief for this trial | not scored |
| Round 1 (2026-10-07) | New design on the owner's flat-cel strips: recipes `thorn_tithe_back`/`_front` (E7 registration, shared canvas, arch feet registered with the single vines, retract frames baked); layers `back` (ground) and `front`, `dust`, `sap`, `leaves`; cue 0.30 to 0.40 and the cast sound re-cut; `vfx.kick`; no drift, no scale. Tuned within the round: size 330 to 290, sink by drift (-0.25) replaced by baked backward frames (the drift pushed the canes through the floor line in view) | worker (own frame sheets; `spell_feel`) | worker's score above; step 3 pending |
| Tuning after step 6 (2026-10-07) | Sink frames replaced by the held dome, `seconds` 0.5, `fade` 0.28 | orchestrator's step 6 check | cage fades from the dome on both bodies |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | 1 (the sink: drift replaced by baked frames, size 330 to 290) |
| Generation attempts per art piece | 0 (stand-ins) |
| Active work time / waiting time | about 1 h, of which the content builds, captures, tests and `spell_feel` were about 12 min |
| Owner handling time (generate, download, upload art; review) | none yet |
| Owner-found defects the binding checks should have caught (target 0) | 0 so far |
| Final quality against the reference pack (owner's verdict on the rubric) | not yet |

## Reviewed snapshot

- Commit: see the report (the commit that lands this file).
- Capture run ids: `20261007T184717Z_65af58fb43b8` (rat 1x), `20261007T184609Z_e601322bdf6c` (treant 1x), `20261007T184756Z_15e90d39d376` (treant 1.5x),
  under `tools/screenshots/gif/runs/`.
- Rubric (motion, art): 1 ~, 2 ~, 3 yes, 4 ~, 5 yes, 6 yes. Art: not scored (stand-ins).
- Still weak: no soil crack; sap and leaves are specks; cage taller than the body on a rat; painted particles beside flat vines.
