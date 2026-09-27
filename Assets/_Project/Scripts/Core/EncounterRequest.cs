using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // WHAT FIGHT IS ABOUT TO HAPPEN, AND WHAT ITS END MEANS
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.1).
    //
    // Every fight is one of these. A room's is derived from the node, exactly
    // as RunEncounter.For always derived it; an event's is read from the open
    // event's `fights` block while RunSnapshot.pendingFight names it. The one
    // producer is RunOrchestrator.CurrentEncounterRequest, and its readers are
    // the build (RunOrchestrator.BuildFight -> RunEncounter.For), the screen's
    // class (FightBootstrap) and the settlement (RunOrchestrator.SettleFight),
    // which branches on IsEventFight exactly once. Nothing else learns that
    // event fights exist, which is why the bot plays them for free.
    //
    // WHAT VARIES is only what the two uses differ in: who fights, what they
    // fight, the class, the round limit, what a loss does, whether it pays and
    // whether it clears the room. The constant half -- the adapter build,
    // carried HP, the ledger fold, the write-back, the seeded stream, combat
    // itself -- is shared code, not a field here.
    public sealed class EncounterRequest
    {
        // The node's room type. For an event fight it is the event node's
        // (RoomType.Event), which no room rule reads.
        public RoomType RoomType { get; }

        // Null for a room fight.
        public ResolvedEventFight EventFight { get; }

        // The event that started it, "" for a room fight.
        public string EventId { get; }

        private EncounterRequest(RoomType roomType, string eventId, ResolvedEventFight eventFight)
        {
            RoomType = roomType;
            EventId = eventId ?? "";
            EventFight = eventFight;
        }

        public static EncounterRequest ForRoom(RoomType roomType) =>
            new EncounterRequest(roomType, "", null);

        public static EncounterRequest ForEventFight(RoomType roomType, string eventId, ResolvedEventFight fight) =>
            new EncounterRequest(roomType, eventId, fight);

        public bool IsEventFight => EventFight != null;

        // Which backdrop and which reward class. A room reads its type, as
        // FightBootstrap.EncounterFor did; an event fight is elite or normal,
        // never a boss (the run's boss is a room).
        public EncounterClass Class
        {
            get
            {
                if (IsEventFight) return EventFight.Elite ? EncounterClass.Elite : EncounterClass.Normal;

                switch (RoomType)
                {
                    case RoomType.Boss: return EncounterClass.Boss;
                    case RoomType.EliteFight: return EncounterClass.Elite;
                    default: return EncounterClass.Normal;
                }
            }
        }

        // 0 = none, every room fight. FightSession.RoundLimit takes it before
        // Begin.
        public int RoundLimit => IsEventFight ? EventFight.SurviveRounds : 0;

        // A room loss always ends the run; an event fight's `onLoss` says.
        public bool EndsRunOnLoss => !IsEventFight || EventFight.Loss == EventFightLoss.EndRun;

        // A `wake` fight fields no second lives (plan section 2, item 5,
        // assumed): the loss is already survivable, and a charge spent where
        // nothing was at stake would be a charge taken from a fight where it
        // was.
        public bool AllowsSecondLives => EndsRunOnLoss;

        // Payout, spell drop and Reckoning. A room always pays.
        public bool Pays => !IsEventFight || EventFight.Pays;

        // The character ids who fight instead of the fieldable squad, or null
        // for the whole fieldable squad. RunEncounter intersects this with
        // EncounterRoll.FieldableParty, so an override never fields someone
        // who is down or not in the squad.
        public System.Collections.Generic.IReadOnlyCollection<string> PartyOverride =>
            IsEventFight && EventFight.OverridesParty ? EventFight.PartyIds : null;

        // The fight's own backdrop key, "" for the class default. Read by the
        // fight screen's presentation (M6).
        public string BackdropKey => IsEventFight ? EventFight.BackdropKey ?? "" : "";
    }
}
