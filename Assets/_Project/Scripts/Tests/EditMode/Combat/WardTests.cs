using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // The Ward -- the one construct the Fragile Lamb's whole path is built on,
    // and since 2026-09-16 (AUDIT #152, the owner's call) a SHIELD: a pool of
    // points that damage is taken out of, not a percentage off one blow.
    //
    // WARDS STACK, decided the same day, once the one-ward-per-character rule
    // had been built and read back. A character carries several entries; the
    // total is the sum; damage drains the entry that expires soonest first.
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

        private static ActiveStatus Ward(CombatantState caster, CombatantState wearer, int points, int turns = 1) =>
            StatusEffects.ApplyWard(wearer.Statuses, points, turns, caster);

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

        // ---- stacking, and the order damage drains it ----------------------

        // WARDS ADD UP. Nothing replaces, refreshes or refuses anything: three
        // wards on one character are three entries and one total.
        [Test]
        public void EveryWardIsItsOwnEntry_AndThePointsAddUp()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 50);
            Ward(lamb, tank, 10);
            Ward(lamb, tank, 20);

            Assert.AreEqual(3, tank.Statuses.Count(s => s.Type == StatusEffectType.Shielded));
            Assert.AreEqual(80, StatusEffects.WardPoints(tank));
        }

        // A THIN WARD OVER A THICK ONE IS NOT REFUSED. This is the case the
        // rule that shipped for a few hours got wrong: The Flock's
        // half-strength share bounced off anybody already covered, and a free
        // relic ward was a coin flip against whatever the player had spent a
        // turn on.
        [Test]
        public void AThinnerWardOverAThickerOne_Lands()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 50);
            Ward(lamb, tank, 5);

            Assert.AreEqual(55, StatusEffects.WardPoints(tank));
        }

        // SOONEST TO LAPSE IS SPENT FIRST, which is the only order that does
        // not waste shield -- the pool that was about to go anyway pays, and
        // the durable one is still there for the hit after this.
        [Test]
        public void DamageDrainsTheEntryThatExpiresSoonestFirst()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 30, turns: 3);
            Ward(lamb, tank, 20, turns: 1);

            var outcome = StatusEffects.ConsumeWard(tank, 20);

            Assert.AreEqual(0, outcome.Damage);
            Assert.AreEqual(30, StatusEffects.WardPoints(tank), "the three-turn pool paid instead of the one-turn one");
            Assert.AreEqual(3, StatusEffects.WardsInDrainOrder(tank).Single().TurnsRemaining);
        }

        // AND THEN THE NEXT ONE. A hit bigger than the head of the order
        // reaches through it, and everything after it, before it reaches
        // health.
        [Test]
        public void AHitReachesThroughOnePoolIntoTheNext_AndThenIntoHealth()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 30, turns: 3);
            Ward(lamb, tank, 20, turns: 1);

            var outcome = StatusEffects.ConsumeWard(tank, 60);

            Assert.AreEqual(10, outcome.Damage, "60 less 20 less 30");
            Assert.AreEqual(50, outcome.Absorbed);
            Assert.AreEqual(2, outcome.Hits.Count);
            Assert.AreEqual(20, outcome.Hits[0].Absorbed, "the one-turn pool went first");
            Assert.AreEqual(30, outcome.Hits[1].Absorbed);
            Assert.IsFalse(StatusEffects.IsWarded(tank));
        }

        // TIES BREAK BY AGE, oldest first, so the order is total and two reads
        // of the same board can never disagree.
        [Test]
        public void TwoWardsWithTheSameClock_DrainOldestFirst()
        {
            var lamb = Fighter();
            var owl = Fighter("Odette");
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 20, turns: 2);
            Ward(owl, tank, 20, turns: 2);

            var outcome = StatusEffects.ConsumeWard(tank, 20);

            Assert.AreEqual(1, outcome.Hits.Count);
            Assert.AreSame(lamb, outcome.Hits[0].Caster, "the newer ward paid first");
            Assert.AreSame(owl, StatusEffects.WardsInDrainOrder(tank).Single().Source);
        }

        // A WARD THAT NEVER EXPIRES SORTS LAST however few turns its counter
        // happens to say -- it is not going anywhere, so it is the one worth
        // keeping.
        [Test]
        public void APermanentWardIsSpentAfterEveryWardOnAClock()
        {
            var shawn = Fighter();
            Give(shawn, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            var owl = Fighter("Odette");
            var tank = Fighter("Turtle");

            Ward(shawn, tank, 20, turns: 1);
            Ward(owl, tank, 20, turns: 5);

            var outcome = StatusEffects.ConsumeWard(tank, 20);

            Assert.AreSame(owl, outcome.Hits[0].Caster,
                "the five-turn ward paid before the permanent one-turn one");
            Assert.AreSame(shawn, StatusEffects.WardsInDrainOrder(tank).Single().Source);
        }

        // ---- the clock -----------------------------------------------------
        //
        // A WARD IS THE ONE DURATION IN THE GAME THAT RUNS AT THE END OF THE
        // WEARER'S TURN rather than at its start, and the turn it was raised
        // on does not count (owner's answer to AUDIT #153, 2026-09-16). That
        // is a turn of visibility: at turn start it would be gone before the
        // player got the turn back.

        [Test]
        public void TheTurnStartTickDoesNotTouchAWard()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60, turns: 1);

            StatusEffects.Tick(lamb);
            StatusEffects.Tick(lamb);
            StatusEffects.Tick(lamb);

            Assert.AreEqual(60, StatusEffects.WardPoints(lamb),
                "a ward's clock runs at the END of the turn -- Tick is the wrong end");
        }

        // THE TURN IT WENT UP DOES NOT COUNT. Handed to the end-of-turn tick
        // as a raised-this-turn entry, a one-turn ward survives that turn's
        // end and goes at the end of the next one.
        [Test]
        public void AWardRaisedThisTurn_SurvivesThisTurnsEnd_AndGoesAtTheNextOne()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60, turns: 1);
            var raised = new List<ActiveStatus>(lamb.Statuses);

            Assert.AreEqual(0, StatusEffects.TickAtTurnEnd(lamb, raised).Count, "the turn it went up counted");
            Assert.AreEqual(60, StatusEffects.WardPoints(lamb));

            Assert.AreEqual(1, StatusEffects.TickAtTurnEnd(lamb).Count);
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "still standing past its one turn");
        }

        // N TURNS IS N OF THE WEARER'S NEXT TURNS, the same spelling a
        // cooldown uses.
        [Test]
        public void ALongerWardOutlastsTheTurnsItWasGiven()
        {
            var lamb = Fighter();
            Ward(lamb, lamb, 60, turns: 3);

            StatusEffects.TickAtTurnEnd(lamb);
            StatusEffects.TickAtTurnEnd(lamb);
            Assert.AreEqual(60, StatusEffects.WardPoints(lamb), "gone a turn early");

            StatusEffects.TickAtTurnEnd(lamb);
            Assert.IsFalse(StatusEffects.IsWarded(lamb));
        }

        // ---- The Golden Fleece belongs to the CASTER ------------------------
        //
        // Three cases, owner 2026-09-16, and the middle one is the whole
        // reason it is read off the ward's Source rather than off whoever is
        // wearing it.

        [Test]
        public void TheGoldenFleece_KeepsTheCastersOwnWardStanding()
        {
            var shawn = Fighter();
            Give(shawn, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            Ward(shawn, shawn, 60, turns: 1);

            for (int turn = 0; turn < 20; turn++)
            {
                StatusEffects.TickAtTurnEnd(shawn);
            }

            Assert.AreEqual(60, StatusEffects.WardPoints(shawn), "his own ward aged away");
        }

        [Test]
        public void TheGoldenFleece_KeepsAWardHeSpreadOntoSomebodyElseStanding()
        {
            var shawn = Fighter();
            Give(shawn, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            var ally = Fighter("Bjorn");
            Ward(shawn, ally, 30, turns: 1);

            for (int turn = 0; turn < 20; turn++)
            {
                StatusEffects.TickAtTurnEnd(ally);
            }

            Assert.AreEqual(30, StatusEffects.WardPoints(ally),
                "a Flock share is his work and holds the capstone, whatever the wearer bought");
        }

        [Test]
        public void TheGoldenFleece_DoesNothingForAWardSomebodyElsePutOnHim()
        {
            var shawn = Fighter();
            Give(shawn, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            var odette = Fighter("Odette");
            Ward(odette, shawn, 30, turns: 1);

            StatusEffects.TickAtTurnEnd(shawn);

            Assert.IsFalse(StatusEffects.IsWarded(shawn),
                "the capstone is the caster's, and Odette does not hold it");
        }

        // AND IT IS NOT IMMUNITY. The clock stops; the pool still drains, and
        // a hit that empties it takes the ward with it.
        [Test]
        public void TheGoldenFleece_StopsTheClock_NotTheDraining()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            Ward(lamb, lamb, 60);

            var outcome = StatusEffects.ConsumeWard(lamb, 100);

            Assert.AreEqual(40, outcome.Damage);
            Assert.AreSame(lamb, outcome.WardedBy, "it still counts as having been warded, so the engine still pays");
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "a permanent ward is still spent by what empties it");
        }

        // ---- who a ward belongs to -----------------------------------------

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

        // EACH ENTRY HEALS BY ITS OWN CASTER'S TALENT. A hit that breaks
        // Shawn's ward and a relic's in one blow pays for Shawn's and not for
        // the relic's.
        [Test]
        public void AHitThroughTwoWards_HealsOnlyForTheOneWhoseCasterHasTheNode()
        {
            var lamb = Fighter();
            Give(lamb, new TalentEffect(TalentEffectType.WardHealsWhenSpent, 10));

            var tank = Fighter("Turtle", maxHealth: 300);
            tank.CurrentHealth = 100;

            StatusEffects.ApplyWard(tank.Statuses, 10, 1);       // unsourced relic ward, goes first
            Ward(lamb, tank, 10, turns: 2);

            var outcome = StatusEffects.ConsumeWard(tank, 40);

            Assert.AreEqual(20, outcome.Damage, "40 less 10 less 10");
            Assert.AreEqual(2, outcome.Hits.Count);
            Assert.AreEqual(0, outcome.Hits[0].Healed, "nobody cast the relic ward, so nobody heals for it");
            Assert.AreEqual(30, outcome.Hits[1].Healed, "a tenth of the wearer's 300");
            Assert.AreEqual(30, outcome.Healed, "and the total is the sum");
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

        // IsWardedBy IS THE PRECISE QUESTION, and IsWarded/WardedBy are the
        // loose ones. With wards stacking, "who has warded this combatant" has
        // no single answer, so everything that has to be right about a
        // PARTICULAR caster -- Weight of Wool, Shatter's eligibility, the
        // self-ward grace period -- asks this instead.
        [Test]
        public void IsWardedBy_AnswersForOneCasterRatherThanForAnybody()
        {
            var lamb = Fighter();
            var odette = Fighter("Odette");
            var tank = Fighter("Turtle");

            StatusEffects.ApplyWard(tank.Statuses, 10, 1);      // unsourced, at the head of the order
            Ward(lamb, tank, 40, turns: 2);

            Assert.IsTrue(StatusEffects.IsWardedBy(tank, lamb));
            Assert.IsFalse(StatusEffects.IsWardedBy(tank, odette));
            Assert.AreSame(lamb, StatusEffects.WardedBy(tank), "the first entry with a source at all");
        }

        // Non-positive damage does not touch the pool: nothing hit the shield,
        // so there is nothing to spend on it.
        [Test]
        public void AZeroHit_SpendsNothing()
        {
            var wearer = Fighter();
            StatusEffects.ApplyWard(wearer.Statuses, 50, 1);

            Assert.AreEqual(0, StatusEffects.ConsumeWard(wearer, 0).Damage);
            Assert.AreEqual(50, StatusEffects.WardPoints(wearer));
        }

        // THE GENERIC APPLY REFUSES THIS STATUS OUTRIGHT -- tier 1 of
        // CODE_STANDARDS section 9, the API refusing to express the mistake.
        // Apply MERGES a second application into the first, which is the exact
        // opposite of what a ward does, and it would silently turn two
        // 20-point shields into one.
        [Test]
        public void TheGenericApply_WillNotPutUpAWard()
        {
            var wearer = Fighter();

            Assert.Throws<System.ArgumentException>(() =>
                StatusEffects.Apply(wearer.Statuses, StatusEffectType.Shielded, 50, 1));
        }

        [Test]
        public void ApplyWard_RefusesANonPositivePool()
        {
            var wearer = Fighter();

            Assert.IsNull(StatusEffects.ApplyWard(wearer.Statuses, 0, 1));
            Assert.IsNull(StatusEffects.ApplyWard(wearer.Statuses, -5, 1));

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

        // ---- the summary the HUD draws -------------------------------------

        [Test]
        public void SummariseWards_TotalsThePointsAndNamesTheSoonestClock()
        {
            var lamb = Fighter();
            var tank = Fighter("Turtle");

            Ward(lamb, tank, 30, turns: 4);
            Ward(lamb, tank, 20, turns: 2);

            var summary = StatusEffects.SummariseWards(tank);

            Assert.AreEqual(50, summary.Points);
            Assert.AreEqual(2, summary.Entries);
            Assert.AreEqual(2, summary.SoonestTurns);
            Assert.IsFalse(summary.AllPermanent);
        }

        [Test]
        public void SummariseWards_SaysSoWhenNothingIsOnAClock()
        {
            var shawn = Fighter();
            Give(shawn, new TalentEffect(TalentEffectType.WardsNeverExpire, 0));
            Ward(shawn, shawn, 30, turns: 1);

            var summary = StatusEffects.SummariseWards(shawn);

            Assert.IsTrue(summary.AllPermanent);
            Assert.AreEqual(0, summary.SoonestTurns, "0 here means nothing is counting down, not that it goes now");
        }

        [Test]
        public void SummariseWards_IsEmptyForAnUnwardedCombatant()
        {
            Assert.IsFalse(StatusEffects.SummariseWards(Fighter()).Any);
            Assert.IsFalse(StatusEffects.SummariseWards(null).Any);
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
