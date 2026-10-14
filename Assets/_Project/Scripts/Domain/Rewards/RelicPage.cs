using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;

namespace PrincesPalace.Domain.Rewards
{
    // THE RELIC PAGE (docs/PLAN_ITEMS.md 1.2): k + extraCards relics drawn by
    // the source's rarity weights, from what the run may still be offered,
    // at or above the source's minimum rarity. Pure, like GearPage.
    public static class RelicPage
    {
        // `available` is already filtered by Core: unlocked, draftable, not
        // held, and not a relic every member it reaches already gets the
        // mechanic of.
        public static List<RelicOption> Roll(ResolvedRewardSource source, int chips,
            IReadOnlyList<RelicOption> available, Func<int, int> nextIndex)
        {
            if (source == null || available == null || nextIndex == null) return new List<RelicOption>();

            var eligible = available
                .Where(r => r.Rarity >= source.RelicRarityMin && source.RelicWeightOf(r.Rarity) > 0)
                .ToList();

            int count = Math.Max(0, chips) + source.ExtraCards;
            return RelicPool.DraftWeighted(eligible, source.RelicWeightOf, nextIndex, count);
        }

        // What one unused choice pays: 30% (ShopPricing.SellFraction) of the
        // cheapest shown relic's price, through ScalePrice. Nothing shown
        // pays nothing.
        public static int UnusedChoicePayout(IEnumerable<RelicRarity> shown, int step)
        {
            var list = shown?.ToList() ?? new List<RelicRarity>();
            if (list.Count == 0) return 0;

            int cheapest = list.Min(r => ShopPricing.RelicAuthored(r));
            return ShopPricing.SellPrice(cheapest, step);
        }
    }
}
