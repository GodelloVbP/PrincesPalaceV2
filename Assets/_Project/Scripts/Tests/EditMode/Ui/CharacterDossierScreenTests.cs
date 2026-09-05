using System.Linq;
using NUnit.Framework;
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
