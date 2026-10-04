# Viper's Bite, "after" (plan S3, spell 3.6)

Captured 2026-10-04 with `tools/gif.ps1 spell vipers_bite` (also `-PreStatus Poison`,
`-Darkness 0.9`, `-Repeat 5 -Speed 1.5`) and `tools/spell_feel.ps1` (no Poison
there). No painted art: the existing twelve bite frames and four venom particles.
Stage gloom 0.20 (owner O1). GIFs are untracked next to this file. Owner gates
skipped; the calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | **Lunge.** Gloom rises to 0.20 over 0.15 s. The open jaws (f0-f4) drop onto the body from half a box above, easing out; opacity 0.85, glow 0.2. Gone on the cue (no fade). |
| 0.30 | **First payoff: the bite.** Sound, the "-3" number, the Poison badge, f5 (jaws snapping shut in a starburst, punch 0.3, glow 1.25) then the fangs withdrawing and the venom dripping (f6-f11, 0.42 s). 12 drops settle. |
| 0.48 | **Second payoff: the detonation, only when Poison was already on the target.** A crown-shaped venom splash (particle f2, punch 0.4, glow 1.6) plus a 16-particle acid spray, a second number ("-9" on a 3 x 3 Poison), and a second sound (`spore_cloud_impact`, `secondSfxPath`), `detonationSeconds` 0.18 after the cue. |

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

## Measured (spell_feel, 60 fps, no Poison)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.38 s | 0.38 s | 0.45 s | 0.25 s |

No report WARNs. With Poison the detonation extends the tail to about 0.7 s after
the cue (frames 29-34 at 1x).

## Frame notes

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

My verdict: **passes, narrowly.** Jaws drop onto it, snap shut in a starburst, the
fangs pull out dripping, and the number appears: "something bit it with venom". The
second payoff reads as "the poison went off" because the crown splash and the extra
number are distinct from the bite. What stays weak: there is no snake, only a pair
of jaws and green smoke, so "a snake" is inferred from the fangs; and the hit
flash covers the snap itself for 2-3 frames.

## What a painted still would fix

A snake head mid-lunge and a venom-burst still (prompt in
`docs/prompts/2026-10-03_spell_rework.md`) would put a creature in the strike and
give the detonation a plume rather than a splash crown.
