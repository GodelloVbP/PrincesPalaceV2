using PrincesPalace.Domain.Stats;
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
        //
        // DefensePercent/DefenseFlat below apply to BOTH broad Defenses —
        // there is no longer one generic Defense stat for either to name
        // alone. See RelicStat.Defence's own comment.
        AttackPercent,
        DefensePercent,
        MaxHealthPercent,
        MaxManaPercent,
        SpeedPercent,

        // RESISTANCE TO ONE KIND OF HARM. The kind is named by the modifier's
        // own damageType field rather than by a value per type here: five types
        // would be five more entries saying the same thing, and a sixth damage
        // type would make it six.
        //
        // "magical" is accepted there as shorthand for every type that is not
        // physical -- what a buckler is actually sold as, and what the existing
        // two-way split already means by the word.
        //
        // FLAT ONLY, no percent twin. Resistance is already a percentage of a
        // sort: it reduces on the R/(R+100) curve, so "+20% resistance" would
        // be a percentage of a percentage and nobody could predict it.
        ResistanceFlat,

        // Flat additions, applied the same way.
        AttackFlat,
        DefenseFlat,
        MaxHealthFlat,
        MaxManaFlat,
        SpeedFlat,

        // Mechanic (e), ARMOR PENETRATION: flat, on the ATTACKER, applied
        // to CombatantState.ArmorPenetration -- see CombatMath.BroadDefense
        // for where it is spent. No percent twin, the same reasoning
        // ResistanceFlat gives for staying flat-only: this already lands
        // against a stat (broad Defense) that is itself already a
        // percentage-shaped mitigation curve, so a percent-of-a-percent
        // would be exactly as unreadable there as it would be for
        // Resistance.
        ArmorPenetrationFlat,

        // Balance pass 2, Jo-Sun's Book of Anatomy: a flat percent added
        // MULTIPLICATIVELY to the super-effective (weakness) multiplier
        // only -- see CombatMath.EffectivenessMultiplier's own header. No
        // percent/flat twin, the same reasoning ArmorPenetrationFlat gives:
        // this already lands against a multiplier, so a second "flat"
        // reading would mean something different (add to the RAW
        // multiplier rather than scale it) and this relic is authored as
        // the scaling reading.
        WeaknessDamageBonusPercent,

        // Vampire Dentures: the RELIC half of lifesteal, on the same stat
        // ModifierEffectType.LifestealPercent already grants from gear --
        // see CombatantState.RelicLifestealPercent's own comment for why
        // the two stay separate fields that sum at the one read site
        // rather than one shared bag.
        LifestealPercent,
    }

    // One numeric change, exactly as typed into relics.json.
    [Serializable]
    public class RawRelicModifier
    {
        public string type = "";
        public int amount;

        // Which kind of harm, for ResistanceFlat. One of the DamageType names,
        // or "magical" for every type that is not physical. Required by that
        // type and refused on every other -- an author who names a damage type
        // on an AttackPercent believes they have made a typed thing.
        public string damageType = "";
    }

    // One validated numeric change.
    public readonly struct RelicModifier
    {
        public readonly RelicModifierType Type;
        public readonly int Amount;

        // Set only for ResistanceFlat. Against names one element; AgainstMagical
        // means every type that is not physical, resolved from the "magical"
        // shorthand once at authoring time so nothing downstream knows the word.
        public readonly DamageType? Against;
        public readonly bool AgainstMagical;

        public RelicModifier(RelicModifierType type, int amount,
                             DamageType? against = null, bool againstMagical = false)
        {
            Type = type;
            Amount = amount;
            Against = against;
            AgainstMagical = againstMagical;
        }

        public bool IsPercent
        {
            get
            {
                switch (Type)
                {
                    case RelicModifierType.AttackPercent:
                    case RelicModifierType.DefensePercent:
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
                    case RelicModifierType.DefensePercent:
                    case RelicModifierType.DefenseFlat:
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
                    case RelicModifierType.ArmorPenetrationFlat:
                        return RelicStat.ArmorPenetration;
                    case RelicModifierType.WeaknessDamageBonusPercent:
                        return RelicStat.WeaknessBonus;
                    case RelicModifierType.LifestealPercent:
                        return RelicStat.Lifesteal;
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

        // Not renamed to match DefensePercent/DefenseFlat above — the British
        // spelling survives here on purpose, as a visible seam: this ONE
        // value now means "apply to BOTH PhysicalDefense and MagicalDefense",
        // since there is no longer a single generic Defense stat it could
        // target alone. FightEncounterAdapter.ToCombatant calls
        // RelicModifiers.Apply(_, RelicStat.Defence, _) twice, once per
        // broad Defense, reading the same modifier list both times.
        Defence,
        MaxHealth,
        MaxMana,
        Speed,

        // Mechanic (e). See RelicModifierType.ArmorPenetrationFlat.
        ArmorPenetration,

        // Balance pass 2. See RelicModifierType.WeaknessDamageBonusPercent
        // and .LifestealPercent respectively.
        WeaknessBonus,
        Lifesteal,
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
        // TYPED RESISTANCE DOES NOT GO THROUGH Apply, and cannot: Apply
        // answers "what does this ONE stat become", and resistance is five
        // numbers a relic contributes to independently. Folded into a block
        // instead, which is the shape the combatant already holds.
        public static ResistanceByType ApplyResistance(ResistanceByType baseValue,
                                                       IEnumerable<RelicModifier> modifiers)
        {
            if (modifiers == null) return baseValue;

            var result = baseValue;

            foreach (var modifier in modifiers)
            {
                if (modifier.Type != RelicModifierType.ResistanceFlat) continue;

                if (modifier.AgainstMagical) result = result.WithMagical(modifier.Amount);
                else if (modifier.Against.HasValue) result = result.With(modifier.Against.Value, modifier.Amount);
            }

            return result;
        }

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
