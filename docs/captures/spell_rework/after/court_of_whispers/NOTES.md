# Court of Whispers: state record

Trial 2, spell 1 of the spell workflow (`docs/PROPOSAL_SPELL_WORKFLOW.md`), step 2 "block with existing assets", round 1.
Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3.
**Status: step 2 round 3 done by the worker (2026-10-07, see "Round 3"); step 3 (orchestrator's watch and rubric) is next. Nothing here is owner-reviewed.**
The 2026-10-03/04 pilot passes (one painted courtier, ring-and-core payoff) are superseded; their GIFs and reports are in
`previous_pass/`, untracked.

Captures 2026-10-07 with `tools/gif.ps1 spell court_of_whispers -Vs rat|treant [-Speed 1.5]` (full rack of three).
Phone previews are committed beside this file (`court_of_whispers_r4_*_preview.gif`); the full GIFs
(`court_of_whispers_r4_*.gif`, 12-18 MB) are untracked. Each capture's `timing.json` is `r4_*_timing.json`.
`tools/spell_feel.ps1` was not run as written: its capture step is `screenshot.ps1 -Runtime` (pops a Unity window), which
the brief rules out. `tools/spell_feel.py report` was run on the same 60 fps frames of the four `gif.ps1` captures
instead (same input format, whole 1080p frame); reports are in `spell_feel/`.

## Identity sheet

Locked by the orchestrator's plan; the owner confirms or edits it with the first GIF.

- **Verb:** whisper.
- **Signature beat:** the courtiers all bend in to the head at once and the Fear lands as a violet shiver on the
  body's outline.
- **Must read as:** "they are whispering to them" (the identity test of the pilot).
- **Must NOT read as:** a magic circle, ring or targeting reticle; a drain or ghost attack; a wall of purple hiding the
  enemy; a full-silhouette flash.
- **Motion reference per moving element:**
  - orbit (`orbit_back`, `orbit_front`): courtiers walking a slow circle round a throne, seen from a slant: the far
    side is higher, smaller and dimmer, the near side lower and full size; it slows into the lean rather than stopping;
  - `lean`: a person bending from the waist to speak into an ear, held a beat, then straightening;
  - smoke (`smoke_a`, `smoke_b`, `curl`): a snuffed candle's smoke, thick first, then thinner, then one last curl;
  - `price`: a candle flame guttering on Shawn.
- **Intentional exceptions:** the rim pulses cut in and out (a shiver is a flicker); the far arc hands over to the near
  arc with a size jump 130 to 190, covered by a 0.1 s crossfade; the lean is full strength on the cue, not before.
- **Mechanics, unchanged:** every non-boss enemy Feared 1 turn, bosses lose 2 places, Shawn Vulnerable 1 turn; sound,
  Fear badges and the first rim pulse on one frame; Shawn's badge waits 0.18 s (`casterCostSeconds`).

## Beat table

Authored seconds on the release clock; hit cue **0.70 s** (was 0.30; the cast riser is re-cut to 0.70 s,
`make_spell_cast_cues.py`, `MeleeSoundOverrideTests`). Cue = f41 at 1x (`timing.json`, one frame is the first-drawn
wait), f27 at 1.5x. "Visible" is the stage-crop check: layer box = size x fit factor, centred on the body.
Sprite layers a body: 3 (orbit_back, orbit_front, lean) x 3 bodies, plus `price` = 10 of the effects band's 12;
ground band 0.

| t | Beat | Layer | Must be visible in the stage crop | Easing / feel |
|---|---|---|---|---|
| 0.00-0.70 | Stage gloom 0.35 | stage `gloom` | n/a | in 0.4, holds to the cue, out 0.4 after it |
| 0.00-0.50 | Far arc: a courtier fades in small and dim and drifts left to right across the body | `orbit_back` | yes, 130 box | `fadeIn` 0.18, opacity 0.4, tint `#9A90C0`, drift -0.6 to +0.6 box widths, ease-out; fade 0.1 |
| 0.30-0.78 | Near arc: the same figure, mirrored, full size, comes back right to left and slows into the lean spot | `orbit_front` | yes, 190 box | `fadeIn` 0.1 (the hand-off), opacity 0.7, runs +0.6 to -0.35 on screen (a reverse layer mirrors its drift), ease-out |
| 0.58-1.14 | The painted leaning courtier crossfades in over the near arc, full on the cue, closest to the head at 0.78 s, then draws back out | `lean` | yes, 190 box | `fadeIn` 0.12, opacity 0.7, breathe, drift -0.35, -0.30 at 36%, -0.8; fade 0.25 |
| 0.70 | **Cue.** Impact sound, Fear badges on all enemies, violet rim shiver on every body: 4 pulses 0.07 s apart (f41, f45, f49, f53), the last held and faded 0.16 s | `vfx.bodyRim` (engine) | n/a | `#B58CFF`, peak 0.9; rim only, interior share unchanged |
| 0.70 | Whisper motes burst | `settle` | emitter | 8 particles |
| 0.88 | Shawn's price: a small violet wisp flickers on him, his Vulnerable badge shows | `price` | yes, 150 box | `fadeIn` 0.04, 0.35 s, breathe alpha 0.6 rate 2.5, no ring |
| 0.95-1.30 | The courtier comes apart: smoke curls rise where it stood | `smoke_a` | emitter | 18 a second for 0.35 s, rising, thinning |
| 1.25-1.55 | Less and smaller | `smoke_b` | emitter | 7 a second for 0.3 s |
| 1.55-2.65 | The last curl | `curl` | emitter | one particle, scale x1.6, life 1.1 s |

Visually clean at 1.3-1.4 s on 1x (measured below); `clearedSeconds` is 2.65 because the last curl's life runs on.

## Playback, as seen (1x rat and treant, run ids in `r4_*`; contact sheets by the worker; the orchestrator has not yet watched)

- **f0-f20 far arc.** Faint violet ghosts fade in over each body and slide right. On the rats they read as a dark smudge
  across the torso; on the treants as a faint lattice. It is dim by design and barely carries the "orbit".
- **f19-f26 hand-off.** The near arc fades in on the right of each body (f20) and is full by f26 at 190 box, mirrored,
  hood left. There is a size and brightness step at the hand-off, softened by the crossfade; it still reads as a figure
  appearing at the right more than as one coming round the body.
- **f26-f41 near arc to the lean.** The figure slides left across the front of each body and slows (f23 right of the
  body, f38 left). At f35-f41 the painted lean takes over; the drawing changes from a back view to the bent figure
  while the position matches.
- **f41 cue.** Badges under every body, violet outline pulses on each body (visible on both rats and treants at
  f43, f49, f55, f61; the canopy of a treant carries a thick pale-violet rim), the courtier at full strength at each
  head, the gloom starts to lift.
- **f41-f65 lean.** Hood at the head, the cupped hand at the hood. On a three-rack the robes (a long ragged train) cover
  the lower half of its body and reach the next body; the three courtiers read as one purple band from f23 to f58
  (about 0.6 s).
- **f65-f85 draw back.** The courtier slides left and fades. Smoke is small wisps above the heads (f85-f130); the curl is
  a few violet specks. The "comes apart into smoke" beat is weak: the courtier fades more than it dissolves.
- **Rat 1.5x, treant 1.5x:** the same shape in two thirds of the time, cue f27, clean at 0.85 s (rat), 0.92 s (treant).

## Orchestrator's step 3 check (2026-10-07, r4 full GIFs, rat and treant 1x): **fails, not shown to the owner**

Frames at 60 fps:
1. **No figures and no circling.** f26-f68 (~0.7 s): the three courtiers merge into one purple band over the lower
   half of the whole rack, on both rats and treants. A circling figure does not read at any frame.
2. **The payoff is missing.** The figures are anchored at the body's base. On the treant they lean in at waist
   height, about one body-height under the face. Nothing leans "in to the head".
3. **The ending is disconnected.** The courtiers fade at the feet (f68); grey specks then appear above the heads
   (f84-f104). This reads as two unrelated events, not as figures dissolving into smoke.
4. Works: the violet rim shiver on the cue (treant canopies, f56); the gloom; cue timing.

Round 3 direction (owner call 3 answered: one per enemy, smaller, at the head): anchor the lean at the head (top of the body), not the base. Make the
figures small enough that three of them on a rack stay three. The smoke starts where the figure is and rises
from its body.

## The two-layer orbit hand-off (plan section 2, E6 prototyped without E5): **fails on a full rack**

Round 1 authored the far arc as a ground-band layer (behind every figure) and the near arc in the effects band, as
the plan says. Evidence kept in `round1_ground_orbit/` (rat 1x preview GIF and two contact sheets):

1. **The ground band cannot hold a rack.** `FightHudSpec.SpellGroundRenderers` is 2 and a three-enemy cast needs 3
   (per-target layer x 3). `SpellPoolCapacityTests.NoSpellAsksForMoreGroundRenderersThanTheBandReserves` fails
   ("court_of_whispers wants 3") and in play the third enemy gets no far arc: `third_rat_has_no_far_arc_f6-f24.png`,
   f6-f18, the rightmost rat has nothing behind it while the other two do.
2. **A figure behind a body is mostly hidden by it.** The far arc is as wide as the body it passes behind, so on the
   rats only slivers show at the edges (f10-f19); it reads as a smudge at the feet (`first_rat_orbit_f4-f52.png`).
3. **The hand-off pops.** f19 to f22: a faint 170 box at the right becomes a 230 box at full opacity (f22).

Raising the ground pool to 6 would fix (1) only (the pool is scene-generated and shared with Cinderfault's ownership
rule, `FightHudSpec` comment) and not (2). This needs E5 body depth bands: cross-layer (Domain tree, Core runtime,
scene generation), so it is **not built here and goes to a senior with `Escalation: cross-layer`**. The committed
content moves the far arc to the effects band (over the body, 130 box, opacity 0.4) so the gate passes and the
motion can be watched; with it the orbit is a dim ghost sliding over the body. To restore the real thing: set
`orbit_back.sort` to `ground` once the band can hold 3 per cast, and raise its size and brightness (it is occluded).

## Checks

Binding (proposal contract 4):

| Check | Result | Evidence |
|---|---|---|
| No transform jump in the spell's own layers | pass with one explained step | drifts are continuous (ease-out, `orbit_back` ends at +0.6 where `orbit_front` begins, `orbit_front` ends at -0.35 where `lean` begins: `CourtOfWhispersTimingTests`). The one step is the size 130 to 190 at the hand-off, an intentional exception, crossfaded over 0.1 s. A reverse layer mirrors its drift: the authored x of `orbit_front` is the mirror of its path on screen |
| Cue on one frame | pass | `timing.json` `hitFrame` 41 (authored 0.7 s = f42 minus the first-drawn wait) at 1x, 27 at 1.5x; sound, Fear badges, the rim's first pulse on that frame; Shawn's badge at +0.18 s. `spell_feel` peak frame = hit frame (41 and 27) on all four |
| Nothing marked visible leaves the stage | pass | sizes in the beat table; the largest is 190 x the treant's fit factor on its body; the contact sheets show no clipping; the leftmost courtier at f50-f80 is inside the stage |

`spell_feel` on the `gif.ps1` frames: rat 1x no warnings (clean 1.28 s, residue 0.37 s), rat 1.5x no warnings (clean
0.85 s, residue 0.25 s). **Treant 1x and 1.5x WARN "contact desync"** (biggest rise after release at f59 vs cue f42; f35
vs f28). That is the fourth rim pulse (the last, held and faded) landing on a dark, courtier-covered rack: a pulse train
has several rises and the instrument takes the largest. The recorded hit frame is f41/f27 and the peak activity is on
it, so cue timing is fine. Explained, not changed.

Advisory:

- **Pop-in:** none on the layers (all fade in); the rim pulses cut in by design.
- **Frozen holds:** none; the lean breathes, the price breathes, the smoke moves.
- **Body coverage at the hit:** fails. The lean covers the lower half of each body and its train reaches the neighbour
  for about 0.6 s on rats and treants (f23-f58). See the rubric.
- **Flash guard:** four rim pulses in 0.2 s on a body are four flashes in a quarter second on the strict count; the
  pulses are thin rims at peak 0.9 and the interior share is 0.08, so the lit area is the outline. Not measured with the
  rendered-frame tool. Say so if the orchestrator wants it measured.

Gates: dotnet slices `CourtOfWhispersTimingTests` (6), `SpellLayerRulesTests` (91), `SpellPresentationCopyTests` (4),
`MeleeSoundOverrideTests` (18), `SpellPoolCapacityTests` (5), `SpellLayerHandoffTests` (3) green. The commit gate
(`run_tests_parallel.ps1 -Changed -BuildContent`) result is at the end of this file.

## Rubric (worker's score; the orchestrator's check is still due)

Motion, 1x and 1.5x:

1. Each beat reads, in order: ~. Gloom, near arc, lean, rim shiver and cue, smoke are in order; the far arc is too dim
   to read as part of a circle and the smoke is weak.
2. Weight and easing feel physical: ~. The near arc slows into the lean; the rim is a flicker; the drift is ease-out
   only, so the middle of an arc is slow where an orbit is fast.
3. One focal point at a time: ✘ at f23-f58 on a three-rack: three courtiers form one mass.
4. The body stays readable at the hit: ✘. Robes cover about half of each body for about 0.6 s.
5. The ending decays, not cuts: ~. The courtier fades, smoke thins in three steps; the smoke is too small to carry the
   ending.
6. Distinct from and consistent with the approved spells: ~. Not played beside the Censer or Ashen Reckoning. Fear's
   violet rim is the only rim colour so far and is distinct.

Art, not scored: every piece is a painted stand-in (the rejected style).

## Art record (one row per piece)

| Piece | Accepted prompt | Source file | Recipe | Output | Attempts |
|---|---|---|---|---|---|
| Stand-ins, none accepted | n/a | `Art/Sheets/Spells/Court of Whispers/court_of_whispers_courtier-v2.png` (lean), `courtier_back.png` (orbit), `court_of_whispers_atlas_v1.png` cells 13-16 (wisps) | `recipes/court_of_whispers_courtier.json`, `court_of_whispers_courtier_back.json` (new), `court_of_whispers_particles.json` | `Resources/Spells/court_of_whispers_courtier`, `..._courtier_back`, `..._particles` | 0 |

The ring sheet `Resources/Spells/court_price_pulse` and the old atlas frames `court_of_whispers/f0-f11` are no longer
referenced by the spell (the ring was the rejected payoff and price).

### Art needed for step 4

**Step 3 passed (owner, 2026-10-07): the round 3 motion is approved. Prompts updated for round 3:** the figures play
small (52-80 px boxes) at the head of dark rats and green treants. Each piece therefore needs a pale outline over
the dark one and a lighter violet body, with a silhouette that still reads at 60 px.

Prompt-ready per `docs/prompts/TEMPLATE_spell_art.md`; style block and attachments as in its section 1. No green in any
subject below (they are violet), so the green key `#00FF00` applies.

1. **Leaning courtier** (the payoff drawing; replaces `court_of_whispers_courtier-v2.png`)
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

2. **Courtier from behind** (the orbit; replaces `courtier_back.png`)
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

3. **Smoke and wisps** (the ending, the motes and Shawn's price flicker)
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
(Cells 1, 2 and 3 are picked by weight in the emitters; cell 4 is the `price` still. Engine-made on top: the rim
shiver, glow, breathing, fades.)

### Step 5: art inspection (orchestrator, 2026-10-07)

All three pieces delivered at the right canvas on solid green and accepted for wiring, no revision:
- **Lean:** a light violet figure with a pale lilac rim and a dark outline. It reads as whispering: the cupped hand at the
  empty hood, the other hand on the hip.
- **Back:** the same figure seen from behind, recognisably the same courtier.
- **Wisps:** curl, S-wisp, three motes and a guttering lick, in the robe's violet with a pale rim.

At play size these should carry on dark rats where the round 3 stand-in vanished; confirm that at step 6.

## Round 3 (2026-10-07): courtiers at the head, small, one per enemy

Owner's answer to Q3: one per enemy, smaller, leaning in at head height. Captures `court_of_whispers_r5_*`
(rat 1x, treant 1x, treant 1.5x; previews committed, full GIFs untracked; timings `r5_*_timing.json`). Cue f41 at 1x.

**What changed (all in `ContentData/skills.json`, no new field, no new art):**

- The six courtier layers (`orbit_back`, `orbit_front`, `lean`, `smoke_a`, `smoke_b`, `curl`) are `place: above`,
  `height: 0.3`: the existing "straight over the struck body, N body-heights over its middle" placement
  (`SpellFlight.AbovePoint`, Crownfall's `spike`). It reads the visible body, so it follows a rat and a treant alike;
  a non-travelling `above` layer hangs and is held under the stage top, as Censer's lantern is. 0.3 is the face on a
  treant and the neck on a rat (0.45, the top of the body, put the treant's courtier over the canopy and under the
  enemy plates; it is not kept).
- Sizes 130/190/190 to 52/70/80 (still `fit: target`).
- Head offset: `dx` 60 toward the rat's nape on the three sprites (authored sign follows the layer's facing: -60
  on the unmirrored far arc, +60 on the two reverse layers). `dx` is not fit-scaled. Enemies face left, so the
  courtier stands behind the head and leans in from the right (`lean` is now `facing: reverse`).
- Drift in own-box widths: far arc -1.1 to +1.1; near arc -1.5 to -0.8; lean -0.7, via -0.2 at 36%, to -0.3
  (it stays near the head while it dissolves). Hand-off points match (within 2 units, tested).
- Smoke emitters have `sourceDx` 15-19 and `sourceDy` -5 to 35 from the same head point, where the lean ends, so the
  smoke rises from the courtier.
- Far arc kept (a dim ghost at head height). It reads as a faint smudge at f8-f26 and does not hurt readability.

**Beat table updates:** the table above holds for timings. Sizes: far arc 52, near arc 70, lean 80. Anchors: head.

**Playback, as seen (60 fps, my own contact sheets: rat 1x, treant 1x, cue f41):**

1. **Merging band: fixed.** Three separate figures at every frame on both racks: f16-f40 (small, one at each head),
   f41-f64 (at the cue and the lean; on the treant rack the robes of two neighbours come within a few pixels at
   f41-f56 but do not overlap), and in the smoke f72-f120. On rats the figure of one rat sits between its head and
   the next rat's snout at f41-f56 (touches, no merge).
2. **Anchored at the base: fixed.** The lean is at the treant's face/upper trunk (f41, f56) and at the rat's
   head/nape. On rats it covers the neck and ear, not the face. Caveat: the courtier is a painted stand-in, a
   hooded robe, and on a treant at the cue (f41-f56) it covers the brow and part of the canopy beside the face; the eyes stay visible.
3. **Disconnected ending: fixed.** The lean fades from f64 where it stands; the smoke is born there at f66
   (hit +0.25 s) and rises from the same spot, f72-f120. One event, not two. The smoke is small (this art), and one
   curl hangs to about f130.

**Checks:** `CourtOfWhispersTimingTests` (6, updated to the new anchors and sizes), `SpellLayerRulesTests` (91),
`SpellPresentationCopyTests` (4), `SpellPoolCapacityTests` (5) pass. `tools/spell_feel.ps1 -Spell court_of_whispers`
(runtime capture): treant 1x no warnings (contact f41, visual f41, clean); **rat WARN contact desync**: the
biggest rise is f20 (authored hit cue f42). That is the near arc's dark courtier fading in at head height on the
light rats (f19-f26) outweighing the rim pulses. Cue timing is unchanged: the recorded hit frame is f41, sound,
badges and the first rim pulse on it. Explained, not changed. Flash guard not measured (unchanged: rim pulses).

**Rubric (worker's score):** 1. Beats read in order: ✔. 2. Weight and easing: ~ (the arcs are short, ease-out
only). 3. One focal point at a time: ✔ (three small figures, same beat). 4. Body readable at the hit: ~ (the
courtier covers the neck on rats and the brow of a treant, never the eyes). 5. Ending decays: ✔ (the figure
fades where the smoke starts). 6. Distinct: ~ (not seen beside the Censer or Ashen). Art not scored.

**New owner question:** none. Still open: Q1 (E5 depth bands), Q2, Q4. A `place` word for "the head" (an authored head
point per actor, as `StanceManifest.HeadFor` already holds for the icon crop) would replace the `height` + `dx`
pair; not built, it is a contract change.

### Orchestrator's step 3 check of round 3 (2026-10-07, r5 full GIFs, rat and treant 1x; frames at 60 fps)

Shown to the owner. The motion structure is fixed:
- three separate figures;
- at the head;
- the smoke born where the figure stands.

Legibility depends on the art:
1. Rats, f20-f64: the courtier is dark violet on dark grey fur at the nape, so it reads as fur texture. At f36-f48 it
   is barely visible. The spell's main figure does not carry on rats with this stand-in.
2. Treants, f28-f64: three small figures on the canopy beside each face, visible but dark against the green.
3. The cue, f42-f64: the violet rim shiver round every whole body is now the strongest thing on screen and the
   courtiers come second. It is a fine payoff, but it outweighs the figures.
4. The smoke is small and pale yellow (f72-f104), closer to sparks than to dissolving robes.
5. The far arc (f10-f20) is close to invisible.
Step 4 art must therefore carry the read: a light outline or rim on the courtier, lighter violet, and smoke in the
robe's colour.

## Step 6: art integrated and compared in context (2026-10-07)

Captures `tools/gif.ps1 spell court_of_whispers -Vs rat` (run `20261007T202519Z_65bedc26a0e6`) and `-Vs treant`
(`20261007T202742Z_8aad1e087641`). Copies beside this file: `court_of_whispers_step6_{rat,treant}_full_1x.gif`
(untracked) and `*_preview.gif` (committed), `step6_*_timing.json`. Timing unchanged: cue f41, freed f158.
Round 3 timing, anchors (`place: above`, height 0.3, dx), sizes 52/70/80 and beat order are kept.

### Art record

| Piece | Source | Recipe | Output | Attempts |
|---|---|---|---|---|
| Lean | `court_of_whispers_courtier_lean.png` (1024, step 4) | `recipes/court_of_whispers_courtier.json`: hue key, `content_crop` (852x876), `alpha_floor` 40 | `Resources/Spells/court_of_whispers_courtier` f0 | accepted at step 5, wired first try |
| Orbit | `court_of_whispers_courtier_back.png` (1024) | `recipes/court_of_whispers_courtier_back.json`: same, crop 715x876 | `..._courtier_back` f0 | accepted at step 5, wired first try |
| Wisps | `court_of_whispers_wisps.png` (4x1 of 256) | `recipes/court_of_whispers_particles.json`: grid 1x4, hue key, `alpha_floor` 40 | `..._particles` f0-f3 (curl, S-wisp, motes, lick) | accepted at step 5, wired first try |

The stand-ins (`court_of_whispers_courtier-v2.png`, `courtier_back.png`, `court_of_whispers_atlas_v1.png`) stay on disk,
unreferenced. No new tooling: `slice_spell_sheet.py` as it is. The recipes' pivots are in the cropped frame, so
`impactX`/`impactY` were recomputed from the prompts' pivots: lean 0.84/0.72 to 0.86/0.77 (hood opening 82% across, 22%
down), orbit 0.83/0.88 to 0.86/0.90 (hood 80%, 12%).

**Changes in `skills.json`:** emitter weights by the new cells (smoke `[0.6, 0.4, 0, 0]` curl and S-wisp, was the old
cell 1 and 4; last curl `[1, 0, 0, 0]`; motes unchanged on cell 3), `price` `startFrame` 1 to 3 (the lick),
`bodyRim.peak` 0.9 to 0.7, lean `glow` 0.3 to 0.1 with `glowCurve` `{to 0.2, pulse 0.7, period 2, crest 0.12}` so the
lean flares on the cue (0.58 + 0.12 = 0.70 s) and settles.

### Playback, as seen (60 fps, my own contact sheets, cue f41)

1. **Courtier on the dark rats: yes.** f20-f38 a lilac robe at each nape (the f8-f14 far arc a pale smudge), f41-f64
   the lilac robe is the brightest thing on the back of each rat with the rim a thin line round the fur. Caveat: at 80 px
   on a rat the figure reads as a lilac hooded robe on the neck; the cupped hand and the empty hood are not legible. On
   the treants (f22-f70) it reads as a hooded figure leaning toward the face.
2. **Courtiers against the rim: courtiers lead.** With peak 0.7 the rim is a thin violet edge on the rats (f41-f56) and
   the figures are the focus. On the treants the rim is still a wide violet halo with branches (f41-f56, f64) and the
   figures sit inside it, readable but not dominant. Not tuned further; 0.6 is the next step if the owner wants it.
3. **Smoke: violet, reads as the robe coming apart.** f72-f130 thin violet S-curls rise from the nape (rat) and over the
   canopy (treant). Small. On the treant the curls rise above the stage top and run under the enemy plates at f84-f128 (the
   hold under the stage top, as in round 3).
4. **Far arc: kept, visible.** f8-f20 a pale lilac figure at head height before the near arc, faint on the rats and on
   the treant canopy. Opacity unchanged (0.4, tint `#9A90C0`).

Open: on the treant rack the lean robes of neighbours 1 and 2 touch at f41 (two figures overlapping the shared canopy).

### Checks

`tools/spell_feel.ps1 -Spell court_of_whispers`: rat contact f41, **WARN contact desync stays**: the biggest rise is
f20 (the near arc fading in at the head, now a brighter lilac against the dark fur), not the cue. As in round 3 it is the
art entering, not a timing fault; the recorded hit frame is f41 (sound, badges, first rim pulse on it).

### Worker's rubric

1. Beats read in order: yes. 2. Weight and easing: ~ (ease-out arcs, unchanged). 3. One focal point: yes on rats, ~ on
treants (rim halo). 4. Body readable at the hit: ~ (the robe covers the neck, never the face or eyes). 5. Ending decays:
yes (violet smoke from where the figure faded). 6. Distinct: not yet seen beside the Censer or Ashen; owner side by
side still due. Art: reads at 52-80 px as a hooded robe, not as a whispering gesture.

### Round log row, measures

Round 4 (step 6): real art wired; rim peak 0.7; lean glowCurve; caught by the worker's own frames. Generation attempts
per piece: 1 each. One content build, 2 captures, 1 spell_feel run.

### Step 7

Snapshot id: `0fe926ca` (step 6 and 7 closed there; gate: full suite green via `-Changed`, 6244 EditMode, 1955 PlayMode).

## Open owner questions

Each asked once.

| # | Question | Asked in round | Status / answer |
|---|---|---|---|
| 1 | The far arc cannot go behind the bodies on a rack without E5 body depth bands (cross-layer, senior). Build E5 now for Court (and Aegis, Shackles later), or accept the far arc as a dim ghost over the body for this spell? | 1 | open |
| 2 | The violet rim shiver lands on every enemy the court is drawn on, bosses included, though a boss only loses 2 places and is not Feared. Shiver a boss too, or skip bosses? (Assumed: shiver all; the code draws the court on them already.) | 1 | open |
| 3 | On a tight rack (three treants) three courtiers read as one mass for about 0.6 s. Keep one per enemy (current), or one court for the whole rack that bends to the front enemy? | 1 | owner, 2026-10-07: try one per enemy, smaller, leaning at head height |
| 4 | Wind-up is now 0.70 s (tier 3, same as Blackglass), cast riser re-cut. At 1.5x it is 0.47 s. Right length? | 1 | open |

## Round log

| Round | What changed | Who caught it (owner / orchestrator / binding check / worker) | Rubric result |
|---|---|---|---|
| 0 (pilot pass 2) | One painted courtier per enemy, ring-and-core payoff demoted, cue 0.3 s | worker, owner gates skipped | superseded |
| 1 | Orbit as two hand-off layers, far arc in the ground band; cue 0.7 s; rim shiver (new `vfx.bodyRim`); smoke steps; price wisp, no ring | worker (frames) and `SpellPoolCapacityTests`: third enemy has no far arc, far arc occluded, hand-off pops | not scored (failed) |
| 1b | `facing: reverse` mirrors the drift x (frames showed the near arc running the wrong way): authored mirrored | worker (frames) | not scored |
| 2 | Far arc to the effects band (gate), crossfaded hand-off (0.1 s), lean crossfaded to full on the cue (the treant's visual contact moved from f47 to f59, a later rim pulse, and still WARNs), smaller figures, bigger smoke | worker (frames, `spell_feel`) | worker score above |
| 3 | Courtiers at the head (`place: above` 0.3, dx, sizes 52/70/80), one per enemy, lean reversed, smoke from the lean spot | worker (own frames), `spell_feel` rat WARN | worker score in Round 3 |
| 4 (step 6) | Step 4 art wired; rim peak 0.7; lean `glowCurve` | worker (own frames) | worker score in Step 6 |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 so far |
| Internal retries | 4 (round 1, 1b, 2, 3) |
| Generation attempts per art piece | 0 (stand-ins) |
| Active work time / waiting time | worker run about 2 h; 11 captures of 30-45 s each, one content build each round |
| Owner handling time (generate, download, upload art; review) | not started |
| Owner-found defects the binding checks should have caught (target 0) | 0 so far; `SpellPoolCapacityTests` caught the ground-band overflow before play |
| Final quality against the reference pack (owner's verdict on the rubric) | not yet |

## Reviewed snapshot

- Commit: see the report (step 2 commit hash).
- Capture run ids: rat 1x `20261007T104110Z_ecda87db2c60`, treant 1x `20261007T104306Z_83c046e7b27f`, treant 1.5x
  `20261007T104448Z_efa87f6eb757`, rat 1.5x `20261007T104611Z_a01cb6e54cfb`.
- Rubric (motion, art): worker's score above; art not scored.
- Still weak: the far arc is invisible, the near arc and lean overlap neighbours into one mass, the body is covered
  for 0.6 s, smoke is small, the treant rim WARN.
