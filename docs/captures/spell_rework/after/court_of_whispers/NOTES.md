# Court of Whispers: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3.

## Status

Step 6 closed in `0fe926ca` (steps 6 and 7); values-only tuning after the orchestrator's step 6 check in `474747ad` (2026-10-07). Waits on the owner's review of the tuned GIFs (`court_of_whispers_tune_{rat,treant}_1x.gif`, strips beside it). Owner passed step 3 on round 3 motion (2026-10-07).

Captures: `tools/gif.ps1 spell court_of_whispers -Vs rat|treant [-Speed 1.5]`. Previews committed, full GIFs untracked, `timing.json` per capture as `<prefix>_timing.json`. `tools/spell_feel.ps1` pops a Unity window (`screenshot.ps1 -Runtime`); without runtime permission run `tools/spell_feel.py report` on the `gif.ps1` frames. The 2026-10-03/04 pilot (one painted courtier, ring-and-core payoff) is superseded; its files are in `previous_pass/`, untracked.

## Identity

- **Verb:** whisper.
- **Signature beat:** the courtiers all bend in to the head at once and the Fear lands as a violet shiver on the body outline.
- **Must read as:** "they are whispering to them".
- **Must NOT read as:** a magic circle, ring or reticle; a drain or ghost attack; a wall of purple hiding the enemy; a full-silhouette flash.
- **Motion references:**
  - orbit (`orbit_back`, `orbit_front`): courtiers walking a slow circle round a throne, seen from a slant; the far side is higher, smaller, dimmer; it slows into the lean rather than stopping;
  - `lean`: a person bending from the waist to speak into an ear, held a beat, then straightening;
  - smoke (`smoke_a`, `smoke_b`, `curl`): a snuffed candle's smoke, thick, thinner, one last curl;
  - `price`: a candle flame guttering on Shawn.
- **Exceptions:** rim pulses cut in and out (a shiver flickers); the far arc hands to the near arc with a size jump 52 to 70, crossfaded 0.1 s; the lean is full strength on the cue, not before.
- **Mechanics, unchanged:** every non-boss enemy Feared 1 turn, bosses lose 2 places, Shawn Vulnerable 1 turn; sound, Fear badges and first rim pulse on one frame; Shawn's badge waits 0.18 s (`casterCostSeconds`).

## Beat table

Release clock; cue **0.70 s** = f41 at 1x, f27 at 1.5x (`timing.json`); cast riser re-cut to 0.70 s (`make_spell_cast_cues.py`, `MeleeSoundOverrideTests`); `clearedSeconds` 2.65, freed f158. Six courtier layers are `place: above`, `height: 0.3` (face on a treant, neck on a rat; 0.45 put the treant's courtier over the canopy), `dx` 60 toward the nape (sign follows facing; not fit-scaled), `fit: target`. Enemies face left, so the courtier stands behind the head and leans in from the right (`lean` is `facing: reverse`; a reverse layer mirrors its drift). Sprite layers a body: 3 x 3 bodies plus `price` = 10 of the effects band's 12.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.70 | stage gloom 0.35 | stage `gloom` | n/a | in 0.4, holds to the cue, out 0.4 after |
| 0.00-0.50 | far arc: dim courtier drifts left to right | `orbit_back` | courtier_back | 52; `fadeIn` 0.18, opacity 0.4, tint `#B9A0FF`, drift -1.1 to +1.1 box widths, ease-out, fade 0.1 |
| 0.30-0.78 | near arc: mirrored, comes back right to left, slows into the lean spot | `orbit_front` | courtier_back | 70; `fadeIn` 0.1, opacity 1, drift -1.5 to -0.8, ease-out |
| 0.58-1.14 | lean crossfades in, full on the cue, closest at 0.78 s, then draws back | `lean` | courtier_lean | 80; `fadeIn` 0.12, opacity 1, breathe, drift -0.7, -0.2 at 36%, -0.3, fade 0.25; `glow` 0.1 with `glowCurve` {to 0.2, pulse 0.7, period 2, crest 0.12} (flares at 0.70 s) |
| 0.70 | **Cue.** Sound, Fear badges on all enemies, violet rim shiver on every body: 4 pulses 0.07 s apart (f41, f45, f49, f53), last held, fade 0.16 s | `vfx.bodyRim` | engine | `#B58CFF`, peak 0.7; rim only, interior share unchanged |
| 0.70 | whisper motes burst | `settle` | wisps cell 3 | emitter, 8 particles |
| 0.88 | Shawn's price: violet wisp flickers on him, Vulnerable badge | `price` | wisps cell 4 (`startFrame` 3) | 150; `fadeIn` 0.04, 0.35 s, breathe alpha 0.6 rate 2.5, no ring |
| 0.95-1.30 | smoke rises where the courtier stood | `smoke_a` | wisps cells 1-2 `[0.6, 0.4, 0, 0]` | emitter, 18/s for 0.35 s, tint `#B58CFF` |
| 1.25-1.55 | less and smaller | `smoke_b` | same | emitter, 7/s for 0.3 s, tint `#B58CFF` |
| 1.55-2.65 | last curl | `curl` | wisps cell 1 `[1, 0, 0, 0]` | one particle, scale x1.6, life 1.1 s, tint `#B58CFF` |

Smoke emitters have `sourceDx` 15-19 and `sourceDy` -5 to 35 from the head point where the lean ends. A non-travelling `above` layer hangs under the stage top (as the Censer lantern). Visually clean at 1.3-1.4 s at 1x.

## Open items

1. Rat f20-f30: at 80 px the courtier is a saturated violet patch on the back, not a figure; the cupped hand and empty hood are illegible. Treant reads as a hooded figure at the face. Sizes are owner-approved. **Owner call**: accept, or larger/other placement.
2. Treant f41-f56, f64: the rim is still a wide violet halo with branches; figures readable inside it, not dominant. Next step peak 0.6.
3. Treant rack f41: the lean robes of neighbours 1 and 2 touch (overlap on the shared canopy). On rats the figure sits between its head and the next rat's snout f41-f56.
4. Rat `spell_feel` WARN contact desync: biggest rise f20 (near arc fading in at the head), cue f41. Art entering, not a timing fault; hit frame f41 carries sound, badges, first pulse. Explained, not changed. Treant 1x and 1.5x clean in round 3.
5. Smoke is small. Treant f84-f128: curls rise above the stage top and run under the enemy plates. One curl hangs to about f130.
6. Far arc (f8-f20) is a faint pale lilac figure; kept.
7. Painted lean covers the brow and part of the canopy beside the face on a treant f41-f56; eyes stay visible.
8. Flash guard: four rim pulses in 0.2 s per body count as four flashes on the strict count (thin rim, peak 0.7, interior share 0.08); not measured with the rendered-frame tool.
9. Not yet played beside the Censer or Ashen Reckoning (rubric item 6).
10. Restoring the real orbit needs E5 body depth bands (cross-layer; senior with `Escalation: cross-layer`): `FightHudSpec.SpellGroundRenderers` is 2, a 3-enemy cast needs 3, and a ground-band figure is mostly hidden by the body. With the band, set `orbit_back.sort` to `ground` and raise its size and brightness. Evidence in `round1_ground_orbit/`. **Owner call** Q1 below.
11. A `place` word for "the head" (an authored head point per actor, as `StanceManifest.HeadFor` holds for the icon crop) would replace `height` + `dx`; not built, a contract change.
12. Owner calls Q2 (shiver bosses too; assumed yes, code draws the court on them) and Q4 (wind-up 0.70 s, 0.47 s at 1.5x) are unanswered.

## Decisions

- 2026-10-07 (owner): Q3, one courtier per enemy, smaller, leaning at head height (resolved the merging band on a tight rack).
- 2026-10-07 (owner): step 3 passed on round 3 motion.
- 2026-10-07 (orchestrator): step 4 prompts updated for round 3: figures play at 52-80 px on dark rats and green treants, so each piece needs a pale outline over the dark one and a lighter violet body.
- 2026-10-07 (orchestrator): step 5, all three pieces accepted, no revision.
- Open: Q1 (build E5 for Court, Aegis, Shackles, or keep the far arc as a dim ghost over the body), Q2, Q4.

## Art

Delivered on solid green `#00FF00` (no green in any subject). Recipes: hue key, `alpha_floor` 40. `impactX`/`impactY` recomputed from the prompt pivots in the cropped frame. No new tooling.

| Piece | File | Recipe | Status |
|---|---|---|---|
| Lean | `court_of_whispers_courtier_lean.png` 1024 | `recipes/court_of_whispers_courtier.json`: `content_crop` 852x876; impact 0.86/0.77 | accepted; reads as hooded robe at 80 px |
| Orbit (back) | `court_of_whispers_courtier_back.png` 1024 | `recipes/court_of_whispers_courtier_back.json`: crop 715x876; impact 0.86/0.90 | accepted |
| Wisps | `court_of_whispers_wisps.png` 4x1 of 256 | `recipes/court_of_whispers_particles.json`: grid 1x4 | accepted; averages grey-lilac (148,123,182), the green-key fringe pulled smoke yellow-green until tinted violet |
| Stand-ins | `court_of_whispers_courtier-v2.png`, `courtier_back.png`, `court_of_whispers_atlas_v1.png` | n/a | stand-in, unreferenced, on disk |

Unreferenced: ring sheet `Resources/Spells/court_price_pulse` and old atlas `court_of_whispers/f0-f11` (the rejected ring payoff and price).

Step-4 prompts, verbatim. Style block and attachments as in `docs/prompts/TEMPLATE_spell_art.md` section 1.

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

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | 0, pilot pass 2 | one painted courtier per enemy, ring-and-core payoff demoted, cue 0.3 s | worker | n/a |
| 2026-10-07 | 1 | orbit as two hand-off layers, far arc in the ground band; cue 0.7 s; new `vfx.bodyRim`; smoke steps; price wisp. Failed: third enemy had no far arc, far arc occluded, hand-off popped | worker frames, `SpellPoolCapacityTests` | n/a |
| 2026-10-07 | 1b | `facing: reverse` mirrors drift x (near arc ran the wrong way); authored mirrored | worker frames | n/a |
| 2026-10-07 | 2 | far arc to the effects band (gate), 0.1 s crossfaded hand-off, lean full on cue, smaller figures, bigger smoke | worker frames, `spell_feel` | n/a |
| 2026-10-07 | step 3 on r4 | failed: courtiers merged into one band f26-f68, anchored at the base not the head, ending disconnected (fade at feet f68, specks above heads f84-f104) | orchestrator | n/a |
| 2026-10-07 | 3 | courtiers at the head (`place: above` 0.3, dx, sizes 52/70/80), one per enemy, lean reversed, smoke from the lean spot; separate figures every frame, smoke born at f66 where the lean fades f64. Orchestrator: rats f36-f48 dark on dark fur, rim outweighs figures | worker frames; orchestrator | n/a |
| 2026-10-07 | 4, step 6 | real art wired; rim peak 0.9 to 0.7; lean `glowCurve`; smoke weights by new cells; `price` `startFrame` 3 | worker frames | `0fe926ca` |
| 2026-10-07 | tuning | `orbit_front` and `lean` opacity 0.7 to 1 (grey-white ghosts f20-f36, f52); `orbit_back` tint grey to lilac; violet tint on the three smoke emitters (curls yellow-green f64-f80). Treant f6-f33 solid violet, f36-f48 curls violet; rat still a patch | orchestrator's step 6 check | `474747ad` |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 |
| Internal retries | 4 (rounds 1, 1b, 2, 3) |
| Attempts per art piece | 1 each |
| Active / waiting time | worker run about 2 h; 11 captures of 30-45 s, one content build per round |
| Owner-found defects the checks should have caught | 0; `SpellPoolCapacityTests` caught the ground-band overflow before play |
| Quality vs reference pack | not yet |

## Snapshot

- **Commit:** `0fe926ca` (steps 6 and 7); tuning `474747ad`.
- **Capture run ids:** step 6 rat 1x `20261007T202519Z_65bedc26a0e6`, treant 1x `20261007T202742Z_8aad1e087641`. Round 1 (`r4`): rat 1x `20261007T104110Z_ecda87db2c60`, treant 1x `20261007T104306Z_83c046e7b27f`, treant 1.5x `20261007T104448Z_efa87f6eb757`, rat 1.5x `20261007T104611Z_a01cb6e54cfb`.
- **Gate:** `run_tests_parallel.ps1 -Changed` full suite green: EditMode 6244, PlayMode 1955. Slices: `CourtOfWhispersTimingTests` (6), `SpellLayerRulesTests` (91), `SpellPresentationCopyTests` (4), `MeleeSoundOverrideTests` (18), `SpellPoolCapacityTests` (5), `SpellLayerHandoffTests` (3).
- **Binding checks:** no transform jump pass (drifts continuous; hand-off points match within 2 units, tested; size step 52 to 70 crossfaded); cue on one frame pass (`hitFrame` 41 at 1x, 27 at 1.5x; sound, badges, first pulse; Shawn's badge +0.18 s); nothing visible leaves the stage pass.
- **Rubric (worker, step 6):** 1 beats in order ✔; 2 weight and easing ~ (ease-out arcs only); 3 one focal point ✔ rats, ~ treants (rim halo); 4 body readable ~ (robe covers neck, never face or eyes); 5 ending decays ✔; 6 distinct: not yet seen beside Censer/Ashen. Art: reads at 52-80 px as a hooded robe, not as a whispering gesture.
- **Still weak:** rat figure is a patch; treant rim halo; smoke small; far arc faint.
