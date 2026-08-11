using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // Rolls the three items a won fight offers.
    //
    // Every RULE here already existed and had no caller: RarityTable decides
    // what a depth and an encounter class are worth, ItemOfferTable picks which
    // items given a target tier, and both are pure, injected-randomness and
    // fully tested. What was missing was the one Core-side step neither can
    // take -- turning ContentDatabase's ItemDefinitions into candidates -- so
    // the whole reward table sat unreachable behind it.
    //
    // Kept out of the controller because "what does a fight drop" is a rule
    // about the game, not about a screen, and the Reckoning is not the only
    // thing that will ever want to ask.
    public static class ItemOfferRoll
    {
        // The ceiling the tier roll clamps against, taken from content rather
        // than declared. A content pass that adds a tier 11 set should widen
        // the roll without anyone remembering to update a constant here.
        public static int MaxTier =>
            ContentDatabase.Items.Count == 0 ? 0 : ContentDatabase.Items.Max(i => i.tier);

        // EQUIPPABLES ONLY.
        //
        // A "choose one of three" that can offer a health potion is not a
        // choice, it is a tax on the one player who reads carefully. Potions
        // come from the shop and from drops; the fight-reward slot is where
        // gear comes from.
        public static IReadOnlyList<ItemOffer> Candidates()
        {
            return ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrEmpty(i.id))
                .Select(i => new ItemOffer(i.id, i.tier))
                .ToList();
        }

        // The three offers, each with its own independently rolled plus.
        //
        // `nextIndex` is upper-bound-exclusive and injected, matching the shape
        // both Domain tables already take -- so a caller under test can hand in
        // a seeded stand-in and get the same three items every time.
        public static List<ItemOffer> Roll(EncounterClass encounter, int depthStep, Func<int, int> nextIndex)
        {
            var offers = new List<ItemOffer>();
            if (nextIndex == null) return offers;

            var candidates = Candidates();
            if (candidates.Count == 0) return offers;

            int maxTier = MaxTier;
            int targetTier = RarityTable.RollTier(encounter, depthStep, maxTier, nextIndex);

            // Tier is rolled ONCE for the offer set and plus is rolled PER
            // ITEM. Rolling the tier per item would quietly widen the spread
            // ItemOfferTable.TierSpread already controls, and make the three
            // offers three separate difficulty statements rather than one.
            foreach (var offer in ItemOfferTable.Choose(candidates, targetTier, maxTier, nextIndex))
            {
                offers.Add(offer.WithPlus(RarityTable.RollPlus(encounter, nextIndex)));
            }

            return offers;
        }
    }
}
