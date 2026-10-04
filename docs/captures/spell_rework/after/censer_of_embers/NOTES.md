# Censer of Embers, "after" (plan S3, spell 3.10)

Captured 2026-10-04 with `tools/gif.ps1 spell censer_of_embers -Vs <enemy>` (1x) and series at
1.5x. The payoff is the painted `censer_of_embers_catch.png`; the swing frames (f0-f4) and the
ember and flame particles are the old sheet's; the smoke is not drawn. GIFs are untracked next to this file. Owner
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
| 0.30 | **Payoff.** The painted crescent of embers (`catch`, keyed still, 1701x619 after the crop) laid at the target's feet (`place: target`, 420 box scaled to the body, punch 0.2, glow 1.0), held 0.25 s and faded over 0.2 s, with the sound (`censer_of_embers_impact`) and the Burn badge on the target. Tongues are tallest at the two ends; the centre is open. 16 coals and flame tongues are thrown off (lifetimes 0.22-0.36 s). |
| 0.35-0.67 | **Take hold.** Six small flames lick up the body (0.05 s after the cue, up to 0.32 s long). |

The sheet's f5 column and smoke frames (f6-f9) and the smoke wisp particle are no longer drawn.

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| contact | f18 | f18 (visual f19) | f12 | f12 |
| payoff to clean | 0.38 s | 0.38 s | 0.48 s | 0.50 s |

WARNs: "no accent" on all four (peak 2.8-7.7 against a typical level of 2.2-6.9) and, on the
1.5x rat, "contact desync" (visual f40). The peaks sit before the cue (0.18-0.25 s, the swing's
sparks), because the crescent is low, wide and dim next to the swinging censer. A run with punch
0.5 and glow 1.5 raised the treant 1.5x peak only from 7.7 to 8.1 and lowered the rat's, so it
was reverted: the flat crescent does not make a flash, the Burn badge and the coal glow after it
carry the cue. The 1.5x residue of 0.48 s is the target's coal glow (the plan's 3.11 piece, not
this spell) and the 0.45 s crescent; at 1x the same glow is under the 0.38 s.
An earlier run on the rat at 1x with the same numbers had no warnings, so the accent check is
borderline here.

## Frame notes

- **Burn badge on the cue.** On the treant and the rat the badge ("3") is absent on frame 17
  and present on frame 18, the cue, under the body's feet, with the flame frame opening on the
  same frame. Burn has no damage number on the cast.
- **Flash guard.** The cue frame brightens 0.4% of the frame on the rat and 1.1% on the treant
  (area gaining 10% luminance in one step). Over the 1.5x treant series (about 9 s, five casts)
  the largest step anywhere is 4.4% and at most one such event falls in a second; none is the
  cast. One small flash per cast.
- **Readability.** The crescent sits on the ground line and spans the treant's width; the
  treant's face is clear on every frame (the old column hid it for about 0.2 s). On the rat the
  crescent is about as wide as the rat and its tongues reach the rat's chest. The badge sits below
  it and the plates and log are clear.
- **One empty frame** (about 16 ms) between the last swing drawing and the crescent (the cue
  clock lags the release clock by a frame).
- **Size.** The crescent is low by design (40% of its canvas at most); at 420 it reads as a
  ring of fire at the feet, closer to "campfire ring" than to "set alight" on the treant, whose
  trunk only starts to glow from the coal glow after it.
- **Series.** Five casts on the treant at 1.5x. The rat dies of its own burn on the fifth, so
  that series is four casts (a stack reads "12 damage each turn start across 2 stacks").
- **Not looked at:** `-Darkness`; a back-rank target (single-target, same layers).

## Identity test: "set alight"

Verdict: **passes, weaker on tall targets.** A lantern swings, a crescent of flame and coals
stands up at the target's feet on the frame the badge appears, the coals fly off, and the coal
glow takes the body after. The face stays clear, which was the defect. What is weak: the
crescent is a ground effect, so the target is "standing in fire" for about 0.3 s before the coal
glow makes it "burning"; on the treant the flames reach only the roots. It does not read as a
wall of fire or an explosion, as asked. Nothing painted shows the body itself catching; that is
the coal glow's job.
