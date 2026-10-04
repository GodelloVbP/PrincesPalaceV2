# Gale Scythe, "after" (plan S3, spell 3.4)

Pass 1 (2026-10-04) used the 12-frame wind sheet only. Pass 2 (2026-10-04) wires the owner's
painted blade (`gale_scythe_blade.png`, 2172x724). Captured with `tools/gif.ps1 spell
gale_scythe` and `tools/spell_feel.ps1`. GIFs are untracked next to this file. Owner gates
skipped; the calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.20 | **Quiet.** The sheet's first four frames, a small whirl closing in Shawn's hand. Nothing on the enemies. |
| 0.13-0.26 | **Sweep.** The painted blade (`Spells/gale_scythe_blade/f0`, one still at its own 6.3:1 proportion) laid along the line of struck bodies (`place formation`, `align span`, overhang 0.5), carried from the caster's side of the front body to past the last one (`drift`), fading out at the cue. Decorative. |
| 0.30 | **Shared payoff.** Every enemy at once: the same blade drawn across each body (still, size 380, aspect 6.3, 0.12 s, fade 0.2, a short drift through the body; punch 0.2, glow 1.2), sound, all numbers, the push in the turn order, 16 leaves. The sheet's three tornado whorls are gone. |
| 0.30-0.65 | The blades fade; the leaves settle. |

## Measured (spell_feel, 60 fps, lone enemy)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.40 s | 0.38 s | 0.48 s | 0.25 s |

No report WARNs. The rat 1.5x "0.48 s" is the rat's own idle settling after the effect, as
in pass 1; the blades are gone about 0.3 s after the cue at 1x.

## Frame notes

- **One payoff frame for all enemies** (decision 8): on the full formation, two enemies,
  one enemy and the mixed rack, the numbers, the cut marks and the hurt flashes open on the
  same frame. Nothing is staggered.
- **Flash guard.** Five casts at 1.5x on a three-rat formation (161 GIF frames, every second
  frame): the largest single-step brightening (more than 0.1 luminance) covers 7.9% of the stage
  crop (the white hurt flash plus the sweep), nowhere near a 25% large flash; casts are about a
  second apart.
- **Readability.** The cut marks are thin strokes at the waist of each body; the numbers (-7,
  -9, -9) sit above them on every frame I looked at, including the treants.
- **Keying.** The blade is pale sea-green and white with gold flecks. The spell kit's `keyed:
  "green"` clamps every pixel's green to min(red, blue), which turned this blade lilac and the
  gold flecks red. A new `keyed: "green-soft"` in `tools/slice_spell_sheet.py` keeps the
  keyer's own spill suppression and skips that clamp; this recipe uses it.
- **Known rough spots.** On a three-wide rack the sweep's tip runs past the right edge of the
  stage crop (the crop, not the stage). On the treants, whose boxes are large, the blade at the
  cue frame shows a dark grey smear at the right edge for one frame (the sweep's last fading
  frame over the white hurt flash); not investigated.
- **Push.** Not visible in these frames (HUD strip off the crop); unchanged by this pass.

## Identity test: "one cut across all of them"

My verdict: **passes.** A single long blade of wind crosses the whole line left to right in
one stroke, then every enemy is marked by the same blade on the same frame: a viewer says
"it cut through all of them". The pass-1 failure, a rat-sized whirl rolling like a wheel and
three tornados, is gone. What is still weaker than it could be: the per-enemy payoff is a
thin stroke at the waist, so the hit reads mostly through the white hurt flash and the numbers
rather than through the blade; a diagonal or heavier per-body slash would need a second painted
piece (a short slash mark), which I did not ask for.

## Open (owner)

- A short heavier slash mark for the per-body payoff, if the thin stroke is not enough.
- Sweep tip leaving the crop on a three-wide rack: trim `drift.toX` (0.22) or `overhang` (0.5) if it matters in a fight.
