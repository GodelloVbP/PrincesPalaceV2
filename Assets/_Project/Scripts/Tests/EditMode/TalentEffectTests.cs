using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The talent effect vocabulary, at the Domain layer: how a set of rules
    // resolves, and what each rule does to a damage figure.
    //
    // Deliberately engine-free and away from FightController. Every passive
    // half of the Black Ram lives in CombatMath, in the funnels every damage
    // path already reads through, which is exactly what makes it testable
    // without a fight — and the reason it was put there rather than at the
    // call sites.
    public class TalentEffectSetTests
    {
        private static TalentEffectSet Set(params TalentEffect[] effects)
        {
            return new TalentEffectSet(effects);
        }

        [Test]
        public void AnEmptySet_AnswersZeroAndFalseForEverything()
        {
            var empty = TalentEffectSet.Empty;

            Assert.IsTrue(empty.IsEmpty);
            Assert.IsFalse(empty.Has(TalentEffectType.CheatDeathOncePerFight));
            Assert.AreEqual(0, empty.Best(TalentEffectType.IgnoreDefensePercent));
            Assert.AreEqual(0, empty.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth,
                new CombatantState("nobody", true, 10, 0, 1, 0, 1)));
        }

        // The single most load-bearing rule in the file. A strand is one idea
        // developed three times and its prereq chain means owning T2
        // necessarily means owning T1 — so Sharp Horns' 25% and 50% are BOTH
        // in the set at once. Summing them gives 75%, which is neither tier
        // and gets worse the longer a strand runs.
        [Test]
        public void RepeatedRules_TakeTheStrongest_NotTheSum()
        {
            var horns = Set(
                new TalentEffect(TalentEffectType.IgnoreDefensePercent, 25),
                new TalentEffect(TalentEffectType.IgnoreDefensePercent, 50));

            Assert.AreEqual(50, horns.Best(TalentEffectType.IgnoreDefensePercent));
        }

        // The handoff (§6.1) asked "do the HP tiers stack, or replace?" and
        // never answered. Replace: a Ram at 30% health satisfies both his 67%
        // tier (worth 2) and his 33% tier (worth 3), and the answer is 3.
        // Stacking gives 5 and pushes the ceiling past what the 7-wool
        // transform was priced against.
        [Test]
        public void TieredHealthGates_ReplaceRatherThanStack()
        {
            var engine = Set(
                new TalentEffect(TalentEffectType.WoolPerTurnBelowHealth, 2, 67),
                new TalentEffect(TalentEffectType.WoolPerTurnBelowHealth, 3, 33));
            var ram = new CombatantState("Shawn", true, 100, 10, 5, 2, 8);

            Assert.AreEqual(0, engine.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, ram),
                "At full health neither tier is satisfied, so the baseline stands alone");

            ram.CurrentHealth = 50;
            Assert.AreEqual(2, engine.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, ram),
                "Half health satisfies the 67% tier only");

            ram.CurrentHealth = 30;
            Assert.AreEqual(3, engine.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, ram),
                "Thirty percent satisfies both tiers, and the answer is the stronger one, not their sum");
        }

        // Exactly AT the threshold counts. "Below 33%" in the design means
        // "at or below" everywhere else in this codebase
        // (IsBelowHealthFraction, BreakShield), and a rule that switched on
        // one point of health later than the number printed on the node
        // would read as a bug.
        [Test]
        public void AHealthGate_IsSatisfiedExactlyAtItsThreshold()
        {
            var engine = Set(new TalentEffect(TalentEffectType.WoolPerTurnBelowHealth, 2, 67));
            var ram = new CombatantState("Shawn", true, 100, 10, 5, 2, 8) { CurrentHealth = 67 };

            Assert.AreEqual(2, engine.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, ram),
                "Exactly at the gate counts — the float form of this comparison used to miss it");
        }

        [Test]
        public void FlagRules_AreAskedWithHasRatherThanRead()
        {
            var lastStand = Set(new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0));

            Assert.IsTrue(lastStand.Has(TalentEffectType.CheatDeathOncePerFight));
            Assert.IsFalse(lastStand.Has(TalentEffectType.ProvokeHitsEveryEnemy));
        }

        [Test]
        public void IsBelowThreshold_ReadsTheHoldersOwnHealth()
        {
            var wrath = Set(new TalentEffect(TalentEffectType.TransformHoldsBelowHealth, 0, 33));
            var ram = new CombatantState("Shawn", true, 100, 10, 5, 2, 8);

            Assert.IsFalse(wrath.IsBelowThreshold(TalentEffectType.TransformHoldsBelowHealth, ram));

            ram.CurrentHealth = 33;
            Assert.IsTrue(wrath.IsBelowThreshold(TalentEffectType.TransformHoldsBelowHealth, ram));
        }
    }

    // What each rule actually does to a number, through the real formulas.
    public class TalentCombatRuleTests
    {
        private static CombatantState Fighter(int maxHealth = 100, int attack = 10, int defense = 4)
        {
            return new CombatantState("Fighter", true, maxHealth, 10, attack, defense, 8);
        }

        private static void Give(CombatantState combatant, params TalentEffect[] effects)
        {
            combatant.Talents = new TalentEffectSet(effects);
        }

        [Test]
        public void SharpHorns_IgnoresItsShareOfTheTargetsDefense()
        {
            var attacker = Fighter();
            var target = Fighter(defense: 8);

            Assert.AreEqual(8, CombatMath.EffectiveDefense(target, attacker), "No talent, no penetration");

            Give(attacker, new TalentEffect(TalentEffectType.IgnoreDefensePercent, 50));

            Assert.AreEqual(4, CombatMath.EffectiveDefense(target, attacker));
        }

        // Penetration and the low-health armour bonus have to compose in a
        // stated order, or the two strands race. Sharp Horns ignores a
        // fraction of the armour ACTUALLY IN THE WAY, which means the bonus
        // is applied first.
        [Test]
        public void Penetration_AppliesToTheArmourThatIsActuallyThere()
        {
            var attacker = Fighter();
            Give(attacker, new TalentEffect(TalentEffectType.IgnoreDefensePercent, 50));

            var target = Fighter(defense: 10);
            Give(target, new TalentEffect(TalentEffectType.DefenseBonusPercentBelowHealth, 50, 50));
            target.CurrentHealth = 40;

            Assert.AreEqual(15, CombatMath.EffectiveDefense(target),
                "Last Stand should have raised 10 to 15 while he is under half");
            Assert.AreEqual(8, CombatMath.EffectiveDefense(target, attacker),
                "And Sharp Horns should ignore half of the 15 that is in the way (integer-floored to 7), not half of the base 10");
        }

        [Test]
        public void LastStand_AddsNoArmourWhileTheHolderIsHealthy()
        {
            var target = Fighter(defense: 10);
            Give(target, new TalentEffect(TalentEffectType.DefenseBonusPercentBelowHealth, 25, 50));

            Assert.AreEqual(10, CombatMath.EffectiveDefense(target));
        }

        [Test]
        public void Trample_AddsDamageOnlyAgainstAWeakenedTarget()
        {
            var attacker = Fighter();
            Give(attacker, new TalentEffect(TalentEffectType.ExecuteDamageBonusPercent, 25, 30));

            var healthy = Fighter();
            Assert.AreEqual(100, CombatMath.ApplyExecuteBonus(100, attacker, healthy));

            var dying = Fighter();
            dying.CurrentHealth = 20;
            Assert.AreEqual(125, CombatMath.ApplyExecuteBonus(100, attacker, dying));
        }

        // Stands in for the handoff's "cannot be critically hit", which has
        // nothing to hook onto in a game with no crits — see
        // TalentEffectType.DamageCapPercentBelowHealth. The intent is that
        // the spike which ends the run cannot happen.
        [Test]
        public void Unflinching_CapsASingleHitOnceTheHolderIsBadlyWounded()
        {
            var target = Fighter(maxHealth: 200);
            Give(target, new TalentEffect(TalentEffectType.DamageCapPercentBelowHealth, 25, 25));

            target.CurrentHealth = 150;
            Assert.AreEqual(180, CombatMath.CapSpikeDamage(180, target), "Above the gate, nothing is capped");

            target.CurrentHealth = 40;
            Assert.AreEqual(50, CombatMath.CapSpikeDamage(180, target), "Under it, no hit exceeds a quarter of 200");
            Assert.AreEqual(30, CombatMath.CapSpikeDamage(30, target), "A small hit is not raised to the cap");
        }

        [Test]
        public void NotYet_LeavesTheHolderOnOneHealth_ExactlyOncePerFight()
        {
            var ram = Fighter(maxHealth: 100);
            Give(ram, new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0));
            ram.CurrentHealth = 30;

            var first = CombatMath.ApplyDamageDetailed(ram, 500);

            Assert.IsTrue(first.CheatedDeath);
            Assert.AreEqual(1, ram.CurrentHealth);
            Assert.IsTrue(ram.IsAlive);

            var second = CombatMath.ApplyDamageDetailed(ram, 500);

            Assert.IsFalse(second.CheatedDeath, "It is once a fight, not once a hit");
            Assert.AreEqual(0, ram.CurrentHealth);
            Assert.IsFalse(ram.IsAlive);
        }

        // A hit that would NOT have been lethal must not burn the save. The
        // whole value of a once-per-fight net is that it is still there when
        // the fight turns.
        [Test]
        public void NotYet_IsNotSpentOnASurvivableHit()
        {
            var ram = Fighter(maxHealth: 100);
            Give(ram, new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0));

            var result = CombatMath.ApplyDamageDetailed(ram, 30);

            Assert.IsFalse(result.CheatedDeath);
            Assert.IsFalse(ram.CheatDeathSpent);
            Assert.AreEqual(70, ram.CurrentHealth);
        }

        // The two Last Stand tiers meet inside ApplyDamage: the cap runs
        // first, so a blow that WOULD have been lethal can be reduced to one
        // that is survivable without the death save being touched at all.
        [Test]
        public void TheSpikeCap_RunsBeforeTheDeathSave()
        {
            var ram = Fighter(maxHealth: 200);
            Give(ram,
                new TalentEffect(TalentEffectType.DamageCapPercentBelowHealth, 25, 50),
                new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0));
            ram.CurrentHealth = 60;

            var result = CombatMath.ApplyDamageDetailed(ram, 1000);

            Assert.IsFalse(result.CheatedDeath, "The cap should have made the hit survivable on its own");
            Assert.AreEqual(10, ram.CurrentHealth, "Sixty less the capped fifty");
            Assert.IsFalse(ram.CheatDeathSpent, "And the once-per-fight save should still be in hand");
        }

        [Test]
        public void ACombatantWithNoTalents_IsUntouchedByAnyOfIt()
        {
            var plain = Fighter(maxHealth: 100, defense: 6);
            var attacker = Fighter();
            plain.CurrentHealth = 5;

            Assert.AreEqual(6, CombatMath.EffectiveDefense(plain, attacker));
            Assert.AreEqual(50, CombatMath.ApplyExecuteBonus(50, attacker, plain));
            Assert.AreEqual(999, CombatMath.CapSpikeDamage(999, plain));

            CombatMath.ApplyDamage(plain, 999);
            Assert.IsFalse(plain.IsAlive, "No death save, so the hit lands as it always did");
        }
    }

    // Black Ram Mode's runtime half: what entering grants and what leaving
    // has to hand back.
    public class TalentTransformTests
    {
        private static CombatantState Ram()
        {
            return new CombatantState("Shawn", true, 200, 10, 10, 4, 10);
        }

        [Test]
        public void Entering_GrantsAttackSpeedAndTemporaryHealth()
        {
            var ram = Ram();

            Transformation.Enter(ram, "the Black Ram", 3, 50, 30, 25);

            Assert.AreEqual(15, ram.Attack);
            Assert.AreEqual(13, ram.Speed);
            Assert.AreEqual(250, ram.MaxHealth);
            Assert.AreEqual(250, ram.CurrentHealth, "The temporary pool arrives filled");
        }

        // Exact reversal is why Transformation stores the DELTAS rather than
        // the percentages: 10 x 1.5 is 15, and 15 / 1.5 is 10 only because
        // these numbers happen to divide. Attack 7 would not round-trip.
        [Test]
        public void Leaving_HandsBackExactlyWhatItTook_EvenWhenThePercentDoesNotDivide()
        {
            var ram = Ram();
            ram.Attack = 7;
            ram.Speed = 9;

            Transformation.Enter(ram, "the Black Ram", 3, 50, 30, 25);
            Transformation.Exit(ram);

            Assert.AreEqual(7, ram.Attack);
            Assert.AreEqual(9, ram.Speed);
            Assert.AreEqual(200, ram.MaxHealth);
            Assert.IsNull(ram.Transformation);
        }

        // The temporary pool behaves like temporary health should: spent
        // absorbing hits it is already gone, unspent it evaporates. Either
        // way he lands back in the wounded band his engine runs hottest in,
        // never below where the fight actually left him.
        [Test]
        public void UnspentTemporaryHealth_EvaporatesRatherThanBeingRefunded()
        {
            var ram = Ram();

            Transformation.Enter(ram, "the Black Ram", 3, 0, 0, 25);
            Assert.AreEqual(250, ram.CurrentHealth);
            Transformation.Exit(ram);

            Assert.AreEqual(200, ram.CurrentHealth, "The unspent fifty goes with the transform");
        }

        [Test]
        public void TemporaryHealthAlreadySpent_IsNotDeductedTwiceOnTheWayOut()
        {
            var ram = Ram();

            Transformation.Enter(ram, "the Black Ram", 3, 0, 0, 25);
            CombatMath.ApplyDamage(ram, 120);
            Assert.AreEqual(130, ram.CurrentHealth);

            Transformation.Exit(ram);

            Assert.AreEqual(130, ram.CurrentHealth, "He keeps what the fight actually left him");
            Assert.AreEqual(200, ram.MaxHealth);
        }
    }

    // Provoke rides the status list, which is what makes it visible,
    // clearable and attributable through machinery that already exists.
    public class ProvokeStatusTests
    {
        private static CombatantState Combatant(string name)
        {
            return new CombatantState(name, false, 100, 10, 5, 2, 8);
        }

        [Test]
        public void ProvokedBy_ReportsWhoLandedTheTaunt()
        {
            var ram = Combatant("Shawn");
            var goblin = Combatant("Goblin");

            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Provoked, 20, 1, ram);

            Assert.AreSame(ram, StatusEffects.ProvokedBy(goblin));
        }

        // A dead provoker holds nothing: an enemy locked onto a corpse would
        // spend its turn attacking something that is not there.
        [Test]
        public void ADeadProvoker_HoldsNoTaunt()
        {
            var ram = Combatant("Shawn");
            var goblin = Combatant("Goblin");
            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Provoked, 20, 1, ram);

            ram.CurrentHealth = 0;

            Assert.IsNull(StatusEffects.ProvokedBy(goblin));
        }

        // The reduction is a property of the PAIR, not of the target: a
        // goaded enemy hits the ram softly and everyone else exactly as hard
        // as before.
        [Test]
        public void TheDamageReduction_AppliesOnlyToWhoeverDidTheProvoking()
        {
            var ram = Combatant("Shawn");
            var bystander = Combatant("Owl");
            var goblin = Combatant("Goblin");

            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Provoked, 20, 1, ram);

            Assert.AreEqual(0.8f, StatusEffects.ProvokedDamageMultiplier(goblin, ram), 0.0001f);
            Assert.AreEqual(1f, StatusEffects.ProvokedDamageMultiplier(goblin, bystander), 0.0001f);
        }

        // Spent by the turn it redirects, not counted down by it. This tick
        // runs at the START of the holder's turn, before the action it is
        // meant to redirect resolves — so a Provoke that decremented here
        // would expire a beat before it could do anything.
        [Test]
        public void ATurnStartTick_DoesNotExpireATaunt()
        {
            var ram = Combatant("Shawn");
            var goblin = Combatant("Goblin");
            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Provoked, 20, 1, ram);

            StatusEffects.Tick(goblin);

            Assert.AreSame(ram, StatusEffects.ProvokedBy(goblin), "The taunt has to survive its own turn's tick");

            StatusEffects.ConsumeProvoke(goblin.Statuses);
            Assert.IsNull(StatusEffects.ProvokedBy(goblin), "And be spent by the turn it redirected");
        }

        // Both non-Ram engines are paid for their own SETUP, so who applied a
        // status has to be answerable. Without it the Lamb would be paid for
        // standing near a healer.
        [Test]
        public void CountAfflictedBy_CreditsOnlyTheSourceThatEarnedIt()
        {
            var lamb = Combatant("Shawn");
            var somebodyElse = Combatant("Owl");
            var tank = Combatant("Turtle");
            var untouched = Combatant("Dog");

            StatusEffects.Apply(tank.Statuses, StatusEffectType.Protect, 20, 3, lamb);
            StatusEffects.Apply(untouched.Statuses, StatusEffectType.Regen, 5, 3, somebodyElse);

            var party = new List<CombatantState> { tank, untouched };

            Assert.AreEqual(1, StatusEffects.CountAfflictedBy(party, lamb));
            Assert.AreEqual(1, StatusEffects.CountAfflictedBy(party, somebodyElse));
            Assert.AreEqual(0, StatusEffects.CountAfflictedBy(party, null));
        }

        // A refresh from nobody in particular must not disown a status the
        // mage's engine is being paid for — the income would just stop, and
        // nothing about it would look wrong.
        [Test]
        public void ASourcelessRefresh_DoesNotClearTheExistingCredit()
        {
            var mage = Combatant("Shawn");
            var goblin = Combatant("Goblin");

            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Poison, 5, 3, mage);
            StatusEffects.Apply(goblin.Statuses, StatusEffectType.Poison, 7, 3);

            Assert.AreEqual(1, StatusEffects.CountAfflictedBy(new[] { goblin }, mage));
        }
    }

    // Headbutt's shove, at the scheduler.
    public class TalentQueuePushTests
    {
        [Test]
        public void PushBack_SendsAnActorBehindWhoeverWasNextBelowThem()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("fast", 30);
            order.AddCombatant("middle", 20);
            order.AddCombatant("slow", 10);
            order.Start();

            // "fast" opened and spent its charge, so the contest is now
            // between middle and slow. Shoving middle back a slot should hand
            // the next turn to slow instead.
            Assert.IsTrue(order.PushBack("middle", 1));

            Assert.AreEqual("slow", order.Advance());
        }

        [Test]
        public void PushBack_RefusesAnActorThatIsNotInTheOrder()
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("only", 10);
            order.Start();

            Assert.IsFalse(order.PushBack("ghost", 1));
        }
    }

    // talents.json's new payload, at the resolver — the boundary where an
    // authoring slip should become a named build failure rather than a
    // number sitting quietly in an asset doing nothing.
    public class TalentEffectResolverTests
    {
        private static RawTalentEntry Entry(params RawTalentEffect[] effects)
        {
            return new RawTalentEntry
            {
                id = "test_node",
                displayName = "Test Node",
                characterId = "sheep",
                column = 0,
                row = 1,
                effects = effects,
            };
        }

        private static bool Resolve(RawTalentEntry entry, out List<string> errors)
        {
            return TalentEntryResolver.TryResolveAll(new[] { entry }, out _, out errors);
        }

        private static string Error(RawTalentEntry entry)
        {
            Assert.IsFalse(Resolve(entry, out var errors), "This entry was expected to be rejected");
            return string.Join(" ", errors);
        }

        [Test]
        public void AnEffectName_IsParsedFromTheEnumMemberName()
        {
            var entry = Entry(new RawTalentEffect { type = "IgnoreDefensePercent", magnitude = 25 });

            Assert.IsTrue(TalentEntryResolver.TryResolveAll(new[] { entry }, out var resolved, out _));
            Assert.AreEqual(TalentEffectType.IgnoreDefensePercent, resolved[0].Effects[0].Type);
            Assert.AreEqual(25, resolved[0].Effects[0].Magnitude);
        }

        [Test]
        public void AnUnknownEffectName_IsNamedRatherThanIgnored()
        {
            StringAssert.Contains("SharpenTheHorns", Error(Entry(new RawTalentEffect { type = "SharpenTheHorns", magnitude = 1 })));
        }

        [Test]
        public void NoneIsNotAuthorable()
        {
            StringAssert.Contains("None", Error(Entry(new RawTalentEffect { type = "None" })));
        }

        [Test]
        public void AThresholdOnARuleWithNoHealthGate_IsRejected()
        {
            StringAssert.Contains("threshold",
                Error(Entry(new RawTalentEffect { type = "IgnoreDefensePercent", magnitude = 25, threshold = 50 })));
        }

        [Test]
        public void AHealthGatedRule_NeedsAThresholdInRange()
        {
            StringAssert.Contains("threshold",
                Error(Entry(new RawTalentEffect { type = "WoolPerTurnBelowHealth", magnitude = 2 })));
            StringAssert.Contains("threshold",
                Error(Entry(new RawTalentEffect { type = "WoolPerTurnBelowHealth", magnitude = 2, threshold = 140 })));
        }

        [Test]
        public void AMagnitudeOnAFlagRule_IsRejected()
        {
            StringAssert.Contains("flag",
                Error(Entry(new RawTalentEffect { type = "CheatDeathOncePerFight", magnitude = 3 })));
        }

        [Test]
        public void ARuleThatReadsAMagnitude_NeedsAPositiveOne()
        {
            StringAssert.Contains("magnitude",
                Error(Entry(new RawTalentEffect { type = "IgnoreDefensePercent" })));
        }

        // TalentEffectSet takes the strongest of a type, so a second entry on
        // ONE node is dead weight — except for the health-gated rules, where
        // two tiers on one node is the whole point (the Black Ram's root
        // carries both its 67% and its 33% wool tiers).
        [Test]
        public void TwoEntriesOfOneUngatedRule_AreRejectedAsDeadWeight()
        {
            StringAssert.Contains("listed 2 times", Error(Entry(
                new RawTalentEffect { type = "IgnoreDefensePercent", magnitude = 25 },
                new RawTalentEffect { type = "IgnoreDefensePercent", magnitude = 50 })));
        }

        [Test]
        public void TwoTiersOfOneHealthGatedRule_AreTheIntendedShape()
        {
            var entry = Entry(
                new RawTalentEffect { type = "WoolPerTurnBelowHealth", magnitude = 2, threshold = 67 },
                new RawTalentEffect { type = "WoolPerTurnBelowHealth", magnitude = 3, threshold = 33 });

            Assert.IsTrue(Resolve(entry, out var errors), string.Join(" ", errors));
        }

        // A node granting an ABILITY grants something, even though it carries
        // no stat bonus and no effects — the check that catches an empty node
        // has to know about both new payload routes or a pure-ability node
        // would be rejected as doing nothing.
        [Test]
        public void ANodeThatOnlyGrantsASkill_CountsAsGrantingSomething()
        {
            var entry = new RawTalentEntry
            {
                id = "test_node",
                displayName = "Provoke",
                characterId = "sheep",
                column = 0,
                row = 1,
                grantsSkillId = "provoke",
            };

            Assert.IsTrue(Resolve(entry, out var errors), string.Join(" ", errors));
        }

        [Test]
        public void ANodeGrantingNothingAtAll_IsStillRejected()
        {
            var entry = new RawTalentEntry
            {
                id = "test_node",
                displayName = "Nothing",
                characterId = "sheep",
                column = 0,
                row = 1,
            };

            StringAssert.Contains("grants nothing", Error(entry));
        }
    }
}
