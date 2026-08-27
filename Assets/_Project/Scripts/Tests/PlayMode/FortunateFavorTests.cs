using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // Fortunate: a PASSIVE bonus to Prince's Favor, read LIVE off whatever
    // the character currently has equipped -- no stored state anywhere. This
    // REPLACES an earlier shape (FortunateFavorOnWin) that wrote a one-time
    // chunk of favour to RunSnapshot.runFavor on every fight WON, an
    // accumulating currency that only ever grew across a run. See git
    // history for the retired shape and its own tests.
    //
    // Mirrors ItemModifierScalingReachesCombatTests' posture: real,
    // content-authored modifiers.json entries, not test fixtures.
    public class FortunateFavorTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-fortunate-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static ItemDefinition TierZeroEquippable() =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0);

        private static Character FreshCharacter()
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");
            return new Character(definition.id);
        }

        private static CharacterDefinition DefinitionFor(Character character) =>
            ContentDatabase.Characters.FirstOrDefault(d => d != null && d.id == character.definitionId);

        [Test]
        public void WearingFortunate_GrantsAHigherFavorOf_ThanTheSameCharacterBare()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacter();
            var definition = DefinitionFor(character);
            int bare = ItemOfferRoll.FavorOf(character, definition);

            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);

            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            // fortunate's authored base is 3; TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            // 3 x 1.6 = 4.8, away-from-zero rounded to 5.
            int wearing = ItemOfferRoll.FavorOf(character, definition);
            Assert.AreEqual(bare + 5, wearing,
                "a live-equipped Fortunate must add its scaled bonus straight into FavorOf");
        }

        [Test]
        public void UnequippingFortunateMidRun_ImmediatelyDropsTheBonus_NoLingeringState()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacter();
            var definition = DefinitionFor(character);
            int bare = ItemOfferRoll.FavorOf(character, definition);

            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);
            int wearing = ItemOfferRoll.FavorOf(character, definition);
            Assert.Greater(wearing, bare, "fixture check: equipping Fortunate must raise FavorOf first");

            // Unequip -- Set with an empty itemId clears the slot (see
            // EquipmentLoadout.Set's own header: "a null/empty itemId
            // clears the slot, so Set and Clear are the same operation").
            character.equipment.Set(item.equipSlot, "");

            Assert.AreEqual(bare, ItemOfferRoll.FavorOf(character, definition),
                "taking Fortunate off must drop the bonus on the very next query -- nothing should persist it");
        }

        [Test]
        public void ReequippingFortunate_BringsTheBonusRightBackAgain()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacter();
            var definition = DefinitionFor(character);

            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);
            int firstWearing = ItemOfferRoll.FavorOf(character, definition);

            character.equipment.Set(item.equipSlot, "");
            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);

            Assert.AreEqual(firstWearing, ItemOfferRoll.FavorOf(character, definition),
                "re-equipping the identical roll must reproduce the identical bonus, not a stale or doubled one");
        }

        [Test]
        public void FortunateBonus_ScalesByRiftTier_LikeEveryOtherModifierEffect()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var ordinary = FreshCharacter();
            ordinary.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.Ordinary);

            var riftForged = FreshCharacter();
            riftForged.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);

            int ordinaryBonus = ContentDatabase.ModifierEffects(ordinary)
                .Best(ModifierEffectType.FortunateFavorBonusFlat);
            int riftForgedBonus = ContentDatabase.ModifierEffects(riftForged)
                .Best(ModifierEffectType.FortunateFavorBonusFlat);

            // base 3: Ordinary scale is exactly 1.0 (unscaled base survives), RiftForged is 3 x 1.6 = 4.8 -> 5.
            Assert.AreEqual(3, ordinaryBonus, "tier 0, RiftTier Ordinary: scale is exactly 1.0");
            Assert.AreEqual(5, riftForgedBonus, "3 x 1.6 = 4.8, away-from-zero rounded");
            Assert.Greater(riftForgedBonus, ordinaryBonus, "a higher RiftTier must scale the bonus up");
        }

        [Test]
        public void FortunateBonus_ScalesByItemTier_LikeEveryOtherModifierEffect()
        {
            var tierZero = TierZeroEquippable();
            Assert.IsNotNull(tierZero, "fixture: content has a tier-0 equippable item");

            var higherTier = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && i.tier > 0)
                .OrderBy(i => i.tier)
                .FirstOrDefault();
            Assert.IsNotNull(higherTier, "fixture: content has an equippable item above tier 0");

            var lowTierCharacter = FreshCharacter();
            lowTierCharacter.equipment.Set(tierZero.equipSlot, tierZero.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.Ordinary);

            var highTierCharacter = FreshCharacter();
            highTierCharacter.equipment.Set(higherTier.equipSlot, higherTier.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.Ordinary);

            int lowTierBonus = ContentDatabase.ModifierEffects(lowTierCharacter)
                .Best(ModifierEffectType.FortunateFavorBonusFlat);
            int highTierBonus = ContentDatabase.ModifierEffects(highTierCharacter)
                .Best(ModifierEffectType.FortunateFavorBonusFlat);

            Assert.Greater(highTierBonus, lowTierBonus,
                "a higher item tier must scale Fortunate's bonus up, the same TierMultiplier curve every other modifier reads");
        }

        [Test]
        public void CurrentSquadFavor_ReflectsFortunateOnWhicheverSquadMemberWearsIt()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            int before = ItemOfferRoll.CurrentSquadFavor();

            var wearer = SaveSlotManager.CurrentSave.ActiveSquad().First();
            wearer.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);

            Assert.Greater(ItemOfferRoll.CurrentSquadFavor(), before,
                "equipping Fortunate on a fielded squad member must raise the squad's live Favor");

            wearer.equipment.Set(item.equipSlot, "");

            Assert.AreEqual(before, ItemOfferRoll.CurrentSquadFavor(),
                "unequipping it must drop the squad's Favor straight back down -- nothing should linger on the save");
        }
    }
}
