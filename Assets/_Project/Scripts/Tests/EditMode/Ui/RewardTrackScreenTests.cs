using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // P5's own art check, over the two per-TrackReward lookups ScreenRegistry
    // sizes and binds (docs/PLAN_REWARD_TRACKS.md P5): markByReward (the
    // rail's mark, RewardTrackLayout.IconFor) and cardArtByReward (the focus
    // card's medallion, RewardTrackLayout.CardArtKeyFor).
    //
    // NOT re-proving RewardTrackDefinitionTests.
    // EveryRewardKindResolvesAnArtKeyAndAName, which already walks the same
    // enum and asserts both lookups are non-empty per kind -- that is a
    // Domain-layer fact about the switch statements themselves. What is new
    // here, and is a UI-layer property rather than a Domain one, is what
    // ScreenRegistry actually NEEDS from CardArtKeyFor: twelve DIFFERENT
    // keys. Two reward kinds sharing one would still compile and still
    // resolve -- cardArtByReward would just have two slots pointing at the
    // same Sprite, and the card would draw the wrong medallion for one of
    // them with nothing in a diff to say so.
    public class RewardTrackScreenTests
    {
        [Test]
        public void EveryRewardKindBindsAMarkAndACardSprite()
        {
            var cardKeys = new HashSet<string>();
            int kindCount = 0;

            foreach (TrackReward reward in Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;
                kindCount++;

                string mark = RewardTrackLayout.IconFor(reward);
                string card = RewardTrackLayout.CardArtKeyFor(reward);

                Assert.IsFalse(string.IsNullOrEmpty(mark), $"{reward} has no rail mark key");
                Assert.IsFalse(string.IsNullOrEmpty(card), $"{reward} has no card art key");

                Assert.IsTrue(cardKeys.Add(card),
                    $"{reward}'s card art key ({card}) is shared with another reward kind -- " +
                    "cardArtByReward would draw the same medallion for both");
            }

            Assert.AreEqual(kindCount, cardKeys.Count,
                "the twelve reward kinds should resolve to twelve distinct card sprites");
        }
    }
}
