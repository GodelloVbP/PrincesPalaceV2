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

                    // ONLY THE TWO EFFECTS THAT ACTUALLY PREVIEW A NUMBER.
                    //
                    // PreviewSkillPower ends in SkillResolution.Amount, whose
                    // switch handles DamageSingle/DamageAll, the three heals,
                    // and Summon -- and THROWS on everything else. Eight
                    // authored player skills land in that default (Provoke,
                    // Transform, Ward, Shatter, BuffParty and the three
                    // Gifts), so asking for a preview of one is not a poor
                    // estimate, it is an ArgumentOutOfRangeException out of
                    // the middle of a fight. The balance bot found it on
                    // seed 1; FightController.Hud's detail card reaches the
                    // same call for the same skills.
                    //
                    // Scored 0 rather than guarded with a catch, because 0 is
                    // the honest answer to the question this method asks: a
                    // Provoke deals no damage, and this ranks DAMAGE. It does
                    // mean GreedyAggressive never opens with a Ward or a
                    // Shatter unless nothing else is on the menu -- which is
                    // a real limit of the archetype and is the kind of thing
                    // Phase 6's GreedyDefensive/Lookahead2 exist to cover,
                    // not something to paper over with a preview that throws.
                    bool previewable = option.Skill.Effect == SkillEffect.DamageSingle
                                    || option.Skill.Effect == SkillEffect.DamageAll;

                    return previewable ? session.PreviewSkillPower(actor, option.Skill) : 0;
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

        // SCORED WHEN THERE IS A SCORE, tier-then-plus when there is not.
        //
        // The fallback is what this method used to be outright, and its
        // comment (kept, on GreedyDefensive's copy) explains why: an ItemOffer
        // names an id, a tier and a plus, and nothing about what wearing it
        // would DO. That is still true of the offer -- what changed is that
        // the driver now asks Core's GearEvaluator the same question the
        // Reckoning screen asks it for the player, and hands the answers down
        // on the view. Ranking by tier alone made this archetype take a tier-3
        // helm over a tier-2 sword while holding nothing in either hand, which
        // is not "aggressive", it is "reads only the biggest number".
        //
        // The fallback is not dead code: a squad with nobody who can wear any
        // of the three scores all three at zero, and RunView.OfferScores is
        // empty whenever the driver had no live save to score against.
        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng)
        {
            var scores = view.OfferScores;
            if (scores != null && scores.Count == offers.Count && offers.Count > 0)
            {
                return BestIndexTiedByRng(offers.Count, i => scores[i], rng);
            }

            return BestIndexTiedByRng(offers.Count,
                i => (offers[i].Tier, offers[i].Plus),
                rng);
        }

        public GearWeights Gear => GearWeights.Aggressive;

        // THE SAME WEIGHTS THE GEAR PICK USES, applied to what one point would
        // derive. Strength is not hardcoded here and must not be: STR derives
        // nothing through AbilityDerivation and reaches damage through weapon
        // scaling instead, so "the score that raises attack most" is DEX for a
        // character holding a finesse weapon and STR for one holding a
        // greatsword. Core measures both and this ranks the answers.
        public int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng)
        {
            if (options.Count == 0) return -1;
            var weights = Gear;
            return BestIndexTiedByRng(options.Count, i => weights.Score(options[i].Deltas), rng);
        }

        // Best measurable orb, ties by rng -- and a tie is the common case,
        // because a talent whose whole effect is a combat rule change derives
        // nothing this can see (see TalentOption's header). Taking one anyway
        // rather than returning -1 is deliberate: an unspent ember buys
        // nothing, and the tree's own prerequisite chain means the orb taken
        // now is what opens the one that might be measurable later.
        public int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng)
        {
            if (options.Count == 0) return -1;
            var weights = Gear;
            return BestIndexTiedByRng(options.Count, i => weights.Score(options[i].Deltas), rng);
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
