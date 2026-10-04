# Censer of Embers, "after" (plan S3, spell 3.10)

Captured 2026-10-04 with `tools/gif.ps1 spell censer_of_embers -Vs <enemy>` (1x) and series at
1.5x. No painted art and no new pieces: the existing swing frames, the flame frame and the
ember and flame particles; the smoke is not drawn. GIFs are untracked next to this file. Owner
gates skipped; the calls below are mine. The lingering coal glow is not part of this spell.

## What the tools had wrong, fixed here

- **A pin on an emitter's weights read nothing.** `SpellVfxJson` blanked everything inside an
  array before reading it, so `emitter.weights` was null for every spell and every test built on
  it passed without checking. It reads the numbers from the real block now; the Aegis, Borrowed
  and Censer pins use it, and `SpellVfxRecipeDriftTests` still passes with real weights in hand.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | **Swing.** The censer (f0-f4) swings across the target on its chain, sparks trailing. Opacity 0.9, glow 0.5, no fade. |
| 0.30 | **Payoff.** f5, the embers catching in a column of flame (punch 0.2, glow 1.6), held 0.14 s and faded over 0.08 s, with the sound (`censer_of_embers_impact`) and the Burn badge on the target. 16 coals and flame tongues are thrown off (lifetimes 0.22-0.36 s). |
| 0.35-0.67 | **Take hold.** Six small flames lick up the body (0.05 s after the cue, up to 0.32 s long). Last thing on screen at 0.67 s. |

The sheet's smoke frames (f6-f9) and the smoke wisp particle are no longer drawn.

## Measured (timing.json, 60 fps)

| | 1x | 1.5x |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.37 s (cleared 0.67 s) | 0.25 s |

Before this pass the particles lived to about 0.7 s after the cue, so payoff to clean was about
0.7 s: the column's smoke and a drift of embers outlasted the payoff by more than twice its length.

## Frame notes

- **Burn badge on the cue.** On the treant and the rat the badge ("3") is absent on frame 17
  and present on frame 18, the cue, under the body's feet, with the flame frame opening on the
  same frame. Burn has no damage number on the cast.
- **Flash guard.** The cue frame brightens 0.4% of the frame on the rat and 1.1% on the treant
  (area gaining 10% luminance in one step). Over the 1.5x treant series (about 9 s, five casts)
  the largest step anywhere is 4.4% and at most one such event falls in a second; none is the
  cast. One small flash per cast.
- **Readability.** The flame column (350 box, scaled to the body) covers the target's whole
  body for about 0.2 s; the badge sits below it and the plates and log are clear. On the treant
  the column hides the face while it lasts.
- **Series.** Five casts on the treant at 1.5x. The rat dies of its own burn on the fifth, so
  that series is four casts (a stack reads "12 damage each turn start across 2 stacks").
- **Not looked at:** `-Darkness`; a back-rank target (single-target, same layers).

## Identity test: "set alight"

My verdict: **passes.** A lantern swings, flame stands up on the body on the same frame the
badge appears, and the coals fly off. Nothing else about it reads as anything but fire.

What is weak: f5 still carries its own small smoke wisp at the top of the column, so the smoke is
trimmed, not gone; and the "take hold" flames are small and mostly hidden behind the column at
this size.

## What a painted still would fix

A painted still of the target's body already burning at the edges (flames licking its outline,
no column, no smoke) would let the payoff show the body catching, not a column drawn in front of
it. The coal glow that follows (a separate task) is where that belongs.
