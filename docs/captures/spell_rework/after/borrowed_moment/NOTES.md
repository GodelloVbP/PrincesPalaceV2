# Borrowed Moment: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.9. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Painted chevrons landed (`a2c9b665`), drift stopped on the ally's own body (`546ebf3c`). Not yet through the workflow's steps 3-7. Captured 2026-10-04 with `tools/gif.ps1 spell borrowed_moment -Party shawn,odette,bjorn -Target <ally>` (1x; Bjorn at `-Aspect 4:3`) and one three-cast series at 1.5x. GIFs untracked beside this file.

## Identity

- **Verb:** hasten.
- **Signature beat:** three after-image chevrons point forward on the ally and slide ahead of it while its turn chip jumps up.
- **Must read as:** "time sped up for my ally".
- **Must NOT read as:** a dash only; a starburst.

## Beat table

Release clock. Cue 0.30 s = f18 at 1x, f12 at 1.5x. Payoff to clean 0.43 s at 1x, 0.30 s at 1.5x.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.30 | hourglass in its ring spins up on the ally, cyan swirl | turning | old sheet f0-f4 | opacity 0.85, glow 0.3, no fade |
| 0.30 | payoff: chevrons with sound (`borrowed_moment_impact`), log line, tracker moving the ally up | `quicken` | `borrowed_moment_quicken-v2.png` (three nested cyan arcs pointing right, sand streams, a gold clock hand) | 460; punch 0.2, glow 1.0, `facing: none`, `drift` -0.05 to 0.22 box forward over 0.3 s, fade 0.15 |
| 0.30-0.45 | grains and glints thrown up, gravity pulling up | particles | old sheet's four particles | 12 grains |

## Open items

1. Weak read: the chevrons read as "dash" more than "time"; the clock hand is a few pixels at game size and the sand streams vanish; the hourglass beat carries the time half. Identity test passes narrowly.
2. The art is not an afterimage of the ally's own sprite (the engine feature the prompt prefers is unbuilt).
3. Brightness: `spell_feel` peak 1.9 (1.8 at 1.5x), no visual contact frame found; the chevrons are thin cyan light on a busy body, so the cue is carried by the tracker and the log line. No warnings.
4. Bjorn is presentation-only: with shipped enemies (all slower than his 8) he is already next after Shawn's opening turn, so the session refuses him; the GIF shows the vfx with no log line or reorder. Shawn is presentation-only too (a Hasten never targets its caster, AllyTargeting).
5. A series of five casts is impossible: after three, both allies are next and the session has no legal target. When Odette was already next, the forced press took the first eligible ally; the log line names who.
6. Readability: chevrons cross the ally's chest and sit over the face edge for about 0.3 s; face stays visible. Turn chips are about 40 px at 1080p.
7. Flash guard: largest single-step brightening in the series is 6.2% of the frame (squad and enemy attacks); Borrowed's own cue peaks at 0.9%; at most 2 events of 3% or more in any second. Well inside the limit.

## Decisions

- 2026-10-04 (worker, owner gates skipped): painted `quicken-v2` is the payoff (v1 unused); the starburst and the breaking hourglass (f6-f11) are gone.
- 2026-10-04 (worker): `facing: none` so the chevrons point toward the enemy whichever side the ally stands.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Chevrons | `borrowed_moment_quicken-v2.png` | n/a | accepted (painted); v1 unused |
| Hourglass f0-f4, four particles | old sheet | n/a | stand-in |

No step-4 prompts: this pass predates the art workflow.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | after pass | turn tracker now records the queue before the move (`CombatBeat.PreTurnOrder`) and paints the new order on the impact; the chips change on f18 (the cue), not f0. Pinned in `BorrowedMomentPresentationTests` and `PositionCastTrackerTests` | worker | n/a |
| 2026-10-04 | after pass | the cast was never real in a capture; the "moves 2 places earlier" line and reorder now appear | worker | `e4adbb39` |
| 2026-10-04 | after pass | a refused ally (Hasten refuses whoever is already next) was recorded as somebody else; the scenario runner now says so and records the presentation only | worker | n/a |
| 2026-10-04 | handoff | chevrons open on the frame after the last hourglass drawing (cursor-compare bug left one empty frame; `../../README.md`) | worker | `d3c2c3b8` |
| 2026-10-04 | art | painted forward chevrons replace the starburst and breaking hourglass | worker | `a2c9b665` |
| 2026-10-04 | drift | drift 0.45 to 0.22 box: on Bjorn (leftmost) the chevrons reached Odette's wing; rechecked per frame on Bjorn at 4:3 and Odette, the tip stays on the ally's own body. Two earlier notes disagreed (0.27 / 0.55); the commit's 0.22 / 0.45 wins | worker | `546ebf3c` |

## Measures

Not recorded (pre-workflow). Timing: cast start to payoff 0.30 s (f18) at 1x, 0.20 s (f12) at 1.5x.

## Snapshot

- **Commit:** `546ebf3c`.
- **Capture run id:** not recorded.
- **Binding checks / rubric:** not scored.
- **Still weak:** see Open items 1-3.
