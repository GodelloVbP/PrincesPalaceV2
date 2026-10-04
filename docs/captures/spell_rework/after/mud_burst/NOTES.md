# Mud Burst, "after" (plan S3, spell 3.3)

Pass 1 (2026-10-04) used a procedural glob, a derived splat and the layer field `tint`. Pass 2
(2026-10-04) wires the owner's painted stills: `mud_glob.png` and `mud_splat.png` (recipes
`mud_glob`, `mud_splat`, both keyed `green-soft`; the plain green key clamps green to min(r,b)
and drags browns toward red). Captured with `tools/gif.ps1 spell mud_burst`; GIFs are untracked
next to this file. Owner gates skipped; the calls below are mine.

## What changed

- **Glob and splat are painted and untinted.** `tint` stays only on the `gather` layer (the old
  gold vortex would read as fire without it). The procedural `mud_glob`, the derived
  `mud_burst_splat` (folder, recipe, generator, tests) are deleted; `mud_splat_drops` stays.
- Splat: one still, held 0.3 s then faded 0.22 s, punch 0.15, size 480 (`at: hit` sprites are
  tails, shrunk to a fraction of the body, so it lands rat-sized on a rat).

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.20 | **Gather.** The old sheet's vortex in Shawn's hand, tinted brown. |
| 0.17-0.325 | **Lob.** The painted glob leaves the hand and reaches the body in 0.155 s along an arc (`arcY` 0.6), its three drips trailing. |
| 0.325 | **Payoff cue.** Sound, damage number, Vulnerable badge, the painted splat and 14 clots. Matte, no glow anywhere. |
| 0.325-0.85 | Splat held then fading; clots land. |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.33 s (f20) | 0.33 s | 0.22 s (f13) | 0.22 s |
| payoff to clean | 0.38 s | 0.38 s | 0.25 s | 0.25 s |

The report WARNs "no accent" on the rat (1x and 1.5x). The effect is matte by
design; its accent is the splat opening with the target's hurt flash on the cue
frame, and by eye that frame is the clear peak. The treant's 1.5x "clean at
0.75 s" is the treant's own idle movement after the cast, not the effect: the
splat is gone by f27.

## Frame notes (pass 1 figures, the flash and readability counts not re-measured)

- **Flash guard.** Five casts at 1.5x on the treant (259 GIF frames): per cast
  one brightening of 7.5% of the frame (the hurt flash on the cue) and one of
  about 4% eight GIF frames later, casts at least 1.3 s apart; nothing near a
  25%-area large flash. At most 2 small flashes per cast. A 5.7%/4.9%/4.7%
  cluster at frames 154-172 is the treant's own turn between casts.
- **Readability.** The damage number sits above and left of the splat and stays
  legible on rat and treant (the splat draws under it). The Vulnerable badge
  appears one frame after the cue. The splat covers the struck body's torso on
  a rat for about 0.2 s.
- **Back-rank:** the glob flies in front of the rat and lands on the beetle
  behind the treant; the number (-7) and splat are on the beetle. The glob
  passing in front of an uninvolved body is a mild ambiguity: nothing shows
  that the rat is not the target until the splat.
- **Arc:** present. `drift` used to run over the layer's whole life, held phase included, so a waypoint lifted the glob in Shawn's hand. `SpellFrameCursor.DriftAt` now runs a travelling layer's drift over its flight (held at its start while it waits at the caster, at its end after arrival), and `SpellDrift.arcY` adds a parabola. On the rat 1x frames the glob sits level in the hand at f12, is clearly above the line at f14-f16, and comes down onto the body by f19-f20. The flight is 0.155 s (was 0.125) so the arc has about 9 frames to read.
- **Bug-shaped, not fixed:** none found.

## Identity test: "mud", not "fire"

My verdict: **passes, clearly.** The glob has a wet highlight and a stringy tail and lands as a
round core with radial tendrils and flat spatter: a thrown handful of wet mud, no gold, no glow.
The weakness the procedural version had (a soft clod, a spiky burst) is gone. What stays weak:
the splat is rat-sized on a treant (the tail cap), so a treant reads as "splashed" more than
"hit"; and on a back-rank cast (rat, treant, beetle, slot 3) the glob still flies in front of
the uninvolved rat/treant and the splat is small on the beetle behind the treant.
Spell_feel was not re-run: the cue (0.325 s) and the splat window are unchanged in kind.
