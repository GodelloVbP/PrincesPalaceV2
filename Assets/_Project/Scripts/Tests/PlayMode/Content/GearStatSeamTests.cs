using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // ContentDatabase.Effective is the one stat accessor of the copy model
    // (docs/PLAN_ITEMS.md 2.3). While content still holds one asset per tier,
    // every asset carries its base's curve; these tests hold the curve to the
    // numbers the per-tier assets were baked with, so a caller moved onto
    // Effective sees no change, and pin a real base past the art ladder.
    public class GearStatSeamTests
    {
        private const int MinimumGeneratedBases = 50;

        [Test]
        public void EveryGeneratedPiece_ReadsTheSameThroughItsBaseAndTierId()
        {
            var generated = ContentDatabase.Equippables
                .Where(d => d.IsSetPiece || !string.IsNullOrEmpty(d.weaponFamilyId))
                .ToList();
            Assert.GreaterOrEqual(generated.Count, MinimumGeneratedBases, "vacuity guard: the seam must be checked against real content");

            foreach (var definition in generated)
            {
                for (int tier = 0; tier <= ItemInstance.TierCap; tier++)
                {
                    var at = ContentDatabase.Effective(ItemInstance.Of(definition.id, tier), 4);
                    Assert.IsTrue(at.HasValue, $"{definition.id} at tier {tier} does not resolve");

                    string label = $"{definition.id} (tier {tier})";
                    Assert.AreEqual(tier, at.Value.Tier, label);
                    Assert.IsFalse(string.IsNullOrEmpty(at.Value.DisplayName), label);
                    Assert.AreEqual(4, at.Value.Honing, label);
                }

                Assert.AreEqual(definition.iconPath, ContentDatabase.Effective(ItemInstance.Of(definition.id, 0)).Value.IconPath,
                    $"{definition.id}: the asset wears its tier-0 art");
            }
        }

        // Literals read off the generated assets as they were baked before
        // the stat fields left ItemDefinition (CLAUDE.md gotcha 5): three
        // bases at tiers 0, 5 and 10, then the cap below.
        [Test]
        public void ArmourBases_PinTheirStatsScoresAndRequirements()
        {
            var boots0 = ContentDatabase.Effective(ItemInstance.Of("brigandine_boots", 0)).Value;
            Assert.AreEqual("Patched Brigandine Boots", boots0.DisplayName);
            Assert.AreEqual(7, boots0.StatBonus.maxHealth);
            Assert.AreEqual(2, boots0.StatBonus.physicalDefense);
            Assert.AreEqual(new AbilityScoreBlock(1, 2, 1, 0, 0, 0), boots0.Requirements);

            var boots5 = ContentDatabase.Effective(ItemInstance.Of("brigandine_boots", 5)).Value;
            Assert.AreEqual("Silent Brigandine Boots", boots5.DisplayName);
            Assert.AreEqual(20, boots5.StatBonus.maxHealth);
            Assert.AreEqual(7, boots5.StatBonus.physicalDefense);
            Assert.AreEqual(new AbilityScoreBlock(3, 4, 1, 0, 0, 0), boots5.Requirements);

            var boots10 = ContentDatabase.Effective(ItemInstance.Of("brigandine_boots", 10)).Value;
            Assert.AreEqual("Ghoststep Brigandine Boots", boots10.DisplayName);
            Assert.AreEqual(63, boots10.StatBonus.maxHealth);
            Assert.AreEqual(2, boots10.StatBonus.speed);
            Assert.AreEqual(23, boots10.StatBonus.physicalDefense);
            Assert.AreEqual(new AbilityScoreBlock(2, 2, 1, 0, 0, 0), boots10.AbilityScoreBonus);
            Assert.AreEqual(new AbilityScoreBlock(12, 14, 5, 0, 0, 0), boots10.Requirements);

            var robe0 = ContentDatabase.Effective(ItemInstance.Of("silk_robe_top", 0)).Value;
            Assert.AreEqual(4, robe0.StatBonus.magicalDefense);
            Assert.AreEqual(1, robe0.AbilityScoreBonus.intelligence);
            Assert.AreEqual(3, robe0.Requirements.intelligence);

            var robe5 = ContentDatabase.Effective(ItemInstance.Of("silk_robe_top", 5)).Value;
            Assert.AreEqual(12, robe5.StatBonus.magicalDefense);
            Assert.AreEqual(1, robe5.AbilityScoreBonus.intelligence);
            Assert.AreEqual(7, robe5.Requirements.intelligence);

            var robe10 = ContentDatabase.Effective(ItemInstance.Of("silk_robe_top", 10)).Value;
            Assert.AreEqual(3, robe10.StatBonus.manaRegen);
            Assert.AreEqual(38, robe10.StatBonus.magicalDefense);
            Assert.AreEqual(5, robe10.AbilityScoreBonus.intelligence);
            Assert.AreEqual(new AbilityScoreBlock(0, 0, 2, 0, 23, 0), robe10.Requirements);
        }

        [Test]
        public void WeaponBases_PinTheirAttackAndGrades()
        {
            var sword0 = ContentDatabase.Effective(ItemInstance.Of("sword", 0)).Value;
            Assert.AreEqual("Worn Sword", sword0.DisplayName);
            Assert.AreEqual(10, sword0.AttackBonus);
            Assert.AreEqual(ScalingGrade.C, sword0.Scaling.strength);

            var sword5 = ContentDatabase.Effective(ItemInstance.Of("sword", 5)).Value;
            Assert.AreEqual(45, sword5.AttackBonus);
            Assert.AreEqual(ScalingGrade.C, sword5.Scaling.strength);

            var sword10 = ContentDatabase.Effective(ItemInstance.Of("sword", 10)).Value;
            Assert.AreEqual(202, sword10.AttackBonus);
            Assert.AreEqual(ScalingGrade.S, sword10.Scaling.strength);

            var staff0 = ContentDatabase.Effective(ItemInstance.Of("staff", 0)).Value;
            Assert.AreEqual("Crooked Staff", staff0.DisplayName);
            Assert.AreEqual(9, staff0.AttackBonus);
            Assert.AreEqual(ScalingGrade.C, staff0.Scaling.intelligence);
            Assert.AreEqual(ScalingGrade.A, staff0.SpellScaling.intelligence);

            var staff5 = ContentDatabase.Effective(ItemInstance.Of("staff", 5)).Value;
            Assert.AreEqual(40, staff5.AttackBonus);
            Assert.AreEqual(ScalingGrade.C, staff5.Scaling.intelligence);

            var staff10 = ContentDatabase.Effective(ItemInstance.Of("staff", 10)).Value;
            Assert.AreEqual("Worldroot Staff", staff10.DisplayName);
            Assert.AreEqual(181, staff10.AttackBonus);
            Assert.AreEqual(ScalingGrade.S, staff10.Scaling.intelligence);
            Assert.AreEqual(ScalingGrade.A, staff10.SpellScaling.intelligence);
        }

        // No copy outruns the art ladder: a request past tier 10 is the tier-10 copy.
        [Test]
        public void ARealBase_PastTheArtLadder_IsTheTopCopy()
        {
            var boots = ContentDatabase.Effective(ItemInstance.Of("brigandine_boots", 12)).Value;

            Assert.AreEqual(10, boots.Tier);
            Assert.AreEqual(63, boots.StatBonus.maxHealth);
            Assert.AreEqual(23, boots.StatBonus.physicalDefense);
            Assert.AreEqual("Ghoststep Brigandine Boots", boots.DisplayName);
        }

        // The sword's tier 0 is 10 Attack and tier 10 is 202, on the 1.35 weapon curve.
        [Test]
        public void AWeaponBase_AtTheCap_HonedInItsSlot()
        {
            var sword = ContentDatabase.Effective(ItemInstance.Of("sword", 12), 4).Value;

            Assert.AreEqual(202, sword.AttackBonus);
            Assert.AreEqual(323, sword.WeaponPower, "202 x 1.6 = 323.2");
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
        public void EveryBase_Resolves_AndHasNoTierSuffix()
        {
            foreach (var definition in ContentDatabase.Equippables)
            {
                Assert.AreSame(definition, ContentDatabase.GetItem(definition.id), definition.id);
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(definition.id, "_p[0-9]+$"),
                    $"{definition.id} still carries a tier suffix");
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
