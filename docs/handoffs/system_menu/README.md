# Handoff: System Menu — design pass back to engineering

This is design's reply to the outbound brief engineering sent. That brief — the
empty frame, the 210px tab pitch, the four-tab bar — is superseded by everything
below; it is still readable at commit `0946625` if the history is wanted.

> **The two prototype files this document names — `System Menu.dc.html` and
> `support.js` — were never dropped into this folder.** Every other handoff here
> carries them; this one does not, so this README is the whole of the design
> pass on disk. Where it says the cards are authoritative for positions and
> colours, treat these words as the authority instead, and the numbers below as
> the ones that were built.

Sections mirror the reply's original order so a GAP_AUDIT can go one row per
section. What is built against it, and what is not, is in `REMAINING.md`.

> **Everything except the geometry, the tokens and the stated rules is placeholder.**
> Every `ART` box, node stand-in and figure ("128 gold", "8,420 damage dealt",
> "2h 14m") is dummy data invented to lay out the screen. Numbers, labels and
> copy are not authority; **positions, sizes, colours, states and rules are.**

## What the cards are

| Card | Pane | What it decides |
|---|---|---|
| `1a` | Character & Inventory | The shell: title lintel, scrim, panel frame, bar treatment, and the hosted dossier region |
| `2a` | Options | Grouped cards, two columns, everything visible without scrolling |
| `2b` | Options | Category rail alternative — also carries the gamepad focus-ring treatment |
| `3a` | Main menu | Three exits, abandon set apart behind a hold |
| `4a` | Floor map | In-run five-tab bar, and the hosted Run Map region |
| `4b` | Run statistics | The one genuinely new content pane |

`2a` and `2b` are alternatives — **pick one before building**; the recommendation
is `2a` (six groups fit at once; the rail earns its keep only past ~8 groups).

## Overview — the decisions asked for

| Question | Answer |
|---|---|
| Character/Inventory vs. the existing screen | **One tab, not two.** `CHARACTER & INVENTORY` hosts the existing combined screen. `C` and `I` keep working — the menu is another door, not a replacement. |
| Does opening pause the fight? | **Yes.** It pauses. |
| Can a tab be disabled? | **No disabled tabs.** The set is context-driven instead: out of a run, three tabs; in a run, five (Floor map and Run statistics appear). A tab that cannot be used is absent, never greyed. |
| Does it remember its tab? | **Context-appropriate.** Opened from a fight or the map: Floor map. Opened in the hub: Character & Inventory. An explicit tab passed by the caller still wins. |
| Keyboard/gamepad? | **Full.** See *States* and *Interaction logic*. |
| Close button? | **Yes** — an X at the right end of the title lintel, with the `ESC` hint beside it. Escape still works and still yields as it does today. |

## Layout (1920 × 1080 reference)

**Unchanged from `SystemMenuLayout.cs`:** panel 1600 × 900 centred (screen x 160–1760,
y 90–990); tab bar 1600 × 96 at the panel's top (panel y 0–96); content pane 1600 × 804
at panel y 96–900; tab boxes 56 tall at panel y 20; dividers 2 × 40 at panel y 28;
selected underline 3 tall at panel y 81; 40px left inset.

### The tab bar rule changes — two modes, one component

Merging Character and Inventory produces a label no 210px box can hold, and the in-run
set is five tabs, which no uniform width fits (5 × 420 = 2100 in a 1520 row). So:

**Mode A — three tabs or fewer** (out of a run). Uniform width filling the row with
130px gaps inside the 40px insets.

| Tab | Left | Width |
|---|---|---|
| CHARACTER & INVENTORY | 40 | 420 |
| OPTIONS | 590 | 420 |
| MAIN MENU | 1140 | 420 |

Dividers (2px) at **524** and **1074** — gap centres 525 / 1075. Row ends flush at 1560.

**Mode B — four tabs or more** (in a run). Width = **label width + 24px padding each
side, rounded to the nearest 4px**; the remainder inside the insets splits into equal
gaps.

| Tab | Label width | Left | Width |
|---|---|---|---|
| CHARACTER & INVENTORY | 272 | 40 | 320 |
| FLOOR MAP | 120 | 487 | 168 |
| RUN STATISTICS | 168 | 782 | 216 |
| OPTIONS | 92 | 1125 | 140 |
| MAIN MENU | 119 | 1392 | 168 |

Widths sum 1012, gaps **127**, row ends flush at 1560. Dividers (2px) at **422 / 717 /
1060 / 1327**.

**Both modes:** 40px insets each end, labels centred in their box, dividers centred in
the gaps, and one underline rule — **width = label width + 24, centred under its tab**
(296 / 116 / 143 in mode A; 296 / 144 / 192 / 116 / 143 in mode B).

Label widths are measured at 18px Chakra Petch 500, letter-spacing `.14em`.

**Overflow fallback, instead of a build failure:** if a localised label set cannot fit
1520 at minimum padding, shorten the first tab's label to `CHARACTER`. Only if that
still overflows should the bar wrap or scroll.

### New geometry (deltas — not in the original frame)

1. **Title lintel.** 1600 × 52 at screen **x 160, y 34** — above the panel, in the 90px
   of screen above it. Carries, left to right: the run title (19px, `.22em`,
   `GoldLight`), a 1px hairline, a context line (floor / room, or "Between descents"),
   then right-aligned `GOLD` and `EMBERS`, a hairline, the `ESC` hint, and the 34 × 34
   close X (1px `BorderGold` rim, hover tint `#E7B25C24`).
2. **Gold rule under the lintel.** 1600 × 1 at screen y **86**, a horizontal gradient
   fading to nothing at both ends (opaque `#E7B25C8C` between 12% and 88%).
3. **Gamepad prompt strip.** Inside each pane's own bottom padding, right-aligned — not
   panel chrome. Absent from `1a`, where the hosted dossier fills the pane and owns its
   own prompts.

## States

| Element | State | Treatment |
|---|---|---|
| Tab | selected | Label `TextPrimary`; underline `GoldLight`, 3px, at panel y 81 |
| Tab | unselected | Label `TextMuted`, no underline |
| Tab | hover | Background `#C8AAE60F` across the whole 56px box. **Drop the 1.03 scale** — it wobbles a bar whose positions are arithmetic |
| Tab | pressed | Background `#C8AAE61F`, label `TextPrimary` |
| Tab | disabled | Does not exist — see *Overview* |
| Tab | focused (pad/keys) | 2px `#E7B25C8C` outline at 2px offset, plus the hover tint |
| Row / control | hover | Row tint `#C8AAE60D` |
| Row / control | focused | 2px gold outline at 2px offset + row tint (shown on `2b`'s Music row) |
| Segmented option | on | Fill `#583216`@60%, 1px `BorderGold`, label `GoldLight` |
| Segmented option | off | 1px `#B496D23D`, label `TextMuted` |
| Menu | open | Scrim over the scene behind — **`#0A0614ED`**, see below |

## Interaction logic

As built, plus:

- **Pause on open, resume on close.**
- **LB / RB cycle tabs**; the A button activates, B closes. Prompts are printed in each
  pane so the mapping is visible rather than learned.
- **Focus is per pane**, and re-entering a pane restores its last focused row.
- **Options apply immediately** — no confirm step, no apply button. `2b` states this on
  the card.
- **Nothing on Main menu fires on a single press.** The two exits confirm; abandon is a
  **1.2s hold** with the fill drawn in the button.

## Design tokens

All from `FightHudPalette.cs`. **The two hardcoded values are replaced, no new tokens
needed:**

| Was | Use |
|---|---|
| `#E8D7A0` (underline) | **`GoldLight` `#FFE0A8`** |
| `#6B5B8A` (divider) | **`Hairline` `#C8AAE638`** |

| Where | Value |
|---|---|
| Scrim | **`#0A0614ED`** (was `#0A0614D9` — the thin one let "BETWEEN DESCENTS" read through the bar) |
| Panel fill | `PanelViolet` at 96% — `#1A1024F5` |
| Panel rim | `BorderGold` `#E7B25CB3`, 1px |
| Tab bar plate | **`#12091C`** with a `Hairline` bottom border — the bar gets its own plate, opaque against the panel |
| Group / card fill | `#12091C8C`, rim `#C8AAE638` |
| Track (sliders) | `Track` `#0E070CD9`, fill `GoldLight` |
| Body copy | `TextMuted` `#8A7AA0` (4.7:1 on the panel) |
| Titles / values | `TextPrimary` `#F4EBFF`; group headings `GoldText` `#FFD9A2` |
| `QuietHotkey` `#6D5F85` | **Only** the `ESC` glyph. It measures 3.17:1 — never body copy |
| Better / worse figures | `IntentHeal` `#7FE0A0` / `HpBright` `#E07A62` |
| Destructive framing | Fill `PanelRed` `#1F0F14DE`, rim `#E0786E73`, text `#E0A89C` |

Type is Chakra Petch throughout, matching the other handoff prototypes. Nothing in the
menu sets text below 12px; body copy is 14–19px.

## For the engine

- **The bar is still generated, not drawn.** Mode A is index × pitch as before, only the
  constants change. Mode B needs one measure pass over the labels — that is the
  deliberate change, and it is what lets the tab set vary by context without a
  hand-placed bar.
- **Capacity.** Mode B's guard should be arithmetic, not a fixed count: refuse when
  Σ(label + 48, rounded) + (n − 1) × minimum gap exceeds 1520, and say so in the same
  voice the current message uses.
- **Panes are still named containers**, one per tab. Two new ones: `SystemPaneFloorMap`
  and `SystemPaneRunStats`, both run-only.
- **Two panes host existing screens and must not be rebuilt:**
  - `CHARACTER & INVENTORY` → **`CharacterDossierScreen`** at `DossierLayout`'s
    1360 × 766, centred in the 1600 × 804 pane: 120px clear each side, **19px clear top
    and bottom**, so it never scales. Both tabs are doors into this one screen.
  - `FLOOR MAP` → the **Run Map** screen (its own handoff). Its scrolling canvas is
    authored 1920 × 820 and auto-scrolls to hold the current node ~700px from the left
    edge; hosted here the viewport narrows by 320 and clips 16px of height. Nothing
    scales, and it keeps its own mossy ground rather than the menu's violet. **Its
    pinned header and gold HUD drop** — the lintel already carries floor, room counter
    and currency. Read-only in this pane: planning, not travelling.
- **Options is one of two layouts — pick before building.** `2a` groups: Audio (3
  sliders), Display (resolution stepper, fullscreen, v-sync), Readability (text size,
  tooltip delay), Gameplay (battle speed, show tooltips), Keybinds (opens a rebind
  screen, shows a conflict count), Language (stepper, applies immediately), plus
  `RESTORE DEFAULTS`. **No autosave control** — autosave is the only save mode, so
  there is nothing to choose.
- **Main menu holds exactly three exits:** back to title (run stays as it is), quit to
  desktop (saves first), and abandon the descent (ends the run; embers kept), the last
  one framed in red and behind the hold.
- **Run statistics is eighteen figures in three groups** — Battle, The Fold, Spoils —
  under a header of floor/room, elapsed, days, turns. No charts: a run this short has
  nothing to trend. **Every row binds to a tracked field or gets cut.** Do here what
  was done to the dossier's Dodge/Carried/Shop-prices/Morale rows: if the run does not
  record it, delete the row rather than print a plausible number.

## Known rough edges — resolved

1. **Scrim too thin** → `#0A0614ED`, and the bar no longer relies on it.
2. **Bar has no plate** → it has one: `#12091C`, opaque, with a `Hairline` bottom edge.
3. **No title** → the lintel, above the panel, outside the 900.
4. **Pane has no bounds** → the panel's gold rim plus the bar's bottom hairline bound
   it; panes divide internally with `Hairline` rules, not more rims.

## Still open

- **Open/close animation.** None designed. `ReckoningController.PlayIn` is the
  reference; the only ask is that the lintel and panel arrive together, and that the
  pause takes effect on the first frame of the animation rather than at its end.
- **Sound.** Not designed — open, close, tab change, slider tick.
- **Rebind screen.** `2a` shows the door, not the room.
- **Talents / MainMenu scenes.** Still out of scope, unchanged.

---

## Where engineering departed from this

Two places, both recorded here so the deviation is visible from the design side rather
than only in a commit message. Everything else below the line in `REMAINING.md` is
scope not yet reached rather than a disagreement.

1. **"Back to title (run stays as it is)" is not built, and will not be.** `RunManager`
   states the opposite as a rule with money attached: leaving a descent, or anything
   that does not continue it, kills the run — and `EndRun` is what *pays out* the
   embers its bosses earned. Both existing doors enforce it (the hub's title button,
   the main menu's quit), and the first thing the game does at startup is settle any
   run it finds. A third title door that parked a run would leave the player at the
   title with a live descent in the save, which is exactly the state the hub's own
   button refuses to create. All three exits end the descent; each says so in a line
   under it.

2. **Abandon is absent between descents**, rather than always present. That is this
   document's own tab rule applied one level down — a control that cannot be used is
   absent, never greyed — and between descents there is no descent to abandon.
