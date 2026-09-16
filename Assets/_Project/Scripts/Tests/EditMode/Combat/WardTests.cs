using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // The Ward -- the one construct the Fragile Lamb's whole path is built on,
    // and since 2026-09-16 (AUDIT #152, the owner's call) a SHIELD: a pool of
    // points that damage is taken out of, not a percentage off one blow.
    //
    // Called a Ward everywhere, and implemented with
    // StatusEffectType.Shielded. "Shield" already means something else in this
    // pipeline (BreakShield is a stagger meter), so the two names stay apart
    // even now that a ward is a shield in the player's sense.
    //
    // Every number is asserted as a literal rather than recomputed from the
    // production formula -- CLAUDE.md gotcha 5.
    public class WardTests
    {
        private static CombatantState Fighter(string name = "Shawn", int maxHealth = 200)
        {
            return new CombatantState(name, true, maxHealth, 30, 10, 8);
        }

        private static void Give(CombatantState combatant, params TalentEffect[] effects)
        {
            combatant.Talents = new TalentEffectSet(effects);
        }

        private static void Ward(CombatantState caster, CombatantState wearer, int points, int turns = 2)
        {
            Assert.IsTrue(StatusEffects.ApplyWard(wearer.Statuses, points, turns, caster),
                "fixture: the ward did not land");
        }

        [Test]
        public void AWard_EatsUpToItsPoolAndTheRestReachesHealth()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60);

            var outcome = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(40, outcome.Damage, "100 less the pool's 60");
            Assert.AreEqual(60, outcome.Absorbed);
            Assert.IsTrue(outcome.Broke);
            Assert.AreSame(lamb, outcome.WardedBy);
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "the pool was emptied, so the ward is gone");
        }

        // THE HALF THE OLD MODEL COULD NOT DO. A percentage ward was spent
        // whole by the first blow whatever its size; a pool keeps what a small
        // hit did not need.
        [Test]
        public void ASmallHit_SpendsOnlyWhatItCost()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60);

            var outcome = StatusEffects.ConsumeWard(lamb, 19);

            Assert.AreEqual(0, outcome.Damage, "nothing reached health");
            Assert.AreEqual(19, outcome.Absorbed);
            Assert.IsFalse(outcome.Broke);
            Assert.AreEqual(41, StatusEffects.WardPoints(lamb));
        }

        [Test]
        public void ASecondHit_FindsWhateverTheFirstOneLeft()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60);

            StatusEffects.ConsumeWard(lamb, 100);
            var second = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(100, second.Damage);
            Assert.IsNull(second.WardedBy);
            Assert.AreEqual(0, second.Absorbed);
        }

        // ONE WARD PER CHARACTER. A bigger pool replaces a smaller one, an
        // equal one refreshes it, a smaller one is refused outright -- and the
        // refusal is a return value rather than a silent no-op precisely
        // because the caster has a line to write about it.
        [Test]
        public void ABiggerPoolReplaces_AnEqualOneRefreshes_ASmallerOneIsRefused()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Assert.IsTrue(StatusEffects.ApplyWard(tank.Statuses, 50, 2, lamb));
            Assert.IsFalse(StatusEffects.ApplyWard(tank.Statuses, 49, 2, lamb), "a thinner ward pushed in");
            Assert.AreEqual(50, StatusEffects.WardPoints(tank));

            Assert.IsTrue(StatusEffects.ApplyWard(tank.Statuses, 50, 2, lamb), "an equal ward failed to refresh");
            Assert.IsTrue(StatusEffects.ApplyWard(tank.Statuses, 51, 2, lamb));
            Assert.AreEqual(51, StatusEffects.WardPoints(tank));

            Assert.AreEqual(1, tank.Statuses.Count(s => s.Type == StatusEffectType.Shielded),
                "wards stacked instead of replacing");
        }

        // A PARTLY SPENT POOL IS NOT A BIGGER ONE. The replacement rule reads
        // what is LEFT, so a 60 ward with 10 points remaining gives way to a
        // fresh 20 -- which the old Math.Max refresh could not have done,
        // because it kept the magnitude as authored.
        [Test]
        public void TheRuleReadsWhatIsLeftInThePool_NotWhatItStartedAt()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60);
            StatusEffects.ConsumeWard(lamb, 50);
            Assert.AreEqual(10, StatusEffects.WardPoints(lamb));

            Assert.IsTrue(StatusEffects.ApplyWard(lamb.Statuses, 20, 2, lamb));
            Assert.AreEqual(20, StatusEffects.WardPoints(lamb));
        }

        // TWO OF THE WEARER'S OWN TURNS by default, on the ordinary tick.
        [Test]
        public void AWardExpiresOnTheClock()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60, turns: 2);

            StatusEffects.Tick(lamb);
            Assert.AreEqual(60, StatusEffects.WardPoints(lamb), "gone a turn early");

            StatusEffects.Tick(lamb);
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "still standing past its duration");
        }

        // The CASTER's talents decide what a ward does on the way out, not the
        // wearer's. A ward Shawn put on the turtle heals by Shawn's Mending
        // Fleece, and the turtle has no say in it.
        [Test]
        public void MendingFleece_HealsTheWearer_WhenThePoolIsEmptied()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardHealsWhenSpent, 10));

            var tank = Fighter("Turtle", maxHealth: 300);
            tank.CurrentHealth = 100;
            Ward(lamb, tank, 50);

            var outcome = StatusEffects.ConsumeWard(tank, 80);

            Assert.AreEqual(30, outcome.Damage, "80 less the pool's 50");
            Assert.AreEqual(30, outcome.Healed, "A tenth of the WEARER's 300 max health");
            Assert.AreEqual(130, tank.CurrentHealth);
        }

        // AND NOT WHILE THE POOL STILL STANDS. The node pays for the ward
        // BREAKING; a hit the shield shrugged off has not broken it.
        [Test]
        public void MendingFleece_PaysNothingForAHitThePoolSurvived()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardHealsWhenSpent, 10));

            var tank = Fighter("Turtle", maxHealth: 300);
            tank.CurrentHealth = 100;
            Ward(lamb, tank, 50);

            Assert.AreEqual(0, StatusEffects.ConsumeWard(tank, 20).Healed);
            Assert.AreEqual(100, tank.CurrentHealth);
        }

        [Test]
        public void WithoutMendingFleece_AWardBreakingHealsNothing()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");
            tank.CurrentHealth = 100;
            Ward(lamb, tank, 50);

            Assert.AreEqual(0, StatusEffects.ConsumeWard(tank, 80).Healed);

            // Unchanged, because consuming a ward REDUCES A FIGURE and does
            // not apply it -- CombatMath.ApplyDamage is still what touches
            // health, further down the same funnel. Worth pinning: a version
            // of this that also dealt the damage would double every hit that
            // met a ward.
            Assert.AreEqual(100, tank.CurrentHealth);
        }

        // THE GOLDEN FLEECE IS A DURATION, NOT IMMUNITY, and this is the test
        // that changed most with the model. It used to assert the ward did not
        // pop at all -- the only reading "permanent" could have while a ward
        // was one hit's worth of percentage. Against a pool that would be
        // immunity to everything forever, so the capstone buys a clock that
        // never runs out and the pool drains normally.
        [Test]
        public void TheGoldenFleece_StopsTheClock_NotTheDraining()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            Ward(lamb, lamb, 60, turns: StatusEffects.PermanentWardTurns);

            for (int turn = 0; turn < 20; turn++)
            {
                StatusEffects.Tick(lamb);
            }

            Assert.AreEqual(60, StatusEffects.WardPoints(lamb), "a permanent ward aged away");

            var outcome = StatusEffects.ConsumeWard(lamb, 100);
            Assert.AreEqual(40, outcome.Damage);
            Assert.AreSame(lamb, outcome.WardedBy, "it still counts as having been warded, so the engine still pays");
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "a permanent ward is still spent by what empties it");
        }

        // The Magical Shield relic applies the same status with no source.
        // Every Lamb rule has to read that as absent rather than throwing.
        [Test]
        public void AnUnsourcedShield_StillAbsorbsAndBreaks()
        {
            var wearer = Fighter();
            StatusEffects.ApplyWard(wearer.Statuses, 50, 99);

            var outcome = StatusEffects.ConsumeWard(wearer, 80);

            Assert.AreEqual(30, outcome.Damage);
            Assert.IsNull(outcome.WardedBy, "Nobody cast it, so nobody is paid for it");
            Assert.AreEqual(0, outcome.Healed);
            Assert.IsFalse(StatusEffects.IsWarded(wearer));
        }

        // Non-positive damage does not touch the pool: nothing hit the shield,
        // so there is nothing to spend on it.
        [Test]
        public void AZeroHit_SpendsNothing()
        {
            var wearer = Fighter();
            StatusEffects.ApplyWard(wearer.Statuses, 50, 2);

            Assert.AreEqual(0, StatusEffects.ConsumeWard(wearer, 0).Damage);
            Assert.AreEqual(50, StatusEffects.WardPoints(wearer));
        }

        // THE GENERIC APPLY REFUSES THIS STATUS OUTRIGHT -- tier 1 of
        // CODE_STANDARDS section 9, the API refusing to express the mistake.
        // Apply's merge keeps the larger magnitude and the longer duration,
        // which against a partly-spent pool hands back points a hit already
        // ate, and it reports nothing to a caller that owes the player a line.
        [Test]
        public void TheGenericApply_WillNotPutUpAWard()
        {
            var wearer = Fighter();

            Assert.Throws<System.ArgumentException>(() =>
                StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 50, 2));
        }

        [Test]
        public void ApplyWard_RefusesANonPositivePool()
        {
            var wearer = Fighter();

            Assert.IsFalse(StatusEffects.ApplyWard(wearer.Statuses, 0, 2));
            Assert.IsFalse(StatusEffects.ApplyWard(wearer.Statuses, -5, 2));
            Assert.IsFalse(StatusEffects.IsWarded(wearer));
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

        [Test]
        public void WardPoints_IsZeroForAnUnwardedCombatant()
        {
            Assert.AreEqual(0, StatusEffects.WardPoints(Fighter()));
            Assert.AreEqual(0, StatusEffects.WardPoints(null));
        }
    }

    // Gift: Fury — the exact mirror of a Ward. Spent by the swing rather than
    // by the clock.
    public class EmpoweredTests
    {
        private static CombatantState Fighter()
        {
            return new CombatantState("Owl", true, 100, 10, 10, 8);
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
        // this is where "does the multiplier reach damage at all" is answered.
        //
        // It used to also answer "before defense or after". That question has
        // stopped existing a second time over: mitigation moved entirely out
        // of CombatMath and into DamagePipeline (Phase 1 of the balance
        // redesign), so ComputeAttackDamage no longer reads the target AT
        // ALL any more -- the bonus folds into the attack side, is scaled,
        // and that is the whole function. See CombatMathTests.
        // ComputeAttackDamage_IsIndependentOfTheTargetsArmour for the
        // property this now is.
        [Test]
        public void TheAttackBonus_MultipliesTheDamageThatLands()
        {
            var attacker = new CombatantState("Shawn", true, 200, 30, 10, 8);
            var target = new CombatantState("Golem", false, 300, 0, 5, 4);

            // 10 attack, raw, with no bonus.
            Assert.AreEqual(10, CombatMath.ComputeAttackDamage(attacker, target));

            attacker.BonusAttackPercent = 100;

            // Doubling the attack to 20, folded in before the (now absent)
            // scale, same place weapon scaling lands: exactly double, since
            // nothing sits between the bonus and the raw figure any more.
            Assert.AreEqual(20, CombatMath.ComputeAttackDamage(attacker, target));
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
