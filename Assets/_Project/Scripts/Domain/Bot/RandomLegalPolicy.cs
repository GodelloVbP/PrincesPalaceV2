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
    }
}
