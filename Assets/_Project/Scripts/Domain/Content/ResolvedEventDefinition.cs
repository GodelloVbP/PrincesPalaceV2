using System;
using System.Collections.Generic;
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
    //
    // TWO SPEAKER KINDS, ONE FILE-NAME FIELD. A party character's face is a
    // DialogueExpression; an event speaker's is one of the names its own
    // speakers[] row declares, which no enum can hold. ExpressionName is the
    // bust file name for either (DialogueBust.FileNameOf for a character),
    // so a bust loader joins it with a folder and never branches on which
    // kind spoke. Expression stays Neutral on an event speaker's line.
    [Serializable]
    public sealed class ResolvedEventLine
    {
        public string SpeakerId = "";
        public bool IsNarration;
        public DialogueExpression Expression;
        public DialogueSide Side;
        public string Text = "";

        // True when SpeakerId names one of the event's own speakers
        // (ResolvedEventDefinition.SpeakerById), not a character.
        public bool IsEventSpeaker;

        // The bust file name for this line's face; empty on narration.
        public string ExpressionName = "";

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
            ExpressionName = isNarration ? "" : DialogueBust.FileNameOf(expression);
        }

        // An event speaker's line: the face is one of its declared names.
        public static ResolvedEventLine ForEventSpeaker(string speakerId, string expressionName, DialogueSide side, string text) =>
            new ResolvedEventLine(speakerId, false, DialogueExpression.Neutral, side, text)
            {
                IsEventSpeaker = true,
                ExpressionName = expressionName ?? "",
            };
    }

    // One of an event's own speakers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // 1.3): shown on the stage exactly like a party bust, always present at
    // its event, never a character.
    [Serializable]
    public sealed class ResolvedEventSpeaker
    {
        public string Id = "";
        public string Name = "";
        public string Epithet = "";

        // Resources-relative folder; DialogueBust.ResourcePath joins it with
        // an expression name. Empty means no bust art.
        public string BustPath = "";

        // Declared order; the first is the face of a line that names none.
        public string[] Expressions = Array.Empty<string>();

        public ResolvedEventSpeaker()
        {
        }

        public ResolvedEventSpeaker(string id, string name, string epithet, string bustPath, string[] expressions)
        {
            Id = id ?? "";
            Name = name ?? "";
            Epithet = epithet ?? "";
            BustPath = bustPath ?? "";
            Expressions = expressions ?? Array.Empty<string>();
        }
    }

    // What losing an event fight does (RawEventFight.onLoss). Appended,
    // never inserted: serialized as an int on the EventDefinition asset.
    public enum EventFightLoss
    {
        // The run ends, as a room fight's loss does today.
        EndRun,

        // The run continues; fielded members at 0 HP stand at 1.
        Wake,
    }

    // How an event fight ended, which picks its result outcome.
    public enum EventFightResult
    {
        Defeated,
        Survived,
        Fell,
    }

    // One validated event fight: the content half of an encounter request
    // (plan 3.1). A result that cannot happen or is not authored has no
    // outcome: OutcomeFor returns null for it. The Has* flags carry that,
    // because Unity's serializer never leaves a class-typed field null.
    [Serializable]
    public sealed class ResolvedEventFight
    {
        public string Id = "";
        public string[] EnemyIds = Array.Empty<string>();
        public bool Elite;

        // Empty means the normal fieldable squad.
        public string[] PartyIds = Array.Empty<string>();

        // 0 means no round limit.
        public int SurviveRounds;
        public string RoundLabel = "";
        public EventFightLoss Loss;
        public bool Pays = true;

        // Keys baked at scene build (backdrop, overlay) or Resources paths
        // (sounds), exactly as authored; empty means none / the class default.
        public string BackdropKey = "";
        public string RoundSfxPath = "";
        public string AmbiencePath = "";
        public string RoundOverlayKey = "";
        public float RoundOverlayFromScale = 1f;
        public float RoundOverlayToScale = 1f;

        public ResolvedEventOutcome OnDefeated = new ResolvedEventOutcome();
        public ResolvedEventOutcome OnSurvived = new ResolvedEventOutcome();
        public ResolvedEventOutcome OnFell = new ResolvedEventOutcome();
        public bool HasOnSurvived;
        public bool HasOnFell;

        public bool HasRoundLimit => SurviveRounds > 0;
        public bool OverridesParty => PartyIds != null && PartyIds.Length > 0;

        public ResolvedEventOutcome OutcomeFor(EventFightResult result)
        {
            switch (result)
            {
                case EventFightResult.Defeated: return OnDefeated;
                case EventFightResult.Survived: return HasOnSurvived ? OnSurvived : null;
                case EventFightResult.Fell: return HasOnFell ? OnFell : null;
                default: return null;
            }
        }

        // Every result outcome this fight can produce, in result order.
        public IEnumerable<ResolvedEventOutcome> Outcomes()
        {
            foreach (EventFightResult result in Enum.GetValues(typeof(EventFightResult)))
            {
                var outcome = OutcomeFor(result);
                if (outcome != null) yield return outcome;
            }
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

    // One merchant shelf's recipe (RawEventShelf), validated. The stock it
    // rolls lives on the run (RunSnapshot.shelves), never here.
    [Serializable]
    public sealed class ResolvedEventShelf
    {
        public string Id = "";
        public int PriceFactorPercent = 100;
        public int FakeShare;

        // ShopStock section ids (only ShopStock.GearSection today), in
        // authored order.
        public int[] Sections = Array.Empty<int>();
        public int ConsumableCount;

        // What the shelf screen says about who is selling: its title ("" =
        // the keeper's name, else the room shop's SHOP) and the event
        // speaker keeping it ("" = nobody named).
        public string Title = "";
        public string KeeperId = "";

        public ResolvedEventShelf()
        {
        }

        public ResolvedEventShelf(string id, int priceFactorPercent, int fakeShare, int[] sections, int consumableCount,
            string title = "", string keeperId = "")
        {
            Id = id ?? "";
            PriceFactorPercent = priceFactorPercent;
            FakeShare = fakeShare;
            Sections = sections ?? Array.Empty<int>();
            ConsumableCount = consumableCount;
            Title = title ?? "";
            KeeperId = keeperId ?? "";
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

        // The event fight this outcome starts, or "" when it starts none
        // (the resolver allows at most one, and then GoTo is empty: the
        // fight's result outcome goes on from here).
        public string FightId
        {
            get
            {
                if (Effects == null) return "";
                foreach (var effect in Effects)
                {
                    if (effect != null && effect.Kind == EventEffectKind.Fight) return effect.FightId ?? "";
                }

                return "";
            }
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

        // See RawEventEntry.mayReturn: opening does not mark it seen; only a
        // finish effect does.
        public bool MayReturn;
        public ResolvedEventSpeaker[] Speakers = Array.Empty<ResolvedEventSpeaker>();
        public ResolvedEventFight[] Fights = Array.Empty<ResolvedEventFight>();
        public ResolvedEventShelf[] Shelves = Array.Empty<ResolvedEventShelf>();

        public ResolvedEventDefinition()
        {
        }

        public ResolvedEventDefinition(string id, int sortOrder, int[] floors, EventRequirement[] requires, ResolvedEventPage[] pages,
            string backdropKey = "", bool mayReturn = false, ResolvedEventSpeaker[] speakers = null,
            ResolvedEventFight[] fights = null, ResolvedEventShelf[] shelves = null)
        {
            Id = id ?? "";
            SortOrder = sortOrder;
            Floors = floors ?? Array.Empty<int>();
            Requires = requires ?? Array.Empty<EventRequirement>();
            Pages = pages ?? Array.Empty<ResolvedEventPage>();
            BackdropKey = backdropKey ?? "";
            MayReturn = mayReturn;
            Speakers = speakers ?? Array.Empty<ResolvedEventSpeaker>();
            Fights = fights ?? Array.Empty<ResolvedEventFight>();
            Shelves = shelves ?? Array.Empty<ResolvedEventShelf>();
        }

        public ResolvedEventShelf ShelfById(string shelfId)
        {
            if (Shelves == null || string.IsNullOrEmpty(shelfId)) return null;
            foreach (var shelf in Shelves)
            {
                if (shelf != null && shelf.Id == shelfId) return shelf;
            }

            return null;
        }

        public ResolvedEventFight FightById(string fightId)
        {
            if (Fights == null || string.IsNullOrEmpty(fightId)) return null;
            foreach (var fight in Fights)
            {
                if (fight != null && fight.Id == fightId) return fight;
            }

            return null;
        }

        public ResolvedEventSpeaker SpeakerById(string speakerId)
        {
            if (Speakers == null || string.IsNullOrEmpty(speakerId)) return null;
            foreach (var speaker in Speakers)
            {
                if (speaker != null && speaker.Id == speakerId) return speaker;
            }

            return null;
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
