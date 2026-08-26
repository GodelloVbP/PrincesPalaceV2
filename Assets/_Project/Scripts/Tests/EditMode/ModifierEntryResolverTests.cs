using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class ModifierEntryResolverTests
    {
        private static RawModifierEntry Modifier(string id = "fiery_test", params RawModifierEffect[] effects)
        {
            return new RawModifierEntry { id = id, displayName = "Test Modifier", effects = effects };
        }

        private static RawModifierEffect Effect(string type, int magnitude = 0, int threshold = 0, string damageType = "")
        {
            return new RawModifierEffect { type = type, magnitude = magnitude, threshold = threshold, damageType = damageType };
        }

        [Test]
        public void AValidEntry_ResolvesWithItsEffectIntact()
        {
            var entry = Modifier("fiery_test", Effect("ElementalDamageOnHitPercent", magnitude: 15, damageType: "Fire"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("fiery_test", resolved[0].Id);
            Assert.AreEqual(1, resolved[0].Effects.Count);
            Assert.AreEqual(ModifierEffectType.ElementalDamageOnHitPercent, resolved[0].Effects[0].Type);
            Assert.AreEqual(15, resolved[0].Effects[0].Magnitude);
            Assert.AreEqual(DamageType.Fire, resolved[0].Effects[0].Against);
        }

        [Test]
        public void EffectTypeParsing_IsCaseInsensitive()
        {
            var entry = Modifier("swift_test", Effect("flatspeedbonus", magnitude: 5));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(ModifierEffectType.FlatSpeedBonus, resolved[0].Effects[0].Type);
        }

        [Test]
        public void UnknownEffectType_IsRejectedWithAClearError()
        {
            var entry = Modifier("bogus_test", Effect("NotARealEffect", magnitude: 5));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("isn't a modifier effect", errors[0]);
        }

        // "None" is the resolver's own sentinel for "the author typo'd this"
        // (see ModifierEffectType.None's own comment) — authoring it directly
        // must be rejected the same way a genuine typo is, not silently
        // accepted as a real effect that grants nothing.
        [Test]
        public void ExplicitNone_IsRejectedTheSameWayATypoIs()
        {
            var entry = Modifier("none_test", Effect("None"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
        }

        [Test]
        public void MalformedMagnitude_OnANonFlagMember_IsRejected()
        {
            var entry = Modifier("weak_test", Effect("FlatSpeedBonus", magnitude: 0));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a positive magnitude", errors[0]);
        }

        [Test]
        public void MagnitudeOnAFlagShapedMember_IsRejected()
        {
            var entry = Modifier("flag_test", Effect("GuaranteedFirstAction", magnitude: 3));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("is a flag", errors[0]);
        }

        [Test]
        public void FlagShapedMember_WithNoMagnitude_ResolvesCleanly()
        {
            var entry = Modifier("flag_test", Effect("GuaranteedFirstAction"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void ThresholdOnAMemberWithNoHealthGate_IsRejected()
        {
            var entry = Modifier("bad_threshold_test", Effect("FlatSpeedBonus", magnitude: 5, threshold: 50));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("has no health gate", errors[0]);
        }

        // ---- TypedResistanceFlat's damageType side-channel -------------------

        [Test]
        public void TypedResistance_WithoutADamageType_IsRejected()
        {
            var entry = Modifier("naked_resist_test", Effect("TypedResistanceFlat", magnitude: 10));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("names damageType", errors[0]);
        }

        [Test]
        public void TypedResistance_WithARealDamageType_ResolvesAgainstIt()
        {
            var entry = Modifier("fire_resist_test", Effect("TypedResistanceFlat", magnitude: 10, damageType: "Fire"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(DamageType.Fire, resolved[0].Effects[0].Against);
            Assert.IsFalse(resolved[0].Effects[0].AgainstMagical);
        }

        [Test]
        public void TypedResistance_WithMagicalShorthand_SetsTheFlagInsteadOfAnElement()
        {
            var entry = Modifier("magical_resist_test", Effect("TypedResistanceFlat", magnitude: 10, damageType: "magical"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(resolved[0].Effects[0].AgainstMagical);
            Assert.IsFalse(resolved[0].Effects[0].Against.HasValue);
        }

        [Test]
        public void TypedResistance_WithAnUnknownDamageTypeName_IsRejected()
        {
            var entry = Modifier("bad_element_test", Effect("TypedResistanceFlat", magnitude: 10, damageType: "Shadow"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("isn't a damage type or 'magical'", errors[0]);
        }

        [Test]
        public void DamageTypeOnANonResistanceMember_IsRejected()
        {
            var entry = Modifier("confused_test", Effect("FlatSpeedBonus", magnitude: 5, damageType: "Fire"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("names a damageType, which only", errors[0]);
        }

        // ElementalDamageOnHitPercent shares TypedResistanceFlat's
        // damageType side-channel but NOT its "magical" shorthand — a hit
        // is dealt AS one element, not as "something not physical".
        [Test]
        public void ElementalDamageOnHit_RejectsTheMagicalShorthand()
        {
            var entry = Modifier("confused_elemental_test",
                Effect("ElementalDamageOnHitPercent", magnitude: 10, damageType: "magical"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("isn't a damage type", errors[0]);
        }

        [Test]
        public void ElementalDamageOnHit_WithoutADamageType_IsRejected()
        {
            var entry = Modifier("naked_elemental_test", Effect("ElementalDamageOnHitPercent", magnitude: 10));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("names damageType", errors[0]);
        }

        // Two DIFFERENT elements on one modifier is allowed -- it is the
        // whole point of the (Type, Against, AgainstMagical) grouping key
        // rather than plain Type (see ModifierEntryResolver's own comment).
        [Test]
        public void TwoDifferentResistanceElementsOnOneModifier_AreBothKept()
        {
            var entry = Modifier("dual_resist_test",
                Effect("TypedResistanceFlat", magnitude: 10, damageType: "Fire"),
                Effect("TypedResistanceFlat", magnitude: 8, damageType: "Ice"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(2, resolved[0].Effects.Count);
        }

        // The SAME element twice is dead weight -- ModifierEffectSet takes
        // the strongest, so the weaker entry does nothing.
        [Test]
        public void TheSameResistanceElementTwice_IsRejected()
        {
            var entry = Modifier("dupe_resist_test",
                Effect("TypedResistanceFlat", magnitude: 10, damageType: "Fire"),
                Effect("TypedResistanceFlat", magnitude: 8, damageType: "Fire"));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("only the strongest would ever apply", errors[0]);
        }

        [Test]
        public void ADuplicateNonResistanceType_IsRejected()
        {
            var entry = Modifier("dupe_speed_test",
                Effect("FlatSpeedBonus", magnitude: 5),
                Effect("FlatSpeedBonus", magnitude: 8));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("only the strongest would ever apply", errors[0]);
        }

        [Test]
        public void AModifierWithNoEffectsAtAll_IsRejected()
        {
            var entry = Modifier("empty_test");

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("grants no effects at all", errors[0]);
        }

        [Test]
        public void MissingId_IsRejected()
        {
            var entry = Modifier("", Effect("FlatSpeedBonus", magnitude: 5));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("id is required", errors[0]);
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            var a = Modifier("dup", Effect("FlatSpeedBonus", magnitude: 5));
            var b = Modifier("dup", Effect("LifestealPercent", magnitude: 10));

            bool ok = ModifierEntryResolver.TryResolveAll(new List<RawModifierEntry> { a, b }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Duplicate modifier id", errors[0]);
        }
    }
}
