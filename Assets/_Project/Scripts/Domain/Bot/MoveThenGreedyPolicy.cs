using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // A BOUNDED SCRIPT, not an archetype: Move on the first two player turns
    // of a fight, then play greedy-aggressive for the rest of it.
    //
    // It exists so a test can prove Move survives a whole fight -- the swap,
    // the enemy's reply against the new formation, the re-picked intents, the
    // party-order invariant -- without leaving termination to chance. An
    // ALWAYS-Move policy is not asked to finish a fight and is not written
    // here: stalling by pacing back and forth is legitimate play, so a fight
    // that never ends under it would be the policy working, not a bug, and a
    // test that cannot tell those apart is not a test.
    //
    // Bounded means bounded at TWO, not "until it stops being legal". Two is
    // enough to put a different body in rank 0 and then put it back, which is
    // the whole shape; more would only add turns for the enemy to spend.
    //
    // NOT REGISTERED in Archetypes: it is not a way of playing, and a batch
    // run measuring it would be measuring a script.
    public sealed class MoveThenGreedyPolicy : IFightPolicy
    {
        public const int ScriptedMoves = 2;

        private readonly GreedyAggressivePolicy _afterwards = new GreedyAggressivePolicy();
        private int _movesMade;

        // Which way the scripted moves go. Back first, then forward again --
        // a retreat and a return, so the formation ends where it started and
        // a test asserting on the final order is asserting on the round trip.
        private static readonly MoveDirection[] Script =
        {
            MoveDirection.Back,
            MoveDirection.Forward,
        };

        public FightAction Choose(FightSession session, CombatantState actor,
                                  IReadOnlyList<FightAction> legal, SeededRandom rng)
        {
            if (_movesMade < ScriptedMoves)
            {
                var wanted = Script[_movesMade];

                foreach (var action in legal)
                {
                    if (action.Kind != FightActionKind.Move || action.MoveDirection != wanted) continue;

                    // Counted only when one is actually TAKEN. A solo party
                    // or a rooted actor offers no Move at all, and burning a
                    // scripted step on a turn where none was available would
                    // silently reduce the script to one move or none -- the
                    // failure a bounded script exists to avoid.
                    _movesMade++;
                    return action;
                }
            }

            return _afterwards.Choose(session, actor, legal, rng);
        }
    }
}
