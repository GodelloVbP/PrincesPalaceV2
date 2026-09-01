using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    public class FightHudSpecTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int speed = 5) =>
            new CombatantState(name, isPlayerSide, 20, 10, 5, speed);

        [Test]
        public void CapacitiesMatchV1()
        {
            Assert.AreEqual(6, FightHudSpec.InitiativeSlots);
            Assert.AreEqual(3, FightHudSpec.StageSlotsPerSide);
            Assert.AreEqual(16, FightHudSpec.WoolPips);

            // NOT v1 parity any more -- v1 shipped RUN as a fifth verb, and it
            // never actually let anyone flee (see FightScreen.BuildVerbColumn's
            // own comment). Four, once RUN is gone and HOLD BACK takes its
            // place rather than joining it.
            Assert.AreEqual(4, FightHudSpec.Verbs);
        }

        // DamagePopups moved out of CapacitiesMatchV1 on purpose -- it no
        // longer does. See FightHudSpec.DamagePopups' own comment for why 6
        // undersold what an AllEnemies cast needs.
        [Test]
        public void DamagePopupsIsDoubledPastV1ForSimultaneousMultiTargetHits()
        {
            Assert.AreEqual(12, FightHudSpec.DamagePopups);
        }

        [Test]
        public void OnePlatePerEnemyStageSlot()
        {
            // v1 tied these together with `EnemyPlateCount = EnemyStageSlots`.
            // Stated as a relationship rather than two independent numbers,
            // because two numbers that must agree are two numbers that drift.
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, FightHudSpec.EnemyPlates);
        }

        [Test]
        public void MeleeCannotReachPastALivingFrontRank()
        {
            var player = Fighter("Player", true);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var encounter = new CombatEncounter(new[] { player }, new[] { front, back });

            Assert.IsTrue(encounter.CanMeleeReach(front), "the front rank is always reachable");
            Assert.IsFalse(encounter.CanMeleeReach(back), "the front rank has to be cleared first");
        }

        [Test]
        public void ClearingTheFrontRankOpensTheOneBehindIt()
        {
            var player = Fighter("Player", true);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var encounter = new CombatEncounter(new[] { player }, new[] { front, back });

            front.CurrentHealth = 0;

            Assert.IsFalse(front.IsAlive);
            Assert.IsTrue(encounter.CanMeleeReach(back),
                "rank falls out of who is still standing, so a dead front rank blocks nothing");
        }

        [Test]
        public void AlliesAndNullAreNotConstrainedByTheFrontRank()
        {
            var player = Fighter("Player", true);
            var ally = Fighter("Ally", true);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var encounter = new CombatEncounter(new[] { player, ally }, new[] { front, back });

            Assert.IsTrue(encounter.CanMeleeReach(ally), "this rule is about reaching PAST enemies, not about allies");
            Assert.IsFalse(encounter.CanMeleeReach(null), "nothing is reachable when there is no target");
        }
    }
}
