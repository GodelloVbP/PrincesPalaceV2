using System.Collections.Generic;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.UiKit
{
    // One chip of the CHARACTER tab's portrait strip.
    public readonly struct StripChip
    {
        public readonly string Id;

        // A PartySeat index for a seated character, -1 otherwise.
        public readonly int Seat;

        // Set apart after the divider and dimmed.
        public readonly bool Benched;

        public StripChip(string id, int seat, bool benched)
        {
            Id = id;
            Seat = seat;
            Benched = benched;
        }
    }

    // Who the portrait strip shows, and in what order (plan C15): the seated
    // party in stage order, left to right, then the benched roster. LB/RB
    // step through every chip, and the strip pages when there are more chips
    // than fit.
    public static class CharacterStrip
    {
        // Stage order, left to right: the rear seat stands furthest from the
        // enemy, which is the left of the fight's stage.
        public static readonly int[] StageSeats = { PartySeat.Rear, PartySeat.Middle, PartySeat.Front };

        // `seats` is indexed by PartySeat (null or empty for an open seat);
        // `roster` is every character in the save's own order. A seat naming
        // an id the roster lacks is dropped. With nobody seated the roster
        // reads as the party, the way SaveData.ActiveSquad falls back, so a
        // save with no selection shows nobody dimmed.
        public static IReadOnlyList<StripChip> Order(IReadOnlyList<string> seats, IReadOnlyList<string> roster)
        {
            var chips = new List<StripChip>();
            var seated = new HashSet<string>();
            var known = new HashSet<string>();
            if (roster != null)
            {
                foreach (var id in roster)
                {
                    if (!string.IsNullOrEmpty(id)) known.Add(id);
                }
            }

            foreach (int seat in StageSeats)
            {
                string id = seats != null && seat < seats.Count ? seats[seat] : null;
                if (string.IsNullOrEmpty(id) || !known.Contains(id) || !seated.Add(id)) continue;
                chips.Add(new StripChip(id, seat, benched: false));
            }

            bool nobodySeated = chips.Count == 0;
            if (roster == null) return chips;

            foreach (var id in roster)
            {
                if (string.IsNullOrEmpty(id) || seated.Contains(id)) continue;
                seated.Add(id);
                chips.Add(new StripChip(id, -1, benched: !nobodySeated));
            }

            return chips;
        }

        // The next chip in `direction`, wrapping at both ends.
        public static int Step(int index, int count, int direction)
        {
            if (count <= 0) return 0;
            return ((index + direction) % count + count) % count;
        }

        // The first chip the strip draws when `selected` must be on screen and
        // `capacity` chips fit: the strip turns a whole page at a time, and
        // the last page is kept full rather than showing a lone chip.
        public static int WindowStart(int selected, int count, int capacity)
        {
            if (capacity <= 0 || count <= capacity) return 0;

            int start = selected / capacity * capacity;
            int lastFull = count - capacity;
            return start > lastFull ? lastFull : start;
        }

        // Where the divider falls inside a window: the position of the first
        // benched chip, or -1 when the window holds none or starts benched.
        public static int DividerIndex(IReadOnlyList<StripChip> chips, int start, int shown)
        {
            for (int i = 0; i < shown; i++)
            {
                int at = start + i;
                if (at >= chips.Count) break;
                if (!chips[at].Benched) continue;
                return i == 0 && start > 0 && chips[at - 1].Benched ? -1 : i;
            }

            return -1;
        }
    }
}
