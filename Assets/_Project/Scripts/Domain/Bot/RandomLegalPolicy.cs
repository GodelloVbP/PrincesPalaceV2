using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // The fuzzer archetype: uniform over whatever is legal, in a fight and
    // out of it alike. Stands in for a lost novice clicking at random --
    // the floor every other archetype is measured against, not a strategy.
    public sealed class RandomLegalPolicy : IFightPolicy, IRunPolicy
    {
        public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            return legal[rng.NextInt(0, legal.Count)];
        }

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng)
        {
            return choices[rng.NextInt(0, choices.Count)];
        }

        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng)
        {
            return rng.NextInt(0, offers.Count);
        }

        public int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng)
        {
            return rng.NextInt(0, offer.Count);
        }

        // NO OPINION, which is this archetype's whole answer to every
        // question. Deliberately Indifferent and not Uniform: an all-ones
        // vector still RANKS (a +40 health belt beats a +2 speed ring), and
        // ranking is exactly what this archetype does not do. Every legal
        // candidate scores zero, ties, and Core's evaluator draws between them
        // with the seeded rng -- see GearWeights.Indifferent.
        public GearWeights Gear => GearWeights.Indifferent;

        public int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng)
        {
            return options.Count == 0 ? -1 : rng.NextInt(0, options.Count);
        }

        public int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng)
        {
            return options.Count == 0 ? -1 : rng.NextInt(0, options.Count);
        }

        // NO REST RULE, so nothing carves out of ShopNodePreference: this
        // archetype takes whatever it is handed at whatever health.
        public float RestBelowPartyHpFraction => 0f;

        // UNIFORM OVER WHAT IS LEGAL, with a coin flip for leaving first.
        //
        // The flip is not a strategy, it is the termination argument. Every
        // other choice this archetype could make leaves at least one legal
        // choice available afterwards, so "uniform over legal choices
        // including leave" would leave a shelf of six cards with a 1-in-7
        // chance of ending per call -- against the driver's cap of twelve,
        // that is a fuzzer that mostly hits the cap and mostly reports a
        // truncated visit. Half a chance per call ends a visit in two
        // choices on average and reaches the cap about once in 4,000 visits.
        public ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng)
        {
            if (rng.NextInt(0, 2) == 0) return ShopChoice.Leave();

            var legal = new List<ShopChoice>();

            foreach (var card in shop.Cards)
            {
                if (!card.Buyable) continue;
                if (card.Kind == ShopEntryKind.Gear) legal.Add(ShopChoice.BuyGear(card.Index));
                else if (card.Kind == ShopEntryKind.Relic) legal.Add(ShopChoice.BuyRelic(card.Index));
                else if (card.Kind == ShopEntryKind.Book) legal.Add(ShopChoice.BuyBook(card.Index));
                else if (card.Kind == ShopEntryKind.Consumable) legal.Add(ShopChoice.BuyConsumable(card.Index));
            }

            foreach (var row in shop.Bag)
            {
                if (row.SellPrice > 0) legal.Add(ShopChoice.Sell(row.BagIndex, 1));
            }

            for (int section = 0; section < ShopStock.SectionCount; section++)
            {
                if (shop.CanAffordReroll(section)) legal.Add(ShopChoice.Reroll(section));
            }

            return legal.Count == 0 ? ShopChoice.Leave() : legal[rng.NextInt(0, legal.Count)];
        }

        public SpellAssignmentChoice ChooseSpellAssignment(SpellAssignmentView view, RunView runView, SeededRandom rng) =>
            SpellAssignmentDefault.Choose(view);
    }
}
