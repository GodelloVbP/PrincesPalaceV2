using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // v1-parity, pinned to literals. These numbers are what v1's two
    // hand-mirrored copies of the constants produced; if this file and the
    // shipped game ever disagree, a ported combat screen moves.
    public class FightSubmenuLayoutTests
    {
        [Test]
        public void RowY_MatchesV1_AtTheFullEightSlotReservation()
        {
            // RowsBottom -440, pitch 74, half-height 33.
            Assert.AreEqual(111f, FightSubmenuLayout.RowY(8, 0), 0.001f);
            Assert.AreEqual(37f, FightSubmenuLayout.RowY(8, 1), 0.001f);
            Assert.AreEqual(-407f, FightSubmenuLayout.RowY(8, 7), 0.001f);
        }

        [Test]
        public void TheLastVisibleRow_LandsOnTheSameSlot_WhateverTheCount()
        {
            // The whole point of the runtime re-anchor, and the behaviour a
            // playtest asked for: a two-skill list sits just above BACK rather
            // than floating at the top of an eight-slot reservation.
            Assert.AreEqual(-407f, FightSubmenuLayout.RowY(1, 0), 0.001f);
            Assert.AreEqual(-407f, FightSubmenuLayout.RowY(2, 1), 0.001f);
            Assert.AreEqual(-407f, FightSubmenuLayout.RowY(5, 4), 0.001f);
            Assert.AreEqual(-407f, FightSubmenuLayout.RowY(8, 7), 0.001f);
        }

        [Test]
        public void RowsAreSpacedByExactlyOnePitch()
        {
            Assert.AreEqual(74f, FightSubmenuLayout.RowY(4, 0) - FightSubmenuLayout.RowY(4, 1), 0.001f);
            Assert.AreEqual(74f, FightSubmenuLayout.RowY(4, 1) - FightSubmenuLayout.RowY(4, 2), 0.001f);
        }

        [Test]
        public void ColumnHeight_CountsGapsBetweenRowsOnly()
        {
            Assert.AreEqual(0f, FightSubmenuLayout.ColumnHeight(0), 0.001f);
            Assert.AreEqual(66f, FightSubmenuLayout.ColumnHeight(1), 0.001f);
            Assert.AreEqual(140f, FightSubmenuLayout.ColumnHeight(2), 0.001f);
            // 8 rows of 66 plus 7 gaps of 8.
            Assert.AreEqual(584f, FightSubmenuLayout.ColumnHeight(8), 0.001f);
        }

        [Test]
        public void ConstantsMatchV1Exactly()
        {
            // Pinned so a "tidy-up" of these numbers has to be a deliberate,
            // visible change rather than a silent drift away from shipped art.
            Assert.AreEqual(66f, FightSubmenuLayout.RowHeight, 0.001f);
            Assert.AreEqual(74f, FightSubmenuLayout.RowPitch, 0.001f);
            Assert.AreEqual(-440f, FightSubmenuLayout.RowsBottom, 0.001f);
            Assert.AreEqual(8, FightSubmenuLayout.MaxRows);
        }
    }
}
