using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.EditModeTests
{
    // The anchor roll (docs/PLAN_RUN_MAP.md section 5): one enemy, one slot,
    // keyed like any fight so the map fight and the heart agree.
    public class RealmAnchorRollTests
    {
        private static IReadOnlyList<EnemyCandidate> Pool() => new List<EnemyCandidate>
        {
            new EnemyCandidate("rat"),
            new EnemyCandidate("wolf"),
            new EnemyCandidate("treant", slotSpan: 2),
            new EnemyCandidate("deep_horror", minFloor: 5),
            new EnemyCandidate("warden", isBoss: true),
        };

        [Test]
        public void FieldsExactlyOneEnemy_MarkedElite()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                var result = EncounterRoll.RollAnchor(Pool(), RngStreams.Open(seed, RngStreams.Fight, 6, 17), floor: 10);

                Assert.AreEqual(1, result.EnemyIds.Count, "seed " + seed);
                Assert.IsTrue(result.IsElite);
                Assert.IsFalse(result.IsBoss);
            }
        }

        [Test]
        public void NeverDrawsABoss_OrAWideCreature()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 0; seed < 500; seed++)
            {
                seen.Add(EncounterRoll.RollAnchor(Pool(), RngStreams.Open(seed, RngStreams.Fight, 6, 17), floor: 10).EnemyIds[0]);
            }

            CollectionAssert.AreEquivalent(new[] { "rat", "wolf", "deep_horror" }, seen);
        }

        [Test]
        public void RespectsTheFloorBand()
        {
            for (ulong seed = 0; seed < 200; seed++)
            {
                var id = EncounterRoll.RollAnchor(Pool(), RngStreams.Open(seed, RngStreams.Fight, 2, 3), floor: 1).EnemyIds[0];

                Assert.That(id, Is.EqualTo("rat").Or.EqualTo("wolf"));
            }
        }

        [Test]
        public void SameStreamKey_SameAnchor()
        {
            var first = EncounterRoll.RollAnchor(Pool(), RngStreams.Open(4242UL, RngStreams.Fight, 6, 17), floor: 10);
            var second = EncounterRoll.RollAnchor(Pool(), RngStreams.Open(4242UL, RngStreams.Fight, 6, 17), floor: 10);

            CollectionAssert.AreEqual(first.EnemyIds, second.EnemyIds);
        }

        [Test]
        public void OnlyWideCreaturesInBand_FieldsOneAnyway()
        {
            var pool = new List<EnemyCandidate> { new EnemyCandidate("treant", slotSpan: 2) };

            var result = EncounterRoll.RollAnchor(pool, RngStreams.Open(1UL, RngStreams.Fight, 1, 1), floor: 1);

            CollectionAssert.AreEqual(new[] { "treant" }, result.EnemyIds);
        }

        [Test]
        public void EmptyPool_FieldsNothing()
        {
            var result = EncounterRoll.RollAnchor(new List<EnemyCandidate>(), RngStreams.Open(1UL, RngStreams.Fight, 1, 1));

            Assert.AreEqual(0, result.EnemyIds.Count);
        }

        [Test]
        public void RollingAnAnchor_DoesNotChangeTheEliteRoll()
        {
            var before = EncounterRoll.Roll(RoomType.EliteFight, Pool(), RngStreams.Open(9UL, RngStreams.Fight, 4, 4), floor: 10);
            EncounterRoll.RollAnchor(Pool(), RngStreams.Open(9UL, RngStreams.Fight, 4, 4), floor: 10);
            var after = EncounterRoll.Roll(RoomType.EliteFight, Pool(), RngStreams.Open(9UL, RngStreams.Fight, 4, 4), floor: 10);

            CollectionAssert.AreEqual(before.EnemyIds, after.EnemyIds);
        }
    }
}
