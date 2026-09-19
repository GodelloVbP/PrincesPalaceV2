using System;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // T1 from docs/archive/PLAN_BATTLE_SPEED.md: the preset table's rule list,
    // nothing about the fight that reads it.
    //
    // EVERY EXPECTED VALUE IS A LITERAL, never TodaysPaceDisplay or
    // DefaultDisplay recomputed into an expectation (CODE_STANDARDS.md
    // Sec8) -- a test that derived 1/3 as "0.5f / BattleSpeed.TodaysPaceDisplay"
    // would still pass the day somebody typo'd the baseline into the
    // production file, because both sides would move together.
    public class BattleSpeedTests
    {
        // ---- the table itself --------------------------------------------------

        [Test]
        public void TheBaselineIsFiniteAndPositive()
        {
            Assert.IsTrue(float.IsFinite(BattleSpeed.TodaysPaceDisplay));
            Assert.Greater(BattleSpeed.TodaysPaceDisplay, 0f);
        }

        [Test]
        public void EveryDisplayIsFiniteAndPositive()
        {
            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                Assert.IsTrue(float.IsFinite(row.Display), $"{row.Display} is not finite");
                Assert.Greater(row.Display, 0f);
            }
        }

        [Test]
        public void EveryComputedMultiplierIsFiniteAndPositive()
        {
            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                Assert.IsTrue(float.IsFinite(row.Multiplier), $"display {row.Display} produced a non-finite multiplier");
                Assert.Greater(row.Multiplier, 0f);
            }
        }

        [Test]
        public void DisplaysAreStrictlyAscending()
        {
            float previous = float.NegativeInfinity;

            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                Assert.Greater(row.Display, previous, "a row is not strictly greater than the one before it");
                previous = row.Display;
            }
        }

        [Test]
        public void DisplayNumbersAreUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                Assert.IsTrue(seen.Add(row.DisplayNumber), $"\"{row.DisplayNumber}\" appears on more than one row");
            }
        }

        [Test]
        public void TheDefaultDisplayIsARow()
        {
            bool found = false;

            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                if (row.Display == BattleSpeed.DefaultDisplay)
                    found = true;
            }

            Assert.IsTrue(found, "DefaultDisplay does not match any row's Display");
            Assert.AreEqual(BattleSpeed.DefaultDisplay, BattleSpeed.Default.Display, 0f);
        }

        [Test]
        public void SomeRowsMultiplierIsExactlyOne()
        {
            bool found = false;

            foreach (BattleSpeed.Preset row in BattleSpeed.Rows)
            {
                if (Math.Abs(row.Multiplier - 1f) < 1e-6f)
                    found = true;
            }

            Assert.IsTrue(found, "no row reproduces today's shipped pace (multiplier 1)");
        }

        // The array itself must never be reachable through the public
        // surface -- a caller with a live Preset[] could mutate a row in
        // place and every other reader of Rows would see it.
        [Test]
        public void RowsCannotBeCastBackToTheArray()
        {
            BattleSpeed.Preset[] asArray = BattleSpeed.Rows as BattleSpeed.Preset[];

            Assert.IsNull(asArray);
        }

        // ---- Nearest: contract 7 ------------------------------------------------

        [Test]
        public void AMidpointTieGoesToTheSlowerRow()
        {
            Assert.AreEqual(1f, BattleSpeed.Nearest(1.25f).Display, 0f);
        }

        [Test]
        public void ACloserNeighbourWinsOnEitherSide()
        {
            Assert.AreEqual(1f, BattleSpeed.Nearest(1.2f).Display, 0f);
            Assert.AreEqual(1.5f, BattleSpeed.Nearest(1.3f).Display, 0f);
        }

        [Test]
        public void BelowTheFirstRowSnapsToTheFirstRow()
        {
            Assert.AreEqual(0.5f, BattleSpeed.Nearest(0.1f).Display, 0f);
        }

        [Test]
        public void AboveTheLastRowSnapsToTheLastRow()
        {
            Assert.AreEqual(2f, BattleSpeed.Nearest(9f).Display, 0f);
        }

        [Test]
        public void NonFiniteValuesFallBackToTheDefaultRow()
        {
            Assert.AreEqual(BattleSpeed.Default.Display, BattleSpeed.Nearest(float.NaN).Display, 0f);
            Assert.AreEqual(BattleSpeed.Default.Display, BattleSpeed.Nearest(float.PositiveInfinity).Display, 0f);
        }

        [Test]
        public void AnExactValueMapsToItself()
        {
            Assert.AreEqual(1.5f, BattleSpeed.Nearest(1.5f).Display, 0f);
        }

        // ---- pinned literals: the table exactly as authored --------------------

        // Display numbers, pinned as the literal strings the Options row
        // will actually show -- not recomputed from Display via ToString
        // inside this test, or a formatting regression would grade itself.
        [Test]
        public void DisplayNumbersAreExactlyTheAuthoredStrings()
        {
            string[] expected = { "0.5", "1", "1.5", "2" };
            var rows = BattleSpeed.Rows;

            Assert.AreEqual(expected.Length, rows.Count, "row count moved without this test being updated");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rows[i].DisplayNumber, $"row {i}");
            }
        }

        // Multipliers, pinned as literal fractions -- see the class header
        // for why these are not computed from TodaysPaceDisplay here.
        [Test]
        public void MultipliersAreExactlyTheAuthoredFractions()
        {
            float[] expected = { 1f / 3f, 2f / 3f, 1f, 4f / 3f };
            var rows = BattleSpeed.Rows;

            Assert.AreEqual(expected.Length, rows.Count, "row count moved without this test being updated");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rows[i].Multiplier, 1e-6f, $"row {i}");
            }
        }
    }
}
