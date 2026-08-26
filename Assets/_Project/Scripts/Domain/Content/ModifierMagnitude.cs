using System;

namespace PrincesPalace.Domain.Content
{
    // How an item modifier's AUTHORED (base) magnitude becomes the number a
    // combat hook actually reads — the item-modifier plan's own formula, from
    // "the model at a glance":
    //
    //     Magnitude = base x TierMultiplier(itemTier) x RiftMultiplier(riftTier)
    //
    // Applied ONCE, at ContentDatabase.ModifierEffects — the one assembly
    // point that has both the modifier's raw ModifierEffect (from
    // ModifierDefinition.ResolvedEffects) and the ONE equipped item instance
    // it rolled onto (ItemDefinition.tier, EquipmentSlotEntry.riftTier).
    // Every combat hook downstream (FightSession's on-hit riders,
    // DamagePipeline, FightEncounterAdapter's mana wiring, ...) reads an
    // already-scaled ModifierEffect.Magnitude and never has to know this
    // formula exists — the same reasoning GearScaling centralises armour's
    // own budget math in one place rather than trusting every reader to
    // reapply it identically.
    //
    // Binary/flag-shaped members (GuaranteedFirstAction, ManaToWardOnTurnStartPercent)
    // carry Magnitude 0 and scaling 0 is still 0, so nothing here needs a
    // special case for them — see ModifierEffect's own header on why a flag's
    // "power" is being rare rather than a bigger number.
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

        // A MILD, NAMED step per RiftTier rather than a geometric curve —
        // RiftTier only spans four rungs (0-3), and a steep curve over four
        // rungs reads as a cliff rather than a climb. These are tuning
        // knobs a designer will retune once real drops are played, not
        // derived truths — named individually (rather than folded into an
        // array literal) so each one can be retuned without hunting for
        // which index means what.
        public const double RiftMultiplierOrdinary = 1.0;
        public const double RiftMultiplierRiftTouched = 1.3;
        public const double RiftMultiplierRiftForged = 1.6;
        public const double RiftMultiplierConvergent = 2.0;

        public static double RiftMultiplier(RiftTier riftTier)
        {
            switch (riftTier)
            {
                case RiftTier.RiftTouched: return RiftMultiplierRiftTouched;
                case RiftTier.RiftForged: return RiftMultiplierRiftForged;
                case RiftTier.Convergent: return RiftMultiplierConvergent;

                // Ordinary, AND any out-of-range value a corrupted or
                // hand-edited save could carry — the same defensive "unknown
                // reads as the weakest real answer" posture RiftTier's own
                // int storage already assumes (see EquipmentSlotEntry.riftTier's
                // header: 0 is always a safe, correct reading of "nothing rolled").
                default: return RiftMultiplierOrdinary;
            }
        }

        // The combined scale ONE item instance's rolled modifiers read at —
        // both axes of the one item this copy actually is, never a
        // character-wide average.
        public static double Scale(int itemTier, RiftTier riftTier)
        {
            return TierMultiplier(itemTier) * RiftMultiplier(riftTier);
        }
    }
}
