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
        [ContentDoc("Which EventEffectKind this is: gold, healPercent, damagePercent, exp, item, counter, relic, princesFavor, fillSpecialPool, fight, finish, shelf or takeShelf.")]
        public string kind = "";
        [ContentDoc("The amount this effect changes: gold (+ gain/- spend), heal/damage percent (1-100), exp, a counter delta, or princesFavor's run-long bonus (> 0). fillSpecialPool takes exactly 1; relic ignores it; fight, finish and shelf refuse it; takeShelf reads it as how many unsold cards are lost first (0 or more).")]
        public int amount;
        [ContentDoc("The item id granted; required by item.")]
        public string item = "";
        [ContentDoc("The counter id this effect changes; required by counter.")]
        public string counter = "";
        [ContentDoc("Optional on healPercent and exp: that one character id instead of the whole squad. healPercent never revives (a member at 0 HP stays at 0); exp goes to that member alone rather than being split. Refused on any other kind.")]
        public string character = "";
        [ContentDoc("The relic id (relics.json) added to the run; required by relic. Already held is a no-op.")]
        public string relic = "";
        [ContentDoc("fight only, required: the id of one of this event's own fights[] to start. Allowed only in an outcome's effects, at most one per outcome, and that outcome's goTo must be empty -- the fight's result outcome decides where the event goes next.")]
        public string fight = "";
        [ContentDoc("shelf and takeShelf only, required: the id of one of this event's own shelves[]. shelf opens it (the stock is rolled on the first shelf or takeShelf of the run and kept until finish); takeShelf hands the party its unsold cards, amount of them lost first, fakes staying fake.")]
        public string shelf = "";
        [ContentDoc("shelf only: when true this visit marks the shelf's fakes, and the mark stays with the stock for every later visit and reload. Refused on any other kind.")]
        public bool reveal;
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
        [ContentDoc("The result text shown after this outcome is chosen, in the body's place; capped at EventEntryResolver.MaxBodyLength characters like the body, or at MaxLineLength when it plays on the dialogue stage (the choice's page or the goTo page has lines).")]
        public string result = "";
        [ContentDoc("The next page's id, or the literal 'Leave' (case-insensitive) to close the event. Must be empty on an outcome that carries a fight effect, and only there: the fight's own onDefeated/onSurvived/onFell outcome goes on from it.")]
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
        [ContentDoc("The character id (characters.json) or event speaker id (the event's speakers[]) this row places; must speak on this page.")]
        public string character = "";
        [ContentDoc("Which screen edge the bust stands against: left or right (case-insensitive). A right-side bust is mirrored so it faces inward.")]
        public string side = "";
    }

    // One row of a page's `lines` array.
    [Serializable]
    public class RawEventLine
    {
        [ContentDoc("Who says this: a character id (characters.json), one of this event's own speakers[] ids, or the literal 'narration' (case-insensitive) for an unvoiced line with no bust and no name plate.")]
        public string speaker = "";
        [ContentDoc("The speaker's face for this line. A party character takes a DialogueExpression, empty meaning neutral; an event speaker takes one of its own declared expressions, empty meaning the first it declares. Refused on narration.")]
        public string expression = "";
        [ContentDoc("The line's text; at most EventEntryResolver.MaxLineLength characters, tags included. The only markup allowed is <i>...</i> (lowercase, balanced, not nested); any other tag, <b> included, is refused.")]
        public string text = "";
    }

    // One event, exactly as typed into events.json.
    [Serializable]
    public class RawEventEntry
    {
        [ContentDoc("Stable identifier; persisted in the run's eventsSeen list so an event shows at most once per run (a mayReturn event: until a finish effect applies).")]
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

        // ---- Returning events, fights, speakers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.5) ----

        [ContentDoc("When true, opening this event does not mark it seen: it stays eligible for every later Event node this run until a finish effect applies. False (every event before the Bell) marks it seen the moment it opens. A finish effect on an event without mayReturn is refused.")]
        public bool mayReturn;
        [ContentDoc("This event's own speakers: people who belong to the event rather than the party (a merchant, say). A line or cast entry may name one by id; they are always present at their own event.")]
        public RawEventSpeaker[] speakers = Array.Empty<RawEventSpeaker>();
        [ContentDoc("Fights this event can start, each named by a fight effect in some outcome. Its result (every enemy down, the round limit reached, or the party down) picks onDefeated, onSurvived or onFell, which apply like any outcome.")]
        public RawEventFight[] fights = Array.Empty<RawEventFight>();
        [ContentDoc("Merchant shelves this event can open (a shelf effect) or hand over (a takeShelf effect). A shelf is the room shop's own roll and buying on a stock that belongs to the event, not the node: rolled once per run, kept across Walk on and later visits, ended by finish.")]
        public RawEventShelf[] shelves = Array.Empty<RawEventShelf>();
    }

    // One row of an event's `shelves` array: a merchant's stock recipe
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.4, Stage B).
    [Serializable]
    public class RawEventShelf
    {
        [ContentDoc("Stable id within this event, named by a shelf or takeShelf effect.")]
        public string id = "";
        [ContentDoc("Every card's price as a percent of the room shop's price for the same card, rounded half away from zero, never below 1 gold. 1-100.")]
        public int priceFactorPercent = 100;
        [ContentDoc("How many cards are fake: max(1, round(cards / fakeShare)), so 3 is a third (1, 1, 2, 2 of 3, 4, 5, 6 cards). 0 means none. A fake looks and sells like the genuine card; fake gear falls apart after 3 fights worn, a fake consumable does nothing when used.")]
        public int fakeShare;
        [ContentDoc("Which room-shop shelves this stock rolls, each at most once: 'gear' (the room shop's gear roll: same candidates, tier band and affixes). Books and relics are refused -- they carry no item instance, so a fake could not apply.")]
        public string[] sections = Array.Empty<string>();
        [ContentDoc("How many consumable cards (items.json consumables, drawn without repeats) sit beside the sections' cards; 0 up to ShopStock.ConsumableCount (3).")]
        public int consumableCount;
        [ContentDoc("The shelf screen's title in place of the room shop's SHOP, capped like a page title (EventEntryResolver.MaxTitleLength). Empty shows the keeper's name, else SHOP.")]
        public string title = "";
        [ContentDoc("One of this event's own speakers: who keeps the shelf. The shelf screen shows their bust (first declared expression), name and epithet in the panel the room shop gives its relics. Empty leaves that panel with the title and the fakes note only.")]
        public string keeper = "";
    }

    // One row of an event's `speakers` array: a bust on the dialogue stage
    // that is not a party character.
    [Serializable]
    public class RawEventSpeaker
    {
        [ContentDoc("Stable id within this event, named by a line's speaker or a cast entry. Refused when it is a character id in characters.json or 'narration'.")]
        public string id = "";
        [ContentDoc("The name plate's name; required.")]
        public string name = "";
        [ContentDoc("The name plate's second line; optional, capped at CharacterEntryResolver.MaxEpithetLength characters like a character's.")]
        public string epithet = "";
        [ContentDoc("Resources-relative folder of this speaker's busts, one PNG per declared expression (<bustPath>/<expression>), the same shape as a character's dialogueBustPath. Empty shows the name plate and text with no bust.")]
        public string bustPath = "";
        [ContentDoc("The expressions this speaker has, lowercase letters, digits and underscores (they are bust file names). A line naming one not listed is refused; a line with none takes the first. Empty declares just 'neutral'.")]
        public string[] expressions = Array.Empty<string>();
    }

    // One row of an event's `fights` array.
    [Serializable]
    public class RawEventFight
    {
        [ContentDoc("Stable id within this event, named by a fight effect.")]
        public string id = "";
        [ContentDoc("Enemy ids (enemies.json, active) in stage order; at least one, and their slotSpans may not add up past the stage's three slots.")]
        public string[] enemies = Array.Empty<string>();
        [ContentDoc("True fights at the elite class (payout multiplier and default backdrop); false at the normal class.")]
        public bool elite;
        [ContentDoc("Character ids who fight, overriding the normal fieldable squad; empty means the normal squad. Members not listed sit out untouched. The choice starting it should require one of them alive.")]
        public string[] party = Array.Empty<string>();
        [ContentDoc("0 means no round limit. Above 0, reaching round surviveRounds + 1 with someone standing ends the fight as Survived; onSurvived is then required.")]
        public int surviveRounds;
        [ContentDoc("The round counter's word ('Toll'), capped at EventEntryResolver.MaxRoundLabelLength characters. Only read with a round limit, so refused without one; empty shows the default.")]
        public string roundLabel = "";
        [ContentDoc("What losing does, one of EventFightLoss: endRun (empty; the run ends, as in a room) or wake (the run continues and the fallen fighters stand at 1 HP). onFell is refused on endRun and required on wake.")]
        public string onLoss = "";
        [ContentDoc("True pays out like a room fight (gold, spell drop, the Reckoning); false pays nothing and ends on Continue.")]
        public bool pays = true;
        [ContentDoc("The fight's backdrop, Assets-relative with its extension, baked at scene build. Empty uses the class's own. Filed under Assets/_Project/Art/Backgrounds/ or this event's own Assets/_Project/Art/Events/<event_id>/.")]
        public string backdrop = "";
        [ContentDoc("Resources-relative sound (no extension) played as each round starts. Only read with a round limit, so refused without one.")]
        public string roundSfx = "";
        [ContentDoc("Resources-relative sound (no extension) looped for the whole fight; empty plays none.")]
        public string ambience = "";
        [ContentDoc("An image laid over the stage that steps from fromScale to toScale across the round limit; see EventRoundOverlay. Only read with a round limit, so a path without one is refused.")]
        public EventRoundOverlay roundOverlay = new EventRoundOverlay();
        [ContentDoc("The outcome when every enemy is down; required. An outcome row without requires (refused here), whose goTo is a page or Leave.")]
        public RawEventOutcome onDefeated = new RawEventOutcome();
        [ContentDoc("The outcome when the round limit is reached with someone standing; required when surviveRounds > 0 and refused otherwise.")]
        public RawEventOutcome onSurvived = new RawEventOutcome();
        [ContentDoc("The outcome when the fighters fall; required on wake, refused on endRun (the run is over).")]
        public RawEventOutcome onFell = new RawEventOutcome();
    }

    // A fight's round overlay. Not a Raw* type: it is a block inside a fight
    // row, and ArtPathConvention keys its path as "roundOverlay.path".
    [Serializable]
    public class EventRoundOverlay
    {
        [ContentDoc("The overlay image, Assets-relative with its extension, baked at scene build; filed in this event's own Assets/_Project/Art/Events/<event_id>/. Empty means no overlay.")]
        public string path = "";
        [ContentDoc("The overlay's scale at the first round; above 0.")]
        public float fromScale = 1f;
        [ContentDoc("The overlay's scale at the last round of the limit; above 0.")]
        public float toScale = 1f;
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawEventFile
    {
        public RawEventEntry[] events = Array.Empty<RawEventEntry>();
    }
}
