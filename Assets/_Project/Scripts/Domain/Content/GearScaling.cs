using System;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Content
{
    // What a piece of armour is worth, as one formula, split across TWO
    // channels -- balance redesign Phase 4 (D4).
    //
    //     Points(slot, tier) = BudgetPerSlotAtBase x SlotWeight[slot] x TierGrowth^tier
    //     ability scores = Base x SlotWeight[slot] x StyleWeight[style][score] x TierCurve(tier)   (40%)
    //     combat stats    = Points(slot, tier) x CombatStatShare x StatProfile[set][stat] x UnitCost[stat]   (60%)
    //
    // Everything an armour set grants comes out of here, and NOTHING is
    // hand-authored per piece any more. Before Phase 4, every piece authored
    // its own baseStats and topStats by hand, which meant a set was balanced
    // against the other sets only by whoever typed it -- rebalancing "torso
    // should matter less" was a hundred and ten edits spread across a JSON
    // file, and the same-tier budgets visibly drifted apart because nothing
    // tied one piece's numbers to another's.
    //
    // Now itemsets.json names TWO things per set -- styleWeights (what the
    // material is FOR, spent through the 40% ability-score channel, exactly
    // as before Phase 4) and statProfile (what it PROTECTS WITH, spent
    // through the new 60% combat-stat channel) -- and the constants below
    // decide the rest. Retuning the whole economy is editing numbers here;
    // retuning one material is editing its two authored lists in
    // itemsets.json. A piece naming baseStats/topStats is now a content-build
    // error -- see ItemSetEntryResolver.
    public static class GearScaling
    {
        // The value one average slot of a pure single-stat style grants at
        // tier 0. Every other number here is a multiplier on it, so this is
        // the volume knob for the ABILITY SCORE channel specifically (the 40%
        // half of the Phase 4 split below), not for gear as a whole.
        //
        // RECALIBRATED for balance redesign Phase 4 (D4), 4.0 -> 0.4. Before
        // Phase 4, this was the ENTIRE budget a piece spent — a torso alone
        // could carry 47 points of a pure style's ability score at tier 10.
        // Now ability scores are only 40% of a piece's worth (the other 60%
        // is CombatStatShare below, spent through a set's statProfile), and
        // D4 asks for that 40% to stay deliberately small: "a 100-weight
        // style yields ~2 score points across the full 5-piece set at tier 0,
        // ~18 at tier 10" — so gear-fed ability inflation stays a minor drip
        // and the weapon (Phase 3) remains the offensive decision. Solving
        // Base*5.0 (the five slot weights sum to 5.0) = 2.0 gives 0.4, and
        // 0.4*5.0*9.313 (TopMultiplier(10)) = 18.6 lands on the far end too.
        public const double Base = 0.4;

        // How much stronger each tier is than the one below it.
        //
        // GEOMETRIC, not linear, and that is the design rather than a detail:
        // 1.25 per tier makes tier 10 worth 9.3x tier 0, so the last floor
        // feels wild. A straight line cannot do that -- it only gets steeper,
        // it never accelerates. The cost is that the first floors are flat in
        // absolute terms (tier 0 to 1 on a torso is under a point), which is
        // what the plus axis exists to cover.
        public const double TierGrowth = 1.25;

        // WEAPONS climb a STEEPER curve than armour -- balance redesign
        // Phase 3 (D3). Armour's job is survival plus a secondary offensive
        // drip; the weapon is meant to be THE offensive decision, so a tier
        // of sword has to outrun a tier of torso. 1.35^10 is ~20.1x tier 0,
        // against armour's 9.3x -- see WeaponEntryResolver, the only reader
        // of this constant. Kept here rather than beside ItemUpgrade's own
        // constants because it is the same KIND of number as TierGrowth
        // above (a per-tier growth rate feeding CurveFraction), not a hone
        // rate.
        public const double WeaponTierGrowth = 1.35;

        // ---- Phase 4 (D4): the combat-stat budget ---------------------
        //
        // Every armour piece spends ONE budget, split 60/40 between combat
        // stats (this section) and ability scores (Base above). The split
        // and the unit costs below are TUNING PARAMETERS, not derived
        // truths -- D4 says so explicitly, and says where they live if a
        // playtest wants them to move: right here.
        //
        //     Points(slot, tier) = BudgetPerSlotAtBase x SlotWeight(slot) x TierGrowth^tier
        //     CombatBudget(slot, tier) = Points(slot, tier) x CombatStatShare
        //
        // The "10" in the plan's Points formula.
        public const double BudgetPerSlotAtBase = 10.0;

        // The 60/40 split. CombatStatShare is load-bearing (CombatBudget
        // multiplies by it below); ScoreShare is not multiplied anywhere --
        // the ability-score channel hits its 40% by Base being small, not by
        // an explicit multiply -- but it is kept here, named, so the two
        // halves of the split are visible together and still read as one
        // decision that sums to 1.0.
        public const double CombatStatShare = 0.60;
        public const double ScoreShare = 0.40;

        // Unit costs: what ONE budget point buys of a given combat stat,
        // after the 60% share is taken.
        //
        // NOT the plan's literal D4 numbers (10 / 2 / 2 / 0.5 / 0.4).
        // Those were run through the actual formula chain against the D4
        // power-band test (T5 physical EHP multiplier vs naked in [1.3,
        // 2.2]; leather's T5 turn-rate advantage <= +20%) and DID NOT hold
        // -- e.g. steel's T5 full-set EHP multiplier came out at 3.75x and
        // leather's turn-rate advantage at +76%, both far outside the
        // targets, because a 60%-shared, tier-9.3x budget is a much bigger
        // number at T5 than the literal unit costs assume. Found by running
        // the derivation, the same way D6 found enemy defenses double-
        // dipping with the asymptotic curve -- see AUDIT and the Phase 4
        // report for the worked numbers. HP/PDEF/MDEF are halved from the
        // plan's figures; Speed and ManaRegen needed a much steeper cut
        // (roughly a sixth) because the turn-rate curve is a square root --
        // small absolute Speed swings move it a lot near the baseline, and
        // Speed a set actually WEARS also has to survive being floored per
        // SLOT before it is floor-interpolated again across tiers, which
        // eats small numbers fast. See GearBudgetTests for the pinned
        // outcome and the three sets (silk, vellum, court) that still sit
        // under the 1.3 floor -- all three spend under 15% of their budget
        // on HP+PDEF combined, and no linear unit cost can lift them there
        // without also pushing the 90%-invested sets (steel) over 2.2. That
        // ceiling is a real tension in the D4 statProfile table, not a
        // tuning miss; it is reported rather than silently patched over.
        public const double HpPerBudgetPoint = 5.0;
        public const double PhysicalDefensePerBudgetPoint = 1.0;
        public const double MagicalDefensePerBudgetPoint = 1.0;
        public const double SpeedPerBudgetPoint = 0.16;
        public const double ManaRegenPerBudgetPoint = 0.13;

        // Points(slot, tier) x CombatStatShare -- a pure function of slot and
        // tier, with no set or style argument at all. That is what makes the
        // equal-budget invariant true BY CONSTRUCTION: there is no code path
        // through which two sets could spend a different Torso-tier-5
        // budget. See GearBudgetTests.
        public static double CombatBudget(EquipmentSlot slot, int tier)
        {
            return BudgetPerSlotAtBase * SlotWeight(slot) * CombatStatShare * Math.Pow(TierGrowth, tier);
        }

        // One combat stat's value at an arbitrary tier -- the combat-channel
        // sibling of AtBaseTier/AtTopTier below. `weight` is the set's
        // statProfile share for this stat (0.35 for "physicalDefense 35").
        // Callers pass tier 0 for the base end and maxTier for the top end,
        // same reasoning as AtTopTier: scaled from the UNROUNDED budget at
        // whichever tier is asked for, not from a rounded base value.
        public static int CombatStatAt(EquipmentSlot slot, double weight, double unitCost, int tier)
        {
            if (weight <= 0.0) return 0;
            return Round(CombatBudget(slot, tier) * weight * unitCost);
        }

        // A slot's share of a full set. These sum to 5.0 across the five
        // armour slots, so a complete set is always worth Base x 5 whatever
        // the mix, and the numbers read directly as percentages: torso 25%,
        // gloves 17%.
        //
        // DELIBERATELY FLAT. A steeper spread (40/20/15/13/12) makes the chest
        // the event and everything else filler -- and with three offers per
        // fight, two of every three would be a consolation prize.
        public static double SlotWeight(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Torso: return 1.25;
                case EquipmentSlot.Legs: return 1.05;
                case EquipmentSlot.Head: return 0.95;
                case EquipmentSlot.Shoes: return 0.90;
                case EquipmentSlot.Gloves: return 0.85;

                // Not armour slots. A necklace is not part of a material set
                // and a weapon comes from weapons.json, which has its own
                // scaling; both return zero so a set that names one by mistake
                // grants nothing and trips the resolver's "grants nothing"
                // check rather than silently inventing a value.
                default: return 0.0;
            }
        }

        // What tier `maxTier` multiplies tier 0 by. 9.31 at the default 10.
        public static double TopMultiplier(int maxTier)
        {
            return TopMultiplier(maxTier, TierGrowth);
        }

        // The same, on an arbitrary growth rate -- WeaponEntryResolver's own
        // attack curve, at WeaponTierGrowth, without disturbing what every
        // armour caller above already gets from the no-arg overload.
        public static double TopMultiplier(int maxTier, double growth)
        {
            return maxTier <= 0 ? 1.0 : Math.Pow(growth, maxTier);
        }

        // Where a tier sits between the two authored ends, 0 at tier 0 and 1
        // at maxTier.
        //
        // Expressed as a FRACTION of the span rather than as a multiplier on
        // the value, which is what lets it work for a stat that starts at zero
        // or goes negative -- steel's speed penalty runs 0 to -2, and no
        // geometric curve on the value itself can express that.
        //
        //     f(t) = (g^t - 1) / (g^maxTier - 1)
        //
        // so both ends stay exactly where they were authored and only the
        // shape between them changes. Raising maxTier still stretches the
        // curve instead of inflating the tiers that already exist, which is
        // the property the two-ended authoring was chosen for.
        public static double CurveFraction(int tier, int maxTier)
        {
            return CurveFraction(tier, maxTier, TierGrowth);
        }

        // The same shape on an arbitrary growth rate. WeaponEntryResolver's
        // attack-axis interpolation is the one caller that ever passes
        // WeaponTierGrowth here; every existing armour call site keeps using
        // the no-arg overload above and is untouched by this parameter
        // existing at all.
        public static double CurveFraction(int tier, int maxTier, double growth)
        {
            if (maxTier <= 0 || tier <= 0) return 0.0;
            if (tier >= maxTier) return 1.0;

            double top = Math.Pow(growth, maxTier) - 1.0;
            if (top <= 0.0) return 0.0;

            return (Math.Pow(growth, tier) - 1.0) / top;
        }

        // One stat's value at tier 0, before any hand-authored line overrides
        // it. `weight` is the style's share for that stat.
        public static int AtBaseTier(EquipmentSlot slot, double weight)
        {
            return Round(Base * SlotWeight(slot) * weight);
        }

        // The same stat at maxTier.
        //
        // Scaled from the UNROUNDED tier-0 value on purpose. Rounding first
        // and multiplying second turns a 2.88 into a 2 and then into an 18.6
        // rather than a 26.8, so a third of the top end would be lost to a
        // rounding step nobody could see.
        public static int AtTopTier(EquipmentSlot slot, double weight, int maxTier)
        {
            return Round(Base * SlotWeight(slot) * weight * TopMultiplier(maxTier));
        }

        // What a piece DEMANDS, as against what it grants.
        //
        // A flat baseline distributed by the style's own weights, so a
        // material gates on exactly the scores it is for: a pure style asks 10
        // of its one stat, and a dual asks 4 and 7 rather than 10 and 10. The
        // set that makes you strong is the set that expects you to be.
        //
        // NO SLOT WEIGHT, deliberately, where the granted stats have one. Gate
        // on the MATERIAL and every piece of a set becomes wearable at the same
        // moment; gate per piece and gloves would be free while the torso stays
        // locked, which fragments a set into five separate decisions and makes
        // the lightest slot the one nobody thinks about.
        //
        // RECALIBRATED for Phase 4, 10.0 -> 2.5 -- D4 does not name this
        // constant, but leaving it untouched while Base above dropped 10x
        // breaks the invariant AFullSetIsWearable_ButOnePieceOfItAloneIsNot
        // exists to guard. A full top-tier single-material set used to grant
        // ~200 of its own ability score against a demand of 93 (Base=4.0);
        // at Base=0.4 the same set only grants ~19, so leaving the demand at
        // 93 would make a complete top-tier set of its own signature
        // material permanently inert -- "THE CASE THAT WOULD BREAK THE
        // GAME", per that test's own comment. 2.5 keeps demand comfortably
        // between a bare character's own score (16 CON, nothing invested)
        // and what a full matching top-tier set grants on top of it, the
        // same relationship 10.0 held at the old scale.
        public const double RequirementBaseline = 2.5;

        public static int RequirementAtBaseTier(double weight)
        {
            return Round(RequirementBaseline * weight);
        }

        // Up the same curve as everything else, so the gate keeps pace with
        // what the gear is worth rather than falling behind it.
        //
        // Reachable by construction: at tier 10 a pure style asks 23 of its
        // own stat, and the other four pieces of that same style supply about
        // 19 of it between them before base scores are counted -- see
        // GearScalingTests.AFullSetIsWearable_ButOnePieceOfItAloneIsNot for
        // the worked numbers. What it refuses is wearing one piece of a
        // material you have not otherwise committed to -- which is the whole
        // point of a requirement, and it is what makes the cascade in
        // ItemComparison something a player can actually meet.
        public static int RequirementAtTopTier(double weight, int maxTier)
        {
            return Round(RequirementBaseline * weight * TopMultiplier(maxTier));
        }

        // Away from zero, explicitly.
        //
        // Math.Round defaults to banker's rounding and Mathf.RoundToInt is the
        // same, so 2.5 goes to 2 and 3.5 goes to 4 -- an asymmetry that would
        // land differently on two stats that happen to sit either side of a
        // half. CLAUDE.md records a real test flake from exactly this.
        //
        // Double rather than the integer arithmetic ItemUpgrade insists on:
        // this runs once at content-build time on numbers under a hundred, not
        // per-hit at runtime, and the values are pinned by literal-valued
        // tests rather than by re-deriving this formula.
        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
