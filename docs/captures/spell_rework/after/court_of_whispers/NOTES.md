# Court of Whispers, "after" (plan S1, spell 3.1)

Captured 2026-10-03/04 with `tools/gif.ps1 spell court_of_whispers` and
`tools/spell_feel.ps1`, built from existing assets only (the court frames and
one procedural ring, `tools/make_spell_fx.py`). The GIFs are untracked next to
this file (regenerate with the same commands). Owner gates were skipped this
session; the calls below are mine and not reviewed by the owner.

## What the cast does, authored seconds

| t | Beat |
|---|---|
| 0.00 | Cast opens. Stage gloom rises to 0.35 over 0.2 s. Two translucent copies of the court sheet (one mirrored) slide in from both sides of every enemy to 0.25 box-widths off the body while the rising figures (f0-f4) play: the lean. |
| 0.30 | **Payoff cue** (`hitCueSeconds`, equal to the 0.3 s cast riser). Impact sound, Fear badges on all enemies, the ring-and-core frames (f5-f6) with a punch, a 12-spark settle. Gloom starts lifting (0.25 s). |
| 0.30-0.65 | The same two copies slide back out to 0.7 box-widths as the sheet dissolves: the courtiers recede. |
| 0.48 | **Price**: a faint violet ring on Shawn, and his Vulnerable badge appears (`casterCostSeconds` 0.18, held by `FightBeatPlayer`). |
| about 0.72 | Clean (measured). |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.40 s | 0.40 s | 0.30 s | 0.30 s |
| residue above 25% of peak | 0.38 s | 0.37 s | 0.27 s | 0.25 s |

The cue stays at the old 0.3 s: the cast riser is 0.3 s and `TheSpellCastCuesFitTheirBlows` pins that. A longer wind-up needs a recut riser, which I did not make or listen to.
Payoff to clean is close to Blackglass Spear's 0.4 s.

Reports: `spell_feel_1x/`, `spell_feel_1.5x/`. The report still WARNs on rat 1x:
"contact desync, biggest rise at f29, cue f18". f29 is the price ring on Shawn,
the intended second payoff, so this WARN is expected. The "no accent" WARN
cleared once the wind-up was dimmed (opacity 0.5, glow 0.3); at the first
blocking the wind-up was as bright as the payoff and the cue was not a peak.

## Frame notes

- **Fear lands on every enemy at once.** Before this pass the court was drawn on
  the front enemy only: `ResolveEnthrall` recorded no splash targets, so the beat
  had one target while the Fear landed on all. Fixed (a test pins it). Now all
  three rats and all three treants get the courtiers, and the Fear badge icons
  appear under every body on the cue frame (f18 at 1x), the same frame as the
  sound.
- **Shawn's price is after the payoff.** The ring peaks about f29-34 and his
  Vulnerable badge is on his plate from f29, 0.18 s after the cue.
- **Flash guard.** On the 5-cast 1.5x series (856 frames) the largest
  single-frame brightening covers 2.9% of the stage and there is one luminance
  peak per cast: at most 2 small flashes per cast (core, ring), none near the
  25%-area "large flash" size. Five casts at 1.5x is inside 3 per second.
- **Readability.** Enemy and party plates, the Fear badges and the log stay
  uncovered in every frame. Under the 0.9 darkness GIF the court and Shawn are
  the light sources and the Round counter reads. Court deals no damage, so there
  is no number to cover. The courtiers hide the treants' bodies from f12 to f28;
  the badges stay visible under the bodies.
- **Cost pulse visible:** yes, on every cast, but faint (opacity 0.6, thin ring).
  It reads as a halo, not as a cost.
- **Repeat series:** no smear between casts; the plates return to idle.
- **4:3:** `court_of_whispers_rat_full_4x3_1x.gif` recorded; not inspected beyond
  the plate layout being intact.

## Identity test: "they are whispering to them"

My verdict: **fails, partly**. A viewer would say "a violet curse swarms them",
not "they are whispering". What works: a wind-up with the stage dimming, hooded
shapes closing on each enemy, one bang, the shapes leaving, and a price after
it. What fails:

1. The courtiers are the same three-figures-per-frame drawing mirrored twice, so
   at the payoff they are a wall of six overlapping figures over a ring, not a
   figure bending to an ear. The lean is a slide, not a bend.
2. The violet ring and core at the foot read as a generic curse circle, the
   complaint the audit made of the old version. The ring is the strongest frame.
3. On a three-treant formation the effect is as wide as the formation and the
   bodies disappear under it for about 0.15 s.
4. Whispering has no gesture: nothing moves toward an ear.

## What a painted still would fix

The one leaning courtier (prompt in `docs/prompts/2026-10-03_spell_rework.md`)
replaces six overlapping figures with one figure per side whose silhouette is
the lean, so the identity test can pass, and the ring can shrink to a ground
shadow. Layer fields cannot make the bend.

## Open (owner)

- A 0.3 s wind-up is short for a 14-mana, tier-3 spell on cooldown 4; the lean has little time to read. Lengthening it needs a recut cast riser. Judged on GIFs only, not in a fight; sound not listened to.
