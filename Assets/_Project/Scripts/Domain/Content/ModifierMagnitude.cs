using System;

namespace PrincesPalace.Domain.Content
{
    // How an item modifier's AUTHORED (base) magnitude becomes the number a
    // combat hook actually reads — the item-modifier plan's own formula, from
    // "the model at a glance":
    //
    //     Magnitude = base x TierMultiplier(itemTier)
    //
    // Applied ONCE, at ContentDatabase.Effects — the one assembly
    // point that has both the modifier's raw Effect (from
    // ModifierDefinition.ResolvedEffects) and the ONE equipped item instance
    // it rolled onto (its tier, EquipmentSlotEntry.tier).
    // Every combat hook downstream (FightSession's on-hit riders,
    // DamagePipeline, FightEncounterAdapter's mana wiring, ...) reads an
    // already-scaled Effect.Magnitude and never has to know this
    // formula exists — the same reasoning GearScaling centralises armour's
    // own budget math in one place rather than trusting every reader to
    // reapply it identically.
    //
    // A flag-shaped kind (ManaToWardOnTurnStartPercent) carries Magnitude 0
    // and scaling 0 is still 0, so nothing here needs a special case for
    // it.
    public static class ModifierMagnitude
    {
        // REUSES GearScaling's OWN per-tier growth rate rather than inventing
        // a second curve — the plan's own suggestion. Tier 0 -> 1.0x, tier 10
        // (today's top armour tier) -> ~9.3x, the identical shape a piece of
        // gear's own combat-stat budget already climbs (GearScaling.
        // CombatBudget), so a Fiery modifier on a tier-10 glove scales up
        // exactly as aggressively as the glove's own stats did.
        public static double TierMultiplier(int itemTier)
        {
            return itemTier <= 0 ? 1.0 : Math.Pow(GearScaling.TierGrowth, itemTier);
        }

        // The scale ONE item instance's rolled modifiers read at — the tier
        // of the one item this copy actually is, never a character-wide
        // average.
        public static double Scale(int itemTier)
        {
            return TierMultiplier(itemTier);
        }
    }
}
