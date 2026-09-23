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
        // WINDOW-PER-COUNT now (2026-09-22), not the static ViewportCentreY/
        // ViewportHeight -- the container GROWS TO FIT up to RowsInView rows
        // (FightController.AnchorSubmenuRows' own header), so the window a
        // row actually has to land inside is ViewportCentreYFor(shown), not
        // the STATIC tree's own oversized BuildReservationRows placeholder.
        // The two agreed by construction before this rework (ContentY had
        // only one ViewportCentreY to read); they no longer do, so mixing
        // them here would pin a window this test never actually sees a row
        // drawn against.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        [TestCase(17)]
        [TestCase(200)]
        public void EveryRowCanBeScrolledIntoAWindowThatIsOnScreen(int requested)
        {
            int shown = FightSubmenuLayout.VisibleCount(requested);

            float windowTop = FightSubmenuLayout.ViewportCentreYFor(shown) + FightSubmenuLayout.ViewportHeightFor(shown) * 0.5f;
            float windowBottom = FightSubmenuLayout.ViewportCentreYFor(shown) - FightSubmenuLayout.ViewportHeightFor(shown) * 0.5f;

            Assert.Less(windowTop, CanvasHalfHeight, "the list's window runs off the top of the canvas");
            Assert.Greater(windowBottom, -CanvasHalfHeight, "the list's window runs off the bottom");

            if (shown == 0) return;

            float range = FightSubmenuLayout.ScrollRange(shown);

            // Row 0 is the top of the list, which scroll 0 shows; row shown-1 is
            // the bottom, which full scroll shows. If either falls outside the
            // window at the scroll that is supposed to reveal it, it cannot be
            // reached at all -- which is the failure the original test was for.
            AssertInsideWindow(RowAt(shown, 0, 0f), 0, shown, "scroll 0 must show the top of the list");
            AssertInsideWindow(RowAt(shown, shown - 1, range), shown - 1, shown,
                "full scroll must show the bottom of the list");
        }

        // A row's absolute y: the window, plus where the content sits inside it,
        // plus the row's fixed slot in the pool.
        private static float RowAt(int count, int index, float scroll) =>
            FightSubmenuLayout.ViewportCentreYFor(count)
            + FightSubmenuLayout.ContentY(count, scroll)
            + FightSubmenuLayout.RowYInContent(index);

        private static void AssertInsideWindow(float y, int index, int count, string why)
        {
            float top = FightSubmenuLayout.ViewportCentreYFor(count) + FightSubmenuLayout.ViewportHeightFor(count) * 0.5f;
            float bottom = FightSubmenuLayout.ViewportCentreYFor(count) - FightSubmenuLayout.ViewportHeightFor(count) * 0.5f;

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
            // 2030/1038, UP FROM 1870/942 -- RowPitch 58->62 (owner playtest,
            // 2026-09-23, "submenu rows drift upward against the command
            // rows"): RowPitch now reads VerbPitch directly instead of a
            // separately hand-tuned RowGap, so this test's own literals move
            // with it again; see FightSubmenuLayout.RowPitch's own comment
            // for why 62.
            Assert.AreEqual(2030f, FightSubmenuLayout.RowY(40, 0), 0.01f,
                "unclamped, RowY still produces the runaway position - this documents the input, not the behaviour");

            int shown = FightSubmenuLayout.VisibleCount(40);
            Assert.AreEqual(1038f, FightSubmenuLayout.RowY(shown, 0), 0.01f,
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

        // Twelve rows against a FIVE-row window (RowsInView, down from eight,
        // 2026-09-22): seven rows' worth of travel. 434, UP FROM 406
        // (RowPitch 58->62, owner playtest 2026-09-23, submenu row spacing
        // now reads VerbPitch directly), not the 434... er, not a round
        // number, for the same reason as before: travel is measured in
        // PIXELS and twelve rows carry eleven gaps against the window's four,
        // each of those gaps now 4px wider (RowGap 6->10).
        [Test]
        public void AListLongerThanTheWindowScrollsByTheDifference()
        {
            Assert.AreEqual(434f, FightSubmenuLayout.ScrollRange(12), 0.01f);
        }

        // SCROLL ZERO IS THE TOP OF THE LIST, which is the inversion worth
        // pinning: rows are laid out bottom-anchored, so at rest the content
        // sits pushed DOWN by its whole range and only reaches its authored
        // position once scrolled to the end.
        [Test]
        public void ScrollZeroShowsTheTopOfTheListAndFullScrollTheBottom()
        {
            Assert.AreEqual(-434f, FightSubmenuLayout.ContentOffsetY(12, 0f), 0.01f);
            Assert.AreEqual(0f, FightSubmenuLayout.ContentOffsetY(12, 434f), 0.01f);
        }

        // The thumb is the visible fraction of the list, floored so it stays
        // grabbable, and it travels the track in the same direction as scroll.
        [Test]
        public void TheThumbShowsHowMuchOfTheListIsOnScreen()
        {
            // 122.62, UP FROM 116.89 (RowPitch 58->62, owner playtest
            // 2026-09-23) -- the thumb is the visible fraction of the content
            // BY HEIGHT (300 of 734 now), and twelve rows carry eleven gaps
            // against the window's four. Pinned at the real number, because
            // the tidy one would mean the thumb was measuring rows rather
            // than pixels and would drift the moment the gap changed.
            Assert.AreEqual(122.62f, FightSubmenuLayout.ThumbHeight(12), 0.01f,
                "the thumb must be the visible fraction of the content's height");

            // ABOVE AND BELOW THE VIEWPORT'S OWN CENTRE, not the container's.
            // The back row sits in the container's bottom padding, so the two
            // centres are 23px apart and a bare sign test against zero would
            // pass for the wrong reason at one end and fail at the other.
            // ViewportOffsetInContainer (the STATIC property) still answers
            // this correctly for ANY count -- the offset algebraically
            // cancels every ViewportHeight-dependent term (see
            // ViewportOffsetInContainerFor's own derivation), so it is the
            // same 23px whether the window is RowsInView's five rows or
            // BuildReservationRows' eight.
            float middle = FightSubmenuLayout.ViewportOffsetInContainer;

            Assert.Greater(FightSubmenuLayout.ThumbCentreY(12, 0f), middle,
                "at the top of the list the thumb sits high");
            Assert.Less(FightSubmenuLayout.ThumbCentreY(12, 322f), middle,
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

            // FrameTop, not the old ContainerCentreY + ContainerHeight * 0.5f --
            // the frame is VisibleBottomLine-flush now (FrameCentreY's own
            // header), a few pixels off ContainerCentreY's own convention,
            // so the frame the header actually has to clear is the one built
            // from that flush edge, not the inner box's own raw rect.
            Assert.Greater(FightSubmenuLayout.HeaderY(5), FightSubmenuLayout.FrameTop,
                "the header must clear the top of the frame it labels");
        }

        // ---- the frame must never render narrower than its own rows --------------
        //
        // THE 98a4e062 BUG: FrameHeight dropped to BuildReservationRows(8)'s
        // figure at build time and ResizeSubmenuContainer shrinks only the
        // RECT's height per count afterward -- but Ui.Container's frame art
        // carried PreserveAspect, which does not stretch a mismatched rect,
        // it LETTERBOXES the art down to whichever axis is over-constrained.
        // A short list's rect was far off the art's 3:4, so the rendered art
        // came out narrower than the 240-wide rows sitting on it.
        //
        // FIXED TWICE NOW. PreserveAspect off (Type.Simple non-uniform
        // stretch) was tried first and fixed the overflow, but distorted the
        // painted border hard at 1-3 rows -- the same "stretched container
        // art looks bad" defect the owner rejected on the relic draft screen
        // the same day. The 2026-09-23 rework replaces the Violet 3:4
        // Ui.Container outright with RelicDraftScreen's own flat Solid-fill-
        // plus-Rim idiom (that file's own DraftFrameFill/DraftFrame header):
        // FrameWidth IS ContainerWidth now, at every height, because a flat
        // fill has no aspect to keep in the first place.
        //
        // 334 is hand-computed from the current constants, not read back
        // through the property it checks (CLAUDE.md's fifth gotcha):
        // ContainerWidth = RowWidth(300) + ContainerPad*2(20) +
        // ScrollbarGap(8) + ScrollbarWidth(6) = 334. RowWidth is 300 now,
        // UP FROM 240 (owner playtest 2026-09-23: submenu rows read
        // VerbRowW directly -- FightSubmenuLayout.RowWidth's own header).
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(8)]
        public void FrameStaysWideEnoughForItsOwnRowsAtEveryCount(int requested)
        {
            int shown = FightSubmenuLayout.VisibleRows(requested);
            Assert.Greater(shown, 0);

            Assert.AreEqual(334f, FightSubmenuLayout.ContainerWidth, 0.01f,
                "ContainerWidth's own formula moved without this pin moving too");
            Assert.AreEqual(334f, FightSubmenuLayout.FrameWidth, 0.01f,
                "FrameWidth is ContainerWidth now -- a flat fill has no art to pad past the content, " +
                "so a value other than 334 here means a pad crept back in");

            // FrameWidth is the SAME number at every count (it is never
            // resized), so this is really "does the one static figure clear
            // the content box" -- checked at 1/3/5/8 rather than once so a
            // future FrameWidthFor(count) that shrinks it per count, the
            // exact shape of the shipped bug, fails here immediately.
            Assert.GreaterOrEqual(FightSubmenuLayout.FrameWidth, FightSubmenuLayout.ContainerWidth,
                $"the frame must never render narrower than the {shown}-row content it wraps");
        }

        // ---- the frame is flush with the verb column and closes tight under BACK --
        //
        // FrameCentreY(For) is VisibleBottomLine-flush now, ZERO pad (a flat
        // Solid fill's rect bottom IS its last painted pixel -- FrameCentreY's
        // own header) -- pinned here with FightScreenTests.
        // TheSkillPanelEndsOnTheSameLineAsTheVerbColumn covering the built
        // scene's own rect, and this covering the pure Domain formula the
        // Unity side reads.
        [Test]
        public void TheFrameIsFlushWithVisibleBottomLineAtEveryCount()
        {
            float bottom5 = FightSubmenuLayout.FrameCentreYFor(5) - FightSubmenuLayout.FrameHeightFor(5) * 0.5f;
            float bottom1 = FightSubmenuLayout.FrameCentreYFor(1) - FightSubmenuLayout.FrameHeightFor(1) * 0.5f;

            Assert.AreEqual(FightSubmenuLayout.VisibleBottomLine, bottom5, 0.01f,
                "a five-row frame's rect bottom must sit exactly on the verb column's own visible line");
            Assert.AreEqual(FightSubmenuLayout.VisibleBottomLine, bottom1, 0.01f,
                "a one-row frame's rect bottom must ALSO sit there -- the frame shrinks from its TOP, " +
                "never its bottom (ResizeSubmenuContainer's own header)");
        }

        // BACK sits in the container's own bottom padding (BackRowY), and
        // the frame has to close AROUND that padding, not cut through it --
        // the frame's bottom is VisibleBottomLine-flush (just above), so
        // what is left to check is that BACK's own bottom edge clears the
        // CONTENT box's bottom (ContainerBottom) by at least ContainerPad,
        // at every count the container can be resized to.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void BackRowStaysInsideTheContainersBottomPadding(int count)
        {
            float backBottom = FightSubmenuLayout.BackRowY - FightSubmenuLayout.BackRowHeight * 0.5f;
            float contentBottom = FightSubmenuLayout.ContainerBottom;

            Assert.GreaterOrEqual(backBottom - contentBottom, FightSubmenuLayout.ContainerPad - 0.01f,
                $"BACK's own bottom edge must clear the content box's bottom by ContainerPad at count {count}");
        }
    }
}
