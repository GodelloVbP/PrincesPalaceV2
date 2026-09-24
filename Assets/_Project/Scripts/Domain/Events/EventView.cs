using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Events
{
    // ONE CHOICE AS THE PANEL PAINTS IT. Index is the choice's AUTHORED
    // position on its page, which is what ChooseEventOption takes -- not its
    // position among the visible rows, which moves as hiddenUntilMet choices
    // appear.
    public sealed class EventChoiceView
    {
        public readonly int Index;
        public readonly string Text;
        public readonly bool Visible;
        public readonly bool Enabled;
        public readonly string LockReason;

        public EventChoiceView(int index, string text, EventChoiceState state)
        {
            Index = index;
            Text = text ?? "";
            Visible = state.Visible;
            Enabled = state.Enabled;
            LockReason = state.LockReason;
        }
    }

    // THE OPEN EVENT, read-only, for the panel (phase 3) and the bot.
    //
    // Two shapes:
    //   * On a page: Title/Body/ArtKey are the page's, Choices are its
    //     choices (every one, Visible false for a hidden one).
    //   * Concluded: a choice ended in Leave but had something to say.
    //     Choices is empty and the only thing left to do is
    //     RunOrchestrator.LeaveEvent -- the panel shows its own Leave.
    //
    // ResultText/EffectsLine are the LAST choice's outcome (contract 11) and
    // are empty before the first choice. When ResultText is non-empty the
    // panel shows it in place of Body.
    public sealed class EventView
    {
        public readonly string EventId;
        public readonly string PageId;
        public readonly string ArtKey;
        public readonly string Title;
        public readonly string Body;
        public readonly bool Concluded;
        public readonly string ResultText;
        public readonly string EffectsLine;
        public readonly IReadOnlyList<EventChoiceView> Choices;

        public EventView(string eventId, string pageId, string artKey, string title, string body,
            bool concluded, string resultText, string effectsLine, IReadOnlyList<EventChoiceView> choices)
        {
            EventId = eventId ?? "";
            PageId = pageId ?? "";
            ArtKey = artKey ?? "";
            Title = title ?? "";
            Body = body ?? "";
            Concluded = concluded;
            ResultText = resultText ?? "";
            EffectsLine = effectsLine ?? "";
            Choices = choices ?? Array.Empty<EventChoiceView>();
        }
    }

    // WHY A CHOICE DID NOTHING. Same posture as ShopRefusal: a reason, not
    // a bool, because the panel, the bot and the tests each need a
    // different amount of it.
    public enum EventRefusal
    {
        None,

        // No run, no open event, or the party is not standing on the event's
        // node. A call arriving out of order, not a player-facing refusal.
        NoEvent,

        // Outside the page's choices, or the event has concluded and has no
        // choices left (only LeaveEvent).
        BadIndex,

        // The choice's requirements (or the gold its own spend implies) do
        // not pass. The panel should never have let it be pressed.
        Locked,
    }

    public enum EventChoiceOutcome
    {
        // Applied and persisted.
        Ok,

        // Nothing happened; the pre-state is intact.
        Refused,

        // Applied in memory, the write failed -- ShopOutcome's reasoning.
        AppliedNotPersisted,
    }

    public readonly struct EventChoiceResult
    {
        public readonly EventChoiceOutcome Outcome;
        public readonly EventRefusal Reason;

        // The outcome's result text and the effects line built from what was
        // applied. Empty on a refusal.
        public readonly string ResultText;
        public readonly string EffectsLine;

        // True when this choice closed the event and cleared the room: a
        // Leave outcome with nothing to say. The panel closes on this; on
        // false it repaints from RunOrchestrator.CurrentEvent().
        public readonly bool Closed;

        private EventChoiceResult(EventChoiceOutcome outcome, EventRefusal reason,
            string resultText, string effectsLine, bool closed)
        {
            Outcome = outcome;
            Reason = reason;
            ResultText = resultText ?? "";
            EffectsLine = effectsLine ?? "";
            Closed = closed;
        }

        public static EventChoiceResult Refused(EventRefusal reason) =>
            new EventChoiceResult(EventChoiceOutcome.Refused, reason, "", "", false);

        public static EventChoiceResult Applied(bool persisted, string resultText, string effectsLine, bool closed) =>
            new EventChoiceResult(persisted ? EventChoiceOutcome.Ok : EventChoiceOutcome.AppliedNotPersisted,
                EventRefusal.None, resultText, effectsLine, closed);

        public bool WasApplied => Outcome != EventChoiceOutcome.Refused;
    }
}
