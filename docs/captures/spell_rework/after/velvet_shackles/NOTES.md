# Velvet Shackles, "after" (plan S3, spell 3.5)

Captured 2026-10-04 with `tools/gif.ps1 spell velvet_shackles` and
`tools/spell_feel.ps1`. No painted art: the existing nine velvet frames, a
procedural ring (`tools/make_spell_fx.py shackle_cinch_ring`) and the stage gloom
(0.20, owner O1). GIFs are untracked next to this file. Owner gates skipped; the
calls below are mine.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | **Wind-up.** Stage gloom rises to 0.20 over 0.15 s. The ribbons (f0-f4) rise and spiral round the body, drawn larger than the final cuff (470 box). Opacity 0.8, no glow. The layer ends on the cue with no fade. |
| 0.30 | **Payoff cue.** Sound, Rooted badge (the count under the body), the cuff (f5-f6) opened 50% oversized and snapped in, and the accent: a gold-white ring drawn twice as wide that is pulled onto the body in two frames, glow 1.5. 10 petals settle. Gloom starts lifting (0.25 s). |
| 0.42-0.84 | **Held cuff.** The cuff frame stays on the body (opacity 0.85, 0.3 s) and fades over 0.22 s. The one leftover on purpose: the bind the badge is counting down. |

## Measured (spell_feel, 60 fps)

| | 1x rat | 1x treant | 1.5x rat | 1.5x treant |
|---|---|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.30 s | 0.20 s (f12) | 0.20 s |
| payoff to clean | 0.38 s | 0.38 s | 0.47 s | 0.32 s |

No report WARNs. "Clean" is activity-based, so the held cuff (static) does not
count against it; the cuff is visible for about 0.64 s after the cue by
construction. Payoff to clean is therefore longer than Blackglass's 0.4 s on
purpose (plan: end on the held cuff). The 1.5x rat 0.47 s is the rat's own idle.

## Frame notes

- **One frame.** On the treant at 1x the ring and the cuff open on f18, the same
  frame as the Rooted badge and the log line (checked per frame; before the
  cue-clock fix in dbea77ae the beat's side trailed the layers by two frames).
- **Flash guard.** Five casts at 1.5x on a treant (487 frames): one brightening
  of 5.7% of the frame per cast (frames 12, 148, 229, 365, 446, at least 81
  frames, 1.35 s apart), nothing near a 25% large flash. At most one flash a
  second.
- **Readability.** No number (the spell deals no damage). The Rooted badge sits
  under the body and is clear on every frame after the cue, including under
  `-Darkness 0.9`, where the effect is the brightest thing in frame and the
  "Round 1" counter reads. The spell's gloom changes nothing there (event
  darkness wins, `max`). Plates, log and HUD stay uncovered. 4:3 recorded
  (`velvet_shackles_rat_4x3_1x.gif`), plate layout intact, not looked at closer.
- **The body.** The cuff hides the rat's torso for about 0.4 s and the treant's
  trunk for as long; the face stays visible above it.
- **Bug-shaped, not fixed:** none found.

## Identity test: "tied down"

My verdict: **passes.** Ribbons wind round it, the cuff snaps shut with a bright
ring, and the cuff stays on it while the badge counts: a viewer says "it got
bound". What is weak: the ring is a perfect geometric circle, a UI ring more than
a restraint, and it is the loudest thing in the cast; the cuff itself is a still
pair of rings that sits on the body rather than cinching it (the contraction is
the ring's, not the cuff's). The ribbon wind-up is the old sheet's drawing,
unchanged.

## What a painted still would fix

A 4-frame cinch sheet (slack loops, pulled half closed, closed, rebound; prompt in
`docs/prompts/2026-10-03_spell_rework.md`) would put the contraction in the
ribbon and chain themselves, so the procedural ring can go or shrink to a spark.
