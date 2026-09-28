using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // GREEDY AGGRESSIVE, PLUS ONE RULE: a front-rank character below 40%
    // health steps behind a healthier ally instead of swinging.
    //
    // WHY IT EXISTS. Enemy melee now concentrates on rank 0 the way the
    // player's always has, which makes Move a real tank swap and makes its
    // cost -- a whole turn -- a real question. Measuring that cost against
    // the four existing archetypes alone would answer it badly: three of them
    // never Move at all and the fourth (RandomLegal) Moves at random, so a
    // batch would report what "Move used stupidly" costs, not what the rule
    // is worth. This is the archetype that uses it sensibly, and the paired
    // numbers only mean something with it in the set.
    //
    // COMPOSED, NOT INHERITED. GreedyAggressivePolicy is sealed and its
    // fight brain carries a live repeat guard; wrapping one instance keeps
    // that guard's state intact and keeps the four existing archetypes
    // byte-identical, which unsealing and overriding would not promise.
    // Every IRunPolicy member forwards untouched -- outside a fight this IS
    // GreedyAggressive, so any difference in the numbers is Move's.
    public sealed class ProtectTheFrontPolicy : IFightPolicy, IRunPolicy
    {
        // The health fraction below which the front rank is judged not to be
        // able to take another round of it.
        public const float RetreatBelowHealthFraction = 0.40f;

        private readonly GreedyAggressivePolicy _greedy = new GreedyAggressivePolicy();

        public FightAction Choose(FightSession session, CombatantState actor,
                                  IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            if (WantsToRetreat(session, actor))
            {
                foreach (var action in legal)
                {
                    if (action.Kind == FightActionKind.Move
                        && action.MoveDirection == MoveDirection.Back)
                    {
                        return action;
                    }
                }
            }

            return _greedy.Choose(session, actor, legal, rng);
        }

        // ASKED OF THE ACTING CHARACTER ONLY, because that is the only
        // character this decision can move: Move is the acting character's
        // own command, so "the front member retreats" can only happen on the
        // front member's turn. A healthy ally stepping FORWARD to take the
        // hit instead would be the other half of the same idea and is
        // deliberately not here -- one rule, so the measurement reads as one
        // change.
        //
        // FRACTIONS, NOT RAW HEALTH, on both sides of "healthier": party
        // members do not share a max, and a 300/900 tank is in more trouble
        // than a 120/200 caster despite carrying more points.
        private static bool WantsToRetreat(FightSession session, CombatantState actor)
        {
            if (session == null || actor == null || !actor.IsAlive) return false;
            if (session.Encounter.LivingRankOf(actor) != 0) return false;

            float mine = HealthFraction(actor);
            if (mine >= RetreatBelowHealthFraction) return false;

            // "BEHIND" IS BY SEAT, and only the living count. A step back is
            // only a retreat when it TRADES: stepping into an empty seat
            // leaves the actor rank 0 among the living and still the one
            // melee lands on (PLAN_BELLWETHER_KIT 1.1). With a trade on offer,
            // anyone at all behind who is in better shape is enough -- the
            // trade is with the next seat back, so which one is healthiest
            // does not change what the command would do. In a full party the
            // next seat back is always occupied, so this reads as it always
            // did.
            var encounter = session.Encounter;
            int seat = encounter.SeatOf(actor);
            if (encounter.OccupantOf(seat + 1) == null) return false;

            foreach (var member in encounter.PlayerParty)
            {
                if (ReferenceEquals(member, actor)) continue;
                if (encounter.SeatOf(member) <= seat) continue;

                if (HealthFraction(member) > mine) return true;
            }

            return false;
        }

        private static float HealthFraction(CombatantState combatant) =>
            combatant == null || combatant.MaxHealth <= 0
                ? 0f
                : combatant.CurrentHealth / (float)combatant.MaxHealth;

        // ---- outside a fight, this IS GreedyAggressive ------------------------

        public DescentNode ChooseNode(IReadOnlyList<DescentNode> choices, RunView view, SeededRandom rng) =>
            _greedy.ChooseNode(choices, view, rng);

        public int ChooseOffer(IReadOnlyList<ItemOffer> offers, RunView view, SeededRandom rng) =>
            _greedy.ChooseOffer(offers, view, rng);

        public int ChooseRelic(IReadOnlyList<RelicOption> offer, RunView view, SeededRandom rng) =>
            _greedy.ChooseRelic(offer, view, rng);

        public GearWeights Gear => _greedy.Gear;

        public int ChooseStat(IReadOnlyList<StatOption> options, RunView view, SeededRandom rng) =>
            _greedy.ChooseStat(options, view, rng);

        public int ChooseTalent(IReadOnlyList<TalentOption> options, RunView view, SeededRandom rng) =>
            _greedy.ChooseTalent(options, view, rng);

        public ShopChoice ChooseShop(ShopView shop, RunView view, SeededRandom rng) =>
            _greedy.ChooseShop(shop, view, rng);

        public SpellAssignmentChoice ChooseSpellAssignment(SpellAssignmentView view, RunView runView, SeededRandom rng) =>
            _greedy.ChooseSpellAssignment(view, runView, rng);

        public float RestBelowPartyHpFraction => _greedy.RestBelowPartyHpFraction;
    }
}
