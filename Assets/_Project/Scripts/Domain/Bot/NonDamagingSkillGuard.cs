using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Bot
{
    // Per-actor memory of "I have chosen this non-progressing skill without
    // a swing landing since", shared by GreedyDefensivePolicy (fleece_ward,
    // the seed 629 livelock) and Lookahead2Policy (woolgathering, seed 18
    // and others -- see that policy's own header for the actual root cause,
    // which this guard is a backstop for rather than the fix itself).
    //
    // Neither policy is stateless by construction (both already hold a
    // Dictionary keyed by actor for exactly this purpose; GreedyDefensive's
    // was private and ward-only before this type existed), and the shape a
    // livelock takes is always the same: a per-turn score or priority ranks
    // some action highest for reasons that do not change turn to turn, so a
    // memoryless policy keeps making the same choice forever even after it
    // has stopped buying anything. The fix is not "score it better" (that is
    // a arms race against every enemy kit that can out-heal or out-shield
    // whatever the score sees) -- it is "remember that this was already
    // tried and did not convert, and stop trying it".
    //
    // `key` is caller-defined (a skill id is typical) so two different
    // no-progress skills on the same actor are tracked independently -- an
    // actor who alternates Ward/Ward/Provoke/Provoke should not have the
    // Ward count bleed into the Provoke count or vice versa.
    public sealed class NonDamagingSkillGuard
    {
        // The task's own number: a non-damaging, non-restorative skill may
        // repeat twice in a row with nothing to show for it before the
        // guard steps in. GreedyDefensive's pre-existing ward guard keeps
        // its own higher threshold (3) rather than being retuned to this --
        // see that policy's MaxConsecutiveWardsWithoutASwing for why.
        public const int DefaultMaxConsecutive = 2;

        private readonly Dictionary<CombatantState, Dictionary<string, int>> _counts =
            new Dictionary<CombatantState, Dictionary<string, int>>();

        // True while `key` has been chosen fewer than `max` times running
        // for `actor` since the last RecordProgress call. Actors and keys
        // never seen before always read as allowed.
        public bool MayChoose(CombatantState actor, string key, int max)
        {
            if (actor == null || key == null) return true;
            return !(_counts.TryGetValue(actor, out var perActor) &&
                      perActor.TryGetValue(key, out int count) && count >= max);
        }

        // Call once, for the actor's actual final choice this turn, when
        // that choice was the guarded no-progress skill.
        public void RecordChosen(CombatantState actor, string key)
        {
            if (actor == null || key == null) return;

            if (!_counts.TryGetValue(actor, out var perActor))
            {
                perActor = new Dictionary<string, int>();
                _counts[actor] = perActor;
            }

            perActor.TryGetValue(key, out int count);
            perActor[key] = count + 1;
        }

        // Call once, for the actor's actual final choice this turn, whenever
        // that choice was anything else -- a swing landed (or was at least
        // attempted), a heal actually restored something, an item was
        // drunk. Clears EVERY key for the actor, not just the one that was
        // guarded: a turn spent on real progress is what the whole guard
        // exists to wait for, so nothing tracked before it should still
        // count against a later, unrelated repeat.
        public void RecordProgress(CombatantState actor)
        {
            if (actor == null) return;
            _counts.Remove(actor);
        }
    }
}
