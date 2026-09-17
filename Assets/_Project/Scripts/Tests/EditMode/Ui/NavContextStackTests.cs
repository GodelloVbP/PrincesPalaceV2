using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The pure stack behind NavigationInputModule (docs/GAMEPAD_NAVIGATION_PLAN.md
    // section 3-4). Engine-free, so the push/pop/remove-anywhere invariants
    // that decide which frame's input belongs to which screen are provable
    // without a scene, a MonoBehaviour, or an EventSystem.
    public class NavContextStackTests
    {
        private static NavContext Ordinary() => new NavContext(null, null, null);

        [Test]
        public void EmptyStack_TopIsNullAndNothingIsTop()
        {
            var stack = new NavContextStack();

            Assert.IsNull(stack.Top);
            Assert.IsFalse(stack.IsTop(Ordinary()));
            Assert.AreEqual(0, stack.Count);
        }

        [Test]
        public void Push_MakesTheNewestContextTop()
        {
            var stack = new NavContextStack();
            var a = Ordinary();
            var b = Ordinary();

            stack.Push(a);
            Assert.IsTrue(stack.IsTop(a));

            stack.Push(b);
            Assert.IsTrue(stack.IsTop(b));
            Assert.IsFalse(stack.IsTop(a));
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void Pop_RestoresThePreviousTop()
        {
            var stack = new NavContextStack();
            var a = Ordinary();
            var b = Ordinary();
            stack.Push(a);
            stack.Push(b);

            stack.Pop();

            Assert.IsTrue(stack.IsTop(a));
            Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void Pop_OnAnEmptyStack_DoesNothing()
        {
            var stack = new NavContextStack();

            Assert.DoesNotThrow(() => stack.Pop());
            Assert.IsNull(stack.Top);
        }

        [Test]
        public void RemoveAnywhere_ANonTopEntry_LeavesTheCurrentTopUnchanged()
        {
            // A parent hidden or a scene unloading can deactivate a context
            // that is not on top (plan section 4's OnDisable/OnDestroy
            // safety net) -- removing it must not disturb whatever IS top.
            var stack = new NavContextStack();
            var a = Ordinary();
            var b = Ordinary();
            var c = Ordinary();
            stack.Push(a);
            stack.Push(b);
            stack.Push(c);

            stack.Remove(a);

            Assert.IsTrue(stack.IsTop(c));
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void RemoveAnywhere_TheTopEntry_RestoresTheNextOneDown()
        {
            var stack = new NavContextStack();
            var a = Ordinary();
            var b = Ordinary();
            stack.Push(a);
            stack.Push(b);

            stack.Remove(b);

            Assert.IsTrue(stack.IsTop(a));
            Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void RemoveAnywhere_TheOnlyEntry_EmptiesTheStack()
        {
            var stack = new NavContextStack();
            var a = Ordinary();
            stack.Push(a);

            stack.Remove(a);

            Assert.IsNull(stack.Top);
            Assert.AreEqual(0, stack.Count);
        }

        [Test]
        public void RemoveAnywhere_AContextNotOnTheStack_DoesNothing()
        {
            var stack = new NavContextStack();
            var a = Ordinary();
            var untracked = Ordinary();
            stack.Push(a);

            Assert.DoesNotThrow(() => stack.Remove(untracked));
            Assert.IsTrue(stack.IsTop(a));
            Assert.AreEqual(1, stack.Count);
        }
    }
}
