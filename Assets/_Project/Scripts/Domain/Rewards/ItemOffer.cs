using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Rewards
{
    // One item on the end-of-combat "choose one of three" screen.
    //
    // Carries both axes. TIER is which item is being offered and is the one
    // this table scales by depth; PLUS is how honed that particular copy is,
    // rolled separately and independently (see RarityTable.RollPlus) because
    // it is the long-tail excitement rather than the progression.
    public readonly struct ItemOffer
    {
        public readonly string ItemId;
        public readonly int Tier;
        public readonly int Plus;

        public ItemOffer(string itemId, int tier, int plus = 0)
        {
            ItemId = itemId;
            Tier = tier;
            Plus = plus;
        }

        // The same offer at a different plus. Used by the reward roll, which
        // picks WHICH item from the candidate pool and HOW HONED it is in two
        // separate steps.
        public ItemOffer WithPlus(int plus)
        {
            return new ItemOffer(ItemId, Tier, plus);
        }
    }

    // Picks the three items a fight offers, scaled to how deep the run is.
    //
    // Pure and engine-free: it is handed a candidate list of (id, tier) and a
    // depth, and returns three. Nothing here knows what an ItemDefinition is,
    // which is what lets the whole rule be unit-tested without a scene.
    public static class ItemOfferTable
    {
        // How many the player chooses between. Named rather than inline
        // because the offer screen builds exactly this many slots and the two
        // must agree.
        public const int OfferCount = 3;

        // How far either side of the target tier an offer may sit. A little
        // spread stops every choice at a given depth being three variants of
        // the same power, which is a choice in name only.
        public const int TierSpread = 1;

        // Chooses the offers.
        //
        // Takes the TARGET TIER rather than a floor. Deciding what a depth is
        // worth used to live here as `floor - 1`, which made this the only
        // floor-sensitive system in the game and left no room for a boss to
        // be worth more than the room next door. RarityTable owns that
        // decision now — it knows what killed you and how deep you are — and
        // this is back to the one job its name claims: picking which items,
        // given a target.
        //
        // `nextIndex` is the caller's randomness (an upper-bound-exclusive
        // Random.Range, or a seeded stand-in), injected rather than taken
        // from UnityEngine.Random so the selection is deterministic under
        // test. Domain cannot see UnityEngine at all.
        //
        // Returns fewer than OfferCount only when the candidate pool is
        // genuinely smaller. Callers must handle that rather than assuming
        // three, since a set could be removed from content at any time.
        public static List<ItemOffer> Choose(
            IReadOnlyList<ItemOffer> candidates, int targetTier, int maxTier, Func<int, int> nextIndex)
        {
            var chosen = new List<ItemOffer>();
            if (candidates == null || candidates.Count == 0 || nextIndex == null)
            {
                return chosen;
            }

            int target = targetTier < 0 ? 0 : targetTier;

            // Widen the band until enough distinct items are in range. A
            // narrow band at an awkward depth would otherwise return one
            // offer and silently make the screen a non-choice; widening
            // degrades the scaling gently instead of the offer count.
            for (int spread = TierSpread; chosen.Count < OfferCount; spread++)
            {
                var pool = candidates
                    .Where(c => Math.Abs(c.Tier - target) <= spread)
                    .Where(c => chosen.All(already => already.ItemId != c.ItemId))
                    .ToList();

                if (pool.Count == 0)
                {
                    // Nothing new at any width means the pool is exhausted.
                    if (spread > maxTier + candidates.Count)
                    {
                        break;
                    }

                    continue;
                }

                while (pool.Count > 0 && chosen.Count < OfferCount)
                {
                    int index = nextIndex(pool.Count);
                    if (index < 0 || index >= pool.Count)
                    {
                        index = 0;
                    }

                    chosen.Add(pool[index]);
                    pool.RemoveAt(index);
                }
            }

            return chosen;
        }
    }
}
