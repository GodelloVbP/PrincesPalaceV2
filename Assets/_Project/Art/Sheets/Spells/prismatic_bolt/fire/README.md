# Prismatic Orb — fire source pack

Generated with the imagegen skill and built-in image tool. Original RGBA pixels preserved. Source artwork only; not wired into gameplay or validated in Unity this turn.

| File | Dimensions | Grid |
| --- | --- | --- |
| fire_core_6f.png | 1536 x 1024 | 3 columns x 2 rows |
| fire_contact_8f.png | 1774 x 887 | 4 columns x 2 rows |
| fire_particles_8.png | 1774 x 887 | 4 columns x 2 rows |
| fire_wake.png | 2172 x 724 | Single |

Read left to right, top row first. Core cells are 512 square. For contact and particle sheets use x boundaries [0,444,887,1331,1774], y boundaries [0,444,887], in top-origin image coordinates. Preserve full source cells and pad consistently in recipes; do not auto-crop each animation frame independently. Record contact pivots explicitly. Wake attaches at its thick right end; mirror the whole composition for right-side casters.

Use the existing Water layered presentation as the schema reference, with separate core, wake, flight emitter, contact and impact emitter. Canonical gameplay naming is prismatic_orb; the prismatic_bolt folder is the art source location. Add recipes and content through the existing pipeline; no new player behaviour is implied by this pack.

## Motion direction and tuning

Compact pointed spear; short bright crown at contact. Emit few short-lived embers during flight and a brief burst at impact. Use low downward acceleration or mild upward drift rather than Water's heavy droplet gravity. Keep the wake short and reduce broad core glow if it washes out the stage.

## Implementation acceptance

Core loop ends at arrival; wake fades; old emitted particles finish. Choose contact start frame and hit cue together so the approaching pose is not replayed after arrival. Peak is approximately source frame 4, but tune by viewing real-time playback, not by assuming a fixed frame. Test loop seam, pivot stability, core-to-contact scale, right/left facing, UI readability and two consecutive casts. Do not assume uniform scale across generated sheets. Core halos are visible and should be reviewed in game. Transparent encoding does not itself prove clean edges or perfect registration.

No scenes, recipes, mechanics or working Water content were changed in this delivery. See PROMPTS.md for generation provenance.

## Charge (2026-09-09)

Sliced from `fire_charge_6f.png` (1536 x 1024, 3 columns x 2 rows, 512 px cells; see `CHARGE_HANDOFF.md`) by `recipes/prismatic_orb_fire_charge.json`, keyed false (delivered alpha). All six cells are cut to `Resources/Spells/prismatic_orb_fire_charge/f0..f5`; the played clip stops the layer's own lifetime one frame early (`seconds: 0.1667` at `fps: 30`, i.e. frames 1-5 only) so frame 6 fades out (`fade: 0.0333`) instead of holding at full opacity into the flight core's own appearance -- see skills.json's Fire element for why (avoids two full-strength cores on screen at once).

Timing: charge opens at release and runs 0.1667s + 0.0333s fade = 0.2s total. `core` (and its followers `wake`/`shed`) now open with `offset: 0.1667` instead of at release, so the flight starts exactly as the charge's fade begins. `hitCueSeconds` moved from 0.25 to 0.4167 (+0.1667) to keep the arrival and the damage cue on the same instant. Box: `size: 170`, smaller than the core's 175 -- measured off the sheet's own painted bounds (28-44% of its 512 canvas across the six frames, against the core's own 86%), not copied from the core's number.
