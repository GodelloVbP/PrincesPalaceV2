# Velvet Shackles: state record

Trial 5, spell 5 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), steps 1 and 2 "identity sheet, block with existing assets", round 1.
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3 "Velvet Shackles".
**Status: step 2 round 1 done by the worker (2026-10-07); step 3 (orchestrator's watch and rubric) is next. Nothing here is owner-reviewed.**
The 2026-10-04 passes (painted nine-frame stand-in, then the painted four-frame cinch sheet) are superseded; their GIFs
(`velvet_shackles_rat_1x.gif` etc.) and `spell_feel_1x/`, `spell_feel_1.5x/` stay untracked beside this file. The old defect (plan section 0): a red
velvet blob covered the whole rat and read as a flower or a sash, not as shackles.

Captures 2026-10-07 with `tools/gif.ps1 spell velvet_shackles` (`-Vs rat`, `-Vs treant`, `-Vs treant -Speed 1.5`). Phone previews are committed
(`velvet_shackles_r1_*_preview.gif`); the full GIFs (`velvet_shackles_r1_*.gif`, 5-7 MB) are untracked; `r1_*_timing.json` is each capture's
`timing.json`. `tools/spell_feel.ps1` was run once, 1x, treant and rat (`spell_feel_r1_1x/`, untracked).

## Identity sheet

Locked by the orchestrator's plan; the owner confirms or edits it with the first GIF.

- **Verb:** bind (a ribbon is lashed from Shawn's hand, wraps the legs and a cuff cinches shut).
- **Signature beat:** the small cuff snaps tight at the feet on the cue, gold clasp showing, with the Rooted badge.
- **Must read as:** "a velvet ribbon whipped out of Shawn's hand and shackled its legs", then went slack and let go.
- **Must NOT read as:** a velvet blob or sash over the body (the old pass); a flower; a cuff sized to the body that hides it; a glowing ring.
- **Motion reference per moving element** (a real thing, not invented parameters):
  - `lash`: a ribbon cracked from the hand like a whip, flying along its own length;
  - `back`, `front`: ribbon bands pulled tight round the ankles, so the loop shrinks and stops; after the cinch they go slack, loosen and drop;
  - `cuff`: a padded cuff snapped shut: it arrives oversized and settles onto the legs;
  - `thread`, `glint`, `settle`: gold thread and velvet scraps, falling.
- **Intentional exceptions** (so the binding checks do not flag them):
  - the cuff arrives on the cue frame (pop-in by design; `punch` 0.3 settles it);
  - the lash hands over to the two wrap layers on a cross-fade at 0.22 s, and lands at the body's middle while the wrap sits at the feet;
  - the wrap is a straight band, not a coil (the strip does not bend; see art step 4).
- **Mechanics, unchanged:** Rooted for 2 turns, no damage, cannot be dodged.

## Beat table

Authored seconds on the release clock; hit cue **0.40 s** (was 0.30; the cast sound is re-cut to 0.40 s with `make_spell_cast_cues.py`, pinned by
`MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`). Cue = f24 at 1x, f16 at 1.5x (`timing.json`). Single target (`SingleEnemy`), so `back` takes
the one ground renderer (the ground band holds 2); no E5 question. E6 (orbit) is not built: the wrap is two layers, tilted +10 and -10 degrees,
with baked scale. Sizes: wraps `size 240 x fit`, cuff `size 130 x fit`, both on the target's ground line.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.00-0.15 | Gloom 0.20 rises; gold thread tufts at Shawn's hand | `thread`, gloom | emitter at the hand | 5 tufts, 0.15-0.25 s |
| 0.02-0.22 | Ribbon lashed from Shawn's hand to the target | `lash` (travel, `orient: path`) | yes, 170 box, crosses the stage | 0.20 s flight; fades 0.10 s on arrival |
| 0.20-0.40 | Two bands pull tight round the legs: back band behind (ground band), front band over | `back`, `front` | yes, 240 box at the feet, tilted +10 and -10 | ribbon f0-f4 baked scale 1.30 to 1.0, 20 fps; fade-in 0.12 s |
| 0.40 | **Cue.** The cuff snaps shut at the feet, gold clasp. Sound, Rooted badge on this frame | `cuff` (f2) | yes, at the feet | opens 30% oversized (`punch` 0.3) and settles; biggest energy rise |
| 0.40 | Gold glint off the clasp | `glint` | emitter at the feet | 6 specks, glow 0.6, 0.15-0.30 s |
| 0.40-0.45 | Bands hold tight | `back`, `front` f5 | yes | 1 frame |
| 0.45-0.65 | Bands go slack (scale 1.12, 1.28) and fade | `back`, `front` f6-f7 | yes | fade 0.12 s |
| 0.45-0.90 | Velvet scraps and thread settle | `settle` | emitter | 8 pieces, 0.3-0.45 s |
| 0.40-0.82 | Cuff held, then fades | `cuff` | yes | held to 0.62 s, fade 0.20 s |

Everything is gone at 0.82 s authored: 0.42 s after the cue (the residue limit is 0.5). The plan keeps the cuff as the Root status visual (E9); E9 is not
built, so the cuff fades in the resolution. **E9 (a cuff that stays while the status lasts) is future work and is not built here.**

Travel: reused the Dark Chains pattern (`travelSeconds` 0.20, `orient: path`, `artDegrees` 0, strip painted pointing +x, `place: caster-centre`), no new
mechanism. The ribbon crosses the whole stage and reads as a thrown ribbon on both bodies; it lands at the body's middle, not at the feet.

Stage kick: none. A root is not a blow; the cue rests on the cuff's punch, the glint and the sound.

## Playback, as seen (my own frame contact sheets, 60 fps; the orchestrator has not yet watched)

Rat 1x (cue f24):
- **f0-f7.** The rat idles. Gold tufts at Shawn's hand, then a red ribbon appears at his hand and flies right, level and wavy (f2 at his head, f8-12
  mid-stage).
- **f13-f18.** The ribbon reaches the rat's snout and chest and thins out (translucent, fading). From about f15 two thin red bands fade in at the rat's feet,
  crossing in a shallow X over the front paws and tail base.
- **f21-23.** Bands full opacity, crossing the legs; the rat is fully readable above them.
- **f24 cue.** A small red cuff with a gold clasp appears at the feet, about 40% of the rat's width, 30% oversized on its first frame; the two bands cross
  over it. The Rooted badge `2` is under the body on f24. The gold specks (glint) are tiny.
- **f25-27.** The cuff settles tight. **f32-f40.** The bands widen and slacken across the cuff; **f37-f45** the cuff turns translucent; **f54** clear.
- Body: the cuff and bands cover the feet and a strip of the belly line; head, chest and arms are never covered. On the dark rat the saturated red with
  gold stitching reads clearly against the grey fur and the dark floor; no violet-on-dark loss like Court's.

Treant 1x (cue f24; frames viewed at f12-f54): the same beats. The ribbon crosses the face and trunk in flight (translucent, f12-f16), then the bands and cuff
sit at the root base; the cuff is about the width of the trunk's base (`fit: target` scales it with the body, so it is bigger than a legs-only cuff).
Red on brown bark and the green canopy is the most saturated thing on stage and reads at once. The face is never covered once the wrap starts. Clear by f54.

Treant 1.5x: cue f16, clear f36, from `timing.json`; I did not step through its frames (the same layers on a faster clock).

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass, with intentional exceptions | `back`/`front` only turn by a fixed `rotate` and scale by baked frames; the `cuff` punch is eased; the lash-to-wrap hand-off is a cross-fade over 0.1 s |
| Cue on one frame | pass | `timing.json` hitFrame 24 at 1x, 16 at 1.5x; cuff, sound and badge on one frame; no layer is an `at: hit` sprite (no tail scale) |
| Nothing marked visible leaves the stage | pass | the lash enters from the hand and ends on the target; wraps and cuff sit on the ground line |
| Frame registration (E7) | pass | the ribbon set shares one 2500x440 canvas, ribbon centred, scaled about the centre; the cuff set shares one 440x300 canvas, cut with explicit rects centred on each drawing's own content box and bottomed 10 px under it |

`spell_feel` (1x, runtime capture, `spell_feel_r1_1x/`, run once at the end):
- Treant: contact f24 (visual f24); peak 5.8 at 0.40 s (the cue); residue 0.37 s after contact, clean at 0.78 s; WARN no accent (peak 5.8 against a typical
  4.3, the rubric wants 1.5x); NOTE travel 0.38 s measured against 0.20 s authored.
- Rat: contact f24 (visual f8); peak 5.2 at 0.40 s (the cue); residue 0.37 s, clean at 0.78 s; WARN no accent (5.2 against 3.8); **WARN contact desync**
  (the biggest rise after release is f8, the lash's first frames in view; the visual contact reads f8 for that reason); NOTE gather 0.10 s measured against 0.02 s
  authored, travel 0.30 s against 0.20 s.
- Before the final run I checked the cue against my own GIF frames with a frame-difference script (treant, stage crop): lash peak 4.9 against cue 1.5 at the first
  tune, then lash 2.6 against cue 4.3. `spell_feel` still reads the lash's entry on the rat as a rise. Probable fix: a short `fadeIn` on `lash` so it does not
  appear at full alpha; not made, because it needs a re-capture and the one `spell_feel` run was spent.

Advisory:

- **Pop-in:** the cuff on f24 (by design) and the lash on its first frame (no fade-in on the lash).
- **Frozen holds:** the cuff holds 0.22 s (to 0.62 s) while glint and scraps move; the bands hold one frame.
- **Flash guard:** cuff glow 0.2, glint glow 0.6 on 6 small specks; no large area brightens.

Gates: dotnet slice `VelvetShacklesPresentationTests` and `VelvetShacklesTests` green (16); the commit gate result is in the report.

## Rubric (worker's score; the orchestrator's check is still due)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ~. Lash, wrap, cinch, slack are in order; the lash lands on the chest while the wraps sit at the feet, which breaks the line of the delivery.
2. Weight and easing feel physical: ~. The cuff's punch and the baked tightening give a snap; the wrap is a straight band that scales, not a loop that closes.
3. One focal point at a time: yes at the cue (the cuff); the lash is alone before it.
4. The body stays readable at the hit: yes. Legs and feet are covered; head and chest are clear.
5. The ending decays, not cuts: yes. Bands loosen, then cuff and bands fade; clear 0.42 s after the cue.
6. Distinct from and consistent with the approved spells: yes on identity (red velvet, gold); flat cel strips match the rat sheet's style; the particles (painted) do not.

Art: the ribbon and cuff strips are flat cel with a dark outline, two or three flat tones and gold stitching, in the rat's style; the particles are the old painted
atlas cells. Not scored.

## Art record (one row per piece)

| Piece | Accepted prompt (path or pasted) | Source file | Recipe | Output (folder) | Attempts |
|---|---|---|---|---|---|
| Ribbon wrap (stand-in, flat cel) | not recorded (owner's 2026-10-04 evening delivery) | `Art/Sheets/Spells/Velvet Shackles/shackle_ribbon.png` (1983x793, one wavy ribbon, #00FF00) | `recipes/velvet_shackles_ribbon.json` (new) | `Resources/Spells/velvet_shackles_ribbon` (f0-f4 tighten 1.30 to 1.0, f5 hold, f6-f7 slack) | 0 |
| Ribbon lash (stand-in, flat cel) | not recorded | same strip, tight crop | `recipes/velvet_shackles_lash.json` (new) | `Resources/Spells/velvet_shackles_lash` (f0) | 0 |
| Cuff (stand-in, flat cel) | not recorded | `shackle_cuff.png` (2172x724, three drawings: open, nearly closed, closed with clasp, #00FF00) | `recipes/velvet_shackles_cuff.json` (new) | `Resources/Spells/velvet_shackles_cuff` (f0 and f1 cut but not played; f2 played) | 0 |
| Thread, thorn, scrap, curl (stand-in, painted) | n/a | `velvet_shackles_atlas_v1.png` cells 13-16 | `recipes/velvet_shackles_particles.json` (unchanged) | `Resources/Spells/velvet_shackles_particles` | 0 |
| Painted cinch (superseded, not referenced) | n/a | `velvet_shackles_cinch.png` | `recipes/velvet_shackles_cinch.json` | `Resources/Spells/velvet_shackles_cinch` | pass 2 |
| Painted atlas, nine frames (superseded, not referenced) | n/a | `velvet_shackles_atlas_v1.png` cells 1-12 | `recipes/velvet_shackles.json` | `Resources/Spells/velvet_shackles` | pass 1 |

Keys: all three strips are #00FF00, keyed `green-soft` (the gold must keep its hue). The two superseded folders and recipes stay in the tree unreferenced;
delete them when the owner accepts the new motion.

### Art needed for step 4

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`; the style block and its attachments as in its section 1. The subjects are red and gold, so the backdrop is
solid #00FF00 as the delivered strips (no green in the subject). At play size the pieces sit on a dark grey rat and a brown-and-green treant: saturated
red with a dark outline and gold stitching reads on both.

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

## Open owner questions

Each asked once. Status: open, answered (with the answer), or dropped.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | The ribbon is a straight band: two of them crossing at the feet, not a coil. Accept that for blocking, or should the art draw a ribbon that bends into a loop (prompt 1)? | 1 | open |
| 2 | The lash lands on the chest and the wrap starts at the feet. Keep, or drop the lash and start the wrap at the target? | 1 | open |
| 3 | The cue moved 0.30 to 0.40 s so the wrap has room to pull tight (the cast sound is re-cut). Keep? | 1 | open |
| 4 | The cuff is sized by `fit: target`, so on the treant it is as wide as the trunk base. Fine, or smaller on large bodies? | 1 | open |

## Round log

| Round | What changed | Who caught it (owner / orchestrator / binding check / worker) | Rubric result |
|---|---|---|---|
| Pass 1 (2026-10-04) | Nine painted velvet frames, procedural ring | worker | not scored |
| Pass 2 (2026-10-04) | The owner's painted four-frame cinch sheet as one layer round the body; ring and snap removed; ribbons stay as wind-up. Found: the cuff hid a rat and a treant's trunk, read as a sash | worker | not scored |
| Round 1 (2026-10-07) | New design on the owner's flat-cel strips: layers `lash` (travel), `back` (ground) and `front` wrap bands, `cuff`, `thread`, `glint`, `settle`; recipes `velvet_shackles_ribbon`, `_lash`, `_cuff`; cue 0.30 to 0.40 and the cast sound re-cut; no kick. Tuned within the round: wrap size 330 to 240, dy 45 to 18 and tilt 16 to 10 (the bands sat on the rat's torso), cuff 170 to 110 to 130 (it covered the legs); the lash was the loudest frame (diff 4.9 against the cue's 1.5), so lash 250 to 170 with a fade, wraps fade in over 0.12 s, and the cuff arrives on the cue with `punch` 0.3 (diff 4.3 against 2.6) | worker (own frame sheets, a frame-difference script, `spell_feel`) | worker's score above; step 3 pending |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | 2 (size, height and tilt of the wrap and cuff; the cue energy against the lash) |
| Generation attempts per art piece | 0 (stand-ins) |
| Active work time / waiting time | about 1.5 h, of which the content builds, captures, tests and `spell_feel` were about 15 min |
| Owner handling time (generate, download, upload art; review) | none yet |
| Owner-found defects the binding checks should have caught (target 0) | 0 so far |
| Final quality against the reference pack (owner's verdict on the rubric) | not yet |

## Reviewed snapshot

- Commit: see the report (the commit that lands this file).
- Capture run ids (under `tools/screenshots/gif/runs/`): `20261007T200645Z_26f5b5228302` (rat 1x), `20261007T200521Z_9f7c4871068e` (treant 1x), `20261007T200729Z_0e3ce2a6143f` (treant 1.5x).
- Rubric (motion, art): 1 ~, 2 ~, 3 yes, 4 yes, 5 yes, 6 yes. Art: not scored (stand-ins).
- Still weak: lash pop-in (`spell_feel` desync WARN on the rat) and the no-accent WARN; the wrap is a straight band; the lash and the wrap do not meet; painted particles beside flat strips; the glint is tiny.
