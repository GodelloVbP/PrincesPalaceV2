using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The submenu's placement arithmetic, pinned after it shipped a menu torn
    // in half down the screen.
    //
    // The bug it exists to prevent: RowY was handed the TRUE row count (17 for
    // a level 4 Shawn) while the rect pool holds 8. It sized the column for 17
    // rows, so the eight rects that exist landed at y 777..259 -- above the top
    // of a 1080 canvas, over the bark banner -- while BACK stayed at -464. The
    // header sat at the 8-row height above a 5-row list, stranded mid-screen.
    //
    // Everything here is a literal, deliberately. Recomputing RowY's formula to
    // build the expected value is the tautology CLAUDE.md's fifth gotcha bans,
    // and it would have passed against the broken version too.
    public class FightSubmenuLayoutTests
    {
        // The canvas is 1080 tall, so a row centre above +540 is off-screen and
        // one below -540 is too. This is the invariant the shipped bug broke.
        private const float CanvasHalfHeight = 540f;

        // WHAT REPLACED "no row is ever placed off the canvas".
        //
        // That was the shipped bug's exact shape and it was written when the
        // rows were loose on the battlefield: an unclamped count laid out
        // seventeen of them and pushed the whole column off the top. It kept
        // passing after the scroll container arrived, and it kept passing for
        // the WRONG REASON -- sixteen rows happen to fit in 1080, so an
        // invariant about the canvas and an invariant about the pool agreed by
        // coincidence. Raising the pool to 24 separated them and the test
        // failed on a list that is perfectly fine, because rows above the
        // window are now supposed to be off-canvas. That IS the scroll.
        //
        // The property that still matters is that nothing is UNREACHABLE, and
        // these three are it: the window is on screen, the top of the list can
        // be scrolled to, and so can the bottom.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        [TestCase(17)]
        [TestCase(200)]
        public void EveryRowCanBeScrolledIntoAWindowThatIsOnScreen(int requested)
        {
            int shown = FightSubmenuLayout.VisibleCount(requested);

            float windowTop = FightSubmenuLayout.ViewportCentreY + FightSubmenuLayout.ViewportHeight * 0.5f;
            float windowBottom = FightSubmenuLayout.ViewportCentreY - FightSubmenuLayout.ViewportHeight * 0.5f;

            Assert.Less(windowTop, CanvasHalfHeight, "the list's window runs off the top of the canvas");
            Assert.Greater(windowBottom, -CanvasHalfHeight, "the list's window runs off the bottom");

            if (shown == 0) return;

            float range = FightSubmenuLayout.ScrollRange(shown);

            // Row 0 is the top of the list, which scroll 0 shows; row shown-1 is
            // the bottom, which full scroll shows. If either falls outside the
            // window at the scroll that is supposed to reveal it, it cannot be
            // reached at all -- which is the failure the original test was for.
            AssertInsideWindow(RowAt(shown, 0, 0f), 0, "scroll 0 must show the top of the list");
            AssertInsideWindow(RowAt(shown, shown - 1, range), shown - 1,
                "full scroll must show the bottom of the list");
        }

        // A row's absolute y: the window, plus where the content sits inside it,
        // plus the row's fixed slot in the pool.
        private static float RowAt(int count, int index, float scroll) =>
            FightSubmenuLayout.ViewportCentreY
            + FightSubmenuLayout.ContentY(count, scroll)
            + FightSubmenuLayout.RowYInContent(index);

        private static void AssertInsideWindow(float y, int index, string why)
        {
            float top = FightSubmenuLayout.ViewportCentreY + FightSubmenuLayout.ViewportHeight * 0.5f;
            float bottom = FightSubmenuLayout.ViewportCentreY - FightSubmenuLayout.ViewportHeight * 0.5f;

            Assert.LessOrEqual(y + FightSubmenuLayout.RowHeight * 0.5f, top + 0.01f,
                $"row {index} sits above the window - {why}");
            Assert.GreaterOrEqual(y - FightSubmenuLayout.RowHeight * 0.5f, bottom - 0.01f,
                $"row {index} sits below the window - {why}");
        }

        [Test]
        public void AskingForMoreRowsThanThePoolHoldsIsClampedRatherThanHonoured()
        {
            Assert.AreEqual(24, FightSubmenuLayout.VisibleCount(40));
            Assert.AreEqual(24, FightSubmenuLayout.VisibleCount(FightSubmenuLayout.PoolSize));
            Assert.AreEqual(12, FightSubmenuLayout.VisibleCount(12), "a list longer than the window is not truncated to it");
            Assert.AreEqual(5, FightSubmenuLayout.VisibleCount(5));
            Assert.AreEqual(0, FightSubmenuLayout.VisibleCount(0));
            Assert.AreEqual(0, FightSubmenuLayout.VisibleCount(-3), "a negative count must not become a negative layout");
        }

        // The shipped bug's shape, kept as literals so this fails loudly if
        // the clamp is ever removed. The NUMBERS have moved four times now --
        // the pitch changed, BACK moved inside the frame, the panel dropped to
        // sit flush with the verb column, and the pool grew to 24 -- but the
        // property is unchanged: unclamped placement runs away, clamped
        // placement does not.
        //
        // ASKED FOR FORTY, not seventeen. Seventeen was above the old pool and
        // is inside the new one, so the test's two halves had become the same
        // call and it proved nothing while still passing.
        [Test]
        public void TheShippedBug_AnUnclampedCountRunningAwayUpTheScreen_CannotRecur()
        {
            Assert.AreEqual(1708f, FightSubmenuLayout.RowY(40, 0), 0.01f,
                "unclamped, RowY still produces the runaway position - this documents the input, not the behaviour");

            int shown = FightSubmenuLayout.VisibleCount(40);
            Assert.AreEqual(844f, FightSubmenuLayout.RowY(shown, 0), 0.01f,
                "clamped, the top row must sit at the pool height");
        }

        // ---- the scroll ----------------------------------------------------------

        // A list that fits does not move and shows no bar. This is the common
        // case -- Shawn reaches nine skills only with a talent root behind him
        // -- and it has to look exactly like the screen always did.
        [Test]
        public void AListThatFitsDoesNotScrollAtAll()
        {
            Assert.AreEqual(0f, FightSubmenuLayout.ScrollRange(5), 0.01f);
            Assert.AreEqual(0f, FightSubmenuLayout.ScrollRange(FightSubmenuLayout.RowsInView), 0.01f,
                "a list of exactly the window's height must not scroll by a pixel");
            Assert.AreEqual(0f, FightSubmenuLayout.ContentOffsetY(5, 0f), 0.01f);
        }

        // Twelve rows against an eight-row window: four rows' worth of travel.
        // 216 rather than the 192 four rows suggest, because travel is measured
        // in PIXELS and twelve rows carry eleven gaps against the window's seven.
        [Test]
        public void AListLongerThanTheWindowScrollsByTheDifference()
        {
            Assert.AreEqual(216f, FightSubmenuLayout.ScrollRange(12), 0.01f);
        }

        // SCROLL ZERO IS THE TOP OF THE LIST, which is the inversion worth
        // pinning: rows are laid out bottom-anchored, so at rest the content
        // sits pushed DOWN by its whole range and only reaches its authored
        // position once scrolled to the end.
        [Test]
        public void ScrollZeroShowsTheTopOfTheListAndFullScrollTheBottom()
        {
            Assert.AreEqual(-216f, FightSubmenuLayout.ContentOffsetY(12, 0f), 0.01f);
            Assert.AreEqual(0f, FightSubmenuLayout.ContentOffsetY(12, 216f), 0.01f);
        }

        // The thumb is the visible fraction of the list, floored so it stays
        // grabbable, and it travels the track in the same direction as scroll.
        [Test]
        public void TheThumbShowsHowMuchOfTheListIsOnScreen()
        {
            // 282.67 rather than the 284 that eight-of-twelve suggests, and the
            // difference is the gaps: the thumb is the visible fraction of the
            // content BY HEIGHT (426 of 642), and twelve rows carry eleven gaps
            // against the window's seven. Pinned at the real number, because the
            // tidy one would mean the thumb was measuring rows rather than
            // pixels and would drift the moment the gap changed.
            Assert.AreEqual(282.67f, FightSubmenuLayout.ThumbHeight(12), 0.01f,
                "the thumb must be the visible fraction of the content's height");

            // ABOVE AND BELOW THE VIEWPORT'S OWN CENTRE, not the container's.
            // The back row sits in the container's bottom padding, so the two
            // centres are 27px apart and a bare sign test against zero would
            // pass for the wrong reason at one end and fail at the other.
            float middle = FightSubmenuLayout.ViewportOffsetInContainer;

            Assert.Greater(FightSubmenuLayout.ThumbCentreY(12, 0f), middle,
                "at the top of the list the thumb sits high");
            Assert.Less(FightSubmenuLayout.ThumbCentreY(12, 216f), middle,
                "at the bottom it sits low");
        }

        [Test]
        public void ThumbHeightNeverCollapsesHoweverLongTheList()
        {
            Assert.GreaterOrEqual(FightSubmenuLayout.ThumbHeight(FightSubmenuLayout.PoolSize),
                FightSubmenuLayout.ThumbMinHeight);
        }

        // THE HEADER NO LONGER MOVES AT ALL, which is a deliberate reversal.
        // It used to ride the top row so a short list kept its label attached
        // to it; with a container that is wrong twice -- the label would sit
        // inside the frame for a short list, and it would slide every time the
        // count changed while the box around it did not.
        [Test]
        public void TheHeaderSitsOnTheContainerRatherThanOnTheList()
        {
            Assert.AreEqual(FightSubmenuLayout.HeaderY(2), FightSubmenuLayout.HeaderY(17), 0.01f,
                "the header moved with the count instead of staying on its frame");

            float containerTop = FightSubmenuLayout.ContainerCentreY
                                 + FightSubmenuLayout.ContainerHeight * 0.5f;

            Assert.Greater(FightSubmenuLayout.HeaderY(5), containerTop,
                "the header must clear the top of the frame it labels");
        }
    }
}
