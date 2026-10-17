using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // WHERE IN THE RUN a realm is has to reach the generator. The profile
    // alone cannot tell two realms of one floor class apart, so the stream is
    // keyed to (run seed, RngStreams.Leg, legStartStep): each realm its own
    // map, and the same realm regenerated identically on load.
    public class RealmLegSeedingTests
    {
        private const ulong RunSeed = 4242UL;

        private static string Shape(RealmMap map) =>
            string.Join("|", map.Nodes.Select(n =>
                $"{n.Id}:{n.Offset}:{n.Role}:{n.Type}:{n.Prize}->[{string.Join(",", n.Next)}]"));

        [Test]
        public void EachRealmOfOneDescentIsItsOwnMap_EvenOnOneProfile()
        {
            var profile = RealmProfiles.Default(4);
            var shapes = new List<string>();
            for (int realm = 0; realm < 8; realm++)
            {
                shapes.Add(Shape(RealmMapGenerator.GenerateFor(RunSeed, realm * RealmMap.SpineLength, profile)));
            }

            CollectionAssert.AllItemsAreUnique(shapes, "two realms of one descent generated the identical map");
        }

        [Test]
        public void AskingForTheSameRealmTwiceGivesTheSameMap()
        {
            Assert.AreEqual(
                Shape(RealmMapGenerator.GenerateFor(RunSeed, 24, RealmProfiles.ForRealm(24, restBeforeBoss: true))),
                Shape(RealmMapGenerator.GenerateFor(RunSeed, 24, RealmProfiles.ForRealm(24, restBeforeBoss: true))));
        }

        // Why GenerateFor exists: a stream seeded on the run alone repeats one
        // map for every realm of a floor class.
        [Test]
        public void SeedingOnTheRunAloneRepeatsOneMap()
        {
            var profile = RealmProfiles.Default(4);
            Assert.AreEqual(
                Shape(RealmMapGenerator.Generate(new SeededRandom(RunSeed), 0, profile)),
                Shape(RealmMapGenerator.Generate(new SeededRandom(RunSeed), 8, profile)));
        }

        // Rows read: first step of the leg, then pockets, anchors, forks.
        [TestCase(0, 1, 1, 0)]
        [TestCase(8, 2, 1, 0)]
        [TestCase(792, 6, 6, 3)]
        public void ForRealm_IsTheDefaultProfileOfThatFloor(int legStartStep, int pockets, int anchors, int forks)
        {
            var resolved = RealmProfiles.ForRealm(legStartStep);
            Assert.AreEqual(pockets, resolved.pocketCount);
            Assert.AreEqual(anchors, resolved.anchorCount);
            Assert.AreEqual(forks, resolved.forkCount);
        }
    }
}
