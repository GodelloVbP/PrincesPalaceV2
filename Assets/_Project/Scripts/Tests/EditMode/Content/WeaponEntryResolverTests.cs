using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The weapon generator: one authored family becomes one base weapon whose
    // curve answers for every tier.
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
                tierAdjectives = new[] { "Worn", "Plain", "Fine", "Keen", "Tempered", "Masterwork", "Runed", "Ancient", "Radiant", "Mythic", "Sovereign", "Celestial", "Godforged" },
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

        // The tier is on the copy, so a family is one asset.
        [Test]
        public void OneFamily_BecomesOneBaseWeapon()
        {
            var weapons = Resolve(Sword());

            Assert.AreEqual(1, weapons.Count);
            Assert.AreEqual("sword", weapons[0].Id);
            Assert.AreEqual("Sword", weapons[0].DisplayName, "the asset carries the base name; a copy's name comes from the curve");
        }

        // "<tier adjective> <family> +<plus>" — no modifier segment any more.
        [TestCase(0, "Worn Sword")]
        [TestCase(1, "Plain Sword")]
        [TestCase(4, "Tempered Sword")]
        [TestCase(10, "Sovereign Sword")]
        [TestCase(11, "Celestial Sword")]
        [TestCase(12, "Godforged Sword")]
        public void TheName_CarriesTheTier(int tier, string expected)
        {
            Assert.AreEqual(expected, Resolve(Sword()).Single().Curve.At(tier).DisplayName);
        }

        // The ladder of adjectives is authored up to the copy cap; a list that
        // stops earlier repeats its last word.
        [Test]
        public void TheNameAtTiersPastTheAuthoredList_RepeatsTheLastAdjective()
        {
            var family = Sword();
            family.tierAdjectives = new[] { "Worn", "Plain" };

            Assert.AreEqual("Plain Sword", Resolve(family).Single().Curve.At(12).DisplayName);
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
        public void GradesClimbWithTier_BetweenTheTwoAuthoredEnds(int tier, ScalingGrade primary, ScalingGrade secondary)
        {
            var weapon = Resolve(Sword()).Single().Curve.At(tier);

            Assert.AreEqual(primary, weapon.Scaling.strength, $"primary at tier {tier}");
            Assert.AreEqual(secondary, weapon.Scaling.dexterity, $"secondary at tier {tier}");
        }

        // Grades are a letter ladder with S at the end: tiers past the art
        // ladder hold the top grade while the numbers keep climbing.
        [Test]
        public void GradesStopAtTheirAuthoredTop_PastTheArtLadder()
        {
            var weapon = Resolve(Sword()).Single().Curve.At(12);

            Assert.AreEqual(ScalingGrade.S, weapon.Scaling.strength);
            Assert.AreEqual(ScalingGrade.C, weapon.Scaling.dexterity);
        }

        [Test]
        public void BothEndsOfTheGradeCurve_AreExactlyWhatWasAuthored()
        {
            var curve = Resolve(Sword()).Single().Curve;
            var bottom = curve.At(0);
            var top = curve.At(10);

            Assert.AreEqual(ScalingGrade.C, bottom.Scaling.strength);
            Assert.AreEqual(ScalingGrade.S, top.Scaling.strength);
            Assert.AreEqual(ScalingGrade.E, bottom.Scaling.dexterity);
            Assert.AreEqual(ScalingGrade.C, top.Scaling.dexterity);
        }

        // A shorter ladder stretches the curve across its steps rather than
        // ending partway up it -- the same promise the armour sets make about
        // stats. The ladder tops out at the tier cap.
        [Test]
        public void ALongerMaxPlus_StretchesTheCurveRatherThanCappingItEarly()
        {
            var short5 = Resolve(Sword(maxTier: 5)).Single().Curve;
            var full10 = Resolve(Sword(maxTier: 10)).Single().Curve;

            Assert.AreEqual(ScalingGrade.S, short5.At(5).Scaling.strength);
            Assert.AreEqual(ScalingGrade.S, full10.At(10).Scaling.strength);
            Assert.AreEqual(ScalingGrade.C, full10.At(1).Scaling.strength,
                "A +1 weapon on a 10-step curve should still be near the bottom of it");
        }

        // No copy exists above ItemInstance.TierCap (10), so an art ladder
        // longer than that would be steps nothing can reach (AUDIT #276).
        [Test]
        public void AMaxPlusAboveTheTierCap_IsRefused_NamingTheFamily()
        {
            var errors = Errors(Sword(maxTier: 11));

            Assert.IsTrue(errors.Any(e => e.Contains("weapon family 'sword'") && e.Contains("maxTier is 11") && e.Contains("tier cap of 10")),
                string.Join("; ", errors));
        }

        [Test]
        public void AMaxPlusAtTheTierCap_Resolves()
        {
            Assert.AreEqual(10, Resolve(Sword(maxTier: 10)).Single().Curve.maxTier);
        }

        // The decision the whole axis still buys, even collapsed to one
        // weapon per family: a build that matches the family's scaling
        // hits harder than one that doesn't.
        [Test]
        public void AMatchingBuild_HitsHarderThanAMismatchedOne()
        {
            var top = Resolve(Sword()).Single().Curve.At(10);

            var brawler = new AbilityScoreBlock(20, 10, 10, 10, 10, 10);
            var duellist = new AbilityScoreBlock(10, 20, 10, 10, 10, 10);

            // Primary strength S (0.10/pt) + secondary dexterity C (0.03/pt).
            Assert.AreEqual(2.00f, top.Scaling.MultiplierFor(brawler), 0.0001f);
            Assert.AreEqual(1.30f, top.Scaling.MultiplierFor(duellist), 0.0001f);
        }

        [Test]
        public void FlatAttack_WalksBetweenTheAuthoredEnds()
        {
            var curve = Resolve(Sword()).Single().Curve;

            Assert.AreEqual(3, curve.At(0).AttackBonus);
            Assert.AreEqual(16, curve.At(10).AttackBonus);
            Assert.Greater(curve.At(5).AttackBonus, 3);
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
            var weapon = Resolve(OneStatFamily("sword", "Sword", 10, 202)).Single().Curve.At(tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
        }

        [TestCase(0, 9)]
        [TestCase(1, 12)]
        [TestCase(5, 40)]
        [TestCase(9, 134)]
        [TestCase(10, 181)]
        public void StaffEndpoints_9To181_InterpolateAtWeaponGrowth(int tier, int expected)
        {
            var weapon = Resolve(OneStatFamily("staff", "Staff", 9, 181)).Single().Curve.At(tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
        }

        [TestCase(0, 8)]
        [TestCase(1, 10)]
        [TestCase(5, 35)]
        [TestCase(9, 119)]
        [TestCase(10, 161)]
        public void DaggerEndpoints_8To161_InterpolateAtWeaponGrowth(int tier, int expected)
        {
            var weapon = Resolve(OneStatFamily("dagger", "Dagger", 8, 161)).Single().Curve.At(tier);
            Assert.AreEqual(expected, weapon.AttackBonus);
        }

        [Test]
        public void EveryGeneratedWeapon_IsHoldableAndIsNotStoreStock()
        {
            var weapon = Resolve(Sword()).Single();

            Assert.AreEqual(EquipmentSlot.Weapon1, weapon.Slot);
            Assert.AreEqual(0, weapon.Cost, $"{weapon.Id} is loot, not Store stock");
            for (int tier = 0; tier <= ItemInstance.TierCap; tier++)
            {
                Assert.Greater(weapon.Curve.At(tier).AttackBonus, 0, $"{weapon.Id} at tier {tier} grants no Attack at all");
            }
        }

        // A short adjective list is fine — the last one covers the rest,
        // rather than the list having to be exactly maxTier + 1 long.
        [Test]
        public void AShortAdjectiveList_RunsOutGracefullyRatherThanBlank()
        {
            var family = Sword();
            family.tierAdjectives = new[] { "Worn", "Fine" };

            var curve = Resolve(family).Single().Curve;

            Assert.AreEqual("Worn Sword", curve.At(0).DisplayName);
            Assert.AreEqual("Fine Sword", curve.At(9).DisplayName);
        }

        [Test]
        public void NoAdjectivesAtAll_StillProducesAReadableName()
        {
            var family = Sword();
            family.tierAdjectives = null;

            Assert.AreEqual("Plain Sword", Resolve(family).Single().Curve.At(2).DisplayName);
        }

        // A generated name carries the TIER (as its adjective) and nothing
        // else. Plus is an instance property, so baking it here would make
        // the definition's name wrong for every copy but one.
        [Test]
        public void TheGeneratedName_NeverCarriesAPlus()
        {
            var weapon = Resolve(Sword()).Single();
            for (int tier = 0; tier <= ItemInstance.TierCap; tier++)
            {
                string name = weapon.Curve.At(tier).DisplayName;
                StringAssert.DoesNotContain("+", name, $"'{name}' bakes honing into a name — honing belongs to the slot");
            }
        }

        // ---- id scheme -----------------------------------------------------

        [Test]
        public void GeneratedId_IsTheFamilyId_WithNoTierSuffix()
        {
            Assert.AreEqual("sword", Resolve(Sword()).Single().Id);
        }

        [Test]
        public void ThreeFamilies_GenerateOneBaseEach()
        {
            var sword = OneStatFamily("sword", "Sword", 10, 202);
            var staff = OneStatFamily("staff", "Staff", 9, 181);
            var dagger = OneStatFamily("dagger", "Dagger", 8, 161);

            var weapons = Resolve(sword, staff, dagger);

            CollectionAssert.AreEqual(new[] { "sword", "staff", "dagger" }, weapons.Select(w => w.Id).ToList());
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

            var weapon = Resolve(family).Single().Curve.At(10);

            Assert.AreEqual(ScalingGrade.S, weapon.Scaling.strength);
            Assert.AreEqual(ScalingGrade.None, weapon.Scaling.dexterity);
        }

        [Test]
        public void AlsoScalesWith_AddsAFlatThirdStat()
        {
            var family = Sword();
            family.alsoScalesWith = new[] { "constitution D" };

            var curve = Resolve(family).Single().Curve;

            Assert.AreEqual(ScalingGrade.D, curve.At(0).Scaling.constitution);
            Assert.AreEqual(ScalingGrade.D, curve.At(10).Scaling.constitution,
                "A flat extra does not move with plus — that is what makes it flat");
        }

        // A staff: a real spell axis alongside its plain-Attack scaling —
        // the whole point of the two profiles being separate fields.
        [Test]
        public void SpellScalesWith_IsASeparateAxisFromScaling_AndDoesNotMoveWithTier()
        {
            var family = Sword();
            family.spellScalesWith = new[] { "intelligence A" };

            var curve = Resolve(family).Single().Curve;

            Assert.AreEqual(ScalingGrade.A, curve.At(0).SpellScaling.intelligence);
            Assert.AreEqual(ScalingGrade.A, curve.At(10).SpellScaling.intelligence,
                "Spell scaling is authored flat, same as alsoScalesWith — it does not move with tier");

            // The primary/secondary Attack axis is untouched by spellScaling
            // being present — the two are genuinely separate fields, not one
            // profile relabelled.
            Assert.AreEqual(ScalingGrade.None, curve.At(0).SpellScaling.strength);
            Assert.AreNotEqual(ScalingGrade.None, curve.At(0).Scaling.strength,
                "The Attack axis (primary=strength) should still resolve normally alongside spellScaling");
        }

        [Test]
        public void AWeaponWithNoSpellScalesWith_HasANeutralSpellScalingProfile()
        {
            var weapon = Resolve(Sword()).First();
            Assert.IsTrue(weapon.Curve.At(0).SpellScaling.IsNeutral);
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

            var curve = Resolve(family).Single().Curve;

            Assert.AreEqual(5, curve.At(0).Requirements.strength);
            Assert.AreEqual(15, curve.At(10).Requirements.strength);
            // Halfway along the LADDER is not halfway up the CURVE: the tier
            // scale is geometric, so tier 5 has spent under a quarter of its
            // travel and a 5->15 requirement sits at 7. Requirements gating
            // late rather than evenly is the intended consequence — a mid-tier
            // weapon stays wearable by a mid-tier character, and the demanding
            // ones are the last two floors.
            Assert.AreEqual(7, curve.At(5).Requirements.strength);
        }

        [Test]
        public void AWeaponWithNoRequires_HasAZeroRequirement()
        {
            var weapon = Resolve(Sword()).First();
            Assert.AreEqual(AbilityScoreBlock.Zero, weapon.Curve.At(0).Requirements);
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
