using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // Column A's Blue 3:4 container frame. The rest of the dossier's geometry
    // is covered by SystemMenuPaneTests; this file is only about the kit
    // placement -- the frame itself, its theme, and that column A's identity
    // content actually lives inside the measured inset rather than under the
    // painted border.
    public class CharacterDossierScreenTests
    {
        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(CharacterDossierScreen.Build().Root, frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        // Column A's container theme/ratio and content inset are covered by
        // KitContainerPlacementTests, not repeated here.

        [Test]
        public void ColumnAIdentityContentRidesInsideTheFrame()
        {
            // If the portrait/name/nav rows were siblings of the frame rather
            // than descendants, moving or hiding the frame would leave them
            // stranded over the sky next to it -- the same bug class the
            // talent panel's own container test guards.
            var frame = Walk(CharacterDossierScreen.Build().Root)
                .First(n => n.Name == "DossierColumnAFrame");
            var names = Walk(frame).Select(n => n.Name).ToList();

            CollectionAssert.Contains(names, "DossierName");
            CollectionAssert.Contains(names, "DossierSubLine");
            CollectionAssert.Contains(names, "DossierPackRow");
        }

        // DossierLayout.LeaderAt used to restate every slot's leader top as
        // an independent literal (187, 285, 387, 489...) that happened to
        // equal SlotAt's own top + 37 -- a slot moved in SlotAt without its
        // leader being edited too left a hairline pointing at the old spot.
        // LeaderTop/SlotTop now share one table (DossierLayout.SlotGeometry),
        // and this pins the relationship with a literal 37, not the const
        // LeaderTop actually adds -- reading the const back would make this
        // tautological.
        [Test]
        public void LeaderTopTracksItsSlotTopByExactly37()
        {
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.Head) continue;  // no leader for the centred slot

                Assert.AreEqual(DossierLayout.SlotTop(slot) + 37f, DossierLayout.LeaderTop(slot),
                    $"{slot}'s leader top should be its slot top + 37");
            }
        }

        // The minus mirrors the plus across the cell's own centre. Solved in
        // canvas space, so both centres are read relative to the cell's own
        // solved centre before comparing -- the cell itself sits wherever the
        // grid places it, and only the offset inside it is what §7 measured.
        //
        // Literal expected values, not DossierLayout's own constants: a bug
        // that broke the constants and this test's use of them the same way
        // would still pass.
        //
        // NOT §7's own literals. ContentCWidth carried a stale "// 340"
        // comment (DossierLayout.cs:632, now fixed) against a computed 400 --
        // ColumnCWidth(480) - ColumnCPadX(40)*2. §7's arithmetic was built on
        // the comment rather than the computation, so its 42.67/63.33 read as
        // plausible and were wrong; these are recomputed from the real
        // constants once, by hand, and then pinned as literals so a second
        // regression here cannot mark its own homework either.
        [Test]
        public void TheMinusSitsOppositeThePlusInsideItsCell()
        {
            var solved = UiSolver.Solve(CharacterDossierScreen.Build().Root, UiFrames.Reference);
            var cell = FindSolved(solved, "DossierAttrCell0");
            var plus = FindSolved(solved, "DossierAttrPlus0");
            var minus = FindSolved(solved, "DossierAttrMinus0");

            Assert.IsNotNull(cell);
            Assert.IsNotNull(plus);
            Assert.IsNotNull(minus);

            Assert.AreEqual(131.33f, cell.Rect.Width, 0.01f);
            Assert.AreEqual(84f, cell.Rect.Height, 0.01f);

            var plusLocal = plus.Rect.Centre - cell.Rect.Centre;
            var minusLocal = minus.Rect.Centre - cell.Rect.Centre;

            Assert.AreEqual(52.67f, plusLocal.X, 0.01f);
            Assert.AreEqual(29f, plusLocal.Y, 0.01f);
            Assert.AreEqual(22f, plus.Rect.Width, 0.01f);
            Assert.AreEqual(22f, plus.Rect.Height, 0.01f);

            Assert.AreEqual(-52.67f, minusLocal.X, 0.01f);
            Assert.AreEqual(29f, minusLocal.Y, 0.01f);
            Assert.AreEqual(22f, minus.Rect.Width, 0.01f);
            Assert.AreEqual(22f, minus.Rect.Height, 0.01f);

            float gap = plus.Rect.Left - minus.Rect.Right;
            Assert.AreEqual(83.33f, gap, 0.01f);
        }

        private static SolvedNode FindSolved(SolvedNode root, string name) =>
            root.Name == name ? root : root.Descendants().FirstOrDefault(n => n.Name == name);

        private static System.Collections.Generic.IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}
