# Thorn Tithe, "after" (plan S3, spell 3.2)

Captured 2026-10-04 with `tools/gif.ps1 spell thorn_tithe` and
`tools/spell_feel.ps1`, from existing assets only (the 12-frame thorn sheet,
no new art, no new generator piece). GIFs are untracked next to this file.
Owner gates were skipped this session; the calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00 | Cast opens. Two arcs of the same sprout: the near copy on the effects sort, a mirrored dimmer, slightly larger copy on the ground sort (behind the body). Both start a tenth of their height below the floor and rise while the vines grow (f0-f4). |
| 0.30 | **Payoff cue.** Sound, Thorned badge and `accent`: f5's green-white sap burst alone, punch 0.3, glow 1.9, held 0.1 s. The cage closes round the body, front arc over it, back arc behind. 13 leaves settle. |
| 0.30-0.72 | The cage holds two frames then sinks into the floor (f8-f11), leaves clear by 0.72. |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.40 s | 0.40 s | about 0.27 s by eye | about 0.27 s |

The report's 1.5x "residue tail" WARN (0.57 s) is a measurement artefact: its
peak lands at 0.70 s, after the effect has cleared (the rat's idle ring pulse),
so the 25%-of-peak line is set too low. In the frames the cage is gone by
frame 28, 16 frames (0.27 s) after the cue. The 1x rat "no accent" WARN stays:
the sheet's own star frames f5-f7 keep the whole cage bright for about 0.1 s
after the cue, so the cue is not a sharp peak against them (ratio 1.35 vs 1.5).
On the treant it passes.

## Frame notes

- **Flash guard.** The five-cast 1.5x series (treant, 296 GIF frames) has one
  cue brightening per cast of about 9% of the frame (pixels up by more than
  10% luminance), at least 45 GIF frames (1.5 s) apart, and nothing near the
  25%-of-view size of a large flash. At most one flash per 1.5 s.
- **Readability.** Plates, the HUD and the log are never covered. The Thorned
  badge (3) shows 1-2 frames (under 35 ms) after the sap burst, not on the
  same frame. Thorn Tithe deals no damage on cast, so there is no number.
- **The body.** On the rat the cage hides the body between f17 and f26 (0.17 s);
  the same on the treant, whose face stays visible above the crossing vines.
- **Rat repeat is not recordable**: `-Repeat 5` on a lone rat fails ("the
  player's turn never came back before cast 3"): Thorned kills it by the third
  cast. The series is on the treant.
- **Bug-shaped, not fixed:** none found.

## Identity test: "green thorns from the ground"

My verdict: **passes, narrowly.** The vines rise out of the floor and close
round the body with the cage straddling it (behind and in front), then sink
back; a viewer says "thorn vines grow around it". The green is the sap
lightning between the vines and the single burst frame, not green thorns:
the vines themselves are brown. What does not pass: the star at the cue is a
generic sparkle and the mirrored back arc makes a symmetric butterfly shape at
f19-f23. Verb *sprout* is carried by the rise, not by growth you can see
tip by tip.

## What a painted still or sheet would fix

Growth cannot be faked: a 4-frame sheet of tips thrusting up and thickening
(prompt in `docs/prompts/2026-10-03_spell_rework.md`) replaces the slide-up
with real growth, and gives the back arc its own drawing so it is not a mirror.
