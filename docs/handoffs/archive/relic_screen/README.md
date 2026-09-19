# Handoff: Relic Screen — shared pool, per-hero equip

## Overview

Redesign of the relic/equipment screen from the attached reference screenshot.
Same 3-state card language (equipped / held by another hero / unclaimed) but
restyled into the project's painterly gold-rim-on-dark system, and the same
hero-switcher component as the Talent Tree screen.

## Files

`Relic Screen.dc.html` (open directly, `support.js` beside it) — interactive
prototype. `reference_screenshot.png` is the original UI this replaces.

## Layout (1920×1080 reference)

- Hero switcher tabs: top-left, x 56 y 44 (identical component to Talent Tree).
- Hero name + "Equipped: X" line: centred, y 150–170.
- Relic card row: centred, y 320, cards 320×200 min, `gap 36`, wraps if the pool
  grows past what fits one row.
- Back button: bottom-centre, y -44 from bottom.

## Card states

| State | Border/badge | Behaviour |
|---|---|---|
| Equipped (owned by active hero) | Green→gold accent, "EQUIPPED" badge, pulses off | Click to unequip |
| Held by another hero | Muted red accent, "HELD BY {NAME}" badge | Click to reassign to active hero |
| Unclaimed | Neutral gold border, pulsing beacon, no badge | Click to equip to active hero |

## Data model

Relics are a **shared party pool**, not per-hero inventories — each relic has at
most one `owner` field (hero id or null). Clicking a card while viewing hero A
reassigns that relic's owner to A, taking it from whoever held it (B loses it
automatically — no separate "unequip from B" step). Clicking your own equipped
card sets owner back to null.

## Design tokens

Same system as the rest of the suite — Chakra Petch, purple base gradient, gold
`#e7b25c` accents. Card accent colour is state-driven (green `#8fe07f` equipped,
red `#e05a5a` held elsewhere, gold `#e7b25c` unclaimed), independent of any
per-relic art.

## For the engine

- Icon is a placeholder gem/shard SVG — swap per relic; the state ring/glow/badge
  wrapper around it is chrome driven by ownership, not the icon art itself.
- Hero switcher tabs and their accent-color table are shared with the Talent
  Tree screen — build this as one reusable component if both ship together.
