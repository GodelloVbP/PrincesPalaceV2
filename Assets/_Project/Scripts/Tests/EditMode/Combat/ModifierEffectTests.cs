using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The item-modifier effect vocabulary, at the Domain layer — the gear
    // half of TalentEffectTests, mirroring its structure exactly (see that
    // file's own header for why this layer is tested away from
    // FightController: engine-free and reusable from EditMode).
    //
    // Phase A2 only: nothing here is read by a real combat hook yet (that is
    // Phase C's job) — these tests cover the BAG's own behaviour, the same
    // scope TalentEffectTests covers for TalentEffectSet.
    public class ModifierEffectTests
    {
        private static ModifierEffectSet Set(params ModifierEffect[] effects)
        {
            return new ModifierEffectSet(effects);
        }

        [Test]
        public void AnEmptySet_AnswersZeroAndFalseForEverything()
        {
            var empty = ModifierEffectSet.Empty;

            Assert.IsTrue(empty.IsEmpty);
            Assert.IsFalse(empty.Has(ModifierEffectType.GuaranteedFirstAction));
            Assert.AreEqual(0, empty.Best(ModifierEffectType.FlatSpeedBonus));
        }

        // Constructing with null must behave exactly like Empty rather than
        // throwing — TalentEffectSet's constructor makes the same promise,
        // and CombatantState.ModifierEffects leans on it never needing a
        // null-check at any call site.
        [Test]
        public void ConstructedWithNull_BehavesLikeEmpty_NotNullReferenceError()
        {
            var set = new ModifierEffectSet(null);

            Assert.IsTrue(set.IsEmpty);
            Assert.IsFalse(set.Has(ModifierEffectType.LifestealPercent));
            Assert.AreEqual(0, set.Best(ModifierEffectType.LifestealPercent));
        }

        // Empty really is ONE shared instance, not a fresh empty list built
        // per call — the point of the static field existing at all (see its
        // own comment: "the common case allocates nothing").
        [Test]
        public void EmptyIsTheSameSharedInstanceEveryTime()
        {
            Assert.AreSame(ModifierEffectSet.Empty, ModifierEffectSet.Empty);
        }

        // MAX, NOT SUM — the single most load-bearing rule this class
        // borrows from TalentEffectSet. Two Swift-shaped modifiers on the
        // same combatant (a plausible drop even before Phase A3's roll
        // exists to make it real) must not add up.
        [Test]
        public void RepeatedRules_TakeTheStrongest_NotTheSum()
        {
            var set = Set(
                new ModifierEffect(ModifierEffectType.FlatSpeedBonus, 5),
                new ModifierEffect(ModifierEffectType.FlatSpeedBonus, 8));

            Assert.AreEqual(8, set.Best(ModifierEffectType.FlatSpeedBonus));
        }

        [Test]
        public void Has_IsTrueRegardlessOfMagnitude_ForAFlagShapedMember()
        {
            var set = Set(new ModifierEffect(ModifierEffectType.GuaranteedFirstAction, 0));

            Assert.IsTrue(set.Has(ModifierEffectType.GuaranteedFirstAction));
        }

        [Test]
        public void UnrelatedTypesDoNotAnswerForEachOther()
        {
            var set = Set(new ModifierEffect(ModifierEffectType.LifestealPercent, 10));

            Assert.IsFalse(set.Has(ModifierEffectType.ElementalDamageOnHitPercent));
            Assert.AreEqual(0, set.Best(ModifierEffectType.ElementalDamageOnHitPercent));
        }

        // TypedResistanceFlat's Against/AgainstMagical side-channel survives
        // the round trip through the set and stays queryable via .All — the
        // escape hatch ModifierEffectSet's own header points a future
        // resistance consumer at instead of Best(), which collapses every
        // element into one number.
        [Test]
        public void TypedResistance_CarriesItsElementThroughAll()
        {
            var set = Set(
                new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 10, against: DamageType.Fire),
                new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 15, against: DamageType.Ice));

            Assert.AreEqual(2, set.All.Count(e => e.Type == ModifierEffectType.TypedResistanceFlat));
            Assert.AreEqual(10, set.All.Single(e => e.Against == DamageType.Fire).Magnitude);
            Assert.AreEqual(15, set.All.Single(e => e.Against == DamageType.Ice).Magnitude);

            // Best() DELIBERATELY collapses across elements — documented
            // behaviour, not a gap: it answers "the strongest resistance to
            // ANY element", which is why a real consumer must read .All
            // instead (see ModifierEffectSet.Best's own comment).
            Assert.AreEqual(15, set.Best(ModifierEffectType.TypedResistanceFlat));
        }

        [Test]
        public void AgainstMagical_IsAValidTargetDistinctFromAnySpecificElement()
        {
            var effect = new ModifierEffect(ModifierEffectType.TypedResistanceFlat, 12, againstMagical: true);

            Assert.IsTrue(effect.AgainstMagical);
            Assert.IsFalse(effect.Against.HasValue);
        }
    }
}
