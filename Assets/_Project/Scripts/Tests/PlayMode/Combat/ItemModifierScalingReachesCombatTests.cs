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
    // Phase C's scaling seam (ContentDatabase.Effects x
    // ModifierMagnitude), exercised against REAL, content-authored
    // modifiers.json entries -- not test fixtures. Proves the full chain a
    // dropped item actually walks: equip a real item with a real modifier id
    // rolled at a known tier -> ContentDatabase.Effects
    // scales the authored base by TierMultiplier ->
    // FightEncounterAdapter reaches CombatantState with the SCALED number.
    //
    // ModifierMagnitudeTests already pins TierMultiplier as literals; this
    // test hand-derives its own expected numbers from 1.25^5 = 3.0517578125
    // (never itself calling ModifierMagnitude.Scale), so nothing here
    // recomputes the production formula under test.
    public class ItemModifierScalingReachesCombatTests
    {
        // Tier 5, where TierMultiplier is 3.0517578125 (pinned in
        // ModifierMagnitudeTests); a piece the first character can wear there.
        private const int Tier = 5;

        private static ItemDefinition TierFiveEquippable() => GearFixtures.WearableAt(Tier);

        private static Character FreshCharacterWearing(ItemDefinition item, string modifierId, int tier = Tier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { modifierId }, tier: tier);
            return character;
        }

        private static Character FreshCharacterWearing(ItemDefinition item, IReadOnlyList<string> modifierIds, int tier = Tier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string>(modifierIds), tier: tier);
            return character;
        }

        // POST-AFFIX-SPLIT: Fire's family used to be one bundled "fiery" id
        // (ElementalDamageOnHitPercent + ResistanceFlat at once). A
        // designer pass split it into "fiery" (damage) and "emberguard"
        // (resistance) -- see modifiers.json's own entries. The two used to
        // be proven together in one test; now each affix scales on its own.
        [Test]
        public void FieryModifier_ScalesItsDamageEffectByTheItemsOwnTier()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "fiery");

            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must actually be LIVE (requirement met), or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // fiery's authored base: ElementalDamageOnHitPercent 10 (Fire) -- halved
            // from 20 in the affix-power-down pass (AUDIT-adjacent balance
            // call; see modifiers.json's own header).
            // Scale = TierMultiplier(5) = 3.0517578125, so 10 x that = 30.52,
            // away-from-zero rounded to 31.
            var onHit = effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(DamageType.Fire, onHit.Against);
            Assert.AreEqual(31, onHit.Magnitude);
        }

        [Test]
        public void EmberguardModifier_ScalesItsResistanceEffectByTheItemsOwnTier()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "emberguard");
            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // emberguard's authored base: ResistanceFlat 8 (Fire) -- halved from 15.
            // Scale = TierMultiplier(5) = 3.0517578125, so 8 x that = 24.41,
            // away-from-zero rounded to 24.
            var resist = effects.All.Single(e => e.Kind == EffectKind.ResistanceFlat);
            Assert.AreEqual(DamageType.Fire, resist.Against);
            Assert.AreEqual(24, resist.Magnitude);
        }

        [Test]
        public void FieryAndEmberguard_CanBothBeEquippedTogether_AndBothStillApply()
        {
            // The two split-off Fire affixes are still free to land on the
            // SAME item across two different affix slots -- proving the two
            // rules can coexist, the same claim the old bundled "fiery"
            // entry made on its own, now via two ids instead of one.
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, new[] { "fiery", "emberguard" });
            var effects = ContentDatabase.CharacterEffects(character);

            Assert.AreEqual(31, effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent).Magnitude);
            Assert.AreEqual(24, effects.All.Single(e => e.Kind == EffectKind.ResistanceFlat).Magnitude);
        }

        [Test]
        public void TheSameModifier_AtTierZero_ScalesByExactlyOne()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "fiery", tier: 0);
            var effects = ContentDatabase.CharacterEffects(character);

            var onHit = effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(10, onHit.Magnitude, "tier 0: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void TypedResistance_FromARealModifier_ReachesCombatantStateTypedResistance()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            // "emberguard" now, not "fiery" -- Fire's resistance half moved
            // to its own split-off id; see this file's own header above.
            var character = FreshCharacterWearing(item, "emberguard");
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: a real fight must build");
            var state = built.Party[0];

            Assert.AreEqual(24, state.TypedResistance.Fire,
                "the elemental family's typed-resistance half must reach CombatantState.TypedResistance " +
                "the same seam a relic's ResistanceFlat already does");
        }

        [Test]
        public void RunicModifier_ScalesAndReachesMaxManaAndManaRegen()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            // Runic's mana-pool bonus and its mana-regen bonus are now two
            // separate split ids ("runic" and "manaflow") -- equip both to
            // prove they still coexist the way one bundled Runic used to.
            var withRunic = FreshCharacterWearing(item, new[] { "runic", "manaflow" }, tier: 0);
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

            // runic's authored base at tier 0 (scale exactly 1.0):
            // MaxManaFlat 5, FlatManaRegenBonus 1 -- both halved from 10/2.
            Assert.AreEqual(5, withState.MaxMana - withoutState.MaxMana, "Runic's flat Max Mana bonus must reach MaxMana");
            Assert.AreEqual(5, withState.CurrentMana - withoutState.CurrentMana, "and start the fight already full");
            Assert.AreEqual(1, withState.ManaRegen - withoutState.ManaRegen, "Runic's flat ManaRegen bonus must reach ManaRegen");
        }
    }
}
