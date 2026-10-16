using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Rewards
{
    // One thing a settled fight paid, recorded as the map showed it and as it
    // landed (docs/PLAN_REWARD_SCREENS.md C12). Read-only history on
    // CombatReward: never saved, never an input to a roll.
    public sealed class PaidPrize
    {
        // The node's chip as the map card drew it. None for the fight's own
        // gold, which has no chip.
        public readonly PrizeKind Shown;

        // What actually paid, after RealmPrize.Effective and chip overflow:
        // a Book or Relic chip with an empty pool, or a Gear/Relic chip past
        // the page limit, pays Gold.
        public readonly PrizeKind Paid;

        // Gold paid, or potions drawn. 0 for Gear, Relic and Book (a pick).
        public readonly int Amount;

        // The potions or the book that were drawn, in draw order. Empty for
        // gold, and for Gear and Relic, whose cards are rolled later.
        public readonly IReadOnlyList<string> Ids;

        public PaidPrize(PrizeKind shown, PrizeKind paid, int amount, IReadOnlyList<string> ids = null)
        {
            Shown = shown;
            Paid = paid;
            Amount = amount;
            Ids = ids ?? System.Array.Empty<string>();
        }
    }
}
