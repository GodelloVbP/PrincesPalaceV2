using System;

namespace PrincesPalace.Domain.Combat.Session
{
    // A combatant's health, mana and signature resource at one instant.
    //
    // Replaces the `Vector3Int` v1 used as an (hp, mana, signature) tuple --
    // the beat system's ONLY dependency on UnityEngine, and the single thing
    // that stopped the whole recording half being engine-free. Naming the three
    // components also ends the class of bug where a caller reads `.z` and has
    // to remember which resource that was.
    public readonly struct Vitals : IEquatable<Vitals>
    {
        public readonly int Health;
        public readonly int Mana;
        public readonly int Signature;

        public Vitals(int health, int mana, int signature)
        {
            Health = health;
            Mana = mana;
            Signature = signature;
        }

        public bool Equals(Vitals other) =>
            Health == other.Health && Mana == other.Mana && Signature == other.Signature;

        public override bool Equals(object obj) => obj is Vitals other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Health;
                hash = (hash * 397) ^ Mana;
                hash = (hash * 397) ^ Signature;
                return hash;
            }
        }

        public override string ToString() => $"hp{Health} mp{Mana} sig{Signature}";
    }
}
