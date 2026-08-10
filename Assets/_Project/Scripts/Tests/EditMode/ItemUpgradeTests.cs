using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class ItemUpgradeTests
    {
        // PINNED LITERALS rather than a recomputed 1 + 0.04 * plus. Calling
        // the formula to build its own expected value would assert only that
        // the method is deterministic (AUDIT.md #18); these numbers assert
        // that the CURVE is the one that was designed, and a retune has to
        // come here and say so.
        [TestCase(0, 1.00f)]
        [TestCase(1, 1.04f)]
        [TestCase(5, 1.20f)]
        [TestCase(10, 1.40f)]
        public void MultiplierFor_WalksFourPercentPerPlus(int plus, float expected)
        {
            Assert.AreEqual(expected, ItemUpgrade.MultiplierFor(plus), 0.0001f);
        }

        // Plus arrives from save data, which a player can edit and a
        // truncated write can corrupt. Neither may hand a stat consumer an
        // arbitrary multiplier.
        [Test]
        public void MultiplierFor_ClampsBothEnds()
        {
            Assert.AreEqual(1f, ItemUpgrade.MultiplierFor(-5), 0.0001f, "A negative plus is no bonus, not a penalty");
            Assert.AreEqual(ItemUpgrade.MultiplierFor(ItemUpgrade.MaxPlus), ItemUpgrade.MultiplierFor(9999), 0.0001f);
        }

        [Test]
        public void PlusZero_ChangesNothing()
        {
            Assert.AreEqual(17, ItemUpgrade.Apply(17, 0));
            Assert.AreEqual(-6, ItemUpgrade.Apply(-6, 0));
            Assert.AreEqual(0, ItemUpgrade.Apply(0, 7));
        }

        // 20 * 1.40 = 28 exactly; 17 * 1.20 = 20.4 floors to 20.
        [TestCase(20, 10, 28)]
        [TestCase(17, 5, 20)]
        [TestCase(1, 10, 1)]
        public void Apply_ScalesAndFloors(int amount, int plus, int expected)
        {
            Assert.AreEqual(expected, ItemUpgrade.Apply(amount, plus));
        }

        // REGRESSION. Apply used to route through MultiplierFor and floor the
        // float, and 1.4f is really 1.39999997615814208984375 — so 20 at +10
        // came out as 27 rather than 28. Every exact-looking case is one a
        // player is likely to check by eye, which makes it the worst possible
        // place to be one short. CLAUDE.md gotcha 5, on the other side of the
        // fence: not a test recomputing a formula, but a formula that could
        // not survive being computed in float32.
        [TestCase(20, 10, 28)]
        [TestCase(50, 10, 70)]
        [TestCase(25, 5, 30)]
        [TestCase(100, 10, 140)]
        public void Apply_IsExactWhereTheArithmeticIsExact(int amount, int plus, int expected)
        {
            Assert.AreEqual(expected, ItemUpgrade.Apply(amount, plus),
                $"{amount} at +{plus} should be exactly {expected} — float32 rounding must not cost a point");
        }

        // Steel is slow on purpose, so items carry negative stats — and a
        // penalty must floor the SAME DIRECTION as a bonus. A plain (int)
        // cast truncates toward zero, which would quietly make honing a
        // steel platebody reduce its own speed penalty. Same asymmetry
        // ItemSetEntryResolver.ValueAt and AbilityDerivation.FloorDiv2 guard.
        [TestCase(-2, 5, -3)]
        [TestCase(-10, 10, -14)]
        public void Apply_FloorsAPenaltyDownwardsToo(int amount, int plus, int expected)
        {
            Assert.AreEqual(expected, ItemUpgrade.Apply(amount, plus));
        }

        // Honing must never make an item worse on any stat it grants — the
        // same monotonicity the tier ladder promises, on the other axis.
        [Test]
        public void Apply_NeverRegressesAsPlusRises()
        {
            foreach (int amount in new[] { 1, 3, 20, 137 })
            {
                for (int plus = 1; plus <= ItemUpgrade.MaxPlus; plus++)
                {
                    Assert.GreaterOrEqual(ItemUpgrade.Apply(amount, plus), ItemUpgrade.Apply(amount, plus - 1),
                        $"{amount} at +{plus} is worth less than at +{plus - 1}");
                }
            }
        }

        // The design claim the whole two-axis split rests on: a well-honed
        // low-tier piece stays comparable to a fresh higher-tier one, so an
        // early drop is worth investing in. If plus were worth much more than
        // this, tier — and with it the rarity bands — would stop mattering.
        [Test]
        public void AFullyHonedItem_IsWorthFortyPercentMore_AndNoMore()
        {
            Assert.AreEqual(1.40f, ItemUpgrade.MultiplierFor(ItemUpgrade.MaxPlus), 0.0001f);
            Assert.AreEqual(10, ItemUpgrade.MaxPlus, "The two axes are the same length by design");
        }
    }
}
