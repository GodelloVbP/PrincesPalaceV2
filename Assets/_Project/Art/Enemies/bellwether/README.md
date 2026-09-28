# The Bellwether

Generated 2026-09-28 with the built-in imagegen tool. Rendering reference:
`Art/Enemies/Giant_rat_sheet.png`, style only. The character is an eerie
upright, gaunt black ram with the posture of a tired man; the source is a
green-screen 4x2 pose sheet with the last cell empty. Redesigned after review.

Accepted prompt: uniform bold dark outlines, 2-3 flat tones per colour,
small palette, no gradients, painterly texture, rim light, floor shadows,
captions, particles or effects. One identical creature facing right in each
cell: idle with hanging arms, forelimb swipe, head thrown back casting,
crossed-arm guard, hurt recoil, face-down defeated, and arms-spread extra.
Wide clear gutters. All mouths closed; no visible green in the keyed poses.

Detail inventory: matted black wool, charcoal face, two curled dark horns,
pale pupil-less eyes, long forelimbs ending in split-hoof fingers, two
reverse-kneed hind legs, one small floral-engraved brass sheep's bell on a
brown leather neck strap with a silver buckle. No scattered accessories.

Delivery scale 1.2152777777777777 preserves the requested existing recipe.
The new idle is 559px high, deliberately taller than Shawn's 350px idle.
All seven poses share a 758x593 canvas and a ground line 8px from the bottom,
with measured ground spread 0px.

Provenance: `recipe.json` records source hash, slicer hash and full arguments.
Replay with `python tools/slice_actor_sheet.py --recipe
Assets/_Project/Art/Enemies/bellwether/recipe.json`.

Speck: the cast cell carried a detached 25px dark fleck ~70px left of the
body (cast.png around canvas pixel 200,230). The recipe now passes
`--drop-far-components-px 40`, the slicer's own stray-component rule; it
drops only that fleck (every other stance is byte-identical, and the rest of
cast.png's diff is alpha-1 resample halo). `sourceSha256` in the recipe is a
hand-kept field the slicer does not write; it was restored after the re-slice.

Head box (StanceManifest.json, for the enemy plate icon): re-fitted by eye to
the upright idle, a 150px square centred at canvas pixel (447, 92) -- face
and both horns.
