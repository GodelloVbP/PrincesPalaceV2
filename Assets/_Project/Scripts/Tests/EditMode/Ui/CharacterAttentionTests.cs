using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHEN A CHARACTER WANTS LOOKING AT (plan C20): the truth table for the
    // chip pip, the rail pips and the map's count. The rule is three ORed
    // conditions, and the two that are easy to get wrong are the book one (it
    // needs a holder, a free slot AND a book to place) and the gear one (an
    // empty gear slot never lights anything, so it is not even an input).
    public class CharacterAttentionTests
    {
        // (unspent points, rewards waiting, can hold books, empty book slots, unassigned books)
        private static AttentionFacts Facts(int points = 0, int rewards = 0, bool holds = true,
                                            int emptySlots = 0, int books = 0) =>
            new AttentionFacts(points, rewards, holds, emptySlots, books);

        [Test]
        public void NothingOwedLightsNothing()
        {
            var facts = Facts();

            Assert.IsFalse(CharacterAttention.Any(facts));
            foreach (var section in CharacterSections.All)
            {
                Assert.IsFalse(CharacterAttention.Lights(section, facts), section.ToString());
            }
        }

        [TestCase(1)]
        [TestCase(3)]
        public void UnspentPointsLightAttributesAndNothingElse(int points)
        {
            var facts = Facts(points: points);

            Assert.IsTrue(CharacterAttention.Lights(CharacterSection.Attributes, facts));
            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Loadout, facts));
            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.RewardTrack, facts));
            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Skills, facts));
            Assert.IsTrue(CharacterAttention.Any(facts));
        }

        [Test]
        public void RewardsWaitingLightTheRewardTrackAndNothingElse()
        {
            var facts = Facts(rewards: 2);

            Assert.IsTrue(CharacterAttention.Lights(CharacterSection.RewardTrack, facts));
            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Attributes, facts));
            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Loadout, facts));
            Assert.IsTrue(CharacterAttention.Any(facts));
        }

        [Test]
        public void AnEmptyBookSlotWithABookToPlaceLightsLoadout()
        {
            var facts = Facts(emptySlots: 2, books: 1);

            Assert.IsTrue(CharacterAttention.Lights(CharacterSection.Loadout, facts));
            Assert.IsTrue(CharacterAttention.Any(facts));
        }

        [Test]
        public void AnEmptyBookSlotWithNoBookToPlaceLightsNothing()
        {
            var facts = Facts(emptySlots: 3, books: 0);

            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Loadout, facts));
            Assert.IsFalse(CharacterAttention.Any(facts));
        }

        [Test]
        public void ABookToPlaceWithEverySlotFullLightsNothing()
        {
            var facts = Facts(emptySlots: 0, books: 2);

            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Loadout, facts));
        }

        // A Fury user refuses books: he has slots on paper and no use for them,
        // and a pip on a section that can do nothing for him would never clear.
        [Test]
        public void ACharacterWhoCannotHoldBooksNeverLightsLoadoutForOne()
        {
            var facts = Facts(holds: false, emptySlots: 3, books: 2);

            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Loadout, facts));
            Assert.IsFalse(CharacterAttention.Any(facts));
        }

        [Test]
        public void SkillsNeverLightsWhateverIsOwed()
        {
            var facts = Facts(points: 5, rewards: 5, emptySlots: 3, books: 3);

            Assert.IsFalse(CharacterAttention.Lights(CharacterSection.Skills, facts));
        }

        [Test]
        public void TheChipLightsWhenAnySectionDoes()
        {
            Assert.IsTrue(CharacterAttention.Any(Facts(points: 1)));
            Assert.IsTrue(CharacterAttention.Any(Facts(rewards: 1)));
            Assert.IsTrue(CharacterAttention.Any(Facts(emptySlots: 1, books: 1)));
            Assert.IsTrue(CharacterAttention.Any(Facts(points: 1, rewards: 1, emptySlots: 1, books: 1)));
            Assert.IsFalse(CharacterAttention.Any(Facts()));
        }

        [Test]
        public void TheMapCountsCharactersNotReasons()
        {
            var party = new List<AttentionFacts>
            {
                Facts(points: 4, rewards: 3, emptySlots: 2, books: 1),   // three reasons, one character
                Facts(),
                Facts(rewards: 1),
            };

            Assert.AreEqual(2, CharacterAttention.CountLit(party));
        }

        [Test]
        public void NobodyToCountIsZero()
        {
            Assert.AreEqual(0, CharacterAttention.CountLit(null));
            Assert.AreEqual(0, CharacterAttention.CountLit(new List<AttentionFacts>()));
        }
    }
}
