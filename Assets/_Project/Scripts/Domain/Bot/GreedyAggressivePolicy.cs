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

        // Livelock guard for the fallback below: when NOTHING in `legal`
        // can put a positive number on anyone (DamagingTargetSelection
        // found no eligible target), a non-damaging Skill is picked at
        // most twice in a row before FightAction.LastResort takes over --
        // see GreedyDefensivePolicy's own repeat guard for the seed this
        // shape of bug was first found at (629, a ward rather than a
        // provoke, but the same "re-scored identically every turn with
        // nothing to show for it" livelock). Not stateless by construction
        // any more than GreedyDefensive is: BotRunDriver holds one policy
        // instance per archetype for a whole run, same as that policy's own
        // _repeatGuard.
        private readonly NonDamagingSkillGuard _repeatGuard = new NonDamagingSkillGuard();

        public FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            // One ally per ally-facing skill, before anything below ranks
            // anything. LegalActions offers a ward or a gift once per eligible
            // squadmate -- the player's own menu -- and this archetype has no
            // opinion about which squadmate; AllyTargetSelection carries those
            // rules. Narrowing here rather than teaching every score to break
            // the tie keeps that judgement in one place.
            legal = AllyTargetSelection.Narrow(session, actor, legal);

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

            // Every damaging option, locked onto the lowest-HP enemy among
            // the ones something here can actually damage -- not just the
            // lowest-HP enemy full stop. Locking on HP alone and only then
            // filtering by target would drop Attack outright whenever the
            // lowest-HP enemy sits outside melee reach (a back-rank foe with
            // avoidsFrontSlot, say), leaving nothing but a 0-damage Skill
            // (Provoke) aimed at that same enemy -- which never changes
            // anyone's HP, so the same target would be picked again next turn.
            // See DamagingTargetSelection's own header for the full shape.
            var damaging = legal.Where(a =>
                a.Kind == FightActionKind.Attack ||
                a.Kind == FightActionKind.Skill).ToList();

            if (DamagingTargetSelection.TryChooseDamagingAction(
                    damaging,
                    a => EstimateDamage(session, actor, a),
                    targets => targets.OrderBy(t => t.CurrentHealth).First(),
                    out var best))
            {
                _repeatGuard.RecordProgress(actor);
                return best;
            }

            // Nothing in `legal` can put a positive number on anyone right
            // now -- every damaging candidate whiffs (no reachable enemy for
            // any of them) and only non-damaging Skills remain. Take one,
            // but only while the guard says it has not already been tried
            // and failed to convert -- see NonDamagingSkillGuard's own
            // header for why a per-turn score alone cannot be the whole fix.
            var nonDamagingSkill = legal.FirstOrDefault(a =>
                a.Kind == FightActionKind.Skill && EstimateDamage(session, actor, a) == 0);

            if (nonDamagingSkill.Kind == FightActionKind.Skill)
            {
                string skillId = SkillIdFor(session, actor, nonDamagingSkill);
                if (_repeatGuard.MayChoose(actor, skillId, NonDamagingSkillGuard.DefaultMaxConsecutive))
                {
                    _repeatGuard.RecordChosen(actor, skillId);
                    return nonDamagingSkill;
                }

                // Guard tripped: the non-damaging Skill is treated as
                // unavailable this turn. An Attack would already have been
                // picked above (its Max(1, ...) floor always keeps its own
                // target eligible), so this is reachable only when no
                // Attack is legal at all -- kept anyway so this fallback
                // reads the same way GreedyDefensive's does.
                // FirstOrDefault + a Kind check is not safe here on its own --
                // FightActionKind.Attack is enum value 0, the same as
                // default(FightAction).Kind, so an empty match would read as
                // a false "yes, found one". Any() first.
                if (legal.Any(a => a.Kind == FightActionKind.Attack))
                {
                    return legal.First(a => a.Kind == FightActionKind.Attack);
                }
            }

            // Nothing to hit, nothing safe to repeat. See
            // FightAction.LastResort for why this is no longer a First(...)
            // on a pass action that is no longer unconditionally legal.
            return FightAction.LastResort(legal);
        }

        // The authored skill id behind one legal Skill action -- the key
        // NonDamagingSkillGuard tracks repeats under, read the same way
        // GreedyDefensivePolicy.FirstSkillWithEffect already reads a
        // skill's own SkillEffect off SkillOptionsFor rather than guessing
        // from SkillIndex alone.
        private static string SkillIdFor(FightSession session, CombatantState actor, FightAction action)
        {
            var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
            return option.Skill.Id;
        }

        // Pre-mitigation reads off the session's own preview queries --
        // PreviewSkillPower skips DamagePipeline.AfterDefences entirely (see
        // its own header), which is a proxy for "expected damage" that
        // ignores the target's defence and any variance roll. Good enough to
        // RANK the options against each other, wrong as an absolute
        // prediction -- and reimplementing DamagePipeline here to do better
        // is exactly what the plan says not to do.
        private static int EstimateDamage(FightSession session, CombatantState actor, FightAction action)
        {
            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    return FightAction.PreviewAttackDamage(actor, action.Target);
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
                    // a real limit of the archetype and the kind of thing
                    // GreedyDefensive/Lookahead2 exist to cover, not something
                    // to paper over with a preview that throws.
                    return FightAction.PreviewDamage(session, actor, option, action);
                default:
                    return 0;
            }
        }

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng)
        {
            if (choices.Count == 1) return choices[0];

            var treasure = choices.FirstOrDefault(n => n.Type == RoomType.Treasure);
            if (treasure != null) return treasure;

            if (view.PartyHpFraction < RestBelowPartyHpFraction)
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

        // Scored when there is a score, tier-then-plus when there is not.
        //
        // The tier-then-plus fallback exists because an ItemOffer names an
        // id, a tier and a plus, and nothing about what wearing it would do
        // (see GreedyDefensive's copy of this comment). The driver asks
        // Core's GearEvaluator the same question the Reckoning screen asks it
        // for the player, and hands the answers down on the view; ranking by
        // tier alone would make this archetype take a tier-3 helm over a
        // tier-2 sword while holding nothing in either hand, which is not
        // "aggressive", it is "reads only the biggest number".
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

        // Same 50% threshold ChooseNode uses above, surfaced for
        // ShopNodePreference rather than restated in it.
        public float RestBelowPartyHpFraction => 0.5f;

        // BUY THE BIGGEST NUMBER, NEVER REROLL, NEVER SELL.
        //
        // The same read of "best" the offer picker uses -- Core's
        // GearEvaluator score against this archetype's own weights -- so a
        // piece it would have taken for free is a piece it will pay for.
        // Gear before relics because gear is the thing it can score: a relic
        // is ranked only by rarity, which says how rare it is and not what it
        // does, and spending the purse on that first would starve the ranking
        // this archetype actually has.
        //
        // Score must be POSITIVE, not merely highest. GearEvaluator answers
        // zero for a piece nobody in the squad can wear and for one strictly
        // worse than what is already on, and paying gold for either is not
        // aggression.
        //
        // No reroll at all: rerolling is a bet that the next shelf is better,
        // and this archetype's whole posture is taking what is in front of it.
        public ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng)
        {
            // BOOKS FIRST. A spell is a straightforward power gain with no
            // gear-slot opportunity cost, and the archetype's whole read of
            // a shelf is "grab what's strong" -- the same reasoning that
            // puts rarest-relic ahead of gear below applies a step earlier
            // here. Cheapest affordable, not the most expensive: books have
            // no Score (ShopCardView's own header explains why -- it is a
            // gear-only number), so price is the only ranking signal a
            // policy has, and the cheap one leaves the most gold for
            // whatever else the shelf has.
            int cheapestBook = -1;
            int cheapestBookPrice = -1;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Book || !card.Buyable) continue;
                if (cheapestBook >= 0 && card.Price >= cheapestBookPrice) continue;

                cheapestBookPrice = card.Price;
                cheapestBook = card.Index;
            }

            if (cheapestBook >= 0) return ShopChoice.BuyBook(cheapestBook);

            int bestGear = -1;
            float bestScore = 0f;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Gear || !card.Buyable) continue;
                if (card.Score <= bestScore) continue;

                bestScore = card.Score;
                bestGear = card.Index;
            }

            if (bestGear >= 0) return ShopChoice.BuyGear(bestGear);

            // RAREST AFFORDABLE RELIC, which is the identical read of "best"
            // ChooseRelic uses on the draft screen -- lowest pool weight
            // first. Price is deliberately not the tiebreak: RelicPrice is a
            // pure function of rarity, so ranking on either gives the same
            // order and ranking on rarity says what is actually meant.
            int bestRelic = -1;
            int bestPrice = -1;

            foreach (var card in shop.Cards)
            {
                if (card.Kind != ShopEntryKind.Relic || !card.Buyable) continue;
                if (card.Price <= bestPrice) continue;

                bestPrice = card.Price;
                bestRelic = card.Index;
            }

            return bestRelic >= 0 ? ShopChoice.BuyRelic(bestRelic) : ShopChoice.Leave();
        }

        public SpellAssignmentChoice ChooseSpellAssignment(SpellAssignmentView view, RunView runView, SeededRandom rng) =>
            SpellAssignmentDefault.Choose(view);

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
