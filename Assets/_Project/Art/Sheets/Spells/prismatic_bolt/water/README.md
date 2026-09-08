# Prismatic Bolt — Water art

Generated with the built-in image tool. Original generated alpha is preserved; no color-key removal was applied.

| File | Pixels | Layout |
| --- | --- | --- |
| water_core_6f.png | 1536 × 1024 | 3 columns × 2 rows; 512 px cells |
| water_contact_8f.png | 1774 × 887 | 4 columns × 2 rows |
| water_particles_8.png | 1774 × 887 | 4 columns × 2 rows |
| water_wake.png | 2172 × 724 | Single sprite |

Read atlases left to right, top row first. For the 1774 × 887 sheets, use image-coordinate column boundaries 0, 444, 887, 1331, 1774 and row boundaries 0, 444, 887. These generated dimensions require alternating 443/444 px cells, not a uniform integer grid. Convert top-origin rectangles to Unity bottom-origin coordinates when slicing.

Core: intended six-frame internal motion loop, travels right. Includes a soft blue halo. Contact: approach, compression, crown expansion, breakup, falling droplets. Particle order: round drop, stretched drop, heavy drop, curved streak, reverse curved streak, torn sheet, forked sheet, splash fork. Wake: thin left end, thick right attachment end.

Suggested assembly: moving core sprite, short wake, world-space droplet emission. Stop the core at contact and play the contact sequence once; emit secondary droplets and let existing flight particles finish. Tune scale, pivots, loop seam and contact registration in-engine. Artwork has not been tested in a Unity animation or prefab.
