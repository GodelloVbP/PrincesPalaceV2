# Gamepad Navigation -- Plan v3

> ## Status: 2026-09-19. HARDWARE ROUND 3. Start reaches the shop, Left/Right pick a target, and the spell list finally shows the books
>
> Round 3 is the same play-test's remainder, and three of its four items are one-liners that
> only look small because the hard part was deciding where they belong.
>
> - **Start opens the menu from inside the shop.** The shop is nested in the Map scene and has
>   no scene of its own, so it shares the Map's one `SystemMenuController` instance rather than
>   owning a second (`ScreenRegistry.cs:625`). Wiring it exposed a z-order bug in the other
>   direction: `MapScreen` had drawn the shop OVER the menu on purpose, back when "no dossier
>   access from inside the shop" was the rule that ask has now reversed. See `AUDIT.md` #191,
>   which also names the two contexts still absorbing Start and why they were left alone.
> - **Target cycling on Left/Right**, the owner's follow-up to round 2's inspect ring
>   ("selecting different mobs with gamepad goes with up down, but it should work with left
>   right"). One cycling rule serves both axes and both racks; what the two axes have to do
>   for themselves is translate their own sign, because Y is not mirrored and X is. The ally
>   rack is therefore the one side whose horizontal hand flips. `AUDIT.md` #190.
> - **The first press now MOVES.** `FocusedElement`/`ConfirmFocus` already read "nothing
>   hovered" as enemy 0 -- the marker sits there before any press and Submit would hit it --
>   so the older "first press lands ON index 0" rule made a pad player's first Up or Right
>   look like it did nothing. `CycleTargetFromPad` is the pad-only entry point that fixes it
>   without moving any direct-call test's literal indices.
> - **The fight's spell submenu draws each book's art**, which it never had
>   (`FightController.Hud.cs`'s `RefreshSubmenu`). The mark is painted only at Skill depth:
>   the same pooled rows serve the Element list, and a null id is what stops a mark from
>   surviving the reuse.
>
> **Open calls this round produced**, all in `AUDIT.md`: #181 (bloom was unreachable until a
> spell shader crossed it; two numbers now need a fresh look), #184 (the settle gate is narrow
> on purpose and widening it costs ~4.4s a round), #190 (the ally rack's horizontal direction,
> which needs a hand on a pad), #191 (Start over the Reckoning and the RelicDraft) and #192
> (Bjorn's portrait). #182 and #187 are small fixes deliberately left for the next commit in
> their own areas.
>
> The pad-side checklist for all of it is section 13, which grew four rows for the target
> cycling and the first-press rule.

> ## Status: 2026-09-19. HARDWARE ROUND 2. The pause menu moves from B to Start, the hub ring stops being hand-typed, and the fight's horizontal axis gets a job
>
> The owner played the round-1 build with a pad. Round 2 is what came back, and unlike round 1's
> visual half these were six unrelated things rather than one defect wearing six faces.
>
> **What landed**, one line each:
>
> - **Start opens the overarching menu; B only steps back.** `NavContext` grows a `systemMenu`
>   handler beside `cancel`, and Hub, Map and Fight hang their one shared open call off it. Cancel
>   at a root is now deliberately nothing at all.
> - **`SystemMenu` is a new axis** in `ProjectSettings/InputManager.asset`: `escape` as
>   `positiveButton`, `joystick button 7` (Xbox Start on Windows) as `altPositiveButton`.
> - **`TriggerLeft`/`TriggerRight`** on the 9th and 10th joystick axes drive `INavSectionStrip`.
> - **`SystemMenuController.OpenOnCancel` is renamed `OpenFromRoot`**, at its definition and all
>   three call sites; the name described the button, and the button moved.
> - **The hub ring is derived from `HubAnchors` x**, not hand-typed -- which reverses round 1's
>   literal reading of the owner's own words. See `AUDIT.md` #180.
> - **`RuntimeNavWiring.LinkBoth`** closes the class of bug the Map's missing Up link was an
>   instance of: a caller that wants a two-way edge asks once and gets both halves.
> - **Map's choice column gets its Up links**, and Main Menu's two modals are refactored onto
>   `LinkBoth`.
> - **The fight's horizontal axis steps off the verb column onto the actors**
>   (`IFightNavigationTarget.InspectMove`), so "what is this monster carrying" is answerable
>   without committing to a verb first.
> - **One status box replaces the per-badge tooltip**, hung under the inspected actor and listing
>   every status on it -- the old tooltip answered for one badge, landed beside that badge, and a
>   pad could not reach it at all.
> - **ATTACK loses its `Primary` ring** at rest, which closes `AUDIT.md` #171 by the owner's
>   answer rather than by a new argument.
> - **Talents: A on an unkindled star kindles it**, through `Button.onClick` so the mouse gets the
>   identical path; and the panel loses the character name, the constellation line, the fill bar,
>   the "CHOOSE A STAR" prompt and the violet container.
> - **The dossier's Skills row opens a real (read-only) pane** instead of doing nothing.
> - **Forty talent descriptions** are rewritten to state their numbers.
>
> **Open owner calls this round produced**, all in `AUDIT.md`:
>
> | # | The call |
> |---|---|
> | #166 (addendum) | Start's button number, the unbound DualSense Options button, and the two trigger axes are all documentation-correct and hardware-unverified |
> | #173 | Start is absorbed by every modal, so it is "from a root", not "from anywhere" -- closing that needs a suspend/restore mechanism |
> | #174 | Start over a Party carry undoes the carry rather than being ignored |
> | #175 | `ThemedMenuState.Primary` has one production user left, and it lights three save slots at once |
> | #176 | While inspecting, Up/Down walk the actor ring, so changing verb costs a press first |
> | #177 | (stated deviation) `ProcessFight` spends one axis per frame, so no diagonal does two things |
> | #178 | Nothing on the Talents screen names the open character or the constellation any more |
> | #179 | Four nav-link asymmetries found and deliberately not fixed |
> | #180 | The hub ring is derived from x, reversing the owner's own literal wording |

> ## Status: 2026-09-18. HARDWARE ROUND 1, VISUAL HALF COMPLETE. The owner's four visual findings were one defect, and section 12 item 2's open call is answered by replacement rather than by tuning
>
> The round's navigation half is the status block below this one: six findings, all fixed, the pad
> going where it should. This is the other half of the same play-test -- what the pad LOOKS like
> when it gets there -- and it is a shorter story because the complaints turned out to be one defect
> wearing six faces.
>
> | # | The owner's words | Root cause | Commit |
> |---|---|---|---|
> | 1 | "The selector on the start descent is huge and looks weird." | Every halo was a radial glow SIZED TO THE CONTROL, so its apparent size was a property of the control rather than of the indicator. The Hub's gate is 620x620. | `ce405df9` |
> | 2 | "The gold halo (e.g. in party screen) is way too strong." | The same halo, at `ThemedButtonState.SelectedGlowAlpha` (1.0) behind a Party seat. Section 8 called this "a candidate treatment, not a verified solution -- the owner reviews the picture". They did. | `ce405df9` |
> | 3 | "A player can't see where they're going in the character sheets screen: no obvious selectors." | `AUDIT.md` #160's own remedy: a soft glow the same footprint as a cell, under an icon and a name, which is a tint rather than a selector. | `ce405df9` |
> | 4 | The talent tree has no visible focus at all (and neither did Options or Fight). | Three screens never grew a halo, because a halo was something each screen had to add for itself. | `ce405df9` |
> | 5 | "There is no proper selector. Maybe we can make a small hovering arrow for the thing you're targeting with gamepad?" | Fight has no EventSystem selection by design (section 3), so no screen-level treatment could ever have reached it. | `ce405df9` |
> | 6 | "Attack is always seeming to be hovered over (not a gamepad bug) and makes it difficult to notice if you hover/select it." | `RefreshVerbs` painted `ThemedMenuState.Open` off `_focusedVerb`, which is 0 from wake and never -1. `AUDIT.md` #169. | `0995736b` |
>
> **ONE INDICATOR, and why tuning was never going to close items 1-3.** A glow scaled to the
> control it sits behind cannot be small on a 620-unit gate AND visible over a 96-unit orb: no pair
> of numbers satisfies both, because the quantity that varies is not one of the two being tuned. A
> marker of FIXED size beside the control is the same size everywhere by construction.
> `Core/FocusMarker.cs` is that marker, `Domain/UiKit/FocusMarkerPlacement.cs` is its arithmetic
> (EditMode, literal rects), and `NavigationInputModule.ShowFocusOn` is the only thing that drives
> it. No screen wires it and no screen can forget to.
>
> **Section 12 item 2 is ANSWERED, not merely progressed.** "The `ThemedButtonState` ratio is a
> candidate pending the visual capture, not approved" -- the capture was taken, the owner played it,
> and the answer was no. The ratio is retired as a focus visual everywhere it was used: Hub's five,
> Party's two groups, the dossier's three, the Reckoning's brightening, and
> `ThemedButtonState._isSelected`'s own arm of `IsSelectedHalo`. `Core/SelectHaloPainter.cs` had no
> callers left and is deleted. Section 8's four-state Party question survives in a different form
> and is re-captured: the gold ring says which slots are LEGAL, several at once, and the arrow says
> which one Submit will resolve on.
>
> **What Fight needed that no other screen did.** The dispatcher holds Fight's EventSystem selection
> at null every frame (section 3), so there is no selection for a marker to read.
> `IFightNavigationTarget.FocusedElement` is the model's own answer, read-only over `_focusedVerb`,
> `_menu.RowSelection` and the two hover indices -- every one of which `MoveFocus` and `ConfirmFocus`
> already maintained. Those three methods are untouched, and so are the direct-call tests that drive
> them. At Target depth it answers the HIT AREA over the figure rather than the plate in the corner,
> because the thing being targeted is the monster.
>
> **A defect a green suite had pinned, recorded because the mechanism is the point.** Item 6's
> `highlighted` clause carried a comment asserting the two states were "mutually exclusive in
> practice", and `FightFlowTests` had written the same wrong belief into an assertion WITH a comment
> explaining it. Nothing was failing. What caught it was a person holding a controller -- the same
> lesson the navigation half's own status header records about its items 1 and 6.
>
> **Two deviations from the brief, both stated in their commits and repeated here.**
> 1. **The edge hint is DERIVED from the control's aspect, not carried from the source of truth.**
>    The brief asked the module to hand the marker "a RectTransform plus an edge hint". An authored
>    hint means roughly a hundred new per-control declarations, each forgettable and each able to
>    contradict its neighbour; the control's own shape already answers the question. One rule,
>    computed, cannot drift. Its cost is real and is `AUDIT.md` #172.
> 2. **The arrow is a baked `proc:` sprite, not two rotated `Solid` squares.** The brief offered the
>    squares as the no-new-art route. At 26 units a chevron built from two rectangles is about a
>    third corners. A baked PNG from `ProceduralSpriteBaker` IS this project's no-new-art route --
>    generated by a tool anyone can re-run, committed, diffable -- and is where every other shape a
>    flat uGUI `Image` cannot draw already lives.
>
> **Where the marker lives, after two rejected shapes.** Runtime creation put a second rect preamble
> in `Core/`, which `UiKitLintTests.OnlyTheEmitterMayCreateGameObjects` exists to prevent. A node in
> every screen tree would be eleven `NodeRef`s, eleven bindings and eleven `UiAudit` overlap
> exemptions for an object whose job is to sit on top of what it points at. It is a scene ROOT
> FIXTURE instead, beside the camera, the Volume, the canvas and the EventSystem, and that lint's own
> bounded number was raised 4 -> 5 -- which is that lint's stated mechanism for making the allowance
> a visible decision rather than a drift.
>
> **Seven existing tests were ADAPTED, never relaxed**, each named in its own commit with what it
> asks instead: Party's seat-halo test, the dossier's four halo tests and the Reckoning's two
> brightening tests all made a claim about an indicator that no longer exists and now make the same
> claim about the marker; `FightFlowTests`' two verb-state assertions changed because the behaviour
> they pinned was the defect. The Reckoning pair additionally pins that focus does NOT touch the
> rarity alpha, which is a real behaviour change -- a focused Common used to read at 1.0 while an
> unfocused Legendary read at 0.30.
>
> **A defect the CAPTURES caught and no assertion could have** (`AUDIT.md` #170): the marker first
> parented to the root canvas as the last sibling, which is the ordinary uGUI rule and is wrong here
> -- `FightScreen` wraps its HUD in `Ui.NestedCanvas("FightHud", 1000)` with `overrideSorting`, so
> every root-canvas child draws under all of it. The Reckoning's marker reported itself shown, at the
> right coordinates, with the right target, and was nowhere in the picture. It attaches to its
> TARGET's own canvas now.
>
> **Eight pictures for the owner**, in `tools/screenshots/gamepad_visuals/`, produced by
> `tools/screenshot.ps1 -Runtime -RuntimeFilter FocusMarkerVisualCaptureTests` and copied out before
> the next runtime capture wipes that directory. Three open owner calls come with them and are in
> `AUDIT.md` rather than decided here: #171 (ATTACK still wears the Primary ring at rest, which is
> half of what "looks hovered" was), #172 (the derived edge, and the two places it collides with a
> label) and #160's own re-strike.
>
> Every commit gated on its own classes, the `ui` and `combat` areas, and a full
> `tools/run_tests_parallel.ps1` -- `-BuildScenes` on `ce405df9`, whose regenerated scenes WERE
> committed: they differ by far more than fileIDs (the marker fixture in all five, thirteen halo
> nodes gone). No gate was red at any commit, and the three known flakes passed on every run.

> ## Status: 2026-09-18. HARDWARE ROUND 1 COMPLETE. Section 12 item 4 has been answered by an owner play-test on a real controller, and all six of its findings are fixed
>
> This is the first entry in this document written from something a pad actually did rather than
> from reading code, and it is worth saying what that bought: **four of the six were defects no
> test in this project could have caught, because every one of them was a gap between the real
> scene and the fixtures the tests build.** The suite was green throughout. It still is.
>
> | # | The owner's words | Root cause | Commit |
> |---|---|---|---|
> | 1 | "I found no way to move in the talent screen." | Three of the Talents entry's four directions were unset and Back was still `Navigation.Mode.Automatic`. The stick worked, in one direction of four, on a screen with no focus visual to say so. | `f990021e` |
> | 2 | Relics -> Right went to Talents, not Character Sheet | The hub was a 2x2 Grid keyed on `HubAnchors` depth/lateral signs. Those are a STAGING fact; the composition on screen is a horseshoe, and Grid row-wrap sent Right off Relics across the whole screen. | `a9f89ebe` |
> | 3 | "The joystick only is wonky... it feels almost random." | The ordinary branch had no press edge at all and deferred to `StandaloneInputModule`, whose repeat DELAY is never armed by a stick resting near its own dead zone -- so it repeated at the bare rate, ten selections a second, untouched. | `f6672a26` |
> | 4 | "Arrow buttons should work as well as joystick." | The D-pad was bound to nothing: an Xbox pad reports its hat on the 6th and 7th joystick axes and `InputManager.asset` had only the left stick's two. Keyboard arrows were already fine and are now pinned. | `4b9f3ece` |
> | 5 | "Back (B) should not close a screen but take you back first." | The dossier could stack a level on itself (the reward track, the pack, the books) and had no `INavCancelClaim` at all, so one Back press ran SystemMenu's `Close` and spent three levels. | `9dd9ca36` |
> | 6 | "No intuitive navigating [in] the character sheets screen" | `CharacterDossierController.PairAcross` paired two groups by LIST INDEX, a rule correct only when they are already aligned across the crossed axis. Four of five cross-group edges landed 150-190 units from what the eye expected. | `12fc7385` |
>
> **The method that found items 1 and 6, recorded because it is reusable and reading the code was
> not enough for either.** Both were measured on the REAL path -- `Hub -> building -> Submit`,
> through `JourneyFixture`'s own scene loads -- with a throwaway diagnostic that dumped every
> declared link and the world centre of its owner, then deleted. Item 1's four-direction dump ruled
> out every one of the brief's other candidate causes in the same run (the context IS pushed, the
> top IS selecting, the entry IS selected on arrival, the module IS the subclass). Item 6's position
> dump is what turned "feels wrong" into "188 units, past a control 2 units away".
>
> **Two costs and one unknown are carried as open owner calls rather than closed quietly:**
> - **`AUDIT.md` #167** -- item 3's armed edge means a held stick no longer auto-repeats AT ALL.
>   That is a real loss on the reward track's rail and the dossier's pack. Predictable-and-slower
>   beat unpredictable in the owner's own report; whether it still does after living with it is the
>   owner's call, and the entry states what rebuilding repeat on top of the edge would cost.
> - **`AUDIT.md` #166** -- item 4's bindings are correct for an Xbox pad on Windows BY
>   DOCUMENTATION and verified on no hardware at all. Whether axes 6 and 7 are the hat, whether up
>   reads positive, and the DualSense entirely, all need the pad in hand. The test file says so in
>   its own header rather than implying coverage it does not have.
> - **Item 6 is half a fix.** "No obvious selectors" is the other half and belongs with the shared
>   arrow marker, not here -- the same gap item 1 hit on the talent orbs, and the same one
>   `AUDIT.md` #160 opened. A Move that goes to the right place is still a Move nobody can see.
>
> **Two deviations, both stated in their own commits and repeated here so the deviations register
> below stays complete:**
> 1. **The hub's ring is authored in the owner's stated order, not by screen x** (item 2). The two
>    expectations they gave are not both satisfiable by x order -- by x, the thing left of the gate
>    is Talents, not Principality. Where the heuristic and the owner's own words disagree the words
>    win, and the one hop that is not x-ordered is the price.
> 2. **Nothing on this screen is fully reversible across columns** (item 6). Three left-file slots
>    are nearest the same score cell and only one of them can be what that cell steps back to. Two
>    columns of different lengths cannot be a bijection.
>
> **Three existing tests and three existing assertions were ADAPTED, never relaxed**, each because
> it pinned behaviour this round rejected: the 2x2 hub grid, Cancel closing the menu from a pane
> (twice, in the pad journey and its mouse twin), and three index-paired dossier edges. Every one
> is named in its own commit message with what it now asks instead.
>
> Every commit gated on its own classes, the `ui` and `run` areas, and a full
> `tools/run_tests_parallel.ps1` before the next -- `-BuildScenes` on item 4, whose regenerated
> scenes were NOT committed (normalising fileIDs and anchors away left zero differing lines in all
> five, so the whole diff was renumbering). No gate was red at any commit, and the three
> `AUDIT.md` #157/#165 flakes passed on every run in this round.

> ## Status: 2026-09-18. PHASE 4 COMPLETE except hardware acceptance (owner, section 12 item 4). The two mechanism-level items the previous pass left open are closed
>
> `bd6f80af` (AUDIT.md #162, struck) and `fd7c8984` (AUDIT.md #163's second half, struck) --
> the two things the status block below this one recorded as unresolved. Neither needed a new
> feature; both were a rule this document already stated and the code did not implement.
>
> **AUDIT.md #162 was not a race, a leak, or the synchronous-load-inside-Process theory the
> previous pass left as the one thing it had not tested.** It was a knife edge in the test
> harness, and naming it took five instrumented reproductions of `tools/test.ps1 run` rather
> than another theory: `JourneyFixture.MoveMouseTo` aimed the scripted pointer at
> `RectTransform.position`, which is the PIVOT, not the rect centre its own comment claimed.
> The Hub's gate is pivoted (0.5, 0) -- rect (x:-310, y:0, w:620, h:620), pivot flat on the
> bottom edge, because a building is placed by the ground it stands on -- so the pointer sat
> exactly on that rect's `yMin`, inside only because `Rect.Contains` is inclusive there. The
> pointer's own arrival then tipped it out: `OnPointerEnter` starts `ButtonPressAnimator`
> lerping the button up to `HoverScale`, and the sub-pixel shift that puts in the pivot's
> screen position moves the frozen pointer below `yMin` on the very next frame -- the frame
> carrying `MouseButton0Down`. `GraphicRaycaster` then finds nothing, the press lands on no
> target, and the Button's `onClick` never fires. Batch size decided it because the size of
> that first hover step is `Time.deltaTime`-driven, which is the whole of "fails in every big
> batch, never alone". Fixed at the aim point (`rect.TransformPoint(rect.rect.center)`, interior
> to a rect for every pivot), not with a wait and not by touching an assertion. AUDIT.md #162
> carries the frame-by-frame evidence and the list of what was measured as sane at the failing
> frame.
>
> **Sections 4 and 6's "focus memory" was never implemented at all**, which AUDIT.md #163's
> first half had already noticed in passing ("grepped, `NavContext.Remember` is called
> nowhere") and left standing. It is now the dispatcher's job:
> `NavigationInputModule.Process` records the top context's settled selection as that
> context's remembered id every frame, so no screen calls `Remember` and no screen can forget
> to. Three model-level things had to change with it, and they are worth reading before
> assuming a context's lifetime is what it looks like:
> 1. **A context destroyed on close can never satisfy "Push selects remembered ?? entry".**
>    SystemMenu, the debug menu, the glossary and the shop each built a fresh `NavContext` per
>    open. Their contexts now outlive their time ON THE STACK -- created once, put back with the
>    new `NavContextStack.PushIfAbsent`, removed but not discarded on close. The other seven
>    contexts are one-per-scene or one-per-fight and are untouched.
> 2. **"Remembered if still valid" means usable, not declared.** A controller declares what it
>    owns, not what is on screen, so the whole rule now lives once in
>    `NavigationInputModule.SelectionFor` and `NavContext.ResolveSelection` is gone rather than
>    left beside it.
> 3. **Section 6's "if the focused node vanishes mid-session" was specified and unimplemented.**
>    The reselection rule tested non-null and declared, so hiding the control that held the
>    focus left focus on something the player can neither see nor move off.
>
> `FocusMemoryGamepadNavigationTests` is the new class (the Hub's pop case, the System Menu's
> cross-visit case, a hidden remembered node falling back to entry), all three through the real
> dispatcher. **Two existing tests were adapted rather than relaxed, both stated in
> `fd7c8984`'s own message**: `DebugMenuGamepadNavigationTests`' pinned claim passes unchanged
> and only its reason was stale, while `DossierGamepadNavigationTests`' tooltip-on-close test
> genuinely changed behaviour and is renamed -- a reopened menu now restores the remembered
> cell, and a box describing the selected cell is section 7's tooltip following focus rather
> than a stale flag surviving a close.
>
> **A third, unrelated flake found while gating this and filed rather than waved off**:
> AUDIT.md #165, a `combat` test whose own wait loop can exit at a value its own assertion
> rejects (a `float` literal against a `double` tolerance, 4.7e-8 apart). Not fixed here -- it
> is another area's arithmetic and deserves its own pass.
>
> **Section 12's open owner calls are UNCHANGED by this pass**, and section 12 item 4 (hardware
> acceptance on a real pad) remains the one thing this plan cannot automate.

> ## Status: 2026-09-18. PHASE 4 COMPLETE except hardware acceptance (owner, section 12 item 4)
>
> Items 3 and 4 landed this pass, joining item 1 (halos) and item 2 (the nine journey segments)
> from the two passes before it. Nine mouse-only regression classes
> (`Tests/PlayMode/Run/Journey*MouseTests.cs`, one per item 2 segment) replay every segment with
> the mouse only, sharing item 2's own assertion helpers (`AssertSelectedName`,
> `AssertFightIsTopWithNoSelection`, `AssertTopIsNotFight`, `Node`, `WaitForScene`, `WaitUntil`) --
> none of them copied, all of them called, per this pass's own brief. One new mixed-input class
> (`JourneyMixedInputTests.cs`) proves section 3's reselection rule and section 6's eligibility
> invariants with both devices touched in the same test, rule (a) through (g). Every gate green
> except two new tests that fail only under the full `run_tests_parallel.ps1` run and never alone
> or per-area (AUDIT.md #162, investigated at length, not resolved -- see its own writeup for what
> was ruled out) and the two pre-existing Combat/Stage failures AUDIT.md #157 already covers.
>
> **Item 3, the mouse-only regression.** `JourneyFixture.cs` gained the pointer half of its own
> contract: `MoveMouseTo`/`Click`/`ClickBackground`/`SettleMouseAt`/`WorldPointAtFraction`/
> `ClickWorldPoint`, all going through `ScriptedBaseInput.MousePosition`/`MouseButton0Down`/`Up`
> the same seam the pad half already used, positioned via `RectTransformUtility.WorldToScreenPoint`
> against the scene's own root Canvas (every scene this project builds is `ScreenSpaceCamera`, per
> `SceneBuilder.cs` -- `PartyGamepadVisualCaptureTests`' own `HoverCard` is the precedent this
> reuses rather than the Overlay-only shortcut `NavigationDispatcherTests`' synthetic fixture gets
> away with). Every Move-then-Submit walk in a pad segment collapses to one `Click` on the final
> control in its mouse twin -- a pointer aims at what it wants directly, it does not walk a graph
> to it -- and entry selection right after a screen opens is never asserted in the mouse files
> (this pass's own brief: a mouse player has clicked nothing yet, so there is no entry to assert);
> what carries over instead is model state and the visible result, the same claims the pad files
> make about what commits, resolves and loads.
>
> **Two deviations item 3 forced, not guessed past:**
> 1. **"Cancel opens the menu" has no click target on any screen this plan touches** -- Hub's own
>    `mainMenuButton` is a DIFFERENT shortcut (`RunManager.EndRun(); Navigation.Go(MainMenu)`,
>    straight to the title, bypassing the menu entirely), not an "open the system menu" button.
>    Every mouse file keeps `PressCancel()` for this one action, on the stated grounds that a
>    mouse player still has a keyboard and the legacy Input Manager's Cancel button is bound to
>    Escape either way -- the brief's own allowance ("the mouse uses the ESC/X control").
> 2. **A reward-track dot's onClick is not the mouse equivalent of a pad Move onto it.**
>    `RewardTrackController.Input.cs`'s own `Press(level)` COLLECTS a level already owed rather
>    than merely centring it ("a waiting node collects; anything else glides", that file's own
>    header) -- a pad `Move` only ever selects (`SelectIndex`, never `Press`), so it can never
>    collect by accident, and a click can. `JourneyVictoryToRewardScreensMouseTests` hovers
>    (`HoverIndex`, the same `ScrollTo` call a pad selection makes) rather than clicks, and
>    exercises the actual click-collects path deliberately, on the collect button, separately.
>
> **A real, previously-unproven mechanism bug found doing this, item 4's own rule (e) is where it
> surfaced but item 3's own tests hit its symptom too (AUDIT.md #163, fixed in the same pass):**
> `NavigationInputModule`'s post-dispatch reselection rule fell back to `NavContext.Entry` on
> EVERY null selection, because `NavContext.Remember` is called nowhere in this project -- correct
> for the cross-context case (`DebugMenuGamepadNavigationTests`' own pinned claim, a Cancel that
> pops a modal), silently wrong for the same-context case a plain background click or a click on a
> `Navigation.Mode.None` stepper button produces (`PointerInputModule.DeselectIfSelectionChanged`
> nulls the PREVIOUS selection based on `ISelectHandler` ancestry, which `Navigation.Mode.None`
> does nothing to prevent -- it only stops the CLICKED target from being reselected, per
> `OptionsController.cs`'s own, now-corrected comment). Fixed by capturing selection before
> `base.Process()` runs and preferring it over `Entry` whenever the SAME top context still
> declares it -- AUDIT.md #163 has the full argument for why this cannot regress the cross-context
> case it leaves alone.
>
> **Item 4, the mixed-input pass.** `JourneyMixedInputTests.cs`, one class, rules (a) through (g),
> each its own test, each asserting literal state: (a) a background click on the Hub restores the
> pre-click selection in the same frame, a following Move reaches the literal Grid neighbour; (b)
> a mouse click through a System Menu modal over Fight fires nothing behind it
> (`FocusedVerbForTest`/enemy HP unchanged), a following pad Submit arms the modal's own selected
> control exactly once (`ExitsController.ArmedIndex`); (c) selecting a Dossier pack cell then
> hovering another with the mouse leaves the tooltip describing the SELECTED item, both while
> hovering and after the mouse leaves entirely (section 7's focus-over-pointer rule, on a real
> screen with a real `TooltipFocusRouter`, not the Domain-level `TooltipFocus` unit tests alone);
> (d) picking up a Party seat with the pad then starting a real mouse drag (`PartyDragSource`'s own
> `IBeginDragHandler`, crossed via a scripted press-then-move-while-held rather than a direct
> method call) on another seat cancels the first carry and begins a new one, selection landing on
> the seat the drag actually started from; (e) is where AUDIT.md #163 was found -- a pad-selected
> Options stepper row survives a mouse click on its own Next button (value steps once, focus
> stays on the row), and a following pad Right adjusts the same row; (f) a shoulder-button frame
> over a context with no `INavTabStrip` (the Hub) selects nothing and changes nothing, the no-op
> `NavContext.RaiseTabStep` was already built for; (g) a mouse click on a Fight verb plate fires
> its `onClick`, selection is null the very next frame (Fight's own unconditional assert), and one
> following stick frame moves Fight's own hover focus exactly once.
>
> **AUDIT.md #164 (open, not a production bug)**: a scripted mouse cannot reach a reward-track dot
> scrolled outside the rail's own masked viewport -- a test-harness capability gap, matching
> `PartyGamepadVisualCaptureTests`' own already-stated hedge on the identical technique.
>
> **Section 12's open owner calls are UNCHANGED by this pass** -- items 3 and 4 proved existing
> behaviour under a second and a mixed input mode, they did not answer any of the six standing
> design questions. Item 4 (hardware: Xbox + DualSense/DualShock) remains the one thing this whole
> plan cannot automate, and section 13's own checklist is the walk-it-by-hand form of the same
> nine segments item 2 through 4 now also prove in CI.
>
> ## The deviations register, every phase, consolidated in one place
>
> Pulled from each phase's own status entry below rather than left scattered -- read this list
> before assuming a screen's behaviour matches this document's own prose literally; several
> screens turned out not to match the brief that was written before anyone read their code.
>
> 1. **Hub has no tab bar** (phase 3a) -- four staged buildings and a gate, not a tab strip.
> 2. **Talent's orbs are a tree, not a fixed-width Grid** (phase 3a) -- `TalentSkeleton`'s row-of-1-
>    then-rows-of-3 shape cannot be expressed by `row = i / cols`.
> 3. **Main Menu's Cancel closes the save-slot modal instead of being a no-op everywhere** (phase
>    3a) -- a stick user with no mouse would otherwise be unable to back out of a panel they opened.
> 4. **Job 1's tooltip placement computes against each screen's own painted interior, not the
>    canvas** (phase 3b) -- the brief said canvas; using it would let a tooltip hang off the panel
>    it belongs to and onto the scene behind it.
> 5. **RelicDraft's Cancel is a deliberate no-op, not "closes them" like the other two Hub modals**
>    (phase 3b item 3, four modals corrected to three) -- "a draft you can navigate around is not a
>    draft," and closing it would let `RunOrchestrator.FinishDraft` never run.
> 6. **Segment 5 (Hub -> Shop) is Hub -> gate -> the draft -> Map -> a Shop room, never a Hub
>    building** (phase 4 item 2) -- the Hub has no Shop building; the in-run shop is a Map room.
> 7. **Segment 6's refused talent case is PrerequisiteMissing, not the brief's own NotAuthored**
>    (phase 4 item 2) -- which slot has no authored talent is a fact about content, not a stable
>    thing to pin; PrerequisiteMissing is guaranteed by `TalentPage.Evaluate`'s own ordering.
> 8. **Segment 7 is the map's FIRST real decision point, not literally "the run's second
>    traversal"** (phase 4 item 2) -- reaching a genuine second fight would duplicate segment 8's
>    own cost (a full pad-driven win) for no new claim; the property actually asked for (Move-driven
>    aiming past the entry) does not need a second traversal to exercise.
> 9. **Segment 8 proves TWO reward screens, not the one the brief's phrase names** -- the Reckoning
>    (opened automatically by a win) and the reward track (a different screen, reached from the
>    System Menu, never opened automatically -- grepped, nothing in `FightController`/
>    `FightBootstrap` calls it).
> 10. **Every journey segment reconstructs its own precondition rather than reading a prior
>     class's leftover save** (phase 4 item 2, stated once, applies to all nine) -- NUnit does not
>     guarantee cross-class execution order.
> 11. **Segment 4 reaches the Hub through a deliberate LOSS, not "win it, or flee"** (phase 4 item
>     2) -- Flee/Run was removed from `FightScreen.cs` outright, and a won fight's own Continue
>     leads to the Map (a run still standing), not the Hub.
> 12. **Item 3's mouse files never assert entry selection right after a screen opens** (phase 4
>     item 3) -- a mouse player has clicked nothing yet; see this header's own item 3 section.
> 13. **A reward-track dot's click is not the mouse equivalent of a pad Move onto it** (phase 4
>     item 3) -- clicking can COLLECT a waiting level; selecting (pad or hover) only ever scrolls.
>
> ### Item 2 detail, carried over unchanged from its own landing (below)
>
> `cd311629` (segments 5-6), `695e97ff` (segment 7), `0fa3b05a` (segment 8,
> plus a real bug found and fixed), `f1b92b9f` (segment 9) -- each gated on
> its own new class(es), the `ui`/`run` areas, and a full
> `tools/run_tests_parallel.ps1` before the next, the same shape segments
> 1-4 and phase 3a both already used. Every segment class lives directly
> under `Tests/PlayMode/Run/`, extends the shared `JourneyFixture`, and
> drives only through `ScriptedBaseInput` -- the one rule every class in
> this family follows.
>
> **Segment 5 (`JourneyHubToShopTests`), deviated and stated rather than
> followed past what is real**: the brief's own "Hub -> Shop... navigate to
> the shop building from the hub entry" does not describe this game -- the
> Hub has no Shop building at all (`HubController.WireNavigation`'s own
> four staged buildings are Talents, Relics, Principality,
> CharacterSheet). The in-run shop is a MAP ROOM, so this segment is Hub ->
> gate -> the relic draft (spent, same as segment 1) -> Map -> a Shop
> room's own entry (seeded to depth 1's own slot, the same shape as
> segment 1's `SeedWithAPlainFightAtDepth1Entry`) -> Submit walks there and
> opens the shop as a panel over the Map, no scene load. Both eligibility
> shapes against the same card, in the brief's own order: refused a gold
> short (painted, gold unchanged), then bought once affordable (gold down
> by exactly its price). Cancel-Cancel leaves back onto the same Map, never
> "the hub" -- there is no path from this shop straight there, on the pad
> or the mouse.
>
> **Segment 6 (`JourneyHubToTalentsTests`)** is the Hub's own real Talents
> building, reached through three declared links off the gate (Up ->
> CharacterSheetBuilding, Left -> PrincipalityBuilding, Up ->
> TalentsBuilding -- the Grid's own row-stepping plus
> `HubController.WireNavigation`'s own explicit pair). Deviated from the
> brief's own "refused on an unauthored one": which slot a character's
> tree has no content for (`TalentPage.Refusal.NotAuthored`) is a fact
> about content, not structure, and not stable to pin against whichever
> character or authored talents happen to exist. The refusal proven
> instead is `PrerequisiteMissing` (a tier-1 orb before its root is
> unlocked), guaranteed by `TalentPage.Evaluate`'s own ordering regardless
> of content. Two separate reconstructed journeys, not one chained
> session: investing the root removes the very `PrerequisiteMissing`
> condition the refused case needs, and there is no dispatcher-proven way
> back from `InvestButton` onto the tree short of a Cancel that would
> leave the screen.
>
> **Segment 7 (`JourneyMapChosenNodeTests`), deviated from "the run's
> second traversal"**: reaching a real second fight would mean playing the
> first to a win through the pad first, which is segment 8's own scope --
> doing that again here to reach the same KIND of decision a second time
> would duplicate that cost for no new claim. What the sizing note
> actually asks for is the property segment 1 deliberately sidesteps
> ("there is no independent way for this segment to steer Move presses
> toward one otherwise"), so this is the map's first real decision point,
> seeded (live search, never pinned) so a plain Fight room sits at a slot
> OTHER than the entry -- reachable only by moving past it, proving the
> selection actually landed on the CHOSEN node before Submit, not the
> entry segment 1's own shortcut always accepts.
>
> **Segment 8 (`JourneyVictoryToRewardScreensTests`), deviated from "fight
> victory -> reward track"**: that phrase names one screen, but the real
> game has two. The instant a fight is won, `FightController.OpenReckoning`
> shows the Reckoning (an item pick) -- that is what section 13's own
> Victory bullet ("Move along... Submit to take one... Continue")
> describes. The reward track (`RewardTrackController`, "Move along its
> rail... ScrollTo... literal level... Submit collect if owed" -- language
> only a level rail fits) is a different screen, reached from the System
> Menu's own Character & Inventory tab, never opened automatically by a
> win (grepped: nothing in `FightController`/`FightBootstrap` calls it).
> Both are proven, as two journeys in one class: the fight-to-Reckoning
> path is one continuous pad session (`LevelTheSquadTo(90)` makes a
> floor-1 room a certain win, played with a literal round cap on top of
> the usual real-time deadline, then take-or-skip the offer, Continue
> lands on the Map since the run is still standing after a win); the
> reward track is reconstructed separately from the Hub with a
> level/claimed state built to owe something, since crediting that debt
> DURING the pad-driven fight would mean pinning an exact
> experience-to-level threshold this file has no business asserting.
>
> **A real bug found and fixed, in the mechanism, same commit**: driving
> `DossierTrackRow`'s own Submit through the real dispatcher left the pad
> standing on the row that had just been covered by the panel it opened,
> with nothing to Move onto -- `SystemMenuController.RefreshSelectables`
> declares every Selectable under the whole pane regardless of visibility,
> so the hidden row still counted as "declared" and the
> reselect-if-outside-the-set rule stayed silent. The identical mechanism,
> and the identical fix shape, `ShowSpells` already applies to itself
> (`AUDIT.md` #161) -- whose own comment named `ShowPack`'s twin gap as
> "left alone here... a separate change", and it turns out
> `RewardTrackController`'s own panel had the same untreated gap. Fixed
> rather than routed around: `RewardTrackController.OnEnable` now selects
> its own ribbon's first dot (it already declares that Rail in `Wire()`,
> it just never selected into it); a new public `Closed` callback,
> assigned fresh by `CharacterDossierController.ShowTrack` on every open,
> hands selection back to `DossierTrackRow` when the panel closes (fired
> from `OnDisable`, the same safety-net shape this project's
> `NavContextStack` already uses). `ShowPack`'s own identical gap is left
> alone, matching the precedent its sibling fix already set -- fixing an
> unrequested control is a separate change.
>
> **Segment 9 (`JourneySystemMenuToMainMenuTests`)**, the smallest of the
> five: reconstructed from the Hub directly rather than chained onto
> segment 8's own class (the same cross-class-order reason every segment
> in this suite reconstructs its own precondition). Cancel opens the menu
> on its default tab; one `TabPrev` wraps from the first visible tab
> straight to the last one, MainMenu, landing inside its pane on
> `ExitTitle`; Submit arms Title, a second Submit fires it (`EndRun` -- a
> no-op outside a run -- then `Navigation.Go(MainMenu)`, a real scene
> load). Lands on the Main Menu's own entry with exactly one `NavContext`
> left on the stack, proving the exit did not strand the hub's or the
> menu's own context underneath.
>
> Every gate green: each new class, the `ui` and `run` areas, and a full
> `tools/run_tests_parallel.ps1` after every commit -- no regressions from
> either the five new segment classes or the `RewardTrackController`/
> `CharacterDossierController` fix. Phase 4 item 2 is complete: Main Menu
> through a full descent, a fight won and lost, both reward screens, the
> shop, the talent tree, and back out to the Main Menu, entirely through
> the real production dispatcher.

> ## Status: 2026-09-18. Phase 4, item 2: segments 1-4 landed as automated PlayMode tests; segments 5-9 sized, not attempted this pass
>
> Four commits (`4f3cf2fc`, `d59d0963`, `e1f6cdf7` plus this doc update),
> each gated on the new classes, `tools/test.ps1 run`, and a full
> `tools/run_tests_parallel.ps1` before the next -- the shape the previous
> status block's own sizing note asked for ("item 2 (controller-only) as
> its own session first"). `Tests/PlayMode/Shared/JourneyFixture.cs` is the
> one shared mechanism every segment class reuses (scripted `BaseInput`
> takeover, `PressSubmit`/`PressCancel`/`PressTabNext`/`PressTabPrev`/`Move`,
> and the three assertion shapes the brief asked for); segments live as
> separate classes directly under `Tests/PlayMode/Run/` (the area whose own
> header describes exactly this spine -- a descent, its fights, the
> reward track, saves -- closer than `Ui`, which is presentation and single
> screens only).
>
> **Segments 1-4, done.** `JourneyToFirstFightTests`: Main Menu -> Play -> an
> empty save slot -> Hub -> the descent gate -> the relic draft (spent, not
> skipped -- a fresh save has not drafted one yet) -> Descend -> the Map ->
> a plain Fight room's own entry node -> the Fight scene, every step through
> real scene loads (`Navigation.LoadOverride` is never set in this suite,
> unlike every single-screen gamepad-nav test in this project). The run is
> seeded (`RunOrchestrator.StartRun`) only AFTER `SaveSlotManager.EnterSlot`,
> never before -- seeding earlier collides with `EnterSlot`'s own
> `SettleOnOpening` (AUDIT #117), which ends a run that already exists the
> instant a slot is opened. The seed is chosen live
> (`SeedWithAPlainFightAtDepth1Entry`, the same search shape
> `ShopGamepadNavigationTests` already uses) so the Map's own entry is
> already a plain Fight room, because this suite never teleports
> `EventSystem` selection to aim a Move at a specific one -- see segment 7's
> own note below for why that stays true on the run's SECOND traversal too.
> `JourneyFightRoundTests`: the verb column moved with the pad (Up then
> Down, proving the press reaches it rather than merely that ATTACK is
> already focused), a target hovered and confirmed, the round played with
> no further input, the literal damage (6) pinned rather than recomputed.
> `JourneySystemMenuMidFightTests`: Cancel at Root opens the system menu
> over a live Fight (section 3's own transition case, now proven end to end
> rather than only at the dispatcher-fixture level
> `CancelOpensSystemMenuTests` already covers), a Move proves Fight's own
> branch is inert underneath it, `TabNext` reaches Options, one stepper and
> one slider row are each adjusted by exactly one step, and Cancel closes
> back to Fight with selection null and the verb focus untouched.
> `JourneyFightToHubOnDefeatTests`: Inspect/Return's Rail walked with Move,
> Submit on Return raises `Dismissed` and loads the Hub for real, gate
> selected.
>
> **Deviation, stated rather than silently reinterpreted: segment 4 reaches
> the Hub through a deliberate LOSS, not "win it, or flee".**
> `FightScreen.cs`'s own header on `BuildVerbColumn` records Flee/Run as
> REMOVED, not hidden -- "it never actually fled a fight... there was no
> Flee/Run method anywhere in `FightSession` to wire it to" -- so there is
> no flee verb this suite could press. Winning does not reach the Hub
> either: `FightController.LeaveFight` is
> `Navigation.Go(RunManager.HasRun ? Navigation.Map : Navigation.Hub)`, and
> a win leaves the run standing (`RunOrchestrator.SettleFight` only calls
> `RunManager.EndRun` on a loss) -- so a won fight's own Continue lands on
> the Map, which is segment 7's path, not segment 4's. A deliberate loss is
> the one route this screen actually offers to the Hub, and it is an
> established, sanctioned pattern in this project's own suite already
> (`FightSettlementTests`' `WalkInOn`/solo-squad shape), reused here through
> the pad rather than through `Button.onClick`.
>
> **A second stated deviation, load-bearing for every segment after the
> first: state is RECONSTRUCTED per segment class, not physically read off
> a prior class's leftover save file**, despite the brief's own "starting
> from a deterministic state the previous segment ends in... the save state
> the previous one leaves on disk" framing. NUnit does not guarantee
> cross-CLASS execution order (only within a fixture), so a segment reading
> another class's save file would be correct only by accident of whichever
> order the runner happens to invoke fixture classes in -- a fragility no
> existing single-screen gamepad-nav test in this project accepts; every one
> of them sets up its OWN scenario through the orchestrator rather than
> depending on another test's leftovers, and segments 2-4 follow that same,
> already-proven shape instead of introducing a new, order-dependent one.
>
> **Two bugs found and fixed in this pass, both in the mechanism rather
> than in a screen, both because a JOURNEY chains presses no single-screen
> test ever had to:**
> 1. `NavigationInputModule.ProcessFight`'s own `_fightVerticalArmed` only
>    re-arms once it reads `|vertical| < 0.5f` -- two Fight-branch `Move`
>    calls back to back with no neutral frame between them read the second
>    as still-held. `JourneyFixture.Move` now drives a settle frame at
>    neutral before returning, once, for every caller.
> 2. `StandaloneInputModule.AllowMoveEventProcessing` ORs a fresh nonzero
>    axis read against a REAL-TIME repeat-delay gate with no `BaseInput`
>    seam (section 10's own "repeat-cadence testing... has to wait real
>    frames" note, met here from the test-writing side rather than the
>    production side) -- a single isolated press is always let through, but
>    a SECOND chained ordinary-context move arriving sooner than Unity's own
>    0.5s `moveRepeatDelay` after the first is silently dropped. Every
>    existing single-screen gamepad-nav file in this project sidesteps this
>    by never chaining two dispatcher moves in one test (their own headers
>    say so, e.g. `SystemMenuGamepadNavigationTests`' "ONE PRESS PER TEST");
>    `JourneyFixture.Move` now settles for real time after releasing the
>    stick, confirmed by a diagnostic wait inserted and then removed once
>    the cause was named. This is a real cost: each `Move` call now spends
>    0.6 real seconds, which is why this whole suite runs in single-digit
>    seconds per class rather than milliseconds -- accepted rather than
>    chased further, since the alternative (per section 12 item 6) is
>    hardware-acceptance-only coverage of chained moves, which is less, not
>    more.
>
> **Segments 5-9, sized rather than attempted this pass** -- each is
> comparable in scope to one of the four already shipped, and the previous
> status block's own estimate ("several sessions of work... splitting it
> into [segments 1-4] as one class and [segments 5-9] as a second") is
> confirmed rather than revised by what actually shipped: four segments
> plus their shared mechanism and two mechanism-level bugs was a full
> session on its own.
> - **Segment 5 (Hub -> Shop)** needs a seed search for a Shop room reached
>   from the Hub gate's own Map (not the Hub's four buildings --
>   `HubGamepadNavigationTests`' own button list has no Shop building; the
>   in-run shop is a Map room, `ShopGamepadNavigationTests`' own fixture
>   confirms this), then the buy/refuse pair section 2's own verification
>   note requires (both eligibility shapes).
> - **Segment 6 (Talents)** is reachable only from the HUB's own
>   `TalentsBuilding`, which -- unlike Shop -- is between-descents furniture,
>   not a Map room; reaching it after segment 4's Hub arrival is
>   straightforward, but investing (or being refused) needs a real,
>   eligible/ineligible orb state assembled the way
>   `TalentGamepadNavigationTests` already does.
> - **Segment 7 (the run's SECOND traversal, Hub -> Map -> a chosen node)**
>   is the one segment that genuinely needs Move-driven aiming at a
>   specific room type rather than a chosen entry -- segments 1-4's own
>   seed-search shortcut does not apply a second time once the player has a
>   real choice of nodes, which is section 13's own "second fight, same
>   checklist as the first" bullet, not a repeat of segment 1.
> - **Segment 8 (victory or defeat, whichever the seed reaches, then the
>   reward track)** is the one segment needing an actual WIN driven to
>   completion through the pad (segments 2/3 stop after one round;
>   segment 4 is a deliberate loss) -- `FightSettlementTests.LevelTheSquadTo`
>   is the established lever, but reaching the Reckoning through Move/Submit
>   rather than a direct `_reckoning.Show()` call (`ReckoningGamepadNavigationTests`'
>   own shortcut) means playing a real, winnable multi-round fight to its
>   end on the pad first -- itself close to segment 2's own scope, before
>   the reward-track rail and its claim are reachable at all.
> - **Segment 9 (System Menu -> Main Menu)** is the smallest of the five,
>   and depends on wherever segment 8 actually ends (the brief's own "from
>   wherever segment 8 ends" -- Hub or Map, decided by which of victory or
>   defeat the seed reaches), so it was left for the same session as 8
>   rather than built against a guessed starting screen.
>
> Recommendation unchanged from the previous status block: segments 5-9 as
> their own follow-up session, the same gated-commit shape this pass and
> phase 3a both already used.
>
> **Item 1** (`65ad5325`, `AUDIT.md` #160 closed): the dossier's pack cells,
> equipment slots and ability-score cells, and the Reckoning's offer cards,
> now carry a selected-visual -- see `AUDIT.md` #160's own resolution
> paragraph and `docs/CODE_MAP.md`'s phase 4 entry for the mechanism.
> Narrower than the brief that opened this item: the pack's sort tabs and the
> pack/spells Close buttons turned out to already answer focus for free
> (`.ThemedPlate()`/`.Themed()` already wires a `ThemedButtonState`), found by
> reading `UiEmitter.WireThemedButton` before wiring a redundant halo behind
> an already-themed button. Verified by re-running the existing
> `DossierTooltipCaptureTests`/`ReckoningTooltipCaptureTests` capture classes
> rather than writing new ones -- the hole was in what those pictures showed,
> not in what they covered.
>
> **Items 2-4 (the full journeys), sized rather than attempted this pass.**
> Real research, not a guess: `Core/Navigation.cs`'s `Go(scene)` calls
> `SceneManager.LoadScene` -- SYNCHRONOUS, not `LoadSceneAsync` -- unless a
> test has set `LoadOverride` (every existing single-screen gamepad test sets
> it to a no-op specifically so IT does not have to cross a scene boundary).
> Only five scenes exist at all (`Navigation.MainMenu/Hub/Fight/Map/Talents`);
> Shop, Party, RewardTrack, Reckoning, Defeat, SystemMenu, the Dossier,
> Glossary, the debug menu and RelicDraft are all panels inside one of those
> five, not scene loads -- so a controller-only journey crosses far fewer real
> scene boundaries than its own prose suggests, and each one, left
> un-stubbed, resolves in the same frame a Submit press does. That makes "one
> PlayMode test through the real dispatcher, start to finish" architecturally
> reachable rather than the section 11 concern it reads as at first -- the
> genuine cost is not scene-crossing plumbing, it is standing up a REAL fight
> to a resolvable state (a seeded encounter, a full round of enemy AI,
> `IFightNavigationTarget`'s own non-selecting dispatcher branch) and a real
> shop/talent economy (an eligible purchase AND a refused one, per section
> 2's own verification note that both eligibility shapes exist) inside the
> same test, chained through two fights, a defeat-or-victory branch and a
> reward-track claim, with every step asserting a literal state and no step
> touching a mouse or a controller method directly -- section 10's own
> "growing with each screen" scale, applied to the whole game at once rather
> than one screen. That is several sessions of work by this plan's own
> established rate (one screen, one session, phase 3a's own convention), not
> one sitting bolted onto item 1's -- attempting it in the time left over
> from item 1 would have meant shipping thousands of lines of new
> integration test with at most one or two real Unity runs behind them, on a
> repo whose own standing rule is that a weakened assertion is worse than an
> absent one. Recommendation: item 2 (controller-only) as its own session
> first, splitting it into MainMenu -> Hub -> Map -> Fight (first fight,
> mid-fight system menu, flee-or-finish) as one PlayMode test class and
> Hub -> Shop/Talents -> Map -> second fight -> Reckoning/Defeat -> reward
> track -> SystemMenu -> quit as a second, each starting from the save state
> the previous one leaves on disk (`SaveSystem.RootOverride`, the same
> throwaway-root shape every test in this family already uses) rather than
> the same save state existing in the same in-memory scene -- items 3 (the
> mouse-only regression, sharing item 2's assertions) and 4 (the mixed-input
> pass, section 3/6's rules) follow once item 2's own shape exists to share
> from and drive against. Section 12.7 below has the manual checklist an
> owner can walk on real hardware today, independent of whether the
> automated version of it exists yet.
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
   four-state screenshot before treating it as done. **ANSWERED 2026-09-18**, by
   this file's own top status header: the capture was taken, the owner
   played it on hardware, and the answer was no. The ratio is retired as a
   focus visual project-wide and `Core/FocusMarker.cs` replaces it. What is
   still open is narrower and lives in `AUDIT.md` rather than here -- #171
   (ATTACK's Primary ring at rest) and #172 (the derived edge hint).
3. **Wrap/clamp defaults** (§5): wrap Rail/Grid-row, clamp List, Map
   follows reachability.
4. **Hardware list**: recommend Xbox + DualSense/DualShock on the shipping
   platform. **ROUND 1 DONE** (2026-09-18, this file's own top status
   header): played on a real controller, six findings, all six fixed. What
   this item still wants is a SECOND pass -- the six fixes themselves have
   not been played, and the D-pad bindings item 4 added are verified on no
   hardware at all (`AUDIT.md` #166). The DualSense half is untouched.
5. **Party pick-up depth**: Submit-to-pick-up/drop only, unchanged default.
6. **Repeat-cadence testing gap** (§10): accept hardware-acceptance-only,
   or invest in a real-time PlayMode wait despite the speed/flakiness cost.

## 13. How to play-test on a pad

Section 12 item 4's answer, stated once here rather than left as a bare
recommendation: an Xbox pad (or Xbox Cloud/XInput-compatible) and a
DualSense or DualShock, both over USB first -- Bluetooth adds its own
latency and drop-out class of bug that is worth a second pass once the
mapping itself is confirmed correct, not the first thing to debug against.
Legacy Input Manager reads a generic "joystick" axis layout, so no
per-controller code exists to differ between the two; the point of testing
both is confirming the OS/driver layer maps them onto that layout the same
way, not exercising different game code.

**Getting a controller into the game**: either play from the Editor with a
controller plugged in (Unity's own Input Manager reads it with no extra
setup -- `ProjectSettings/InputManager.asset`'s axes are already generic
joystick bindings, not per-device), or make a build (`File > Build Settings`
in the Editor, or `tools/`'s own build path if one exists by the time this
is read -- check `docs/WORKFLOW.md` for the current one, since this plan
does not own the build pipeline) and run it standalone, which is closer to
what a player's machine actually does (no Editor window stealing focus, no
Editor-only input quirks). Either way, confirm the pad is seen before
walking the checklist: open Options (any screen, System Menu's own tab) and
press a stick direction -- if selection moves, the module is reading the
pad.

**The journey checklist.** This is section 11 phase 4 item 2's own journey
list, unimplemented as an automated test as of this status header (see the
header's own sizing note) but walkable by hand today -- every step should be
reachable on stick + Submit + Cancel alone, with no mouse touch at any
point, and the halo from phase 4 item 1 should mark the selected control on
the dossier and the Reckoning at every step that reaches them:

- [ ] Main Menu: stick to Play (or Continue, if a save exists), Submit.
- [ ] New Descent (or an empty save slot): stick to a slot, Submit --
      lands in the Hub.
- [ ] Hub: stick to the descent gate, Submit -- reaches the Map (or starts
      the run's first fight directly, depending on what a fresh run does;
      follow whichever the game actually does rather than assuming).
- [ ] Map: stick to a reachable node, Submit -- enters the first fight.
- [ ] Fight: stick through the verb menu to Attack (or a Skill, if one is
      off cooldown), Submit; stick to a target, Submit; let the round play
      with no further input and confirm it resolves on its own.
- [ ] Fight, target pick, against two or more living enemies: with nothing
      hovered yet, Right should land on the nearest one (same as a first
      Down) and Left on the farthest (same as a first Up). From there,
      Right/Left should walk the rack exactly like Down/Up do -- one slot
      deeper (further right and higher on stage) per Right or Down, one
      slot nearer per Left or Up, wrapping at both ends -- and the marker
      (and the status box, if the target carries one) should follow every
      step. This is the owner's 2026-09-19 follow-up ("selecting different
      mobs with gamepad goes with up down, but it should work with left
      right"); if only Up/Down move the target, the horizontal branch did
      not reach Target depth.
- [ ] Fight, an ally-targeting skill (a ward, a heal -- whatever this
      roster has) against two or more party members: Left/Right should
      cycle the party rack too, but the DIRECTION reads the other way from
      the enemy rack on screen -- the party's diagonal is mirrored
      (FightStageAnchors.SlotOffset negates X for it), so Right walks
      TOWARD the near end there (further right on screen) and Left walks
      away from it, the opposite hand from which key goes "deeper" on the
      enemy side. Up/Down on the ally rack read the same way as the enemy
      one (Up deeper, Down nearer) -- only the horizontal hand flips.
- [ ] Fight, mid skill/item submenu (the row list under SKILL or ITEM):
      Left/Right should still do nothing there -- only Up/Down walk that
      list, unchanged by this pass.
- [ ] Mid-fight: START opens the System Menu over the fight (§3's own
      transition case -- confirm Fight's own menu depth is unmoved when you
      leave the System Menu again). Stick to Options, Submit; stick to a
      row, Left/Right to adjust it; Cancel back out to the fight.
- [ ] START, on the owner's own pad, is joystick button 7 -- the whole of
      what the 2026-09-19 call rests on, and unverified here (`AUDIT.md`
      #166). Press it on the hub, on the map and in a fight: each should
      open the menu, and pressing it again should close it. If nothing
      happens on any of the three, the button number is wrong rather than
      the wiring -- the keyboard's escape drives the same axis, so try that
      first to tell the two apart.
- [ ] B (Cancel) on the hub, on the map, and in a fight at its Root depth
      should now do NOTHING. B one level INTO a fight (inside a target pick
      or a submenu) should still step back exactly one level. Both halves
      matter: B doing nothing at Root is the change, and B still stepping
      back is what says the change did not cost anything.
- [ ] DualSense's Options button is believed to enumerate as joystick
      button 9 on Windows, which is the Xbox pad's right-stick click, so it
      is deliberately NOT bound (`AUDIT.md` #166). On a DualSense, expect
      Start/Options to do nothing and say so -- that is a known gap, not a
      new finding.
- [ ] LT/RT (`TriggerLeft`/`TriggerRight`, the 9th and 10th joystick axes):
      on any screen that declares an `INavSectionStrip`, one pull should
      move one section and a HELD trigger should move exactly one (the
      armed edge, `NavigationInputModule.TriggerPressed`). If a pull moves
      the SELECTION instead of a section, the axis numbers landed on the
      sticks -- the same failure mode #166 names for the D-pad. On a
      DualSense L2/R2 are believed to be axes 4/5 and are not bound.
- [ ] Finish or flee the fight the way the game allows on this screen
      (check the verb menu for a Flee/Retreat verb, or simply let the fight
      resolve to victory/defeat) -- confirm the stick reaches whichever one
      the mouse could.
- [ ] Victory: the Reckoning opens -- stick across the offer cards (confirm
      the halo/brightened glow marks the selected one, phase 4 item 1),
      Submit to take one; stick to Continue on the summary, Submit -- back
      to the Hub or the Map, whichever the game returns to.
- [ ] Hub: stick to the Shop building (if unlocked this run), Submit; stick
      through the shelf, Submit on an item -- confirm either a purchase
      completes or a refusal is shown (both are real states, per §2's own
      verification note that Shop has both eligibility shapes); Cancel or
      the pack's own Close control to back out.
- [ ] Hub: stick to the Talents building, Submit -- reaches the Talents
      scene; stick through the tree, Submit to invest a point (or confirm a
      refusal if none are eligible); Cancel back to the Hub.
- [ ] Map: stick to the next reachable node, Submit -- second fight, same
      checklist as the first.
- [ ] Defeat (deliberately lose one run, or force it in a debug build if
      the game has a shortcut for it): stick to Inspect then Return, or
      whichever the Defeat screen offers, Submit.
- [ ] Reward Track (System Menu's own tab, or wherever a level-up routes
      to it): stick along the rail, confirm the ribbon reveals as selection
      moves rather than needing a hover; Submit to collect anything owed.
- [ ] System Menu: stick across every tab (Character & Inventory, Party,
      Options, Main Menu) with the shoulder buttons (`TabPrev`/`TabNext`,
      phase 3 item 2) as well as Move; confirm each tab's own entry is
      selected on arrival.
- [ ] Main Menu / Quit: from the System Menu's own Main Menu tab, Submit to
      leave the run, or reach Quit if the build offers one on this screen.

Anything on this list that a controller cannot reach, or that requires the
mouse to recover from, is a real finding -- record it the way `AUDIT.md`
records every other gap this plan has found, with the exact step and what
happened instead of what was expected.
