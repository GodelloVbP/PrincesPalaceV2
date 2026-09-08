# Prismatic Orb — earth source pack

Generated with the imagegen skill and built-in image tool. Original RGBA pixels preserved. Source artwork only; not wired into gameplay or validated in Unity this turn.

| File | Dimensions | Grid |
| --- | --- | --- |
| earth_core_6f.png | 1536 x 1024 | 3 columns x 2 rows |
| earth_contact_8f.png | 1774 x 887 | 4 columns x 2 rows |
| earth_particles_8.png | 1774 x 887 | 4 columns x 2 rows |
| earth_wake.png | 2172 x 724 | Single |

Read left to right, top row first. Core cells are 512 square. For contact and particle sheets use x boundaries [0,444,887,1331,1774], y boundaries [0,444,887], in top-origin image coordinates. Preserve full source cells and pad consistently in recipes; do not auto-crop each animation frame independently. Record contact pivots explicitly. Wake attaches at its thick right end; mirror the whole composition for right-side casters.

Use the existing Water layered presentation as the schema reference, with separate core, wake, flight emitter, contact and impact emitter. Canonical gameplay naming is prismatic_orb; the prismatic_bolt folder is the art source location. Add recipes and content through the existing pipeline; no new player behaviour is implied by this pack.

## Motion direction and tuning

Packed rigid rock core; impact breaks it into heavy pieces. Emit very sparse chips in flight; impact gets downward gravity and stronger weight than Water. Keep dust wake subtle. Atlas cells 1–6 are individual rock pieces, 7 is a grit cluster, 8 dust: avoid uniformly random selection across all eight if it produces oversized dust or too many heavy chunks. Peak impact approaches the source top edge; inspect for clipped fragments before finalizing the recipe.

## Implementation acceptance

Core loop ends at arrival; wake fades; old emitted particles finish. Choose contact start frame and hit cue together so the approaching pose is not replayed after arrival. Peak is approximately source frame 4, but tune by viewing real-time playback, not by assuming a fixed frame. Test loop seam, pivot stability, core-to-contact scale, right/left facing, UI readability and two consecutive casts. Do not assume uniform scale across generated sheets. Core halos are visible and should be reviewed in game. Transparent encoding does not itself prove clean edges or perfect registration.

No scenes, recipes, mechanics or working Water content were changed in this delivery. See PROMPTS.md for generation provenance.
