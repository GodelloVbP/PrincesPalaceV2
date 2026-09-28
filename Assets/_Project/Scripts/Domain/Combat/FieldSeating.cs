using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat
{
    // What a placement request came to (CombatEncounter.CanPlaceAt/PlaceAt).
    // One enum for every caller -- Move, Palace Passage, Reposition -- so each
    // phrases its own refusal off the same answer.
    public enum PlaceOutcome
    {
        Placed,

        // The member is dead, absent, or not on the party side.
        NotOnTheField,

        // Out of 0..SeatsPerSide-1, or the seat the member already holds.
        NoSuchSeat,

        MemberRooted,
        OccupantRooted,
    }

    // HOW A SEAT IS COUNTED OFF A PARTY FIELD LIST -- the one counting rule,
    // shared by the combat rules (CombatEncounter.SeatOf, "holds a place" =
    // alive) and the stage (FightController.DrawSide, "holds a place" = alive
    // or a corpse that has not finished fading). One rule and two predicates
    // is what keeps the drawn seat and the rule's seat from drifting apart.
    //
    // The list: members in formation order, a null per stored empty seat
    // (CombatEncounter._field). Every null and every member that holds a
    // place takes the next seat; a member that does not hold one takes none,
    // so the line behind it closes up by one.
    public static class FieldSeating
    {
        // `who`'s seat, or -1 when it holds no place (or is not in `field`).
        public static int SeatIn(IReadOnlyList<CombatantState> field, CombatantState who,
                                 Func<CombatantState, bool> holdsPlace)
        {
            if (field == null || who == null || holdsPlace == null) return -1;

            int skip = ExcessHoles(field, holdsPlace);
            int seat = 0;

            for (int i = 0; i < field.Count; i++)
            {
                var entry = field[i];

                if (entry == null)
                {
                    if (skip > 0) { skip--; continue; }
                    seat++;
                    continue;
                }

                bool holds = holdsPlace(entry);
                if (ReferenceEquals(entry, who)) return holds ? seat : -1;
                if (holds) seat++;
            }

            return -1;
        }

        // How many stored holes there are beyond the seat count -- only after
        // a revival put a member back in front of a hole that was holding its
        // place. The frontmost surplus holes are the ones not counted.
        public static int ExcessHoles(IReadOnlyList<CombatantState> field, Func<CombatantState, bool> holdsPlace)
        {
            if (field == null) return 0;

            int holders = 0;
            int holes = 0;
            foreach (var entry in field)
            {
                if (entry == null) holes++;
                else if (holdsPlace(entry)) holders++;
            }

            int excess = holders + holes - CombatEncounter.SeatsPerSide;
            if (excess <= 0) return 0;
            return excess < holes ? excess : holes;
        }
    }
}
