using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The character sheet's stat rows.
    //
    // The sheet used to show no numbers at all -- the one screen for deciding
    // what to wear never said what wearing it did. These pin the row list, its
    // order, and which field each row reads, because the tree lays out
    // thirteen boxes and the controller fills a flat array of thirteen values:
    // if those two ever disagree about position 7, the sheet shows Attack
    // under the heading Speed and nothing throws.
    public class CharacterSheetStatsTests
    {
        [Test]
        public void EveryStatAppearsExactlyOnce()
        {
            var declared = Enum.GetValues(typeof(SheetStat)).Cast<SheetStat>().ToList();

            CollectionAssert.AreEquivalent(declared, SheetStats.All,
                "a stat exists in the enum that no column lays out, or the other way round");
            Assert.AreEqual(declared.Count, SheetStats.All.Distinct().Count(),
                "a stat is laid out twice");
        }

        // All is the flat order the controller fills, and it has to be the two
        // columns end to end -- abilities first. Restating it as a literal
        // would be the second copy this class exists to prevent, so the check
        // is structural.
        [Test]
        public void TheFlatOrderIsTheTwoColumnsEndToEnd()
        {
            CollectionAssert.AreEqual(
                SheetStats.Abilities.Concat(SheetStats.Derived).ToList(),
                SheetStats.All);
        }

        [Test]
        public void TheColumnsDoNotShareAStat()
        {
            CollectionAssert.IsEmpty(SheetStats.Abilities.Intersect(SheetStats.Derived).ToList());
        }

        [Test]
        public void EveryStatHasItsOwnLabel()
        {
            var keys = SheetStats.All.Select(s => SheetStats.LabelFor(s).Key).ToList();

            Assert.AreEqual(keys.Count, keys.Distinct().Count(),
                "two stats share a label, so one row is captioned as another");
            CollectionAssert.IsEmpty(keys.Where(string.IsNullOrEmpty).ToList());
        }

        // Distinct values in every field, so a row reading the wrong one
        // cannot pass by coincidence.
        private static StatBlock Stats() => new StatBlock
        {
            maxHealth = 101,
            attack = 102,
            speed = 104,
            manaRegen = 105,
            physicalDefense = 106,
            magicalDefense = 107,
        };

        private static AbilityScoreBlock Scores() => new AbilityScoreBlock
        {
            strength = 11,
            dexterity = 12,
            constitution = 13,
            wisdom = 14,
            intelligence = 15,
            charisma = 16,
        };

        [TestCase(SheetStat.Strength, 11)]
        [TestCase(SheetStat.Dexterity, 12)]
        [TestCase(SheetStat.Constitution, 13)]
        [TestCase(SheetStat.Wisdom, 14)]
        [TestCase(SheetStat.Intelligence, 15)]
        [TestCase(SheetStat.Charisma, 16)]
        [TestCase(SheetStat.MaxHealth, 101)]
        [TestCase(SheetStat.Attack, 102)]
        [TestCase(SheetStat.Speed, 104)]
        [TestCase(SheetStat.ManaRegen, 105)]
        [TestCase(SheetStat.PhysicalDefense, 106)]
        [TestCase(SheetStat.MagicalDefense, 107)]
        public void EachRowReadsItsOwnField(SheetStat stat, int expected)
        {
            Assert.AreEqual(expected, SheetStats.ValueOf(stat, Stats(), Scores()));
        }

        // Physical DEF was the example given for what the sheet had to show,
        // and it is also one of the two figures that reached EffectiveStats and
        // then got dropped on the way into combat. Worth its own line.
        [Test]
        public void ResistancesAreShownRatherThanQuietlyOmitted()
        {
            CollectionAssert.Contains(SheetStats.All, SheetStat.PhysicalDefense);
            CollectionAssert.Contains(SheetStats.All, SheetStat.MagicalDefense);
        }

        // ---- what the row actually shows (D7.3) ------------------------------

        [Test]
        public void PhysicalDefenseRendersTheMitigationCurve()
        {
            // Same pinned figure the design doc and ItemStatLinesTests both
            // use: 50 Defense is 33% less damage. Compact ("50 (33%)")
            // rather than a full sentence -- see DisplayText's own header
            // for why this one row cannot afford the longer text.
            Assert.AreEqual("50 (33%)", SheetStats.DisplayText(SheetStat.PhysicalDefense, 50));
        }

        [Test]
        public void MagicalDefenseRendersTheMitigationCurveTheSameWay()
        {
            Assert.AreEqual("50 (33%)", SheetStats.DisplayText(SheetStat.MagicalDefense, 50));
        }

        [Test]
        public void EveryOtherRowIsJustTheBareNumber()
        {
            // The percentage read is specific to the two Defense rows -- a
            // sheet where every number grew a parenthetical would bury the
            // two that actually need one.
            Assert.AreEqual("104", SheetStats.DisplayText(SheetStat.MaxHealth, 104));
            Assert.AreEqual("14", SheetStats.DisplayText(SheetStat.Wisdom, 14));
        }
    }
}
