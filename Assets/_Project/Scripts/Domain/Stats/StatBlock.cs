using System;

namespace PrincesPalace.Domain.Stats
{
    // A combatant's stats. Deliberately a plain serializable struct with no
    // UnityEngine dependency, so the domain layer stays engine-free and
    // unit-testable while Unity can still show it in the Inspector on a
    // ScriptableObject (Unity honours System.SerializableAttribute).
    //
    // Values combine additively: a character's effective stats are their
    // definition's base block plus the block from every unlocked talent.
    //
    // The last three arrived with equipment sets and are OPTIONAL on the
    // constructor, so the several dozen existing `new StatBlock(hp, spd, atk,
    // def)` call sites keep meaning exactly what they meant. A stat nobody
    // authors is zero and costs nothing, which is the same compatibility rule
    // AbilityDerivation follows.
    [Serializable]
    public struct StatBlock : IEquatable<StatBlock>
    {
        public int maxHealth;
        public int speed;
        public int attack;
        public int defense;

        // Mana returned at the start of each of this combatant's turns.
        public int manaRegen;

        // Damage reduction in POINTS, not percent, against Physical and
        // against everything else respectively. See CombatMath.AfterResistance
        // for the curve they feed and why it is that shape.
        public int physicalResistance;
        public int magicalResistance;

        public StatBlock(int maxHealth, int speed, int attack, int defense,
            int manaRegen = 0, int physicalResistance = 0, int magicalResistance = 0)
        {
            this.maxHealth = maxHealth;
            this.speed = speed;
            this.attack = attack;
            this.defense = defense;
            this.manaRegen = manaRegen;
            this.physicalResistance = physicalResistance;
            this.magicalResistance = magicalResistance;
        }

        public static StatBlock Zero => new StatBlock(0, 0, 0, 0);

        public int this[StatType stat]
        {
            get
            {
                switch (stat)
                {
                    case StatType.MaxHealth: return maxHealth;
                    case StatType.Speed: return speed;
                    case StatType.Attack: return attack;
                    case StatType.Defense: return defense;
                    case StatType.ManaRegen: return manaRegen;
                    case StatType.PhysicalResistance: return physicalResistance;
                    case StatType.MagicalResistance: return magicalResistance;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatBlock has no field wired up for this StatType.");
                }
            }
        }

        // Builds a block carrying one stat. This is what lets a data-driven
        // authoring layer name stats as strings and sum them, without a
        // switch at every call site — the item-set resolver is built on it.
        public static StatBlock ForStat(StatType stat, int value)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return new StatBlock(value, 0, 0, 0);
                case StatType.Speed: return new StatBlock(0, value, 0, 0);
                case StatType.Attack: return new StatBlock(0, 0, value, 0);
                case StatType.Defense: return new StatBlock(0, 0, 0, value);
                case StatType.ManaRegen: return new StatBlock(0, 0, 0, 0, manaRegen: value);
                case StatType.PhysicalResistance: return new StatBlock(0, 0, 0, 0, physicalResistance: value);
                case StatType.MagicalResistance: return new StatBlock(0, 0, 0, 0, magicalResistance: value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatBlock cannot build a block for this StatType.");
            }
        }

        public static StatBlock operator +(StatBlock a, StatBlock b)
        {
            return new StatBlock(
                a.maxHealth + b.maxHealth,
                a.speed + b.speed,
                a.attack + b.attack,
                a.defense + b.defense,
                a.manaRegen + b.manaRegen,
                a.physicalResistance + b.physicalResistance,
                a.magicalResistance + b.magicalResistance);
        }

        // The comparison a hover panel diffs two loadouts with — negative
        // where `b` grants more than `a` does, same sign convention `+`
        // already establishes.
        public static StatBlock operator -(StatBlock a, StatBlock b)
        {
            return new StatBlock(
                a.maxHealth - b.maxHealth,
                a.speed - b.speed,
                a.attack - b.attack,
                a.defense - b.defense,
                a.manaRegen - b.manaRegen,
                a.physicalResistance - b.physicalResistance,
                a.magicalResistance - b.magicalResistance);
        }

        // Clamps every stat to at least `floor`. Useful once talents or
        // effects can subtract, so a stack of penalties can't drive a
        // combatant's speed (and therefore their turn order) negative.
        public StatBlock ClampedAtLeast(int floor)
        {
            return new StatBlock(
                Math.Max(floor, maxHealth),
                Math.Max(floor, speed),
                Math.Max(floor, attack),
                Math.Max(floor, defense),
                Math.Max(floor, manaRegen),
                Math.Max(floor, physicalResistance),
                Math.Max(floor, magicalResistance));
        }

        // Multiplies every stat by the same factor and rounds to the nearest
        // int — used for tiered enemy strength (e.g. an Elite pack) without
        // needing a second full StatBlock authored per tier.
        //
        // Rounds away-from-zero via Rounding.AwayFromZero, the same rule
        // CombatMath's own damage math uses — see that type's header for why
        // (AUDIT.md #16: a scaled stat and the damage math reading it used to
        // disagree at the exact .5 tie).
        public StatBlock Scaled(float multiplier)
        {
            return new StatBlock(
                Rounding.AwayFromZero(maxHealth * multiplier),
                Rounding.AwayFromZero(speed * multiplier),
                Rounding.AwayFromZero(attack * multiplier),
                Rounding.AwayFromZero(defense * multiplier),
                Rounding.AwayFromZero(manaRegen * multiplier),
                Rounding.AwayFromZero(physicalResistance * multiplier),
                Rounding.AwayFromZero(magicalResistance * multiplier));
        }

        // Like Scaled, but defense AND attack each ride their own, gentler
        // multiplier instead of the main one. Damage in this game is flat
        // subtraction (attack - defense, floored at 1 — see CombatMath.
        // ComputeAttackDamage), which makes either side of that subtraction
        // worth far more than an equal-percentage change to a stat that
        // isn't directly subtracted: scaling defense by the full multiplier
        // drives the PLAYER's damage toward the floor, and scaling attack by
        // the full multiplier passes straight through into what the elite
        // deals, undiminished, because nothing on the player's side softens
        // it back. That combination — defense originally uncapped, attack
        // still uncapped even after defense was fixed — is what made Elites
        // "completely clap you" in playtesting, twice: once before the
        // defense multiplier existed, and again after, because only half of
        // flat subtraction's two levers had been softened. See
        // FightController.EliteStatMultiplier / EliteDefenseMultiplier /
        // EliteAttackMultiplier for where this is actually used.
        public StatBlock ScaledForElite(float multiplier, float defenseMultiplier, float attackMultiplier)
        {
            return new StatBlock(
                Rounding.AwayFromZero(maxHealth * multiplier),
                Rounding.AwayFromZero(speed * multiplier),
                Rounding.AwayFromZero(attack * attackMultiplier),
                Rounding.AwayFromZero(defense * defenseMultiplier),
                Rounding.AwayFromZero(manaRegen * multiplier),
                Rounding.AwayFromZero(physicalResistance * multiplier),
                Rounding.AwayFromZero(magicalResistance * multiplier));
        }

        public bool Equals(StatBlock other)
        {
            return maxHealth == other.maxHealth
                && speed == other.speed
                && attack == other.attack
                && defense == other.defense
                && manaRegen == other.manaRegen
                && physicalResistance == other.physicalResistance
                && magicalResistance == other.magicalResistance;
        }

        public override bool Equals(object obj)
        {
            return obj is StatBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = maxHealth;
                hash = (hash * 397) ^ speed;
                hash = (hash * 397) ^ attack;
                hash = (hash * 397) ^ defense;
                hash = (hash * 397) ^ manaRegen;
                hash = (hash * 397) ^ physicalResistance;
                hash = (hash * 397) ^ magicalResistance;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"HP {maxHealth}, SPD {speed}, ATK {attack}, DEF {defense}, MPR {manaRegen}, PRES {physicalResistance}, MRES {magicalResistance}";
        }
    }
}
