using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // The Ward — the one construct the Fragile Lamb's whole path is built on.
    //
    // Called a Ward everywhere, and implemented with
    // StatusEffectType.Shielded. "Shield" already means two other things in
    // this pipeline (BreakShield is a stagger meter, and Shielded itself is
    // the Magical Shield relic's per-hit reduction), so a third meaning was
    // ruled out by name before any of this was written.
    //
    // Every number is asserted as a literal rather than recomputed from the
    // production formula — CLAUDE.md gotcha 5.
    public class WardTests
    {
        private static CombatantState Fighter(string name = "Shawn", int maxHealth = 200)
        {
            return new CombatantState(name, true, maxHealth, 30, 10, 4, 8);
        }

        private static void Give(CombatantState combatant, params TalentEffect[] effects)
        {
            combatant.Talents = new TalentEffectSet(effects);
        }

        private static void Ward(CombatantState caster, CombatantState wearer, int reduction)
        {
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, reduction, 999, caster);
        }

        [Test]
        public void AWard_SoftensTheNextHitAndIsThenSpent()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardReductionPercent, 60));
            Ward(lamb, lamb, 60);

            var outcome = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(40, outcome.Damage);
            Assert.AreSame(lamb, outcome.WardedBy);
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "A ward pops on the first hit");
        }

        // Popping on the first hit is load-bearing, not incidental: a ward
        // that absorbed several hits would pay the engine each time and hand
        // back more wool than it cost.
        [Test]
        public void ASecondHit_FindsNoWard()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60);

            StatusEffects.ConsumeWard(lamb, 100);
            var second = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(100, second.Damage);
            Assert.IsNull(second.WardedBy);
        }

        // The CASTER's talents decide what a ward does on the way out, not the
        // wearer's. A ward Shawn put on the turtle heals by Shawn's Mending
        // Fleece, and the turtle has no say in it.
        [Test]
        public void MendingFleece_HealsTheWearer_ByTheCastersTalent()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardHealsWhenSpent, 10));

            var tank = Fighter("Turtle", maxHealth: 300);
            tank.CurrentHealth = 100;
            Ward(lamb, tank, 50);

            var outcome = StatusEffects.ConsumeWard(tank, 40);

            Assert.AreEqual(20, outcome.Damage, "Half of forty");
            Assert.AreEqual(30, outcome.Healed, "A tenth of the WEARER's 300 max health");
            Assert.AreEqual(130, tank.CurrentHealth);
        }

        [Test]
        public void WithoutMendingFleece_AWardBreakingHealsNothing()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");
            tank.CurrentHealth = 100;
            Ward(lamb, tank, 50);

            Assert.AreEqual(0, StatusEffects.ConsumeWard(tank, 40).Healed);

            // Unchanged, because consuming a ward REDUCES A FIGURE and does
            // not apply it — CombatMath.ApplyDamage is still what touches
            // health, further down the same funnel. Worth pinning: a version
            // of this that also dealt the damage would double every hit that
            // met a ward.
            Assert.AreEqual(100, tank.CurrentHealth);
        }

        // The Golden Fleece. Note WardedBy is still reported — the engine's
        // question is "was this combatant warded when it was struck", not
        // "did something break", which is exactly what keeps her income alive
        // after the capstone.
        [Test]
        public void TheGoldenFleece_LeavesTheWardStanding()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            Ward(lamb, lamb, 60);

            var outcome = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(40, outcome.Damage, "It still softens the hit");
            Assert.AreSame(lamb, outcome.WardedBy, "And still counts as having been warded, so the engine still pays");
            Assert.IsTrue(StatusEffects.IsWarded(lamb), "But it does not pop");
        }

        // The Magical Shield relic applies the same status with no source.
        // Every Lamb rule has to read that as absent rather than throwing.
        [Test]
        public void AnUnsourcedShield_StillReducesAndBreaks()
        {
            var wearer = Fighter();
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 50, 999);

            var outcome = StatusEffects.ConsumeWard(wearer, 80);

            Assert.AreEqual(40, outcome.Damage);
            Assert.IsNull(outcome.WardedBy, "Nobody cast it, so nobody is paid for it");
            Assert.AreEqual(0, outcome.Healed);
            Assert.IsFalse(StatusEffects.IsWarded(wearer));
        }

        [Test]
        public void TheOldIntReturningOverload_StillAnswersTheSame()
        {
            var wearer = Fighter();
            StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 50, 999);

            Assert.AreEqual(40, StatusEffects.ConsumeShieldedReduction(wearer, 80));
        }

        [Test]
        public void WardedBy_NamesTheCaster()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Assert.IsNull(StatusEffects.WardedBy(tank));

            Ward(lamb, tank, 50);
            Assert.AreSame(lamb, StatusEffects.WardedBy(tank));
        }
    }

    // Gift: Fury — the exact mirror of a Ward. Spent by the swing rather than
    // by the clock.
    public class EmpoweredTests
    {
        private static CombatantState Fighter()
        {
            return new CombatantState("Owl", true, 100, 10, 10, 2, 8);
        }

        [Test]
        public void AnEmpoweredSwing_SpendsTheGift()
        {
            var ally = Fighter();
            StatusEffects.Apply(ally.Statuses, StatusEffectType.Empowered, 50, 999);

            Assert.AreEqual(50, StatusEffects.ConsumeEmpowerment(ally));
            Assert.AreEqual(0, StatusEffects.ConsumeEmpowerment(ally), "One swing, not a window");
        }

        [Test]
        public void WithNoGift_ThereIsNothingToSpend()
        {
            Assert.AreEqual(0, StatusEffects.ConsumeEmpowerment(Fighter()));
            Assert.AreEqual(0, StatusEffects.ConsumeEmpowerment(null));
        }

        // Weight of Wool and Gift: Fury both arrive through the same field, so
        // this is where the "does the multiplier land on the attack side"
        // question gets answered — before defense, not after.
        [Test]
        public void TheAttackBonus_IsAppliedBeforeDefenseIsSubtracted()
        {
            var attacker = new CombatantState("Shawn", true, 200, 30, 10, 4, 8);
            var target = new CombatantState("Golem", false, 300, 0, 5, 6, 4);

            // (10 - 6) x 5 = 20 with no bonus.
            Assert.AreEqual(20, CombatMath.ComputeAttackDamage(attacker, target));

            attacker.BonusAttackPercent = 100;

            // Doubling the ATTACK gives (20 - 6) x 5 = 70. Doubling the
            // finished figure would have given 40 -- and would have made the
            // golem's armour worth half as much against exactly the swing it
            // most needs to blunt.
            Assert.AreEqual(70, CombatMath.ComputeAttackDamage(attacker, target));
        }
    }

    // Gift: Haste, at the scheduler. The mirror of Headbutt's shove.
    public class GiftHasteTests
    {
        [Test]
        public void PullToFront_HandsTheNextTurnToTheHurriedActor()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("fast", 30);
            order.AddCombatant("middle", 20);
            order.AddCombatant("slow", 10);
            order.Start();

            // "fast" opened and spent its charge, so "middle" would be next.
            Assert.IsTrue(order.PullToFront("slow"));

            Assert.AreEqual("slow", order.Advance());
        }

        // Landing them just above the most-charged rival rather than over the
        // threshold outright. Crossing the line here would hand them the turn
        // AND carry the overflow into the next contest, so one gift would
        // quietly buy a turn and a half.
        [Test]
        public void BeingHurried_DoesNotAlsoBuyTheTurnAfterIt()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("fast", 30);
            order.AddCombatant("slow", 10);
            order.Start();

            order.PullToFront("slow");

            Assert.AreEqual("slow", order.Advance());
            Assert.AreEqual("fast", order.Advance(), "The queue should go straight back to normal");
        }

        [Test]
        public void HurryingSomethingNotInTheOrder_IsRefused()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("only", 10);
            order.Start();

            Assert.IsFalse(order.PullToFront("ghost"));
        }
    }
}
