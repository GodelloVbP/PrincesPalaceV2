using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // The party pane's own "how big is this figure drawn" rule: one scale per
    // slot kind, taken from the TALLEST idle canvas in the roster, not a
    // per-actor fit-to-box. See PartyArtScale's own header for the defect
    // this replaces (Bjorn reading smaller than Shawn in the pane despite
    // being taller on the fight stage).
    public class PartyArtScaleTests
    {
        // PINNED LITERALS (CODE_STANDARDS SS5) -- 150 / 500 = 0.3 by hand,
        // not by re-deriving the formula under test.
        [Test]
        public void TheTallestCanvasSetsTheScale()
        {
            float scale = PartyArtScale.ScaleFor(150f, new List<float> { 366f, 500f });
            Assert.AreEqual(0.3f, scale, 0.0001f);
        }

        // Order must not matter -- the formula asks "which is tallest", not
        // "which was authored/enumerated last".
        [Test]
        public void TheTallestCanvasWinsRegardlessOfOrder()
        {
            float ascending = PartyArtScale.ScaleFor(150f, new List<float> { 366f, 500f });
            float descending = PartyArtScale.ScaleFor(150f, new List<float> { 500f, 366f });

            Assert.AreEqual(ascending, descending, 0.0001f);
        }

        // A single-member roster is its own tallest -- the everyday case
        // before a second actor's art ever lands.
        [Test]
        public void ASingleActorScalesToFillTheSlotExactly()
        {
            float scale = PartyArtScale.ScaleFor(150f, new List<float> { 370f });
            Assert.AreEqual(150f / 370f, scale, 0.0001f);
        }

        // No roster, or a roster with no art at all, has nothing to measure
        // against -- 1f (draw at native size) rather than a divide-by-zero
        // or an exception a caller would have to guard against first.
        [Test]
        public void AnEmptyRosterScalesToOne()
        {
            Assert.AreEqual(1f, PartyArtScale.ScaleFor(150f, new List<float>()), 0.0001f);
            Assert.AreEqual(1f, PartyArtScale.ScaleFor(150f, null), 0.0001f);
        }

        // A zero or negative height in the mix (should not happen -- a real
        // sprite always has positive dimensions -- but costs nothing to
        // refuse) never wins against a real actor's height.
        [Test]
        public void AZeroHeightEntryIsIgnoredAgainstARealOne()
        {
            float scale = PartyArtScale.ScaleFor(150f, new List<float> { 0f, 370f });
            Assert.AreEqual(150f / 370f, scale, 0.0001f);
        }

        // THE TWO REAL SLOT KINDS, at the roster this defect was filed
        // against -- Shawn (sheep, 540x370), Bjorn (bear, 486x467) and Odette
        // (owl, 649x366). Bjorn's canvas is the tallest, so he is the one
        // that ends up drawn at exactly the slot's own height in both rows;
        // everyone else scales down from that same factor, never up.
        [Test]
        public void SeatAndCardSlotsShareTheSameTallestActor()
        {
            var canvasHeights = new List<float> { 370f, 467f, 366f };

            const float seatSlotHeight = 150f;
            const float cardSlotHeight = 76f;

            float seatScale = PartyArtScale.ScaleFor(seatSlotHeight, canvasHeights);
            float cardScale = PartyArtScale.ScaleFor(cardSlotHeight, canvasHeights);

            Assert.AreEqual(seatSlotHeight / 467f, seatScale, 0.0001f);
            Assert.AreEqual(cardSlotHeight / 467f, cardScale, 0.0001f);

            // Bjorn (467) drawn at either scale exactly fills its slot;
            // Shawn (370) at the SAME scale reads smaller, matching the
            // fight stage where 467 > 370 in native pixels too.
            Assert.AreEqual(seatSlotHeight, 467f * seatScale, 0.01f);
            Assert.Less(370f * seatScale, seatSlotHeight);
        }
    }
}
