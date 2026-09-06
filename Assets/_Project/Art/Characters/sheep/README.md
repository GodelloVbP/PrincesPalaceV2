# Shawn — stance stills

Delivered id: `sheep`. The party lead, and **the only real character in this
project** — every other animal on the roster is a placeholder.

> **There is no source design sheet in this tree.** Not in this folder, not
> anywhere under `Art/`. This file exists because `docs/STANCE_SHEET_SPEC.md`
> puts an actor's record at `Art/<Enemies|Characters>/<actor>/README.md`, and
> because the absence itself is worth committing.

## Provenance

**Protected legacy — see `Art/Sheets/hand_assembled.json`'s `actors` block.**
The strongest case in that register: `attack` is `f2` of an attack sheet named
in `48131f4`, and the other five stances predate that commit with no recorded
source at all. There is nothing to slice, so there can be no `recipe.json`.

`HandAssembledArtTests` pins all six stills by content hash. If Shawn is ever
redrawn, the delivery is a fresh design sheet through
`tools/slice_actor_sheet.py`, which writes a recipe — and the register entry
and the hashes come out in the same commit as the new art.

## groundLine 43

Authored, and the second reason the stance manifest exists: **Shawn's idle
plants a staff roughly 33px below his feet**, so `idle.png` measures a ground
line of 9 against the other five stances' 41-42. A runtime scan read the staff
as the floor and floated him. The manifest's 43 against a measured median of
41 is a 2px difference, inside the 8px band.

Shawn's portraits are a separate kit (`Art/Portraits/Sheep/`) and are
themselves marked `reproduces: False` — a fresh run of
`tools/remove_portrait_backgrounds.py` produces a 1122x1360 crop where the
committed `Shawn_neutral.png` is 1122x1402. See `docs/ART_PIPELINE.md`.
