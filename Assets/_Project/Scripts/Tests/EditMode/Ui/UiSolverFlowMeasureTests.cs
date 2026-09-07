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
    }
}
