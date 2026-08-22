using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    public class RewardTrackLayoutTests
    {
        [Test]
        public void ThereIsOneNodeForEveryLevelThatPays()
        {
            // Level 1 is where a character starts, not somewhere they arrive,
            // so the rail runs 2..100.
            Assert.AreEqual(2, RewardTrackLayout.FirstLevel);
            Assert.AreEqual(RewardTrack.MaxLevel - 1, RewardTrackLayout.NodeCount);
        }

        [Test]
        public void NodesAreEvenlySpacedAlongTheRail()
        {
            for (int level = RewardTrackLayout.FirstLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.AreEqual(RewardTrackLayout.NodePitch,
                    RewardTrackLayout.NodeX(level) - RewardTrackLayout.NodeX(level - 1), 0.001f,
                    $"level {level} is not one pitch from level {level - 1}");
            }
        }

        // Every node sits inside the rect it is drawn in, with the end padding
        // keeping the first and last off the edge.
        [Test]
        public void EveryNodeSitsInsideTheContent()
        {
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                float half = RewardTrackLayout.CaptionWidth * 0.5f;

                Assert.GreaterOrEqual(RewardTrackLayout.NodeX(level) - half, 0f,
                    $"level {level}'s caption starts left of the content rect");
                Assert.LessOrEqual(RewardTrackLayout.NodeX(level) + half, RewardTrackLayout.ContentWidth,
                    $"level {level}'s caption runs past the content rect");
            }
        }

        // THE MISTAKE THIS PAIR EXISTS TO PREVENT. NodeX is measured from the
        // content's LEFT EDGE because that is how a rail is easy to reason
        // about; Ui.Place.At measures from a parent's CENTRE. Placing NodeX
        // directly put every node half a content-width too far right, and
        // UiAudit refused the build with "TrackDot100 escapes its parent:
        // 9,330px past the right".
        [Test]
        public void ThePlacementOffsetIsCentreRelativeAndNodeXIsNot()
        {
            int middle = RewardTrackLayout.FirstLevel + RewardTrackLayout.NodeCount / 2;

            Assert.AreEqual(RewardTrackLayout.NodeX(middle) - RewardTrackLayout.ContentWidth * 0.5f,
                RewardTrackLayout.NodeOffsetX(middle), 0.001f);

            Assert.Less(RewardTrackLayout.NodeOffsetX(RewardTrackLayout.FirstLevel), 0f,
                "the first node should sit LEFT of the content's centre");
            Assert.Greater(RewardTrackLayout.NodeOffsetX(RewardTrack.MaxLevel), 0f,
                "the last node should sit RIGHT of the content's centre");
        }

        // ---- scrolling ------------------------------------------------------------

        private const float Window = 1520f;

        [Test]
        public void ScrollingCentresTheLevelAskedFor()
        {
            int middle = RewardTrackLayout.FirstLevel + RewardTrackLayout.NodeCount / 2;
            float left = RewardTrackLayout.ScrollFor(middle, Window);

            // The node's position within the window, once the content has slid.
            float onScreen = left + RewardTrackLayout.NodeX(middle);

            Assert.AreEqual(0f, onScreen, 0.001f,
                "a level in the middle of the track should land in the middle of the window");
        }

        // CLAMPED AT BOTH ENDS. Without this, opening on level 2 scrolls the
        // rail off the right of the window and opening on level 100 scrolls it
        // off the left -- in both cases the player is looking at empty space
        // with their own progress just out of frame.
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(99)]
        [TestCase(100)]
        public void TheRailAlwaysFillsTheWindow(int level)
        {
            float left = RewardTrackLayout.ScrollFor(level, Window);

            Assert.LessOrEqual(left, -Window * 0.5f + 0.001f,
                $"at level {level} the content's left edge is inside the window, showing dead space");
            Assert.GreaterOrEqual(left + RewardTrackLayout.ContentWidth, Window * 0.5f - 0.001f,
                $"at level {level} the content's right edge is inside the window, showing dead space");
        }

        // ---- milestones -----------------------------------------------------------

        [TestCase(10)]
        [TestCase(25)]
        [TestCase(50)]
        [TestCase(90)]
        [TestCase(100)]
        public void AMilestoneLevelDrawsLarge(int level)
        {
            Assert.IsTrue(RewardTrackLayout.IsMilestone(level));
        }

        [Test]
        public void FillerDoesNotDrawLarge()
        {
            int filler = 0;
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (!RewardTrackLayout.IsMilestone(level)) filler++;
            }

            // 87 filler levels against 12 milestones -- if this ever inverts,
            // the rail has lost its landmarks.
            Assert.AreEqual(87, filler);
            Assert.Greater(RewardTrackLayout.MilestoneDiameter, RewardTrackLayout.NodeDiameter);
        }

        [Test]
        public void ThereAreTwelveLandmarks()
        {
            var levels = RewardTrackLayout.MilestoneLevels().ToList();

            Assert.AreEqual(12, levels.Count);
            CollectionAssert.IsOrdered(levels, "the ribbon numbers its dots in this order");
            CollectionAssert.AllItemsAreUnique(levels);
        }

        // ---- the bands ------------------------------------------------------------
        //
        // Handoff section 1, checked against the numbers as the handoff states
        // them, so this file can be read beside that document.

        [Test]
        public void TheBandsSitWhereTheHandoffPutsThem()
        {
            // Panel 1600x804, y measured down from its top-left.
            Assert.AreEqual(359f, RewardTrackLayout.SummaryCentreY, 0.001f, "summary band, 20..66");
            Assert.AreEqual(209f, RewardTrackLayout.CardCentreY, 0.001f, "focus card, 118..268");
            Assert.AreEqual(6f, RewardTrackLayout.BandCentreY, 0.001f, "rail band, 290..502");
            Assert.AreEqual(-323f, RewardTrackLayout.RibbonCentreY, 0.001f, "ascent ribbon, 682..768");
        }

        // THE ONE THE WHOLE BAND IS BUILT AROUND. The rail has to land on the
        // panel's exact vertical centre while the band around it is 112 above
        // and 100 below, and RailOffsetY is what reconciles those.
        [Test]
        public void TheRailLandsOnThePanelsExactVerticalCentre()
        {
            Assert.AreEqual(0f, RewardTrackLayout.BandCentreY + RewardTrackLayout.RailOffsetY, 0.001f);
            Assert.AreEqual(-6f, RewardTrackLayout.RailOffsetY, 0.001f);
        }

        // The scrolled content is measured from the RAIL at its centre, not
        // from the band. Sizing it to the band instead put the tallest thing on
        // a node six pixels outside its parent at all four aspects, and UiAudit
        // refused the build -- which is the whole reason this test exists.
        [Test]
        public void TheNextMarkHangsInTheGapAboveTheNode()
        {
            float half = RewardTrackLayout.ScrollContentHeight * 0.5f;

            Assert.LessOrEqual(RewardTrackLayout.CaptionY + RewardTrackLayout.CaptionHeight * 0.5f,
                half + 0.001f,
                "the caption box escapes the content rect it scrolls inside");

            // BELOW THE CAPTION, NOT ABOVE IT, which is the whole difference
            // between this and the chevron it replaced. That one lived in the
            // 14px gap between the caption's top edge and the band's, which put
            // it a hundred pixels above the node it pointed at and made it read
            // as dust on the band's edge. This hangs in the strip between the
            // caption's fixed lower edge and the disc.
            Assert.LessOrEqual(RewardTrackLayout.NextMarkTopY,
                RewardTrackLayout.CaptionBottomY + 0.001f,
                "the NEXT mark has climbed above the caption again");

            // And it stops above the filler disc it points at, which is
            // eighty-seven of the ninety-nine nodes it can hang over.
            //
            // NOT ABOVE A MILESTONE'S, and that is the design's own geometry
            // rather than an oversight in this test: a milestone disc reaches
            // 22 above the rail and its plate ring 29, so on those twelve
            // levels the tick's last few pixels cross the ring. The tick fades
            // to nothing along its length, so what crosses is its transparent
            // end -- and shortening it for those twelve would leave it hanging
            // in mid-air on the other eighty-seven.
            float tickBottom = RewardTrackLayout.NextMarkTopY - RewardTrackLayout.NextMarkHeight;
            Assert.GreaterOrEqual(tickBottom, RewardTrackLayout.NodeDiameter * 0.5f - 0.001f,
                "the NEXT mark's tick runs into the filler disc it hangs over");
        }

        [Test]
        public void EveryNodeFitsBetweenTheRailAndTheBandsEdges()
        {
            float half = RewardTrackLayout.ScrollContentHeight * 0.5f;

            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                float top = RewardTrackLayout.CaptionY + RewardTrackLayout.CaptionHeight * 0.5f;
                float bottom = RewardTrackLayout.LevelNumberY
                               - RewardTrackLayout.LevelNumberHeight * 0.5f;

                Assert.LessOrEqual(top, half + 0.001f, $"level {level}'s caption leaves the band");
                Assert.GreaterOrEqual(bottom, -half - 0.001f, $"level {level}'s number leaves the band");

                // The orbit ring is the widest DRAWN EDGE a milestone carries --
                // wider than the plate ring it sits outside -- and it has to
                // clear both the caption above and the number below.
                //
                // THE AURA IS WIDER STILL AND IS NOT CHECKED HERE, on purpose.
                // It is a radial glow that has faded to nothing well before its
                // own rect ends, so it is meant to reach behind the level
                // number; a rect measurement of a gradient is a measurement of
                // where it stopped being drawn, not of where it stopped being
                // seen. It stays inside the content rect, which is what the
                // caption bound above already checks for everything.
                float ring = RewardTrackLayout.PlateRingDiameter(level) * 0.5f;

                Assert.Less(ring, RewardTrackLayout.CaptionY - RewardTrackLayout.CaptionHeight * 0.5f,
                    $"level {level}'s plate ring runs into its caption");
                Assert.Less(ring, -(RewardTrackLayout.LevelNumberY
                                    + RewardTrackLayout.LevelNumberHeight * 0.5f),
                    $"level {level}'s plate ring runs into its level number");

                // THE AURA IS WIDER THAN THE RING AND IS BOUNDED DIFFERENTLY.
                // It is a radial glow, faded to nothing well before its own
                // rect ends, so it is allowed to reach behind the level number
                // -- a rect measurement of a gradient says where it stopped
                // being drawn, not where it stopped being seen. What it may not
                // do is leave the band, because that IS a hard edge: the
                // viewport would cut it.
                if (!RewardTrackLayout.IsMilestone(level)) continue;

                Assert.LessOrEqual(RewardTrackLayout.MilestoneAuraSize * 0.5f, half + 0.001f,
                    $"level {level}'s aura is clipped by the band it sits in");
            }
        }

        // ---- the summary row ----------------------------------------------------------

        // The centred summary is computed from the two buttons rather than
        // measured, so relabelling either one cannot leave it underneath. That
        // collision really happened: a full-width label centred under CLOSE,
        // refused by UiAudit because the button draws later and would have
        // taken the clicks while hiding the end of the sentence.
        [Test]
        public void TheSummaryClearsBothButtonsOnItsRow()
        {
            float summaryHalf = RewardTrackLayout.SummaryWidth * 0.5f;

            float collectRight = RewardTrackLayout.CollectCentreX
                                 + RewardTrackLayout.CollectWidth * 0.5f;
            float closeLeft = RewardTrackLayout.CloseCentreX - RewardTrackLayout.CloseWidth * 0.5f;

            Assert.Less(-summaryHalf, 0f, "the summary has no width at all");
            Assert.Greater(-summaryHalf, collectRight, "the summary sits under the collect button");
            Assert.Less(summaryHalf, closeLeft, "the summary sits under CLOSE");
        }

        [Test]
        public void BothButtonsKeepTheSameClearanceFromThePanelsEdge()
        {
            float half = SystemMenuLayout.PanelWidth * 0.5f;

            Assert.AreEqual(RewardTrackLayout.SummaryInsetX,
                RewardTrackLayout.CollectCentreX - RewardTrackLayout.CollectWidth * 0.5f + half,
                0.001f);
            Assert.AreEqual(RewardTrackLayout.SummaryInsetX,
                half - RewardTrackLayout.CloseCentreX - RewardTrackLayout.CloseWidth * 0.5f,
                0.001f);
        }

        // ---- the seal pip -------------------------------------------------------------

        // The pip STRADDLES the rim rather than sitting inside it, and it does
        // so DIAGONALLY -- which is the whole subtlety, and the reason this is
        // a test rather than an assertion about one number.
        //
        // The rule is stated on ONE AXIS: the pip's centre sits on the disc's
        // rim, half a pixel inside, in x and in y. That is the whole placement,
        // and it is what makes the pip look the same on a 26px disc and a 44px
        // one despite being the same 15px on both.
        //
        // WHAT IT DOES RADIALLY IS NOT THE SAME AT THE TWO SIZES, which is
        // worth pinning because it looks like a bug in one of them and is not.
        // The offset applies to both axes, so the pip's centre is root two
        // times it from the disc's -- and against a circular rim that means a
        // filler pip cuts the edge while a milestone pip comes to rest just
        // outside it, tangent by about a pixel. Both read as applied to the
        // node; neither sits in it.
        [Test]
        public void TheSealPipSitsOnTheRimAtBothSizes()
        {
            // The design's own number for a milestone: it states the pip's LEFT
            // EDGE at d/2 - 8, and 8 is the pip's radius plus the half-pixel
            // bite, so the centre lands at d/2 - 0.5.
            Assert.AreEqual(RewardTrackLayout.MilestoneDiameter * 0.5f - 8f
                            + RewardTrackLayout.SealPipSize * 0.5f,
                RewardTrackLayout.SealPipOffset(10), 0.001f, "44px disc");

            foreach (int level in new[] { 3, 10 })
            {
                float rim = RewardTrackLayout.DiameterOf(level) * 0.5f;
                const float Half = RewardTrackLayout.SealPipSize * 0.5f;
                float offset = RewardTrackLayout.SealPipOffset(level);

                Assert.AreEqual(rim, offset + RewardTrackLayout.SealPipRimBite, 0.001f,
                    $"level {level}'s pip has come off the rim it is meant to sit on");

                // Straddling, as the rects the audit measures see it: the pip
                // reaches past the disc's edge and back inside it.
                Assert.Greater(offset + Half, rim,
                    $"level {level}'s pip sits entirely inside the disc instead of cutting its rim");
                Assert.Less(offset - Half, rim,
                    $"level {level}'s pip floats clear of the disc instead of straddling it");
            }
        }

        // ONE SIZE FOR BOTH DISCS, and the thing that makes that safe is WHERE
        // it sits rather than how big it is.
        //
        // The pip was scaled with its disc for one build -- 15 on a milestone,
        // 9 on filler -- on the grounds that a 15px pip on a 26px disc covers
        // the reward's mark. It does, if its centre is placed at the design's
        // d/2 - 8; that number is the pip's LEFT EDGE, and read as a centre it
        // lands the pip's near edge half a pixel past the mark's own middle.
        // Placed as the design places it, the pip's near edge clears the mark
        // by ten pixels on a filler node, and one size can serve both.
        [Test]
        public void TheSealPipNeverCoversTheMarkItSitsBeside()
        {
            foreach (int level in new[] { 3, 10 })
            {
                float offset = RewardTrackLayout.SealPipOffset(level);
                float radial = Mathf.Sqrt(offset * offset * 2f);
                float near = radial - RewardTrackLayout.SealPipSize * 0.5f;

                Assert.Greater(near, RewardTrackLayout.IconSizeOf(level) * 0.5f,
                    $"level {level}'s pip overlaps its own reward mark");
            }
        }

        // ---- the ascent ribbon --------------------------------------------------------

        [Test]
        public void EveryLevelFitsOnTheRibbon()
        {
            float half = RewardTrackLayout.RibbonWidth * 0.5f;

            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                float x = RewardTrackLayout.RibbonOffsetX(level);

                // Measured at the WAITING width, which is what the tree emits:
                // a tick is built at its largest and shrunk by the controller,
                // never the other way about, so this is the rect UiAudit sees.
                Assert.LessOrEqual(Mathf.Abs(x) + RewardTrackLayout.RibbonWaitingTickWidth * 0.5f,
                    half + 0.001f, $"level {level}'s ribbon tick leaves the ribbon");
            }
        }

        // LEVEL 100 IS THE CONSTRAINT, not the widest numeral, and the two are
        // not the same question. Its dot sits at 705.6 of the ribbon's own 720,
        // so its label has 14.4px either side -- at 40 wide it escaped by 5.6
        // and UiAudit refused the build.
        [Test]
        public void TheLastRibbonNumberFitsInsideTheRibbon()
        {
            float half = RewardTrackLayout.RibbonWidth * 0.5f;
            float right = RewardTrackLayout.RibbonOffsetX(RewardTrack.MaxLevel)
                          + RewardTrackLayout.RibbonNumberWidth * 0.5f;

            Assert.LessOrEqual(right, half + 0.001f,
                "the last milestone's number runs off the end of the ribbon");
        }

        [Test]
        public void TheRibbonWindowIsTheFractionOfTheTrackTheRailCanShow()
        {
            float width = RewardTrackLayout.RibbonWindowWidth(Window);

            // 1520 of 19,000 drawn across 1440.
            Assert.AreEqual(1520f / 19000f * 1440f, width, 0.001f);

            // Which is about eight and a half nodes of ninety-nine -- the
            // clearest statement this screen makes about how little of the
            // track the rail shows.
            Assert.Less(width, RewardTrackLayout.RibbonWidth * 0.1f);
        }

        // Drag the box to the far left and the rail must be at the far left,
        // not somewhere the clamp put it and the box did not follow.
        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void SeekingByFractionAndDrawingTheWindowAgreeWithEachOther(float fraction)
        {
            float scroll = RewardTrackLayout.ScrollForFraction(fraction, Window);
            float box = RewardTrackLayout.RibbonWindowOffsetX(scroll, Window);

            float half = RewardTrackLayout.RibbonWidth * 0.5f;
            float boxHalf = RewardTrackLayout.RibbonWindowWidth(Window) * 0.5f;

            Assert.GreaterOrEqual(box - boxHalf, -half - 0.001f,
                "the window box hangs off the left of the ribbon");
            Assert.LessOrEqual(box + boxHalf, half + 0.001f,
                "the window box hangs off the right of the ribbon");
        }

        [Test]
        public void SeekingIsClampedTheSameWayScrollingIs()
        {
            Assert.AreEqual(RewardTrackLayout.ScrollFor(RewardTrackLayout.FirstLevel, Window),
                RewardTrackLayout.ScrollForFraction(0f, Window), 0.001f,
                "grabbing the very start of the ribbon does not match opening on level 2");

            Assert.AreEqual(RewardTrackLayout.ScrollFor(RewardTrack.MaxLevel, Window),
                RewardTrackLayout.ScrollForFraction(1f, Window), 0.001f,
                "grabbing the very end of the ribbon does not match opening on level 100");
        }

        // ---- depth of field -------------------------------------------------------------

        [Test]
        public void ANodeInTheMiddleOfTheWindowIsAtFullStrength()
        {
            Assert.AreEqual(1f, RewardTrackLayout.DepthOfFieldAt(0f), 0.001f);
            Assert.AreEqual(1f, RewardTrackLayout.DepthOfFieldAt(299f), 0.001f);
            Assert.AreEqual(1f, RewardTrackLayout.DepthOfFieldAt(-299f), 0.001f);
        }

        [Test]
        public void ANodeFarFromTheCentreFallsToTheFloorAndStops()
        {
            Assert.AreEqual(0.32f, RewardTrackLayout.DepthOfFieldAt(920f), 0.001f);
            Assert.AreEqual(0.32f, RewardTrackLayout.DepthOfFieldAt(19000f), 0.001f);
            Assert.AreEqual(0.32f, RewardTrackLayout.DepthOfFieldAt(-19000f), 0.001f);
        }

        [Test]
        public void TheFalloffIsHalfwayAcrossAtItsMidpoint()
        {
            // 300 + 620/2 = 610, halfway between 1 and 0.32.
            Assert.AreEqual(0.66f, RewardTrackLayout.DepthOfFieldAt(610f), 0.001f);
        }

        // ---- which node the window is looking at ------------------------------------------

        [TestCase(2)]
        [TestCase(47)]
        [TestCase(100)]
        public void TheLevelAtTheCentreIsTheOneScrolledTo(int level)
        {
            Assert.AreEqual(level,
                RewardTrackLayout.LevelAtCentre(-RewardTrackLayout.NodeX(level)));
        }

        // Half a pitch either side of a node is that node's territory. Flooring
        // instead of rounding would make the left arrow a no-op whenever the
        // rail is clamped at its right end.
        [Test]
        public void JustPastANodeIsStillThatNode()
        {
            float centre = -RewardTrackLayout.NodeX(47);

            Assert.AreEqual(47, RewardTrackLayout.LevelAtCentre(centre - 94f));
            Assert.AreEqual(47, RewardTrackLayout.LevelAtCentre(centre + 94f));
        }

        [Test]
        public void TheLevelAtTheCentreNeverLeavesTheTrack()
        {
            Assert.AreEqual(RewardTrackLayout.FirstLevel, RewardTrackLayout.LevelAtCentre(100000f));
            Assert.AreEqual(RewardTrack.MaxLevel, RewardTrackLayout.LevelAtCentre(-100000f));
        }
    }
}
