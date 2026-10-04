# Ashen Reckoning, "after" (plan S3, spell 3.7)

Captured 2026-10-04 with `tools/gif.ps1 spell ashen_reckoning -PreStatus Poison` and
`tools/spell_feel.ps1` (with `PP_RUNTIME_PRESTATUS=Poison`). No painted art: the
existing twelve ignition frames and four particles. GIFs are untracked next to this
file. Owner gates skipped; the calls below are mine.

## What the runtime had wrong, fixed here

- **The two packets were not staggered.** The brief and the plan assumed "its two
  packets are already staggered", but `ResolveReclaim` never recorded them on the
  beat, so the stage showed one summed "-12" in a single colour. It now records the
  Poison and Fire packets, and the stage draws "-5" (Poison green) on the cue and
  "-7" (Fire red) one stagger step later. Pinned in `AshenReckoningTests`.
- **The capture tool refused the spell.** `PreviewStage` refuses Reclaim (it needs a
  status on the target), so every earlier capture was presentation-only: no number, no
  badge, no cue. The scenario runner now treats a Reclaim whose `-PreStatus` satisfies
  its `requiresStatus` as a real cast.

## Beats, authored seconds

| t | Beat |
|---|---|
| 0.00-0.30 | **Converge.** The target's own venom (f0-f4) streams in and spirals onto a forming coal. Opacity 0.85, glow 0.3, gone on the cue (no fade). |
| 0.30 | **First packet (Poison).** Sound (`ashen_reckoning_impact`), "-5", the Vulnerable badge, and the accent: the green swirl frame held hot (punch 0.3, glow 1.6, 0.07 s), 8 venom drops. |
| 0.35 | **Second packet (Fire), one stagger step later.** "-7", the second sound (`censer_of_embers_impact`, `secondSfxPath`), and the ignition: f5 (the coal bursts into flame, punch 0.35, glow 1.9) running on through the burn-down and ash (f6-f11, 14 fps), 14 embers, rock and ash. The poison catches fire. |

## Measured (spell_feel, 60 fps, with Poison)

| | 1x rat | 1.5x rat |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.38 s | 0.27 s |

The report WARNs "no accent" at 1x (peak 4.2 against a 4.2 threshold): the two accents
are 3 frames apart and the typed hit flashes sit under both, so no single peak stands out.
By eye f18 (green) and f21 (orange) are both clear.

## Frame notes

- **Cue frame.** On the rat at 1x the Poison accent, the Vulnerable badge and the log
  line open on f18. The ignition opens on f21, 3 frames later, as authored. The "-5" is
  faint on f18 and clear on f19 (the popup fades in); the "-7" is clear by about f24,
  one to three frames after the ignition: its popup carries the stagger delay plus the
  same fade-in. Number and accent are within about 2 frames of each other for packet 2,
  not the strict single frame of packet 1; I did not tighten the popup.
- **Flash guard.** Five consecutive casts are not recordable: each cast spends the
  Poison, so the second is refused and the series stops. One cast: two bright events 50
  ms apart (green then orange) over the rat's own typed hit flash, nothing near 25% of
  the frame. A cast is on a cooldown of 2 turns, so a second cast within a second is
  not reachable. Counted as one large flash.
- **Readability.** Both numbers sit above the body and read on every frame after the
  first fade-in; the fire burst (400 box) covers the rat's whole body for about 0.25 s but
  not the numbers, plates or the log. 1.5x recorded.
- **Not looked at:** `-Darkness 0.9` (the plan only asks it of Shackles and Viper).
- **Odd, not mine:** Shawn holds his staff down on the cast frame (the plan's capture
  notes saw the same in the before GIF), unlike his raised-hand cast pose.
- **Bug-shaped, fixed:** the two above.

## Identity test: "the poison caught fire"

My verdict: **passes.** Green venom streams onto a coal, the green swirl pops with a
green number, and 50 ms later the coal bursts into flame with an orange number: the
order green-then-fire is what says the poison caught fire. What is weak: the 3-frame gap
is short enough that at speed it reads as one two-colour burst, and the ignition frame
is the sheet's old drawing, a generic fireball with a rock in it.

## What a painted still would fix

A burst still of green poison flame turning to orange at its edge (prompt in
`docs/prompts/2026-10-03_spell_rework.md`) would let one drawing carry the catch, so the
ignition is not a fireball that happens to follow a swirl.
