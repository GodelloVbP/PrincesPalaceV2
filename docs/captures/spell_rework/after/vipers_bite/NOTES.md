# Viper's Bite, "after" (plan S3, spell 3.6)

Pass 1 (2026-10-04) used the old jaws and a splash crown. Pass 2 (2026-10-04) wires the owner's
painted stills: `vipers_bite_lunge-v2.png` (v1 stays beside it, unused) and `vipers_bite_burst.png`
(recipes `vipers_bite_lunge`, `vipers_bite_burst`, both keyed `green-soft`: teal scales and
emerald venom would shift under the plain green clamp). Captured with `tools/gif.ps1 spell
vipers_bite` (also `-PreStatus Poison`, `-Darkness 0.9`, `-Repeat 5 -Speed 1.5`); GIFs are
untracked next to this file. Stage gloom 0.20 (owner O1). Owner gates skipped; the calls below
are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.12 | Gloom rises to 0.20 over 0.15 s. Nothing yet on the body. |
| 0.12-0.30 | **Lunge.** The painted head opens a box and a half left of the body (beside Shawn) and travels in, easing out, to bite the body at the cue. Opacity 0.95, no glow. Then it draws back (to -0.6 box) and fades (0.15 s) by 0.67 s. |
| 0.30 | **First payoff: the bite.** Sound, the "-3" number, the Poison badge, and the old sheet's f5 snap-starburst alone (punch 0.3, glow 1.25, 0.1 s held). 12 drops settle. The old fangs-withdrawing frames are gone. |
| 0.48 | **Second payoff, only with Poison already on the target.** The painted venom burst (plume and crown of droplets, rising from the body's base, size 420 `at: hit` so tail-scaled, glow 0.9, held 0.22 s then faded 0.2 s), the 16-particle spray, the second number ("-9") and `spore_cloud_impact`, `detonationSeconds` 0.18 after the cue. |

## The cut neck

The lunge art's neck leaves the canvas at the left edge: 818 px of the left column are opaque, a
straight vertical cut. It cannot be cropped away (the neck is the snake) so it is dissolved:
the slicer has a new recipe key `feather` (`{"left": 0.32}`: alpha smoothstepped to 0 over the
leftmost 32%), applied before the content crop. Result: no straight edge on any frame of the rat,
treant or darkness clips; the neck reads as a tail fading into the dark. The head also starts a
box and a half out, so the faded tail is the only thing that enters from the left. One honest
cost: at the layer's first frame (0.12 s) the head appears at full opacity between Shawn and
the body (a pop, there is no fade-in field), after which it travels in.

## What changed in the runtime, and a bug found

- **The detonation damage was never shown.** `ResolveDetonation` dealt the Poison's
  remaining worth through the ledger, but the beat's `Amount` held only the bite's own
  3, so the stage showed "-3" over a bar that fell by 12 (seen in the "before"
  capture of this pass). The beat now carries `DetonatedAmount`; the view draws it
  as a second Poison-coloured number `detonationSeconds` later, beside the first.
  This holds for every cast that sets off Poison (single target), not only Viper.
  Pinned in `SpellPresentationMechanismTests`.
- **A second payoff can have its own sound**: `SpellPresentation.secondSfxPath`,
  played for the detonation and for each staggered packet after the first (Ashen
  Reckoning uses it). Pinned in `FightBeatImpactClipTests`.
- Open: the bar already shows the whole loss on the cue frame, a beat before the
  "-9" appears. The two hits are one session resolution, so the bar cannot show
  them apart without staging vitals per packet. Not done.

## Frame notes (pass 1 counts, not re-measured)

- **First payoff on one frame.** On the rat at 1x the snap, the Poison badge and
  the log line all open on f18 (checked per frame). The number appears one to
  two frames later: the damage popup fades in by design.
- **Second payoff on one frame.** On the rat with Poison, f29 (0.18 s later) shows
  the crown splash, the spray and "-9" together; the second clip is scheduled on
  that same offset (pinned by test; I did not listen to it).
- **Flash guard.** Five casts at 1.5x on a treant (563 frames; casts 2-5 detonate
  the Poison the earlier ones left): one brightening of 4-5% of the frame per
  payoff, at most two per cast (the bite, then the pop 0.12 s later at 1.5x), at
  least 41 frames (0.68 s) between the two and more between casts. Nothing near
  a 25% large flash; at most 2 small flashes in any second.
- **Readability.** The numbers sit above the body and read on every frame I looked
  at, including under `-Darkness 0.9` (effect and number are the brightest things;
  the Round counter reads; the gloom changes nothing there, event darkness wins).
  The pale blob over the rat on the cue is the typed hit flash (the rat's silhouette
  in poison-white), which also hides the fangs for 2-3 frames; the Poison badge
  under the body is clear throughout.
- **Bug-shaped, fixed:** the missing detonation number above.

## Identity test: "a snake bit it"

My verdict: **passes, clearly.** A snake's head comes out of the dark beside Shawn, strikes the
enemy and withdraws; the numbers, badge and starburst land on the strike; with Poison the venom
erupts as a dark plume with a crown of droplets, a second number and a second sound. The old
version had no snake. What stays weak: the head is body-sized or larger (it covers a rat for
about 0.25 s on the cue, and the typed hit flash hides the snap itself for 2-3 frames); the
snap starburst is the old drawing and is a different style from the painted head; the venom burst
is nearly black on a dark stage and relies on its glints and the number to read; spell_feel was
not re-run (cue and `seconds` unchanged), and the flash/readability counts below are pass 1's.
