# Ashen Reckoning: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`.

## Status

Closed (owner, 2026-10-07): accepted after the kick (`7cc52e64`). Steps 3-7 done; step 7 in `7a3ea8ef`. No body glow (needs a spell-owned glow, plan E9, not built).

Captures: `tools/gif.ps1 spell ashen_reckoning -Vs rat|treant [-Speed 1.5] -PreStatus Poison`. Full GIFs sit beside this file, untracked; previews committed; treant 1x preview at `../../showcase/ashen_reckoning_trial_preview.gif`. Older `ashen_reckoning_*_poison_*.gif` and `spell_feel_*` folders are the 2026-10-04 version (before/after). Untracked `ashen_reckoning_catch.png` (2026-10-04, painted) is the pre-workflow still, not step 4 art.

## Identity

- **Verb:** ignite.
- **Signature beat:** the green venom round the target CATCHES: green turns to fire.
- **Must read as:** "the poison caught fire".
- **Must NOT read as:** a fireball thrown at it; an explosion or nuke; a magic circle; cyan fire.
- **Motion references:**
  - `ritual`: smoke drawn into a vortex, spiralling inward and speeding up, denser and brighter toward the catch;
  - `catch`, `catch_b`, `catch_c`, `venom_hold`: a gas burner lighting, a quick whoomph swell that settles, sized to the body, ignited from the vortex core outward;
  - `flare`: a flare-up as fuel is added;
  - `fire_a`-`fire_d`, `embers_a`-`_c`, `ash`: a dying campfire. Flames shrink and gutter in steps, embers lose glow one batch at a time, ash falls.
- **Exceptions:** the catch core appears instantly (impact); the wind-up ends on the cue with no fade (`venom_hold` carries the last drawing across); the turn stops at the cue.
- **Mechanics, unchanged:** Poison packet then Fire packet, each number with its own accent (`-3` Poison green on the cue, `-9` Fire red 3 frames later); sound, number and badge on the cue frame.

## Beat table

Release clock; hit cue **0.6 s** = f35 at 1x, f23 at 1.5x (`timing.json`); `clearedSeconds` 2.800 (the `ash` emitter is last). Fires and stills are on the release clock, not `at: hit` (a hit still is capped at 0.75 of the body, `TargetBody.TailMaxOfBody`). Largest box on the treant: 460 x 1.63 fit = 750 px at the body middle.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.60 | wind-up, turning ever faster, 150 deg in all | `ritual` | vortex cells 1-5 (pull baked 1.3x to 0.9x) | 460; fade-in 0.4, glow 0.1 to 1.2; `rotate` rising quarter sine (amp -150, period 2.4, phase 270), at most 6.5 deg/frame |
| 0.00-0.90 | stage gloom 0.15 | stage `gloom` | n/a | in 0.4, out 0.3 after the cue |
| 0.60 | catch, Poison packet: sound, `-3`, Vulnerable badge, accent; small bright core | `catch` | catch cell 1 | 280; opens at once, punch |
| 0.60 | green gives way | `venom_hold` | vortex cell 5 | 380 box, held 0.05 s, fade 0.2, opacity 0.75 |
| 0.65 | fire spreads | `catch_b` | catch cell 2 | 235; fade-in 0.04, fade 0.1 |
| 0.70 | ring at its widest, hands off to the campfire | `catch_c` | catch cell 3 | 250; opacity 0.7, fade 0.15 |
| 0.60 | venom drops | `drops` | emitter | 8 particles |
| 0.65 | flare, Fire packet: second sound, `-9` | `flare` | catch cell 3 | 270; 0.06 s, glow 1.8, opacity 0.6, fade 0.15 |
| 0.65 | embers and flame tongues | `settle` | emitter | 14 particles |
| 0.65-1.45 | campfire takes at the feet | `fire_a` | `censer_flame_loop` | 220; fade-in 0.15, 8 fps, glow 1.0 to 0.5 |
| 0.65-1.40 | bright coals float up | `embers_a` | censer particles | glow 1.2 |
| 1.05-1.60 | smaller, dimmer fire | `fire_b` | `censer_flame_loop` | 170; glow 0.4, breathe 0.35 |
| 1.00-1.70 | embers dim | `embers_b` | censer particles | glow 0.5 |
| 1.35-1.85 | low fire, flickering hard | `fire_c` | `censer_flame_loop` | 125; glow 0.15, breathe 0.5 |
| 1.35-1.95 | last embers | `embers_c` | censer particles | glow 0, opacity 0.7 |
| 1.00-2.80 | ash flakes fall | `ash` | smoke cells 3-4 | size 0.7-1.1, gravity -220, 12/s for 0.6 s |
| 1.60-2.10 | gutter: a few low flames | `fire_d` | `censer_flame_loop` | 85; fade 0.3 |

Stage kick on the cue: `vfx.kick` 0.025 / 0.03 / 0.18, as the Censer; the stage punches in at f35, eases back by f40, equals the pre-cue framing at f44; no `unkicked` layer.

## Open items

1. Rat f20-f36: the vortex is dense by f20, so the cue reads by colour more than energy (`spell_feel` WARN, contact desync). A lighter vortex needs new art, not tuning (`ritual` opacity 0.85 to 0.7 changed nothing). Accepted at closure.
2. Treant f38-f46: the flame ring covers the face and upper torso for about 0.15 s. Accepted at closure.
3. Treant f36-f38: roots and legs fill pale green; reads as a fill, not the rim contract E10 asks for. Milder on the rat. Accepted at closure.
4. Treant `spell_feel` WARN, residue tail: the campfire steps down over about 1.1 s, by design. Not changed.
5. Ash flakes are small near the head at 1x. Accepted.
6. Body glow: needs a spell-owned body glow (plan E9, not built). **Owner call** if wanted.
7. Catch cell 4 (flat ground ellipse) is unplayed: cell 3 is an upright hoop, so the catch ends on cell 3 and hands to the flame loop. Chosen from stills, not motion.
8. Pieces 3 (campfire loop) and 5 (venom drops) not made, optional; `censer_flame_loop` and `ashen_reckoning_particles` f0 stand in.
9. `audio_levels.json` not touched: the 0.6 s cast cut measures -18.1 dB RMS, so its gain would move from -2.7 to +0.1 dB. Whoever commits that table applies it.
10. Cue flash guard: core, flare 3 frames later and the hit rim fall in one second, counted as one large flash; not measured with the rendered-frame tool.
11. Disk was 100% full (5 GB free) at round 2; run `tools/gif.ps1 -TrimFrames` after filming.

## Decisions

- 2026-10-05 (orchestrator): wind-up 0.6 s, cast sound re-cut (`tools/make_spell_cast_cues.py`, `MeleeSoundOverrideTests` 0.60; build peak at 0.54 s plus 0.06 s silence). The owner can reverse.
- 2026-10-05 (orchestrator): ending about 2.4 s after the cue is OK. The owner can reverse.
- 2026-10-05 (orchestrator): key colour for the venom art magenta `#FF00FF`; moot, art arrived already keyed.
- 2026-10-05 (orchestrator): hit flash is a rim globally (`BodyLight` `FlashInteriorShare` 0.22 to 0.08, `FlashRimWidth` 0.022 to 0.012).
- 2026-10-07 (orchestrator): art accepted at step 5, no revision prompt.
- 2026-10-07 (owner): step 3 passed, round 2 motion accepted.
- 2026-10-07 (owner): smoke layers (`decay`, `smoke`, `curl`) removed; ash kept; timings not redistributed.
- 2026-10-07 (owner): polish: kick added, stoke dropped (it only lifts a Burn status glow, `StageBurnGlow`); no body glow.
- 2026-10-07 (owner): closed, accepted as is after the kick.

## Art

Delivered 2026-10-07 already keyed (RGBA, exact canvases). Recipes use `alpha_floor` 40 (cells hold 750-8000 pixels at alpha 1-40: outline fringe and sparse specks, no key spill). Slicer gained recipe `alpha_floor` and sequence-step `shift` (`docs/ART_PIPELINE.md`, "Composing frames"); the `feather_edges` name clash is fixed (ring one is `feather_ring`).

| Piece | File | Recipe | Status |
|---|---|---|---|
| Venom vortex | `ashen_reckoning_venom_vortex.png` 2560x512 5x1 | `recipes/ashen_reckoning_venom_vortex.json`: cells baked 1.3x/1.18x/1.08x/1.0x/0.9x (`SpellLayer` has no scale-over-life) | accepted; radius equal across cells, "inward" is density plus baked scale |
| Catch | `ashen_reckoning_catch_cel.png` 2048x512 4x1 | `recipes/ashen_reckoning_catch_cel.json`: cell 1 `shift` [50, -54] onto the centre | accepted; cell 4 unplayed |
| Smoke and ash | `ashen_reckoning_smoke_ash.png` 2048x512 4x1 | `recipes/ashen_reckoning_smoke_ash.json` | accepted; cool grey, existing warm tints kept |
| Campfire loop, venom drops | not delivered (optional) | n/a | stand-in: `censer_flame_loop`, `ashen_reckoning_particles` f0 |
| Atlas f0-f11 | `Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_atlas_v1.png` | `recipes/ashen_reckoning*.json` | stand-in, no longer played; kept for recipe and drift tests |

Step-4 prompts, verbatim. Pieces 1-4 delivered; 5 optional. Style block and attachments as in `docs/prompts/TEMPLATE_spell_art.md` section 1.

1. **Venom vortex sheet** (the wind-up; replaces f0-f4)
```
Piece: Ashen Reckoning: the target's own venom winding inward
Beat it serves: wind-up, 0.00-0.60 s (ritual)
Filename: ashen_reckoning_venom_vortex.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_venom_vortex.png
Canvas: 2560x512   Grid: 5x1 of 512x512
Pivot: centre of every cell (50%, 50%), the vortex's axis
Size in play: about the width of the target's body
Drawing: flat cel ribbons of toxic venom, bold dark outline, two green tones and a pale hit. Cell 1 a few thin
  wisps far from the axis; cell 2 more, curving in; cell 3 ribbons winding round the axis; cell 4 a tight spiral;
  cell 5 a dense knot round an empty centre (the coal is drawn elsewhere). A few outlined drops.
Verb: ignite (the build to it): smoke drawn into a vortex, faster as it closes.
Must read as: venom being drawn inward, speeding up.
Must NOT read as: a magic circle or rune ring; a cyclone of wind; fire.
Not in the picture: the coal, any orange, the target, smoke, glow.
```
(on solid `#FF00FF`, no magenta or pink in the subject; the engine turns the sheet.)

2. **Catch burst sheet** (the signature beat; replaces ashen f6)
```
Piece: Ashen Reckoning: the venom catches fire
Beat it serves: catch, 0.60 s (catch, catch_b, catch_c) and flare 0.65 s (flare)
Filename: ashen_reckoning_catch_cel.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_catch_cel.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: centre of every cell (50%, 50%)
Size in play: grows to the width of the target's body and no more; it leaves the face clear
Drawing: flat cel flame only, bold outline, deep red outside, orange, a small yellow core. Cell 1: a small bright
  core of flame. Cell 2: the flame spreading outward in a ring of tongues round an empty centre. Cell 3: the ring
  at its widest, tongues leaning in. Cell 4: the ring settled to a low band of tongues. No venom in it (the venom
  sheet is drawn separately, in the same ring shape).
Verb: ignite: a gas burner lighting, a quick whoomph then it settles.
Must read as: the ring of poison catching fire.
Must NOT read as: a fireball or an explosion thrown at the target; a magic circle; cyan or teal fire; a star burst.
Not in the picture: green, venom, coal, rocks, sparks, smoke, glow.
```

3. **Campfire loop** (optional: the censer's cel flame loop does this today): a 4x1 of 768x384, a waist-low band.
   Cell 1 full, cells 2 and 3 smaller tongues, cell 4 a few low flames on a coal line. Pivot: the coal line at 12.5%
   from the bottom, centred. Must NOT read as: a bonfire taller than the body, a torch.

4. **Smoke curl and ash sheet** (the last curl, the ash)
```
Piece: Ashen Reckoning: a curl of smoke and ash flakes
Beat it serves: ending, 1.8-3.0 s (curl, smoke, ash)
Filename: ashen_reckoning_smoke_ash.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_smoke_ash.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: centre of every cell
Size in play: the curl about half the body's height
Drawing: flat cel, bold outline, warm grey in two tones with a pale hit. Cell 1 one S-shaped curl of smoke, thick
  at the base; cell 2 a thinner broken curl; cell 3 three flakes of ash, angular and large enough to read at 1x;
  cell 4 two flakes and a coal chip.
Verb: ignite (the end of it): a dying campfire's last smoke.
Must read as: the last curl of smoke and flakes of ash.
Must NOT read as: a cloud, a storm, a gas attack, soft painted vapour.
Not in the picture: flame, glow, the ground, colour other than grey.
```

5. **Venom drops** (optional, low priority): the drop sprite, outlined, two greens, on the magenta key, 2x2 of 256.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pre-workflow | painted catch, tinted; cue and two packets; no ending. Owner: "best" of ten, "needs an ending" | worker | n/a |
| 2026-10-05 | trial 1, round 1, blocks 1-4 | fires as `at: hit` stills all one size, then a flame band read as a shelf, then f6 at body size read as an explosion; settled on f6 at 210 box and a fire/decay/curl ending | worker (stills, pixel measure) | `6b5a81fa`, `cdb869ac` |
| 2026-10-05 | round 2 | pop-in at f0, catch an explosion with the body filled pale green, body hidden 0.35 s, weak ending. Now: 0.6 s building wind-up, core spreading to body width, global hit rim, burst behind the body, four-step ending, three ember batches, ash, large curl | orchestrator | `9fd78fca` |
| 2026-10-07 | step 6 | step 4 art wired; ring sizes cut from 330/290/310/340 to 280/235/250/270 (ring wider than treant, hid face); ash spread after a clump covered the face | worker | `7a3ea8ef` |
| 2026-10-07 | owner | smoke layers removed (`step6b` captures); kick added, stoke dropped (`step6c` captures) | owner | `7cc52e64` |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 before closure |
| Internal retries | round 1: 3 block revisions, 1 test fix; round 2: 4 captures settling flash and coverage, 1 test fix (tick 0 of a fade-in) |
| Attempts per art piece | 1 each for the three delivered sheets |
| Active / waiting time | round 1 about 80/20 min; round 2 about 70/35 min (estimates) |
| Owner-found defects the checks should have caught | none. The orchestrator found the pop-in (advisory, untested) and the filled-silhouette flash (no check) |
| Captures | round 1: 6; round 2: 7 treant 1x, rat 1x, treant 1.5x, a censer, a melee attack; step 6: 3 filmed, 1 first try, 2 `spell_feel` runs |

## Snapshot

- **Commit:** `7a3ea8ef` (step 6 integration), then owner polish `7cc52e64`.
- **Capture run ids:** step 6 treant 1x `20261007T120009Z_2271e90b1699`, rat 1x `20261007T120220Z_c5443b8abbe4`, treant 1.5x `20261007T120344Z_b1d8189eba42`. Round 2: treant 1x `20261005T125314Z_5888648fb1a9`, rat 1x `20261005T125429Z_cde8c2fab1d8`, treant 1.5x `20261005T125534Z_79058fbf7da0` (frames deleted for disk).
- **Gate:** `run_tests_parallel.ps1 -Changed` promoted to full (changed `tools/make_spell_cast_cues.py`): EditMode 6242/6245 (3 skipped), PlayMode 1955/2046 (91 skipped), 0 failed. Round 1 gates failed on disk space (Unity "Disk space low").
- **Binding checks:** no transform jump pass (only `ritual` turns, at most 6.5 deg/frame, 0 at the cue); cue on one frame pass (`hitFrame` 35 / 23, `-9` 3 frames later; `SpellLayerHandoffTests`, `AshenReckoningTests`, `AshenReckoningPresentationTests`, `MeleeSoundOverrideTests`); nothing marked visible leaves the stage pass (largest 460 box).
- **Rubric (worker, step 6):** 1 beats in order ✔; 2 weight and easing ✔; 3 one focal point ~ (f35-f46: knot, ring, two numbers); 4 body readable at the hit ~ (knot covers torso f20-f46, ring torso and part of head f38-f46); 5 ending decays ✔ (ash ~, small flakes); 6 distinct from the Censer ✔ (shares the cel campfire loop and ember sprites; both end on smoke).
- **Still weak:** the green is overrun rather than recoloured; the torso is covered about 0.18 s; the ash is barely readable at 1x.
