using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // Everything an archetype decides OUTSIDE a fight: which room to walk
    // to, which item offer to take, which relic to draft, where a level-up
    // goes, which talent orb to kindle, and what it wants out of a piece of
    // gear. One archetype implements both this and IFightPolicy -- see
    // RandomLegalPolicy/GreedyAggressivePolicy -- but the two are separate
    // interfaces because the Core driver asks them at different points in
    // RunOrchestrator's sequence and a fight-only stand-in should not have to
    // fake map rules.
    public interface IRunPolicy
    {
        DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng);

        // Index into `offers`. `view.OfferScores` is index-aligned with
        // `offers` and carries what Core's GearEvaluator thinks each one is
        // worth to the squad -- empty when nothing scored them, which is what
        // RandomLegal ignores and every greedy archetype falls back from.
        int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng);

        // Index into `offer`.
        int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng);

        // WHAT THIS ARCHETYPE WANTS OUT OF A STAT. Read by Core's
        // GearEvaluator, which is the only side of the wall that can see what
        // an item id actually does -- see GearWeights' own header for why the
        // preference travels down as a vector instead of the numbers
        // travelling up.
        GearWeights Gear { get; }

        // Index into `options` -- which ability score one unspent point goes
        // into. Each option carries the deltas placing it there would actually
        // derive, measured by Core off the character sheet's own readers.
        int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng);

        // Index into `options` -- which talent orb to kindle, from those that
        // are affordable and whose prerequisites and gate are already met.
        // -1 for "buy nothing", which is a legal answer: an archetype with
        // embers in hand and nothing on the frontier worth having should be
        // able to say so.
        int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng);
    }
}
