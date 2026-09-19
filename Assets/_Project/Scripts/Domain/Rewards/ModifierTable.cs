using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
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
        //   Normal      50 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //   Elite             0   .150     85.0%    12.75%    1.91%    0.34%
        //   Elite             4   .158     84.2%    13.30%    2.10%    0.39%
        //   Elite            10   .170     83.0%    14.11%    2.40%    0.49%
        //   Elite            20   .190     81.0%    15.39%    2.92%    0.69%
        //   Elite       35 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //   Boss              0   .180     82.0%    14.76%    2.66%    0.58%
        //   Boss              4   .188     81.2%    15.27%    2.87%    0.66%
        //   Boss             10   .200     80.0%    16.00%    3.20%    0.80%
        //   Boss        20 (cap)  .220     78.0%    17.16%    3.78%    1.06%
        //
        // THE CEILING ROW IS THE STEP CAP, NOT A REACHABLE FAVOR. It used to
        // read "55 (cap)" on the claim that 55 was the highest Favor a real
        // save could carry -- Shawn's authored 4 plus 51 off the reward
        // track. THE TRACK NO LONGER GRANTS FAVOR AT ALL: TrackReward has no
        // Favor member, and ItemOfferRoll.FavorOf states the surviving rule
        // as "TWO SOURCES ONLY ... authored-plus-live, full stop".
        //
        // What a save can actually reach today is Shawn's authored 4
        // (characters.json, still the only authored princesFavor) plus the
        // single best worn Fortunate -- .Best, not a sum. Fortunate's base
        // magnitude is 2, scaled by ModifierMagnitude.Scale, so it is 6 on a
        // mid-ladder tier-5 Ordinary piece and 37 at the very top of both
        // axes (tier 10, Convergent): a realistic ceiling near 10 and an
        // absolute one of 41, both well under the old 55.
        //
        // The per-row arithmetic below is unchanged and still exact -- each
        // row is P(k) at that step chance. What moved is only which rows a
        // player can stand on, and the cap rows are now labelled with the
        // Favor at which each class first reaches MaxStep rather than with a
        // ceiling that no longer exists. Worth a designer's eye before the
        // next retune of MaxStep: at 41 Boss and Elite still reach the cap,
        // Normal (0.12 + 41 x 0.002 = 0.202) never does.
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
        // range (see the ceiling note above) sees Boss already flat, exactly the
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
        // WHICH SLOT AN EFFECT IS ALLOWED TO ROLL ON, closed over
        // ModifierEffectType exactly the way ModifierEntryResolver's own
        // UsesThreshold/IgnoresMagnitude/UsesDamageType sets are -- one line
        // per member here rather than a second field authored into
        // modifiers.json, because the split is a property of what the RULE
        // DOES (an on-hit rider only a swing can trigger vs. a passive worn
        // while standing there to take a hit), not a tag a content author
        // could get out of sync with the effect itself.
        //
        // A STAFF ROLLED WITH DEFENSIVE AFFIXES is the bug this exists to
        // close: before this split, ItemOfferRoll.ModifierPool() was ONE
        // flat list built from every modifiers.json entry, handed to
        // PickModifiers for every equippable alike -- so a caster weapon
        // could land Emberguard (fire resistance) or Wardrune (mana-to-Ward
        // on the wearer's own turn start), rules that read as armour doing
        // its job on a thing meant to deal damage. Weapons (kind ==
        // ItemKind.Weapon, staves included -- see RawWeaponEntry's own
        // header: "Sword->STR, Staff->INT, Dagger->DEX" are three families
        // of the SAME kind, not three different slot types) now draw only
        // from the OFFENSIVE half; everything else equippable (kind ==
        // ItemKind.Equipment) draws only from the DEFENSIVE half.
        //
        // OFFENSIVE reads "something a landed hit, a cast, or the wielder's
        // own aggression triggers": the elemental on-hit riders, the
        // on-hit status chances, lifesteal, kill-splash, the tempo/mana
        // rider that only arms off a swing, and the two flag members ready
        // for a future modifier of the same shape (GuaranteedFirstAction --
        // acting first is what the WIELDER of the weapon does; FlatSpeedBonus
        // -- speed is the brief's own "speed" example for the weapon list).
        //
        // Everything else is DEFENSIVE: every resistance and damage-taken
        // reduction, the two passive mana-pool members (a pool you carry,
        // not a rider a hit triggers), the mana->Ward conversion (a
        // protection effect, gated on the WEARER's own turn start rather
        // than on landing a hit), Fortunate's Favor bonus (a passive worn
        // charm, not an attack rider), and DodgeRating (Swift is read by the
        // wearer as the TARGET of an incoming swing -- see
        // ModifierEffectType.DodgeRating's own header -- so it is armour's
        // job, not a weapon's, however much "speed" it sounds like).
        private static readonly HashSet<ModifierEffectType> OffensiveEffects = new HashSet<ModifierEffectType>
        {
            ModifierEffectType.ElementalDamageOnHitPercent,
            // Never actually authored via modifiers.json -- only the reward
            // track appends it (see docs/archive/PLAN_REWARD_TRACKS.md P4), so
            // IsOffensiveModifier is never called on it in production today.
            // Classified anyway so the vocabulary stays honest if a future
            // modifier ever wants the same combat hook, and because
            // ModifierTableTests' exhaustiveness guard requires SOME answer
            // for every member of the closed enum.
            ModifierEffectType.ElementalDamagePercent,
            ModifierEffectType.FlatSpeedBonus,
            ModifierEffectType.LifestealPercent,
            ModifierEffectType.GuaranteedFirstAction,
            ModifierEffectType.OnKillSplashPercent,
            ModifierEffectType.PushBackOnHitChancePercent,
            ModifierEffectType.NextSkillManaDiscountPercent,
            ModifierEffectType.ChilledOnHitChancePercent,
            ModifierEffectType.RootChancePercent,
        };

        // True for a rule a WEAPON (staves included) may roll; false means
        // it belongs to the defensive/armour half instead. A modifier with
        // no resolved effect at all (content mid-edit, or a resolver that
        // rejected it) is treated as defensive rather than thrown -- the
        // same "degrade gracefully" posture PickModifiers itself already
        // takes on a thin pool.
        public static bool IsOffensiveModifier(ModifierEffectType type) => OffensiveEffects.Contains(type);

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
