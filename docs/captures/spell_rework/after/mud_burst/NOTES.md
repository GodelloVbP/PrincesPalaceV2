# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 6 art wired, glob flight and its surroundings reworked (r7, r8) in the commit that carries this file; `mud_puddle` and `mud_drips` were accepted at step 5 (`docs/prompts/2026-10-08_gale_mud_art.md`). Captures `mud_burst_r6_{rat,treant}` (1x) and `mud_burst_r6_rat_1.5x`, sheet `mud_r6_sheet.png` (approved frames on top; previews and timing committed, full GIFs and the sheet untracked). Waits on the orchestrator's check, then the owner (step 3).

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
| 0.17-0.325 | ground shadow: dark flat blob travels the ground from Shawn's feet to the target's feet with the glob | `shadow` | `mud_puddle` still tinted `#000000`, opacity 0.5, size 130, `place: caster`, `ballistic`, `sort: ground`, same delay and flight as the glob, gone on arrival | sells the lob's height; not shrunk at the apex (no layer field scales a still over its life) |
| 0.17-0.30 | launch: flecks spray from the hand along the throw | `launch` | `mud_splat_drops`, burst 10, aim 25 deg, `gravity` -1400, life 0.15-0.30, size 0.25-0.5 | the plop |
| 0.17-0.325 | wake: smeared streak behind the glob | `wake` | `prismatic_orb_earth_wake` (dry tan dust art), `layer:glob` `follow`, `dx` -130, scale 0.8, tint `#4A3020` | the one tinted still: the stand-in reads as sand untinted |
| 0.17-0.60 | drips shed behind the glob, falling off the arc | `glob_trail` | drops cells, `layer:glob`, 70/s, size 0.45-0.8, `gravity` -700, life 0.2-0.42 | sparse at 1x |
| 0.325 | **Cue.** Sound, number, badge, splat, 14 clots, ring of 22 flecks, stage kick | `splat`, `drops`, `impact_ring`, `vfx.kick` | `mud_splat.png`; drops cells; kick 0.012 | splat 480, held 0.3 s, fade 0.22 s; ring `spreadDegrees` 360, size 0.3-0.6, `gravity` -900 |
| 0.52-1.0 | resolution: three strands of mud run down the body below the splat | `drip_a/b/c` | `mud_drips`, 4 cells once over 0.5 s (`fps` 8), size 80 aspect 0.5 fit target, `drift` y -0.15, `at: hit` offsets 0.2 / 0.3 / 0.38, opacity 1, untinted | strand top is the anchor (`impactY` 0.9) |
| 0.43-1.0 | resolution: puddle lies at the feet, then fades | `puddle` | `mud_puddle` still (1066x366 after crop), size 260 fit target, `sort: ground`, `at: release` offset 0.43 (an `at: hit` layer is shrunk), fadeIn 0.1, hold 0.25, fade 0.5, opacity 1, untinted | the layer-ending maximum is 0.5 s |

## Open items

1. **Glob colour, owner call with the drips and puddle:** at gameplay size the glob reads red-brown, closer to a meat or blood clot than to mud (rat f11-f18, back-rank f12-f17). Untinted.
2. Flight, rat 1x f10-f20 (r7 flight, superseded by `mud_r8_flight_rat.png`): leaves the hand f10, apex about f12-f13 near a third of the way across, falls onto the rat's body middle f19, splat f20. At f8-f10 it sits at the hand with the gather's dark bits.
3. r8 (`mud_r8_flight_rat.png`, f9-f30): launch flecks at the hand f9-f12; a brown smear trails the glob f12-f19; the ground shadow runs under it from Shawn's side to the rat's feet and ends at f19; the ring at f20-f22 is a few flecks beside the splat. Rat head and body stay readable under the splat f20-f30. Spin 540 deg/s; no stretch or squash on launch and no shrink of the shadow at the apex (no layer field scales a still over its life; E3 is not built). The stand-in contact art (`prismatic_orb_earth_contact`) is rocks, not mud, so it is not used.
4. Back-rank, mixed rack, beetle as target (`gif.ps1 spell mud_burst -Vs rat,treant,beetle -Target 3`, r7 sheet, removed; the GIF stays): the arc clears the rat's head and drops behind the treant onto the beetle at f20; the glob draws over the treant f16-f19 (effects band). The rat is visibly not hit.
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
| 2026-10-08 | r8 | `shadow` (new layer field `ballistic`), `launch`, `wake`, heavier `glob_trail`, `impact_ring` | owner: still just a rotating blob | this commit |

## Measures

Not recorded (pre-workflow). Timing (`spell_feel`, 60 fps): cast start to payoff 0.33 s (f20) at 1x, 0.22 s (f13) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `1de9f948`.
- **Capture run id:** not recorded.
- **Rubric:** not scored; identity test "mud, not fire" passes clearly.
- **Still weak:** Open items 1-2.
