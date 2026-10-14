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

        // The lowest tier a class may ever pay out, at any depth.
        //
        // The BOSS FLOOR OF 3 IS ABSOLUTE: a boss never drops Common, on any
        // floor, at any depth. It is the one hard guarantee in this file —
        // everything else is a distribution — because a boss handing over
        // grey loot reads as the fight having been pointless, and no amount
        // of "it was statistically unlikely" repairs that when it happens.
        public static int TierFloorFor(EncounterClass encounter)
        {
            switch (encounter)
            {
                case EncounterClass.Elite: return 1;
                case EncounterClass.Boss: return 3;
                default: return 0;
            }
        }

        // Offsets from the depth's expected tier, with weights out of 100.
        //
        // Normal is WIDE AND MOSTLY LOW, centred just below the depth: the
        // majority of fights are the run's texture rather than its rewards,
        // and a normal fight reliably matching the depth would make elites
        // and bosses pointless.
        //
        // Elite is NARROWER AND BIASED UP, centred on the depth.
        //
        // Boss is centred ABOVE the depth and carries a genuine tail — the
        // +5 at 5% is the "real chance of a big jump" the design asks for,
        // and it is the only entry in this file that can move a run's power
        // level in one payout.
        // The tier this encounter pays out at this depth, clamped into
        // [class floor, maxTier].
        //
        // maxTier is passed rather than assumed so a thin or re-authored
        // catalogue cannot be asked for a tier that does not exist — the same
        // reason ItemOfferTable takes it.
        // Favor's effect on the TIER roll -- deliberately SMALL, so a Favor
        // stack barely moves how often the tier ladder climbs an extra rung.
        public const float TierFavorPerPoint = 0.002f;
        public const float TierMaxStep = 0.45f;

        public static int RollTier(EncounterClass encounter, int depthStep, int maxTier, int favor, Func<int, int> nextIndex)
        {
            if (maxTier < 0)
            {
                return 0;
            }

            // The depth sets the EXPECTATION and the ladder sets the surprise.
            //
            // The old shape rolled an offset in [-3, +1], so the very best a
            // normal fight could ever do was one tier above depth -- which is
            // why an early run only ever produced tier 0 and 1 and read as
            // stale. The ladder only ever climbs, and can climb five, so a
            // floor-1 fight can still hand over something that changes the run.
            //
            // TierFavorPerPoint/TierMaxStep, not LootLadder.Climb's own
            // (encounter, favor) convenience overload: the tier's OWN small
            // favor dial.
            int centre = FloorTier(depthStep);
            float tierStep = LootLadder.StepChanceFor(encounter, favor,
                LootLadder.NormalStep, LootLadder.EliteStep, LootLadder.BossStep,
                TierFavorPerPoint, TierMaxStep);
            int offset = LootLadder.Climb(tierStep, LootLadder.MaxRungs, nextIndex);

            int floor = TierFloorFor(encounter);
            if (floor > maxTier)
            {
                // A catalogue too short to honour the boss floor gets the best
                // it has rather than nothing. Degrading gracefully beats
                // refusing to pay out.
                return maxTier;
            }

            int tier = centre + offset;
            if (tier < floor)
            {
                return floor;
            }

            return tier > maxTier ? maxTier : tier;
        }
    }
}
