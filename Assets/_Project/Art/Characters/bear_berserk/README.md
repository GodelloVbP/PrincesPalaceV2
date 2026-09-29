# Bjorn Berserk -- the form's nine key stills

The battle art Bjorn wears while the Einherjar path's Berserk form runs
(`ContentData/skills.json`, skill `berserk`, `transform.spritePath`
`Characters/bear_berserk`). A whole delivered actor with its own canvas,
ground line and `StanceManifest` entry, exactly like `Characters/sheep_black_ram`
is for Shawn: the runtime swaps the folder at the transform beat's impact
instant and puts `Characters/bear` back on the exit beat.

## Sources

Both sheets are white-background RGB, copied verbatim from `Art/Sheets/`:

| here | original | grid |
|---|---|---|
| `sheet_poses.png` | `bjorn_berserk_poses-v1.png` (1536x1024) | 3x2 |
| `sheet_combat.png` | `bjorn_berserk_combat-v1.png` (1254x1254) | 2x2 |

Cells, row-major. Poses: idle, attack, cast (roar, hammer raised), hurt,
defeated, victory. Combat: cell 1 shoulder charge -> `rush`, cell 2 forward
leap -> not sliced, cell 3 overhead windup -> `overhead`, cell 4 forward
smash -> `slam`. Cell 2 is left out for the same reason the base Bjorn's
second combat cell is: the fight has no beat that asks for it, and the mapping
mirrors `Characters/bear` cell for cell. He faces RIGHT in every cell.

## Keying

`white_flood --pocket-max-area 4000`, as `Characters/bear`. Both sheets carry a
flat white ground, and the raised arm and hammer enclose white pockets well
over the default speck cap.

## Sizing

Two delivery scales, one per sheet, sliced in ONE invocation so every stance
shares one canvas (591x495) and one ground line (8):

- Poses sheet, `1.013157894736842`: the base Bjorn's idle is 385px tall, and
  this sheet's idle measures 380px at scale 1.0, so 385/380 puts the form's
  idle at 385px too -- the figure does not change size when the form goes on.
  Area cross-check at that scale: idle 82.3k opaque px against the base idle's
  81.6k.
- Combat sheet, `0.84`: the second generation is drawn larger than the first
  (sqrt of the largest component's mass is 351-376 against the poses sheet's
  274-314 at scale 1.0), so it needs its own scale, chosen as the base Bjorn's
  combat sheet was: put the wide, extended-limb poses in the same
  sqrt(mass) band as this actor's own `cast`/`hurt`/`victory`
  (296-318 after delivery). Result 316 / 311 / 295 for rush / overhead / slam.
  A hammer-head measurement was tried first and abandoned: the head's dark
  face is too broken up by highlights and ribbon on this sheet to isolate
  reliably. Judgement, not measurement; the owner can move `0.84` and replay.

`tools/actor_stance_qa.py` against `Resources/Characters` (bear vs this
actor), ratio of each stance's sqrt(opaque area) to the actor's median:

| stance | bear_berserk | bear |
|---|---:|---:|
| idle | 0.937 | 0.993 |
| cast | 0.997 | 1.092 |
| attack | 0.953 | 0.966 |
| hurt | 1.024 | 0.963 |
| victory | 1.023 | 0.958 |
| defeated | 0.894 | 0.868 |
| overhead | 1.003 | 1.088 |
| rush | 1.020 | 1.056 |
| slam | 0.952 | 1.007 |

The form's spread (0.89-1.02) is tighter than the base's (0.87-1.09).
Overhead's topmost pixel sits 470px above the ground line, under the
483px forest_warden reference in `FightStageAnchors`.

Ground line: all nine stances land within 0px of each other under the
`ground_band` anchor, so no `--nudge` was needed.

## Provenance

Reproducible: `recipe.json` beside this file.

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Characters/bear_berserk/recipe.json
```

Replayed into scratch; all nine stills are byte-identical to the committed
ones. `groundLine 8` and `groundLineSource: "slicer"` in
`Resources/StanceManifest.json`; `breath: 1.0` is copied from
`Characters/bear`, not tuned.
