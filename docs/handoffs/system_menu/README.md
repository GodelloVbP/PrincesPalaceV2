# Handoff: System Menu -- the overarching menu

> **This handoff runs BACKWARDS.** Every other folder in `docs/handoffs/` is a
> designer handing a finished prototype to engineering. This one is engineering
> handing a working, tested, empty **frame** to design. What comes back should
> be the normal shape -- `System Menu.dc.html` + `support.js` in this folder --
> and it will be built into the frame described below.

## Overview

A single menu reachable from anywhere in the game: a horizontal tab bar across
the top and one large content pane beneath it. Built from a sketch (top bar
`Character | Inventory | Options | Main menu`, dividers between, big content
area under).

It **replaces nothing yet**. See *Out of scope* -- the Character and Inventory
tabs overlap an existing screen that is still live, and resolving that is part
of what this design pass decides.

The skeleton is built, wired into three scenes, and covered by 16 tests. Every
content pane is **deliberately empty**.

## Files

| File | Status |
|---|---|
| `skeleton_screenshot.png` | What exists today, captured from the running game. A starting point, **not** a target. |
| `System Menu.dc.html` + `support.js` | **Yours to add.** Authoritative for layout and behaviour once it lands. |

Code, for reference -- you do not need to read it, but it is where the numbers
below are enforced:

- `Domain/UiKit/SystemMenuLayout.cs` -- all the arithmetic
- `Domain/UiKit/SystemMenuTabs.cs` -- the tab list
- `Domain/UiKit/Screens/SystemMenuScreen.cs` -- the tree
- `Core/SystemMenuController.cs` -- open/close/select

## Layout (1920x1080 reference)

The panel is **1600x900, centred** -- it occupies x 160-1760, y 90-990 in screen
coordinates. Everything below is in canvas coordinates measured from the
**panel's own centre**, which is how the codebase states positions.

| Element | Position | Size |
|---|---|---|
| Panel | centre 0, 0 | 1600 x 900 |
| Tab bar | centre y **+402** | 1600 x 96 |
| Content pane | centre y **-48** | 1600 x 804 |

Tabs run left-to-right from a 40px left inset, pitch **236** (210 wide + 26 gap):

| Tab | Centre x | Box | Divider after it |
|---|---|---|---|
| CHARACTER | -655 | 210 x 56 | x -537 |
| INVENTORY | -419 | 210 x 56 | x -301 |
| OPTIONS | -183 | 210 x 56 | x -65 |
| MAIN MENU | +53 | 210 x 56 | none (no trailing divider) |

- Dividers: 2 x 40, centred in the gap.
- Selected underline: 147 x 3, y -34 relative to the bar centre.
- **Capacity: 6 tabs.** 4 are used. A 7th fails the build (see *For the engine*).

## States

Only two interactive states exist today. **Naming the rest is your job** -- the
skeleton has no hover, pressed, disabled or focus treatment beyond a 1.03 hover
scale on the tab buttons.

| Element | State | Reads as today |
|---|---|---|
| Tab | selected | gold underline `#E8D7A0`, 147x3, beneath the tab |
| Tab | unselected | no underline |
| Tab | hover | 1.03 scale pop (shared button behaviour) |
| Pane | selected | active |
| Pane | unselected | switched off entirely |
| Menu | closed | whole modal inactive |
| Menu | open | modal scrim `#0A0614D9` over the screen behind |

Please specify at minimum: tab hover, tab pressed, tab disabled (if a tab can
ever be unavailable), and whether the scrim strength is right -- see *Known
rough edges*.

## Interaction logic (as built)

- **Escape opens the menu.** Escape closes it when open.
- Escape **yields** to anything already using it: the character sheet in the
  fight and the map, and the glossary and relic draft in the hub. Those are
  declared per scene, not guessed by name.
- Clicking a tab selects it. **Exactly one** pane and one underline are live at
  a time; that is enforced by test, not by convention.
- Default tab is index 0 (Character), and the default does **not** override a
  tab chosen by whoever opened the menu.
- There is **no close button** -- Escape only. Add one if the design wants it,
  and say where.

Undecided, and yours to answer:

1. Does opening the menu **pause** the fight? It currently does not.
2. Can a tab be **disabled** in some contexts (e.g. Character during a fight)?
3. Should the menu **remember** its last tab between openings, or always open on
   Character?
4. Is there a **keyboard/gamepad** path through the tabs, or mouse only?

## Design tokens

Use the existing vocabulary -- `Domain/UiKit/FightHudPalette.cs` is the shared
list, so a new screen reads as part of the same suite. What this menu already
touches:

| Token | Value | Used for |
|---|---|---|
| `TextPrimary` | `#F4EBFF` | tab labels |
| `TextMuted` | `#8A7AA0` | the placeholder text |
| `GoldLight` | `#FFE0A8` | the selected-tab family |
| `PanelViolet` | `#1A1024E6` | panel fills elsewhere |
| `BorderGold` | `#E7B25CB3` | gold rims elsewhere |
| `Hairline` | `#C8AAE638` | dividers elsewhere |

The skeleton currently hardcodes `#E8D7A0` (underline) and `#6B5B8A` (divider)
because no token matched. **Replace both with tokens**, or tell me which to add.

## For the engine

Things the frame does that a mockup should not fight:

- **The tab bar is generated, not drawn.** Positions come from index x pitch. A
  mock that hand-places four tabs is fine as a picture, but say explicitly if
  you want a different *rule* -- centred rather than left-aligned, or tabs sized
  to their text -- because that changes the arithmetic and I need it stated
  rather than measured off a PNG.
- **Adding a tab must stay one line.** If the design needs 7 or more, the bar
  has to wrap, scroll or narrow; it cannot simply be given another entry. The
  build refuses with: *"The system menu has 8 tabs but the bar holds 6 at 210px
  wide. Either widen the panel, narrow the tabs, or the bar needs to wrap or
  scroll."*
- **Panes are named containers**, one per tab: `SystemPaneCharacter`,
  `SystemPaneInventory`, `SystemPaneOptions`, `SystemPaneMainMenu`. Design each
  independently; nothing about one affects another.
- **Layout is checked at build time** at four canvas aspects -- overlaps,
  overflow, duplicate names and zero-sized graphics fail the build. Overlapping
  deliberately is fine, it just has to be *stated* so the exemption carries a
  reason.
- **Nothing is hooked up.** Options does not read `GameSettings`, Main menu does
  not navigate, Character and Inventory show nothing. Behaviour comes after
  layout.

## Known rough edges in the screenshot

Worth knowing so they are not mistaken for intent:

1. The scrim (`#0A0614D9`) is **thin** -- hub text reads through behind the tab
   bar, and "BETWEEN DESCENTS" collides visually with "MAIN MENU". Either the
   scrim goes darker or the bar gets its own opaque plate.
2. The bar has **no plate of its own** -- it is transparent over whatever is
   behind it. Almost certainly wrong, and the single biggest thing the design
   needs to answer.
3. There is **no title** on the menu, and no visible frame around the panel.
4. The content pane has no visible bounds at all; only the placeholder text
   marks where it is.

## Out of scope

Deliberately not covered, so a gap is not read as an oversight:

- **The Character/Inventory overlap.** Those two tabs duplicate the existing
  `CharacterOverlayScreen` (paperdoll + bag), reachable today on **C** and **I**
  and fully built. It was *not* removed -- deleting a working screen is a
  migration, not a skeleton. Whether these panes host that screen, replace it,
  or link to it is a design decision, and the most consequential one here.
- **Talents and MainMenu scenes.** The menu is embedded in Hub, Map and Fight
  only. Talents has no overlay precedent; MainMenu already has its own Options
  panel, and two Options screens would be worse than none.
- **Content for any pane.** All four are empty.
- **Open/close animation.** None. Every other modal in the game has one --
  `ReckoningController.PlayIn` is the reference.
- **Sound.** No open, close or tab-change sound is wired.
- **Gamepad/keyboard navigation.**

## When the design lands

Drop `System Menu.dc.html` + `support.js` in this folder. I will build it
against this frame and then write `GAP_AUDIT.md` here -- one row per section
above, spec versus `file:line`, verdict -- per `docs/HANDOFF_TEMPLATE.md`.
