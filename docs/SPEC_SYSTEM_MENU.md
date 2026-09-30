# System menu design spec

The pixel half of `docs/PLAN_SYSTEM_MENU.md`: that plan owns the rules
(C-numbers), this file owns sizes, states and focus. Canvas 1920×1080,
positions in the panel's own frame unless stated. Build audits
(`UiAudit`, four aspects) outrank any number here that turns out not to
fit; change the number here in the same commit.

## 1. Type floor (C14)

Measured from 1080p captures, text body height (ascender top to
descender bottom, XAG 101):

| Font | Size | Body height | Ratio |
|---|---|---|---|
| Chakra Petch Regular | 14 | 13 px | 0.93 |
| Source Sans 3 Regular | 16 | 15 px | 0.94 |
| Chakra Petch Regular | 34 | 32 px | 0.94 |
| Chakra Petch, all caps | 18 | 13 px (cap height) | 0.72 |

Floors, enforced by the menu type lint:

- **Mixed case: TMP 20** (≈ 18.8 px body).
- **All caps: TMP 24** (≈ 17.3 px cap height; caps read larger than their
  pixel height, and the tab bar carries no descenders).
- Numbers in stat rows: TMP 22 (tabular, Chakra Petch).

Roles used in the menu (`Typography.cs`):

| Role | Where | Size |
|---|---|---|
| FunctionalHeading | tab labels, section titles | 24–26, caps |
| Body | descriptions, notices, effect lines | 20–22 |
| TacticalData | stat names and values | band raised to 20–22 for menu use via literal 22 (the literal becomes the max, `Typography.cs:82-87`) |
| ButtonLabel | plates (Exits, Options restore) | 26–30 as today |

Any literal below the floor fails the lint. `AllowSmallText("reason")`
exists for ornaments only.

## 2. Frame

```
element                          size          place
lintel                           1600 × 52     directly above the panel (as today)
panel                            1600 × 900    as today
  tab bar                        1600 × 96     top of the panel
  content pane                   1600 × 804    unchanged
footer (notice | prompts)        1600 × 48     directly below the panel, 4 px gap
```

The footer sits outside the panel, mirroring the lintel, so no pane loses
height. Lintel + panel + footer = 1004 of 1080. In the pane sections below,
"1600 × 756" budgets remain the design target: they leave 48 px of each pane
free for pane-level headers and pagers.

- **Lintel:** title (FunctionalHeading 24, tracked), context line (Body
  20), gold, embers (Body 22: TacticalData's material tints text violet,
  and a number that reads as a link is wrong here), mode pill (FunctionalHeading 24 in a
  1px-rimmed plate, 36 tall, fitted to its words), ESC glyph plus close. The
  pill's words are all caps, so it takes the caps floor of 24 (section 1)
  rather than Body 20.
- **Tab bar:** labels FunctionalHeading 24 caps, tracked 0.14em. Game
  group from the left inset (40), system group flush right. Group gap =
  the leftover width, minimum 96; the gap between tabs inside a group is
  48. Underline 3 px under the label width + 24. The system group's label
  colour is `TextMuted`, and it turns `GoldLight` when selected.
- **Estimated widths at 24 caps:** CHARACTER 165, PARTY 92, RUN 55,
  GLOSSARY 147, OPTIONS 128, EXIT 73 (each + 48 padding). The full set is
  about 950 of 1520. The short labels (CHAR, PARTY, RUN, LORE, OPTS, EXIT)
  exist for the C1 overflow policy.
- **Footer:** the notice sits left (Body 20, fades after 3 s unless it is
  a persistent state line). Prompts sit right, as a row of
  `[glyph] Label` pairs, glyph plates 32 tall (32 wide, wider for a key
  name) with the letters in TacticalData 24, 24 apart. The letters take 24
  because "LB" and "RT" are caps (section 1). The prompt set is computed
  from the focused control and the pending state (§8), and the row holds at
  most five pairs, dropping from the tail.

## 3. CHARACTER (C15)

The pane is 1600 × 756. Insets are 40 horizontally, giving 1520 wide.

```
┌ strip 1520 × 72 ────────────────────────────────────────────────┐
│ [LB] (Odette·Rear ●) (Shawn·Middle) (Bjorn·Front) │ bench… [RB] │
├ rail 300 ┬ detail 1196 × 660 ───────────────────────────────────┤
│ portrait │                                                       │
│ 120×120  │   section content                                     │
│ Name 28  │                                                       │
│ Level 20 │                                                       │
│ XP bar   │                                                       │
│──────────│                                                       │
│ LOADOUT ●│                                                       │
│ ATTRIBUTES│                                                      │
│ SKILLS   │                                                       │
│ REWARD TRACK ●                                                   │
└──────────┴───────────────────────────────────────────────────────┘
```

- **Strip chips:** 240 × 64 each, 16 apart. Each chip holds a 48 px
  portrait, the name (Body 20), the seat (Body 20, muted) and a
  pip (12 px gold dot, top right). The selected chip gets a gold rim, the
  others a hairline. Benched chips sit after a 32 px divider at 60 %
  alpha. More chips than fit: the strip pages, and LB/RB steps through
  every chip regardless. The name and the seat are 30-tall lines: a Body 20
  line is 26.5 px, and TMP draws nothing at all in a box shorter than its
  line, which is how the first build shipped chips with a portrait only.
  Every label that truncates or clamps is tested to be boxed for one line
  (`CharacterDossierScreenTests`), and a PlayMode net checks that a label with
  words draws some (`DossierLoadoutTests`).
- **Pager plates:** the LB and RB plates at the strip's ends are the footer's
  glyph plate (`SystemMenuScreen.GlyphPlate`: the same rimmed 32-tall plate,
  TacticalData 24 letters), painted from `PromptGlyphs` for the last device
  used and refitted to their letters, so they read as the same control as the
  footer's prompt.
- **Rail:** the identity block is 260 tall. Below it, 4 section rows at
  64 each (label FunctionalHeading 24 caps, pip at the right). The
  selected row shows a bright label and a 3 px gold bar on its left edge.

### 3.1 LOADOUT (C16), detail 1196 × 660

| Column | x (detail frame) | width | content |
|---|---|---|---|
| Slots | 0 | 404 | the mannequin with 8 gear slots, then the book row |
| Pack | 420 | 408 | filters, a 3-column grid, and the count |
| Compare | 844 | 352 | the focused entry, and deltas |

The columns are 16 apart. The pack keeps 408 because five 78-wide filter chips
need it; the slot column gave up what the gear files did not use (gear files at
52, 202 and 352, so the figure keeps 20 either side), and the compare column
took it, so "Physical DEF 1000 (75%) → 1200 (79%) (+200)" sits on one line.

- **Slots:** cells are 88 × 88, captions below at Body 20 in a 30-tall box
  (2 below the cell), row pitch 122. Every gear cell carries its slot name
  (`UiStrings.EquipmentSlotName`, so a language can change it), every book
  cell "Book 1".."Book 3" or, once filled, the book's name (128 wide, cut to
  fit). The gear hangs in four rows (`DossierLayout.GearCell`): Head top centre
  with Necklace beside it on the left; Torso, Gloves, Legs down the left;
  Weapon 1, Weapon 2, Shoes down the right, level with them. The armour
  stand sits between at its own aspect (about 169 × 360), starting under
  Head's caption. The book row sits under it: the heading "SPELL BOOKS"
  (FunctionalHeading 24, 34 tall), then `MaxSpellSlots` cells of 88 × 88 with
  captions, centred at 64, 202 and 340 so the 128-wide captions stay inside the
  column. A character who can't hold books gets one Body 20 line in place of the
  book row.
- **Pack:**
  - Filter row: 5 chips (All, Armour, Weapons, Salves, Books), 78 × 44,
    4 apart, Body 20.
  - Grid: 3 × 4 cells of 112 × 112, each with a 64 px icon, a rarity
    tick, a count badge (Body 20) and a tier ring. No names in the
    cell; the compare column names the focused entry.
  - The grid scrolls vertically (the scrollbar exists today).
  - Footer: "Carried {n}" (Body 20).
  - The empty state is a Body 20 sentence centred in the grid: "Nothing
    carried. Gear from fights and shops lands here."
- **Compare**, showing the focused pack entry or worn slot:
  - name (FunctionalHeading 24, rarity colour)
  - kind · tier (Body 20)
  - for gear: the summary (Body 20) wraps to four lines and ends in an
    ellipsis only past the fourth (it once cut itself at the first), then a
    delta list, one row per affected stat. A row is two Body 20 lines of 27:
    the stat's name, and under it `280 → 338 (+58)` (gains green, losses
    red). Two lines because the longest real delta, `1000 (75%) → 1200
    (79%) (+200)`, does not share a line with a name. Eight rows end at 644 of
    660, and a test lays out that longest delta in every row and checks it
    fits on one line with no row touching the next
  - for a book or a salve: its words (cost, then description; the longest
    authored one is seven lines) take the column from under the kind line
    down, and no stat rows show. The description and the rows are hidden while
    it does
  - nothing focused: the character's derived stats

### 3.2 ATTRIBUTES (C17)

- **Header:** one line above the rows. Left, "{n} points to spend" (Body 20);
  right, why a change is refused, in the words of `MenuPermissions`: "Refunds
  at camp" in a descent, "Locked during a fight" in a fight, nothing at camp.
- **Six rows**, 700 wide on a 102 pitch (a 100 panel), 40 below the top:
  - top line (44): the name (FunctionalHeading 24, 96 wide), the value
    (Body 26, gold, 56 wide), then the `[−] [+]` buttons, 44 × 44 and 8 apart,
    with their glyphs at 32 (`−` is U+2212; the font has it, and a test says
    so). They start 8 after the value, so the eye that reads "CON 9" finds
    them without crossing the pane (they were about 500 px away at the far
    right), then the points invested (Body 20, muted)
  - below (54): the effect line (Body 20), wrapping to two lines and ending in
    an ellipsis only past the second; Wisdom's, which names three things, needs
    both
  - the value is Body, not TacticalData: that role's material tints a plain
    number violet
- **Derived stats** fill the right 470 px as one column of 8, 44 apart, name
  170 and value 300 wide. The row fed by the focused attribute is lit
  (`SheetStats.Feeds`). While a `+` or `−` that would do something is focused,
  each lit row reads `before → after` (green for a gain, red for a loss, one
  line, e.g. `29 (22%) → 31 (23%)`), from the same `SheetStats.ScoreDelta` the
  compare column uses. A stepper that would do nothing (no points, refused
  here, nothing invested) lights the rows and previews no change. A press
  redraws the preview for the next press, because focus stays on the stepper.
- Points commit on press (plan C5): there is no pending point.

### 3.3 SKILLS (C18)

- **One vertical scroll list.** Group headers are FunctionalHeading 24:
  INNATE, FROM TALENTS, FROM SPELL BOOKS, FROM THE REWARD TRACK. Empty
  groups are hidden.
- **Row**, 96 tall:
  - line 1: name (Body 22 SemiBold), then cost in pool name, cooldown and
    target/reach (TacticalData 20, right)
  - line 2: description (Body 20, up to 2 lines)
- **TALENTS**, after the skills in the same list: the kindled talents as
  2-line rows (name, description), plus the line "{n} embers unspent".
  At camp only, a link row "Open the constellation" (a Body 20 plate)
  loads the Talents scene through the existing hub route.

### 3.4 REWARD TRACK (C19)

The existing `RewardTrackScreen` is re-laid into 1196 × 660, with no
close button or frame of its own (B closes the menu). Top to bottom: the
summary row, the focus card (180 tall), the rail band, the ribbon ("THE
WHOLE ASCENT", inset 24 each side, so 1148 wide). The rail node pitch
drops from 190 to 170, showing 7 nodes. Every caption goes to the floor:
captions, ribbon numbers and level numbers Body 20; caps labels
FunctionalHeading 24 in 34-tall boxes. `RewardTrackLayout` reads the
width from `DossierLayout.DetailWidth` instead of the pane constant.

TacticalData is not used on this tab: its colour is the violet accent,
which reads as a state on numbers that are plain values.

## 4. RUN (C23)

The pane is 1520 × 756.

| Column | x | width | content |
|---|---|---|---|
| Relics | 0 | 640 | header "RELICS · {n}" with the pager on its line, a page of 5 rows |
| Effects + supplies | 664 | 400 | RUN EFFECTS list, then SUPPLIES list |
| This descent | 1088 | 432 | condensed stats |

Geometry lives in `RunLayout`; `RunPaneScreenTests` pins it in literals. The
pane uses Body for names, rarity, descriptions and figures, not
TacticalData: that role's material tints its text violet, which would turn a
rarity colour or a plain number lilac.

- **Relic row**, 132 tall (was 88): icon 64, name (Body 22) with rarity
  (Body 20, rarity colour) on one line, description (Body 20) under it.
  The longest authored description is 154 characters, three lines in the
  528-wide text column; a row that clips what a relic does defeats the pane,
  so `EveryAuthoredRelicDescriptionFitsItsRow` measures every one. Five rows
  fit under the heading line. An unknown id shows its raw id and "Unknown
  relic".
- **Run effects row**, 64 tall: label (Body 20) and value (Body 22), for
  example "Prince's favor +2", "Special pools full each turn — this leg",
  "Bonus damage +6 %", "Second lives 1". With none: "No effects on this
  descent." Four rows: one per `EventBuffs` kind plus bonus damage and
  second lives (`RunEffectRows`).
- **Supplies:** one row per consumable stack, four rows (icon 44, name,
  ×count, effect line; "and {n} more" past four), then the run's gold.
- **This descent:** Battle (5 rows), The Fold (4) and Spoils (4) headings
  (FunctionalHeading 24), rows 36 apart at Body 20/22. Battle: damage dealt,
  damage taken, healing done, foes felled, times downed. The Fold: floor,
  rooms cleared, deepest room, bosses. Spoils: gold earned, experience,
  relics carried, items in pack.

## 5. GLOSSARY (C25)

The pane is 1520 × 756.

| Column | x | width | content |
|---|---|---|---|
| Categories | 0 | 280 | rows 64 tall: label + count |
| List | 304 | 520 | 8 rows of 64, pager below (44) |
| Detail | 848 | 672 | icon 120, name 28, meta 20, body 20, locked-by 20 |

- **Category order:** Relics, Statuses, Monsters, Skills (was "Spells",
  C21), Items, Talents, Mechanics, Deeds.
- **Statuses** comes from the status definitions behind the fight's
  status box (`StatusHud`, listed by `StatusDefinitions`): name, a
  `CODE · Benefit|Detriment` meta line, and the tooltip's own sentence with the
  number left as X.
- **As built** (`GlossaryLayout`): rail rows 64 with a 4 gap, each a name over
  its "unlocked / total" count; list rows 64 with the pager 12 under the last
  row; the detail's icon (120) sits left of the name and meta, the body under
  both at the column's full width. Selection follows focus on the rail and in
  the list, so nothing there takes A. LB/RB clamp at the first and last
  category. Metas are mixed case: Body 20 in caps would sit under the caps
  floor.

## 6. PARTY (C22)

The pane is 1520 × 756.

- **Header** (64): "FORMATION" (FunctionalHeading 24) and the subtitle
  (Body 20): "The front rank takes the enemies' blows. Melee skills can
  strike only the enemy front rank."
- **Seats:** 3 × 488 wide, 420 tall, drawn Rear | Middle | Front
  (facing-enemy ribbon on the right).
  - Each seat shows the sprite (200 tall), name (Body 22 SemiBold), role
    (Body 20), an HP bar with "HP 180 / 240" (TacticalData 22) and
    "Phys def 40 · Mag def 20" (TacticalData 20).
  - While carrying, each target seat's badge reads "Swap with {name}", and
    both characters' HP and defence show in the badge (C22 preview).
- **Roster** (when C22 shows it): a row of 160-wide cards (Body 20).

## 7. OPTIONS and EXIT

The layouts are unchanged; every label reaches the floor. OPTIONS is
shared with the main menu's Settings modal. It is resized in place, and
that owner is told.

## 8. States and prompts

Prompt sets by state (pad glyph / keyboard key):

| State | Prompts |
|---|---|
| Any tab, nothing pending | A Select · B Close · LT/RT Tab · (LB/RB {what pages}) |
| LOADOUT, gear or book slot focused, worn | A Unequip / Remove · B Close |
| LOADOUT, empty slot focused | A Choose from pack · B Close |
| `SlotArmed` | A Equip · B Back |
| `BookPicked` | A Replace this book · B Back |
| PARTY `Carry` | A Place · B Put back |
| Any refused control (C3) | the prompt stays; pressing shows the notice |
| Fight context | the mode pill reads "IN A FIGHT — VIEW ONLY" and every mutating prompt label gets a lock suffix, e.g. "A Equip (locked)". The suffix is computed from `MenuPermissions`, so it follows the table in every context: a book at camp or a refund in a descent is "(locked)" too |

Keyboard names: Enter (A), Esc (B and close), Q/E (LB/RB), mouse click
for tabs, and the C/I/P/R/G hotkeys shown as a hint in each tab's tooltip.

## 9. Focus maps

Notation: → means the D-pad or stick moves focus. "Entry" is where LT/RT
or opening lands.

- **Frame:** Down from any tab goes to the pane entry. Up from the pane's
  top row goes to the selected tab. The tab rail wraps.
- **CHARACTER:**
  - Entry: the rail's selected section row. Right from the rail goes to
    the detail entry; Left from the detail's left column goes to the rail.
  - The strip is reached only through LB/RB. It is not in the D-pad graph,
    so the character never changes by accident.
  - **LOADOUT:**
    - Detail entry is the first gear slot (Head).
    - Slots form a grid by rows. Right from the slot column goes to the
      pack grid; Right from the pack goes to compare, which is not
      focusable, so it goes nowhere. Left from the pack goes to the slots.
    - Up from the pack grid's top row goes to the filter chips. The book
      row is below Legs and Shoes.
    - `SlotArmed` moves focus to the first matching pack cell; B returns
      to the slot. `BookPicked` moves focus to the first book slot; B
      returns to the pack cell.
  - **ATTRIBUTES:** entry is the first attribute's +. Rows are a list; −
    and + are a rail within the row.
  - **SKILLS:** entry is the first row. It is a list; Down past the last
    row reaches the constellation link at camp.
  - **REWARD TRACK:** as today (`RewardTrackController.Input`), entered
    from the rail.
- **PARTY:** entry is the Rear seat. Seats are a rail; Down goes to the
  roster when shown.
- **RUN:** entry is the first relic row. Two focus columns, matching where
  the focusable rows are: the relic list, and the effects and supplies
  stacked as one list (the stats column has nothing to focus). Left and
  Right cross to the row level with the focused one, Up and Down move
  within, and Up from a column's first row reaches the selected tab. LB/RB
  page the relics and focus stays on its slot. The footer offers no A: no
  row does anything when pressed.
- **GLOSSARY:** entry is the category rail. Right goes to the list, and the
  detail follows list focus. LB/RB step categories.
- **OPTIONS, EXIT:** as today.

## 10. Press counts for the validation tasks

Pad, from the fixed start (plan: Validation). Keyboard figures in
brackets.

| Task | Presses |
|---|---|
| 1 Relic mid-fight | Start → RUN entry = the first relic row: 1 (+1 per relic down) [R] |
| 2 Status mid-fight | Start, RT (GLOSSARY), RB (Statuses), Right, then Down to the row: 4 + n [G, E] |
| 3 Odette's HP mid-fight | Start, LT (PARTY): 2, with HP visible on the seat [P] |
| 4 Better boots on Shawn (Map) | Start, RB (Shawn), Right (detail), Down×3 and Right to Shoes, A (armed), A (equip): 9 [C, E, click, click] |
| 5 Book on Odette | Start, Right, Down×4 to book slot 1, A, A: 8 [I, click book] |
| 6 Spend a point | Start, Down×1 (ATTRIBUTES), Right, A: 4 [C, click, click] |
| 7 Odette to the front | Start, RT (PARTY), A (carry Rear), Right×2, A: 6 [P, click, click] |
| 8 Leave to title | Start, LT (wrap to EXIT), A, A: 4 [Esc, click EXIT, click, click] |

Phase 7 asserts the pad counts in a PlayMode journey test per task. A
count above this table is a regression.
