# Handoff: Shop Screen — upgrades & consumables

## Overview

Redesign of the shop screen from the attached reference screenshot. Currency
readout moves from plain text to the same gold/relic chip pairing used on the
Run Map and Hub screens; rows keep the source's gold-rimmed-plate language but
add a purchase pulse and immediate afford/unafford feedback.

## Files

`Shop Screen.dc.html` (open directly, `support.js` beside it) — interactive
prototype. `reference_screenshot.png` is the original UI this replaces.

## Layout (1920×1080 reference)

- Currency HUD (Gold + Relics chips): top-centre, y 56.
- Two columns, "UPGRADES" left / "ITEMS" right, each 640px wide, centred with a
  120px gutter between them, starting y 200.
- Rows: 14px gap, each a full-width plate with name left, cost/status right.
- Back button: bottom-centre, y -44 from bottom.

## Row behaviour

| Type | Rule |
|---|---|
| Upgrade (one-time) | Click deducts gold once, row becomes permanently "OWNED" (green, non-interactive) — matches the source's "Extra Recruit Slot (Owned)" |
| Item (repeatable) | Click deducts gold, row stays purchasable; dims and stops responding to clicks only when `gold < cost` |

Rows at 55% opacity + no pointer when unaffordable; full opacity + pointer
cursor when affordable or already owned.

## Design tokens

Same system as the rest of the suite. Gold chip `#e7b25c`/`#ffe0a8`, relic chip
violet `#b48cff`/`#e6d8ff` (matches the event/relic accent used on the Run Map).
Owned rows use success green `#7fc25a`/`#bde8a8`.

## For the engine

- State is a flat `{gold, relics, owned: {id: bool}}` plus static upgrade/item
  definition lists (`{id, name, cost, oneTime}`) — port the definitions from
  your real economy data, the interaction logic is generic over that shape.
- Relics currency is displayed but nothing in this mock spends it — flag if any
  row should cost relics instead of/alongside gold.
