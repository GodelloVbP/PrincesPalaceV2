# Ashen Reckoning: state record

Trial 1 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), step 2 "block with existing assets", round 2.
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`.
**Step 3 passed (owner, 2026-10-07): the round 2 motion is accepted. Steps 4-6 done (art delivered, accepted, wired);
step 7 closed in `7a3ea8ef`; see "Step 6".** The untracked `ashen_reckoning_catch.png` (2026-10-04, painted, green key) is the
pre-workflow still, not step 4 art.
Captures 2026-10-05 with `tools/gif.ps1 spell ashen_reckoning -Vs rat|treant [-Speed 1.5] -PreStatus Poison`.
Full GIFs sit next to this file, untracked (`ashen_reckoning_trial_*.gif`, 13-23 MB); the phone previews are
`*_preview.gif` beside them, and the treant 1x preview is committed as
`../../showcase/ashen_reckoning_trial_preview.gif`. The older `ashen_reckoning_*_poison_*.gif` and `spell_feel_*`
folders are the 2026-10-04 version, kept for the before/after.

## Identity sheet

Locked by the orchestrator; the owner confirms or edits it with the first GIF.

- **Verb:** ignite.
- **Signature beat:** the green venom swirling round the target CATCHES: the colour turns from green to fire.
- **Must read as:** "the poison caught fire".
- **Must NOT read as:** a fireball thrown at it; an explosion or nuke; a magic circle; cyan fire.
- **Motion reference per moving element:**
  - wind-up (`ritual`): smoke drawn into a vortex, spiralling inward and speeding up; it builds from a few faint
    wisps and gets denser and brighter toward the catch;
  - catch (`catch`, `catch_b`, `catch_c`, `venom_hold`): a gas burner lighting, a quick "whoomph" swell that then
    settles, sized to the body, ignited from the vortex's core outward;
  - second packet (`flare`): a flare-up as fuel is added;
  - ending (`fire_a` to `fire_d`, `decay`, `embers_a` to `_c`, `ash`, `smoke`, `curl`): a dying campfire. The flames
    shrink and gutter in steps, the embers lose their glow one batch at a time, ash falls, one last curl of smoke
    rises and thins.
- **Intentional exceptions:** the catch core appears instantly (it is an impact); the wind-up ends on the cue with
  no fade (`venom_hold` carries its last drawing across); the turn stops at the cue.
- **Mechanics, unchanged:** a Poison packet then a Fire packet, each number with its own accent (`-3` Poison green
  on the cue, `-9` Fire red 3 frames later); sound, number and badge on the cue frame.

## Beat table

Authored seconds on the release clock; hit cue **0.6 s** (decided by the orchestrator in round 2; was 0.3). The cue
is f35 at 1x and f23 at 1.5x (`timing.json`; one frame is the first-drawn wait). "Visible" is the stage-crop check:
the layer's box (size x fit factor, centred on the body) stays inside the 1920x1080 stage; the largest on the treant
is the 380 box x 1.63 = 620 px at the body's middle.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.00-0.60 | Wind-up: the venom (ashen f0-f4: streaks far from the body, a swirl, a forming coal) fades in from nothing over 0.4 s, brightens (glow 0.1 to 1.2) and turns about the body's centre ever faster, 150 degrees in all | `ritual` | yes, 380 box | `rotate` a rising quarter sine (amplitude -150, period 2.4, phase 270): at rest at the start, fastest on the cue, at most 6.5 degrees a frame |
| 0.00-0.9 | Stage gloom 0.15 | stage `gloom` | n/a | in 0.4, out 0.3 after the cue |
| 0.60 | Catch, Poison packet: sound, `-3`, Vulnerable badge, accent: a small bright core of ashen f6 (the swirl with fire through it) | `catch` | yes, 120 box | opens at once, oversized 35% and settling, 0.06 s, fade 0.1 |
| 0.60 | The last wind-up drawing (f4, green venom round the coal) held 0.05 s and faded 0.2 s, so the green gives way as the fire grows inside it | `venom_hold` | yes, 380 box | opacity 0.75 |
| 0.65 | The fire spreads out from the core | `catch_b` | yes, 165 box | fades in 0.04, fade 0.1 |
| 0.70 | The fire at its widest, about the body's width, then it fades into the campfire | `catch_c` | yes, 190 box | opacity 0.7, fade 0.15 |
| 0.60 | Venom drops thrown with the first packet | `drops` | emitter | 8 particles |
| 0.65 | Flare, Fire packet: second sound, `-9`, a flare-up: the same drawing, hotter | `flare` | yes, 200 box | 0.06 s, glow 1.8, opacity 0.6, fade 0.15 |
| 0.65 | Embers and flame tongues thrown by the fire | `settle` | emitter | 14 particles |
| 0.65-1.45 | Ending 1: the campfire takes at the feet | `fire_a` | yes, 220 box | fades in 0.15, loops 8 fps, breathes, glow 1.0 to 0.5 |
| 0.75-1.95 | The burst's smoke and ash, behind the body (ashen f7-f11) | `decay` | yes, 300 box | 5 fps, opacity 0.55, sort ground |
| 0.65-1.4 | Bright coals float up | `embers_a` | emitter | glow 1.2 |
| 1.05-1.6 | Ending 2: smaller, dimmer fire | `fire_b` | yes, 170 box | glow 0.4, breathe 0.35 |
| 1.0-1.7 | The embers dim: fewer, half the glow | `embers_b` | emitter | glow 0.5 |
| 1.35-1.85 | Ending 3: low fire, flickering hard | `fire_c` | yes, 125 box | glow 0.15, breathe 0.5 |
| 1.35-1.95 | The last embers, dull | `embers_c` | emitter | glow 0, opacity 0.7 |
| 1.0-2.8 | Ash flakes fall (the ashen sheet's grey shard) | `ash` | emitter | grey, 12 a second for 0.6 s |
| 0.9-2.4 | Thin smoke leaves the fire, behind the body | `smoke` | emitter | sort ground |
| 1.6-2.1 | Ending 4, the gutter: a few low flames, deep fast breathe, no glow | `fire_d` | yes, 85 box | fade 0.3 |
| 1.8-3.0 | The last curl of smoke rises from the dead fire, large, and thins | `curl` | emitter | one particle, scale x2 |

The beat clears at 3.0 s (2.4 s after the cue; the censer pilot runs about 3.5 s). The fire and burst stills are on
the release clock, offset from the cue, because an `at: hit` still is capped at 0.75 of the body
(`TargetBody.TailMaxOfBody`).

## What changed, round 2 (orchestrator's check of the round 1 preview)

Defects named: (1) wind-up opens at full opacity and clutters; (2) the catch is an explosion with the body filled
pale green; the green should turn to fire from the core, sized to the body; (3) the body hidden for about 0.35 s;
(4) the ending is not the strongest part.

- **Wind-up is 0.6 s** and builds: fade-in 0.4 s from faint wisps, glow rising, the turn accelerating to the cue.
  The cast sound is re-cut to 0.6 s (`tools/make_spell_cast_cues.py`, `MeleeSoundOverrideTests` 0.60). The source
  clip's build peak is at 0.54 s, so the cut carries 0.06 s of silence before it and the peak lands on the cue.
  `audio_levels.json` was not touched (other work is uncommitted in it): the new clip measures -18.1 dB RMS, so its
  gain would move from -2.7 dB to +0.1 dB. Left for whoever commits that table.
- **The catch** is no longer one big burst. A small core opens on the cue (120 box), spreads (165), reaches the body's
  width (190), while the green vortex's last drawing fades under it. No catch layer is larger than 200.
- **The hit flash is a rim, globally** (`BodyLight`): `FlashInteriorShare` 0.22 to 0.08 and `FlashRimWidth` 0.022 to
  0.012 (shader default, material, constants, pins). In round 1 the committed material still carried 0.22, and the
  rim, 2.2% of the canvas height, reached across a whole bush-like canopy. Checked on a plain melee hit (Shawn hit by
  the treant, frames 109-113): a thin pale rim, the interior unchanged. The Censer's cue has no hit flash (a status
  cast), so the Censer was not a test of this; a Burn tick was not recaptured.
- **The body stays in view:** the burst's stills are thinner, the smoke and ash sprite and the smoke emitter are on
  the ground sort (behind the body), and the swirl and core cover the torso for about 0.2 s, not 0.35.
- **The ending is the longest part and has steps:** four fires stepping down in size, glow and flicker; three ember
  batches with glow 1.2, 0.5, 0; grey ash; a curl of smoke 2.7 times its old size.
- Tests: `AshenReckoningPresentationTests` (4), `SpellLayerHandoffTests` (cue now a parameter), `BodyLightTests`,
  `MeleeSoundOverrideTests`.

## Playback, as seen (treant 1x, run `20261005T125314Z_5888648fb1a9`; 60 fps; frames looked at 1920x1080 around the target, and the preview GIF)

- **f0-f34 wind-up.** f4 a few faint green wisps; f10 streaks across the trunk; f16-f22 they wind into a swirl round
  the body; f28-f33 the swirl tightens round a forming coal. It builds and turns faster toward the cue: a vortex
  drawn inward.
- **f35 cue.** `-3` (green) and the badge; a small orange core opens in the green swirl over the coal; the treant's
  hit flash is a pale outline now, the face and body visible inside it.
- **f36-f46.** The fire spreads from the core through the green coil: orange veins widen across the coal and the
  coil (f38 to f44), the green still on the outside. At f38 the flare and `-9` (f38). The torso is behind the swirl,
  coal and fire f35-f46 (about 0.18 s); the face and canopy stay visible. f44 the campfire is up at the feet.
- **f46-f66.** The burst thins to smoke behind the body; the campfire holds, full width, at f58-f66.
- **f74-f110, the fire steps down.** f74 full, f82 smaller and flickering, f90 low, f98 a few low flames, f106 two
  sparks; out by f110. Embers float f58-f90, dim after f80. Ash flakes are not readable at 1x (a few grey specks).
- **f110-f160 the curl.** One large S curl of smoke, light brown, rises over the trunk (f110-f146) and thins out by
  f160; the treant stands in the same place (its own attack move, f110).
- **f179 freed, clear at 3.0 s.**
- **Rat 1x.** The same beats. The swirl covers the rat f35-f42; the fire at the feet is small (f56-f90); the rat is
  killed (12 damage) and leaves at about f125; the curl of smoke is the only thing on the empty stage at f130-f150.
- **1.5x treant.** Same shape, 0.4 s wind-up, cue f23, freed f119.

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass | the only moving layer is `ritual`'s turn: at most 6.5 degrees a frame, smooth, 0 at the cue (formula of the layer, per frame; no position field is authored). The first frame is faint (fade-in), so nothing pops. `ritual`, `venom_hold` and the catch stills share the body centre. The stopped turn at the cue is the impact, an intentional exception |
| Cue on one frame | pass | `timing.json` `hitFrame` 35 at 1x, 23 at 1.5x; `-3` and the badge on that frame, `-9` three frames after; `SpellLayerHandoffTests` (3, cue 0.6), `AshenReckoningTests`, `AshenReckoningPresentationTests` (4), `MeleeSoundOverrideTests` (cast clip 0.60 s): green |
| Nothing marked visible leaves the stage | pass | sizes in the beat table; largest 620 px on the treant at its middle; the stills show no clipping |

Advisory (reported only):

- **Pop-in:** none now (fade-in 0.4; the first drawn frame is faint).
- **Frozen holds:** none; the fires loop and breathe.
- **Body coverage at the hit:** the swirl, coal and fire cover the torso for about 0.18 s on the treant and about
  0.2 s on the rat; face, canopy and outline stay visible on the treant.
- **Flash guard:** the core, the flare 3 frames later and the target's hit flash (now a rim) fall in one second:
  counted as one large flash; not measured with the rendered-frame tool.

Gate: `run_tests_parallel.ps1 -Changed -ChangedPaths <12 files> -BuildContent` promoted itself to a full run (the
changed `tools/make_spell_cast_cues.py`): EditMode 6240 of 6243 passed (3 skipped), PlayMode 1955 of 2046 passed,
0 failed, 91 skipped. Round 1's gates failed because the disk was down to under 1 GB (Unity's PlayMode import
crashes: "Disk space low", then `Crash!!!`); the filmed runs of 1080p frames were the main user, so
`tools/gif.ps1 -TrimFrames` was run, and my own runs' frames deleted. The drive is still 100% full (5 GB free).

## Rubric (orchestrator's check is still due; this is the worker's score)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ~. The wind-up builds and accelerates, the core ignites and spreads, the flare, the
   ending in steps: all in order. The green does not change colour: the fire grows through and over it, so it
   reads as "the venom is catching", not as a recolour.
2. Weight and easing feel physical: ~. The wind-up accelerates; the catch is an instant core and a 0.1 s spread
   (a burner lighting) but still small; the fire steps down in size, glow and flicker.
3. One focal point at a time: ✔ in the wind-up and the ending; ~ at f35-f46 (core, coil and two numbers together).
4. The body stays readable at the hit: ~. The face, canopy and outline stay visible; the torso is behind the swirl,
   coal and fire for about 0.18 s (the effect is on the body by design). Not ✔ on the torso.
5. The ending decays, not cuts: ✔ for the fire (visible steps), the embers and the curl, which is clear and thins;
   ~ for the ash, which is not readable at 1x.
6. Distinct from and consistent with the approved spells: ~. Not played beside the Censer here. Both end in a fire
   at the feet and smoke. The fire here is the censer's cel loop; the swirl and burst are painted stand-ins.

Art, not scored: every piece is a stand-in (the painted ashen frames are the rejected style, REFERENCE.md).

## Art record (one row per piece)

| Piece | Accepted prompt | Source file | Recipe | Output | Attempts |
|---|---|---|---|---|---|
| Stand-ins, none accepted | n/a | `Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_atlas_v1.png` (frames f0-f11), `censer_flame_loop.png`, `censer_of_embers_particles` | `recipes/ashen_reckoning*.json` | `Resources/Spells/ashen_reckoning`, `censer_flame_loop` | 0 |

### Art needed for step 4

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`. Style block and attachments as in section 1 of the template.
**The key colour does not exist yet.** `tools/slice_spell_sheet.py` keys only on green (`green`, `green-soft`) and
luminance; the template also bans green in the subject, and the venom is green. Pieces 1 and 5 need the magenta key
`#FF00FF` (orchestrator's decision) and a `magenta` mode in the slicer: not built, to be built when art arrives.
Pieces 2-4 have no green and use the template as it is. The venom sheet in the stand-in blocking had to cover the
swirl, so a venom piece is the first thing wanted.

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

### Step 5: art inspection (orchestrator, 2026-10-07)

Delivered 2026-10-07 already keyed (RGBA, transparent), exact canvases and grids: `ashen_reckoning_venom_vortex.png`
2560x512 5x1, `ashen_reckoning_catch_cel.png` 2048x512 4x1, `ashen_reckoning_smoke_ash.png` 2048x512 4x1. Pieces 3 and
5 (campfire, drops) not made; both optional. Recipes take the alpha as is (no key) with an alpha floor of 40: each
cell has 750-8000 pixels at alpha 1-40, which are dark outline fringe plus sparse specks up to ~100 px outside the
drawing, not key spill (no magenta or green fringe).

| Piece | Style (flat cel, bold outline) | Reads as | Usable | For step 6 |
|---|---|---|---|---|
| Venom vortex | ✔ two greens and a pale hit, outlined drops | venom winding into a knot ✔ | yes | All five cells have the same radius (~100 px), so "drawn inward" is carried only by density. The engine supplies the pull: spin plus a scale-down over the wind-up. |
| Catch | ✔ red, orange, yellow core | core, then a ring of flame ✔ | yes, with two watch items | (a) Cell 1 sits off-centre: solid box x 151-262, y 260-360 against centred cells 2-4. Register it in the recipe. (b) Cells 2-3 are an upright, camera-facing hoop and cell 4 is a flat ellipse on the ground. Playing 3 then 4 in sequence may read as a perspective pop. If it does, end on cell 3 and hand off to the flame loop at the feet. Tongues lean outward, not in (asked for in); acceptable. |
| Smoke and ash | ✔ | S-curl, broken curl, three flakes, two flakes and a chip ✔ | yes | The grey is cool, not warm. Tint it in the engine if it clashes with the fire. |

Verdict: accepted for wiring; no revision prompt. The catch's hoop-to-ground change (b) is judged in motion at step 6.

## Step 6: art integrated and compared in context (2026-10-07)

Captures `tools/gif.ps1 spell ashen_reckoning -Vs treant -PreStatus Poison` (run `20261007T120009Z_2271e90b1699`), `-Vs rat`
(`20261007T120220Z_c5443b8abbe4`), `-Vs treant -Speed 1.5` (`20261007T120344Z_b1d8189eba42`). Copies beside this file:
`ashen_reckoning_step6_{treant_poison_1x,rat_poison_1x,treant_poison_1.5x}.gif` (untracked) and `*_preview.gif` (committed).
Timing unchanged: cue f35 at 1x, f23 at 1.5x, freed f179 / f119.

### Art record

| Piece | Source | Recipe | Output | Attempts |
|---|---|---|---|---|
| Venom vortex | `ashen_reckoning_venom_vortex.png` (step 4, keyed RGBA) | `recipes/ashen_reckoning_venom_vortex.json`: `alpha_floor` 40, cells baked 1.3x/1.18x/1.08x/1.0x/0.9x (the pull inward) | `Resources/Spells/ashen_reckoning_venom_vortex` f0-f4 | accepted at step 5, wired first try |
| Catch | `ashen_reckoning_catch_cel.png` | `recipes/ashen_reckoning_catch_cel.json`: `alpha_floor` 40, cell 1 `shift` [50, -54] onto the cell centre | `Resources/Spells/ashen_reckoning_catch_cel` f0-f3 | accepted at step 5, wired first try |
| Smoke and ash | `ashen_reckoning_smoke_ash.png` | `recipes/ashen_reckoning_smoke_ash.json`: `alpha_floor` 40 | `Resources/Spells/ashen_reckoning_smoke_ash` f0-f3 | accepted at step 5, wired first try |
| Campfire, venom drops | not delivered (optional) | kept: `censer_flame_loop`, `ashen_reckoning_particles` f0 | n/a | n/a |

The atlas folder `Spells/ashen_reckoning` (f0-f11) is no longer played; it stays because its recipe and the drift tests
still reference it. Tooling added to `tools/slice_spell_sheet.py` (documented in ART_PIPELINE, "Composing frames"):
recipe `alpha_floor`, sequence-step `shift`. A latent bug fixed in passing: two functions were both named
`feather_edges`, so any sequence step with a `scale` crashed; the ring one is now `feather_ring`.

### Beat table, as wired

| t | Beat | Layer | Carried by |
|---|---|---|---|
| 0.00-0.60 | wind-up | `ritual` | vortex cells 1-5, 460 box, turning, fade-in 0.4 |
| 0.60 | green gives way | `venom_hold` | vortex cell 5 |
| 0.60 | catch, core | `catch` | catch cell 1, 280 box, punch |
| 0.65 | spreads | `catch_b` | catch cell 2, 235 |
| 0.70 | widest | `catch_c` | catch cell 3, 250 |
| 0.65 | flare | `flare` | catch cell 3, 270, glow 1.8 |
| 0.75-1.75 | burst smoke behind the body | `decay` | smoke cells 1-2, 2 fps |
| 0.65-2.1 | campfire steps | `fire_a`-`fire_d` | censer cel flame loop (unchanged) |
| ending | embers | `embers_*` | censer particles (unchanged) |
| 1.0-2.8 | ash | `ash` | smoke cells 3-4, size 0.7-1.1, gravity -220 |
| 0.9-2.4 | smoke | `smoke` | smoke cells 1-2 |
| 1.8-3.0 | last curl | `curl` | smoke cell 1 at size 3.5-3.8 |

### Playback, as seen (treant 1x, frames at 1920x1080 around the target)

- **f4-f34 wind-up.** f4 a faint thin ring of ribbons; f10-f16 ribbons sweep round the trunk; f22 a loose green spiral;
  f28-f34 a dense knot over the torso, turning faster. Reads as venom drawn inward, no pop-in.
- **f35 cue.** `-3` and the badge; a small flame core opens in the middle of the green knot, the hit flash is a rim.
- **f36-f37** the core settles; **f38** the ring of flame opens round the knot, flare and `-9` land; **f40-f46** the
  ring at its widest, about the body's width, the green knot burns out inside it (f44 a few green flecks);
  campfire up at the feet from f40. **f48-f54** the ring is gone, torso clear.
- **f60-f100** campfire steps down (f62 full, f76 smaller, f90 low, f100 embers); thin smoke behind the trunk; a few
  small grey flakes near the head, readable but small.
- **f110-f160** the grey S-curl rises over the trunk (the treant's own attack move is on screen), thins by f160; clear by f178.
- **Rat 1x.** Same beats. The vortex covers the whole rat f20-f36; the ring is wider than the rat; the rat fades out
  from about f125 and the curl is the last thing on the stage at f160.
- **1.5x treant.** Same shape, cue f23, ring f26-f30, freed f119.

**Catch cell 3 to 4 (hoop to ground ellipse):** not played. In the contact sheet cell 3 is an upright, camera-facing
ring and cell 4 is a flat ellipse; in sequence that is a view change in one frame. The catch ends on cell 3 and
hands off to the flame loop at the feet, as the step 5 note allowed. Not filmed with cell 4 in: this is a call from
the stills, not from motion. The ring's upright tongues reach the treant's head for about 4 frames (f38-f42).

**Vortex scale-down:** `SpellLayer` has no scale-over-life (`endScale` is an emitter field), so the pull is baked into
the cells by the recipe. No code change.

**Smoke grey:** the cool grey sits fine beside the fire against the brown trunk; the existing warm tints on `smoke` and
`curl` stay, no new tint.

### Checks

| Check | Result | Evidence |
|---|---|---|
| No transform jump | pass | only `ritual` turns, at most 6.5 deg a frame as in round 2; the catch cells share the body centre (cell 1 registered); cell 4 not played |
| Cue on one frame | pass | `timing.json` hitFrame 35 / 23; `AshenReckoningTests`, `AshenReckoningPresentationTests` green |
| Nothing marked visible leaves the stage | pass | largest box 460 (the vortex), inside the 1080 crop at the body's middle; frames show no clipping |

`tools/spell_feel.ps1 ashen_reckoning` (reports in `docs/captures/spell_feel/ashen_reckoning_vs_{rat,treant}`), two WARNs:

- **treant, residue tail** (above 25% of peak 0.75 s after contact, limit 0.5 s): the campfire steps down over about
  1.1 s and the curl is the last thing on stage, by design (round 2 asked for a long ending). Not changed.
- **rat, contact desync** (biggest rise at f19, cue f36): the activity measure climbs from f11 to f20 (1.4 to 2.6) as
  the dense vortex fades in, then plateaus (2.6-3.1) to the catch. The vortex is a solid green mass, so the wind-up
  carries about 85% of the catch's activity and the cue stands out by colour (green to fire) more than by energy.
  One try, `ritual` opacity 0.85 to 0.7, left every number unchanged, so it was reverted. Open: a lighter vortex
  (fewer dense cells) would separate the cue; that is new art, not tuning.

### Rubric (worker's score)

1. Each beat reads, in order: ✔. Venom winds in, the core ignites inside it, the ring spreads, the green burns out, the
   campfire steps down, ash and smoke, the curl. The green is still overrun rather than recoloured, but the ring now
   reads as the venom catching.
2. Weight and easing feel physical: ✔. The knot tightens and speeds; core, spread and widest ring take 0.1 s; the
   flame steps are clear.
3. One focal point at a time: ~. f35-f46 holds the knot, the ring and two numbers.
4. The body stays readable at the hit: ~. The knot covers the torso f20-f46; the ring covers torso and part of the
   head f38-f46 (about 0.15 s); the face is clear in the wind-up.
5. The ending decays, not cuts: ✔ for the fire steps and the S-curl; ~ the ash is small flakes near the head.
6. Distinct from and consistent with the Censer of Embers (compared with its 1x treant GIF): ✔. Censer: a hanging
   brazier, thrown embers, an orange burn tint on the body. Ashen: a green vortex, a ring of flame, no object. They
   share the cel campfire loop and the ember sprites, and both end on smoke.

What changed: the art swapped in (vortex, catch, smoke, ash, curl); burst smoke moved to the smoke sheet; ring sizes cut
from 330/290/310/340 to 280/235/250/270 after the first capture (the ring was wider than the treant and hid its face);
ash flakes spread and reduced after a clump covered the face. Presentation tests updated.

### Orchestrator's check of step 6 (2026-10-07, full 1x GIFs, treant and rat; frames at 60 fps)

Passes to the owner. The art reads as the fight's cel style. The order reads: vortex (f12-f34, tightening), core
(f36), ring of flame round the green (f38-f46), campfire at the feet (f46-f72), curl (f120-f150).
Reservations for the owner to weigh:
1. Treant f38-f46: the flame ring covers the face and the upper torso for about 0.15 s.
2. Cue, f36-f38: the roots and legs fill pale green. It reads as a fill on the lower body, not the rim contract E10
   asks for. On the rat it is milder.
3. Rat: the vortex is fully dense by f20, so the cue reads by colour change more than by energy (spell_feel WARN).
4. Rat f132-f170: the rat takes its turn and dashes at Shawn while the curl still hangs over the empty spot where it
   stood. The ~2.4 s ending runs into the next action.

## Open owner questions

Each asked once.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | Wind-up length: 0.3 s (cast sound cut for it) or 0.5-0.6 s | trial 1, step 2 | decided by the orchestrator in round 2: 0.6 s, cast sound re-cut; the owner can reverse |
| 2 | Ending length, 2.4 s after the cue | trial 1, step 2 | decided by the orchestrator: ~2.4 s is OK; the owner can reverse |
| 3 | Key colour for the green venom art | trial 1, step 2 | decided by the orchestrator: magenta `#FF00FF`; the slicer has no magenta key yet (needed at step 4) |

## Round log

| Round | What changed | Who caught it | Rubric result |
|---|---|---|---|
| 2026-10-04 (before the workflow) | painted catch, tinted; cue and two packets; no ending | worker | not scored; owner: "best" of the ten, "needs an ending" |
| Trial 1, block 1 | fires as `at: hit` stills: every fire the same size | worker, from a pixel measure of the flame width per frame | n/a |
| Trial 1, block 2 | fires moved to the release clock; the flame band at the body's middle read as a shelf of fire across the body | worker, from the stills | n/a |
| Trial 1, block 3 | catch is ashen f6 at body size: it read as an explosion over the whole crop | worker, from the stills | n/a |
| Trial 1, block 4 (first review) | f6 at 210 box; the ending as a fire, a decay, a curl | worker | worker's score only |
| Round 2 | orchestrator's playback check failed it: full-opacity pop-in at f0; the catch an explosion and the body filled pale green (the committed material still had the 0.22 interior, and the 2.2% rim); the body hidden for 0.35 s; the ending not the strongest. Changed: 0.6 s wind-up that builds and accelerates, a core that spreads to the body's width, the global hit-flash rim, burst and smoke behind the body, a four-step ending with dimming embers, ash and a large curl | orchestrator | see Rubric |
| Step 6 | step 4 art wired into recipes and layers; ring sizes cut after the first capture (wider than the body); ash flakes spread | worker (own frames) | see Step 6 |
| Owner 2026-10-07 | owner 2026-10-07: smoke layers removed (decay, smoke, curl); ash kept. Timings not redistributed. `timing.json` now `clearedSeconds` 2.800 (the ash emitter is the last thing; was 3.0 with the curl); capture `ashen_reckoning_step6b_treant_poison_1x*.gif` | owner | not rescored |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | round 1: 3 block revisions and one test fix; round 2: 4 captures to settle the flash (material default 0.22, rim 0.022) and the coverage, one test fix (tick 0 of a fade-in) |
| Generation attempts per art piece | 1 each for the three delivered sheets (accepted at step 5); campfire and drops not made |
| Active work time / waiting time | round 1 about 80 min active, 20 waiting; round 2 about 70 min active, 35 waiting (captures, two gates, a disk-space crash). Estimates; no clock |
| Owner handling time | none yet |
| Owner-found defects the binding checks should have caught (target 0) | none from the owner. The orchestrator's check found the pop-in (a binding check does not test it: advisory) and the filled-silhouette flash (no check) |
| Final quality against the reference pack | not judged |
| Captures taken | step 6: 3 filmed, a first treant try, 2 spell_feel runs; round 1: 6; round 2: 7 treant 1x (4 while tuning), rat 1x, treant 1.5x, a censer, a melee attack |

## Reviewed snapshot

- Step 6 art integration, gate green (EditMode 6242/6245, 3 skipped; PlayMode 1955/2046, 91 skipped, 0 failed): `7a3ea8ef`.
- Commit: round 1 `6b5a81fa`, `cdb869ac`; round 2 code, content, audio, flash `9fd78fca` (NOTES in the commit after it).
- Capture run ids: treant 1x `20261005T125314Z_5888648fb1a9`, rat 1x `20261005T125429Z_cde8c2fab1d8`, treant 1.5x
  `20261005T125534Z_79058fbf7da0` (frames deleted by me to free disk).
- Rubric: see above.
- Still weak: the green is overrun rather than recoloured; the torso is covered for about 0.18 s; the ash is
  barely readable at 1x; the swirl, burst and smoke are painted stand-ins beside the cel fire; the drive is full.
