# Forest Warden — stance stills

> **Delivered id is `forest_warden`, art folder is `forest_troll`.** They have
> always disagreed. Do not rename either; just be careful which one you are
> typing. `Art/Sheets/hand_assembled.json` registers the actor under its
> DELIVERED id, because that is the half `Resources/`,
> `StanceManifest.json` and `enemies.json` all key on.

Stances: idle, attack, attack_roar, attack_charge, hurt, defeated.

## Provenance

**Protected legacy — see `Art/Sheets/hand_assembled.json`'s `actors` block.**
No `recipe.json`. The delivery had two steps and only the second used a tool
this project ships: one frame was chosen by hand out of each `<stance>_frames/`
folder, and those already-cropped PNGs were then re-composited onto one
741x499 canvas through `slice_actor_sheet.py`'s ground_band anchor (`48131f4`).
The tool's input was a set of crops rather than a design sheet, so there is no
invocation to replay — reproducing it would mean recovering which frame of
which folder was picked, six times over.

That recompositing pass is why `groundLine` is a measured **8** rather than the
old 14. The 14 was never a foot position at all: it was the uniform pad around
six independently cropped frames per stance, and the manifest entry admitted at
the time to a possible vertical pop between stances because of it. The stage
sizes each slot to the sprite it is showing, so six canvases were six
positions.

`HandAssembledArtTests` pins all six stills by content hash.
