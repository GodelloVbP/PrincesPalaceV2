using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // What a numeric relic changes.
    //
    // THE TABLE THAT SITS ALONGSIDE RelicEffect, not instead of it. The enum
    // keeps the mechanics that are genuinely special cases -- "strike twice",
    // "halve the next hit", "take another turn" have no shared shape and
    // generalising over them would be a fake abstraction. This covers the
    // opposite kind of relic: the ones that are a number applied to a stat, of
    // which there can be dozens and none of which should cost a line of C#.
    //
    // A relic may carry both. "+15% attack AND strike twice" is one relic with
    // one modifier and one effect, which is exactly the composition the split
    // buys.
    public enum RelicModifierType
    {
        // Inert, and the default -- an authored modifier with no type does
        // nothing rather than silently becoming the first real entry.
        None = 0,

        // Percent of the character's own base, applied once when the fight
        // builds their kit. 15 means +15%.
        AttackPercent,
        DefencePercent,
        MaxHealthPercent,
        MaxManaPercent,
        SpeedPercent,

        // Flat additions, applied the same way.
        AttackFlat,
        DefenceFlat,
        MaxHealthFlat,
        MaxManaFlat,
        SpeedFlat,
    }

    // One numeric change, exactly as typed into relics.json.
    [Serializable]
    public class RawRelicModifier
    {
        public string type = "";
        public int amount;
    }

    // One validated numeric change.
    public readonly struct RelicModifier
    {
        public readonly RelicModifierType Type;
        public readonly int Amount;

        public RelicModifier(RelicModifierType type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        public bool IsPercent
        {
            get
            {
                switch (Type)
                {
                    case RelicModifierType.AttackPercent:
                    case RelicModifierType.DefencePercent:
                    case RelicModifierType.MaxHealthPercent:
                    case RelicModifierType.MaxManaPercent:
                    case RelicModifierType.SpeedPercent:
                        return true;
                    default:
                        return false;
                }
            }
        }

        // Which stat this touches, with the percent/flat distinction removed.
        // Lets a consumer switch on five stats rather than ten cases, and means
        // adding a flat/percent pair to the enum needs one line here rather
        // than a new branch everywhere a modifier is applied.
        public RelicStat Stat
        {
            get
            {
                switch (Type)
                {
                    case RelicModifierType.AttackPercent:
                    case RelicModifierType.AttackFlat:
                        return RelicStat.Attack;
                    case RelicModifierType.DefencePercent:
                    case RelicModifierType.DefenceFlat:
                        return RelicStat.Defence;
                    case RelicModifierType.MaxHealthPercent:
                    case RelicModifierType.MaxHealthFlat:
                        return RelicStat.MaxHealth;
                    case RelicModifierType.MaxManaPercent:
                    case RelicModifierType.MaxManaFlat:
                        return RelicStat.MaxMana;
                    case RelicModifierType.SpeedPercent:
                    case RelicModifierType.SpeedFlat:
                        return RelicStat.Speed;
                    default:
                        return RelicStat.None;
                }
            }
        }
    }

    public enum RelicStat
    {
        None = 0,
        Attack,
        Defence,
        MaxHealth,
        MaxMana,
        Speed,
    }

    // Folds a set of modifiers into a single change per stat.
    //
    // PERCENTS ARE SUMMED, NOT COMPOUNDED, and flats land after them. Two
    // relics at +50% attack give +100%, not +125%. Summing is the choice
    // players can actually do arithmetic on, and compounding is how a stack of
    // individually reasonable relics turns into an unplayable number without
    // anybody having authored a large one.
    public static class RelicModifiers
    {
        public static int Apply(int baseValue, RelicStat stat, IEnumerable<RelicModifier> modifiers)
        {
            if (modifiers == null) return baseValue;

            int percent = 0;
            int flat = 0;

            foreach (var modifier in modifiers)
            {
                if (modifier.Stat != stat || stat == RelicStat.None) continue;

                if (modifier.IsPercent) percent += modifier.Amount;
                else flat += modifier.Amount;
            }

            // Percent first against the BASE, then the flat. The other order
            // would make a flat bonus scale with every percent relic, which
            // reads as a bug the moment a player checks the number.
            long scaled = baseValue + (long)baseValue * percent / 100L + flat;

            // Never below zero: a stack of negative relics that produced a
            // negative max health would take the fight down rather than make
            // the character weak.
            if (scaled < 0) return 0;
            if (scaled > int.MaxValue) return int.MaxValue;

            return (int)scaled;
        }
    }
}
