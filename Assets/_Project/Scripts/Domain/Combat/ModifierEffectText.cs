using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // "Fiery -- deals 24% bonus Fire damage on hit", not
    // "ElementalDamageOnHitPercent: 24". ONE shared formatter for every screen
    // that lists a rolled modifier's effects, mirroring how ItemStatLines'
    // DamageReductionPercent is the one place resistance ever becomes a
    // percentage rather than every caller rounding R/(R+100) by hand --
    // the item-modifier plan's Phase E asks for exactly this shape.
    //
    // TAKES AN ALREADY-SCALED EFFECT. Every Magnitude here is read as the
    // final number a combat hook would see -- base x TierMultiplier x
    // RiftMultiplier already applied (see ModifierEffect's own header on
    // where that happens: ContentDatabase.ModifierEffects /
    // ModifierEffectsForItem, never here). This class only maps a TYPE to a
    // sentence FRAGMENT; it holds no scaling logic of its own to keep in sync
    // with ModifierMagnitude, and never will.
    //
    // FRAGMENTS, not full sentences: no leading capital, no trailing period,
    // so a caller can prefix "{DisplayName} -- " (ItemStatLines.ModifierLines
    // does exactly this) without carrying two conventions.
    //
    // A few members here name a FIXED TUNING CONSTANT alongside the authored,
    // scaled Magnitude (Chilled's own speed/duration, Root's own duration,
    // Hardened's push distance, Runic's ward conversion rate) -- those
    // constants are not per-modifier authored numbers (see each
    // ModifierEffectType member's own comment), so there is nothing to scale
    // and nothing wrong with reading FightTuning directly for display, the
    // exact posture FightSession's own on-hit riders already take.
    public static class ModifierEffectText
    {
        public static string Describe(ModifierEffect effect)
        {
            switch (effect.Type)
            {
                case ModifierEffectType.ElementalDamageOnHitPercent:
                    return $"deals {effect.Magnitude}% bonus {ElementName(effect)} damage on hit";

                case ModifierEffectType.TypedResistanceFlat:
                    return $"reduces {ElementName(effect)} damage taken by {ItemStatLines.DamageReductionPercent(effect.Magnitude)}%";

                case ModifierEffectType.FlatSpeedBonus:
                    return $"+{effect.Magnitude} Speed";

                case ModifierEffectType.LifestealPercent:
                    return $"heals for {effect.Magnitude}% of damage dealt";

                case ModifierEffectType.GuaranteedFirstAction:
                    return "always acts first in turn order";

                case ModifierEffectType.FlatPhysicalDamageReduction:
                    return $"-{effect.Magnitude} flat Physical damage taken";

                case ModifierEffectType.BreakShieldDepletionResistPercent:
                    return $"{effect.Magnitude}% less Break Shield depletion taken";

                case ModifierEffectType.OnKillSplashPercent:
                    return $"kills splash {effect.Magnitude}% Attack onto nearby enemies";

                case ModifierEffectType.PushBackOnHitChancePercent:
                    return $"{effect.Magnitude}% chance on hit to push the target back {FightTuning.ModifierPushBackSlots} in turn order";

                case ModifierEffectType.FlatMaxManaBonus:
                    return $"+{effect.Magnitude} Max Mana";

                case ModifierEffectType.FlatManaRegenBonus:
                    return $"+{effect.Magnitude} Mana Regen";

                case ModifierEffectType.NextSkillManaDiscountPercent:
                    return $"next skill after a plain hit costs {effect.Magnitude}% less mana";

                case ModifierEffectType.ManaToWardOnTurnStartPercent:
                    return $"converts {RatePercent(FightTuning.RunicWardConversionRate)}% of unspent mana to a Ward at the start of your turn";

                case ModifierEffectType.FortunateFavorOnWin:
                    return $"+{effect.Magnitude} Favor (this run only) on a fight won";

                case ModifierEffectType.DodgeChancePercent:
                    return $"{effect.Magnitude}% chance to dodge an attack outright";

                case ModifierEffectType.ChilledOnHitChancePercent:
                    return $"{effect.Magnitude}% chance on hit to Chill the target " +
                           $"(-{FightTuning.ChilledOnHitSpeedPercent}% Speed for {FightTuning.ChilledOnHitTurns} turns)";

                case ModifierEffectType.RootChancePercent:
                    return $"{effect.Magnitude}% chance on hit to Root the target for {FightTuning.RootOnHitTurns} turns";

                // None, and any future member this formatter has not caught up
                // with yet: an empty fragment rather than a thrown exception,
                // the same graceful-degradation posture the rest of the
                // content layer takes on an unrecognised id -- ItemStatLines.
                // ModifierLines skips an empty fragment rather than printing
                // "DisplayName -- ".
                default:
                    return "";
            }
        }

        // 0.25 -> "25", not "0.25%". A rate constant is authored as a
        // fraction (FightTuning.RunicWardConversionRate's own doc comment
        // calls it a rate), and the player-facing line wants the percentage
        // that fraction represents.
        private static int RatePercent(float rate) => Rounding.AwayFromZero(rate * 100f);

        // TypedResistanceFlat's and ElementalDamageOnHitPercent's target,
        // read the same way both members carry it -- AgainstMagical's
        // "magical" shorthand only ever reaches TypedResistanceFlat (see
        // ModifierEntryResolver.AcceptsMagicalShorthand), but this reads both
        // fields defensively rather than assuming Against is always set.
        private static string ElementName(ModifierEffect effect)
        {
            if (effect.AgainstMagical)
            {
                return "magic";
            }

            return effect.Against.HasValue ? effect.Against.Value.ToString() : "";
        }
    }
}
