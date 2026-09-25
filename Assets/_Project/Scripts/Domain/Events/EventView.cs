using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

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
    //
    // HasLines picks the presentation (docs/PLAN_DIALOGUE_STAGE.md): a page
    // with dialogue lines plays on the stage, one without shows exactly as
    // before (contract 14). Gated on the lines, never on BackdropKey -- every
    // page carries a backdrop, defaulted at content build. The concluded
    // state has no page and so no lines; it plays on the stage when the
    // EVENT has lines on any page (EventHasLines, D4), so a resume from a
    // save paints what the live pick painted. Staged is that one rule.
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
        public readonly string BackdropKey;
        public readonly IReadOnlyList<EventLineView> Lines;

        // ResolvedEventDefinition.HasAnyLines for the open event.
        public readonly bool EventHasLines;

        public bool HasLines => Lines.Count > 0;

        // Stage or legacy layout: this page has lines, or the event concluded
        // and has lines somewhere.
        public bool Staged => HasLines || (Concluded && EventHasLines);

        public EventView(string eventId, string pageId, string artKey, string title, string body,
            bool concluded, string resultText, string effectsLine, IReadOnlyList<EventChoiceView> choices,
            string backdropKey = "", IReadOnlyList<EventLineView> lines = null, bool eventHasLines = false)
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
            BackdropKey = backdropKey ?? "";
            Lines = lines ?? Array.Empty<EventLineView>();
            EventHasLines = eventHasLines || Lines.Count > 0;
        }
    }

    // ONE DIALOGUE LINE AS THE STAGE DRAWS IT: the resolved line joined to
    // its speaker's character record, so the panel never goes looking in
    // content for a name or a bust folder. Narration carries no speaker,
    // and its name, epithet and folder are empty.
    public sealed class EventLineView
    {
        public readonly string SpeakerId;
        public readonly bool IsNarration;

        // The character's display name; the bare id when content no longer
        // has that character, so the plate still says who is talking rather
        // than going blank (contract 15's posture).
        public readonly string SpeakerName;

        // Empty for every character today -- the plate hides the line.
        public readonly string Epithet;

        // ResolvedCharacter.DialogueBustPath, the Resources folder the
        // expression PNGs sit in. Empty means no bust at all.
        public readonly string BustFolder;

        // The bust file name the line asks for ("happy"); the stage walks
        // DialogueBust.Fallbacks from here.
        public readonly string Expression;
        public readonly DialogueSide Side;
        public readonly string Text;

        public EventLineView(string speakerId, bool isNarration, string speakerName, string epithet,
            string bustFolder, string expression, DialogueSide side, string text)
        {
            SpeakerId = speakerId ?? "";
            IsNarration = isNarration;
            SpeakerName = speakerName ?? "";
            Epithet = epithet ?? "";
            BustFolder = bustFolder ?? "";
            Expression = string.IsNullOrEmpty(expression) ? DialogueBust.Neutral : expression;
            Side = side;
            Text = text ?? "";
        }

        // `character` may be null: a narration line, or a speaker content
        // dropped after the save was written.
        public static EventLineView From(ResolvedEventLine line, ResolvedCharacter character)
        {
            if (line == null) return null;

            if (line.IsNarration)
            {
                return new EventLineView("", true, "", "", "", DialogueBust.Neutral, line.Side, line.Text);
            }

            string name = character != null && !string.IsNullOrEmpty(character.DisplayName)
                ? character.DisplayName
                : line.SpeakerId;

            return new EventLineView(line.SpeakerId, false, name, character?.Epithet, character?.DialogueBustPath,
                DialogueBust.FileNameOf(line.Expression), line.Side, line.Text);
        }

        // Every line of a page, in order; empty for a page without lines
        // (or no page). `characterById` is the caller's content lookup.
        public static IReadOnlyList<EventLineView> ListFor(ResolvedEventPage page, Func<string, ResolvedCharacter> characterById)
        {
            if (page == null || !page.HasLines) return Array.Empty<EventLineView>();

            var lines = new List<EventLineView>(page.Lines.Length);
            foreach (var line in page.Lines)
            {
                if (line == null) continue;
                var character = line.IsNarration || characterById == null ? null : characterById(line.SpeakerId);
                lines.Add(From(line, character));
            }

            return lines;
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

        // The pick named a page that is no longer the run's current one: a
        // second press that landed before the repaint the first one caused.
        // Nothing happened; the page the first press opened stays.
        StalePage,
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
