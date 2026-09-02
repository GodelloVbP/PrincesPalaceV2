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

The other half of what this screen sells is new: the automatic per-level spell
that every character casts today **disappears**. Spells become books found
during a run and learned into **3 slots per character, per run**, carried over
into nothing. That is why the screen carries a spell-slot strip: a book
purchase has to show what it would replace before it is bought.

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

### Row A — spell books and slots, y 120-392

| Element | (x, y, w, h) |
|---|---|
| Section header `SPELL BOOKS` | 56, 120, 320, 28 |
| Spell card 1 | 56, 156, 380, 236 |
| Spell card 2 | 476, 156, 380, 236 |
| Spell card 3 | 896, 156, 380, 236 |
| Section header `SPELL SLOTS` | 1316, 120, 320, 28 |
| Slot panel | 1316, 156, 548, 236 |

Inside the slot panel, one row per **fielded** character, 548×40, 8px gap,
first row at y 158 (max 5 characters = 5×40 + 4×8 = 232, fits 236):

| Element | offset within row |
|---|---|
| Character name | +12, +8, 160, 24 |
| Slot chip 1 | +184, +4, 112, 32 |
| Slot chip 2 | +304, +4, 112, 32 |
| Slot chip 3 | +424, +4, 112, 32 |

A chip carries the learned book's name, or `EMPTY`.

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
| Section header `RELICS` | 56, 672, 200, 28 |
| Page prev `◀` | 300, 668, 40, 40 |
| Page label `1 / 6` | 348, 672, 116, 32 |
| Page next `▶` | 472, 668, 40, 40 |
| Relic card 1 | 56, 708, 380, 220 |
| Relic card 2 | 476, 708, 380, 220 |
| Relic card 3 | 896, 708, 380, 220 |
| Section header `SELL FROM BAG` | 1316, 672, 320, 28 |
| Sell viewport | 1316, 708, 548, 220 |

The sell viewport scrolls vertically. Rows are 548×44 with a 4px gap — 4.5
visible, which is intentional: a half-row at the bottom edge is the cheapest
possible affordance saying "there is more".

| Element | offset within sell row |
|---|---|
| Item name | +12, +10, 300, 24 |
| Meta (`T3 · +2 · 1 AFFIX`) | +316, +13, 108, 18 |
| Sell price (`+ 10 G`) | +432, +10, 104, 24 |

### Footer

| Element | (x, y, w, h) |
|---|---|
| Leave button `LEAVE` | 800, 952, 320, 64 |

### Card internals

**Spell / relic card (380×220 or ×236), offsets from card origin:**

| Element | offset |
|---|---|
| Icon | +142, +16, 96, 96 |
| Name | +20, +124, 340, 28 |
| Sub-line (relic rarity, or spell `TIER 3 · 18 MP`) | +20, +156, 340, 20 |
| Price chip | +120, +184, 140, 32 |

**Item card (420×204):**

| Element | offset |
|---|---|
| Icon | +170, +12, 80, 80 |
| Name | +12, +100, 396, 26 |
| Meta (`TIER 3 · +2 · 1 AFFIX`) | +12, +130, 396, 20 |
| Price chip | +140, +158, 140, 32 |

### Vertical budget check

Row A ends 392, Row B starts 412 (20px). Row B ends 652, Row C header 672
(20px). Row C ends 928, leave button 952 (24px), bottom margin 64. No band
overlaps another. This must survive the layout audit at 1920×1080, 2580×1080,
1920×1440 and 1920×1200 — the panel is fixed-size and centred, so the only
expected exemption is the panel covering the map behind it.

## 4. States

| Element | State | Reads as |
|---|---|---|
| Any card | **Affordable** | Full opacity, gold border `#E7B25CB3`, pointer cursor, price chip in gold text `#FFD9A2` |
| Any card | **Unaffordable** | 55% opacity, border drops to `#B496D233`, no pointer, price chip text turns red `#E07A62` and gains a `NEED 24` suffix — the shortfall, not the price, is the information |
| Any card | **Sold out** | 35% opacity, contents replaced by a centred `SOLD` in `#8A7AA0`, border `#B496D233`, no pointer. **The card stays in place** — it does not collapse and the row does not re-centre |
| Any card | **Armed** (first press) | Border brightens to `#FFC45A`, price chip label becomes `CONFIRM · 24 G`, one settle-then-hold pulse |
| Relic card | **Owned already** | Not offered at all — a held relic is filtered out of the roll. There is no owned state to draw |
| Spell card | **Owned by everyone** | Not offered. A book already in every fielded character's slots is filtered out of the roll |
| Spell card | **Owned by someone** | Offered, with an `OWNED BY {NAME}` badge top-left, `#8FBF6A`. Buyable for a *different* character |
| Spell card | **All slots full** | Still affordable and still buyable; pressing opens the replace picker (§5.3) instead of confirming. The card gains a `REPLACES A SPELL` sub-badge in `#E7B25C` so this is known before the press |
| Slot chip | **Filled** | Book name, `#E9DFF8` on `#120A1AD1`, quiet border `#B496D233` |
| Slot chip | **Empty** | `EMPTY` in `#7F6F98`, dashed border `#B496D233` |
| Slot chip | **Would be filled by the armed purchase** | Green accent `#8FBF6A`, book name previewed |
| Slot chip | **Would be replaced by the armed purchase** | Red accent `#E05A5A`, existing name struck through |
| Sell row | **Idle** | Name `#E9DFF8`, price `#FFD9A2` prefixed `+` |
| Sell row | **Selected for sell** (first press) | Row fill lifts to `#583216F2`, price label becomes `SELL FOR 10 G`, gold accent border |
| Sell row | **Not sellable** | Never rendered — worn gear is not in the bag (§5.4) |
| Reroll button | **Affordable** | Gold, price shown |
| Reroll button | **Unaffordable** | 55% opacity, `NEED {n}` on the label |
| Whole screen | **Rerolled** | Every card cross-fades to the new stock over ~200ms; the reroll button's price doubles in place; sold-out cards come back as fresh stock |

## 5. Interaction logic

### 5.1 Buying — confirm on the second press

Every purchase is two presses on the same card. The first press **arms** it
(§4); the second press commits. This is the relic draft's deselect rule
generalised: pressing an armed card that is *not* the one you want disarms it
and arms the new one, and pressing the armed card again a second time commits.
Pressing anywhere else on the panel disarms without buying. Esc disarms; Esc
again leaves.

Only one thing is ever armed at a time, across all four sections — arming a
sell row disarms an armed card, and vice versa. A purchase that would leave
gold negative is refused at arm time (the card is unaffordable and does not
arm), never at commit time.

On commit: gold is deducted, the card goes **sold out** in place, and the gold
chip counts down rather than snapping.

### 5.2 What each section does on commit

| Section | Effect |
|---|---|
| Gear | The item is added to the bag with its rolled plus and affixes. It is **not** equipped — equipping is the character sheet's job |
| Consumable (a gear card may be a potion) | Added to the bag, stacking normally |
| Relic | Appended to the run's relics. There is no slot limit and no choice to make |
| Spell book | Learned into a slot on **one chosen character** — see 5.3 |

### 5.3 The replace-slot picker

Buying a book always names a character, because a book belongs to one.

- Arming a spell card highlights the slot strip: every character row that has a
  **free** slot shows that chip in the green preview state.
- The second press on the card, with a character row already chosen, commits.
- Choosing the character is a press on that character's row in the slot strip
  while the card is armed. If exactly one fielded character has a free slot,
  they are preselected and one press on the card is enough.
- If the chosen character's slots are **full**, the second press does not
  commit: it enters the replace picker — the three chips of that character
  become individually pressable, each showing the red replace preview, and
  pressing one commits the swap. Esc backs out of the picker to the armed card,
  not out of the shop.
- A replaced book is **gone**. It does not go to the bag, it cannot be re-bought
  at a discount, and the confirm state must say so: the chip's struck-through
  name is the whole warning.

### 5.4 Selling

Sell rows are the run's bag, which is the same bag the character sheet paints.
**Worn gear is not sellable.** It is not in the bag, so it is not in the list —
there is no dimmed "equipped" row to explain, and no unequip affordance on this
screen. A player who wants to sell a worn piece unequips it on the character
sheet first. The empty-bag case shows a single centred line, `NOTHING TO SELL`,
in `#8A7AA0`.

Selling is two presses like buying: the first selects the row and turns its
price label into `SELL FOR 10 G`, the second commits. One item per press — a
stack of three potions takes three presses, deliberately, because the alternative
is a quantity control nobody will need at these prices.

Sell price is 30% of what that exact copy would cost in this shop, rounded, floor
1. Plus and affixes raise it because they raise the buy price. Selling everything
in a full bag does not approach a relic's price, and that is the intent.

### 5.5 Reroll

One button, one price, always visible on the label. It rerolls **the whole shop**
— all three offer sections at once. The sell list is not stock and does not
change.

Price starts at 25 and **doubles per reroll within one visit**: 25, 50, 100, 200,
400. It resets to 25 when the player leaves. There is no cap on the number of
rerolls; the doubling is the cap.

Rerolling clears sold-out flags — the new stock is genuinely new. It does not
refund anything already bought.

### 5.6 Leaving

`LEAVE` closes the panel, returns to the map, and **clears the room**. A shop is
a one-visit room: the node is marked cleared and cannot be re-entered, exactly
like a treasure room. Nothing is confirmed on the way out — the player has
already confirmed every purchase individually, and a "are you sure you're done
shopping" prompt is the kind of thing that gets clicked through without reading.

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
  already has. The mock will fake this with a plain shuffle; the real version
  must not, or quitting becomes a reroll.
- **Prices come from the plan's formula** (`docs/PLAN_SHOP.md` §2b), not from
  `ItemDefinition.cost`. That field is v1 residue priced for a permanent-Gold
  store — roughly 5× too expensive for run gold, and 0 for every weapon in the
  game. Consumables are the one exception and do read `cost`.
- **`NEED {n}` is a shortfall**, computed as `price − gold`, not the price. A
  prototype that shows the price in red is showing the wrong number.
- **Cards are a fixed count emitted at scene-build time** and hidden when
  unused. Scenes are generated once and a runtime count cannot widen a tree, so
  the relic pager exists because the *maximum* pool is paged, not because 18
  relics need paging today.
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
- **Selling worn gear**, per §5.4.
- **Relic slot limits.** There are none, by design.
- **Gamepad navigation** of the four-section grid. Mouse and keyboard only, same
  as every other screen in the suite.
- **The book-drop presentation** after a fight — this brief covers the shop as a
  purchase point, not the reward-screen moment where a dropped book is offered.
