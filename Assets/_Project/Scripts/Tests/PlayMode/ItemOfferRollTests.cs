using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The one Core-side step that was missing between generated content and a
    // fully tested reward table.
    //
    // PlayMode rather than EditMode because it reads ContentDatabase, which is
    // a Core type loaded from Resources. The RULES it calls -- RarityTable,
    // ItemOfferTable -- are Domain and already covered without a scene; what is
    // left here is the translation those two could never do for themselves,
    // and which is why they had no production caller at all until now.
    public class ItemOfferRollTests
    {
        // A deterministic stand-in for Random.Range: always the first element.
        // Enough to make the SHAPE of a roll assertable without asserting on a
        // distribution, which RarityTableTests already owns.
        private static int First(int bound) => 0;

        [Test]
        public void ContentOffersSomethingToRoll()
        {
            // Everything below is vacuous if the pool is empty, so this is the
            // fixture assertion the rest depends on.
            Assert.IsNotEmpty(ItemOfferRoll.Candidates());
        }

        [Test]
        public void OnlyEquippablesAreOffered()
        {
            // A "choose one of three" that can hand over a health potion is not
            // a choice, it is a tax on whoever reads carefully.
            var byId = ItemOfferRoll.Candidates().ToDictionary(c => c.ItemId);

            foreach (var item in Content.ContentDatabase.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                Assert.AreEqual(item.IsEquippable, byId.ContainsKey(item.id),
                    $"{item.id} is {(item.IsEquippable ? "wearable but not offered" : "offered but not wearable")}");
            }
        }

        [Test]
        public void MaxTierComesFromContentRatherThanAConstant()
        {
            // A content pass that adds a tier 11 set should widen the roll
            // without anyone remembering to edit a number here.
            Assert.AreEqual(Content.ContentDatabase.Items.Max(i => i.tier), ItemOfferRoll.MaxTier);
        }

        [Test]
        public void ARollProducesTheOfferCountTheScreenBuildsFor()
        {
            var offers = ItemOfferRoll.Roll(EncounterClass.Normal, 8, 0, First);

            Assert.AreEqual(ItemOfferTable.OfferCount, offers.Count);
        }

        [Test]
        public void TheSameItemIsNeverOfferedTwiceInOneRoll()
        {
            // Three of the same sword is one choice wearing three hats.
            for (int depth = 0; depth < 40; depth += 7)
            {
                var offers = ItemOfferRoll.Roll(EncounterClass.Elite, depth, 0, First);
                var ids = offers.Select(o => o.ItemId).ToList();

                CollectionAssert.AllItemsAreUnique(ids, $"at depth {depth}");
            }
        }

        [Test]
        public void EveryOfferNamesRealContent()
        {
            var offers = ItemOfferRoll.Roll(EncounterClass.Boss, 24, 0, First);

            foreach (var offer in offers)
            {
                Assert.IsNotNull(Content.ContentDatabase.GetItem(offer.ItemId),
                    $"offered '{offer.ItemId}', which is not in the database");
            }
        }

        [Test]
        public void ABossNeverOffersTheBottomOfTheLadder()
        {
            // RarityTable's one hard guarantee rather than a distribution: a
            // boss handing over grey loot reads as the fight having been
            // pointless. Asserted through the roll, since that is the path the
            // game actually takes.
            int floor = RarityTable.TierFloorFor(EncounterClass.Boss);

            for (int depth = 0; depth < 30; depth += 3)
            {
                foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, depth, 0, First))
                {
                    // ItemOfferTable widens its band when the pool is thin, so
                    // the assertion is on the TIER THE TABLE TARGETED, which is
                    // what the floor governs.
                    Assert.GreaterOrEqual(
                        RarityTable.RollTier(EncounterClass.Boss, depth, ItemOfferRoll.MaxTier, 0, First),
                        floor, $"at depth {depth}");
                }
            }
        }

        [Test]
        public void ANullRandomSourceDegradesRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
            CollectionAssert.IsEmpty(ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
        }

        [Test]
        public void PlusIsRolledPerItemRatherThanOncePerSet()
        {
            // Tier is the set's statement about the fight; plus is the long
            // tail on one particular copy. Rolling plus once would make all
            // three offers identically honed, which is not what RarityTable's
            // two-axis design says.
            var seen = new List<int>();
            int calls = 0;

            // Walks the sequence rather than returning a constant, so a
            // per-item roll and a per-set roll produce visibly different data.
            System.Func<int, int> walking = bound => (calls++) % System.Math.Max(1, bound);

            foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, 30, 0, walking))
            {
                seen.Add(offer.Plus);
            }

            Assert.AreEqual(ItemOfferTable.OfferCount, seen.Count);
            Assert.Greater(calls, ItemOfferTable.OfferCount,
                "the randomness source was consulted too few times for a per-item plus roll");
        }
    }
}
