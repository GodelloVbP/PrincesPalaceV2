using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // Plays one already-built FightSession to its end with one IFightPolicy,
    // one command at a time, exactly the way a player would: Begin(), then
    // while the player still has a turn, ask the policy, issue the command
    // through FightAction.Apply, check invariants, record a beat.
    public static class FightRunner
    {
        // A hard stop above FightInvariants.MaxPlayerCommands so a policy
        // that keeps re-offering a command the session silently refuses
        // (CastSkill returning false without spending the turn -- "Shatter
        // with nothing to detonate" and its like) cannot spin forever: the
        // TooManyCommands hit fires first and this loop exits on the same
        // pass, but the cap exists independently so a session with that
        // check somehow bypassed still cannot hang the batch.
        public const int HardCommandCap = FightInvariants.MaxPlayerCommands + 50;

        public static List<InvariantHit> Play(
            FightSession session, IFightPolicy policy, IReadOnlyList<SatchelStack> satchel,
            SeededRandom rng, FightTrace traceOut)
        {
            var hits = new List<InvariantHit>();
            if (session == null || policy == null || rng == null) return hits;

            session.Begin();

            // A LOCAL copy, decremented as items are used. The real
            // save-side satchel decrement is Core's job (RunOrchestrator,
            // Phase 1) -- this exists only so a policy asked twice in the
            // same fight sees an accurate count and cannot "use" more
            // potions than the satchel actually holds.
            var localSatchel = satchel?
                .Select(s => new SatchelStack(s.ItemId, s.DisplayName, s.Count, s.RestoresMana))
                .ToList() ?? new List<SatchelStack>();

            int commands = 0;

            while (!session.IsOver && session.IsPlayerTurn && commands < HardCommandCap)
            {
                var actor = session.Current;
                var legal = FightAction.LegalActions(session, actor, localSatchel);

                if (legal.Count == 0)
                {
                    hits.Add(new InvariantHit("NoLegalAction",
                        $"{actor?.Name ?? "(null)"} has no legal action at command {commands}"));
                    break;
                }

                var action = policy.Choose(session, actor, legal, rng);
                FightAction.Apply(session, action);
                commands++;

                if (action.Kind == FightActionKind.Item)
                {
                    DecrementSatchel(localSatchel, action.ItemId);
                }

                if (traceOut != null)
                {
                    traceOut.TurnTraces.Add(new TurnTrace
                    {
                        ActorId = actor?.Name ?? "",
                        Action = action.ToString(),
                        TargetId = action.Target?.Name ?? "",
                        PartyHpAfter = session.Encounter.LivingPlayerParty.Sum(c => c.CurrentHealth),
                        EnemyHpAfter = session.Encounter.LivingEnemies.Sum(c => c.CurrentHealth),
                    });
                }

                var turnHits = FightInvariants.Check(session, commands);
                hits.AddRange(turnHits);

                if (turnHits.Any(h => h.Name == "TooManyCommands"))
                {
                    break;
                }
            }

            return hits;
        }

        private static void DecrementSatchel(List<SatchelStack> satchel, string itemId)
        {
            for (int i = 0; i < satchel.Count; i++)
            {
                if (satchel[i].ItemId != itemId) continue;

                var stack = satchel[i];
                satchel[i] = new SatchelStack(stack.ItemId, stack.DisplayName,
                    System.Math.Max(0, stack.Count - 1), stack.RestoresMana);
                return;
            }
        }
    }
}
