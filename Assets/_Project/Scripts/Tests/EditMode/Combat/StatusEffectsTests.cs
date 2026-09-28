using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class StatusEffectsTests
    {
        private static CombatantState MakeCombatant(int maxHealth = 1000)
        {
            return new CombatantState("Test", true, maxHealth, 10, 5, 5);
        }

        // TickReport carries typed rows rather than a PoisonDamage field.
        // FirstOrDefault rather than Single: a tick with nothing left to deal
        // reports no Poison row at all, and TickRow's default ToHealth (0) is
        // the right answer for "a fourth tick landed" on an expired status,
        // not a thrown exception.
        private static int PoisonDamageOf(StatusEffects.TickReport report) =>
            report.Rows.FirstOrDefault(r => r.Status == StatusEffectType.Poison).ToHealth;

        // ---- Apply / stacking ----------------------------------------------

        [Test]
        public void Apply_NewType_AddsAnEntry()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusEffectType.Poison, target.Statuses[0].Type);
            Assert.AreEqual(10, target.Statuses[0].Magnitude);
            Assert.AreEqual(3, target.Statuses[0].TurnsRemaining);
        }

        // THE MERGE BRANCH STILL EXISTS and these three still pin it, aimed
        // at Empowered. Empowered is a single-spend token
        // (StackingPolicy.Refresh): "two of it" has no
        // meaning beyond duration, so re-casting it must never be strictly
        // better than casting it once, which is the rule these were always
        // about.
        [Test]
        public void Apply_SameTypeTwice_RefreshesRatherThanStacking()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 10, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 10, 3);

            Assert.AreEqual(1, target.Statuses.Count, "A second application should refresh, not add a second entry");
        }

        [Test]
        public void Apply_SameTypeAgain_TakesTheStrongerMagnitudeAndLongerDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 10, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 25, 5);

            Assert.AreEqual(25, target.Statuses[0].Magnitude);
            Assert.AreEqual(5, target.Statuses[0].TurnsRemaining);
        }

        [Test]
        public void Apply_AWeakerReapplication_NeverWeakensTheExistingOne()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 25, 5);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Empowered, 10, 1);

            Assert.AreEqual(25, target.Statuses[0].Magnitude, "A weaker re-application should not downgrade the stronger one already active");
            Assert.AreEqual(5, target.Statuses[0].TurnsRemaining);
        }

        // ---- the two tables, and neither may go stale -------------------------

        // A table that answers for eleven of twelve members and throws on the
        // twelfth is a crash waiting for the content that authors it. Walking
        // Enum.GetValues is the only check that cannot be outrun by a new
        // member, and the count assertion is the vacuity guard: a reflection
        // sweep over nothing passes everything.
        [Test]
        public void EveryStatusTypeAnswersDurationClock()
        {
            var all = (StatusEffectType[])System.Enum.GetValues(typeof(StatusEffectType));
            Assert.GreaterOrEqual(all.Length, 12, "the sweep found fewer members than this game ships");

            foreach (var type in all)
            {
                Assert.DoesNotThrow(() => StatusEffects.DurationClock(type),
                    $"{type} has no duration family -- it would expire a turn early or never");
            }
        }

        [Test]
        public void EveryStatusTypeAnswersStackPolicy()
        {
            var all = (StatusEffectType[])System.Enum.GetValues(typeof(StatusEffectType));
            Assert.GreaterOrEqual(all.Length, 12, "the sweep found fewer members than this game ships");

            foreach (var type in all)
            {
                Assert.DoesNotThrow(() => StatusEffects.StackPolicyOf(type),
                    $"{type} does not say what a second application of it does");
            }
        }

        // THE THIRD TABLE (milestone D). A content row that names a status is
        // required to author a magnitude for it -- unless it has none, in
        // which case it is required NOT to. A member with no answer here would
        // throw the moment anybody authored it.
        [Test]
        public void EveryStatusTypeAnswersCarriesMagnitude()
        {
            var all = (StatusEffectType[])System.Enum.GetValues(typeof(StatusEffectType));
            Assert.GreaterOrEqual(all.Length, 12, "the sweep found fewer members than this game ships");

            foreach (var type in all)
            {
                Assert.DoesNotThrow(() => StatusEffects.CarriesMagnitude(type),
                    $"{type} does not say whether it has a magnitude an author could write");
            }
        }

        // The judgement itself, written down. NOT the same line StackPolicyOf
        // draws: Empowered and Feared both refresh rather than stacking and
        // both carry a real number, so a test that asserted the two tables
        // agreed would be asserting a coincidence.
        [Test]
        public void TheGatesCarryNoMagnitudeAndTheQuantitiesDo()
        {
            foreach (var gate in new[]
                     {
                         StatusEffectType.Stun, StatusEffectType.Provoked,
                         StatusEffectType.Rooted, StatusEffectType.Marked,
                     })
            {
                Assert.IsFalse(StatusEffects.CarriesMagnitude(gate), $"{gate} is a gate, not a quantity");
            }

            foreach (var quantity in new[]
                     {
                         StatusEffectType.Poison, StatusEffectType.Regen, StatusEffectType.Protect,
                         StatusEffectType.Vulnerable, StatusEffectType.Chilled, StatusEffectType.Shielded,
                         StatusEffectType.Empowered, StatusEffectType.Feared,
                     })
            {
                Assert.IsTrue(StatusEffects.CarriesMagnitude(quantity),
                    $"{quantity} carries a number something reads off the entry");
            }
        }

        // The families themselves, as a literal table. DurationClock is a
        // judgement about each status and this is where that judgement is
        // written down in a form that fails when somebody moves a member.
        [Test]
        public void TheThreeDurationFamiliesHoldTheMembersTheyWereDecidedFor()
        {
            Assert.AreEqual(StatusClock.AtTick, StatusEffects.DurationClock(StatusEffectType.Poison));
            Assert.AreEqual(StatusClock.AtTick, StatusEffects.DurationClock(StatusEffectType.Regen));

            Assert.AreEqual(StatusClock.AtUse, StatusEffects.DurationClock(StatusEffectType.Stun));
            Assert.AreEqual(StatusClock.AtUse, StatusEffects.DurationClock(StatusEffectType.Feared));
            Assert.AreEqual(StatusClock.AtUse, StatusEffects.DurationClock(StatusEffectType.Provoked));
            Assert.AreEqual(StatusClock.AtUse, StatusEffects.DurationClock(StatusEffectType.Empowered));

            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Protect));
            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Vulnerable));
            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Chilled));
            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Rooted));
            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Marked));
            Assert.AreEqual(StatusClock.AtTurnEnd, StatusEffects.DurationClock(StatusEffectType.Shielded));
        }

        [Test]
        public void ARestrictionRefreshes_AndAQuantityStacks()
        {
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Stun));
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Feared));
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Rooted));
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Provoked));
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Empowered));
            Assert.AreEqual(StackingPolicy.Refresh, StatusEffects.StackPolicyOf(StatusEffectType.Marked));

            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Poison));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Regen));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Protect));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Vulnerable));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Chilled));
            Assert.AreEqual(StackingPolicy.Stack, StatusEffects.StackPolicyOf(StatusEffectType.Shielded));
        }

        // ---- instance stacking -------------------------------

        [Test]
        public void ASecondPoison_StandsBesideTheFirst_WithItsOwnMagnitudeAndClock()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 7, 5);

            Assert.AreEqual(2, target.Statuses.Count, "the second application merged instead of stacking");
            Assert.AreEqual(4, target.Statuses[0].Magnitude);
            Assert.AreEqual(2, target.Statuses[0].TurnsRemaining);
            Assert.AreEqual(7, target.Statuses[1].Magnitude,
                "the weaker first instance must not be raised to the stronger second one");
            Assert.AreEqual(5, target.Statuses[1].TurnsRemaining);
        }

        // 4 + 7 + 2 = 13. Written as a literal, not as a sum of the same
        // fields the production code adds up.
        [Test]
        public void ThreePoisonInstances_TickForTheirSum_InOnePass()
        {
            var target = MakeCombatant(maxHealth: 200);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 7, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 2, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(13, PoisonDamageOf(report), "the tick must be the sum of every live instance");
            Assert.AreEqual(187, target.CurrentHealth);
        }

        [Test]
        public void TheShortestPoisonInstanceGoesFirst_AndTheOthersKeepTicking()
        {
            var target = MakeCombatant(maxHealth: 200);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 5, 1);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 3, 3);

            var first = StatusEffects.Tick(target);
            Assert.AreEqual(8, PoisonDamageOf(first));
            CollectionAssert.AreEqual(new[] { StatusEffectType.Poison }, first.Expired,
                "one instance ran out and one removal is what should be reported");
            Assert.AreEqual(1, target.Statuses.Count, "the longer instance went with the shorter one");

            var second = StatusEffects.Tick(target);
            Assert.AreEqual(3, PoisonDamageOf(second), "the survivor must keep ticking at its own strength");
        }

        [Test]
        public void TwoCastersPoisoningOneTarget_EachKeepTheirOwnAttribution()
        {
            var target = MakeCombatant();
            var mage = MakeCombatant();
            var lamb = MakeCombatant();

            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 3, mage);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 3, lamb);

            Assert.AreSame(mage, target.Statuses[0].Source);
            Assert.AreSame(lamb, target.Statuses[1].Source,
                "a second caster must not re-point the first caster's instance -- two wool engines are paid from this");
        }

        [Test]
        public void ASecondStun_RefreshesRatherThanStacking()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 0, 1);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 0, 1);

            Assert.AreEqual(1, target.Statuses.Count,
                "a turn cannot be skipped twice, so a second Stun is one Stun");
        }

        // 1 + 0.25 + 0.30 = 1.55. Pinned as a literal.
        [Test]
        public void TwoVulnerables_AddUpInTheDamageMultiplier()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 30, 2);

            Assert.AreEqual(1.55f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
            Assert.AreEqual(55, StatusEffects.MagnitudeOf(target, StatusEffectType.Vulnerable));
        }

        [Test]
        public void MagnitudeOf_ReadsTheWholePile_NotTheFirstEntry()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Chilled, 20, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Chilled, 25, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Chilled, 15, 2);

            Assert.AreEqual(60, StatusEffects.MagnitudeOf(target, StatusEffectType.Chilled));
            Assert.AreEqual(0, StatusEffects.MagnitudeOf(target, StatusEffectType.Protect),
                "a type the combatant does not carry is 0, not the first thing in the list");
        }

        [Test]
        public void SummariseStatus_ReportsTheSum_TheCount_AndTheSoonestExpiry()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 5);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 7, 2);

            var summary = StatusEffects.SummariseStatus(target, StatusEffectType.Poison);

            Assert.AreEqual(11, summary.Magnitude);
            Assert.AreEqual(2, summary.Instances);
            Assert.AreEqual(2, summary.SoonestTurns, "the soonest is the next moment the number changes on its own");
            Assert.IsTrue(summary.Any);
        }

        // ---- the turn-end clock ---------------------------------------

        // Ticking at turn END, not turn start: a turn-start countdown would
        // remove a two-turn Vulnerable at the start of the bearer's second
        // turn, before that turn's action ever happened, exposing them for
        // one turn instead of two.
        [Test]
        public void AStandingStatusWithTwoTurns_StillApplies_OnTheSecondTurnsAction()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 2);

            StatusEffects.Tick(target);
            Assert.AreEqual(1.25f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f,
                "turn one's action");
            StatusEffects.TickAtTurnEnd(target);

            StatusEffects.Tick(target);
            Assert.AreEqual(1.25f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f,
                "turn two's action -- the turn a turn-start countdown used to take it away before");
            StatusEffects.TickAtTurnEnd(target);

            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f,
                "and gone at the end of the second turn, not a third");
        }

        [Test]
        public void AStatusAppliedOnTheBearersOwnTurn_DoesNotAgeAtThatTurnsEnd()
        {
            var target = MakeCombatant();
            var applied = StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 1);
            var thisTurn = new List<ActiveStatus> { applied };

            CollectionAssert.IsEmpty(StatusEffects.TickAtTurnEnd(target, thisTurn),
                "the turn it was applied during counted");
            Assert.AreEqual(1, target.Statuses[0].TurnsRemaining);

            CollectionAssert.AreEqual(new[] { StatusEffectType.Vulnerable },
                StatusEffects.TickAtTurnEnd(target));
        }

        [Test]
        public void TheTurnStartTickLeavesEveryStandingModifierAlone()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Chilled, 20, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 2);

            StatusEffects.Tick(target);

            foreach (var status in target.Statuses)
            {
                Assert.AreEqual(2, status.TurnsRemaining,
                    $"{status.Type} is on the turn-END clock and the turn-start tick moved it");
            }
        }

        [Test]
        public void Tick_Poison_StillDealsExactlyThreeTicksForThree()
        {
            var target = MakeCombatant(maxHealth: 200);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 5, 3);

            StatusEffects.Tick(target);
            StatusEffects.Tick(target);
            var third = StatusEffects.Tick(target);

            Assert.AreEqual(185, target.CurrentHealth, "three ticks of five, no more and no fewer");
            CollectionAssert.AreEqual(new[] { StatusEffectType.Poison }, third.Expired);
            Assert.AreEqual(0, PoisonDamageOf(StatusEffects.Tick(target)), "a fourth tick landed");
        }

        [Test]
        public void Apply_DifferentTypes_CoexistIndependently()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 2);

            Assert.AreEqual(2, target.Statuses.Count);
        }

        // ---- Stun ------------------------------------------------------------

        [Test]
        public void HasStun_FalseWithoutOne()
        {
            var target = MakeCombatant();
            Assert.IsFalse(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void HasStun_TrueOnceApplied()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 1);

            Assert.IsTrue(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void ConsumeStun_RemovesItEntirely_NotJustDecrementsDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 5);

            StatusEffects.ConsumeStun(target.Statuses);

            Assert.IsFalse(StatusEffects.HasStun(target.Statuses),
                "A spent Stun should be gone outright, not merely a shorter Stun");
            Assert.AreEqual(0, target.Statuses.Count);
        }

        [Test]
        public void ConsumeStun_LeavesOtherStatusesUntouched()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 1);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            StatusEffects.ConsumeStun(target.Statuses);

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusEffectType.Poison, target.Statuses[0].Type);
        }

        // ---- Rooted (Phase D3, item-modifier plan) ----------------------------
        //
        // The mechanism-level coverage (the plain-attack gate, the no-legal-
        // skill forfeit, coexistence with Chilled/Dodge) lives in
        // RootedStatusTests -- these mirror HasStun's own small, direct
        // shape: Rooted is queried like Stun, but decays by turn count like
        // Chilled/Protect/Vulnerable, so there is no ConsumeRooted to test.

        [Test]
        public void HasRooted_FalseWithoutOne()
        {
            var target = MakeCombatant();
            Assert.IsFalse(StatusEffects.HasRooted(target.Statuses));
        }

        [Test]
        public void HasRooted_TrueOnceApplied()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 3);

            Assert.IsTrue(StatusEffects.HasRooted(target.Statuses));
        }

        [Test]
        public void Rooted_ExpiresAfterItsDuration_LikeAnyOtherTurnCountedStatus()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 1);

            // The turn-END clock since plan D1: a restriction has to survive
            // the action of the turn it is restricting.
            Assert.IsTrue(StatusEffects.HasRooted(target.Statuses),
                "a turn-start tick must no longer take a restriction away before the turn it restricts");
            var expired = StatusEffects.TickAtTurnEnd(target);

            Assert.IsFalse(StatusEffects.HasRooted(target.Statuses), "a one-turn Rooted must be gone after one turn end");
            Assert.IsTrue(expired.Contains(StatusEffectType.Rooted));
        }

        [Test]
        public void Rooted_SurvivesATickWithTurnsRemaining_UnlikeStunWhichIsSpentOutright()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 2);

            StatusEffects.TickAtTurnEnd(target);

            Assert.IsTrue(StatusEffects.HasRooted(target.Statuses), "a two-turn Rooted must survive one turn end");
            Assert.AreEqual(1, target.Statuses[0].TurnsRemaining);
        }

        // ---- DamageTakenMultiplier --------------------------------------------

        [Test]
        public void DamageTakenMultiplier_NoStatuses_IsOne()
        {
            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(new System.Collections.Generic.List<ActiveStatus>()));
        }

        [Test]
        public void DamageTakenMultiplier_Protect30_ReducesByThirtyPercent()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 3);

            Assert.AreEqual(0.7f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        [Test]
        public void DamageTakenMultiplier_Vulnerable25_IncreasesByTwentyFivePercent()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 3);

            Assert.AreEqual(1.25f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        // Additive, not compounding — a player can add the two contributions
        // up in their head, same rule ScalingProfile.MultiplierFor follows.
        [Test]
        public void DamageTakenMultiplier_ProtectAndVulnerableTogether_AreAdditive()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 20, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 50, 3);

            // 1 - 0.20 + 0.50 = 1.30, not (1 - 0.20) * (1 + 0.50) = 1.20.
            Assert.AreEqual(1.30f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        [Test]
        public void DamageTakenMultiplier_HugeProtect_NeverReachesZeroOrNegative()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 500, 3);

            Assert.AreEqual(StatusEffects.MinimumDamageTakenMultiplier, StatusEffects.DamageTakenMultiplier(target.Statuses));
        }

        // ---- Tick --------------------------------------------------------------

        [Test]
        public void Tick_Poison_DealsItsMagnitudeAsDamage()
        {
            var target = MakeCombatant();
            int before = target.CurrentHealth;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 40, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(40, PoisonDamageOf(report));
            Assert.AreEqual(before - 40, target.CurrentHealth);
        }

        // SPELL-EXPANSION BASELINE (docs/SPELL_EXPANSION_BASELINE.md, areas 1
        // and 5): a Poison tick calls CombatMath.ApplyDamage(target, Magnitude)
        // directly -- a two-argument call with no defense or affinity term to
        // pass -- never DamagePipeline.AfterDefences. A huge PhysicalDefense/
        // MagicalDefense must not blunt it, which is the one thing that would
        // look wrong if a future change accidentally routed a tick through the
        // mitigated path.
        [Test]
        public void Tick_Poison_IgnoresTheTargetsDefense_UnlikeAnOrdinaryHit()
        {
            var target = MakeCombatant();
            target.PhysicalDefense = 999999;
            target.MagicalDefense = 999999;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(10, PoisonDamageOf(report),
                "a tick's flat Magnitude reaches health untouched -- defense is a DamagePipeline concern " +
                "and a tick never enters that funnel");
        }

        [Test]
        public void Tick_Regen_HealsItsMagnitude()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 100;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Regen, 40, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(40, report.RegenHealed);
            Assert.AreEqual(140, target.CurrentHealth);
        }

        [Test]
        public void Tick_DecrementsDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            StatusEffects.Tick(target);

            Assert.AreEqual(2, target.Statuses[0].TurnsRemaining);
        }

        // MOVED WITH PLAN D1. The baseline (docs/SPELL_EXPANSION_BASELINE.md
        // area 9) recorded these two decrementing at the bearer's turn START,
        // and recorded why that was wrong: a standing modifier reached zero and
        // was removed before the action of its last counted turn. Both are on
        // the turn-END clock now. Marked's own 99-turn duration is still long
        // specifically so ordinary decay can never expire an unconsumed mark
        // first (see Marks.MarkDurationTurns), whichever end of the turn it
        // decays at.
        [Test]
        public void Tick_VulnerableAndMarked_DecrementAtTurnEnd_NotAtTurnStart()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Marked, 0, 99);

            StatusEffects.Tick(target);

            var vulnerable = target.Statuses.First(s => s.Type == StatusEffectType.Vulnerable);
            var marked = target.Statuses.First(s => s.Type == StatusEffectType.Marked);
            Assert.AreEqual(2, vulnerable.TurnsRemaining, "the turn-start tick moved a turn-end clock");
            Assert.AreEqual(99, marked.TurnsRemaining, "the turn-start tick moved a turn-end clock");

            StatusEffects.TickAtTurnEnd(target);

            Assert.AreEqual(1, vulnerable.TurnsRemaining);
            Assert.AreEqual(98, marked.TurnsRemaining);
        }

        [Test]
        public void Tick_ExpiresAndRemovesAStatusThatReachesZeroDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 1);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(0, target.Statuses.Count);
            CollectionAssert.Contains(report.Expired, StatusEffectType.Poison);
        }

        [Test]
        public void Tick_NotYetExpired_StaysOnTheList()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(1, target.Statuses.Count);
            CollectionAssert.IsEmpty(report.Expired);
        }

        [Test]
        public void Tick_MultipleStatuses_AllTickIndependently()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 500;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 30, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Regen, 20, 2);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(30, PoisonDamageOf(report));
            Assert.AreEqual(20, report.RegenHealed);
            Assert.AreEqual(500 - 30 + 20, target.CurrentHealth);
        }

        [Test]
        public void Tick_NoStatuses_ReturnsAnEmptyReport()
        {
            var target = MakeCombatant();
            var report = StatusEffects.Tick(target);

            Assert.IsTrue(report.IsEmpty);
        }

        // Poison respects the same floor everything else in the damage
        // pipeline does — it can bring a combatant to 0, never negative.
        [Test]
        public void Tick_Poison_NeverDropsHealthBelowZero()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 10;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 9999, 1);

            StatusEffects.Tick(target);

            Assert.AreEqual(0, target.CurrentHealth);
        }

        // ---- the ward pool (Magical Shield, and every other ward) ---------
        //
        // WardTests owns the model in full; these four are what this file's
        // own Magical Shield coverage always asserted, repinned as points.

        [Test]
        public void ConsumeWard_WithNoWard_ReturnsDamageUnchanged()
        {
            var target = MakeCombatant();

            Assert.AreEqual(100, StatusEffects.ConsumeWard(target, 100).Damage);
        }

        [Test]
        public void ConsumeWard_EatsThePoolAndRemovesTheStatus()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(50, StatusEffects.ConsumeWard(target, 100).Damage,
                "50 points of a 100 hit");
            Assert.IsFalse(target.Statuses.Exists(s => s.Type == StatusEffectType.Shielded),
                "The shield should be gone once its pool is empty");
        }

        // It is spent by damage, not by a turn count, and a hit bigger than
        // the pool takes the whole pool with it.
        [Test]
        public void ConsumeWard_HasNothingLeftForASecondBigHit()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(50, StatusEffects.ConsumeWard(target, 100).Damage);
            Assert.AreEqual(100, StatusEffects.ConsumeWard(target, 100).Damage,
                "The pool was emptied by the first hit");
        }

        // RE-RAISING WHILE THE SHIELD STILL STANDS ADDS TO IT: wards stack.
        // The relic's "does not stack" promise is held by its once-per-turn
        // lock instead, which is where a limit on how often it raises
        // belongs.
        [Test]
        public void ReapplyingAWard_WhileStillUp_AddsASecondPool()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(2, target.Statuses.Count);
            Assert.AreEqual(100, StatusEffects.WardPoints(target));
        }

        [Test]
        public void ConsumeWard_DoesNotSpendOnNonPositiveDamage()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            StatusEffects.ConsumeWard(target, 0);

            Assert.AreEqual(50, StatusEffects.WardPoints(target),
                "A non-hit should not spend the shield");
        }

        // ---- a poison tick a signature pool eats whole -----------------------

        [Test]
        public void APoisonTickAWoolPoolAbsorbsIsStillCountedAndStillSaid()
        {
            // StatusEffects.Tick measures poison as HEALTH LOST, and
            // CombatMath.ApplyDamage spends the signature pool BEFORE health --
            // so a tick the pool ate in full reported zero. TickStatuses is
            // gated on each row's `ToHealth + Absorbed` (D5's typed rows;
            // ToHealth alone would have missed it), which meant no log line, no
            // RecordUnattributedDamage, no Ledger.Took absorbed column, and no
            // NoteDamageForPools: the exact bookkeeping d6814ce0 moved into one
            // funnel precisely so it could not be forgotten.
            //
            // Two contracts say so. FightSession.Ledger.cs:219-225: "THE POOLS
            // ARE TOLD HERE TOO, which is the whole reason this is not just a
            // Ledger.Took call. A status tick is damage its victim took by
            // every account that matters to a resource pool." And
            // CombatMath.cs:508-513: "Absorption lives HERE, in the single
            // funnel every damage source already goes through... one of them
            // forgetting would make the resource silently stop being armour
            // depending on who hit you."
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10)
            {
                SignaturePool = new ResourcePool("wool", "Wool", 20, 0, 0, 0,
                    absorbPerPoint: 1, absorbsDamage: true),
            };
            hero.SignaturePool.Current = 20;

            var encounter = new CombatEncounter(new[] { hero }, new[]
            {
                new CombatantState("Rat", false, 5000, 10, 8, 1),
            });
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(2)) { DamageVarianceRange = 0f };

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 5, 3);

            int healthBefore = hero.CurrentHealth;
            session.TickStatusesForTest(hero);

            Assert.AreEqual(healthBefore, hero.CurrentHealth, "fixture: the pool ate the whole tick");
            Assert.AreEqual(15, hero.SignaturePool.Current, "fixture: and paid five points for it");

            Assert.AreEqual(5, session.Ledger.For("shawn").Shielded,
                "the pool ate five points of poison and the ledger never heard about it");
            Assert.AreEqual(0, session.Ledger.For("shawn").DamageTaken,
                "and none of it reached health, so none of it is damage taken");

            // Immediate messages as well as beat ones: nothing has opened a
            // beat in this fixture (TickStatusesForTest is called directly, by
            // design -- see its own header), so AppendMessage files the lines
            // under _immediateMessages rather than onto a beat.
            var lines = session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages())
                .ToList();
            Assert.IsTrue(lines.Any(l => l.Contains("poison damage")),
                "a tick the player's armour absorbed was never mentioned at all");
            Assert.IsTrue(lines.Any(l => l.Contains("Wool soaks 5 of it")),
                "and the armour doing its job is the half worth saying");
        }
    

        // ---- what a status's damage is made of ------------------------------

        [Test]
        public void ElementOf_PoisonIsPoison()
        {
            // The literal, not a re-read of the switch: a poison tick's flash
            // and its damage number are both coloured through
            // FightHudPalette.ForDamageType off this answer, and "it agrees
            // with itself" is what a tautology looks like.
            Assert.AreEqual(DamageType.Poison, StatusEffects.ElementOf(StatusEffectType.Poison));
        }

        [Test]
        public void ElementOf_EveryOtherStatusDealsNoDamageAndSaysSo()
        {
            // The vacuity guard first -- a switch asked about nothing passes
            // anything, and this list is the whole point of the test.
            var all = System.Enum.GetValues(typeof(StatusEffectType)).Cast<StatusEffectType>().ToList();
            Assert.GreaterOrEqual(all.Count, 12, "StatusEffectType shrank; this test stopped covering it");

            // NULL, not Physical. A status that deals no damage has no
            // element, and a caller must not have to know that Physical is
            // the enum's zero to tell the two apart. Burn (Fire) and Thorned
            // (Nature) are the two new members that DO deal damage (plan
            // 1.5) -- excluded here for the same reason Poison always was,
            // and covered by their own assertions in NewDotTests.
            foreach (var type in all.Where(t => t != StatusEffectType.Poison
                                              && t != StatusEffectType.Burn
                                              && t != StatusEffectType.Thorned
                                              && t != StatusEffectType.Bleed))
            {
                Assert.IsNull(StatusEffects.ElementOf(type),
                    $"{type} answered with an element; if it deals damage now, say which in ElementOf");
            }
        }

        // ---- TrySpend, milestone B (plan 1.8) -------------------------------

        [Test]
        public void TrySpend_ReportsTheEntryItRemoved()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Marked, 0, 99);

            bool spent = StatusEffects.TrySpend(target.Statuses, StatusEffectType.Marked, out var entry);

            Assert.IsTrue(spent);
            Assert.IsNotNull(entry);
            Assert.AreEqual(StatusEffectType.Marked, entry.Type);
            Assert.IsFalse(target.Statuses.Any(s => s.Type == StatusEffectType.Marked));
        }

        [Test]
        public void TrySpend_NoMatchingEntry_ReportsFalseAndTouchesNothing()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 10, 3);

            bool spent = StatusEffects.TrySpend(target.Statuses, StatusEffectType.Poison, out var entry);

            Assert.IsFalse(spent);
            Assert.IsNull(entry);
            Assert.AreEqual(1, target.Statuses.Count, "the unrelated Protect entry must survive untouched");
        }

        // ONE ENTRY, NOT EVERY ENTRY -- a caller after the whole pile (like
        // StatusCombos.SpendPoisonIfMatched) loops this itself; TrySpend does
        // not loop for it.
        [Test]
        public void TrySpend_OnAPile_RemovesOnlyTheFirstEntry()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 4, 5);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 2, 3);

            bool spent = StatusEffects.TrySpend(target.Statuses, StatusEffectType.Poison, out var entry);

            Assert.IsTrue(spent);
            Assert.AreEqual(4, entry.Magnitude, "the FIRST entry, not the pile's total");
            Assert.AreEqual(1, target.Statuses.Count(s => s.Type == StatusEffectType.Poison),
                "the second instance must still be standing");
        }
    }
}
