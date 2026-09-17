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

        // The Fight branch's own debounce state, moved verbatim from the
        // deleted FightController.Input.cs PollGamepadNavigation. One field
        // is enough because at most one Fight context can be top at a time.
        private bool _fightVerticalArmed = true;

        // ProjectSettings/InputManager.asset's own two new button axes
        // (plan phase 3, item 2): TabPrev binds Q and joystick button 4,
        // TabNext binds E and joystick button 5 -- named constants so a
        // future rebind touches one file rather than a string repeated at
        // every call site (ScriptedBaseInput's own test double included).
        private const string TabPrevButton = "TabPrev";
        private const string TabNextButton = "TabNext";

        protected override void Awake()
        {
            base.Awake();
            Contexts = new NavContextStack();
        }

        public override void Process()
        {
            var topAtStart = Contexts?.Top;

            if (topAtStart != null && topAtStart.IsNonSelecting)
            {
                ProcessFight(topAtStart);
                return;
            }

            base.Process();

            // Empty stack (no context registered anywhere -- e.g. Main Menu
            // in phase 1): base.Process() alone, nothing else reads this
            // frame's input. Plan section 3, last bullet of the "top is not
            // Fight" branch.
            if (topAtStart == null) return;

            // RaiseCancel, not Cancel: the active pane gets first refusal
            // (NavContext.RaiseCancel / INavCancelClaim) -- Party carrying a
            // character spends this press putting them back rather than
            // closing the menu around the player.
            if (input.GetButtonDown(cancelButton)) topAtStart.RaiseCancel();

            // THE SHOULDER SHORTCUT (plan section 7's "Tabs" contract,
            // phase 3 item 2), offered the same way and off the same
            // topAtStart -- a context with no tab strip (INavTabStrip)
            // absorbs this silently (NavContext.RaiseTabStep's own no-op).
            // Read through `input`, never UnityEngine.Input directly, same
            // seam every other value this dispatcher reads goes through
            // (plan section 2).
            if (input.GetButtonDown(TabPrevButton)) topAtStart.RaiseTabStep(-1);
            else if (input.GetButtonDown(TabNextButton)) topAtStart.RaiseTabStep(1);

            // RE-READ HERE, deliberately -- this is not the same "capture
            // once" rule Cancel-dispatch above follows, and conflating the
            // two was a real bug phase 2's own SystemMenu work found. A
            // Cancel that just POPPED topAtStart (SystemMenu closing back to
            // the hub) still has a live NavContext instance sitting in
            // memory with its own Selectables/entry -- reselecting against
            // THAT stale instance would put its last-selected control right
            // back as the active selection the same frame Close() cleared
            // it, because ContainsSelectable/ResolveSelection have no idea
            // the context is no longer on the stack. Reading the CURRENT top
            // instead asks the question this rule is actually for: what
            // does the stack look like NOW, after whatever Cancel/Submit did
            // this call. Safe for the Fight-becomes-top case test 1 guards
            // (section 3): a Fight NavContext's own ContainsSelectable/
            // ResolveSelection are both inert (EmptySelectables, no entry),
            // so this can only ever resolve to null there, never invoke
            // MoveFocus/ConfirmFocus -- those stay exclusively behind
            // IsNonSelecting's branch above, decided from topAtStart same as
            // ever.
            ReselectIfOutsideDeclaredSet(Contexts?.Top);
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
            if (target == null) return;

            float vertical = input.GetAxisRaw(verticalAxis);
            if (Mathf.Abs(vertical) < 0.5f)
            {
                _fightVerticalArmed = true;
            }
            else if (_fightVerticalArmed)
            {
                _fightVerticalArmed = false;
                target.MoveFocus(vertical > 0f ? -1 : 1);
            }

            if (input.GetButtonDown(submitButton)) target.ConfirmFocus();
            if (input.GetButtonDown(cancelButton)) target.OnBackPressed();
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
        private void ReselectIfOutsideDeclaredSet(NavContext top)
        {
            if (top == null) return;

            var current = EventSystem.current.currentSelectedGameObject;
            if (current != null && top.ContainsSelectable(current)) return;

            // NOT guarded on `resolved != null` -- a null resolution is a
            // legitimate answer here too (section 6's "screen-level
            // fallback": select nothing), and it has to actually BE applied,
            // not skipped, or a selection that belonged to a context now
            // gone (a closed modal's last-selected tab) sits there forever:
            // `current` just failed ContainsSelectable against the NEW top,
            // so it is exactly the stale value this rule exists to replace,
            // never a value worth defending by leaving it alone.
            var resolved = top.ResolveSelection() as GameObject;
            EventSystem.current.SetSelectedGameObject(resolved);
        }
    }
}
