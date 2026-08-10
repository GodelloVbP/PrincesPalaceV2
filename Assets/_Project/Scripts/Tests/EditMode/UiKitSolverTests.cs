using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Every expected value here is a LITERAL, never a re-derivation of the
    // solver's own arithmetic -- CLAUDE.md gotcha 5, and doubly load-bearing
    // for a layout engine, where a test that recomputes the formula would pass
    // against any consistent-but-wrong solver.
    public class UiKitSolverTests
    {
        private static readonly UiVec Frame = UiFrames.Reference;

        private static SolvedNode Child(SolvedNode parent, string name) =>
            parent.Children.Find(c => c.Name == name);

        private static void AssertRect(SolvedNode node, float cx, float cy, float w, float h)
        {
            Assert.IsNotNull(node, "node not found in solved tree");
            Assert.AreEqual(cx, node.Rect.Centre.X, 0.01f, $"{node.Path} centre.x");
            Assert.AreEqual(cy, node.Rect.Centre.Y, 0.01f, $"{node.Path} centre.y");
            Assert.AreEqual(w, node.Rect.Size.X, 0.01f, $"{node.Path} width");
            Assert.AreEqual(h, node.Rect.Size.Y, 0.01f, $"{node.Path} height");
        }

        [Test]
        public void Column_StacksChildrenDownwardWithSpacing()
        {
            var tree = Ui.Column("Col", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre,
                Ui.Solid("A", "#ffffff", new UiVec(100f, 40f)),
                Ui.Solid("B", "#ffffff", new UiVec(100f, 60f)));

            var solved = UiSolver.Solve(tree, Frame);

            // FromChildren: 100 wide (the wider child), 40 + 60 + 10 spacing tall.
            AssertRect(solved, 0f, 0f, 100f, 110f);
            AssertRect(Child(solved, "A"), 0f, 35f, 100f, 40f);
            AssertRect(Child(solved, "B"), 0f, -25f, 100f, 60f);
        }

        [Test]
        public void Row_RunsLeftToRightWithSpacing()
        {
            var tree = Ui.Row("Row", Place.At(0f, 0f), spacing: 20f, UiAlign.Centre,
                Ui.Solid("A", "#ffffff", new UiVec(50f, 30f)),
                Ui.Solid("B", "#ffffff", new UiVec(70f, 30f)));

            var solved = UiSolver.Solve(tree, Frame);

            AssertRect(solved, 0f, 0f, 140f, 30f);
            AssertRect(Child(solved, "A"), -45f, 0f, 50f, 30f);
            AssertRect(Child(solved, "B"), 35f, 0f, 70f, 30f);
        }

        [Test]
        public void Column_Padding_ShrinksTheContentBoxOnAllFourSides()
        {
            var tree = Ui.Column("Col", Place.At(0f, 0f), spacing: 0f, UiAlign.Start,
                    Ui.Solid("A", "#ffffff", new UiVec(100f, 40f)))
                .Padded(new UiPad(left: 20f, right: 10f, bottom: 5f, top: 15f));

            var solved = UiSolver.Solve(tree, Frame);

            // FromChildren + padding: 100+30 wide, 40+20 tall.
            AssertRect(solved, 0f, 0f, 130f, 60f);
            // Content box spans x[-45,55], y[-25,15]. Start-aligned, top-anchored.
            AssertRect(Child(solved, "A"), 5f, -5f, 100f, 40f);
        }

        [Test]
        public void Fill_SplitsLeftoverSpaceEquallyBetweenFillChildren()
        {
            var tree = Ui.Column("Col", Place.At(0f, 0f), spacing: 0f, UiAlign.Centre,
                    Ui.Solid("A", "#ffffff", new UiVec(100f, 40f)),
                    Ui.Solid("Fill1", "#ffffff", Place.Flow, UiSize.FillHeight(100f)),
                    Ui.Solid("Fill2", "#ffffff", Place.Flow, UiSize.FillHeight(100f)))
                .Sized(UiSize.Fixed(200f, 240f));

            var solved = UiSolver.Solve(tree, Frame);

            // 240 tall, 40 taken by A, 200 left, split two ways.
            AssertRect(Child(solved, "A"), 0f, 100f, 100f, 40f);
            AssertRect(Child(solved, "Fill1"), 0f, 30f, 100f, 100f);
            AssertRect(Child(solved, "Fill2"), 0f, -70f, 100f, 100f);
        }

        [Test]
        public void FillInsideFromChildren_IsRefusedAsCircular_NamingBothSides()
        {
            // The one size combination with no fixed point: the parent needs the
            // child's height to compute its own, and the child needs the
            // parent's. Guessing would produce a plausible wrong layout.
            var tree = Ui.Column("Col", Place.At(0f, 0f), spacing: 0f, UiAlign.Centre,
                Ui.Solid("Greedy", "#ffffff", Place.Flow, UiSize.FillHeight(100f)));

            var ex = Assert.Throws<UiSolveException>(() => UiSolver.Solve(tree, Frame));

            StringAssert.Contains("Greedy", ex.Message);
            StringAssert.Contains("Col", ex.Message);
            StringAssert.Contains("circular", ex.Message);
            StringAssert.Contains("Fix by", ex.Message, "the house style is to name remedies, not just the fault");
        }

        [Test]
        public void FlowChildInsideAPanel_IsRefused_WithRemedies()
        {
            var tree = Ui.Panel("Root", UiSize.Fill,
                Ui.Solid("Loose", "#ffffff", new UiVec(10f, 10f)));

            var ex = Assert.Throws<UiSolveException>(() => UiSolver.Solve(tree, Frame));

            StringAssert.Contains("Loose", ex.Message);
            StringAssert.Contains("Fix by", ex.Message);
        }

        [Test]
        public void Stretch_FillsTheParentMinusItsInsets()
        {
            var tree = Ui.Panel("Root", UiSize.Fill,
                Ui.Solid("Bg", "#ffffff", Place.Stretch(left: 10f, right: 20f, bottom: 30f, top: 40f), UiSize.Fill));

            var solved = UiSolver.Solve(tree, Frame);

            AssertRect(solved, 0f, 0f, 1920f, 1080f);
            AssertRect(Child(solved, "Bg"), -5f, -5f, 1890f, 1010f);
        }

        [Test]
        public void At_PositionsRelativeToTheParentCentre()
        {
            var tree = Ui.Panel("Root", UiSize.Fill,
                Ui.Solid("Box", "#ffffff", new UiVec(200f, 60f), Place.At(100f, -50f)));

            var solved = UiSolver.Solve(tree, Frame);

            AssertRect(Child(solved, "Box"), 100f, -50f, 200f, 60f);
        }

        [Test]
        public void Grid_FillsRowMajorAcrossColumnsThenDown()
        {
            var tree = Ui.Grid("G", Place.At(0f, 0f), columns: 2,
                cell: new UiVec(100f, 50f), gap: new UiVec(10f, 20f),
                children: new[]
                {
                    Ui.Solid("C0", "#ffffff", Place.Flow, UiSize.Fill),
                    Ui.Solid("C1", "#ffffff", Place.Flow, UiSize.Fill),
                    Ui.Solid("C2", "#ffffff", Place.Flow, UiSize.Fill),
                });

            var solved = UiSolver.Solve(tree, Frame);

            // 2 columns x 2 rows of 100x50 cells with 10/20 gaps.
            AssertRect(solved, 0f, 0f, 210f, 120f);
            AssertRect(Child(solved, "C0"), -55f, 35f, 100f, 50f);
            AssertRect(Child(solved, "C1"), 55f, 35f, 100f, 50f);
            AssertRect(Child(solved, "C2"), -55f, -35f, 100f, 50f);
        }

        [Test]
        public void Pin_KeepsItsDistanceFromTheAnchoredCorner_AcrossAspects()
        {
            // The exact shape v1's NewUiRect could not express, and the reason
            // the audit runs at several frames: a pinned node MOVES relative to
            // centred content when the aspect changes.
            UiNode Tree() => Ui.Panel("Root", UiSize.Fill,
                Ui.Solid("Corner", "#ffffff",
                    Place.Pin(UiVec.One, UiVec.One, new UiVec(-40f, -30f)),
                    UiSize.Fixed(100f, 50f)));

            var at169 = UiSolver.Solve(Tree(), UiFrames.Reference);
            AssertRect(Child(at169, "Corner"), 870f, 485f, 100f, 50f);

            var at219 = UiSolver.Solve(Tree(), UiFrames.UltraWide);
            AssertRect(Child(at219, "Corner"), 1200f, 485f, 100f, 50f);

            // Same gap to the right edge in both: 960-920 and 1290-1250.
            Assert.AreEqual(40f, 960f - Child(at169, "Corner").Rect.Right, 0.01f);
            Assert.AreEqual(40f, 1290f - Child(at219, "Corner").Rect.Right, 0.01f);
        }

        [Test]
        public void PanelContent_IsAspectInvariant_AcrossEveryAuditFrame()
        {
            // Panels are fixed 1920x1080, so their contents must land identically
            // at every frame. If this ever fails, the audit's four-frame sweep is
            // doing more than it claims and panel layouts are aspect-dependent.
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(
                    Ui.Panel("Root", UiSize.Fixed(1920f, 1080f),
                        Ui.Column("Col", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre,
                            Ui.Solid("A", "#ffffff", new UiVec(100f, 40f)),
                            Ui.Solid("B", "#ffffff", new UiVec(100f, 60f)))),
                    frame);

                var col = Child(solved, "Col");
                AssertRect(Child(col, "A"), 0f, 35f, 100f, 40f);
                AssertRect(Child(col, "B"), 0f, -25f, 100f, 60f);
            }
        }

        [Test]
        public void Space_ConsumesFlowExtentAndDrawsNothing()
        {
            var tree = Ui.Column("Col", Place.At(0f, 0f), spacing: 0f, UiAlign.Centre,
                Ui.Solid("A", "#ffffff", new UiVec(100f, 40f)),
                Ui.Space(25f),
                Ui.Solid("B", "#ffffff", new UiVec(100f, 40f)));

            var solved = UiSolver.Solve(tree, Frame);

            // 40 + 25 + 40 tall, and the Space emits no solved child.
            AssertRect(solved, 0f, 0f, 100f, 105f);
            Assert.AreEqual(2, solved.Children.Count, "Space should not survive into the solved tree");
            AssertRect(Child(solved, "A"), 0f, 32.5f, 100f, 40f);
            AssertRect(Child(solved, "B"), 0f, -32.5f, 100f, 40f);
        }

        [Test]
        public void AllowOverlap_WithoutAReason_IsRejectedAtDeclarationTime()
        {
            var node = Ui.Solid("X", "#ffffff", new UiVec(10f, 10f));
            Assert.Throws<System.ArgumentException>(() => node.AllowOverlap(" "));
            Assert.Throws<System.ArgumentException>(() => node.AllowOverflow(null));
        }
    }
}
