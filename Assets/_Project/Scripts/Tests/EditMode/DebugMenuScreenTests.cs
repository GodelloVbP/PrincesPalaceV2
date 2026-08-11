using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.DebugMenu;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The debug menu's tree.
    //
    // A developer tool still has to clear the same audit as everything else --
    // twelve 44px rows stacked on a 50px pitch is exactly the sort of thing
    // that passes by eye and overlaps by 6px, and the whole point of building
    // screens as trees is that nobody has to notice that by eye.
    public class DebugMenuScreenTests
    {
        private static UiNode Tree() => DebugMenuScreen.Build().Root;

        [Test]
        public void TheMenuAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheHubItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(HubScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TheMenuStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void ThereIsOneRowNodePerCatalogueRow()
        {
            // One number, read by the tree, the controller and the count
            // audit. A tree with fewer rows than a page holds would silently
            // drop items off the end.
            var screen = DebugMenuScreen.Build();

            Assert.AreEqual(DebugMenuCatalog.RowsPerPage, screen.RowButtons.Count);
            Assert.AreEqual(DebugMenuCatalog.RowsPerPage, screen.RowLabels.Count);
        }

        [Test]
        public void ThereIsAFilterForEveryKindPlusAll()
        {
            // Three ItemKinds and the no-filter sentinel. Stated here because
            // the controller indexes FilterKinds by button position, so a
            // fourth kind added to content without a button would make the two
            // arrays disagree silently.
            Assert.AreEqual(4, DebugMenuScreen.Build().FilterButtons.Count);
        }

        [Test]
        public void TheDebugMenuDrawsOverTheCharacterOverlay()
        {
            // Declaration order is painter's order. A debug tool covered by the
            // very screen you opened it to debug is the one stacking mistake
            // that makes it useless -- and both are modals over the same hub,
            // so nothing else decides this.
            var root = HubScreen.Build().Root;
            var names = root.Children.Select(c => c.Name).ToList();

            int overlay = names.IndexOf("CharacterOverlayPanel");
            int debug = names.IndexOf("DebugMenuPanel");

            Assert.Greater(overlay, -1, "the overlay is not a direct child of HubPanel any more");
            Assert.Greater(debug, -1, "the debug menu is not a direct child of HubPanel any more");
            Assert.Greater(debug, overlay, "the debug menu must be declared after the overlay");
        }

        [Test]
        public void ARowLabelDoesNotCollideWithTheButtonsGeneratedCaption()
        {
            // UiEmitter names a button's own caption "<button>Label". A tree
            // child by that name produces two GameObjects with one name under
            // one parent, and every by-name lookup takes the emitter's empty
            // one. This screen shipped that bug for one build.
            var offenders = Walk(Tree())
                .Where(n => n.Kind == UiNodeKind.Button)
                .SelectMany(button => button.Children
                    .Where(c => c.Name == button.Name + "Label")
                    .Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheCollisionCheckIsNotVacuous()
        {
            // A deliberate collision must FAIL the audit. Without this the
            // check above proves only that no such node exists today, not that
            // the audit would catch one tomorrow -- and a guard nobody has seen
            // fire is a guard nobody knows is wired up.
            var button = Ui.Button("Probe", UiStrings.Close, new UiVec(200f, 50f), 16, Place.At(0f, 0f));
            button.Children.Add(Ui.Label("ProbeLabel", UiStrings.Close, new UiVec(180f, 40f), 14,
                "#FFFFFF", Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("ProbeRoot", UiSize.Fixed(1920f, 1080f), button);
            var errors = UiAudit.Run(UiSolver.Solve(root, UiFrames.All[0]), UiFrames.All[0]);

            Assert.IsTrue(errors.Any(e => e.Check == UiAuditCheck.DuplicateName),
                "the audit did not object to a child named exactly '<button>Label'");
        }

        [Test]
        public void EveryExemptionStatesARealReason()
        {
            foreach (var node in Walk(Tree()))
            {
                if (node.AllowOverlapReason != null)
                {
                    Assert.Greater(node.AllowOverlapReason.Length, 20, $"{node.Name}'s overlap reason is too thin");
                }

                if (node.AllowOverflowReason != null)
                {
                    Assert.Greater(node.AllowOverflowReason.Length, 20, $"{node.Name}'s overflow reason is too thin");
                }
            }
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}
