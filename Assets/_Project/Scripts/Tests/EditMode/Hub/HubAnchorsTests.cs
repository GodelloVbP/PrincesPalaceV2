using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Where the Divine Principality's buildings stand.
    //
    // Literals, not recomputation. Deriving the expected value from the same
    // StageLayout call the production code makes would assert that arithmetic
    // equals itself -- the tautology CLAUDE.md's fifth gotcha exists about.
    public class HubAnchorsTests
    {
        [Test]
        public void TheNearBuildingsStandLowAndWide()
        {
            var principality = HubAnchors.PositionFor(HubAnchors.Principality);

            Assert.AreEqual(-673f, principality.X, 0.5f);
            Assert.AreEqual(-69.6f, principality.Y, 0.5f);
        }

        [Test]
        public void TheFarBuildingsRiseAndPullIn()
        {
            // Floating things get smaller AND higher with distance. Only
            // shrinking them would read as a wall of buildings at one depth.
            var relics = HubAnchors.PositionFor(HubAnchors.Relics);

            Assert.AreEqual(295f, relics.X, 0.5f);
            Assert.AreEqual(216f, relics.Y, 0.5f);
        }

        [Test]
        public void DistanceShrinksABuilding()
        {
            Assert.Greater(
                HubAnchors.ScaleFor(HubAnchors.Principality),
                HubAnchors.ScaleFor(HubAnchors.Relics),
                "the far plot must read smaller than the near one");
        }

        [Test]
        public void EveryPlotSitsInsideTheDepthCurvesRange()
        {
            // Depth outside [0,1] is clamped by StageLayout, so a typo would
            // silently pin a building to an endpoint rather than fail.
            foreach (var plot in new[]
            {
                HubAnchors.Principality, HubAnchors.CharacterSheet,
                HubAnchors.Talents, HubAnchors.Relics,
            })
            {
                Assert.GreaterOrEqual(plot.Depth, 0f);
                Assert.LessOrEqual(plot.Depth, 1f);
                Assert.AreEqual(1f, System.Math.Abs(plot.Lateral), 0.0001f, "lateral is a side, not a distance");
            }
        }

        [Test]
        public void TheFourPlotsAlternateSides()
        {
            // Near-left, near-right, far-left, far-right. A composition with
            // both near buildings on one side leaves half the terrace empty.
            Assert.AreEqual(-1f, HubAnchors.Principality.Lateral);
            Assert.AreEqual(1f, HubAnchors.CharacterSheet.Lateral);
            Assert.AreEqual(-1f, HubAnchors.Talents.Lateral);
            Assert.AreEqual(1f, HubAnchors.Relics.Lateral);
        }

        [Test]
        public void NoTwoPlotsShareADepth()
        {
            // Painter's order IS declaration order, so two buildings at the same
            // depth would have an arbitrary winner where they overlap.
            var depths = new[]
            {
                HubAnchors.Principality.Depth, HubAnchors.CharacterSheet.Depth,
                HubAnchors.Talents.Depth, HubAnchors.Relics.Depth,
            };

            CollectionAssert.AllItemsAreUnique(depths);
        }

        [Test]
        public void TheGateStandsOnTheTerraceBelowEverythingElse()
        {
            // It is the only thing with its feet on solid ground; the annexes
            // float out over the void above it.
            Assert.AreEqual(0f, HubAnchors.Gate.X, 0.0001f, "dead centre");
            Assert.Less(HubAnchors.Gate.Y, HubAnchors.NearY, "lower than the nearest floating plot");
        }

        [Test]
        public void TheGateIsTheLargestThingOnScreen()
        {
            // The descent is the primary action and the composition says so
            // before any label does.
            foreach (var plot in new[]
            {
                HubAnchors.Principality, HubAnchors.CharacterSheet,
                HubAnchors.Talents, HubAnchors.Relics,
            })
            {
                Assert.Greater(HubAnchors.GateSize, HubAnchors.SizeFor(plot));
            }
        }

        [Test]
        public void TheCaptionHangsBelowTheArch()
        {
            // The arch's centre is a swirling void the eye has to read as an
            // opening; a caption across it would fill the hole.
            Assert.Less(HubAnchors.GateCaptionOffset, 0f);

            // And it clears the art rather than merely being below centre --
            // the failure the first version shipped, where every nameplate
            // stayed printed across the building it named.
            // Below the ART, which is what the second wrong answer got wrong:
            // half the box put one caption under the book, one across the stall
            // and one above the shrine, because each sheet pads differently.
            foreach (var plot in new[]
            {
                HubAnchors.Principality, HubAnchors.CharacterSheet,
                HubAnchors.Talents, HubAnchors.Relics,
            })
            {
                float size = HubAnchors.SizeFor(plot);
                float caption = HubAnchors.CaptionOffsetFor(size, plot.ContentBottom);
                float artBottom = size * (0.5f - plot.ContentBottom);

                Assert.Less(caption, artBottom, "the plate must clear the art, not merely the centre");
            }
        }

        [Test]
        public void EveryPlotKnowsWhereItsOwnArtEnds()
        {
            // Measured off the PNGs, not guessed. A default 0.5 would put every
            // caption back on the building.
            foreach (var plot in new[]
            {
                HubAnchors.Principality, HubAnchors.CharacterSheet,
                HubAnchors.Talents, HubAnchors.Relics,
            })
            {
                Assert.Greater(plot.ContentBottom, 0.8f, "no sheet pads that heavily at the bottom");
                Assert.LessOrEqual(plot.ContentBottom, 1f);
            }
        }

        [Test]
        public void SizeIsBakedRatherThanLeftToATransform()
        {
            // The audit measures declared boxes. A full-size node scaled down by
            // a transform would be reported as overlapping things it does not
            // touch.
            Assert.Less(HubAnchors.SizeFor(HubAnchors.Relics), HubAnchors.Relics.BaseSize);
            Assert.AreEqual(
                HubAnchors.Relics.BaseSize * StageLayout.ScaleForDepth(HubAnchors.Relics.Depth),
                HubAnchors.SizeFor(HubAnchors.Relics),
                0.0001f);
        }
    }
}
