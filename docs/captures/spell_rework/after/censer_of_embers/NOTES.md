# Censer of Embers, owner's fixes (plan `docs/PLAN_SPELL_ANIMATION.md` A1, second pass)

Captured 2026-10-05 with `tools/gif.ps1 spell censer_of_embers -Vs treant|rat` (1x), `-Vs treant -Seconds 12`,
`-Vs treant -Repeat 3 -Speed 1.5`, and `spell gale_scythe -Vs rat,treant,beetle` for the flash change. GIFs are
untracked next to this file (the 12 s one is 77 MB); the treant 1x is also `../../showcase/censer_of_embers_pilot_treant.gif`.
Frames below were looked at at 1920x1080 around the target. No verdict: the owner judges.

Art is now the owner's: `censer_hanging.png` (the censer layer and its break) and `censer_flame_loop.png` (the fire),
sliced by `Art/Sheets/recipes/censer_hanging.json` and `censer_flame_loop.json` (`green-soft` key, no crop, one canvas
per sheet so the chain top and the coal line do not move). The smoke column and the flame-tongue emitter of the pilot are
gone (the swing's smoke is now the `trail` emitter): the hanging art carries its own smoke wisps and the fire carries its own tongues. The embers and coals thrown by the
break are still the painterly `censer_of_embers_particles` stand-ins.

## Beats, authored seconds (hit cue 1.0 s)

| t | Beat | Layer |
|---|---|---|
| 0.00-1.00 | **Wind-up.** The censer is pulled back to the left and released: it fades in over 0.2 s held at its left extreme (`rotate.delay`), then swings left to right (0.6 s) and right to left (1.0 s), a cosine (`phase` 90), fastest at the bottom and slowing toward the ends. It hangs from a pivot 4.5 box heights above the art (1250 px to the lantern); angle -9.5, +11.1, -13.1 degrees (`damping` -0.4, each swing a little larger). Stage gloom 0.25 over 0.4 s. | `censer` (still, `above`, `rotate`, `fadeIn` 0.2) |
| 0.00-0.95 | Smoke is born at the lantern body 10 times a second and hangs almost still where it was born, so the swing's path is drawn behind it. | `trail` (emitter on `layer:censer`) |
| 1.00 | **Break.** The censer's own still, held at the third extreme, drawn exactly where the swinging layer is, white-hot for 0.04 s and gone in 0.05 s. | `crack` |
| 1.00-2.2 | **Shower.** 38 embers (coals and flecks, 0.35-0.9 of a 64 px particle) in every direction at 180-560 px/s, gravity 760, life 0.6-1.2 s; 5 big coals (1.4-2.2) tumbling at up to 540 deg/s, gravity 900. Born at the lantern's position at the third extreme. | `burst`, `coals` |
| 1.00 | Burn badge, impact sound, skill shake 0.25, all on the cue frame. | |
| 1.45-2.5 | **Fire.** The painted fire loops at the feet (4 frames, 8 fps), catches 0.45 s after the cue as the embers land (fades in 0.25 s), holds 0.5 s, dies over 0.5 s into the Burn coal glow. Coals float up from 1.45 s. | `fire`, `rise` |

Cast sound recut to 1.0 s (`tools/make_spell_cast_cues.py`, window 0.63-1.63 s of the source, rise -16.5 to -7.1 dB);
loudest 50 ms of the wav ends in its last 0.15 s (`MeleeSoundOverrideTests`). Gain +5.2 dB for that clip only.

## Fixes, as seen

**1. Swing** (bronze-pixel centroid of the lantern per 60 fps frame in the stage crop, 1920x1080 pixels; treant, rat in brackets):
held at the left extreme frames 11 to 13 at x 1141 [1095]; through the bottom at frame 25 to 26 at x 1355-1382 [1327-1356];
right extreme frames 36 to 37 at x 1551 [1540]; bottom again at frame 48 at x 1329 [1299]; left extreme at the break, frame 59 to 61,
at x 1074-1076 [1020]. Travel left to right is 410 px and right to left 477 px (the previous version swept 193 and 262).
dx per frame rises smoothly 0, +3, +7, +9, +13 ... +27 at the bottom, falls to 0 at the right extreme, and mirrors:
-4, -8, ... -32 at frame 49, -8 at frame 59, +2 at 60 (the crack), 0 at 61: no frame-to-frame jump larger than the motion either side
(the rat: -9, -3, 0, 0, 0 at the break). The lantern rises about 11 px at the right extreme and 15 at the last (y 340 to 329 to 325).
The break draws the censer's own still at the same pivot and angle, with no scale punch (a scale about a pivot 1250 px away moved
the lantern 38 px in the previous version). The chain's top is off the stage crop; no gap shows inside it. The lantern reaches the
left of the crown.

**2. Height.** New rule: a hanging layer (`place: above`, not travelling) keeps its lantern on the visible stage; only the top
0.28 of its box, the chain, rises above the stage top (`SpellFlight.StageTop` 300, `HangOverhang`). It is never over the
enemy plates (full 1920x1080 frame 36: the chain goes up behind the plate, the lantern is below it). On the treant it
overlaps the right side of the crown (the stage-room limit AUDIT records for Crownfall); on the rat it hangs about 120 px over
the head. Crownfall (travelling) still uses the 240 sky ceiling, unchanged (`SpellFlightTests`).

**3. Break.** On the cue frame (60) the lantern is a brightened still at its left extreme (frames 60-62), and the shower is
born from the lantern's centre there, wherever the swing has put it; embers and a few coals fly out and fall. On the treant (frames 62-76) they burst at the crown and rain down the face and
body through ~frame 100; the larger coals tumble past the face. The fire at the feet appears at ~frame 88-94, as they land. It reads "the censer breaks into embers" rather than a flame column. On the rat they
fall 250 px and mostly die before reaching it.

**4. Burn coal glow** (`UIBurnGlow`, `BodyLight.BurnWeight`). An orange rim along the silhouette, strongest at the roots and
half strength at the crown, 16% tint over the interior, with a shimmer running along the edge. The treant keeps its brown,
green and yellow eyes. At the roots the rim is thick enough to read as an orange outline rather than embers.

**5. Spell and status flash** (`UIRimFlash`, `BodyLight.FlashWeight`). A Burn tick (frames 529-534 of the 12 s run) lights the
treant's outline pale orange with a light peach interior, 2 frames then 0.08 s. The Gale Scythe on rat, treant and beetle
flashes a pale cyan edge with the body's own colours showing. The sheep's own staff swing (Nature) still flashes the whole
body flat green, as before.

## Polish pass, engine-made (no new art)

Captures 2026-10-05 (`gif.ps1 spell censer_of_embers`, treant 1x, rat 1x, treant `-Repeat 3 -Speed 1.5`); frames are 60 fps, the cue
is f60 at 1x and f40 at 1.5x. Frames looked at at 1920x1080 around the target. No verdict: the owner judges.
New layers `pool`, `sparks`, `smoke`, `ash`; new fields `glowCurve` (with `knee`), `echo`, `stoke`, `emitter.burstTimes/burstEach`,
`vfx.kick`, plus `glow` and `tint` now drawn on an emitter's particles (`docs/ART_PIPELINE.md` rows, `SpellPolishFieldsTests`).

Swing
1. **Gloom 0.20** (was 0.25): background patch mean 25.2 at f0, 22.5 by f30 (held to f58), 26.3 by f80 once lifted. Barely perceptible:
   the stage is already dark, so 0.20 costs about 2.7 levels of 25.
2. **Coal light breathing**: `glow` alone moved nothing (the shader's knee 0.72 lifts only the hottest pixels; the lantern's gold sits
   under it: hot-pixel count flat at ~2000 over f10-f62). `glowCurve.knee` 0.4 fixed it: hot pixels 2273 at f12, crest 3892 at f20, trough
   2152 at f38, crest 3519 at f46, 3845 at f58. The visible result is the gold flaring yellow-white at the bottom of each swing (f20, f46)
   and settling in between (f38). It brightens the lantern's body, not its red windows.
3. **Afterimages**: three ghost lanterns trail the swing, strongest at the bottom (f20, f30, f48, f54). Clear on the rat (f24, f34, f54).
   Over the treant's green canopy a ghost reads as a murky green-gold smear (f30, f36, f52). Dark and translucent, not light.
4. **Sparks**: 8 at the release (f12-f20, small orange flecks on the lantern's lower left) and 8 at the right extreme (f36-f44). Small,
   about 8 px, and few; they read as flecks, not a shower.
5. **Light pool**: a soft warm disc on the body/floor under the lantern, sliding with it (f6 and f58 left, f24 under the trunk, f34 right,
   f46 centre). On the treant it tints the lower trunk orange when it passes (f22, f28, f46); on the rat it is a clear glow on the floor.
   Low contrast; the lantern is about 400 px above it.

Break
6. **Stage kick**: ground ring x 1322 -> 1370.7 (+48 px) on f62, back to 1322 by f75. Same frame as the cue's embers (f60-62). At 1.5x:
   +44 at f41, home by f48. The lantern's crack moves with it: lantern centroid on the rat dx -5.4 (f59), +5.9, +11.7, +2.2 (f62), then gone,
   so the lantern drifts left and is shoved right 20 px for the three crack frames before it breaks.
7. **Glowing embers**: the 38 embers and 5 coals bloom a little (f66-f96); the coals read hotter than before. Not a bright flare.

Aftermath
8. **Smoke**: grey wisps rise from the body around f96-f166 and one S wisp above the crown at f182. Thin and low contrast; it starts at
   the feet under the flames and is mostly seen over the face and crown, not as a plume from the fire.
9. **Ash**: 6 pale grey soft discs over the face and chest, f118-f166. They read as dust specks on the face, not as drifting flakes
   (the particle sheet has no grey flake; the soft disc is `glow_disc`, baked by `ProceduralSpriteBaker.BakeGlowDisc`).
10. **Burn glow with the fire**: the body's mean red rises 73 -> 80 over f88-f101 as the fire fades in and falls back as it dies; no frame
    to frame flicker is visible. The fire's `breathe` runs on the slow Ember curve, so the lift follows its fade, not a flicker.

Guards and checks
- Flashes (largest single-step brightening area, luminance rise of 10% or more): treant 1x 49,036 px (2.4% of the screen) at f102 (the
  treant's own hit flash), 21,716 px at the crack (f61); `-Repeat 3 -Speed 1.5` 51,728 px at f68, none at 57,000 px (a quarter of a
  10 degree field) or more, so no large flash in any second.
- Burn badge: first appears in the status strip at f60, the cue frame; hitCueSeconds is unchanged and the cue tests are green.
- Particle pool: the spell throws 86 particles in all and 64 are alive at most (`SpellPoolPeak`); effects renderers 7 of 12.

## What is weak

- The rat: the lantern is about 120 px over its head, the shower falls onto it from there; nothing covers the rat.
- On the treant the lantern sits on the crown and covers the right canopy during the swings.
- The lantern is small in the GIF (about 65 px); a larger one covers more of the treant's head.
- The break is flat-art lantern into painterly particles. The embers and coals do not match the cel style yet.
- The crack is 3 frames of the same drawing, brightened; there is no art of the lantern in pieces.
- The Burn rim at the roots is heavy next to the thin rim elsewhere. The interior tint of the rim flash is visible on pale bodies.
- The fire is about the width of the treant and a little over the rat's; on the rat it reads as embers at the feet.
- Not measured: `spell_feel` WARN/accent numbers; no `-Darkness` capture; no pixel test of the shaders (the rule is pinned in
  `BodyLight`, the shader files' property defaults are checked against it).
