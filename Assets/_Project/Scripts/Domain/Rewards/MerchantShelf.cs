using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Rewards
{
    // A MERCHANT SHELF'S RULES (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, 3.4):
    // an event's own stock, built from the room shop's roll and then priced,
    // stamped and salted with fakes here.
    //
    // Pure and engine-free like ShopStock: the gear arrives already rolled
    // (Core rolls it through the room shop's own ShopStock.RollGear, which is
    // what "same candidates, tier band and affixes" means), the consumable
    // candidates arrive as a list, and every draw arrives as a delegate on its
    // own stream. The caller (RunOrchestrator.Shelf.cs) owns the streams and
    // the persistence; everything decided about a card is decided here.
    public static class MerchantShelf
    {
        // One consumable the shelf may stock, with the room-shop price it is
        // discounted from (the item's authored cost, RunOrchestrator.BuyPriceOf).
        public readonly struct ConsumableCandidate
        {
            public readonly string ItemId;
            public readonly int ShopPrice;

            public ConsumableCandidate(string itemId, int shopPrice)
            {
                ItemId = itemId;
                ShopPrice = shopPrice;
            }
        }

        // `factorPercent` of the room shop's price, rounded half AWAY from
        // zero (10.5 rounds to 11), never below 1 gold. Decimal, not
        // double, so 15 * 70 / 100 is exactly 10.5 and the midpoint rule
        // is the one that decides it.
        public static int Price(int shopPrice, int factorPercent)
        {
            decimal exact = (decimal)shopPrice * factorPercent / 100m;
            int price = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
            return price < 1 ? 1 : price;
        }

        // max(1, round(cards / fakeShare)) of `cards` real cards, rounded
        // half away from zero; none when fakeShare is 0 or there is nothing
        // to fake. Never more fakes than cards.
        public static int FakeCount(int cards, int fakeShare)
        {
            if (cards <= 0 || fakeShare <= 0) return 0;

            int rounded = (int)Math.Round((decimal)cards / fakeShare, MidpointRounding.AwayFromZero);
            return Math.Min(cards, Math.Max(1, rounded));
        }

        // A lot names one physical copy for the whole run: which event, which
        // shelf, the (step, node) the stock was rolled at, and the card's
        // position on the shelf. Stored on the card at roll time, so it is
        // reload-stable by being on disk rather than by being recomputed.
        public static string LotFor(string eventId, string shelfId, int step, int nodeId, int cardNumber) =>
            $"{eventId}:{shelfId}:{step}:{nodeId}:{cardNumber}";

        // THE WHOLE STOCK, from the room shop's gear roll (already priced at
        // the room shop's prices; empty when the recipe stocks no gear) and
        // the consumable pool.
        //
        //   1. Gear cards keep their section and index; consumable cards take
        //      ShopStock.ConsumableSection, drawn without repeats on their own
        //      stream and padded with NO OFFER when the pool is short (the row
        //      never shrinks, the room shop's rule).
        //   2. Every real card is repriced (Price) and stamped with its lot.
        //   3. FakeCount of the real cards are marked fake, picked on their
        //      own stream -- so which cards are fake cannot move what the
        //      shelf holds, and the reverse.
        public static List<ShopStockEntry> Build(
            IReadOnlyList<ShopStockEntry> gear,
            IReadOnlyList<ConsumableCandidate> consumables, int consumableCount,
            int priceFactorPercent, int fakeShare,
            Func<int, string> lotForCard,
            Func<int, int> nextConsumable, Func<int, int> nextFake)
        {
            var stock = new List<ShopStockEntry>();
            foreach (var entry in gear ?? Array.Empty<ShopStockEntry>())
            {
                if (entry != null) stock.Add(entry);
            }

            var pool = new List<ConsumableCandidate>(consumables ?? Array.Empty<ConsumableCandidate>());
            for (int i = 0; i < consumableCount; i++)
            {
                if (pool.Count == 0 || nextConsumable == null)
                {
                    stock.Add(ShopStockEntry.NoOffer(ShopStock.ConsumableSection, i, ShopEntryKind.Consumable));
                    continue;
                }

                int roll = Clamp(nextConsumable(pool.Count), pool.Count);
                var picked = pool[roll];
                pool.RemoveAt(roll);
                stock.Add(ShopStockEntry.Consumable(i, picked.ItemId, picked.ShopPrice));
            }

            var real = new List<ShopStockEntry>();
            for (int card = 0; card < stock.Count; card++)
            {
                var entry = stock[card];
                if (entry.noOffer) continue;

                entry.price = Price(entry.price, priceFactorPercent);
                entry.lot = lotForCard?.Invoke(card) ?? "";
                real.Add(entry);
            }

            int fakes = FakeCount(real.Count, fakeShare);
            var candidates = new List<ShopStockEntry>(real);
            for (int i = 0; i < fakes && candidates.Count > 0 && nextFake != null; i++)
            {
                int roll = Clamp(nextFake(candidates.Count), candidates.Count);
                candidates[roll].fake = true;
                candidates.RemoveAt(roll);
            }

            return stock;
        }

        // THE SCUFFLE (plan 1.5, Rob): of the cards still on sale, `lost` go,
        // picked on the caller's stream; the rest are handed over. Returns
        // (handed over, lost), both in shelf order. Fewer on sale than
        // `lost`: they all go, and nothing is handed over. The reveal plays no
        // part -- Odette's marks do not steer the loss (plan section 2, 9).
        public static (List<ShopStockEntry> Taken, List<ShopStockEntry> Lost) Scuffle(
            IReadOnlyList<ShopStockEntry> stock, int lost, Func<int, int> nextIndex)
        {
            var unsold = (stock ?? Array.Empty<ShopStockEntry>())
                .Where(e => e != null && !e.noOffer && !e.sold)
                .ToList();

            var lostCards = new List<ShopStockEntry>();
            var remaining = new List<ShopStockEntry>(unsold);
            for (int i = 0; i < lost && remaining.Count > 0; i++)
            {
                int roll = nextIndex == null ? 0 : Clamp(nextIndex(remaining.Count), remaining.Count);
                lostCards.Add(remaining[roll]);
                remaining.RemoveAt(roll);
            }

            return (remaining, unsold.Where(lostCards.Contains).ToList());
        }

        private static int Clamp(int roll, int count)
        {
            if (roll < 0) return 0;
            return roll >= count ? count - 1 : roll;
        }
    }
}
