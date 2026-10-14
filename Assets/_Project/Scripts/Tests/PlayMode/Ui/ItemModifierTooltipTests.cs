using System.Collections.Generic;
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
    // exercised end to end against the REAL "fiery"/"emberguard"
    // modifiers.json entries -- never a hand-built Effect fixture,
    // the same posture FrostyModifierReachesCombatTests/
    // ItemModifierScalingReachesCombatTests already take for Phase C/D's
    // combat hooks.
    //
    // POST-AFFIX-SPLIT: "fiery" used to bundle BOTH the on-hit damage rider
    // AND the Fire resistance under one id, so one card line used to read
    // "Fiery -- deals N% bonus Fire damage on hit, reduces Fire damage taken
    // by M%". A designer pass split the resistance off into its own id,
    // "emberguard" -- each now renders its OWN card line, so this file
    // covers both separately rather than expecting one combined fragment.
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
        // Bases here (10, 8) are fiery/emberguard's CURRENT authored
        // magnitudes -- halved from 20/15 in the affix-power-down pass; see
        // modifiers.json's own entries.
        private static int FieryExpectedDamage(int itemTier)
        {
            double scale = ModifierMagnitude.Scale(itemTier);
            return Rounding.AwayFromZero((float)(10 * scale));
        }

        private static int EmberguardExpectedResistPercent(int itemTier)
        {
            double scale = ModifierMagnitude.Scale(itemTier);
            int resistanceFlat = Rounding.AwayFromZero((float)(8 * scale));
            return ItemStatLines.DamageReductionPercent(resistanceFlat);
        }

        [Test]
        public void ModifierEffectsForItem_ScalesTheRealFieryEntry_ByTier()
        {
            int expectedDamage = FieryExpectedDamage(itemTier: 3);

            var scaled = ContentDatabase.ModifierEffectsForItem(3,
                new System.Collections.Generic.List<string> { "fiery" });

            Assert.IsTrue(scaled.Count > 0, "the real fiery modifier must resolve to real effects");

            var damageRider = scaled.Single(pair => pair.Effect.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual("fiery", damageRider.Modifier.id);
            Assert.AreEqual(expectedDamage, damageRider.Effect.Magnitude,
                "the UI-facing ModifierEffectsForItem must scale identically to the combat-facing ModifierEffects seam");
        }

        [Test]
        public void ModifierEffectsForItem_AtTierZero_ScalesByExactlyOne()
        {
            var scaled = ContentDatabase.ModifierEffectsForItem(0,
                new System.Collections.Generic.List<string> { "fiery" });

            var damageRider = scaled.Single(pair => pair.Effect.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(10, damageRider.Effect.Magnitude, "tier 0: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void CardSummary_ListsTheRolledFieryModifierByDisplayNameWithAPlainEnglishFragment()
        {
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            int expectedDamage = FieryExpectedDamage(4);

            string card = ItemDescription.CardSummary(ItemInstance.Of(item.id, 4, new[] { "fiery" }));

            StringAssert.Contains("AFFIXES", card);
            StringAssert.Contains($"Fiery -- <color={ModifierEffectText.KeywordHex}>+{expectedDamage}%</color> Fire dmg on hit", card);
        }

        [Test]
        public void CardSummary_ListsTheRolledEmberguardModifierByDisplayNameWithAPlainEnglishFragment()
        {
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            int expectedResistPercent = EmberguardExpectedResistPercent(4);

            string card = ItemDescription.CardSummary(ItemInstance.Of(item.id, 4, new[] { "emberguard" }));

            StringAssert.Contains("AFFIXES", card);
            StringAssert.Contains($"Emberguard -- <color={ModifierEffectText.KeywordHex}>-{expectedResistPercent}%</color> Fire dmg taken", card);
        }

        [Test]
        public void CardSummary_ListsBothFieryAndEmberguard_AsTwoSeparateAffixLines_WhenBothAreRolled()
        {
            // The two split-off Fire affixes can still both land on the same
            // item (two affix slots, two ids) -- proving the card renders
            // BOTH lines rather than only ever showing one, the same claim
            // the old combined "Fiery" line made on its own.
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            int expectedDamage = FieryExpectedDamage(4);
            int expectedResistPercent = EmberguardExpectedResistPercent(4);

            string card = ItemDescription.CardSummary(ItemInstance.Of(item.id, 4, new[] { "fiery", "emberguard" }));

            StringAssert.Contains($"Fiery -- <color={ModifierEffectText.KeywordHex}>+{expectedDamage}%</color> Fire dmg on hit", card);
            StringAssert.Contains($"Emberguard -- <color={ModifierEffectText.KeywordHex}>-{expectedResistPercent}%</color> Fire dmg taken", card);
        }

        [Test]
        public void CardSummary_WithNoRolledModifiers_PrintsNoAffixesSectionAtAll()
        {
            // THE regression guard: an item with nothing rolled (the vast
            // majority) must not gain a stray heading.
            var item = AnyEquippable();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            string card = ItemDescription.CardSummary(ItemInstance.Of(item.id, 4));

            StringAssert.DoesNotContain("AFFIXES", card);
        }

        // ---- ITEM-MODIFIER PLAN PHASE F: the AFFIXES section becomes a
        // comparison, the same "VS. EQUIPPED" posture DeltaLines already
        // takes for numbers -- ComparisonBody now diffs the candidate's
        // rolled ids against whatever is CURRENTLY worn in the slot the
        // candidate would occupy. ----

        private static ItemDefinition EquippableInSlot(EquipmentSlot slot) =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.equipSlot == slot);

        private static Character FreshCharacterWearing(ItemDefinition item, System.Collections.Generic.List<string> modifierIds)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: modifierIds);
            return character;
        }

        [Test]
        public void ComparisonBody_AGainedAffixAndALostAffixAreBothShown_CorrectlyColoured()
        {
            var item = EquippableInSlot(EquipmentSlot.Head);
            Assert.IsNotNull(item, "fixture: content has a Head-slot equippable");

            // The character already wears THIS SAME item, rolled "frosty" --
            // Compare's own header notes hovering a candidate already worn
            // is safe (a zero stat delta, but a real affix diff), which is
            // exactly what this test needs: one item definition, two rolls.
            var character = FreshCharacterWearing(item, new List<string> { "frosty" });

            string body = ItemDescription.ComparisonBody(character, ItemInstance.Of(item.id, 4, new[] { "fiery" }));

            StringAssert.Contains($"<color={ItemStatLines.GainHex}>Fiery", body,
                "an affix the candidate rolls that the equipped copy does not have is a gain");
            StringAssert.Contains($"<color={ItemStatLines.LossHex}>Losing: Frosty</color>", body,
                "an affix on the equipped copy the candidate does not roll needs its own loss line");
        }

        [Test]
        public void ComparisonBody_ASharedAffixIsNeutral_NotAGainAndALossBoth()
        {
            var item = EquippableInSlot(EquipmentSlot.Head);
            Assert.IsNotNull(item, "fixture: content has a Head-slot equippable");

            var character = FreshCharacterWearing(item, new List<string> { "fiery" });

            string body = ItemDescription.ComparisonBody(character, ItemInstance.Of(item.id, 4, new[] { "fiery" }));

            StringAssert.Contains("Fiery --", body, "the shared affix still lists");
            StringAssert.DoesNotContain($"<color={ItemStatLines.GainHex}>Fiery", body,
                "an affix already worn is not a gain");
            StringAssert.DoesNotContain("Losing: Fiery", body,
                "an affix rolled on both the candidate and the equipped copy is not lost");
        }

        [Test]
        public void ComparisonBody_WithNothingCurrentlyEquipped_DegradesToTodaysPlainListing()
        {
            // First-time equip: the slot is empty, so there is nothing to
            // diff against -- this must read exactly like CardSummary's own
            // no-comparison listing, per CardSummary_ListsTheRolledFieryModifierByDisplayNameWithAPlainEnglishFragment above.
            var item = EquippableInSlot(EquipmentSlot.Head);
            Assert.IsNotNull(item, "fixture: content has a Head-slot equippable");

            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");
            var character = new Character(definition.id);

            string body = ItemDescription.ComparisonBody(character, ItemInstance.Of(item.id, 4, new[] { "fiery" }));

            StringAssert.Contains("AFFIXES", body);
            StringAssert.Contains("Fiery --", body);
            StringAssert.DoesNotContain($"<color={ItemStatLines.GainHex}>Fiery", body,
                "a first equip has nothing to gain against");
            StringAssert.DoesNotContain("Losing:", body);
        }
    }
}
