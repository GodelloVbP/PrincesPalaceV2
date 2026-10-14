using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The Core-side step between generated content and the shop's gear shelf:
    // which (base, tier) copies the game may hand out at all.
    //
    // PlayMode rather than EditMode because it reads ContentDatabase, which is
    // a Core type loaded from Resources. The reward pages' own rolls are
    // RewardPagesTests' and GearPageTests'.
    public class ItemOfferRollTests
    {
        [Test]
        public void ContentOffersSomethingToRoll()
        {
            // Everything below is vacuous if the pool is empty, so this is the
            // fixture assertion the rest depends on.
            Assert.IsNotEmpty(ItemOfferRoll.Candidates());
        }

        // REWRITTEN FROM OnlyEquippablesAreOffered (AUDIT #114, owner's call
        // 2026-09-11). That test asserted `IsEquippable`, which is what
        // Candidates() filtered on, and the two agreed with each other and
        // with nothing else: ContentDatabase.Offerable had said for months
        // that the hand-authored one-offs in items.json are not rewards
        // because they "would otherwise turn up as a 'reward' the player
        // already owns six of", and the roll drew from them anyway.
        //
        // The predicate is "was this generated with a tier", not the kind --
        // an item with no tier cannot be ranked on the axis the offer screen
        // scales by. Asserted against Offerable rather than restated here, so
        // a content pass that adds a generated family widens both at once.
        [Test]
        public void OnlyOfferableItemsAreOffered()
        {
            var candidateIds = new HashSet<string>(ItemOfferRoll.Candidates().Select(c => c.ItemId));
            var offerable = new HashSet<string>(Content.ContentDatabase.Offerable.Select(i => i.id));

            foreach (var item in Content.ContentDatabase.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                bool expected = offerable.Contains(item.id);
                Assert.AreEqual(expected, candidateIds.Contains(item.id),
                    $"{item.id} is {(expected ? "offerable but not offered" : "offered but not offerable")}");
            }
        }

        // The half of #114 the repro is written about: the starting kit is
        // gear the player is already wearing on the first floor, and it sat in
        // exactly the tier band floor 1 rolls from. Stated separately from the
        // Offerable comparison above because it is the rule a content author
        // would go looking for, and because it keeps saying something the day
        // items.json carries a startingStock row again.
        [Test]
        public void TheStartingKitIsNeverOfferedAsAReward()
        {
            var offered = new HashSet<string>(ItemOfferRoll.Candidates().Select(c => c.ItemId));

            foreach (var item in Content.ContentDatabase.StartingStock)
            {
                Assert.IsFalse(offered.Contains(item.id),
                    $"'{item.id}' is granted to every new profile and is also on the reward table");
            }
        }

        [Test]
        public void MaxTierIsTheCopyCap()
        {
            Assert.AreEqual(ItemInstance.TierCap, ItemOfferRoll.MaxTier);
        }
    }
}
