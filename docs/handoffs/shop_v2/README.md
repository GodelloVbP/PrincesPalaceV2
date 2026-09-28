# Handoff: Shop Screen v2 — the in-run merchant

## 0. Revision 2026-09-03 — the prototype is in, and it deviates

`Shop Screen v2.dc.html` now exists (`docs/handoffs/shop_v2/Shop Screen v2.dc.html`
+ `support.js` + `image-slot.js`; screenshots and `Prototype Notes.md` alongside
it). Per §2's own rule it is authoritative for layout and behaviour from here
on, and §3-§5 below become the written record of what was first asked for, not
the current truth for the areas it changed. **Nothing in §3-§5 is deleted** —
history stays visible, per this project's own convention — but five rows are
now superseded, marked inline where they occur. This section is the map of
what changed and what a build now has to do about it; `docs/PLAN_SHOP.md` §1g,
§2c, §3d and §3e carry the engine-facing consequences.

**Accepted deviations**, per `docs/handoffs/shop_v2/Prototype Notes.md`:

1. **The recipient strip and replace picker are cut from the shop.** Buying a
   spell book is now `arm → confirm → SOLD`, identical to gear and relics. The
   shop no longer names a character at purchase time.
2. **A bought book is not learned by anyone yet.** It has to land somewhere —
   §1g below gives it a run-scoped home (`RunSnapshot.unassignedSpellBooks`)
   and moves the recipient/replace-picker machinery this section originally
   specified (§5.3) onto the character dossier screen instead of deleting it.
   Cutting the strip from the shop is a relocation of a decided interaction,
   not a decision to skip designing it.
3. **The persistent detail panel is now a hover/focus tooltip.** §3d's reason
   for choosing the persistent-panel shape (audit containment, and reserved
   space rather than a runtime-repositioned overlay) is overridden by the
   prototype's own layout, which has no reserved panel to put it in. The
   engine-side consequence — how a tooltip positions itself for a
   keyboard-focused card, since there is no mouse coordinate to anchor it to —
   is in `PLAN_SHOP.md` §3d.
4. **Selling moved into a `PACK` button that opens a modal**, replacing the
   inline sell viewport in Row C. It is the only thing `PACK` opens.
5. **The gear detail panel's delta against "the currently selected
   recipient" is dropped**, because there is no recipient selection left on
   this screen once the strip is gone. A gear card's tooltip now shows its own
   slot, affixes and stats — never a comparison to what's currently worn. The
   full compare-against-worn view is not lost; it lives on the dossier, where
   a recipient is actually selected by being on-screen at all.
6. **Layout is a fixed five-panel grid** (Relics / Spell Books / Shopkeeper
   art across the top; a 2×2 Gear grid + a Shop Actions panel below) rather
   than the three-row layout in §3. `PLAN_SHOP.md` §3f gives a 1920×1080
   coordinate table for this shape, because the prototype's CSS `fr`/`minmax`
   columns carry no pixel budget `UiTextFitAudit` can check.

**Not accepted as-is, corrected in the engine notes rather than copied:**

- The prototype's own demo data shows a weapon's tooltip stat line as flat
  `+6 ATK / +2 REACH`. §3d already established gear grants no flat Attack —
  a weapon's contribution is `+0 attack` in `ItemDescription`'s own words, and
  the real value is the damage line. The demo number is prototype-data
  noise, not a spec change; `PLAN_SHOP.md` §3d says so explicitly now so it
  isn't copied into the real tooltip.
- Two interaction ambiguities the prototype's code resolved one way without
  the README ever deciding: what a "click outside" does on the main screen
  (not just the Pack modal), and what Escape does when more than one section
  is armed at once. Both are decided in `PLAN_SHOP.md` §3f, not inherited
  silently from whatever the prototype happened to code.

**Filing note.** The prototype was originally produced at
`Handovers/Shop screen/Handover/` — outside `docs/handoffs/`, spaces in the
path, untracked. The files this handoff depends on are now also at
`docs/handoffs/shop_v2/` per `docs/HANDOFF_TEMPLATE.md`'s convention; that copy
is the one referenced throughout this document and the one `GAP_AUDIT.md`
audits against. The original location is the design tool's own workspace and
is left alone, not deleted.

## 0.7 Revision 2026-09-03 (b) — the product review's decisions for this screen

`docs/PLAN_SHOP.md` §7 records a product review of the plan above and decides
what changes. §7 is canonical; where it disagrees with anything below —
including the rest of this §0 — **§7 wins**. This subsection is the map of
what an implementer of *this screen* has to read differently as a result.
Nothing in §1-§6 is deleted for it; superseded paragraphs are marked inline
where they occur, same convention as the rest of §0.

1. **Counts: Gear 3 / Relics 2 / Books 1**, not the 3 spells / 4 items / 3
   relics this document specs throughout (`PLAN_SHOP.md` §7.1 point 3). Six
   primary cards, not ten. The featured-book count is one card because the
   whole book pool is five entries and the fielded squad is one character —
   three cards would show 60% of the pool at every visit. These are the
   *starting* constants; gate 1 (`PLAN_SHOP.md` §7.3) may move them again
   before the screen is built, from measured affordability rather than from
   this number.
2. **Single global selection plus a `BUY` button, not per-section arming**
   (`PLAN_SHOP.md` §7.1 point 6). One card is selected at a time, screen-wide,
   across all three offer sections. A press selects a card and shows its
   detail; unaffordable cards are selectable for inspection and their chip
   still reads `NEED {n}`. The selected card's chip reads `CONFIRM · {price} G`
   — a second press on it, or the `BUY` button in the Shop Actions panel,
   commits. `BUY` exists so keyboard and a future gamepad have one commit
   control. Selecting another card moves the selection; click outside clears
   it. §5.1 below is rewritten to this model — the per-section arming this
   document specs everywhere else (§4, §5.1, §5.2, §5.5, §7) no longer
   applies to the shop.
3. **Reroll is per section, not whole-shop.** Each of Relics / Spell Books /
   Gear carries its own `REROLL` button in that section's own panel header,
   its own counter, and its own price — `RerollPrice(n) = min(15 · 2^n, 9999)`,
   `n` per section (`PLAN_SHOP.md` §7.1 point 7). §5.5's single whole-shop
   reroll at `25 · 2^n` is superseded.
4. **The book card carries purchase-time facts now** — the "no ownership
   badge" rule this document states in §4 and §5.3 is reversed
   (`PLAN_SHOP.md` §7.1 point 4). From run state the shop already has, a book
   card and its tooltip show: `KNOWN BY SHAWN` / `KNOWN BY {n}`,
   `ELIGIBLE {k}/{m}`, `ALL SLOTS FULL` when every eligible character already
   has three learned spells, and `1 UNASSIGNED COPY` when a copy is already
   sitting in `run.unassignedSpellBooks`. The map screen gains a pending-book
   indicator while that list is non-empty, and the dossier's assignment panel
   header carries the count. There is no forced assignment on leaving the
   shop; the indicator is the nudge, not a gate.
5. **`LEAVE` keeps its one confirm** (`LEAVE` → `LEAVE?`), unchanged from
   §5.6 below. The review asked for a single click; the decision was not to
   give it one — leaving discards the visit permanently, sits beside
   `REROLL` and `BUY` in the same panel, and a misclick there is cheap to make
   and impossible to undo (`PLAN_SHOP.md` §7.1 point 6).
6. **Keyboard navigation is a declared graph**, not the focus-order list §3d
   describes: up/down/left/right neighbours per card and action button, over
   the six primary cards plus `REROLL` (×3), `PACK`, `BUY` and `LEAVE`, so a
   future gamepad's d-pad/A/B maps onto the same model without a second one
   being built later (`PLAN_SHOP.md` §7.1 point 8). `Enter` commits, `Escape`
   clears the selection per point 2.
7. **Every animation on the screen reads `UiMotion.DurationScale`** (Domain,
   default 1) — one shared multiplier rather than a per-screen retrofit, so
   reduced motion is one constant to change later (`PLAN_SHOP.md` §7.1
   point 8). §6's "no reduced-motion setting" note is superseded by this.
8. **Replacement returns the displaced book to the pool. It is not destroyed**
   (`PLAN_SHOP.md` §7.1 point 5). §5.3's `REPLACING` label and the picker
   itself are unchanged — the picker still exists and a replace is still a
   deliberate act — but the struck-through-chip warning language and the "a
   replaced book is gone" sentence describe a destroy path that no longer
   exists: `LearnSpell`'s replace branch appends the displaced slot's
   `skillId` back to `run.unassignedSpellBooks` before overwriting it.
   Reassignment outside combat is free.

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
| `README.md` (this file) | The spec. Authoritative for states and interaction rules not covered or changed by the prototype; superseded on layout and on the six points in §0. |
| `Shop Screen v2.dc.html` + `support.js` + `image-slot.js` | **Produced.** The interactive prototype, now authoritative for layout and behaviour per §0. |
| `Prototype Notes.md` | The designer's own deviation log — §0 is this README's response to it. |
| `GAP_AUDIT.md` | The build-vs-spec record. Every row is "not built" today. |

**Visual anchors.** Two reference screenshots already in the repo:

- `docs/handoffs/shop/reference_screenshot.png` — the v1 hub store. Use it for
  the *plate* language (gold-rimmed rows, cost right-aligned) and for nothing
  else; its layout and its currency model are both wrong for this screen.
- The relic screen is the closer anchor: card row, state-driven accent colour,
  the gold-rim-on-dark treatment, and the paging behaviour the relic section
  needs.

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

**Superseded by §0.** The recipient strip and detail panel below do not exist
in the shop as built by the prototype — the strip moves to the dossier (§0
point 2, `PLAN_SHOP.md` §1g) and the panel becomes a tooltip (§0 point 3,
`PLAN_SHOP.md` §3d). Coordinates kept as the written record of the original
ask; do not build against them for this screen.

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
**1** (`Data/SaveData.cs:50`, `:193-199`), so the ceiling today is **two**.
Three is the same headroom `ReckoningScreen` takes for the same reason —
`RowCount = 3` (`ReckoningScreen.cs:94-101`; the comment there has since been
reworded — it now argues from `SquadOfThreeReady`'s eventual base of 3 rather
than "1 today, target 3" — but the constant and the citation both still hold)
— and it is pinned the same way, by a test asserting the row count against
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

**Sell viewport superseded by §0 point 4** — selling is a `PACK` modal, not an
inline viewport in this row. The row layout, rules and worst-case strings
below (§0.4's replacement is `PLAN_SHOP.md` §3f) still govern
the relic cards; only the sell half moved.

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
| Card price chip | 180 | `CONFIRM · 9999 G` | The selected-card label is the longest state, not the idle price |
| Card price chip, unaffordable | 180 | `🔒 NEED 9999` | Lock glyph plus a four-digit shortfall |
| Reroll button (superseded — was whole-shop, one per screen; see the per-section row below) | 260 | `REROLL · 9999 G` | The price saturates at 9999 (`PLAN_SHOP.md` §2b) so five digits are unreachable |
| Section `REROLL` button (`PLAN_SHOP.md` §7.1 point 7, new) | **not sized here — flagged, not invented** | `REROLL · 9999 G` | Same string, `15 · 2^n` saturating at 9999 (§7.1 point 7), but the control now sits inside a section's own panel header (§3f's five-panel grid) rather than the screen header this table was written against. The 260px budget above does not carry over automatically; size it against §3f's panel-header geometry before implementation, not against this row |
| Gold chip | 240 | `9,999 G` | Run gold; the measured ceiling is well under this |
| Sell row meta | 148 | `T10 · +5 · 3 AFFIX` | Max tier, max plus, max affix slots |
| Sell row price | 148 | `SELL · 9999 G` | Deliberately shorter than the card's `CONFIRM · …`, so it fits 148 — sell and buy must not read the same anyway |
| Sell quantity chip | 148 | `ALL 99 · 9999 G` | A bag stack has no authored cap |
| Slot chip | 112 | The longest book display name | `LIGHTNING BOLT` today; a longer one truncates with an ellipsis and the detail panel carries the full name |
| Recipient row, full | 148 name field | `REPLACE REQUIRED` replaces the three chips' region, not the name | See §4 |
| Spell card badge (superseded — no `OWNED BY`/`CHOOSE RECIPIENT` on the shop card any more; see the four rows below) | 340 | `OWNED BY SHAWN` / `CHOOSE RECIPIENT` | Both fit the card's full inner width |
| Spell card fact badge — `KNOWN BY` (`PLAN_SHOP.md` §7.1 point 4, new) | **not sized here — flagged, not invented** | `KNOWN BY {n}` past one name, else `KNOWN BY ` + the longest character display name | Needs a real name-length budget from the roster content, not guessed here |
| Spell card fact badge — `ELIGIBLE k/m` (new) | **not sized here — flagged, not invented** | `ELIGIBLE 9/9` | `m` is bounded by `EffectiveMaxSquadSize()` (§3e/§7e — 2 today, 3 designed-for), so single digits suffice unless the squad cap changes; still needs a real budget pass against §3f's card geometry |
| Spell card fact badge — `ALL SLOTS FULL` (new) | **not sized here — flagged, not invented** | `ALL SLOTS FULL` | Fixed string, no numeric growth — still needs a width check against §3f's card, not this table's now-superseded 380px card |
| Spell card fact badge — `1 UNASSIGNED COPY` (new) | **not sized here — flagged, not invented** | `{n} UNASSIGNED COPIES` | Plural form is the worst case once `n` > 1; needs the same real budget pass |
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

**Superseded by §0, revised 2026-09-03 (b) per `PLAN_SHOP.md` §7.1 points 4
and 6:** every row below for "Recipient row" and "Slot chip" — none of these
are shop states; they are the dossier assignment panel's, specced as its own
table now in §5.3. `Armed` below is renamed `Selected` throughout, per §5.1's
rewrite — there is no more per-section arming to distinguish it from. The
"Spell card" rows for `Owned by one`/`Owned by several`/`Armed, more than one
eligible recipient` are also superseded: the shop no longer shows who owns a
book (that was always dossier information, §1g), but it now shows a
*different* set of purchase-time facts — added below, per point 4 — so
"the shop card carries no badge" is not the replacement either. Recipient
row/slot chip rows are kept here as the record of what they looked like
before the move; §5.3 is the current spec for wherever they live now.

| Element | State | Reads as |
|---|---|---|
| Any card | **Affordable** | Full opacity, gold border `#E7B25CB3`, pointer cursor, price chip in gold text `#FFD9A2` |
| Any card | **Unaffordable** | 55% opacity, border drops to `#B496D233`, no pointer, price chip becomes a lock glyph plus `NEED 24` in red `#E07A62` — the shortfall, not the price, is the information, and the glyph is what carries it for a reader who cannot see the red |
| Any card | **Sold out** | 35% opacity, contents replaced by a centred `SOLD` in `#8A7AA0`, border `#B496D233`, no pointer. **The card stays in place** — it does not collapse and the row does not re-centre |
| Any card | **Selected** (renamed from `Armed`, §5.1) | Border brightens to `#FFC45A`, price chip label becomes `CONFIRM · 24 G`, one settle-then-hold pulse. The label change is the state; the pulse only draws the eye to it. A second press on the card, or `BUY`, commits |
| Any card | **No offer** | The section's pool held fewer candidates than it has cards. A centred `NO OFFER` in `#8A7AA0`, border `#B496D233`, no pointer. **The card stays in place** — it is not hidden, not filled with a duplicate, and the row does not re-centre |
| Any card | **Keyboard focus** | A 2px `Focus outline` ring drawn outside the border, in addition to whatever else the card is. A ring rather than a colour swap, so it is still visible on a selected card that is already amber |
| Relic card | **Owned already** | Not offered at all — a held relic is filtered out of the roll. There is no owned state to draw |
| Spell card | **Owned by everyone** | Not offered. A book already in every fielded character's slots is filtered out of the roll — no press on it could do anything |
| Spell card (`PLAN_SHOP.md` §7.1 point 4, new) | **`KNOWN BY {NAME}` / `KNOWN BY {n}`** | A badge naming who already knows this book, when at least one fielded character does. The count form (`KNOWN BY 2`) is used past one name for the same width reason the old `OWNED BY` badge was — 340px, roster not bounded by it |
| Spell card (new) | **`ELIGIBLE {k}/{m}`** | Of `m` fielded characters, `k` have a free slot or could gain one through a replace. Read alongside `KNOWN BY` when both apply — a card can be known by one character and still eligible for others |
| Spell card (new) | **`ALL SLOTS FULL`** | Every eligible fielded character already has three learned spells. The card is still buyable — the shop never refuses a purchase on the buyer's behalf — the badge is a warning that assignment will require a replace |
| Spell card (new) | **`1 UNASSIGNED COPY`** | A copy of this exact book is already sitting in `run.unassignedSpellBooks`, unplaced. Buying a second is legal (§1b) but this badge is the nudge that says so before the player does it by accident |
| Spell card | **Owned by one** (superseded, §7.1 point 4 — see the new badges above) | Offered, with an `OWNED BY {NAME}` badge top-left in `#8FBF6A`. Buyable for a *different* character; that owner's row is inert (below) |
| Spell card | **Owned by several** (superseded, §7.1 point 4) | Offered, badge reads `OWNED BY 2` — the count, not a list of names, because the badge is 340px and the roster is not bounded by it. Which characters own it is legible from the strip: their rows are inert |
| Spell card | **Armed, more than one eligible recipient** (superseded — no recipient step in the shop, §5.1) | The card's badge reads `CHOOSE RECIPIENT` in `#E7B25C`. This replaces the first draft's global `REPLACES A SPELL`, which claimed a thing about the purchase that is only true of *some* recipients |
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
| Section `REROLL` button (was one whole-shop button — §5.5) | **Affordable** | Gold, price shown, `15 · 2^n` for that section's own `n` |
| Section `REROLL` button | **Unaffordable** | 55% opacity, lock glyph and `NEED {n}` on the label |
| Section `REROLL` button | **At the price ceiling** | Still drawn, still showing `9999 G`, still unaffordable. It does not disappear: a control that vanishes teaches nothing, which is the same rule the fight's skill submenu follows for unaffordable rows |
| Leave button | **Idle / armed** | `LEAVE`, then `LEAVE?` after one press. Leaving clears the room permanently, so it is the one action in the shop that confirms |
| One section (renamed from "Whole screen", §5.5) | **Rerolled** | That section's cards cross-fade to new stock over ~200ms; that section's reroll button price doubles in place; that section's sold-out cards come back as fresh stock. The other two sections are untouched |

## 5. Interaction logic

### 5.1 The state machine

**Rewritten 2026-09-03 (b) against `PLAN_SHOP.md` §7.1 point 6.** The version
below is canonical for the shop as it will be built — no strike-throughs, no
"read it as": the per-section arming this document specced earlier, and the
whole recipient/replace machinery that used to hang off it, are not part of
this table any more. The recipient/replace states have a home of their own —
§5.3 below, which is now the dossier assignment panel's table, not the
shop's.

**One selection, globally, across every section.** There is no per-section
arming left to keep independent: pressing a card in Relics moves the
selection there even if a Gear card was selected a moment ago. Nothing is
committed by selecting, so there is nothing lost by that — the old
"arming is per section" argument protected a state that no longer exists.

**States.** `Idle`, `Selected`, `PackOpen` (its own two rows below,
`PackIdle`/`PackSelected`), `LeaveConfirm` (the footer, reachable from `Idle`
or `Selected` only — the footer is behind the `PACK` modal while it is open).
Commit is transient in every state that has one: the mutation runs through
its single orchestrator method (`PLAN_SHOP.md` §2f), the card goes `SOLD` in
place, the gold chip counts down, and the state returns to `Idle` in the same
frame — there is no separate `Committed` row because no input reaches a
screen mid-repaint.

**A purchase that would leave gold negative is refused at selection time** —
the card is unaffordable and can be inspected but not confirmed — never at
commit time. So no state below can fail on money.

**Opening `PACK` clears the main-screen selection** and the modal owns
selection while it is open; closing it (Escape, or its own close control)
returns to `Idle`, never back to whatever was selected before.

| | card press (same card) | card press (other card, any section) | card press (unaffordable card) | `BUY` | section `REROLL` (this card's section) | section `REROLL` (other section) | `PACK` | pack row press | quantity chip | Escape | click outside | `LEAVE` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Idle** | → Selected | → Selected on that card | → Selected (inspection only; chip reads `NEED n`) | ignored, nothing selected | re-rolls that section; no selection to lose | re-rolls that section; unaffected | → `PackOpen` (`PackIdle`) | n/a | n/a | ignored | ignored | → `LeaveConfirm` |
| **Selected**, affordable | **commit** → `Idle` (card `SOLD`, gold debited) | → Selected on the new card | → Selected on that card (inspection only) | **commit** → `Idle`, same as same-card press | re-rolls that section; selection is on a card that no longer exists as rolled, so it clears → `Idle` | re-rolls that section; this selection is unaffected | → `PackOpen` (`PackIdle`); selection cleared | n/a | n/a | → `Idle` | → `Idle` | → `LeaveConfirm` |
| **Selected**, unaffordable | no commit; chip stays `NEED n` | → Selected on the new card | → Selected on that card | ignored (nothing to commit) | re-rolls that section; selection clears → `Idle` | re-rolls that section; unaffected | → `PackOpen` (`PackIdle`); selection cleared | n/a | n/a | → `Idle` | → `Idle` | → `LeaveConfirm` |
| **`PackOpen`: `PackIdle`** | n/a | n/a | n/a | n/a | ignored — main-screen sections are behind the modal | ignored | closes → `Idle` | → `PackSelected` on that row | n/a | closes modal → `Idle` | closes modal → `Idle` (backdrop press) | ignored — close `PACK` first |
| **`PackOpen`: `PackSelected`**, stack of 1 | **commit sale** → `PackIdle` (row `SOLD`, gold credited) | n/a | n/a | n/a | ignored | ignored | closes → `Idle` | → `PackSelected` on the new row | n/a | → `PackIdle` | → `PackIdle` | ignored |
| **`PackOpen`: `PackSelected`**, stack of 2+ | → `PackIdle` (deselect; the row is not the confirm surface, the chips are) | n/a | n/a | n/a | ignored | ignored | closes → `Idle` | → `PackSelected` on the new row | **commit 1 or ALL** → `PackIdle` | → `PackIdle` | → `PackIdle` | ignored |
| **`LeaveConfirm`** (footer) | reverts to `LEAVE`, press handled as `Idle`/`Selected` | as left | as left | as left | as left | as left | as left | n/a | n/a | reverts to `LEAVE`, swallowed | reverts to `LEAVE` | **leaves the shop** |

**A card that is currently `Selected` and gets rerolled out from under the
player clears its own selection rather than pointing at a card that no
longer matches what was rolled.** Any other selected card, or no selection at
all, is unaffected by a reroll in a different section.

### 5.2 What each section does on commit

| Section | Effect |
|---|---|
| Gear | The item is added to the bag with its rolled plus and affixes. It is **not** equipped, and it is not auto-equipped into an empty slot either — deliberately unlike the post-fight reward path, which does (`PLAN_SHOP.md` §2c) |
| Consumable (a gear card may be a potion) | Added to the bag, stacking normally |
| Relic | Appended to the run's relics. There is no slot limit and no choice to make |
| Spell book | Appended to `run.unassignedSpellBooks`. No character is named and no slot is chosen at commit time — see §5.3, which now describes where that happens |
| Sell | One copy, or the whole stack, removed from the bag and its price credited |

### 5.3 The dossier assignment panel — where a bought book actually gets learned

**Rewritten 2026-09-03 (b).** This subsection is now the assignment panel's
own spec, not a copy of shop states with a note that they moved. The panel
lives on `CharacterDossierScreen`, one character's three slots at a time
(`PLAN_SHOP.md` §1g), and reads `run.unassignedSpellBooks` rather than
anything the shop rolled — a book in this list is already owned, bought and
paid for; nothing here is a purchase flow.

**Source list.** Each unplaced copy in `run.unassignedSpellBooks` is one
learnable entry, deduplicated for display: two unplaced copies of the same
`skillId` show one row with a `LEARN ({n} OWNED)` badge, but placing one copy
consumes exactly one entry from the list and leaves the rest available for
another character or another slot.

**No purchase context, no recipient strip.** There is no card to arm first —
a row in this list is already a fact, not an offer — so pressing it directly
runs `CanLearn`/`LearnSpell` (`PLAN_SHOP.md` §1d) for the character whose page
is on screen. What used to be "arm the card, then confirm a recipient row" on
the shop collapses to one act here, because the recipient is already decided
by which dossier page is open.

**States**, per row:

| State | Reads as |
|---|---|
| Eligible, free slot available | Normal row treatment, pressable |
| Already known by this character | Inert: 55% opacity, not pressable, not focusable — a duplicate press is refused by `LearnSpell` anyway (§1d), this is the row saying so before the press |
| All three slots full | Pressable. A press opens the replace picker rather than committing directly |
| Replace picker open | That character's three slot chips become individually pressable, each showing the red replace preview and the word `REPLACING` — pressing one commits the swap |

**A replacement returns the displaced book to the pool. It is not destroyed**
(`PLAN_SHOP.md` §7.1 point 5). Committing a swap removes the incoming
`skillId` from `run.unassignedSpellBooks`, appends the *displaced* slot's
`skillId` back into that same list, then overwrites the slot. The struck-
through chip name during `REPLACING` is still the preview of what leaves the
slot — it now previews a move, not a deletion — and the displaced book
reappears in this same list, learnable by any eligible character including
the one it just left. Reassignment outside combat is free and reversible.

**Slot chip states** carried over unchanged from the shop-hosted design this
replaces: `Filled` (book name), `Empty` (`EMPTY`), `Would be filled by the
selected book` (green accent, name previewed), `Would be replaced` (red
accent, existing name struck through — a preview of relocation, per above,
not of loss).

Escape backs out of the replace picker to the row list. It does not leave the
dossier screen.

### 5.4 Selling

**Delivery mechanism superseded by §0.4** — this whole subsection's rules
(what's sellable, the two-press quantity flow, the 30% price) are unchanged;
only the surface changes, from an always-visible viewport in Row C to rows
inside the `PACK` modal. **Superseded 2026-09-03 (b) — see §5.1 above.**
Opening `PACK` clears whatever card was selected on the main screen rather
than leaving it untouched; the modal owns selection while it is open, per
`PLAN_SHOP.md` §7.1 point 6.

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

### 5.5 Reroll — per section

**Rewritten 2026-09-03 (b) against `PLAN_SHOP.md` §7.1 point 7.** Reroll is
no longer one whole-shop button. Each of the three offer sections — Relics,
Spell Books, Gear — carries its own `REROLL` button in that section's own
panel header, with its own price and its own counter. Rerolling a section
touches only that section's cards; the other two sections and the sell list
are unaffected.

Price per section starts at 15 and **doubles per reroll**: 15, 30, 60, 120,
240 … **saturating at 9999**
(`RerollPrice(n) = min(15 · 2^n, 9999)`, `PLAN_SHOP.md` §2b/§7.1 point 7), at
which point the button stays visible and unaffordable rather than
disappearing. There is no reset, because a shop is cleared on leaving and
never re-entered — "per visit" and "per node" are the same thing here, and
each section's count is stored per node (`run.shopRerollsUsed[section]`) so
that its price and its stock's seed read the same number.

Rerolling a section clears that section's sold-out flags — its stock is
genuinely new. It does not refund anything already bought, and it does not
touch the other sections' sold-out flags or stock. If the currently selected
card (§5.1) sits in the section being rerolled, the selection clears; a
selection in a different section is unaffected.

### 5.6 Leaving, and what Escape does

**Superseded 2026-09-03 (b) — see §7.1 point 6, §5.1 above.** Escape's
behavior below is stated in terms of "disarms" and "the replace picker,"
both gone from the shop's own state machine now. The current rule, per §5.1's
canonical table: Escape closes the `PACK` modal if it is open; otherwise it
clears the main-screen selection; otherwise it does nothing. The replace
picker is a dossier concern now (§5.3) and Escape there backs out to the row
list, not to a "selected recipient" the shop no longer has.

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
  `ReckoningTests.cs:183` pins `ReckoningScreen.RowCount`. A test carrying the
  literal `3` is the drift it exists to catch.
- **There is no minimum-resolution policy for this screen to state**, and that
  is deliberate: it is settled project-wide. The canvas is
  `ScaleWithScreenSize` at a 1920×1080 reference with
  `ScreenMatchMode.Expand`, i.e. `scale = min(w/1920, h/1080)`
  (`Editor/SceneBuilder/SceneBuilder.cs:213-220`), so the whole authored frame
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
- **The dossier's spell-assignment panel is out of scope for *this* handoff's
  layout** (it is a different screen and needs its own coordinates), but it is
  **not** out of scope for the feature — a book that can be bought but never
  assigned is not shippable. `PLAN_SHOP.md` §1g tracks it as a required
  companion piece, not a deferred nice-to-have, and reuses this document's §4
  recipient/slot-chip states and §5.3 machinery rather than inventing new ones.
