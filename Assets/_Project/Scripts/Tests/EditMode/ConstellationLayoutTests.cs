using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The sky's arithmetic: where a star sits, and how paging between trees
    // moves.
    //
    // All of it pure, so the whole navigation model is pinned before a single
    // GameObject exists -- which is the point of the layer. The screen and the
    // controller will both call these, and a constellation drawn at build time
    // that disagreed with the one re-anchored at runtime is the exact drift
    // FightSubmenuLayout was extracted to stop.
    public class ConstellationLayoutTests
    {
        [Test]
        public void TheClimbRunsUpTheScreen()
        {
            // A capstone is the thing at the top of a climb. A constellation
            // that grew downward would read as falling.
            float root = ConstellationLayout.StarY(0, 6);
            float capstone = ConstellationLayout.StarY(5, 6);

            Assert.Greater(capstone, root);
        }

        [Test]
        public void TheClimbFillsTheSkyExactly()
        {
            Assert.AreEqual(-ConstellationLayout.SkyHeight * 0.5f, ConstellationLayout.StarY(0, 6), 0.01f);
            Assert.AreEqual(ConstellationLayout.SkyHeight * 0.5f, ConstellationLayout.StarY(5, 6), 0.01f);
        }

        [Test]
        public void ASingleTierSitsOnTheHorizon()
        {
            // Guarded rather than divided by: a one-deep tree is a legitimate
            // shape and must not divide by zero.
            Assert.AreEqual(0f, ConstellationLayout.StarY(0, 1), 0.01f);
        }

        [Test]
        public void TheSpineIsCentredAndTheBranchesSpreadEitherSide()
        {
            Assert.AreEqual(0f, ConstellationLayout.StarX(0, 1), 0.01f);
            Assert.Less(ConstellationLayout.StarX(-1, 1), 0f, "the left branch is left");
            Assert.Greater(ConstellationLayout.StarX(1, 1), 0f, "the right branch is right");
        }

        [Test]
        public void TheBranchesSpreadSymmetrically()
        {
            Assert.AreEqual(
                -ConstellationLayout.StarX(-1, 1),
                ConstellationLayout.StarX(1, 1),
                0.01f);
        }

        [Test]
        public void AWiderTierDoesNotOverflowTheSky()
        {
            // Multiplied out rather than indexed, so a future four-wide tier
            // needs no new case and still fits.
            Assert.AreEqual(ConstellationLayout.SkyWidth * 0.5f, ConstellationLayout.StarX(2, 2), 0.01f);
            Assert.AreEqual(ConstellationLayout.SkyWidth * 0.25f, ConstellationLayout.StarX(1, 2), 0.01f);
        }

        // ---- paging --------------------------------------------------------------

        [Test]
        public void OnlyTheCurrentTreeIsCentred()
        {
            Assert.AreEqual(0f, ConstellationLayout.PageX(1, 1), 0.01f);
            Assert.AreEqual(-ConstellationLayout.PageStride, ConstellationLayout.PageX(0, 1), 0.01f);
            Assert.AreEqual(ConstellationLayout.PageStride, ConstellationLayout.PageX(2, 1), 0.01f);
        }

        [Test]
        public void TheNeighboursAreGenuinelyOffStage()
        {
            // A full screen width apart, so a neighbour never peeks in at the
            // edge -- which is what makes three trees read as three PLACES
            // rather than three tabs.
            Assert.GreaterOrEqual(ConstellationLayout.PageStride, 1920f);
        }

        [Test]
        public void SteppingRightFromTheLeftTreeLandsOnTheMiddle()
        {
            Assert.AreEqual(1, ConstellationLayout.Step(0, +1, 3));
        }

        [Test]
        public void SteppingRightFromTheMiddleLandsOnTheRight()
        {
            Assert.AreEqual(2, ConstellationLayout.Step(1, +1, 3));
        }

        [Test]
        public void SteppingLeftComesBack()
        {
            Assert.AreEqual(1, ConstellationLayout.Step(2, -1, 3));
            Assert.AreEqual(0, ConstellationLayout.Step(1, -1, 3));
        }

        [Test]
        public void TheEndsAreEndsRatherThanAWrap()
        {
            // Wrapping would make the arrows lie about where the edges are: a
            // player who has paged to the far right should be able to tell they
            // are there without counting.
            Assert.AreEqual(0, ConstellationLayout.Step(0, -1, 3), "already at the left");
            Assert.AreEqual(2, ConstellationLayout.Step(2, +1, 3), "already at the right");

            Assert.IsFalse(ConstellationLayout.CanStep(0, -1, 3));
            Assert.IsFalse(ConstellationLayout.CanStep(2, +1, 3));
            Assert.IsTrue(ConstellationLayout.CanStep(1, -1, 3));
            Assert.IsTrue(ConstellationLayout.CanStep(1, +1, 3));
        }

        [Test]
        public void ASingleTreeOffersNoArrowsAtAll()
        {
            Assert.IsFalse(ConstellationLayout.CanStep(0, -1, 1));
            Assert.IsFalse(ConstellationLayout.CanStep(0, +1, 1));
        }

        [Test]
        public void NoTreesAtAllIsSurvivable()
        {
            Assert.AreEqual(0, ConstellationLayout.Step(0, +1, 0));
        }

        // ---- the slide -------------------------------------------------------------

        [Test]
        public void TheSlideStartsStoppedAndEndsArrived()
        {
            Assert.AreEqual(0f, ConstellationLayout.SlideProgress(0f), 0.0001f);
            Assert.AreEqual(1f, ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds), 0.0001f);
        }

        [Test]
        public void OvershootingTheSlideStaysArrived()
        {
            // A frame that overshoots must not wrap into a second slide, which
            // is what an unclamped curve would do.
            Assert.AreEqual(1f, ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 3f), 0.0001f);
        }

        [Test]
        public void TheSlideEasesAtBothEnds()
        {
            // A slide that starts and stops abruptly is a cut with extra steps.
            // Ease-in-out is what lets the player keep their bearings.
            float quarter = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.25f);
            float half = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.5f);
            float threeQuarters = ConstellationLayout.SlideProgress(ConstellationLayout.SlideSeconds * 0.75f);

            Assert.AreEqual(0.5f, half, 0.0001f, "the midpoint is halfway across");
            Assert.Less(quarter, 0.25f, "slow off the mark");
            Assert.Greater(threeQuarters, 0.75f, "and slow into the arrival");
        }

        [Test]
        public void TheSkyArrivesExactlyOnTheTargetTree()
        {
            // The assertion that makes the slide safe to interrupt: wherever it
            // is stopped, finishing puts the target dead centre.
            float arrived = ConstellationLayout.SlideOffset(0, 2, 1f);

            Assert.AreEqual(-2f * ConstellationLayout.PageStride, arrived, 0.01f);
            Assert.AreEqual(0f, arrived + ConstellationLayout.PageX(2, 0) - ConstellationLayout.PageStride * 0f, 0.01f * ConstellationLayout.PageStride);
        }

        [Test]
        public void TheSkyStartsOnTheTreeItLeft()
        {
            Assert.AreEqual(
                -ConstellationLayout.PageStride * 1f,
                ConstellationLayout.SlideOffset(1, 2, 0f),
                0.01f);
        }

        // The guarantee that used to be a runtime guard.
        //
        // SlideProgress divided by SlideSeconds behind an `if (SlideSeconds <=
        // 0f)` check that the compiler folded away, because the constant is
        // positive -- CS0162, unreachable code. A guard that cannot fire is
        // noise that hides the next real one, so it was removed and the
        // condition it protected moved here.
        //
        // It protected something real: at zero the division of a zero elapsed
        // is NaN, every comparison in the method is false against NaN, and the
        // slide returns NaN instead of a progress. Failing here is how that
        // stays impossible.
        [Test]
        public void TheSlideHasAPositiveDuration()
        {
            Assert.Greater(ConstellationLayout.SlideSeconds, 0f,
                "SlideProgress divides by this -- at zero it returns NaN for a zero elapsed");
        }

        [Test]
        public void SlideProgressStaysWithinItsRangeAndIsNeverNaN()
        {
            foreach (float elapsed in new[] { -1f, 0f, 0.01f, 0.21f, 0.41f, ConstellationLayout.SlideSeconds, 99f })
            {
                float p = ConstellationLayout.SlideProgress(elapsed);

                Assert.IsFalse(float.IsNaN(p), $"NaN progress at elapsed {elapsed}");
                Assert.That(p, Is.InRange(0f, 1f), $"out of range at elapsed {elapsed}");
            }
        }
    }
}
