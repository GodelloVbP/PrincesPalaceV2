# Viper's Bite

Generated using the built-in image generation tool.

## Atlas layout

- Source: vipers_bite_atlas_v1.png
- Verified delivery: 1254 x 1254 pixels, RGBA with genuine transparent pixels. The generator returned this size despite the preferred size in the prompt.
- Nominal crop edges on each axis: 0, 314, 627, 940, 1254 pixels. The dimensions are not divisible by four; use explicit rectangles and pad extracted frames to a shared canvas rather than assuming integer-sized grid cells. Inspect gutters before accepting crops.
- Grid: 4 columns x 4 rows; equal cells, read left-to-right then top-to-bottom.
- Cells 1-12: main animation sequence.
- Cells 13-16: independent reusable particle sprites. Do not play these as part of the main sequence.
- Generation brief targeted impact at cell 6; inspect the artwork when setting the actual cue. Crownfall visibly lands at cell 7.
- Transparent source art; preserve alpha and dark materials.
- Starting playback suggestion: 12-16 fps, with a short hold before impact and a fade after cell 12. Tune in engine.

## Identity and secondary motion

Spectral snake jaws close over the target, shedding venom droplets. Secondary sprites: round venom drop, falling drop, venom splash, poison curl.

## Integration notes

This is a generated source atlas, visually reviewed as a sheet. It has not been sliced or verified in Unity playback. Measure actual image dimensions and cell bounds before slicing; generated gutters and anchors may need cleanup. Preserve shared frame canvases rather than independently recentering each frame. Create separate recipes for the main sequence and particle folder when importing. Particle timing, gravity, spin, and fading are authored in the existing layered VFX system. Permanent ward/curse visuals require a held or looped layer; this atlas depicts cast and dissipation.

## Exact generation prompt

Use case: stylized-concept. Create a production 2D fantasy game VFX animation sprite sheet for VIPER'S BITE. Exactly 4 columns by 4 rows of equal square cells, 2048x2048 canvas, genuine transparent background, no grid lines, no text, no numbering, no characters or victim. Row-major first 12 cells are consecutive frames of ONE stationary target-centered animation: ghostly emerald snake jaws materialize above and below an empty central victim space, open wide with long curved ivory fangs, snap shut together in frame 6, recoil, dissolve into green venom droplets. Last 4 cells are isolated reusable secondary particle sprites: round venom drop, elongated falling drop, tiny venom splash, wispy poison curl. Each frame centered on exactly the same anchor with consistent scale and safe empty margins; no art crossing cells. Rich hand-painted luminous material, sharp readable silhouette, dark emerald translucent scales, bright acid green venom, ivory fangs. Anticipation, emphatic bite, follow-through and gravity-pulled droplets falling lower in the decay frames, lively physically readable secondary motion. Broad painterly shapes that read at small game scale; no surrounding scene, no cast shadows or baked checkerboard. This is a temporal animation atlas, not different icon designs.
