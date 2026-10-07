# Viper's Bite: state record

Trial 2, spell 2 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), steps 1 and 2 "identity sheet, block with existing assets", round 1.
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3.
**Status: step 2 round 1 done by the worker (2026-10-07); step 3 (orchestrator's watch and rubric) is next. Nothing here is owner-reviewed.**
The 2026-10-04 passes (painted snake-head lunge, then the owner's lunge-v2) are superseded: the owner rejected the head. Their GIFs
(`vipers_bite_rat_1x.gif` etc.) stay untracked beside this file.

Captures 2026-10-07 with `tools/gif.ps1 spell vipers_bite` (`-Vs rat`, `-Vs treant`, `-Vs rat -PreStatus Poison`, `-Vs treant -Speed 1.5`).
Phone previews are committed (`vipers_bite_r1_*_preview.gif`); the full GIFs (`vipers_bite_r1_*.gif`, 13 MB each) are untracked;
`r1_*_timing.json` is each capture's `timing.json`. `tools/spell_feel.ps1` was run once, 1x, treant and rat (reports in
`spell_feel_r1_1x/`).

## Identity sheet

Locked by the orchestrator's plan; the owner confirms or edits it with the first GIF.

- **Verb:** bite.
- **Signature beat:** a pair of jaws closes on the body and the fangs snap shut on the cue, with the number and the Poison badge.
- **Must read as:** "a snake bit it": a pair of eyes in the dark, then jaws and fangs on the body.
- **Must NOT read as:** a snake head or any whole animal (owner: it looks bad); a thrown or flying object; a green blob over the body.
- **Motion reference per moving element** (a real thing, not invented parameters):
  - `eye_a`, `eye_b`: a snake's eyes opening in the dark one after the other, then holding; they do not move;
  - `jaws`: a strike's jaws closing, drawn in four steps (f0-f4 of the old sheet) from faint to full, no travel;
  - `bite`: the snap, an instant: one frame, a small punch, the brightest thing in the spell;
  - `drip`: venom running down fur, falling, thinning;
  - `mist`: the vapour off a bite, rising and thinning.
- **Intentional exceptions:** the jaws pop in at their first frame (they fade in over 0.06 s, the drawing itself does not move);
  the snap is one held frame (0.1 s), not an animation; the detonation burst is the owner's painted piece and is the one
  thing that is a different style from the rest.
- **Mechanics, unchanged:** 3 Poison damage, detonates the Poison already on the target (second number "-9" and
  `spore_cloud_impact` 0.18 s after the cue), then applies 3 Poison; cue sound, number and badge on one frame.

## Beat table

Authored seconds on the release clock; hit cue **0.40 s** (was 0.30; the cast sound is re-cut to 0.40 s with
`make_spell_cast_cues.py`, pinned by `MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`). Cue = f21 at 1x, f14 at 1.5x
(`timing.json`; authored 0.40 s is f24 minus the 3 first-drawn-wait frames). "Visible" is the stage-crop check: layer box =
size x fit factor, centred over the body. Sprite layers on a body: up to 5 of the effects band's 12.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.00-0.40 | Stage gloom 0.20 | stage `gloom` | n/a | in 0.15, holds to the cue, out 0.25 |
| 0.04-0.38 | Left eye glint opens above the head | `eye_a` | yes, 30 box, `above` 0.62, dx -50 | `fadeIn` 0.08, `breathe`, tint `#C8FF50`, glow 0.6, fade 0.08 |
| 0.10-0.38 | Right eye glint opens | `eye_b` | yes, 30 box, dx -22 | same, 0.06 s later |
| 0.14-0.41 | Jaws form on the body: halves draw together, fangs show | `jaws` | yes, 140 box over the torso | old sheet f0-f4 at 18 fps, opacity 0.6, no glow, `fadeIn` 0.06 |
| 0.40 | **Cue.** The snap: jaws shut in a starburst. Sound, "-3", Poison badge on this frame | `bite` | yes, 120 box | old sheet f5, punch 0.3, glow 1.25, held 0.1, fade 0.14 |
| 0.40 | Drops thrown down from the bite | `settle` | emitter | 12 particles, falling |
| 0.50-0.70 | Jaws shut, fangs in, fading | `closed` | yes, 110 box | f6-f7 at 10 fps, opacity 0.6, fade 0.15 |
| 0.55-1.00 | Green mist off the bite, thinning | `mist` | emitter | 12 a second for 0.5 s, rising, opacity 0.5 |
| 0.62-1.04 | Venom runs down the body | `drip` | yes, 150 box, `above` 0.18 | old sheet f8-f11 at 9 fps, `fadeIn` 0.06, fade 0.2 |
| 0.58 (Poison only) | Second payoff: painted venom burst, "-9", `spore_cloud_impact` | `pop`, `detonate` | yes, 200 box | held 0.1, fade 0.15, glow 0.9; 16-particle spray |

Visually clean around 0.7-0.8 s after the cue on 1x (see playback); the harness's `clearedSeconds` is 1.85 because the mist's last
particles live to about 1.45 s.

## Playback, as seen (my own frame contact sheets, 60 fps; the orchestrator has not yet watched)

Rat 1x (frames cut 450x400 round the body):
- **f0-f4 eyes.** Two lime glints fade in above the rat's head: the left at f2, the right at f6. They read as a pair of eyes in the dark.
- **f8-f20 jaws.** A faint green mist-jaw draws over the rat's torso at f8; f12-f20 shows the upper and lower fangs, dim, the body
  readable through it.
- **f21 cue.** The jaws snap in a bright starburst over the torso, the "-3" appears (f21-f22), the badge is under the body, the typed
  hit flash outlines the rat in pale green (f21-f23). The eyes are still visible and fade out over f21-f24.
- **f28-f31.** The closed jaws, a dim green mass over the torso (about a quarter of the body); fangs visible.
- **f35-f46.** The body is clear; green drops and a vertical drip hang over the torso (f40), then thin mist rises (f46-f60); gone by about f70.
- **Contract 2:** the snap covers about a quarter of the rat for f21-f31 (0.17 s); not over a third.

Treant 1x: the same beats. The eyes sit above the canopy (f4-f23), clear of the tree but not obviously "beside" it, and the treant's own orange
eyes compete lower down. The jaws land on the face (f8-f20), the snap covers the face and top of the trunk, about a fifth of the body (f21-f31).
The drip and mist stay on the face and upper trunk and read against brown bark; against the green canopy at f46 the mist is weaker.

Rat with Poison: f21 is the same snap. At f32 (0.18 s later) the painted burst (a dark teal plume with droplets) rises over the torso, with "-9" beside "-3".
The burst covers about 40-50% of the rat for f32-f40 (about 0.13 s): **slightly over contract 2**. Reduced from 420 to 200 box and from 0.22 s to 0.1 s
hold in this round; it is the owner's painted piece and the dark teal sits near-black on a dark rat, so it reads by its shape and the green droplets.

Treant 1.5x: cue f14, the same shape in two thirds of the time; eyes f2-f13 are short but both open by f5; the drip and mist overlap the snap's fade more.

## Orchestrator's step 3 check (2026-10-07, r1 full GIFs, rat, treant, rat with Poison at 1x; frames at 60 fps)

Shown to the owner. All four beats read in order with no head: glints, jaws forming, snap on the cue, drip and mist,
plus the detonation. Defects:
1. Treant f2-f20: the glints sit at the top edge of the frame above the canopy, nearly off screen and under the
   enemy plate. They do not read as eyes beside the target. On the rat they sit above the head and read as two
   small lights.
2. Rat f10-f20: the forming jaws are a faint green smear over the torso; the fangs do not read. On the treant
   (f14-f20) the fangs at the face do read.
3. Cue f22: the whole body flashes pale (the rat fills beige, the treant gets a thick pale-green rim and a pale
   canopy). This is the same look as Ashen's reservation 2, now in two spells, so it is the shared spell hit flash,
   not this spell.
4. Drip f40: a large bright green V of goo over the chest; it reads as venom, though crude.
5. Mist: faint until about f68, the spell_feel residue WARN (the cheap fix is in Checks).

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass | no layer moves (no drift or rotate); the only change is the jaws' drawing between f0-f4, and the 0.06 s `fadeIn` |
| Cue on one frame | pass | `timing.json` `hitFrame` 21 at 1x, 14 at 1.5x; sound, "-3" and badge on it; the snap is `at: hit` offset 0 |
| Nothing marked visible leaves the stage | pass | sizes in the beat table; the eyes at 0.62 body-heights over a treant are 30 px glints above the canopy, still on the stage |

`spell_feel` (1x, runtime capture, `spell_feel_r1_1x/`):
- Treant: contact f21 (visual f21); biggest rise after release f21, which is the cue; peak at f21. WARNs: residue tail 1.10 s (limit 0.5) and
  "contact desync: authored hit cue is frame 24, shows on 21". The 3-frame gap is this capture path's first-drawn wait (`gif.ps1`'s timing.json reports
  hitFrame 21 for the same authored 0.40 s), so it is a harness offset, not a drift.
- Rat: contact f21; WARN residue tail 1.28 s; WARN biggest rise after release is f96, which is the rat's own idle pose change after the cast
  (no spell layer is alive then; the gif at f60-f70 is clean), plus the same 3-frame offset.
- **The residue WARN is real:** the mist lives to 1.45 s after the cue. Cheap to fix in round 2 (mist window 0.35, life 0.4-0.6 would end near 1.1 s).
  Not changed, to hold the captures steady for step 3.
- Cue-is-biggest-rise passes on the treant and on the rat's own frames: the wind-up is dim (jaws opacity 0.6, no glow, eyes glow 0.6) and the snap is glow 1.25 plus punch.

Advisory:

- **Pop-in:** the jaws' first frame is faint (f0) and fades in; the snap and the burst cut in by design.
- **Frozen holds:** the eyes breathe; the jaws change drawing every 0.055 s; nothing holds still for more than 0.1 s.
- **Flash guard:** the snap and the burst are two events 0.18 s apart (Poison only), each a green burst plus the typed hit flash; not measured with the rendered-frame tool.

Gates: dotnet slice `VipersBitePresentationTests`, `VipersBiteTests`, `SpellLayerRulesTests`, `SpellPoolCapacityTests`, `SpellBaselineTimingTests`,
`MeleeSoundOverrideTests` green (128). The commit gate (`run_tests_parallel.ps1 -Changed -BuildContent -Shards 1`, promoted itself to the full run) was
green on the final content: EditMode 6246 (6243 passed, 3 skipped), PlayMode 2046 (1955 passed, 91 skipped), 0 failed.

## Rubric (worker's score; the orchestrator's check is still due)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ✔. Eyes, jaws, snap, drip and mist are distinct and in order on rat and treant; the eyes are round dots, not slits.
2. Weight and easing feel physical: ~. The jaws are four stepped frames with no travel, so the closing has no speed; the snap's punch carries the weight.
3. One focal point at a time: ✔ at the cue (the snap alone); the eyes are still visible for 3 frames after it.
4. The body stays readable at the hit: ✔ on rat and treant for the bite (a quarter to a fifth); ~ on the detonation (40-50% for 0.13 s).
5. The ending decays, not cuts: ✔. Drips and mist thin out over about 0.6 s, with one long faint tail.
6. Distinct from and consistent with the approved spells: ~. Not played beside the Censer or Ashen Reckoning; green venom and a lime glow are shared with Ashen's Poison.

Art, not scored: every piece is an old painted stand-in (jaws sheet, particles) or the owner's painted burst.

## Art record (one row per piece)

| Piece | Accepted prompt | Source file | Recipe | Output | Attempts |
|---|---|---|---|---|---|
| Jaws and drainage (stand-in) | n/a | `Art/Sheets/Spells/Viper's Bite/vipers_bite_atlas_v1.png` cells 1-8, `vipers_bite_atlas_v3.png` cells 9-12 | `recipes/vipers_bite.json` (unchanged) | `Resources/Spells/vipers_bite` (f0-f4 jaws, f5 snap, f6-f7 closed, f8-f11 drain) | 0 |
| Wisps and drops (stand-in) | n/a | `vipers_bite_atlas_v1.png` cells 13-16 | `recipes/vipers_bite_particles.json` (unchanged) | `Resources/Spells/vipers_bite_particles` | 0 |
| Venom burst (owner's painted still, kept) | n/a | `vipers_bite_burst.png` | `recipes/vipers_bite_burst.json` (unchanged) | `Resources/Spells/vipers_bite_burst` | 0 |
| Eye glint (stand-in: the round glow disc) | n/a | `Resources/Spells/glow_disc` | n/a | n/a | 0 |
| Snake-head lunge (rejected, not referenced) | n/a | `vipers_bite_lunge.png`, `vipers_bite_lunge-v2.png` | `recipes/vipers_bite_lunge.json` | `Resources/Spells/vipers_bite_lunge` | the owner's two passes |

The lunge recipe and its generated folder stay in the tree unreferenced; delete both with the sheets when the owner confirms the head is gone for good.

### Art needed for step 4

**Step 3 passed (owner, 2026-10-07): the round 2 motion is approved. Sizes below follow round 2.**

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`; the style block and its attachments as in its section 1. **The subjects are green (venom, scales),
so the backdrop is solid #FF00FF, the recipes are keyed `"keyed": "magenta"`, and "no pink or magenta in the subject" replaces the no-green line.** At play
size the pieces sit on a dark grey rat and a brown-and-green treant: pale ivory fangs, a light lime fill and a dark outline.

1. **Snake eyes** (the anticipation)
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

2. **Jaws** (the delivery, the payoff and the settle)
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

3. **Venom drip and mist** (the resolution)
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

4. **Venom burst** (the detonation; optional restyle of the owner's painted piece to the flat style)
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

### Step 5: art inspection (orchestrator, 2026-10-07)

All four pieces delivered at the right canvas on solid magenta and accepted, no revision:
- **Eyes:** two cells, lime slit eyes with a dark outline, half-open then open. Use them as an opening pair; the engine
  places two of them.
- **Jaws:** 3x2 teal scaled jaws seen from the side (no head): open, closing, closing; then the snap with a lime spark,
  shut, and shut and dripping. Register them on one pivot.
- **Venom:** row 1 drips that thin out across the 4 cells, hung from the cell's top edge (pivot at the top); row 2 a
  drop, a small drop and two mist wisps.
- **Burst:** a flat lime splash plume.

## Round 2 (2026-10-07, owner: "the jaws have to be bigger, definitely")

Captures `vipers_bite_r2_rat_1x`, `_treant_1x`, `_rat_poison_1x` (previews committed, full GIFs untracked, `r2_*_timing.json`; cue f21 at 1x). All edits are in
`ContentData/skills.json`; no new field, no new art. The owner's call overrides contract 2 (payoff at most a third of the body) for the snap; the fully
covering moment is f21-f27 (about 0.1 s), then the jaws thin to the dim `closed` layer.

**What changed**

- Sizes: `jaws` 140 to 230, `bite` 120 to 200, `closed` 110 to 190.
- Forming jaws read on dark fur: `jaws` opacity 0.6 to 0.95 and glow 0 to 0.7 (still under the snap's 1.25); `closed` opacity 0.6 to 0.85.
- Glints: `place: above`, `height` 0.62 to 0.17 (face height on both bodies; Court's 0.3 put the treant's under its canopy line), `dx` -50/-22 to -108/-56
  (enemies face left, so negative is toward the face), size 30 to 32. Tried 0.3 and size 40 with dx -88/-52 first: on the treant they sat above the
  face and the two discs merged into one blob; the final spacing keeps two separate points.
- Mist: `window` 0.5 to 0.35, `lifeMin/lifeMax` 0.5/0.8 to 0.4/0.6.
- Burst, hit flash, drip, settle: unchanged. `VipersBitePresentationTests` pinned "jaws glow is 0"; now "jaws glow is under the snap's 1.25".

**Beat table changes** (all else as round 1)

| t | Layer | Now |
|---|---|---|
| 0.04-0.38 / 0.10-0.38 | `eye_a`, `eye_b` | 32 box, `above` 0.17, dx -108 / -56 |
| 0.14-0.41 | `jaws` | 230 box, opacity 0.95, glow 0.7 |
| 0.40 | `bite` | 200 box, glow 1.25, punch 0.3 |
| 0.50-0.70 | `closed` | 190 box, opacity 0.85 |
| 0.55-0.90 | `mist` | window 0.35, life 0.4-0.6 |

**Playback by frame** (my own contact sheets, 60 fps)

- Rat f4-f8: two lime glints open at face height on and in front of the snout; they read as lights beside the head, the second a few frames later.
  They are small and soft, not slit eyes (art item 1 still stands).
- Treant f4-f8: the glints sit at the left edge of the face beside the orange eyes, inside the frame and clear of the plate and canopy top.
- Rat f8-f20: green jaw wisps at f8, from f12 the upper and lower fangs read clearly in ivory on a green mouth over the whole torso; the fur shows through.
  Treant f12-f20: the same, the jaws bracket the face, its orange eyes stay readable between the halves.
- f21-f27: the snap fills the body: on the rat the face is covered, on the treant the face and the top of the canopy. Bright starburst, "-3", badge.
  f28 onward the body is back; the closed jaws thin over f28-f34.
- Rat f34-f46: drops and drip over the torso, thin mist; clear by about f52.
- Poison rat: f21 snap; the burst plume at f34-f40 as in round 1, "-3" and "-9" side by side; the body is clear by f46.

**Orchestrator's defects against round 2**

| # | Defect | Fixed | Evidence |
|---|---|---|---|
| 1 | Jaws bigger | yes | 230/200/190; f13 jaws span the rat and the treant's whole face and trunk top |
| 1b | Forming jaws on the rat | yes | f12-f20 fangs read in ivory on dark fur (round 1: a faint smear) |
| 2 | Glints beside the head | mostly | face height on both; on the rat they sit at the snout, on the treant at the face's left edge; a pair, small and soft |
| 3 | Mist tail | fixed by config, not measured | window 0.35, life 0.6 max, so the last particle is at about 0.15+0.35+0.6 = 1.1 s from the cue; visually clear by f52. `spell_feel` was not run this round (it pops a window), so the residue number is open |
| 4 | Burst | unchanged | owner |

**Rubric (worker's score, round 2)**: 1 yes, 2 ~ (still four stepped frames, no travel), 3 ~ (the snap is the biggest rise; the 0.7 glow on the forming jaws
makes the wind-up much stronger than round 1), 4 ~ (the snap covers the body for 0.1 s by the owner's call), 5 yes, 6 ~.

## Step 6: art integrated and compared in context (2026-10-07)

Captures `tools/gif.ps1 spell vipers_bite -Vs rat`, `-Vs treant`, `-Vs rat -PreStatus Poison`, all 1x. Copies beside this file:
`vipers_bite_step6_{rat,treant,rat_poison}_1x.gif` (untracked), `*_preview.gif` (committed), `step6_*_timing.json` (cue f21, freed f87).
Round 2 timing (cue 0.40 s), sizes (jaws 230 / bite 200 / closed 190, glints 32 at height 0.17, dx -108/-56) and beat order are kept.

### Art record

| Piece | Source | Recipe | Output | Attempts |
|---|---|---|---|---|
| Eyes | `vipers_bite_eyes.png` (2x1 of 256) | `recipes/vipers_bite_eyes.json`: hue key (magenta), `content_crop` (229x119), `alpha_floor` 40 | `Resources/Spells/vipers_bite_eyes` f0-f1 | accepted at step 5, wired first try |
| Jaws | `vipers_bite_jaws.png` (3x2 of 512) | `recipes/vipers_bite.json` rewritten: hue key, `content_crop` (354x446), `alpha_floor` 40 | `Resources/Spells/vipers_bite` f0-f5 (f0-f2 forming, f3 snap, f4 shut, f5 shut and dripping; f6-f11 removed) | accepted at step 5, wired first try |
| Drip | `vipers_bite_venom.png` row 1 | `recipes/vipers_bite_drip.json` (new): hue key, `alpha_floor` 40, no crop (pivot at the cell's top), `feather` top 0.1 | `Resources/Spells/vipers_bite_drip` f0-f3 | one revision: the cells are cut flat at the top edge and the cut showed as a straight line on the body, so the top 10% is feathered |
| Particles | `vipers_bite_venom.png` row 2 | `recipes/vipers_bite_particles.json` rewritten: hue key, `alpha_floor` 40 | `..._particles` f0 drop, f1 small drop, f2-f3 vapour | accepted at step 5, wired first try |
| Burst | `vipers_bite_burst_flat.png` (1024) | `recipes/vipers_bite_burst.json` rewritten: hue key, `content_crop` (818x875), `alpha_floor` 40 | `..._burst` f0 | accepted at step 5, wired first try |

The stand-ins (`vipers_bite_atlas_v1/v3.png`, `vipers_bite_burst.png`, the `glow_disc` glints) stay on disk, unreferenced. No new tooling.
Pivots are in the cropped frames: jaws, eyes 0.5/0.5 (the prompts' centres are the crops' centres to 0.01); drip `impactY` 0.5 to 1 (pivot at the top);
burst `impactY` 0.2 to 0.01 (base of the plume, 85% down the cell, is the bottom of the crop).

**Changes in `skills.json`:** glints `render` still to sprite, `glow_disc` to `vipers_bite_eyes` startFrame 1 fps 8 (half open, then open), tint removed so the
lime of the art shows; jaws `fps` 18 to 10 (three forming cells in 0.27 s), `bite` startFrame 6 to 4, `closed` startFrame 7 to 5, `drip` to
`vipers_bite_drip` startFrame 1 `impactY` 1, `settle` weights `[1,1,0,0]` (the drops; mist and detonate already took the vapour cells 2-3), `pop` `impactY` 0.01.
`VipersBitePresentationTests`: the snap is startFrame 4.

### Playback, as seen (60 fps, my own contact sheets, cue f21)

1. **Eyes as a pair: yes.** Rat f5: two lime slits with dark pupils at the snout, the near one half open; f8 both open. The far one is hidden behind the
   forming jaws from about f12. Treant f5-f11: the pair sits at the left edge of the face above its orange eyes, clear of the canopy; the second is
   half behind the plate. They read as eyes, not dots, but at 32 px they are small (about 32x17).
2. **Jaws clamp the body, fangs read: yes.** Rat f11-f17: teal scales over the whole torso, ivory fangs sharp against the grey fur. Treant f11-f17: the jaws
   bracket the face and trunk top, the orange eyes show between the halves.
3. **Snap is the biggest rise: yes.** f21: the starburst cell at 200 with glow 1.25 and the "-3"/"-2"; the forming jaws are 0.7 glow, under it.
4. **Drip and mist as venom: yes for the drip, weak for the mist.** Rat f33-f46 and treant f35-f47: lime rivulets hang from the jaws and fall,
   with drops below; this reads as venom, where the stand-in was a smear. The vapour is faint and thin; it adds little.
5. **Mist residue: kept.** Window 0.35, life 0.4-0.6 unchanged; the body is clear of vapour by f50. The last drip stain fades by f62 (rat) and f58 (treant),
   the drip layer's own 0.2 s fade, not the mist. `spell_feel` was not run (it pops a window), so the residue number is still open.
6. **Detonation burst (rat, Poison): yes.** f30-f39: a flat lime plume with thrown droplets rises off the body, "-3" and "-9" side by side; it covers the
   rat's upper half and rises above its head for about 0.1 s; clear by f46. Same size and hold as round 2.

### Checks

- Content build (`GenerationRun.RunAll`, BelowNormal): BUILD-COMPLETE on all four sentinels.
- Gate: see the commit.
- Defect found and fixed in this step: the drip cells' flat top edge (feathered, above).

### Rubric (worker's score, step 6)

1 yes, 2 yes (art; still four stepped frames, no travel), 3 yes, 4 yes (the burst still covers about half the rat for about 0.1 s), 5 yes, 6 yes.

### Round log row and measures

Round log: Step 6 (2026-10-07), art swapped for the delivered sheets, drip top feathered; caught by the worker's frame check; rubric above.
Measures: owner reviews 0; internal retries 1 (drip top edge); generation attempts per art piece 0 at this step; the step took about 35 min active, captures 5 x 25 s.
Snapshot id: `79996a9d` (step 6 commit); capture runs `20261007T204449Z_0285f59a157a` (rat, first wiring), `20261007T204808Z_5eea02a58920` (treant), `20261007T204909Z_897db6826a73` (rat), `20261007T204956Z_fd5879453228` (rat, Poison).

### Orchestrator's check of step 6 (2026-10-07, step6 full GIFs; frames at 60 fps)

For the owner's review. The art carries the spell: teal jaws close on the rat and the treant (f12-f20) with the fangs
sharp on dark fur, the snap lands on the cue (f22), the drips fall from the jaws (f34-f40), and the flat lime burst
reads on the Poison path (f34-f40). Open:
1. **Eyes are near invisible.** At 32 px only one small slit shows, at the rat's snout around f22; nothing reads at
   f6. Grow them (about 56 px) so the anticipation beat exists.
2. On the treant at the snap (f22) the jaws cover the face and the canopy. This is the owner's bigger-jaws call, so
   it is not changed.

### Tuning after step 6 (2026-10-07; `vipers_bite_tune_{rat,treant}_1x.gif`, strips beside it; values only, `skills.json`)

Changed: `eye_a` and `eye_b` size 32 to 56; `dx` -108/-56 to -134/-96, so the pair sits side by side left of the head, 38 px apart, not on the face.
(First try, -118/-52, put `eye_b` over the treant's own yellow eyes and only one slit showed.)

1. Eyes: **fixed**. Rat and treant f3-f4 (100-133 ms): two slit eyes read beside the head before the jaws. At f1-f2 only `eye_a` shows, because
   `eye_b` opens 0.06 s later (its authored offset).
2. Treant jaws covering the face at the snap: unchanged, the owner's call.

## Open owner questions

Each asked once. Status: open, answered (with the answer), or dropped.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | The glints are round dots above the head (on a treant they float above the canopy). Fine until the slit-eye art, or should they sit lower, beside the face, even if the treant's own eyes compete? | 1 | open |
| 2 | The detonation burst still covers about 40-50% of a rat for about 0.13 s (contract 2 is a third). Accept it as the "kept" painted piece, or shrink it further? | 1 | open |

## Round log

| Round | What changed | Who caught it (owner / orchestrator / binding check / worker) | Rubric result |
|---|---|---|---|
| Pass 1 (2026-10-04) | Old jaws and a splash crown; stage gloom 0.20 (owner O1); the detonation damage was never drawn (the "-3" showed over a bar that fell by 12), so the beat now carries `DetonatedAmount` and the view draws a second Poison number `detonationSeconds` later (pinned in `SpellPresentationMechanismTests`); `secondSfxPath` added for the detonation's own sound | worker (before/after capture) | not scored |
| Pass 2 (2026-10-04) | Owner's painted snake-head lunge, `feather` recipe key to dissolve its cut neck, painted venom burst | worker | not scored; the owner then rejected the head |
| Round 1 (2026-10-07) | New design: no head. Eye glints (`glow_disc`), jaws forming (old f0-f4), snap on the cue (f5), closed jaws, venom drip (f8-f11), mist emitter; cue 0.30 to 0.40 and the cast sound re-cut; burst kept and sized 420 to 200, held 0.22 to 0.1 s. Tuned within the round: eyes first sat on the rat's ear (moved to 0.62 above the head), the snap covered about half the rat at 190 (now 120) | worker (own frame sheets), the contract 2 coverage check | worker's score above; step 3 pending |
| Round 2 (2026-10-07) | Jaws 140/120/110 to 230/200/190, forming jaws opacity 0.95 glow 0.7, glints to face height (0.17, dx -108/-56), mist window 0.35 life 0.4-0.6 | owner (bigger jaws), orchestrator step 3 (glints, rat jaws, mist) | worker score in Round 2 |
| Tuning after step 6 (2026-10-07) | Eyes 32 to 56 px, dx -134/-96 | orchestrator's step 6 check | eyes read on rat and treant |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | 3 (eye position, snap size, burst size and hold); round 2: 2 (glint spacing and height, two recaptures) |
| Generation attempts per art piece | 0 (stand-ins) |
| Active work time / waiting time | about 1 h 20 min, of which the gate, the content builds and the captures were about 25 min |
| Owner handling time (generate, download, upload art; review) | none yet |
| Owner-found defects the binding checks should have caught (target 0) | 0 so far; the lunge's body-sized head was the owner's earlier rejection |
| Final quality against the reference pack (owner's verdict on the rubric) | not yet |

## Reviewed snapshot

- Commit: see the report (the commit that lands this file).
- Capture run ids: final runs are `20261007T161203Z_f48609f393b6` (rat), `20261007T161314Z_c30e4bae90ab` (treant),
  `20261007T162526Z_c11ae7a5ed87` (rat, Poison), `20261007T162633Z_b7e1f08b3294` (treant 1.5x), under `tools/screenshots/gif/runs/`.
- Rubric (motion, art): 1 ✔, 2 ~, 3 ✔, 4 ✔ bite / ~ burst, 5 ✔, 6 ~. Art: not scored (stand-ins).
- Still weak: round-dot eyes; the jaws have no travel; the 25% residue tail from the mist; the burst's coverage; the painted old jaws sheet is a different style
  from the rat.
