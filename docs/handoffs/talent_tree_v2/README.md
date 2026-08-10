# Handoff: Talent Tree v2 — 3×3 root grid, double convergence

## Overview

Replaces the hero-switcher vine tree with three independent point-based paths
that never intertwine, each sharing one skeleton: a 3×3 grid of roots, a single
convergence into one trunk, a 3-way branch into limbs, and a second convergence
at the capstone. Same Chakra Petch / purple-gold painterly language as the rest
of the suite, with an orange progress accent for this screen specifically.

## Files

`Talent Tree v2.dc.html` (open directly in a browser, `support.js` beside it) —
fully interactive prototype, authoritative for layout, state logic, and
behaviour. Node circles and connecting lines are placeholder shapes, not final
art.

## Layout (1920×1080 reference)

- Back control: top-left, x 56 y 28 — left arrow + "BACK" label.
- Mid title/indicator: top-center — reads "TALENT TREE" by default, swaps to
  the currently-hovered orb's name.
- 3 path columns, centered at x = 340 / 960 / 1580, never overlapping.
- Per-path skeleton (depth = vertical tier, 0 at bottom):
  - Depth 0: single root.
  - Depth 1–3: 3×3 grid — 3 roots (dx ±150/0), each climbing 3 rings straight up.
  - Depth 4: **first convergence** — all 9 grid-tips feed one node (OR prereq).
  - Depth 5–7: convergence branches into 3 limbs (left/center/right), each
    climbing 3 more orbs independently.
  - Depth 8: **second convergence** — all 3 limb-tips feed the capstone (OR
    prereq), same mechanic as the first.
- Info bar: bottom, spanning left 56px to right 56px, height 88 — shows
  hovered orb's cost chip, name, grant, and one-line flavor.
- Respec All / Back buttons: bottom-right, 210×88 stack.

## Node states

| State | Look |
|---|---|
| Locked (prereq or point-gate not met) | Grey fill, dim border, 50% opacity, no glow |
| Available (prereq + gate met, affordable) | Dim orange outline, pulsing beacon glow |
| Available but unaffordable | Same as available, muted border, no beacon |
| Invested | Orange radial fill, glow, checkmark |

Capstone renders largest, convergence orbs render slightly larger than regular
orbs — sizing marks role only, independent of point cost.

## Prerequisite logic

Every orb's `prereq` is either a single parent id (strict chain) or an array of
parent ids (convergence — **any one** invested parent satisfies it, not all).
Both convergence orbs in every path use the array form. Un-investing an orb is
only allowed when none of its children are invested (checked generically against
every orb that lists it as a parent, so it works the same for chains and
convergences).

**Point gates (new):** in addition to the prereq check, two orbs per path also
require a minimum number of points already spent *in that path*, regardless of
which route got you there:
- First convergence: requires **8** points already invested.
- Capstone (second convergence): requires **20** points already invested.

This is a separate `minSpent` field per node, checked alongside prereq — see
`minSpentMet()` in the logic class.

## Cost system

Every orb has a `cost` field (defaults to `1` if omitted in the `node(...)`
constructor call — see the last argument). Spend, remaining-points, and
afford-checks all read `node.cost` rather than assuming 1, so **individual orbs
can be set to cost more than 1 point** by passing that argument — no other code
changes needed. Nothing in the current data uses a cost above 1 yet; that's an
intentional decision left to design/balance, not a limitation.

## Design tokens

Chakra Petch throughout; orange `#ff9145` / `#ffd9a2` for currency, progress
line, and invested state (this screen's signature glow, distinct from the
gold used elsewhere in the suite); purple base gradient (`#241736`→`#07040c`).

## For the engine

- One `Set<nodeId>` of invested orbs per path drives all state.
- `PATHS[i].list` is a flat array of `{id, prereq, dx, y, kind, name, grant,
  desc, minSpent, cost}` — `prereq` is `null | string | string[]`.
- Edge (connector line) lights up orange only when **both** its own node and
  that specific parent are invested — correct for convergences, where an
  unused alternate route stays dark even if the convergence itself is lit.
- All three paths currently share the identical node skeleton (only names/
  grants differ) — diverge them later by editing the `VIGOR`/`FEROCITY`/
  `CUNNING`-named arrays independently if per-path shapes are wanted again.
