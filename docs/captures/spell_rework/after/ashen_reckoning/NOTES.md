# Ashen Reckoning, "after" (plan S3, spell 3.7)

Captured 2026-10-04 with `tools/gif.ps1 spell ashen_reckoning -PreStatus Poison` and
`tools/spell_feel.ps1` (with `PP_RUNTIME_PRESTATUS=Poison`). Painted catch: `ashen_reckoning_catch-v2.png` (the v1 file is unused). GIFs are untracked next to this
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
| 0.30 | **First packet (Poison).** Sound (`ashen_reckoning_impact`), "-5", the Vulnerable badge, and the accent: the painted catch (`catch`, one still, `ashen_reckoning_catch` recipe, `green-soft`), tinted `#D8FF98`, 340 box, punch 0.3, glow 1.0, 0.12 s then a 0.2 s fade; 8 venom drops. |
| 0.35 | **Second packet (Fire), one stagger step later.** "-7", the second sound (`censer_of_embers_impact`), and `flare`: the same still, 400 box, opacity 0.8, punch 0.4, glow 1.8, 0.08 s then a 0.2 s fade over the catch; 14 embers, rock and ash. The poison catching fire is one drawing flaring, not a second drawing. |

## Measured (spell_feel, 60 fps, with Poison)

| | 1x rat | 1.5x rat |
|---|---|---|
| cast start to payoff | 0.30 s (f18) | 0.20 s (f12) |
| payoff to clean | 0.38 s | 0.27 s |

No warnings at either speed (the old "no accent" WARN is gone: the catch is the
one bright frame). Reports: `spell_feel_1x/`, `spell_feel_1.5x/`.

## Frame notes

- **Colour.** The painted core is teal-emerald, not the game's Poison green. Untinted it
  read as a cyan fire (frames judged, then tinted). `#D8FF98` pulls the core to green
  while the orange tips stay orange; stronger tints turned the tips olive. Tint is on
  both layers so the flare does not re-add teal.
- **Cue frame.** The catch opens on the frame after the last coal frame, with the number
  (checked per frame, no empty frame). Before 2026-10-04 one empty frame sat between them:
  see "Wind-up to payoff handoff" in `../../README.md`.
- **Numbers.** "-5" and "-7" sit inside the upper flame of the burst on the treant and
  the rat. Readable (bright against the dark teal) but not clear of the art; the old
  fireball covered them the same way. Not moved.
- **Cover.** The burst (340-400 box) hides the rat for about 0.25 s and the treant's face
  and trunk for about 0.3 s. The rat is visible through gaps from about 0.1 s after the cue.
- **Flash guard.** One cast: green catch, 50 ms later the flare, over the target's typed
  hit flash. Counted as one large flash. A cast spends the Poison, so no series.
- **Odd, not mine:** Shawn holds his staff down on the cast frame.

## Identity test: "the poison caught fire"

Verdict: **passes**, better than the stand-ins. Green venom winds onto a coal, a single
burst of green flame with orange tips opens with the green number, and 50 ms later the
same burst flares with the orange number. Weak spots: with the tint the green is a
flat emerald, not the toxic yellow-green of the venom frames before it; the burst is
large for a rat and reads as an explosion more than as vapour igniting; the rock in the
`settle` emitter (cells 13-16 of the old atlas) still flies out and is the one leftover
of the old fireball.
