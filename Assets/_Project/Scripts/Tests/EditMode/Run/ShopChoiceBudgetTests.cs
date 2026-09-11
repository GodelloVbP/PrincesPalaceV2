using NUnit.Framework;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.EditModeTests
{
    // THE BOUND ON ONE SHOP VISIT, HELD AGAINST THE SHELF IT BOUNDS.
    //
    // A headless caller has to stop a visit that never ends -- a policy that
    // never says leave is a hang and nothing times it out -- but the bound has
    // to sit ABOVE what a visit can legitimately want, or a policy doing
    // nothing unusual gets truncated and the truncation is read as a finding.
    // That is exactly what happened: the bound was a hand-typed 12 justified
    // against a six-card shelf, the shelf became ten cards, and the sentence
    // justifying the 12 stayed behind. One in five shop visits was being cut
    // off.
    //
    // So the arithmetic is pinned here rather than trusted to whoever next
    // changes a count. This is a test about a NUMBER'S RELATIONSHIP to other
    // numbers, which is why it can be this small and still be worth having.
    public class ShopChoiceBudgetTests
    {
        [Test]
        public void TheShopChoiceCapIsAboveWhatOneVisitCanLegitimatelyWant()
        {
            // The shelf, as literals: four gear, three books, three relics.
            Assert.AreEqual(4, ShopStock.GearCount);
            Assert.AreEqual(3, ShopStock.BookCount);
            Assert.AreEqual(3, ShopStock.RelicCount);
            Assert.AreEqual(3, ShopStock.SectionCount);

            // Ten buys, three section rerolls, one Leave. A visit that does all
            // of it has done nothing strange, and the bound must clear it --
            // BEFORE any allowance for selling, which is unbounded in
            // principle and is the one judgement in the number.
            Assert.GreaterOrEqual(ShopStock.MaxChoicesPerVisit, 14,
                "a policy could buy every card, reroll every section once and then leave, " +
                "and be cut off mid-visit for it");

            Assert.Greater(ShopStock.SellHeadroom, 0,
                "selling is a choice like any other and a bag can hold more rows than a shelf has cards");
        }
    }
}
