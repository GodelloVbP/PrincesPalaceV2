# GAP_AUDIT: Party screen

Audited against `README.md` and `DECISIONS.md` (which overrides the README
wherever the two disagree — recorded there, not re-litigated here). Every
"Built" cell below was checked by opening the cited file at the cited line,
not taken from a commit message or from `DECISIONS.md`'s own prose.

Current state as of commit `e6a34224` (this session's toast reposition).
Package history: P1-P2 (`Domain/Party/*`, `PartyScreen.cs`/`PartyLayout.cs`,
tree only), P3 (`PartyController.cs`, `ScreenRegistry.WireParty`, click
interaction), P4 (`PartyDragSource.cs`, `PartyToast.cs`, drag-and-drop, the
fade).

## Overview / framing

| # | Handoff section | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 1 | Overview | A new menu screen: view roster, place/reposition 3 active members, swap benched companions out of a run; lives alongside Character/Inventory/Party/Run Stats | `PartyScreen.cs` built and hosted as a `SystemMenuTab.Party` entry (`SystemMenuTabs.cs:34`), wired for real in `ScreenRegistry.cs:863` (`WireParty`) | match |
| 2 | About the Design Files — do not reuse the mock's colors/fonts/character art | Real Six-theme kit + real TMP fonts + real `Resources/Characters/<id>` art, not the HTML mock's assets | `PartyScreen.cs` reuses `FightHudPalette` tokens (`PartyScreen.cs:51-61`) and `TypographyRole` styling (`.Styled(TypographyRole.FunctionalHeading/Body/TacticalData)` throughout); art loads via `PartyController.ArtFor` → `StanceAnimationLibrary.Resolve` off `Resources/Characters/<id>` (`PartyController.cs:850-856`), not a copied sprite | match |
| 3 | Fidelity — mid-fidelity; visual chrome rebuilt against the real kit, not pixel-matched | Layout/interaction/states final; colors/borders/radii approximate | Panel ground is a themed `Ui.SystemMenuPane(..., ButtonTheme.Silver, ...)` container (`PartyScreen.cs:178`), not a hand-authored gradient/border — kit-driven per the brief | match |

## Screens / Views

| # | Requirement | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 4 | Page title inside the pane | Handoff shows a "Party" page title inside the panel | No such node exists anywhere in `PartyScreen.cs` (grepped for a title label — none) | deliberate-deviation — `DECISIONS.md:34-38`: the system-menu tab already reads PARTY and the lintel sits above the panel, "no redundant 'Party' heading inside the content" |
| 5 | Prototype-controls strip (Camp/In-a-Run toggle, "lock Position 3" checkbox) | A flat black demo bar at the top | Not present — no such node in `PartyScreen.cs`; mode comes from `PartyController.Refresh` reading `lockedForFight`/`menu.InDescent` (`PartyController.cs:311-321`) | deliberate-deviation — the handoff's own README says it "should not ship" (`README.md:20`); `DECISIONS.md:40-44` records the call explicitly |
| 6 | Status pill with contextual copy | "At Camp — Swap freely." / "In a Run — Reposition only." | Built (`PartyScreen.cs:213-227` StatusPill/StatusText; `PartyController.PaintHeader`, `PartyController.cs:560,564-572` `StatusStringFor`) | match, extended (see row 7) |
| 7 | Three status states, not two | Handoff names only Camp/Run | `PartyMode` has `Camp`/`Run`/`ViewOnly` (`PartyMode.cs:9-14`); `UiStrings.PartyStatusCamp/Run/Fight` (`UiStrings.cs:867-872`): "AT CAMP — Swap freely.", "IN A RUN — Reposition only.", "IN A FIGHT — Formation fixed." | deliberate-deviation — `DECISIONS.md:24-33`, front-rank rule landed 2026-09-07 and a fight owns its own order |
| 8 | Instruction banner, default + selected copy, inline Cancel/Send-to-bench | Default: "Select a companion, then choose a position."; selected: "{name} selected...", Cancel always visible, Send-to-bench only from an active-seat selection | `PartyScreen.cs:191-211` (Banner/CancelLink/BenchLink); `PartyController.PaintHeader` `PartyController.cs:551-558` sets banner text and `cancelLink.SetShown(hasSelection)` / `benchLink.SetShown(Formation.CanSendToBench)`; `PartyFormation.CanSendToBench` (`PartyFormation.cs:179-184`) requires Camp + seat-origin + `FilledCount>1` + unlocked | match |
| 9 | Formation header row: "Formation" + "{n}/3 positions filled" | Left label, right count | `PartyScreen.cs:239-255`; `PartyController.PaintHeader`, `PartyController.cs:561` `filledCount.Set(UiStrings.PartyFilledCount, Formation.FilledCount)` | match |
| 10 | Formation subtitle: "Order only — every position fights the same." | Positions cosmetic-only | `UiStrings.PartyFormationSubtitle` (`UiStrings.cs:876-878`) reads "The front rank takes the enemies' blows. Some skills reach only from the rank they name." | deliberate-deviation — `DECISIONS.md:8-23`, the front-rank rule makes seats mechanically different; the handoff's own escape clause is invoked |
| 11 | "Facing the Enemy →" ribbon | Thin top ribbon over the 3-column strip | `PartyScreen.cs:278-283`, `UiStrings.PartyFacingRibbon` = "FACING THE ENEMY ->" | match |
| 12 | 3 positions in one shared panel, divided by hairlines | One dark panel, 3 columns | `BuildFormationPanel`/dividers, `PartyScreen.cs:267-304` | match |
| 13 | Position labels "Position 1/2/3" | Order-only, role-neutral labels | `SeatLabelFor` returns `PartySeatFront`/`Middle`/`Rear` = "FRONT"/"MIDDLE"/"REAR" (`PartyScreen.cs:423-431`, `UiStrings.cs:884-886`) | deliberate-deviation — same rationale as row 10 |
| 14 | Column draw order (left→right = Position 1,2,3) | Positions drawn in seat-index order | `PartyLayout.VisualColumnForSeat` mirrors the order: seat 0 (front) draws in the **rightmost** column (`PartyLayout.cs:148-157`) | deliberate-deviation — `DECISIONS.md:15-19`, keeps front nearest the "facing the enemy" ribbon, matching the fight stage's party-left/enemy-right orientation |
| 15 | Gold "Replace {name}"/"Swap with {name}"/"Place {name} here" pill, live preview before commit | Per-seat pending-placement pill | `PartyScreen.cs:371-383` (Badge node); `PartyFormation.SeatBadge` (`PartyFormation.cs:136-153`) computes which of `None`/`PlaceHere`/`Replace`/`SwapWith`; `PartyController.SetSeatBadgeText` (`PartyController.cs:615-632`) fills the copy from `UiStrings.PartyBadgePlace/Replace/Swap` | match |
| 16 | Fixed 150px art slot, bottom-aligned, shared feet line | Every seat's figure/monogram/empty-state shares one foot line | `PartyLayout.ArtHeight = 150f` (`PartyLayout.cs:106`), `FeetLine` shared across all 3 columns (`PartyLayout.cs:175`); `PartyController.AlignArtSlots` pins the baseline once (`PartyController.cs:250-270`), `GroundArt` (`PartyController.cs:740-757`) then offsets per-actor by `StanceManifestLoader.Manifest.GroundLineFor`, scaled to the slot's own drawn size | match, and closes a gap `DECISIONS.md:125-146` itself flags as P4 work (two actors with different sprite headroom now share a real foot line, not just a shared canvas-bottom) |
| 17 | Ground-glow ellipse under feet, green/gold/faint by state | Occupied=green, selected/valid-destination=gold, empty=faint neutral | `PartyScreen.cs:326-332` (`SeatGlows`, sprite `"proc:radial_glow"` — a real baked radial-falloff sprite, `ProceduralSpriteBaker.cs:35`, not literally an ellipse but the same soft-glow shape); `PartyController.GlowColorFor` (`PartyController.cs:892-896`) with `GlowNeutral`/`GlowOccupied`/`GlowHighlighted` hex constants (`PartyController.cs:111-113`) read off the handoff's own token table | match |
| 18 | Name + role text under the figure | Plain text | `PartyScreen.cs:385-399`; `PartyController.PaintSeat`, `PartyController.cs:601-602` | match |
| 19 | Locked position: scrim + lock icon + "Locked this run" | Scrim, icon, caption | Scrim + caption built and painted (`PartyScreen.cs:406-417` SeatScrim/ScrimCaption; `PartyController.PaintSeat`, `PartyController.cs:606-612`, `UiStrings.PartyScrimLocked`) — **no lock-icon graphic**, caption text only | partial — the icon in the design's own screenshot (`party-screen-reference/screenshots/03-in-run-locked-position.png`) has no built equivalent; nothing in `PartyScreen.cs`'s scrim declares an icon sprite |
| 20 | Closed seat state ("No seat yet") | Not in the original handoff at all | Same scrim mechanism, gated on `PartyFormation.IsSeatClosed` (`PartyFormation.cs:114-118`), caption `UiStrings.PartyScrimClosed` = "No seat yet" (`UiStrings.cs:904`), painted at `PartyController.cs:606-612` | match (added per `DECISIONS.md:52-58`, not a handoff requirement) |
| 21 | Click filled seat → select; click same seat again → cancel; click with selection → commit | The click-to-place state machine | `PartyFormation.ClickSeat` (`PartyFormation.cs:190-254`) implements all four branches (select, self-cancel, replace, swap) in the stated precedence; `PartyController.ClickSeat` (`PartyController.cs:328-332`) is a thin pass-through | match |
| 22 | Occupants remain drag sources | A seated card can start a drag too | `PartyController.BeginSeatDrag` (`PartyController.cs:397-409`), wired per seat via `PartyDragSource` (`ScreenRegistry.cs:897`) | match |
| 23 | Roster: `grid-template-columns: repeat(auto-fill, minmax(112px,1fr))`, 10px gap — a wrapping/auto-filling grid | An auto-filling multi-row grid | One fixed row: `PartyLayout.CardWidth=150f`, `CardGap=14f` (`PartyLayout.cs:204-207`); `RosterFits`/`MaxRosterCards` (`PartyLayout.cs:256-265`) refuse a count that would need a second row rather than wrapping one — at today's `UsableWidth` (~1480px) that ceiling computes to 9 cards | deliberate-deviation — `DECISIONS.md:64-72`; a build-time refusal replaces a browser's free-flowing wrap, which uGUI has no equivalent for without a second layout pass this package didn't add |
| 24 | Roster card: fixed 76px art slot, bottom-aligned (same convention as Formation) | Shared foot line per card | `PartyLayout.CardArtHeight=76f` (`PartyLayout.cs:210`), `CardArtBottom` shared line (`PartyLayout.cs:240`); same `AlignArtSlots`/`GroundArt` machinery as seats (`PartyController.cs:217,642`) | match |
| 25 | Card name/role, then one of "In Party · Position N" / "Art pending" / "Benched" | Priority order: active > no-art > benched > nothing | `PartyController.PaintCard` (`PartyController.cs:634-683`) implements exactly that if/else-if chain against `PartyCardState` (`PartyFormation.CardState`, `PartyFormation.cs:157-177`); "Position N" is now "In party · FRONT/MIDDLE/REAR" (`UiStrings.PartyCardTagInParty`, `PartyController.cs:654` via `SeatLabelFor`) | match, tag wording follows the row-13 seat-label deviation |
| 26 | Selected card: 2px gold border + ring shadow + lift + explicit "Selected" tag | Ring, glow-shadow, a lift (scale/translate), and a tag | `CardRings`/`CardSelectedTags` built and shown on `state.IsSelected` (`PartyScreen.cs:537-553`, `PartyController.cs:674-675`) — **no lift**: no `localScale`/position change exists anywhere in `PartyController.cs` or `PartyScreen.cs` for a selected card | partial — ring + tag shipped, the "lift" (and any soft glow-shadow beyond the ring itself) did not |
| 27 | Roster cards: click targets + drag sources; dropping back on the grid unassigns | Click-to-select and drag-to-unassign | Click: `PartyController.ClickCardAt` (`PartyController.cs:352-356`). Drag: `BeginCardDragAt`/`EndDrag` (`PartyController.cs:411-423,436-477`) → `PartyFormation.DropOnRoster` (`PartyFormation.cs:319-323`), which **is** `SendToBench` (not a second rule set) | match — "native HTML5 drag" is necessarily reimplemented as uGUI's `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler` (`PartyDragSource.cs:25-36`), an engine translation rather than a behavioral gap |
| 28 | Benched cards in Run mode are dimmed with a `not-allowed` cursor and are not selectable/draggable | Visually and functionally inert | `PartyFormation.CardState.IsSelectable` correctly returns false for a benched card in Run (`PartyFormation.cs:173-174`); `PartyController.PaintCard` dims via `CardWashes` but **keeps the button clickable on purpose** ("DIMMED, NEVER DISABLED", `PartyController.cs:677-682`) so the model can still explain the refusal via toast; no cursor change exists anywhere (no `Cursor.SetCursor` call in the assembly) | partial — the dim and the (correct) click-through-to-toast behavior are built; the `not-allowed` cursor affordance is not, and Unity's cursor API is simply never touched here |
| 29 | Toast: small dark gold-bordered pill, fixed bottom-center, confirms/explains every action, fades ~2.4s | Bottom-center overlay over the roster | Style/fade: match (`PartyScreen.cs:561-586` OutlineBox styling; `PartyToast.cs` Hold 2.0s + Fade 0.4s = 2.4s). **Position**: no longer bottom-center — this session's Task 1 moved it into the ROSTER heading row, right-aligned (`PartyLayout.cs:267-286`, `PartyScreen.cs:569-582`) | deliberate-deviation, this session — the bottom-center position sat directly over the bottom of the card row, hiding a swapped card's own role/tag lines (`tools/screenshots/runtime/SystemMenu_party_toast.png` before the fix); commit `e6a34224` |

## Interactions & Behavior

| # | Requirement | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 30 | Two equivalent input paths (click-then-click, or drag-and-drop) driving the same underlying move logic | No divergent rule sets | `PartyFormation.Drop` (`PartyFormation.cs:348-371`) resolves to the exact same `ClickSeat` call a click path would take; `PartyController.EndDrag` (`PartyController.cs:436-456`) is the only drag-specific code, and it hands off to `Apply` the same way a click does (`PartyController.cs:363-368`) | match |
| 31 | Positions are NOT mechanically different (order-only) | No row/rank framing | Reversed: `PartyFormation.cs:12-16` states seats ARE mechanical (front rank absorbs melee); `PartySeat.cs:1-8` names them Front/Middle/Rear | deliberate-deviation — same front-rank-rule rationale as rows 7/10/13 |
| 32 | Placing a roster card on an occupied seat → Replace | Occupant bumped to roster | `PartyFormation.ClickSeat`, Roster-origin branch (`PartyFormation.cs:224-236`) | match, mechanism only — see row 36 for reachability |
| 33 | Placing an already-active companion on another occupied seat → Swap | Two seats trade occupants | `PartyFormation.ClickSeat`, Seat-origin branch (`PartyFormation.cs:238-253`) | match |
| 34 | Removing from Formation to Roster blocked entirely in Run mode | Toast, no state change | `PartyFormation.SendToBench` (`PartyFormation.cs:303-304`) returns `RepositionOnlyDuringRun` when `Mode==Run` | match |
| 35 | Removing blocked if it would leave 0 occupants | Party can never be empty | `SendToBench`, `FilledCount==1` guard (`PartyFormation.cs:306-307`) → `PartyNeverEmpty` | match |
| 36 | Benched roster cards not selectable/draggable in Run mode (dimmed, not-allowed cursor) | See row 28 | Covered at row 28 | partial (see row 28) |
| 37 | Locked position rejects any placement, and its occupant can't be moved out either | Both directions blocked | `ClickSeat` locked check (`PartyFormation.cs:215-219`) refuses a placement INTO a locked seat; `SendToBench`'s own locked check (`PartyFormation.cs:310-311`) refuses moving the occupant OUT | match |
| 38 | Prototype-controls strip is not part of the shipped design | Not built | Confirmed not built — see row 5 | deliberate-deviation (duplicate of row 5, restated per the handoff's own Interactions section) |
| 39 | "Replace" and "Swap" outcomes reachable in the shipped game | Any roster member can be off-seat and re-fielded | `characters.json` authors exactly 3 characters against exactly 3 seats, so in Camp every character is always seated — a Roster-origin selection (the only path to `Replaced`) cannot occur through the UI today; `Swapped` (two seated members) IS reachable and is what `tools/screenshots/runtime/SystemMenu_party_toast.png` shows | deliberate-deviation, content-shape limit — `SystemMenuPartyTests.cs:25-34` states this plainly rather than skipping the case silently; covered at the model level only by `PartyFormationTests.cs` (e.g. `PartyFormationTests.cs:444,459`) |

## State Management

| # | Requirement | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 40 | `roster`: `{id, displayName, role, spriteId|null, position}` sourced from `characters.json` + save data | One flat per-character record | Split across layers instead of one flat shape: `PartyRosterEntry` carries only `Id`/`DisplayName`/`HasArt` (`PartyRosterEntry.cs:14-18`); `role` is resolved separately by `PartyController.RoleNameOf` at paint time (`PartyController.cs:822-826`), not stored on the entry; `position` lives in `PartyFormation`'s own seats array, not on the roster record. Built from `save.roster`/`save.selectedCharacterIds` in `PartyController.Refresh` (`PartyController.cs:274-321`) | partial — same information is available, deliberately factored across Domain (`PartyRosterEntry`/seats) vs. Core (`role`) rather than as one record; a real layering choice, not an oversight, but a different shape than the handoff specified |
| 41 | `mode: 'camp' \| 'run'` read from real run state, not a manual toggle | e.g. `RunManager` or equivalent | `PartyController.Refresh`, `PartyController.cs:311-313`: `lockedForFight ? ViewOnly : (menu.InDescent ? Run : Camp)` — `menu.InDescent` is `SystemMenuController`'s existing run-state read, the same one `ExitsController`/`CharacterDossierController` already use | match, and extended to 3 modes (row 7) |
| 42 | `positionLocked` (mock: hardcoded to Position 3) → wire to a real run-modifier system if one exists | A real lock source | No run-modifier system exists; `PartyController.Refresh` passes `_ => false` for every seat (`PartyController.cs:315-321`) — locked is a rendered predicate with no production feeder | deliberate-deviation, flagged not hidden — `DECISIONS.md:46-50`, `PartyFormation.cs:18-23`'s own header names the seam for a future system to fill |
| 43 | `selectedId`/`selectedFrom` (roster id + origin) | The pending click-to-place selection | `PartyFormation._selectedId`/`_selectedFrom`, exposed via `SelectedId`/`SelectedFrom` (`PartyFormation.cs:129-134`); `PartySelectionSource` (Roster vs. Seat) (`PartySelectionSource.cs:26-53`) | match |
| 44 | Transient `dragId`/`dragFrom` for a drag session | Separate drag-session state | No separate fields — a drag reuses the SAME `Formation` selection a click would set (`PartyController.BeginSeatDrag`/`BeginCardDragAt` call `ClickSeat`/`ClickCard` directly, `PartyController.cs:397-423`); `_dragging` (`PartyController.cs:134`) is the only drag-specific flag, gating whether the ghost shows | match, by design (`PartyFormation.Drop`'s own header, `PartyFormation.cs:331-347`, explains why a second copy of selection state was deliberately avoided) |
| 45 | Transient `toast` (message + auto-clear timer) | A timed toast | `PartyToast.Show`/`HoldThenFade` (`PartyToast.cs:36-63`) | match |

## Design Tokens

| # | Requirement | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 46 | Background: near-black/purple radial gradient (`#26193a → #150f22 → #0a0812`) | A custom gradient background | No custom gradient — the pane ground is the kit's own `ButtonTheme.Silver` 2:1 container art (`PartyScreen.cs:178`) | match — the handoff's own instruction is "replace with real UI kit values" (`README.md:59`), and using the kit's container art rather than a hand-painted gradient is exactly that |
| 47 | Gold accent colors (`#c9a24a`/`#e8c07a`/`#f0d28c`/`#b8ab8a`) | Borders, headings, active states | `FightHudPalette.GoldText`/`GoldLight`/`BorderGold` reused throughout (`PartyScreen.cs:53-61`; hex values at `FightHudPalette.cs:50,78-79`) rather than the handoff's own approximated hexes | match |
| 48 | Panel fills (`#0d0c11`/`#100e16`/`#120f18`/`#14111c`) | Layered near-black fills | `FightHudPalette.CardFill = "#12091C8C"` (`FightHudPalette.cs:48`) reused for both the Formation panel fill and card/badge grounds — one shared token, not four layered ones | partial — same near-black family, collapsed to one reused token rather than 4 elevation-distinct ones (no separate elevation levels were built for this pane) |
| 49 | Ground-glow accent colors (green/gold/faint, with the handoff's own alpha values) | `rgba(95,224,122,.55)` / `rgba(232,192,122,.7-.75)` / `rgba(180,170,150,.16)` | `PartyController.cs:111-113`: `#5FE07A8C` (0.545), `#E8C07AB3` (0.70), `#B4AA9629` (0.16) — matches the handoff's own numbers almost exactly | match |
| 50 | Radii (3-10px) / border widths (1-2px) | Kit-typical rounding and stroke widths | Inherited from `Ui.OutlineBox`/kit container art rather than independently authored per node — no custom radius/stroke value appears in `PartyScreen.cs` | match (kit-driven, per the handoff's own "replace with real UI kit values" instruction) |
| 51 | Typography: Cinzel 600/700 headings, Source Sans 3 body, tracked uppercase | Cinzel-SemiBold SDF / SourceSans3-* SDF | `.Styled(TypographyRole.FunctionalHeading)` for headings, `.Styled(TypographyRole.Body)` for body text, `.Styled(TypographyRole.TacticalData)` for tracked-uppercase labels (badges, status pill, seat labels) — used throughout `PartyScreen.cs` | match |

## Assets

| # | Requirement | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 52 | Load `sheep`/`owl` art from `Resources/Characters/<id>/idle.png` directly, not the handoff's bundled copies | Real art, loaded live | `PartyController.ArtFor` → `StanceAnimationLibrary.Resolve(definition.Data.BattleSpritePath, FightSession.Stances.Idle)` (`PartyController.cs:850-856`) — no reference anywhere to `party-screen-reference/sprites/` | match |
| 53 | Monogram-standee fallback for characters with no art (Turtle/Fly/Dog in the mock) | A plate + a letter | Built and functional: `PartyController.MonogramFor`/`SetMonogram` (`PartyController.cs:868-872,909-916`), shown whenever `ArtFor` returns null | match as code, but see row 54 for reachability |
| 54 | Turtle/Fly/Dog as a 5-character demo roster | Not shipped content (handoff's own words) | `characters.json` authors exactly 3 (`sheep`, `bear`, `owl`); as of 2026-09-07 all three point `battleSpritePath` at their own delivered stance art (`bear` was the last to still borrow `Characters/sheep`, see `DECISIONS.md:147-154`), so with today's content the monogram fallback path is real but **unreachable** through the game — every card/seat always has art | deliberate-deviation, flagged not hidden — `DECISIONS.md:147-154` names this explicitly: "the moment any future character ships with no `battleSpritePath`, the fallback is already live" |

## Verdict tally

54 rows total (some restate the same underlying deviation from two different
README sections — e.g. row 5 and row 38 are both "prototype strip", rows 7/10/
13/31 are all the front-rank-rule deviation — counted once per row as the
template's "one row per discrete requirement" asks, not de-duplicated away):

- **match: 35** — rows 1, 2, 3, 6, 8, 9, 11, 12, 15, 16, 17, 18, 20, 21, 22,
  24, 25, 27, 30, 32, 33, 34, 35, 37, 41, 43, 44, 45, 46, 47, 49, 50, 51, 52, 53
- **deliberate-deviation: 13** — rows 4, 5, 7, 10, 13, 14, 23, 29, 31, 38, 39,
  42, 54
- **partial: 6** — rows 19, 26, 28, 36, 40, 48
- **missing: 0**

Nothing was found missing outright: everything the README/DECISIONS describe
is either built as specified, built with a recorded and justified deviation,
or built partially with the gap named plainly rather than glossed over.

## Open, owner's calls

- **The locked-seat scrim has no lock icon**, only a caption (row 19). Cheap
  to add (`Ui.Sprite` + a lock glyph) once art exists for one; not blocking
  anything today since no run-modifier system locks a seat yet (row 42).
- **The selected roster card has no "lift"** (row 26) — ring and tag exist,
  the scale/translate does not. A one-line addition if the owner wants it;
  skipped here because nothing in `CODE_STANDARDS.md`'s reuse-first registry
  hands a menu-card lift for free the way it does the toast's fade.
- **No `not-allowed` cursor for benched-in-Run cards** (rows 28/36). The dim
  wash carries the same information; whether a real cursor change is worth
  adding is a small polish call, not a behavior gap (the click already
  refuses correctly and explains why via toast).
- **Panel-fill tokens collapsed to one reused hex** rather than the handoff's
  4 elevation-distinct fills (row 48). Reads flatter than the mock in
  exchange for reusing an existing `FightHudPalette` token instead of
  authoring 3 new ones nobody else uses.
- **`PartyRosterEntry` doesn't carry `role`/`position`** (row 40) — a
  deliberate Domain/Core split (Domain can't see `ContentDatabase`), not an
  oversight, but worth knowing if a future package wants one flat roster
  record instead of three lookups.
- **Replace and Benched are unreachable through the UI today** (rows 25b/39),
  purely because `characters.json` has exactly 3 characters against exactly
  3 seats. This stops being cosmetic the day a 4th character ships — nothing
  needs to change in `PartyFormation`, but `SystemMenuPartyTests.cs` should
  gain a real benched-card PlayMode case at that point rather than continuing
  to rely on `PartyFormationTests.cs`'s model-only coverage.
- **Roster count is fixed at scene-build time**, but — correcting a reading
  `DECISIONS.md:64-72`'s own wording invites — it is NOT stuck on the
  3-character compatibility default in production: `ScreenRegistry.cs` calls
  `FightScreen.Build(ContentDatabase.Characters.Count)` /
  `HubScreen.Build(ContentDatabase.Characters.Count)` /
  `MapScreen.Build(ContentDatabase.Characters.Count)` (`ScreenRegistry.cs:100,
  338,523`, added in the same P3 commit `bb54d564` that wrote that
  `DECISIONS.md` entry). The `3` default on `SystemMenuScreen.Build`/
  `PartyScreen.Build` only fires for a caller that omits the argument (tests,
  mainly). Real content growth still needs a scene rebuild to show up (scenes
  are generated artifacts, per this project's own rule), but it is not
  hand-pinned to today's 3 the way the prose alone suggests.
