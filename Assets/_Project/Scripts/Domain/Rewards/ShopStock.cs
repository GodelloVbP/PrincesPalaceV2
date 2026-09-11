using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;

namespace PrincesPalace.Domain.Rewards
{
    // Which shelf an entry sits on. Exactly one per entry; the fields the
    // other kinds use are simply not read.
    //
    // A discriminated union is not a shape JsonUtility can serialise -- the
    // same constraint that made learnedSpells a flat list rather than a
    // dictionary -- so `kind` is the discriminator and the rule is held by
    // the constructors below rather than by the type system.
    public enum ShopEntryKind
    {
        Gear,
        Book,
        Relic,
    }

    // One card on the shop shelf, as it is persisted.
    //
    // PRICE IS RESOLVED AT ROLL TIME AND STORED. Recomputing it on load looks
    // free -- prices are pure (ShopPricing) -- and it is free right up until
    // a content patch or a constant change lands under an in-flight run.
    // Then a card the player is looking at silently changes price, or becomes
    // affordable after they decided it was not. An open shop is a quoted
    // price (docs/PLAN_SHOP.md §2e).
    //
    // SOLD IS A FLAG, NOT A REMOVAL, and so is NO OFFER. The screen binds
    // cards by index, so removing an entry renumbers every card after it --
    // which is the class of bug ScreenDef.CountBindings exists to catch.
    [Serializable]
    public class ShopStockEntry
    {
        public ShopEntryKind kind;

        // itemId | skillId | relicId, per kind. Empty on a NO OFFER card.
        public string contentId = "";

        // NO OFFER, held EXPLICITLY rather than inferred from an empty
        // contentId. Reconciliation turns a card whose content vanished into
        // a NO OFFER in place (§2e) and the honest record of that is a flag,
        // not a blanked id: the id is worth keeping to say WHAT went missing,
        // and "empty means placeholder" would make those two states one.
        public bool noOffer;

        // Gear only.
        public int plus;
        public List<string> modifiers = new List<string>();
        public int riftTier;

        public int price;
        public bool sold;

        // Where the card sits. Section indices are ShopStock's constants
        // below; index is the position within the section, and it is what
        // the screen binds to.
        public int section;
        public int index;

        public ShopStockEntry() { }

        public static ShopStockEntry Gear(int index, string itemId, int plus, IEnumerable<string> modifiers,
            int riftTier, int price)
        {
            return new ShopStockEntry
            {
                kind = ShopEntryKind.Gear,
                contentId = itemId ?? "",
                plus = plus,
                // ORDINAL-SORTED at build, so two cards that rolled the same
                // affixes in a different order compare and read as the same
                // card. InventoryOps already treats modifierIds as an
                // unordered set; sorting here means the persisted stock says
                // so too instead of relying on every reader to remember.
                modifiers = (modifiers ?? Enumerable.Empty<string>())
                    .Where(m => !string.IsNullOrEmpty(m))
                    .OrderBy(m => m, StringComparer.Ordinal)
                    .ToList(),
                riftTier = riftTier,
                price = price,
                section = ShopStock.GearSection,
                index = index,
            };
        }

        public static ShopStockEntry Relic(int index, string relicId, int price)
        {
            return new ShopStockEntry
            {
                kind = ShopEntryKind.Relic,
                contentId = relicId ?? "",
                price = price,
                section = ShopStock.RelicSection,
                index = index,
            };
        }

        public static ShopStockEntry Book(int index, string skillId, int price)
        {
            return new ShopStockEntry
            {
                kind = ShopEntryKind.Book,
                contentId = skillId ?? "",
                price = price,
                section = ShopStock.BookSection,
                index = index,
            };
        }

        // A card with nothing behind it. The row never shrinks and never
        // re-centres: an undersized pool renders these IN PLACE, because a
        // row that re-centres moves every card the player was already
        // looking at (§2d).
        public static ShopStockEntry NoOffer(int section, int index, ShopEntryKind kind)
        {
            return new ShopStockEntry
            {
                kind = kind,
                noOffer = true,
                section = section,
                index = index,
            };
        }
    }

    // WHAT A SHOP HAS ON ITS SHELVES, AND THE ROLL THAT PUTS IT THERE.
    //
    // Pure and engine-free like the rest of Domain/Rewards: the gear draw
    // takes its candidates and its per-item roll as parameters rather than
    // reaching for ContentDatabase, exactly as ItemOfferTable already does
    // and for the same reason -- the whole rule is then unit-testable
    // without a scene, and Core owns the one step Domain cannot take.
    //
    // Each section is rolled from ITS OWN stream (RngStreams.ShopGear /
    // ShopBooks / ShopRelics) at (step, currentNodeId, that section's reroll
    // count), so rerolling one shelf cannot move another.
    public static class ShopStock
    {
        // ---- what the shelves hold ------------------------------------------
        //
        // Ten cards down to §7.1 point 3's six, revised again 2026-09-03 (c)
        // against gate 1's own numbers (docs/handoffs/shop_v2/GAP_AUDIT.md,
        // "Gate exit checks -> Gate 1"): even a policy that buys every
        // affordable, positive-scoring card on a 3/1/2 shelf left runs with
        // 6-9x the pre-shop median gold at death, so the shelf was the
        // binding constraint, not price. Widened to 4 gear / 3 books / 3
        // relics -- ten offers again, but no longer ten offers priced
        // against a shelf nobody could empty. The book count no longer
        // matches "one card because the pool is five" (that reasoning is
        // superseded, not wrong on its own terms); gate 3 is what tests
        // whether three book cards against a five-entry pool reads as
        // repetitive once real content rolls there.
        //
        // This is also the UI design: the five-panel grid's Gear / Spell
        // Books / Relics panels each show exactly this many cards (§3f).
        //
        // Constants rather than layout: the screen emits exactly this many
        // cards and ScreenDef.CountBindings pairs each array with its
        // constant, so the two cannot drift.
        public const int GearCount = 4;
        public const int BookCount = 3;
        public const int RelicCount = 3;

        // ---- where they sit -------------------------------------------------
        //
        // ONE ORDER, EVERYWHERE: gear, books, relics. It is the reading order
        // of the shelf and the index into RunSnapshot.shopRerollsUsed, and
        // the two being the same array position is the point -- a second
        // order would be a second thing to keep in step.
        public const int GearSection = 0;
        public const int BookSection = 1;
        public const int RelicSection = 2;
        public const int SectionCount = 3;

        // ---- how many decisions one visit can want ---------------------------
        //
        // DERIVED, NOT TYPED. A headless caller has to bound a shop visit --
        // a policy that never says leave is a hang and nothing times it out
        // (docs/PLAN_SHOP.md 2g) -- and the bound was a hand-written 12
        // justified against a six-card shelf. The shelf became ten cards on
        // 2026-09-03 and the justification did not follow: buying every card
        // (10), rerolling every section once (3) and the Leave that ends the
        // visit (1) is already 14, so a policy doing nothing unusual was
        // being cut off and the cut recorded as a finding. It sits here
        // rather than in the caller precisely so the shelf and its bound
        // cannot drift apart again: change a count above and this moves with
        // it.
        //
        // SELL HEADROOM IS THE ONE JUDGEMENT IN IT, and it is PROVISIONAL.
        // Selling is unbounded in principle -- a bag can hold more rows than a
        // shelf has cards -- so no arithmetic derives it and a number has to
        // be chosen. MEASURED rather than guessed: with the ceiling lifted far
        // out of the way, 2,050 shop visits across two archetypes over a
        // 200-run batch peaked at 24 decisions in one visit, so 10 would be
        // exactly the observed maximum and this leaves two above it -- the
        // tail is thin (three visits past 18 for the seller) and a different
        // seed moves it. Cheap to be generous: the loop exits on Leave, so a
        // higher ceiling costs nothing on a visit that does not need it.
        //
        // The real question behind the number -- how many sales one visit
        // SHOULD be allowed -- is the owner's, and this is a bound, not an
        // answer to it.
        public const int SellHeadroom = 12;

        public const int MaxChoicesPerVisit =
            GearCount + BookCount + RelicCount   // buy every card on the shelf
            + SectionCount                       // reroll every section once
            + 1                                  // the Leave that ends the visit
            + SellHeadroom;

        public static int CountFor(int section)
        {
            switch (section)
            {
                case GearSection: return GearCount;
                case BookSection: return BookCount;
                case RelicSection: return RelicCount;
                default: return 0;
            }
        }

        public static ShopEntryKind KindFor(int section)
        {
            switch (section)
            {
                case BookSection: return ShopEntryKind.Book;
                case RelicSection: return ShopEntryKind.Relic;
                default: return ShopEntryKind.Gear;
            }
        }

        // WHICH GENERATOR PRODUCED A SAVED SHELF.
        //
        // An open shop from an older build is NOT rewritten -- it is left
        // exactly as it was rolled, and the new generator applies at the next
        // node. A shop that reshuffles itself because the game updated is a
        // free reroll granted by a patch note (§2e). Bump this when the roll's
        // SHAPE changes: a section added, a count moved, a candidate filter
        // changed.
        public const int StockVersion = 1;

        // How many times the affordability floor may re-draw the cheapest
        // gear slot before giving up. Bounded because the guarantee is worth
        // having and a loop that cannot terminate is not: at a depth where
        // every candidate prices above the anchor, the honest answer is an
        // expensive shelf, not a hang (assumption 11).
        public const int AffordabilityRedraws = 8;

        public static int PriceOf(ItemOffer offer) =>
            ShopPricing.GearPrice(offer.Tier, offer.Plus, (int)offer.RiftTier);

        // ---- the gear shelf --------------------------------------------------

        // `rollOne` is the per-item three-axis roll (plus, rift tier, which
        // modifiers), handed in rather than reimplemented: it is the SAME
        // seam ItemOfferRoll.Roll uses for the reward path, so the shop and
        // the fight reward cannot drift on what "one rolled copy" means
        // (§2d). Domain cannot build it -- the modifier pools come from
        // ContentDatabase -- which is why it arrives as a delegate.
        //
        // Duplicate contentId is impossible within the section rather than
        // filtered afterwards: ItemOfferTable.Choose draws without
        // replacement by item id.
        public static List<ShopStockEntry> RollGear(
            IReadOnlyList<ItemOffer> candidates, int depthStep, int maxTier,
            Func<ItemOffer, ItemOffer> rollOne, Func<int, int> nextIndex)
        {
            var entries = new List<ShopStockEntry>();
            if (candidates == null || rollOne == null || nextIndex == null)
            {
                return PadTo(entries, GearSection, GearCount, ShopEntryKind.Gear);
            }

            // EncounterClass.Normal is the caller's business (a shop is not a
            // fight and browsing must not beat winning, assumption 7) and it
            // is already baked into `rollOne`; what this needs from depth is
            // only which tier band to stock.
            var chosen = ItemOfferTable.Choose(
                candidates, RarityTable.FloorTier(depthStep), maxTier, nextIndex, GearCount);

            var rolled = chosen.Select(rollOne).ToList();

            ApplyAffordabilityFloor(chosen, rolled, rollOne);

            for (int i = 0; i < rolled.Count; i++)
            {
                var offer = rolled[i];
                entries.Add(ShopStockEntry.Gear(i, offer.ItemId, offer.Plus, offer.Modifiers,
                    (int)offer.RiftTier, PriceOf(offer)));
            }

            return PadTo(entries, GearSection, GearCount, ShopEntryKind.Gear);
        }

        // ASSUMPTION 11, as a re-draw rather than a discount.
        //
        // A shelf whose cheapest card costs more than a normal fight pays is
        // a room that charged a fight and returned a reroll button. The fix
        // re-rolls the cheapest slot's plus/rift -- never its tier, never its
        // price -- so the price stays a pure function of tier/plus/rift and
        // the only thing the floor changes is WHICH copy is on the card.
        //
        // Keeps the cheaper of the two every time, so a re-draw can only
        // improve the shelf; and stops the moment the anchor is met.
        private static void ApplyAffordabilityFloor(
            List<ItemOffer> chosen, List<ItemOffer> rolled, Func<ItemOffer, ItemOffer> rollOne)
        {
            if (rolled.Count == 0) return;

            int cheapest = 0;
            for (int i = 1; i < rolled.Count; i++)
            {
                if (PriceOf(rolled[i]) < PriceOf(rolled[cheapest])) cheapest = i;
            }

            for (int attempt = 0; attempt < AffordabilityRedraws; attempt++)
            {
                if (PriceOf(rolled[cheapest]) <= ShopPricing.NormalFightPayoutAnchor) return;

                var redrawn = rollOne(chosen[cheapest]);
                if (PriceOf(redrawn) < PriceOf(rolled[cheapest])) rolled[cheapest] = redrawn;
            }
        }

        // ---- the relic shelf --------------------------------------------------

        // `available` is the pool MINUS what the run already holds, decided
        // by the caller exactly as RelicDraftOffer decides it -- being
        // offered what you are already carrying reads as a bug.
        //
        // DraftWeighted draws without replacement, so no duplicate contentId
        // here either, and it returns fewer than asked when the pool is
        // smaller. Those slots become NO OFFER rather than shrinking the row.
        public static List<ShopStockEntry> RollRelics(
            IReadOnlyList<RelicOption> available, Func<int, int> nextIndex)
        {
            var entries = new List<ShopStockEntry>();
            if (available == null || nextIndex == null)
            {
                return PadTo(entries, RelicSection, RelicCount, ShopEntryKind.Relic);
            }

            var drawn = RelicPool.DraftWeighted(available, nextIndex, RelicCount);
            for (int i = 0; i < drawn.Count && i < RelicCount; i++)
            {
                entries.Add(ShopStockEntry.Relic(i, drawn[i].Id, ShopPricing.RelicPrice(drawn[i].Rarity)));
            }

            return PadTo(entries, RelicSection, RelicCount, ShopEntryKind.Relic);
        }

        // ---- the book shelf ---------------------------------------------------

        // One book-eligible skill, as the shop's roll needs to see it -- a
        // pure Domain-side view, not a ContentDatabase lookup, for the same
        // reason RollGear takes its candidates as a parameter instead of
        // reaching for content itself.
        public readonly struct BookCandidate
        {
            public readonly string SkillId;
            public readonly int BookTier;

            public BookCandidate(string skillId, int bookTier)
            {
                SkillId = skillId;
                BookTier = bookTier;
            }
        }

        // `available` is already filtered by the caller to skills nobody
        // fielded already knows (§2d's "owned by everyone" rule) and to
        // skills with bookTier > 0 -- ShopStock draws from what it is
        // handed, it does not decide eligibility. Uniform draw without
        // replacement (RelicPool.DraftWeighted's shape with every weight
        // equal to 1): books carry no rarity to weight by.
        //
        // gate 3 is the first thing that draws from RngStreams.ShopBooks --
        // every prior shop rolled this section as BookCount NO OFFER cards
        // without touching the stream, so there is no phantom consumption
        // to preserve and this draw's numbers are the first that stream has
        // ever produced.
        public static List<ShopStockEntry> RollBooks(IReadOnlyList<BookCandidate> available, Func<int, int> nextIndex)
        {
            var entries = new List<ShopStockEntry>();
            if (available == null || available.Count == 0 || nextIndex == null)
            {
                return PadTo(entries, BookSection, BookCount, ShopEntryKind.Book);
            }

            var remaining = new List<BookCandidate>(available);
            int wanted = Math.Min(BookCount, remaining.Count);

            for (int i = 0; i < wanted; i++)
            {
                int roll = nextIndex(remaining.Count);
                if (roll < 0) roll = 0;
                if (roll >= remaining.Count) roll = remaining.Count - 1;

                var picked = remaining[roll];
                remaining.RemoveAt(roll);

                entries.Add(ShopStockEntry.Book(i, picked.SkillId, ShopPricing.BookPrice(picked.BookTier)));
            }

            return PadTo(entries, BookSection, BookCount, ShopEntryKind.Book);
        }

        // ---- the whole shelf ---------------------------------------------------

        // Rolls one section by index, which is the shape both callers need:
        // arriving rolls all three, a reroll rolls exactly one.
        public static List<ShopStockEntry> RollSection(
            int section,
            IReadOnlyList<ItemOffer> gearCandidates, int depthStep, int maxTier,
            Func<ItemOffer, ItemOffer> rollOne,
            IReadOnlyList<RelicOption> availableRelics,
            IReadOnlyList<BookCandidate> availableBooks,
            Func<int, int> nextIndex)
        {
            switch (section)
            {
                case GearSection:
                    return RollGear(gearCandidates, depthStep, maxTier, rollOne, nextIndex);
                case BookSection:
                    return RollBooks(availableBooks, nextIndex);
                case RelicSection:
                    return RollRelics(availableRelics, nextIndex);
                default:
                    return new List<ShopStockEntry>();
            }
        }

        // An undersized pool never shrinks the row (§2d).
        private static List<ShopStockEntry> PadTo(
            List<ShopStockEntry> entries, int section, int count, ShopEntryKind kind)
        {
            for (int i = entries.Count; i < count; i++)
            {
                entries.Add(ShopStockEntry.NoOffer(section, i, kind));
            }

            return entries;
        }
    }
}
