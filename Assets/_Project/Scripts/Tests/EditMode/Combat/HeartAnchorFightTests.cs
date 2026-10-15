using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Anchors at the heart (docs/PLAN_RUN_MAP.md section 6): the boss in the
    // rear with living anchors in front, the rest queued; a reinforcement takes
    // a fallen enemy's list index (its slot and rank); the boss's death ends
    // the fight and the queue leaves with it; elite pay is per enemy.
    public class HeartAnchorFightTests
    {
        private const int StageSlots = 3;

        private static ResolvedEnemy Source(string id, int exp, int gold, bool isBoss = false) =>
            new ResolvedEnemy(id, id, new StatBlock(), exp, gold, isBoss,
                DamageType.Physical, DamageType.Physical, 0);

        private sealed class Heart
        {
            public FightSession Session;
            public CombatantState Hero;
            public CombatantState Boss;
            public List<CombatantState> Anchors = new List<CombatantState>();
            public CombatEncounter Encounter => Session.Encounter;
        }

        // The boss fight as FightEncounterAdapter lays it out: the first
        // FrontCount anchors in front of the boss, the rest queued, the boss
        // declared leader. Anchors have 1 HP so one swing kills; the boss has
        // plenty, so only a ranged packet aimed at it can end the fight.
        private static Heart Build(int anchorCount, int bossHealth = 5000)
        {
            var heart = new Heart();
            heart.Hero = new CombatantState("Shawn", true, 50000, 30, 40, 10);
            heart.Boss = new CombatantState("Boss", false, bossHealth, 10, 1, 1);

            int front = HeartAnchors.FrontCount(anchorCount, StageSlots);
            var field = new List<CombatantState>();
            var fieldKits = new List<EnemyKit>();
            for (int i = 0; i < anchorCount; i++)
            {
                heart.Anchors.Add(new CombatantState($"Anchor{i}", false, 1, 10, 1, 1));
            }

            for (int i = 0; i < front; i++)
            {
                field.Add(heart.Anchors[i]);
                fieldKits.Add(new EnemyKit(Source($"anchor{i}", 20, 10), isElite: true));
            }

            field.Add(heart.Boss);
            fieldKits.Add(new EnemyKit(Source("boss", 100, 50, isBoss: true), isElite: false));

            var encounter = new CombatEncounter(new[] { heart.Hero }, field);
            Assert.IsTrue(encounter.DeclareLeader(heart.Boss));

            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { TestSkills.RangedPacket(100000) }, null, null);
            heart.Session = new FightSession(encounter, new List<PlayerKit> { kit }, fieldKits,
                new SeededRandom(7), isBossFight: true, stageSlotsPerSide: StageSlots)
            {
                DamageVarianceRange = 0f,
            };

            for (int i = front; i < anchorCount; i++)
            {
                Assert.IsTrue(heart.Session.QueueReinforcement(heart.Anchors[i],
                    new EnemyKit(Source($"anchor{i}", 20, 10), isElite: true)));
            }

            for (int i = 0; i < anchorCount; i++) heart.Session.MarkAnchor(heart.Anchors[i], 100 + i);

            heart.Session.Begin();
            return heart;
        }

        private static void KillWithASwing(Heart heart, CombatantState target)
        {
            Assert.IsTrue(heart.Session.IsPlayerTurn, "the hero acts");
            Assert.IsTrue(heart.Session.ExecuteAttack(target), $"{target.Name} is in melee reach");
            Assert.IsFalse(target.IsAlive);
        }

        [Test]
        public void ZeroAnchors_TheBossStandsInFront_AndMeleeReachesIt()
        {
            var heart = Build(0);

            Assert.AreEqual(0, heart.Encounter.LivingRankOf(heart.Boss));
            Assert.IsTrue(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Boss));
            Assert.AreEqual(0, heart.Encounter.Reinforcements.Count);
        }

        [Test]
        public void OneAnchor_GuardsTheBossUntilItFalls()
        {
            var heart = Build(1);

            Assert.AreEqual(1, heart.Encounter.LivingRankOf(heart.Boss));
            Assert.IsFalse(heart.Session.ExecuteAttack(heart.Boss), "melee is refused on a guarded boss");
            Assert.AreEqual(5000, heart.Boss.CurrentHealth);

            KillWithASwing(heart, heart.Anchors[0]);

            Assert.IsFalse(heart.Session.IsOver, "the boss still stands");
            Assert.AreEqual(0, heart.Encounter.LivingRankOf(heart.Boss));
            Assert.IsTrue(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Boss));
        }

        [Test]
        public void TwoAnchors_BothStandInFront_AndTheBossIsRankTwo()
        {
            var heart = Build(2);

            CollectionAssert.AreEqual(new[] { heart.Anchors[0], heart.Anchors[1], heart.Boss },
                heart.Encounter.Enemies.ToList());
            Assert.AreEqual(2, heart.Encounter.LivingRankOf(heart.Boss));
            Assert.IsFalse(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Boss));
            Assert.IsFalse(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Anchors[1]));

            KillWithASwing(heart, heart.Anchors[0]);

            Assert.AreEqual(1, heart.Encounter.LivingRankOf(heart.Boss));
            Assert.IsFalse(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Boss),
                "the second anchor still guards");
        }

        [Test]
        public void FourAnchors_TwoQueue_AndEachTakesAFallenAnchorsIndex()
        {
            var heart = Build(4);
            var encounter = heart.Encounter;

            CollectionAssert.AreEqual(new[] { heart.Anchors[0], heart.Anchors[1], heart.Boss },
                encounter.Enemies.ToList(), "two in front, the boss in the rear");
            CollectionAssert.AreEqual(new[] { heart.Anchors[2], heart.Anchors[3] },
                encounter.Reinforcements.ToList(), "the rest queue in formation order");

            KillWithASwing(heart, heart.Anchors[0]);

            // Seated at the turn boundary the swing's own advance crossed.
            Assert.AreSame(heart.Anchors[2], encounter.Enemies[0], "the reinforcement takes index 0");
            Assert.AreEqual(3, encounter.Enemies.Count, "the list never outgrows the stage");
            CollectionAssert.AreEqual(new[] { heart.Anchors[0] }, encounter.DisplacedEnemies.ToList());
            Assert.AreEqual(0, encounter.LivingRankOf(heart.Anchors[2]), "and its place at the front");
            Assert.AreEqual(2, encounter.LivingRankOf(heart.Boss));
            Assert.IsFalse(heart.Anchors[2].IsSummon, "a reinforcement counts as a kill");
            Assert.IsNotNull(heart.Session.SourceFor(heart.Anchors[2]), "its kit was registered");

            // The newcomer falls in turn; the last one takes the same index.
            KillWithASwing(heart, heart.Anchors[2]);

            Assert.AreSame(heart.Anchors[3], encounter.Enemies[0], "the next takes index 0 again");
            Assert.AreSame(heart.Anchors[1], encounter.Enemies[1], "the other front anchor never moved");
            Assert.AreEqual(0, encounter.Reinforcements.Count);
            Assert.AreEqual(3, encounter.LivingEnemies.Count(), "the field is never left short");
            Assert.IsFalse(heart.Session.CanReachEnemy(heart.Hero, Reach.Melee, heart.Boss));

            var lines = heart.Session.DrainBeats().SelectMany(b => b.Messages).ToList();
            Assert.AreEqual(2, lines.Count(m => m.Contains("steps into the line")));
        }

        [Test]
        public void TheBossKilledWithAnchorsQueued_EndsTheFight_AndTheyLeaveUnpaid()
        {
            var heart = Build(4);

            KillWithASwing(heart, heart.Anchors[0]);

            // A ranged packet past the guard: the leader rule, not a clear.
            Assert.IsTrue(heart.Session.CastSkill(TestSkills.RangedPacket(100000), heart.Boss));

            Assert.IsFalse(heart.Boss.IsAlive);
            Assert.IsTrue(heart.Session.IsOver);
            Assert.IsTrue(heart.Session.PlayerWon);
            Assert.AreEqual(FightEndReason.LeaderFell, heart.Session.EndReason);
            Assert.AreEqual(0, heart.Encounter.Reinforcements.Count, "the queue left with the leader");
            CollectionAssert.AreEqual(new[] { heart.Anchors[2], heart.Anchors[1] },
                heart.Encounter.DismissedEnemies.ToList(), "the two standing anchors died with the boss");
            Assert.AreEqual(0, heart.Encounter.LivingEnemies.Count());

            // Paid: the boss (100 exp, 50 gold) and one elite anchor (20 x 1.56
            // = 31.2 -> 31 exp; 10 x 1.56 = 15.6 -> 16 gold), 66 raw gold cut
            // to 10, at depth 0. The
            // two standing and the one queued pay nothing.
            Assert.AreEqual(131, heart.Session.Payout.Value.Experience);
            Assert.AreEqual(10, heart.Session.Payout.Value.Gold);

            CollectionAssert.AreEqual(new[] { 100 }, heart.Session.FallenAnchorNodeIds.ToList());
        }

        [Test]
        public void TheBossKilledLast_IsAnOrdinaryDefeat()
        {
            var heart = Build(1, bossHealth: 1);

            KillWithASwing(heart, heart.Anchors[0]);
            KillWithASwing(heart, heart.Boss);

            Assert.AreEqual(FightEndReason.Defeated, heart.Session.EndReason);
        }

        [Test]
        public void TheFieldEmptyWithAQueue_IsNotOver()
        {
            // No leader: the queue alone keeps the fight going.
            var hero = new CombatantState("Shawn", true, 500, 30, 40, 10);
            var first = new CombatantState("First", false, 1, 10, 1, 1);
            var second = new CombatantState("Second", false, 1, 10, 1, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { first });
            Assert.IsTrue(encounter.QueueReinforcement(second));

            first.CurrentHealth = 0;

            Assert.IsFalse(encounter.IsOver);
            Assert.AreEqual(1, encounter.ReseatFallen());
            Assert.AreSame(second, encounter.Enemies[0]);
            Assert.IsFalse(encounter.IsOver);

            second.CurrentHealth = 0;
            Assert.IsTrue(encounter.IsOver);
            Assert.IsTrue(encounter.PlayerWon);
            Assert.IsFalse(encounter.EndedByLeaderFalling);
        }

        [Test]
        public void TheEncounterRefusesABadQueueOrLeader()
        {
            var hero = new CombatantState("Shawn", true, 500, 30, 40, 10);
            var foe = new CombatantState("Foe", false, 10, 10, 1, 1);
            var stranger = new CombatantState("Stranger", false, 10, 10, 1, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });

            Assert.IsFalse(encounter.QueueReinforcement(foe), "already fielded");
            Assert.IsFalse(encounter.QueueReinforcement(hero), "a party member");
            Assert.IsFalse(encounter.DeclareLeader(stranger), "not on the field");
            Assert.IsTrue(encounter.DeclareLeader(foe));
            Assert.IsFalse(encounter.DeclareLeader(foe), "only once");
        }

        // ---- the heart's pure rules ----------------------------------------------

        [Test]
        public void Formation_IsStrongestFirst_TiesByMapOrder()
        {
            var anchors = new[]
            {
                new HeartAnchors.Anchor(7, 5, "c", 10, PrizeKind.None),
                new HeartAnchors.Anchor(3, 2, "a", 40, PrizeKind.None),
                new HeartAnchors.Anchor(9, 2, "b", 40, PrizeKind.None),
                new HeartAnchors.Anchor(4, 1, "d", 20, PrizeKind.None),
            };

            CollectionAssert.AreEqual(new[] { 3, 9, 4, 7 },
                HeartAnchors.Formation(anchors).Select(a => a.NodeId).ToList());
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(4, 2)]
        [TestCase(6, 2)]
        public void FrontCount_LeavesTheBossItsSlot(int anchors, int front)
        {
            Assert.AreEqual(front, HeartAnchors.FrontCount(anchors, StageSlots));
        }

        [Test]
        public void HealthAtHeart_IsTheShare_NeverZero()
        {
            Assert.AreEqual(500, HeartAnchors.HealthAtHeart(1000));
            Assert.AreEqual(1, HeartAnchors.HealthAtHeart(1));
        }

        [Test]
        public void AHeartAnchorsGearChip_IsQueuedForTheBossSettlement_InMapOrder()
        {
            // PLAN_ITEMS.md 5.4: anchors that die at the heart add their chips
            // to the boss settlement in map order. Gear (PrizeKind.Item) is a
            // no-op pipeline hook today; what is pinned is that it is queued.
            var fallen = new[]
            {
                new HeartAnchors.Anchor(12, 6, "late", 90, PrizeKind.Gold),
                new HeartAnchors.Anchor(5, 3, "gear", 10, PrizeKind.Item),
                new HeartAnchors.Anchor(8, 3, "none", 50, PrizeKind.None),
            };

            var order = HeartAnchors.SettlementOrder(fallen);

            CollectionAssert.AreEqual(new[] { 5, 8, 12 }, order.Select(a => a.NodeId).ToList());
            Assert.AreEqual(PrizeKind.Item, order[0].Prize, "the Gear chip is queued first");
        }
    }
}
