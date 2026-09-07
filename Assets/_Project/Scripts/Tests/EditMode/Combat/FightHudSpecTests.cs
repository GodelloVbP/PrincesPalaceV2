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

        // THE REACH RULE ITSELF LIVES ON THE SESSION now (FightSession.
        // CanReach -- it has to see relics and taunts, which the encounter
        // cannot). What stayed here is the fact the rule is computed FROM:
        // the encounter's own living rank.

        [Test]
        public void RankCountsFromTheFrontInListOrder()
        {
            var player = Fighter("Player", true);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var encounter = new CombatEncounter(new[] { player }, new[] { front, back });

            Assert.AreEqual(0, encounter.LivingRankOf(front));
            Assert.AreEqual(1, encounter.LivingRankOf(back));
            Assert.AreSame(front, encounter.FrontEnemy);
            Assert.AreSame(player, encounter.FrontPartyMember);
        }

        [Test]
        public void DeathCompressesTheRanksBehindTheCorpse()
        {
            var player = Fighter("Player", true);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var encounter = new CombatEncounter(new[] { player }, new[] { front, back });

            front.CurrentHealth = 0;

            Assert.IsFalse(front.IsAlive);
            Assert.AreEqual(-1, encounter.LivingRankOf(front), "a corpse has no rank");
            Assert.AreEqual(0, encounter.LivingRankOf(back),
                "rank falls out of who is still standing, so a dead front rank blocks nothing");
            Assert.AreSame(front, encounter.Enemies[0], "and the corpse keeps its slot in the list");
            Assert.AreSame(back, encounter.Enemies[1], "list order is untouched by a death");
        }

        [Test]
        public void RankIsAnsweredForAnyoneNotOnTheFieldWithTheSameSentinel()
        {
            var player = Fighter("Player", true);
            var ally = Fighter("Ally", true);
            var front = Fighter("Front", false);
            var encounter = new CombatEncounter(new[] { player, ally }, new[] { front });

            Assert.AreEqual(1, encounter.LivingRankOf(ally), "both sides are ranked the same way");
            Assert.AreEqual(-1, encounter.LivingRankOf(null));
            Assert.AreEqual(-1, encounter.LivingRankOf(Fighter("Stranger", false)),
                "someone who is not in this encounter has no rank in it");
        }

        [Test]
        public void SwappingPartySlotsReordersTheListAndNothingElse()
        {
            var player = Fighter("Player", true);
            var ally = Fighter("Ally", true);
            var encounter = new CombatEncounter(new[] { player, ally }, new[] { Fighter("Front", false) });

            Assert.IsTrue(encounter.SwapPartySlots(0, 1));

            Assert.AreSame(ally, encounter.PlayerParty[0]);
            Assert.AreSame(player, encounter.PlayerParty[1]);
            Assert.IsFalse(encounter.SwapPartySlots(0, 0), "a slot cannot trade with itself");
            Assert.IsFalse(encounter.SwapPartySlots(0, 5), "and an out-of-range slot is refused, not clamped");
        }
    }
}
