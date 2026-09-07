using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // Floor 1 is a sweep, and stays one.
    //
    // The pool used to be every non-boss enemy at every depth, so a floor-1
    // room could field a golem: twelve rounds against the starting party, next
    // to a rat's one, decided by nothing but the roll.
    public class EnemyBandTests
    {
        private static readonly EnemyCandidate Rat = new EnemyCandidate("rat", minFloor: 1);
        private static readonly EnemyCandidate Wolf = new EnemyCandidate("wolf", minFloor: 2);
        private static readonly EnemyCandidate Golem = new EnemyCandidate("golem", minFloor: 5);
        private static readonly EnemyCandidate Choir =
            new EnemyCandidate("hollow_choir", isBoss: true, minFloor: 1);
        private static readonly EnemyCandidate Colossus =
            new EnemyCandidate("throne_colossus", isBoss: true, minFloor: 3);

        private static readonly List<EnemyCandidate> Pool =
            new List<EnemyCandidate> { Rat, Wolf, Golem, Choir, Colossus };

        // Every seed, not one: a band that holds for the seed somebody happened
        // to try is not a band.
        [Test]
        public void FloorOneNeverFieldsSomethingFromDeeper()
        {
            for (int seed = 0; seed < 400; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.Fight, Pool, new SeededRandom((ulong)seed),
                    declaredBossId: null, floor: 1);

                foreach (string id in roll.EnemyIds)
                {
                    Assert.AreEqual("rat", id,
                        $"seed {seed} put '{id}' on floor 1, and only the rat is banded that shallow");
                }
            }
        }

        [Test]
        public void TheBandIsAMinimumSoDepthWidensThePool()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 400; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.Fight, Pool, new SeededRandom((ulong)seed),
                    declaredBossId: null, floor: 5);
                foreach (string id in roll.EnemyIds) seen.Add(id);
            }

            CollectionAssert.AreEquivalent(new[] { "rat", "wolf", "golem" }, seen.ToList(),
                "floor 5 should still roll shallow enemies as well as its own -- the difficulty " +
                "curve is what keeps a rat dangerous down there, not its absence");
        }

        [Test]
        public void ABossIsBandedToo()
        {
            var shallow = EncounterRoll.Roll(RoomType.Boss, Pool, new SeededRandom(7),
                declaredBossId: null, floor: 1);
            Assert.AreEqual("hollow_choir", shallow.EnemyIds.Single(),
                "the throne colossus is thirty-two rounds against a starting party");

            var deep = EncounterRoll.Roll(RoomType.Boss, Pool, new SeededRandom(7),
                declaredBossId: "throne_colossus", floor: 3);
            Assert.AreEqual("throne_colossus", deep.EnemyIds.Single());
        }

        // An empty stage is a soft lock, which is worse than an easy fight.
        [Test]
        public void ARoomShallowerThanEveryEnemyStillFightsSomething()
        {
            var deepOnly = new List<EnemyCandidate> { Golem, Colossus };

            var roll = EncounterRoll.Roll(RoomType.Fight, deepOnly, new SeededRandom(3),
                declaredBossId: null, floor: 1);

            CollectionAssert.IsNotEmpty(roll.EnemyIds,
                "nothing was in band and the room fielded nobody at all");
        }

        // THE BAND FALLBACK HAS TO COVER THE BOSS ROOM ON ITS OWN.
        //
        // The existing fallback fires only when NOTHING is in band. A boss
        // room whose bosses are all too deep but whose regulars are not
        // slipped past it and fell through to the ordinary draw: rats on the
        // boss node, Result.IsBoss false, so RarityTable pays Normal rather
        // than the absolute tier-3 boss floor, RecordBossKill credits
        // nothing, and no Ember drops. Silent, and a leg ends on a forced
        // boss room every eight steps -- floor 1 included -- so raising a
        // boss's minFloor is all it takes.
        [Test]
        public void ABossRoomWithNoBossInBandStillFieldsABoss()
        {
            // Rat is in band at floor 1; both bosses are not.
            var bossesTooDeep = new List<EnemyCandidate>
            {
                Rat,
                new EnemyCandidate("hollow_choir", isBoss: true, minFloor: 4),
                Colossus,
            };

            var roll = EncounterRoll.Roll(RoomType.Boss, bossesTooDeep, new SeededRandom(11),
                declaredBossId: null, floor: 1);

            Assert.IsTrue(roll.IsBoss, "a boss room that fields no boss pays out as an ordinary fight");
            CollectionAssert.Contains(new[] { "hollow_choir", "throne_colossus" }, roll.EnemyIds.Single());
        }

        // A leg is eight rooms, and legStartStep moves by exactly that.
        [Test]
        public void FloorFollowsTheLegBoundary()
        {
            Assert.AreEqual(1, RunDepth.FloorFor(0));
            Assert.AreEqual(2, RunDepth.FloorFor(8));
            Assert.AreEqual(3, RunDepth.FloorFor(16));
            Assert.AreEqual(1, RunDepth.FloorFor(-5), "a corrupt step must not read as floor zero");
        }
    }
}
