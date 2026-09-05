using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Phase C's scaling seam (ContentDatabase.ModifierEffects x
    // ModifierMagnitude), exercised against REAL, content-authored
    // modifiers.json entries -- not test fixtures. Proves the full chain a
    // dropped item actually walks: equip a real item with a real modifier id
    // rolled at a known tier/riftTier -> ContentDatabase.ModifierEffects
    // scales the authored base by TierMultiplier x RiftMultiplier ->
    // FightEncounterAdapter reaches CombatantState with the SCALED number.
    //
    // ModifierMagnitudeTests already pins TierMultiplier/RiftMultiplier as
    // literals; this test uses those SAME already-pinned constants (1.6 for
    // RiftForged, never itself calling ModifierMagnitude.Scale) to hand-derive
    // its own expected numbers, so nothing here recomputes the production
    // formula under test.
    public class ItemModifierScalingReachesCombatTests
    {
        // A tier-0 item, deliberately: TierMultiplier(0) is exactly 1.0 (pinned
        // in ModifierMagnitudeTests), so the only multiplier left in play is
        // RiftMultiplier -- keeps the arithmetic in this file to one axis.
        private static ItemDefinition TierZeroEquippable() =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0);

        private static Character FreshCharacterWearing(ItemDefinition item, string modifierId, RiftTier riftTier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { modifierId }, riftTier: (int)riftTier);
            return character;
        }

        private static Character FreshCharacterWearing(ItemDefinition item, IReadOnlyList<string> modifierIds, RiftTier riftTier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string>(modifierIds), riftTier: (int)riftTier);
            return character;
        }

        // POST-AFFIX-SPLIT: Fire's family used to be one bundled "fiery" id
        // (ElementalDamageOnHitPercent + TypedResistanceFlat at once). A
        // designer pass split it into "fiery" (damage) and "emberguard"
        // (resistance) -- see modifiers.json's own entries. The two used to
        // be proven together in one test; now each affix scales on its own.
        [Test]
        public void FieryModifier_ScalesItsDamageEffectByTheItemsOwnTierAndRiftTier()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, "fiery", RiftTier.RiftForged);

            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must actually be LIVE (requirement met), or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // fiery's authored base: ElementalDamageOnHitPercent 10 (Fire) -- halved
            // from 20 in the affix-power-down pass (AUDIT-adjacent balance
            // call; see modifiers.json's own header).
            // Scale = TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var onHit = effects.All.Single(e => e.Type == ModifierEffectType.ElementalDamageOnHitPercent);
            Assert.AreEqual(DamageType.Fire, onHit.Against);
            Assert.AreEqual(16, onHit.Magnitude, "10 x 1.6 = 16.0 exactly");
        }

        [Test]
        public void EmberguardModifier_ScalesItsResistanceEffectByTheItemsOwnTierAndRiftTier()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, "emberguard", RiftTier.RiftForged);
            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // emberguard's authored base: TypedResistanceFlat 8 (Fire) -- halved from 15.
            // Scale = TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var resist = effects.All.Single(e => e.Type == ModifierEffectType.TypedResistanceFlat);
            Assert.AreEqual(DamageType.Fire, resist.Against);
            Assert.AreEqual(13, resist.Magnitude, "8 x 1.6 = 12.8, away-from-zero rounded");
        }

        [Test]
        public void FieryAndEmberguard_CanBothBeEquippedTogether_AndBothStillApply()
        {
            // The two split-off Fire affixes are still free to land on the
            // SAME item across two different affix slots -- proving the two
            // rules can coexist, the same claim the old bundled "fiery"
            // entry made on its own, now via two ids instead of one.
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, new[] { "fiery", "emberguard" }, RiftTier.RiftForged);
            var effects = ContentDatabase.ModifierEffects(character);

            Assert.AreEqual(16, effects.All.Single(e => e.Type == ModifierEffectType.ElementalDamageOnHitPercent).Magnitude);
            Assert.AreEqual(13, effects.All.Single(e => e.Type == ModifierEffectType.TypedResistanceFlat).Magnitude);
        }

        [Test]
        public void TheSameModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, "fiery", RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var onHit = effects.All.Single(e => e.Type == ModifierEffectType.ElementalDamageOnHitPercent);
            Assert.AreEqual(10, onHit.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void TypedResistance_FromARealModifier_ReachesCombatantStateTypedResistance()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            // "emberguard" now, not "fiery" -- Fire's resistance half moved
            // to its own split-off id; see this file's own header above.
            var character = FreshCharacterWearing(item, "emberguard", RiftTier.RiftForged);
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: a real fight must build");
            var state = built.Party[0];

            Assert.AreEqual(13, state.TypedResistance.Fire,
                "the elemental family's typed-resistance half must reach CombatantState.TypedResistance " +
                "the same seam a relic's ResistanceFlat already does");
        }

        [Test]
        public void RunicModifier_ScalesAndReachesMaxManaAndManaRegen()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            // Runic's mana-pool bonus and its mana-regen bonus are now two
            // separate split ids ("runic" and "manaflow") -- equip both to
            // prove they still coexist the way one bundled Runic used to.
            var withRunic = FreshCharacterWearing(item, new[] { "runic", "manaflow" }, RiftTier.Ordinary);
            var bareCharacter = new Character(withRunic.definitionId);

            var builtWith = FightEncounterAdapter.Build(
                new List<string> { withRunic.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { withRunic });

            var builtWithout = FightEncounterAdapter.Build(
                new List<string> { bareCharacter.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { bareCharacter });

            Assert.IsNotNull(builtWith);
            Assert.IsNotNull(builtWithout);

            var withState = builtWith.Party[0];
            var withoutState = builtWithout.Party[0];

            // runic's authored base at Ordinary/tier-0 (scale exactly 1.0):
            // FlatMaxManaBonus 5, FlatManaRegenBonus 1 -- both halved from 10/2.
            Assert.AreEqual(5, withState.MaxMana - withoutState.MaxMana, "Runic's flat Max Mana bonus must reach MaxMana");
            Assert.AreEqual(5, withState.CurrentMana - withoutState.CurrentMana, "and start the fight already full");
            Assert.AreEqual(1, withState.ManaRegen - withoutState.ManaRegen, "Runic's flat ManaRegen bonus must reach ManaRegen");
        }
    }
}
