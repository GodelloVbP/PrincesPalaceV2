# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 6 (step-4 art wired) in the commit that carries this file; `mud_puddle` and `mud_drips` were accepted at step 5 (`docs/prompts/2026-10-08_gale_mud_art.md`). Captures `mud_burst_r6_{rat,treant}` (1x) and `mud_burst_r6_rat_1.5x`, sheet `mud_r6_sheet.png` (approved frames on top; previews and timing committed, full GIFs and the sheet untracked). Waits on the orchestrator's check, then the owner (step 3).

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
| 0.52-1.0 | resolution: three strands of mud run down the body below the splat | `drip_a/b/c` | `mud_drips`, 4 cells once over 0.5 s (`fps` 8), size 80 aspect 0.5 fit target, `drift` y -0.15, `at: hit` offsets 0.2 / 0.3 / 0.38, opacity 1, untinted | strand top is the anchor (`impactY` 0.9) |
| 0.43-1.0 | resolution: puddle lies at the feet, then fades | `puddle` | `mud_puddle` still (1066x366 after crop), size 260 fit target, `sort: ground`, `at: release` offset 0.43 (an `at: hit` layer is shrunk), fadeIn 0.1, hold 0.25, fade 0.5, opacity 1, untinted | the layer-ending maximum is 0.5 s |

## Open items

1. Puddle, rat f36-f44: reads as a flat pool of mud on the ground with splash fingers and clots; its two light tans and the orange make it paler and more orange than the dark chocolate splat above it at f36. Gone by f54. On the treant (size scales with the body) it is about the trunk's width.
2. Drips, **owner call on look**: the strands are glossy and caramel-orange with a pale stripe. Rat f44-f54: they read as mud on the fur; treant f36-f44: the three strands start directly under the mouth, so at f44 the treant looks to be drooling. At 1x they pass as honey or caramel more than as mud (rat f44, treant f44); untinted per the rule. A flatter, darker strand recolour is a step-4 revision, not a tint.
3. Aftermath is gone by f54 (rat) and f60 (treant); the last drop falls to the ground f54-f66.
4. Puddle fade 0.5 s, not 0.6 (layer-ending maximum).
5. Old items kept: splat is rat-sized on a treant (the tail cap); back-rank cast shows the glob in front of uninvolved bodies; `spell_feel` not re-run.

## Decisions

- 2026-10-04 (worker): glob and splat painted and untinted; recipes `mud_glob`, `mud_splat` keyed `green-soft` (the plain green key clamps green to min(r,b) and drags browns toward red). The procedural `mud_glob`, the derived `mud_burst_splat` (folder, recipe, generator, tests) are deleted; `mud_splat_drops` stays.
- 2026-10-04 (worker): `SpellFrameCursor.DriftAt` runs a travelling layer's drift over its flight (held at start while it waits at the caster, at its end after arrival) and `SpellDrift.arcY` adds a parabola; before, `drift` ran over the layer's whole life, so a waypoint lifted the glob in Shawn's hand. Rat 1x: glob level in the hand at f12, clearly above the line f14-f16, down onto the body by f19-f20.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Glob | `mud_glob.png` | `mud_glob.json` | accepted (owner's painted) |
| Splat | `mud_splat.png` | `mud_splat.json` | accepted (owner's painted) |
| Puddle | `mud_puddle.png` 1536x512 | `mud_puddle.json`, `green-soft`, content crop | accepted (step 5); prompt 3 of `docs/prompts/2026-10-08_gale_mud_art.md`, frame `2026-10-08_refs/mud_puddle_approved.png` |
| Drip strand | `mud_drips.png` 1024x512, 4x1 | `mud_drips.json`, `green-soft`, no crop | accepted (step 5); prompt 4, frame `mud_drips_approved.png` |
| Vortex, drops | old sheet | n/a | stand-in (`gather`, `drops`) |

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | procedural glob, derived splat, layer field `tint` | worker | n/a |
| 2026-10-04 | drift | drift runs over a travelling layer's flight; glob arcs | worker | `86aca286` |
| 2026-10-04 | pass 2 | owner's painted glob and splat replace the procedural lump and derived burst | worker | `1de9f948` |
| 2026-10-08 | step 2 | `drips_a/b` and `puddle` added; fade capped at 0.5 s; `at: hit` puddle was capped small, moved to `at: release` | orchestrator brief | this commit |
| 2026-10-08 | step 6 | painted puddle and drip strands replace the splat-still puddle and the drop emitters; strands lowered (drift y -0.15) and puddle 330 to 260 after the first capture (strands hung from the face, puddle wider than a rat) | orchestrator step 6 | this commit |

## Measures

Not recorded (pre-workflow). Timing (`spell_feel`, 60 fps): cast start to payoff 0.33 s (f20) at 1x, 0.22 s (f13) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `1de9f948`.
- **Capture run id:** not recorded.
- **Rubric:** not scored; identity test "mud, not fire" passes clearly.
- **Still weak:** Open items 1-2.
