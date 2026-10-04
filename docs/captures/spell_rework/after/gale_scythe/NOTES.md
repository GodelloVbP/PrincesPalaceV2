# Gale Scythe, "after" (plan S3, spell 3.4)

Captured 2026-10-04 with `tools/gif.ps1 spell gale_scythe` and
`tools/spell_feel.ps1`, from existing assets only (the 12-frame wind sheet; no
new field, no new piece). GIFs are untracked next to this file. Owner gates
skipped; the calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.20 | **Quiet.** The sheet's first four frames, a small whirl closing in Shawn's hand. Nothing on the enemies (the old version put a ground whirl on every enemy from frame one). |
| 0.13-0.26 | **Sweep.** One crescent (f5) laid along the line of struck bodies (`place formation`, `align span`, overhang 0.5), carried from the caster's side of the front body to the last one (`drift`), fading out at the cue. Decorative. |
| 0.30 | **Shared payoff.** Every enemy at once: f5's crescent (size 300, was 380; punch 0.2, glow 1.2), sound, all numbers, the push in the turn order. 16 leaves. |
| 0.30-0.73 | The crescents trail off (f6-f11 at 14 fps). |

## Measured (spell_feel, 60 fps, lone enemy)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18-19) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.42 s | 0.40 s | about 0.28 s | 0.32 s |

No report WARNs. The rat 1.5x "clean at 0.72 s" is the rat's own idle after the
effect (the visible crescents are gone by about 0.28 s after the cue).

## Frame notes

- **One payoff frame for all enemies** (decision 8): on the full formation the
  three numbers (-7, -9, -9), the three crescents and the hurt flashes all open on
  the same frame. Two enemies and one enemy do the same. Nothing is staggered.
- **Flash guard.** Five casts at 1.5x on a three-rat formation (164 GIF frames):
  each cast is one cluster of three frames at 5-10% of the frame (cue) plus one
  small 3.5% after, casts about 1.1 s apart; nothing near a 25%-area large flash.
  At most one flash cluster a second.
- **Readability.** The old version put 380-box crescents on all three rats, a
  white wall that covered the numbers at f19-f21 (the -9s were white on white).
  At 300 the numbers sit above the crescents and read on every frame I looked
  at. On a treant the cut is still very large and clips off the top of the stage
  (fit target scales by the body); that was true of the old one too.
- **Push.** The turn-order change is not in these frames (the HUD strip is off
  the crop); unchanged by this pass.
- **Bug-shaped, not fixed:** none found.

## Identity test: "one cut across all of them"

My verdict: **partly fails.** What works: a quiet beat, a crescent that visibly
crosses the line front to back, and then every enemy is hit on the same frame;
a viewer sees a wind blade passing through the line. What fails: the sweep is a
rat-sized whirl rolling along the floor like a wheel, not a long blade, and the
three payoff shapes are three separate tornado whorls (the sheet's drawing),
not a straight reaping stroke. "One cut" would need a blade as long as the
line.

## What a painted still would fix

A single long thin crescent blade (prompt in
`docs/prompts/2026-10-03_spell_rework.md`) laid across the span by the same
`formation`/`span` layer replaces the rolling whorl, and a short slash mark for
the per-enemy payoff replaces three tornados.
