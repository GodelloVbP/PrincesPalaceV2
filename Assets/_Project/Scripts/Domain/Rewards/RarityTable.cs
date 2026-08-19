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
    // The two axes are rolled SEPARATELY and on purpose. Tier is the
    // progression: it climbs with depth, it is what the rarity bands read,
    // and a deeper run should visibly hand out better objects. Plus is the
    // long tail: it rolls low almost always, it does not climb with depth,
    // and it is what makes one particular copy of an ordinary item worth
    // keeping. Rolling them together would average into "everything is
    // slightly better than last floor", which is the outcome this is meant
    // to avoid.
    public static class RarityTable
    {
        // How many steps of descent buy one tier.
        //
        // Eight, which is one leg — so a player is a tier better off at
        // every elite, and the top of the ladder sits around step 80. Tied to
        // the leg length rather than to a floor number because the descent is
        // continuous (Phase 3); "floor" is cosmetic.
        private const int StepsPerTier = 8;

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
            int centre = FloorTier(depthStep);
            int offset = LootLadder.Climb(encounter, favor, nextIndex);

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

        // Plus climbs its OWN ladder, and deliberately never sees the depth.
        //
        // That independence is the point and it predates this change: a +3 is
        // exciting at step 4 and still exciting at step 40, because it never
        // became the expectation.
        public static int RollPlus(EncounterClass encounter, int favor, Func<int, int> nextIndex)
        {
            return LootLadder.Climb(encounter, favor, nextIndex);
        }


        // The rarity band a class is guaranteed never to fall below. Stated
        // in the player's vocabulary rather than in tiers, since that is how
        // the guarantee is worth describing.
        public static Rarity MinimumRarityFor(EncounterClass encounter)
        {
            return RarityBands.For(TierFloorFor(encounter));
        }




    }
}
