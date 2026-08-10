using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Every expectation here is a PINNED LITERAL. Nothing in this file
    // recomputes a production formula, calls the method under test to build
    // its own expected value, or reaches for a constant from
    // AbilityDerivation — all three are ways a test moves in lockstep with
    // the code and stops being able to fail (CLAUDE.md gotcha #5,
    // AUDIT.md #18).
    public class AbilityDerivationTests
    {
        private static AbilityScoreBlock Scores(int str = 10, int dex = 10, int con = 10, int wis = 10, int intel = 10, int cha = 10)
        {
            return new AbilityScoreBlock(str, dex, con, wis, intel, cha);
        }

        // THE compatibility guarantee, and the reason this layer could be
        // added to a shipping game without rebalancing anything. Every
        // character in the game currently carries a flat 10 across the
        // board; if any of these stopped being zero, all five would silently
        // change stats at once.
        [Test]
        public void AllTens_DeriveExactlyZeroOnEveryStat()
        {
            var neutral = Scores();

            Assert.AreEqual(0, AbilityDerivation.AttackBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.SpeedBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.MaxHealthBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.MaxManaBonus(neutral));
            Assert.AreEqual(StatBlock.Zero, AbilityDerivation.DerivedStats(neutral));
        }

        [TestCase(4, -3)]
        [TestCase(5, -3)]
        [TestCase(6, -2)]
        [TestCase(7, -2)]
        [TestCase(8, -1)]
        [TestCase(9, -1)]
        [TestCase(10, 0)]
        [TestCase(11, 0)]
        [TestCase(12, 1)]
        [TestCase(13, 1)]
        [TestCase(14, 2)]
        [TestCase(15, 2)]
        [TestCase(16, 3)]
        [TestCase(20, 5)]
        public void Modifier_IsPinnedAcrossTheAuthorableRange(int score, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.Modifier(score));
        }

        // C# integer division truncates toward zero, so a naive (score-10)/2
        // gives 0 for both 9 and 10 while giving -1 for 8 — bonuses would
        // step every two points but penalties every two points offset by
        // one. The asymmetry is invisible until someone authors a
        // below-neutral score, which is exactly what Shawn does.
        [Test]
        public void Modifier_FloorsBelowNeutralRatherThanTruncatingTowardZero()
        {
            Assert.AreEqual(-1, AbilityDerivation.Modifier(9), "9 must be a penalty, not a rounding-to-zero");

            // The curve is symmetric about 10.5, NOT about 10 — scores pair
            // up as (10,11) -> 0, (12,13) -> +1, (8,9) -> -1. That is the
            // classic modifier and it is deliberate; do not "fix" 11 into a
            // bonus to make it look balanced around 10.
            Assert.AreEqual(AbilityDerivation.Modifier(10), AbilityDerivation.Modifier(11), "10 and 11 pair");
            Assert.AreEqual(AbilityDerivation.Modifier(8), AbilityDerivation.Modifier(9), "8 and 9 pair");

            // Monotonic, and stepping exactly once per two points. Truncation
            // breaks this: it produces a three-wide plateau at 9/10/11 while
            // every other step is two wide.
            for (int score = 3; score <= 20; score++)
            {
                int step = AbilityDerivation.Modifier(score) - AbilityDerivation.Modifier(score - 1);
                Assert.IsTrue(step == 0 || step == 1, $"Modifier jumped by {step} between {score - 1} and {score}");
            }

            for (int k = -3; k <= 5; k++)
            {
                Assert.AreEqual(k, AbilityDerivation.Modifier(10 + 2 * k), $"Every even offset should land exactly on {k}");
            }
        }

        [TestCase(-3, -2)]
        [TestCase(-2, -1)]
        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        public void FloorDiv2_IsPinned(int value, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.FloorDiv2(value));
        }

        [Test]
        public void Strength_MovesAttackByOnePerTwoPoints()
        {
            Assert.AreEqual(0, AbilityDerivation.AttackBonus(Scores(str: 11)), "One point alone buys nothing");
            Assert.AreEqual(1, AbilityDerivation.AttackBonus(Scores(str: 12)));
            Assert.AreEqual(-1, AbilityDerivation.AttackBonus(Scores(str: 8)));
        }

        [Test]
        public void Dexterity_MovesSpeedByOnePerTwoPoints()
        {
            Assert.AreEqual(1, AbilityDerivation.SpeedBonus(Scores(dex: 12)));
            Assert.AreEqual(-1, AbilityDerivation.SpeedBonus(Scores(dex: 9)));
        }

        // 20 per point, not 2: HP pools moved to a x10 scale, and Constitution
        // had to move with them or the score would have stopped mattering.
        [Test]
        public void Constitution_MovesMaxHealthByTwentyPerPoint()
        {
            Assert.AreEqual(120, AbilityDerivation.MaxHealthBonus(Scores(con: 16)));
            Assert.AreEqual(-40, AbilityDerivation.MaxHealthBonus(Scores(con: 8)));
        }

        [Test]
        public void Wisdom_MovesMaxManaByTwoPerPoint()
        {
            Assert.AreEqual(10, AbilityDerivation.MaxManaBonus(Scores(wis: 15)));
            Assert.AreEqual(-6, AbilityDerivation.MaxManaBonus(Scores(wis: 7)));
        }

        // The scores are independent inputs; moving one must not disturb
        // another's output. Cheap to assert, and the kind of thing a
        // copy-paste slip between six near-identical methods breaks silently.
        [Test]
        public void DerivedStats_CombinesTheThreeStatScoresAndTouchesNothingElse()
        {
            var block = AbilityDerivation.DerivedStats(Scores(str: 14, dex: 8, con: 16, wis: 20, intel: 20, cha: 20));

            Assert.AreEqual(120, block.maxHealth, "CON 16");
            Assert.AreEqual(-1, block.speed, "DEX 8");
            Assert.AreEqual(2, block.attack, "STR 14");
            Assert.AreEqual(0, block.defense, "No ability score feeds Defense");
        }

        // Shawn's authored spread, pinned here so his identity is a fact the
        // suite protects rather than a number in a builder someone can
        // adjust without noticing what it does to him.
        [Test]
        public void ShawnsSpread_DerivesHisIntendedProfile()
        {
            var shawn = Scores(str: 8, dex: 9, con: 16, wis: 15, intel: 10, cha: 14);

            Assert.AreEqual(-1, AbilityDerivation.AttackBonus(shawn), "STR 8: he is a sheep");
            Assert.AreEqual(-1, AbilityDerivation.SpeedBonus(shawn), "DEX 9: woolly and unhurried");
            Assert.AreEqual(120, AbilityDerivation.MaxHealthBonus(shawn), "CON 16: his defining stat");
            Assert.AreEqual(10, AbilityDerivation.MaxManaBonus(shawn), "WIS 15: shepherd's patience");
        }
    }
}
