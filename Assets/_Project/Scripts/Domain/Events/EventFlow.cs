using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // What picking one choice on one page produces: the effects to apply (in
    // order), the result text to show, and where to go next.
    public readonly struct EventChoiceResolution
    {
        public readonly IReadOnlyList<EventEffect> Effects;
        public readonly string Result;
        public readonly string NextPageId;
        public readonly bool IsLeave;

        public EventChoiceResolution(IReadOnlyList<EventEffect> effects, string result, string nextPageId, bool isLeave)
        {
            Effects = effects ?? Array.Empty<EventEffect>();
            Result = result ?? "";
            NextPageId = nextPageId ?? "";
            IsLeave = isLeave;
        }
    }

    // Plan contract 8: a choice's own effects apply first, then the first
    // outcome whose requirements all pass wins.
    public static class EventFlow
    {
        public static EventChoiceResolution Resolve(ResolvedEventPage page, int choiceIndex, IEventContext context)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (page.Choices == null || choiceIndex < 0 || choiceIndex >= page.Choices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(choiceIndex),
                    choiceIndex, $"Page '{page.Id}' has no choice at index {choiceIndex}.");
            }

            var choice = page.Choices[choiceIndex];

            ResolvedEventOutcome chosen = null;
            if (choice.Outcomes != null)
            {
                chosen = choice.Outcomes.FirstOrDefault(
                    o => o.Requires == null || o.Requires.Length == 0 || o.Requires.All(r => r.Evaluate(context).Passed));
            }

            if (chosen == null)
            {
                // Reachable only for content the build should have refused --
                // EventEntryResolver requires a choice's last outcome to carry
                // no requirements, so a validated choice always has a fallback.
                throw new InvalidOperationException(
                    $"Page '{page.Id}' choice {choiceIndex} ('{choice.Text}') has no outcome whose " +
                    "requirements pass. The content build should have refused an event without an " +
                    "unconditional last outcome.");
            }

            var effects = new List<EventEffect>();
            if (choice.Effects != null) effects.AddRange(choice.Effects);
            if (chosen.Effects != null) effects.AddRange(chosen.Effects);

            return new EventChoiceResolution(effects, chosen.Result, chosen.GoTo, chosen.IsLeave);
        }
    }
}
