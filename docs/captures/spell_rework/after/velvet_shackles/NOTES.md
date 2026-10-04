# Velvet Shackles, "after" (plan S3, spell 3.5)

Pass 1 (2026-10-04) used the old nine velvet frames and a procedural ring. Pass 2 (2026-10-04)
wires the owner's painted cinch sheet (`velvet_shackles_cinch.png`, recipe `velvet_shackles_cinch`,
keyed `green-soft`, 4 frames sharing one crop). Captured with `tools/gif.ps1 spell velvet_shackles`;
GIFs are untracked next to this file. Owner gates skipped; the calls below are mine.

## What changed

- One `cinch` layer plays the sheet at 20 fps from 0.2 s: f0 slack with the chain hanging
  (0.20 s), f1 half closed (0.25 s), **f2 closed and creased on the cue (0.30 s)**, f3 the rebound
  shimmer (0.35 s), held to 0.72 s and faded over 0.22 s. Size 290, opacity 0.9, glow 0.4.
- The ring accent, the oversized snap (`punch`) and the separate held copy are gone, and so are
  `shackle_cinch_ring` (folder, recipe, generator, test). The `wind` ribbons (old sheet f0-f4) stay
  as the wind-up, opacity 0.8 to 0.6, ending on the cue.
- Gloom 0.20 unchanged.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | Gloom rises to 0.20 over 0.15 s. Ribbons spiral round the body. From 0.20 s the painted cuff appears slack over the body and is pulled shut over the next 0.1 s. |
| 0.30 | **Payoff cue.** Closed, creased cuff (f2) with the sound and the Rooted badge. Ribbons gone. 10 petals settle. Gloom lifts (0.25 s). |
| 0.35-0.84 | Rebound shimmer, then the cuff is held while the badge counts, and fades from 0.72 s. |

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

My verdict: **passes, with a cost.** The contraction the stand-in faked is in the drawing now:
slack velvet with a hanging chain, drawn tight, pinched at a gold clasp. A viewer says "it got
bound". Costs: (1) the cuff is a big opaque velvet collar in front of the body and hides a rat
and a treant's whole trunk, face included, for about 0.5 s (1x treant clip), (2) it is
drawn in front, round the middle, so it reads as a velvet sash or scrunchie being cinched more
than as shackles on limbs, (3) the old ribbon spiral underneath is still the old drawing and
muddies f12-f17. Under `-Darkness 0.9` the cuff is the brightest thing on the stage and the
Round counter reads.

## Open (owner)

- Size or opacity of the cuff on large bodies (treant face); the ribbon wind-up could go now that
  the cuff arrives by itself.
- spell_feel was not re-run (cue and `seconds` unchanged); the flash and readability figures
  above are from pass 1.
