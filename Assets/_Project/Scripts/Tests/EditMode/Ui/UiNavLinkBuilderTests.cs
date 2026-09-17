using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The pure-C# proof docs/GAMEPAD_NAVIGATION_PLAN.md section 9a calls for:
    // the link-builder algorithm against fake nodes, no scene, no Selectable.
    // The builder is generic over what a node is, so this file drives it with
    // UiNodes while production drives the same Build() with live Selectables
    // through RuntimeNavWiring -- one algorithm, pinned once here and
    // exercised through the adapter in RuntimeNavWiringTests (PlayMode).
    public class UiNavLinkBuilderTests
    {
        private static UiNode Btn(string name) => Ui.Button(name, UiString.FromContent(name), new UiVec(100f, 40f), 18);

        // ---- List: vertical, clamp by owner default (plan section 12.3) ---

        [Test]
        public void List_ClampsAtBothEnds_AndStepsInDeclaredOrder()
        {
            var a = Btn("A"); var b = Btn("B"); var c = Btn("C");
            var group = new UiNavGroup<UiNode>("rows", UiNavGroupKind.List, new[] { a, b, c });
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            Assert.IsNull(resolved[a].Up, "a List clamps at its first member - nothing above it");
            Assert.AreEqual(b, resolved[a].Down);
            Assert.AreEqual(a, resolved[b].Up);
            Assert.AreEqual(c, resolved[b].Down);
            Assert.AreEqual(b, resolved[c].Up);
            Assert.IsNull(resolved[c].Down, "a List clamps at its last member - nothing below it");

            // A List never touches Left/Right at all.
            Assert.IsNull(resolved[b].Left);
            Assert.IsNull(resolved[b].Right);
        }

        // ---- Rail: horizontal, wrap by owner default -----------------------

        [Test]
        public void Rail_WrapsAtBothEnds()
        {
            var a = Btn("A"); var b = Btn("B"); var c = Btn("C");
            var group = new UiNavGroup<UiNode>("ribbon", UiNavGroupKind.Rail, new[] { a, b, c });
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            Assert.AreEqual(c, resolved[a].Left, "a wrapped Rail's first member steps LEFT into the last one");
            Assert.AreEqual(b, resolved[a].Right);
            Assert.AreEqual(a, resolved[c].Right, "a wrapped Rail's last member steps RIGHT into the first one");
        }

        [Test]
        public void Rail_CanBeForcedToClamp()
        {
            var a = Btn("A"); var b = Btn("B");
            var group = new UiNavGroup<UiNode>("ribbon", UiNavGroupKind.Rail, new[] { a, b }, wrap: UiNavWrap.Clamp);
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            Assert.IsNull(resolved[a].Left);
            Assert.IsNull(resolved[b].Right);
        }

        // ---- Grid: row-length columns, row-wrap by owner default -----------

        [Test]
        public void Grid_WrapsWithinARow_AndNeverCrossesIntoAnotherRow()
        {
            // 5 members, 3 columns: row0 = [0,1,2] (full), row1 = [3,4] (partial).
            var nodes = Enumerable.Range(0, 5).Select(i => Btn($"N{i}")).ToArray();
            var group = new UiNavGroup<UiNode>("orbs", UiNavGroupKind.Grid, nodes, gridRowLength: 3);
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            // Row 0 wraps across its own 3 members only.
            Assert.AreEqual(nodes[2], resolved[nodes[0]].Left, "row 0 wraps left from its first member");
            Assert.AreEqual(nodes[1], resolved[nodes[0]].Right);
            Assert.AreEqual(nodes[0], resolved[nodes[2]].Right, "row 0 wraps right from its last member");

            // Row 1 is PARTIAL (2 members) and must wrap within ITS OWN length,
            // never reaching back into row 0's third column.
            Assert.AreEqual(nodes[4], resolved[nodes[3]].Left,
                "a partial row wraps within its own length, not the grid's full column count");
            Assert.AreEqual(nodes[3], resolved[nodes[4]].Right);

            // Up/Down step a whole row, never diagonally.
            Assert.AreEqual(nodes[3], resolved[nodes[0]].Down);
            Assert.AreEqual(nodes[0], resolved[nodes[3]].Up);
            Assert.AreEqual(nodes[1], resolved[nodes[4]].Up, "column 1 exists in both rows - straight up, not diagonal");
        }

        [Test]
        public void Grid_ColumnWithNoRowAbove_HasNoUpLink()
        {
            var nodes = Enumerable.Range(0, 5).Select(i => Btn($"N{i}")).ToArray();
            var group = new UiNavGroup<UiNode>("orbs", UiNavGroupKind.Grid, nodes, gridRowLength: 3);
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            // Column 2 (index 2, row 0) has no row-1 counterpart since row 1
            // only has 2 members (indices 3, 4) - column 2 of row 1 does not
            // exist, so node 2's Down must be null, not a wrapped guess.
            Assert.IsNull(resolved[nodes[2]].Down, "row 1 has no third column - nothing to step down into");
        }

        // ---- Explicit links override/extend group defaults ------------------

        [Test]
        public void ExplicitLink_OverridesWhatTheGroupComputed()
        {
            var a = Btn("A"); var b = Btn("B"); var footer = Btn("Footer");
            var group = new UiNavGroup<UiNode>("rows", UiNavGroupKind.List, new[] { a, b });
            var nav = new UiNavDeclaration<UiNode>(new[] { group },
                links: new[] { new UiNavLink<UiNode>(b, UiNavDirection.Down, footer) });

            var resolved = UiNavLinkBuilder.Build(nav);

            Assert.IsNull(resolved[a].Up);
            Assert.AreEqual(footer, resolved[b].Down,
                "an explicit link states an inter-group jump the List's own order cannot express");
        }

        // ---- Degenerate case --------------------------------------------------

        [Test]
        public void SingleMemberGroup_LinksToNothing_NotToItself()
        {
            var only = Btn("Only");
            var group = new UiNavGroup<UiNode>("lonely", UiNavGroupKind.Rail, new[] { only });
            var nav = new UiNavDeclaration<UiNode>(new[] { group });

            var resolved = UiNavLinkBuilder.Build(nav);

            // Build() only ever populates the dictionary for nodes an
            // explicit link or a 2+-member group actually touched - a lone
            // group member never gets an entry made for it, which the caller
            // reads exactly the same as "no link that direction".
            Assert.IsFalse(resolved.ContainsKey(only),
                "a wrapped modulo over a single member would otherwise link it to itself");
        }
    }
}
