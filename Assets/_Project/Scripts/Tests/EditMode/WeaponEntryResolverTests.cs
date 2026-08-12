using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The weapon generator: one authored family becomes one weapon per
    // modifier per plus level.
    //
    // Expected grades and names are PINNED literals rather than the
    // production interpolation run again (CLAUDE.md gotcha #5).
    public class WeaponEntryResolverTests
    {
        private static RawWeaponEntry Sword(int maxTier = 10)
        {
            return new RawWeaponEntry
            {
                id = "sword",
                displayName = "Sword",
                maxTier = maxTier,
                attackAtZero = 3,
                attackAtMax = 16,
                cost = 0,
                costPerTier = 0,
                tierAdjectives = new[] { "Worn", "Plain", "Fine", "Keen", "Tempered", "Masterwork", "Runed", "Ancient", "Radiant", "Mythic", "Sovereign" },
                modifiers = new[]
                {
                    new RawWeaponModifier
                    {
                        id = "sturdy", displayName = "Sturdy",
                        primary = "strength", primaryAtZero = "C", primaryAtMax = "S",
                        secondary = "dexterity", secondaryAtZero = "E", secondaryAtMax = "C",
                    },
                    new RawWeaponModifier
                    {
                        id = "nimble", displayName = "Nimble",
                        primary = "dexterity", primaryAtZero = "C", primaryAtMax = "S",
                        secondary = "strength", secondaryAtZero = "E", secondaryAtMax = "C",
                    },
                }
            };
        }

        private static List<ResolvedWeapon> Resolve(params RawWeaponEntry[] families)
        {
            bool ok = WeaponEntryResolver.TryResolveAll(families, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static List<string> Errors(params RawWeaponEntry[] families)
        {
            WeaponEntryResolver.TryResolveAll(families, out _, out var errors);
            return errors;
        }

        // The second axis, and the reason this generator exists at all.
        [Test]
        public void OneFamily_BecomesOneWeaponPerModifierPerPlusLevel()
        {
            var weapons = Resolve(Sword());

            Assert.AreEqual(22, weapons.Count, "Two modifiers across eleven plus levels");
            CollectionAssert.AllItemsAreUnique(weapons.Select(w => w.Id).ToList());
            CollectionAssert.Contains(weapons.Select(w => w.Id).ToList(), "sword_sturdy_p0");
            CollectionAssert.Contains(weapons.Select(w => w.Id).ToList(), "sword_nimble_p10");
        }

        // "<modifier> <tier adjective> <family> +<plus>" — both halves of what
        // a weapon is, readable without opening anything.
        [TestCase("sturdy", 0, "Sturdy Worn Sword")]
        [TestCase("sturdy", 1, "Sturdy Plain Sword")]
        [TestCase("nimble", 4, "Nimble Tempered Sword")]
        [TestCase("nimble", 10, "Nimble Sovereign Sword")]
        public void TheName_CarriesTheModifierTheTierAndThePlus(string modifier, int plus, string expected)
        {
            var weapon = Resolve(Sword()).Single(w => w.ModifierId == modifier && w.Tier == plus);
            Assert.AreEqual(expected, weapon.DisplayName);
        }

        // C at the bottom, S at the top, on the same GEOMETRIC ladder every
        // tiered thing in the game now walks (GearScaling.TierGrowth). Pinned
        // so a coefficient change cannot quietly reshape the whole curve.
        //
        // Primary C -> S and secondary E -> C over eleven tiers lands as
        // C C C C C C B B B A S and E E E E E E E E D D C. The grades hold
        // longer and then jump, which is the curve doing what it was changed
        // to do: a weapon's character is stable through the early floors and
        // the last two are where it transforms.
        [TestCase(0, ScalingGrade.C, ScalingGrade.E)]
        [TestCase(3, ScalingGrade.C, ScalingGrade.E)]
        [TestCase(5, ScalingGrade.C, ScalingGrade.E)]
        [TestCase(6, ScalingGrade.B, ScalingGrade.E)]
        [TestCase(8, ScalingGrade.B, ScalingGrade.D)]
        [TestCase(9, ScalingGrade.A, ScalingGrade.D)]
        [TestCase(10, ScalingGrade.S, ScalingGrade.C)]
        public void GradesClimbWithPlus_BetweenTheTwoAuthoredEnds(int plus, ScalingGrade primary, ScalingGrade secondary)
        {
            var sturdy = Resolve(Sword()).Single(w => w.ModifierId == "sturdy" && w.Tier == plus);

            Assert.AreEqual(primary, sturdy.Scaling.strength, $"primary at +{plus}");
            Assert.AreEqual(secondary, sturdy.Scaling.dexterity, $"secondary at +{plus}");
        }

        [Test]
        public void BothEndsOfTheGradeCurve_AreExactlyWhatWasAuthored()
        {
            var weapons = Resolve(Sword());
            var bottom = weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 0);
            var top = weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 10);

            Assert.AreEqual(ScalingGrade.C, bottom.Scaling.strength);
            Assert.AreEqual(ScalingGrade.S, top.Scaling.strength);
            Assert.AreEqual(ScalingGrade.E, bottom.Scaling.dexterity);
            Assert.AreEqual(ScalingGrade.C, top.Scaling.dexterity);
        }

        // Raising maxTier should stretch the curve, not run out of ladder
        // partway up — the same promise the armour sets make about stats.
        [Test]
        public void RaisingMaxPlus_StretchesTheCurveRatherThanCappingItEarly()
        {
            var weapons = Resolve(Sword(maxTier: 20));

            Assert.AreEqual(ScalingGrade.C, weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 0).Scaling.strength);
            Assert.AreEqual(ScalingGrade.S, weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 20).Scaling.strength);
            Assert.AreEqual(ScalingGrade.C, weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 5).Scaling.strength,
                "A +5 weapon on a 20-step curve should still be near the bottom of it");
        }

        // The decision the whole feature is for: same family, same plus, two
        // modifiers, and a character who is good at exactly one of them.
        [Test]
        public void TwoModifiersOfTheSameWeapon_PayOffForDifferentCharacters()
        {
            var weapons = Resolve(Sword());
            var sturdy = weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 10);
            var nimble = weapons.Single(w => w.ModifierId == "nimble" && w.Tier == 10);

            var brawler = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);
            var duellist = new AbilityScoreBlock(10, 20, 10, 10, 10, 10);

            Assert.AreEqual(2.00f, sturdy.Scaling.MultiplierFor(brawler), 0.0001f);
            Assert.AreEqual(1.30f, nimble.Scaling.MultiplierFor(brawler), 0.0001f);
            Assert.AreEqual(1.30f, sturdy.Scaling.MultiplierFor(duellist), 0.0001f);
            Assert.AreEqual(2.00f, nimble.Scaling.MultiplierFor(duellist), 0.0001f);

            Assert.AreEqual(sturdy.AttackBonus, nimble.AttackBonus,
                "The two are the same steel — only the balance differs, so the flat Attack must match");
        }

        [Test]
        public void FlatAttack_WalksBetweenTheAuthoredEnds()
        {
            var weapons = Resolve(Sword());

            Assert.AreEqual(3, weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 0).AttackBonus);
            Assert.AreEqual(16, weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 10).AttackBonus);
            Assert.Greater(weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 5).AttackBonus, 3);
        }

        [Test]
        public void EveryGeneratedWeapon_IsHoldableAndIsNotStoreStock()
        {
            foreach (var weapon in Resolve(Sword()))
            {
                Assert.AreEqual(EquipmentSlot.Weapon1, weapon.Slot);
                Assert.Greater(weapon.AttackBonus, 0, $"{weapon.Id} grants no Attack at all");
                Assert.AreEqual(0, weapon.Cost, $"{weapon.Id} is loot, not Store stock");
            }
        }

        // A short adjective list is fine — the last one covers the rest,
        // rather than the list having to be exactly maxTier + 1 long.
        [Test]
        public void AShortAdjectiveList_RunsOutGracefullyRatherThanBlank()
        {
            var family = Sword();
            family.tierAdjectives = new[] { "Worn", "Fine" };

            var weapons = Resolve(family);

            Assert.AreEqual("Sturdy Worn Sword", weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 0).DisplayName);
            Assert.AreEqual("Sturdy Fine Sword", weapons.Single(w => w.ModifierId == "sturdy" && w.Tier == 9).DisplayName);
        }

        [Test]
        public void NoAdjectivesAtAll_StillProducesAReadableName()
        {
            var family = Sword();
            family.tierAdjectives = null;

            Assert.AreEqual("Sturdy Plain Sword",
                Resolve(family).Single(w => w.ModifierId == "sturdy" && w.Tier == 2).DisplayName);
        }

        // A generated name carries the TIER (as its adjective) and nothing
        // else. Plus is an instance property, so baking it here would make
        // the definition's name wrong for every copy but one.
        [Test]
        public void TheGeneratedName_NeverCarriesAPlus()
        {
            foreach (var weapon in Resolve(Sword()))
            {
                StringAssert.DoesNotContain("+", weapon.DisplayName,
                    $"'{weapon.DisplayName}' bakes a plus into a definition — plus belongs to the instance");
            }
        }

        // ---- what gets refused ------------------------------------------

        // Upgrading a weapon must never make it scale worse. Caught at build
        // time rather than discovered by a player whose +7 hits softer than
        // their +6.
        [Test]
        public void AGradeCurveThatGoesDown_IsRejected()
        {
            var family = Sword();
            family.modifiers[0].primaryAtZero = "S";
            family.modifiers[0].primaryAtMax = "C";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("scale worse")).ToList());
        }

        [Test]
        public void AModifierWhosePrimaryAndSecondaryAreTheSameStat_IsRejected()
        {
            var family = Sword();
            family.modifiers[0].secondary = "strength";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("silently overwrite")).ToList());
        }

        [Test]
        public void AFamilyWithNoModifiers_IsRejectedRatherThanGeneratingNothing()
        {
            var family = Sword();
            family.modifiers = new RawWeaponModifier[0];

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("no modifiers")).ToList());
        }

        [Test]
        public void AModifierWithNoPrimaryStat_IsRejected()
        {
            var family = Sword();
            family.modifiers[0].primary = "";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("primary is required")).ToList());
        }

        [Test]
        public void ANonHandSlot_IsRejected()
        {
            var family = Sword();
            family.slot = "Head";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("holdable")).ToList());
        }

        [Test]
        public void TwoFamiliesGeneratingTheSameId_AreReported()
        {
            Assert.IsNotEmpty(Errors(Sword(), Sword()).Where(e => e.Contains("share the id")).ToList());
        }

        // A modifier is allowed to be a secondary-less one-stat weapon, but
        // then a mismatched wielder has nothing to fall back on — worth
        // being able to author deliberately rather than by accident.
        [Test]
        public void AModifierWithNoSecondary_ScalesOnItsPrimaryAlone()
        {
            var family = Sword();
            family.modifiers[0].secondary = "";

            var weapon = Resolve(family).Single(w => w.ModifierId == "sturdy" && w.Tier == 10);

            Assert.AreEqual(ScalingGrade.S, weapon.Scaling.strength);
            Assert.AreEqual(ScalingGrade.None, weapon.Scaling.dexterity);
        }

        [Test]
        public void AlsoScalesWith_AddsAFlatThirdStat()
        {
            var family = Sword();
            family.modifiers[0].alsoScalesWith = new[] { "constitution D" };

            var weapons = Resolve(family).Where(w => w.ModifierId == "sturdy").ToList();

            Assert.AreEqual(ScalingGrade.D, weapons.Single(w => w.Tier == 0).Scaling.constitution);
            Assert.AreEqual(ScalingGrade.D, weapons.Single(w => w.Tier == 10).Scaling.constitution,
                "A flat extra does not move with plus — that is what makes it flat");
        }

        // A staff: no Attack scaling at all, but a real spell axis — the
        // whole point of the two profiles being separate fields.
        [Test]
        public void SpellScalesWith_IsASeparateAxisFromScaling_AndDoesNotMoveWithTier()
        {
            var family = Sword();
            family.modifiers[0].spellScalesWith = new[] { "intelligence A" };

            var weapons = Resolve(family).Where(w => w.ModifierId == "sturdy").ToList();

            Assert.AreEqual(ScalingGrade.A, weapons.Single(w => w.Tier == 0).SpellScaling.intelligence);
            Assert.AreEqual(ScalingGrade.A, weapons.Single(w => w.Tier == 10).SpellScaling.intelligence,
                "Spell scaling is authored flat, same as alsoScalesWith — it does not move with tier");

            // The primary/secondary Attack axis is untouched by spellScaling
            // being present — the two are genuinely separate fields, not one
            // profile relabelled.
            Assert.AreEqual(ScalingGrade.None, weapons.Single(w => w.Tier == 0).SpellScaling.strength);
            Assert.AreNotEqual(ScalingGrade.None, weapons.Single(w => w.Tier == 0).Scaling.strength,
                "The Attack axis (primary=strength) should still resolve normally alongside spellScaling");
        }

        [Test]
        public void AWeaponWithNoSpellScalesWith_HasANeutralSpellScalingProfile()
        {
            var weapon = Resolve(Sword()).First();
            Assert.IsTrue(weapon.SpellScaling.IsNeutral);
        }

        // A legendary (high-tier) blade demands a higher score than its
        // tier-0 self — the whole point of interpolating requirements the
        // same two-ended way primary/secondary already are.
        [Test]
        public void RequiresAtZeroAndAtMax_InterpolateAcrossTiers()
        {
            var family = Sword();
            family.modifiers[0].requiresAtZero = new[] { "strength 5" };
            family.modifiers[0].requiresAtMax = new[] { "strength 15" };

            var weapons = Resolve(family).Where(w => w.ModifierId == "sturdy").ToList();

            Assert.AreEqual(5, weapons.Single(w => w.Tier == 0).Requirements.strength);
            Assert.AreEqual(15, weapons.Single(w => w.Tier == 10).Requirements.strength);
            // Halfway along the LADDER is not halfway up the CURVE: the tier
            // scale is geometric, so tier 5 has spent under a quarter of its
            // travel and a 5->15 requirement sits at 7. Requirements gating
            // late rather than evenly is the intended consequence — a mid-tier
            // weapon stays wearable by a mid-tier character, and the demanding
            // ones are the last two floors.
            Assert.AreEqual(7, weapons.Single(w => w.Tier == 5).Requirements.strength);
        }

        [Test]
        public void AWeaponWithNoRequires_HasAZeroRequirement()
        {
            var weapon = Resolve(Sword()).First();
            Assert.AreEqual(AbilityScoreBlock.Zero, weapon.Requirements);
        }
    }
}
