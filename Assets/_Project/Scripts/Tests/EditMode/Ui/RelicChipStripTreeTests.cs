using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The "Gained" chip strip (docs/PLAN_BUGS_QA.md C2): its runtime
    // layout arithmetic, and where the two surfaces put it. The audits of the
    // whole Reckoning and event trees (ReckoningScreenTests, EventScreenTests)
    // cover overlap and containment; this pins what they cannot see.
    public class RelicChipStripTreeTests
    {
        // ---- RelicChipLayout ----------------------------------------------------------

        [Test]
        public void TwoShortNames_TakeTheirOwnWidths_LeftToRight()
        {
            var slots = RelicChipLayout.Lay(new List<float> { 150f, 210f }, 948f, 8f, 96f);

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(0f, slots[0].Left, 0.001f);
            Assert.AreEqual(150f, slots[0].Width, 0.001f);
            Assert.AreEqual(158f, slots[1].Left, 0.001f);
            Assert.AreEqual(210f, slots[1].Width, 0.001f);
        }

        [Test]
        public void ALongName_IsCappedAtAnEqualShare_AndATinyOneRaisedToTheFloor()
        {
            // Two chips in 300: share = (300 - 8) / 2 = 146.
            var slots = RelicChipLayout.Lay(new List<float> { 500f, 40f }, 300f, 8f, 96f);

            Assert.AreEqual(146f, slots[0].Width, 0.001f);
            Assert.AreEqual(154f, slots[1].Left, 0.001f);
            Assert.AreEqual(96f, slots[1].Width, 0.001f);
        }

        [Test]
        public void FourAtTheirShare_FillTheAreaExactly()
        {
            // The case the tree is emitted at: (612 - 24) / 4 = 147 each.
            var slots = RelicChipLayout.Lay(new List<float> { 999f, 999f, 999f, 999f }, 612f, 8f, 96f);

            Assert.AreEqual(147f, slots[3].Width, 0.001f);
            Assert.AreEqual(612f, slots[3].Left + slots[3].Width, 0.001f);
            Assert.AreEqual(147f, RelicChipLayout.Share(4, 612f, 8f), 0.001f);
        }

        [Test]
        public void NoChips_NoSlots()
        {
            CollectionAssert.IsEmpty(RelicChipLayout.Lay(new List<float>(), 612f, 8f, 96f));
        }

        // ---- the Reckoning --------------------------------------------------------------

        // Directly above Continue, and clear of the lowest row of every page:
        // Continue carries the summary's one AllowOverlap, so the audit cannot
        // see a strip under it, and the pages are exclusive with each other but
        // not with the strip.
        [Test]
        public void OnTheReckoning_TheStripSitsBetweenEveryPagesRowsAndContinue()
        {
            foreach (var frame in UiFrames.All)
            {
                var all = UiSolver.Solve(ReckoningScreen.Build().Root, frame).Descendants().ToList();
                UiRect Rect(string name) => all.Single(n => n.Name == name).Rect;

                var strip = Rect("ReckoningGainedStrip");
                var continueRect = Rect("ReckoningContinueButton");

                Assert.AreEqual(1f, strip.Bottom - continueRect.Top, 0.01f, "1px above Continue");
                Assert.AreEqual(continueRect.Centre.X, strip.Centre.X, 0.01f, "centred over Continue");

                var lowest = new[]
                {
                    "ReckoningRow" + (ReckoningScreen.RowCount - 1),
                    "ReckoningTally" + (ReckoningScreen.RowCount - 1),
                    "ReckoningRelic" + (ReckoningScreen.RelicRowCount - 1),
                };
                foreach (var name in lowest)
                {
                    Assert.Greater(Rect(name).Bottom, strip.Top, $"{name} reaches into the chips at {UiFrames.Describe(frame)}");
                }

                for (int i = 0; i < RelicChipTree.ChipCount; i++)
                {
                    var chip = Rect("ReckoningGainedChip" + i);
                    Assert.GreaterOrEqual(chip.Left, strip.Left - 0.01f);
                    Assert.LessOrEqual(chip.Right, strip.Right + 0.01f, $"chip {i} leaves the strip");
                }
            }
        }

        [Test]
        public void OnTheReckoning_TheChipsAndTooltipAreOnTheSummaryPhase_TooltipLast()
        {
            var screen = ReckoningScreen.Build();
            var summary = screen.SummaryPhase.Node.Children.Select(c => c.Name).ToList();

            Assert.Contains("ReckoningGainedStrip", summary);
            Assert.AreEqual("ReckoningGainedTooltip", summary.Last(), "drawn over everything on the summary");
            Assert.AreEqual(RelicChipTree.ChipCount, screen.GainedChips.Count);
        }

        // ---- the event page --------------------------------------------------------------

        // The legacy page reserves the band: chips stand GainedToChoicesGap over
        // the first row and under the effects line.
        [Test]
        public void OnTheLegacyEventPage_TheStripSitsBetweenTheEffectsLineAndTheFirstRow()
        {
            var all = UiSolver.Solve(EventScreen.Build().Root, UiFrames.Reference).Descendants().ToList();
            UiRect Rect(string name) => all.Single(n => n.Name == name).Rect;

            var strip = Rect("EventGainedStrip");
            var firstRow = Rect("EventChoice0");
            var effects = Rect("EventEffects");

            Assert.AreEqual(12f, strip.Bottom - firstRow.Top, 0.01f);
            Assert.AreEqual(8f, effects.Bottom - strip.Top, 0.01f);
            Assert.AreEqual(firstRow.Left, strip.Left, 0.01f, "lined up with the rows");
            Assert.AreEqual(firstRow.Right, strip.Right, 0.01f);
        }

        [Test]
        public void OnTheEventPage_TheStripIsOutsideTheRowsPanel_AndTheTooltipIsLast()
        {
            var screen = EventScreen.Build();
            var children = screen.Root.Children.Select(c => c.Name).ToList();

            Assert.Greater(children.IndexOf("EventGainedStrip"), children.IndexOf("EventChoices"),
                "a sibling drawn after the rows: the rows hide during dialogue and the chips must not");
            Assert.AreEqual("EventGainedTooltip", children.Last());
        }
    }
}
