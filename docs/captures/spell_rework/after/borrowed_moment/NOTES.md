# Borrowed Moment, "after" (plan S3, spell 3.9)

Captured 2026-10-04 with `tools/gif.ps1 spell borrowed_moment -Party shawn,odette,bjorn -Target <ally>`
(1x; Bjorn at `-Aspect 4:3`) and one three-cast series at 1.5x. No painted art: the existing
twelve hourglass frames and four particles. GIFs are untracked next to this file. Owner gates
skipped; the calls below are mine.

## What the runtime and the tools had wrong, fixed here

- **The tracker moved before the cast.** A beat's `TurnOrder` is taken when it closes, so the
  ally's new place showed from the first frame of the cast, ahead of the sound, the log line and
  the starburst. The beat now also records the queue before the move (`CombatBeat.PreTurnOrder`),
  opens on it and paints the new one on the impact, like a swap's formation. Pinned in
  `BorrowedMomentPresentationTests` (session) and `PositionCastTrackerTests` (the paint order).
  In the Odette GIF the chips change on frame 18, the cue; before, they had changed on frame 0.
- **The cast was never real in a capture** (commit e4adbb39): the "moves 2 places earlier" line
  and the reorder are now in the GIFs because the cast is real.
- **A refused ally was recorded as somebody else.** The forced press takes the first eligible ally
  when the aimed one is refused (a Hasten refuses whoever is already next), so a "Bjorn" GIF
  showed Odette. The scenario runner now says so and records the presentation only.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | **Turning.** The hourglass in its ring (f0-f4) spins up on the ally, cyan swirl around it. Dim (opacity 0.85, glow 0.3), no fade. |
| 0.30 | **Payoff.** f5, the cyan clock-hand starburst (glow 1.7, punch 0.3), held 0.1 s with the sound (`borrowed_moment_impact`), the log line, and the tracker moving the ally up. |
| 0.38-0.63 | **Sand.** The hourglass breaks (f6-f11, 24 fps) and the sand rises a quarter of its box off the ally; 12 grains and glints are thrown up with gravity pulling up, so they quicken. Clean at 0.69 s. |

## Measured (timing.json, 60 fps)

| | 1x | 1.5x |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.39 s authored (freed f42 = 0.70 s total) | 0.26 s |

Before this pass the sheet ran 0.72 s plus a 0.14 s fade from the cast's start, so payoff to
clean was about 0.56 s.

## Frame notes

- **Visible turn-order change.** Odette on the 1x GIF: the chips read Shawn, Bjorn, rat, Odette
  until frame 17 and Shawn, Odette, Bjorn, rat on frame 18. The chips are about 40 px at 1080p;
  the log line ("Odette moves N places earlier in the order.") and the starburst are what the eye
  is on, and the chips are what proves it.
- **Ally anchoring: confirmed on Odette**, a real cast. **Bjorn is presentation-only**: with the
  shipped enemies (all slower than Bjorn's 8) he is already next after Shawn's opening turn, so
  the session refuses him; the GIF shows the vfx on him, with no log line or reorder. **Shawn is
  presentation-only**: a Hasten never targets its caster (AllyTargeting), so that GIF only checks
  where the effect sits. The vfx is centred on the chosen body in all three.
- **Series.** Five casts in a row are not possible: after three, both allies are already next and
  the session has no legal target (the fourth stalled in two attempts). Three are recorded at 1.5x;
  when Odette was already next, the press took the first eligible ally, and the log line in the
  GIF names who. Largest single-step brightening in the series is 6.2% of the frame, from the
  squad's and enemies' own attacks; Borrowed's own cue peaks at 0.9%. At most 2 events of 3% or more fall in any
  second.
- **Flash guard.** One bright event per cast (the starburst), small in area: inside the limit.
- **Readability.** The starburst covers the ally's body for 0.1 s; no number is under it (a
  Hasten has no damage number); the log line and plates are clear.

## Identity test: "time sped up for my ally"

My verdict: **partly passes.** An hourglass spins up, bursts into cyan clock hands and sand
that rises, and the ally's chip jumps ahead on the same frame. Read alone, the vfx says "time
magic on this ally", not "faster": the hourglass and the sand are the sheet's old drawing, and
nothing on the body itself quickens (no afterimage, no speed lines). The tracker move on the cue
is what finishes the sentence.

## What a painted still would fix

An afterimage still of an ally mid-stride, doubled and cyan-edged, would put the "quicker"
on the body itself instead of in a 40 px chip.
