# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 2 (aftermath with existing art) in the commit that carries this file. Pass 2 `1de9f948` wired the painted glob and splat. Captures `mud_burst_r5_{rat,treant}` (1x) and `mud_burst_r5_rat_1.5x` in this folder (previews, timing and `mud_r5_sheet.png`; full GIFs untracked). Waits on the orchestrator's check, then the owner (step 3).

## Identity

- **Verb:** lob.
- **Signature beat:** a thrown handful of wet mud lands as a round core with radial tendrils and flat spatter, then runs down the body and pools.
- **Must read as:** "mud", not "fire"; matte, no gold, no glow, no tint on painted stills.
- **Must NOT read as:** a soft clod, a spiky burst (the procedural version); a wound (red) on the body.
- **Motion references:** `drips_*`: wet mud creeping down fur or bark under its own weight, slower than a thrown clot; `puddle`: a splash settling into a pool and soaking in.

## Beat table

Release clock; cue **0.325 s** = f20 at 1x, f13 at 1.5x.

| t | Beat | Layer | Stand-in | Notes |
|---|---|---|---|---|
| 0.00-0.20 | anticipation: vortex in Shawn's hand, tinted brown | `gather` | old sheet | `tint` only here |
| 0.17-0.325 | delivery: glob arcs from the hand to the body | `glob` | `mud_glob.png` (painted) | `arcY` 0.6; flight 0.155 s |
| 0.325 | **Cue.** Sound, number, Vulnerable badge, splat, 14 clots | `splat`, `drops` | `mud_splat.png` (painted); drops sheet | 480, held 0.3 s, fade 0.22 s |
| 0.65-1.15 | resolution: drips run down the body | `drips_a`, `drips_b` | `mud_splat_drops` cells, 10/s for 0.5 s each, `sourceDy` 45 up the body, `sourceDx` -34 / +30, aim 270, speed 15-40, `gravity` -150, life 1.0-1.3, size 0.5-0.8 | born after the splat has faded (`at: hit`, offset 0.3); no art made for strands, so they are blotches |
| 0.43-1.0 | resolution: puddle at the feet, then fades | `puddle` | `mud_splat` (square, not flattened), `at: release` offset 0.43 (an `at: hit` layer is capped to 0.75 of the body), size 260 fit target, fadeIn 0.1, hold 0.25, fade 0.5 (the layer maximum) | matte, `sort: effects`, no tint, no glow |

## Open items

1. Puddle is a round star-shaped splat about 100 px on a rat, not a flat pool: `aspect` sets the box and does not squash the art, and no layer field flattens a still. f34-f44 it reads as mud thrown at the feet. Gone by f56 (rat) and f56 (treant). Needs the flat puddle still (Art needed 1).
2. Drips, rat f50-f80: brown blotches appear on the upper body and slide to the hip by f80; they read as stains, not strands. Treant: low contrast on brown bark, hardly visible at 1x.
3. The aftermath is faint beside the splat: the splat is gone by f34 (rat) and the drips start at f39. Not played at 1.5x beside the cue frame; capture `mud_burst_r5_rat_1.5x` made, not studied frame by frame.
4. Puddle fade 0.5 s, not the 0.6 s asked: the layer ending is capped at 0.5 s (content build refuses more).
5. Old items kept: splat is rat-sized on a treant (the tail cap); back-rank cast shows the glob in front of uninvolved bodies; `spell_feel` not re-run after step 2.

## Decisions

- 2026-10-04 (worker): glob and splat painted and untinted; recipes `mud_glob`, `mud_splat` keyed `green-soft` (the plain green key clamps green to min(r,b) and drags browns toward red). The procedural `mud_glob`, the derived `mud_burst_splat` (folder, recipe, generator, tests) are deleted; `mud_splat_drops` stays.
- 2026-10-04 (worker): `SpellFrameCursor.DriftAt` runs a travelling layer's drift over its flight (held at start while it waits at the caster, at its end after arrival) and `SpellDrift.arcY` adds a parabola; before, `drift` ran over the layer's whole life, so a waypoint lifted the glob in Shawn's hand. Rat 1x: glob level in the hand at f12, clearly above the line f14-f16, down onto the body by f19-f20.

## Art

| Piece | File | Status |
|---|---|---|
| Glob | `mud_glob.png` | accepted (owner's painted) |
| Splat | `mud_splat.png` | accepted (owner's painted) |
| Vortex, drops | old sheet | stand-in |
| Drips, puddle | none | stand-in: drops cells and the splat |

Art needed for step 4 (not prompted yet): 1. a flat puddle still: a wet brown pool seen from a low three-quarter angle, 3:1, soft edge, a few dried flecks, matte, reading at 260-400 px; 2. a drip-strand sheet, 4 cells of a streak of mud running down and thinning (vertical 1:3), brown on a keyable green ground, reading at 60-120 px on bark and fur. Prompts follow `docs/prompts/TEMPLATE_spell_art.md`.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | procedural glob, derived splat, layer field `tint` | worker | n/a |
| 2026-10-04 | drift | drift runs over a travelling layer's flight; glob arcs | worker | `86aca286` |
| 2026-10-04 | pass 2 | owner's painted glob and splat replace the procedural lump and derived burst | worker | `1de9f948` |
| 2026-10-08 | step 2 | `drips_a/b` and `puddle` added; fade capped at 0.5 s; `at: hit` puddle was capped small, moved to `at: release` | orchestrator brief | this commit |

## Measures

Not recorded (pre-workflow). Timing (`spell_feel`, 60 fps): cast start to payoff 0.33 s (f20) at 1x, 0.22 s (f13) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `1de9f948`.
- **Capture run id:** not recorded.
- **Rubric:** not scored; identity test "mud, not fire" passes clearly.
- **Still weak:** Open items 1-2.
