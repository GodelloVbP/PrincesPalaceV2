using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // rewards.json and the relic page (docs/PLAN_ITEMS.md 1.2, 2.4).
    public class RewardSourceTests
    {
        private static List<ResolvedRewardSource> Shipped()
        {
            var raw = ContentDataFiles.ParseFile<RawRewardSourceFile>(ContentDataFiles.DataPath("rewards.json")).sources;
            Assert.IsTrue(RewardSourceEntryResolver.TryResolveAll(raw, out var rows, out var errors), string.Join("\n", errors ?? new List<string>()));
            return rows;
        }

        [Test]
        public void TheShippedRows_CarryThePlansDials()
        {
            var rows = Shipped().ToDictionary(r => r.Id);
            CollectionAssert.AreEquivalent(
                new[] { "normal_gear", "anchor_gear", "boss_gear", "anchor_relic", "boss_relic" }, rows.Keys);

            var normal = rows["normal_gear"];
            Assert.AreEqual((0, 0.22f, 0.35f, Quality.Common, false), (normal.TierOffset, normal.TierStep, normal.LadderStep, normal.QualityFloor, normal.WeaponGuarantee));
            var anchor = rows["anchor_gear"];
            Assert.AreEqual((0, 0.30f, 0.42f, Quality.Common, true), (anchor.TierOffset, anchor.TierStep, anchor.LadderStep, anchor.QualityFloor, anchor.WeaponGuarantee));
            var boss = rows["boss_gear"];
            Assert.AreEqual((1, 0.38f, 0.45f, Quality.Uncommon, true, Quality.Rare),
                (boss.TierOffset, boss.TierStep, boss.LadderStep, boss.QualityFloor, boss.WeaponGuarantee, boss.WeaponFloor));

            Assert.AreEqual(RelicRarity.Uncommon, rows["boss_relic"].RelicRarityMin);
            Assert.AreEqual(RelicRarity.Common, rows["anchor_relic"].RelicRarityMin);
            CollectionAssert.AreEqual(new[] { 100, 45, 18, 6, 2, 1 }, rows["anchor_relic"].RelicWeights);

            foreach (var row in rows.Values)
            {
                Assert.AreEqual(2, row.ExtraCards, row.Id);
                Assert.AreEqual(14u, row.Stream, row.Id);
                Assert.AreEqual(row.Kind == RewardPageKind.Gear ? UnusedChoicePays.Hone : UnusedChoicePays.CheapestSale,
                    row.UnusedPays, row.Id);
            }
        }

        [Test]
        public void EveryMomentThatPaysAPage_HasARow()
        {
            var ids = new HashSet<string>(Shipped().Select(r => r.Id));
            foreach (EncounterClass cls in System.Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.IsTrue(ids.Contains(RewardSources.IdFor(cls, RewardPageKind.Gear)), cls.ToString());
            }

            Assert.IsTrue(ids.Contains(RewardSources.IdFor(EncounterClass.Elite, RewardPageKind.Relic)));
            Assert.IsTrue(ids.Contains(RewardSources.IdFor(EncounterClass.Boss, RewardPageKind.Relic)));
            Assert.AreEqual("normal_relic", RewardSources.IdFor(EncounterClass.Normal, RewardPageKind.Relic));
        }

        private static RawRewardSourceEntry Gear(string id) => new RawRewardSourceEntry
        {
            id = id, kind = "Gear", tierStep = 0.2f, ladderStep = 0.3f, unusedPays = "Hone",
        };

        [Test]
        public void TheResolver_RefusesTheWholeFileOnAnyBadRow()
        {
            void Refused(RawRewardSourceEntry bad, string because)
            {
                Assert.IsFalse(RewardSourceEntryResolver.TryResolveAll(new[] { Gear("ok"), bad }, out var rows, out var errors), because);
                Assert.IsNull(rows);
                Assert.IsNotEmpty(errors);
            }

            var badStream = Gear("a"); badStream.stream = "Nope";
            Refused(badStream, "unknown stream");
            var badPays = Gear("b"); badPays.unusedPays = "CheapestSale";
            Refused(badPays, "a gear page hones");
            Refused(Gear("ok"), "duplicate id");
            var badStep = Gear("c"); badStep.tierStep = 1.2f;
            Refused(badStep, "tierStep outside (0,1)");
            var badKind = Gear("d"); badKind.kind = "Hat";
            Refused(badKind, "unknown kind");
            Refused(new RawRewardSourceEntry { id = "e", kind = "Relic", relicWeights = new[] { 1, 2 }, unusedPays = "CheapestSale" },
                "six weights");
        }

        [Test]
        public void Highest_RanksBossOverAnchorOverNormal()
        {
            Assert.AreEqual(EncounterClass.Boss, RewardSources.Highest(new[] { EncounterClass.Elite, EncounterClass.Boss, EncounterClass.Normal }));
            Assert.AreEqual(EncounterClass.Elite, RewardSources.Highest(new[] { EncounterClass.Normal, EncounterClass.Elite }));
            Assert.AreEqual(EncounterClass.Normal, RewardSources.Highest(new EncounterClass[0]));
        }

        // ---- the relic page ----

        private static readonly ResolvedRewardSource BossRelic = new ResolvedRewardSource
        {
            Id = "boss_relic", Kind = RewardPageKind.Relic, ExtraCards = 2, RelicRarityMin = RelicRarity.Uncommon,
            RelicWeights = new[] { 100, 45, 18, 6, 2, 1 }, UnusedPays = UnusedChoicePays.CheapestSale,
        };

        private static readonly List<RelicOption> Pool = new List<RelicOption>
        {
            new RelicOption("c1", RelicRarity.Common), new RelicOption("c2", RelicRarity.Common),
            new RelicOption("u1", RelicRarity.Uncommon), new RelicOption("u2", RelicRarity.Uncommon),
            new RelicOption("r1", RelicRarity.Rare), new RelicOption("ur1", RelicRarity.UltraRare),
        };

        [Test]
        public void ABossRelicPage_ShowsKPlusTwo_UncommonAndUp()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var rng = new SeededRandom(seed);
                var drawn = RelicPage.Roll(BossRelic, 1, Pool, n => rng.NextInt(0, n));

                Assert.AreEqual(3, drawn.Count);
                Assert.IsTrue(drawn.All(r => r.Rarity >= RelicRarity.Uncommon), $"seed {seed}");
                CollectionAssert.AllItemsAreUnique(drawn.Select(r => r.Id));
            }
        }

        [Test]
        public void AThinPool_ShowsWhatItHas()
        {
            var rng = new SeededRandom(1);
            Assert.AreEqual(4, RelicPage.Roll(BossRelic, 3, Pool, n => rng.NextInt(0, n)).Count);
        }

        [Test]
        public void AnUnusedRelicChoice_PinnedInGold()
        {
            Assert.AreEqual(33, RelicPage.UnusedChoicePayout(new[] { RelicRarity.Rare, RelicRarity.Uncommon }, 0));
            Assert.AreEqual(0, RelicPage.UnusedChoicePayout(new RelicRarity[0], 0));
        }
    }
}
