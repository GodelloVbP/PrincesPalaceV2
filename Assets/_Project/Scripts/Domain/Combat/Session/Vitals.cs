using System;

namespace PrincesPalace.Domain.Combat.Session
{
    // A combatant's health and both of its resource pools at one instant.
    //
    // Replaces the `Vector3Int` v1 used as an (hp, mana, signature) tuple --
    // the beat system's ONLY dependency on UnityEngine, and the single thing
    // that stopped the whole recording half being engine-free. Naming the three
    // components also ends the class of bug where a caller reads `.z` and has
    // to remember which resource that was.
    //
    // `Mana` BECAME `Primary` when mana became one pool among several: the
    // slot always held "whatever this combatant's skills spend", and calling
    // that mana was true only for as long as everyone spent mana. Nothing
    // about the beat snapshot changed -- the same number, recorded at the
    // same two moments, replayed by the same painter (FightController's
    // PaintVitals).
    public readonly struct Vitals : IEquatable<Vitals>
    {
        public readonly int Health;
        public readonly int Primary;
        public readonly int Signature;

        // THE DENOMINATORS ARE PART OF THE MOMENT TOO (hunt 2026-09-11, F7).
        //
        // A snapshot used to hold three CURRENT values and nothing else, and
        // the painter paired each of them with a maximum read LIVE. That was
        // true for exactly as long as a maximum could not move inside a
        // fight -- and Transformation.Enter/Exit moves MaxHealth twice per
        // transform, mid-round, inside a round that is then replayed beat by
        // beat. Every beat recorded before the entry then drew its recorded
        // health over the post-transform maximum: the numerals stayed right
        // and every denominator and every bar fraction in the first half of
        // the round was wrong, in the same direction, until the form expired
        // and they were wrong the other way (with SetFill handed a fraction
        // over 1).
        //
        // Zero means NOT RECORDED, which is what the three-argument
        // constructor below still produces: a painter handed a zero maximum
        // falls back to live state rather than dividing by it.
        public readonly int MaxHealth;
        public readonly int MaxPrimary;

        public Vitals(int health, int primary, int signature)
            : this(health, primary, signature, 0, 0)
        {
        }

        public Vitals(int health, int primary, int signature, int maxHealth, int maxPrimary)
        {
            Health = health;
            Primary = primary;
            Signature = signature;
            MaxHealth = maxHealth;
            MaxPrimary = maxPrimary;
        }

        public bool Equals(Vitals other) =>
            Health == other.Health && Primary == other.Primary && Signature == other.Signature
            && MaxHealth == other.MaxHealth && MaxPrimary == other.MaxPrimary;

        public override bool Equals(object obj) => obj is Vitals other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Health;
                hash = (hash * 397) ^ Primary;
                hash = (hash * 397) ^ Signature;
                hash = (hash * 397) ^ MaxHealth;
                hash = (hash * 397) ^ MaxPrimary;
                return hash;
            }
        }

        public override string ToString() => $"hp{Health} mp{Primary} sig{Signature}";
    }
}
