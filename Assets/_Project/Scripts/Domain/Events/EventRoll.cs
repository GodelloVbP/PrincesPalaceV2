using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Events
{
    // Picks one event id from a floor's eligible pool. Plan contract 3: an
    // event is eligible on floor F when its Floors list contains F (empty
    // means every floor), it has not been seen this run, and its event-level
    // Requires all pass. Empty pool returns "" rather than throwing -- the
    // caller (RunOrchestrator.Event.cs) falls through to today's
    // RoomResolver.Resolve path when that happens.
    public static class EventRoll
    {
        // The caller passes `rng` (a stream keyed to (runSeed, step, nodeId)
        // -- RngStreams.Event, deliberately not shared with
        // Treasure's stream, see plan contract 3) rather than this method
        // owning a seed, so the pick stays reproducible from whatever state
        // the caller is already threading through a run.
        public static string Pick(
            IReadOnlyList<ResolvedEventDefinition> candidates,
            int floor,
            IReadOnlyCollection<string> seenEventIds,
            IEventContext context,
            SeededRandom rng)
        {
            if (candidates == null || candidates.Count == 0 || rng == null) return "";

            var eligible = new List<ResolvedEventDefinition>();
            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (seenEventIds != null && seenEventIds.Contains(candidate.Id)) continue;
                if (candidate.Floors != null && candidate.Floors.Length > 0
                    && System.Array.IndexOf(candidate.Floors, floor) < 0) continue;
                if (candidate.Requires != null
                    && candidate.Requires.Any(r => !r.Evaluate(context).Passed)) continue;

                eligible.Add(candidate);
            }

            if (eligible.Count == 0) return "";

            int index = rng.NextInt(0, eligible.Count);
            return eligible[index].Id;
        }
    }
}
