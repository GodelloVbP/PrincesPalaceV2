# Handoff: Talent Tree — branching vine layout

## Overview

Replaces a flat 6-column button grid with a rooted, branching talent tree: a hero
crest at the root, six limbs fanning upward, each ending in a keystone capstone.
Hover any node to see its cost and effect; click to invest. Same Chakra Petch /
purple-gold painterly language as the rest of the suite.

## Files

`Talent Tree.dc.html` (open directly in a browser, `support.js` beside it) — fully
interactive prototype, authoritative for layout, state logic, and behaviour. Not
final art — node circles, vine lines, and the crest are placeholder shapes.

## Layout (1920×1080 reference)

- Hero switcher tabs: top-left, x 56 y 34.
- Hero name + live stat line: below tabs, x 56 y 78.
- Talent points counter + progress bar: top-right, x -56 y 70, 300×~70.
- Tree canvas: x 0 y 170, 1920×760. Root at (960, 870) in canvas-local coords;
  6 branch columns at x = 220/516/812/1108/1404/1700; tier rows at y =
  90(keystone)/230/370/510/650(bottom, closest to root).
- Info panel (hover/selected node): bottom-left, x 56 y -96 from bottom, 560×150 min.
- Respec / Back buttons: bottom-centre.

## Node states

| State | Look |
|---|---|
| Locked (prereq not invested) | Grey fill, dim border, 50% opacity, no glow |
| Available (prereq met, affordable) | Dim gold outline, pulsing beacon glow |
| Available but unaffordable | Same as available, muted border, no beacon |
| Invested | Gold radial fill, purple-gold rim, checkmark, drop glow |

Keystone (tier 0, 3pt) nodes render larger (96px vs 76px) with a heavier border.

## Prerequisite logic

Each of the 6 branches is a strict chain: tier 4 (bottom, cheapest, closest to
root) → tier 0 (top, keystone, 3pt). A node is investable only once the node
directly below it in its own branch is invested; the root is always "invested."
Un-investing is allowed only on the topmost invested node per branch (nothing
above it invested) — keeps every chain always in a valid state; Respec clears all
branches at once.

Cross-links between adjacent branches at the tier-2 row are **decorative only**
in this mock — they don't gate anything. Flag if you want them to become real
shared prerequisites.

## Hero switcher

3 heroes included (Sheep, Wolf, Owl), each a full 30-node tree with its own
accent colour, base stats, and starting invested nodes — swap in your real
roster's data using the same shape.

## Design tokens

Same as the rest of the project: Chakra Petch throughout; gold `#e7b25c` /
`#ffe0a8` for currency and positive state; purple base gradient
(`#241736`→`#07040c`); per-hero accent drives node/vine glow colour (Sheep
`#e7b25c`, Wolf `#e05a5a`, Owl `#b48cff`).

## For the engine

- Node = circle + cost badge + label, all driven by one state object per hero
  (`invested: Set<nodeId>`). `nodeId = "b{branchIndex}-{tier}"`.
- Vine segment between two nodes lights up (grey→accent gold) the instant the
  lower node becomes invested — no separate "unlock" animation asset needed,
  just a colour/dash swap on the same stroke.
- Stat readout at the top sums flat bonuses parsed from each invested node's
  grant text — port as a real stat-modifier system, this is a display-only stub.

## Addendum (2026-08-01) — Phase 7 design gate, confirmed answers

This mock's shape (6 branches × 5 tiers = 30 nodes/hero) was superseded before
this phase started — the shape actually being built is **3 paths × 21 points
per character**, per the workflow-restructure plan (`docs/WORKFLOW.md`'s
linked execution blueprint). Five questions were confirmed with the designer
before any Phase 7 code was written, all recommended defaults accepted:

1. **Node economy**: 21×1-point nodes per path (not an escalating cost curve).
   Every node costs exactly 1 point; the tree's shape alone paces investment —
   matches how the pre-Phase-7 (150-talent) tree already worked.
2. **Obtainable-points cap**: ~30 of the 63 total (21×3 paths) obtainable
   across a playthrough — forces roughly 1.5-path specialization, no
   character maxes every path.
3. **Path themes**: Claude drafts a themed 3-path split per character from
   their existing role/kit, for the designer to review rather than blocking
   content authoring on a from-scratch spec.
4. **Respec/refund semantics**: unchanged from the current rule — un-invest
   only the topmost-invested node on a path; Respec clears every path at
   once. Applied to the new 3×21 shape, not redesigned.
5. **Scroll behavior**: vertical scroll per path, matching what the delivered
   art kit (`Art/UI/TalentTree/Processed/`) assumed.

The state model, node visual states table, and vine-lighting behaviour above
still apply — only the branch count (6→3) and per-branch node count (5→21)
changed.
