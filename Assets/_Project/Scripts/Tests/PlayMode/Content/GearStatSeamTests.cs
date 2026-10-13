using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // ContentDatabase.Effective is the one stat accessor of the copy model
    // (docs/PLAN_ITEMS.md 2.3). While content still holds one asset per tier,
    // every asset carries its base's curve; these tests hold the curve to the
    // numbers the per-tier assets were baked with, so a caller moved onto
    // Effective sees no change, and pin a real base past the art ladder.
    public class GearStatSeamTests
    {
        private const int MinimumGeneratedGear = 600;

        [Test]
        public void EveryGeneratedPiece_ReadsTheSameThroughItsBaseAndTier()
        {
            var generated = ContentDatabase.Equippables
                .Where(d => d.IsSetPiece || !string.IsNullOrEmpty(d.weaponFamilyId))
                .ToList();
            Assert.GreaterOrEqual(generated.Count, MinimumGeneratedGear, "vacuity guard: the seam must be checked against real content");

            foreach (var definition in generated)
            {
                var viaBase = ContentDatabase.Effective(ItemInstance.Of(definition.baseId, definition.tier), 4);
                var viaTierId = ContentDatabase.Effective(ItemInstance.Of(definition.id, 0), 4);
                Assert.IsTrue(viaBase.HasValue, $"{definition.id}: base '{definition.baseId}' does not resolve");
                Assert.IsTrue(viaTierId.HasValue, definition.id);

                foreach (var at in new[] { viaBase.Value, viaTierId.Value })
                {
                    string label = $"{definition.id} (tier {at.Tier})";
                    Assert.AreEqual(definition.tier, at.Tier, label);
                    Assert.AreEqual(definition.StatBonusAt(4), at.StatBonus, label);
                    Assert.AreEqual(definition.WeaponPowerAt(4), at.WeaponPower, label);
                    Assert.AreEqual(definition.attackBonus, at.AttackBonus, label);
                    Assert.AreEqual(definition.AbilityScoreBonusAt(4), at.AbilityScoreBonus, label);
                    Assert.AreEqual(definition.requirements, at.Requirements, label);
                    Assert.AreEqual(definition.scaling, at.Scaling, label);
                    Assert.AreEqual(definition.spellScaling, at.SpellScaling, label);
                    Assert.AreEqual(definition.displayName, at.DisplayName, label);
                    Assert.AreEqual(definition.iconPath, at.IconPath, label);
                }
            }
        }

        // Literals read off brigandine_boots_p0/_p10 and floored on the
        // 1.25 curve by hand (CLAUDE.md gotcha 5).
        [Test]
        public void ARealBase_PastTheArtLadder_KeepsClimbing()
        {
            var boots = ContentDatabase.Effective(ItemInstance.Of("brigandine_boots", 12)).Value;

            Assert.AreEqual(98, boots.StatBonus.maxHealth);
            Assert.AreEqual(36, boots.StatBonus.physicalDefense);
            Assert.AreEqual(3, boots.StatBonus.speed);
            Assert.AreEqual(21, boots.Requirements.dexterity);
            Assert.AreEqual("Ghoststep Brigandine Boots", boots.DisplayName, "the last adjective covers tiers past the list");
        }

        // sword_p0 10 Attack, sword_p10 202, on the 1.35 weapon curve.
        [Test]
        public void AWeaponBase_AtTheCap_HonedInItsSlot()
        {
            var sword = ContentDatabase.Effective(ItemInstance.Of("sword", 12), 4).Value;

            Assert.AreEqual(368, sword.AttackBonus);
            Assert.AreEqual(589, sword.WeaponPower, "368 x 1.6 = 588.8");
            Assert.AreEqual("Sovereign Sword", sword.DisplayName);
        }

        [Test]
        public void AnUnknownId_OrAConsumable_HasNoGearStats()
        {
            Assert.IsFalse(ContentDatabase.Effective(ItemInstance.Of("no_such_base", 3)).HasValue);

            var consumable = ContentDatabase.Consumables.FirstOrDefault();
            Assert.IsNotNull(consumable, "fixture: content has a consumable");
            Assert.IsFalse(ContentDatabase.Effective(ItemInstance.Of(consumable.id, 0)).HasValue);
        }

        [Test]
        public void EveryBase_Resolves_AndHasOneSlot()
        {
            foreach (var group in ContentDatabase.Equippables.GroupBy(d => d.baseId))
            {
                Assert.IsFalse(string.IsNullOrEmpty(group.Key), $"{group.First().id} has no baseId");
                Assert.AreSame(group.OrderBy(d => d.tier).First(), ContentDatabase.GetBase(group.Key), group.Key);
                Assert.AreEqual(1, group.Select(d => d.equipSlot).Distinct().Count(), group.Key);
            }
        }

        [Test]
        public void TheCopysQuality_CountsItsAffixes()
        {
            Assert.AreEqual(Domain.Content.Quality.Common, ContentDatabase.QualityOf(ItemInstance.Of("sword", 3)));
            Assert.AreEqual(Domain.Content.Quality.Rare,
                ContentDatabase.QualityOf(ItemInstance.Of("sword", 3, new[] { "a", "b" })));
        }

        // The seam takes the slot's honing as SlotHoning reads it.
        [Test]
        public void HoningFromTheSlot_FeedsTheSeam()
        {
            var honing = new System.Collections.Generic.List<SlotHoningEntry>();
            SlotHoning.Hone(honing, "shawn", EquipmentSlot.Weapon1);
            SlotHoning.Hone(honing, "shawn", EquipmentSlot.Weapon1);

            var sword = ContentDatabase.Effective(ItemInstance.Of("sword", 0),
                SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Weapon1)).Value;

            Assert.AreEqual(13, sword.WeaponPower, "10 x 1.3");
        }
    }
}
