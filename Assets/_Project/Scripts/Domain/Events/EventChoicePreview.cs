using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // THE LINE UNDER A CHOICE: what picking it would apply right now.
    //
    // Derived, never authored. It is the effects EventFlow.Resolve hands the
    // orchestrator for this pick (the choice's own, then the first passing
    // outcome's), spelled by EventEffectSummary.DescribePreview, so the row
    // and the pick read one rule and a future event gets a preview with no
    // field to fill. Outcomes are conditional on run state and never rolled,
    // so the line is exact for the state the player sees.
    //
    // A choice may opt out with hideOutcome (a surprise the player is meant
    // to take on trust). The content build refuses the two ways that flag can
    // rot: an outcome with effects the preview cannot spell, and a flag on a
    // choice that has nothing to hide (CheckAuthoring).
    public static class EventChoicePreview
    {
        // Empty for a locked or hidden-outcome choice, and for a pick that
        // changes nothing the player can read (a way out, a page turn).
        public static string For(ResolvedEventPage page, int choiceIndex, IEventContext context,
            Func<string, string> itemName)
        {
            var choice = page?.Choices != null && choiceIndex >= 0 && choiceIndex < page.Choices.Length
                ? page.Choices[choiceIndex]
                : null;
            if (choice == null || choice.HideOutcome) return "";
            if (!EventChoiceGate.Evaluate(choice, context).Enabled) return "";

            return EventEffectSummary.DescribePreview(EventFlow.Resolve(page, choiceIndex, context).Effects, itemName);
        }

        // Null when the choice's authoring is sound, else what is wrong.
        public static string CheckAuthoring(ResolvedEventChoice choice)
        {
            bool anyPreview = false;
            foreach (var effects in Branches(choice))
            {
                bool substantive = effects.Any(IsOutcome);
                bool previewed = EventEffectSummary.DescribePreview(effects, null).Length > 0;
                anyPreview |= previewed;

                if (substantive && !previewed && !choice.HideOutcome)
                {
                    return "has effects the preview line cannot spell -- give the kind a preview wording in " +
                           "EventEffectSummary, or set hideOutcome if the outcome is meant to be a surprise.";
                }
            }

            if (choice.HideOutcome && !anyPreview)
            {
                return "sets hideOutcome but has no outcome to hide -- remove the flag.";
            }

            return null;
        }

        // Counters and finish are bookkeeping: they change no state the
        // player reads, so a choice made only of them has nothing to preview.
        private static bool IsOutcome(EventEffect effect) =>
            effect != null && effect.Kind != EventEffectKind.Counter && effect.Kind != EventEffectKind.Finish;

        // Each outcome's full effect list for this choice: own effects, then
        // that outcome's.
        private static IEnumerable<List<EventEffect>> Branches(ResolvedEventChoice choice)
        {
            var own = choice.Effects ?? Array.Empty<EventEffect>();
            var outcomes = choice.Outcomes ?? Array.Empty<ResolvedEventOutcome>();
            foreach (var outcome in outcomes)
            {
                var list = new List<EventEffect>(own);
                if (outcome?.Effects != null) list.AddRange(outcome.Effects);
                yield return list;
            }
        }
    }
}
