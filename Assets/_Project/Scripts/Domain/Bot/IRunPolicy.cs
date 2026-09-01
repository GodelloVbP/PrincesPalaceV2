using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // Everything an archetype decides OUTSIDE a fight: which room to walk
    // to, which item offer to take, which relic to draft. One archetype
    // implements both this and IFightPolicy -- see RandomLegalPolicy/
    // GreedyAggressivePolicy -- but the two are separate interfaces because
    // the Core driver asks them at different points in RunOrchestrator's
    // sequence and a fight-only stand-in should not have to fake map rules.
    public interface IRunPolicy
    {
        DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng);

        // Index into `offers`.
        int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng);

        // Index into `offer`.
        int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng);
    }
}
