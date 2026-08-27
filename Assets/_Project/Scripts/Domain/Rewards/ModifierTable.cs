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

        // ModifierTable's OWN step-chance table -- deliberately separate from
        // RarityTable's (LootLadder.StepChanceFor's NormalStep 0.22 /
        // EliteStep 0.30 / BossStep 0.38 / FavorPerPoint 0.006 / MaxStep
        // 0.55). Sharing that table meant "did this roll affixes" could never
        // be tuned as its own rarity band -- it just inherited whatever the
        // tier/plus roll happened to produce. The designer's ask ("1 should
        // be uncommon, 2 rare, 3 very rare, depending on favor") is a request
        // for a THIRD, independently-dialled curve, so it gets one, climbed
        // through the same LootLadder.Climb(stepChance, maxRungs, nextIndex)
        // machinery every other ladder here uses.
        //
        // WORKED DISTRIBUTION, computed from LootLadder.Climb's own documented
        // formula (P(k) = p^k*(1-p) for k < MaxRungs=3, P(3) = p^3) rather
        // than guessed at -- exact numbers, not an eyeball:
        //
        //                 favor   step    0-affix  1-affix  2-affix  3-affix
        //   Normal            0   .120     88.0%    10.56%    1.27%    0.17%
        //   Normal            4   .128     87.2%    11.16%    1.43%    0.21%
        //   Normal           10   .140     86.0%    12.04%    1.69%    0.27%
        //   Normal           20   .160     84.0%    13.44%    2.15%    0.41%
        //   Normal      55 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //   Elite             0   .150     85.0%    12.75%    1.91%    0.34%
        //   Elite             4   .158     84.2%    13.30%    2.10%    0.39%
        //   Elite            10   .170     83.0%    14.11%    2.40%    0.49%
        //   Elite            20   .190     81.0%    15.39%    2.92%    0.69%
        //   Elite       55 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //   Boss              0   .180     82.0%    14.76%    2.66%    0.58%
        //   Boss              4   .188     81.2%    15.27%    2.87%    0.66%
        //   Boss             10   .200     80.0%    16.00%    3.20%    0.80%
        //   Boss        20 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //
        // favor=55 is not an arbitrary stress value -- it is the highest
        // Favor a real save can carry: Shawn's authored 4 (characters.json,
        // the only authored princesFavor) plus the full reward track's 51
        // (RewardTrack.cs's Favor milestone at 10 plus its 23-node filler
        // mix; see that file's own FillerMix comment, which derives the same
        // 55 for the exact same reason). It is the same ceiling RarityTable's
        // header cites for LootLadder.MaxStep, because it is the actual
        // number a completed save reaches, not a round figure picked for
        // this comment.
        //
        // 1-affix reads UNCOMMON: 8-12% at favor 0 across encounter classes,
        // climbing to a still-modest ~17% only at the realistic ceiling.
        // 2-affix reads RARE: 1-2% at favor 0. 3-affix reads VERY RARE AND
        // STAYS THAT WAY: even at the favor ceiling, on the best encounter
        // class, it never exceeds 1.06% -- an order of magnitude under where
        // "uncommon" starts, so Favor fattens the tail without letting the
        // jackpot become routine.
        //
        // Elite and Boss step ahead of Normal by LESS than RarityTable's own
        // table does (0.03 apart here vs 0.08 there, a ~1.25x/1.5x spread
        // rather than RarityTable's 1.36x/1.73x). A straight scale-up of
        // RarityTable's multipliers would put Boss's 3-affix odds at favor 0
        // above 0.8%, already eating into "very rare" before Favor gets
        // involved at all -- so the gap is compressed rather than copied.
        public const float NormalStep = 0.12f;
        public const float EliteStep = 0.15f;
        public const float BossStep = 0.18f;

        // A gentler rate than RarityTable's 0.006/point, and a much lower cap
        // (0.22 vs 0.55): this ladder is only 3 rungs tall and its top rung
        // is meant to stay a genuine jackpot, so the FavorPerPoint that
        // fattens a 5-rung tail sensibly would blow straight through "very
        // rare" here in a handful of Favor points (cubic in p, not linear --
        // P(3) = p^3, so doubling p roughly triples-to-quadruples it).
        //
        // 0.22 is not an arbitrary cap either: it is the point at which all
        // three encounter classes' 3-affix odds converge to the same 1.06%,
        // the same convergence-at-cap shape LootLadder's own table already
        // has (Normal/Elite/Boss all top out at MaxStep=0.55 there too, just
        // at different Favor values). Boss reaches this cap at favor 20,
        // Elite at 35, Normal at 50 -- so most of a completed save's Favor
        // range (up to 55) sees Boss and Elite already flat, exactly the
        // "cannot fill up however generous Favor gets" property LootLadder's
        // header calls the whole point of a ladder over a table.
        public const float FavorPerPoint = 0.002f;
        public const float MaxStep = 0.22f;

        // Same switch-clamp-add-clamp wiring LootLadder.StepChanceFor uses
        // for its own tier/plus roll -- shared there rather than duplicated
        // here, with this table's own constants (deliberately separate from
        // LootLadder's, per this file's own header) passed straight through.
        public static float StepChanceFor(EncounterClass encounter, int favor) =>
            LootLadder.StepChanceFor(encounter, favor, NormalStep, EliteStep, BossStep, FavorPerPoint, MaxStep);

        // Climbs ITS OWN ladder (StepChanceFor above), not RarityTable's tier
        // and plus one -- see that table's header for why they used to be
        // shared and why that stopped being right. What still comes from
        // LootLadder is only the mechanism: the maxRungs overload of
        // LootLadder.Climb, so this ladder's draw-and-count rule and its
        // reproducibility guarantee are exactly RarityTable's, just walked to
        // a height of 3 instead of 5.
        public static RiftTier RollRiftTier(EncounterClass encounter, int favor, Func<int, int> nextIndex)
        {
            int rungs = LootLadder.Climb(StepChanceFor(encounter, favor), MaxRungs, nextIndex);
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
