using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Tests
{
    // Normalised positions for a realm (docs/PLAN_RUN_MAP.md section 4,
    // "Shape").
    public class RealmLayoutTests
    {
        private static RealmMap Make(ulong seed, int floor) =>
            RealmMapGenerator.GenerateFor(seed, (floor - 1) * 8, RealmProfiles.Default(floor));

        [Test]
        public void OrientationIsTopToBottom()
        {
            var map = Make(3, 10);
            var layout = RealmLayout.Place(map);

            Assert.AreEqual(0f, layout.Nodes[map.Entry.Id].Y);
            Assert.AreEqual(1f, layout.Nodes[map.Boss.Id].Y);
        }

        [Test]
        public void EveryPosition_IsInsideTheUnitSquare_AndTravelAlwaysGoesDown()
        {
            for (int floor = 1; floor <= 40; floor += 3)
            {
                for (ulong seed = 0; seed < 25; seed++)
                {
                    var map = Make(seed, floor);
                    var layout = RealmLayout.Place(map);

                    foreach (var node in map.Nodes)
                    {
                        var p = layout.Nodes[node.Id];
                        Assert.That(p.X, Is.InRange(0f, 1f));
                        Assert.That(p.Y, Is.InRange(0f, 1f));
                        foreach (int next in node.Next)
                        {
                            Assert.Greater(layout.Nodes[next].Y, p.Y, $"floor {floor} seed {seed} edge {node.Id}->{next}");
                        }
                    }
                }
            }
        }

        [Test]
        public void Height_FollowsTheLongestRoute()
        {
            for (int floor = 1; floor <= 40; floor += 3)
            {
                var map = Make((ulong)floor, floor);
                map.RouteLengths(out _, out int longest);

                Assert.AreEqual(longest + 1, RealmLayout.Place(map).Rows, "floor " + floor);
            }
        }

        [Test]
        public void ForkSiblings_ShareARow_AndStandSideBySide()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                var map = Make(seed, 12);
                var layout = RealmLayout.Place(map);

                foreach (var group in map.Nodes.Where(n => n.Role == RealmNodeRole.ForkSibling).GroupBy(n => n.Group))
                {
                    var pair = group.Select(n => layout.Nodes[n.Id]).ToList();
                    Assert.AreEqual(pair[0].Row, pair[1].Row);
                    Assert.AreNotEqual(pair[0].X, pair[1].X);
                }
            }
        }

        [Test]
        public void PocketRooms_HangOffTheSpine_OnAlternatingSides()
        {
            var map = Make(9, 14);
            var layout = RealmLayout.Place(map);

            foreach (var pocket in map.Pockets)
            {
                float expected = pocket.Stretch % 2 == 1 ? 0.12f : 0.88f;
                foreach (int id in pocket.Rooms) Assert.AreEqual(expected, layout.Nodes[id].X, 1e-6f);
            }
        }
    }
}
