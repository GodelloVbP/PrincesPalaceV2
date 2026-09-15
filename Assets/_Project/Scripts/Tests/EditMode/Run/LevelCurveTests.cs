using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // THE ARITHMETIC OVER A COST TABLE, not the table itself.
    //
    // The authored numbers and their three authoring rules are pinned in
    // LevelCurveContentPinTests, against the real level_curve.json. What is
    // here is the indexing, the cap and the level-up loop -- the half that
    // has to be right whatever the content says, exercised over a table small
    // enough to check by eye.
    //
    // Progression v2 phase 2 replaced a geometric formula (BaseCost 100, 90
    // permille a level, cap 200) with that table, so the old per-level
    // literals here are gone rather than retuned: there is no longer a
    // formula for them to be literals OF.
    public class LevelCurveTests
    {
        // Four levels' worth: 2 costs 10, 3 costs 20, 4 costs 20, 5 costs 40.
        // Deliberately holds a repeat, because the authored table does (17
        // and 18 both cost 4,000) and a flat step must not read as the end of
        // the table.
        private static readonly IReadOnlyList<int> Table = new[] { 10, 20, 20, 40 };

        // ---- what a level costs ---------------------------------------------

        [TestCase(1, 10)]
        [TestCase(2, 20)]
        [TestCase(3, 20)]
        [TestCase(4, 40)]
        public void ALevelCostsWhatTheTableSays(int level, int expected)
        {
            Assert.AreEqual(expected, LevelCurve.ExpToNextLevel(Table, level));
        }

        [Test]
        public void TheTablesLastRowIsTheCap()
        {
            Assert.AreEqual(5, LevelCurve.MaxLevel(Table));
        }

        // AT AND PAST THE CAP the last authored cost is what is quoted, so
        // the dossier's experience bar has a denominator that fills rather
        // than a sentinel it can never approach. Nothing levels off it -- the
        // cap is enforced in AddExperience, once.
        [TestCase(5)]
        [TestCase(40)]
        [TestCase(int.MaxValue)]
        public void PastTheCapTheLastCostIsQuoted(int level)
        {
            Assert.AreEqual(40, LevelCurve.ExpToNextLevel(Table, level));
        }

        // A save can hold any level at all -- this one is defensive, not
        // balance. A non-positive requirement would make AddExperience's
        // `newExp >= cost` always true, which is a spin rather than a wrong
        // answer.
        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(190)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void NoLevelProducesACostThatWouldHangTheLevelUpLoop(int level)
        {
            Assert.Greater(LevelCurve.ExpToNextLevel(Table, level), 0,
                $"level {level} produced a non-positive requirement");
        }

        // NO CONTENT AT ALL -- an Editor session before the first build, a
        // test holding the catalogue empty. The answer is "unreachable",
        // never a guessed default: a character levelling on numbers nobody
        // authored is the plausible-wrong answer this project refuses.
        [Test]
        public void AnAbsentTableStopsLevellingRatherThanGuessing()
        {
            Assert.AreEqual(LevelCurve.NoTableCost, LevelCurve.ExpToNextLevel(null, 1));
            Assert.AreEqual(LevelCurve.NoTableCost, LevelCurve.ExpToNextLevel(new int[0], 1));

            var after = LevelCurve.AddExperience(new int[0], level: 1, exp: 0, amount: 1_000_000);

            Assert.AreEqual(1, after.Level);
            Assert.AreEqual(0, after.LevelsGained);
        }

        // ---- cumulative ------------------------------------------------------

        // What the track is actually priced against -- a player asks "how
        // long to 40", not "what does 23 cost".
        [TestCase(1, 0)]
        [TestCase(2, 10)]
        [TestCase(3, 30)]
        [TestCase(5, 90)]
        [TestCase(99, 90)]
        public void ReachingALevelCostsThisMuchInTotal(int level, int expected)
        {
            Assert.AreEqual(expected, LevelCurve.CumulativeCost(Table, level));
        }

        // ---- the level-up loop -----------------------------------------------

        [Test]
        public void ExperienceShortOfTheNextLevelJustBanks()
        {
            var after = LevelCurve.AddExperience(Table, level: 1, exp: 0, amount: 9);

            Assert.AreEqual(1, after.Level);
            Assert.AreEqual(9, after.Exp);
            Assert.AreEqual(0, after.LevelsGained);
        }

        [Test]
        public void OneBigGainCanCrossSeveralLevelsAtOnce()
        {
            // 10 + 20 + 20 = 50 buys levels 2, 3 and 4, leaving 5 against
            // level 5's 40.
            var after = LevelCurve.AddExperience(Table, level: 1, exp: 0, amount: 55);

            Assert.AreEqual(4, after.Level);
            Assert.AreEqual(5, after.Exp);
            Assert.AreEqual(3, after.LevelsGained);
        }

        [Test]
        public void ExactlyEnoughLevelsUp()
        {
            var after = LevelCurve.AddExperience(Table, level: 1, exp: 0, amount: 10);

            Assert.AreEqual(2, after.Level);
            Assert.AreEqual(0, after.Exp);
            Assert.AreEqual(1, after.LevelsGained);
        }

        [Test]
        public void ANonPositiveGainChangesNothing()
        {
            var after = LevelCurve.AddExperience(Table, level: 3, exp: 7, amount: 0);

            Assert.AreEqual(3, after.Level);
            Assert.AreEqual(7, after.Exp);
            Assert.AreEqual(0, after.LevelsGained);
        }

        // THE CAP, which is new in progression v2 phase 2. Nothing used to
        // bound `level`: income alone could carry a character past the last
        // authored node, and the track would answer TrackEntry.None forever
        // after. Harmless at a cap of 100 nobody reached; at 40, which a
        // career reaches on run 24, it would have every later run advertise
        // levels that pay nothing.
        [Test]
        public void LevellingStopsAtTheCapAndKeepsTheOverflow()
        {
            var after = LevelCurve.AddExperience(Table, level: 1, exp: 0, amount: 1000);

            Assert.AreEqual(5, after.Level, "the table's last row is level 5");
            Assert.AreEqual(4, after.LevelsGained);

            // 1000 - (10 + 20 + 20 + 40) = 910, kept rather than discarded:
            // throwing away a player's last fight because they happened to be
            // at the cap is a worse answer than a number that stops
            // mattering.
            Assert.AreEqual(910, after.Exp);
        }

        [Test]
        public void ACappedCharacterNeverLevelsAgain()
        {
            var after = LevelCurve.AddExperience(Table, level: 5, exp: 0, amount: 100_000);

            Assert.AreEqual(5, after.Level);
            Assert.AreEqual(0, after.LevelsGained);
            Assert.AreEqual(100_000, after.Exp);
        }

        // ---- the race it is in -----------------------------------------------

        // Income compounds with depth (DifficultyCurve.ScaleExperience, 25
        // permille a step) and the cost table climbs with level. The
        // relationship that matters is no longer "one rate beats the other"
        // -- there is no cost rate any more -- but that the table's own steps
        // are much bigger than one step of income, so the track slows down
        // with level rather than speeding up with depth.
        //
        // The income rate is READ, not restated: writing 1.025 here would put
        // the number this assertion depends on in a second place nothing
        // keeps in sync.
        [Test]
        public void TheTableClimbsFasterThanIncomeDoesPerStep()
        {
            double incomeStep = Dungeon.DifficultyCurve.ExperienceMultiplier(1);

            // Level 5's 40 against level 4's 20 is a doubling; one step of
            // depth is 2.5%.
            double tableStep = LevelCurve.ExpToNextLevel(Table, 4) / (double)LevelCurve.ExpToNextLevel(Table, 3);

            Assert.Greater(tableStep, incomeStep,
                "a level costs less more than a step of depth pays more, so the track speeds up with depth");
        }
    }
}
