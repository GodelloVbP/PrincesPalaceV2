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

        public int Count => _stack.Count;

        public NavContext Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public bool IsTop(NavContext context) => context != null && ReferenceEquals(Top, context);

        public void Push(NavContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _stack.Add(context);
        }

        // Pops whatever is currently on top -- the ordinary Close() path,
        // which is confident it IS top.
        public void Pop()
        {
            if (_stack.Count == 0) return;
            _stack.RemoveAt(_stack.Count - 1);
        }

        // Removes a context wherever it sits, top or not -- the
        // OnDisable/OnDestroy safety net plan section 4 calls for: a parent
        // hidden or a scene unloading can deactivate a context that never
        // got a chance to Close() first, and is not necessarily top when it
        // does. Removing a non-top entry changes nothing about the current
        // top's selection; removing the top behaves exactly like Pop().
        public void Remove(NavContext context) => _stack.Remove(context);
    }
}
