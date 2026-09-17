using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHO OWNS THE ONE TOOLTIP BOX, one case per rule
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 7's tooltip contract, phase
    // 3b job 1). Every rule in TooltipFocus' own header is one test here, in
    // the same order, because the interesting half of this component is the
    // combinations -- a hover and a selection both existing is the ordinary
    // state of a player who touched the mouse once, and it is the state
    // neither screen's hover handler could have decided on its own.
    //
    // Nodes are bare objects: the class compares handles by reference and
    // nothing else, so a scene would add nothing but cost.
    public class TooltipFocusTests
    {
        private readonly object _a = new object();
        private readonly object _b = new object();

        private TooltipFocus _focus;

        [SetUp]
        public void Fresh() => _focus = new TooltipFocus();

        [Test]
        public void SelectionLandsOnANode_ShowsThatNode()
        {
            var change = _focus.Selection(_a, selected: true);

            Assert.AreSame(_a, change.Enter, "selecting a node should show its tooltip");
            Assert.IsNull(change.Leave, "nothing was showing to leave");
            Assert.AreSame(_a, _focus.Shown);
        }

        [Test]
        public void PointerOnlyEnter_StillShows_SoTheMousePathIsUnchanged()
        {
            var change = _focus.Pointer(_a, entered: true);

            Assert.AreSame(_a, change.Enter);
            Assert.AreSame(_a, _focus.Shown);
        }

        [Test]
        public void PointerEntersAnotherNodeWhileANodeIsSelected_FocusWins()
        {
            _focus.Selection(_a, selected: true);

            var change = _focus.Pointer(_b, entered: true);

            Assert.IsFalse(change.Any,
                "the hovered node must not replace the selected node's tooltip -- focus outranks hover");
            Assert.AreSame(_a, _focus.Shown);
        }

        [Test]
        public void PointerEntersTheNodeAlreadyShowing_ChangesNothing()
        {
            _focus.Selection(_a, selected: true);

            var change = _focus.Pointer(_a, entered: true);

            Assert.IsFalse(change.Any, "it is already up; re-showing it would rebuild the box for nothing");
        }

        [Test]
        public void PointerLeavesAnotherNodeWhileANodeIsSelected_DoesNotHideIt()
        {
            _focus.Selection(_a, selected: true);
            _focus.Pointer(_b, entered: true);

            var change = _focus.Pointer(_b, entered: false);

            Assert.IsFalse(change.Any,
                "the mouse wandering off an unrelated card must not close the box the stick is holding open");
            Assert.AreSame(_a, _focus.Shown);
        }

        [Test]
        public void SelectionMovesToAnotherNode_LeavesTheOldOneAndEntersTheNew()
        {
            _focus.Selection(_a, selected: true);

            // Unity's own order: the new node reports selected, then the old
            // one reports deselected.
            var change = _focus.Selection(_b, selected: true);

            Assert.AreSame(_a, change.Leave, "the outgoing node has to be told, or its highlight outlives it");
            Assert.AreSame(_b, change.Enter);

            var stale = _focus.Selection(_a, selected: false);

            Assert.IsFalse(stale.Any,
                "a deselect arriving AFTER the next node reported itself selected must not close the new box");
            Assert.AreSame(_b, _focus.Shown);
        }

        [Test]
        public void SelectionLeavesWithThePointerElsewhere_Hides()
        {
            _focus.Selection(_a, selected: true);

            var change = _focus.Selection(_a, selected: false);

            Assert.AreSame(_a, change.Leave);
            Assert.IsNull(change.Enter, "nothing else claims it");
            Assert.IsNull(_focus.Shown);
        }

        [Test]
        public void SelectionLeavesWhileThePointerIsOnThatSameNode_TheBoxStaysForTheMouse()
        {
            _focus.Pointer(_a, entered: true);
            _focus.Selection(_a, selected: true);

            var change = _focus.Selection(_a, selected: false);

            Assert.IsFalse(change.Any,
                "the pointer is still on it, so this is today's mouse behaviour and nothing should move");
            Assert.AreSame(_a, _focus.Shown);
        }

        [Test]
        public void ForceClose_HidesWhateverWasShowing()
        {
            _focus.Selection(_a, selected: true);

            var change = _focus.ForceClose();

            Assert.AreSame(_a, change.Leave);
            Assert.IsNull(change.Enter);
            Assert.IsNull(_focus.Shown);
        }

        [Test]
        public void ForceClose_AlsoDropsTheRememberedHoverAndSelection()
        {
            _focus.Pointer(_a, entered: true);
            _focus.Selection(_a, selected: true);

            _focus.ForceClose();

            // If either record had survived, this leave would have been
            // believed and the re-show below would be reporting against a
            // node the player left a screen ago.
            var change = _focus.Pointer(_b, entered: true);

            Assert.AreSame(_b, change.Enter);
            Assert.IsNull(change.Leave, "nothing was showing after the force close");
        }

        [Test]
        public void ForceClose_WithNothingShowing_IsNotAChange()
        {
            Assert.IsFalse(_focus.ForceClose().Any);
        }
    }
}
