using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The talent screen's tree.
    //
    // This class exists because it DIDN'T. Every other screen got an EditMode
    // audit harness and the constellation never did, so two things nobody could
    // see from a passing suite shipped together: orbs drawn with their own glow
    // sprite (a soft white smudge inside a slightly larger soft white smudge,
    // with no rim for the three state colours to sit on), and a detail plate
    // that opened as an empty 880x150 coloured slab because blanking its labels
    // left the slab behind.
    //
    // Both were visible in the first screenshot and invisible to 1190 passing
    // tests. That gap is the thing being closed here.
    public class ConstellationScreenTests
    {
        private static UiNode Tree() => TalentScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
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
        public void AnOrbIsADiscRatherThanItsOwnGlow()
        {
            // The orb read "proc:radial_glow" -- the SAME asset as the glow
            // child sitting behind it. A radial glow has no body and no edge,
            // so an orb had nothing for OrbTaken/OrbReachable/OrbDistant to
            // colour: three carefully graded states rendering as three barely
            // different smudges.
            var screen = TalentScreen.Build();
            var byName = Walk(screen.Root).ToDictionary(n => n.Name, n => n);

            var orb = byName["Orb0_0"];
            var glow = byName["Orb0_0Glow"];

            Assert.AreEqual("proc:solid_circle", orb.SpriteKey);
            Assert.AreNotEqual(orb.SpriteKey, glow.SpriteKey,
                "an orb and its own halo cannot be the same picture");
        }

        [Test]
        public void EveryOrbUsesTheDisc()
        {
            // Walked rather than spot-checked: the orbs are built in a loop, so
            // a per-path divergence would mean the loop grew a branch.
            var offenders = Walk(TalentScreen.Build().Root)
                .Where(n => n.Name.StartsWith("Orb") && !n.Name.EndsWith("Glow"))
                .Where(n => n.SpriteKey != "proc:solid_circle")
                .Select(n => n.Name)
                .ToList();

            CollectionAssert.IsEmpty(offenders);
            Assert.AreEqual(TalentPage.PathCount * TalentScreen.OrbCount,
                Walk(TalentScreen.Build().Root).Count(n => n.Name.StartsWith("Orb") && !n.Name.EndsWith("Glow")),
                "fixture: one orb per slot per path");
        }

        [Test]
        public void TheDetailPlateStartsHidden()
        {
            // Nothing is selected when the screen opens, and a plate with
            // nothing written on it is just a coloured rectangle. The
            // controller shows it the moment an orb is picked.
            var screen = TalentScreen.Build();

            Assert.IsTrue(screen.DetailPlate.Node.StartInactive);
        }

        [Test]
        public void TheDetailTextRidesOnThePlateSoHidingOneHidesBoth()
        {
            // If the labels were siblings of the plate rather than its
            // children, hiding the plate would leave the text floating over the
            // sky -- the same class of bug in the other direction.
            var plate = Walk(TalentScreen.Build().Root).First(n => n.Name == "TalentDetailPlate");
            var names = plate.Children.Select(c => c.Name).ToList();

            CollectionAssert.Contains(names, "TalentDetailName");
            CollectionAssert.Contains(names, "TalentDetailBody");
        }

        [Test]
        public void EveryExemptionStatesARealReason()
        {
            foreach (var node in Walk(TalentScreen.Build().Root))
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
