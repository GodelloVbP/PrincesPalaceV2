using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class ItemEntryResolverTests
    {
        private static RawItemEntry Consumable(string id = "potion")
        {
            return new RawItemEntry { id = id, displayName = "Potion" };
        }

        private static RawItemEntry Weapon(string id = "sword", int attackBonus = 3)
        {
            return new RawItemEntry { id = id, displayName = "Sword", kind = "Weapon", attackBonus = attackBonus };
        }

        [Test]
        public void MinimalConsumable_DefaultsToHealWithSensibleAmountAndCost()
        {
            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { Consumable() }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(ResolvedItemKind.Consumable, resolved[0].Kind);
            Assert.AreEqual(ResolvedItemEffect.Heal, resolved[0].Effect);
            Assert.Greater(resolved[0].Amount, 0);
            Assert.Greater(resolved[0].Cost, 0);
            Assert.AreEqual(0, resolved[0].AttackBonus);
        }

        [Test]
        public void MinimalWeapon_DefaultsToCostZero_AndKeepsItsAttackBonus()
        {
            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { Weapon(attackBonus: 5) }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(ResolvedItemKind.Weapon, resolved[0].Kind);
            Assert.AreEqual(5, resolved[0].AttackBonus);
            Assert.AreEqual(0, resolved[0].Cost, "Weapons are loot, not Store stock — a nonzero default would list them for sale");
        }

        [Test]
        public void KindAndEffectParsing_AreCaseInsensitive()
        {
            var entry = Consumable();
            entry.kind = "consumable";
            entry.effect = "restoremana";

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(ResolvedItemEffect.RestoreMana, resolved[0].Effect);
        }

        [Test]
        public void ExplicitCostOfZero_IsRespected_NotTreatedAsOmitted()
        {
            var entry = Consumable();
            entry.cost = 0;

            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(0, resolved[0].Cost);
        }

        [Test]
        public void WeaponWithoutAnAttackBonus_ProducesAClearError()
        {
            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { Weapon(attackBonus: -1) }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Weapon needs a positive attackBonus", errors[0]);
        }

        // The cross-field rules: which fields mean anything depends on kind,
        // so mixing them is an authoring mistake worth naming rather than
        // silently dropping the value.
        [Test]
        public void WeaponCarryingConsumableFields_ProducesAClearError()
        {
            var withAmount = Weapon("w1");
            withAmount.amount = 20;
            var withEffect = Weapon("w2");
            withEffect.effect = "Heal";

            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { withAmount }, out _, out var amountErrors);
            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { withEffect }, out _, out var effectErrors);

            StringAssert.Contains("amount is a Consumable field", amountErrors[0]);
            StringAssert.Contains("effect is a Consumable field", effectErrors[0]);
        }

        [Test]
        public void ConsumableCarryingAWeaponField_ProducesAClearError()
        {
            var entry = Consumable();
            entry.attackBonus = 4;

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("attackBonus is a Weapon field", errors[0]);
        }

        [Test]
        public void ConsumableThatRestoresNothing_ProducesAClearError()
        {
            var entry = Consumable();
            entry.amount = 0;

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("restores nothing", errors[0]);
        }

        [Test]
        public void UnrecognizedKind_ProducesAClearErrorListingValidOptions()
        {
            var entry = Consumable();
            entry.kind = "Armour";

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Armour", errors[0]);
            StringAssert.Contains("Weapon", errors[0]);
        }

        [Test]
        public void MissingIdOrDisplayName_ProducesAClearError()
        {
            var noId = Consumable("");
            var noName = Consumable("x");
            noName.displayName = "";

            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { noId }, out _, out var idErrors);
            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { noName }, out _, out var nameErrors);

            StringAssert.Contains("id is required", idErrors[0]);
            StringAssert.Contains("displayName is required", nameErrors[0]);
        }

        [Test]
        public void DuplicateIds_ProduceAClearError()
        {
            bool ok = ItemEntryResolver.TryResolveAll(
                new List<RawItemEntry> { Consumable("dupe"), Weapon("dupe") }, out var resolved, out var errors);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("Duplicate item id 'dupe'", errors[0]);
        }

        [Test]
        public void MultipleInvalidEntries_ReportEveryErrorTogether()
        {
            var entries = new List<RawItemEntry> { Consumable(""), Weapon("w", attackBonus: 0) };

            bool ok = ItemEntryResolver.TryResolveAll(entries, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void SortOrder_FollowsFileOrder()
        {
            var entries = new List<RawItemEntry> { Consumable("a"), Weapon("b"), Consumable("c") };

            ItemEntryResolver.TryResolveAll(entries, out var resolved, out _);

            Assert.AreEqual(0, resolved[0].SortOrder);
            Assert.AreEqual(1, resolved[1].SortOrder);
            Assert.AreEqual(2, resolved[2].SortOrder);
        }

        // --- Equipment: the worn-but-not-a-weapon kind.

        private static RawItemEntry Gloves(string id = "gloves_of_strength")
        {
            return new RawItemEntry
            {
                id = id,
                displayName = "Gloves of Strength",
                kind = "Equipment",
                slot = "Gloves",
                abilityScoreBonus = new AbilityScoreBlock(1, 0, 0, 0, 0, 0),
            };
        }

        [Test]
        public void Equipment_ResolvesItsSlotAndBonuses()
        {
            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { Gloves() }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("\n", errors));
            Assert.AreEqual(ResolvedItemKind.Equipment, resolved[0].Kind);
            Assert.AreEqual(EquipmentSlot.Gloves, resolved[0].Slot);
            Assert.AreEqual(1, resolved[0].AbilityScoreBonus.strength);
            Assert.AreEqual(0, resolved[0].Cost, "Equipment is loot, not Store stock, unless a cost is authored");
        }

        [Test]
        public void Equipment_WithoutASlot_IsRejected()
        {
            var entry = Gloves();
            entry.slot = "";

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Equipment needs a slot", errors[0]);
        }

        [Test]
        public void Equipment_WithAnUnknownSlot_IsRejected()
        {
            var entry = Gloves();
            entry.slot = "Backpack";

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("slot 'Backpack' isn't valid", errors[0]);
        }

        // The same rule a Weapon with no attackBonus gets, for the same
        // reason: a piece that grants nothing is wearable and pointless.
        [Test]
        public void Equipment_GrantingNothing_IsRejected()
        {
            var entry = Gloves();
            entry.abilityScoreBonus = AbilityScoreBlock.Zero;

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a statBonus or an abilityScoreBonus", errors[0]);
        }

        [Test]
        public void Equipment_UsingAttackBonusInsteadOfStatBonus_IsRejected()
        {
            var entry = Gloves();
            entry.attackBonus = 2;

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("attackBonus is a Weapon field", errors[0]);
        }

        [Test]
        public void AWeapon_MayNameAHandSlot_ButNothingElse()
        {
            var offHand = Weapon("off_hand");
            offHand.slot = "Weapon2";

            Assert.IsTrue(ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { offHand }, out var resolved, out var errors),
                string.Join("\n", errors));
            Assert.AreEqual(EquipmentSlot.Weapon2, resolved[0].Slot);

            var onHead = Weapon("hat_sword");
            onHead.slot = "Head";

            Assert.IsFalse(ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { onHead }, out _, out var headErrors));
            StringAssert.Contains("can only go in a hand slot", headErrors[0]);
        }

        [Test]
        public void AWeapon_DefaultsToTheFirstHand()
        {
            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { Weapon() }, out var resolved, out _);

            Assert.AreEqual(EquipmentSlot.Weapon1, resolved[0].Slot);
        }

        [Test]
        public void AConsumable_CarryingWornFields_IsRejected()
        {
            var withSlot = Consumable("slotted");
            withSlot.slot = "Head";

            Assert.IsFalse(ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { withSlot }, out _, out var slotErrors));
            StringAssert.Contains("slot is an Equipment/Weapon field", slotErrors[0]);

            var withBonus = Consumable("bonused");
            withBonus.statBonus = new StatBlock(0, 0, 0, 3);

            Assert.IsFalse(ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { withBonus }, out _, out var bonusErrors));
            StringAssert.Contains("worn-while-equipped fields", bonusErrors[0]);
        }

        [Test]
        public void StartingStock_DefaultsToFalseAndRoundTripsWhenSet()
        {
            var plain = Gloves("plain");
            var kitted = Gloves("kitted");
            kitted.startingStock = true;

            ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { plain, kitted }, out var resolved, out _);

            Assert.IsFalse(resolved[0].StartingStock);
            Assert.IsTrue(resolved[1].StartingStock);
        }
    }
}
