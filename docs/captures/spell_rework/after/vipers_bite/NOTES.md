# Viper's Bite: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3.

## Status

Round 4 landed in `7f5e1821` (eyes inside the bite, darker); round 3 `95c4fed6` (ghost jaws restored). Waits on the owner's review of the round 4 previews (`vipers_bite_eyes2_{rat,treant}_preview.gif`). Step 7 not recorded since the owner's round 3-4 changes.

Captures: `tools/gif.ps1 spell vipers_bite` (`-Vs rat`, `-Vs treant`, `-Vs rat -PreStatus Poison`, `-Vs treant -Speed 1.5`); previews committed, full GIFs untracked. The 2026-10-04 snake-head lunges (`vipers_bite_rat_1x.gif` etc.) are rejected by the owner and stay untracked beside this file.

## Identity

- **Verb:** bite.
- **Signature beat:** a pair of jaws closes on the body and the fangs snap shut on the cue, with the number and the Poison badge.
- **Must read as:** "a snake bit it": eyes in the dark, then jaws and fangs on the body; menacing, not cute.
- **Must NOT read as:** a snake head or any whole animal (owner: it looks bad); a thrown or flying object; a green blob over the body; a cartoon cat.
- **Motion references:**
  - `eye_a`, `eye_b`: a snake's eyes opening in the dark one after the other, then holding; they do not move;
  - `jaws`: a strike's jaws closing in stepped drawings from faint to full, no travel;
  - `bite`: the snap, one frame, a small punch, the brightest thing in the spell;
  - `drip`: venom running down fur, falling, thinning; `mist`: vapour off a bite, rising and thinning.
- **Exceptions:** the jaws pop in at their first frame (fade in 0.06 s, the drawing does not move); the snap is one held frame (0.1 s); the detonation burst is a different style from the rest.
- **Mechanics, unchanged:** 3 Poison damage, detonates Poison already on the target (second number "-9" and `spore_cloud_impact` 0.18 s after the cue), then applies 3 Poison; cue sound, number and badge on one frame. The detonation number is `DetonatedAmount` drawn `detonationSeconds` later (`SpellPresentationMechanismTests`); `secondSfxPath` is the detonation's own sound.

## Beat table

Release clock; cue **0.40 s** = f21 at 1x, f14 at 1.5x (`timing.json`; cast sound re-cut, `MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`); freed f87, `clearedSeconds` 1.85. Up to 5 of the effects band's 12 sprite layers on a body.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.40 | stage gloom 0.20 | stage `gloom` | n/a | in 0.15, holds to the cue, out 0.25 (owner O1) |
| 0.04-0.38 | left eye opens inside the jaw gap | `eye_a` | eyes sheet, startFrame 1 fps 8 (half open, then open) | 56; `place: above`, `height` 0.12, `impactY` 0.5, `dx` -28 (fit-scaled, like the jaws), `aspect` 2.6, tint `#66741a`, glow 0.15, `fadeIn` 0.08, `breathe`, fade 0.08 |
| 0.10-0.38 | right eye opens 0.06 s later | `eye_b` | same | 56; `dx` +28, seconds 0.28 |
| 0.14-0.41 | frontal ghost jaws form, fangs show | `jaws` | `vipers_bite_ghost_jaws` startFrame 1 | 230; 18 fps, opacity 0.95, glow 0.7, `fadeIn` 0.06 |
| 0.40 | **Cue.** The snap in a starburst; sound, "-3", Poison badge | `bite` | ghost jaws f5 (startFrame 6) | 200; punch 0.3, glow 1.25, held 0.1, fade 0.14 |
| 0.40 | drops thrown down | `settle` | particles f0-f1 (weights `[1,1,0,0]`) | emitter, 12 particles |
| 0.50-0.70 | jaws shut, fangs in, fading | `closed` | ghost jaws f6-f7 (startFrame 7) | 190; 10 fps, opacity 0.85, fade 0.15 |
| 0.55-0.90 | green vapour | `mist` | particles f2-f3 | emitter, 12/s, window 0.35, life 0.4-0.6, opacity 0.5 |
| 0.62-1.04 | venom runs down the body from the jaw line (starts 0.22 s after the cue) | `drip` | `vipers_bite_drip` f0-f3 (startFrame 1, `impactY` 1) | 150; `above` 0.18, 9 fps, `fadeIn` 0.06, fade 0.2 |
| 0.58 (Poison only) | detonation: flat lime plume, "-9", `spore_cloud_impact` | `pop`, `detonate` | `vipers_bite_burst` f0 (`impactY` 0.01) | 200; held 0.1, fade 0.15, glow 0.9; 16-particle spray |

## Open items

1. Burst coverage: the plume covers about half the rat f30-f39 (about 0.1 s) and rises above its head; contract 2 is a third. **Owner call**: accept or shrink further.
2. Snap f21-f27 covers the body (rat face; treant face and top of canopy): the owner's bigger-jaws call overrides contract 2 for the snap. Jaws thin to `closed` from f28.
3. `spell_feel` WARNs, round 4 same as round 3: treant residue tail 1.07 s (mist; limit 0.5) and contact desync (cue f21 vs authored f24, the capture path's first-drawn wait: harness offset); rat no accent (peak 6.5 over floor 2.4) with biggest rise f5 (rat pose change and eye glints, not the snap). Mist window 0.35, life max 0.6 already shortened; clear by f50-f52.
4. Treant eyes are about 60 px each (`fit target` scales them with the body) and sit over its own orange eyes f12-f18. Check at review.
5. Hit flash at the cue (f22) fills the body pale (rat beige, treant thick pale-green rim) in two spells (also Ashen); shared hit flash, not this spell.
6. Mist is faint; the drip is a large bright green V over the chest at f40.
7. Flash guard: snap and burst are two events 0.18 s apart (Poison only), each a green burst plus the typed hit flash; not measured with the rendered-frame tool.
8. Unreferenced art on disk, delete when the owner confirms: `vipers_bite_lunge.png`, `vipers_bite_lunge-v2.png`, `recipes/vipers_bite_lunge.json`, `Resources/Spells/vipers_bite_lunge`; side-view jaws `vipers_bite_jaws.png`, `recipes/vipers_bite.json`, `Resources/Spells/vipers_bite` (f0-f5); stand-ins `vipers_bite_atlas_v1/v3.png`, `vipers_bite_burst.png`.
9. Not yet played beside the Censer or Ashen Reckoning (rubric item 6; green venom and lime glow are shared with Ashen's Poison).

## Decisions

- 2026-10-04 (owner): the snake-head lunge is rejected.
- 2026-10-04 (owner): stage gloom 0.20 (O1).
- 2026-10-07 (owner): "the jaws have to be bigger, definitely": jaws 230 / bite 200 / closed 190; overrides contract 2 for the snap.
- 2026-10-07 (owner): step 3 passed, round 2 motion approved.
- 2026-10-07 (orchestrator): step 4 art accepted at step 5, no revision: eyes, jaws, venom, burst.
- 2026-10-08 (owner): `vipers_bite_jaws.png` (side-view jaws) rejected: "the frontal ghost viper bite is better than this weird-ass jaw". Round 2 ghost jaws restored.
- 2026-10-08 (owner): eyes go inside the bite and darker: "it looks like a cartoon cat and should be a menacing snake".
- Dropped: glints above the head (superseded by round 4); whether to keep the burst's painted piece (replaced by the flat burst).

## Art

Subjects are green, so the backdrop is solid `#FF00FF` and recipes are keyed `"keyed": "magenta"` ("no pink or magenta in the subject" replaces the no-green line). Recipes: hue key, `alpha_floor` 40. Pivots are in the cropped frames.

| Piece | File | Recipe | Status |
|---|---|---|---|
| Eyes | `vipers_bite_eyes.png` 2x1 of 256 | `recipes/vipers_bite_eyes.json`: `content_crop` 229x119 | accepted; wired, tinted dark olive in engine |
| Jaws, side view | `vipers_bite_jaws.png` 3x2 of 512 | `recipes/vipers_bite.json`: `content_crop` 354x446 | **rejected (owner 2026-10-08)**, unreferenced, kept on disk |
| Jaws, frontal ghost (round 2 art) | `Art/Sheets/Spells/Viper's Bite/vipers_bite_atlas_v1.png` cells 1-8 + `vipers_bite_atlas_v3.png` cells 9-12 | `recipes/vipers_bite_ghost_jaws.json` (pre-step-6 `vipers_bite` recipe; output `Resources/Spells/vipers_bite_ghost_jaws` f0-f11, PNGs byte-identical to `475f9849`, fresh GUIDs) | stand-in, in use by owner's choice |
| Drip | `vipers_bite_venom.png` row 1 | `recipes/vipers_bite_drip.json`: no crop (pivot at the cell top), `feather` top 0.1 (the flat cut showed as a line on the body) | accepted |
| Particles | `vipers_bite_venom.png` row 2 | `recipes/vipers_bite_particles.json` | accepted; f0 drop, f1 small drop, f2-f3 vapour |
| Burst | `vipers_bite_burst_flat.png` 1024 | `recipes/vipers_bite_burst.json`: `content_crop` 818x875, `impactY` 0.01 | accepted |
| Lunge (2026-10-04) | `vipers_bite_lunge.png`, `vipers_bite_lunge-v2.png` | `recipes/vipers_bite_lunge.json` | rejected, unreferenced |

Step-4 prompts, verbatim. Style block and attachments as in `docs/prompts/TEMPLATE_spell_art.md` section 1. Piece 2 (jaws) is rejected; the prompt stays as the record.

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

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | old jaws and a splash crown; gloom 0.20; detonation damage was never drawn, so the beat carries `DetonatedAmount` and the view draws a second Poison number; `secondSfxPath` added | worker | n/a |
| 2026-10-04 | pass 2 | painted snake-head lunge, `feather` recipe key, painted venom burst; owner then rejected the head | owner | n/a |
| 2026-10-07 | 1 | no head: glints, forming jaws, snap, closed jaws, drip, mist; cue 0.30 to 0.40 with cast sound re-cut; burst 420 to 200 box, hold 0.22 to 0.1 s; eyes moved off the rat's ear, snap cut from 190 to 120 | worker frames, contract 2 coverage | n/a |
| 2026-10-07 | 2 | jaws 140/120/110 to 230/200/190, forming jaws opacity 0.95 glow 0.7, glints to face height, mist window 0.35 | owner (bigger jaws); orchestrator (glints, rat jaws, mist) | n/a |
| 2026-10-07 | 6 | step 4 art wired; drip top feathered | worker frames | `79996a9d` |
| 2026-10-07 | tuning | eyes 32 to 56 px, `dx` -134/-96 (only one slit showed at 32 px) | orchestrator step 6 check | `479e7222` |
| 2026-10-08 | 3 | side-view jaw art rejected; ghost jaws restored; new eyes, drip, mist and burst kept | owner | `95c4fed6` |
| 2026-10-08 | 4 | eyes inside the bite, dark olive, flatter; rat f3 `eye_a` alone, f5 both, f12-f18 pair between the fang rows, f22 eyes out | owner | `7f5e1821` |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 before round 3 |
| Internal retries | round 1: 3 (eye position, snap size, burst size and hold); round 2: 2; step 6: 1 (drip top edge) |
| Attempts per art piece | 1 each (drip: one revision) |
| Active / waiting time | round 1 about 1 h 20 min (25 min gate, builds, captures); step 6 about 35 min active |
| Owner-found defects the checks should have caught | 0; the lunge's body-sized head was the owner's rejection |
| Quality vs reference pack | not yet |

## Snapshot

- **Commit:** `7f5e1821` (round 4); step 6 snapshot `79996a9d`.
- **Capture run ids:** step 6 `20261007T204449Z_0285f59a157a` (rat, first wiring), `20261007T204808Z_5eea02a58920` (treant), `20261007T204909Z_897db6826a73` (rat), `20261007T204956Z_fd5879453228` (rat, Poison). Round 1: `20261007T161203Z_f48609f393b6` (rat), `20261007T161314Z_c30e4bae90ab` (treant), `20261007T162526Z_c11ae7a5ed87` (rat, Poison), `20261007T162633Z_b7e1f08b3294` (treant 1.5x), under `tools/screenshots/gif/runs/`.
- **Gate:** `run_tests_parallel.ps1 -Changed -BuildContent -Shards 1` promoted to full on round 1 content: EditMode 6246 (6243 passed, 3 skipped), PlayMode 2046 (1955 passed, 91 skipped), 0 failed. Slices: `VipersBitePresentationTests`, `VipersBiteTests`, `SpellLayerRulesTests`, `SpellPoolCapacityTests`, `SpellBaselineTimingTests`, `MeleeSoundOverrideTests`.
- **Binding checks:** no transform jump pass (no layer moves; only the jaws' drawing and 0.06 s `fadeIn`); cue on one frame pass (`hitFrame` 21 / 14; snap `at: hit` offset 0); nothing visible leaves the stage pass.
- **Rubric (worker, step 6):** 1 beats in order ✔; 2 weight and easing ✔ (art; four stepped frames, no travel); 3 one focal point ✔; 4 body readable ✔ bite, ~ burst (about half the rat, 0.1 s); 5 ending decays ✔; 6 distinct ✔ (not seen beside the Censer).
- **Still weak:** jaws have no travel; mist residue WARN; burst coverage.
