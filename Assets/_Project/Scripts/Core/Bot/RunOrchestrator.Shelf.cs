using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // AN EVENT'S MERCHANT SHELF (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, 3.4).
    //
    // The shop's rolling, buying and panel, on a stock the EVENT owns rather
    // than the node. Everything that differs from the room shop is here:
    //
    //   - Where the stock lives: run.shelves, one entry per (event, shelf),
    //     rolled the first time a `shelf` or `takeShelf` effect needs it and
    //     kept -- across Walk on, a room shop in between, a return at another
    //     node, a reload -- until the event's `finish` or the end of the run.
    //     No reroll, ever: every visit shows the same cards minus what sold.
    //   - What is in front: run.pendingShelf names the open event's shelf the
    //     party is looking at. CurrentShopStock and the buy path read the
    //     stock in front (RunOrchestrator.Shop.cs), so the screen and the bot
    //     buy through the room shop's own code.
    //   - Leaving: LeaveShelf returns to the event. It never clears the room;
    //     the event's own Leave does that, as it always has.
    //   - The robbery: `takeShelf` hands over the unsold cards, fakes still
    //     fake, after the scuffle loses `amount` of them on a seeded stream.
    public static partial class RunOrchestrator
    {
        // ---- reading -------------------------------------------------------------------

        // Whether the open event has its shelf in front of the party. The event
        // panel hands over to the shelf on this; the bot shops on it.
        public static bool EventShelfPending => OpenShelfOn(RunManager.Run) != null;

        // Whether the stock in front is a merchant shelf rather than the room
        // shop: the screen stands down what a merchant does not have (books,
        // relics, rerolls, selling) and shows consumables in their place.
        public static bool ShelfInFrontIsMerchant => OpenShelfOn(RunManager.Run) != null;

        // Whether the shelf in front has had its fakes revealed ("Browse with
        // Odette"). The mark belongs to the stock, so it survives Look again
        // and a reload.
        public static bool ShelfInFrontIsRevealed => OpenShelfOn(RunManager.Run)?.revealed ?? false;

        // Who is selling, for the shelf screen's title and keeper panel; null
        // when the stock in front is the room shop (or nothing).
        public static MerchantShelfFront ShelfInFront
        {
            get
            {
                var run = RunManager.Run;
                var stock = OpenShelfOn(run);
                if (stock == null) return null;

                var evt = FindEvent(run.eventId);
                var recipe = evt?.ShelfById(stock.shelfId);
                var keeper = evt?.SpeakerById(recipe?.KeeperId);
                return new MerchantShelfFront(recipe?.Title, keeper, recipe != null && recipe.FakeShare > 0,
                    stock.revealed);
            }
        }

        private static MerchantShelfStock OpenShelfOn(RunSnapshot run)
        {
            if (!EventIsOpenOn(run) || string.IsNullOrEmpty(run.pendingShelf)) return null;
            return FindShelfStock(run, run.eventId, run.pendingShelf);
        }

        // The stock in front of the party: the open event's shelf, else the
        // room shop at this node, else null.
        private static List<ShopStockEntry> StockInFront(RunSnapshot run)
        {
            var shelf = OpenShelfOn(run);
            if (shelf != null) return shelf.entries;
            return ShopIsOpen(run) ? run.shopStock : null;
        }

        internal static MerchantShelfStock FindShelfStock(RunSnapshot run, string eventId, string shelfId) =>
            run?.shelves?.FirstOrDefault(s => s != null && s.IsOwnedBy(eventId, shelfId));

        // ---- leaving ---------------------------------------------------------------------

        // Back to the event, which is still open on the page the Browse went
        // to. One write. Nothing else moves: the stock stays, and the room
        // stays uncleared until the event's own Leave.
        public static void LeaveShelf()
        {
            var run = RunManager.Run;
            if (run == null || string.IsNullOrEmpty(run.pendingShelf)) return;

            run.pendingShelf = "";
            SaveSlotManager.SaveCurrent();
        }

        // ---- the effects (ApplyEffect's shelf half; no writes) ---------------------------

        // `shelf`: roll the stock if this run has none yet, mark the fakes when
        // the pick reveals, and put the shelf in front. Not an effects-line
        // entry: the shelf itself is what the player sees next.
        private static void ApplyOpenShelf(RunSnapshot run, EventEffect effect)
        {
            var stock = EnsureShelfStock(run, run.eventId, effect.ShelfId);
            if (stock == null) return;

            if (effect.Reveal) stock.revealed = true;
            run.pendingShelf = stock.shelfId;
        }

        // `takeShelf`: the unsold cards, into the bag as the copies a purchase
        // would have stamped (lot and fake flag carried), minus `Amount` lost
        // to the scuffle. Each card handed over is an item line; the loss is
        // its own line, and says so when there was nothing left to lose.
        // Every card handed over or lost is marked sold, so the stock reads
        // empty even before `finish` removes it.
        private static void ApplyTakeShelf(SaveData save, RunSnapshot run, EventEffect effect, List<EventEffect> applied)
        {
            var stock = EnsureShelfStock(run, run.eventId, effect.ShelfId);
            if (stock == null) return;

            var rng = RngStreams.Open(run.runSeed, RngStreams.ShelfScuffle, run.step, run.currentNodeId,
                ShelfKey(stock.eventId, stock.shelfId));
            var (taken, lost) = MerchantShelf.Scuffle(stock.entries, effect.Amount, bound => rng.NextInt(0, bound));

            save.stockpiledItems ??= new List<InventoryEntry>();
            foreach (var card in taken)
            {
                card.sold = true;
                if (ContentDatabase.GetItem(card.contentId) == null) continue;

                InventoryOps.Add(save.stockpiledItems, card.Instance(), 1);
                applied.Add(EventEffect.ItemGrant(card.contentId, 1));
            }

            foreach (var card in lost)
            {
                card.sold = true;
                applied.Add(EventEffect.ShelfLoss(stock.shelfId, card.contentId));
            }

            if (effect.Amount > 0 && lost.Count == 0) applied.Add(EventEffect.ShelfLoss(stock.shelfId, ""));
        }

        // `finish`: the event is done for the run, and so is every stock it
        // owns.
        private static void EndShelvesOf(RunSnapshot run, string eventId)
        {
            run.shelves?.RemoveAll(s => s == null || s.eventId == eventId);
            run.pendingShelf = "";
        }

        // ---- the roll ------------------------------------------------------------------

        // The stock for (event, shelf), rolled here at (step, node) on the
        // shelf's own streams if the run has none, found otherwise. In memory
        // only: the pick that calls this persists it with everything else.
        // Null for a shelf content does not have.
        private static MerchantShelfStock EnsureShelfStock(RunSnapshot run, string eventId, string shelfId)
        {
            var existing = FindShelfStock(run, eventId, shelfId);
            if (existing != null) return existing;

            var recipe = FindEvent(eventId)?.ShelfById(shelfId);
            if (recipe == null) return null;

            int step = run.step;
            int node = run.currentNodeId;
            int shelfKey = ShelfKey(eventId, shelfId);

            var gear = new List<ShopStockEntry>();
            if (recipe.Sections != null && recipe.Sections.Contains(ShopStock.GearSection))
            {
                var gearRng = RngStreams.Open(run.runSeed, RngStreams.ShelfGear, step, node, shelfKey);
                gear = RollGearWith(run, bound => gearRng.NextInt(0, bound));
            }

            var consumableRng = RngStreams.Open(run.runSeed, RngStreams.ShelfConsumables, step, node, shelfKey);
            var fakeRng = RngStreams.Open(run.runSeed, RngStreams.ShelfFakes, step, node, shelfKey);

            var entries = MerchantShelf.Build(
                gear, ConsumableCandidates(), recipe.ConsumableCount,
                recipe.PriceFactorPercent, recipe.FakeShare,
                card => MerchantShelf.LotFor(eventId, shelfId, step, node, card),
                bound => consumableRng.NextInt(0, bound),
                bound => fakeRng.NextInt(0, bound));

            var stock = new MerchantShelfStock
            {
                eventId = eventId,
                shelfId = shelfId,
                rolledStep = step,
                rolledNodeId = node,
                entries = entries,
            };

            run.shelves ??= new List<MerchantShelfStock>();
            run.shelves.Add(stock);
            return stock;
        }

        // WHICH SHELF, as the streams' third coordinate. (step, node) alone
        // is where a stock was rolled, and two shelves of one event -- or two
        // events' -- at one node would otherwise roll identical stock and
        // lose identical cards. Stable across launches (RngStreams.KeyOf), so
        // a reload that re-rolls a dropped stock rolls the same one.
        private static int ShelfKey(string eventId, string shelfId) =>
            RngStreams.KeyOf((eventId ?? "") + "/" + (shelfId ?? ""));

        // Every consumable in the catalogue, in authored order, at the room
        // shop's price for it (its authored cost, BuyPriceOf's rule). The room
        // shop's gear pool (ContentDatabase.Offerable) is generated gear only,
        // which is why a merchant's consumables need a pool of their own.
        private static List<MerchantShelf.ConsumableCandidate> ConsumableCandidates() =>
            ContentDatabase.Consumables
                .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                .Select(i => new MerchantShelf.ConsumableCandidate(i.id, i.cost))
                .ToList();
    }
}
