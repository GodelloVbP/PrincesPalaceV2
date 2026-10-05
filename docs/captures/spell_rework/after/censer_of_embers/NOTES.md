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
| 0.00-1.00 | **Wind-up.** The censer hangs on a long chain from a pivot 2.5 box heights above the art (650 px to the lantern), so it travels a wide arc and tilts little. Left extreme 0.2 s, right 0.6 s, left 1.0 s; each swing larger than the last (angle 7.9, 10.8, 14.9 degrees; `damping` -0.8 builds momentum); a plain sine, fastest at the bottom. Stage gloom 0.25 over 0.4 s. | `censer` (still, `above`, `rotate`, `fadeIn` 0.2) |
| 0.00-0.95 | Smoke is born at the lantern body 10 times a second and hangs almost still where it was born, so the swing's path is drawn behind it. | `trail` (emitter on `layer:censer`) |
| 1.00 | **Break.** The censer's own still, held at the third extreme, punches +40% white-hot for 0.04 s and fades in 0.05 s. | `crack` |
| 1.00-2.2 | **Shower.** 42 embers (coals and flecks, 0.35-0.9 of a 64 px particle) in every direction at 180-560 px/s, gravity 760, life 0.6-1.2 s; 5 big coals (1.4-2.2) tumbling at up to 540 deg/s, gravity 900. Born at the lantern's position at the third extreme. | `burst`, `coals` |
| 1.00 | Burn badge, impact sound, skill shake 0.25, all on the cue frame. | |
| 1.45-2.5 | **Fire.** The painted fire loops at the feet (4 frames, 8 fps), catches 0.45 s after the cue as the embers land (fades in 0.25 s), holds 0.5 s, dies over 0.5 s into the Burn coal glow. Coals float up from 1.45 s. | `fire`, `rise` |

Cast sound recut to 1.0 s (`tools/make_spell_cast_cues.py`, window 0.63-1.63 s of the source, rise -16.5 to -7.1 dB);
loudest 50 ms of the wav ends in its last 0.15 s (`MeleeSoundOverrideTests`). Gain +5.2 dB for that clip only.

## Fixes, as seen

**1. Swing** (treant, stage-crop pixel centroid of the lantern's bronze, full-res frames; rat in brackets):
left extreme frame 13 at x 1247, y 348 [1211, 338]; through the bottom at frame 25 at x 1340, y 349 [1311, 340];
right extreme frame 37 at x 1439, y 339 [1418, 327]; bottom again at 48 to 49 (x ~1320); left extreme at the cue, frame 59 at x 1181,
y 335 [1138, 324]. So the sweeps are 93 px left, 100 px right, 160 px left of the bottom, the lantern rises 10 px at the
right extreme and 14 px at the last, and its tilt is -7.9, +10.8, -14.9 degrees (the lantern leans visibly more on each
swing). It is quickest at x 1316-1366 (about 13 px a frame, frames 23-27) and slowest at the ends (1 px a frame, frames 12-14, 36-38).
The chain's top is off the stage crop; no gap shows inside it. The lantern reaches the left of the crown.

**2. Height.** New rule: a hanging layer (`place: above`, not travelling) keeps its lantern on the visible stage; only the top
0.28 of its box, the chain, rises above the stage top (`SpellFlight.StageTop` 300, `HangOverhang`). It is never over the
enemy plates (full 1920x1080 frame 36: the chain goes up behind the plate, the lantern is below it). On the treant it
overlaps the right side of the crown (the stage-room limit AUDIT records for Crownfall); on the rat it hangs about 120 px over
the head. Crownfall (travelling) still uses the 240 sky ceiling, unchanged (`SpellFlightTests`).

**3. Break.** On the cue frame (60) the lantern is a punched, glowing still at its left extreme (frames 60-62), and the shower is
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
