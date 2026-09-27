using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // Which characters are GUARANTEED to be in the squad on each page of an
    // event (docs/PLAN_DIALOGUE_STAGE.md contract 4) -- what the content
    // build checks every non-narration speaker against, so a line can never
    // play for somebody who is not there.
    //
    // A forward dataflow over the page graph. The start page gets the
    // event-level requires. Each edge page -> choice -> outcome -> goTo
    // carries the source page's set plus whatever that choice's and that
    // outcome's requires guarantee, and a page's set is the INTERSECTION over
    // every edge into it: a speaker is only safe if every route in put them
    // there. Loops iterate to a fixpoint; sets only ever shrink once seeded,
    // so the walk terminates.
    //
    // PRESENCE IS SQUAD MEMBERSHIP ONLY. A knocked-out member may speak
    // (owner, 2026-09-25), so a future `alive` requirement guarantees
    // nothing extra here. Nothing an event does changes the squad
    // mid-event, which is what lets a guarantee carry across pages.
    //
    // Negative knowledge ("the earlier outcome failed, so sheep is absent")
    // is deliberately not used: it can only take speakers away, and the
    // check only asks who is certainly present.
    //
    // EVENT SPEAKERS AND FIGHTS (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.2,
    // 1.3). An event's own speakers are seeded into the start page's set, so
    // every page reached from it carries them: they are always present at
    // their own event. An outcome that starts a fight has no goTo; each of
    // that fight's result outcomes is an edge FROM THE LAUNCHING PAGE, with
    // the launching choice's and outcome's guarantees. A fight does not
    // change squad membership (an override party benches, never removes), so
    // nothing is taken away across it.
    public static class EventCastPresence
    {
        // Pages unreachable from the start page have no entry.
        public static Dictionary<string, HashSet<string>> GuaranteedByPage(ResolvedEventDefinition evt)
        {
            var sets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var start = evt?.StartPage;
            if (start == null) return sets;

            var seeded = Guaranteed(evt.Requires);
            foreach (var speaker in evt.Speakers ?? Array.Empty<ResolvedEventSpeaker>())
            {
                if (speaker != null && !string.IsNullOrEmpty(speaker.Id)) seeded.Add(speaker.Id);
            }

            sets[start.Id] = seeded;

            var queue = new Queue<string>();
            var queued = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(start.Id);
            queued.Add(start.Id);

            while (queue.Count > 0)
            {
                string pageId = queue.Dequeue();
                queued.Remove(pageId);

                var page = evt.PageById(pageId);
                if (page == null) continue;

                var here = sets[pageId];
                foreach (var choice in page.Choices ?? Array.Empty<ResolvedEventChoice>())
                {
                    if (choice == null) continue;

                    var viaChoice = new HashSet<string>(here, StringComparer.Ordinal);
                    viaChoice.UnionWith(Guaranteed(choice.Requires));

                    foreach (var outcome in choice.Outcomes ?? Array.Empty<ResolvedEventOutcome>())
                    {
                        if (outcome == null) continue;

                        var edge = new HashSet<string>(viaChoice, StringComparer.Ordinal);
                        edge.UnionWith(Guaranteed(outcome.Requires));

                        var fight = evt.FightById(outcome.FightId);
                        if (fight != null)
                        {
                            foreach (var result in fight.Outcomes())
                            {
                                var viaResult = new HashSet<string>(edge, StringComparer.Ordinal);
                                viaResult.UnionWith(Guaranteed(result.Requires));
                                Flow(result, viaResult, sets, queue, queued);
                            }

                            continue;
                        }

                        Flow(outcome, edge, sets, queue, queued);
                    }
                }
            }

            return sets;
        }

        // One edge into `outcome.GoTo` carrying `edge`: the first edge seeds
        // the page's set, every later one intersects it.
        private static void Flow(ResolvedEventOutcome outcome, HashSet<string> edge,
            Dictionary<string, HashSet<string>> sets, Queue<string> queue, HashSet<string> queued)
        {
            if (outcome.IsLeave || string.IsNullOrEmpty(outcome.GoTo)) return;

            bool changed;
            if (!sets.TryGetValue(outcome.GoTo, out var existing))
            {
                sets[outcome.GoTo] = edge;
                changed = true;
            }
            else
            {
                int before = existing.Count;
                existing.IntersectWith(edge);
                changed = existing.Count != before;
            }

            if (changed && queued.Add(outcome.GoTo)) queue.Enqueue(outcome.GoTo);
        }

        // The characters a list of requirements, all passing, puts in the
        // squad: a named inParty, memberLevel or ability row (each passes only
        // when that character is in the squad -- EventRequirement.Passes,
        // `namedInSquad`). An unnamed memberLevel/ability row is satisfied by
        // whoever qualifies, so it guarantees nobody in particular.
        public static HashSet<string> Guaranteed(IEnumerable<EventRequirement> requires)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            if (requires == null) return present;

            foreach (var requirement in requires)
            {
                if (requirement == null || string.IsNullOrEmpty(requirement.CharacterId)) continue;

                switch (requirement.Kind)
                {
                    case EventRequirementKind.InParty:
                    case EventRequirementKind.MemberLevel:
                    case EventRequirementKind.Ability:
                        present.Add(requirement.CharacterId);
                        break;
                }
            }

            return present;
        }
    }
}
