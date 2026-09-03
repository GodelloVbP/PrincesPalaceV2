using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Bot
{
    // WHAT A POLICY SEES WHEN IT STANDS IN A SHOP.
    //
    // The same bargain RunView already makes: the Core driver flattens the
    // live run into the handful of facts a player has in front of them, and
    // the policy cannot reach past it. A policy handed the shop stock itself
    // would be able to ask ContentDatabase what an item does, which is the
    // omniscience RunView.OfferScores exists to avoid -- Core resolves an
    // item id into a score (the plan's F1) and the answer travels down as a
    // number, index-aligned, exactly as it does for the reward offer.
    //
    // Struct-of-lists rather than the live ShopStockEntry list for the reason
    // ShopOfferTrace is flat: an entry is a mutable object the orchestrator
    // still owns, and a policy holding one across a mutation would be reading
    // a shelf that moved under it.

    // What a policy decided to do next. One of five, and `Leave` is always
    // legal -- the driver's loop ends on it.
    public enum ShopChoiceKind
    {
        Leave,
        BuyGear,
        BuyRelic,
        BuyBook,
        Sell,
        Reroll,
    }

    public readonly struct ShopChoice
    {
        public readonly ShopChoiceKind Kind;

        // Card index within its section for the two buys, bag index for a
        // sell, unused for a reroll and a leave.
        public readonly int Index;

        // Sell only.
        public readonly int Quantity;

        // Reroll only. ShopStock's section constants.
        public readonly int Section;

        private ShopChoice(ShopChoiceKind kind, int index, int quantity, int section)
        {
            Kind = kind;
            Index = index;
            Quantity = quantity;
            Section = section;
        }

        public static ShopChoice Leave() => new ShopChoice(ShopChoiceKind.Leave, -1, 0, -1);

        public static ShopChoice BuyGear(int index) =>
            new ShopChoice(ShopChoiceKind.BuyGear, index, 0, ShopStock.GearSection);

        public static ShopChoice BuyRelic(int index) =>
            new ShopChoice(ShopChoiceKind.BuyRelic, index, 0, ShopStock.RelicSection);

        public static ShopChoice BuyBook(int index) =>
            new ShopChoice(ShopChoiceKind.BuyBook, index, 0, ShopStock.BookSection);

        public static ShopChoice Sell(int bagIndex, int quantity) =>
            new ShopChoice(ShopChoiceKind.Sell, bagIndex, quantity, -1);

        public static ShopChoice Reroll(int section) =>
            new ShopChoice(ShopChoiceKind.Reroll, -1, 0, section);

        public override string ToString()
        {
            switch (Kind)
            {
                case ShopChoiceKind.BuyGear: return "BuyGear:" + Index;
                case ShopChoiceKind.BuyRelic: return "BuyRelic:" + Index;
                case ShopChoiceKind.BuyBook: return "BuyBook:" + Index;
                case ShopChoiceKind.Sell: return "Sell:" + Index + "x" + Quantity;
                case ShopChoiceKind.Reroll: return "Reroll:" + Section;
                default: return "Leave";
            }
        }
    }

    // One card on the shelf, as a policy reads it.
    public readonly struct ShopCardView
    {
        public readonly int Section;
        public readonly int Index;
        public readonly ShopEntryKind Kind;
        public readonly string ContentId;
        public readonly int Price;
        public readonly bool Sold;
        public readonly bool NoOffer;

        // Against the purse AT THE MOMENT THE VIEW WAS BUILT. The driver
        // rebuilds the view before every choice, so this is never stale
        // within one decision -- and a policy that cached it across a
        // purchase would be reading a purse it already spent.
        public readonly bool Affordable;

        // GEAR ONLY, and zero everywhere else. Core's GearEvaluator against
        // the archetype's own GearWeights, the same number RunView.OfferScores
        // carries for the reward offer -- so "is this worth buying" and "is
        // this worth taking" are the same question asked of the same
        // evaluator, rather than two rankings that can disagree.
        //
        // Zero is also the honest answer for a piece nobody in the squad can
        // wear, which is why a policy must treat "score 0" as "no reason to
        // buy" rather than as "unscored".
        public readonly float Score;

        public ShopCardView(int section, int index, ShopEntryKind kind, string contentId,
            int price, bool sold, bool noOffer, bool affordable, float score)
        {
            Section = section;
            Index = index;
            Kind = kind;
            ContentId = contentId ?? "";
            Price = price;
            Sold = sold;
            NoOffer = noOffer;
            Affordable = affordable;
            Score = score;
        }

        // A card that can still be bought by somebody with enough gold.
        public bool OnSale => !NoOffer && !Sold;

        public bool Buyable => OnSale && Affordable;
    }

    // One bag stack, as the sell list shows it.
    public readonly struct ShopBagRow
    {
        // Index into SaveData.stockpiledItems, which is what
        // RunOrchestrator.Sell takes. It moves when a stack empties, so the
        // driver rebuilds the view after every sale.
        public readonly int BagIndex;

        public readonly string ItemId;
        public readonly int Count;
        public readonly int SellPrice;

        // What wearing one would be worth to the squad, on the archetype's
        // own weights -- the same GearEvaluator number the cards carry, so
        // "sell the junk" and "buy the good thing" are ranked on one scale.
        public readonly float Score;

        // Another row earlier in the bag holds the same itemId. The cheap
        // read of "I already have one of these", which is the only kind of
        // duplicate a bag can show: two stacks exist precisely because their
        // (plus, modifiers, riftTier) differ.
        public readonly bool Duplicate;

        // Not equippable, so Score says nothing about it. Held separately
        // rather than inferred from a zero score, because a potion and an
        // unwearable helm are the same number and very different decisions.
        public readonly bool Consumable;

        public ShopBagRow(int bagIndex, string itemId, int count, int sellPrice,
            float score, bool duplicate, bool consumable)
        {
            BagIndex = bagIndex;
            ItemId = itemId ?? "";
            Count = count;
            SellPrice = sellPrice;
            Score = score;
            Duplicate = duplicate;
            Consumable = consumable;
        }
    }

    public readonly struct ShopView
    {
        public readonly IReadOnlyList<ShopCardView> Cards;

        // Index by ShopStock section. What the NEXT reroll of that section
        // would cost, and how many have already been paid for at this node.
        public readonly IReadOnlyList<int> RerollPrices;
        public readonly IReadOnlyList<int> RerollsUsed;

        public readonly IReadOnlyList<ShopBagRow> Bag;

        // The purse. Also on RunView, and duplicated here deliberately: every
        // affordability answer in this struct was computed against THIS
        // number, so a policy comparing a card against RunView.Gold instead
        // would be mixing two reads of the same value.
        public readonly int Gold;

        public ShopView(IReadOnlyList<ShopCardView> cards, IReadOnlyList<int> rerollPrices,
            IReadOnlyList<int> rerollsUsed, IReadOnlyList<ShopBagRow> bag, int gold)
        {
            Cards = cards ?? Array.Empty<ShopCardView>();
            RerollPrices = rerollPrices ?? Array.Empty<int>();
            RerollsUsed = rerollsUsed ?? Array.Empty<int>();
            Bag = bag ?? Array.Empty<ShopBagRow>();
            Gold = gold;
        }

        public int RerollPriceFor(int section) =>
            RerollPrices != null && section >= 0 && section < RerollPrices.Count
                ? RerollPrices[section]
                : ShopPricing.RerollCeiling;

        public int RerollsUsedIn(int section) =>
            RerollsUsed != null && section >= 0 && section < RerollsUsed.Count
                ? RerollsUsed[section]
                : 0;

        public bool CanAffordReroll(int section) => Gold >= RerollPriceFor(section);

        // The cheapest card still on sale anywhere, or 0 when the shelf is
        // empty. "Arrived with less than the cheapest card" is read off this
        // in the report, and a policy uses it to know when browsing is
        // pointless.
        public int CheapestOnSale()
        {
            int cheapest = 0;
            foreach (var card in Cards)
            {
                if (!card.OnSale) continue;
                if (cheapest == 0 || card.Price < cheapest) cheapest = card.Price;
            }

            return cheapest;
        }
    }
}
