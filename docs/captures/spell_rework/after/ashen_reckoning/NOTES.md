# Ashen Reckoning: state record

Trial 1 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), step 2 "block with existing assets".
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. No verdict: the owner judges.
Captures 2026-10-05 with `tools/gif.ps1 spell ashen_reckoning -Vs rat|treant [-Speed 1.5] -PreStatus Poison`.
Full GIFs sit next to this file, untracked (`ashen_reckoning_trial_*.gif`, 13-19 MB); the phone previews are
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
  - wind-up (`ritual`): smoke drawn into a vortex, spiralling inward and speeding up;
  - catch (`catch`, `venom_hold`): a gas burner lighting, a quick "whoomph" swell that then settles;
  - second packet (`flare`): a flare-up as fuel is added;
  - ending (`fire_a`, `fire_mid`, `fire_low`, `decay`, `embers`, `embers_late`, `ash`, `smoke`, `curl`): a dying
    campfire. The flames shrink and gutter, the embers lose their glow, ash falls, one last curl of smoke rises and
    thins.
- **Intentional exceptions:** the catch appears instantly (it is an impact); the wind-up ends on the cue with no
  fade (the `venom_hold` layer carries its last drawing across).
- **Mechanics, unchanged:** a Poison packet then a Fire packet, each number with its own accent (`-3` Poison green on
  the cue, `-9` Fire red 3 frames later); sound, number and badge on the cue frame; `hitCueSeconds` 0.3.

## Beat table

Authored seconds on the release clock; hit cue 0.3 s. The cue is f18 at 1x and f12 at 1.5x (`timing.json`).
"Visible" is the stage-crop check: the layer's box (size x fit factor, centred on the body) stays inside the 1920x1080
stage; for the treant the largest box is 620 px square at the body's middle.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.00-0.30 | Wind-up: the venom (ashen f0-f4: streams, swirl, a forming coal) turns on the body's centre ever faster, 120 degrees in all; glow 0.3 to 1.0 | `ritual` | yes, 380 box | `rotate` a rising quarter sine (amplitude -120, period 1.2, phase 270): at rest at the start, fastest on the cue, 0.5 to 10.5 degrees a frame |
| 0.00-0.55 | Stage gloom 0.15 | stage `gloom` | n/a | fades in 0.25, out 0.3 after the cue |
| 0.30 | Catch, Poison packet: sound, `-3`, Vulnerable badge, accent: ashen f6, the venom swirl with fire through it | `catch` | yes, 210 box | opens at once, oversized 35% and settling over the first fifth of its 0.15 s life, held 0.15 s, fades 0.25 s |
| 0.30 | The last wind-up drawing (f4) held and faded under the catch, so the green goes out as the fire comes in | `venom_hold` | yes, 380 box | 0.06 s hold, fade 0.25 |
| 0.30 | Venom drops thrown with the first packet | `drops` | emitter | 8 particles |
| 0.35 | Flare, Fire packet: second sound, `-9`, flare-up: the same drawing larger and hotter | `flare` | yes, 290 box | 0.08 s, glow 1.8, fade 0.2 |
| 0.35 | Embers, flame tongues thrown by the fire | `settle` | emitter | 14 particles |
| 0.35-1.15 | The campfire takes at the feet | `fire_a` | yes, 220 box | fades in 0.15 s, loops 8 fps, breathes, glow 1.0 to 0.4 |
| 0.45-1.7 | The burst dissolves to smoke and ash (ashen f7-f11) | `decay` | yes, 380 box | 5 fps, opacity 0.75 |
| 0.45-1.4 | Bright coals float up | `embers` | emitter | glow 1.0 |
| 0.85-1.6 | Flames smaller and dimmer | `fire_mid` | yes, 160 box | glow 0.4 to 0.1, breathe alpha 0.4 |
| 0.9-1.7 | The embers lose their glow | `embers_late` | emitter | glow 0.2, opacity 0.8 |
| 0.9-2.3 | Grey ash falls; thin smoke leaves the fire | `ash`, `smoke` | emitter | |
| 1.3-1.95 | The gutter: a last small flame, deep fast breathe, glow 0.1 | `fire_low` | yes, 110 box | fade 0.3 |
| 1.5-2.7 | The last curl of smoke rises from the dead fire and thins | `curl` | emitter | one particle, scale x2.2 |

The beat clears at 2.7 s (about 2.4 s after the cue; the censer pilot ran about 3.5 s). The fire stills are authored
on the release clock, offset from the cue, because an `at: hit` still is capped at 0.75 of the body
(`TargetBody.TailMaxOfBody`). The first blocking of this session used `at: hit` and every fire came out the same
size whatever `size` said.

## What changed from the 2026-10-04 version

- The painted teal "catch" still and its green tint are gone (the tint pushed the flame salmon; untinted the core
  is cyan). The catch is now ashen f6, the swirl of green venom with fire through it; the flare reuses it.
- The wind-up turns accelerating (new use of `rotate` with `phase` 270) and brightens (`glowCurve`), with stage gloom.
- A real ending: the campfire at the feet steps down in size and glow, the embers lose their glow, ash, smoke and one
  last curl; the old version cut after 0.6 s.
- Cue and sounds are unchanged (the cast clip is cut for 0.3 s).
- `AshenReckoningPresentationTests` re-pinned: catch and flare layers on the release clock at the cue and 3 frames
  after it, no tint, the turn's phase and period, the ending's order (sizes and glow step down, the curl ends last).

## Playback, as seen (treant 1x, run `20261005T11...`, 60 fps; frames looked at 1920x1080 around the target)

- **f0-f17 wind-up.** f2-f8 streaks of green venom lash round the trunk; f10-f12 they wind into a swirl round the
  body; f14-f17 the swirl tightens round a forming coal (a lava ball). It turns faster toward the cue and brightens.
  Reads as a vortex drawn inward; the acceleration shows in the streaks (f2 to f12 turn about 90 degrees) and the
  swirl frames carry the inward pull.
- **f18 cue.** The burst opens at once: green streaks with orange veins burn through them, over the coal. `-3`
  (green) is on f18; the typed hit flash lights the whole treant pale green at the same time (engine, not authored).
  The Vulnerable badge appears on the status strip.
- **f21 flare and `-9`.** Three frames later the same drawing flares larger and the red `-9` appears.
- **f18-f27.** The burst covers most of the treant's body for about 0.15 s; the face is visible through gaps from
  f22. f27 the campfire is up at the feet.
- **f30-f48.** The burst thins into smoke and ash over the body; the fire at the feet holds.
- **f48-f108.** The fire steps down (f56 wide, f72-f80 about half, f92 a few low flames, f100 two tongues) and is
  out by f108. Faint embers rise f40-f64; ash specks are not visible; a thin grey wisp climbs the trunk f92-f132.
- **f132-f162.** Nothing but the treant, which is standing the whole time (its own attack move f96-f132).
- **Rat 1x.** The same beats. The swirl hides the rat f18-f30; the fire at the feet is small (f40-f96); the rat dies
  of the 12 damage and leaves at about f110, and the last curl of smoke is the only thing on the empty stage at f128.
- **1.5x treant.** Same shape, 0.2 s wind-up (f0-f11), cue f12, clear at f108.

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass | the only moving layer is `ritual`'s turn: 0.5, 1.4, 2.3 ... 10.5 degrees a frame, ends at 0 degrees on the cue (formula of the layer, checked per frame; no position field is authored). Handoff: `ritual`, `venom_hold` and `catch` share the body centre; `venom_hold` and the last wind-up drawing are one size (380, 0 degrees); `catch` is smaller (210) by design, an impact. The stopped turn at the cue (10.5 degrees a frame to 0) is the impact, an intentional exception |
| Cue on one frame | pass | `timing.json` `hitFrame` 18 at 1x, 12 at 1.5x; `-3` and the badge on that frame, `-9` three frames after (`StageHitFlash.StaggerSeconds`); `SpellLayerHandoffTests` (3), `AshenReckoningTests`, `AshenReckoningPresentationTests` (4), `MeleeSoundOverrideTests`: green |
| Nothing marked visible leaves the stage | pass | box sizes in the beat table; largest 620 px on the treant (centred at its middle, y 210-830 of 1080); frames at f18-f27 show no clipping at the left, right or top edge |

Advisory (reported only):

- **Pop-in:** `ritual` opens at full opacity 0.85 on f0 (no `fadeIn`).
- **Frozen holds:** none; the fire loops and breathes. The ash specks are not visible at 1x.
- **Body coverage at the hit:** the burst covers most of the treant for about 0.15 s (f18-f27) and most of the rat for
  about 0.2 s (f18-f30).
- **Flash guard:** the burst's bright frame, the flare 3 frames later and the target's typed hit flash fall in one
  second; counted as one large flash. Not measured with the rendered-frame tool.

## Rubric (orchestrator's check is still due; this is the worker's score)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ~. Wind-up, catch, flare and ending are each visible and in order; the catch's
   green-to-fire reads only because the green swirl has orange veins through it; the green does not visibly change
   colour, it is overrun.
2. Weight and easing feel physical: ~. The wind-up accelerates, but only 0.3 s of it; the catch is an instant swell
   with no "burner" build; the campfire shrink is real but slow and quiet.
3. One focal point at a time: ✔ in the wind-up and the ending; ~ at f18-f27 (swirl, burst and two numbers at once).
4. The body stays readable at the hit: ✘ for about 0.15 s on the treant and 0.2 s on the rat (see advisory); the
   body is readable from f27.
5. The ending decays, not cuts: ~. It decays (fire steps down, smoke, a curl) in about 2.4 s, and there is a last
   thing; the gutter, the ash and the curl are faint at 1x and the ending is the quiet part of the cast.
6. Distinct from and consistent with the approved spells: ~. Not played beside the Censer here. Both end in a fire
   at the feet and smoke; the stand-in decay is painterly and the Censer's fire is cel, so they are not yet
   consistent.

Art, not scored: every piece is a stand-in (the painted ashen frames are the rejected style, REFERENCE.md).

## Art record (one row per piece)

| Piece | Accepted prompt | Source file | Recipe | Output | Attempts |
|---|---|---|---|---|---|
| Stand-ins, none accepted | n/a | `Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_atlas_v1.png` (frames f0-f11), `censer_flame_loop.png`, `censer_of_embers_particles` | `recipes/ashen_reckoning*.json` | `Resources/Spells/ashen_reckoning`, `censer_flame_loop` | 0 |

### Art needed for step 4

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`. Style block and attachments as in section 1 of the template.
A conflict to settle first: the template bans green in the subject, and the venom is green. Pieces 1 and 5 need a
green subject, so they need another key colour (magenta `#FF00FF`) and the recipe's key to support it; I did not
check that `slice_spell_sheet.py` takes a non-green key. Pieces 2-4 have no green and use the template as it is.

1. **Venom vortex sheet** (the wind-up; replaces f0-f4)
```
Piece: Ashen Reckoning: the target's own venom winding inward
Beat it serves: wind-up, 0.00-0.30 s (ritual)
Filename: ashen_reckoning_venom_vortex.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_venom_vortex.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: centre of every cell (50%, 50%), the vortex's axis
Size in play: about the width of the target's body
Drawing: flat cel ribbons of toxic venom, bold dark outline, two green tones and a pale hit; cell 1 wide lashing
  streaks far from the axis, cell 2 the ribbons curving in, cell 3 a tight spiral, cell 4 a dense knot round an
  empty centre (the coal is drawn elsewhere). A few drops, outlined.
Verb: ignite (the build to it): smoke drawn into a vortex, faster as it closes.
Must read as: venom being drawn inward, speeding up.
Must NOT read as: a magic circle or rune ring; a cyclone of wind; fire.
Not in the picture: the coal, any orange, the target, smoke, glow.
```
(on solid `#FF00FF`, no magenta or pink in the subject; the engine turns the sheet.)

2. **Catch burst sheet** (the signature beat, replaces ashen f6)
```
Piece: Ashen Reckoning: the venom catches fire
Beat it serves: catch, 0.30 s (catch) and flare 0.35 s (flare)
Filename: ashen_reckoning_catch_cel.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_catch_cel.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: centre of every cell (50%, 50%)
Size in play: the width of the target's body; the burst does not cover its face for more than 0.1 s
Drawing: flat cel flame only, bold outline, deep red outside, orange, a small yellow core. Cell 1: a ring of low
  flame licking round an empty centre. Cell 2: the ring swelling, tongues up and out, a single bright core.
  Cell 3: the largest, tongues leaning in from the ring, centre still empty. Cell 4: the ring settled to a
  waist-high band of tongues. No venom in it (the venom sheet is drawn separately, in the same ring shape).
Verb: ignite: a gas burner lighting, a quick whoomph then it settles.
Must read as: the ring of poison catching fire.
Must NOT read as: a fireball or an explosion thrown at the target; a magic circle; cyan or teal fire; a star burst.
Not in the picture: green, venom, coal, rocks, sparks, smoke, glow.
```

3. **Campfire loop** (the fire at the feet; the Censer's flame loop does this today and is cel; an Ashen-own
   version is optional). Dying-fire variant: 4 cells in a 4x1 of 768x384, a waist-low band; cell 1 full, cell 2 and 3
   smaller tongues, cell 4 a few low flames on a coal line. Same pivot rule (coal line at 12.5% from the bottom,
   centred). Must NOT read as: a bonfire taller than the body, a torch.

4. **Smoke curl and ash sheet** (the last curl, the ash)
```
Piece: Ashen Reckoning: a curl of smoke and ash flakes
Beat it serves: ending, 1.5-2.7 s (curl, smoke, ash)
Filename: ashen_reckoning_smoke_ash.png
Delivery path: Assets/_Project/Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_smoke_ash.png
Canvas: 2048x512   Grid: 4x1 of 512x512
Pivot: centre of every cell
Size in play: the curl about a third of the body's height
Drawing: flat cel, bold outline, warm grey in two tones with a pale hit. Cell 1 one S-shaped curl of smoke, thick
  at the base; cell 2 a thinner broken curl; cell 3 three flakes of ash, angular, spaced apart; cell 4 two flakes
  and a spark-less coal chip.
Verb: ignite (the end of it): a dying campfire's last smoke.
Must read as: the last curl of smoke and flakes of ash.
Must NOT read as: a cloud, a storm, a gas attack, soft painted vapour.
Not in the picture: flame, glow, the ground, colour other than grey.
```

5. **Venom drops** (optional): the drop sprite, outlined, two greens, on the magenta key, 2x2 of 256 for the
   `drops` emitter. Low priority: the engine already has one.

## Open owner questions

Each asked once.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | The wind-up is 0.3 s because the cast sound is cut to it. A longer wind-up (0.5-0.6 s) lets the turn read, but the cue moves, the cast sound is re-cut and the pins change (as the censer did for 1.0 s). Longer, or keep 0.3? | trial 1, step 2 | open |
| 2 | Ending length: 2.4 s after the cue today (the censer is about 3.5 s). Keep? | trial 1, step 2 | open |

## Round log

| Round | What changed | Who caught it | Rubric result |
|---|---|---|---|
| 2026-10-04 (before the workflow) | painted catch, tinted; cue and two packets; no ending | worker | not scored; owner: "best" of the ten, "needs an ending" |
| Trial 1, block 1 | fires as `at: hit` stills: every fire the same size | worker, from a pixel measure of the flame width per frame | n/a |
| Trial 1, block 2 | fires moved to the release clock; the flame band at the body's middle read as a shelf of fire across the body | worker, from the stills | n/a |
| Trial 1, block 3 | catch is ashen f6 at body size: it read as an explosion over the whole crop | worker, from the stills | n/a |
| Trial 1, block 4 | f6 at 210 box (about 55% of the body): reads as venom with fire through it; the ending (fire, decay, curl) as in the beat table | worker | see Rubric |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | 3 (blocks 2 to 4 above), plus one test-ordering fix (the ash outlived the curl) |
| Generation attempts per art piece | 0 (no art yet) |
| Active work time / waiting time | about 80 min active (reading 10, authoring and tests 25, captures and stills 40, notes); about 20 min waiting on captures and gates |
| Owner handling time | none yet |
| Owner-found defects the binding checks should have caught (target 0) | none yet; note the checks found none of the three block-1 to block-3 defects: they came from measuring and looking, not from the binding checks |
| Final quality against the reference pack | not judged |
| Captures taken | 6 (treant 1x four times, rat 1x, treant 1.5x); the rat 1x and treant 1.5x only on the final block |

## Reviewed snapshot

- Commit: `6b5a81fa` (skills.json, generated asset, stamp, presentation tests). Gate: `run_tests_parallel.ps1 -Changed -ChangedPaths ... -BuildContent` ran twice; both times the PlayMode Unitys crashed on memory (OutOfMemory, import worker), so 0 of 3 PlayMode shards reported. EditMode 4235 of 4243 passed; the 5 failures were `OutOfMemoryException` in unrelated classes (SkillEffectBehaviourTests, UiKitAspectPinTests, UiKitVisiblePadTests) and pass alone via `test.ps1`. Run alone and green: `AshenReckoning*`, `SpellLayerHandoffTests`, `SpellLayerRules*`, `SpellPolishFieldsTests`, `SpellVfxTests` (PlayMode 65), `SpellBooksTests` (PlayMode 31), `SpellPerformanceTests`. The focused gate is therefore not green as a whole: rerun it when memory is free.
- Capture run ids: treant 1x `20261005T11...` (the last run of that name), rat 1x and treant 1.5x runs after it; frames are deleted by the recorder after two runs.
- Rubric: see above.
- Still weak: the catch reads as venom overrun by fire, not as the green turning; the burst covers the body for
  about 0.15 s; the ending's gutter, ash and curl are faint; the fire and the smoke are in two styles (painted
  stand-in over the cel campfire); the wind-up is 0.3 s.
