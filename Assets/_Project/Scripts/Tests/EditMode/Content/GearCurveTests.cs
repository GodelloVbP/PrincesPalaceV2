using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.EditModeTests
{
    // GearCurve.At is what a copy's base and tier are worth (docs/PLAN_ITEMS.md
    // 2.3). Every literal below is hand-computed from the authored ends on the
    // geometric curve, floored; none is the production formula rerun
    // (CLAUDE.md gotcha 5). Tiers 11 and 12 sit past the art ladder (maxTier
    // 10) and pin the extrapolation plan 1.6 asks for.
    public class GearCurveTests
    {
        private static GearCurve Coif() => new GearCurve
        {
            maxTier = 10,
            statsAtZero = new StatBlock(10, 0, 0, physicalDefense: 4),
            statsAtMax = new StatBlock(93, -2, 0, physicalDefense: 37),
            scoresAtZero = new AbilityScoreBlock(1, 0, 0, 0, 0, 0),
            scoresAtMax = new AbilityScoreBlock(9, 0, 0, 0, 0, 0),
            requiresAtZero = AbilityScoreBlock.Zero,
            requiresAtMax = new AbilityScoreBlock(12, 0, 0, 0, 0, 0),
            baseName = "Leather Coif",
            tierAdjectives = new[] { "Ragged", "Worn", "Hardened" },
            fallbackAdjective = "",
            iconSheet = "Assets/_Project/Art/Items/coif",
            iconLevels = 10,
            iconPath = "",
        };

        private static GearCurve Sword() => new GearCurve
        {
            maxTier = 10,
            attackAtZero = 6,
            attackAtMax = 40,
            scalingAtZero = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.E),
            scalingAtMax = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A),
            spellScaling = ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.D),
            baseName = "Sword",
            tierAdjectives = new[] { "Worn", "Plain" },
            fallbackAdjective = "Plain",
            iconSheet = "",
            iconPath = "Assets/_Project/Art/Items/sword.png",
        };

        [TestCase(0, 10, 4, 0)]
        [TestCase(3, 19, 7, -1)]
        [TestCase(10, 93, 37, -2)]
        [TestCase(11, 116, 46, -3)]
        [TestCase(12, 145, 57, -4)]
        public void ArmourStats_FollowTheCurve_AndKeepClimbingPastMaxTier(int tier, int health, int defense, int speed)
        {
            var at = Coif().At(tier);

            Assert.AreEqual(health, at.StatBonus.maxHealth);
            Assert.AreEqual(defense, at.StatBonus.physicalDefense);
            Assert.AreEqual(speed, at.StatBonus.speed, "a penalty floors away from zero, as ValueAt does");
            Assert.AreEqual(tier, at.Tier);
        }

        [TestCase(3, 1, 1)]
        [TestCase(10, 9, 12)]
        [TestCase(12, 14, 19)]
        public void ScoresAndRequirements_RideTheSameCurve(int tier, int strength, int requiredStrength)
        {
            var at = Coif().At(tier);

            Assert.AreEqual(strength, at.AbilityScoreBonus.strength);
            Assert.AreEqual(requiredStrength, at.Requirements.strength);
        }

        [TestCase(0, 6)]
        [TestCase(3, 8)]
        [TestCase(10, 40)]
        [TestCase(12, 69)]
        public void WeaponAttack_ClimbsOnWeaponGrowth_AndFoldsIntoStats(int tier, int attack)
        {
            var at = Sword().At(tier);

            Assert.AreEqual(attack, at.AttackBonus);
            Assert.AreEqual(attack, at.StatBonus.attack);
        }

        // E at 0, A at 10. Past the top the letter stays at its authored
        // end: unclamped, tier 11 would read S.
        [TestCase(3, ScalingGrade.E)]
        [TestCase(10, ScalingGrade.A)]
        [TestCase(11, ScalingGrade.A)]
        [TestCase(12, ScalingGrade.A)]
        public void Grades_StopAtTheAuthoredTop(int tier, ScalingGrade expected)
        {
            Assert.AreEqual(expected, Sword().At(tier).Scaling.strength);
        }

        [Test]
        public void SpellScaling_IsNotTiered()
        {
            Assert.AreEqual(ScalingGrade.D, Sword().At(0).SpellScaling.intelligence);
            Assert.AreEqual(ScalingGrade.D, Sword().At(12).SpellScaling.intelligence);
        }

        [TestCase(0, "Ragged Leather Coif", "Assets/_Project/Art/Items/coif/level_1.png")]
        [TestCase(3, "Hardened Leather Coif", "Assets/_Project/Art/Items/coif/level_4.png")]
        [TestCase(12, "Hardened Leather Coif", "Assets/_Project/Art/Items/coif/level_10.png")]
        public void NameAndIcon_TopOutOnTheLastAdjectiveAndFrame(int tier, string name, string icon)
        {
            var at = Coif().At(tier);

            Assert.AreEqual(name, at.DisplayName);
            Assert.AreEqual(icon, at.IconPath);
        }

        [Test]
        public void AFlatIconPath_IsUsedWhenThereIsNoSheet()
        {
            Assert.AreEqual("Assets/_Project/Art/Items/sword.png", Sword().At(7).IconPath);
        }

        // Honing scales every combat stat (ItemUpgrade, +15% a plus, rounded
        // away from zero) and WeaponPower; never scores or requirements.
        [Test]
        public void Honing_ScalesCombatStats_NotScoresOrRequirements()
        {
            var honed = Coif().At(12).WithHoning(3);

            Assert.AreEqual(211, honed.StatBonus.maxHealth);
            Assert.AreEqual(83, honed.StatBonus.physicalDefense);
            Assert.AreEqual(-6, honed.StatBonus.speed);
            Assert.AreEqual(14, honed.AbilityScoreBonus.strength);
            Assert.AreEqual(19, honed.Requirements.strength);
            Assert.AreEqual(3, honed.Honing);
        }

        [Test]
        public void Honing_ScalesWeaponPower_NotTheRawAttack()
        {
            var honed = Sword().At(12).WithHoning(5);

            Assert.AreEqual(121, honed.WeaponPower, "69 x 1.75 = 120.75, away from zero");
            Assert.AreEqual(69, honed.AttackBonus);
            Assert.AreEqual(69, Sword().At(12).WeaponPower, "unhoned");
        }

        [Test]
        public void AFlatCurve_IsTheSameAtEveryTier()
        {
            var flat = GearCurve.Flat("Charm", new StatBlock(5, 1, 0), 0,
                new AbilityScoreBlock(0, 2, 0, 0, 0, 0), AbilityScoreBlock.Zero, "");

            Assert.AreEqual(5, flat.At(0).StatBonus.maxHealth);
            Assert.AreEqual(5, flat.At(12).StatBonus.maxHealth);
            Assert.AreEqual(2, flat.At(12).AbilityScoreBonus.dexterity);
            Assert.AreEqual("Charm", flat.At(12).DisplayName);
        }

        [Test]
        public void ANegativeTier_ReadsAsTierZero()
        {
            Assert.AreEqual(10, Coif().At(-3).StatBonus.maxHealth);
            Assert.AreEqual(0, Coif().At(-3).Tier);
        }

        // Plan 1.2 step 8: Uncommon one affix, Rare two (three when no trait
        // was left), Legendary two plus a trait.
        [TestCase(0, 0, Quality.Common)]
        [TestCase(1, 0, Quality.Uncommon)]
        [TestCase(2, 0, Quality.Rare)]
        [TestCase(3, 0, Quality.Rare)]
        [TestCase(2, 1, Quality.Legendary)]
        public void Quality_DerivesFromAffixesAndTrait(int affixes, int traits, Quality expected)
        {
            Assert.AreEqual(expected, ItemQuality.Of(affixes, traits));
        }

        [Test]
        public void QualityColours_KeepTheRarityPaletteForTheSameNames()
        {
            Assert.AreEqual("#9A93A8", ItemQuality.HexColor(Quality.Common));
            Assert.AreEqual("#6FBF73", ItemQuality.HexColor(Quality.Uncommon));
            Assert.AreEqual("#5AA6E8", ItemQuality.HexColor(Quality.Rare));
            Assert.AreEqual("#F0913D", ItemQuality.HexColor(Quality.Legendary));
        }
    }
}
