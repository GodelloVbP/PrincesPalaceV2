using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Tests
{
    public class DifficultyCurveTests
    {
        // PINNED LITERALS. The curve is the design decision; recomputing
        // 1 + 0.055 * step here would assert only that the method is
        // deterministic (CLAUDE.md gotcha 5, AUDIT.md #18).
        [TestCase(0, 1.000f)]
        [TestCase(1, 1.055f)]
        [TestCase(8, 1.440f)]
        [TestCase(16, 1.880f)]
        [TestCase(40, 3.200f)]
        [TestCase(80, 5.400f)]
        public void EnemyMultiplier_ClimbsInAStraightLine(int step, float expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.EnemyMultiplier(step), 0.0001f);
        }

        // LINEAR, not geometric, and this is the assertion that says so.
        //
        // At 5.5% compounding, step 80 would be about 72x — far past
        // anything the item ladder can answer, since a fully honed top-tier
        // set is worth roughly 4x a starting one. Linear keeps the player's
        // power and the dungeon's climbing at comparable rates.
        [Test]
        public void TheCurveIsLinear_NotCompounding()
        {
            float atForty = DifficultyCurve.EnemyMultiplier(40) - 1f;
            float atEighty = DifficultyCurve.EnemyMultiplier(80) - 1f;

            Assert.AreEqual(2f * atForty, atEighty, 0.0001f,
                "Twice the depth should be twice the climb, not the square of it");
        }

        [Test]
        public void StepZero_ChangesNothing()
        {
            Assert.AreEqual(1f, DifficultyCurve.EnemyMultiplier(0), 0.0001f);
            Assert.AreEqual(37, DifficultyCurve.Scale(37, 0));
            Assert.AreEqual(37, DifficultyCurve.Scale(37, -12), "A negative step is the surface, not a discount");
        }

        // Integer arithmetic throughout, so exact-looking cases stay exact —
        // the same float32 trap ItemUpgrade hit, where 20 * 1.4f floors to
        // 27 rather than 28.
        [TestCase(100, 8, 144)]
        [TestCase(200, 16, 376)]
        [TestCase(1000, 40, 3200)]
        public void Scale_IsExactWhereTheArithmeticIsExact(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.Scale(amount, step));
        }

        [Test]
        public void Scale_NeverRegressesAsTheRunGoesDeeper()
        {
            foreach (int amount in new[] { 1, 12, 240, 3000 })
            {
                for (int step = 1; step <= 120; step++)
                {
                    Assert.GreaterOrEqual(DifficultyCurve.Scale(amount, step), DifficultyCurve.Scale(amount, step - 1),
                        $"{amount} got weaker between step {step - 1} and {step}");
                }
            }
        }

        // `step` comes off a save and nothing else bounds it. An unclamped
        // multiplication would overflow into a NEGATIVE enemy — one with
        // negative health, which every combat check would read as already
        // dead.
        [Test]
        public void AnAbsurdStep_IsClampedRatherThanOverflowing()
        {
            int atCeiling = DifficultyCurve.Scale(1000, DifficultyCurve.MaxScaledStep);

            Assert.Greater(atCeiling, 0);
            Assert.AreEqual(atCeiling, DifficultyCurve.Scale(1000, 999999));
            Assert.Greater(DifficultyCurve.Scale(int.MaxValue / 100000, 999999), 0);
        }

        // Reward and threat ride the SAME curve. If pay lagged difficulty,
        // deep fights would be worse value per fight and the optimal play in
        // an endless dungeon would be to farm shallow rooms forever.
        [Test]
        public void RewardsRideTheSameCurveAsTheThreat()
        {
            foreach (int step in new[] { 0, 8, 16, 40, 100 })
            {
                Assert.AreEqual(DifficultyCurve.Scale(250, step), DifficultyCurve.ScaleReward(250, step),
                    $"pay and threat disagree at step {step}");
            }
        }

        // The shape the whole phase exists to create: a fight deep in a run
        // has to be meaningfully harder than one at the surface. Before this,
        // a floor-9 fight was statistically identical to a floor-1 fight.
        [Test]
        public void ADeepFight_IsSubstantiallyHarderThanASurfaceOne()
        {
            int surface = DifficultyCurve.Scale(100, 1);
            int deep = DifficultyCurve.Scale(100, 40);

            Assert.Greater(deep, surface * 2,
                "Forty steps down should be more than twice the fight, or descending means nothing");
        }
    }
}
