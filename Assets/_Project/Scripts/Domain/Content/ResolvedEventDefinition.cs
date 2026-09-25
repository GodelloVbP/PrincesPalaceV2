using System;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Content
{
    // One validated event page. Pages form a small graph inside an event --
    // a choice's outcome names the next page by Id, or leaves.
    [Serializable]
    public sealed class ResolvedEventPage
    {
        public string Id = "";

        // The page's authored artPath. Named a key here because that is all
        // the runtime does with it: look it up in the table
        // ScreenRegistry.WireEvent baked from these same strings.
        public string ArtKey = "";
        public string Title = "";
        public string Body = "";
        public ResolvedEventChoice[] Choices = Array.Empty<ResolvedEventChoice>();

        // ---- Dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, phase D1) ----

        // The EFFECTIVE backdrop: the page's own when it names one, else its
        // event's (already defaulted to EventEntryResolver.DefaultBackdrop).
        // A key for the same reason ArtKey is -- baked at scene build and
        // looked up at runtime. Empty only from a hand-built fixture, which
        // contract 15 draws as solid dark.
        public string BackdropKey = "";

        // Every distinct non-narration speaker on this page, in order of
        // first appearance, with the side the build settled for them
        // (contract 5). Empty when the page has no lines.
        public ResolvedEventCastMember[] Cast = Array.Empty<ResolvedEventCastMember>();
        public ResolvedEventLine[] Lines = Array.Empty<ResolvedEventLine>();

        // Contract 14: a page without lines shows exactly as before -- body
        // at once, choices at once, no stage.
        public bool HasLines => Lines != null && Lines.Length > 0;

        public ResolvedEventPage()
        {
        }

        public ResolvedEventPage(string id, string artKey, string title, string body, ResolvedEventChoice[] choices,
            string backdropKey = "", ResolvedEventCastMember[] cast = null, ResolvedEventLine[] lines = null)
        {
            Id = id ?? "";
            ArtKey = artKey ?? "";
            Title = title ?? "";
            Body = body ?? "";
            Choices = choices ?? Array.Empty<ResolvedEventChoice>();
            BackdropKey = backdropKey ?? "";
            Cast = cast ?? Array.Empty<ResolvedEventCastMember>();
            Lines = lines ?? Array.Empty<ResolvedEventLine>();
        }
    }

    // One speaker's settled side on one page.
    [Serializable]
    public sealed class ResolvedEventCastMember
    {
        public string CharacterId = "";
        public DialogueSide Side;

        public ResolvedEventCastMember()
        {
        }

        public ResolvedEventCastMember(string characterId, DialogueSide side)
        {
            CharacterId = characterId ?? "";
            Side = side;
        }
    }

    // One validated dialogue line. Side is resolved at build (contract 5),
    // so the runtime reads it rather than recomputing the alternation.
    // Narration carries no speaker, Neutral and Left, none of which the
    // stage reads for it.
    [Serializable]
    public sealed class ResolvedEventLine
    {
        public string SpeakerId = "";
        public bool IsNarration;
        public DialogueExpression Expression;
        public DialogueSide Side;
        public string Text = "";

        public ResolvedEventLine()
        {
        }

        public ResolvedEventLine(string speakerId, bool isNarration, DialogueExpression expression, DialogueSide side, string text)
        {
            SpeakerId = speakerId ?? "";
            IsNarration = isNarration;
            Expression = expression;
            Side = side;
            Text = text ?? "";
        }
    }

    // One choice on a page. Effects apply first (always), then the first
    // Outcome whose Requires all pass wins (plan contract 8).
    [Serializable]
    public sealed class ResolvedEventChoice
    {
        public string Text = "";
        public EventRequirement[] Requires = Array.Empty<EventRequirement>();
        public bool HiddenUntilMet;
        public EventEffect[] Effects = Array.Empty<EventEffect>();
        public ResolvedEventOutcome[] Outcomes = Array.Empty<ResolvedEventOutcome>();

        public ResolvedEventChoice()
        {
        }

        public ResolvedEventChoice(string text, EventRequirement[] requires, bool hiddenUntilMet,
            EventEffect[] effects, ResolvedEventOutcome[] outcomes)
        {
            Text = text ?? "";
            Requires = requires ?? Array.Empty<EventRequirement>();
            HiddenUntilMet = hiddenUntilMet;
            Effects = effects ?? Array.Empty<EventEffect>();
            Outcomes = outcomes ?? Array.Empty<ResolvedEventOutcome>();
        }
    }

    // One branch of a choice. EventEntryResolver refuses a list whose last
    // outcome is conditional, so EventFlow.Resolve can always find one that
    // matches.
    [Serializable]
    public sealed class ResolvedEventOutcome
    {
        public EventRequirement[] Requires = Array.Empty<EventRequirement>();
        public EventEffect[] Effects = Array.Empty<EventEffect>();
        public string Result = "";

        // The next page's id. Empty (with IsLeave true) means the event
        // closes -- see plan contract 15 for why the open-event discriminator
        // is eventId, never a 0-able int.
        public string GoTo = "";
        public bool IsLeave;

        public ResolvedEventOutcome()
        {
        }

        public ResolvedEventOutcome(EventRequirement[] requires, EventEffect[] effects, string result, string goTo, bool isLeave)
        {
            Requires = requires ?? Array.Empty<EventRequirement>();
            Effects = effects ?? Array.Empty<EventEffect>();
            Result = result ?? "";
            GoTo = goTo ?? "";
            IsLeave = isLeave;
        }
    }

    // One validated event -- the shape EventDefinition (Core/Content) stores.
    [Serializable]
    public sealed class ResolvedEventDefinition
    {
        public string Id = "";
        public int SortOrder;

        // Empty means every floor (plan contract 3).
        public int[] Floors = Array.Empty<int>();
        public EventRequirement[] Requires = Array.Empty<EventRequirement>();
        public ResolvedEventPage[] Pages = Array.Empty<ResolvedEventPage>();

        // The event-level backdrop with the default already applied
        // (EventEntryResolver.DefaultBackdrop when authored empty). Each
        // page's BackdropKey is what the stage draws; this is kept so the
        // scene build can bake every key an event can show.
        public string BackdropKey = "";

        public ResolvedEventDefinition()
        {
        }

        public ResolvedEventDefinition(string id, int sortOrder, int[] floors, EventRequirement[] requires, ResolvedEventPage[] pages,
            string backdropKey = "")
        {
            Id = id ?? "";
            SortOrder = sortOrder;
            Floors = floors ?? Array.Empty<int>();
            Requires = requires ?? Array.Empty<EventRequirement>();
            Pages = pages ?? Array.Empty<ResolvedEventPage>();
            BackdropKey = backdropKey ?? "";
        }

        // The page an event opens on. Authored order, first page -- the same
        // "authoring order is the meaningful order" convention SortOrder
        // itself follows.
        public ResolvedEventPage StartPage => Pages != null && Pages.Length > 0 ? Pages[0] : null;

        // Whether any page plays on the dialogue stage. The concluded state
        // has no page of its own, so this is what decides its presentation:
        // an event with lines anywhere shows its result on the stage, one
        // without shows the legacy layout (D4; nothing persisted, so a resume
        // from a save paints the same as the live pick did).
        public bool HasAnyLines
        {
            get
            {
                if (Pages == null) return false;
                foreach (var page in Pages)
                {
                    if (page != null && page.HasLines) return true;
                }

                return false;
            }
        }

        public ResolvedEventPage PageById(string pageId)
        {
            if (Pages == null) return null;
            foreach (var page in Pages)
            {
                if (page != null && page.Id == pageId) return page;
            }

            return null;
        }
    }
}
