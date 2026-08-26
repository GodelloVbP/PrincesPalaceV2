using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // How many "Rift" modifier slots a drop rolled, and which modifiers fill
    // them. A THIRD axis alongside RarityTable's tier and plus -- see
    // RarityTable's own header for why tier and plus are rolled separately;
    // RiftTier joins them as a third independent climb rather than being
    // derived from either. Rolled PER ITEM, exactly like plus (RollPlus),
    // never once per offer-batch the way tier is: a slot count is a property
    // of the one copy sitting on the offer card, not a statement about the
    // whole set the fight is paying out.
    //
    // Pure and engine-free like the rest of Rewards: randomness arrives as
    // the same upper-bound-exclusive Func<int,int> nextIndex every other
    // table here takes. Domain cannot see UnityEngine.Random at all.
    public static class ModifierTable
    {
        // The ceiling on rolled slots. Three, not LootLadder.MaxRungs's five:
        // a 3-slot item is already "every socket is lit", and a 5-rung climb
        // would either waste two rungs no RiftTier value can ever use or
        // require inventing a 4th/5th glow tier nothing in the design calls
        // for.
        public const int MaxRungs = 3;

        // Climbs its OWN ladder using the SAME per-class step chance
        // RarityTable's tier and plus climb (LootLadder.StepChanceFor) --
        // Favor and encounter class matter the same way to "did this roll
        // affixes" as they do to "how honed is it". What changes is only the
        // ladder's height (3 instead of 5), via the maxRungs overload of
        // LootLadder.Climb rather than a duplicated draw-and-count loop.
        public static RiftTier RollRiftTier(EncounterClass encounter, int favor, Func<int, int> nextIndex)
        {
            int rungs = LootLadder.Climb(LootLadder.StepChanceFor(encounter, favor), MaxRungs, nextIndex);
            return (RiftTier)rungs;
        }

        // Picks `slotCount` DISTINCT ids out of `pool`, in the order rolled.
        // Removal-based, same technique ItemOfferTable.Choose already uses
        // for "no repeats" -- draw an index into what remains, take it out,
        // shrink the pool -- rather than a reject-and-retry loop, which would
        // make the number of draws (and so a seeded run's reproducibility)
        // depend on how often the loop got unlucky.
        //
        // Returns fewer than `slotCount` only when the pool itself is that
        // thin -- degrading gracefully rather than repeating an id, the same
        // posture ItemOfferTable.Choose takes when the candidate pool runs
        // out.
        public static List<string> PickModifiers(IReadOnlyList<string> pool, int slotCount, Func<int, int> nextIndex)
        {
            if (pool == null)
            {
                return new List<string>();
            }

            // Copied rather than sampled in place: SampleWithoutReplacement
            // mutates the list it is handed, and `pool` here belongs to the
            // caller.
            var remaining = new List<string>(pool);
            return SamplingOps.SampleWithoutReplacement(remaining, slotCount, nextIndex);
        }
    }
}
