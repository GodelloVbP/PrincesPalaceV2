using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The realm rules layered on the map shape (docs/PLAN_RUN_MAP.md
    // sections 2-3 and 8): strength bands, chip amounts and the skip heal's
    // trigger.
    public class RealmP2RulesTests
    {
        // ---- strength ----------------------------------------------------

        [TestCase(0, 1000, 1)]
        [TestCase(269, 1000, 1)]
        [TestCase(270, 1000, 2)]
        [TestCase(479, 1000, 2)]
        [TestCase(480, 1000, 3)]
        [TestCase(5000, 1000, 3)]
        [TestCase(10, 0, 3)]
        public void Strength_BandsOnTheEvSheetThresholds(double threat, double party, int expected)
        {
            Assert.AreEqual(expected, NodeStrength.Of(threat, party));
        }

        [Test]
        public void Weight_IsTheSumOfMaxHpTimesAttack()
        {
            var bodies = new[] { new FighterBody(200, 7), new FighterBody(260, 10), new FighterBody(140, 4) };
            Assert.AreEqual(4560d, NodeStrength.Weight(bodies));
        }

        [Test]
        public void StrengthEntry_MissingNodeReadsZero()
        {
            var entries = new List<NodeStrengthEntry> { new NodeStrengthEntry(4, 2) };
            Assert.AreEqual(2, NodeStrengthEntry.Find(entries, 4));
            Assert.AreEqual(0, NodeStrengthEntry.Find(entries, 5));
            Assert.AreEqual(0, NodeStrengthEntry.Find(null, 5));
        }

        // ---- chips -------------------------------------------------------

        [TestCase(1, 5)]
        [TestCase(2, 15)]
        [TestCase(3, 30)]
        [TestCase(0, 5)]
        public void GoldChip_AtStepZero(int strength, int gold)
        {
            Assert.AreEqual(gold, RealmPrize.GoldFor(strength, 0));
        }

        [Test]
        public void GoldChip_RidesTheRewardCurve()
        {
            Assert.AreEqual(71, RealmPrize.GoldFor(3, 12));
            Assert.Greater(RealmPrize.GoldFor(1, 40), RealmPrize.GoldFor(1, 0));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void ConsumableChip_Counts(int strength, int count)
        {
            Assert.AreEqual(count, RealmPrize.ConsumablesFor(strength));
        }

        [Test]
        public void EmptyBookOrRelicPool_PaysGold_GearNeverEmpties()
        {
            Assert.AreEqual(PrizeKind.Gold, RealmPrize.Effective(PrizeKind.Book, poolHasAny: false));
            Assert.AreEqual(PrizeKind.Gold, RealmPrize.Effective(PrizeKind.Relic, poolHasAny: false));
            Assert.AreEqual(PrizeKind.Book, RealmPrize.Effective(PrizeKind.Book, poolHasAny: true));
            Assert.AreEqual(PrizeKind.Item, RealmPrize.Effective(PrizeKind.Item, poolHasAny: false));
            Assert.AreEqual(PrizeKind.Consumable, RealmPrize.Effective(PrizeKind.Consumable, poolHasAny: false));
        }

        [Test]
        public void PrizeStream_IsAppendedAfterReward()
        {
            Assert.AreEqual(15u, RngStreams.Prize);
        }

        // ---- the skip heal's trigger ---------------------------------------

        // The edge lists are the generator's output for these seeds, so a
        // generator change re-pins them. Rows read: seed, pocket stretches,
        // skip-heal edges. Seed 1 forks past a pocket (9>12 9>13), seed 3
        // has two forked stretches, seed 7 has pockets on consecutive
        // stretches 3 and 4 and a skip into the fork from node 3.
        [TestCase(1UL, "3 4 5", "3>6 6>9 9>12 9>13")]
        [TestCase(2UL, "2 3 6", "3>6 6>8 10>13")]
        [TestCase(3UL, "1 2 6", "1>4 1>5 4>8 5>8 11>13")]
        [TestCase(7UL, "3 4 6", "3>5 3>6 5>8 6>8 9>12")]
        public void SkipHeal_FiresOnlyOnTheSpineEdgePastAPocket(ulong seed, string pocketStretches, string skippedEdges)
        {
            var map = RealmMapGenerator.GenerateFor(seed, 24, RealmProfiles.Default(4));
            Assert.AreEqual(pocketStretches, string.Join(" ", map.Pockets.Select(p => p.Stretch)));

            var skipped = new List<string>();
            foreach (var node in map.Nodes)
            {
                foreach (int next in node.Next)
                {
                    if (map.PocketSkippedBy(node.Id, next) != null) skipped.Add(node.Id + ">" + next);
                }
            }

            Assert.AreEqual(skippedEdges, string.Join(" ", skipped));
        }

        // Seed 1 (pockets on stretches 3, 4, 5): one edge per way to not
        // fire. 3>4 enters a pocket, 4>5 and 5>6 run inside or leave it (off
        // the spine); 0>1 and 12>14 are spine edges on stretches with no
        // pocket; 3>9 skips two stretches and is not an edge.
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        [TestCase(5, 6)]
        [TestCase(0, 1)]
        [TestCase(12, 14)]
        [TestCase(3, 9)]
        public void SkipHeal_DoesNotFireOffTheEdgeShapeItNeeds(int from, int to)
        {
            var map = RealmMapGenerator.GenerateFor(1UL, 24, RealmProfiles.Default(4));
            Assert.IsNull(map.PocketSkippedBy(from, to));
            Assert.IsNull(map.PocketSkippedBy(map.Entry.Id, map.Boss.Id));
        }

        [Test]
        public void SkipHeal_IsEightPercent()
        {
            Assert.AreEqual(8, RealmMap.SkipHealPercent);
        }
    }
}
