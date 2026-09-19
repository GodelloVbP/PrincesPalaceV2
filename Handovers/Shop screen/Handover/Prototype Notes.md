# Shop Screen v2 — prototype notes (deviations from the README handoff)

`Shop Screen v2.dc.html` is the interactive prototype and is now authoritative for
layout/behaviour, per the handoff's own rule. Changes made during design review:

- **PACK button opens the Pack modal** — a dialog listing the run's bag for selling
  (name, meta, sell price, arm → sell 1 / sell all). It is the *only* thing PACK
  opens. It replaced the original "SELL FROM BAG" panel that sat inline in Row C;
  there is no separate bag/inventory surface anywhere else in this screen.
- **Spell slots / recipient assignment removed from the shop.** Buying a spell book
  now behaves exactly like buying gear or a relic (arm → confirm → SOLD). Assigning
  a learned book to a character's 3 slots happens in the character/inventory screen,
  which is out of scope here (per the user's direction).
- **Layout is a single fixed-viewport panel grid**, not the coordinate-exact
  horizontal rows from README §3: RELICS / SPELL BOOKS / SHOPKEEPER art across the
  top, GEAR (2×2) + a SHOP actions panel (gold, reroll, pack, leave) on the bottom.
  No scrolling.
- **Persistent detail panel replaced with hover tooltips.** Every buyable row shows
  name/kicker/effect in a tooltip on hover instead of a fixed "select a card" panel.
- Icons are drag-and-drop placeholders (no art pipeline hooked up yet); a shopkeeper
  portrait placeholder was added per the sketch.

Everything else — reroll doubling/ceiling, afford/unafford/sold/no-offer states,
gold token colors, LEAVE two-press confirm, Escape behaviour — follows the README.
