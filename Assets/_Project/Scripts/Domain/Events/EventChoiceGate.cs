using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // What one choice looks like to the player right now (plan contract 7):
    // shown or not, pickable or not, and the caption a locked one carries.
    public readonly struct EventChoiceState
    {
        public readonly bool Visible;
        public readonly bool Enabled;

        // The first failing requirement's own caption ("Requires Shawn").
        // Empty when Enabled.
        public readonly string LockReason;

        public EventChoiceState(bool visible, bool enabled, string lockReason)
        {
            Visible = visible;
            Enabled = enabled;
            LockReason = lockReason ?? "";
        }
    }

    // THE ONE ANSWER TO "CAN THIS CHOICE BE PICKED", read by the screen that
    // paints it, by ChooseEventOption that refuses it and by the bot that
    // skips it. Three readers of one rule is why it is here and not in any of
    // them: a screen that greys a choice the orchestrator would accept (or
    // the reverse) is two rulebooks.
    public static class EventChoiceGate
    {
        public static EventChoiceState Evaluate(ResolvedEventChoice choice, IEventContext context)
        {
            if (choice == null) return new EventChoiceState(false, false, "");

            string reason = FirstFailure(Requirements(choice), context);
            bool enabled = reason == null;

            // hiddenUntilMet hides a LOCKED choice, never an open one.
            bool visible = enabled || !choice.HiddenUntilMet;

            return new EventChoiceState(visible, enabled, reason ?? "");
        }

        // The authored requirements, then the gold gate the choice's own
        // spend implies (contract 6). Authored first so the caption a player
        // reads is the one the author wrote when both fail.
        public static List<EventRequirement> Requirements(ResolvedEventChoice choice)
        {
            var all = new List<EventRequirement>();
            if (choice == null) return all;

            if (choice.Requires != null)
            {
                foreach (var requirement in choice.Requires)
                {
                    if (requirement != null) all.Add(requirement);
                }
            }

            var implied = EventRequirement.ImpliedGoldRequirement(choice.Effects);
            if (implied != null) all.Add(implied);

            return all;
        }

        private static string FirstFailure(IEnumerable<EventRequirement> requirements, IEventContext context)
        {
            foreach (var requirement in requirements)
            {
                var result = requirement.Evaluate(context);
                if (!result.Passed) return result.Reason;
            }

            return null;
        }
    }
}
