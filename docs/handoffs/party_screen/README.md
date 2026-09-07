# Handoff: Party Screen (Roster & Formation)

## Overview
A new menu screen for Prince's Palace: view the full companion roster, place/reposition the 3 active party members via click-to-select-then-place (or drag-and-drop), and — out of a run — swap benched companions in and out. Lives alongside Character / Inventory / Run Stats in the game's menu system. No equivalent screen exists in the codebase yet — this is new.

## About the Design Files
The bundled file (`party-screen-reference/Party.dc.html`) is an **HTML design reference** — a clickable prototype showing intended layout, states and interaction. It is NOT production code and should not be ported as HTML/CSS/JS. Recreate it as a **Unity screen**, following the same pattern as the existing screens in `Assets/_Project/Scripts/Domain/UiKit/Screens/` (e.g. `FightScreen.cs`, `TalentScreen.cs`) and built via the project's `SceneBuilder` conventions — not React/web.

**⚠️ Do not reuse this file's colors, fonts, or character art as final assets.** They are stand-ins built from screenshots because I didn't have direct access to the real UI kit source or theme constants. Before shipping:
- **Style:** use the project's real **Six-theme UI kit** (`Art/UI/Buttons/Processed/` — button plates, containers, panels) and the real **TMP font assets** already in `Assets/_Project/Fonts/` (`Cinzel-SemiBold SDF` for headings/buttons, `SourceSans3-* SDF` for body text). The hex colors and Google Fonts used in the HTML are approximations sampled by eye from `tools/screenshots/HubPanel.png` / `MainMenuPanel.png` / `FightPanel.png` — close, but not the source of truth.
- **Character art:** use each character's real battle sprite from `Resources/Characters/<id>/idle.png` (the stance art `slice_actor_sheet.py` produces) exactly as `FightScreen`/`StanceAnimationLibrary` already load it — not a copied/rehosted PNG. Only `sheep` (Shawn) and `owl` (Odette) currently have this art; every other roster card in the mock is a placeholder (a plain initial monogram standee) and must be swapped for real stance art as it's delivered, per `docs/ART_PIPELINE.md`.

## Fidelity
**Mid-fidelity.** Layout, the select-then-place interaction, and screen states (empty position / locked position / camp vs. in-run / min-1-occupant rule / valid-destination highlighting) are intended to be final. Visual chrome (exact colors, border widths, corner radii, type sizes) is an approximation of the house style and should be rebuilt against the real UI kit rather than matched pixel-for-pixel.

## Screens / Views

### Party (single screen, two data-driven sections, Formation above Roster)

**Layout:** full-height dark screen. A visually distinct **prototype-controls strip** at the very top (flat black bar, monospace label, Camp/In-a-Run toggle, "lock Position 3" checkbox) — **this strip is a demo-only affordance and should not ship**; in the real game, mode is read from actual run state, not toggled by the player here. Below it, the real game chrome: top bar (back button left, `Character / Inventory / Party / Run Stats` tab row right), page title "Party" with a contextual status pill ("At Camp — Swap freely." / "In a Run — Reposition only."), then a persistent instruction banner, then **Formation** (primary/hero section), then a hairline, then **Roster** (secondary/collection section).

**Instruction banner**
- Default: "Select a companion, then choose a position."
- While something is selected: "{name} selected — choose a position." plus inline "Cancel" (always) and "Send to bench" (only when the selection came from an active position, not the roster) text actions.

**Formation** (hero section — sized to its content, not padded out)
- Header row: "Formation" label (left) + "{assigned}/3 positions filled" (right); a quiet subtitle "Order only — every position fights the same." — **positions are NOT mechanically differentiated** (see Interactions note below), so no Front/Middle/Rear role framing.
- One shared dark panel containing all 3 positions in a row, divided by hairlines. A thin top ribbon reads "Facing the Enemy →" to root the strip as a battle line (matches the real `FightPanel`'s left-party/right-enemy orientation).
- Each position column, top to bottom: "Position 1/2/3" label → (if a placement is pending) a gold "Replace {name}" / "Swap with {name}" / "Place {name} here" pill → a **fixed 150px-tall art slot** (bottom-aligned, so every character's feet land at the same line whether it's real sprite art, a monogram standee, or the empty-state icon) → a small ground-glow ellipse directly under the feet (green = occupied, gold = selected or a valid destination, faint = empty/neutral) → name → role. An occupied, locked position shows a scrim + lock icon + "Locked this run" instead.
- Clicking a filled position selects that occupant (if nothing is selected) or commits a placement into it (if something else is selected); clicking the same selected position again cancels. Occupants remain drag sources too.

**Roster** (secondary/collection section, deliberately quieter — no glow, no big frame)
- `grid-template-columns: repeat(auto-fill, minmax(112px,1fr))`, 10px gap.
- Each card: the same **fixed 76px-tall, bottom-aligned art slot** convention (sprite / monogram), name, role (plain text, no pill), then any of: "In Party · Position N" (gold, only when active), "Art pending" (only when no art exists yet), "Benched" (only in In-a-Run mode when not active). A card currently selected gets a 2px gold border, a soft gold ring shadow, a slight lift, and an explicit "Selected" tag — deliberately unmistakable, not a subtle dot.
- Cards are click targets (select/deselect) and native HTML5 drag sources; dropping a dragged card back onto the grid unassigns it from its position.

**Toast**
- Small dark gold-bordered pill, fixed bottom-center, confirms every committed action ("Shawn takes Position 2.", "Swapped Shawn and Odette.", "Turtle steps aside for Fly.") or explains a blocked one, then fades after ~2.4s.

## Interactions & Behavior
- **Two equivalent input paths, same underlying move logic:** (1) click a roster/position card to select it, then click a valid position to commit — the target shows a live "Replace X" / "Swap with X" / "Place {selected} here" preview before you commit, and every valid destination gets a gold ring; or (2) drag a card and drop it on a position or back onto the roster grid.
- **Positions are NOT mechanically different** in this design — Position 1/2/3 only preserves whatever order the game wants to display (turn order / stage order), it doesn't imply "tank up front" or similar. If the real game DOES want row-based combat roles, rename the labels and re-add role-specific copy; nothing else about the interaction changes.
- **Placing a roster card onto an occupied position** → "Replace {occupant}": the occupant is bumped to the roster (or to the newly-freed slot, if the dragged card came from a position — see Swap below).
- **Placing an already-active companion onto another occupied position** → "Swap with {occupant}": the two positions trade occupants.
- **Removing a companion from the Formation back to the Roster** (drag, or the "Send to bench" link while a positioned companion is selected):
  - Blocked entirely in **In-a-Run** mode (toast) — mid-run you may only reposition the existing 3, not remove one.
  - Blocked if it would leave **0 occupants** (toast) — the party can never be empty (supports solo runs, never zero).
- **Benched roster cards are not selectable/draggable in In-a-Run mode** (dimmed, `not-allowed` cursor) — new companions can only join between runs.
- **A locked position** rejects any placement (toast) and its current occupant can't be moved out either.
- The **prototype-controls strip is not part of the shipped design** — see note above.

## State Management
- `roster`: list of `{ id, displayName, role, spriteId|null, position: 1|2|3|null }`. Source from `ContentData/characters.json` (`id`, `displayName`, `role`, `battleSpritePath`) plus whatever save-data tracks which characters are recruited and which 3 occupy the active squad (`squadSlot` already exists in content for the starting 3, and maps directly to this screen's Position 1/2/3).
- `mode`: `'camp' | 'run'` — in the shipped game this is read from whether a run is active (`RunManager` or equivalent), not a manual toggle; the toggle in this mock exists only to preview both states and belongs in the prototype-controls strip, not the final UI.
- `positionLocked` (mock only, currently hardcoded to Position 3): stands in for a real run-modifier/curse system that would lock a squad position for the run's duration — wire to whatever system authors run modifiers, if one exists.
- `selectedId` / `selectedFrom`: the click-to-place selection (a roster id and its origin, `'roster'` or a position). Cleared on commit, cancel, or starting a drag.
- Transient: `dragId`, `dragFrom` (drag session only), `toast` (message + auto-clear timer).

## Design Tokens (reference only — replace with real UI kit values)
- **Background:** near-black/purple radial gradient, `#26193a → #150f22 → #0a0812`.
- **Gold accent:** `#c9a24a` (borders, tags), `#e8c07a` (headings, active/selected state, destination highlights), `#f0d28c` (active tab text), `#b8ab8a` (muted tab/label text).
- **Panel fills:** `#0d0c11` / `#100e16` / `#120f18` / `#14111c` (near-black, layered slightly lighter per elevation).
- **Ground-glow accents:** green `rgba(95,224,122,.55)` for an occupied, unselected position (echoes the green player-position markers on the real `FightPanel` battle floor); gold `rgba(232,192,122,.7–.75)` for a selected occupant or a valid destination; faint neutral `rgba(180,170,150,.16)` for empty/inactive.
- **Radii:** 3–4px (buttons/tags/pills), 6–10px (cards/panels), 8px (monogram standees).
- **Borders:** 1–2px, gold at 12–35% opacity depending on state, full-opacity `#e8c07a` on selection/hover/valid-destination.
- **Type:** Cinzel 600/700 for headings, buttons, labels, position names, destination pills (uppercase, letter-spaced ~0.05–0.14em); Source Sans 3 400/600 for body/instruction text. **Swap for the project's TMP font assets** (`Cinzel-SemiBold SDF`, `SourceSans3-* SDF`).

## Assets
- `sprites/sheep.png`, `sprites/owl.png` — copies of the real `Resources/Characters/sheep/idle.png` and `Resources/Characters/owl/idle.png` stance art, used as-is for Shawn and Odette. **In-engine, load the originals from `Resources/Characters/<id>/idle.png` directly — don't ship these copies.**
- All other roster cards (Placeholder Brawler, Turtle, Fly, Dog) use a plain gold-initial monogram standee as a stand-in — no real art exists for them yet. Turtle/Fly/Dog aren't in `ContentData/characters.json` today; they're named after the signature-resource narrative in that file's own `_readme` (Shell/Venom/Fangs) as a plausible near-future roster for demoing the bench/swap mechanic, not shipped content.

## Screenshots
- `party-screen-reference/screenshots/01-default.png` — default state: Formation filled 3/3, Roster below.
- `party-screen-reference/screenshots/02-selected-replace-preview.png` — Turtle (bench) selected; every position shows its live "Replace {name}" preview and a gold ring before committing.
- `party-screen-reference/screenshots/03-in-run-locked-position.png` — In-a-Run mode with Position 3 locked (scrim + lock icon + "Locked this run"), selection still active on the other two positions.

## Files
- `party-screen-reference/Party.dc.html` — the interactive prototype (open directly in a browser). Try: click any bench card (e.g. Turtle), then click a filled position to see the "Replace" preview and commit it; toggle "In a Run" in the prototype-controls strip to see benched cards lock out and removal get blocked; check "Lock Position 3" to see the locked-position state.
- `party-screen-reference/support.js` — runtime the prototype depends on; keep alongside it.
- `party-screen-reference/sprites/` — the two real sprite copies referenced above.
- `party-screen-reference/screenshots/` — static reference captures of the three key states, for a quick look without opening the HTML.
