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

            Assert.AreEqual(0f, RewardTrackLayout.NodeOffsetX(middle), 0.001f);

            Assert.Less(RewardTrackLayout.NodeOffsetX(RewardTrackLayout.FirstLevel), 0f,
                "the first node should sit LEFT of the content's centre");
            Assert.Greater(RewardTrackLayout.NodeOffsetX(RewardTrack.MaxLevel), 0f,
                "the last node should sit RIGHT of the content's centre");
        }

        // ---- scrolling ------------------------------------------------------------

        // The frame's own width: the viewport spans the detail area.
        private const float Window = 1196f;

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

        [TestCase(3)]
        [TestCase(10)]
        [TestCase(25)]
        [TestCase(30)]
        [TestCase(40)]
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

            // 29 filler levels against 10 milestones -- if this ever inverts,
            // the rail has lost its landmarks.
            Assert.AreEqual(29, filler);
            Assert.Greater(RewardTrackLayout.MilestoneDiameter, RewardTrackLayout.NodeDiameter);
        }

        [Test]
        public void ThereAreTenLandmarks()
        {
            var levels = RewardTrackLayout.MilestoneLevels().ToList();

            Assert.AreEqual(10, levels.Count);
            CollectionAssert.IsOrdered(levels, "the ribbon numbers its dots in this order");
            CollectionAssert.AllItemsAreUnique(levels);
        }

        // ---- the bands ------------------------------------------------------------

        // The frame is the CHARACTER tab's detail area, 1196 x 660 (spec 3.4),
        // and each band is written as a y down from its top edge.
        [Test]
        public void TheFrameIsTheDetailArea()
        {
            Assert.AreEqual(1196f, RewardTrackLayout.FrameWidth, 0.001f);
            Assert.AreEqual(660f, RewardTrackLayout.FrameHeight, 0.001f);
            Assert.AreEqual(330f, RewardTrackLayout.ContentTop, 0.001f);
        }

        [Test]
        public void TheBandsStackDownTheFrame()
        {
            // summary 0..56, card 58..238, band 244..522, ribbon 562..648.
            Assert.AreEqual(302f, RewardTrackLayout.SummaryCentreY, 0.001f, "summary row");
            Assert.AreEqual(182f, RewardTrackLayout.CardCentreY, 0.001f, "focus card");
            Assert.AreEqual(-53f, RewardTrackLayout.BandCentreY, 0.001f, "rail band");
            Assert.AreEqual(-275f, RewardTrackLayout.RibbonCentreY, 0.001f, "ascent ribbon");
        }

        [Test]
        public void TheBandsDoNotOverlapAndStayInTheFrame()
        {
            float cardTop = 148f - RewardTrackLayout.CardHeight * 0.5f;
            float cardBottom = 148f + RewardTrackLayout.CardHeight * 0.5f;

            Assert.GreaterOrEqual(RewardTrackLayout.SummaryYFromTop - RewardTrackLayout.CollectHeight * 0.5f, 0f,
                "the collect plate leaves the frame's top");
            Assert.LessOrEqual(RewardTrackLayout.SummaryYFromTop + RewardTrackLayout.CollectHeight * 0.5f, cardTop);
            Assert.LessOrEqual(cardBottom, RewardTrackLayout.BandTopFromTop);
            Assert.LessOrEqual(RewardTrackLayout.BandBottomFromTop,
                RewardTrackLayout.RibbonLabelYFromTop - RewardTrackLayout.RibbonLabelHeight * 0.5f);
            Assert.LessOrEqual(RewardTrackLayout.RibbonLabelYFromTop + RewardTrackLayout.RibbonLabelHeight * 0.5f,
                RewardTrackLayout.RibbonTopFromTop);
            Assert.LessOrEqual(RewardTrackLayout.RibbonBottomFromTop, RewardTrackLayout.FrameHeight);
        }

        // The rail sits 184 below the band's top and 94 above its foot, and
        // RailOffsetY is what moves the scrolled content (measured from the
        // rail) onto it.
        [Test]
        public void TheRailSitsWhereTheBandPutsIt()
        {
            Assert.AreEqual(-98f, RewardTrackLayout.BandCentreY + RewardTrackLayout.RailOffsetY, 0.001f);
            Assert.AreEqual(-45f, RewardTrackLayout.RailOffsetY, 0.001f);
            Assert.AreEqual(184f, RewardTrackLayout.RailToBandTop, 0.001f);
        }

        // Seven nodes across the frame (spec 3.4).
        [Test]
        public void TheWindowShowsSevenNodes()
        {
            Assert.AreEqual(170f, RewardTrackLayout.NodePitch, 0.001f);
            Assert.AreEqual(7, (int)(Window / RewardTrackLayout.NodePitch));
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

            // And it stops above whatever node it points at: 31 above the
            // rail, over a milestone's plate ring at 29.
            float tickBottom = RewardTrackLayout.NextMarkTopY - RewardTrackLayout.NextMarkHeight;
            Assert.AreEqual(31f, tickBottom, 0.001f);
            Assert.GreaterOrEqual(tickBottom,
                RewardTrackLayout.PlateRingDiameter(RewardTrack.MaxLevel) * 0.5f - 0.001f,
                "the NEXT mark's tick runs into the milestone ring it hangs over");
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

        // Collect at the frame's left edge, the five parts after it, and the
        // reward's name running to the frame's right edge.
        [Test]
        public void TheSummaryRunsFromTheCollectButtonToTheFramesEdge()
        {
            Assert.AreEqual(-598f, RewardTrackLayout.CollectCentreX - RewardTrackLayout.CollectWidth * 0.5f, 0.001f);

            float collectRight = RewardTrackLayout.CollectCentreX + RewardTrackLayout.CollectWidth * 0.5f;
            float firstLeft = RewardTrackLayout.SummaryPartX(0) - RewardTrackLayout.LevelWordWidth * 0.5f;
            Assert.AreEqual(20f, firstLeft - collectRight, 0.001f, "the gap after the collect button");

            float lastRight = RewardTrackLayout.SummaryPartX(4) + RewardTrackLayout.NextRewardWidth * 0.5f;
            Assert.AreEqual(598f, lastRight, 0.001f);
            Assert.AreEqual(484f, RewardTrackLayout.NextRewardWidth, 0.001f,
                "the longest reward name measures about 450 at 20");
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

        // THE LAST LEVEL IS THE CONSTRAINT, not the widest numeral: its label
        // has to stay inside the ribbon. Every landmark's number does.
        [Test]
        public void EveryRibbonNumberFitsInsideTheRibbon()
        {
            float half = RewardTrackLayout.RibbonWidth * 0.5f;

            foreach (int level in RewardTrackLayout.MilestoneLevels())
            {
                float x = RewardTrackLayout.RibbonNumberX(level);
                Assert.LessOrEqual(Mathf.Abs(x) + RewardTrackLayout.RibbonNumberWidth * 0.5f, half + 0.001f,
                    $"level {level}'s number runs off the end of the ribbon");
            }

            Assert.AreEqual(1148f, RewardTrackLayout.RibbonWidth, 0.001f);
        }

        [Test]
        public void TheRibbonWindowIsTheFractionOfTheTrackTheRailCanShow()
        {
            float width = RewardTrackLayout.RibbonWindowWidth(Window);

            // 1196 of 6,800 drawn across 1148. The content is 40 node
            // pitches wide -- 39 levels plus half a pitch of padding at each
            // end -- at 170 apiece.
            Assert.AreEqual(1196f / 6800f * 1148f, width, 0.001f);

            // Which is about seven nodes of thirty-nine: the clearest
            // statement this screen makes about how little of the track the
            // rail shows.
            Assert.Less(width, RewardTrackLayout.RibbonWidth * 0.25f);
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
            Assert.AreEqual(-598f, RewardTrackLayout.ScrollFor(RewardTrackLayout.FirstLevel, Window), 0.001f);
            Assert.AreEqual(-598f, RewardTrackLayout.ScrollForFraction(0f, Window), 0.001f,
                "grabbing the very start of the ribbon does not match opening on level 2");

            Assert.AreEqual(-6202f, RewardTrackLayout.ScrollFor(RewardTrack.MaxLevel, Window), 0.001f);
            Assert.AreEqual(-6202f, RewardTrackLayout.ScrollForFraction(1f, Window), 0.001f,
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
        [TestCase(21)]
        [TestCase(40)]
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
            float centre = -RewardTrackLayout.NodeX(21);

            Assert.AreEqual(21, RewardTrackLayout.LevelAtCentre(centre - 84f));
            Assert.AreEqual(21, RewardTrackLayout.LevelAtCentre(centre + 84f));
        }

        [Test]
        public void TheLevelAtTheCentreNeverLeavesTheTrack()
        {
            Assert.AreEqual(RewardTrackLayout.FirstLevel, RewardTrackLayout.LevelAtCentre(100000f));
            Assert.AreEqual(RewardTrack.MaxLevel, RewardTrackLayout.LevelAtCentre(-100000f));
        }
    }
}
