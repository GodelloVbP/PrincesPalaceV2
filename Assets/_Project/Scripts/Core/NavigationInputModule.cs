using UnityEngine;
using UnityEngine.EventSystems;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE ONE DISPATCH POINT. docs/GAMEPAD_NAVIGATION_PLAN.md section 3 is
    // the full argument; this class is its mechanism.
    //
    // Nothing else in the project reads Vertical/Horizontal/Submit/Cancel
    // for UI navigation after this lands -- SceneBuilder.CreateEventSystem
    // attaches this in place of the stock StandaloneInputModule, and
    // EventSystem.Update() calls Process() at most once per engine frame
    // (verified against the uGUI package source, plan section 2), which is
    // what makes "ask the stack once, at entry" safe across a same-frame
    // transition (a Cancel that pops Options while Fight sits underneath
    // cannot also reach Fight's own branch until the NEXT call).
    //
    // topAtStart is captured once per Process() and never re-read mid-call --
    // see the class comment above for why that single capture is the whole
    // fix for the double-drive bug this replaces.
    public class NavigationInputModule : StandaloneInputModule
    {
        // Fresh every time this component wakes, which is once per scene
        // load -- SceneBuilder attaches exactly one of these per scene, and
        // only one scene is ever loaded at a time (Single mode), so a static
        // field scoped to "the current scene's stack" is safe the same way
        // CursorController's own _instance is scoped to "the current
        // cursor" (see that file's header for the precedent this follows).
        // A fresh stack on every load is also the correct behaviour, not
        // just a convenient one: a reloaded scene has no business
        // remembering a context pushed by the one it replaced.
        public static NavContextStack Contexts { get; private set; }

        // ---- the one pad-focus visual (hardware round 1, the owner's visual
        // ---- findings; Core/FocusMarker.cs has the argument) -------------------
        //
        // THE SOURCE OF TRUTH FOR "WHAT HAS FOCUS" IS THIS CLASS, because it
        // is already the one place that settles the selection every frame
        // (ReselectIfOutsideDeclaredSet) and the one place that drives Fight's
        // own focus model (ProcessFight). Every screen used to answer this
        // question for itself with a halo of its own, three screens did not
        // answer it at all, and no two of the answers looked alike.
        //
        // The marker itself is a scene root fixture SceneBuilder builds
        // beside this EventSystem (Core/FocusMarker.cs's own header argues
        // why it is neither a runtime object nor a screen-tree node), and
        // this is the wire between them. Null is a supported state, not a
        // broken one: a PlayMode fixture that stands up its own EventSystem
        // by hand has no marker, and every call below is then a no-op.

        // WHICH DEVICE MOVED THE FOCUS LAST. The marker is a PAD affordance:
        // a mouse click selects the control it lands on, and an arrow jumping
        // to wherever the pointer last clicked is noise on a screen the player
        // is driving with the pointer.
        //
        // Static and NOT reset on scene load, deliberately -- this is a fact
        // about the player's hands, not about a scene. Resetting it in Awake
        // would show a mouse player the marker on every screen's entry control
        // for the frames between the load and their next mouse move, on every
        // transition. Tests reset it through TestGlobals.ResetAll.
        //
        // Starts TRUE so that a pad player who has not yet pressed anything on
        // a freshly launched game still sees where the focus is; the first
        // mouse movement of a mouse player's session clears it and it stays
        // clear.
        public static bool LastInputWasPad { get; private set; } = true;

        // The control the marker is pointing at this frame, or null. Exposed
        // for the tests, which assert against the literal node rather than
        // against the marker's own arithmetic.
        public static RectTransform FocusTarget { get; private set; }

        [SerializeField] internal FocusMarker focusMarker;

        // The marker belonging to the CURRENT scene's dispatcher. Static for
        // the same reason Contexts is (one EventSystem per scene, one scene
        // loaded at a time), and set from the serialized field in Awake so a
        // reloaded scene replaces the previous scene's destroyed one rather
        // than keeping a fake-null reference to it.
        public static FocusMarker Marker { get; private set; }

        private Vector2 _lastMousePosition;
        private bool _mousePositionKnown;

        public static void ResetInputDeviceForTests()
        {
            LastInputWasPad = true;
            FocusTarget = null;
        }

        // The Fight branch's own debounce state, moved verbatim from the
        // deleted FightController.Input.cs PollGamepadNavigation. One field
        // is enough because at most one Fight context can be top at a time.
        private bool _fightVerticalArmed = true;

        // The horizontal twin, armed and spent by exactly the same rule --
        // one flick, one step, and no step again until the axis has been back
        // below MoveThreshold. Its own field rather than a shared one because
        // the two axes are pushed independently and a stick held right while
        // being flicked up must not have the up-flick swallowed by the right
        // one's spent edge.
        private bool _fightHorizontalArmed = true;

        // HOW FAR THE STICK HAS TO GO BEFORE A PRESS COUNTS, for BOTH
        // branches. The Fight branch has always had an armed edge of its own
        // at this value; the ordinary branch had none and simply let
        // StandaloneInputModule's analog repeat machinery decide, which is
        // two answers to one question and is what the hardware play-test
        // found ("the joystick only is wonky... it feels almost random").
        public const float MoveThreshold = 0.5f;

        // THE ORDINARY BRANCH'S OWN ARMED EDGE -- one flick, one Move.
        //
        // What the stock path did instead, measured against this project's
        // own ProjectSettings/InputManager.asset (joystick Horizontal/
        // Vertical, type Joystick Axis, dead 0.19) and uGUI's own source:
        // BaseInputModule.DetermineMoveDirection answers None below a
        // magnitude of 0.6 and SendMoveEventToSelectedObject then sets
        // m_ConsecutiveMoveCount = 0 WITHOUT touching m_PrevActionTime. So a
        // stick whose rest position creeps across that 0.6 line -- any worn
        // or slightly off-centre pad, since 0.19 is all the dead zone it gets
        // before the module sees it -- never accumulates a consecutive move,
        // never arms the 0.5s repeat DELAY, and therefore repeats at the bare
        // 1/inputActionsPerSecond rate, ten selections a second, off a stick
        // nobody is touching. That is the "almost random" the owner saw, and
        // it is also why one deliberate flick could produce two Moves: hold
        // the stick past 0.5s and the repeat delay expires mid-flick.
        //
        // The rule here is the one the Fight branch already shipped: the
        // press has to return below MoveThreshold before another one counts.
        // COST, STATED RATHER THAN HIDDEN: a held stick no longer auto-
        // repeats at all. That is a real loss on a long list (the reward
        // track's rail, the dossier's pack) and it is reversible -- but
        // predictable-and-slower beat unpredictable in the owner's own
        // report, and one rule across both branches beats two.
        private bool _moveArmed = true;
        private bool _movePassesThisFrame;

        // ProjectSettings/InputManager.asset's own two new button axes
        // (plan phase 3, item 2): TabPrev binds Q and joystick button 4,
        // TabNext binds E and joystick button 5 -- named constants so a
        // future rebind touches one file rather than a string repeated at
        // every call site (ScriptedBaseInput's own test double included).
        private const string TabPrevButton = "TabPrev";
        private const string TabNextButton = "TabNext";
        private const string CharacterSelectButton = "Jump";

        // THE OVERARCHING MENU'S OWN BUTTON -- Start on a pad, not B (the
        // owner's 2026-09-19 call). InputManager.asset binds escape and
        // joystick button 7 to it. Escape is ALSO Cancel's positiveButton,
        // which is the whole reason the dispatch below spends a frame on one
        // of the two and never on both.
        private const string SystemMenuButton = "SystemMenu";

        // The triggers, bound as AXES rather than buttons because an analog
        // control has no digital edge for GetButtonDown to report at all.
        private const string TriggerLeftAxis = "TriggerLeft";
        private const string TriggerRightAxis = "TriggerRight";

        // HOW FAR A TRIGGER HAS TO TRAVEL BEFORE A PULL COUNTS. Deliberately
        // its own number rather than MoveThreshold, which happens to share
        // the value: that one answers "how far has the stick been pushed",
        // and tuning it against a worn stick has no business silently
        // retuning how hard a trigger has to be squeezed.
        public const float TriggerThreshold = 0.5f;

        // One armed edge per trigger, the stick's own rule (f6672a26)
        // applied to a control the legacy input system reports only as a
        // level: a pull counts once and does not count again until the
        // trigger has been back below TriggerThreshold.
        private bool _triggerLeftArmed = true;
        private bool _triggerRightArmed = true;

        protected override void Awake()
        {
            base.Awake();
            Contexts = new NavContextStack();
            Marker = focusMarker;
        }

        public override void Process()
        {
            var topAtStart = Contexts?.Top;

            TrackInputDevice();

            if (topAtStart != null && topAtStart.IsNonSelecting)
            {
                // Fight drives its own armed edge below and never wants
                // base.Process()'s move dispatch, whose target is null there
                // anyway -- so the ordinary gate stays shut for this call.
                _movePassesThisFrame = false;
                ProcessFight(topAtStart);
                return;
            }

            UpdateMoveGate();

            // CAPTURED BEFORE base.Process() RUNS -- phase 4 item 4's own
            // mixed-input pass found that this is load-bearing, not
            // defensive. PointerInputModule.DeselectIfSelectionChanged nulls
            // the CURRENT selection the instant a click's `currentOverGo`
            // resolves to anything without an ISelectHandler ancestor
            // matching it -- and that includes a click that lands on
            // nothing (a background click, section 3/6's own "(a)") AND a
            // click on a Navigation.Mode.None Selectable (the stepper
            // buttons, section 7's own contract: "does nothing to the row's
            // own remembered focus"). Mode.None only stops the CLICKED
            // target from being reselected (Selectable.OnPointerDown's own
            // guard) -- it does nothing to stop the PREVIOUS selection from
            // being deselected first, which is a real gap the mouse-only
            // and mixed-input tests actually exercising a raycasted click
            // (rather than calling a handler directly) were the first to
            // catch. See ReselectIfOutsideDeclaredSet's own comment for how
            // this is used.
            var selectedBeforeDispatch = EventSystem.current.currentSelectedGameObject;

            base.Process();

            // Empty stack (no context registered anywhere -- e.g. Main Menu
            // in phase 1): base.Process() alone, nothing else reads this
            // frame's input. Plan section 3, last bullet of the "top is not
            // Fight" branch.
            //
            // The marker still updates before returning. A screen with no
            // NavContext is still a screen a pad can move around -- Unity's
            // own Explicit links do the moving there -- and "no context" is
            // not "no focus".
            if (topAtStart == null)
            {
                ShowFocusOn(EventSystem.current.currentSelectedGameObject);
                return;
            }

            // START, NOT B -- and it takes this frame's Cancel with it when
            // it lands.
            //
            // One keyboard key drives both axes: escape is SystemMenu's
            // positiveButton and Cancel's alike. Without this guard the same
            // press would open the menu on a root screen and then hand a
            // Cancel to topAtStart -- which is the root, whose Cancel is now
            // nothing, but on the MENU's own context is Close, so Escape
            // would open and shut it inside one frame.
            //
            // RaiseSystemMenu answering whether the context HAD a handler is
            // the only thing that tells the two cases apart, and it is why
            // this is not simply `if (menu) else if (cancel)`: a modal --
            // the glossary, the debug menu, the relic draft -- declares no
            // systemMenu handler, absorbs the press, and must still get its
            // Cancel, or Escape would stop closing it.
            bool systemMenuHandled = input.GetButtonDown(SystemMenuButton) && topAtStart.RaiseSystemMenu();

            // RaiseCancel, not Cancel: the active pane gets first refusal
            // (NavContext.RaiseCancel / INavCancelClaim) -- Party carrying a
            // character spends this press putting them back rather than
            // closing the menu around the player.
            if (!systemMenuHandled && input.GetButtonDown(cancelButton)) topAtStart.RaiseCancel();

            // THE SHOULDER SHORTCUT (INavSectionStrip) -- owner's 2026-09-19
            // hardware-round call: LB/RB page sections/characters (dossier
            // paging, StepCharacter on the talent screen), not tabs. Offered
            // off the same topAtStart Cancel is -- a context with no
            // section strip (INavSectionStrip) absorbs this silently
            // (NavContext.RaiseSectionStep's own no-op). Read through
            // `input`, never UnityEngine.Input directly, same seam every
            // other value this dispatcher reads goes through (plan section
            // 2).
            if (input.GetButtonDown(TabPrevButton)) topAtStart.RaiseSectionStep(-1);
            else if (input.GetButtonDown(TabNextButton)) topAtStart.RaiseSectionStep(1);

            // THE TRIGGER SHORTCUT (INavTabStrip) -- owner's 2026-09-19
            // hardware-round call: LT/RT step the tab strip (system-menu
            // tabs, talent constellations/paths), beside the shoulders and
            // absorbed the same way by a context that declares no tab strip.
            //
            // Both edges are READ before either is spent, unlike the
            // shoulders' plain if/else: TriggerPressed re-arms as well as
            // reports, so short-circuiting past the second call would leave
            // a trigger that was held through this frame armed the instant
            // it is released. At most one step still dispatches -- two
            // directions of one axis of navigation, and a frame carrying
            // both would otherwise step there and straight back.
            bool leftPulled = TriggerPressed(TriggerLeftAxis, ref _triggerLeftArmed);
            bool rightPulled = TriggerPressed(TriggerRightAxis, ref _triggerRightArmed);
            if (leftPulled) topAtStart.RaiseTabStep(-1);
            else if (rightPulled) topAtStart.RaiseTabStep(1);

            // RE-READ HERE, deliberately -- this is not the same "capture
            // once" rule Cancel-dispatch above follows, and conflating the
            // two was a real bug phase 2's own SystemMenu work found. A
            // Cancel that just POPPED topAtStart (SystemMenu closing back to
            // the hub) still has a live NavContext instance sitting in
            // memory with its own Selectables/entry -- reselecting against
            // THAT stale instance would put its last-selected control right
            // back as the active selection the same frame Close() cleared
            // it, because ContainsSelectable/SelectionFor have no idea
            // the context is no longer on the stack. Reading the CURRENT top
            // instead asks the question this rule is actually for: what
            // does the stack look like NOW, after whatever Cancel/Submit did
            // this call. Safe for the Fight-becomes-top case test 1 guards
            // (section 3): a Fight NavContext's own ContainsSelectable/
            // RememberedSelectable are both inert (EmptySelectables, no
            // entry), so this can only ever resolve to null there, never
            // invoke MoveFocus/ConfirmFocus -- those stay exclusively behind
            // IsNonSelecting's branch above, decided from topAtStart same as
            // ever.
            var topAtEnd = Contexts?.Top;
            ReselectIfOutsideDeclaredSet(topAtEnd, selectedBeforeDispatch);

            // FOCUS MEMORY IS THE DISPATCHER'S JOB, not each screen's (plan
            // sections 4 and 6, AUDIT.md #163's second half). Before this,
            // NavContext.Remember was called from nowhere at all, so
            // "remembered ?? entry" was only ever entry and a context popped
            // and re-entered always landed on its entry rather than where the
            // player left it.
            //
            // Recorded HERE, after the reselection rule above has settled the
            // frame, so what gets remembered is the selection this context
            // actually ends the call with -- never a mid-call value a
            // click nulled and this rule then restored. Recorded against
            // topAtEnd, the same context the reselection was resolved
            // against: a Submit that just pushed a modal remembers the
            // modal's own entry on the MODAL, and leaves the screen
            // underneath remembering where its focus was.
            RememberSelection(topAtEnd);

            // LAST, off the settled selection -- the same value
            // RememberSelection just recorded, so the marker and the memory
            // can never disagree about what this frame focused.
            ShowFocusOn(EventSystem.current.currentSelectedGameObject);
        }

        // WHICH DEVICE IS DRIVING, read once a frame off the same `input`
        // seam every other value in this class goes through (plan section 2),
        // so ScriptedBaseInput can prove it.
        //
        // PAD WINS A TIE. A frame carrying both a pointer movement and a
        // stick past the threshold is a player with a hand on the pad, and
        // the sub-pixel drift a real mouse produces while sitting still is
        // exactly the kind of thing that would otherwise flicker the marker
        // off mid-Move.
        //
        // The axis is read here as a LEVEL rather than waiting for a
        // dispatched Move, on purpose: the ordinary branch's armed edge
        // (_moveArmed) can swallow a press the player definitely made, and a
        // marker that only appears on presses the gate accepted would be
        // invisible for exactly the frames the player is wondering where the
        // focus went.
        private void TrackInputDevice()
        {
            var mouse = input.mousePosition;
            if (_mousePositionKnown && (mouse - _lastMousePosition).sqrMagnitude > 0.01f)
            {
                LastInputWasPad = false;
            }

            _lastMousePosition = mouse;
            _mousePositionKnown = true;

            if (input.GetMouseButtonDown(0)) LastInputWasPad = false;

            float horizontal = input.GetAxisRaw(horizontalAxis);
            float vertical = input.GetAxisRaw(verticalAxis);
            if (horizontal * horizontal + vertical * vertical >= MoveThreshold * MoveThreshold)
            {
                LastInputWasPad = true;
            }

            if (input.GetButtonDown(submitButton) || input.GetButtonDown(cancelButton)
                || input.GetButtonDown(TabPrevButton) || input.GetButtonDown(TabNextButton)
                || input.GetButtonDown(SystemMenuButton))
            {
                LastInputWasPad = true;
            }

            // Read as LEVELS, for the reason the stick above is: the armed
            // edge can swallow a pull the player definitely made, and a
            // marker that vanished on exactly those frames would be missing
            // when the player is most likely to be looking for it.
            if (Mathf.Abs(input.GetAxisRaw(TriggerLeftAxis)) >= TriggerThreshold
                || Mathf.Abs(input.GetAxisRaw(TriggerRightAxis)) >= TriggerThreshold)
            {
                LastInputWasPad = true;
            }
        }

        // ONE PULL, ONE STEP. Arming is updated on every call, whether or
        // not the caller spends the edge -- see the caller's own comment on
        // why both triggers are asked before either is answered.
        //
        // Abs, though the per-trigger axes InputManager.asset binds read
        // 0..1: a driver that hands back the SIGNED combined-trigger axis
        // under these names should still produce one step per pull rather
        // than none in one direction.
        private bool TriggerPressed(string axisName, ref bool armed)
        {
            if (Mathf.Abs(input.GetAxisRaw(axisName)) < TriggerThreshold)
            {
                armed = true;
                return false;
            }

            if (!armed) return false;

            armed = false;
            return true;
        }

        // THE ONE PLACE THE MARKER IS TOLD ANYTHING. Both branches end here,
        // with whatever each of them calls focus: an EventSystem selection in
        // the ordinary branch, Fight's own focused element in the other.
        //
        // A scene with no marker (a hand-built PlayMode EventSystem) still
        // records FocusTarget -- the fact of what has focus is the
        // dispatcher's, and only the drawing of it needs the fixture.
        private void ShowFocusOn(GameObject focused)
        {
            var rect = LastInputWasPad && focused != null && focused.activeInHierarchy
                ? focused.transform as RectTransform
                : null;

            FocusTarget = rect;

            if (Marker == null) return;

            if (rect == null)
            {
                Marker.Hide();
                return;
            }

            Marker.PointAt(rect);
        }

        // READ ONCE A FRAME, BEFORE base.Process() DISPATCHES ANYTHING.
        //
        // It has to be here rather than inside the GetAxisEventData override
        // below, because the frames that RE-ARM the gate are exactly the
        // frames base.Process() never reaches that override on: a neutral
        // stick makes SendMoveEventToSelectedObject return at its own
        // "movement is approximately zero" guard, and a move the real-time
        // repeat gate refuses returns before it too. Asking the axes
        // ourselves, unconditionally, is the only reading that sees every
        // frame.
        //
        // Through `input`, never UnityEngine.Input -- the same seam every
        // other value this dispatcher reads goes through (plan section 2),
        // which is what makes ScriptedBaseInput able to prove this at all.
        private void UpdateMoveGate()
        {
            float horizontal = input.GetAxisRaw(horizontalAxis);
            float vertical = input.GetAxisRaw(verticalAxis);

            // sqrMagnitude against the squared threshold, the SAME comparison
            // BaseInputModule.DetermineMoveDirection makes, so the frame this
            // says "far enough" is exactly the frame that resolves to a real
            // MoveDirection rather than None -- never one that arms the gate
            // and then dispatches nothing.
            if (horizontal * horizontal + vertical * vertical < MoveThreshold * MoveThreshold)
            {
                _moveArmed = true;
                _movePassesThisFrame = false;
                return;
            }

            _movePassesThisFrame = _moveArmed;
        }

        // WHERE THE GATE IS ACTUALLY SPENT. BaseInputModule's own virtual,
        // called by StandaloneInputModule.SendMoveEventToSelectedObject and
        // by nothing else -- so overriding it is how this module says "not
        // this frame" without reimplementing the whole move path or
        // switching eventSystem.sendNavigationEvents off and taking Submit
        // down with it.
        //
        // _moveArmed is cleared HERE and not in UpdateMoveGate, deliberately:
        // by the time this runs, base has already cleared its own real-time
        // repeat gate, so this is the first point at which a Move is
        // certainly about to be dispatched. Clearing it a step earlier would
        // spend one flick on a press the repeat gate then swallowed, and the
        // player would have to centre the stick and try again for no visible
        // reason.
        //
        // MoveThreshold is passed down in place of the caller's own 0.6 so
        // there is ONE number in this file rather than Unity's and ours
        // disagreeing about the same edge.
        protected override AxisEventData GetAxisEventData(float x, float y, float moveDeadZone)
        {
            if (!_movePassesThisFrame) return base.GetAxisEventData(0f, 0f, moveDeadZone);

            _moveArmed = false;
            return base.GetAxisEventData(x, y, MoveThreshold);
        }

        // THE ONE PLACE section 4's "remembered ?? entry" is decided.
        //
        // NavContext states the half it can (RememberedSelectable: the
        // remembered node if its id is still declared) and deliberately not
        // the half it cannot -- whether that node is still usable is an
        // engine question, and a Domain type has no business asking it
        // (docs/CODE_STANDARDS.md section 1). Hidden or destroyed both fall
        // back to the entry: Talent's invest button and RewardTrack's collect
        // button are the two controls in this project that genuinely vanish
        // on a state change, and a remembered selection pointing at one of
        // them after it went away is a focus the player cannot see and cannot
        // move off.
        //
        // `remembered != null` is Unity's own null, which is the destroyed
        // case -- a GameObject destroyed by a repaint is still a live managed
        // reference in the Selectables dictionary, so this comparison, not a
        // pattern match, is what catches it.
        public static GameObject SelectionFor(NavContext context)
        {
            if (context == null) return null;

            var remembered = context.RememberedSelectable() as GameObject;
            if (Usable(remembered)) return remembered;

            return context.Entry as GameObject;
        }

        // SHOWN AND NOT DESTROYED -- the one engine-level question every
        // selection decision above asks, in one place.
        //
        // `go != null` is Unity's own operator on purpose, not a pattern
        // match or ReferenceEquals: a GameObject destroyed by a repaint is
        // still a live managed reference wherever a dictionary or a captured
        // local holds it, and this comparison is the only one that reports it
        // as gone.
        private static bool Usable(GameObject go) => go != null && go.activeInHierarchy;

        // A null selection is NOT forgetting. A frame that ends with nothing
        // selected is either Fight's own steady state or section 6's
        // screen-level fallback (every node in the entry's group gone), and in
        // both cases the last node this context did hold is still the right
        // answer for when it is entered again -- overwriting it with "nothing"
        // would turn every transient empty frame into an erased memory.
        //
        // A selection this context does not declare is not forgotten either:
        // IdOf answers null and nothing is written. That is the case of a
        // screen whose own context sits under a modal while something else
        // owns the selection.
        private static void RememberSelection(NavContext top)
        {
            if (top == null || top.IsNonSelecting) return;

            var current = EventSystem.current.currentSelectedGameObject;
            if (current == null) return;

            var id = top.IdOf(current);
            if (id != null) top.Remember(id);
        }

        // Five steps, nothing else runs this call -- plan section 3.
        private void ProcessFight(NavContext top)
        {
            // Every frame, unconditionally: selection is null BECAUSE this
            // asserts it every frame, defeating a stale modal selection, an
            // Explicit link, or a stray programmatic call by brute force,
            // not by trusting Navigation.Mode.None (which stays on Fight's
            // Buttons only as a defensive second layer, plan section 3).
            EventSystem.current.SetSelectedGameObject(null);

            // Verified safe against the base implementation: with selection
            // null, SendSubmitEventToSelectedObject returns immediately and
            // SendMoveEventToSelectedObject resolves through a null-target
            // no-op -- mouse/click still runs (Fight's own Buttons need it),
            // Move/Submit dispatch is structurally inert. 3 BEFORE 4: a
            // future Fight verb that selects an EventSystem object
            // synchronously must not also receive this same call's still-true
            // Submit -- see test 2 and the plan's own worked example.
            base.Process();

            var target = top.FightTarget;
            if (target == null)
            {
                ShowFocusOn(null);
                return;
            }

            // BOTH AXES, THROUGH THE SAME TWO AXIS NAMES THE ORDINARY BRANCH
            // READS. "Horizontal" and "Vertical" each have three entries in
            // ProjectSettings/InputManager.asset -- the keyboard's arrows, the
            // left stick, and the D-pad hat (its own `axis: 5`/`axis: 6`
            // entries, the pad's 6th and 7th -- hardware round 1 item 4) --
            // and legacy Input.GetAxisRaw answers with the largest of them
            // under one name, so asking once is asking all three. Nothing
            // here needs to know which device moved.
            float vertical = input.GetAxisRaw(verticalAxis);
            float horizontal = input.GetAxisRaw(horizontalAxis);

            // Re-arming is unconditional and per axis: a frame that spends
            // neither edge must still be able to re-arm the axis that went
            // slack, or a diagonal released one axis at a time would leave the
            // other permanently spent.
            if (Mathf.Abs(vertical) < MoveThreshold) _fightVerticalArmed = true;
            if (Mathf.Abs(horizontal) < MoveThreshold) _fightHorizontalArmed = true;

            // ONE AXIS PER FLICK, THE DOMINANT ONE -- the rule
            // BaseInputModule.DetermineMoveDirection applies for us in the
            // ordinary branch and that this branch has to apply for itself,
            // because it reads the axes directly. Without it a diagonal push
            // would step the verb column AND step onto the actor row on the
            // same frame, which is two moves the player made one gesture for.
            // Vertical wins a tie, which is what a purely vertical push has
            // always resolved to here.
            bool verticalLeads = Mathf.Abs(vertical) >= Mathf.Abs(horizontal);

            if (verticalLeads && Mathf.Abs(vertical) >= MoveThreshold && _fightVerticalArmed)
            {
                _fightVerticalArmed = false;
                target.MoveFocus(vertical > 0f ? -1 : 1);
            }
            else if (!verticalLeads && Mathf.Abs(horizontal) >= MoveThreshold && _fightHorizontalArmed)
            {
                _fightHorizontalArmed = false;
                target.InspectMove(horizontal > 0f ? 1 : -1);
            }

            // START REACHES THE MENU FROM INSIDE A FIGHT TOO, off the same
            // axis and the same NavContext handler the hub and the map use
            // -- Fight's context carries one now (NavContext.ForFight's own
            // systemMenu argument), so there is one read of this button in
            // this class rather than a second one wired through
            // IFightNavigationTarget.
            //
            // Guarded the same way the ordinary branch is, and here it is
            // not merely tidy: escape drives both axes, and a fight sitting
            // at a submenu depth would otherwise open the menu AND step back
            // a level on one keypress. B keeps its one fight meaning -- step
            // back -- which at Root is now nothing at all.
            bool systemMenuHandled = input.GetButtonDown(SystemMenuButton) && top.RaiseSystemMenu();

            if (input.GetButtonDown(CharacterSelectButton)) target.EnterCharacterSelect();
            if (input.GetButtonDown(submitButton)) target.ConfirmFocus();
            if (!systemMenuHandled && input.GetButtonDown(cancelButton)) target.OnBackPressed();

            // AFTER the three, so the marker lands on wherever this frame's
            // press left the focus rather than a frame behind it -- a Submit
            // that opens the Skill submenu moves the focus from a verb to a
            // row in the same call.
            //
            // FightTarget.FocusedElement, not EventSystem selection: Fight
            // asserts the selection null every frame by construction (see the
            // top of this method), so the EventSystem has nothing to say here
            // and never will. Fight's own model is the only thing that knows.
            ShowFocusOn(target.FocusedElement as GameObject);
        }

        // The ordinary-context twin of the Fight branch's null-assert --
        // one rule for three cases (plan section 3): a background click
        // nulling selection, a mouse click on a Selectable a modal should
        // have blocked (defence in depth; modals already block the raycast,
        // plan section 2), and a stray/programmatic SetSelectedGameObject.
        //
        // `top` is the CURRENT top (Contexts?.Top at the point this is
        // called, not topAtStart -- see the caller's own comment), so a
        // Cancel that popped the stack down to nothing lands here too: null
        // is a legitimate answer, meaning nothing declares a set to enforce
        // right now, not an error.
        private void ReselectIfOutsideDeclaredSet(NavContext top, GameObject selectedBeforeDispatch)
        {
            if (top == null) return;

            // USABLE, not merely non-null and declared. A node that was
            // hidden or destroyed while it held the focus is still a member
            // of the declared set (a controller declares what it owns, not
            // what happens to be on screen -- SystemMenuController.
            // RefreshSelectables says so in its own header), so the old
            // membership-only test left the focus standing on a control the
            // player can neither see nor move off. Plan section 6 already
            // states the rule this closes: "if the focused node vanishes
            // mid-session: the group's next-nearest eligible node, or the
            // entry if none remain."
            var current = EventSystem.current.currentSelectedGameObject;
            if (Usable(current) && top.ContainsSelectable(current)) return;

            // PREFER WHAT WAS SELECTED A MOMENT AGO, if the SAME top context
            // still declares it -- item 4's own mixed-input finding (see
            // Process()'s own comment on `selectedBeforeDispatch`). This is
            // deliberately narrower than "never let selection go stale":
            // `top.ContainsSelectable` is false for a node that belonged to
            // a context this frame just POPPED OUT FROM UNDER (Debug menu's
            // own Cancel-closes-and-reselects-the-gate case,
            // DebugMenuGamepadNavigationTests' own pinned claim, still true
            // here -- the popped context's last-selected row is never a
            // member of the NEW top's declared set) or PUSHED fresh over
            // (a modal's own entry is chosen by SelectionFor below, not
            // by whatever the screen underneath happened to have selected).
            // It only fires for the one case those two are not: the SAME
            // context, still top, whose current selection was nulled by a
            // click that never replaced it with anything -- a background
            // click, or a click on a Navigation.Mode.None Selectable.
            if (Usable(selectedBeforeDispatch) && top.ContainsSelectable(selectedBeforeDispatch))
            {
                EventSystem.current.SetSelectedGameObject(selectedBeforeDispatch);
                return;
            }

            // NOT guarded on `resolved != null` -- a null resolution is a
            // legitimate answer here too (section 6's "screen-level
            // fallback": select nothing), and it has to actually BE applied,
            // not skipped, or a selection that belonged to a context now
            // gone (a closed modal's last-selected tab) sits there forever:
            // `current` just failed ContainsSelectable against the NEW top,
            // so it is exactly the stale value this rule exists to replace,
            // never a value worth defending by leaving it alone.
            //
            // SelectionFor, not NavContext's own half of it: this is the pop
            // path as well as the push path (a Cancel that closed a modal
            // lands here with the screen underneath as `top`), and both want
            // the same answer -- where that context left off if it is still
            // there to go back to, its entry otherwise.
            var resolved = SelectionFor(top);
            EventSystem.current.SetSelectedGameObject(resolved);
        }
    }
}
