using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The cache's map rules (docs/PLAN_RUN_MAP.md section 3): at most one a
    // realm, in a pocket, and it counts as its pocket's prize.
    public class CacheRoomRulesTests
    {
        private static RealmMap Make(ulong seed, int floor) =>
            RealmMapGenerator.GenerateFor(seed, (floor - 1) * 8, RealmProfiles.Default(floor));

        private static bool Breaks(RealmMap map, string rule) =>
            RealmMapRules.Validate(map).Any(v => v.Rule == rule);

        [Test]
        public void DefaultProfile_WeightsCacheAtSix()
        {
            Assert.AreEqual(6, RealmProfiles.Default(1).waypointWeights.Single(w => w.type == RoomType.Cache).weight);
        }

        [Test]
        public void Generator_NeverMakesMoreThanOneCache_NeverOutsideAPocket_AndOftenMakesOne()
        {
            int withCache = 0;
            for (ulong seed = 1; seed <= 600; seed++)
            {
                int floor = 1 + (int)(seed % 20);
                var map = Make(seed, floor);
                var caches = map.Nodes.Where(n => n.Type == RoomType.Cache).ToList();

                Assert.LessOrEqual(caches.Count, 1, $"seed {seed} floor {floor}");
                Assert.IsTrue(caches.All(n => n.Role == RealmNodeRole.Pocket), $"seed {seed} floor {floor}");
                Assert.IsEmpty(RealmMapRules.Validate(map), $"seed {seed} floor {floor}");
                if (caches.Count == 1) withCache++;
            }

            Assert.Greater(withCache, 0, "the weight never produced a cache across 600 realms");
            Assert.AreEqual(PinnedCacheRealms, withCache);
        }

        private const int PinnedCacheRealms = 197;

        [Test]
        public void Validator_FlagsACacheOnTheSpine()
        {
            var map = Make(1, 1);
            map.Nodes.First(n => n.Role == RealmNodeRole.Spine).Type = RoomType.Cache;

            Assert.IsTrue(Breaks(map, "cache outside a pocket"));
        }

        [Test]
        public void Validator_FlagsASecondCache()
        {
            var map = Make(1, 12);
            foreach (var room in map.Pockets.SelectMany(p => p.Rooms).Take(2)) map.Node(room).Type = RoomType.Cache;

            Assert.GreaterOrEqual(map.Nodes.Count(n => n.Type == RoomType.Cache), 2, "fixture: two pocket caches");
            Assert.IsTrue(Breaks(map, "more than one cache"));
        }

        [Test]
        public void ACacheIsAPocketPrize()
        {
            var map = Make(1, 1);
            var pocket = map.Pockets[0];
            foreach (var id in pocket.Rooms)
            {
                var room = map.Node(id);
                room.Type = RoomType.Fight;
                room.Prize = PrizeKind.Gold;
            }

            Assert.IsTrue(Breaks(map, "pocket without a prize"), "fixture: a pocket of fights has none");

            map.Node(pocket.Rooms[0]).Type = RoomType.Cache;
            Assert.IsFalse(Breaks(map, "pocket without a prize"));
        }

        [Test]
        public void TheCardSaysCache()
        {
            Assert.AreEqual("CACHE", MapNodeCard.Caption(RoomType.Cache));
        }

        [Test]
        public void ACacheWithoutItsEventClearsAsAnEmptyRoom()
        {
            Assert.AreEqual(RoomResolution.Kind.Empty, RoomResolution.Resolve(RoomType.Cache, null).Result);
        }

        [Test]
        public void TheMapPaintsACacheWithItsOwnMedallion()
        {
            Assert.AreNotEqual(UiKit.Screens.MapScreen.TreasureIconKey, UiKit.Screens.MapScreen.IconKeyFor(RoomType.Cache));
            Assert.IsNotNull(UiKit.Screens.MapScreen.IconKeyFor(RoomType.Cache));
        }
    }
}
