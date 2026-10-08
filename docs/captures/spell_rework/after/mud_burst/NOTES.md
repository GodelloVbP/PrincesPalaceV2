# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

**Closed** (step 7, 2026-10-09). The owner approved r9 as it is. Snapshot below is `066462a9`. Open items stay listed as accepted as is: the glob lands high on the rat (the body centre of a sitting rat is at its shoulders), the smear is parallel to the launch-to-aim line, the strand and puddle colours are unchanged.

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
| 0.00-0.325 | delivery: the glob sits in the hand from the start, leaves at 0.17 s and flies ONE parabola to the body's centre, nine frames | `glob` | `mud_glob_flight` (6 cells, looped, `fps` 20, `until: loop`), size 260 fit target, `orient: path` so the smear trails behind, `drift.arcY` 0.6, no spin; constant sideways speed (`SpellPerformance.Ballistic`) | pinned by `MudBurstPresentationTests.TheGlobFliesOneParabolaAtConstantSidewaysSpeed`; opacity 1, untinted |
| 0.17-0.325 | ground shadow: dark flat blob travels the ground from Shawn's feet to the target's feet with the glob | `shadow` | `mud_puddle` still tinted `#000000`, opacity 0.5, size 130, `place: caster`, `ballistic`, `sort: ground`, same delay and flight as the glob, gone on arrival | sells the lob's height; not shrunk at the apex (no layer field scales a still over its life) |
| 0.17-0.30 | launch: flecks spray from the hand along the throw | `launch` | `mud_splat_drops`, burst 10, aim 25 deg, `gravity` -1400, life 0.15-0.30, size 0.25-0.5 | the plop |
| 0.17-0.60 | drips shed behind the glob, falling off the arc | `glob_trail` | drops cells, `layer:glob`, 70/s, size 0.45-0.8, `gravity` -700, life 0.2-0.42 | sparse at 1x |
| 0.325 | **Cue.** Sound, number, badge, splat, 14 clots, ring of 22 flecks, stage kick | `splat`, `drops`, `impact_ring`, `vfx.kick` | `mud_splat.png`; drops cells; kick 0.012 | splat 480, held 0.3 s, fade 0.22 s; ring `spreadDegrees` 360, size 0.3-0.6, `gravity` -900 |
| 0.52-1.0 | resolution: three strands of mud run down the body below the splat | `drip_a/b/c` | `mud_drips`, 4 cells once over 0.5 s (`fps` 8), size 80 aspect 0.5 fit target, `drift` y -0.15, `at: hit` offsets 0.2 / 0.3 / 0.38, opacity 1, untinted | strand top is the anchor (`impactY` 0.9) |
| 0.43-1.0 | resolution: puddle lies at the feet, then fades | `puddle` | `mud_puddle` still (1066x366 after crop), size 260 fit target, `sort: ground`, `at: release` offset 0.43 (an `at: hit` layer is shrunk), fadeIn 0.1, hold 0.25, fade 0.5, opacity 1, untinted | the layer-ending maximum is 0.5 s |

## Open items

1. Glob colour: the drawn ball reads as orange-brown mud with a pale highlight at f12-f18; the strands and puddle are still the colour calls.
2. Flight, rat 1x f10-f20 (`mud_r9_flight_rat.png`): leaves the hand f10, apex about f12-f13 near a third of the way across, falls onto the rat's body middle f19, splat f20. At f8-f10 it sits at the hand with the gather's dark bits.
3. r9 (`mud_r9_flight_rat.png`, f8-f30): the drawn ball with its smear is in the hand f8-f10 (the old hairy still is gone), launch flecks f9-f12, the ball flies the arc f11-f19 with the smear trailing left and droplets, drip clots fall off the arc, the ground shadow runs under it to the rat's feet and ends at f19, the ring at f20-f22 is a few flecks beside the splat. The rat's head and body stay readable under the splat f20-f30. No spin. `orient: path` turns the ball onto the straight launch-to-aim line, not the arc's tangent, so the smear is parallel to that line (slightly downhill) and does not follow the curve. No stretch or squash and no shrink of the shadow at the apex (no layer field scales a sprite over its life; E3 is not built). The stand-in contact art (`prismatic_orb_earth_contact`) is rocks, not mud, so it is not used; the orb wake stand-in is dropped.
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
| Glob in flight | `mud_glob_flight.png` 1536x256, 6x1 of 256 | `mud_glob_flight.json`, `green-soft`, no crop | accepted (step 5). Spec: a brown cel mud ball with a short pointed smear trailing left and a few droplets behind it, six wobble frames in one registration (ball centre about 0.49 across, 0.5 up), on #00FF00. Replaces `mud_glob.png` in flight |
| Vortex, drops | old sheet | n/a | stand-in (`gather`, `drops`) |

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | procedural glob, derived splat, layer field `tint` | worker | n/a |
| 2026-10-04 | drift | drift runs over a travelling layer's flight; glob arcs | worker | `86aca286` |
| 2026-10-04 | pass 2 | owner's painted glob and splat replace the procedural lump and derived burst | worker | `1de9f948` |
| 2026-10-08 | step 2 | `drips_a/b` and `puddle` added; fade capped at 0.5 s; `at: hit` puddle was capped small, moved to `at: release` | orchestrator brief | `d061bf56` |
| 2026-10-08 | step 6 | painted puddle and drip strands replace the splat-still puddle and the drop emitters; strands lowered (drift y -0.15) and puddle 330 to 260 after the first capture (strands hung from the face, puddle wider than a rat) | orchestrator step 6 | `a826b78b` |
| 2026-10-08 | r7 | glob flight: one parabola (engine: arc flights keep constant sideways speed, `Ballistic`), tumble, drip wake, stage kick; strands start at the splat centre | owner: the arc was flat then vertical | `778db201` |
| 2026-10-08 | r8 | `shadow` (new layer field `ballistic`), `launch`, `wake`, heavier `glob_trail`, `impact_ring` | owner: still just a rotating blob | `b074199f` |
| 2026-10-09 | r9 | `mud_glob_flight` sheet replaces the glob still, held and flown; spin and the orb wake removed; `orient: path` | owner: the hairy red still and the stick wake | `066462a9` |

## Measures

`tools/spell_feel.ps1 -Spell mud_burst -Versus treant,rat` at 1x and `-Speed 1.5`; reports in `spell_feel_close_1x/` and `spell_feel_close_1.5x/` (run 2026-10-09). 60 fps; times in seconds.

| Run | Contact (authored / visual) | Peak (floor), at | Residue after contact | Payoff to clean | Hot peak (flash proxy) | Warnings |
|---|---|---|---|---|---|---|
| 1x rat | f20 / f20 | 3.1 (0.4), 0.33 s | 0.55 | 1.00 | 0.00% at f0 | residue tail (limit 0.50), no accent |
| 1x treant | f20 / f20 | 4.6 (0.8), 0.33 s | 0.92 | 1.07 | 0.00% at f0 | residue tail |
| 1.5x rat | f13 / f13 | 3.2 (0.4), 0.22 s | 0.53 | 0.87 | 0.00% at f0 | residue tail |
| 1.5x treant | f13 / f13 | 4.5 (0.2), 0.22 s | 0.83 | 0.85 | 0.01% at f64 | residue tail |

The residue tail is by design: the drips run and the puddle fades on purpose for 0.5-1.0 s after the splat (the aftermath beat), and the rat's own idle settles in the same window. "No accent" on the rat at 1x is the matte identity (peak 3.1 on the cue frame against a typical 2.6). The peak is on the cue in all four runs. The measure's "gather 0.18 s, authored 0" note is the glob held in the hand (the sheet plays from release).

Other measures (owner reviews, retries, attempts per piece, handling time): not recorded.

## Snapshot

- **Commit:** `066462a9` (the content at the owner's approval; the closing commit only edits NOTES and adds the reports).
- **Capture run id:** `20261008T170854Z_9b51d33691a3` (r9 rat 1x); `spell_feel_close_1x` and `spell_feel_close_1.5x` (2026-10-09); GIFs `mud_burst_r9_*`.
- **Gate:** `tools/run_tests_parallel.ps1 -Changed -BuildContent`: "All tests passed -- a SLICE: 271 EditMode + 144 PlayMode classes, 227s wall" (r9; the r8 run was the full suite, 356s, with `SpellPoolCapacityTests`).
- **Binding checks:** no transform jump: pass (the glob is one parabola in time, pinned by `MudBurstPresentationTests.TheGlobFliesOneParabolaAtConstantSidewaysSpeed`). Sound, number, badge and accent on one frame: pass (contact authored = visual: f20 at 1x, f13 at 1.5x, all four runs; the peak is on the cue). Beat layers inside the stage crop: pass (the back-rank cast keeps glob, shadow and splat on the stage).
- **Flash guard:** `spell_feel` hot peak 0.00-0.01% of stage pixels on any frame; a matte spell, no white flash beyond the shared hurt flash.
- **Rubric (worker scored):** Motion: 1 ✔, 2 ✔ (one parabola, shadow, constant sideways speed), 3 ~ (launch spray, shadow, glob and drips compete before the cue; the splat is the one focal point at it), 4 ✔ (the rat's head and body readable under the splat f20-f30), 5 ~ (decays over 0.9-1.1 s: the residue tail WARN), 6 ~ (not played beside the Censer or Ashen Reckoning). Art: 1 ✔, 2 ~ (glossy caramel strands and the paler puddle are lighter than the flat brown splat), 3 ✔, 4 ~ (the strands read as caramel at 1x), 5 ✔ (not fire, not blood).
- **Still weak (accepted as is):** the glob lands high on the rat; the smear is parallel to the line, not the arc; strand and puddle colour; no stretch or squash and no shrink of the shadow (no field); the glob passes in front of an uninvolved treant on a back-rank cast.

