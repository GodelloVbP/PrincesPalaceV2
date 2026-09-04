# Odette — the owl scholar, six key stills

Delivered 2026-09-04. The second party member with battle art after Shawn,
and the first actor commissioned directly under the static-art policy
(`docs/STATIC_COMBAT_ART_DEEP_DIVE.md`): one still per stance, posed by the
fight's static cues. INT-based caster, fragile, and she flies.

## Source

`sheet_poses.png` — 1536x1024, 3x2, from the spec's Stage 1 template with
the owl SUBJECT block (midnight-blue plumage, pale face disc, round brass
spectacles, crescent-moon pendant, mustard scarf, brass-cornered teal book).
Third generation: v1 dangled the talons in every hovering pose, which read as
a perched bird; v3 tucks them everywhere but hurt. Baked checkerboard rather
than alpha, keyed with `white_flood --pocket-max-area 4000` (the wings
enclose checkerboard pockets well over the 200px speck cap).

Cells, row-major: idle, attack, cast, hurt, defeated, victory — the six
stances `FightSession.Stances` drives for a party member.

## Accepted metrics

| stance | filled area / idle | plumage px / idle | sqrt(mass) |
|---|---:|---:|---:|
| idle | 1.00 | 1.00 | 275.0 |
| attack | 1.03 | 0.98 | 279.5 |
| cast | 1.08 | 1.09 | 285.6 |
| hurt | 0.98 | 0.93 | 273.3 |
| defeated | 0.80 | 0.72 | 246.5 |
| victory | 1.14 | 1.20 | 294.8 |

Victory sits over the 1.10 band on filled area and it is the spread wings,
not body scale: plumage pixels rise with it and the head is idle-sized. Not
resized. Defeated legitimately loses mass.

## Delivery

```
python tools/slice_actor_sheet.py --sheet Assets/_Project/Art/Characters/owl/sheet_poses.png \
    --actor Characters/owl --stances idle,attack,cast,hurt,defeated,victory \
    --key white_flood --pocket-max-area 4000 --prune
```

No nudges. Canvas 649x366, `groundLine 8`, `delivery_scale` 1.0: idle content
height 345px against Shawn's delivered 349px, so the two stand at one height
with no correction.

- **The hover moved out of the drawings.** The first delivery baked a 35px
  upward nudge into the five airborne stances so Odette would clear the
  party's front row. It was replaced the same day: she was still hidden
  behind the front-row figure at her far slot, a baked height cannot be
  tuned without re-slicing (every fix meant a new sheet), and a still cannot
  bob — a flyer held dead level reads as pasted on, not aloft. The altitude
  now lives in `StanceManifest.json`'s `hover` block for this actor and is
  pushed every frame, in every stance but `defeated`, by
  `FightController.StageVisuals.HoverIdle` calling
  `StageActorAnimator.SetHover`. All six stills sit on the one shared floor
  the slicer checks by default.
- `--max-ground-spread` stays available on the slicer for a future actor
  whose drawings must legitimately disagree about where the floor is; Odette
  no longer needs it now that her lift is a runtime channel instead of a
  baked offset.
- `--pocket-max-area` was restored after the tools rewrite dropped the
  treant's per-sheet cap; the wings enclose checkerboard pockets well over
  the 200px default speck cap.

## Content

She replaced `placeholder_caster` in `characters.json` (id `owl`) with that
entry's INT/WIS build kept as her starting numbers and its three skills
(`placeholder_caster_bolt`, `_firebolt`, `_mend`) re-pointed at her. Two
things are still borrowed and say so: the skill ids and descriptions still
read "placeholder", and her portrait is Shawn's neutral until an owl
portrait exists. A proper design pass on stats, skill ids and an INT-scaled
kit is the next step, not this one.
