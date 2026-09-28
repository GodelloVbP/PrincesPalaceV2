using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Session
{
    // WHERE EVERYBODY STOOD AT THE MOMENT A BEAT RESOLVED.
    //
    // The third thing a beat snapshots, alongside the vitals and the turn
    // queue, and it exists for exactly the same reason both of those do: one
    // command resolves the whole round in a single pass, so by the time
    // playback reaches beat 1 the live lists have already been through beats
    // 2..n. A view that read Encounter.PlayerParty while painting a beat would
    // draw the END of the round on every frame of it -- which is precisely
    // what happened to the vitals before they were recorded, and what would
    // happen to a Move the instant one existed: both figures would swap on the
    // first beat of the round and stand there while the beat that moved them
    // played out three beats later.
    //
    // COPIED, NOT REFERENCED. ToArray() at capture time, never a live list and
    // never a deferred LINQ enumeration -- CombatEncounter.PlaceAt writes
    // the party list in place, so a beat holding a reference to it would hold
    // the CURRENT order under a different name, and a deferred query would
    // evaluate against whatever the list holds when the view first asks.
    // FightSessionTests.AMovesFormationSnapshotDoesNotChangeWhenTheListMoves
    // Again is what keeps that true.
    //
    // LIST ORDER, INCLUDING THE DEAD, and this is the one place this type
    // deviates from what the plan sketched (§1.5 said "the LIVING combatants
    // of each side"). Ranks compress behind a corpse the instant it falls, so
    // a formation of the living alone has the survivors already compacted on
    // the very beat the kill lands -- and the view would slide them forward
    // while the body is still standing there at full opacity, before its own
    // death fade has so much as started. Carrying the ORDER and letting the
    // view decide who still occupies a rank (a corpse holds its rank until it
    // has faded out) is what makes "position is a per-beat function of rank"
    // and "the survivors slide only once the corpse is gone" both true at
    // once. CombatEncounter.LivingRankOf remains the authority for every
    // combat RULE; this is a presentation record and says so.
    public sealed class BeatFormation
    {
        public static readonly BeatFormation Empty =
            new BeatFormation(Array.Empty<CombatantState>(), Array.Empty<CombatantState>(),
                              Array.Empty<CombatantState>());

        private readonly CombatantState[] _party;
        private readonly CombatantState[] _partyField;
        private readonly CombatantState[] _enemies;

        private BeatFormation(CombatantState[] party, CombatantState[] partyField, CombatantState[] enemies)
        {
            _party = party;
            _partyField = partyField;
            _enemies = enemies;
        }

        public IReadOnlyList<CombatantState> Party => _party;
        public IReadOnlyList<CombatantState> Enemies => _enemies;

        // THE PARTY'S SEATS AT THIS BEAT (PLAN_BELLWETHER_KIT 3.1): the same
        // members as Party, in the same order, with a null per stored empty
        // seat (CombatEncounter.PartyField). The stage counts seats off it
        // with FieldSeating and its own "holds a place" rule (a corpse holds
        // its seat until it has faded), so a lone Shawn who stepped back is
        // drawn in the seat he stepped to, and a line closes up only once the
        // body is gone. Copied for the same reason Party is.
        public IReadOnlyList<CombatantState> PartyField => _partyField;

        // The side a combatant belongs to, asked the way every caller actually
        // asks it -- off CombatantState.IsPlayerSide rather than by picking a
        // property, so nothing downstream has to branch twice.
        public IReadOnlyList<CombatantState> SideOf(bool playerSide) => playerSide ? _party : _enemies;

        public static BeatFormation Capture(CombatEncounter encounter)
        {
            if (encounter == null) return Empty;

            return new BeatFormation(ToArray(encounter.PlayerParty), ToArray(encounter.PartyField),
                                     ToArray(encounter.Enemies));
        }

        // Hand-copied rather than System.Linq's ToArray, only because this is
        // called once per beat on a list of at most three and the loop is the
        // whole of it.
        private static CombatantState[] ToArray(IReadOnlyList<CombatantState> side)
        {
            if (side == null || side.Count == 0) return Array.Empty<CombatantState>();

            var copy = new CombatantState[side.Count];
            for (int i = 0; i < side.Count; i++) copy[i] = side[i];
            return copy;
        }
    }
}
