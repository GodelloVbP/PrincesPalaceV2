# Mud Burst: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.3. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Pass 2 (2026-10-04) wired the owner's painted stills (`1de9f948`); the drift-over-flight fix is `86aca286`; the badge lands on the cue frame (rechecked in `2bb3ac09`). Captured with `tools/gif.ps1 spell mud_burst`; GIFs untracked beside this file. Not yet through the workflow's steps 3-7. `spell_feel` was not re-run after pass 2 (cue 0.325 s and splat window unchanged in kind).

## Identity

- **Verb:** lob.
- **Signature beat:** a thrown handful of wet mud lands as a round core with radial tendrils and flat spatter.
- **Must read as:** "mud", not "fire"; matte, no gold, no glow.
- **Must NOT read as:** a soft clod, a spiky burst (the procedural version).

## Beat table

Release clock; cue 0.325 s = f20 at 1x, f13 at 1.5x.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.20 | vortex in Shawn's hand, tinted brown | `gather` | old sheet | `tint` only here (the gold vortex would read as fire) |
| 0.17-0.325 | glob leaves the hand and reaches the body in 0.155 s along an arc, three drips trailing | glob (travel) | `mud_glob.png` | `arcY` 0.6; flight 0.155 s (was 0.125) so the arc has about 9 frames |
| 0.325 | **Cue.** Sound, damage number, Vulnerable badge, the splat, 14 clots; matte, no glow | splat | `mud_splat.png` | one still, 480 (an `at: hit` sprite is a tail, shrunk to a fraction of the body: rat-sized on a rat), held 0.3 s, fade 0.22 s, punch 0.15 |
| 0.325-0.85 | splat held then fading; clots land | `mud_splat_drops` | particles | |

## Open items

1. Splat is rat-sized on a treant (the tail cap), so a treant reads as "splashed" more than "hit".
2. Back-rank cast (rat, treant, beetle, slot 3): the glob flies in front of the uninvolved rat/treant and nothing shows the rat is not the target until the splat; the splat is small on the beetle behind the treant. The number (-7) and splat are on the beetle.
3. `spell_feel` WARN "no accent" on the rat at 1x and 1.5x: the effect is matte by design; the accent is the splat opening with the hurt flash on the cue frame, the clear peak by eye. The treant 1.5x "clean at 0.75 s" is the treant's idle movement; the splat is gone by f27.
4. The splat covers the rat's torso for about 0.2 s. Damage number sits above and left of the splat and stays legible; badge, number and splat first appear on the cue frame.
5. Pass 1 figures: the flash and readability counts were not re-measured after pass 2. Five casts at 1.5x on the treant (259 frames): per cast one 7.5% brightening (hurt flash on the cue) and one of about 4% eight GIF frames later, casts at least 1.3 s apart; no 25% flash.

## Decisions

- 2026-10-04 (worker): glob and splat painted and untinted; recipes `mud_glob`, `mud_splat` keyed `green-soft` (the plain green key clamps green to min(r,b) and drags browns toward red). The procedural `mud_glob`, the derived `mud_burst_splat` (folder, recipe, generator, tests) are deleted; `mud_splat_drops` stays.
- 2026-10-04 (worker): `SpellFrameCursor.DriftAt` runs a travelling layer's drift over its flight (held at start while it waits at the caster, at its end after arrival) and `SpellDrift.arcY` adds a parabola; before, `drift` ran over the layer's whole life, so a waypoint lifted the glob in Shawn's hand. Rat 1x: glob level in the hand at f12, clearly above the line f14-f16, down onto the body by f19-f20.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Glob | `mud_glob.png` | `mud_glob`, `green-soft` | accepted (owner's painted) |
| Splat | `mud_splat.png` | `mud_splat`, `green-soft` | accepted (owner's painted) |
| Vortex, drops | old sheet | n/a | stand-in |

No step-4 prompts: this pass predates the art workflow.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | procedural glob, derived splat, layer field `tint` | worker | n/a |
| 2026-10-04 | drift | drift runs over a travelling layer's flight; glob arcs | worker | `86aca286` |
| 2026-10-04 | pass 2 | owner's painted glob and splat replace the procedural lump and derived burst | worker | `1de9f948` |

## Measures

Not recorded (pre-workflow). Timing (`spell_feel`, 60 fps): cast start to payoff 0.33 s (f20) at 1x, 0.22 s (f13) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `1de9f948`.
- **Capture run id:** not recorded.
- **Rubric:** not scored; identity test "mud, not fire" passes clearly.
- **Still weak:** Open items 1-2.
