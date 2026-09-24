using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // AN ABSOLUTELY-PLACED CHILD OF A FLOW CONTAINER DOES NOT CONSUME FLOW
    // SPACE. UiSolver.ArrangeFlow says so in as many words ("a glow pinned
    // behind a row, say") and arranges accordingly -- it builds its flow list
    // from the children whose Place.IsFlow is true, and spaces only those.
    //
    // The MEASURE pass does not agree. UiSolver.ContentSize walks every child,
    // so a pinned decoration is added to the stack's own extent AND earns a
    // spacing gap of its own; UiAudit.CheckFlowCapacity walks every child for
    // the same sum, so a Fixed container is reported over capacity for a child
    // that occupies none of it. One tree, three different answers to "how tall
    // is this column".
    public class UiSolverFlowMeasureTests
    {
        private static readonly UiVec Canvas = new UiVec(1920f, 1080f);

        private static UiNode Bar(string name) =>
            Ui.Solid(name, "#336699", new UiVec(200f, 100f));

        // Pinned behind the stack, taller than the whole of it -- the shape the
        // solver's own comment names.
        private static UiNode PinnedGlow() =>
            Ui.Solid("Glow", "#ffffff", Place.At(0f, 0f), UiSize.Fixed(240f, 500f));

        [Test]
        public void AFromChildrenColumnMeasuresOnlyItsFlowChildren()
        {
            var column = Ui.Column("Stack", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Bar("First"), Bar("Second"), PinnedGlow());

            var solved = UiSolver.Solve(column, Canvas);

            // Two 100px bars and the one 10px gap between them. The glow is
            // placed, not stacked, so it adds neither its height nor a gap.
            Assert.AreEqual(210f, solved.Rect.Height, 0.01f);
        }

        [Test]
        public void AFromChildrenRowMeasuresOnlyItsFlowChildren()
        {
            var row = Ui.Row("Strip", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Bar("First"), Bar("Second"),
                Ui.Solid("Glow", "#ffffff", Place.At(0f, 0f), UiSize.Fixed(900f, 40f)));

            var solved = UiSolver.Solve(row, Canvas);

            Assert.AreEqual(410f, solved.Rect.Width, 0.01f);
        }

        // The audit's mirror of the same walk. A Fixed column sized to exactly
        // what its flow children need must not be reported over capacity
        // because something is pinned inside it.
        [Test]
        public void FlowCapacityCountsOnlyFlowChildren()
        {
            var column = Ui.Column("Stack", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Bar("First"), Bar("Second"), PinnedGlow()).Sized(UiSize.Fixed(240f, 210f));

            var overflows = UiAudit.Run(UiSolver.Solve(column, Canvas), Canvas)
                .Where(e => e.Check == UiAuditCheck.FlowCapacity)
                .Select(e => e.Message)
                .ToArray();

            CollectionAssert.IsEmpty(overflows, string.Join("\n", overflows));
        }

        // ...and it still fires for the case it exists for: real flow children
        // that do not fit the box they were given.
        [Test]
        public void FlowCapacityStillRefusesAColumnTooSmallForItsFlowChildren()
        {
            var column = Ui.Column("Stack", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Bar("First"), Bar("Second")).Sized(UiSize.Fixed(240f, 120f));

            var overflows = UiAudit.Run(UiSolver.Solve(column, Canvas), Canvas)
                .Where(e => e.Check == UiAuditCheck.FlowCapacity)
                .ToArray();

            Assert.AreEqual(1, overflows.Length, "the capacity check stopped firing for a real overflow");
        }

        // A Ui.Space consumes flow extent and a gap but draws nothing, so it
        // has no SolvedNode. The capacity check used to sum solved children
        // and so never saw it: 90 + 60 + 90 read as 180 in a 200px column.
        private static UiNode Block(string name) =>
            Ui.Solid(name, "#336699", new UiVec(100f, 90f));

        private static UiAuditError[] CapacityErrors(UiNode tree) =>
            UiAudit.Run(UiSolver.Solve(tree, Canvas), Canvas)
                .Where(e => e.Check == UiAuditCheck.FlowCapacity)
                .ToArray();

        [Test]
        public void FlowCapacityCountsASpaceBetweenChildren()
        {
            var column = Ui.Column("Stack", Place.At(0f, 0f), 0f, UiAlign.Centre,
                Block("First"), Ui.Space(60f), Block("Second")).Sized(UiSize.Fixed(100f, 200f));

            var overflows = CapacityErrors(column);

            Assert.AreEqual(1, overflows.Length, "a 240px stack in a 200px column passed the capacity check");
            StringAssert.Contains("needs 240px", overflows[0].Message);
        }

        // The trailing case, which containment cannot catch: nothing drawn
        // sits outside the box, but the declared stack still does not fit.
        [Test]
        public void FlowCapacityCountsATrailingSpaceAndItsGap()
        {
            var row = Ui.Row("Strip", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Block("First"), Block("Second"), Ui.Space(50f)).Sized(UiSize.Fixed(250f, 90f));

            var overflows = CapacityErrors(row);

            // 100 + 10 + 100 + 10 + 50.
            Assert.AreEqual(1, overflows.Length, "a trailing Space's extent and gap were not counted");
            StringAssert.Contains("needs 270px", overflows[0].Message);
        }

        [Test]
        public void FlowCapacityPassesAColumnThatFitsItsSpaceExactly()
        {
            var column = Ui.Column("Stack", Place.At(0f, 0f), 10f, UiAlign.Centre,
                Block("First"), Ui.Space(40f), Block("Second")).Sized(UiSize.Fixed(100f, 240f));

            // 90 + 10 + 40 + 10 + 90 = 240.
            var overflows = CapacityErrors(column).Select(e => e.Message).ToArray();

            CollectionAssert.IsEmpty(overflows, string.Join("\n", overflows));
        }
    }
}
