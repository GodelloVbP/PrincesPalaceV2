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
        public string ArtKey = "";
        public string Title = "";
        public string Body = "";
        public ResolvedEventChoice[] Choices = Array.Empty<ResolvedEventChoice>();

        public ResolvedEventPage()
        {
        }

        public ResolvedEventPage(string id, string artKey, string title, string body, ResolvedEventChoice[] choices)
        {
            Id = id ?? "";
            ArtKey = artKey ?? "";
            Title = title ?? "";
            Body = body ?? "";
            Choices = choices ?? Array.Empty<ResolvedEventChoice>();
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

        public ResolvedEventDefinition()
        {
        }

        public ResolvedEventDefinition(string id, int sortOrder, int[] floors, EventRequirement[] requires, ResolvedEventPage[] pages)
        {
            Id = id ?? "";
            SortOrder = sortOrder;
            Floors = floors ?? Array.Empty<int>();
            Requires = requires ?? Array.Empty<EventRequirement>();
            Pages = pages ?? Array.Empty<ResolvedEventPage>();
        }

        // The page an event opens on. Authored order, first page -- the same
        // "authoring order is the meaningful order" convention SortOrder
        // itself follows.
        public ResolvedEventPage StartPage => Pages != null && Pages.Length > 0 ? Pages[0] : null;

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
