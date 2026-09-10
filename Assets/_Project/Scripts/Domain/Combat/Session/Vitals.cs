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

        public Vitals(int health, int primary, int signature)
        {
            Health = health;
            Primary = primary;
            Signature = signature;
        }

        public bool Equals(Vitals other) =>
            Health == other.Health && Primary == other.Primary && Signature == other.Signature;

        public override bool Equals(object obj) => obj is Vitals other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Health;
                hash = (hash * 397) ^ Primary;
                hash = (hash * 397) ^ Signature;
                return hash;
            }
        }

        public override string ToString() => $"hp{Health} mp{Primary} sig{Signature}";
    }
}
