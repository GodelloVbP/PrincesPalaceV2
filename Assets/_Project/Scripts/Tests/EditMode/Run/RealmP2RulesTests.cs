using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The rules run map P2 adds on top of the P1 shape (docs/PLAN_RUN_MAP.md
    // sections 2-3 and 8): strength bands, chip amounts, the skip heal's
    // trigger, and the column projection the current screen draws through.
    public class RealmP2RulesTests
    {
        // ---- strength ----------------------------------------------------

        [TestCase(0, 1000, 1)]
        [TestCase(339, 1000, 1)]
        [TestCase(340, 1000, 2)]
        [TestCase(609, 1000, 2)]
        [TestCase(610, 1000, 3)]
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
            Assert.AreEqual(DifficultyCurve.ScaleReward(30, 12), RealmPrize.GoldFor(3, 12));
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

        [Test]
        public void SkipHeal_FiresOnlyOnTheSpineEdgePastAPocket()
        {
            int checkedPockets = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 24, RealmProfiles.Default(4));
                foreach (var node in map.Nodes)
                {
                    foreach (int next in node.Next)
                    {
                        var to = map.Node(next);
                        bool expected = RealmMap.OnSpine(node) && RealmMap.OnSpine(to)
                                        && to.Offset == node.Offset + 1 && map.PocketAt(node.Offset) != null;
                        Assert.AreEqual(expected, map.PocketSkippedBy(node.Id, next) != null,
                            $"seed {seed} edge {node.Id}->{next}");
                        if (expected) checkedPockets++;
                    }
                }

                // Never off an edge that does not exist.
                Assert.IsNull(map.PocketSkippedBy(map.Entry.Id, map.Boss.Id));
            }

            Assert.Greater(checkedPockets, 200, "vacuity: the sample walked past pockets");
        }

        [Test]
        public void SkipHeal_IsEightPercent()
        {
            Assert.AreEqual(8, RealmMap.SkipHealPercent);
        }

        // ---- the column projection --------------------------------------

        [Test]
        public void Projection_EveryDefaultRealmFits_AndEveryChoiceIsBesideItsNode()
        {
            int maxEdges = 0;
            foreach (int floor in new[] { 1, 2, 4, 8, 10, 14, 30, 100 })
            {
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    var map = RealmMapGenerator.GenerateFor(seed, (floor - 1) * 8, RealmProfiles.Default(floor));
                    var columns = MapColumns.Of(map);

                    Assert.IsEmpty(columns.Overflow, $"floor {floor} seed {seed} overflowed the column pool");

                    var indices = new HashSet<int>();
                    foreach (var node in map.Nodes)
                    {
                        Assert.AreEqual(node.Offset, columns.ColumnOf(node.Id));
                        Assert.IsTrue(indices.Add(columns.IndexOf(node.Id)), "two nodes share a pool slot");
                        Assert.Less(columns.IndexOf(node.Id), MapLayout.Capacity);
                        Assert.AreSame(node, columns.At(columns.IndexOf(node.Id)));

                        foreach (int next in node.Next)
                        {
                            int gap = columns.ColumnOf(next) - columns.ColumnOf(node.Id);
                            Assert.That(gap, Is.InRange(0, 1), $"floor {floor} seed {seed}: a choice off screen");
                        }
                    }

                    maxEdges = System.Math.Max(maxEdges, map.Nodes.Sum(n => n.Next.Count));
                }
            }

            Assert.LessOrEqual(maxEdges, MapLayout.LinkCapacity, "the trail pool cannot draw every edge");
        }

        [Test]
        public void Projection_ColumnRowsAreDistinctAndInsideTheClearingBand()
        {
            for (int count = 1; count <= MapColumns.MaxRows; count++)
            {
                var ys = Enumerable.Range(0, count).Select(row => MapLayout.RowY(row, count)).ToList();
                CollectionAssert.AllItemsAreUnique(ys, $"{count} rows");
                foreach (float y in ys)
                {
                    Assert.That(y, Is.InRange(MapLayout.ClearingRowY[2] - 0.01f, MapLayout.ClearingRowY[0] + 0.01f));
                }
            }
        }

        [Test]
        public void CrowdedColumn_TilesShrinkSoTheyNeverOverlap()
        {
            Assert.AreEqual(1f, MapLayout.TileScale(3));
            for (int count = 4; count <= MapColumns.MaxRows; count++)
            {
                float pitch = MapLayout.RowY(0, count) - MapLayout.RowY(1, count);
                float tallest = (MapLayout.EliteTileWidth + MapLayout.TileHeightBonus) * MapLayout.TileScale(count);
                Assert.LessOrEqual(tallest, pitch, $"{count} rows");
            }

            Assert.AreEqual(124.8f, MapLayout.RowY(0, 6) - MapLayout.RowY(1, 6), 0.01f);
        }
    }
}
