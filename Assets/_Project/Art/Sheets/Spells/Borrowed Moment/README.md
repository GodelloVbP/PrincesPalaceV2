# Borrowed Moment

Generated using the built-in image generation tool.

## Atlas layout

- Source: borrowed_moment_atlas_v1.png
- Verified delivery: 1254 x 1254 pixels, RGBA with genuine transparent pixels. The generator returned this size despite the preferred size in the prompt.
- Nominal crop edges on each axis: 0, 314, 627, 940, 1254 pixels. The dimensions are not divisible by four; use explicit rectangles and pad extracted frames to a shared canvas rather than assuming integer-sized grid cells. Inspect gutters before accepting crops.
- Grid: 4 columns x 4 rows; equal cells, read left-to-right then top-to-bottom.
- Cells 1-12: main animation sequence.
- Cells 13-16: independent reusable particle sprites. Do not play these as part of the main sequence.
- Generation brief targeted impact at cell 6; inspect the artwork when setting the actual cue. Crownfall visibly lands at cell 7.
- Transparent source art; preserve alpha and dark materials.
- Starting playback suggestion: 12-16 fps, with a short hold before impact and a fade after cell 12. Tune in engine.

## Identity and secondary motion

An antique amber hourglass inside a tilted oval clock ring, NO numerals: grains gather upward, hourglass rolls backward through a quarter turn, two clock hands snap counterclockwise at frame 6 with a sharp turquoise temporal ripple, ring unthreads into arcs, golden sand falls upward in decay. Secondary sprites: gold sand cluster, turquoise curved time streak, small brass gear, tiny fourpoint glint.

## Integration notes

This is a generated source atlas, visually reviewed as a sheet. It has not been sliced or verified in Unity playback. Measure actual image dimensions and cell bounds before slicing; generated gutters and anchors may need cleanup. Preserve shared frame canvases rather than independently recentering each frame. Create separate recipes for the main sequence and particle folder when importing. Particle timing, gravity, spin, and fading are authored in the existing layered VFX system. Permanent ward/curse visuals require a held or looped layer; this atlas depicts cast and dissipation.

## Exact generation prompt

Use case: stylized-concept. Production 2D fantasy game VFX sprite animation atlas. Exactly 4 columns x 4 rows equal square cells on a square canvas, preferably 2048x2048. Genuine transparent background, no checkerboard, no labels, no grid, no text, no scene, no victim or caster. First 12 cells row-major are a coherent temporal animation: anticipation frames 1-4, acceleration 5, impact 6, follow-through 7-8, dissipation 9-12. Last row cells 13-16 are four separate reusable particle sprites, NOT more main animation frames. Same fixed camera, anchor, palette and scale across all animation frames. Generous transparent gutters and 12 percent internal padding; nothing touches or crosses cell boundaries. Luminous hand-painted fantasy material, rich shading, decisive silhouette and crisp small-scale readability. Unique material-driven motion and playful secondary motion, loose debris continuing after the main shape disappears, no full-canvas glow. Spell: Borrowed Moment. An antique amber hourglass inside a tilted oval clock ring, NO numerals: grains gather upward, hourglass rolls backward through a quarter turn, two clock hands snap counterclockwise at frame 6 with a sharp turquoise temporal ripple, ring unthreads into arcs, golden sand falls upward in decay. Secondary sprites: gold sand cluster, turquoise curved time streak, small brass gear, tiny fourpoint glint.
