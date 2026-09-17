using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The one navigation-context stack a scene's NavigationInputModule reads
    // from every frame (docs/GAMEPAD_NAVIGATION_PLAN.md sections 3-4). Pure
    // C# on purpose: which context is on top decides what a frame's input
    // means, and that decision should not need a scene, a MonoBehaviour, or
    // an EventSystem to test.
    public sealed class NavContextStack
    {
        private readonly List<NavContext> _stack = new List<NavContext>();

        // WHEN THE SHAPE OF THE STACK CHANGED, for a screen that has to react
        // to something going up over it rather than to its own state
        // (docs/GAMEPAD_NAVIGATION_PLAN.md section 7's tooltip rule: a modal
        // pushed above force-closes a showing tooltip, and the screen's own
        // context being popped hides it).
        //
        // An EVENT rather than a per-frame "am I still top" poll, and that is
        // the plan's own reason for existing: gating readers on a question
        // they ask themselves every frame is Draft 2's rejected approach
        // (section 1). It carries no argument -- every subscriber so far
        // wants the same thing, "is Top still what it was when I decided
        // something", and Top is right here to be read.
        public event System.Action Changed;

        public int Count => _stack.Count;

        public NavContext Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public bool IsTop(NavContext context) => context != null && ReferenceEquals(Top, context);

        public void Push(NavContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _stack.Add(context);
            Changed?.Invoke();
        }

        // Pops whatever is currently on top -- the ordinary Close() path,
        // which is confident it IS top.
        public void Pop()
        {
            if (_stack.Count == 0) return;
            _stack.RemoveAt(_stack.Count - 1);
            Changed?.Invoke();
        }

        // Removes a context wherever it sits, top or not -- the
        // OnDisable/OnDestroy safety net plan section 4 calls for: a parent
        // hidden or a scene unloading can deactivate a context that never
        // got a chance to Close() first, and is not necessarily top when it
        // does. Removing a non-top entry changes nothing about the current
        // top's selection; removing the top behaves exactly like Pop().
        // Fires Changed only when something was actually removed -- Remove is
        // called from OnDisable safety nets that run whether or not the
        // context is still on the stack, and a notification for a no-op
        // would have a subscriber reacting to nothing.
        public void Remove(NavContext context)
        {
            if (_stack.Remove(context)) Changed?.Invoke();
        }
    }
}
