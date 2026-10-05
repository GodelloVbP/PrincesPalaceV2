# Censer of Embers, owner's fixes (plan `docs/PLAN_SPELL_ANIMATION.md` A1, second pass)

Captured 2026-10-05 with `tools/gif.ps1 spell censer_of_embers -Vs treant|rat` (1x), `-Vs treant -Seconds 12`,
`-Vs treant -Repeat 3 -Speed 1.5`, and `spell gale_scythe -Vs rat,treant,beetle` for the flash change. GIFs are
untracked next to this file (the 12 s one is 77 MB); the treant 1x is also `../../showcase/censer_of_embers_pilot_treant.gif`.
Frames below were looked at at 1920x1080 around the target. No verdict: the owner judges.

Art is now the owner's: `censer_hanging.png` (the censer layer and its break) and `censer_flame_loop.png` (the fire),
sliced by `Art/Sheets/recipes/censer_hanging.json` and `censer_flame_loop.json` (`green-soft` key, no crop, one canvas
per sheet so the chain top and the coal line do not move). The smoke column and the flame-tongue emitter of the pilot are
gone: the hanging art carries its own smoke wisps and the fire carries its own tongues. The embers and coals thrown by the
break are still the painterly `censer_of_embers_particles` stand-ins.

## Beats, authored seconds (hit cue 1.0 s)

| t | Beat | Layer |
|---|---|---|
| 0.00-1.00 | **Wind-up.** The censer hangs from the top of the stage and swings about the top of its chain: left extreme 0.2, right 0.6, left 1.0; 35 degrees, period 0.8, `dwell` 1.5 so it hangs at each end and quickens through the middle. Stage gloom 0.25 over 0.4 s. | `censer` (still, `above`, `rotate`, `fadeIn` 0.2) |
| 1.00 | **Break.** The censer's own still, held at the third extreme, punches +40% white-hot for 0.04 s and fades in 0.05 s. | `crack` |
| 1.00-2.2 | **Shower.** 48 embers (coals and flecks, 0.35-0.9 of a 64 px particle) in every direction at 180-560 px/s, gravity 760, life 0.6-1.2 s; 6 big coals (1.4-2.2) tumbling at up to 540 deg/s, gravity 900. Born at the lantern's position at the third extreme. | `burst`, `coals` |
| 1.00 | Burn badge, impact sound, skill shake 0.25, all on the cue frame. | |
| 1.00-2.0 | **Fire.** The painted fire loops at the feet (4 frames, 8 fps), breathes, fades in 0.08 s, holds 0.5 s, dies over 0.5 s into the Burn coal glow. Coals float up from 1.05 s. | `fire`, `rise` |

Cast sound recut to 1.0 s (`tools/make_spell_cast_cues.py`, window 0.63-1.63 s of the source, rise -16.5 to -7.1 dB);
loudest 50 ms of the wav ends in its last 0.15 s (`MeleeSoundOverrideTests`). Gain +5.2 dB for that clip only.

## Fixes, as seen

**1. Swing.** Three half-swings read: lantern at the far left (frame ~12), through vertical, far right (~36),
back left (~58) where it breaks. The ends hold visibly (frames 12-18 are nearly the same pose, then it sweeps). The
lantern is 270 px wide-box, about 130 px of brass, small against the 1920 frame. The chain leaves the top of the screen.
It passes over the enemy plate on the treant (drawn above it).

**2. Height.** Rule: a layer that hangs (`place: above`, not travelling) keeps its box top at the stage top
(`SpellFlight.StageTop`), `height` is only a cap, and it is not shrunk to clear the log. Crownfall (travelling) still uses
the 240 sky ceiling, unchanged (`SpellFlightTests`). Treant: lantern bottom at rest about 40 px over the crown, all of it on
screen at every extreme. Rat: the same position, so the lantern hangs about 250 px above the rat's head.

**3. Break.** On the cue frame the lantern vanishes into its own punched, glowing still (3 frames), then a shower of
embers and a few coals fly out from where it hung and fall. On the treant they rain over the canopy and onto the head;
the larger coals tumble past the face. It reads "the censer breaks into embers" rather than a flame column. On the rat they
fall 250 px and mostly die before reaching it.

**4. Burn coal glow** (`UIBurnGlow`, `BodyLight.BurnWeight`). An orange rim along the silhouette, strongest at the roots and
half strength at the crown, 16% tint over the interior, with a shimmer running along the edge. The treant keeps its brown,
green and yellow eyes. At the roots the rim is thick enough to read as an orange outline rather than embers.

**5. Spell and status flash** (`UIRimFlash`, `BodyLight.FlashWeight`). A Burn tick (frames 529-534 of the 12 s run) lights the
treant's outline pale orange with a light peach interior, 2 frames then 0.08 s. The Gale Scythe on rat, treant and beetle
flashes a pale cyan edge with the body's own colours showing. The sheep's own staff swing (Nature) still flashes the whole
body flat green, as before.

## What is weak

- The rat: the censer is far above it, the shower and its light are a long way from the target, and a short body gets
  no hanging height of its own. A chain that ends in mid-air (the alternative) was worse, so the box top is on the stage top.
- The lantern is small; a larger one touches the treant's crown at its resting low point.
- The break is flat-art lantern into painterly particles. The embers and coals do not match the cel style yet.
- The crack is 3 frames of the same drawing, brightened; there is no art of the lantern in pieces.
- The Burn rim at the roots is heavy next to the thin rim elsewhere. The interior tint of the rim flash is visible on pale bodies.
- The fire is about the width of the treant and a little over the rat's; on the rat it reads as embers at the feet.
- Not measured: `spell_feel` WARN/accent numbers; no `-Darkness` capture; no pixel test of the shaders (the rule is pinned in
  `BodyLight`, the shader files' property defaults are checked against it).
