# Gilded Aegis, "after" (plan S3, spell 3.8)

Captured 2026-10-04 with `tools/gif.ps1 spell gilded_aegis -Party shawn,odette,bjorn -Target <ally>`
(1x; Bjorn with `-Aspect 4:3`) and one five-cast series at 1.5x. No painted art: the existing
twelve shield frames and four leaf/glint particles, plus one procedural piece
(`tools/make_spell_fx.py aegis_mote`). GIFs are untracked next to this file. Owner gates
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
| 0.10-0.30 | **Encasing.** The four shards (f0-f4) close around the ally at 25 fps, dim (opacity 0.85, glow 0.3); the last drawing ends on the cue, no fade. |
| 0.30 | **Payoff.** The shield locks shut (f5, glow 1.6, punch 0.12) with the sound (`gilded_aegis_impact`), the ward bar and the ward icon on the ally's plate, and the log line "Shawn wraps <ally> in the fleece." The leaf burst opens. |
| 0.30-0.70 | **Settle.** f6-f11 at 22 fps (0.32 s) and an 0.08 s fade; the last leaf clears at 0.4 s. |

## Measured (timing.json, 60 fps)

| | 1x | 1.5x |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.40 s (freed f42) | 0.27 s |

Before this pass one 0.72 s sheet plus a 0.14 s fade ran from the cast's start, so payoff to
clean was about 0.56 s and there was no delivery.

## Frame notes

- **Ally anchoring: confirmed.** The shards, the lock and the leaves are centred on the ally on
  Odette, on Bjorn and on Shawn himself; the mote crosses to them. The shard ring is sized by the
  struck body (`fit: target`), so it hugs the owl, fills the bear and wraps Shawn.
- **Flash guard.** The lock frame is a bright gold ring but small: the area of the frame that
  brightens by 10% luminance in one 60 fps step peaks at 0.9% on the cue. Over five casts at 1.5x
  (about 22 s), the largest single-step brightening anywhere is 4.9% of the frame and comes from
  the squad's and the treants' own attacks; at most 2 such events fall in any one second. Counted as at most one
  large flash per cast, well inside 3 per second.
- **Readability.** The ward icon and bar are on the cue frame, on the ally's plate. The lock frame
  covers the ally's upper body for about 0.1 s; no number is under it (a ward has no damage
  number). Bjorn was recorded at 4:3 only (the 16:9 crop cuts him at the left edge, per the S0 notes); there he is whole.
- **Self cast.** On Shawn the mote has nowhere to go and shows as a static bright point at his
  staff side for 0.2 s, then the shards. It reads as a gather, not as a delivery.
- **Not looked at:** `-Darkness` (the plan asks it only of Shackles and Viper).

## Identity test: "a golden shield on my ally"

My verdict: **passes.** A gold mote goes from Shawn to the ally, four shards close into a
crowned shield on the body, and the ally's plate gains the ward. The mote is what makes it
"on my ally" and not "around me", which the old sheet could not say on its own.

What is weak: the shield frame is the sheet's old drawing, so the lock is the same picture as
before, only now timed and delivered. The mote is a glow and a tail, not a drawn object.

## What a painted still would fix

A single painted shield-lock still (the crowned shield whole, a halo ring, no loose shards)
would let the lock be one drawing instead of an animated sheet's frame, and would keep the
ally's face visible through the lock. Not worth commissioning ahead of the other spells.
