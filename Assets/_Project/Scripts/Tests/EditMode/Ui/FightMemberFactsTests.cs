using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The fight-facts shape the system menu reads (plan C4): the sum of what
    // the fight has done to a stat, and who did it.
    public class FightMemberFactsTests
    {
        private static FightMemberFacts With(params FightStatModifier[] modifiers) =>
            new FightMemberFacts("sheep", 180, 240, "Mana", 20, 30, "Wool", 3, 10, modifiers);

        [Test]
        public void ANewMemberHasNoModifiers()
        {
            var facts = With();

            Assert.IsFalse(facts.Modifies(SheetStat.Speed));
            Assert.AreEqual(0, facts.DeltaFor(SheetStat.Speed));
            Assert.AreEqual("", facts.SourcesFor(SheetStat.Speed));
        }

        [Test]
        public void ModifiersOnOneStatSumAndNameEachSourceOnceInOrder()
        {
            var facts = With(
                new FightStatModifier(SheetStat.Speed, 3, "Stampede"),
                new FightStatModifier(SheetStat.PhysicalDefense, -10, "Armour shred"),
                new FightStatModifier(SheetStat.Speed, -1, "Chilled"),
                new FightStatModifier(SheetStat.Speed, 2, "Stampede"));

            Assert.AreEqual(4, facts.DeltaFor(SheetStat.Speed));
            Assert.AreEqual("Stampede, Chilled", facts.SourcesFor(SheetStat.Speed));
            Assert.AreEqual(-10, facts.DeltaFor(SheetStat.PhysicalDefense));
            Assert.AreEqual("Armour shred", facts.SourcesFor(SheetStat.PhysicalDefense));
            Assert.AreEqual(0, facts.DeltaFor(SheetStat.Attack));
        }

        [Test]
        public void ModifiersThatCancelStillCountAsAChange()
        {
            var facts = With(
                new FightStatModifier(SheetStat.Speed, 3, "Stampede"),
                new FightStatModifier(SheetStat.Speed, -3, "Chilled"));

            Assert.AreEqual(0, facts.DeltaFor(SheetStat.Speed));
            Assert.IsTrue(facts.Modifies(SheetStat.Speed));
        }

        [Test]
        public void AZeroDeltaIsNotAChange()
        {
            Assert.IsFalse(With(new FightStatModifier(SheetStat.Speed, 0, "Nothing")).Modifies(SheetStat.Speed));
        }

        [Test]
        public void ASignatureNameSaysWhetherTheMemberHasOne()
        {
            Assert.IsTrue(With().HasSignature);
            Assert.IsFalse(new FightMemberFacts("bear", 300, 300, "Fury", 0, 100, null, 0, 0, null).HasSignature);
        }
    }
}
