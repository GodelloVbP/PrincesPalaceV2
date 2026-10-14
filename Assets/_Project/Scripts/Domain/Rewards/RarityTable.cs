using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // What kind of fight paid out. The three the reward tables distinguish,
    // and deliberately not more: a Normal/Elite/Boss split is what the map
    // already generates, so nothing here needs a fourth case invented for it.
    public enum EncounterClass
    {
        Normal,
        Elite,
        Boss,
    }

    // What a fight is worth, in tiers.
    //
    // Pure and engine-free, with randomness injected as a
    // `Func<int,int> nextIndex` (upper-bound-exclusive, the same shape
    // ItemOfferTable already takes) — Domain is noEngineReferences: true and
    // cannot see UnityEngine.Random at all. That is also what makes every
    // distribution below testable rather than eyeballed.
    //
    // Tier is the progression: it climbs with depth, and a deeper run should
    // visibly hand out better objects. What else a copy carries is not rolled
    // here.
    public static class RarityTable
    {
        // How many steps of descent buy one tier.
        //
        // Sixteen — two legs, one floor pair — so the tier curve climbs at
        // HALF the rate one leg would give: at 8, FloorTier(depthStep) would
        // reach tier 2 by floor 3 and tier 4 by floor 5 (RunDepth.FloorFor
        // puts floor N at legStartStep (N-1)*8), so the ladder's own climb
        // — which only ever adds on top, never subtracts — would push the
        // FLOOR of what a fight could pay past what the design calls for at
        // that depth ("floor 3 -> tier 1-2", not "tier 2 guaranteed, usually
        // 2-3"). Doubling the divisor keeps the ladder's shape (LootLadder.Climb, per-rung step
        // chances) untouched and only slows the EXPECTATION it climbs on top
        // of. Tied to the leg length still, just two of them rather than one.
        private const int StepsPerTier = 16;

        // The expected tier at a depth. Everything below is an offset from
        // this.
        public static int FloorTier(int depthStep)
        {
            return depthStep <= 0 ? 0 : depthStep / StepsPerTier;
        }

        // The tier climb (docs/PLAN_ITEMS.md 1.2 step 5). The source's
        // tierStep is the per-rung chance at Favor 0; Favor adds
        // TierFavorPerPoint a point, capped at TierMaxStep. Favor 20 adds
        // 0.12: a normal card then climbs at least one tier 34% of the time
        // instead of 22%.
        public const float TierFavorPerPoint = 0.006f;
        public const float TierMaxStep = 0.45f;

        // Centre FloorTier(depthStep) + tierOffset; the ladder only climbs,
        // up to LootLadder.MaxRungs, and every roll caps at
        // ItemInstance.TierCap (1.6). No class floor: a boss's edge is its
        // +1 centre and its wider step, not a guaranteed tier.
        public static int RollTier(int depthStep, int tierOffset, float tierStep, int favor, Func<int, int> nextIndex)
        {
            int centre = FloorTier(depthStep) + (tierOffset < 0 ? 0 : tierOffset);
            float step = LootLadder.StepChance(tierStep, favor, TierFavorPerPoint, TierMaxStep);
            return ItemInstance.ClampTier(centre + LootLadder.Climb(step, LootLadder.MaxRungs, nextIndex));
        }

        public static int RollTier(ResolvedRewardSource source, int depthStep, int favor, Func<int, int> nextIndex) =>
            source == null ? ItemInstance.ClampTier(FloorTier(depthStep))
                : RollTier(depthStep, source.TierOffset, source.TierStep, favor, nextIndex);
    }
}
