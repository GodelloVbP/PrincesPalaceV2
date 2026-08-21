using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    public class LevelCurveTests
    {
        // PINNED LITERALS, computed once and written down. Recomputing
        // 100 * 1.09^level here would assert only that the method is
        // deterministic -- CLAUDE.md gotcha 5, AUDIT.md #18, and the same
        // stance DifficultyCurveTests takes two files over.
        //
        // Level 10 is the first milestone, 25 and 45 the relic slots, 50 the
        // midpoint, 100 the end of the track.
        [TestCase(1, 109)]
        [TestCase(10, 236)]
        [TestCase(25, 862)]
        [TestCase(45, 4832)]
        [TestCase(50, 7435)]
        [TestCase(100, 552904)]
        public void ALevelCostsWhatTheCurveSays(int level, int expected)
        {
            Assert.AreEqual(expected, LevelCurve.ExpToNextLevel(level));
        }

        // The whole point of the retune, as one assertion.
        //
        // XP income compounds at DifficultyCurve's health rate per depth step,
        // because ScaleReward IS ScaleHealth. A cost curve that does not
        // outrun that is a curve where levels get EASIER the deeper you go,
        // which is what `level * 100` was doing: it put level 100 within four
        // deep runs.
        //
        // The income rate is READ, not restated. HealthPermillePerStep is
        // private, and writing 1.077 here would put the number this assertion
        // depends on in a second place that nothing keeps in sync -- one step
        // of the public multiplier is the same fact, sourced from the file
        // that owns it.
        [Test]
        public void TheCurveOutrunsTheRewardCurveItIsRacing()
        {
            double levelRate = 1d + LevelCurve.PermillePerLevel / 1000d;
            double rewardRate = Dungeon.DifficultyCurve.HealthMultiplier(1);

            Assert.Greater(levelRate, rewardRate,
                "levels cost less per step than a fight pays per step, so the track speeds up " +
                "with depth instead of slowing down");
        }

        // Cumulative cost is what the track is actually priced against -- a
        // player asks "how long to 100", not "what does level 63 cost". Pinned
        // at the same literal standard as the per-level costs.
        [TestCase(10, 1651)]
        [TestCase(50, 88818)]
        [TestCase(100, 6695023)]
        public void ReachingALevelCostsThisMuchInTotal(int level, int expected)
        {
            long total = 0;
            for (int l = 1; l <= level; l++)
            {
                total += LevelCurve.ExpToNextLevel(l);
            }

            Assert.AreEqual(expected, total);
        }

        // A save can hold any level at all -- this one is defensive, not
        // balance. 9% compounding passes int around level 190, and a wrapped
        // cost reads as a NEGATIVE requirement, which AddExperience's
        // `while (exp >= ExpToNextLevel(level))` treats as always met and
        // spins on forever.
        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(190)]
        [TestCase(1000)]
        [TestCase(int.MaxValue)]
        public void NoLevelProducesACostThatWouldHangTheLevelUpLoop(int level)
        {
            Assert.Greater(LevelCurve.ExpToNextLevel(level), 0,
                $"level {level} produced a non-positive requirement");
        }
    }
}
