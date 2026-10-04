# Gilded Aegis, "after" (plan S3, spell 3.8)

Captured 2026-10-04 with `tools/gif.ps1 spell gilded_aegis -Party shawn,odette,bjorn -Target <ally>`
(1x; Bjorn with `-Aspect 4:3`) and one three-cast series at 1.5x (the five-cast series
fails in the squad's pacing, see below). The lock is the painted `gilded_aegis_lock.png`; the shards
(f0-f4 of the old sheet), the leaf/glint particles and the procedural mote
(`tools/make_spell_fx.py aegis_mote`) are unchanged. GIFs are untracked next to this file. Owner gates
skipped; the calls below are mine.

## What the runtime and the tools had wrong, fixed here

- **The ward was never a real cast in a capture** (commit e4adbb39): the preview stage refuses
  Ward, so every earlier GIF drew the vfx with no ward bar, badge or log line. A Ward on a chosen
  ally (or on Shawn) is now a real cast. The ward bar and the ward icon on the ally's plate are
  on the cue frame in every GIF below.
- **A series with a squad stalled**: a squadmate's turn between two casts waited for a hand, and
  the forced press was refused on a kit without the spell. The series now has the squadmate cast
  their own first enemy-aimed spell, so five casts record in a real fight.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.20 | **Delivery.** A white-gold mote (procedural, soft comet tail) flies from Shawn's middle to the ally's middle, shedding gilt leaves and glints. |
| 0.10-0.30 | **Encasing.** The four shards (f0-f4) close around the ally at 25 fps, dim (opacity 0.5, glow 0; the mote's glow is 0.8); the last drawing ends on the cue, no fade. |
| 0.30 | **Payoff.** The painted lock (a closed ring of gold plates with a crown and a halo, centre open) on the cue with the sound (`gilded_aegis_impact`), the ward bar and the ward icon on the ally's plate, and the log line "Shawn wraps <ally> in the fleece." 440 box, punch 0.3, glow 1.7, with `flash`: a soft gold ring (`aegis_bloom`, `tools/make_spell_fx.py`, clear centre) around the lock, 560 box, glow 3.0, 0.08 s then a 0.12 s fade. The leaf burst opens. |
| 0.30-0.70 | **Settle.** The lock is held 0.22 s and fades over 0.18 s; the last leaf clears at 0.4 s. |

## Measured (spell_feel vs rat, presentation-only; 60 fps)

| | 1x | 1.5x |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.38 s | 0.25 s |

`spell_feel` at 1x and 1.5x: no warnings, contact f18 / f12 and the visual contact on the same
frame. The lock frame is the cast's peak (3.07 over a typical 2.02 at 1x; 4.45 over 3.40 at
1.5x). Before 2026-10-04 the mote arriving as the shards closed (f13, peak 3.0) outshone the
lock (f18), which is an outline with an open centre. The shards and mote are now dim, so the
close is a build, and the bloom ring is the one big rise on the cue. The face stays visible
(the ring is narrow inside). The hold was cut from 0.32 s (the old sheet) to 0.22 s so payoff
to clean is 0.4 s as before.

## Frame notes

- **Ally anchoring: confirmed.** The shards, the lock and the leaves are centred on the ally on
  Odette, on Bjorn and on Shawn himself; the mote crosses to them. Both are sized by the struck
  body (`fit: target`).
- **The open centre works.** The owl's and the bear's faces are visible through the lock on every
  held frame (checked on Odette 16:9 and Bjorn 4:3). The shield is a ring sized to the body, so on
  the owl it sits over the head and chest and the wings stay outside it.
- **Halo colour.** The painted halo glow came back lime-yellow where it was painted over the green
  screen. `cap_green` on the recipe (ART_PIPELINE 5b) pulls those pixels to gold.
- **Hand-off.** The lock opens on the frame after the last shard drawing (the one empty frame
  between them was a cursor-compare bug, fixed 2026-10-04; see `../../README.md`).
- **Five-cast series.** Recorded (`gilded_aegis_on_odette_vs_forest_warden_full_x5_1.5x.gif`, 1099
  frames, `-Party shawn,odette,bjorn -Target odette -Vs forest_warden -Formation full -Repeat 5
  -Speed 1.5`). It used to die at cast 4: the capture pacing treated Odette as already armed from
  her previous turn (a stunned teammate's skipped turn sat between) and waited out the stall assert.
  A press is now per turn (`TurnPress` in `CaptureScenarioRunner`).
- **Flash guard.** Five casts at 1.5x (1099 frames, 18 s), steps where more than 10% of the frame
  brightens by 10% luminance (480x270 grey): the lock frame is 1.2% of the frame (the bloom ring is
  small); the largest steps anywhere are 9.3%, 8.8%, 7.3% (frames 62, 487, 809), all the warden's and
  the squad's own attacks across the whole stage, never the cast. Steps over 2% of the frame:
  at most 20 in any one second, mostly sprites moving; none comes from the lock, which adds one
  1.2% step per cast, five in 18 s. Counted as one small flash per cast, well inside 3 per second.
- **Readability.** The ward icon and bar are on the cue frame, on the ally's plate. The lock frame
  covers the ally's upper body for about 0.1 s; no number is under it (a ward has no damage
  number). Bjorn was recorded at 4:3 only (the 16:9 crop cuts him at the left edge, per the S0 notes); there he is whole.
- **Self cast.** On Shawn the mote has nowhere to go and shows as a static bright point at his
  staff side for 0.2 s, then the shards. It reads as a gather, not as a delivery.
- **Not looked at:** `-Darkness` (the plan asks it only of Shackles and Viper).

## Identity test: "a golden shield on my ally"

Verdict: **passes.** A gold mote goes from Shawn to the ally, four shards close, and a finished
gold shield with a crown locks around the ally while their face stays visible; the plate gains
the ward. It reads as "closed ward", not as a mirror, portal or plate hiding the body.

What is weak: the painted shield is a different drawing from the shards that close into it (the
shards are the old sheet's, the lock is the new art), so the hand-off is a swap, not a close.
The lock is quieter than the shards at 1x (the WARN above). The mote is still a glow and a tail.
