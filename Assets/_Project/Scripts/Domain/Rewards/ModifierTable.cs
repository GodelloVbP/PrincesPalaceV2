using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // Which modifiers an item may carry and how a set of them is drawn. How
    // many a copy rolls is a quality question (ItemQuality), not an axis of
    // its own.
    //
    // Pure and engine-free like the rest of Rewards: randomness arrives as
    // the same upper-bound-exclusive Func<int,int> nextIndex every other
    // table here takes. Domain cannot see UnityEngine.Random at all.
    public static class ModifierTable
    {
        // WHICH ITEM AN AFFIX MAY ROLL ON: EffectCatalog's Pool for its kind.
        // A property of what the rule DOES (an on-hit rider only a swing can
        // trigger, against a passive worn to take a hit), so it sits with the
        // kind rather than as a tag an author could get out of step with it.
        // Weapons (staves included) draw only Weapon kinds; everything else
        // equippable draws only Armour kinds, so a staff never rolls
        // Emberguard.
        //
        // A kind with no row, or no pool (a relic kind, a modifier the
        // resolver refused), reads as armour rather than throwing -- the same
        // degrade-gracefully posture PickModifiers takes on a thin pool.
        public static bool IsOffensiveModifier(EffectKind kind) =>
            EffectCatalog.Has(kind) && EffectCatalog.Of(kind).Pool == AffixPool.Weapon;

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
