using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // EffectSet's own behaviour for affix entries, engine-free; the
    // cross-holder stacking table is EffectStackingTests'.
    public class ModifierEffectTests
    {
        private static EffectSet Set(params Effect[] effects)
        {
            return EffectSet.Of(EffectHolder.Affix, effects);
        }

        [Test]
        public void AnEmptySet_AnswersZeroAndFalseForEverything()
        {
            var empty = EffectSet.Empty;

            Assert.IsTrue(empty.IsEmpty);
            Assert.IsFalse(empty.Has(EffectKind.ManaToWardOnTurnStartPercent));
            Assert.AreEqual(0, empty.Magnitude(EffectKind.DodgeRating));
        }

        // Constructing with null must behave exactly like Empty rather than
        // throwing — EffectSet's constructor makes the same promise,
        // and CombatantState.Effects leans on it never needing a
        // null-check at any call site.
        [Test]
        public void ConstructedWithNull_BehavesLikeEmpty_NotNullReferenceError()
        {
            var set = EffectSet.Of(EffectHolder.Affix, null);

            Assert.IsTrue(set.IsEmpty);
            Assert.IsFalse(set.Has(EffectKind.LifestealPercent));
            Assert.AreEqual(0, set.Magnitude(EffectKind.LifestealPercent));
        }

        // Empty really is ONE shared instance, not a fresh empty list built
        // per call — the point of the static field existing at all (see its
        // own comment: "the common case allocates nothing").
        [Test]
        public void EmptyIsTheSameSharedInstanceEveryTime()
        {
            Assert.AreSame(EffectSet.Empty, EffectSet.Empty);
        }

        // MAX, NOT SUM, between two affix copies: two Swift rolls on one
        // combatant must not add up.
        [Test]
        public void RepeatedRules_TakeTheStrongest_NotTheSum()
        {
            var set = Set(
                new Effect(EffectKind.DodgeRating, 5),
                new Effect(EffectKind.DodgeRating, 8));

            Assert.AreEqual(8, set.Magnitude(EffectKind.DodgeRating));
        }

        [Test]
        public void Has_IsTrueRegardlessOfMagnitude_ForAFlagShapedMember()
        {
            var set = Set(new Effect(EffectKind.ManaToWardOnTurnStartPercent, 0));

            Assert.IsTrue(set.Has(EffectKind.ManaToWardOnTurnStartPercent));
        }

        [Test]
        public void UnrelatedTypesDoNotAnswerForEachOther()
        {
            var set = Set(new Effect(EffectKind.LifestealPercent, 10));

            Assert.IsFalse(set.Has(EffectKind.ElementalDamageOnHitPercent));
            Assert.AreEqual(0, set.Magnitude(EffectKind.ElementalDamageOnHitPercent));
        }

        // ResistanceFlat's Against/AgainstMagical selector survives the round
        // trip through the set and stays queryable via .All, which is what
        // EffectFold.ApplyResistance folds per element.
        [Test]
        public void TypedResistance_CarriesItsElementThroughAll()
        {
            var set = Set(
                new Effect(EffectKind.ResistanceFlat, 10, against: DamageType.Fire),
                new Effect(EffectKind.ResistanceFlat, 15, against: DamageType.Ice));

            Assert.AreEqual(2, set.All.Count(e => e.Kind == EffectKind.ResistanceFlat));
            Assert.AreEqual(10, set.All.Single(e => e.Against == DamageType.Fire).Magnitude);
            Assert.AreEqual(15, set.All.Single(e => e.Against == DamageType.Ice).Magnitude);

            // Magnitude sums every copy across elements (EffectStacking.Summed),
            // which is why the real consumer folds .All per element instead.
            Assert.AreEqual(25, set.Magnitude(EffectKind.ResistanceFlat));
        }

        [Test]
        public void AgainstMagical_IsAValidTargetDistinctFromAnySpecificElement()
        {
            var effect = new Effect(EffectKind.ResistanceFlat, 12, againstMagical: true);

            Assert.IsTrue(effect.AgainstMagical);
            Assert.IsFalse(effect.Against.HasValue);
        }
    }
}
