using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace
{
    // A CARAVAN FAKE WEARING OUT (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5).
    //
    // Trigger: a fight completes -- not a run loss. Condition: the fake is worn
    // by a member who was FIELDED in that fight. Outcome: its counter drops by
    // one, and at zero it is gone, taken off the body in that same settlement
    // with nothing handed back to the bag.
    //
    // Bag time does not count and neither does a benched member's gear: only
    // the loadouts the caller hands in are touched, and the caller hands in
    // exactly the fielded ones. A fake works normally until it breaks, so
    // nothing else in the game reads this.
    public static class FakeWear
    {
        // How many completed fights a fake piece of gear lasts. It breaks at
        // the end of the THIRD, not the second: 3 -> 2 -> 1 -> 0.
        public const int FightsBeforeBreaking = 3;

        public const string NoRefundsLine = "No refunds.";

        public static string BreakLine(string itemName) => $"{itemName} falls apart.";

        // The provenance a fake piece of gear is stamped with. M5's shelf is
        // the caller; kept here so the countdown's start and its rule are one
        // file.
        public static Provenance NewFakeGear(string lot) => new Provenance(lot, fake: true, fightsLeft: FightsBeforeBreaking);

        // Has this copy already worn out? The Reconcile rule for a save that
        // somehow holds one (plan 3.3): the settlement removes a fake the
        // moment it reaches zero, so a zero on disk is only ever a write that
        // was interrupted, and the answer is the same removal.
        //
        // GEAR ONLY. A fake consumable carries no countdown (it is spent on
        // use), so its fightsLeft is always 0 and this must not read that as
        // broken -- the caller says whether the item is wearable.
        public static bool IsWornOut(Provenance provenance, bool isGear) =>
            isGear && provenance != null && provenance.fake && provenance.fightsLeft <= 0;

        // One completed fight on one fielded member's body. Returns the copies
        // that broke, already removed from `loadout`, in slot order.
        public static List<ItemInstance> WearOneFight(EquipmentLoadout loadout)
        {
            var broken = new List<ItemInstance>();
            if (loadout?.slots == null) return broken;

            foreach (var slot in EquipmentSlots.All)
            {
                var entry = loadout.slots.Find(e => e != null && e.slot == slot);
                if (entry == null || string.IsNullOrEmpty(entry.itemId)) continue;
                if (entry.provenance == null || !entry.provenance.fake) continue;

                entry.provenance.fightsLeft--;
                if (entry.provenance.fightsLeft > 0) continue;

                broken.Add(entry.Instance);
                loadout.Put(slot, null);
            }

            return broken;
        }
    }
}
