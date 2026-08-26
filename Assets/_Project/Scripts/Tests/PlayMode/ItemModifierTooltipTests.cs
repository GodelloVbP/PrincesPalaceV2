using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // ITEM-MODIFIER PLAN PHASE E: the UI layer's own display pipeline,
    // exercised end to end against the REAL "fiery" modifiers.json entry --
    // never a hand-built ModifierEffect fixture, the same posture
    // FrostyModifierReachesCombatTests/ItemModifierScalingReachesCombatTests
    // already take for Phase C/D's combat hooks.
    //
    // PlayMode only because ContentDatabase needs Resources to load from.
    public class ItemModifierTooltipTests
    {
        private static ItemDefinition AnyEquippable() =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable);

        // Every expected magnitude here is computed by calling the SAME
        // production ModifierMagnitude.Scale/Rounding.AwayFromZero the
        // display pipeline itself calls (ContentDatabase.ModifierEffectsForItem)
        // -- per the item-modifier plan's own test note, this is the one
        // case reusing the production formula in a test is correct, because
        // what is under test is whether the DISPLAY layer reads the already-
        // scaled number correctly, not whether the scaling formula itself is
        // right (that is ModifierMagnitudeTests' job).
        private static (int DamageMagnitude, int ResistancePercent) FieryExpected(int itemTier, RiftTier riftTier)
        {
            double scale = ModifierMagnitude.Scale(itemTier, riftTier);
            int damage = Rounding.AwayFromZero((float)(20 * scale));
            int resistanceFlat = Rounding.AwayFromZero((float)(15 * scale));
            return (damage, ItemStatLines.DamageReductionPercent(resistanceFlat));
        }

        [Test]
        public void ModifierEffectsForItem_ScalesTheRealFieryEntry_ByTierAndRiftTier()
        {
            var (expectedDamage, _) = FieryExpected(itemTier: 3, RiftTier.RiftForged);

            var scaled = ContentDatabase.ModifierEffectsForItem(3, RiftTier.RiftForged,
                new System.Collections.Generic.List<string> { "fiery" });

            Assert.IsTrue(scaled.Count > 0, "the real fiery modifier must resolve to real effects");

            var damageRider = scaled.Single(pair => pair.Effect.Type == ModifierEffectType.ElementalDamageOnHitPercent);
            Assert.AreEqual("fiery", damageRider.Modifier.id);
            Assert.AreEqual(expectedDamage, damageRider.Effect.Magnitude,
                "the UI-facing ModifierEffectsForItem must scale identically to the combat-facing ModifierEffects seam");
        }

        [Test]
        public void ModifierEffectsForItem_AtOrdinary_ScalesByExactlyOne()
        {
            var scaled = ContentDatabase.ModifierEffectsForItem(0, RiftTier.Ordinary,
                new System.Collections.Generic.List<string> { "fiery" });

            var damageRider = scaled.Single(pair => pair.Effect.Type == ModifierEffectType.ElementalDamageOnHitPercent);
            Assert.AreEqual(20, damageRider.Effect.Magnitude, "tier 0, Ordinary: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void CardSummary_ListsTheRolledModifierByDisplayNameWithAPlainEnglishFragment()
        {
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            var (expectedDamage, expectedResistPercent) = FieryExpected(item.tier, RiftTier.RiftForged);

            string card = ItemDescription.CardSummary(item, plus: 0, viewer: null,
                riftTier: RiftTier.RiftForged,
                modifierIds: new System.Collections.Generic.List<string> { "fiery" });

            StringAssert.Contains("AFFIXES", card);
            StringAssert.Contains(
                $"Fiery -- deals {expectedDamage}% bonus Fire damage on hit, reduces Fire damage taken by {expectedResistPercent}%",
                card);
        }

        [Test]
        public void CardSummary_WithNoRolledModifiers_PrintsNoAffixesSectionAtAll()
        {
            // THE regression guard: an item with nothing rolled (the vast
            // majority) must not gain a stray heading.
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            string card = ItemDescription.CardSummary(item);

            StringAssert.DoesNotContain("AFFIXES", card);
        }
    }
}
