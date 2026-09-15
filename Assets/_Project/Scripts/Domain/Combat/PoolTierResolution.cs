using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // WHICH poolTiers RUNG A CAST FIRES, given the caster's ACTUAL primary
    // pool right now -- and the arithmetic every reader of that choice
    // shares: the row caption previewing what would fire, the cast itself
    // spending and multiplying, and the beat/log naming what did. One seam
    // rather than three copies of "the highest tier the pool can afford",
    // which is the whole point of building this as a model instead of
    // hand-wiring Bjorn's Slam.
    //
    // PURE. Nothing here mutates the pool -- Pick only reads Max/Current, and
    // the caller (FightSession.CastSkill) is the one place that actually
    // spends, exactly like SkillResolution.ResourceToSpend/CanAfford already
    // separate "how much would this cost" from "take it".
    public static class PoolTierResolution
    {
        // The tier a cast will fire, and exactly how much of the pool it
        // spends -- bundled because the spend amount is a ROUNDED fraction
        // of Max, not a field the tier itself carries, so a caller cannot
        // reconstruct it correctly from Tier alone without repeating the
        // rounding rule.
        public readonly struct Result
        {
            public readonly ResolvedPoolTier Tier;
            public readonly int SpendAmount;

            public bool Fired => Tier != null;

            public Result(ResolvedPoolTier tier, int spendAmount)
            {
                Tier = tier;
                SpendAmount = spendAmount;
            }

            public static readonly Result None = new Result(null, 0);
        }

        // TIERS ARE TRUSTED TO ALREADY BE ASCENDING BY Spend --
        // SkillEntryResolver.TryResolvePoolTiers refuses content that is not,
        // the same trust SkillResolution.Amount places in every other
        // resolved field. The highest AFFORDABLE tier is therefore simply the
        // LAST one in the list whose rounded spend the pool can currently
        // cover; walking every tier rather than binary-searching keeps this
        // readable for a ladder that will only ever be two or three rungs
        // long.
        public static Result Pick(ResourcePool pool, IReadOnlyList<ResolvedPoolTier> tiers)
        {
            if (pool == null || tiers == null || tiers.Count == 0) return Result.None;

            ResolvedPoolTier chosen = null;
            int spend = 0;

            foreach (var tier in tiers)
            {
                if (tier == null) continue;

                // ROUNDED, NOT FLOORED OR CEILED -- the one rounding rule
                // this mechanic states for itself (docs/CONTENT_SCHEMA.md),
                // rather than inheriting whichever convention a caller
                // happened to reach for. Matches Rounding.AwayFromZero's use
                // everywhere else a fraction becomes a spend (ChargeSkillMana's
                // discount, CombatMath.ScaledAttack).
                int amount = Rounding.AwayFromZero(tier.Spend * pool.Max);
                if (amount > pool.Current) continue;

                chosen = tier;
                spend = amount;
            }

            return chosen == null ? Result.None : new Result(chosen, spend);
        }

        // The skill's own computed damage (SkillResolution.Damage's output),
        // boosted by whichever tier fired -- or handed back untouched when
        // none did, which is every cast below the first threshold and every
        // skill that authors no poolTiers at all.
        public static int ApplyDamageMultiplier(int baseAmount, Result result) =>
            result.Fired ? Rounding.AwayFromZero(baseAmount * result.Tier.DamageMultiplier) : baseAmount;

        // The row caption / log-line name for this cast: "Slam" under the
        // first tier, "Slam x2"/"Slam x4" once one fires. ":0.#" rather than
        // ToString() so an authored whole multiplier (2, 4) reads as "x2" and
        // not "x2.0" -- every multiplier shipped today is a whole number, and
        // a fractional one (a future half-again tier) still prints cleanly.
        public static string Label(string displayName, Result result) =>
            result.Fired ? $"{displayName} x{result.Tier.DamageMultiplier:0.#}" : displayName;
    }
}
