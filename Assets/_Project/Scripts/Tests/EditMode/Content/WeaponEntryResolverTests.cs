using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The weapon generator: one authored family becomes one weapon per plus
    // level.
    //
    // Item-modifier plan Phase B collapsed the old modifier axis (sturdy/
    // nimble/hallowed/arcane/verdant/heavy/quick/cunning) out of this
    // generator entirely — a family generates ONE weapon per tier now, with
    // a single default scaling identity, and what used to differentiate one
    // sword from another is a rolled Rift modifier on the item instance
    // (modifiers.json), not a second baked family variant here.
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
                primary = "strength", primaryAtZero = "C", primaryAtMax = "S",
                secondary = "dexterity", secondaryAtZero = "E", secondaryAtMax = "C",
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

        // One tier axis now, not two.
        [Test]
        public void OneFamily_BecomesOneWeaponPerPlusLevel()
        {
            var weapons = Resolve(Sword());

            Assert.AreEqual(11, weapons.Count, "Eleven plus levels, one weapon each — no more modifier axis");
            CollectionAssert.AllItemsAreUnique(weapons.Select(w => w.Id).ToList());
            CollectionAssert.Contains(weapons.Select(w => w.Id).ToList(), "sword_p0");
            CollectionAssert.Contains(weapons.Select(w => w.Id).ToList(), "sword_p10");
        }

        // "<tier adjective> <family> +<plus>" — no modifier segment any more.
        [TestCase(0, "Worn Sword")]
        [TestCase(1, "Plain Sword")]
        [TestCase(4, "Tempered Sword")]
        [TestCase(10, "Sovereign Sword")]
        public void TheName_CarriesTheTierAndThePlus(int plus, string expected)
        {
            var weapon = Resolve(Sword()).Single(w => w.Tier == plus);
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
            var weapon = Resolve(Sword()).Single(w => w.Tier == plus);

            Assert.AreEqual(primary, weapon.Scaling.strength, $"primary at +{plus}");
            Assert.AreEqual(secondary, weapon.Scaling.dexterity, $"secondary at +{plus}");
        }

        [Test]
        public void BothEndsOfTheGradeCurve_AreExactlyWhatWasAuthored()
        {
            var weapons = Resolve(Sword());
            var bottom = weapons.Single(w => w.Tier == 0);
            var top = weapons.Single(w => w.Tier == 10);

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

            Assert.AreEqual(ScalingGrade.C, weapons.Single(w => w.Tier == 0).Scaling.strength);
            Assert.AreEqual(ScalingGrade.S, weapons.Single(w => w.Tier == 20).Scaling.strength);
            Assert.AreEqual(ScalingGrade.C, weapons.Single(w => w.Tier == 5).Scaling.strength,
                "A +5 weapon on a 20-step curve should still be near the bottom of it");
        }

        // The decision the whole axis still buys, even collapsed to one
        // weapon per family: a build that matches the family's scaling
        // hits harder than one that doesn't.
        [Test]
        public void AMatchingBuild_HitsHarderThanAMismatchedOne()
        {
            var top = Resolve(Sword()).Single(w => w.Tier == 10);

            var brawler = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);
            var duellist = new AbilityScoreBlock(10, 20, 10, 10, 10, 10);

            // Primary strength S (0.10/pt) + secondary dexterity C (0.03/pt).
            Assert.AreEqual(2.00f, top.Scaling.MultiplierFor(brawler), 0.0001f);
            Assert.AreEqual(1.30f, top.Scaling.MultiplierFor(duellist), 0.0001f);
        }

        [Test]
        public void FlatAttack_WalksBetweenTheAuthoredEnds()
        {
            var weapons = Resolve(Sword());

            Assert.AreEqual(3, weapons.Single(w => w.Tier == 0).AttackBonus);
            Assert.AreEqual(16, weapons.Single(w => w.Tier == 10).AttackBonus);
            Assert.Greater(weapons.Single(w => w.Tier == 5).AttackBonus, 3);
        }

        // ---- balance redesign Phase 3 (D3): the real weapons.json
        // endpoints, at WEAPON growth (GearScaling.WeaponTierGrowth, 1.35 --
        // steeper than armour's 1.25, which this file's own Sword() fixture
        // above is agnostic to since it only pins endpoints and monotonicity,
        // never an intermediate tier). Item-modifier plan Phase B confirmed
        // these endpoints are UNCHANGED by the family collapse. ------------
        //
        // INTERMEDIATE TIERS are PINNED LITERALS, independently hand-derived
        // from f(t) = (1.35^t - 1) / (1.35^10 - 1) against the two authored
        // ends -- not the production formula run again (CLAUDE.md gotcha 5).

        private static RawWeaponEntry OneStatFamily(string id, string displayName, int attackAtZero, int attackAtMax)
        {
            return new RawWeaponEntry
            {
                id = id,
                displayName = displayName,
                maxTier = 10,
                attackAtZero = attackAtZero,
                attackAtMax = attackAtMax,
                primary = "strength", primaryAtZero = "C", primaryAtMax = "S",
            };
        }

        [TestCase(0, 10)]
        [TestCase(1, 13)]
        [TestCase(2, 18)]
        [TestCase(3, 24)]
        [TestCase(4, 33)]
        [TestCase(5, 45)]
        [TestCase(6, 60)]
        [TestCase(7, 82)]
        [TestCase(8, 110)]
        [TestCase(9, 149)]
        [TestCase(10, 202)]
        public void SwordEndpoints_10To202_InterpolateAtWeaponGrowth(int tier, int expected)
        {
            var weapon = Resolve(OneStatFamily("sword", "Sword", 10, 202)).Single(w => w.Tier == tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
        }

        [TestCase(0, 9)]
        [TestCase(1, 12)]
        [TestCase(5, 40)]
        [TestCase(9, 134)]
        [TestCase(10, 181)]
        public void StaffEndpoints_9To181_InterpolateAtWeaponGrowth(int tier, int expected)
        {
            var weapon = Resolve(OneStatFamily("staff", "Staff", 9, 181)).Single(w => w.Tier == tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
        }

        [TestCase(0, 8)]
        [TestCase(1, 10)]
        [TestCase(5, 35)]
        [TestCase(9, 119)]
        [TestCase(10, 161)]
        public void DaggerEndpoints_8To161_InterpolateAtWeaponGrowth(int tier, int expected)
        {
            var weapon = Resolve(OneStatFamily("dagger", "Dagger", 8, 161)).Single(w => w.Tier == tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
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

            Assert.AreEqual("Worn Sword", weapons.Single(w => w.Tier == 0).DisplayName);
            Assert.AreEqual("Fine Sword", weapons.Single(w => w.Tier == 9).DisplayName);
        }

        [Test]
        public void NoAdjectivesAtAll_StillProducesAReadableName()
        {
            var family = Sword();
            family.tierAdjectives = null;

            Assert.AreEqual("Plain Sword", Resolve(family).Single(w => w.Tier == 2).DisplayName);
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

        // ---- id scheme -----------------------------------------------------

        [Test]
        public void GeneratedId_IsFamilyIdUnderscorePTier_WithNoModifierSegment()
        {
            foreach (var weapon in Resolve(Sword()))
            {
                Assert.AreEqual($"sword_p{weapon.Tier}", weapon.Id);
            }
        }

        [Test]
        public void ThreeFamilies_GenerateExactlyElevenEach_ThirtyThreeTotal()
        {
            var sword = OneStatFamily("sword", "Sword", 10, 202);
            var staff = OneStatFamily("staff", "Staff", 9, 181);
            var dagger = OneStatFamily("dagger", "Dagger", 8, 161);

            var weapons = Resolve(sword, staff, dagger);

            Assert.AreEqual(33, weapons.Count, "Three families x eleven tiers each, no modifier axis");
            Assert.AreEqual(11, weapons.Count(w => w.FamilyId == "sword"));
            Assert.AreEqual(11, weapons.Count(w => w.FamilyId == "staff"));
            Assert.AreEqual(11, weapons.Count(w => w.FamilyId == "dagger"));
        }

        // ---- what gets refused ------------------------------------------

        // Upgrading a weapon must never make it scale worse. Caught at build
        // time rather than discovered by a player whose +7 hits softer than
        // their +6.
        [Test]
        public void AGradeCurveThatGoesDown_IsRejected()
        {
            var family = Sword();
            family.primaryAtZero = "S";
            family.primaryAtMax = "C";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("scale worse")).ToList());
        }

        [Test]
        public void APrimaryAndSecondaryOnTheSameStat_IsRejected()
        {
            var family = Sword();
            family.secondary = "strength";

            Assert.IsNotEmpty(Errors(family).Where(e => e.Contains("silently overwrite")).ToList());
        }

        [Test]
        public void AFamilyWithNoPrimaryStat_IsRejected()
        {
            var family = Sword();
            family.primary = "";

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

        // A family is allowed to be a secondary-less one-stat weapon, but
        // then a mismatched wielder has nothing to fall back on — worth
        // being able to author deliberately rather than by accident.
        [Test]
        public void AFamilyWithNoSecondary_ScalesOnItsPrimaryAlone()
        {
            var family = Sword();
            family.secondary = "";

            var weapon = Resolve(family).Single(w => w.Tier == 10);

            Assert.AreEqual(ScalingGrade.S, weapon.Scaling.strength);
            Assert.AreEqual(ScalingGrade.None, weapon.Scaling.dexterity);
        }

        [Test]
        public void AlsoScalesWith_AddsAFlatThirdStat()
        {
            var family = Sword();
            family.alsoScalesWith = new[] { "constitution D" };

            var weapons = Resolve(family);

            Assert.AreEqual(ScalingGrade.D, weapons.Single(w => w.Tier == 0).Scaling.constitution);
            Assert.AreEqual(ScalingGrade.D, weapons.Single(w => w.Tier == 10).Scaling.constitution,
                "A flat extra does not move with plus — that is what makes it flat");
        }

        // A staff: a real spell axis alongside its plain-Attack scaling —
        // the whole point of the two profiles being separate fields.
        [Test]
        public void SpellScalesWith_IsASeparateAxisFromScaling_AndDoesNotMoveWithTier()
        {
            var family = Sword();
            family.spellScalesWith = new[] { "intelligence A" };

            var weapons = Resolve(family);

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
            family.requiresAtZero = new[] { "strength 5" };
            family.requiresAtMax = new[] { "strength 15" };

            var weapons = Resolve(family);

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

        // Only one of requiresAtZero/requiresAtMax authored: the empty side
        // parses to AbilityScoreBlock.Zero, which InterpolateScores reads as
        // a real requirement of nothing -- so a top-tier requirement would
        // silently open up to 0 instead of erroring. Refused instead.
        [Test]
        public void AFamilyWithOnlyRequiresAtZeroAuthored_IsRejectedLoudly()
        {
            var family = Sword();
            family.requiresAtZero = new[] { "strength 5" };

            StringAssert.Contains("requiresAtZero", Errors(family)[0]);
        }

        [Test]
        public void AFamilyWithOnlyRequiresAtMaxAuthored_IsRejectedLoudly()
        {
            var family = Sword();
            family.requiresAtMax = new[] { "strength 15" };

            StringAssert.Contains("requiresAtMax", Errors(family)[0]);
        }
    }
}
