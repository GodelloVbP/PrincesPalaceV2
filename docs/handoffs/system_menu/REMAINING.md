# System menu — what is built, and what is not

Running note against the design pass in `README.md`. Kept here rather than in a
commit message because it outlives any one commit, and rather than in `AUDIT.md`
because none of it is a defect — it is scope that has not been reached yet.

Last updated 2026-08-19.

> The design pass names two prototype files, `System Menu.dc.html` and
> `support.js`, and **neither was ever dropped into this folder** — every other
> handoff under `docs/handoffs/` carries them. So where the design says the
> cards are authoritative for positions and colours, the README's own text is
> all there is. Worth knowing before treating a disagreement between the words
> and the build as a build error.

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
  `ESC` hint and a close X, plus the gold rule under it — **and its own opaque
  plate**. The design resolved "scrim too thin" by darkening it and by plating
  the *bar*; the lintel got neither, and it sits over the brightest thing on the
  hub, so "DIVINE PRINCIPALITY" ghosted through it and doubled the lintel's own
  words a few pixels apart. The plate ends that.

  Precisely, because it is not quite total: the lintel occupies screen y 34–86
  and the panel starts at 90, so a **4px band** between the gold rule and the
  panel's top edge still shows the scrim, and the hub title's 56px glyphs pass
  through it. At `#0A0614ED` that is roughly 7% of the letter, four pixels tall,
  with no second copy of the words over it. Closing it would mean overhanging
  the plate past the lintel it belongs to, which is a bigger change to the
  design's stated geometry than the residue is worth.
- **Tokens**: scrim `#0A0614ED`, panel `#1A1024F5` with a `BorderGold` rim, bar
  plate `#12091C` with a hairline bottom, underline `GoldLight`, dividers
  `Hairline`. The two hardcoded values are gone.
- **Hover is a plate, not a scale.** The 1.03 pop is deleted.
- **Letter-spacing exists now**, and the tab bar was wrong without it. Every
  `LabelWidth` was measured off a design prototype drawn at `.14em`, the emitter
  had no way to express letter-spacing at all, and nothing could see the
  mismatch: Domain has no font metrics by design, the EditMode suite is
  Domain-only, and `UiTextFitAudit` only asks whether text *overflows* its box —
  a label a quarter narrower than its box passes that happily. Measured,
  "CHARACTER & INVENTORY" drew **215.5px against the 272 the bar is laid out
  for**, so every box carried ~50px of air the design never put there and the
  selected underline (`label + 24`) overhung its own word by about 40px a side.
  `UiNode.Tracked()` is the new property, applied to the five tab labels and the
  lintel title and nowhere else; `SystemMenuLabelWidthTests` pins both the
  tracking and the widths, and pins that ordinary labels stay untracked.
- **Pause on open, resume on close**, restoring the previous `timeScale` rather
  than assuming 1, and also on `OnDisable` so a scene change cannot strand the
  game at zero.
- **Capacity guard is arithmetic**, not a count — a set with the same number of
  wordier labels is refused, which a count-based limit called fine.
- **Every pane fills the content area.** The dossier used to be hosted at its
  handover's own 1360×766, centred, which left 118px of dead margin down each
  side and all three of its columns stopping about 200px short of the floor — it
  read as a small screen sitting inside a big empty one. It is authored at
  1600×804 now, with the columns re-proportioned in the handover's own ratios
  (23.4% / 30%) and the loadout stage scaled as one unit.

  Worth recording why scaling alone could not do it: the authored panel is 1.775
  wide per 1 tall and the pane is 1.990, so scaling to fill the height leaves
  86px a side and scaling to fill the width overflows the height by 97. **There
  is no scale factor that fills this box.** What the handover's "never reflow"
  rule was protecting is kept — `FromStage` scales the sixteen slot and leader
  coordinates as one unit, so not one of them was re-authored.

  `SystemMenuPaneTests.EveryHostedPaneIsTheSizeOfTheContentArea` and
  `EveryPaneUsesMostOfItsHeight` hold all four panes to it. Nothing else could:
  a pane authored smaller than its box still solves, still passes containment,
  still passes overflow — it just sits in the middle of a box it does not fill.

**Four of the five panes have real content**: Character & Inventory (the
dossier), Options, Run statistics and Main menu. Only Floor map is still a
placeholder. The `Built` flag on each tab is no longer a claim
anybody has to keep true by hand: `SystemMenuPaneTests` checks that a tab
marked built hides its placeholder and hosts a screen, and that one not marked
built still shows it.

## Options — built, and four of its six groups cut

Layout `2a`, no scrolling, applies immediately, `RESTORE DEFAULTS` — but in
**one full-width column**, not the design's two.
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

**One column rather than the design's two, and that is what lets five settings
fill the pane.** Side by side, Audio's two rows and Display's three both stopped
less than halfway down, and no row height closes an 804px pane from a card three
rows deep without a control you could lose a hand in. Stacked at full width the
two cards and the restore button reach 694 of the 696 available, and a row with
its label at the left and its control at the right is the shape every settings
screen has rather than a compromise. `CardsFit()` is what keeps that true — at
two pixels of slack, the next group added fails the build rather than the eye.

What that does NOT fix is that there are five settings. Each cut row above is a
small feature rather than a UI job, and `battle speed` is the one with a consumer
already waiting (`SystemMenuController.Resume` restores the previous `timeScale`
specifically so a speed setting would survive the menu).

## Run statistics — built, seventeen figures and no header

Three cards side by side — Battle (8 rows), The Fold (4), Spoils (5) — no
charts, no scrolling. `RunStatRows` is the table; `RunStatsController.TryFigure`
is the binding.

The design asked for eighteen figures under a header of floor/room, elapsed,
days and turns. Seventeen survive and the header does not, and both are the
design's own rule doing its work:

| Asked for | Status |
|---|---|
| elapsed / days / turns | **Cut.** Nothing counts them. A run has a step and a floor and no clock at all. |
| floor / room header | **Cut.** Tracked, but the **lintel already prints it** two inches above the pane. A second copy is not more information; it is one more thing that can disagree. |

**The rule is mechanised rather than remembered.** `TryFigure` has no default
arm that invents a number, and `SystemMenuRunStatsTests` sweeps every key the
table declares through it — plus a non-vacuity case, so the sweep cannot pass by
the binding answering everything. A row that stops binding fails a test rather
than printing a plausible figure, which is exactly what the dossier's
Dodge/Carried/Shop-price/Morale rows did before they were cut.

Not carried over: a **per-character breakdown**. The ledger is keyed by
character and could support one, but six rows by four heroes is a spreadsheet,
and the dossier is where one hero's numbers belong. This pane answers "how did
the descent go".

## Main menu — built, three exits, none on a single press

Two exits above a rule, and abandon below it behind a 1.2s hold with the fill
drawn in the button. Red framing on abandon, from the design's destructive
tokens.

**All three end the descent**, which is a deliberate departure from the design's
"back to title (run stays as it is)". `RunManager` states the opposite as a rule
with money attached — leaving a descent kills the run, and `EndRun` is what pays
out the embers its bosses earned. The hub's title button and the main menu's
quit both enforce it, and the first thing the game does at startup is settle any
run it finds. A third title door that parked a run would leave the player at the
title with a live descent in the save. Each button carries a line saying what it
does to the run.

**Abandon is absent between descents**, rather than greyed — the design's own
tab rule applied one level down. **And the two exits move when it goes**: the
scene is authored in the three-piece layout and the pane re-centres the pair on
its own when there is no card under them. A fixed layout has to be wrong in one
of the two contexts, and the one it would be wrong in is the hub — where this
pane is the only way out of the game, and two buttons in the top third of an
empty panel is what that would look like. Same shape as the tab bar's two modes,
with the arithmetic still in one place (`ExitsLayout`).

Two things fell out of building it:

- `Navigation.Quit()` now exists, with a `QuitOverride` seam, and
  `MainMenuController` was moved onto it. Two copies of `Application.Quit()` is
  the scattering `Navigation` was written to stop — and without the seam, the
  first test to reach a quit button would take the batch-mode runner down with
  it and report it as a crash.
- `HoldToConfirm` is a component rather than four lines in the controller,
  because it has to count **unscaled** time. The menu's whole pause mechanism is
  `Time.timeScale = 0`, so a hold on `Time.deltaTime` there never advances: the
  fill sits at zero, the button never fires, and nothing says why. That failure
  is invisible in any scene that is not paused, which is every other scene that
  might reuse it later.

## Not built — content panes

1. **Floor map** — hosts the existing Run Map screen read-only, viewport
   narrowed by 320 and clipped 16px, keeping its own mossy ground. Its pinned
   header and gold HUD drop, because the lintel already carries all of it. The
   last placeholder pane, and the largest of the three that were: `MapScreen` is
   a full screen with its own controller and node buttons wired to
   `MapController`, so hosting it read-only in three other scenes is a real
   piece of work rather than a `Build()` call.

   Until it lands, `SystemMenuTabs.DefaultFor` keeps its fallback: opening the
   menu mid-run should land on Floor map by the design's rule, and lands on
   Character & Inventory instead so that no Escape opens onto "CONTENT TO COME".

## Not built — polish the design left open

- **Open/close animation.** None. `ReckoningController.PlayIn` is the reference.
  The design's one ask: lintel and panel arrive together, and the pause takes
  effect on the animation's **first** frame, not its last.
- **Sound.** Open, close, tab change, slider tick — none wired. The abandon hold
  is deliberately `Quiet()`, because a `Button` fires its click on release
  whatever the press was for, and a click sound on a hold released early says
  something happened.
- **Rebind screen.** Options shows the door; the room does not exist.
- **Gamepad and keyboard navigation.** LB/RB to cycle tabs, A to activate, B to
  close, focus per pane restored on re-entry, and the focus ring
  (2px `#E7B25C8C` at 2px offset). None of this is built — only the mouse path
  is. The focus-ring *treatment* is specified, so this is wiring, not design.
  The Main menu pane raises the stakes on it slightly: **the abandon hold is
  pointer-only**, so a pad player has no way to reach it at all.
- **Gamepad prompt strips** in each pane's bottom padding.

## Known gaps in what IS built

- **Label widths are still authored rather than measured, but they are now
  checked.** `SystemMenuTabDef.LabelWidth` carries the design's measurements at
  18px Chakra Petch 500 / `.14em`, and `SystemMenuLabelWidthTests` holds each
  one against what TMP actually draws in the built scene, within 8px. A label
  changed without its width now fails a test instead of laying the bar out
  around a lie. A build-time measure pass would still be better — it would
  remove the table rather than police it — and is still not done.
- **The overflow fallback is declared but not wired.** `SystemTabCharacterShort`
  ("CHARACTER") exists as a string; nothing swaps to it when the row overflows.
  Today the build simply refuses, which is the right failure but not the
  designed one.
- **UiAudit only ever sees one of each two-mode layout.** The scene on disk
  carries the five-tab bar and the three-piece Main menu pane; the three-tab bar
  and the two-exit pane are applied at runtime, so the emitted-tree audit cannot
  see either. `SystemMenuScreenTests` and `SystemMenuPaneTests` cover them
  against the arithmetic instead, and the PlayMode suites check that what is
  supposed to be absent is switched off rather than merely unlisted. Worth
  knowing that this is a standing hole in the strongest check this project has,
  and that it has now widened from one pane to two.
- **`Time.timeScale = 0` is the pause.** It is the whole mechanism. Nothing has
  been checked for coroutines that assume time advances, and the fight is the
  place that would bite. Everything the menu's own panes count — the hold, the
  arming clock — is on unscaled time for exactly this reason.
- **Column A of the dossier carries its space in one place.** The portrait,
  name and XP block sits at the top and the two nav rows are a footer at the
  bottom, with about 180px between them. The portrait cannot absorb it: the
  Image carries `preserveAspect`, so a box taller than the art's own aspect
  letterboxes rather than fills. It reads as two groups that mean different
  things — who this is, and what you can open — rather than as the column
  stopping early, which is what it did before.

## Known gaps in the Options pane

- **No keyboard or gamepad path.** `BarSlider` handles click and drag only.
  Deliberately not `UnityEngine.UI.Slider`: that brings a handle, fill rect,
  interactable and navigation model to do what twenty lines do, and what it
  would buy is focus handling that is not wired anywhere yet. When it is, that
  is the moment to reconsider.
- **`Ui.Label` centres and there is no left-aligned helper**, so left alignment
  is faked by putting the text in a snug box at the left edge — which is what
  the Options card headings and the Run statistics name columns now do, and what
  the dossier's stat table always did. It works, and it means every "left
  aligned" label in this menu is really a centred one in a box that happens to
  fit. A real alignment flag on `UiNode` would remove the trick; nothing needs
  it badly enough yet.
- **`Screen.SetResolution` is never exercised by a test.** `GameSettings.Apply`
  skips device application in batch mode on purpose — it once capped the
  headless runner at 60fps and tripled the PlayMode suite's runtime. So the
  tests prove which value is stored and surfaced, never the OS-level call. That
  last hop needs a real windowed build. **`Application.Quit` is now in the same
  position**: the seam proves the button reaches it, and nothing proves the
  process actually goes away.

## Carried over from before this design pass

Unrelated to the menu, still true, recorded so they do not get lost:

- Pack **paging** on the dossier — 24 cells, 27 items, and the footer now reads
  "24 of 27" rather than implying a capacity that does not exist.
- Pack **filter tabs** are drawn and inert.
- **Attack reads 558 against Health 320** — the `DamageScale ×10` display
  question, open since the dossier landed.
- **Skills reads "0 known"** — only talent-granted skills are counted.
- The eight **container art prompts** in `docs/handoffs/dossier_containers/` are
  written and none of the art is generated.
