using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The main menu's tree.
    //
    // MISSING UNTIL NOW, despite this screen's own class comment claiming it
    // for as long as the screen has existed ("an EditMode test builds and
    // audits this entire screen -- ambience and all, at four canvas frames --
    // in about a millisecond"). Nothing in the repository ever called
    // MainMenuScreen.Build from an EditMode test, so that sentence described a
    // test that did not exist -- the only thing that ever audited this screen
    // was a full scene build, and every layout change to it (the wordmark, the
    // left-anchored composition, Continue, Manage Saves, the delete hold) went
    // in without the fast feedback loop every sibling screen already has. This
    // is that test, the same shape DebugMenuScreenTests and a dozen others use.
    public class MainMenuScreenTests
    {
        private const int RealSlotCount = 5;

        private static UiNode Tree(int slotCount = RealSlotCount) =>
            MainMenuScreen.Build(new MainMenuInputs(slotCount)).Root;

        [Test]
        public void TheMenuAuditsCleanAtEveryFrame()
        {
            var errors = UiAudit.RunAllFrames(Tree());

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        // Slot count is an INPUT (Domain cannot see SaveSystem.SlotCount), and
        // the screen's own header names the real reason to test it at more
        // than the shipped number: a roster the game does not currently field
        // still has to lay out cleanly, or the audit is only ever proving
        // today's constant rather than the screen's own arithmetic.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(8)]
        public void TheMenuAuditsCleanAtOtherSlotCounts(int slotCount)
        {
            var errors = UiAudit.RunAllFrames(Tree(slotCount));

            Assert.IsEmpty(errors,
                $"at {slotCount} slots, first 5 of {errors.Count}: " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void EveryModalStartsHidden()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            Assert.IsTrue(screen.SaveSlotPanel.Node.StartInactive);
            Assert.IsTrue(screen.ManageSavesPanel.Node.StartInactive);
            Assert.IsTrue(screen.ResetConfirmPanel.Node.StartInactive);
        }

        // The three parallel arrays UiCountAudit (E4) checks against at build
        // time -- pinned here too, so a mismatch shows up in the same ~1ms
        // pass as the layout errors rather than only in a full scene build.
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        public void EverySlotHasAButtonALabelAndADeleteButton(int slotCount)
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(slotCount));

            Assert.AreEqual(slotCount, screen.SlotButtons.Count);
            Assert.AreEqual(slotCount, screen.SlotLabels.Count);
            Assert.AreEqual(slotCount, screen.DeleteButtons.Count);
        }

        // Continue is BUILT UNCONDITIONALLY -- see its own field comment on
        // why it cannot be a child the runtime simply chooses not to add.
        // Asserted as a node search rather than trusting the field alone,
        // since a NodeRef can point at a node that never made it into Root.
        [Test]
        public void ContinueExistsInTheTreeEvenThoughItIsHiddenUntilRuntime()
        {
            var root = Tree();

            bool found = Find(root, "ContinueButton");
            Assert.IsTrue(found, "ContinueButton must exist in the built tree for the controller to toggle");
        }

        // The hold's fill has to be authored at its REAL size -- UiAudit
        // refuses a zero-sized graphic, which is exactly the trap a fill
        // meant to start empty falls into if it is authored at zero instead
        // of authored full and shrunk at runtime. Passing
        // TheMenuAuditsCleanAtEveryFrame already proves this holds; this
        // names WHY, so a future edit that authors the fill at zero fails
        // with an explanation rather than a bare audit error.
        [Test]
        public void TheHoldFillIsAuthoredAtItsFullSize()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            Assert.AreEqual(MainMenuScreen.ResetHoldWidth, screen.ResetConfirmYesFill.Node.Size.X);
            Assert.AreEqual(MainMenuScreen.ResetHoldHeight, screen.ResetConfirmYesFill.Node.Size.Y);
        }

        private static bool Find(UiNode node, string name)
        {
            if (node == null) return false;
            if (node.Name == name) return true;

            foreach (var child in node.Children)
            {
                if (Find(child, name)) return true;
            }

            return false;
        }
    }
}
