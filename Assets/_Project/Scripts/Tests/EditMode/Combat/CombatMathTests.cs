using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class CombatMathTests
    {
        private static CombatantState MakeCombatant(int attack = 5, int physicalDefense = 2, int magicalDefense = 2,
            int maxHealth = 20, int maxMana = 10, int speed = 5)
        {
            var combatant = new CombatantState("Test", true, maxHealth, maxMana, attack, speed);
            combatant.PhysicalDefense = physicalDefense;
            combatant.MagicalDefense = magicalDefense;
            return combatant;
        }

        // ---- ComputeAttackDamage / ComputeSkillDamage: RAW, no mitigation --
        //
        // Phase 1 of the balance redesign moved every defense term out of
        // CombatMath and into DamagePipeline's single canonical equation
        // (see its own header) — these two functions never read the target
        // for anything but its type signature (kept for call-site
        // stability).
        //
        // NO LONGER SCALED (fixed 2026-08-26): D1 always said
        // CombatMath.DamageScale's x5 was deleted from these two entry
        // points; two prior implementation passes left it in regardless.
        // They now return Math.Max(1, ScaledAttack(...)) -- see
        // ComputeAttackDamage's own header for the full story.

        [Test]
        public void ComputeAttackDamage_IsUnaffectedByTheTargetsDefense()
        {
            var attacker = MakeCombatant(attack: 10);
            var lightlyArmoured = MakeCombatant(physicalDefense: 3);
            var heavilyArmoured = MakeCombatant(physicalDefense: 500);

            // 10 attack, no grades -> 1.0x, regardless of either target.
            Assert.AreEqual(10, CombatMath.ComputeAttackDamage(attacker, lightlyArmoured));
            Assert.AreEqual(10, CombatMath.ComputeAttackDamage(attacker, heavilyArmoured));
        }

        [Test]
        public void ComputeAttackDamage_FlooredAtOneBeforeTheScale()
        {
            var attacker = MakeCombatant(attack: 0);
            var target = MakeCombatant();

            // Floored at 1 directly -- see ComputeAttackDamage's own header —
            // so a zero attack still lands for 1, never 0.
            Assert.AreEqual(1, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void ComputeSkillDamage_HitsHarderThanABasicAttack()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(physicalDefense: 3);

            int attackDamage = CombatMath.ComputeAttackDamage(attacker, target);
            int skillDamage = CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f);

            Assert.Greater(skillDamage, attackDamage);
        }

        [Test]
        public void ComputeSkillDamage_FlooredAtOneBeforeTheScale()
        {
            var attacker = MakeCombatant(attack: 0);
            var target = MakeCombatant();

            Assert.AreEqual(1, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        // ---- BroadDefense / TotalDefense -- the D_broad and D_broad+D_typed
        // terms of DamagePipeline's canonical equation. See its own header
        // for the full formula; these pin each modifier in isolation.

        [Test]
        public void BroadDefense_NoModifiers_ReadsThePhysicalOrMagicalStatByType()
        {
            var target = MakeCombatant(physicalDefense: 7, magicalDefense: 15);

            Assert.AreEqual(7, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
            Assert.AreEqual(15, CombatMath.BroadDefense(target, DamageType.Fire, null, false));
            Assert.AreEqual(15, CombatMath.BroadDefense(target, DamageType.Ice, null, false), "every non-Physical type reads MagicalDefense");
        }

        [Test]
        public void BroadDefense_IgnoresDefenseFlag_IsZeroRegardlessOfTheStat()
        {
            var target = MakeCombatant(physicalDefense: 40);

            Assert.AreEqual(0, CombatMath.BroadDefense(target, DamageType.Physical, null, ignoresDefense: true));
        }

        [Test]
        public void BroadDefense_NullTarget_IsZeroRatherThanThrowing()
        {
            Assert.AreEqual(0, CombatMath.BroadDefense(null, DamageType.Physical, null, false));
        }

        [Test]
        public void BroadDefense_BreakShieldPresentButNotBroken_IsStillJustTheStat()
        {
            var target = MakeCombatant(physicalDefense: 40);
            target.BreakShield = new BreakShield(10);

            Assert.AreEqual(40, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
        }

        [Test]
        public void BroadDefense_Broken_IsZero()
        {
            var target = MakeCombatant(physicalDefense: 40);
            target.BreakShield = new BreakShield(3);
            target.BreakShield.Deplete(3);

            Assert.IsTrue(target.BreakShield.IsBroken, "Sanity check on the fixture");
            Assert.AreEqual(0, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
        }

        [Test]
        public void BroadDefense_LastStandBonusBelowHealth_IncreasesTheFigure()
        {
            var target = MakeCombatant(physicalDefense: 40, maxHealth: 100);
            target.CurrentHealth = 20; // 20% -- at or below the 25% threshold below
            target.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.DefenseBonusPercentBelowHealth, 50, 25),
            });

            // +50% of 40 = 20, so 40 + 20 = 60.
            Assert.AreEqual(60, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
        }

        [Test]
        public void BroadDefense_BreakOverridesTheLastStandBonus()
        {
            var target = MakeCombatant(physicalDefense: 40, maxHealth: 100);
            target.CurrentHealth = 20;
            target.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.DefenseBonusPercentBelowHealth, 50, 25),
            });
            target.BreakShield = new BreakShield(3);
            target.BreakShield.Deplete(3);

            Assert.AreEqual(0, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
        }

        [Test]
        public void BroadDefense_AttackerPenetration_ReducesWhatIsLeft()
        {
            var target = MakeCombatant(physicalDefense: 40);
            var attacker = MakeCombatant();
            attacker.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.IgnoreDefensePercent, 25),
            });

            // 40 minus 25% = 30.
            Assert.AreEqual(30, CombatMath.BroadDefense(target, DamageType.Physical, attacker, false));
        }

        [Test]
        public void BroadDefense_NoAttacker_SkipsPenetrationRatherThanThrowing()
        {
            var target = MakeCombatant(physicalDefense: 40);

            Assert.AreEqual(40, CombatMath.BroadDefense(target, DamageType.Physical, null, false));
        }

        [Test]
        public void TotalDefense_SumsBroadAndTyped()
        {
            var target = MakeCombatant(magicalDefense: 10);
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 15);

            Assert.AreEqual(25, CombatMath.TotalDefense(target, DamageType.Fire, null, false));
        }

        [Test]
        public void TotalDefense_TypedResistance_SurvivesABrokenBreakShield()
        {
            var target = MakeCombatant(magicalDefense: 10);
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 15);
            target.BreakShield = new BreakShield(3);
            target.BreakShield.Deplete(3);

            // The broad term zeroes on break; the typed term does not -- see
            // TotalDefense's own header.
            Assert.AreEqual(15, CombatMath.TotalDefense(target, DamageType.Fire, null, false));
        }

        [Test]
        public void TotalDefense_IgnoresDefenseFlag_StillAppliesTypedResistance()
        {
            var target = MakeCombatant(magicalDefense: 10);
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 15);

            // The broad term is skipped by the flag; typed is not.
            Assert.AreEqual(15, CombatMath.TotalDefense(target, DamageType.Fire, null, ignoresDefense: true));
        }

        // ---- ApplyStatusEffects ---------------------------------------------

        [Test]
        public void ApplyStatusEffects_NoStatuses_ReturnsTheDamageUnchanged()
        {
            var target = MakeCombatant();
            Assert.AreEqual(100, CombatMath.ApplyStatusEffects(100, target));
        }

        [Test]
        public void ApplyStatusEffects_Protected_ReducesDamage()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 3);

            Assert.AreEqual(70, CombatMath.ApplyStatusEffects(100, target));
        }

        [Test]
        public void ApplyStatusEffects_Vulnerable_IncreasesDamage()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 3);

            Assert.AreEqual(125, CombatMath.ApplyStatusEffects(100, target));
        }

        [Test]
        public void ApplyStatusEffects_NullTarget_ReturnsTheDamageUnchangedRatherThanThrowing()
        {
            Assert.AreEqual(100, CombatMath.ApplyStatusEffects(100, null));
        }

        // ---- scaling ----------------------------------------------------
        //
        // Every number below is a pinned literal. The point of these is that
        // scaling reaches DAMAGE — the Domain unit tests already prove the
        // multiplier is right, and a multiplier nothing multiplies is worth
        // nothing.

        // THE compatibility property. Everything in the game was balanced
        // without scaling, so a combatant nobody has authored grades for must
        // hit for exactly what it always did.
        [Test]
        public void ACombatantWithNoGrades_HitsForAttackTimesTheScale()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, speed: 10);

            // No WeaponScaling authored -> neutral 1.0x. 20 attack, raw.
            Assert.AreEqual(20, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void WeaponScaling_MultipliesABasicAttack()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, speed: 10);

            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // S at Strength 20 is 2.00x: 20 attack becomes 40, raw. No
            // defense term and no DamageScale touch this any more.
            Assert.AreEqual(40, CombatMath.ComputeAttackDamage(attacker, target));
        }

        // PROPORTIONAL MITIGATION used to be a property of ComputeAttackDamage
        // itself, back when it applied Mitigate internally. That step moved to
        // DamagePipeline.AfterDefences (see DamagePipelineTests for the
        // property test that replaces this one) — ComputeAttackDamage no
        // longer reads the target for anything, which is exactly what this
        // now pins instead.
        [Test]
        public void ComputeAttackDamage_IsIndependentOfTheTargetsArmour()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            var naked = new CombatantState("Naked", false, 100, 0, attack: 0, speed: 10);
            var wall = new CombatantState("Wall", false, 100, 0, attack: 0, speed: 10);
            wall.PhysicalDefense = 500;
            wall.MagicalDefense = 500;

            Assert.AreEqual(
                CombatMath.ComputeAttackDamage(attacker, naked),
                CombatMath.ComputeAttackDamage(attacker, wall));
        }

        [Test]
        public void AWeaponTheWielderIsWrongFor_HitsSofterThanNoScalingAtAll()
        {
            var target = new CombatantState("B", false, 100, 0, attack: 0, speed: 10);

            var matched = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            matched.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);
            matched.AbilityScores = new AbilityScoreBlock(18, 10, 10, 10, 10, 10);

            var mismatched = new CombatantState("C", true, 100, 0, attack: 20, speed: 10);
            mismatched.WeaponScaling = matched.WeaponScaling;
            mismatched.AbilityScores = new AbilityScoreBlock(6, 10, 10, 10, 10, 10);

            var plain = new CombatantState("D", true, 100, 0, attack: 20, speed: 10);

            int matchedHit = CombatMath.ComputeAttackDamage(matched, target);
            int plainHit = CombatMath.ComputeAttackDamage(plain, target);
            int mismatchedHit = CombatMath.ComputeAttackDamage(mismatched, target);

            Assert.Greater(matchedHit, plainHit, "The right weapon in the right hands should beat no scaling at all");
            Assert.Less(mismatchedHit, plainHit, "The wrong weapon has to be a real setback, not merely a missed bonus");
        }

        // A caster's spell rides its OWN stats, not the sword in their hand.
        [Test]
        public void SkillScaling_IsASeparateAxisFromTheWeapon()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, speed: 10);

            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.SkillScaling = ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S);
            // Strong, and not at all clever.
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // The swing gets the Strength: 20 * 2.00 = 40, raw.
            Assert.AreEqual(40, CombatMath.ComputeAttackDamage(attacker, target));
            // The cast does not: Intelligence is neutral, so only the tier's
            // 1.5x applies. 20 * 1.5 = 30, raw.
            Assert.AreEqual(30, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        [Test]
        public void SkillScaling_MultipliesAlongsideTheTierRatherThanReplacingIt()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, speed: 10);

            attacker.SkillScaling = ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(10, 10, 10, 10, 20, 10);

            // 2.00x scaling on top of the tier's 1.5x: 20 * 3.0 = 60, raw.
            Assert.AreEqual(60, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        [Test]
        public void ComputeSkillDamage_HigherPowerMultiplier_DealsMoreDamage()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(physicalDefense: 3);

            int lowTierDamage = CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f);
            int highTierDamage = CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 4.2f);

            Assert.Greater(highTierDamage, lowTierDamage, "A higher-level spell tier should hit harder with the same Attack stat");
        }

        [Test]
        public void ApplyDamage_ReducesCurrentHealth()
        {
            var target = MakeCombatant(maxHealth: 20);

            CombatMath.ApplyDamage(target, 8);

            Assert.AreEqual(12, target.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero_NeverGoesNegative()
        {
            var target = MakeCombatant(maxHealth: 10);

            CombatMath.ApplyDamage(target, 999);

            Assert.AreEqual(0, target.CurrentHealth);
            Assert.IsFalse(target.IsAlive);
        }

        [Test]
        public void Heal_RestoresHealth_ClampedAtMax()
        {
            var target = MakeCombatant(maxHealth: 20);
            CombatMath.ApplyDamage(target, 15);

            CombatMath.Heal(target, 999);

            Assert.AreEqual(20, target.CurrentHealth);
        }

        [Test]
        public void SpendMana_ClampsAtZero()
        {
            var combatant = MakeCombatant(maxMana: 5);

            CombatMath.SpendMana(combatant, 999);

            Assert.AreEqual(0, combatant.CurrentMana);
        }

        [Test]
        public void RestoreMana_AddsMana_ClampedAtMax()
        {
            var combatant = MakeCombatant(maxMana: 10);
            CombatMath.SpendMana(combatant, 7);

            CombatMath.RestoreMana(combatant, 999);

            Assert.AreEqual(10, combatant.CurrentMana);
        }

        [Test]
        public void RestoreMana_AddsExactlyTheGivenAmount_WhenBelowMax()
        {
            var combatant = MakeCombatant(maxMana: 10);
            CombatMath.SpendMana(combatant, 7);

            CombatMath.RestoreMana(combatant, 4);

            Assert.AreEqual(7, combatant.CurrentMana);
        }

        [Test]
        public void IsAlive_FalseAtZeroHealth_TrueOtherwise()
        {
            var combatant = MakeCombatant(maxHealth: 5);
            Assert.IsTrue(combatant.IsAlive);

            CombatMath.ApplyDamage(combatant, 5);
            Assert.IsFalse(combatant.IsAlive);
        }

        [Test]
        public void EffectivenessMultiplier_MatchingWeakness_AddsHalfAgain()
        {
            float multiplier = CombatMath.EffectivenessMultiplier(
                DamageType.Fire, ElementalAffinity.Of(DamageType.Fire, DamageType.Ice));
            Assert.AreEqual(1.5f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_MatchingResistance_HalvesDamage()
        {
            float multiplier = CombatMath.EffectivenessMultiplier(
                DamageType.Ice, ElementalAffinity.Of(DamageType.Fire, DamageType.Ice));
            Assert.AreEqual(0.5f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_NeitherMatch_IsNeutral()
        {
            float multiplier = CombatMath.EffectivenessMultiplier(
                DamageType.Arcane, ElementalAffinity.Of(DamageType.Fire, DamageType.Ice));
            Assert.AreEqual(1f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_SameTypeInBothFields_WeaknessWinsOverResistance()
        {
            // Not a valid authoring choice (an enemy shouldn't be weak to and
            // resistant to the same type), but the function still needs a
            // defined answer rather than an ambiguous one.
            float multiplier = CombatMath.EffectivenessMultiplier(
                DamageType.Fire, ElementalAffinity.Of(DamageType.Fire, DamageType.Fire));
            Assert.AreEqual(1.5f, multiplier);
        }

        // ---- several elements a side ---------------------------------------

        // The limit this whole type exists to remove. A forest troll that burns
        // AND freezes, and shrugs off blades AND venom, is now authorable, and
        // every one of those four answers has to come out of the same call.
        [Test]
        public void EffectivenessMultiplier_ScoresEveryElementInEitherSet()
        {
            var troll = ElementalAffinity.Of(
                new[] { DamageType.Fire, DamageType.Ice },
                new[] { DamageType.Physical, DamageType.Poison });

            Assert.AreEqual(1.5f, CombatMath.EffectivenessMultiplier(DamageType.Fire, troll), "first weakness");
            Assert.AreEqual(1.5f, CombatMath.EffectivenessMultiplier(DamageType.Ice, troll), "second weakness");
            Assert.AreEqual(0.5f, CombatMath.EffectivenessMultiplier(DamageType.Physical, troll), "first resistance");
            Assert.AreEqual(0.5f, CombatMath.EffectivenessMultiplier(DamageType.Poison, troll), "second resistance");
            Assert.AreEqual(1f, CombatMath.EffectivenessMultiplier(DamageType.Arcane, troll), "named by neither set");
            Assert.AreEqual(1f, CombatMath.EffectivenessMultiplier(DamageType.Nature, troll), "named by neither set");
        }

        // A LONGER LIST IS BROADER, NOT DEEPER, and this is the assertion that
        // stops someone "fixing" the multiplier into an accumulator later. Two
        // weaknesses means two elements that each deal 1.5x -- never 2.25x.
        [Test]
        public void EffectivenessMultiplier_DoesNotCompoundAcrossASetsOwnEntries()
        {
            var many = ElementalAffinity.Of(
                new[] { DamageType.Fire, DamageType.Ice, DamageType.Arcane },
                new[] { DamageType.Physical });

            Assert.AreEqual(1.5f, CombatMath.EffectivenessMultiplier(DamageType.Fire, many));
        }

        // default(ElementalAffinity) has to be the neutral answer, because that
        // is what every player-side combatant and every fixture with no
        // definition behind it gets -- see DamagePipeline.AfterDefences.
        [Test]
        public void EffectivenessMultiplier_AnEmptyAffinityIsNeutralToEverything()
        {
            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
            {
                Assert.AreEqual(1f, CombatMath.EffectivenessMultiplier(type, ElementalAffinity.Neutral),
                    $"{type} against nothing in particular");
                Assert.AreEqual(1f, CombatMath.EffectivenessMultiplier(type, default),
                    $"{type} against default(ElementalAffinity)");
            }
        }

        // A multi-element spell weighs each packet on its own. This is the
        // whole reason Frost Flare is two instances instead of one 50-point
        // "Frost" hit: against something that hates fire and shrugs off cold,
        // half the cast should land hard and the other half bounce, in the
        // same breath.
        //
        // PINNED. Fire 25 x 1.5 = 37.5 and Frost 25 x 0.5 = 12.5 are BOTH
        // exact ties, which is precisely where rounding conventions disagree
        // — UnityEngine.Mathf.RoundToInt rounds to even and would give
        // 38/12, CombatMath rounds away from zero and gives 38/13. The
        // literals below are the away-from-zero answers on purpose.
        [Test]
        public void AMultiElementSpell_ResolvesEachPacketAgainstTheTargetSeparately()
        {
            var affinity = ElementalAffinity.Of(DamageType.Fire, DamageType.Ice);

            int fire = CombatMath.ApplyEffectiveness(25,
                CombatMath.EffectivenessMultiplier(DamageType.Fire, affinity));
            int frost = CombatMath.ApplyEffectiveness(25,
                CombatMath.EffectivenessMultiplier(DamageType.Ice, affinity));
            int arcane = CombatMath.ApplyEffectiveness(50,
                CombatMath.EffectivenessMultiplier(DamageType.Arcane, affinity));

            Assert.AreEqual(38, fire, "Into the weakness: +50%");
            Assert.AreEqual(13, frost, "Into the resistance: -50%");
            Assert.AreEqual(50, arcane, "Neither: untouched");
            Assert.AreEqual(51, fire + frost, "Frost Flare's two halves against a fire-weak, cold-hardy target");
        }

        // Resisted is not immune, at any size.
        [Test]
        public void ApplyEffectiveness_NeverRoundsAPacketAwayToNothing()
        {
            Assert.AreEqual(1, CombatMath.ApplyEffectiveness(1, CombatMath.ResistanceMultiplier));
            Assert.AreEqual(1, CombatMath.ApplyEffectiveness(1, 0f));
        }

        // ---- Equipment-set resistance -------------------------------------
        //
        // reduction = R / (R + Softener). PINNED literals throughout —
        // nothing here recomputes the formula.

        [Test]
        public void AfterResistance_ZeroResistance_TouchesNothing()
        {
            Assert.AreEqual(100, CombatMath.AfterResistance(100, 0));
        }

        // The one number worth memorising about this curve: resistance equal
        // to the softener is exactly a 50% cut. Every set piece is tuned
        // against this landmark.
        [Test]
        public void AfterResistance_AtTheSoftenerValue_IsExactlyHalved()
        {
            Assert.AreEqual(100, CombatMath.ResistanceSoftener, "The rest of this test assumes the softener is 100");
            Assert.AreEqual(50, CombatMath.AfterResistance(100, 100));
        }

        [Test]
        public void AfterResistance_MoreResistance_ReducesMore_ButNeverToZero()
        {
            int lightlyArmoured = CombatMath.AfterResistance(1000, 20);
            int heavilyArmoured = CombatMath.AfterResistance(1000, 200);

            Assert.Less(heavilyArmoured, lightlyArmoured, "More resistance should always reduce more");
            Assert.Greater(heavilyArmoured, 0, "...but should never fully stop it, at any amount authored so far");
        }

        // A full five-piece set can plausibly reach ~85 points on this game's
        // numbers. That should read as meaningfully tougher, not as a rounding
        // error next to zero armour.
        [Test]
        public void AfterResistance_AFullSetsWorth_IsASubstantialReduction()
        {
            int reduced = CombatMath.AfterResistance(1000, 85);

            Assert.AreEqual(540, reduced, "1000 * 100 / 185, floored");
        }

        [Test]
        public void AfterResistance_FlooredAtOne_SoArmourNeverMakesATargetUnhittable()
        {
            Assert.AreEqual(1, CombatMath.AfterResistance(1, 100000));
        }

        [Test]
        public void AfterResistance_OfZeroDamage_StaysZero()
        {
            Assert.AreEqual(0, CombatMath.AfterResistance(0, 50));
        }

        // ---- Swift's dodge curve --------------------------------------------
        //
        // dodgePercent = R / (R + DodgeSoftener) -- the IDENTICAL shape as
        // AfterResistance above, just read as a probability. PINNED literals
        // throughout, same rule as the block above: nothing here recomputes
        // the formula. Floored, matching AfterResistance's own integer-
        // division rounding (see DodgePercentFrom's own header for why).

        [Test]
        public void DodgePercentFrom_ZeroRating_IsZero()
        {
            Assert.AreEqual(0, CombatMath.DodgePercentFrom(0));
        }

        [Test]
        public void DodgePercentFrom_NegativeRating_IsZero()
        {
            // A rating can never legitimately go negative in content, but a
            // defensive floor here matches AfterResistance's own <=0
            // short-circuit rather than letting a stray negative wrap into
            // undefined integer-division behaviour.
            Assert.AreEqual(0, CombatMath.DodgePercentFrom(-5));
        }

        // The one number worth memorising about this curve, same landmark
        // AfterResistance's own test calls out: rating equal to the softener
        // is exactly a 50% chance. The designer's own spec, verbatim: "100
        // dodge = 50% chance to dodge".
        [Test]
        public void DodgePercentFrom_AtTheSoftenerValue_IsExactlyHalf()
        {
            Assert.AreEqual(100, CombatMath.DodgeSoftener, "The rest of this test assumes the softener is 100");
            Assert.AreEqual(50, CombatMath.DodgePercentFrom(100));
        }

        [Test]
        public void DodgePercentFrom_50Rating_IsThirtyThreePercent()
        {
            Assert.AreEqual(33, CombatMath.DodgePercentFrom(50), "50 * 100 / 150 = 33.33..., floored");
        }

        [Test]
        public void DodgePercentFrom_200Rating_IsSixtySixPercent()
        {
            Assert.AreEqual(66, CombatMath.DodgePercentFrom(200), "200 * 100 / 300 = 66.66..., floored");
        }

        [Test]
        public void DodgePercentFrom_MoreRating_IncreasesTheChance_ButNeverReaches100()
        {
            int lightRating = CombatMath.DodgePercentFrom(20);
            int heavyRating = CombatMath.DodgePercentFrom(100000);

            Assert.Less(lightRating, heavyRating, "more rating should always increase the chance");
            Assert.Less(heavyRating, 100,
                "no finite rating may ever reach a literal 100 -- that is the entire point of this curve " +
                "existing (see DodgePercentFrom's own header): a maxed Swift item under the OLD direct-percent " +
                "reading could exceed 100 outright, an unavoidable guaranteed dodge, which this curve prevents");
        }

        [Test]
        public void IsPhysical_TrueOnlyForThePhysicalType()
        {
            Assert.IsTrue(CombatMath.IsPhysical(DamageType.Physical));
            Assert.IsFalse(CombatMath.IsPhysical(DamageType.Fire));
            Assert.IsFalse(CombatMath.IsPhysical(DamageType.Arcane));
        }

    }
}
