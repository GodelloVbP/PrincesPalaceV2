# Gamepad Navigation — Plan v3

> ## Status: 2026-09-17. Phase 3 ROLLOUT COMPLETE -- items 1-4 landed
>
> `ccb1b08d` (item 1, `AUDIT.md` #158 closed), `a61fc412` (item 2, the
> shoulder shortcut), `c16b1dc3` (item 3, Party's Send-to-bench/Cancel
> links), `7ea33bca` (item 4, `AUDIT.md` #161 closed). Every screen this
> plan's own inventory named for phase 3 now has both a build-time control
> inventory and a runtime behavioural proof (section 9's own "the build
> proves the inventory, runtime proves operability" split) -- the sizing
> assessment in the status block below this one, written before this pass
> started, is superseded by what actually shipped, item by item.
>
> **Item 1** (`ccb1b08d`): the three genuinely unwired Hub modals (AUDIT
> #158's corrected count) each push their own `NavContext` now, rebuilt at
> the end of every repaint rather than once at open -- `RelicDraftController.
> RefreshNavigation` (the offer cards as a wrapping Rail, Descend an
> explicit Down/Up pair since a Rail alone has nothing below it),
> `GlossaryController.RefreshNavigation` (two vertical Lists side by side --
> the category rail and the row list both read as columns off
> `RailX`/`RailTop`/`RailPitch`, not one of them mis-declared a Rail because
> the audit's own prose called it "the rail"), `DebugMenuController.
> RefreshNavigation` (currency/filter Rails, the row List, the pager Rail,
> chained top to bottom by explicit links). `HubController.HandleEscape` is
> simplified to just `SystemMenuController.OpenOnCancel(systemMenu)`: each
> modal's own context now sits above the hub's while open, so the
> dispatcher never reaches the hub's handler at all until nothing else is
> up. **Deviation, stated rather than silently followed**: the brief said
> "Cancel closes them" for all three; RelicDraft's Cancel is a deliberate
> no-op instead (`cancel: null`), matching the screen's own already-stated
> design ("a draft you can navigate around is not a draft") and
> `AUDIT.md` #158's own investigation before this pass -- making it close
> would let a player leave without `RunOrchestrator.FinishDraft` ever
> running, re-offering the same draft on the next hub visit. One
> pre-existing test encoded the removed branch and was adapted rather than
> left red: `SystemMenuTests.CancelOverTheGlossaryClosesItInstead_...` is
> now `HandleEscapeNoLongerKnowsAboutTheGlossary_ItOnlyEverOpensTheMenu`,
> pointed at the narrower claim that survives (`HandleEscape` no longer
> touches the glossary at all); the real "does Cancel close the glossary in
> play" claim moved to `GlossaryGamepadNavigationTests`, driven through the
> real dispatcher.
>
> **Item 2** (`a61fc412`): the SystemMenu tab strip's shoulder shortcut.
> `ProjectSettings/InputManager.asset` gains `TabPrev` (Q / joystick button
> 4) and `TabNext` (E / joystick button 5), read once a frame by
> `NavigationInputModule` through `input` (never `UnityEngine.Input`
> directly) and offered to `topAtStart` the same way Cancel is. A context
> opts in through a new `Domain/UiKit/INavTabStrip.cs` interface, reached
> via `NavContext`'s own optional `tabStrip` provider -- the identical
> "ask at press time, not once" shape `INavCancelClaim`'s own `_claimant`
> already uses, so a context built without one (every context but
> SystemMenu's) answers null and the press is silently absorbed.
> `SystemMenuController.StepTab` is the one implementor: it steps
> `_visible` in SLOT space rather than `_selected` by +-1 (a raw index step
> would walk a hidden run-only tab on a three- or four-tab context),
> through the SAME `Select(index)` each tab's own `onClick` calls, and
> lands selection INSIDE the new pane's own entry (`ActivePaneEntry`,
> falling back to the tab button when the pane has nothing selectable) --
> one press is a shortcut for the Move-to-tab + Submit + Move-down dance
> those three inputs would otherwise take, not merely a faster way to reach
> the tab button. The strip itself stays an ordinary wrapping Rail
> alongside it, untouched.
>
> **Item 3** (`c16b1dc3`): Party's Send-to-bench and Cancel links, the
> phase 2 gap this plan's own status header named. `cancelLink`/`benchLink`
> join a third Rail, `partyLinks`, filtered every repaint to whichever of
> the two is actually shown (`PaintHeader` already hides them outright on
> the right conditions); reachable from the seat rail by an explicit Up,
> and from the roster in the same two hops a Move up the screen already
> takes (the roster's own existing Up into the seat row, unchanged).
> Benching mid-carry needed no new dispatch code -- `benchLink.onClick`
> already called `SendToBench`, so becoming reachable and reaching Submit
> was the whole fix. Structural move, not new mechanism:
> `PartyController.WireNavigation`'s own `RuntimeNavWiring.Apply` call
> (previously run once from `Wire()`) is now `RefreshNavigation`, called at
> the end of every `Paint()` alongside the seat/roster rails in the SAME
> `Apply` call -- `RuntimeNavWiring.Apply` is authoritative per node, so a
> second, later call adding only the new Up link would have erased those
> rails' own Left/Right the instant a seat also gained it.
>
> **Item 4** (`7ea33bca`, `AUDIT.md` #161 closed): the dossier's spell-books
> panel, wired like the pack. `RefreshNavigation`'s two-way branch
> (`IsPackShown`/else) becomes three-way (`IsPackShown` / `IsSpellsShown` /
> else); `DeclareSpells` mirrors `DeclarePack` -- the three spell slots and
> the unassigned-book rows as two Lists (one column, not a Grid: the panel
> draws both bands in a single vertical stack), Close at the top and
> reachable for the identical reason DeclarePack's own Close is (this pane
> does not claim Cancel either). `RefreshSpells` now calls
> `RefreshNavigation()` at its own end -- it never had before, which is
> AUDIT #161's literal bug: the panel's opening/closing never touched the
> graph at all. **Deviation, found while wiring**: neither `ShowPack` nor
> the old `ShowSpells` ever explicitly reselected on open/close, and the
> dispatcher's own next-frame rule cannot cover it here --
> `SystemMenuController.RefreshSelectables` declares every Selectable under
> the whole panel regardless of visibility, so a row hidden behind a panel
> it just opened still reads as "declared" and the rule stays silent.
> `ShowSpells` now explicitly selects the panel's own entry on open and
> `SpellsRow` on close; `ShowPack`'s identical, older gap is left alone --
> untested today, and fixing an unrequested control is a separate change.
> The brief's "Cancel or Close returns to the row" is resolved against the
> pack's own already-proven precedent: this pane does not claim Cancel, so
> Cancel closes the whole menu (matching `Cancel_FromInsideTheDossier_
> ClosesTheMenu`, proven project-wide for the dossier already); it is Close
> specifically that returns to the row, and both halves are pinned rather
> than only the one the phrase's ambiguity could have hidden.
>
> **Every gate green except the two pre-existing AUDIT.md #157 failures**,
> reproduced identically before this pass touched anything (job 0's own
> A/B, unchanged). No screen tree changed in items 1, 3 or 4; item 2's
> `ProjectSettings/InputManager.asset` change triggered a `-BuildScenes`
> run whose regenerated scenes differed from main only by reassigned
> `fileID`s (insertions == deletions exactly in every one) and were
> reverted rather than committed.


> ## Status: 2026-09-17. Phase 3b items 1 and 2 landed, plus the tooltip mechanism both needed
>
> `914c249e` (job 1, the mechanism, and item 1, CharacterDossier) and
> `9c26de94` (item 2, the Reckoning). Items 3, 5 and 6 remain not
> attempted -- the block below has the sizing assessment for each, and
> nothing in it has changed.
>
> **Job 1, tooltips on focus (section 7's "Tooltips" contract).** Two
> screens showed a tooltip on pointer hover only, and a stick has no
> cursor. The shape deliberately NOT taken was a second show/hide path per
> screen driven by selection: two callers with no arbiter disagree the
> moment both a hover and a selection exist, which is the ordinary state of
> a player who touches the mouse once and goes back to the pad -- the
> mouse's "exited" would close a box the stick is holding open, and each
> screen would grow its own copy of the arbitration. So the precedence is
> decided once, in `Domain/UiKit/TooltipFocus.cs` (engine-free, one test
> per rule), each screen keeps ONE show/hide implementation, and selection
> is a second caller of it through `Core/TooltipFocusRouter.cs` (the
> node -> delegate map, the `SelectIndex`, the force-close; it decides no
> rule). `NavContextStack` gained a `Changed` event so a modal pushed above
> force-closes and a popped context hides -- ONE comparison against "what
> was Top when this box opened", which is also the only form the dossier
> could use, its context belonging to `SystemMenuController`. An event,
> never a poll: gating readers on a per-frame question is Draft 2's
> rejected approach (section 1).
>
> `TooltipPlacement.Beside` never lands on its own subject now. It used to
> clamp a box with room on neither side back inside the panel and accept
> covering the anchor as "the lesser evil" -- defensible for a hover (move
> the mouse and it goes), wrong for focus, where nothing moves it. Beside,
> flipped, then UNDER or OVER the anchor, and only then
> clamped-and-overlapping as a stated last resort neither shipped screen
> can reach. Every case that fits beside is bit-identical to before, which
> is why the three existing cases and both real-geometry walks are
> unchanged. It takes the anchor's HEIGHT now (a box cannot be placed clear
> of a rect whose height it was never told); all three call sites pass it.
>
> **Deviation, stated rather than buried:** the brief said "compute against
> the canvas rect". The placement still computes against each screen's own
> painted interior (the dossier pane's `HalfWidth`/`HalfHeight`, the
> Reckoning's `ContentHalfWidth`/`ContentTop`/`ContentBottom`), both
> strictly inside the canvas. Using the canvas would let a tooltip hang off
> the panel it belongs to and onto the scene behind it, which is what those
> insets exist to prevent -- and what `UiAudit`'s containment check would
> then have to be told to allow.
>
> **Item 1, CharacterDossier.** Four groups -- the loadout's two DRAWN files
> (not `EquipmentSlots.All`'s declaration order, which interleaves them),
> the ability scores as a three-wide Grid, the pack window as a two-wide
> Grid, its sort tabs as a Rail -- plus explicit inter-group links, rewired
> on every repaint. Two states, not one declaration with holes:
> `DossierPackPanel` is an opaque Image over the whole of column A, so
> while it is up the mouse cannot reach the nav rows underneath and neither
> should a Move (Shop's shelf/pack reconfigure, same reason). Four
> additions the brief did not name and the screen needs: the column-A nav
> rows and the roster pager (without them the pack group is unreachable at
> all -- `packRow` is its only door -- and a pad could not change
> character); the pack's own Close button (this pane does not claim Cancel,
> so Close is the only way out, exactly as for the mouse);
> `Core/INavPaneEntry` so the Character tab's Down lands on the first
> equipment slot rather than on the roster pager, which is what "the first
> active Selectable in tree order" resolved to; and a re-call of
> `SystemMenuController.RefreshNavLinks` after the panel is shown --
> `ApplyContext`'s own call runs while the panel is still hidden and
> `ActivePaneEntry` resolves through `GetComponentInChildren`, so the
> selected tab's Down-into-its-pane link was silently null on EVERY first
> open (pre-existing, found only because a pane finally declared an entry
> worth reaching). The Skills row is deliberately left out of the graph: it
> carries no `onClick`, so a Move onto it would be a Submit that does
> nothing.
>
> The two files are paired by DRAWN ROW, off `DossierLayout.SlotTop` -- the
> same table `SlotAt` and `LeaderAt` read. Pairing by list position was the
> first version and a test caught it: the left file has two slots above the
> right file's first entry, so Torso paired with Shoes, three body rows
> down.
>
> Both Grid groups are declared CLAMPED against section 12.3's
> wrap-a-Grid-row default, as one rule rather than two special cases: a
> group that hands off sideways to its neighbour cannot also wrap sideways,
> or Left out of column 0 would have to mean two things. A group with
> nowhere to go still wraps (the Reckoning's offer Rail does).
>
> **Item 2, the Reckoning.** One context, two states, pushed in `Show()`
> once `PaintOffers` has settled which cards exist and removed on
> `OnDisable` (Defeat's argument, same reason: Continue ends in a scene
> change). The offers are a wrapping Rail of the cards actually up; Submit
> needs no code, a card being a real Button whose `OnSubmit` fires the
> `onClick` that already calls `Take(index)`, with `Take`'s own `_taken`
> guard making it once-only whichever input arrives. The SUMMARY is wired
> too, which the brief did not ask for and the screen cannot do without:
> once an offer is taken every card goes non-interactable, so leaving the
> declaration on the offers would strand a pad with nothing that answers
> Submit and Continue unreachable. Cancel is per phase, checked against the
> mouse path rather than assumed: a documented no-op while the choice
> stands (`Show()` opens on the offers with no Continue and no skip), and
> `Dismissed` on the summary, which is exactly what Continue's click
> raises.
>
> Found by the tests rather than by reading, and worth recording because
> phase 3a hit it too (`d39d955b`): a context's entry must be the
> GameObject, never the Button. `NavContext` holds an opaque handle and
> reads it back through `as GameObject`, so a `Selectable` resolves to null
> and the dispatcher then clears the selection every frame.
>
> **The captures** (item 1's own gate: "verified by a runtime capture, not
> just asserted") are in `tools/screenshots/gamepad_phase3/` -- four
> dossier shots, one per corner cell of the pack window, and one Reckoning
> shot with the middle card selected. Every one asserts NO OVERLAP between
> the placed box and its own subject in WORLD space inside the capture test
> itself, so the assertion is the gate and the picture is for the owner.
> Two things the pictures show that the assertions do not cover, both
> filed rather than waved off: the box clears its own subject but not its
> SIBLINGS (it covers the pack's other column, the pack's Close button, and
> the Reckoning's third card), and nothing in any shot says which control
> has focus -- `AUDIT.md` #160, which is section 12.2's open owner call
> arriving on two more screens. `AUDIT.md` #161 records the dossier's
> spell-books panel as the one column-A state still mouse-only.

> **Status: 2026-09-17. Phase 3b: job 0 plus items 4 and 7 landed. Items 1,
> 2, 3, 5 and 6 NOT attempted this pass** -- a scope assessment below, not a
> partial or broken attempt at any of them.
>
> Job 0 (`ccdfe2f7`): `AUDIT.md` #157's two Combat/Stage failures
> (`FightPlayableTests`, `StageFormationTests`) confirmed to predate this
> whole branch -- `git checkout f20d76de` (the branch's own base, one commit
> before phase 3a's first) reproduces both failures verbatim, which rules
> out phase 3a's own "most likely trigger" (Main Menu's incidental
> `build_content.ps1` run: `f20d76de` predates every content rebuild this
> plan has done, phase 2's included). Left unfixed, out of scope for this
> plan; `AUDIT.md` #157 carries the evidence for whoever chases the actual
> regression in `combat`.
>
> Item 7 (`232f310b`): Main Menu's Manage Saves list and its own
> reset-confirm dialog (`AUDIT.md` #159, closed) join the ONE shared
> `NavContext` `MainMenuController` already owned for the base menu/save-slot
> toggle -- four states now (confirm dialog, manage list, save-slot list,
> base menu, resolved topmost-first since `ResetConfirmPanel` is a SIBLING
> drawn over `ManageSavesPanel`, not a child of it), `HandleCancel` mirrors
> the same precedence through `ResetProgressController.Dismiss()`/`GoBack()`
> (made `internal`, the identical cross-controller shape `RefreshContinue`
> already used). The confirm dialog's entry is forced to No rather than the
> Rail's own first member: `HoldToConfirm` (the delete button's hold gesture)
> is pointer-only -- `IPointerDown/Up/ExitHandler`, no `ISubmitHandler` --
> so a stray gamepad Submit on Yes is inert either way, but resting the
> selection highlight on the destructive button the instant the dialog opens
> is still the wrong default. Caught and fixed while writing the tests: the
> `OpenSaveSlotPanel` test helper never called `LoadMenu()`, which happened
> to pass whenever an earlier test left a compatible scene loaded and masked
> what looked at first like a real bug in the cross-group Down/Up link (a
> throwaway diagnostic test against the pre-existing, working
> `WireSaveSlotNavigation` proved the link was never the problem -- the
> reused `NavigationInputModule`'s own carried-over move-debounce state was).
>
> Item 4 (`a1b7e7c7`): Defeat is a standalone modal inside Fight (not a
> SystemMenu pane), so it gets its own `NavContext` -- pushed in `Show()`
> once `Wire()` has built the buttons, popped on `OnDisable` rather than
> from either button's own handler, since both `Dismissed` and
> `InspectRequested` end in a scene change and a pop hung on the click would
> double-pop if something else deactivated the GameObject first. Inspect
> then Return is a Rail (`DefeatScreen`'s own `Place.At` puts Inspect on the
> left), Cancel raises `Dismissed` the same as Return -- there is no
> back-out affordance on the mouse path either. Exits is a pane inside
> `SystemMenuController`, which already declares every Selectable under
> whichever pane is active as part of its own shared context -- what was
> missing was an actual Explicit-navigation List (Title, Quit, Abandon only
> when a descent exists), rebuilt every `ApplyContext()` call. No Cancel
> claim: the mouse path has no way to back out of an armed exit either.
> RunStats needed NO CODE CHANGE at all -- checked against the real screen
> before writing anything (the same "challenge the brief" correction phase
> 3a's Hub and Talent commits already made): `RunStatsScreen` declares zero
> Buttons or Selectables, every row is a read-only `TMP_Text`, so "entry on
> the first" has nothing to apply to.
>
> **Items 1, 2, 3, 5 and 6 not attempted IN THAT PASS, by scope assessment
> rather than failure** -- items 1 and 2 have since landed (`914c249e`,
> `9c26de94`; the status block at the top of this file), and the sizing
> below is what that pass was scoped from. Each is comparable in size to one of phase 3a's five screens on
> its own -- this plan's own convention is one screen, one session, one
> gated commit -- and several need net-new mechanism, not just wiring:
> - **Item 1 (CharacterDossier)** needs a focus-driven tooltip (no cursor to
>   anchor to) that must also out-rank hover and force-close on a modal
>   open, PLUS a runtime screenshot capture pass at up to four aspects to
>   verify the tooltip never covers the selected item -- a distinct, slow
>   verification step on top of the wiring itself.
> - **Item 2 (Reckoning)** has the identical focus-driven tooltip problem as
>   item 1, on a different screen (`OnOfferHover`'s `entered=true` path).
> - **Item 3 (the four Hub-covering modals, `AUDIT.md` #158)**: investigated
>   this pass, and the count is wrong -- `characterOverlayPanel` IS
>   `SystemMenu.Root` (`HubController.WireCharacterOverlay`'s own comment
>   confirms it), so the "character overlay" #158 names already has a
>   `NavContext` via `SystemMenuController` (phase 2 step B). Only
>   RelicDraft, Glossary and the Debug menu are genuinely unwired, and each
>   is non-trivial on its own (Glossary: category rail + paged row list +
>   detail pane; Debug menu: grant buttons + filter rail + paged row list;
>   RelicDraft: card rail + a dormant pager, and Cancel is a deliberate
>   no-op by the screen's own design -- "a draft you can navigate around is
>   not a draft"). Wiring them also means simplifying
>   `HubController.HandleEscape`, whose debug-menu/glossary-closing branches
>   become dead code once each modal owns its own Cancel-closing context --
>   left alone this pass rather than half-refactored. `AUDIT.md` #158 is
>   updated with this narrower count, not closed.
> - **Item 5 (SystemMenu shoulder shortcut)** needs a new input axis pair in
>   `ProjectSettings/InputManager.asset` and a new "read once per frame
>   through the module" plumbing path in `NavigationInputModule` -- untouched
>   surface, deserving its own gated pass rather than a bolt-on alongside
>   four other items.
> - **Item 6 (Party's Send-to-bench link)** touches `PartyController`, a
>   phase-2-shipped file with its own carry-state machine; phase 2's own
>   status header above already flagged this as a known gap, not something
>   to rush alongside four other items in one sitting.
>
> Recommendation: each of items 1/2/3/5/6 as its own follow-up session, the
> same gated-commit shape this plan already uses.

> **Status: approved 2026-09-17. PHASE 3a COMPLETE** (the first half of
> phase 3's rollout -- five screens, one commit each, gated green before the
> next). Phase 1 landed (`81f1de14`). Phase 2: step A `00bae36d` (build-time
> nav declarations -- since deleted, see step E), step B `c8837374`
> (SystemMenu nested modal + Options row adjustment), step C `da205520`
> (RewardTrack ribbon as a Rail), step E `6c3eb085` (ONE navigation model:
> the build-time declaration path deleted, `UiNavLinkBuilder` made generic,
> `RuntimeNavWiring` reduced to a thin adapter -- section 9 has the decision
> and its evidence), step F `4eab048b` (Cancel opens the system menu on the
> map and in the fight again, `AUDIT.md` #155), step D `467770ef` (Party:
> seats/cards as navigable Rails, selection-driven carrying, a pane's first
> refusal on Cancel, and the visual acceptance capture, `AUDIT.md` #156).
>
> **Two things phase 2 left for the owner, both found rather than assumed:**
> (1) the Party capture shows THREE states, not four -- Party's slots have no
> mouse-hover treatment at all (no `Hovers()`, no `HoverIndex`, no
> `ThemedButtonState`), so "can hover be told from selection" cannot be
> answered there until a hover channel exists (section 8/12.2);
> (2) Party's Send-to-bench link is not in a nav group yet, so benching
> mid-carry is mouse-only -- still open, not touched by phase 3a.
>
> **Phase 3a: five screens, five commits, each gated on `tools/test.ps1`
> (new classes, then the screen's existing area) and
> `tools/run_tests_parallel.ps1 -BuildScenes` before the next.**
> `ec81b29f` (Hub -- corrected the brief's own "4-tab hub bar" against the
> real screen, which is four staged buildings and a gate; a 2x2 Grid keyed to
> `HubAnchors`' own depth/lateral signs, plus a `SelectHaloPainter` helper for
> the four un-themed buildings and the gate), `d39d955b` (Main Menu and its
> save slots -- one `NavContext` reconfigured between the base menu and the
> save-slot modal; found and fixed a real bug, `NavContext.Entry` read back
> through `as GameObject` silently failing when handed a `Button` instead of
> its `.gameObject`), `7a16ce67` (Map as a Graph -- `UiNavSpec` has no Graph
> kind, so this is explicit links only off `RunManager.Choices()`, rewired
> every `Refresh()`), `49b4ab49` (Talent's orbs as a tree -- corrected the
> brief's own "Grid with row length" against `TalentSkeleton`'s real shape, a
> row of 1 then rows of 3; small per-tier Rail groups plus explicit
> parent/child links derived once from the skeleton), `46d9daec` (Shop, a
> modal context reconfigured between the shelf and the pack, Gear alone is
> the real 2-wide grid). `AUDIT.md` #157-#159 name what phase 3a found and
> deliberately left: two pre-existing Combat/Stage test failures verified
> unrelated (A/B'd via `git stash`), four Hub-covering modals that still do
> not push their own context, and Main Menu's Manage Saves/reset-confirm
> flow left mouse-only.
>
> **Deviations from this document's own screen briefs, all forced by the
> real screen rather than chosen:** Hub has no tab bar (section 12's own
> "hub bar" framing does not describe `HubScreen`'s actual four-buildings-
> and-a-gate composition); Talent's orbs are a tree, not a fixed-width Grid
> (`TalentSkeleton`'s row-of-1-then-rows-of-3 shape cannot be expressed by
> `row = i / cols`); Main Menu's Cancel closes the save-slot modal when it is
> open rather than being a no-op everywhere (a stick user with no mouse would
> otherwise be unable to back out of a panel they could open). Each is
> explained in its own commit message and in `AUDIT.md` where it leaves
> something open.
>
> Research base: `docs/GAMEPAD_NAVIGATION_RESEARCH.md`. Draft 1 (screen
> inventory) and Draft 2 (the rejected gating-on-"am I top" approach this
> draft replaces, section 1) are not carried into the repo — this file and
> the research base are the two documents kept.
>
> Two orchestrator resolutions, settled before phase 1 landed:
>
> 1. **The dispatcher owns stick, Submit and Cancel only.** The C/I/F1
>    keyboard-shortcut polls Draft 2's own section 2 inventoried
>    (`ToggleCharacterSheet`'s `C`/`I`, `SystemMenuController`'s `F1`, and
>    the rest of that list) stay exactly where they are — raw `Update()`
>    polls, outside this dispatcher — through phase 1. They get gated on
>    `Stack.IsTop(myContext)` in phase 2, alongside the screens/contexts they
>    each belong to, rather than folded into this dispatcher now: none of
>    them are stick/Submit/Cancel, so none of them are what section 1's
>    double-drive bug was ever about, and gating them correctly needs the
>    real per-screen contexts phase 2 builds, not the fixture this phase
>    proves the mechanism on.
> 2. **The Fight-to-modal transition needs no mirror of test 1.** Section 3
>    notes the reverse direction of test 1's transition (Fight top, ending
>    combat pushes Reckoning/Defeat) "shares this mechanism but never
>    exercises it same-call, since `_isBusy` already defers it." Phase 1
>    covers that hazard with test 2 instead (same-call ordering against a
>    Fight stub built to select synchronously on purpose) rather than a
>    second transition test, because a mirror of test 1 against the real
>    Fight-to-Reckoning path would exercise `_isBusy`'s deferral, not the
>    dispatcher's own same-frame ordering — the thing actually still open
>    per section 3's "not live today, verified" note.

## 1. Intent

Every screen operable on stick + Submit + Cancel alone. Frozen: legacy
Input Manager, every screen's mouse/click behaviour, and Fight's focus
**model** (`MenuDepth`, `MoveFocus`, `ConfirmFocus`, `WrapFromNoHover`, and
the tests that drive them directly). **Revised freeze, stated plainly**:
Fight's raw `Update()` poll (`FightController.Input.cs:1204-1221`) is not
part of that model — it is the double-drive source Draft 2 tried to gate
around and failed to. It is deleted; the same debounce logic moves into
the dispatcher's Fight branch (§3). Done when: one ordered dispatch point
decides every frame's input, provably (by a test through the real
dispatcher, not a direct handler call); a Cancel that closes a modal
cannot also reach the context exposed underneath it in the same frame,
proven for both transition directions; `UiAudit`'s structural claim is
kept separate from what only a runtime test can prove.

Draft 2 was rejected because gating readers on "am I top of stack" doesn't
stop two readers from each independently, correctly, seeing themselves as
authorized in the same frame (Options closes, Fight becomes top, Fight's
own `Update()` still runs later that frame). The fix in this draft is
structural, not another gate: one method, called once per frame by Unity
itself, is the only place any of this is read.

## 2. Verification

Carried over from Draft 2, unchanged and still load-bearing: (a) Options'
stepper claim is false — only Stepper-kind rows have buttons
(`OptionRows.cs:5-13`, `OptionsScreen.cs:190-247`); (b) Shop has both
eligibility shapes (`ShopController.cs:553` disables sell, `:207-244`
refuses-on-press for buy); Party never disables (`PartyController.cs:765-768`);
Talent/RewardTrack hide (`TalentController.cs:792-819`,
`RewardTrackController.cs:507`); Dossier has no content-hiding tabs or
scroll; exactly two scroll surfaces exist project-wide
(`ListScroll.cs`/`RailScroll.cs`); five screens are separate scenes, the
rest toggle via `Open()` with content populated after activation
(`ShopController.cs:103-117`); modals are scene siblings
(`HubController.cs:146-149`); pause doesn't stall repeat
(`Time.unscaledTime`, `StandaloneInputModule.cs:513,527,533`).

**New verification for this draft — the dispatcher seam is real:**
- `BaseInputModule.Process()` is `public abstract void Process();`
  (`BaseInputModule.cs:125`); `StandaloneInputModule : PointerInputModule`
  is not sealed, and its own `public override void Process()`
  (`StandaloneInputModule.cs:294`) is not sealed either — a project
  subclass can override it again.
- `BaseInputModule` already has exactly the injectable seam claimed:
  `protected BaseInput m_InputOverride`, a public `input` property
  returning `m_InputOverride` when set (else a lazily-created default),
  and a public `inputOverride` setter (`BaseInputModule.cs:58,64-90,98-102`).
- **Every** value `StandaloneInputModule` reads goes through that `input`
  property, never the static `UnityEngine.Input` class directly:
  `input.mousePosition` (`:176`), `input.GetButtonDown`/`GetAxisRaw` in
  `ShouldActivateModule` (`:253-258`), `input.GetButtonDown` in
  `SendSubmitEventToSelectedObject` (`:476,479`), `input.GetAxisRaw`/
  `GetButtonDown` in `GetRawMoveVector` (`:487,490,497`). `BaseInput`
  itself (`BaseInput.cs`, full file read) declares every one of these
  (`GetAxisRaw`, `GetButtonDown`, `mousePosition`, `GetMouseButtonDown`,
  `touchCount`, `GetTouch`, `mousePresent`) as `virtual`, each defaulting
  to the matching `UnityEngine.Input` call — a test subclass can override
  all of them and feed the real module scripted values.
- **Repeat timing is not part of that seam.** `Time.unscaledTime`
  (`:513,527,533`) is a direct static call inside `StandaloneInputModule`,
  with no `BaseInput` equivalent — a scripted `BaseInput` cannot fake it.
  A repeat-cadence test therefore has to wait real frames in PlayMode
  (slow, borderline-flaky) or stay hardware-acceptance-only; a single-press
  Move/Submit/Cancel test does not need it at all.
- `EventSystem.Update()` (`EventSystem.cs:473`, `protected virtual`) calls
  `m_CurrentInputModule.Process()` (`:516`) once per engine frame,
  automatically. A PlayMode test sets the scripted `BaseInput`'s values,
  `yield return null` once, and asserts the result — it never needs to
  call `Process()` by hand.
- **`ExecuteEvents.Execute<T>` invokes every matching component on the
  target, not just one** (`ExecuteEvents.cs:328-339`,
  `go.GetComponents(components)` then adds every one that matches `T`) —
  confirms Draft 2's point-4 mistake: a bare `Selectable` plus a second,
  separate `IMoveHandler` on the same row would **both** run. One subclass
  replacing the plain `Selectable` is required, not an addition.
- **`Selectable.OnMove` is `public virtual`**, switching on
  `eventData.moveDir` (`Selectable.cs:1064-1084`) — directly overridable
  per-axis.
- **`InputManager.asset`'s stock "Horizontal" axis already binds the arrow
  keys**: `negativeButton: left`, `positiveButton: right`, plus
  `altNegativeButton: a`/`altPositiveButton: d`
  (`ProjectSettings/InputManager.asset:9-21`; a second "Horizontal" entry,
  `:153-165`, is the analog stick). Deleting RewardTrackController's own
  arrow-key poll and routing through the module's Horizontal-driven Move
  does not regress keyboard users.
- **A capture pipeline already exists for point 8's visual proof**:
  `tools/screenshot.ps1 -Runtime -RuntimeFilter <PlayModeTestClass>`
  (`tools/screenshot.ps1:4,17,131-169`) drives a named PlayMode test class
  through the test runner and captures the running game, animators
  ticking — reusable as-is for a new capture test class.
- **Modals already block the click that would reach behind them.** All
  eleven (grepped, one shape) are built by the same `Ui.Modal`
  factory (`Ui.cs:730-742`) from a full-screen `Solid` dimmer never marked
  `.AsDecor()` (`Ui.cs:734-735`), so `UiEmitter.EmitImage`'s decor branch
  (`UiEmitter.cs:539`: `if (decor) image.raycastTarget = false;`) never
  fires and the dimmer keeps `Image`'s default `raycastTarget = true` —
  the factory's own comment: "intercepting their input is what modal
  means" (`Ui.cs:738-741`). One canvas/raycaster per scene, so a click on
  the screen underneath never fires that control's `OnPointerDown`.

## 3. Input ownership — one dispatch point

**Mechanism: a project subclass of `StandaloneInputModule` overriding
`Process()`**, attached in place of the stock module wherever `SceneBuilder`
adds one. It is the only method in the game that reads gamepad/keyboard UI
input; nothing else polls `Input.*`/`input.*` for this purpose after this
lands.

Each call: ask the context stack for the top context **once**, at entry,
into `topAtStart` — never re-queried mid-call, which is what makes a
same-frame transition safe (below).

- **Top is Fight**: five steps, nothing else runs this call.
  1. Capture `topAtStart`.
  2. `EventSystem.current.SetSelectedGameObject(null)` — every frame,
     unconditionally: selection is null *because this asserts it every
     frame*, defeating every counter-example (a stale modal selection, an
     Explicit link, a stray programmatic call) by brute force, not trust.
  3. `base.Process()`, unchanged — **verified safe**:
     `SendSubmitEventToSelectedObject` returns immediately on
     `currentSelectedGameObject == null` (`:472-473`), and
     `SendMoveEventToSelectedObject`'s `ExecuteEvents.Execute` against a
     null target resolves through `GetEventList`'s `go == null` return
     (`ExecuteEvents.cs:325`) — mouse/click still runs (needed for Fight's
     own Buttons); Move/Submit dispatch is structurally inert. (Its
     debounce fields still update off the axis read with nothing to
     fire — harmless, but explains a slightly-off first-repeat cadence
     right after leaving Fight.)
  4. Read the same sampled `input.GetAxisRaw`/`input.GetButtonDown`
     values (Fight's debounce logic, moved verbatim from the deleted
     `PollGamepadNavigation`) and call `Fight.MoveFocus`/`ConfirmFocus`/
     `OnBackPressed` directly. `Navigation.None` on Fight's Buttons
     (Draft 2's fix) stays as a defensive second layer against a stray
     click — the null-assert, not this, is what makes Move/Submit inert.
  5. Nothing else.

  **3 before 4.** A future Fight verb that synchronously selects an
  `EventSystem` object (a shared menu opened mid-fight), run in the other
  order, would hand that selection to *this same call's* still-true
  Submit (`StandaloneInputModule.cs:476-479`) — opening the menu and
  firing its first item in one press. Step 3 first spends the frame's
  Submit against nothing (step 2); the new target waits for the *next*
  call. **Not live today, verified**: `OnBackPressed` only calls
  `_menu.Back()` (`FightController.Input.cs:737-739`), and ending combat
  is deferred past this call by `_isBusy`/`OnPlaybackFinished`
  (`:1206,1049`) — future-proofing, proven by a Fight stub built to
  select synchronously on purpose (test 2, below).
- **Top is not Fight**: `base.Process()` unchanged — free Move/Submit to
  whatever is selected (§4); then read `input.GetButtonDown(cancelButton)`
  once for that context's Cancel handler. (Its own internal Cancel read,
  `SendSubmitEventToSelectedObject:479-480`, is harmless in parallel —
  nothing implements `ICancelHandler` on any Selectable here, grepped.)

  **Then, every call, one rule after `base.Process()` returns** — the
  ordinary-context twin of the Fight branch's null-assert: if selection is
  null, or is a `GameObject` outside `topAtStart`'s declared Selectable
  set, reselect its remembered node, else its entry. One rule for three
  cases: (a) a background click nulls selection via legacy input's
  `DeselectIfSelectionChanged` (`PointerInputModule.cs:427-435`), restored
  in the **same call** — no first-Move-restores step, nothing crosses a
  frame boundary null; (b) a mouse click on a Selectable under a modal,
  which `OnPointerDown` would otherwise select (`Selectable.cs:1226-1233`:
  `if (IsInteractable() && navigation.mode != Navigation.Mode.None &&
  EventSystem.current != null) SetSelectedGameObject(gameObject,
  eventData);`) — but **verified this project's modals already block
  that click** (§2), so (b) is defence in depth, no scrim needed (§4);
  (c) a stray or programmatic `SetSelectedGameObject` from elsewhere.

**Consumption across transitions — the fix for point 2.** `topAtStart` is
captured once and `EventSystem.Update()` calls `Process()` at most once a
frame (`EventSystem.cs:473,516`), so a Cancel popping Options runs against
`topAtStart == Options` even though Fight becomes top before the call
returns — Fight's branch waits for the *next* call (test 1). The reverse
(Fight top, ending combat pushes Reckoning/Defeat) shares this mechanism
but never exercises it same-call, since `_isBusy` already defers it — no
mirror test needed; test 2 targets the hazard `_isBusy` doesn't cover.

**Acceptance tests, through the real dispatcher** (scripted `BaseInput`
via `inputOverride`, `yield return null`, assert resulting state — not a
direct handler call):
1. Modal-to-Fight: Options top over Fight; one Cancel frame. Assert
   Options closed and Fight's `MenuDepth` unmoved.
2. Same-call ordering: Fight top; one Submit frame against a Fight stub
   whose `ConfirmFocus` synchronously selects a stub menu entry (standing
   in for the not-yet-live case above). Assert `ConfirmFocus` ran once
   and its entry did not also receive this Submit.
3. Mouse-then-Submit: click a real Button (scripted mouse position/
   `GetMouseButtonDown`), then one Submit frame; assert the clicked
   control's action ran once, not `ConfirmFocus` (Draft 2's Test 1
   watched the wrong signal). Also, with a modal top, script that same
   click at the underlying screen's coordinates then Submit: assert the
   click reaches nothing and Submit still resolves against the modal's
   selected control.
4. Fight isolation: click a Fight verb Button (mouse), then a
   stick-and-Submit frame; assert selection is null next `Process()` and
   no module-driven action fired — only what `MoveFocus`/`ConfirmFocus`
   themselves produce.

Direct-handler tests (`MoveFocus`/`ConfirmFocus` called directly) stay
**alongside**, pinning model logic; the tests above pin the dispatcher
that decides whether the model or the module gets the frame.

## 4. Navigation context stack

- **Owns**: its Selectable set (Fight: none, §3); a declared entry target;
  remembered focus by stable id.
- **Push** (`Open()`, never `OnEnable`, §2): selects
  `remembered ?? entry`; Fight has no `GameObject` to select, it registers
  only its boolean/branch membership.
- **Pop** (`Close()`): restores the previous context's remembered node, or
  falls back per §6's invariants below — no single blanket "never null"
  rule, because Fight and an empty context both legitimately have no
  selection to restore.
- **Registration and cleanup**: pushed only from `Open()`; popped from
  `Close()`, **and** from `OnDisable()`/`OnDestroy()` as a safety net for
  external deactivation (a parent hidden, a scene unload) — the stack
  removes that context wherever it sits, not only if it's on top; removing
  a non-top entry changes nothing about the current top's selection,
  removing the top restores the next one down per the invariants (§6).
- **Runtime graph rewrites**: `MapController.Refresh()` (`:184-249`, the
  same method that already recomputes `reachable`/`occupied` and calls
  `PaintNode` per node) is also where Map's Explicit links between nodes
  are rewritten each call — a runtime write, on the same state-change
  trigger the paint step already has, not a build-time-only graph and not
  a per-frame rebuild.
- **Shortcut suspension** (Draft 2's inventory, unchanged): every listed
  `Update()` poll outside this dispatcher and Fight's now-deleted poll
  checks `Stack.IsTop(myContext)`; the two Escape-literal reads
  (`HubController.cs:183`, `SystemMenuController.cs:143`) are deleted,
  their job absorbed into §3's Cancel branch.
- **Fight as a context**: no `EventSystem` selection, ever (§3) — its
  membership is the dispatcher's branch condition, not a selected node.
- **Modals already own their raycasts** (§2): §3/§6's post-dispatch
  reselection rule is defence in depth for a click through a modal,
  earning its place instead on a background click and a
  stray/programmatic selection. No scrim change needed; `Ui.Modal`
  already behaves like one.

## 5. Navigation groups

Unchanged: List (Options rows, Talent's detail actions), Rail (RewardTrack,
Shop's paged row), Grid (Talent's `orbs[OrbCount]`, `:24`), Graph (Map,
links from `MapController.Refresh()` per §4). `Navigation.Explicit` links
set at scene-build time or, for Map only, rewritten at `Refresh()`-time.
Wrap/clamp per group+axis, not per screen (owner call, §12).

## 6. Runtime eligibility and lifecycle invariants

**Eligibility** (Draft 2, unchanged): hide-on-ineligible (Talent,
RewardTrack) re-resolves on the controller's own repaint; disable-on-
ineligible (Shop's sell buttons) needs no graph change, `Button.OnSubmit`
no-ops on a disabled target; never-disable/refuse-on-press (Party, Shop's
buy button) needs no recovery rule at all.

**Selection invariants, three separate cases — not one blanket rule:**
- **Ordinary context, nonempty group**: selection is never null while this
  context is top, enforced every call by §3's post-dispatch reselection
  rule rather than assumed. On push: `remembered ?? entry`. If the
  focused node vanishes mid-session: the group's next-nearest eligible
  node, or the entry if none remain. A background click, a stray
  leftover, or a programmatic call can unset selection **within** a call;
  §3's rule closes it out before that call returns, so no first-Move-
  restores special case exists — nothing ever crosses a frame null.
- **Ordinary context, its entry's whole group has emptied** (every node in
  it hidden/destroyed): falls back to **screen-level fallback** — select
  nothing, keep Cancel live (the context's Cancel handler still runs, so
  the screen can still be left), and re-select the first node that
  reappears in any group on the next repaint. This is the case Draft 2's
  "fall back to the group entry" silently couldn't handle, and the one
  case §3's rule does not paper over, since there is nothing to reselect.
- **Fight**: selection is null by construction, always, asserted every
  frame by the dispatcher (§3) — not an absence to recover from, the
  correct steady state.

## 7. Contracts

Unchanged from Draft 2 unless noted: initial focus/memory, Submit
semantics, mouse coexistence, tooltips, scroll-follow, tabs, reward-track
claim, text entry.

- **Options adjustment — one Move implementation, not two components.**
  A dedicated `OptionRow : Selectable` (or a thin subclass over whatever
  base the kit's rows already use) is the **only** component on
  `RowHovers[i]` handling movement — not a plain `Selectable` plus a
  second `IMoveHandler`, since `ExecuteEvents.Execute<T>` runs every
  matching component on the target (`ExecuteEvents.cs:328-339`, §2), so
  two would both fire. It overrides `OnMove` (`Selectable.cs:1064`):
  `MoveDirection.Left/Right` adjusts the bound value (`Step`/`SetSlider`,
  unchanged targets) and consumes the event without calling
  `base.OnMove`; `Up/Down` calls `base.OnMove(eventData)` unchanged,
  which is Unity's own ordinary row-to-row navigation. Repeat rides the
  same base `Move Repeat Delay/Rate` the framework already applies before
  calling `OnMove` — no separate repeat code, confirmed by
  `SendMoveEventToSelectedObject`'s debounce running before dispatch
  (`:511-554`). **Stepper Buttons get `Navigation.Mode.None`**: never
  selected by a mouse click, never a navigation target
  (`Selectable.cs:1232-1233`, same mechanism as Fight's fix, §3's prior
  draft) — clicking one still fires its `onClick`/`Step` (the pointer-
  click path is independent of the selection side-effect
  `Navigation.None` suppresses), and does **nothing** to the row's
  remembered focus, since `OnPointerDown` never calls
  `SetSelectedGameObject` for a `None`-mode Selectable.
- **RewardTrack — corrected, the double-drive Party had is fixed, this one
  wasn't.** Ribbon nodes become ordinary Rail-group Selectables navigated
  by the module (§5); `RewardTrackController.Input.cs:250-251`'s raw
  `KeyCode.LeftArrow`/`RightArrow` poll is **deleted outright**, not
  gated — keyboard arrow support survives for free because the stock
  "Horizontal" axis already binds `left`/`right`
  (`InputManager.asset:9-21`, §2). Selecting a node is a distinct action
  from revealing it: `RailScroll` gains an `ISelectHandler`/
  `IDeselectHandler` hook on each node (mirroring §7 v2's visual fix
  shape) that calls the controller's existing `ScrollTo(level)`
  (`RewardTrackController.Input.cs:229-233`) when that node becomes
  selected — one input (Move) drives selection, selection alone drives
  the reveal; there is no second raw poll left to double-drive it.
- **Selected vs Hovered** (Draft 2's finding, unchanged, treated as a
  candidate not a verified fix, §8 below): `ThemedButtonState` already
  gives Selected a distinct, `Max`-composed halo over Hover
  (`ThemedButtonState.cs:270-280,98-99`); `HoverIndex`-only controls
  (Party, Options rows, SystemMenu tabs) need the same shape added.

## 8. Party state table

Unchanged mechanism from Draft 2: destination is the `EventSystem`
selection itself (seats/cards are real Buttons, §5); `Formation.Drop`
reads the selected Button's index via the existing `IndexOfButton` helper
(`PartyController.cs:541-548`) instead of a raycast hit; no custom
`IMoveHandler` needed, ordinary `Selectable.OnMove` relocates the
candidate during Carrying; Cancel restores selection to the source
explicitly.

**Point 8's demand, unresolved by assertion — requires a visual acceptance
capture, not a claim.** A new PlayMode test drives Party into all four
states simultaneously: a carried source, a distinct valid destination, the
current `EventSystem`-selected Submit target, and a mouse hovering a fifth,
unrelated card — then captures via the existing pipeline
(`tools/screenshot.ps1 -Runtime -RuntimeFilter PartyGamepadVisualCaptureTests`,
§2). The reused `ThemedButtonState` alpha/scale ratio (§7) is a **candidate
treatment** applied to this capture, not a verified solution — the owner
reviews the picture; if the four states aren't distinguishable at a glance,
this is the point to add a fifth visual channel (e.g. a border colour
distinct from all three glow tiers) rather than tune the existing numbers
further.

## 9. Audit, and the one navigation model

**Revised 2026-09-17, phase 2 step E, against what step A actually built.**
Step A declared navigation on `ScreenDef.Nav` and wrote
`Navigation.Explicit` at scene-build time (`UiNavWiring`), audited by
`UiAudit.CheckNavigable`. Steps B and C then found that no screen could
use it and added a second, runtime path (`RuntimeNavWiring`) with its own
copy of the prev/next math. Two implementations of one rule is what
`docs/CODE_STANDARDS.md` section 10 forbids, so the two were collapsed
into one:

**The decision: runtime wiring is the only path; the build-time
declaration path is deleted.** The navigable set is a runtime fact on
every surface this phase touches, so a build-time write would be correct
for no frame a player ever sees:

- SystemMenu's tab strip — the scene carries five tabs, a context shows
  three, recomputed in `ApplyContext` (`SystemMenuScreen`'s own header:
  the scene cannot know which).
- RewardTrack — the collect button is hidden whenever nothing is owed
  (`PaintCollectButton`), so the rail's Down target exists or does not by
  save state.
- Party — which seats are occupied, and whether a carry is in progress,
  decide what a Move should reach.
- Map — `MapController.Refresh()` recomputes the reachable set per floor
  (section 4 already said so).

Every one of those rewires on its own repaint, so build-time links are
overwritten before first use. `ScreenDef.Nav`, `UiNavWiring` and
`UiAudit.CheckNavigable` are therefore deleted rather than kept for a
consumer that does not exist; nothing lost with them had a reader
(`UiNavDeclaration.Entry` duplicated `NavContext.Entry`, which is what
actually gets selected on push; `UiRequiredAction` was read only by
`CheckNavigable`).

**One declaration shape, one builder.** `UiNavSpec`'s types are now
generic over what a node is (`UiNavGroup<TNode>`, `UiNavLink<TNode>`,
`UiNavDeclaration<TNode>`) and `UiNavLinkBuilder.Build<TNode>` is the ONE
implementation of List/Rail/Grid + wrap/clamp + inter-group links. Domain
stays engine-free (it never inspects `TNode`), and `RuntimeNavWiring` is a
thin adapter: it feeds live `Selectable`s in and writes the resulting
`Navigation.Explicit` out, computing nothing itself. It is
**authoritative** — `Apply` writes all four directions of every node its
declaration resolved — so each surface passes its whole shape in one call
rather than dribbling one axis per call and depending on what the last
call left behind.

**(a) Structural, build-time — what survives.** `UiNavControlsAudit`, and
it is no longer opt-in. A screen's emitted component set is fixed by the
build, so "this thing answers a click, carries no `Selectable`, and nobody
said why no stick can reach it" is answerable there and nowhere else; the
fix is `AllowUnreachable("reason")` on the node or making it a real
Selectable wired into a group. Its limits are stated rather than hidden:
most custom actionable components in this project (`BarSlider`,
`HoldToConfirm`, `ListScroll`, `RailScroll`) are `AddComponent`'d by their
controller at runtime where no build-time scan sees them, so today it
fires on the build-time `Attach<T>` path only — the path that grows. A
GameObject that IS a Selectable is exempt wholesale: whatever else sits on
it is reached through that Selectable, not around it.

**(b) Behavioural, runtime PlayMode** — unchanged, and now carrying the
whole load that (a) used to share: for each required action, a test drives
the production dispatcher (section 3's scripted `BaseInput`) from the
context's entry to that action, in the state the action requires — Options'
adjustment with no Button target, Party's drop with the model in Carrying,
Talent's invest with an eligible slot selected. The intent statement
(section 1) narrows accordingly: the build proves the control inventory,
runtime proves operability.

## 10. Test strategy

- **Direct-handler tests**: kept (Fight's `MoveFocus`/`ConfirmFocus`,
  Formation's drop/cancel) — they pin model logic, not dispatch.
- **Dispatcher integration tests** (new, the centre of phase 1): scripted
  `BaseInput` + `inputOverride`, driven by `yield return null` per frame,
  asserting resulting actions/state — §3's four acceptance tests, run on a
  fixture scene with two ordinary Buttons, a modal, and a Fight stub
  exposing `MoveFocus`/`ConfirmFocus`/`OnBackPressed` call counts.
- **Repeat cadence**: hardware-acceptance only (§2) — not unit-tested,
  documented as a known gap rather than silently skipped.
- **Options row / RewardTrack node**: `OnMove` override tested directly
  (Left/Right adjusts, Up/Down delegates) plus one dispatcher-integration
  pass confirming a stepper mouse-click doesn't disturb remembered focus.
- **Party**: dispatcher-level Carrying test (module Move relocates the
  candidate, Submit resolves through `IndexOfButton`+`Formation.Drop`);
  the visual acceptance capture (§8).
- **Audit**: one test per §9(a) rule, one PlayMode test per §9(b) required
  action, growing with each screen in phase 3.
- **Hardware acceptance**: unspecified in repo/research; recommend Xbox +
  DualSense/DualShock on the shipping platform (owner call, §12).

## 11. Phases

**1 — prove ownership across transitions, on a fixture, before touching
real screens.** The module subclass; the context stack; Fight's branch and
deleted poll; the four dispatcher acceptance tests — the transition case
and the same-call ordering case — on two Buttons + a modal + a Fight
stub. *Gate*: all four pass through the real dispatcher, not a direct
call.

**2 — the hard screens.** SystemMenu (nested modal + Options), Options
(the `OnMove`-override row), RewardTrack (selection-drives-reveal), Party
(selection-based Carrying + the visual capture, §8). *Gate*: §9(b)
behavioural tests for each pass; a human plays all four with no mouse.

**3 — rollout.** Remaining screens, `CheckNavigable` (a) strengthened and
(b) behavioural tests written alongside each. *Gate*: every screen has both
kinds of proof, not structural alone.

**3a — done** (`ec81b29f`, `d39d955b`, `7a16ce67`, `49b4ab49`, `46d9daec`):
Hub, Main Menu + save slots, Map, Talent, Shop — this section's own status
header has the per-screen summary and the deviations each one forced.
**3b — not started**: whatever screens phase 3's own inventory still lists
beyond these five (Dossier, RunStats/Exits, Options' remaining rows,
Reckoning/Defeat, Fight's own verb menu if not already covered by phase
1/2) — check `docs/GAMEPAD_NAVIGATION_RESEARCH.md`'s screen inventory
against this file's status header for what is left, rather than assuming
"rollout" is finished because 3a is.

**4 — full journeys.** Controller-only and mouse-only passes across the
whole game, including system menu mid-fight (§3's transition case) and a
mixed-input pass (§3/§6's reselection rule). *Gate*: both clean, on
hardware from §12.

No effort estimates.

## 12. Open owner calls

1. **Options**: row-focus-adjust (this draft's default) vs. real steppers
   on `sound`/`music` too, retiring `OptionKind.Slider`.
2. **Selected-visual treatment** (§7/§8): the `ThemedButtonState` ratio is
   a candidate pending the visual capture, not approved — review the
   four-state screenshot before treating it as done.
3. **Wrap/clamp defaults** (§5): wrap Rail/Grid-row, clamp List, Map
   follows reachability.
4. **Hardware list**: recommend Xbox + DualSense/DualShock on the shipping
   platform.
5. **Party pick-up depth**: Submit-to-pick-up/drop only, unchanged default.
6. **Repeat-cadence testing gap** (§10): accept hardware-acceptance-only,
   or invest in a real-time PlayMode wait despite the speed/flakiness cost.
