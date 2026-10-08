# Velvet Shackles: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3 "Velvet Shackles".

## Status

Step 3 done by the orchestrator (`9a67d602`, 2026-10-07); step 2 round 1 in `83882f88`. Waits on the owner (Q1 wrap shape, Q2 lash) before the step 4 art goes out. Nothing here is owner-reviewed.

Captures: `tools/gif.ps1 spell velvet_shackles` (`-Vs rat`, `-Vs treant`, `-Vs treant -Speed 1.5`); previews `velvet_shackles_r1_*_preview.gif` committed, full GIFs untracked, `r1_*_timing.json`. The 2026-10-04 passes (painted nine-frame stand-in, painted four-frame cinch) are superseded; their GIFs and `spell_feel_1x/`, `spell_feel_1.5x/` stay untracked. Old defect: a red velvet blob covered the whole rat and read as a flower or sash.

## Identity

- **Verb:** bind (a ribbon is lashed from Shawn's hand, wraps the legs, and a cuff cinches shut).
- **Signature beat:** the small cuff snaps tight at the feet on the cue, gold clasp showing, with the Rooted badge.
- **Must read as:** "a velvet ribbon whipped out of Shawn's hand and shackled its legs", then went slack and let go.
- **Must NOT read as:** a velvet blob or sash over the body; a flower; a cuff sized to the body that hides it; a glowing ring.
- **Motion references:**
  - `lash`: a ribbon cracked from the hand like a whip, flying along its own length;
  - `back`, `front`: ribbon bands pulled tight round the ankles, the loop shrinks and stops; after the cinch they go slack, loosen and drop;
  - `cuff`: a padded cuff snapped shut, arriving oversized and settling onto the legs;
  - `thread`, `glint`, `settle`: gold thread and velvet scraps, falling.
- **Exceptions:** the cuff arrives on the cue frame (pop-in; `punch` 0.3 settles it); the lash hands over to the wrap layers on a cross-fade at 0.22 s and lands at the body's middle while the wrap sits at the feet; the wrap is a straight band, not a coil (see Q1).
- **Mechanics, unchanged:** Rooted for 2 turns, no damage, cannot be dodged. No stage kick (a root is not a blow).

## Beat table

Release clock; cue **0.40 s** = f24 at 1x, f16 at 1.5x (`timing.json`; cast sound re-cut, `MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`); clear at f54 at 1x, f36 at 1.5x. Single target, so `back` takes the one ground renderer (band holds 2); no E5 question. E6 (orbit) is not built: the wrap is two layers tilted +10 and -10 degrees with baked scale. Wraps `size 240 x fit`, cuff `size 130 x fit`, both on the target's ground line. E9 (a cuff that stays while the status lasts) is future work, not built; the cuff fades.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.15 | gloom 0.20; gold thread tufts at Shawn's hand | `thread`, gloom | particles | emitter at the hand, 5 tufts, 0.15-0.25 s |
| 0.02-0.22 | ribbon lashed from the hand to the target | `lash` (travel, `orient: path`, `artDegrees` 0, strip points +x, `place: caster-centre`; the Dark Chains pattern) | lash strip | 170; 0.20 s flight, fades 0.10 s on arrival, no `fadeIn` |
| 0.20-0.40 | two bands pull tight round the legs: back band behind (ground band), front over | `back`, `front` | ribbon f0-f4 | 240 at the feet, tilted +10 and -10; baked scale 1.30 to 1.0, 20 fps, fade-in 0.12 |
| 0.40 | **Cue.** Cuff snaps shut at the feet, gold clasp. Sound, Rooted badge | `cuff` (f2) | cuff | 130; opens 30% oversized (`punch` 0.3), glow 0.2; biggest energy rise |
| 0.40 | gold glint off the clasp | `glint` | particles | emitter, 6 specks, glow 0.6, 0.15-0.30 s |
| 0.40-0.45 | bands hold tight | `back`, `front` f5 | ribbon | 1 frame |
| 0.45-0.65 | bands go slack (scale 1.12, 1.28) and fade | `back`, `front` f6-f7 | ribbon | fade 0.12 |
| 0.45-0.90 | velvet scraps and thread settle | `settle` | particles | emitter, 8 pieces, 0.3-0.45 s |
| 0.40-0.82 | cuff held to 0.62 s, then fades | `cuff` | cuff | fade 0.20; gone 0.42 s after the cue (limit 0.5) |

## Open items

1. Lash f4-f16: a flat straight red bar crossing the stage; reads as a stripe or beam, no flutter or curl. Lands across the rat's snout at f12. Art fixes it (a rippling ribbon, prompt 3).
2. No wrap: the bands lie flat at the feet f16-f20 and never go round the body. A coiling ribbon needs E6 orbit or drawn wrap frames. **Owner call** Q1.
3. Cuff f24-f40 is a red ring lying on the ground; on the treant a cushion or bowl at the roots, on the rat about half its width. Art fixes it (a cuff drawn round a leg).
4. Lash lands on the chest/snout while the wrap sits at the feet, breaking the delivery line. **Owner call** Q2.
5. `spell_feel` 1x: treant WARN no accent (peak 5.8 vs typical 4.3); rat WARN no accent (5.2 vs 3.8) and **WARN contact desync** (biggest rise f8, the lash's first frames in view; cue f24). Probable fix: a short `fadeIn` on `lash`; not made (needs a re-capture). Travel 0.30-0.38 s measured vs 0.20 s authored; gather 0.10 s vs 0.02 s.
6. Pop-in: the lash's first frame has no fade-in; the cuff on f24 is by design. Cuff holds 0.22 s with glint and scraps moving.
7. Glint specks are tiny. Flash guard: cuff glow 0.2, glint 0.6 on 6 small specks; no large area brightens.
8. Treant cuff is as wide as the trunk base (`fit: target`). **Owner call** Q4.
9. Treant 1.5x frames not stepped through (same layers on a faster clock).
10. Cleanup once the owner accepts the motion: delete superseded `velvet_shackles_cinch` and `velvet_shackles` (nine-frame) recipes and folders.

## Decisions

- Asked once, unanswered: Q1 the wrap is a straight band, two crossing at the feet, not a coil: accept for blocking, or draw a ribbon that bends into a loop (prompt 1)? Q2 the lash lands on the chest, the wrap starts at the feet: keep, or drop the lash and start the wrap at the target? Q3 cue moved 0.30 to 0.40 s so the wrap can pull tight (cast sound re-cut): keep? Q4 the cuff is `fit: target`, as wide as the trunk base on the treant: fine, or smaller on large bodies?
- 2026-10-07 (orchestrator): cue 0.40 s, no kick, travel pattern from Dark Chains.

## Art

All three strips are `#00FF00`, keyed `green-soft` (the gold must keep its hue). Subjects are red and gold, so the step 4 backdrop is solid `#00FF00` (no green in the subject). Frame registration (E7): the ribbon set shares one 2500x440 canvas, ribbon centred, scaled about the centre; the cuff set shares one 440x300 canvas, explicit rects centred on each drawing's content box and bottomed 10 px under it.

| Piece | File | Recipe | Status |
|---|---|---|---|
| Ribbon wrap | `Art/Sheets/Spells/Velvet Shackles/shackle_ribbon.png` 1983x793, one wavy ribbon | `recipes/velvet_shackles_ribbon.json`; output f0-f4 tighten 1.30 to 1.0, f5 hold, f6-f7 slack | stand-in, flat cel (owner's 2026-10-04 delivery) |
| Ribbon lash | same strip, tight crop | `recipes/velvet_shackles_lash.json`; f0 | stand-in |
| Cuff | `shackle_cuff.png` 2172x724, three drawings (open, nearly closed, closed with clasp) | `recipes/velvet_shackles_cuff.json`; f0 and f1 cut not played, f2 played | stand-in |
| Thread, thorn, scrap, curl | `velvet_shackles_atlas_v1.png` cells 13-16 | `recipes/velvet_shackles_particles.json` | stand-in, painted |
| Painted cinch | `velvet_shackles_cinch.png` | `recipes/velvet_shackles_cinch.json` | superseded, unreferenced |
| Painted nine-frame atlas | `velvet_shackles_atlas_v1.png` cells 1-12 | `recipes/velvet_shackles.json` | superseded, unreferenced |

Step-4 prompts, verbatim; not yet commissioned. Style block and attachments as in `docs/prompts/TEMPLATE_spell_art.md` section 1.

1. **Ribbon wrap set** (delivery and payoff: the ribbon coiling round the legs, back and front halves)
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

2. **Cuff set** (payoff)
```
Piece: Velvet Shackles: the cuff
Beat it serves: payoff 0.40 s (cuff)
Filename: velvet_shackles_cuff_flat.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Velvet Shackles/velvet_shackles_cuff_flat.png
Canvas: 3072x768   Grid: 4x1 of 768x768
Pivot: the middle of the cuff, 50% across and 60% down, in every cell, the same size in every cell (only the opening changes)
Size in play: about 40% of a rat's body width (a 130 px box)
Drawing: the padded crimson velvet cuff seen from the front and slightly above, one drawing per cell: 1 open with the gold plates apart, 2 nearly closed,
  3 closed with the clasp, 4 closed and slightly squeezed with one crease. Two flat reds, a darker inside, gold clasp in two tones, bold dark outline.
  The delivered strip (`shackle_cuff.png`) is the target style; step 4 needs its cells on one grid with a fixed pivot.
Verb: bind.
Must read as: a shackle snapping shut.
Must NOT read as: a collar, a bracelet, a scrunchie, a bag.
Not in the picture: target, legs, floor, shadow, glow, text.
```

3. **Lash** (delivery)
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
Verb: lash.
Must read as: a ribbon thrown like a whip.
Must NOT read as: a snake, a banner, a river, a sash.
Not in the picture: hand, target, glow, text.
```

4. **Gold glint and thread** (payoff and resolution particles)
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

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | nine painted velvet frames, procedural ring | worker | `42d43291` |
| 2026-10-04 | pass 2 | painted four-frame cinch as one layer round the body; ring and snap removed. Cuff hid a rat and a treant trunk, read as a sash | worker | `386f640d` |
| 2026-10-07 | 1 | new design on flat-cel strips: layers `lash`, `back`, `front`, `cuff`, `thread`, `glint`, `settle`; cue 0.30 to 0.40; wrap 330 to 240, dy 45 to 18, tilt 16 to 10 (bands sat on the torso); cuff 170 to 110 to 130; lash was the loudest frame (diff 4.9 vs cue 1.5) so 250 to 170 with a fade, wraps fade in 0.12, cuff `punch` 0.3 (4.3 vs 2.6) | worker frames, frame-difference script, `spell_feel` | `83882f88` |
| 2026-10-07 | step 3 | lash a flat bar, no wrap, cuff a ring on the ground (Open items 1-3) | orchestrator | `9a67d602` |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 |
| Internal retries | 2 (wrap/cuff size, height, tilt; cue energy vs lash) |
| Attempts per art piece | 0 (stand-ins) |
| Active / waiting time | about 1.5 h (15 min builds, captures, tests, `spell_feel`) |
| Owner-found defects the checks should have caught | 0 |
| Quality vs reference pack | not yet |

## Snapshot

- **Commit:** `83882f88` (round 1); orchestrator check `9a67d602`.
- **Capture run ids** (`tools/screenshots/gif/runs/`): rat 1x `20261007T200645Z_26f5b5228302`, treant 1x `20261007T200521Z_9f7c4871068e`, treant 1.5x `20261007T200729Z_0e3ce2a6143f`.
- **Gate:** `VelvetShacklesPresentationTests` and `VelvetShacklesTests` green (16).
- **Binding checks:** no transform jump pass with exceptions (`back`/`front` only turn by a fixed `rotate` and baked scale; cuff punch eased; lash-to-wrap cross-fade 0.1 s); cue on one frame pass (`hitFrame` 24 / 16; no `at: hit` sprite); nothing visible leaves the stage pass; frame registration (E7) pass.
- **Rubric (worker):** 1 beats in order ~ (lash lands on the chest, wrap at the feet); 2 weight and easing ~ (straight band that scales, not a closing loop); 3 one focal point ✔; 4 body readable ✔ (legs and feet covered, head and chest clear); 5 ending decays ✔; 6 distinct ✔ (red velvet, gold; flat strips match the rat sheet; particles painted). Art not scored.
- **Still weak:** lash pop-in and desync WARN on the rat; wrap is a straight band; lash and wrap do not meet; painted particles beside flat strips; glint tiny.
