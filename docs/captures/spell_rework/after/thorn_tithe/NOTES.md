# Thorn Tithe, "after" (plan S3, spell 3.2)

Pass 1 (2026-10-04) used the 12-frame stand-in; pass 2 (2026-10-04) wires the owner's painted
four-frame growth sheet (`thorn_tithe_growth.png`, recipe `thorn_tithe_growth`, keyed
`green-soft`). Captured with `tools/gif.ps1 spell thorn_tithe`; GIFs are untracked next to this
file. Owner gates were skipped; the calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00 | Cast opens. Layer `grow` plays the sheet at 10 fps on the effects sort: f0 three shoots, f1 five vines (0.1 s), f2 full height (0.2 s). `back` plays the same frames mirrored, larger (400 vs 340), at opacity 0.5 on the ground sort, so thorns pass behind the body as well as in front. |
| 0.30 | **Payoff cue.** f3, the closed cage with a bead of sap at the tips, opens on the cue frame with the sound, the Thorned badge and 13 leaves. No accent layer: the cage is the payoff. |
| 0.30-0.72 | Held, then it sinks 0.15 of its height into the soil from 0.5 s and fades (0.14 s). Leaves clear at 0.72. |

## What changed on the way

- **An `at: hit` sprite is a tail.** The first wiring put the cage on its own `at: hit` layer. The
  renderer treats every such layer as a tail and shrinks it to a fraction of the body
  (`TargetBody.TailScale`), so the cage came out at 0.6 of the growth and the handover left an
  empty frame on the cue. The cage is now f3 of the one `grow` layer, which also removes the
  handover. Anything that must be full-size on the cue is `at: release` on the sheet's own clock.
- **Old accent removed.** The sap-burst star ("a generic sparkle" in pass 1) is gone.
- **`back` mirrored (the `facing: reverse` fix from the Court commit).** The sheet is near
  symmetric, so the mirror is a fringe of dimmer thorns behind the cage, not a butterfly. Kept.
- **Badge.** `impactY` 0.36 lifts the soil above the Thorned badge under the body (at 0.5 the
  vines covered it for about 0.5 s).

## Frame notes

- Rat (1x): the cage covers most of the rat for about 0.45 s (f18-f30); its eyes and tail read
  through. Treant (1x): face visible through the cage, base roots outside it.
- Growth reads tip by tip: shoots at f6-f8, full height by f14, cage on f18.
- Five casts at 1.5x on a treant: the plates return to idle between casts, no residue.
- Readability: plates, HUD and log uncovered; badge `3` visible from f18.
- Colour: the painted vines are blue-emerald with pale thorns, the green the plan asked for.
  Against the dark forest they are the saturated thing on stage, brighter than the old brown.
- Not re-measured with `spell_feel.ps1` this pass (hit cue and `seconds` are unchanged at 0.3 and
  0.72; the per-spell timing pin is `ThornTithePresentationTests`).

## Identity test: "green thorns from the ground"

My verdict: **passes.** Thorny vines thrust out of a mound of soil, thicken, and close into a
cage round the enemy; a viewer says "thorn vines grow up and cage it", which the stand-in's slide
never gave. Remaining weaknesses: the cage is large on a rat and hides it for half a second; the
ground-level sink at the end is small and the fade carries most of the exit.
