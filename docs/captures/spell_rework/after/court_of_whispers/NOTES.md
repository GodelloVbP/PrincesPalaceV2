# Court of Whispers, "after" (plan S1, spell 3.1)

Pass 1 (2026-10-03/04) used existing assets only. Pass 2 (2026-10-04) wires the owner's
painted leaning courtier (`court_of_whispers_courtier-v2.png`; v1 stays beside it, unused).
Captured with `tools/gif.ps1 spell court_of_whispers` and `tools/spell_feel.ps1`. The GIFs
are untracked next to this file (regenerate with the same commands). Owner gates were
skipped; the calls below are mine and not reviewed by the owner.

## What the cast does, authored seconds

| t | Beat |
|---|---|
| 0.00 | Cast opens. Stage gloom rises to 0.35 over 0.2 s. Two copies of the one courtier still (`Spells/court_of_whispers_courtier/f0`: `lean-a` near, `lean-b` the mirror, far, authored first so the near one draws over it) slide in from both sides of every enemy to 0.25 box-widths off the body. The hood and cupped hand land at the enemy's head. |
| 0.30 | **Payoff cue** (`hitCueSeconds`, equal to the 0.3 s cast riser). Impact sound, Fear badges on all enemies, the old ring-and-core frames (f5-f6), now 340 box instead of 460 and glow 1.3 instead of 1.9, with a punch, a 12-spark settle. Gloom starts lifting (0.25 s). |
| 0.30-0.65 | The courtiers slide back out to 0.7 box-widths and fade (0.1 s). |
| 0.48 | **Price**: a faint violet ring on Shawn, and his Vulnerable badge appears (`casterCostSeconds` 0.18, held by `FightBeatPlayer`). |
| about 0.72 | Clean (measured). |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.42 s | 0.43 s | 0.30 s | 0.30 s |
| residue above 25% of peak | 0.37 s | 0.37 s | 0.25 s | 0.25 s |

The cue stays at 0.3 s: the cast riser is 0.3 s and `TheSpellCastCuesFitTheirBlows` pins that.
Reports: `spell_feel_1x/`, `spell_feel_1.5x/`. The report WARNs only on rat 1x: "contact
desync, biggest rise at f29, cue f18". f29 is the price ring on Shawn, the intended second
payoff, so the WARN is expected. 1.5x is clean on both targets.

## Frame notes

- **Fear lands on every enemy at once**, courtiers on all three bodies, badges under every
  body on the cue frame.
- **The lean is now the drawing.** On the rats each near courtier's hood sits against the rat's
  head with the cupped hand at the ear: "bending close to speak" reads without the slide. On
  the treants the hood meets the treant's face.
- **Two findings on the way.** `facing: reverse` was parsed but never applied, so the old pass's
  mirrored copy drew un-mirrored on the same side as the first: three figures stacked on
  three, not "both sides". Fixed in `SpellLayerInstance.DrawFacing` (pinned by
  `AReverseLayerDrawsAgainstTheCast`); Thorn Tithe's `back` layer is now actually mirrored as
  authored. And the ground band holds only 2 renderers (`FightHudSpec.SpellGroundRenderers`),
  so the far courtier could not go behind the bodies: it is an effects layer at opacity 0.45,
  drawn under the near one.
- **Shawn's price is after the payoff.** The ring peaks about f29-34 and his Vulnerable badge
  is on his plate from f29, 0.18 s after the cue.
- **Flash guard.** On the 5-cast 1.5x series (851 frames) the largest single-step brightening
  (more than 0.1 luminance, measured on the GIF at every second frame, so an upper bound)
  covers 6.4% of the stage crop, nowhere near the 25% "large flash" size. Mean luminance stays
  within 0.10-0.14. The core is the only bright event; five casts at 1.5x are inside 3 flashes
  a second.
- **Readability.** Enemy and party plates, the Fear badges and the log stay uncovered. Under
  the 0.9 darkness GIF the courtiers (opacity 0.7) and Shawn are the light sources and the
  Round counter reads. No damage number to cover.
- **Bodies hidden by the court.** The near courtier covers the front half of its rat for about
  0.4 s (the rat shows through at 0.7 opacity); on the three-treant rack the courtiers overlap
  the neighbouring treant's left flank. The far courtier of the rightmost enemy leaves the
  stage crop at the right edge in the 1x clips (the 4:3 clip shows it).
- **Mixed rack (rat, treant, beetle):** the beetle sits behind the treant and its courtier is
  barely seen, as before; the cause is the formation.
- **Cost pulse** still faint (opacity 0.6, thin ring), reads as a halo.
- **Repeat series:** no smear between casts; the plates return to idle.

## Identity test: "they are whispering to them"

My verdict: **passes, with two caveats.** A viewer sees hooded figures bending to each
enemy's head, a violet hush, and the figures drawing back: someone whispering into an ear,
which the stand-in never gave. The caveats:

1. The ring-and-core frames (f6-f7 of the old sheet, a violet vortex with lightning) are still
   the loudest frame of the cast and read as a curse circle rather than a hush. Smaller and
   dimmer now, they are still the brightest thing. The prompt's "ground shadow" would be a new
   still (a painted shadow or a faint breath of mist); I did not make one.
2. The far courtier is the mirror of the near one, so it whispers at the enemy's back (the rats
   face Shawn), and on a tight rack it reads as the neighbour's courtier. Dropping `lean-b` is
   the cleaner read if the owner prefers one courtier per enemy; the pair is what the prompt
   asked for.

## Open (owner)

- A 0.3 s wind-up is short for a 14-mana, tier-3 spell on cooldown 4; the lean has little time to read. Lengthening it needs a recut cast riser. Judged on GIFs only, not in a fight; sound not listened to.
- Keep or drop the far courtier (`lean-b`); replace the vortex with a ground shadow (needs a new still).
