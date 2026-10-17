using System;
using System.Collections.Generic;

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
    // `Primary` is whatever this combatant's skills spend; it is mana only for
    // a combatant whose skills spend mana. FightController's PaintVitals
    // replays it.
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

        // THE WARD POOL AT THIS MOMENT (StatusEffects.WardPoints). Recorded
        // so a ward raised by a cast shows on the beat that raised it rather
        // than at the end of the round, when the plates repaint from live
        // state. Zero is both "no ward" and "not recorded": a painter has no
        // use for telling them apart, and a fixture built without one wants
        // no bar.
        public readonly int Ward;

        // THE STATUS DATA AT THIS MOMENT (StatusSnapshot: raw statuses plus
        // every session fact a badge row reads) and whether an enemy's break
        // shield is down, for the same reason as the ward: a status a cast
        // applies shows on the beat that applied it, not at the round-end
        // repaint from live state. The HUD formats it into rows when it paints
        // (FightHudModel.StatusRowsFrom). Null means NOT RECORDED (the short
        // constructors): the painter leaves the badges as they are rather than
        // blanking them. Stays out of Equals and GetHashCode.
        public readonly StatusSnapshot Statuses;
        public readonly bool Broken;

        public Vitals(int health, int primary, int signature)
            : this(health, primary, signature, 0, 0)
        {
        }

        public Vitals(int health, int primary, int signature, int maxHealth, int maxPrimary, int ward = 0,
                      StatusSnapshot statuses = null, bool broken = false)
        {
            Ward = ward;
            Statuses = statuses;
            Broken = broken;
            Health = health;
            Primary = primary;
            Signature = signature;
            MaxHealth = maxHealth;
            MaxPrimary = maxPrimary;
        }

        public bool Equals(Vitals other) =>
            Health == other.Health && Primary == other.Primary && Signature == other.Signature
            && MaxHealth == other.MaxHealth && MaxPrimary == other.MaxPrimary && Ward == other.Ward;

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
                hash = (hash * 397) ^ Ward;
                return hash;
            }
        }

        public override string ToString() => $"hp{Health} mp{Primary} sig{Signature}";
    }
}
