using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The weapon-driven damage model -- balance redesign Phase 3 (D3).
    //
    //     WeaponPower (WP) = round(attackAtTier x (1 + 0.15 x plus))
    //     Scaling Multiplier (M) = 1 + PerPoint(grade) x (score - 10)
    //     raw = AwayFromZero(WP x M)
    //
    // Every literal here is HAND-COMPUTED, never the production formula run
    // again (CLAUDE.md gotcha 5). Fixture: a sword at tier 5 (attackAtTier
    // 45, the T5 endpoint WeaponEntryResolverTests pins independently),
    // grade A on Strength (per-point 0.07), a wielder with Strength 20.
    public class WeaponDamageTests
    {
        private const int SwordT5AttackAtTier = 45;

        private static CombatantState Wielder(int attack, ScalingGrade gradeOnStrength)
        {
            var combatant = new CombatantState("Wielder", true, 100, 10, attack, 10)
            {
                WeaponScaling = ScalingProfile.None.With(AbilityScore.Strength, gradeOnStrength),
                AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10),
            };
            return combatant;
        }

        // ---- WeaponPower: attackAtTier x hone, rounded to nearest --------

        [Test]
        public void WeaponPower_AtPlusZero_IsExactlyTheTierEndpoint()
        {
            Assert.AreEqual(45, WeaponPower.Compute(SwordT5AttackAtTier, 0));
        }

        [Test]
        public void WeaponPower_AtPlusTen_Is2_5xRoundedToNearest()
        {
            // round(45 x (1 + 0.15 x 10)) = round(45 x 2.5) = round(112.5) = 113.
            // Already a whole number times 2.5 -- no rounding ambiguity, but
            // pinned as the exact literal WeaponDamageTests downstream (the
            // raw-damage tests below) build on.
            Assert.AreEqual(113, WeaponPower.Compute(SwordT5AttackAtTier, 10));
        }

        [Test]
        public void WeaponPower_HalfIntegerTie_RoundsAwayFromZero()
        {
            // The sword T0 endpoint (10) at +1: 10 x (1 + 0.15 x 1) = 11.5
            // exactly, the tie case AwayFromZero exists to pin -- a positive
            // .5 rounds UP, per Rounding's own header.
            Assert.AreEqual(12, WeaponPower.Compute(10, 1));
        }

        // ---- raw = AwayFromZero(WP x M), via the real production seam ----
        //
        // CombatMath.ScaledAttack is internal to the Domain assembly and
        // this suite runs from a separate one (PrincesPalace.Domain.Tests
        // has no InternalsVisibleTo grant, unlike PrincesPalace.Editor), so
        // this goes through ComputeAttackDamage instead -- the public
        // function that already IS exactly Math.Max(1, ScaledAttack(...)),
        // per its own header (fixed 2026-08-26 -- CombatMath.DamageScale's
        // x5 no longer applies here; see ComputeAttackDamage's own header
        // for why). So the expected figure IS the hand-computed raw, not the
        // production formula rerun. `target` is unread by ComputeAttackDamage
        // (see its own header) so null is safe.

        [Test]
        public void RawBasicAttack_SwordT5_PlusZero_GradeA_Strength20()
        {
            var wielder = Wielder(WeaponPower.Compute(SwordT5AttackAtTier, 0), ScalingGrade.A);

            // M = 1 + 0.07 x (20 - 10) = 1.7. WP = 45.
            // raw = AwayFromZero(45 x 1.7) = AwayFromZero(76.5) = 77.
            // ComputeAttackDamage = max(1, 77) = 77.
            Assert.AreEqual(77, CombatMath.ComputeAttackDamage(wielder, null));
        }

        [Test]
        public void RawBasicAttack_SwordT5_PlusTen_GradeA_Strength20()
        {
            var wielder = Wielder(WeaponPower.Compute(SwordT5AttackAtTier, 10), ScalingGrade.A);

            // WP = 113 (pinned above). raw = AwayFromZero(113 x 1.7)
            // = AwayFromZero(192.1) = 192.
            // ComputeAttackDamage = max(1, 192) = 192.
            Assert.AreEqual(192, CombatMath.ComputeAttackDamage(wielder, null));
        }

        // ---- DisplayDamage: the weapon card's DMG number (D7.2) --------------
        //
        // Same fixture as the raw-basic-attack pair above, but read through
        // WeaponPower.DisplayDamage rather than a real CombatantState -- this
        // is the tooltip's path, which has an ItemDefinition's own
        // ScalingProfile and a viewer's AbilityScoreBlock in hand, not a
        // resolved combatant.

        [Test]
        public void DisplayDamage_SwordT5_PlusZero_GradeA_Strength20()
        {
            var scaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);
            var viewer = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);

            // Same arithmetic as RawBasicAttack_SwordT5_PlusZero_GradeA_Strength20:
            // WP = 45, M = 1.7, AwayFromZero(45 x 1.7) = 77.
            Assert.AreEqual(77, WeaponPower.DisplayDamage(WeaponPower.Compute(SwordT5AttackAtTier, 0), scaling, viewer));
        }

        [Test]
        public void DisplayDamage_AWeaponTheViewerIsBadlyMatchedFor_StillHitsForSomething()
        {
            // The floor ScalingProfile.MultiplierFor itself guarantees
            // (MinimumMultiplier, 0.25x) -- this just confirms DisplayDamage
            // does not add a second floor on top and disagree with it.
            var scaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            var viewer = new AbilityScoreBlock(0, 10, 10, 10, 10, 10);

            // M floors at 0.25 regardless of how far Strength 0 would
            // otherwise push it. AwayFromZero(45 x 0.25) = AwayFromZero(11.25) = 11.
            Assert.AreEqual(11, WeaponPower.DisplayDamage(SwordT5AttackAtTier, scaling, viewer));
        }
    }
}
