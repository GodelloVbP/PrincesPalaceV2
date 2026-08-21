using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The offer row, which is the first layout in the project that has to be
    // right at two different widths -- once at build time and once at runtime.
    // Both call CardX, so what these pin is the arithmetic they share.
    public class OfferRowLayoutTests
    {
        // A row is centred when its first and last cards are equal and
        // opposite. Asserted as a PROPERTY rather than as pinned coordinates,
        // because the property is what the design asks for ("the heading above
        // does not have to move") and coordinates would break on a card resize
        // that changed nothing about the centring.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ARowOfAnyWidthIsCentredOnZero(int cards)
        {
            float first = OfferRowLayout.CardX(0, cards);
            float last = OfferRowLayout.CardX(cards - 1, cards);

            Assert.AreEqual(0f, first + last, 0.001f,
                $"a row of {cards} is not centred, so it will sit off the screen's axis");
        }

        [Test]
        public void ASingleCardSitsDeadCentre()
        {
            Assert.AreEqual(0f, OfferRowLayout.CardX(0, 1), 0.001f);
        }

        // Cards are one pitch apart, and the pitch is the card plus the gap --
        // so neighbours touch across exactly CardGap and never overlap.
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CardsAreEvenlySpacedAndNeverOverlap(int cards)
        {
            float pitch = OfferRowLayout.CardWidth(cards) + OfferRowLayout.CardGap;

            for (int i = 1; i < cards; i++)
            {
                Assert.AreEqual(pitch,
                    OfferRowLayout.CardX(i, cards) - OfferRowLayout.CardX(i - 1, cards), 0.001f,
                    $"card {i} is not one pitch from card {i - 1} in a row of {cards}");
            }
        }

        // THE NUMBER THAT MUST NOT MOVE. ~340 is what the three-card row has
        // always been, and every player below level 50 still sees it. Asserted
        // here rather than declared in the source because it FALLS OUT of the
        // budget -- if the budget or the gap is ever retuned, this is what says
        // the common case changed.
        //
        // 340.192 rather than a round 340: the interior is 9.8% inset from a
        // 1344 panel, so it is 1080.576 wide. BuildOffer's own note -- "the row
        // runs to x 540 against a clip half-width of 540.3" -- is that same
        // fraction seen from the other end.
        [Test]
        public void AThreeCardRowStillDrawsTheCardsItAlwaysDid()
        {
            Assert.AreEqual(340.192f, OfferRowLayout.CardWidth(3), 0.01f);
            Assert.AreEqual(320.192f, OfferRowLayout.LabelWidth(3), 0.01f);
            Assert.AreEqual(-370.192f, OfferRowLayout.CardX(0, 3), 0.01f);
        }

        // And what the reward actually costs: a four-card row divides the same
        // interior, so every card in it is narrower -- roughly 248 against 340,
        // about 27% off the width of the art. Pinned so the trade is visible
        // rather than discovered.
        [Test]
        public void AFourCardRowBuysItsFourthCardByNarrowingAllOfThem()
        {
            Assert.AreEqual(247.644f, OfferRowLayout.CardWidth(4), 0.01f);
            Assert.Less(OfferRowLayout.CardWidth(4), OfferRowLayout.CardWidth(3));
        }

        // Every row spans the same painted interior. This is the property that
        // lets UiAudit solve ONE layout and have it stand for all of them: if
        // the widest row fits, so does every narrower one, because they are the
        // same width.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryRowSpansTheSameInterior(int cards)
        {
            Assert.AreEqual(OfferRowLayout.RowBudget, OfferRowLayout.RowWidth(cards), 0.001f,
                $"a row of {cards} does not use the interior the audit checked");
        }

        // No row may reach past the painted interior, at any count. This is the
        // property UiAudit's one solved layout stands for: the audit sees the
        // four-card row only, and the rows the game draws instead are covered
        // by this rather than by the audit.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void NoRowReachesPastThePaintedInterior(int cards)
        {
            float edge = OfferRowLayout.CardX(cards - 1, cards) + OfferRowLayout.CardWidth(cards) * 0.5f;

            Assert.LessOrEqual(edge, OfferRowLayout.RowBudget * 0.5f + 0.001f,
                $"a row of {cards} runs over the painted border");
        }

        // Labels stop short of their card at every width, or a long item name
        // in a narrow row runs under the card next door. They are decor, so
        // nothing else would catch it.
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void LabelsAlwaysStopShortOfTheirCard(int cards)
        {
            Assert.Less(OfferRowLayout.LabelWidth(cards), OfferRowLayout.CardWidth(cards),
                $"a label fills its whole card in a row of {cards}");
        }

        [Test]
        public void AnEmptyRowAsksForNothingRatherThanThrowing()
        {
            Assert.AreEqual(0f, OfferRowLayout.CardX(0, 0), 0.001f);
            Assert.AreEqual(0f, OfferRowLayout.RowWidth(0), 0.001f);
            Assert.AreEqual(0f, OfferRowLayout.CardWidth(0), 0.001f);
            Assert.AreEqual(0f, OfferRowLayout.LabelWidth(0), 0.001f);
        }

        // The row is measured to exactly fill the painted interior -- not to
        // sit inside it with room to spare. That is what BuildOffer's "run to x
        // 540 against a clip half-width of 540.3" records, and it is why the
        // budget is derived from the screen rather than rounded to 1080.
        [Test]
        public void TheRowExactlyFillsTheInteriorTheScreenPaints()
        {
            Assert.AreEqual(ReckoningScreen.ContentHalfWidth * 2f, OfferRowLayout.RowWidth(3), 0.001f);
            Assert.AreEqual(ReckoningScreen.ContentHalfWidth * 2f, OfferRowLayout.RowWidth(4), 0.001f);
        }

        // ---- what the track grants ----------------------------------------------

        [TestCase(1, 3)]
        [TestCase(49, 3)]
        [TestCase(50, 4)]
        [TestCase(100, 4)]
        public void TheOfferWidensAtTheTrackLevelAndNotBefore(int level, int expected)
        {
            Assert.AreEqual(expected, OfferRowLayout.CardsFor(level));
        }

        [Test]
        public void TheCardCountIsReadOffTheTrackRatherThanTypedTwice()
        {
            // The failure this prevents: a fourth offer rolled, chosen by the
            // player, and painted onto a card that does not exist.
            Assert.AreEqual(
                RewardTrack.UnlockedAmount(TrackReward.WiderOffer, RewardTrack.MaxLevel, ItemOfferTable.OfferCount),
                OfferRowLayout.MaxCards);
        }
    }
}
