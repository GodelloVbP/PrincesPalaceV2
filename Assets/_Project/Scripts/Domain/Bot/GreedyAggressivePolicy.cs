using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // Plays to win, nothing else. In a fight: burn the lowest-HP reachable
    // enemy down with whatever hits it hardest, heal only when actually in
    // danger, never pass. On the map: bank treasure, rest only when hurt,
    // otherwise fight. On offers and drafts: take the biggest number on the
    // table.
    public sealed class GreedyAggressivePolicy : IFightPolicy, IRunPolicy
    {
        // Below this fraction of max HP, a healing item outranks everything
        // else on the menu -- see the plan's own archetype description.
        private const float HealBelowHealthFraction = 0.30f;

        public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            if (actor != null && actor.MaxHealth > 0 &&
                actor.CurrentHealth <= actor.MaxHealth * HealBelowHealthFraction)
            {
                // A HEALING item specifically -- a mana potion at 20% HP does
                // not save anyone, and the satchel is not assumed to hold one
                // of each.
                foreach (var candidate in legal)
                {
                    if (candidate.Kind == FightActionKind.Item && !candidate.ItemRestoresMana)
                    {
                        return candidate;
                    }
                }
            }

            // Every damaging option, grouped onto the lowest-HP reachable
            // enemy so the whole party's worth of pressure lands on one
            // target instead of spreading thin across the board.
            var damaging = legal.Where(a =>
                a.Kind == FightActionKind.Attack ||
                a.Kind == FightActionKind.BasicSpell ||
                a.Kind == FightActionKind.Skill).ToList();

            if (damaging.Count > 0)
            {
                var lowestHp = damaging
                    .Select(a => a.Target)
                    .OrderBy(t => t.CurrentHealth)
                    .First();

                FightAction best = default;
                int bestEstimate = int.MinValue;
                bool found = false;

                foreach (var candidate in damaging)
                {
                    if (candidate.Target != lowestHp) continue;

                    int estimate = EstimateDamage(session, actor, candidate);
                    if (!found || estimate > bestEstimate)
                    {
                        best = candidate;
                        bestEstimate = estimate;
                        found = true;
                    }
                }

                if (found) return best;
            }

            // Nothing to hit and no reason to heal -- HoldBack is the only
            // thing left standing (it is always in `legal`).
            return legal.First(a => a.Kind == FightActionKind.HoldBack);
        }

        // Pre-mitigation reads off the session's own preview queries --
        // PreviewBasicSpellPower and PreviewSkillPower both skip
        // DamagePipeline.AfterDefences entirely (see their own headers),
        // which is a proxy for "expected damage" that ignores the target's
        // defence and any variance roll. Good enough to RANK the options
        // against each other, wrong as an absolute prediction -- and
        // reimplementing DamagePipeline here to do better is exactly what
        // the plan says not to do.
        private static int EstimateDamage(FightSession session, CombatantState actor, FightAction action)
        {
            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    return CombatMath.ComputeAttackDamage(actor, action.Target);
                case FightActionKind.BasicSpell:
                    return session.PreviewBasicSpellPower(actor);
                case FightActionKind.Skill:
                    var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                    return session.PreviewSkillPower(actor, option.Skill);
                default:
                    return 0;
            }
        }

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng)
        {
            if (choices.Count == 1) return choices[0];

            var treasure = choices.FirstOrDefault(n => n.Type == RoomType.Treasure);
            if (treasure != null) return treasure;

            if (view.PartyHpFraction < 0.5f)
            {
                var rest = choices.FirstOrDefault(n => n.Type == RoomType.Rest);
                if (rest != null) return rest;
            }

            var fight = choices.FirstOrDefault(n => n.Type == RoomType.Fight);
            if (fight != null) return fight;

            // Boss/elite are taken when they are the only way forward -- by
            // the time every other case above has fallen through, that is
            // exactly what is left.
            var boss = choices.FirstOrDefault(n => n.Type == RoomType.Boss);
            if (boss != null) return boss;

            var elite = choices.FirstOrDefault(n => n.Type == RoomType.EliteFight);
            if (elite != null) return elite;

            return choices[0];
        }

        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng)
        {
            return BestIndexTiedByRng(offers.Count,
                i => (offers[i].Tier, offers[i].Plus),
                rng);
        }

        public int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng)
        {
            // RelicPool.WeightOf is the pool's own draft-odds table --
            // common relics weigh the most so they are drawn most often.
            // "First by the pool's weighting order" here means the rarest
            // (lowest-weight) relic on offer, the same read of "best" the
            // offer picker above uses for tier/plus.
            return BestIndexTiedByRng(offer.Count,
                i => -RelicPool.WeightOf(offer[i].Rarity),
                rng);
        }

        // Picks the index whose key compares highest, breaking ties with
        // `rng` -- shared by ChooseOffer and ChooseRelic so both read "best,
        // ties by rng" the same way rather than two hand-rolled loops that
        // could drift apart.
        private static int BestIndexTiedByRng<TKey>(int count, System.Func<int, TKey> keyOf, SeededRandom rng)
            where TKey : System.IComparable<TKey>
        {
            var tied = new List<int> { 0 };
            var bestKey = keyOf(0);

            for (int i = 1; i < count; i++)
            {
                var key = keyOf(i);
                int cmp = key.CompareTo(bestKey);
                if (cmp > 0)
                {
                    bestKey = key;
                    tied.Clear();
                    tied.Add(i);
                }
                else if (cmp == 0)
                {
                    tied.Add(i);
                }
            }

            return tied.Count == 1 ? tied[0] : tied[rng.NextInt(0, tied.Count)];
        }
    }
}
