using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;

namespace PrincesPalace.Domain.Tests
{
    // Which relics may be offered, and which three are.
    //
    // Relics stopped being permanent progression and became a per-run draft,
    // so the rules that matter now are "can the player see this at all" and
    // "are the three on offer three DIFFERENT things". Both are cheap to get
    // subtly wrong and expensive to notice: a locked relic leaking into the
    // draft is invisible until someone asks why they got it, and a duplicate
    // offer reads as a bug rather than as luck.
    public class RelicPoolTests
    {
        private static RelicOption Open(string id, RelicRarity rarity = RelicRarity.Common) =>
            new RelicOption(id, rarity);

        private static RelicOption Locked(string id, string achievement,
                                          RelicRarity rarity = RelicRarity.Uncommon) =>
            new RelicOption(id, rarity, achievement);

        // A deterministic stand-in for the RNG: hands back a fixed sequence,
        // clamped into range. Injected rather than seeded so a test states the
        // draw it is asserting about instead of hoping a seed produces it.
        private static System.Func<int, int> Rolls(params int[] values)
        {
            int call = 0;
            return bound =>
            {
                int v = values[System.Math.Min(call++, values.Length - 1)];
                return bound <= 0 ? 0 : v % bound;
            };
        }

        // ---- what is available at all -------------------------------------------

        [Test]
        public void AnUngatedRelicIsAlwaysAvailable()
        {
            var available = RelicPool.Available(new[] { Open("dual_wield") }, new HashSet<string>());

            Assert.AreEqual(1, available.Count);
        }

        [Test]
        public void AGatedRelicIsHiddenUntilItsAchievementIsEarned()
        {
            var all = new[] { Open("dual_wield"), Locked("wardens_tooth", "first_forest_boss") };

            var before = RelicPool.Available(all, new HashSet<string>());
            var after = RelicPool.Available(all, new HashSet<string> { "first_forest_boss" });

            CollectionAssert.AreEquivalent(new[] { "dual_wield" }, before.Select(r => r.Id));
            CollectionAssert.AreEquivalent(new[] { "dual_wield", "wardens_tooth" }, after.Select(r => r.Id));
        }

        [Test]
        public void AnUnrelatedAchievementDoesNotUnlockAGatedRelic()
        {
            var all = new[] { Locked("wardens_tooth", "first_forest_boss") };

            var available = RelicPool.Available(all, new HashSet<string> { "character_level_30" });

            CollectionAssert.IsEmpty(available);
        }

        [Test]
        public void NoEarnedAchievementsAtAllIsNotAnError()
        {
            // A brand new profile passes null or empty here, and it must mean
            // "nothing gated is available" rather than throwing or -- worse --
            // unlocking everything.
            var all = new[] { Open("dual_wield"), Locked("wardens_tooth", "first_forest_boss") };

            Assert.AreEqual(1, RelicPool.Available(all, null).Count);
        }

        // ---- the draft ------------------------------------------------------------

        [Test]
        public void ADraftOffersThree()
        {
            var pool = Enumerable.Range(0, 10).Select(i => Open("r" + i)).ToList();

            Assert.AreEqual(RelicPool.OfferCount, RelicPool.Draft(pool, Rolls(0)).Count);
        }

        [Test]
        public void TheSameRelicIsNeverOfferedTwiceInOneDraft()
        {
            // Drawn without replacement. With a pool this small a naive draw
            // would repeat constantly, and a duplicate in a three-card offer
            // reads as a broken screen rather than as bad luck.
            var pool = Enumerable.Range(0, 4).Select(i => Open("r" + i)).ToList();

            var offer = RelicPool.Draft(pool, Rolls(0, 0, 0));

            CollectionAssert.AllItemsAreUnique(offer.Select(r => r.Id).ToList());
        }

        [Test]
        public void APoolSmallerThanTheOfferGivesWhatItHasRatherThanPadding()
        {
            var pool = new List<RelicOption> { Open("only") };

            var offer = RelicPool.Draft(pool, Rolls(0));

            Assert.AreEqual(1, offer.Count);
        }

        [Test]
        public void AnEmptyPoolOffersNothingRatherThanThrowing()
        {
            Assert.IsEmpty(RelicPool.Draft(new List<RelicOption>(), Rolls(0)));
            Assert.IsEmpty(RelicPool.Draft(null, Rolls(0)));
            Assert.IsEmpty(RelicPool.Draft(new List<RelicOption> { Open("a") }, null));
        }

        [Test]
        public void AnOutOfRangeRollIsClampedRatherThanThrowing()
        {
            // A generator handing back a bad index must not take the start of
            // a run down with it.
            var pool = Enumerable.Range(0, 5).Select(i => Open("r" + i)).ToList();

            List<RelicOption> offer = null;
            Assert.DoesNotThrow(() => offer = RelicPool.Draft(pool, _ => 9999));
            Assert.AreEqual(RelicPool.OfferCount, offer.Count);
        }

        // ---- rarity -----------------------------------------------------------------

        [Test]
        public void RarityWeightsDescendStrictlyDownTheLadder()
        {
            // The ladder only means something if each band is genuinely rarer
            // than the one below it. Walked from the enum, so a band appended
            // to RelicRarity without a weight fails here rather than silently
            // becoming as common as Godlike.
            var bands = System.Enum.GetValues(typeof(RelicRarity)).Cast<RelicRarity>().ToList();

            for (int i = 1; i < bands.Count; i++)
            {
                Assert.Less(RelicPool.WeightOf(bands[i]), RelicPool.WeightOf(bands[i - 1]),
                    $"{bands[i]} is not rarer than {bands[i - 1]}");
            }
        }

        [Test]
        public void CommonIsTheDefaultBandAndTheFirstOne()
        {
            // Content omits `rarity` constantly, and the default has to be the
            // band the starting pool is made of.
            Assert.AreEqual(0, (int)RelicRarity.Common);
            Assert.AreEqual(RelicRarity.Common, default(RelicRarity));
        }

        [Test]
        public void AWeightedDraftStillNeverRepeats()
        {
            var pool = new List<RelicOption>
            {
                Open("c1"), Open("c2"), Open("c3"),
                Open("g", RelicRarity.Godlike),
            };

            var offer = RelicPool.DraftWeighted(pool, Rolls(0, 0, 0));

            CollectionAssert.AllItemsAreUnique(offer.Select(r => r.Id).ToList());
            Assert.AreEqual(RelicPool.OfferCount, offer.Count);
        }

        [Test]
        public void AWeightedDraftFavoursTheCommonBand()
        {
            // Not a distribution test with a tolerance -- a counted sweep over
            // every possible roll, which is exact. A Godlike at weight 1 against
            // three Commons at 100 must be the last thing the first pick lands
            // on across almost the whole range.
            var pool = new List<RelicOption>
            {
                Open("c1"), Open("c2"), Open("c3"),
                Open("g", RelicRarity.Godlike),
            };

            int total = 100 * 3 + RelicPool.WeightOf(RelicRarity.Godlike);
            int godlikeFirst = 0;

            for (int roll = 0; roll < total; roll++)
            {
                int captured = roll;
                var offer = RelicPool.DraftWeighted(pool, _ => captured, count: 1);
                if (offer[0].Id == "g") godlikeFirst++;
            }

            Assert.AreEqual(RelicPool.WeightOf(RelicRarity.Godlike), godlikeFirst,
                "a Godlike should occupy exactly its own weight's worth of the range");
        }

    }
}
