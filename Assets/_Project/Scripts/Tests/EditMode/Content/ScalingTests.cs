using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Scaling: how hard a weapon or spell rides the wielder's ability scores.
    //
    // Expected values are PINNED literals, not the production formula run a
    // second time (CLAUDE.md gotcha #5). A test that recomputes
    // `1 + PerPoint(grade) * (score - 10)` agrees with the code by
    // construction and would survive every coefficient being wrong.
    public class ScalingTests
    {
        private static ScalingProfile Riding(AbilityScore score, ScalingGrade grade)
        {
            return ScalingProfile.None.With(score, grade);
        }

        private static AbilityScoreBlock Scores(int strength = 10, int dexterity = 10, int constitution = 10,
            int wisdom = 10, int intelligence = 10, int charisma = 10)
        {
            return new AbilityScoreBlock(strength, dexterity, constitution, wisdom, intelligence, charisma);
        }

        // THE property the whole system rests on. Everything that exists today
        // was balanced without scaling, so scaling has to be inert until
        // someone opts in — otherwise this ships as a silent rebalance of
        // every enemy and every fight.
        [Test]
        public void AProfileThatScalesOnNothing_MultipliesByExactlyOne()
        {
            Assert.IsTrue(ScalingProfile.None.IsNeutral);
            Assert.AreEqual(1f, ScalingProfile.None.MultiplierFor(Scores(strength: 20, dexterity: 3)), 0.0001f);
        }

        [Test]
        public void AtTenAcrossTheBoard_EvenAnSGradeMultipliesByExactlyOne()
        {
            Assert.AreEqual(1f, Riding(AbilityScore.Strength, ScalingGrade.S).MultiplierFor(Scores()), 0.0001f);
        }

        // Ten points over neutral is roughly where a committed build lands, so
        // these are the numbers that decide whether the system is felt.
        [TestCase(ScalingGrade.S, 2.00f)]
        [TestCase(ScalingGrade.A, 1.70f)]
        [TestCase(ScalingGrade.B, 1.50f)]
        [TestCase(ScalingGrade.C, 1.30f)]
        [TestCase(ScalingGrade.D, 1.15f)]
        [TestCase(ScalingGrade.E, 1.07f)]
        public void AtTwentyStrength_EachGradeIsWorthAKnownAmount(ScalingGrade grade, float expected)
        {
            Assert.AreEqual(expected, Riding(AbilityScore.Strength, grade).MultiplierFor(Scores(strength: 20)), 0.0001f);
        }

        // The grades have to stay strictly ordered, or the letters stop
        // meaning anything and a "B" sword could out-scale an "A" one.
        [Test]
        public void TheGradeLadder_IsStrictlyIncreasing()
        {
            var ladder = new[]
            {
                ScalingGrade.None, ScalingGrade.E, ScalingGrade.D,
                ScalingGrade.C, ScalingGrade.B, ScalingGrade.A, ScalingGrade.S,
            };

            for (int i = 1; i < ladder.Length; i++)
            {
                Assert.Greater(ScalingGrades.PerPoint(ladder[i]), ScalingGrades.PerPoint(ladder[i - 1]),
                    $"{ladder[i]} should be worth more per point than {ladder[i - 1]}");
                Assert.Greater((int)ladder[i], (int)ladder[i - 1],
                    $"{ladder[i]} must also sort above {ladder[i - 1]} — the enum order is what interpolation walks");
            }
        }

        // Below neutral it goes DOWN. The wrong weapon has to be a real
        // setback or "pick the one that fits your build" has no wrong answer.
        [Test]
        public void BelowNeutral_TheSameGradeIsAPenalty()
        {
            // A is 0.07 a point, and Strength 6 is four points under neutral.
            Assert.AreEqual(0.72f, Riding(AbilityScore.Strength, ScalingGrade.A).MultiplierFor(Scores(strength: 6)), 0.0001f);
        }

        [Test]
        public void ADisastrousMatch_StillLandsForSomething()
        {
            // S on Strength at Strength 0 is 1 + 0.1 * -10 = exactly zero,
            // which would make the weapon literally unusable rather than bad.
            Assert.AreEqual(ScalingProfile.MinimumMultiplier,
                Riding(AbilityScore.Strength, ScalingGrade.S).MultiplierFor(Scores(strength: 0)), 0.0001f);
        }

        // Additive across stats, so a player can add the grades up in their
        // head rather than having to test the product.
        [Test]
        public void TwoStats_ContributeAdditivelyRatherThanCompounding()
        {
            var sword = Riding(AbilityScore.Strength, ScalingGrade.A).With(AbilityScore.Dexterity, ScalingGrade.C);

            // 1 + 0.07*8 + 0.03*4 = 1.68, not the 1.7472 the two would
            // compound to if each were applied in turn.
            Assert.AreEqual(1.68f, sword.MultiplierFor(Scores(strength: 18, dexterity: 14)), 0.0001f);
        }

        // The decision the feature exists for: the same character, two swords.
        [Test]
        public void TheSameCharacter_GetsAVisiblyDifferentResultFromTwoWeapons()
        {
            var brawler = Scores(strength: 20, dexterity: 10);

            var sturdy = Riding(AbilityScore.Strength, ScalingGrade.S).With(AbilityScore.Dexterity, ScalingGrade.C);
            var nimble = Riding(AbilityScore.Dexterity, ScalingGrade.S).With(AbilityScore.Strength, ScalingGrade.C);

            Assert.AreEqual(2.00f, sturdy.MultiplierFor(brawler), 0.0001f);
            Assert.AreEqual(1.30f, nimble.MultiplierFor(brawler), 0.0001f);
        }

        // BonusFor is MultiplierFor minus the 1.0 baseline -- the exact
        // number a UI attribution line ("STR B: +45") is built from.
        [Test]
        public void BonusFor_IsMultiplierForMinusOne()
        {
            var sword = Riding(AbilityScore.Strength, ScalingGrade.A).With(AbilityScore.Dexterity, ScalingGrade.C);
            var scores = Scores(strength: 18, dexterity: 14);

            Assert.AreEqual(sword.MultiplierFor(scores) - 1f, sword.BonusFor(scores), 0.0001f);
        }

        // One score's own share, in isolation -- what "STR B: +45" attributes
        // to Strength alone, not blended with whatever Dexterity contributed.
        [Test]
        public void BonusForOneScore_IsolatesThatScoresOwnShare()
        {
            var sword = Riding(AbilityScore.Strength, ScalingGrade.A).With(AbilityScore.Dexterity, ScalingGrade.C);
            var scores = Scores(strength: 18, dexterity: 14);

            // A is 0.07/point, Strength 18 is 8 over neutral.
            Assert.AreEqual(0.56f, sword.BonusFor(scores, AbilityScore.Strength), 0.0001f);
            // C is 0.03/point, Dexterity 14 is 4 over neutral.
            Assert.AreEqual(0.12f, sword.BonusFor(scores, AbilityScore.Dexterity), 0.0001f);
            // A score this profile does not ride contributes nothing.
            Assert.AreEqual(0f, sword.BonusFor(scores, AbilityScore.Charisma), 0.0001f);
        }

        [Test]
        public void NeutralScores_AreTenAcrossTheBoard()
        {
            foreach (var score in AbilityScores.All)
            {
                Assert.AreEqual(AbilityDerivation.NeutralScore, ScalingProfile.NeutralScores[score], $"{score} should be neutral");
            }
        }

        // ---- the indexer and With(), which interpolation and content both
        // walk by enum rather than by field name -------------------------

        [Test]
        public void Indexer_CoversEveryAbilityScore()
        {
            foreach (var score in AbilityScores.All)
            {
                var profile = Riding(score, ScalingGrade.B);
                Assert.AreEqual(ScalingGrade.B, profile[score], $"{score} did not round-trip through With/indexer");

                foreach (var other in AbilityScores.All.Where(s => s != score))
                {
                    Assert.AreEqual(ScalingGrade.None, profile[other], $"Setting {score} should not have touched {other}");
                }
            }
        }

        [Test]
        public void All_ListsEveryValueOfTheEnumExactlyOnce()
        {
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(AbilityScore)).Cast<AbilityScore>().ToList(), AbilityScores.All);
        }

        [Test]
        public void EveryGrade_HasACoefficientAndALetter()
        {
            foreach (ScalingGrade grade in Enum.GetValues(typeof(ScalingGrade)))
            {
                Assert.DoesNotThrow(() => ScalingGrades.PerPoint(grade), $"{grade} has no coefficient");
                Assert.IsNotEmpty(ScalingGrades.Letter(grade), $"{grade} has nothing to print");
            }
        }

        // ---- authoring ---------------------------------------------------

        [TestCase("S", ScalingGrade.S)]
        [TestCase("a", ScalingGrade.A)]
        [TestCase(" C ", ScalingGrade.C)]
        [TestCase("", ScalingGrade.None)]
        [TestCase(null, ScalingGrade.None)]
        [TestCase("-", ScalingGrade.None)]
        [TestCase("none", ScalingGrade.None)]
        public void TryParse_ReadsAnAuthoredLetter(string raw, ScalingGrade expected)
        {
            Assert.IsTrue(ScalingGrades.TryParse(raw, out var grade), $"'{raw}' should have parsed");
            Assert.AreEqual(expected, grade);
        }

        [TestCase("Z")]
        [TestCase("SS")]
        [TestCase("7")]
        public void TryParse_RejectsALetterThatIsNotAGrade(string raw)
        {
            Assert.IsFalse(ScalingGrades.TryParse(raw, out _), $"'{raw}' is not a grade and should not have parsed");
        }

        [TestCase("strength", AbilityScore.Strength)]
        [TestCase("DEX", AbilityScore.Dexterity)]
        [TestCase("Intelligence", AbilityScore.Intelligence)]
        public void AbilityScores_TryParse_TakesTheLongOrShortName(string raw, AbilityScore expected)
        {
            Assert.IsTrue(AbilityScores.TryParse(raw, out var score));
            Assert.AreEqual(expected, score);
        }

        [Test]
        public void AbilityScores_TryParse_RejectsNonsense()
        {
            Assert.IsFalse(AbilityScores.TryParse("luck", out _));
            Assert.IsFalse(AbilityScores.TryParse("", out _));
            Assert.IsFalse(AbilityScores.TryParse(null, out _));
        }

        // ---- what the player reads --------------------------------------

        [Test]
        public void Describe_LeadsWithTheStrongestGrade()
        {
            var sword = Riding(AbilityScore.Dexterity, ScalingGrade.C).With(AbilityScore.Strength, ScalingGrade.S);
            Assert.AreEqual("STR <size=110%><font=\"SourceSans3-SemiBold SDF\">S</font></size>  DEX <size=110%><font=\"SourceSans3-SemiBold SDF\">C</font></size>", sword.Describe());
        }

        // QA 2026-09-26: the UI font draws D/0, B/8 and S/5 as one glyph each,
        // so every letter a player reads is drawn in a second face. Sized up
        // 110% too: measured at tooltip size, SourceSans3's capitals sit
        // ~1px short of ChakraPetch's, so the swap would otherwise read as a
        // smaller letter, not just a different one.
        [TestCase(ScalingGrade.S, "<size=110%><font=\"SourceSans3-SemiBold SDF\">S</font></size>")]
        [TestCase(ScalingGrade.A, "<size=110%><font=\"SourceSans3-SemiBold SDF\">A</font></size>")]
        [TestCase(ScalingGrade.B, "<size=110%><font=\"SourceSans3-SemiBold SDF\">B</font></size>")]
        [TestCase(ScalingGrade.C, "<size=110%><font=\"SourceSans3-SemiBold SDF\">C</font></size>")]
        [TestCase(ScalingGrade.D, "<size=110%><font=\"SourceSans3-SemiBold SDF\">D</font></size>")]
        [TestCase(ScalingGrade.E, "<size=110%><font=\"SourceSans3-SemiBold SDF\">E</font></size>")]
        public void Display_DrawsEveryGradeLetterInTheGradeFace(ScalingGrade grade, string expected)
        {
            Assert.AreEqual(expected, ScalingGrades.Display(grade));
        }

        [Test]
        public void Display_OfNone_IsAPlainDash_AndLetterStaysPlainForAuthoringErrors()
        {
            Assert.AreEqual("—", ScalingGrades.Display(ScalingGrade.None));
            Assert.AreEqual("D", ScalingGrades.Letter(ScalingGrade.D));
        }

        [Test]
        public void Describe_OfAProfileThatScalesOnNothing_IsEmptyRatherThanALabelWithNothingAfterIt()
        {
            Assert.AreEqual("", ScalingProfile.None.Describe());
        }
    }
}
