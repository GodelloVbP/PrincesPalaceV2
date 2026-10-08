# Thorn Tithe: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3 "Thorn Tithe".

## Status

Step 6 closed in `a68833d8`; sink-to-fade tuning in `49529705` (2026-10-07). Waits on the owner's review of the step 6 and tuned GIFs (`thorn_tithe_step6_{rat,treant}_1x.gif`, `thorn_tithe_tune_{rat,treant}_1x.gif`, previews committed). Owner passed step 3 on round 1 motion.

Captures: `tools/gif.ps1 spell thorn_tithe` (`-Vs rat`, `-Vs treant`, `-Vs treant -Speed 1.5`); full GIFs untracked. The 2026-10-04 passes (12-frame stand-in, painted four-frame growth) are superseded; their GIFs and `spell_feel_1x/`, `spell_feel_1.5x/` stay untracked beside this file.

## Identity

- **Verb:** cage (thorns grow up out of the ground and close round it).
- **Signature beat:** the cage snaps shut round the body on the cue, with the sap burst, the Thorned badge and a small stage kick.
- **Must read as:** "thorn vines grew out of the soil and caged it", then fade away shedding a few leaves.
- **Must NOT read as:** a vine laid over the body or sliding down onto it; a cage bigger than the body that hides it; a tree or a plant growing; a generic sparkle.
- **Motion references:**
  - `back`, `front`: bramble canes forcing up out of soil, fast and in steps, base fixed; they arch over and cross, then hold as the closed dome;
  - `crack`, `dust`: soil cracking and clods thrown up as the canes break the surface, falling under gravity;
  - `sap`: beads flung from the crossing, falling; `leaves`: dead leaves shaken loose, tumbling slowly (drag, spin).
- **Exceptions:** growth is drawn in two-frame steps (a sway pair per stage), as delivered; the cage closes in one frame on the cue (the same canes twist shut; f6 of each set); layers cross-fade between drawings (engine).
- **Mechanics, unchanged:** Thorned for 3 turns, 5 Nature at the start of its turn and after each physical move; cue sound, badge and number on one frame.

## Beat table

Release clock; cue **0.40 s** (cast sound re-cut with `make_spell_cast_cues.py`, pinned by `MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`). `timing.json` cue f24, freed f54 (`clearedSeconds` 0.900; cue + 0.5); earlier captures read f22 at 1x, f15 at 1.5x. Single target, so `back` takes the one ground renderer (ground band holds 2); `front` is in the effects band; no E5 question. Stage kick `{strength 0.025, rise 0.03, fall 0.18}` as Censer and Ashen. No drift, rotate or scale.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.20 | soil cracks | `crack` | crack sheet f0-f3 | 300; effects band, offset 0, 20 fps, `impactY` 0.133 (the cell's 70% pivot) |
| 0.02-0.30 | soil thrown up | `dust` | particles flat: bark, clod | emitter, 8 clods, 0.15-0.30 s, sizes 0.22-0.34 |
| 0.10-0.40 | left canes rise behind the body | `back` f0-f5 | back canes | 341 (dome 442 of 520 px canvas, 290 wide on screen); 20 fps, three growth steps of two frames, base on the ground line |
| 0.10-0.40 | right canes rise in front | `front` f0-f5 | front canes | 341, not mirrored (symmetric set), same clock |
| 0.40 | **Cue.** Cage closes: dome behind, X across. Sound, number, Thorned badge, kick on this frame | `back` f6, `front` f6 | dome / X | one frame, no glow; biggest area change |
| 0.40 | sap beads burst from the crossing | `sap` | particles flat: sap bead | emitter, 10 beads, 0.2-0.4 s, glow 0.5, sizes 0.3-0.45, speed 140-340 |
| 0.40-0.50 | leaves shaken loose | `leaves` | particles flat: leaf | emitter, 5 leaves, 0.3-0.4 s, sizes 0.4-0.55, speed 60-140 |
| 0.40-0.60 | dome held, then fades in place | `back`, `front` f6-f13 (all the dome) | dome / X | `seconds` 0.5, `fade` 0.28, clear at 0.90 |

## Open items

1. Treant f12-f22: canes taller than the treant leave the top of the frame; dome at f24 covers trunk and arms up to the face (fit-to-target width makes tall growth frames huge on a big body). Not changed.
2. Dust burst f6-f8 is large and pale against the dark floor, the loudest thing before the cue. Not changed.
3. The cage leaves by fading from the dome; it does not sink into the soil. Sinking needs drawn frames.
4. Rat: cage settles down by a fifth on the cue (back canes f4-f5 reach 528 px, dome 417); dome 290 wide, about 270 tall.
5. Sap and leaves overlap the X and read as small clusters, partly hidden (f24-f34).
6. Treant 1.5x not re-captured after step 6; dust and sap are first to go missing at that speed (round 1).
7. `spell_feel` not run since round 1 (round 1: no warnings, rat peak 3.5 and treant 5.8 at the cue, residue 0.37 s). Rerun when a runtime capture is allowed.
8. Flash guard: no glow on sprites; sap glow 0.5 on small beads; the typed Nature hit flash is the shared one.
9. Cleanup after the owner accepts the motion: delete the superseded recipes and folders `thorn_tithe_growth`, `thorn_tithe` (12-frame atlas) and the stand-in strips `thorn_back_vines.png`, `thorn_front_vines.png`. Do NOT delete `thorn_tithe_particles` or `thorn_tithe_atlas_v1.png`: Spore Cloud and Splinters use them.

## Decisions

- 2026-10-07 (owner): step 3 passed, round 1 motion approved; cue 0.40 s kept.
- 2026-10-07 (owner): a bunch of canes per side instead of one; the canes grow INTO the cage (no swap on the cue); a squatter cage, about as tall as it is wide. Folded into the step 4 prompts.
- 2026-10-07 (orchestrator): step 5, all four pieces accepted; crack needed `cap_blue` (below).

## Art

Subjects are blue-green: backdrop solid `#FF00FF`, recipes keyed `"keyed": "magenta"`. Recipes: hue key, `alpha_floor` 40. Both vine sets share one rect (x 60-580, y 34-587 of each cell: the union of both sets, feet at 25% and 75%, base row 575), so one canvas, one pivot, no per-frame crop (E7). Registration: dome feet at +-143 px of centre.

`cap_blue` (new optional recipe key in `tools/slice_spell_sheet.py`, `docs/ART_PIPELINE.md`) pulls blue down to 0.83 of green on pixels with red well above green and blue at or above green. Crack cells 3-4 held opaque salmon pixels (250,185,185): tan with magenta mixed in, which the hue key keeps and `alpha_floor` cannot touch. After it no crack pixel is pink.

| Piece | File | Recipe | Status |
|---|---|---|---|
| Back canes | `thorn_tithe_back_vines.png` 7x1 of 640 | `recipes/thorn_tithe_back.json`: rect 520x553 per cell; sequence f0-f5, dome f6 held for f6-f13 | accepted |
| Front canes | `thorn_tithe_front_vines.png` 7x1 of 640 | `recipes/thorn_tithe_front.json`: same rect and sequence; f6 is the X | accepted |
| Crack | `thorn_tithe_crack.png` 4x1 of 512 | `recipes/thorn_tithe_crack.json`: rect 440x240, `cap_blue` 0.83 | accepted after one revision (pink specks) |
| Sap, leaf, bark, clod | `thorn_tithe_particles_flat.png` 4x1 of 256 | `recipes/thorn_tithe_particles_flat.json`: `content_crop` 213x230, `cap_blue` 0.83 | accepted; yellow sap bead (lime was asked), sizes revised once; output f0 sap, f1 leaf, f2 bark, f3 clod |
| Stand-in flat strips | `thorn_back_vines.png`, `thorn_front_vines.png` (2172x724, 7 drawings, `#00FF00`) | superseded recipes | stand-in, unreferenced |
| Painted growth, 12-frame atlas | `thorn_tithe_growth.png`; `thorn_tithe_atlas_v1.png`, `_v2.png` cells 1-12 | `recipes/thorn_tithe_growth.json`, `recipes/thorn_tithe.json` | superseded, unreferenced |
| Painted particles | `thorn_tithe_atlas_v1.png` cells 13-16 | `recipes/thorn_tithe_particles.json` | stand-in; still used by Spore Cloud and Splinters |

Step-4 prompts, verbatim. Style block and attachments as in `docs/prompts/TEMPLATE_spell_art.md` section 1.

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

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | 12-frame painted atlas; sap-burst star accent | worker | n/a |
| 2026-10-04 | pass 2 | painted four-frame growth in two layers; cage as f3 of one `grow` layer (an `at: hit` sprite is a tail, `TailScale`); `back` mirrored (`facing: reverse`); badge `impactY` 0.36. Cage bigger than the treant and slid down from half size | worker | n/a |
| 2026-10-07 | 1 | new design on flat-cel strips: recipes `thorn_tithe_back`/`_front` (E7 registration, shared canvas), layers `back` (ground), `front`, `dust`, `sap`, `leaves`; cue 0.30 to 0.40; `vfx.kick`; size 330 to 290; sink by drift replaced by baked backward frames (drift pushed canes through the floor line) | worker frames, `spell_feel` | n/a |
| 2026-10-07 | 6 | step 4 art wired; `crack` layer; `cap_blue` for pink specks; particles about 2.5x; back/front 290 to 341, front unmirrored. Cue now reads f24 (was f22; later capture wins) | worker frames | `a68833d8` |
| 2026-10-07 | tuning | orchestrator: cage re-opened to tall canes at f38 (pop on both bodies). Sink frames replaced by the held dome, `seconds` 0.7 to 0.5, `fade` 0.1 to 0.28; rat and treant 0.60-0.90 s thin out in shape | orchestrator step 6 check | `49529705` |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 |
| Internal retries | round 1: 1; step 6: 2 (pink specks, particle size) |
| Attempts per art piece | 1 each (crack one revision) |
| Active / waiting time | round 1 about 1 h (12 min builds, captures, tests); step 6 about 45 min, two builds, two captures |
| Owner-found defects the checks should have caught | 0 |
| Quality vs reference pack | not yet |

## Snapshot

- **Commit:** `a68833d8` (step 6), tuning `49529705`.
- **Capture run ids:** step 6 rat `20261007T211000Z_7eea78bebcb5`, treant `20261007T211101Z_e4aca9c0a6d2`. Round 1: rat 1x `20261007T184717Z_65af58fb43b8`, treant 1x `20261007T184609Z_e601322bdf6c`, treant 1.5x `20261007T184756Z_15e90d39d376`, under `tools/screenshots/gif/runs/`.
- **Gate:** slices `ThornTithePresentationTests`, `MeleeSoundOverrideTests`, `SpellPoolCapacityTests`, `SpellVfxRecipeDriftTests`, `SpellLayerRulesTests`, `SpellBaselineTimingTests` green (131). `ThornTithePresentationTests` pins size 341, front not mirrored, the crack at offset 0.
- **Binding checks:** no transform jump pass (only the drawing changes); cue on one frame pass (`back`/`front` are `at: release`, so no `TailScale`); nothing visible leaves the stage pass (rat f20 tip reaches the crop top but stays on stage; treant see Open item 1); frame registration (E7) pass.
- **Rubric (worker, step 6):** 1 beats in order ✔; 2 weight and easing ~ (steps with a sway pair, no ease inside a step); 3 one focal point ✔; 4 body readable ~ (rat face reads, body crossed by canes); 5 ending decays ✔ (but a fade, not a sink); 6 distinct ✔ (teal canes on dark fur and brown bark).
- **Still weak:** treant swamped by tall canes; dust burst loud; ending is a fade.
