# Prismatic Orb — wind source pack

Generated with the imagegen skill and built-in image tool. Original RGBA pixels preserved. Source artwork only; not wired into gameplay or validated in Unity this turn.

| File | Dimensions | Grid |
| --- | --- | --- |
| wind_core_6f.png | 1536 x 1024 | 3 columns x 2 rows |
| wind_contact_8f.png | 1774 x 887 | 4 columns x 2 rows |
| wind_particles_8.png | 1774 x 887 | 4 columns x 2 rows |
| wind_wake.png | 2172 x 724 | Single |

Read left to right, top row first. Core cells are 512 square. For contact and particle sheets use x boundaries [0,444,887,1331,1774], y boundaries [0,444,887], in top-origin image coordinates. Preserve full source cells and pad consistently in recipes; do not auto-crop each animation frame independently. Record contact pivots explicitly. Wake attaches at its thick right end; mirror the whole composition for right-side casters.

Use the existing Water layered presentation as the schema reference, with separate core, wake, flight emitter, contact and impact emitter. Canonical gameplay naming is prismatic_orb; the prismatic_bolt folder is the art source location. Add recipes and content through the existing pipeline; no new player behaviour is implied by this pack.

## Motion direction and tuning

Thin crescent core; impact opens into a hollow pressure ring and disperses into arcs. Use sparse streak emission, near-zero gravity and fast fading. Keep ring center clear and overall opacity restrained. Do not use Water's falling-droplet motion. Core has a broad soft halo including near-transparent canvas residue (sampled corner alpha 1/255); inspect against both dark and light backgrounds before production use.

## Implementation acceptance

Core loop ends at arrival; wake fades; old emitted particles finish. Choose contact start frame and hit cue together so the approaching pose is not replayed after arrival. Peak is approximately source frame 4, but tune by viewing real-time playback, not by assuming a fixed frame. Test loop seam, pivot stability, core-to-contact scale, right/left facing, UI readability and two consecutive casts. Do not assume uniform scale across generated sheets. Core halos are visible and should be reviewed in game. Transparent encoding does not itself prove clean edges or perfect registration.

No scenes, recipes, mechanics or working Water content were changed in this delivery. See PROMPTS.md for generation provenance.
