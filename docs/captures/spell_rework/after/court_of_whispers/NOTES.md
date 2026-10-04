# Court of Whispers, "after" (plan S1, spell 3.1)

Pass 1 (2026-10-03/04) used existing assets only. Pass 2 (2026-10-04) wires the owner's
painted leaning courtier (`court_of_whispers_courtier-v2.png`; v1 stays beside it, unused).
Captured with `tools/gif.ps1 spell court_of_whispers` and `tools/spell_feel.ps1`. The GIFs
are untracked next to this file (regenerate with the same commands). Owner gates were
skipped; the calls below are mine and not reviewed by the owner.

## What the cast does, authored seconds

| t | Beat |
|---|---|
| 0.00 | Cast opens. Stage gloom rises to 0.35 over 0.2 s. One courtier still (`Spells/court_of_whispers_courtier/f0`, layer `lean-a`) slides in at every enemy to 0.25 box-widths off the body. The hood and cupped hand land at the enemy's head. |
| 0.30 | **Payoff cue** (`hitCueSeconds`, equal to the 0.3 s cast riser). Impact sound, Fear badges on all enemies, the old ring-and-core frames (f5-f6), now a 260 box (was 460) at glow 0.7 (was 1.9) and punch 0.12, so the courtier carries the payoff, with a punch, a 12-spark settle. Gloom starts lifting (0.25 s). |
| 0.30-0.65 | The courtier slides back out to 0.7 box-widths and fade (0.1 s). |
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
  authored. The ground band holds only 2 renderers (`FightHudSpec.SpellGroundRenderers`), so
  nothing per enemy can go behind the bodies.
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
  the neighbouring treant's left flank.
- **Mixed rack (rat, treant, beetle):** the beetle sits behind the treant and its courtier is
  barely seen, as before; the cause is the formation.
- **Cost pulse** still faint (opacity 0.6, thin ring), reads as a halo.
- **Repeat series:** no smear between casts; the plates return to idle.

## Identity test: "they are whispering to them"

Follow-up (2026-10-04): `lean-b` (the mirrored far courtier) is dropped and the ring-and-core
payoff is demoted (260 box, glow 0.7). It stays on effects: the ground band holds two renderers
and a rack needs three. Rat and mixed-rack frames (cue f18): one courtier per enemy, hood at the
head, no neighbour's courtier. The pair of courtiers that read as the neighbour's is gone.

My verdict: **passes.** Hooded figures bend to each head, a violet hush, the figure draws back.
Remaining weakness: the demoted core is still the brightest frame of the cast (f20-24 on rats
and the mixed rack, a violet bowl at each body's feet) and still covers the lower half of the
rat; it is smaller and dimmer but reads as a circle until it fades. Removing it outright
would drop the only accent that lands exactly on the cue frame beside the sound; I kept it. A
painted ground mist would replace it (new still).

## Open (owner)

- A 0.3 s wind-up is short for a 14-mana, tier-3 spell on cooldown 4; the lean has little time to read. Lengthening it needs a recut cast riser. Judged on GIFs only, not in a fight; sound not listened to.
- Replace the vortex with a ground shadow or mist (needs a new still), or drop it.
