using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.EditModeTests
{
    // What a room fields, and who from the squad meets it.
    //
    // These properties are what separates a descent from a series of identical
    // battles. Before EncounterRoll existed, FightBootstrap fielded the first
    // character with art against the first three enemies with art at a constant
    // seed, so every fight in every run was the same one -- the difficulty
    // curve was the only thing that varied with depth.
    public class EncounterRollTests
    {
        // A pool with a known shape: three ordinary monsters, two bosses, and
        // one of the ordinaries preferring not to stand at the front.
        private static IReadOnlyList<EnemyCandidate> Pool() => new List<EnemyCandidate>
        {
            new EnemyCandidate("grunt", isBoss: false),
            new EnemyCandidate("archer", isBoss: false, avoidsFrontSlot: true),
            new EnemyCandidate("hound", isBoss: false),
            new EnemyCandidate("warden", isBoss: true),
            new EnemyCandidate("tyrant", isBoss: true),
        };

        private static SeededRandom Rng(ulong seed) => new SeededRandom(seed);

        // The property the whole scheme rests on: position carries the state.
        // The same room in the same run fields the same monsters, so quitting
        // mid-fight and reloading cannot reroll for an easier draw.
        [Test]
        public void SameStreamKeyFieldsTheSameRoster()
        {
            var first = EncounterRoll.Roll(RoomType.Fight, Pool(),
                RngStreams.Open(4242UL, RngStreams.Fight, 6, 17));
            var second = EncounterRoll.Roll(RoomType.Fight, Pool(),
                RngStreams.Open(4242UL, RngStreams.Fight, 6, 17));

            CollectionAssert.AreEqual(first.EnemyIds, second.EnemyIds);
        }

        // Two rooms in one descent must not be the same fight. Different node
        // ids at the same depth are the case that matters -- that is a player
        // choosing between two rooms on the map.
        [Test]
        public void DifferentNodesAtTheSameDepthDifferAcrossTheSeedSpace()
        {
            int differing = 0;

            for (ulong seed = 1; seed <= 40; seed++)
            {
                var left = EncounterRoll.Roll(RoomType.Fight, Pool(),
                    RngStreams.Open(seed, RngStreams.Fight, 3, 1));
                var right = EncounterRoll.Roll(RoomType.Fight, Pool(),
                    RngStreams.Open(seed, RngStreams.Fight, 3, 2));

                if (!left.EnemyIds.SequenceEqual(right.EnemyIds)) differing++;
            }

            // Not "always different" -- two rooms CAN legitimately roll the
            // same pair out of a three-monster pool, and asserting otherwise
            // would be asserting a collision cannot happen. Most of them
            // differing is the real claim.
            Assert.That(differing, Is.GreaterThan(20),
                "Sibling rooms should mostly field different enemies, not the same fight twice.");
        }

        [Test]
        public void EliteRoomFieldsExactlyTwo()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.EliteFight, Pool(), Rng(seed));

                Assert.That(roll.EnemyIds.Count, Is.EqualTo(2), $"seed {seed}");
                Assert.That(roll.IsElite, Is.True, $"seed {seed}");
                Assert.That(roll.IsBoss, Is.False, $"seed {seed}");
            }
        }

        [Test]
        public void NormalRoomFieldsOneOrTwo()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.Fight, Pool(), Rng(seed));

                Assert.That(roll.EnemyIds.Count, Is.InRange(1, 2), $"seed {seed}");
                Assert.That(roll.IsElite, Is.False, $"seed {seed}");
            }
        }

        // A boss cannot wander into an ordinary room. v1 drew normal and elite
        // rooms from a non-boss pool, and losing that would let the run's boss
        // turn up at step two.
        [Test]
        public void OrdinaryRoomsNeverFieldABoss()
        {
            var bossIds = new[] { "warden", "tyrant" };

            for (ulong seed = 1; seed <= 40; seed++)
            {
                foreach (var type in new[] { RoomType.Fight, RoomType.EliteFight })
                {
                    var roll = EncounterRoll.Roll(type, Pool(), Rng(seed));

                    // Element-wise, not IsNotSubsetOf: a mixed roster of one
                    // grunt and one boss is not a subset of the boss list, so
                    // the set assertion would pass a room that fielded a boss.
                    Assert.That(roll.EnemyIds.Any(id => bossIds.Contains(id)), Is.False,
                        $"{type} at seed {seed} fielded a boss: {string.Join(", ", roll.EnemyIds)}");
                }
            }
        }

        // The boss the map promised is the boss that appears. This is what
        // makes RecordBossKill credit the thing the run was sent to kill,
        // rather than whatever happened to be standing there.
        [Test]
        public void BossRoomFieldsTheRunsDeclaredBoss()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.Boss, Pool(), Rng(seed), declaredBossId: "tyrant");

                Assert.That(roll.EnemyIds, Is.EqualTo(new[] { "tyrant" }), $"seed {seed}");
                Assert.That(roll.IsBoss, Is.True, $"seed {seed}");
                Assert.That(roll.IsElite, Is.False, $"seed {seed}");
            }
        }

        // A declared id that no longer resolves is a content gap, not a reason
        // to field nothing.
        [Test]
        public void BossRoomWithAnUnknownDeclaredIdStillFieldsABoss()
        {
            var roll = EncounterRoll.Roll(RoomType.Boss, Pool(), Rng(9),
                declaredBossId: "a-boss-that-was-renamed");

            Assert.That(roll.EnemyIds.Count, Is.EqualTo(1));
            CollectionAssert.Contains(new[] { "warden", "tyrant" }, roll.EnemyIds[0]);
            Assert.That(roll.IsBoss, Is.True);
        }

        // Stated as the invariant rather than by pinning one seed's draw: when
        // the room contains anything willing to stand at the front, the thing
        // that would rather not is not the one standing there.
        [Test]
        public void SomethingElseTakesTheFrontSlotWhenItCan()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var roll = EncounterRoll.Roll(RoomType.Fight, Pool(), Rng(seed));
                if (roll.EnemyIds.Count < 2) continue;

                bool anyWilling = roll.EnemyIds.Any(id => id != "archer");
                if (!anyWilling) continue;

                Assert.That(roll.EnemyIds[0], Is.Not.EqualTo("archer"),
                    $"seed {seed}: the front-avoider took the front slot with a willing pick available");
            }
        }

        // A room full of nothing draws nothing, rather than throwing into
        // OnEnable and leaving a HUD whose buttons resolve into an empty stage.
        [Test]
        public void AnEmptyPoolYieldsAnEmptyRoll()
        {
            var roll = EncounterRoll.Roll(RoomType.Fight, new List<EnemyCandidate>(), Rng(1));

            Assert.That(roll.EnemyIds, Is.Empty);
            Assert.That(roll.IsBoss, Is.False);
        }

        // Bosses only, and an ordinary room: it fights them rather than
        // presenting an empty stage. Graceful degradation, house style.
        [Test]
        public void APoolOfNothingButBossesStillFightsInAnOrdinaryRoom()
        {
            var bossesOnly = new List<EnemyCandidate>
            {
                new EnemyCandidate("warden", isBoss: true),
            };

            var roll = EncounterRoll.Roll(RoomType.Fight, bossesOnly, Rng(3));

            Assert.That(roll.EnemyIds, Is.Not.Empty);
            Assert.That(roll.IsBoss, Is.False, "an ordinary room is not a boss fight just because the pool is");
        }

        // --- who stands up ------------------------------------------------

        // A character with no health entry has never been hurt this run.
        [Test]
        public void AnUnrecordedCharacterFields()
        {
            var party = EncounterRoll.FieldableParty(
                new[] { "shawn", "mira" }, new Dictionary<string, int>());

            CollectionAssert.AreEqual(new[] { "shawn", "mira" }, party);
        }

        // Knocked out earlier in the run: sits this one out until a Rest room.
        [Test]
        public void AKnockedOutCharacterSitsOut()
        {
            var health = new Dictionary<string, int> { ["mira"] = 0, ["shawn"] = 12 };

            var party = EncounterRoll.FieldableParty(new[] { "shawn", "mira", "tam" }, health);

            CollectionAssert.AreEqual(new[] { "shawn", "tam" }, party);
        }

        // Negative health is the same state as zero, and reaches the save
        // through an overkill hit. Treating it as "alive" would field a corpse.
        [Test]
        public void NegativeHealthSitsOutToo()
        {
            var health = new Dictionary<string, int> { ["shawn"] = -6 };

            Assert.That(EncounterRoll.FieldableParty(new[] { "shawn" }, health), Is.Empty);
        }

        // A wipe returns empty rather than substituting someone to avoid it.
        // AUDIT #12 is the reason that matters: a party built at full health
        // for a selection that seeded none made IsSquadWiped unable to fire,
        // and the run became unloseable.
        [Test]
        public void AWipedSquadFieldsNobody()
        {
            var health = new Dictionary<string, int> { ["shawn"] = 0, ["mira"] = 0 };

            Assert.That(EncounterRoll.FieldableParty(new[] { "shawn", "mira" }, health), Is.Empty);
        }

        // The full squad, not the first of them. This is the whole point of
        // the change: the placeholder fielded exactly one character while every
        // screen around the fight -- rewards, settlement, the defeat screen --
        // already reported on all of them.
        [Test]
        public void TheWholeSquadFields()
        {
            var squad = new[] { "shawn", "mira", "tam", "isolde" };

            var party = EncounterRoll.FieldableParty(squad, new Dictionary<string, int>
            {
                ["shawn"] = 30,
                ["mira"] = 1,
                ["tam"] = 44,
                ["isolde"] = 7,
            });

            CollectionAssert.AreEqual(squad, party);
        }
        // ---- the stage is the ceiling -------------------------------------------
        //
        // A combatant with no slot to stand in is not merely undrawn: the
        // session goes on giving it turns, so it hits the player from off
        // screen. These two numbers were independent facts, and nothing said
        // the roll had to respect the stage.
        [Test]
        public void NoRoomEverRollsMoreEnemiesThanTheStageCanShow()
        {
            var pool = new[]
            {
                new EnemyCandidate("a"),
                new EnemyCandidate("b"),
                new EnemyCandidate("c"),
                new EnemyCandidate("d"),
                new EnemyCandidate("e"),
            };

            foreach (RoomType room in new[] { RoomType.Fight, RoomType.EliteFight })
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    var result = EncounterRoll.Roll(room, pool, new SeededRandom((ulong)seed));

                    Assert.LessOrEqual(result.EnemyIds.Count, FightHudSpec.StageSlotsPerSide,
                        $"a {room} on seed {seed} fielded {result.EnemyIds.Count} enemies against " +
                        $"{FightHudSpec.StageSlotsPerSide} stage slots - the extras fight from off screen");
                }
            }
        }

        // The tuning constants themselves, so raising one past the stage fails
        // here rather than being silently clamped and quietly not what was
        // intended.
        [Test]
        public void TheTuningItselfFitsTheStage()
        {
            Assert.LessOrEqual(EncounterRoll.EliteEnemyCount, FightHudSpec.StageSlotsPerSide,
                "an elite room is tuned to field more enemies than the stage has slots");

            Assert.LessOrEqual(EncounterRoll.NormalMaxEnemiesExclusive - 1, FightHudSpec.StageSlotsPerSide,
                "a normal room is tuned to field more enemies than the stage has slots");
        }

    }
}
