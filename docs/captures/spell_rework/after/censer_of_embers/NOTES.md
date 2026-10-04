# Censer of Embers, pilot (plan `docs/PLAN_SPELL_ANIMATION.md` A1)

Captured 2026-10-04 with `tools/gif.ps1 spell censer_of_embers -Vs treant|rat` (1x) and
`-Vs treant -Repeat 3 -Speed 1.5`. GIFs are untracked next to this file; the treant 1x is also
`../../showcase/censer_of_embers_pilot_treant.gif`. Every frame described below was looked at
at 1920x1080 around the target. No verdict: the owner judges.

Everything drawn is a STAND-IN until the owner's `censer_hanging.png` and `censer_flame_loop.png`
land (`docs/prompts/2026-10-04_spell_animation.md`): the censer is the swing sheet's f0, the smoke is
its f6, the flash is its f5, the fire is the painted crescent `censer_of_embers_catch`.

## Beats, authored seconds (hit cue 0.7 s)

| t | Beat | Layer |
|---|---|---|
| 0.00-0.70 | **Wind-up.** The censer fades in over 0.2 s above the target and swings about its chain top: a pendulum, amplitude 18 deg, period 0.93 s, so left extreme 0.23, home 0.47, right extreme 0.70. | `censer` (still, `above`, `rotate`, `fadeIn`) |
| 0.00-0.70 | Smoke column behind it, breathing (alpha 0.4, scale 0.06, rate 3), fades out over 0.3 s after the cue. | `smoke` (still, `breathe`) |
| 0.70 | **Payoff.** Censer cuts out. A 0.1 s flash of the sheet's ember column punches in (0.35), 28 coals and tongues thrown in all directions, skill shake 0.25, impact sound, Burn badge, all on the cue frame. | `flash`, `burst` |
| 0.70-1.60 | **Resolution.** The crescent of fire at the feet fades in over 0.08 s, breathes (alpha 0.35, scale 0.07, rate 4), holds 0.4 s, fades over 0.5 s into the Burn coal glow. Flame tongues lick up 0.4 s; coals float up from 0.75 s, life 0.5-0.9 s. | `fire`, `tongues`, `rise` |

The cast sound is recut to 0.70 s (`tools/make_spell_cast_cues.py`, window 0.93-1.63 s of the source).
By the 50 ms RMS envelope of the written wav it rises to its peak in the last 0.1 s: the riser peaks on the blow.

## What the frames show

**Swing (treant).** The lantern fades in over the treant's crown, chain going up behind the HUD plate.
It hangs left of the trunk, swings to the right across the canopy, through vertical at about 0.47 s,
and is at its right extreme (about 18 deg) on the last frame before the burst. Smoke rises behind it,
thin, brown-orange, drifting right; small sparks fall off the lantern. The censer covers the top of
the canopy (not the face).

**Swing (rat).** Same, but the censer hangs about a rat-height clear of the rat's head: nothing of the
rat is covered, and the swing reads more clearly than on the treant because it has air around it.

**Burst.** The frame after the last swing frame the lantern is gone and the sheet's ember column
stands where it hung, 3-4 times the lantern's size, with coals at its base and sparks to either side.
The Burn badge ("3") is on the same frame. On the treant it sits on the crown; the column is tall enough to
run behind the HUD plate. On the rat it is a standing column above the head, base embers touching
the ears on the first frame. The column lasts about 0.25 s, then thins to smoke and is gone by about 0.4 s.
The 28 particles are small next to the column; I do not see a "shower", mostly a halo of sparks at the edge.

**Fire at the feet.** Two clumps of flame, one each side, joined by a low bed of coals, fade in
within two frames of the cue. They hold at about the same size and brightness for roughly 0.6 s,
the clump shapes shifting from frame to frame (the breathe), and do not dim until the last 0.3 s or so,
by about 1.1 s after the cue only coals remain. The orange coal glow on the body sits over the same
time and carries on after. Sparks drift up from the fire. On the rat, one small flame sits over the
head for about 0.3 s and looks parked.

## What is weak

- The height clamp: raising the censer from 0.9 to 1.3 body-heights moved it not at all on the treant
  (the sky ceiling holds it at the crown), so the censer swings over the canopy and hides its top.
- The burst is one big drawing (the sheet's f5) on a 0.1 s hold plus 0.15 s fade; it is the
  one element of the payoff that is not made of parts, and its size dwarfs the censer it replaces.
  Shower of embers is mostly hidden inside it.
- Swing is a rigid lantern turning about a point, because the art is one still. The chain stays
  straight; no chain curve, no lantern tilt of its own, no stop at the extremes.
- The fire is two clumps and a bed at ground level: "standing in fire", not "burning". The flames
  do not climb the body; that is left to the coal glow.
- The smoke is faint against the dark forest and reads at the edge of notice.
- The stray parked flame on the rat (above).
- Not measured: `spell_feel` WARN/accent numbers were not re-run for this pass; no `-Darkness` capture.
