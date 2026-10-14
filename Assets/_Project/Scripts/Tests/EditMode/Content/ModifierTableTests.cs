using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // The which-modifiers pick and the weapon/armour affix-pool split.
    public class ModifierTableTests
    {
        // ---- PickModifiers -------------------------------------------------

        private static readonly List<string> FivePool = new List<string> { "a", "b", "c", "d", "e" };

        [Test]
        public void PicksExactlyTheRequestedSlotCount()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 3, _ => 0);
            Assert.AreEqual(3, picked.Count);
        }

        [Test]
        public void PickedModifiersAreAlwaysDistinct()
        {
            for (int calls = 0; calls < 5; calls++)
            {
                int n = calls;
                Func<int, int> walking = bound => (n++) % Math.Max(1, bound);
                var picked = ModifierTable.PickModifiers(FivePool, 3, walking);

                CollectionAssert.AllItemsAreUnique(picked);
            }
        }

        // Removal-based selection, pinned: always taking index 0 out of a
        // shrinking pool walks the pool in order. Not a recomputed formula --
        // this is the definition of "always pick the first remaining one".
        [Test]
        public void AlwaysPickingTheFirstRemaining_WalksThePoolInOrder()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 3, _ => 0);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, picked);
        }

        [Test]
        public void ZeroSlots_PicksNothing()
        {
            Assert.IsEmpty(ModifierTable.PickModifiers(FivePool, 0, _ => 0));
        }

        [Test]
        public void RequestingMoreSlotsThanThePool_ReturnsTheWholePoolRatherThanRepeating()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 10, _ => 0);

            Assert.AreEqual(FivePool.Count, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
        }

        [Test]
        public void AnEmptyOrMissingPool_IsHandledRatherThanThrowing()
        {
            Assert.IsEmpty(ModifierTable.PickModifiers(new List<string>(), 3, _ => 0));
            Assert.IsEmpty(ModifierTable.PickModifiers(null, 3, _ => 0));
        }

        [Test]
        public void MissingRandomness_PicksNothing()
        {
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, null));
            Assert.IsEmpty(ModifierTable.PickModifiers(FivePool, 3, null));
        }

        [Test]
        public void OutOfRangeRoll_IsClampedRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, _ => 9999));
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, _ => -5));
        }

        // ---- IsOffensiveModifier: the weapon/armour affix-pool split --------
        //
        // A staff (or any weapon) rolling a defensive affix -- Emberguard's
        // fire resistance, Wardrune's mana-to-Ward -- is the bug this
        // classifier exists to close. Pinned member-by-member rather than
        // sampled, because this is a closed enum and every member's category
        // is a deliberate authoring decision (see ModifierTable's own
        // header), not a distribution to measure.
        [Test]
        public void EveryOnHitOrAttackRider_IsOffensive()
        {
            var offensive = new[]
            {
                EffectKind.ElementalDamageOnHitPercent,
                EffectKind.LifestealPercent,
                EffectKind.OnKillSplashPercent,
                EffectKind.PushBackOnHitChancePercent,
                EffectKind.NextSkillManaDiscountPercent,
                EffectKind.ChilledOnHitChancePercent,
                EffectKind.RootChancePercent,
            };

            foreach (var type in offensive)
            {
                Assert.IsTrue(ModifierTable.IsOffensiveModifier(type), $"{type} should be weapon-eligible");
            }
        }

        [Test]
        public void EveryPassiveOrResistanceRule_IsDefensive()
        {
            var defensive = new[]
            {
                EffectKind.ResistanceFlat,
                EffectKind.FlatPhysicalDamageReduction,
                EffectKind.BreakShieldDepletionResistPercent,
                EffectKind.MaxManaFlat,
                EffectKind.FlatManaRegenBonus,
                EffectKind.ManaToWardOnTurnStartPercent,
                EffectKind.FortunateFavorBonusFlat,
                EffectKind.DodgeRating,
            };

            foreach (var type in defensive)
            {
                Assert.IsFalse(ModifierTable.IsOffensiveModifier(type), $"{type} should be armour-eligible, not weapon-eligible");
            }
        }

        [Test]
        public void EveryAffixKindIsClassifiedOneWayOrTheOther()
        {
            // Closed vocabulary, closed classification: a future affix kind
            // must be sorted into one bucket or the other by hand, not fall
            // through to a silent default that could land a new offensive
            // rider on armour or vice versa.
            var covered = new HashSet<EffectKind>
            {
                EffectKind.ElementalDamageOnHitPercent,
                EffectKind.ElementalDamagePercent,
                EffectKind.LifestealPercent,
                EffectKind.OnKillSplashPercent,
                EffectKind.PushBackOnHitChancePercent,
                EffectKind.NextSkillManaDiscountPercent,
                EffectKind.ChilledOnHitChancePercent,
                EffectKind.RootChancePercent,
                EffectKind.ResistanceFlat,
                EffectKind.FlatPhysicalDamageReduction,
                EffectKind.BreakShieldDepletionResistPercent,
                EffectKind.MaxManaFlat,
                EffectKind.FlatManaRegenBonus,
                EffectKind.ManaToWardOnTurnStartPercent,
                EffectKind.FortunateFavorBonusFlat,
                EffectKind.DodgeRating,
            };

            foreach (var info in EffectCatalog.All)
            {
                if (!info.Allows(EffectHolder.Affix)) continue;

                Assert.IsTrue(covered.Contains(info.Kind),
                    $"{info.Kind} is a new affix kind with no offensive/defensive classification -- " +
                    "give it an EffectCatalog pool, and add it to this test.");
            }
        }
    }
}
