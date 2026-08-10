using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The hub's tree, audited before any scene exists.
    //
    // This class is also the TUNING HARNESS for the composition: every
    // coordinate change is a 1.4-second EditMode loop rather than a scene
    // rebuild, which is the only reason staging four buildings at depth is a
    // cheap thing to iterate on at all.
    public class HubScreenTests
    {
        // UiFrames.All, not a set invented here. The first draft of this test
        // made up its own frames including a 1600x900 -- shorter than anything
        // the project supports -- and then "failed" on chrome that has shipped
        // fine since the hub was built. A harness that disagrees with the build
        // about what a frame is reports bugs that cannot happen.
        private static UiVec[] Frames => UiFrames.All;

        [Test]
        public void TheTreeAuditsCleanAtEveryFrame()
        {
            var screen = HubScreen.Build();

            foreach (var frame in Frames)
            {
                var solved = UiSolver.Solve(screen.Root, frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {frame.X}x{frame.Y}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void EverythingStagedLivesInsideTheWorld()
        {
            // The chrome/world split is what lets one handle drift or settle the
            // whole place without dragging the heading and the wallet with it.
            var screen = HubScreen.Build();
            var world = Find(screen.Root, "HubWorld");

            Assert.IsNotNull(world);
            foreach (var name in new[]
            {
                "HubBackground", "StartRunGate", "TalentsBuilding",
                "PrincipalityBuilding", "CharacterSheetBuilding", "RelicsBuilding",
            })
            {
                Assert.IsNotNull(Find(world, name), $"{name} escaped the world layer");
            }
        }

        [Test]
        public void TheChromeStaysOutsideTheWorld()
        {
            var screen = HubScreen.Build();
            var world = Find(screen.Root, "HubWorld");

            foreach (var name in new[] { "HubHeading", "CurrencyPlate", "MainMenuButton" })
            {
                Assert.IsNull(Find(world, name), $"{name} would drift with the world");
                Assert.IsNotNull(Find(screen.Root, name));
            }
        }

        [Test]
        public void BuildingsAreDeclaredFarToNear()
        {
            // Declaration order IS painter's order. Declared near-to-far, a
            // distant building would draw over a close one where they overlap.
            var world = Find(HubScreen.Build().Root, "HubWorld");
            var names = world.Children.Select(c => c.Name).ToList();

            Assert.Less(names.IndexOf("RelicsBuilding"), names.IndexOf("CharacterSheetBuilding"));
            Assert.Less(names.IndexOf("TalentsBuilding"), names.IndexOf("PrincipalityBuilding"));
            Assert.Less(names.IndexOf("PrincipalityBuilding"), names.IndexOf("StartRunGate"),
                "the gate is nearest of all - it is the only thing on the ground");
        }

        [Test]
        public void TheGateCarriesItsOwnCaption()
        {
            // A declared node rather than the emitter's auto-label, because the
            // controller swaps it between BEGIN DESCENT and RESUME.
            var gate = Find(HubScreen.Build().Root, "StartRunGate");

            Assert.IsNotNull(Find(gate, "StartRunGateCaption"));
        }

        [Test]
        public void EveryBuildingHasArt()
        {
            // An Image with no sprite renders as a solid WHITE QUAD, not as
            // nothing -- a bug class this project has shipped more than once.
            var world = Find(HubScreen.Build().Root, "HubWorld");

            foreach (var name in new[]
            {
                "StartRunGate", "TalentsBuilding", "PrincipalityBuilding",
                "CharacterSheetBuilding", "RelicsBuilding",
            })
            {
                Assert.IsFalse(string.IsNullOrEmpty(Find(world, name).SpriteKey), $"{name} has no sprite");
            }
        }

        [Test]
        public void EveryExemptionStatesAReason()
        {
            // The audit accepts an escape hatch only with a reason, and a reason
            // like "x" satisfies the compiler while explaining nothing.
            foreach (var node in Walk(HubScreen.Build().Root))
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

        private static UiNode Find(UiNode root, string name) =>
            Walk(root).FirstOrDefault(n => n.Name == name);

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
