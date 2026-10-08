# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 6 art wired, glob flight reworked (r7) in the commit that carries this file; `mud_puddle` and `mud_drips` were accepted at step 5 (`docs/prompts/2026-10-08_gale_mud_art.md`). Captures `mud_burst_r6_{rat,treant}` (1x) and `mud_burst_r6_rat_1.5x`, sheet `mud_r6_sheet.png` (approved frames on top; previews and timing committed, full GIFs and the sheet untracked). Waits on the orchestrator's check, then the owner (step 3).

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
| 0.17-0.325 | delivery: glob leaves the hand and flies ONE parabola to the body's centre, tumbling, nine frames | `glob` | `mud_glob.png`; `drift.arcY` 0.6, `rotate.spin` 540, flight 0.155 s at constant sideways speed (`SpellPerformance.Ballistic`) | rises to an apex before the middle (f12-f13 on a rat at 1x), falls faster; pinned by `MudBurstPresentationTests.TheGlobFliesOneParabolaAtConstantSidewaysSpeed` |
| 0.17-0.325 | drips shed behind the glob | `glob_trail` | `mud_splat_drops` cells, `place layer:glob`, `alignFlight`, 60/s, `gravity` -420, life 0.2-0.38 | small; fall behind as it passes |
| 0.325 | **Cue.** Sound, number, badge, splat, 14 clots, stage kick | `splat`, `drops`, `vfx.kick` | `mud_splat.png`; drops; kick 0.012 rise 0.04 fall 0.16 | 480, held 0.3 s, fade 0.22 s |
| 0.52-1.0 | resolution: three strands of mud run down the body below the splat | `drip_a/b/c` | `mud_drips`, 4 cells once over 0.5 s (`fps` 8), size 80 aspect 0.5 fit target, `drift` y -0.15, `at: hit` offsets 0.2 / 0.3 / 0.38, opacity 1, untinted | strand top is the anchor (`impactY` 0.9) |
| 0.43-1.0 | resolution: puddle lies at the feet, then fades | `puddle` | `mud_puddle` still (1066x366 after crop), size 260 fit target, `sort: ground`, `at: release` offset 0.43 (an `at: hit` layer is shrunk), fadeIn 0.1, hold 0.25, fade 0.5, opacity 1, untinted | the layer-ending maximum is 0.5 s |

## Open items

1. **Glob colour, owner call with the drips and puddle:** at gameplay size the glob reads red-brown, closer to a meat or blood clot than to mud (rat f11-f18, back-rank f12-f17). Untinted.
2. Flight, rat 1x f10-f20 (`mud_r7_flight_rat.png`): leaves the hand f10, apex about f12-f13 near a third of the way across, falls onto the rat's body middle f19, splat f20. At f8-f10 it sits at the hand with the gather's dark bits.
3. Spin 540 deg/s turns the clump about 9 degrees a frame; at 1x it reads as a tumbling clot more than a spinning one. No stretch or squash on launch: no layer field scales a still over its life (E3 is not built).
4. Back-rank, mixed rack, beetle as target (`gif.ps1 spell mud_burst -Vs rat,treant,beetle -Target 3`, `mud_r7_flight_backrank.png`): the arc clears the rat's head and drops behind the treant onto the beetle at f20; the glob draws over the treant f16-f19 (effects band). The rat is visibly not hit.
5. Puddle, rat f36-f44: paler and more orange than the splat. Gone by f54.
6. Drips start at the splat's centre, spread across the box (drift x -0.9, +0.9, +0.35; y 0, 0, -0.2), none directly under the treant's mouth. **Owner call on look**: glossy caramel strands (art unchanged).
7. Puddle fade 0.5 s (layer-ending maximum). `spell_feel` not re-run.

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
| 2026-10-08 | r7 | glob flight: one parabola (engine: arc flights keep constant sideways speed, `Ballistic`), tumble, drip wake, stage kick; strands start at the splat centre | owner: the arc was flat then vertical | this commit |

## Measures

Not recorded (pre-workflow). Timing (`spell_feel`, 60 fps): cast start to payoff 0.33 s (f20) at 1x, 0.22 s (f13) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `1de9f948`.
- **Capture run id:** not recorded.
- **Rubric:** not scored; identity test "mud, not fire" passes clearly.
- **Still weak:** Open items 1-2.
