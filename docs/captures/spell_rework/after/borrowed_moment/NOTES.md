# Borrowed Moment, "after" (plan S3, spell 3.9)

Captured 2026-10-04 with `tools/gif.ps1 spell borrowed_moment -Party shawn,odette,bjorn -Target <ally>`
(1x; Bjorn at `-Aspect 4:3`) and one three-cast series at 1.5x. The payoff is the painted
`borrowed_moment_quicken-v2.png` (v1 unused); the turning hourglass (f0-f4) and the four particles are the old sheet's. GIFs are untracked next to this file. Owner gates
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
| 0.30 | **Payoff.** The painted chevrons (`quicken`, three nested cyan arcs pointing right, sand streams, a gold clock hand) on the ally with the sound (`borrowed_moment_impact`), the log line and the tracker moving the ally up. 460 box, punch 0.2, glow 1.0, `facing: none` so they point toward the enemy whichever side the ally stands, `drift` 0.45 box forward over 0.3 s, then a 0.15 s fade. |
| 0.30-0.45 | **Grains.** 12 grains and glints thrown up with gravity pulling up. The breaking hourglass (f6-f11) and the starburst are gone. |

## Measured (spell_feel vs rat, presentation-only; 60 fps)

| | 1x | 1.5x |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.43 s | 0.30 s |

No warnings, but the peak is 1.9 (1.8 at 1.5x) and the tool finds no visual contact frame: the
chevrons are thin cyan light on a busy body, so the cue is carried by the tracker and the
log line more than by brightness.

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
- **Flash guard.** The chevrons are thin and drift; there is no starburst any more. Well inside the limit.
- **One empty frame** (about 16 ms) between the last hourglass drawing and the chevrons (the cue clock lags the release clock by a frame).
- **Drift.** On Bjorn (front of the line, leftmost) the chevrons slide right across the gap and onto the edge of Odette's wing. They read as forward; the overlap is brief.
- **Readability.** The chevrons cross the ally's chest and sit over the face edge for about 0.3 s, the face stays visible; no number is under it (a
  Hasten has no damage number); the log line and plates are clear.

## Identity test: "time sped up for my ally"

Verdict: **passes, narrowly.** The hourglass spins up, and on the cue three after-image
chevrons point forward on the ally and slide ahead of it while its chip jumps up. That puts
"faster" on the body, which the old sheet could not. What is weak: the chevrons are legible
as "dash" more than "time" (the clock hand is a few pixels at game size, and the sand streams
vanish), so the hourglass beat before it carries the time half; the art is not an afterimage of
the ally's own sprite (the engine feature the prompt prefers is still unbuilt).
