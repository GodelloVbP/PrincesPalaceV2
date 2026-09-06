# Giant Rat — stance kit

Delivered id: `rat`. Content id and art folder agree, unlike the troll.

> **The rat's source sheets do NOT live in this folder.** They sit directly in
> `Art/Enemies/` for historical reasons — `Giant_rat_sheet.png` (the original
> 6-pose sheet, and the project's house-style reference), plus
> `Giant_rat_idle_sheet_12_frame.png` and `Giant_rat_attack_sheet_12_frame.png`.
> This file is here because `docs/STANCE_SHEET_SPEC.md` §8.3 puts an actor's
> record at `Art/Enemies/<actor>/README.md`; do not move the sheets to match it
> without re-pointing `tools/slice_actor_sheet.py`'s `rat` entry.

## Provenance

**Protected legacy — see `Art/Sheets/hand_assembled.json`'s `actors` block.**
No `recipe.json`, and there cannot be one for this delivery: `idle` is `f0` of
the 12-frame idle sheet and `attack` is `f5` of the 12-frame attack sheet (the
peak of the leap), chosen by eye off the QA contact sheets in `48131f4`, with
the remaining stances taken as single cells of `Giant_rat_sheet.png`. That was
a judgement against pictures, not an invocation.

`HandAssembledArtTests` pins all seven stills by content hash. Two of them,
`guard.png` and `extra.png`, are byte-identical to each other — recorded here
because a future reader will otherwise take it for a copy-paste mistake in the
pin.

Everything below is the regeneration brief, and it is still live: if the rat is
re-commissioned, it goes through `tools/slice_actor_sheet.py` like any new
actor, a `recipe.json` lands beside this file, and the register entry and the
hashes come out in the same commit as the new art.

## Status against `docs/STANCE_SHEET_SPEC.md`

| stance | source | frames | verdict |
|---|---|---|---|
| idle | `Giant_rat_idle_sheet_12_frame.png` | 12 | **FAILS Protocol A #9 and Protocol B travel — regenerate, see below** |
| attack | `Giant_rat_attack_sheet_12_frame.png` | 12 | fails Protocol A #9; Protocol B in band |
| cast, hurt, defeated, extra/guard | `Giant_rat_sheet.png` | 1 each | stills, no timing to check |

Measured 2026-08-30 (`tools/actor_stance_qa.py --report ... --only rat`):

| stance | redraw | churn/step | travel | feet band | band verdict |
|---|---|---|---|---|---|
| idle | 1.2 | 14.2% | **11.4%** | **19%** | travel rejects >8%; feet rejects >12% |
| attack | 1.5 | 24.7% | 16.7% | 28% | one-shot bands: in band |

**Do not read idle's 1.2 redraw ratio as a pass.** Redraw is churn ÷ travel, so
the out-of-band travel is what makes the ratio look good. Hold travel to the
spec's 6% and the same art scores 2.4 — just under the 2.5 hard reject. The
ratio is only meaningful once travel is in band.

### Sizing — already correct, leave it alone

The original shipped single-drawing idle measures **288px** content height
(commit `3c344f5^`). The 12-frame idle spans 271 (f0, rest) → 302 (f11, peak),
mean 286.5 — within 0.5%. `delivery_scale` stays **1.0**.

`docs/ART_PIPELINE.md` used to list "rat 226" here; that figure was stale and
acting on it would have shrunk the rat by a fifth. That table has been replaced
with a measure-it-yourself instruction for this reason.

## Detail Inventory

Counted off the delivered art. Paste verbatim into every Stage-2 prompt.

```
DETAIL INVENTORY (fixed for every frame of every sheet):
- exactly 2 ears: one near ear shown full with a pink inner cup, one far ear
  behind the skull showing only its outer edge
- exactly 1 visible eye: a red almond eye on the near side of the head, with a
  single dark brow crease above it
- exactly 2 long upper fangs, curving down outside the lower jaw, cream-white,
  the near one fully drawn and the far one slightly shorter behind it
- exactly 1 pink snout pad at the tip of the muzzle
- exactly 4 limbs: 2 forelegs (raised, held in front of the chest) and 2 hind
  legs (planted). 4 claws on each forepaw, 4 toes on each hind foot
- exactly 1 tail: one continuous pink segmented rope leaving the body low at
  the rear, drawn with 11-13 visible ring segments, curving in ONE smooth arc
- palette: near-black outline, 3 charcoal-grey fur tones, 2 salmon-pink skin
  tones (tail/paws/ears/snout), cream-white fangs, one pure red eye
```

**The fur silhouette is this creature's known animation hazard.** The outline is
drawn as an irregular shaggy fringe rather than a smooth edge, and that fringe
is redrawn differently in every generated frame — it is where the idle's feet-band
churn (19%, reject is 12%) actually comes from. Any regeneration prompt must
pin it explicitly; the direction below does.

## Regeneration — idle

Both faults trace to one cause: the sheet was commissioned without §5a's
explicit per-frame direction, so the generator drew twelve illustrations of a
rat rather than one rat posed twelve times.

- **Protocol A #9 (gutters).** Ink crosses all five vertical cut lines
  (69–121 rows each). Figures overlap their cells, so the slicer severs tails
  at the gutter: the severed piece lands in the neighbour cell as a stray, and
  the cell it came from is left with a cut-off tail. `slice_actor_sheet.py`'s
  `drop_far_components_px` currently deletes the strays, which removes the
  visible foreign fragment but cannot restore the tail it was cut from. That
  key is a mitigation, not a fix.
- **Protocol B travel.** 11.4% against a 2–6% target and an 8% reject. §5a asks
  for a full inhale about **4%** of body height above frame 1; this sheet moves
  nearly 3× that, which is the "gliding around the ground" the owner reported.

Attach `Giant_rat_sheet.png` (style + design reference) and use §5's template
with the Detail Inventory above and these frame directions. Twelve frames, so
the §5a six-frame rise is restated at 12-frame resolution — still ONE half
breath, still monotonic, still peaking at the last frame.

```
THE TWELVE FRAMES - this is one slow breath IN, and nothing else. The rat is
standing still. Its feet do not move, its tail does not swing, and its head
does not turn.

FRAME 1: the resting pose, exactly as the attached reference. Fully exhaled,
  torso at its lowest.
FRAMES 2-11: the SAME DRAWING, rising by a further ~0.4% of body height each
  frame. Nothing changes except that the torso, shoulders and head move
  upward together as one rigid piece.
FRAME 12: full inhale, the peak - the torso, shoulders and head at their
  highest, about 4% of body height above frame 1, chest very slightly
  expanded. This is frame 1's silhouette moved upward, not a new drawing.

Total rise across the whole sheet is 4% of body height. That is a SMALL
movement - roughly the height of the eye. If any frame looks obviously
different from its neighbour at a glance, the movement is too large.

This sheet is played back and forth (1-12 then 12-1), so it must NOT return to
the resting pose at frame 12. The rise is monotonic: every frame is higher
than the one before it.

PINNED - traced identically in all twelve frames, same outline, same position
in the cell, copied not redrawn:
- both hind feet and both forepaws, including every claw and toe
- the entire tail, every ring segment, in the same arc
- the shaggy fur fringe along the back and haunch: this is the most important
  one. Trace the outer fur outline identically frame to frame. Do not redraw
  the fringe, do not change which tufts stick out, do not vary their number
  or length.
- the ears, fangs, eye and snout

GUTTERS - each rat must sit COMPLETELY inside its own cell with clear empty
space on all four sides. The tail must not cross into a neighbouring cell.
The previous sheet failed this and its tails were cut off at the cell edges.
```

## Regeneration — attack

The attack sheet's Protocol B numbers are in band, so it only needs the gutter
fix. If it is regenerated anyway, keep the same Detail Inventory, keep the
impact on frame 7 (already authored in `Resources/StanceManifest.json`, and
measured to be the furthest-reach frame of the delivered set), and carry over
the same GUTTERS paragraph.

## Known accepted defect

`attack/f6.png` carries a ~19px grey speck near the fang, left over from the
source sheet. Inspected at 6× zoom: a few pixels, invisible at game scale.
Recorded rather than removed, so nobody re-derives it as new.
