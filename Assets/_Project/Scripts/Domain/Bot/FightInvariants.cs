using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Bot
{
    // One assertion that is never legitimately true, tripped and reported
    // rather than thrown -- the bot cannot tell a bug from a bad matchup,
    // but it can hand back exactly where this one happened.
    public readonly struct InvariantHit
    {
        public readonly string Name;
        public readonly string Detail;

        public InvariantHit(string name, string detail)
        {
            Name = name;
            Detail = detail;
        }

        public override string ToString() => $"{Name}: {Detail}";
    }

    // The fight-level half of the plan's §3 bug list -- the run-level half
    // (offers, gold/exp/level across a won fight, RunManager.Choices) needs
    // the Core orchestrator this phase does not touch, and lands with it.
    public static class FightInvariants
    {
        // "A fight that is not over after 200 player commands." FightRunner
        // stops issuing commands once this is hit, so it fires at most once
        // per fight rather than once per command past the line.
        public const int MaxPlayerCommands = 200;

        public static List<InvariantHit> Check(FightSession session, int commandsIssued)
        {
            var hits = new List<InvariantHit>();
            if (session == null) return hits;

            foreach (var combatant in session.Encounter.PlayerParty.Concat(session.Encounter.Enemies))
            {
                if (!combatant.IsAlive) continue;

                if (combatant.CurrentHealth > combatant.MaxHealth)
                {
                    hits.Add(new InvariantHit("HpAboveMax",
                        $"{combatant.Name} at {combatant.CurrentHealth}/{combatant.MaxHealth}"));
                }

                // Unreachable through any production path -- IsAlive already
                // means CurrentHealth > 0 -- but CurrentHealth is a public
                // mutable field, so a test fixture can still force this
                // shape directly, which is exactly the case the plan asks
                // for coverage on.
                if (combatant.CurrentHealth < 0)
                {
                    hits.Add(new InvariantHit("HpBelowZero", $"{combatant.Name} at {combatant.CurrentHealth}"));
                }
            }

            if (commandsIssued > MaxPlayerCommands)
            {
                hits.Add(new InvariantHit("TooManyCommands", $"{commandsIssued} player commands issued"));
            }

            if (!session.IsOver && session.IsPlayerTurn)
            {
                var actor = session.Current;
                var legal = FightAction.LegalActions(session, actor, System.Array.Empty<SatchelStack>());
                if (legal.Count == 0)
                {
                    hits.Add(new InvariantHit("NoLegalAction", $"{actor?.Name ?? "(null)"} has no legal action"));
                }
            }

            if (session.Payout != null && !session.IsOver)
            {
                hits.Add(new InvariantHit("PayoutBeforeOver", "Payout is set before IsOver"));
            }

            if (session.IsOver && session.PlayerWon && session.Payout == null)
            {
                hits.Add(new InvariantHit("PayoutMissingAfterWin", "Payout is null after a win"));
            }

            return hits;
        }
    }
}
