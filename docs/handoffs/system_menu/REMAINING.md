# System menu — what is built, and what is not

Running note against the design pass in `README.md`. Kept here rather than in a
commit message because it outlives any one commit, and rather than in `AUDIT.md`
because none of it is a defect — it is scope that has not been reached yet.

Last updated 2026-08-19.

## Built

The **shell**, which is most of the design's *Layout*, *States* and *Design
tokens* sections:

- Tab bar in **two modes**, one component. Mode A (≤3 tabs) fixes the 130px gap
  and shares the row out; Mode B (≥4) sizes each box to its label + 24px each
  side rounded to 4, and shares the remainder into equal gaps. Both hit the
  design's numbers exactly — 420/40/590/1140 and 320/168/216/140/168 with a
  127px gap — and `SystemMenuScreenTests` pins both against those literals.
- **Context-driven tab set.** Three tabs between runs, five during one. No
  disabled state anywhere; an unusable tab is absent.
- **Character and Inventory merged** into one tab. `C` and `I` both open it.
- **Title lintel** above the panel with run title, context line, gold, embers,
  `ESC` hint and a close X, plus the gold rule under it.
- **Tokens**: scrim `#0A0614ED`, panel `#1A1024F5` with a `BorderGold` rim, bar
  plate `#12091C` with a hairline bottom, underline `GoldLight`, dividers
  `Hairline`. The two hardcoded values are gone.
- **Hover is a plate, not a scale.** The 1.03 pop is deleted.
- **Pause on open, resume on close**, restoring the previous `timeScale` rather
  than assuming 1, and also on `OnDisable` so a scene change cannot strand the
  game at zero.
- **Capacity guard is arithmetic**, not a count — a set with the same number of
  wordier labels is refused, which a count-based limit called fine.
- Dossier hosted at 1360×766 in the 1600×804 pane: 120 clear each side, 19 top
  and bottom, no scaling.

## Options — built, and four of its six groups cut

Layout `2a`, two columns, no scrolling, applies immediately, `RESTORE DEFAULTS`.
`GameSettings` had been fully implemented for a while with **no UI at all**;
this is the first screen that reaches it.

**Two groups survived, because `GameSettings` holds five values and nothing
else exists to bind to.** The design's own rule for Run statistics — every row
binds to a tracked field or gets cut — is what decided it: a control that
stores nothing is worse than an absent one, because the player moves it,
believes something changed, and is wrong.

| Group | Rows | Status |
|---|---|---|
| Audio | Sound, Music | Built. **Two sliders, not three** — there are two channels; `AudioLevels` is a per-sound gain table, not a third bus. Music is labelled "Stored - no music yet", which `GameSettings`' own header asks for. |
| Display | Resolution, Window, Frame limit | Built. **Frame limit replaces v-sync**, which is not stored; the frame limit is the real setting sitting next to it. |
| Readability | text size, tooltip delay | **Cut.** Neither value exists anywhere. |
| Gameplay | battle speed, show tooltips | **Cut.** Neither value exists anywhere. |
| Keybinds | rebind door, conflict count | **Cut.** No rebind screen and no keybind storage; the design lists that screen as still open, so this would be a door to a room and a number counting nothing. |
| Language | stepper | **Cut.** No localisation table — `UiString`'s key is described in its own header as "the seam a localisation table would key on later". A stepper with one entry is not a choice. |

Consequence worth seeing rather than reading: **the pane is half empty.**
Two cards in a 1600×804 pane leaves the bottom two thirds bare. That is the
honest state of the settings this game has, not a layout problem — but if the
answer is "add the settings", each cut row above is a small feature rather than
a UI job, and `battle speed` is the one with a consumer already waiting
(`SystemMenuController.Resume` restores the previous `timeScale` specifically so
a speed setting would survive the menu).

Also not carried over: the design's **gamepad prompt strip** for this pane.
Printing `LB / RB` prompts before any gamepad input is wired would be the same
failure as a control that stores nothing.

## Not built — content panes

1. **Main menu** — three exits: back to title, quit to desktop (saves first),
   abandon the descent. Abandon is framed in red behind a **1.2s hold** with the
   fill drawn in the button. Nothing here fires on a single press.
2. **Run statistics** — eighteen figures in three groups (Battle, The Fold,
   Spoils) under a floor/room, elapsed, days, turns header. No charts.
   **Every row binds to a tracked field or gets cut** — same rule the dossier's
   Dodge/Carried/Shop-price/Morale rows were held to.
3. **Floor map** — hosts the existing Run Map screen read-only, viewport
   narrowed by 320 and clipped 16px, keeping its own mossy ground. Its pinned
   header and gold HUD drop, because the lintel already carries all of it.

## Not built — polish the design left open

- **Open/close animation.** None. `ReckoningController.PlayIn` is the reference.
  The design's one ask: lintel and panel arrive together, and the pause takes
  effect on the animation's **first** frame, not its last.
- **Sound.** Open, close, tab change, slider tick — none wired. The Options
  sliders now change `GameSettings.SoundVolume` live, so a tick would have
  something to be audible against.
- **Rebind screen.** Options shows the door; the room does not exist.
- **Gamepad and keyboard navigation.** LB/RB to cycle tabs, A to activate, B to
  close, focus per pane restored on re-entry, and the focus ring
  (2px `#E7B25C8C` at 2px offset). None of this is built — only the mouse path
  is. The focus-ring *treatment* is specified, so this is wiring, not design.
- **Gamepad prompt strips** in each pane's bottom padding.

## Known gaps in what IS built

- **Label widths are authored, not measured.** `SystemMenuTabDef.LabelWidth`
  carries the design's measurements at 18px Chakra Petch 500 / `.14em`, and
  nothing re-checks them. Change a label without changing its width and the bar
  lays out around a lie — tabs slightly wrong, underline the wrong length, and
  it reads as sloppy spacing rather than stale data. The fix is a measure pass
  in the builder with a TMP text instance; deliberately not smuggled in with
  this change.
- **The overflow fallback is declared but not wired.** `SystemTabCharacterShort`
  ("CHARACTER") exists as a string; nothing swaps to it when the row overflows.
  Today the build simply refuses, which is the right failure but not the
  designed one.
- **UiAudit only ever sees the five-tab bar.** The scene on disk carries it; the
  three-tab layout is applied at runtime, so the emitted-tree audit cannot check
  it. `SystemMenuScreenTests` covers it against the arithmetic instead, and
  `SystemMenuTests` checks the absent tabs are switched off rather than merely
  unlisted. Worth knowing that this is a standing hole in the strongest check
  this project has.
- **The hub's own title ghosts through the scrim.** At `#0A0614ED` the hub's
  "DIVINE PRINCIPALITY" is still faintly legible behind the lintel — and since
  the lintel prints the same words, it doubles. The design resolved "scrim too
  thin" by darkening it and by giving the *bar* a plate; the lintel got no
  plate and sits over the brightest thing on the hub. Either the lintel needs
  its own plate or the title needs to be something other than the principality's
  name.
- **`Time.timeScale = 0` is the pause.** It is the whole mechanism. Nothing has
  been checked for coroutines that assume time advances, and the fight is the
  place that would bite.

## Known gaps in the Options pane

- **No keyboard or gamepad path.** `BarSlider` handles click and drag only.
  Deliberately not `UnityEngine.UI.Slider`: that brings a handle, fill rect,
  interactable and navigation model to do what twenty lines do, and what it
  would buy is focus handling that is not wired anywhere yet. When it is, that
  is the moment to reconsider.
- **Card headings are centred**, because `Ui.Label` centres and there is no
  left-aligned label helper. Reads slightly odd above left-aligned rows.
- **`Screen.SetResolution` is never exercised by a test.** `GameSettings.Apply`
  skips device application in batch mode on purpose — it once capped the
  headless runner at 60fps and tripled the PlayMode suite's runtime. So the
  tests prove which value is stored and surfaced, never the OS-level call. That
  last hop needs a real windowed build.

## Carried over from before this design pass

Unrelated to the menu, still true, recorded so they do not get lost:

- Pack **paging** on the dossier — 24 cells, 27 items, and the footer now reads
  "24 of 27" rather than implying a capacity that does not exist.
- Pack **filter tabs** are drawn and inert.
- **Attack reads 558 against Health 320** — the `DamageScale ×10` display
  question, open since the dossier landed.
- **Skills reads "0 known"** — only talent-granted skills are counted.
- `run.floor` is never incremented (recorded in `4dc9758`).
- The eight **container art prompts** in `docs/handoffs/dossier_containers/` are
  written and none of the art is generated.
