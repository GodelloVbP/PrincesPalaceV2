using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Tests
{
    // realms.json (docs/PLAN_RUN_MAP.md P8): the resolver that turns a row into
    // a RealmProfile, and RealmProfiles.Resolve, which picks a designed realm
    // or the default for a floor. Fixtures only: no designed realm ships yet.
    public class RealmEntryResolverTests
    {
        private static RawRealmWeight W(string kind, int weight) => new RawRealmWeight { kind = kind, weight = weight };

        private static RawRealmEntry Row(int floor, string name = "The Ashen Court") => new RawRealmEntry
        {
            floor = floor,
            name = name,
            pocketCount = 3,
            pocketLengthMin = 1,
            pocketLengthMax = 3,
            forkCount = 2,
            anchorCount = 4,
            shopChance = 0.5f,
            waypointWeights = new[] { W("Fight", 40), W("Event", 30), W("Rest", 10) },
            prizeWeights = new[] { W("Gold", 10), W("Relic", 5) },
            anchorPrizeWeights = new[] { W("Item", 7) },
            bossPool = new[] { "boss_a", "boss_b" },
            restBeforeBoss = true,
        };

        private static List<ResolvedRealm> Resolved(params RawRealmEntry[] rows)
        {
            Assert.IsTrue(RealmEntryResolver.TryResolveAll(rows, out var resolved, out var errors),
                string.Join("\n", errors ?? new List<string>()));
            return resolved;
        }

        private static List<string> Errors(params RawRealmEntry[] rows)
        {
            Assert.IsFalse(RealmEntryResolver.TryResolveAll(rows, out var resolved, out var errors));
            Assert.IsNull(resolved, "a refused file must hand back no rows");
            return errors;
        }

        // ---- resolving a floor -----------------------------------------------

        [Test]
        public void ADesignedFloorResolvesItsProfileAndName()
        {
            var designed = Resolved(Row(3));

            // legStartStep 16 is floor 3.
            var realm = RealmProfiles.Resolve(16, false, designed);

            Assert.AreEqual(3, realm.Floor);
            Assert.AreEqual("The Ashen Court", realm.Name);
            var p = realm.Profile;
            Assert.AreEqual((3, 1, 3, 2, 4), (p.pocketCount, p.pocketLengthMin, p.pocketLengthMax, p.forkCount, p.anchorCount));
            Assert.AreEqual(0.5f, p.shopChance);
            CollectionAssert.AreEqual(
                new[] { new RoomWeight(RoomType.Fight, 40), new RoomWeight(RoomType.Event, 30), new RoomWeight(RoomType.Rest, 10) },
                p.waypointWeights);
            CollectionAssert.AreEqual(
                new[] { new PrizeWeight(PrizeKind.Gold, 10), new PrizeWeight(PrizeKind.Relic, 5) }, p.prizeWeights);
            CollectionAssert.AreEqual(new[] { new PrizeWeight(PrizeKind.Item, 7) }, p.anchorPrizeWeights);
            CollectionAssert.AreEqual(new[] { "boss_a", "boss_b" }, p.bossPool);
            Assert.IsTrue(p.restBeforeBoss);
        }

        [TestCase(0, 1)]
        [TestCase(8, 2)]
        [TestCase(24, 4)]
        [TestCase(792, 100)]
        public void AnUndesignedFloorGetsTheDefaultProfileAndTheDescent(int legStartStep, int floor)
        {
            var designed = Resolved(Row(3));

            var realm = RealmProfiles.Resolve(legStartStep, false, designed);
            var expected = RealmProfiles.Default(floor);

            Assert.AreEqual("The Descent", realm.Name);
            Assert.AreEqual(floor, realm.Floor);
            Assert.AreEqual((expected.pocketCount, expected.forkCount, expected.anchorCount, expected.pocketLengthMax),
                (realm.Profile.pocketCount, realm.Profile.forkCount, realm.Profile.anchorCount, realm.Profile.pocketLengthMax));
            CollectionAssert.AreEqual(expected.waypointWeights, realm.Profile.waypointWeights);
        }

        [Test]
        public void NoDesignedRealmsAtAllIsTheDefaultEverywhere()
        {
            Assert.AreEqual("The Descent", RealmProfiles.Resolve(0, false, null).Name);
            Assert.AreEqual("The Descent", RealmProfiles.Resolve(0, false, new List<ResolvedRealm>()).Name);
        }

        [Test]
        public void TheRunsRestFlagTurnsRestOnButNeverWritesBackIntoContent()
        {
            var raw = Row(3);
            raw.restBeforeBoss = false;
            var designed = Resolved(raw);

            Assert.IsTrue(RealmProfiles.Resolve(16, true, designed).Profile.restBeforeBoss);
            Assert.IsFalse(RealmProfiles.Resolve(16, false, designed).Profile.restBeforeBoss);
            Assert.IsFalse(designed[0].Profile.restBeforeBoss, "the content row was mutated");
        }

        [Test]
        public void TheResolvedProfileSharesNoListWithTheContentRow()
        {
            var designed = Resolved(Row(3));
            var realm = RealmProfiles.Resolve(16, false, designed);

            realm.Profile.waypointWeights.Clear();
            realm.Profile.bossPool.Add("x");

            Assert.AreEqual(3, designed[0].Profile.waypointWeights.Count);
            Assert.AreEqual(2, designed[0].Profile.bossPool.Count);
        }

        [Test]
        public void RowsComeBackAscendingByFloor()
        {
            var floors = Resolved(Row(9, "C"), Row(2, "A"), Row(5, "B")).Select(r => r.Floor);
            CollectionAssert.AreEqual(new[] { 2, 5, 9 }, floors);
        }

        // ---- refusals --------------------------------------------------------

        [Test]
        public void ADuplicateFloorIsRefused()
        {
            var errors = Errors(Row(3, "One"), Row(3, "Two"));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("floor 3 is used twice", errors[0]);
        }

        [Test]
        public void ANegativeWeightIsRefused()
        {
            var raw = Row(3);
            raw.waypointWeights = new[] { W("Fight", 10), W("Event", -1) };
            var errors = Errors(raw);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("waypointWeights weight for Event is -1", errors[0]);
        }

        [Test]
        public void AllZeroWaypointWeightsAreRefused()
        {
            var raw = Row(3);
            raw.waypointWeights = new[] { W("Fight", 0), W("Event", 0) };
            StringAssert.Contains("at least one weight above 0", Errors(raw).Single());
        }

        [TestCase("Shop")]
        [TestCase("Boss")]
        [TestCase("EliteFight")]
        [TestCase("Entry")]
        [TestCase("fight")]
        [TestCase("1")]
        [TestCase("")]
        public void AWaypointKindOutsideTheRolledFiveIsRefused(string kind)
        {
            var raw = Row(3);
            raw.waypointWeights = new[] { W("Fight", 10), W(kind, 5) };
            StringAssert.Contains("is not one of", Errors(raw).Single());
        }

        [Test]
        public void APrizeKindNoneOrUnknownIsRefused()
        {
            var raw = Row(3);
            raw.prizeWeights = new[] { W("None", 5) };
            raw.anchorPrizeWeights = new[] { W("Gem", 5) };
            Assert.AreEqual(2, Errors(raw).Count);
        }

        [Test]
        public void ARepeatedKindInOneTableIsRefused()
        {
            var raw = Row(3);
            raw.prizeWeights = new[] { W("Gold", 5), W("Gold", 6) };
            StringAssert.Contains("lists Gold twice", Errors(raw).Single());
        }

        [Test]
        public void ErrorsAcrossRowsAreCollected()
        {
            var bad = Row(3, " ");
            var worse = Row(0);
            worse.pocketCount = 7;
            worse.shopChance = 1.5f;
            Assert.AreEqual(4, Errors(bad, worse).Count);
        }

        [TestCase(0, 1, 2, 0, 1, 0.5f)]   // pocketCount below 1
        [TestCase(7, 1, 2, 0, 1, 0.5f)]   // above RealmMap.MaxPockets
        [TestCase(1, 0, 2, 0, 1, 0.5f)]   // pocketLengthMin below 1
        [TestCase(1, 3, 2, 0, 1, 0.5f)]   // max below min
        [TestCase(1, 1, 5, 0, 1, 0.5f)]   // max above the layout's MaxDefaultPocketLength
        [TestCase(1, 1, 2, 4, 1, 0.5f)]   // forks above MaxDefaultForks
        [TestCase(1, 1, 2, 0, 7, 0.5f)]   // anchors above MaxDefaultAnchors
        [TestCase(1, 1, 2, -1, 1, 0.5f)]
        [TestCase(1, 1, 2, 0, 1, -0.1f)]
        [TestCase(1, 1, 2, 0, 1, 1.1f)]
        public void AShapeOutsideTheBoundsIsRefused(int pockets, int lengthMin, int lengthMax, int forks, int anchors, float shop)
        {
            var raw = Row(3);
            raw.pocketCount = pockets;
            raw.pocketLengthMin = lengthMin;
            raw.pocketLengthMax = lengthMax;
            raw.forkCount = forks;
            raw.anchorCount = anchors;
            raw.shopChance = shop;
            Assert.AreEqual(1, Errors(raw).Count);
        }

        [Test]
        public void ABlankOrRepeatedBossIdIsRefused()
        {
            var blank = Row(3);
            blank.bossPool = new[] { "a", " " };
            var repeated = Row(4);
            repeated.bossPool = new[] { "a", "a" };
            Assert.AreEqual(2, Errors(blank, repeated).Count);
        }

        [Test]
        public void AnEmptyFileResolvesToNoRealms()
        {
            Assert.IsTrue(RealmEntryResolver.TryResolveAll(new RawRealmEntry[0], out var resolved, out _));
            Assert.IsEmpty(resolved);
        }

        [Test]
        public void TheShippedFileResolves()
        {
            var raw = ContentDataFiles.ParseFile<RawRealmFile>(ContentDataFiles.DataPath("realms.json")).realms;
            Assert.IsNotNull(raw);
            Assert.IsTrue(RealmEntryResolver.TryResolveAll(raw, out _, out var errors), string.Join("\n", errors ?? new List<string>()));
        }
    }
}
