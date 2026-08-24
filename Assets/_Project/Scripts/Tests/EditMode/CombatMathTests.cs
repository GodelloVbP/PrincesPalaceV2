using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class CombatMathTests
    {
        private static CombatantState MakeCombatant(int attack = 5, int defense = 2, int maxHealth = 20, int maxMana = 10, int speed = 5)
        {
            return new CombatantState("Test", true, maxHealth, maxMana, attack, defense, speed);
        }

        [Test]
        public void ComputeAttackDamage_SubtractsDefenseFromAttack()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(defense: 3);

            // 10 attack - 3 defense = 7, x5 for the health scale (CombatMath.DamageScale).
            Assert.AreEqual(35, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void ComputeAttackDamage_NeverGoesBelowTheFlooredMinimum()
        {
            var attacker = MakeCombatant(attack: 2);
            var target = MakeCombatant(defense: 50);

            // Floored at 1 BEFORE the scale, so the worst case is a scaled 5
            // rather than a scaled 0 — defense can never make a target
            // unhittable, at either end of the multiplication.
            Assert.AreEqual(5, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void ComputeSkillDamage_HitsHarderThanABasicAttack()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(defense: 3);

            int attackDamage = CombatMath.ComputeAttackDamage(attacker, target);
            int skillDamage = CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f);

            Assert.Greater(skillDamage, attackDamage);
        }

        // ---- EffectiveDefense / Break -------------------------------------

        [Test]
        public void EffectiveDefense_NoBreakShield_IsJustDefense()
        {
            var target = MakeCombatant(defense: 7);
            Assert.AreEqual(7, CombatMath.EffectiveDefense(target));
        }

        [Test]
        public void EffectiveDefense_BreakShieldPresentButNotBroken_IsStillJustDefense()
        {
            var target = MakeCombatant(defense: 7);
            target.BreakShield = new BreakShield(10);

            Assert.AreEqual(7, CombatMath.EffectiveDefense(target));
        }

        [Test]
        public void EffectiveDefense_Broken_IsZero()
        {
            var target = MakeCombatant(defense: 7);
            target.BreakShield = new BreakShield(3);
            target.BreakShield.Deplete(3);

            Assert.IsTrue(target.BreakShield.IsBroken, "Sanity check on the fixture");
            Assert.AreEqual(0, CombatMath.EffectiveDefense(target));
        }

        [Test]
        public void EffectiveDefense_NullTarget_IsZeroRatherThanThrowing()
        {
            Assert.AreEqual(0, CombatMath.EffectiveDefense(null));
        }

        // The whole point: a broken target actually takes more damage than
        // the same hit would deal against it undamaged, through the same
        // ComputeAttackDamage formula every other attack uses — no special
        // "Break bonus damage" branch anywhere in the pipeline.
        [Test]
        public void ComputeAttackDamage_AgainstABrokenTarget_IgnoresDefenseEntirely()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(defense: 8);
            target.BreakShield = new BreakShield(5);
            target.BreakShield.Deplete(5);

            // 10 attack - 0 effective defense = 10, x5 for the health scale.
            Assert.AreEqual(50, CombatMath.ComputeAttackDamage(attacker, target));
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
        public void ACombatantWithNoGrades_HitsForExactlyWhatItAlwaysDid()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 13, speed: 10);

            // Unchanged from ComputeAttackDamage_SubtractsDefenseFromAttack.
            Assert.AreEqual(35, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void WeaponScaling_MultipliesABasicAttack()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 10, speed: 10);

            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // S at Strength 20 is 2.00x: 20 attack becomes 40, less 10
            // defense, times the x5 damage scale.
            Assert.AreEqual(150, CombatMath.ComputeAttackDamage(attacker, target));
        }

        // Scaling is folded into the ATTACK, before defense — so armour keeps
        // working against exactly the weapons it most needs to blunt.
        [Test]
        public void Scaling_IsAppliedBeforeDefenseRatherThanToTheFinishedFigure()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 10, speed: 10);

            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // 150, not the 100 that doubling (20 - 10) * 5 would give.
            Assert.AreEqual(150, CombatMath.ComputeAttackDamage(attacker, target));
        }

        [Test]
        public void AWeaponTheWielderIsWrongFor_HitsSofterThanNoScalingAtAll()
        {
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 5, speed: 10);

            var matched = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            matched.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);
            matched.AbilityScores = new AbilityScoreBlock(18, 10, 10, 10, 10, 10);

            var mismatched = new CombatantState("C", true, 100, 0, attack: 20, defense: 0, speed: 10);
            mismatched.WeaponScaling = matched.WeaponScaling;
            mismatched.AbilityScores = new AbilityScoreBlock(6, 10, 10, 10, 10, 10);

            var plain = new CombatantState("D", true, 100, 0, attack: 20, defense: 0, speed: 10);

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
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 0, speed: 10);

            attacker.WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            attacker.SkillScaling = ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S);
            // Strong, and not at all clever.
            attacker.AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // The swing gets the Strength: 20 * 2.00 = 40, x5.
            Assert.AreEqual(200, CombatMath.ComputeAttackDamage(attacker, target));
            // The cast does not: Intelligence is neutral, so only the tier's
            // 1.5x applies. 20 * 1.5 = 30, x5.
            Assert.AreEqual(150, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        [Test]
        public void SkillScaling_MultipliesAlongsideTheTierRatherThanReplacingIt()
        {
            var attacker = new CombatantState("A", true, 100, 0, attack: 20, defense: 0, speed: 10);
            var target = new CombatantState("B", false, 100, 0, attack: 0, defense: 0, speed: 10);

            attacker.SkillScaling = ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S);
            attacker.AbilityScores = new AbilityScoreBlock(10, 10, 10, 10, 20, 10);

            // 2.00x scaling on top of the tier's 1.5x: 20 * 3.0 = 60, x5.
            Assert.AreEqual(300, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        [Test]
        public void ComputeSkillDamage_NeverGoesBelowTheFlooredMinimum()
        {
            var attacker = MakeCombatant(attack: 1);
            var target = MakeCombatant(defense: 50);

            Assert.AreEqual(5, CombatMath.ComputeSkillDamage(attacker, target, powerMultiplier: 1.5f));
        }

        [Test]
        public void ComputeSkillDamage_HigherPowerMultiplier_DealsMoreDamage()
        {
            var attacker = MakeCombatant(attack: 10);
            var target = MakeCombatant(defense: 3);

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
            float multiplier = CombatMath.EffectivenessMultiplier(DamageType.Fire, weakness: DamageType.Fire, resistance: DamageType.Ice);
            Assert.AreEqual(1.5f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_MatchingResistance_HalvesDamage()
        {
            float multiplier = CombatMath.EffectivenessMultiplier(DamageType.Ice, weakness: DamageType.Fire, resistance: DamageType.Ice);
            Assert.AreEqual(0.5f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_NeitherMatch_IsNeutral()
        {
            float multiplier = CombatMath.EffectivenessMultiplier(DamageType.Arcane, weakness: DamageType.Fire, resistance: DamageType.Ice);
            Assert.AreEqual(1f, multiplier);
        }

        [Test]
        public void EffectivenessMultiplier_SameTypeInBothFields_WeaknessWinsOverResistance()
        {
            // Not a valid authoring choice (an enemy shouldn't be weak to and
            // resistant to the same type), but the function still needs a
            // defined answer rather than an ambiguous one.
            float multiplier = CombatMath.EffectivenessMultiplier(DamageType.Fire, weakness: DamageType.Fire, resistance: DamageType.Fire);
            Assert.AreEqual(1.5f, multiplier);
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
            const DamageType weakness = DamageType.Fire;
            const DamageType resistance = DamageType.Ice;

            int fire = CombatMath.ApplyEffectiveness(25,
                CombatMath.EffectivenessMultiplier(DamageType.Fire, weakness, resistance));
            int frost = CombatMath.ApplyEffectiveness(25,
                CombatMath.EffectivenessMultiplier(DamageType.Ice, weakness, resistance));
            int arcane = CombatMath.ApplyEffectiveness(50,
                CombatMath.EffectivenessMultiplier(DamageType.Arcane, weakness, resistance));

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

        [Test]
        public void IsPhysical_TrueOnlyForThePhysicalType()
        {
            Assert.IsTrue(CombatMath.IsPhysical(DamageType.Physical));
            Assert.IsFalse(CombatMath.IsPhysical(DamageType.Fire));
            Assert.IsFalse(CombatMath.IsPhysical(DamageType.Arcane));
        }

        [Test]
        public void ResistanceAgainst_PhysicalDamage_ReadsPhysicalResistance()
        {
            var target = MakeCombatant();
            target.PhysicalResistance = 12;
            target.MagicalResistance = 40;

            Assert.AreEqual(12, CombatMath.ResistanceAgainst(target, DamageType.Physical));
        }

        [Test]
        public void ResistanceAgainst_AnyElement_ReadsMagicalResistance()
        {
            var target = MakeCombatant();
            target.PhysicalResistance = 12;
            target.MagicalResistance = 40;

            Assert.AreEqual(40, CombatMath.ResistanceAgainst(target, DamageType.Fire));
            Assert.AreEqual(40, CombatMath.ResistanceAgainst(target, DamageType.Ice));
            Assert.AreEqual(40, CombatMath.ResistanceAgainst(target, DamageType.Arcane));
        }

        [Test]
        public void ResistanceAgainst_NullTarget_IsZero()
        {
            Assert.AreEqual(0, CombatMath.ResistanceAgainst(null, DamageType.Physical));
        }
    }
}
