# Handoff: Shop Screen v2 — the in-run merchant

## 1. Overview

The Shop is a room on the descent map. Walking into it opens a full-screen
panel over the map where the party spends **run gold** — the gold banked by
fights and treasure this descent, which is lost when the run ends — on spell
books, gear, relics, and sells unwanted bag items back at a poor rate. It has
four sections and a reroll, and leaving clears the room like any other.

It **replaces nothing**, because v2 has no store of any kind: the hub's
Principality button is wired to a `Debug.Log` and there is no store screen in
the game. `docs/handoffs/shop/` is a **v1** handoff for a permanent-Gold hub
store with UPGRADES/ITEMS columns; this screen is not a restyle of that one and
should not inherit its two-column shape. It is also the first in-run sink for
gold the game has ever had.

The other half of what this screen sells is new. Two things change together,
revised 2026-09-02 against the author's own reading of "the standard loadout
disappears":

- The automatic, nameless "Skill" action every character casts today, purely
  off level (`Spark`…`Ascendance`), **disappears** — no free spell at run
  start beyond whatever the character's own kit already unlocks by level
  (for the sheep: `shear`, `woolgathering`, `battering_ram` — none of them a
  spell).
- The named spells — Mud Burst, Static Fleece, Frost Flare, Lightning Bolt,
  Golden Fleece — stop unlocking by character level and become **books found
  during a run and learned into 3 slots per character, per run**, carried over
  into nothing. A book teaches one of these existing spells; it does not add a
  new one. That is why the screen carries a spell-slot strip: a book purchase
  has to show what it would replace before it is bought.

Ward, Woolgathering, Shear, Battering Ram and every enemy skill are ordinary
skills, not spells, and none of them is touched by any of this.

**The two changes do not ship together.** `docs/PLAN_SHOP.md` §4 stages them:
books are added **additively** first — they teach the five spells while those
spells still unlock by level — then the shop, the screen and the bot batches,
and only last do the five spells stop unlocking by level and the free spell
disappear, gated on what the batches measured. This screen is specified against
the finished state and is built once, in the middle of that sequence; nothing
in §3-§6 changes with the staging. It is worth knowing only because a build of
this screen may run against a game where a character still has spells it did
not learn from a book, and the ownership labels in §4 have to be right in that
world too.

Build detail, formulas and the engine-side plan: `docs/PLAN_SHOP.md`.

## 2. Files

| File | What it is |
|---|---|
| `README.md` (this file) | The spec. Authoritative for layout, states and interaction until a prototype exists. |
| `Shop Screen v2.dc.html` + `support.js` | **To be produced by the designer.** The interactive prototype. Once it exists it becomes authoritative for layout and behaviour, and this README's §3-§5 become the written record of what was asked for. |
| `GAP_AUDIT.md` | The build-vs-spec record. Every row is "not built" today. |

**Visual anchors.** Two reference screenshots already in the repo:

- `docs/handoffs/shop/reference_screenshot.png` — the v1 hub store. Use it for
  the *plate* language (gold-rimmed rows, cost right-aligned) and for nothing
  else; its layout and its currency model are both wrong for this screen.
- `docs/handoffs/relic_screen/reference_screenshot.png` — the relic screen.
  This is the closer anchor: card row, state-driven accent colour, the
  gold-rim-on-dark treatment, and the paging behaviour the relic section needs.

**Live v2 renders were not produced, deliberately.** `tools/screenshot.ps1`
can only target panels registered in `ScreenRegistry.All`, which is five —
MainMenuPanel, HubPanel, MapPanel, TalentPanel, FightPanel. There is no hub
store panel to render, and the relic draft is a nested overlay inside HubPanel,
so neither of the two screens asked for is capturable by name. Beyond that,
`-v2-TestRunner2`'s `Temp/UnityLockfile` was live (touched 2 minutes before the
check) and `-v2-TestRunner` has no `Temp/` at all, meaning a render there would
have paid for a cold Library import while another session held the second
runner. The two committed reference PNGs above are the anchors instead.

## 3. Layout — exact coordinates (1920×1080 reference)

Coordinates are **top-left origin, pixels**, `(x, y, w, h)`. The engine's
`Place.At` is centre-relative; converting is the implementer's job, not a
change to the design.

The panel is a fixed 1920×1080 sheet over the map, with a scrim behind it.

### Header — y 0-96

| Element | (x, y, w, h) | Notes |
|---|---|---|
| Header strip | 0, 0, 1920, 96 | Full-bleed, one hairline along its bottom edge |
| Title `SHOP` | 800, 26, 320, 44 | Centred, 38px |
| Gold chip | 1360, 24, 240, 48 | `1,204 G`, right-aligned inside the chip. Same chip component as the Run Map's gold readout |
| Reroll button | 1620, 24, 260, 48 | Label is two lines' worth on one: `REROLL · 25 G`. **The price is always visible on the button**, and it changes as it climbs |

### Row A — spell books, recipients and detail, y 120-428

| Element | (x, y, w, h) |
|---|---|
| Section header `SPELL BOOKS` | 56, 120, 320, 28 |
| Spell card 1 | 56, 156, 380, 236 |
| Spell card 2 | 476, 156, 380, 236 |
| Spell card 3 | 896, 156, 380, 236 |
| Section header `SPELL SLOTS` | 1316, 120, 320, 28 |
| Recipient strip | 1316, 156, 548, 140 |
| Detail panel | 1316, 316, 548, 112 |

**Three recipient rows, not five.** The brief originally sized this strip for
five characters; the save cannot field five. `SaveData.EffectiveMaxSquadSize()`
is `BaseMaxSquadSize + (extra_recruit_slot ? 1 : 0)` and `BaseMaxSquadSize` is
**1** (`Data/SaveData.cs:53`, `:163-166`), so the ceiling today is **two**.
Three is the same headroom `ReckoningScreen` takes for the same reason —
`RowCount = 3`, "it must cover the largest party the save can field… the
design's stated target is 3" (`ReckoningScreen.cs:91-96`) — and it is pinned
the same way, by a test asserting the row count against
`EffectiveMaxSquadSize()` rather than against the literal 3. Unused rows are
hidden. The two rows this saves (96px) are what the detail panel is built from.

Inside the recipient strip, one row per **fielded** character, 548×40, 8px gap,
first row at y 158 (3 rows = 3×40 + 2×8 = 136, fits 140):

| Element | offset within row |
|---|---|
| Selected check `✓` | +2, +12, 16, 16 |
| Character name | +24, +8, 148, 24 |
| Slot chip 1 | +184, +4, 112, 32 |
| Slot chip 2 | +304, +4, 112, 32 |
| Slot chip 3 | +424, +4, 112, 32 |

A chip carries the learned book's name, or `EMPTY`. The check glyph is drawn
only on the selected recipient (§4) — a shape cue, so selection does not depend
on reading a colour.

### Detail panel — 1316, 316, 548, 112

Persistent, not a tooltip. It shows whatever card or row currently has focus,
falling back to whatever is hovered. Empty state is a single centred
`SELECT A CARD` in `Text muted`. Offsets from the panel origin:

| Element | offset | Contents |
|---|---|---|
| Title | +16, +10, 296, 26 | The card's name |
| Kicker | +16, +40, 296, 20 | Gear: the equip slot. Relic: rarity. Spell: `TIER 2 · 18 MP · CD 3` |
| Sub-body | +16, +64, 296, 36 | Gear: affix names, up to two lines. Spell: target shape. Relic: nothing |
| Body | +328, +10, 204, 92 | Gear: the derived-stat delta, one line per changed stat. Weapon: the damage line. Relic/spell: effect text |

The engine notes for what fills this, and the two things the comparison
deliberately does **not** compute, are in `docs/PLAN_SHOP.md` §3d.

### Row B — items, y 412-652

| Element | (x, y, w, h) |
|---|---|
| Section header `GEAR` | 56, 412, 320, 28 |
| Item card 1 | 60, 448, 420, 204 |
| Item card 2 | 520, 448, 420, 204 |
| Item card 3 | 980, 448, 420, 204 |
| Item card 4 | 1440, 448, 420, 204 |

### Row C — relics and sell, y 672-928

| Element | (x, y, w, h) |
|---|---|
| Section header `RELICS` | 56, 672, 320, 28 |
| Relic card 1 | 56, 708, 380, 220 |
| Relic card 2 | 476, 708, 380, 220 |
| Relic card 3 | 896, 708, 380, 220 |
| Section header `SELL FROM BAG` | 1316, 672, 320, 28 |
| Sell viewport | 1316, 708, 548, 220 |

**There is no pager.** The `1 / 6` pager the first draft carried was copied
from the relic draft screen, which pages because the level-70 track reward
shows the *whole* relic pool. A shop shows `ShopStock.RelicCount` cards — three
— from a pool it never displays, so there is nothing to page through. Paging
returns the day a count constant exceeds the cards a row can show, and not
before. Its 212×40 went to widening the `RELICS` header to match every other
section header at 320.

The sell viewport scrolls vertically. Rows are 548×44 with a 4px gap — 4.5
visible, which is intentional: a half-row at the bottom edge is the cheapest
possible affordance saying "there is more".

| Element | offset within sell row |
|---|---|
| Item name | +12, +10, 212, 24 |
| Meta (`T10 · +5 · 3 AFFIX`) | +232, +13, 148, 18 |
| Sell price (`+ 9999 G`) | +388, +10, 148, 24 |

Widened from 300/108/104 against §3's worst-case strings: `T10 · +5 · 3 AFFIX`
did not fit 108px, and no sell label fits 104px once the price can be four
digits. The name field gives up the width because a truncated item name is
recoverable — it is in the detail panel — and a truncated price is not.

**When a row is armed and its stack holds more than one**, the meta and price
fields are replaced by two quantity chips in the same footprint:

| Element | offset within armed sell row |
|---|---|
| `1 · 10 G` | +232, +6, 148, 32 |
| `ALL 3 · 30 G` | +388, +6, 148, 32 |

A stack of one shows no quantity choice — the price field simply becomes
`SELL · 10 G` and takes the confirming press. Either way a sale is two presses;
a stack of six is two presses, not six.

### Footer

| Element | (x, y, w, h) |
|---|---|
| Leave button `LEAVE` / `LEAVE?` | 800, 952, 320, 64 |

The label is the whole confirm: one press arms it, the second leaves (§5.6).
Unchanged in size — `LEAVE?` is shorter than the width already allows.

### Card internals

**Spell / relic card (380×220 or ×236), offsets from card origin:**

| Element | offset |
|---|---|
| Icon | +142, +16, 96, 96 |
| Name | +20, +124, 340, 28 |
| Sub-line (relic rarity, or spell `TIER 3 · 18 MP`) | +20, +156, 340, 20 |
| Price chip | +100, +184, 180, 32 |

**Item card (420×204):**

| Element | offset |
|---|---|
| Icon | +170, +12, 80, 80 |
| Name | +12, +100, 396, 26 |
| Meta (`TIER 3 · +2 · 1 AFFIX`) | +12, +130, 396, 20 |
| Price chip | +120, +158, 180, 32 |

### Worst-case strings, per field

`UiTextFitAudit` checks fit against the string actually assigned, so a field
sized for today's copy fails the day a price gains a digit. These are the
longest English strings each field must hold; English only, and localization is
out of scope (§8).

| Field | w | Worst case | Why |
|---|---|---|---|
| Card price chip | 180 | `CONFIRM · 9999 G` | The armed label is the longest state, not the idle price |
| Card price chip, unaffordable | 180 | `🔒 NEED 9999` | Lock glyph plus a four-digit shortfall |
| Reroll button | 260 | `REROLL · 9999 G` | The price saturates at 9999 (`PLAN_SHOP.md` §2b) so five digits are unreachable |
| Gold chip | 240 | `9,999 G` | Run gold; the measured ceiling is well under this |
| Sell row meta | 148 | `T10 · +5 · 3 AFFIX` | Max tier, max plus, max affix slots |
| Sell row price | 148 | `SELL · 9999 G` | Deliberately shorter than the card's `CONFIRM · …`, so it fits 148 — sell and buy must not read the same anyway |
| Sell quantity chip | 148 | `ALL 99 · 9999 G` | A bag stack has no authored cap |
| Slot chip | 112 | The longest book display name | `LIGHTNING BOLT` today; a longer one truncates with an ellipsis and the detail panel carries the full name |
| Recipient row, full | 148 name field | `REPLACE REQUIRED` replaces the three chips' region, not the name | See §4 |
| Spell card badge | 340 | `OWNED BY SHAWN` / `CHOOSE RECIPIENT` | Both fit the card's full inner width |
| Detail panel title | 296 | The longest item display name | Truncates; the card above it is the redundant copy |

### Vertical budget check

Two columns now, so the check is per column rather than per band.

**Left column, x 56-1276.** Section headers 120-148; spell cards 156-392; gap
20; `GEAR` header 412-440; item cards 448-652; gap 20; `RELICS` header 672-700;
relic cards 708-928; gap 24; leave button 952-1016; bottom margin 64.

**Right column, x 1316-1864.** `SPELL SLOTS` header 120-148; recipient strip
156-296; gap 20; detail panel 316-428; gap 20; item card 4 (1440-1860) 448-652;
gap 20; `SELL FROM BAG` header 672-700; sell viewport 708-928.

The detail panel's bottom bound is set by item card 4, which reaches into the
right column's x-range — 428 leaves the same 20px every other band gap uses.
Nothing overlaps anything in either column.

This must survive the layout audit at 1920×1080, 2580×1080, 1920×1440 and
1920×1200 (`Domain/UiKit/UiFrames.cs:16-22`) — the panel is fixed-size and
centred, so the only expected exemption is the panel covering the map behind
it.

## 4. States

| Element | State | Reads as |
|---|---|---|
| Any card | **Affordable** | Full opacity, gold border `#E7B25CB3`, pointer cursor, price chip in gold text `#FFD9A2` |
| Any card | **Unaffordable** | 55% opacity, border drops to `#B496D233`, no pointer, price chip becomes a lock glyph plus `NEED 24` in red `#E07A62` — the shortfall, not the price, is the information, and the glyph is what carries it for a reader who cannot see the red |
| Any card | **Sold out** | 35% opacity, contents replaced by a centred `SOLD` in `#8A7AA0`, border `#B496D233`, no pointer. **The card stays in place** — it does not collapse and the row does not re-centre |
| Any card | **Armed** (first press) | Border brightens to `#FFC45A`, price chip label becomes `CONFIRM · 24 G`, one settle-then-hold pulse. The label change is the state; the pulse only draws the eye to it |
| Any card | **No offer** | The section's pool held fewer candidates than it has cards. A centred `NO OFFER` in `#8A7AA0`, border `#B496D233`, no pointer. **The card stays in place** — it is not hidden, not filled with a duplicate, and the row does not re-centre |
| Any card | **Keyboard focus** | A 2px `Focus outline` ring drawn outside the border, in addition to whatever else the card is. A ring rather than a colour swap, so it is still visible on an armed card that is already amber |
| Relic card | **Owned already** | Not offered at all — a held relic is filtered out of the roll. There is no owned state to draw |
| Spell card | **Owned by everyone** | Not offered. A book already in every fielded character's slots is filtered out of the roll — no press on it could do anything |
| Spell card | **Owned by one** | Offered, with an `OWNED BY {NAME}` badge top-left in `#8FBF6A`. Buyable for a *different* character; that owner's row is inert (below) |
| Spell card | **Owned by several** | Offered, badge reads `OWNED BY 2` — the count, not a list of names, because the badge is 340px and the roster is not bounded by it. Which characters own it is legible from the strip: their rows are inert |
| Spell card | **Armed, more than one eligible recipient** | The card's badge reads `CHOOSE RECIPIENT` in `#E7B25C`. This replaces the first draft's global `REPLACES A SPELL`, which claimed a thing about the purchase that is only true of *some* recipients |
| Recipient row | **Eligible** | Normal row treatment, pressable while a spell card is armed |
| Recipient row | **Selected** | Row fill lifts to `#583216F2`, a `✓` at the row's left edge (§3), gold accent border |
| Recipient row | **Already owns this book** | Inert: 55% opacity, not pressable, not focusable. The card's `OWNED BY` badge says why |
| Recipient row | **All three slots full** | Selectable, and carries `REPLACE REQUIRED` in `#E7B25C` across its chip region. Confirming on the card opens the replace picker rather than committing |
| Recipient row | **No eligible recipient exists** | Cannot happen — such a book is filtered out of the roll at roll time (`PLAN_SHOP.md` §2d) |
| Slot chip | **Filled** | Book name, `#E9DFF8` on `#120A1AD1`, quiet border `#B496D233` |
| Slot chip | **Empty** | `EMPTY` in `#7F6F98`, dashed border `#B496D233` |
| Slot chip | **Would be filled by the armed purchase** | Green accent `#8FBF6A`, book name previewed |
| Slot chip | **Would be replaced by the armed purchase** | Red accent `#E05A5A`, existing name struck through |
| Slot chip | **Pickable during replace-slot picking** | All three chips of the selected recipient gain a pointer and a `Focus outline`-weight border; the row's chip region carries the word `REPLACING` so the state is named, not only coloured |
| Sell row | **Idle** | Name `#E9DFF8`, price `#FFD9A2` prefixed `+` |
| Sell row | **Armed, stack of 1** | Row fill lifts to `#583216F2`, gold accent border, price label becomes `SELL · 10 G` and takes the confirming press |
| Sell row | **Armed, stack of 2+** | Same fill and border; the meta and price fields are replaced by two quantity chips, `1 · 10 G` and `ALL 3 · 30 G` (§3). The row itself is no longer the confirm surface |
| Sell row | **Not sellable** | Never rendered — worn gear is not in the bag (§5.4) |
| Reroll button | **Affordable** | Gold, price shown |
| Reroll button | **Unaffordable** | 55% opacity, lock glyph and `NEED {n}` on the label |
| Reroll button | **At the price ceiling** | Still drawn, still showing `9999 G`, still unaffordable. It does not disappear: a control that vanishes teaches nothing, which is the same rule the fight's skill submenu follows for unaffordable rows |
| Leave button | **Idle / armed** | `LEAVE`, then `LEAVE?` after one press. Leaving clears the room permanently, so it is the one action in the shop that confirms |
| Whole screen | **Rerolled** | Every card cross-fades to the new stock over ~200ms; the reroll button's price doubles in place; sold-out cards come back as fresh stock |

## 5. Interaction logic

### 5.1 The state machine

The first draft described this as "two presses" and left four questions
unanswered: what a press on a *different* card does, what happens when a spell
has two possible recipients, what Escape means, and whether arming a sell row
disarms a gear card. Here it is as a machine instead.

**Arming is per section.** Each of the four sections — spells, gear, relics,
sell — holds its own state independently, so arming a sell row does not disarm
an armed item card. The first draft's "only one thing is ever armed at a time,
across all four sections" is withdrawn: it made a player who armed a sell row
to check a price lose the card they were deciding about, and nothing is
committed by arming, so there is nothing for the exclusivity to protect.

**States.** `Idle → Armed → RecipientSelected → ReplaceSlotPicking →
Committed`, plus `LeaveConfirm` which belongs to the footer rather than to a
section. `RecipientSelected` and `ReplaceSlotPicking` are reachable only from a
spell card. `Committed` is transient: the mutation runs through its single
orchestrator method (`PLAN_SHOP.md` §2f), the card goes `SOLD` in place, the
gold chip counts down, and the section returns to `Idle` in the same frame.

**A purchase that would leave gold negative is refused at arm time** — the card
is unaffordable and does not arm — never at commit time. So no state below can
fail on money.

| | card press (unarmed, this section) | same card press | other card press (this section) | card press (other section) | recipient row press | slot chip press | quantity chip press | Escape | click outside | LEAVE |
|---|---|---|---|---|---|---|---|---|---|---|
| **Idle** | → Armed (affordable only) | — | → Armed on that card | that section → Armed; this stays Idle | ignored (rows are inert unless a spell card is armed) | ignored | ignored | ignored | ignored | → LeaveConfirm |
| **Armed** (gear / relic / consumable) | — | **commit** → Committed | disarm, → Armed on the new card | that section arms; this stays Armed | ignored | ignored | ignored | → Idle | → Idle | → LeaveConfirm |
| **Armed** (spell, 2+ eligible recipients) | — | no commit; card reads `CHOOSE RECIPIENT` | disarm, → Armed on the new card | that section arms; this stays Armed | → RecipientSelected (eligible rows only; owning rows inert) | ignored | ignored | → Idle | → Idle | → LeaveConfirm |
| **RecipientSelected** | — | recipient has a free slot: **commit** → Committed. Recipient full: → ReplaceSlotPicking | disarm, → Armed on the new card (recipient re-evaluated, preselected again if only one is eligible) | that section arms; this stays put | same row: → Armed (deselect), unless it is the only eligible one, which stays selected. Other eligible row: → RecipientSelected on it | ignored | ignored | → Idle | → Idle | → LeaveConfirm |
| **ReplaceSlotPicking** | — | → RecipientSelected (closes the picker; a mis-press must be recoverable) | → Armed on the new card, picker closes | that section arms; this stays put | other eligible row: → RecipientSelected on it, picker closes | **commit the swap** → Committed | ignored | → RecipientSelected | → Idle | → LeaveConfirm |
| **Armed** (sell row, stack of 1) | — | **commit** → Committed | disarm, → Armed on the new row | that section arms; this stays Armed | ignored | ignored | — | → Idle | → Idle | → LeaveConfirm |
| **Armed** (sell row, stack of 2+) | — | → Idle (disarm; the row is not the confirm surface, the chips are) | disarm, → Armed on the new row | that section arms; this stays Armed | ignored | ignored | **commit 1 or ALL** → Committed | → Idle | → Idle | → LeaveConfirm |
| **Committed** | — | — | — | — | — | — | — | — | — | — |
| **LeaveConfirm** (footer) | reverts label to `LEAVE`, press otherwise handled as Idle/Armed | as left | as left | as left | as left | as left | as left | reverts to `LEAVE`, swallowed | reverts to `LEAVE` | **leaves the shop** |

`Committed` accepts no input: it exists for exactly as long as the mutation and
the repaint, and every cell is a dash because there is no frame in which a
player can press anything.

**A spell card with exactly one eligible recipient never sits in `Armed`.**
Arming it resolves straight to `RecipientSelected` with that row preselected —
which is why the confirming press stays on the card rather than moving to the
row. A press that has no alternative decides nothing and should not be asked
for.

### 5.2 What each section does on commit

| Section | Effect |
|---|---|
| Gear | The item is added to the bag with its rolled plus and affixes. It is **not** equipped, and it is not auto-equipped into an empty slot either — deliberately unlike the post-fight reward path, which does (`PLAN_SHOP.md` §2c) |
| Consumable (a gear card may be a potion) | Added to the bag, stacking normally |
| Relic | Appended to the run's relics. There is no slot limit and no choice to make |
| Spell book | Learned into a slot on **one chosen character** — see 5.3 |
| Sell | One copy, or the whole stack, removed from the bag and its price credited |

### 5.3 Recipients and the replace picker

Buying a book always names a character, because a book belongs to one.

- Arming a spell card highlights the strip: every **eligible** recipient row —
  one that does not already know this book — becomes pressable, and its free
  chips show the green preview state. Rows that already own it are inert, and
  the card says why with its `OWNED BY` badge.
- **With exactly one eligible recipient, that row is preselected** — and the
  confirming press is still on the **card**, not on the row. The row is a
  fact about the purchase, not a step in it; making the player press a row
  they have no alternative to is a press that decides nothing.
- With two or more, the card reads `CHOOSE RECIPIENT` and a press on it does
  not commit. Pressing a row selects it (and the row shows the `✓` of §4).
- **A full recipient goes through slot picking.** Its row carries
  `REPLACE REQUIRED` before anything is pressed, so the extra step is known in
  advance rather than discovered by pressing. Confirming on the card then opens
  the picker: that recipient's three chips become individually pressable, each
  showing the red replace preview and the word `REPLACING`, and pressing one
  commits the swap.
- Escape backs out of the picker to the selected recipient. It does not leave
  the shop — nothing does but `LEAVE`.
- A replaced book is **gone**. It does not go to the bag, it cannot be
  re-bought at a discount, and the struck-through chip name is the whole
  warning.

### 5.4 Selling

Sell rows are the run's bag, which is the same bag the character sheet paints.
**Worn gear is not sellable** — it is not in the bag, so it is not in the list.
There is no dimmed "equipped" row to explain and no unequip affordance on this
screen, and there is no way to reach the character dossier from inside the shop
in v1 either. The path a player actually has:

> buy the upgrade (it goes to the bag, unequipped) → `LEAVE` → equip it from
> the dossier on the map, which displaces the old piece **into the bag** → sell
> that piece at the next shop.

Nothing is lost; it is banked one shop later. `PLAN_SHOP.md` §2c gives the
reason the alternative is worse — a shop that sold off the body would need the
whole equip/displace rule set inside it.

The empty-bag case shows a single centred line, `NOTHING TO SELL`, in
`#8A7AA0`.

Selling is two presses, and a stack is still two presses. The first press arms
the row. If the stack holds one, its price label becomes `SELL · 10 G` and the
second press commits. If it holds more, the row's meta and price are replaced
by two chips — `1 · 10 G` and `ALL 3 · 30 G` — and the second press picks one.
A stack of six potions is two presses, not six.

Sell price is 30% of what that exact copy would cost in this shop, rounded,
floor 1. Plus and affixes raise it because they raise the buy price. Selling
everything in a full bag does not approach a relic's price, and that is the
intent.

### 5.5 Reroll

One button, one price, always visible on the label. It rerolls **the whole
shop** — all three offer sections at once. The sell list is not stock and does
not change.

Price starts at 25 and **doubles per reroll**: 25, 50, 100, 200, 400 …
**saturating at 9999**, at which point the button stays visible and
unaffordable rather than disappearing. There is no reset, because a shop is
cleared on leaving and never re-entered — "per visit" and "per node" are the
same thing here, and the count is stored per node so that the price and the
stock's seed read the same number (`PLAN_SHOP.md` §2b).

Rerolling clears sold-out flags — the new stock is genuinely new. It does not
refund anything already bought.

### 5.6 Leaving, and what Escape does

**Escape never leaves the shop.** It leaves transient states only: it closes
the replace picker back to the selected recipient, and otherwise it disarms.
Pressed with nothing armed and no picker open, it does nothing at all. The
first draft's "Esc disarms; Esc again leaves" is withdrawn — leaving clears the
room permanently, and a key whose meaning changes from "undo the last half-step"
to "throw away a room" depending on invisible state is the shape of a mistake a
player cannot undo.

**`LEAVE` is the only way out, and it confirms once.** The first press changes
the label to `LEAVE?`; the second leaves, returns to the map and **clears the
room**. A shop is a one-visit room: the node is marked cleared and cannot be
re-entered, exactly like a treasure room. That permanence is the reason for the
confirm, and it is the same one-press-arms-one-press-commits pattern every
purchase on this screen already uses, so it is not a new interaction to learn.
Any other input — a card press, Escape, a click outside — reverts the label to
`LEAVE`.

Quitting the game inside a shop and returning finds the **same stock**, at the
same reroll price, with the same items sold out. This is not a nicety: without
it, quitting is a free reroll.

## 6. Design tokens

Taken from the existing suite — `FightHudPalette.cs` and the two handoffs named
in §2 — so this reads as part of the same game rather than its own thing.

| Token | Hex | Use |
|---|---|---|
| Panel ground | `#1A1024E6` | The shop sheet |
| Panel deep | `#120A1AEB` | Section panels (slot strip, sell viewport) |
| Card fill | `#12091C8C` | Card and row grounds |
| Row fill | `#120A1AD1` | Sell rows, slot chips |
| Row active | `#583216F2` | A selected sell row |
| Hover tint | `#C8AAE60F` | A row under the pointer |
| Border gold | `#E7B25CB3` | Affordable card border |
| Border quiet | `#B496D233` | Unaffordable / sold-out / empty-chip border |
| Hairline | `#C8AAE638` | Header underline, section rules |
| Scroll track / thumb | `#0B0612B8` / `#C8AAE68A` | The sell viewport's scrollbar |
| Gold accent | `#E7B25C` | Chips, section headers, the reroll button |
| Gold light | `#FFE0A8` | Gold chip numerals |
| Gold text | `#FFD9A2` | Price labels |
| Target amber | `#FFC45A` | An armed card's border |
| Text primary | `#F4EBFF` | Card names |
| Row name text | `#E9DFF8` | Sell row names, filled chips |
| Text secondary | `#BFB0D4` | Meta lines |
| Text muted | `#8A7AA0` | `SOLD`, `NOTHING TO SELL` |
| Text disabled | `#7F6F98` | `EMPTY` chips |
| Benefit green | `#8FBF6A` | Owned badge, would-fill slot preview |
| Harm red | `#E05A5A` | Would-replace slot preview |
| Shortfall red | `#E07A62` | `NEED {n}` price text |
| Focus outline | `#FFE0A8` | A 2px ring on the keyboard-focused element, drawn outside its border |

**Nothing on this screen is carried by colour alone.** Each state that has an
accent also has a glyph or a word: a lock plus `NEED n` on anything
unaffordable, a `✓` on the selected recipient, `REPLACING` across the chip row
during slot picking, `REPLACE REQUIRED` on a full recipient, `SOLD` and
`NO OFFER` as words rather than as opacity alone, and a focus ring that is a
shape rather than a recolour.

**On motion:** the project has no reduced-motion setting and this brief does
not add one. Keep every pulse subtle and short — the armed state is carried by
the label change (`24 G` → `CONFIRM · 24 G`) and the border colour, and the
single settle-then-hold pulse only draws the eye to a change that has already
happened in text. Nothing on this screen should animate for longer than the
~200ms the reroll cross-fade takes.

Type is Chakra Petch throughout. Section headers 28px uppercase with 2px letter
spacing in gold accent; card names 24px; meta lines 14px; prices 18px.

Geometry: 0 corner radius everywhere, 1px borders. Card gap 40px within a row,
20px between bands. This matches the relic screen's card language (380×220 min,
gap 36-40) rather than inventing a second card size.

## 7. For the engine

- **Everything gold on this screen is run gold** (`RunSnapshot.gold`), never the
  profile wallet. The gold chip reads the run, not `SaveData.Gold`.
- **Stock is seeded per node**, from a new `RngStreams.Shop` stream keyed to
  `(runSeed, step, nodeId, rerollIndex)` — the same property the treasure room
  already has. `Derive` takes only two position inputs today and gains a third
  with a default of 0, which is bit-for-bit identical for every existing caller
  (`PLAN_SHOP.md` F9). The coordinates are **not** packed into one field. The
  mock will fake all this with a plain shuffle; the real version must not, or
  quitting becomes a reroll.
- **The card counts are constants, not layout.** `ShopStock.SpellCount = 3`,
  `ItemCount = 4`, `RelicCount = 3`. A section never shows more cards than its
  constant, which is why there is no pager anywhere on this screen.
- **A card's price is resolved when the stock is rolled and persisted with it.**
  Recomputing on load looks free and is, right until a content patch or a
  pricing constant lands under an in-flight run and a card the player was
  looking at silently changes price. An open shop is a quoted price. The stock
  also carries a `stockVersion`, so a later change to the roll's shape leaves an
  already-open shop exactly as it was rolled rather than reshuffling it.
- **Every mutation is one orchestrator method that validates, then applies,
  then persists once** — `PLAN_SHOP.md` §2f has the ordering per mutation and
  what "atomic" does and does not mean against this project's save path.
  Nothing on this screen writes the save itself.
- **A shop purchase never auto-equips.** The post-fight reward path does
  (`RunOrchestrator.TakeOffer`), and reusing it here would be the easy mistake.
- **Prices come from the plan's formula** (`docs/PLAN_SHOP.md` §2b), not from
  `ItemDefinition.cost`. That field is v1 residue priced for a permanent-Gold
  store — roughly 5× too expensive for run gold, and 0 for every weapon in the
  game. Consumables are the one exception and do read `cost`.
- **`NEED {n}` is a shortfall**, computed as `price − gold`, not the price. A
  prototype that shows the price in red is showing the wrong number.
- **Cards are a fixed count emitted at scene-build time** and hidden when
  unused. Scenes are generated once and a runtime count cannot widen a tree.
  When a section's pool runs short — held relics excluded, known books excluded
  — the leftover cards render `NO OFFER` in place. They are never hidden, never
  filled with a duplicate, and the row never re-centres, because the screen
  binds cards by index.
- **The recipient strip is three rows and that number is asserted, not typed.**
  Pin it against `SaveData.EffectiveMaxSquadSize()`, the way
  `ReckoningTests.cs:159` pins `ReckoningScreen.RowCount`. A test carrying the
  literal `3` is the drift it exists to catch.
- **There is no minimum-resolution policy for this screen to state**, and that
  is deliberate: it is settled project-wide. The canvas is
  `ScaleWithScreenSize` at a 1920×1080 reference with
  `ScreenMatchMode.Expand`, i.e. `scale = min(w/1920, h/1080)`
  (`Editor/SceneBuilder/SceneBuilder.cs:203-210`), so the whole authored frame
  is always on screen and uniformly scaled at any resolution; only *aspect*
  can move nodes relative to each other, and the four aspects that are checked
  are `UiFrames.All` (`Domain/UiKit/UiFrames.cs:16-22`).
- **The sell list is `SaveData.stockpiledItems`**, filtered to `count > 0`, and
  stacks are keyed on `(itemId, plus, modifierIds, riftTier)` — two copies at
  different plus are different rows at different prices and must never merge.
- **All copy goes through `UiStrings.cs`** with numbers as template arguments; a
  direct `.text` assignment fails the build. Size price labels for four digits
  plus a suffix, not for today's two.
- The panel is **nested inside the Map screen**, not its own scene, which means
  `tools/screenshot.ps1 -Panel` cannot capture it — same as the relic draft
  today.

## 8. Explicit out-of-scope

- **The Events screen.** `RoomType.Event` generates at more than twice the
  shop's weight and is still a placeholder. The spell-teaching path this shop
  needs is also the thing blocking the wandering-mage event, so Events becomes
  buildable after this — but it is not specced here.
- **Any change to the hub.** The Principality button stays a stub. This is not a
  hub store and does not spend profile Gold.
- **Reworking drop tiers and plus rolls.** Tiers currently climb one per leg and
  a +3 from an ordinary fight is about 1%. Both are known and both are single
  constants; moving either moves every price in this brief, so it is a separate
  pass with its own bot batch.
- **Equipping from the shop.** Bought gear lands in the bag. The character sheet
  is where things are worn.
- **Selling worn gear**, per §5.4 — an intentional restriction, with the
  buy → leave → equip → sell-next-shop path spelled out there.
- **Reaching the character dossier from inside the shop.** Both are nested
  panels over the map; stacking them needs an overlay stack, a second focus
  owner, and a rule for a bag that changes underneath an open sell list. The
  map is two presses away.
- **Relic paging.** Removed from this brief, not deferred with a stub: there is
  nothing to page while a section shows every card it rolls.
- **Localization.** All copy already goes through `UiStrings`, which is the
  structural half. §3's worst-case string table is English, and so is
  `UiTextFitAudit`; a second language is a pass over every screen in the suite.
- **Relic slot limits.** There are none, by design.
- **Gamepad navigation** of the four-section grid. Mouse and keyboard only, same
  as every other screen in the suite.
- **The book-drop presentation** after a fight — this brief covers the shop as a
  purchase point, not the reward-screen moment where a dropped book is offered.
