using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // Folds a set's stat kinds into the figures a combatant is built with
    // (FightEncounterAdapter, PoolPrecedence).
    //
    // PERCENTS ARE SUMMED, NOT COMPOUNDED, and flats land after them: two
    // relics at +50% attack give +100%, not +125%. Summing is the arithmetic a
    // player can do, and compounding turns a stack of reasonable relics into an
    // unplayable number nobody authored.
    public static class EffectFold
    {
        public static int Apply(int baseValue, EffectStat stat, EffectSet effects)
        {
            if (effects == null || stat == EffectStat.None) return baseValue;

            int percent = 0;
            int flat = 0;

            foreach (var info in EffectCatalog.All)
            {
                if (info.Stat != stat) continue;

                // A talent's stat share is already inside EffectiveStats.
                int magnitude = effects.MagnitudeWithout(info.Kind, EffectHolder.Talent);
                if (info.Percent) percent += magnitude;
                else flat += magnitude;
            }

            // Percent first against the BASE, then the flat. The other order
            // would make a flat bonus scale with every percent relic, which
            // reads as a bug the moment a player checks the number.
            long scaled = baseValue + (long)baseValue * percent / 100L + flat;

            // Never below zero: a stack of drawbacks that produced a negative
            // max health would take the fight down rather than make the
            // character weak.
            if (scaled < 0) return 0;
            if (scaled > int.MaxValue) return int.MaxValue;

            return (int)scaled;
        }

        // Resistance is five numbers a holder contributes to independently, so
        // it folds into the block the combatant holds rather than through
        // Apply. Every copy counts (EffectStacking.Summed).
        public static ResistanceByType ApplyResistance(ResistanceByType baseValue, EffectSet effects)
        {
            if (effects == null) return baseValue;

            var result = baseValue;
            foreach (var effect in effects.All)
            {
                if (effect.Kind != EffectKind.ResistanceFlat) continue;

                if (effect.AgainstMagical) result = result.WithMagical(effect.Magnitude);
                else if (effect.Against.HasValue) result = result.With(effect.Against.Value, effect.Magnitude);
            }

            return result;
        }
    }
}
