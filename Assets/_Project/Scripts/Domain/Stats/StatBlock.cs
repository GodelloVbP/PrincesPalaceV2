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
    // constructor, so the several dozen existing `new StatBlock(hp, spd,
    // atk)` call sites keep meaning exactly what they meant. A stat nobody
    // authors is zero and costs nothing, which is the same compatibility rule
    // AbilityDerivation follows.
    //
    // `defense` — the old single generic mitigation stat — is GONE. It is
    // not renamed to anything: physicalDefense and magicalDefense (formerly
    // physicalResistance/magicalResistance, renamed in place) are the only
    // defensive fields now, and DamagePipeline is the only place either is
    // read for mitigation. See its header for the canonical equation.
    [Serializable]
    public struct StatBlock : IEquatable<StatBlock>
    {
        public int maxHealth;
        public int speed;
        public int attack;

        // Mana returned at the start of each of this combatant's turns.
        public int manaRegen;

        // Damage reduction in POINTS, not percent, against Physical and
        // against everything else respectively. See DamagePipeline.
        // AfterDefences for the curve they feed and why it is that shape.
        public int physicalDefense;
        public int magicalDefense;

        // Critical-hit BONUSES in percent points on top of CritRules' party
        // baseline (see StatType.CritChance), so zero means "the baseline",
        // not "never crits". Appended and optional on the constructor for the
        // same compatibility reason as the set stats above.
        public int critChance;
        public int critDamage;

        public StatBlock(int maxHealth, int speed, int attack,
            int manaRegen = 0, int physicalDefense = 0, int magicalDefense = 0,
            int critChance = 0, int critDamage = 0)
        {
            this.critChance = critChance;
            this.critDamage = critDamage;
            this.maxHealth = maxHealth;
            this.speed = speed;
            this.attack = attack;
            this.manaRegen = manaRegen;
            this.physicalDefense = physicalDefense;
            this.magicalDefense = magicalDefense;
        }

        public static StatBlock Zero => new StatBlock(0, 0, 0);

        public int this[StatType stat]
        {
            get
            {
                switch (stat)
                {
                    case StatType.MaxHealth: return maxHealth;
                    case StatType.Speed: return speed;
                    case StatType.Attack: return attack;
                    case StatType.ManaRegen: return manaRegen;
                    case StatType.PhysicalDefense: return physicalDefense;
                    case StatType.MagicalDefense: return magicalDefense;
                    case StatType.CritChance: return critChance;
                    case StatType.CritDamage: return critDamage;
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
                case StatType.MaxHealth: return new StatBlock(value, 0, 0);
                case StatType.Speed: return new StatBlock(0, value, 0);
                case StatType.Attack: return new StatBlock(0, 0, value);
                case StatType.ManaRegen: return new StatBlock(0, 0, 0, manaRegen: value);
                case StatType.PhysicalDefense: return new StatBlock(0, 0, 0, physicalDefense: value);
                case StatType.MagicalDefense: return new StatBlock(0, 0, 0, magicalDefense: value);
                case StatType.CritChance: return new StatBlock(0, 0, 0, critChance: value);
                case StatType.CritDamage: return new StatBlock(0, 0, 0, critDamage: value);
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
                a.manaRegen + b.manaRegen,
                a.physicalDefense + b.physicalDefense,
                a.magicalDefense + b.magicalDefense,
                a.critChance + b.critChance,
                a.critDamage + b.critDamage);
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
                a.manaRegen - b.manaRegen,
                a.physicalDefense - b.physicalDefense,
                a.magicalDefense - b.magicalDefense,
                a.critChance - b.critChance,
                a.critDamage - b.critDamage);
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
                Math.Max(floor, manaRegen),
                Math.Max(floor, physicalDefense),
                Math.Max(floor, magicalDefense),
                Math.Max(floor, critChance),
                Math.Max(floor, critDamage));
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
                Rounding.AwayFromZero(manaRegen * multiplier),
                Rounding.AwayFromZero(physicalDefense * multiplier),
                Rounding.AwayFromZero(magicalDefense * multiplier),
                // The crit pair is not scaled: percent points, and a tier
                // multiplier is about a body's size, not its luck.
                critChance,
                critDamage);
        }

        // Like Scaled, but attack and the two broad defenses each ride their
        // own, gentler multiplier instead of the main one.
        //
        // PHASE 5B (D6): physicalDefense/magicalDefense now ride
        // `defenseMultiplier`, not `multiplier`. Phase 1 left them on the
        // main multiplier as a deliberate rename-only step (see git history
        // on this method for that note) — this is the fix the plan always
        // meant: an Elite is HP x1.40 / ATK x1.15 / both Defenses x1.15, and
        // a defense riding the HP multiplier let an Elite's mitigation climb
        // as fast as its health pool, which is not what "elite" is supposed
        // to mean.
        public StatBlock ScaledForElite(float multiplier, float defenseMultiplier, float attackMultiplier)
        {
            return new StatBlock(
                Rounding.AwayFromZero(maxHealth * multiplier),
                Rounding.AwayFromZero(speed * multiplier),
                Rounding.AwayFromZero(attack * attackMultiplier),
                Rounding.AwayFromZero(manaRegen * multiplier),
                Rounding.AwayFromZero(physicalDefense * defenseMultiplier),
                Rounding.AwayFromZero(magicalDefense * defenseMultiplier),
                critChance,
                critDamage);
        }

        public bool Equals(StatBlock other)
        {
            return maxHealth == other.maxHealth
                && speed == other.speed
                && attack == other.attack
                && manaRegen == other.manaRegen
                && physicalDefense == other.physicalDefense
                && magicalDefense == other.magicalDefense
                && critChance == other.critChance
                && critDamage == other.critDamage;
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
                hash = (hash * 397) ^ manaRegen;
                hash = (hash * 397) ^ physicalDefense;
                hash = (hash * 397) ^ magicalDefense;
                hash = (hash * 397) ^ critChance;
                hash = (hash * 397) ^ critDamage;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"HP {maxHealth}, SPD {speed}, ATK {attack}, MPR {manaRegen}, PDEF {physicalDefense}, MDEF {magicalDefense}, CRIT +{critChance}%, CDMG +{critDamage}%";
        }
    }
}
