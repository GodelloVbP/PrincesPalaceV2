using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The ADAPTER half of phase 2's one-navigation-model unification
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 9). UiNavLinkBuilderTests
    // pins the algorithm against fake nodes; this pins that live Selectables
    // fed through RuntimeNavWiring come out of that SAME algorithm with the
    // right UnityEngine.UI.Navigation written on them -- which is the claim
    // that used to be untestable, because Core carried its own copy of the
    // prev/next math.
    //
    // Plain GameObjects, no scene: what is under test is Selectable.
    // navigation, which needs no canvas, no EventSystem and no layout.
    public class RuntimeNavWiringTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void DestroySpawned()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private UnityEngine.UI.Selectable Btn(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Button));
            _spawned.Add(go);
            return go.GetComponent<UnityEngine.UI.Button>();
        }

        private UnityEngine.UI.Selectable[] Btns(int count) =>
            Enumerable.Range(0, count).Select(i => Btn($"N{i}")).ToArray();

        [Test]
        public void Rail_Wraps_AndWritesExplicitMode()
        {
            var nodes = Btns(3);

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("rail", UiNavGroupKind.Rail, nodes));

            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.Explicit, nodes[0].navigation.mode,
                "the adapter must write Explicit - Automatic would let Unity re-derive neighbours by geometry");
            Assert.AreEqual(nodes[2], nodes[0].navigation.selectOnLeft,
                "a wrapped Rail's first member steps LEFT into the last one");
            Assert.AreEqual(nodes[1], nodes[0].navigation.selectOnRight);
            Assert.AreEqual(nodes[0], nodes[2].navigation.selectOnRight,
                "a wrapped Rail's last member steps RIGHT into the first one");
        }

        [Test]
        public void List_Clamps_AndNeverTouchesTheHorizontalAxis()
        {
            var nodes = Btns(3);

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("rows", UiNavGroupKind.List, nodes));

            Assert.IsNull(nodes[0].navigation.selectOnUp, "a List clamps at its first member");
            Assert.AreEqual(nodes[1], nodes[0].navigation.selectOnDown);
            Assert.IsNull(nodes[2].navigation.selectOnDown, "a List clamps at its last member");
            Assert.IsNull(nodes[1].navigation.selectOnLeft, "a List declares nothing on the horizontal axis");
            Assert.IsNull(nodes[1].navigation.selectOnRight);
        }

        [Test]
        public void Grid_ShortLastRow_WrapsWithinItsOwnRow()
        {
            // 5 members, 3 columns: row0 = [0,1,2] (full), row1 = [3,4] (short).
            var nodes = Btns(5);

            RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("orbs", UiNavGroupKind.Grid, nodes, gridRowLength: 3));

            Assert.AreEqual(nodes[4], nodes[3].navigation.selectOnLeft,
                "a short last row wraps within its own length, not the grid's full column count");
            Assert.AreEqual(nodes[3], nodes[4].navigation.selectOnRight);
            Assert.AreEqual(nodes[0], nodes[3].navigation.selectOnUp, "Up steps a whole row, never diagonally");
            Assert.IsNull(nodes[2].navigation.selectOnDown,
                "row 1 has no third column - nothing to step down into");
        }

        [Test]
        public void InterGroupLink_ResolvesAcrossTwoGroups()
        {
            var rail = Btns(2);
            var footer = Btn("Footer");

            RuntimeNavWiring.Apply(
                new[]
                {
                    RuntimeNavWiring.Group("rail", UiNavGroupKind.Rail, rail),
                    RuntimeNavWiring.Group("footer", UiNavGroupKind.List, new[] { footer }),
                },
                rail.Select(node => RuntimeNavWiring.Link(node, UiNavDirection.Down, footer)));

            Assert.AreEqual(footer, rail[0].navigation.selectOnDown,
                "an explicit link is how a Rail reaches a control outside its own group");
            Assert.AreEqual(footer, rail[1].navigation.selectOnDown);
            Assert.AreEqual(rail[1], rail[0].navigation.selectOnRight,
                "the link must not cost the group its own axis");
        }

        // The adapter's own contract, not the builder's: a fixed-length array
        // with an empty slot (a headless fixture's unwired row) links PAST
        // the hole rather than into it.
        [Test]
        public void MissingMembers_AreDroppedSoTheSurvivorsLinkPastThem()
        {
            var a = Btn("A");
            var c = Btn("C");
            var members = new[] { a, null, c };

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("rail", UiNavGroupKind.Rail, members));

            Assert.AreEqual(c, a.navigation.selectOnRight,
                "the hole must not become a dead neighbour a Move walks into and stops at");
            Assert.AreEqual(a, c.navigation.selectOnLeft);
        }

        [Test]
        public void AnEmptyGroupIsSkipped_NotThrown()
        {
            Assert.IsNull(RuntimeNavWiring.Group("none", UiNavGroupKind.Rail, new UnityEngine.UI.Selectable[0]),
                "a group with nothing in it is not a declaration, it is an absence");
            Assert.DoesNotThrow(() => RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("none", UiNavGroupKind.Rail, new UnityEngine.UI.Selectable[0])));
        }
    }
}
