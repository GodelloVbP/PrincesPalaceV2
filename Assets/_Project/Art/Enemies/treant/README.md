# Treant — six key stills, one sheet

Delivered 2026-09-04 under the static-art policy
(`docs/archive/STATIC_COMBAT_ART_DEEP_DIVE.md`): one drawing per stance, posed by
`FightBeatPlayer`'s static cues rather than redrawn frame by frame. This
replaces the six-frame kit delivered 2026-08-24, which was off-style
(painterly against the rat's flat cel) and whose sheets were redraws rather
than motion — `tools/actor_stance_qa.py` measured its hurt at a 5.0 redraw
ratio, the worst on the roster, idle 3.1, attack 3.4.

## Source

`sheet_poses.png` — 1536x1024, 3x2 grid, generated from the spec's Stage 1
template (`docs/STANCE_SHEET_SPEC.md` §3) with the treant SUBJECT/FEATURES
block there, after one internal revision by the author. The generator
returned a baked checkerboard rather than real alpha, so the slicer keys it
with `white_flood` and a raised `pocket_max_area` (the roots and the raised
arms each enclose ~1500px of checkerboard, far over the 200px speck cap).

Cells, row-major: idle, attack, trunk_slam, cast, hurt, defeated.

## Accepted metrics (author's measurement on the sheet, then the slicer's)

| check | value | band |
|---|---:|---|
| cast filled area vs idle | 0.91 | 0.90–1.10 |
| cast height vs idle | 0.99 | — |
| cast bbox area vs idle | 1.21 | not a scale measure; the spread arms |
| sliced sqrt(area)/median: idle / attack / trunk_slam / cast / hurt / defeated | 1.03 / 1.01 / 1.00 / 0.99 / 0.97 / 0.89 | 0.90–1.10; defeated legitimately loses mass |
| ground line agreement across the six | 0px | — |

## Provenance

**Reproducible — `recipe.json`, beside this file.** Replay it with:

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Enemies/treant/recipe.json
```

Verified 2026-09-06: the replay writes all six committed stills byte for byte.
`groundLineSource` for this actor is therefore `slicer` in
`Resources/StanceManifest.json` — a measurement a tool can reproduce is a
measurement that tool should own, and the next re-slice updates the number
without anybody copying it off a terminal.

**The recipe is what the README could not be.** This file recorded
`delivery_scale 1.05`, `white_flood`, and "a raised `pocket_max_area`".
Reconstructing the run from that meant guessing the raised value and testing
candidates against the committed bytes; 2000 and 4000 both reproduce, 1600
does not, and 2000 is what the recipe now records. Prose is where the
reasoning belongs. The argv belongs in a file.

## Delivery

- `slice_actor_sheet.py` entry `treant`: `delivery_scale 1.05` = 441 /
  measured idle content height 420px at 1.0 (spec §8 target for the treant).
- Output canvas 646x478, `groundLine 8` — written to
  `Resources/StanceManifest.json`, which carries no `stances` entries for the
  treant any more: every pose is a single drawing and needs no timing.
- The treant's entry in `tools/pad_actor_frames.py` was removed with the
  per-frame crops it operated on.
- Superseded and deleted in the same commit: `sheet_<stance>.png` x6,
  `<stance>_frames/` x6, `01_idle.png`…`06_defeated.png`, `_base_poses/`,
  `_attack_sheet_source.png`. Git history is the archive.
