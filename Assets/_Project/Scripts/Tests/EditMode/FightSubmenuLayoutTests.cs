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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        [TestCase(17)]
        [TestCase(200)]
        public void NoRowIsEverPlacedOffTheCanvas(int requested)
        {
            int shown = FightSubmenuLayout.VisibleCount(requested);

            for (int i = 0; i < shown; i++)
            {
                float y = FightSubmenuLayout.RowY(shown, i);
                float top = y + FightSubmenuLayout.RowHeight * 0.5f;
                float bottom = y - FightSubmenuLayout.RowHeight * 0.5f;

                Assert.Less(top, CanvasHalfHeight,
                    $"row {i} of {requested} requested runs off the TOP of the canvas at y={y}");
                Assert.Greater(bottom, -CanvasHalfHeight,
                    $"row {i} of {requested} requested runs off the BOTTOM of the canvas at y={y}");
            }
        }

        [Test]
        public void AskingForMoreRowsThanThePoolHoldsIsClampedRatherThanHonoured()
        {
            Assert.AreEqual(8, FightSubmenuLayout.VisibleCount(17));
            Assert.AreEqual(8, FightSubmenuLayout.VisibleCount(FightSubmenuLayout.MaxRows));
            Assert.AreEqual(5, FightSubmenuLayout.VisibleCount(5));
            Assert.AreEqual(0, FightSubmenuLayout.VisibleCount(0));
            Assert.AreEqual(0, FightSubmenuLayout.VisibleCount(-3), "a negative count must not become a negative layout");
        }

        // The exact number the shipped bug produced, kept as a literal so this
        // fails loudly if the clamp is ever removed.
        [Test]
        public void TheShippedBug_SeventeenRowsPlacingTheTopRowAt777_CannotRecur()
        {
            Assert.AreEqual(777f, FightSubmenuLayout.RowY(17, 0), 0.01f,
                "unclamped, RowY still produces the off-screen position - this documents the input, not the behaviour");

            int shown = FightSubmenuLayout.VisibleCount(17);
            Assert.AreEqual(111f, FightSubmenuLayout.RowY(shown, 0), 0.01f,
                "clamped, the top row must sit at the 8-row height and stay on screen");
        }

        // The header belongs to the LIST, not to the pool. A five-skill actor
        // whose title sits at the eight-row height has a label floating over
        // the battlefield with nothing beneath it.
        [Test]
        public void TheHeaderSitsJustAboveTheTopRowOfTheListActuallyShown()
        {
            float fiveRowTop = FightSubmenuLayout.RowY(5, 0) + FightSubmenuLayout.RowHeight * 0.5f;
            float header = FightSubmenuLayout.HeaderY(5);

            Assert.Greater(header, fiveRowTop, "the header must clear the top row");
            Assert.Less(header - fiveRowTop, FightSubmenuLayout.RowPitch,
                "the header drifted more than a row's pitch above the list it labels");
        }

        [Test]
        public void TheHeaderIsClampedTheSameWayTheRowsAre()
        {
            Assert.AreEqual(FightSubmenuLayout.HeaderY(FightSubmenuLayout.MaxRows),
                FightSubmenuLayout.HeaderY(17), 0.01f,
                "a 17-row request must place the header exactly where an 8-row one does");
        }
    }
}
