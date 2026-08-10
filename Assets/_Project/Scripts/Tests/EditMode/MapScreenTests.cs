using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The descent map's tree, audited before any scene exists.
    public class MapScreenTests
    {
        [Test]
        public void TheTreeAuditsClean()
        {
            var screen = MapScreen.Build();
            var solved = UiSolver.Solve(screen.Root, new UiVec(1920f, 1080f));
            var errors = UiAudit.Run(solved, new UiVec(1920f, 1080f));

            Assert.IsEmpty(errors,
                "first 8 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(8).Select(e => e.ToString())));
        }

        [Test]
        public void ThePoolCoversEveryPositionALegCouldUse()
        {
            var screen = MapScreen.Build();
            Assert.AreEqual(MapLayout.Capacity, screen.NodeButtons.Count);
            Assert.AreEqual(screen.NodeButtons.Count, screen.NodeLabels.Count);
            Assert.AreEqual(screen.NodeButtons.Count, screen.NodeMarkers.Count);
        }
    }
}
