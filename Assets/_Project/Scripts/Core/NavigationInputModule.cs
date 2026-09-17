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

            if (input.GetButtonDown(cancelButton)) topAtStart.Cancel?.Invoke();

            ReselectIfOutsideDeclaredSet(topAtStart);
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
        private void ReselectIfOutsideDeclaredSet(NavContext top)
        {
            var current = EventSystem.current.currentSelectedGameObject;
            if (current != null && top.ContainsSelectable(current)) return;

            var resolved = top.ResolveSelection() as GameObject;
            if (resolved != null) EventSystem.current.SetSelectedGameObject(resolved);
        }
    }
}
