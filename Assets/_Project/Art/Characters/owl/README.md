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
    --key white_flood --pocket-max-area 4000 \
    --nudge idle:0,-35 --nudge attack:0,-35 --nudge cast:0,-35 --nudge hurt:0,-35 --nudge victory:0,-35 \
    --max-ground-spread 40 --prune
```

- `delivery_scale` 1.0: idle content height 345px against Shawn's delivered
  349px, so the two stand at one height with no correction.
- **The hover.** The stage pins every actor's canvas bottom to one authored
  ground line, and the slicer refuses stances whose lowest rows differ by
  more than 6px because that is how a figure accidentally floats. Odette
  floats on purpose: the five airborne stances are nudged 35px up (a tenth
  of her height) and `defeated` stays on the floor, with the check widened
  to 40px for this one run. Canvas 634x401, `groundLine 8`.
- Two slicer flags were added for her: `--pocket-max-area` (restored after
  the tools rewrite dropped the treant's per-sheet cap) and
  `--max-ground-spread`, plus the canvas making room for an upward nudge so
  the crown is not clipped.

## Content

She replaced `placeholder_caster` in `characters.json` (id `owl`) with that
entry's INT/WIS build kept as her starting numbers and its three skills
(`placeholder_caster_bolt`, `_firebolt`, `_mend`) re-pointed at her. Two
things are still borrowed and say so: the skill ids and descriptions still
read "placeholder", and her portrait is Shawn's neutral until an owl
portrait exists. A proper design pass on stats, skill ids and an INT-scaled
kit is the next step, not this one.
