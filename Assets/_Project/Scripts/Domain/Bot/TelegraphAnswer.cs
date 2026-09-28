using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Bot
{
    // How the bot answered a seat-sized telegraph, for the knell trace.
    public enum TelegraphAnswerKind
    {
        None,
        Step,
        Passage,
    }

    // THE BOT ANSWERS A TELEGRAPH (docs/PLAN_BELLWETHER_KIT.md 3.10, M6).
    //
    // One rule every archetype gets before its own Choose, applied by
    // FightRunner so no policy is edited and -NoTelegraphAnswer can switch
    // it off. Trigger: a living enemy's committed intent is SEAT-SIZED
    // (EnemyIntent.DamageBySeat, the Death Knell) and its target stands in
    // a seat another seat would take less in. Outcome, first that applies:
    //
    //   1. a ready Palace Passage (any seat-destination cast in the legal
    //      list) that moves that target to the cheapest seat -- a free
    //      action, so the runner asks again and the archetype still acts;
    //   2. the target itself holding the turn: a plain Move one seat
    //      (either way) when that seat saves at least
    //      StepMinSavingPercentOfMaxHp of the target's max health.
    //
    // Otherwise nothing, and the archetype chooses as it always did. Any
    // party member: the target is whoever the intent names, and the Passage
    // may be cast by anyone who owns it.
    //
    // R3 of the plan applies: this reads the exact per-seat numbers every
    // time, so it answers better than a human who misreads the badge. The
    // step-only cell (no book) is the contract M7 tunes against.
    public static class TelegraphAnswer
    {
        // A turn-ending step back is worth it when it saves at least this
        // percent of the target's max health (plan 3.10, assumed). The Passage
        // has no threshold: it is free, and any saving is a saving.
        //
        // 15, not M6's 25 (M7): it must sit BELOW the knell's middle entry
        // (20) or the bot stops one seat short -- it would take middle -> rear
        // for granted as "not worth a turn" while the badge says "Step back"
        // and a player steps. An int percent so the ceiling is exact (0.15f
        // times 1000 is 150.00001 and would ceil to 151).
        public const int StepMinSavingPercentOfMaxHp = 15;

        // The command that answers the telegraph, or null for "let the
        // archetype choose". `legal` is the actor's full legal list, and the
        // answer is always one of its entries.
        public static FightAction? Choose(
            FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal,
            out TelegraphAnswerKind kind)
        {
            kind = TelegraphAnswerKind.None;
            if (session == null || actor == null || legal == null || legal.Count == 0) return null;

            FightAction? best = null;
            int bestSaving = 0;

            foreach (var threat in Threats(session))
            {
                var target = threat.Key;
                var cost = threat.Value;
                int here = session.Encounter.SeatOf(target);
                if (here < 0 || here >= cost.Length) continue;

                // 1. The free Passage, to the cheapest seat; the nearer seat
                // on a tie, so a traveller is not flung further than needed.
                foreach (var action in legal)
                {
                    if (action.Kind != FightActionKind.Skill || action.DestinationSeat < 0) continue;
                    if (!ReferenceEquals(action.Target, target)) continue;

                    int saving = cost[here] - cost[action.DestinationSeat];
                    if (saving <= 0) continue;

                    if (best == null || kind != TelegraphAnswerKind.Passage || saving > bestSaving
                        || (saving == bestSaving
                            && System.Math.Abs(action.DestinationSeat - here)
                               < System.Math.Abs(best.Value.DestinationSeat - here)))
                    {
                        best = action;
                        bestSaving = saving;
                        kind = TelegraphAnswerKind.Passage;
                    }
                }

                // 2. The target's own step, only while no Passage answers.
                if (kind == TelegraphAnswerKind.Passage || !ReferenceEquals(actor, target)) continue;

                int threshold = (target.MaxHealth * StepMinSavingPercentOfMaxHp + 99) / 100;
                foreach (var action in legal)
                {
                    if (action.Kind != FightActionKind.Move) continue;

                    int to = action.MoveDirection == MoveDirection.Back ? here + 1 : here - 1;
                    if (to < 0 || to >= cost.Length) continue;

                    int saving = cost[here] - cost[to];
                    if (saving < threshold || saving <= bestSaving) continue;

                    best = action;
                    bestSaving = saving;
                    kind = TelegraphAnswerKind.Step;
                }
            }

            if (best == null) kind = TelegraphAnswerKind.None;
            return best;
        }

        // Every living party member some committed seat-sized intent names,
        // with that intent's per-seat damage summed across every enemy
        // aiming one at them. A player-side target only: an enemy-side one
        // would be a heal or a buff read wrong.
        public static Dictionary<CombatantState, int[]> Threats(FightSession session)
        {
            var threats = new Dictionary<CombatantState, int[]>();
            if (session == null) return threats;

            foreach (var enemy in session.Encounter.LivingEnemies)
            {
                var intent = session.IntentDetailFor(enemy);
                if (!intent.HasValue) continue;

                var bySeat = intent.Value.DamageBySeat;
                var target = intent.Value.Target;
                if (bySeat == null || target == null || !target.IsAlive || !target.IsPlayerSide) continue;

                if (!threats.TryGetValue(target, out var sum))
                {
                    sum = new int[bySeat.Length];
                    threats[target] = sum;
                }

                for (int s = 0; s < sum.Length && s < bySeat.Length; s++) sum[s] += bySeat[s];
            }

            return threats;
        }
    }
}
