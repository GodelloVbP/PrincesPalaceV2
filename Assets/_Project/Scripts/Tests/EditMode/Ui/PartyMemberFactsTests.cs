using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHICH SOURCE DECIDES A SEAT'S HEALTH AND DEFENCES (plan C4, C22): camp is
    // full, a descent is what the run carries, and a fight is what the HUD is
    // showing -- never the run's carried figure, which is written only when
    // the fight ends. The wiring that reads each place is PartySeatFactsTests'.
    public class PartyMemberFactsTests
    {
        private static FightMemberFacts Fight(int health, int max, params FightStatModifier[] modifiers) =>
            new FightMemberFacts("sheep", health, max, "Mana", 10, 20, null, 0, 0, modifiers);

        [Test]
        public void CampIsFullWhateverTheRunCarries()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Camp, 240, 40, 20, carriedHealth: 100, fight: null);

            Assert.AreEqual(240, facts.Health);
            Assert.AreEqual(240, facts.MaxHealth);
            Assert.AreEqual(40, facts.PhysicalDefense);
            Assert.AreEqual(20, facts.MagicalDefense);
        }

        [Test]
        public void ADescentReadsWhatTheRunCarries()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Descent, 240, 40, 20, carriedHealth: 100, fight: null);

            Assert.AreEqual(100, facts.Health);
            Assert.AreEqual(240, facts.MaxHealth);
        }

        [Test]
        public void ADescentWithNoEntryForACharacterIsFull()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Descent, 240, 40, 20, carriedHealth: null, fight: null);

            Assert.AreEqual(240, facts.Health);
        }

        [Test]
        public void ACarriedFigureOverTheMaximumIsClamped()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Descent, 240, 40, 20, carriedHealth: 900, fight: null);

            Assert.AreEqual(240, facts.Health);
        }

        [Test]
        public void ADownedCharacterReadsZero()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Descent, 240, 40, 20, carriedHealth: 0, fight: null);

            Assert.AreEqual(0, facts.Health);
            Assert.AreEqual(0f, facts.HealthFraction, 0.0001f);
        }

        [Test]
        public void AFightReadsTheHudsFactsAndNotTheRunsCarriedFigure()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Fight, 240, 40, 20, carriedHealth: 100, fight: Fight(180, 260));

            Assert.AreEqual(180, facts.Health);
            Assert.AreEqual(260, facts.MaxHealth, "the fight's own maximum");
        }

        [Test]
        public void AFightsDefenceDeltasLandOnTheSheetsDefences()
        {
            var fight = Fight(180, 260,
                new FightStatModifier(SheetStat.PhysicalDefense, -15, "Sundered"),
                new FightStatModifier(SheetStat.MagicalDefense, 5, "Ward"));

            var facts = PartyMemberFacts.Resolve(MenuContext.Fight, 240, 40, 20, carriedHealth: null, fight: fight);

            Assert.AreEqual(25, facts.PhysicalDefense);
            Assert.AreEqual(25, facts.MagicalDefense);
        }

        [Test]
        public void ADefenceShreddedPastZeroReadsZero()
        {
            var fight = Fight(180, 260, new FightStatModifier(SheetStat.PhysicalDefense, -60, "Sundered"));

            var facts = PartyMemberFacts.Resolve(MenuContext.Fight, 240, 40, 20, carriedHealth: null, fight: fight);

            Assert.AreEqual(0, facts.PhysicalDefense);
        }

        // A member the fight is not showing took no part in it, so the run's
        // figure is not stale for them.
        [Test]
        public void AFightMemberTheHudIsNotShowingReadsTheirCarriedHealth()
        {
            var facts = PartyMemberFacts.Resolve(MenuContext.Fight, 240, 40, 20, carriedHealth: 100, fight: null);

            Assert.AreEqual(100, facts.Health);
            Assert.AreEqual(240, facts.MaxHealth);
        }

        [Test]
        public void TheBarFractionIsHealthOverMaximum()
        {
            Assert.AreEqual(0.75f, new PartyMemberFacts(180, 240, 0, 0).HealthFraction, 0.0001f);
            Assert.AreEqual(1f, new PartyMemberFacts(240, 240, 0, 0).HealthFraction, 0.0001f);
            Assert.AreEqual(0f, new PartyMemberFacts(5, 0, 0, 0).HealthFraction, 0.0001f, "no maximum reads empty");
        }
    }
}
