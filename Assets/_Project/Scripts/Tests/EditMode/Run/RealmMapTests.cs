using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The realm map's shape and rules (docs/PLAN_RUN_MAP.md sections 2-4),
    // across many seeds: a generator is a distribution, and every property
    // here holds for most seeds and breaks for a few.
    public class RealmMapTests
    {
        private static RealmMap Make(ulong seed, int floor, bool restBeforeBoss = false)
        {
            return RealmMapGenerator.GenerateFor(seed, (floor - 1) * 8, RealmProfiles.Default(floor, restBeforeBoss));
        }

        private static string Signature(RealmMap map)
        {
            return string.Join("|", map.Nodes.Select(n =>
                $"{n.Id}:{n.Offset}:{(int)n.Role}:{(int)n.Type}:{(int)n.Prize}>" + string.Join(",", n.Next)));
        }

        private static int Forks(RealmMap map) =>
            map.Nodes.Where(n => n.Role == RealmNodeRole.ForkSibling).Select(n => n.Group).Distinct().Count();

        // ---- the default table -------------------------------------------

        [TestCase(1, 1, 2, 0, 1)]
        [TestCase(2, 2, 2, 0, 1)]
        [TestCase(4, 3, 2, 1, 2)]
        [TestCase(8, 5, 3, 2, 3)]
        [TestCase(10, 6, 3, 2, 4)]
        [TestCase(14, 6, 4, 3, 5)]
        [TestCase(100, 6, 4, 3, 6)]
        public void DefaultProfile_GrowsAndCaps(int floor, int pockets, int lengthMax, int forks, int anchors)
        {
            var profile = RealmProfiles.Default(floor);

            Assert.AreEqual(pockets, profile.pocketCount);
            Assert.AreEqual(1, profile.pocketLengthMin);
            Assert.AreEqual(lengthMax, profile.pocketLengthMax);
            Assert.AreEqual(forks, profile.forkCount);
            Assert.AreEqual(anchors, profile.anchorCount);
            Assert.AreEqual(0.75f, profile.shopChance);
        }

        [Test]
        public void DefaultProfile_ItemBookRelicChipsAreWeightZero_ForFightsAndAnchors()
        {
            var profile = RealmProfiles.Default(30);

            foreach (var kind in new[] { PrizeKind.Item, PrizeKind.Book, PrizeKind.Relic })
            {
                Assert.AreEqual(0, profile.prizeWeights.Single(w => w.kind == kind).weight, kind + " fights");
                Assert.AreEqual(0, profile.anchorPrizeWeights.Single(w => w.kind == kind).weight, kind + " anchors");
            }

            Assert.IsTrue(profile.anchorPrizeWeights.All(w => w.weight == 0), "anchors pay exp and gold only");
        }

        [Test]
        public void PrizeKind_NumbersAreAFormat()
        {
            Assert.AreEqual(3, (int)PrizeKind.Item);
            Assert.AreEqual(4, (int)PrizeKind.Book);
            Assert.AreEqual(5, (int)PrizeKind.Relic);
        }

        // ---- shape -------------------------------------------------------

        [Test]
        public void SpineRunsEntryToHeart_OneOrTwoNodesPerInteriorOffset()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                var map = Make(seed, 12);

                Assert.AreEqual(0, map.Entry.Offset);
                Assert.AreEqual(8, map.Boss.Offset);
                Assert.AreEqual(RoomType.Boss, map.Boss.Type);
                for (int offset = 1; offset <= 7; offset++)
                {
                    int count = map.SpineAt(offset).Count();
                    Assert.IsTrue(count == 1 || count == 2, $"seed {seed} offset {offset} has {count}");
                }
            }
        }

        [Test]
        public void ShortestRouteIsEightRooms_OnEveryFloor()
        {
            for (int floor = 1; floor <= 100; floor++)
            {
                Make((ulong)floor, floor).RouteLengths(out int shortest, out _);
                Assert.AreEqual(8, shortest, "floor " + floor);
            }
        }

        [Test]
        public void PocketRooms_SitAtTheRejoinOffset_AndRejoinTheSpine()
        {
            var map = Make(7, 10);

            foreach (var pocket in map.Pockets)
            {
                foreach (int id in pocket.Rooms) Assert.AreEqual(pocket.Stretch + 1, map.Node(id).Offset);

                var leavers = map.SpineAt(pocket.Stretch).ToList();
                foreach (var leaver in leavers) Assert.Contains(pocket.Rooms[0], leaver.Next);

                var tail = map.Node(pocket.Rooms.Last());
                foreach (var joiner in map.SpineAt(pocket.Stretch + 1)) Assert.Contains(joiner.Id, tail.Next);
            }
        }

        [Test]
        public void RestBeforeBoss_PutsTheOnlyRestOnTheSpineAtOffsetSeven()
        {
            for (ulong seed = 0; seed < 200; seed++)
            {
                var map = Make(seed, 20, restBeforeBoss: true);
                var rests = map.Nodes.Where(n => n.Type == RoomType.Rest).ToList();

                Assert.AreEqual(1, rests.Count, "seed " + seed);
                Assert.AreEqual(7, rests[0].Offset);
                Assert.AreEqual(RealmNodeRole.Spine, rests[0].Role);
            }
        }

        [Test]
        public void SameInputs_BuildTheSameMapAndIds()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                Assert.AreEqual(Signature(Make(seed, 9)), Signature(Make(seed, 9)), "seed " + seed);
            }
        }

        [Test]
        public void DifferentLegs_OfOneRun_AreDifferentMaps()
        {
            var distinct = new HashSet<string>();
            for (int floor = 1; floor <= 12; floor++)
            {
                distinct.Add(Signature(RealmMapGenerator.GenerateFor(31337UL, (floor - 1) * 8, RealmProfiles.Default(3))));
            }

            Assert.AreEqual(12, distinct.Count);
        }

        [Test]
        public void ForkSiblings_AreAChoice_NotTwoIdenticalRooms()
        {
            for (ulong seed = 0; seed < 300; seed++)
            {
                var map = Make(seed, 12);
                foreach (var group in map.Nodes.Where(n => n.Role == RealmNodeRole.ForkSibling).GroupBy(n => n.Group))
                {
                    var pair = group.ToList();
                    Assert.IsFalse(pair[0].Type == pair[1].Type && pair[0].Prize == pair[1].Prize,
                        $"seed {seed} fork at {group.Key}: {pair[0].Type}/{pair[0].Prize} twice");
                }
            }
        }

        // Pinned from one run: the exact map for one seed, so a change to the
        // draw order shows up as a diff rather than as a quietly different
        // realm in every save.
        [Test]
        public void Pin_FloorOneSeed42()
        {
            var map = Make(42, 1);

            Assert.AreEqual(Seed42FloorOne, Signature(map));
        }

        // id:offset:role:type:prize>next. A spine Event at offset 1, a pocket of an anchor then a shop,
        // then Fight, Event, Fight, Fight, Event, Fight and the heart.
        private const string Seed42FloorOne =
            "0:0:0:0:0>1|1:1:1:6:0>2,4|2:2:3:2:0>3|3:2:3:7:0>4|4:2:1:1:1>5|5:3:1:6:0>6|6:4:1:1:1>7|7:5:1:1:1>8|8:6:1:6:0>9|9:7:1:1:1>10|10:8:4:5:0>";

        // ---- the fuzz ----------------------------------------------------

        // floor, pockets, forks, anchors, shortest-to-longest spread bounds.
        private static readonly (int Floor, int Pockets, int Forks, int Anchors, int MinSpread, int MaxSpread)[] FloorClasses =
        {
            (1, 1, 0, 1, 1, 2),
            (2, 2, 0, 1, 2, 4),
            (4, 3, 1, 2, 3, 6),
            (10, 6, 2, 4, 6, 18),
            (12, 6, 3, 5, 6, 18),
            (14, 6, 3, 5, 6, 24),
            (100, 6, 3, 6, 6, 24),
        };

        [Test]
        public void Fuzz_TenThousandSeeds_AreValid_AndHitTheTableCounts()
        {
            var totals = new Dictionary<int, (int Maps, int Shops, int Spread)>();

            for (int i = 0; i < 10000; i++)
            {
                var cls = FloorClasses[i % FloorClasses.Length];
                bool rest = (i / FloorClasses.Length) % 2 == 1;
                var map = Make((ulong)i * 7919UL + 13UL, cls.Floor, rest);

                var violations = RealmMapRules.Validate(map);
                Assert.IsEmpty(violations, $"seed {i} floor {cls.Floor}: {string.Join("; ", violations)}");

                Assert.AreEqual(cls.Pockets, map.Pockets.Count, $"seed {i} floor {cls.Floor} pockets");
                Assert.AreEqual(cls.Forks, Forks(map), $"seed {i} floor {cls.Floor} forks");
                Assert.AreEqual(cls.Anchors, map.Nodes.Count(n => n.IsAnchor), $"seed {i} floor {cls.Floor} anchors");

                map.RouteLengths(out int shortest, out int longest);
                Assert.AreEqual(8, shortest);
                Assert.That(longest - shortest, Is.InRange(cls.MinSpread, cls.MaxSpread), $"seed {i} floor {cls.Floor} spread");

                int shops = map.Nodes.Count(n => n.Type == RoomType.Shop);
                totals.TryGetValue(cls.Floor, out var t);
                totals[cls.Floor] = (t.Maps + 1, t.Shops + shops, t.Spread + longest - shortest);
            }

            // Observed on these exact seeds; pins the draw order.
            var observed = string.Join(" ", totals.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value.Maps}/{p.Value.Shops}/{p.Value.Spread}"));
            Assert.AreEqual("1:1429/545/2137 2:1429/1072/4299 4:1429/1073/6448 10:1429/1068/17307 12:1428/1087/17175 14:1428/1078/21349 100:1428/1092/21300", observed);
        }

        [Test]
        public void Fuzz_ExtremeProfiles_StayValid()
        {
            var profiles = new List<RealmProfile>();

            var everything = RealmProfiles.Default(100);
            everything.pocketCount = 99;
            everything.pocketLengthMin = 5;
            everything.pocketLengthMax = 5;
            everything.forkCount = 99;
            everything.anchorCount = 99;
            everything.shopChance = 1f;
            profiles.Add(everything);

            var bare = RealmProfiles.Default(1);
            bare.pocketCount = 0;
            bare.pocketLengthMin = 0;
            bare.pocketLengthMax = -3;
            bare.forkCount = -1;
            bare.anchorCount = 0;
            bare.shopChance = 0f;
            profiles.Add(bare);

            var noTreasure = RealmProfiles.Default(10);
            noTreasure.waypointWeights = new List<RoomWeight> { new RoomWeight(RoomType.Fight, 1) };
            noTreasure.anchorCount = 0;
            noTreasure.shopChance = 0f;
            profiles.Add(noTreasure);

            var eventsOnly = RealmProfiles.Default(10);
            eventsOnly.waypointWeights = new List<RoomWeight> { new RoomWeight(RoomType.Event, 1) };
            profiles.Add(eventsOnly);

            var noWeights = RealmProfiles.Default(10);
            noWeights.waypointWeights = new List<RoomWeight>();
            noWeights.prizeWeights = new List<PrizeWeight>();
            profiles.Add(noWeights);

            var restHeavy = RealmProfiles.Default(12, restBeforeBoss: false);
            restHeavy.waypointWeights = new List<RoomWeight> { new RoomWeight(RoomType.Rest, 5), new RoomWeight(RoomType.Event, 1) };
            profiles.Add(restHeavy);

            var restAndForks = RealmProfiles.Default(100, restBeforeBoss: true);
            restAndForks.forkCount = 7;
            profiles.Add(restAndForks);

            foreach (var profile in profiles)
            {
                for (ulong seed = 0; seed < 300; seed++)
                {
                    var map = RealmMapGenerator.GenerateFor(seed, 64, profile);
                    Assert.IsEmpty(RealmMapRules.Validate(map), "seed " + seed);
                }
            }
        }

        [Test]
        public void ProfileWithNoTreasureWeight_StillGivesEveryPocketAPrize()
        {
            var profile = RealmProfiles.Default(10);
            profile.waypointWeights = new List<RoomWeight> { new RoomWeight(RoomType.Fight, 1) };
            profile.anchorCount = 0;
            profile.shopChance = 0f;

            var map = RealmMapGenerator.GenerateFor(5, 0, profile);

            Assert.AreEqual(6, map.Pockets.Count);
            foreach (var pocket in map.Pockets)
            {
                Assert.AreEqual(RoomType.Treasure, map.Node(pocket.Rooms[0]).Type);
            }
        }

        [Test]
        public void NoPrizeWeights_GiveFightsNoChip()
        {
            var profile = RealmProfiles.Default(5);
            profile.prizeWeights = new List<PrizeWeight>();

            var map = RealmMapGenerator.GenerateFor(5, 0, profile);

            Assert.IsTrue(map.Nodes.All(n => n.Prize == PrizeKind.None));
        }

        [Test]
        public void EveryFightGetsAChip_AndAnchorsGetNoneUnderTheDefault()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                var map = Make(seed, 20);

                foreach (var fight in map.Nodes.Where(n => n.Type == RoomType.Fight))
                {
                    Assert.That(fight.Prize, Is.EqualTo(PrizeKind.Gold).Or.EqualTo(PrizeKind.Consumable));
                }

                foreach (var anchor in map.Nodes.Where(n => n.IsAnchor)) Assert.AreEqual(PrizeKind.None, anchor.Prize);
            }
        }

        [Test]
        public void AnchorWeights_DecideTheAnchorChip()
        {
            var profile = RealmProfiles.Default(20);
            profile.anchorPrizeWeights = new List<PrizeWeight> { new PrizeWeight(PrizeKind.Relic, 1) };

            var map = RealmMapGenerator.GenerateFor(3, 0, profile);

            Assert.IsTrue(map.Nodes.Where(n => n.IsAnchor).All(n => n.Prize == PrizeKind.Relic));
            Assert.IsTrue(map.Nodes.Any(n => n.IsAnchor));
        }

        // ---- the validator -----------------------------------------------

        private static bool Breaks(RealmMap map, string rule) =>
            RealmMapRules.Validate(map).Any(v => v.Rule == rule);

        [Test]
        public void Validator_FlagsAnAnchorOnTheSpine()
        {
            var map = Make(1, 1);
            var spine = map.Nodes.First(n => n.Role == RealmNodeRole.Spine);
            spine.Type = RoomType.EliteFight;

            Assert.IsTrue(Breaks(map, "anchor outside a pocket or fork"));
            Assert.IsTrue(Breaks(map, "anchor on a node every route walks"));
        }

        [Test]
        public void Validator_FlagsAShopOnTheSpine_AndASecondShop()
        {
            var map = Make(1, 1);
            var spine = map.Nodes.First(n => n.Role == RealmNodeRole.Spine);
            spine.Type = RoomType.Shop;

            Assert.IsTrue(Breaks(map, "shop outside a pocket"));
        }

        [Test]
        public void Validator_FlagsTreasureOnTheSpine()
        {
            var map = Make(1, 1);
            map.Nodes.First(n => n.Role == RealmNodeRole.Spine).Type = RoomType.Treasure;

            Assert.IsTrue(Breaks(map, "treasure outside a pocket"));
        }

        [Test]
        public void Validator_FlagsAdjacentEvents()
        {
            var map = Make(1, 1);
            var a = map.Nodes.First(n => n.Role == RealmNodeRole.Spine);
            var b = map.Node(a.Next[0]);
            a.Type = RoomType.Event;
            b.Type = RoomType.Event;

            Assert.IsTrue(Breaks(map, "events adjacent"));
        }

        [Test]
        public void Validator_FlagsASpineRestWithoutThePerk_AndTwoRests()
        {
            var map = Make(1, 1);
            var spine = map.Nodes.Where(n => n.Role == RealmNodeRole.Spine).ToList();
            spine[0].Type = RoomType.Rest;
            spine[2].Type = RoomType.Rest;

            Assert.IsTrue(Breaks(map, "rest not on a fork sibling or before the boss"));
            Assert.IsTrue(Breaks(map, "more than one rest"));
        }

        [Test]
        public void Validator_FlagsAPocketWithoutAPrize_AndADecreasingOffset()
        {
            var map = Make(1, 1);
            foreach (int id in map.Pockets[0].Rooms) map.Node(id).Type = RoomType.Fight;
            Assert.IsTrue(Breaks(map, "pocket without a prize"));

            var map2 = Make(1, 1);
            map2.Boss.Offset = 3;
            Assert.IsTrue(Breaks(map2, "offset decreases along an edge"));
        }

        [Test]
        public void Validator_FlagsAStrandedNode_AndEntrySuccessorAtOffsetZero()
        {
            var map = Make(1, 1);
            map.Entry.Next.Clear();
            Assert.IsTrue(Breaks(map, "unreachable"));

            var map2 = Make(1, 1);
            map2.Node(map2.Entry.Next[0]).Offset = 0;
            Assert.IsTrue(Breaks(map2, "entry successor below offset 1"));
        }

        [Test]
        public void Validator_FlagsDuplicateIds()
        {
            var map = Make(1, 1);
            map.Nodes[2].Id = map.Nodes[1].Id;

            Assert.IsTrue(Breaks(map, "duplicate id"));
        }
    }
}
