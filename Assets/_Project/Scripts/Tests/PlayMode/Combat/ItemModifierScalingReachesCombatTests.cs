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

        // "attuned" is the damage rider in the wearer's own attack type and
        // "veiled" the resistance to every magical type -- the two rows the
        // twelve element mirrors became. Each affix scales on its own.
        [Test]
        public void AttunedModifier_ScalesItsDamageEffectByTheItemsOwnTier()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "attuned");

            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must actually be LIVE (requirement met), or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // attuned's authored base: ElementalDamageOnHitPercent 10, in the
            // wearer's own attack type (Physical when they have none).
            // Scale = TierMultiplier(5) = 3.0517578125, so 10 x that = 30.52,
            // away-from-zero rounded to 31.
            var onHit = effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(OwnAttackType(character), onHit.Against);
            Assert.IsFalse(onHit.AgainstWearer, "resolved to the wearer's element before combat reads it");
            Assert.AreEqual(31, onHit.Magnitude);
        }

        private static DamageType OwnAttackType(Character character) =>
            ContentDatabase.GetCharacter(character.definitionId).Data.AttackType;

        [Test]
        public void VeiledModifier_ScalesItsResistanceEffectByTheItemsOwnTier()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "veiled");
            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped modifier must produce real effects");

            // veiled's authored base: ResistanceFlat 4 against every magical
            // type. Scale = TierMultiplier(5) = 3.0517578125, so 4 x that =
            // 12.21, away-from-zero rounded to 12.
            var resist = effects.All.Single(e => e.Kind == EffectKind.ResistanceFlat);
            Assert.IsTrue(resist.AgainstMagical);
            Assert.AreEqual(12, resist.Magnitude);
        }

        [Test]
        public void AttunedAndVeiled_CanBothBeEquippedTogether_AndBothStillApply()
        {
            // The two rows can land on the SAME item across two affix slots.
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, new[] { "attuned", "veiled" });
            var effects = ContentDatabase.CharacterEffects(character);

            Assert.AreEqual(31, effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent).Magnitude);
            Assert.AreEqual(12, effects.All.Single(e => e.Kind == EffectKind.ResistanceFlat).Magnitude);
        }

        [Test]
        public void TheSameModifier_AtTierZero_ScalesByExactlyOne()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "attuned", tier: 0);
            var effects = ContentDatabase.CharacterEffects(character);

            var onHit = effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(10, onHit.Magnitude, "tier 0: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void TypedResistance_FromARealModifier_ReachesCombatantStateTypedResistance()
        {
            var item = TierFiveEquippable();
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, "veiled");
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: a real fight must build");
            var state = built.Party[0];

            Assert.AreEqual(12, state.TypedResistance.Fire,
                "veiled's magical resistance must reach CombatantState.TypedResistance " +
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
