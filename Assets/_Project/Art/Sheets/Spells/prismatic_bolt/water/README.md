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

## Charge (2026-09-09)

Sliced from `water_charge_6f.png` (1536 x 1024, 3 columns x 2 rows, 512 px cells; see `CHARGE_HANDOFF.md`) by `recipes/prismatic_orb_water_charge.json`, keyed false (delivered alpha). All six cells are cut to `Resources/Spells/prismatic_orb_water_charge/f0..f5`; the played clip stops the layer's own lifetime one frame early (`seconds: 0.1667` at `fps: 30`, i.e. frames 1-5 only) so frame 6 fades out (`fade: 0.0333`) instead of holding at full opacity into the flight core's own appearance — see skills.json's Water element for why (avoids two full-strength cores on screen at once).

Timing: charge opens at release and runs 0.1667s + 0.0333s fade = 0.2s total. `core` (and its followers `wake`/`shed`) now open with `offset: 0.1667` instead of at release, so the flight starts exactly as the charge's fade begins. `hitCueSeconds` moved from 0.25 to 0.4167 (+0.1667) to keep the arrival and the damage cue on the same instant. Box: `size: 170`, smaller than the core's 190 -- measured off the sheet's own painted bounds (22-41% of its 512 canvas across the six frames, against the core's own 74%), not copied from the core's number.
