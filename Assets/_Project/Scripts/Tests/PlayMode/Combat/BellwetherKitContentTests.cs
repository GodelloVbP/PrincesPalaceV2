using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE BELLWETHER'S KIT AS BUILT (docs/PLAN_BELLWETHER_KIT.md M5): the
    // shipped content rows, not fixtures -- the schedule, Scratch's Bleed, the
    // chains' pull and the knell's 110/35/0 by seat through defence. Engine
    // behaviour for the same pieces is pinned with fixtures in
    // BellwetherKitMechanicsTests / EnemyScheduleTests; this pins that the
    // content says what the plan says. NEEDS BUILT CONTENT.
    public class BellwetherKitContentTests
    {
        [TearDown]
        public void Restore() => ContentDatabase.Reset();

        private static ResolvedSkill Skill(string id)
        {
            var definition = ContentDatabase.GetSkill(id);
            Assert.IsNotNull(definition, $"'{id}' is not in built content");
            return definition.Data;
        }

        // The real sheep against the real Bellwether, both made durable so
        // nothing dies of the fixture: Shawn 20000 HP, the bell a wall.
        private static (FightSession session, CombatantState shawn, CombatantState bell) RealFight()
        {
            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { "bellwether" },
                new SeededRandom(11));
            Assert.IsNotNull(built?.Session, "the solo Bellwether fight could not be built");

            var session = built.Session;
            var shawn = built.Party[0];
            var bell = session.Encounter.Enemies[0];
            shawn.MaxHealth = shawn.CurrentHealth = 20000;
            shawn.Speed = 200;
            bell.MaxHealth = bell.CurrentHealth = 1000000;
            session.DamageVarianceRange = 0f;
            session.Begin();
            return (session, shawn, bell);
        }

        [Test]
        public void TheBellwetherDrawsOnlyScratch_AndSchedulesChainsThenKnellOnTurnsOneAndFive()
        {
            var source = ContentDatabase.GetEnemy("bellwether").Data;

            Assert.AreEqual(0f, source.AttackWeight, "no plain swing: Scratch carries the Bleed");
            CollectionAssert.AreEqual(new[] { "bellwether_scratch", "dark_chains", "death_knell" },
                source.Abilities.Select(a => a.SkillId).ToArray());
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f }, source.Abilities.Select(a => a.Weight).ToArray());
            Assert.AreEqual(1, source.Schedule.Length);
            CollectionAssert.AreEqual(new[] { 1, 5 }, source.Schedule[0].OnTurns);
            CollectionAssert.AreEqual(new[] { "dark_chains", "death_knell" }, source.Schedule[0].Skills);
            Assert.AreEqual("cast", source.RallyStance);
            Assert.AreEqual("Spells/bellwether_toll_ripple", source.RallyVfx.layers[0].path);
        }

        [Test]
        public void OnItsOwnActingTurns_ChainsKnellOnOneTwoAndFiveSix_ScratchOtherwise()
        {
            var (session, shawn, bell) = RealFight();
            var labels = new List<string>();

            for (int guard = 0; session.ActingTurnsOf(bell) < 8 && guard < 400 && !session.IsOver; guard++)
            {
                // What it has committed to for its NEXT acting turn, once each.
                if (labels.Count == session.ActingTurnsOf(bell)) labels.Add(session.IntentDetailFor(bell).Value.Label);

                // Out of the knell's reach, so the fight survives the fixture.
                session.Encounter.PlaceAt(shawn, 2, out _);
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            CollectionAssert.AreEqual(new[]
            {
                "Dark Chains", "Death Knell", "Scratch", "Scratch",
                "Dark Chains", "Death Knell", "Scratch", "Scratch",
            }, labels);
        }

        [Test]
        public void ScratchIsAPhysicalLungeInTheAttackPose_AndLeavesBleed()
        {
            var scratch = Skill("bellwether_scratch");
            Assert.AreEqual(SkillEffect.DamageSingle, scratch.Effect);
            Assert.AreEqual("attack", scratch.Stance);
            Assert.AreEqual(StatusEffectType.Bleed, scratch.AppliesStatus);

            var (session, shawn, bell) = RealFight();
            session.ResolveSkillForTest(bell, scratch, shawn);

            Assert.Less(shawn.CurrentHealth, 20000, "a hit");
            Assert.IsTrue(shawn.Statuses.Any(s => s.Type == StatusEffectType.Bleed), "and Bleed");
        }

        [Test]
        public void DarkChainsPullARearShawnToTheFront()
        {
            var chains = Skill("dark_chains");
            Assert.AreEqual(SkillEffect.Reposition, chains.Effect);
            Assert.AreEqual(1, chains.ToSeat);
            Assert.AreEqual("extra", chains.Stance);

            var (session, shawn, bell) = RealFight();
            session.Encounter.PlaceAt(shawn, 2, out _);
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));

            session.ResolveSkillForTest(bell, chains, shawn);

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(20000, shawn.CurrentHealth, "the chains deal nothing");
        }

        [TestCase(0, 0)]
        [TestCase(1, 650)]
        [TestCase(2, 1000)]
        public void TheKnellDeals110_35_0PercentOfMaxHealthBySeat_ThroughAnyDefence(int seat, int left)
        {
            var knell = Skill("death_knell");
            Assert.AreEqual("cast", knell.Stance);
            Assert.AreEqual(DamageType.Void, knell.OwnDamageType);
            Assert.IsTrue(knell.IgnoresDefense);
            CollectionAssert.AreEqual(new[] { 110, 35, 0 }, knell.DamageBySeatMaxHpPercent);

            var shawn = new CombatantState("Shawn", true, 1000, 10, 20, 10) { MagicalDefense = 500, PhysicalDefense = 500 };
            var bell = new CombatantState("The Bellwether", false, 100000, 0, 0, 1);
            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { bell }), null, null,
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            if (seat > 0) Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, seat, out _));

            session.ResolveSkillForTest(bell, knell, shawn);

            Assert.AreEqual(left, shawn.CurrentHealth);
        }

    }
}
