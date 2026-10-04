# Mud Burst, "after" (plan S3, spell 3.3)

Captured 2026-10-04 with `tools/gif.ps1 spell mud_burst` and `tools/spell_feel.ps1`.
No painted art. New: the layer field `tint` (decision 10: "reads as fire" proved
it needed) and three procedural pieces in `tools/make_spell_fx.py`. GIFs are
untracked next to this file. Owner gates skipped; the calls below are mine.

## What changed and why

- **`tint`**, a `#RRGGBB` multiplied into a sprite or still layer. The sheet's
  gold glints are what read as fire; the same frames under `#7A5A40` are dark
  mud. Multiply only, so it cannot brighten; refused on an emitter.
  `SpellTint` + rules + `SpellTintTests`, copy pin extended.
- **`mud_glob`**: a lumpy matte glob with drips trailing behind, one frame.
- **`mud_splat_drops`**: four clot shapes for a falling spray (brown in the pixels).
- **`mud_burst_splat`**: the old sheet's burst frames (f12-f25) with the streak
  back to the caster feathered out. The old single layer drew that streak as a
  rope of mud from Shawn to the target for 0.3 s after the hit, which read as a
  flamethrower jet. The `mud_burst` folder itself is untouched: the Bog Witch's
  enemy cast (`bog_mud_burst`) and the gather still use it.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.20 | **Gather.** The sheet's first eight frames, a dark vortex of mud in Shawn's hand, tinted; the glob is held beside it. |
| 0.20-0.325 | **Throw.** The glob crosses straight to the body in 0.125 s, drips trailing. |
| 0.325 | **Payoff cue.** Sound, damage number, Vulnerable badge, the splat (28 fps, 0.5 s, punch 0.15, size 900 on a rat) and 14 clots thrown up that fall back (0.3-0.55 s). No glow anywhere. |
| 0.325-0.80 | Splat thins and fades; clots land. |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.33 s (f20) | 0.33 s | 0.22 s (f13) | 0.22 s |
| payoff to clean | 0.40 s | 0.40 s | 0.27 s | 0.53 s (measurement, see below) |

The report WARNs "no accent" on the rat (1x and 1.5x). The effect is matte by
design; its accent is the splat opening with the target's hurt flash on the cue
frame, and by eye that frame is the clear peak. The treant's 1.5x "clean at
0.75 s" is the treant's own idle movement after the cast, not the effect: the
splat is gone by f27.

## Frame notes

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
- **Arc:** none. A lob needs a path arc; `drift` runs over the layer's whole
  life, held phase included, so a waypoint lifted the glob in the hand before
  the throw. Dropped; the 0.125 s flight is too short to read an arc anyway.
- **Bug-shaped, not fixed:** none found.

## Identity test: "mud", not "fire"

My verdict: **passes.** The tint alone moved it from fire to mud, and the
splat with falling clots reads as wet earth landing. What stays weak: the glob
is a soft lump (drawn at about 80 px, it reads as a clod, not as glistening
mud), and the burst frames are spiky, an explosion shape more than a splatter.
There is no sheen, no stringy drip from the body afterwards.

## What a painted still would fix

One painted glob (wet highlight, stringy tail) and one splat still with a
splatter silhouette (round core, radial tendrils, flat droplets) would replace
the procedural lump and the spiky burst. Prompt in
`docs/prompts/2026-10-03_spell_rework.md`.
