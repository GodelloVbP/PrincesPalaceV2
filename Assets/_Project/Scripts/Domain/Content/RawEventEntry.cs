using System;

namespace PrincesPalace.Domain.Content
{
    // One requirement row, exactly as typed into an event's `requires` array
    // (event-level, choice-level or outcome-level -- the same shape serves
    // all three, per plan contract 5). Kind is matched case-insensitively
    // against EventRequirementKind by EventEntryResolver.
    [Serializable]
    public class RawEventRequirement
    {
        [ContentDoc("Which EventRequirementKind this is: inParty, memberLevel, ability, counter or gold.")]
        public string kind = "";
        [ContentDoc("The character id this requirement names; required by inParty, an optional narrowing for memberLevel/ability.")]
        public string character = "";
        [ContentDoc("Which AbilityScore this checks; required by ability.")]
        public string ability = "";
        [ContentDoc("The minimum value required; -1 means omitted. Required by memberLevel/ability/gold; optional for counter.")]
        public int min = -1;
        [ContentDoc("The maximum value allowed; -1 means omitted. Only counter reads this.")]
        public int max = -1;
        [ContentDoc("The counter id this requirement reads; required by counter.")]
        public string counter = "";
        [ContentDoc("Optional caption a locked choice shows for this row, replacing the generated one for any kind. Empty means generated (a counter's generated caption is 'Not yet' / 'No longer', never its id). On a choice row it is capped at EventEntryResolver.MaxLockReasonLength characters; event- and outcome-level rows never show it.")]
        public string reason = "";
        [ContentDoc("inParty only: when true the character must also be standing (run health above 0); a downed member fails with the caption 'Requires <name> standing'. Refused on any other kind.")]
        public bool alive;
    }

    // One effect row, exactly as typed into a choice's or outcome's `effects`
    // array. Kind is matched case-insensitively against EventEffectKind.
    [Serializable]
    public class RawEventEffect
    {
        [ContentDoc("Which EventEffectKind this is: gold, healPercent, damagePercent, exp, item, counter, relic, princesFavor or fillSpecialPool.")]
        public string kind = "";
        [ContentDoc("The amount this effect changes: gold (+ gain/- spend), heal/damage percent (1-100), exp, a counter delta, or princesFavor's run-long bonus (> 0). fillSpecialPool takes exactly 1; relic ignores it.")]
        public int amount;
        [ContentDoc("The item id granted; required by item.")]
        public string item = "";
        [ContentDoc("The counter id this effect changes; required by counter.")]
        public string counter = "";
        [ContentDoc("healPercent only, optional: heal just this character id by amount percent of their max HP. Never revives: a member at 0 HP stays at 0. Empty heals the whole squad. Refused on any other kind.")]
        public string character = "";
        [ContentDoc("The relic id (relics.json) added to the run; required by relic. Already held is a no-op.")]
        public string relic = "";
    }

    // One branch of a choice, exactly as typed into a choice's `outcomes`
    // array. The FIRST outcome whose `requires` all pass wins; the content
    // build refuses a list whose last outcome carries any requirement (plan
    // contract 8).
    [Serializable]
    public class RawEventOutcome
    {
        [ContentDoc("Requirements gating this outcome; the last outcome in a choice must have none, so a choice can never fall through with nothing to show.")]
        public RawEventRequirement[] requires = Array.Empty<RawEventRequirement>();
        [ContentDoc("Effects applied when this outcome is chosen, in addition to the choice's own effects.")]
        public RawEventEffect[] effects = Array.Empty<RawEventEffect>();
        [ContentDoc("The result text shown after this outcome is chosen, in the body's place; capped at EventEntryResolver.MaxBodyLength characters like the body.")]
        public string result = "";
        [ContentDoc("The next page's id, or the literal 'Leave' (case-insensitive) to close the event.")]
        public string goTo = "";
    }

    // One choice, exactly as typed into a page's `choices` array. Up to 4 per
    // page (plan contract 10); the content build refuses a page with no
    // unconditional choice, so the player can never be trapped (contract 7).
    [Serializable]
    public class RawEventChoice
    {
        [ContentDoc("The choice's own button text; capped at EventEntryResolver.MaxChoiceTextLength characters.")]
        public string text = "";
        [ContentDoc("Requirements gating this choice; a choice with none is always selectable.")]
        public RawEventRequirement[] requires = Array.Empty<RawEventRequirement>();
        [ContentDoc("When true, this choice is hidden entirely (not shown locked) until its requirements pass.")]
        public bool hiddenUntilMet;
        [ContentDoc("Effects applied immediately when this choice is picked, before an outcome is chosen. A gold spend here implies its own gold requirement -- do not author one by hand.")]
        public RawEventEffect[] effects = Array.Empty<RawEventEffect>();
        [ContentDoc("Ordered outcomes; the first whose requirements pass wins.")]
        public RawEventOutcome[] outcomes = Array.Empty<RawEventOutcome>();
    }

    // One page, exactly as typed into an event's `pages` array. The first
    // page is where the event opens.
    [Serializable]
    public class RawEventPage
    {
        [ContentDoc("Stable id for this page within its event; targeted by an outcome's goTo.")]
        public string id = "";
        [ContentDoc("This page's art, Assets-relative with its extension (Assets/_Project/Art/Events/<event_id>/<page_id>.png; the build refuses art under Art/Events/ that is not in its own event's folder): baked into the Map scene at build time the way item iconPath is, so a new file needs a scene rebuild. Empty, or a file that is not there, hides the image and keeps the frame. Commission at the size docs/EVENTS.md gives.")]
        public string artPath = "";
        [ContentDoc("The page's title, shown above the body; capped at EventEntryResolver.MaxTitleLength characters.")]
        public string title = "";
        [ContentDoc("The page's body text; capped at EventEntryResolver.MaxBodyLength characters.")]
        public string body = "";
        [ContentDoc("Up to 4 choices offered on this page.")]
        public RawEventChoice[] choices = Array.Empty<RawEventChoice>();

        // ---- Dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, phase D1) ----
        // A page with no lines behaves exactly as before (contract 14).

        [ContentDoc("Optional full-bleed backdrop for this page, Assets-relative with its extension, baked at scene build like artPath. Empty inherits the event's backdrop. Must be filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/.")]
        public string backdrop = "";
        [ContentDoc("Optional side overrides for this page's speakers. A speaker not listed takes a side by first appearance on the page: first distinct speaker left, second right, third left, alternating. Every entry must speak on this page, and a character may be listed once.")]
        public RawEventCastMember[] cast = Array.Empty<RawEventCastMember>();
        [ContentDoc("Dialogue lines played before the choices, in order; at most EventEntryResolver.MaxLinesPerPage. Every non-narration speaker must be guaranteed in the party at this page by requirements on the way in (see docs/EVENTS.md, Dialogue lines).")]
        public RawEventLine[] lines = Array.Empty<RawEventLine>();
    }

    // ---- Dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, phase D1) ----

    // One row of a page's `cast` array: pins a speaker to a side.
    [Serializable]
    public class RawEventCastMember
    {
        [ContentDoc("The character id (characters.json) this row places; must speak on this page.")]
        public string character = "";
        [ContentDoc("Which screen edge the bust stands against: left or right (case-insensitive). A right-side bust is mirrored so it faces inward.")]
        public string side = "";
    }

    // One row of a page's `lines` array.
    [Serializable]
    public class RawEventLine
    {
        [ContentDoc("Who says this: a character id (characters.json), or the literal 'narration' (case-insensitive) for an unvoiced line with no bust and no name plate.")]
        public string speaker = "";
        [ContentDoc("The speaker's face for this line; empty means neutral. Refused on narration.")]
        public string expression = "";
        [ContentDoc("The line's text; at most EventEntryResolver.MaxLineLength characters, tags included. The only markup allowed is <i>...</i> (lowercase, balanced, not nested); any other tag, <b> included, is refused.")]
        public string text = "";
    }

    // One event, exactly as typed into events.json.
    [Serializable]
    public class RawEventEntry
    {
        [ContentDoc("Stable identifier; persisted in the run's eventsSeen list so an event shows at most once per run.")]
        public string id = "";
        [ContentDoc("Floor numbers this event may appear on; empty means every floor.")]
        public int[] floors = Array.Empty<int>();
        [ContentDoc("Event-level requirements; all must pass for this event to be eligible to be rolled at all.")]
        public RawEventRequirement[] requires = Array.Empty<RawEventRequirement>();
        [ContentDoc("The event's page graph; the first page is where the event opens.")]
        public RawEventPage[] pages = Array.Empty<RawEventPage>();

        // ---- Dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, phase D1) ----

        [ContentDoc("The dialogue stage's full-bleed backdrop, Assets-relative with its extension, baked at scene build like a page's artPath; a page's own backdrop overrides it. Empty means EventEntryResolver.DefaultBackdrop (Assets/_Project/Art/Backgrounds/Dungeon.png). Must be filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/.")]
        public string backdrop = "";
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawEventFile
    {
        public RawEventEntry[] events = Array.Empty<RawEventEntry>();
    }
}
