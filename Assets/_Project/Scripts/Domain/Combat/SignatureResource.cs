using System;

namespace PrincesPalace.Domain.Combat
{
    // A character-specific resource that builds during a fight and is spent
    // on that character's own abilities. Shawn's Wool is the first; the type
    // is deliberately generic so the second one is content and wiring rather
    // than a parallel system.
    //
    // Distinct from mana on purpose. Mana is shared by everyone, starts full,
    // and only ever goes down — it is a budget. A signature resource starts
    // EMPTY and grows as the fight goes on, so it is an arc: the character
    // who has it is weakest on turn one and strongest at the end, and every
    // turn asks whether to bank it or spend it.
    //
    // Engine-free and pure. Nothing here knows what Wool is for; the rules
    // about who has one and what it buys live in Core.
    public sealed class SignatureResource
    {
        public string Id;
        public string DisplayName;

        public int Current;
        public int Max;

        public int GainPerTurn;
        public int GainOnAttack;
        public int GainOnDamageTaken;

        // How much damage ONE point soaks. The resource is counted in small
        // whole numbers because that is what its skills spend — Shear costs
        // three of them — while damage lives on the x10 health scale. Without
        // this the two halves of the resource cannot both be tuned: a pool
        // big enough to be armour would make a resource-scaling skill absurd,
        // and a pool small enough to spend would be soaked through by a
        // single hit.
        public int AbsorbPerPoint;

        // Whether this resource soaks damage at all.
        //
        // FALSE for Wool as of the talent rework (handoff §2): wool stopped
        // being a damage sponge and became the thing the talent tree's
        // abilities spend. The two jobs were always in tension — banking for
        // armour and banking to spend are the same decision made twice — and
        // the tree only becomes a real set of choices once the resource has
        // exactly one use.
        //
        // The soak machinery below is deliberately KEPT rather than deleted.
        // It is tuned (see ContentDatabase.SignatureAbsorbPerPoint's own
        // long comment on why 2 and not 10), it is the obvious shape for a
        // future character whose signature resource IS armour, and deleting
        // it would throw away a real balance investigation to save one
        // branch.
        public bool AbsorbsDamage;

        public SignatureResource(string id, string displayName, int max, int gainPerTurn, int gainOnAttack, int gainOnDamageTaken, int absorbPerPoint = 1, bool absorbsDamage = false)
        {
            Id = id;
            DisplayName = displayName;
            Max = Math.Max(0, max);
            GainPerTurn = gainPerTurn;
            GainOnAttack = gainOnAttack;
            GainOnDamageTaken = gainOnDamageTaken;
            AbsorbPerPoint = Math.Max(1, absorbPerPoint);
            AbsorbsDamage = absorbsDamage;
            Current = 0;
        }

        public bool IsFull => Current >= Max;

        // Returns how much was ACTUALLY gained after clamping, not what was
        // asked for. Callers use the difference to tell the player their
        // fleece is overflowing — waste they can see is what makes spending
        // feel urgent, where a silent clamp would just look like the number
        // stopped working.
        public int Gain(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int before = Current;
            Current = Math.Min(Max, Current + amount);
            return Current - before;
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && Current >= amount;
        }

        // All or nothing. A partial spend would let an ability fire at a
        // fraction of its cost and a fraction of its effect, which is a
        // balance hole rather than a graceful degradation.
        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            Current -= amount;
            return true;
        }

        // Spends up to `amount` of DAMAGE and reports how much it covered.
        // This is the armour half of the resource: incoming damage eats it
        // before HP, so banking is defence and spending is offence, out of
        // one pool.
        //
        // Partial points round UP against the holder — a graze still scuffs a
        // whole point of fleece. Nothing is actually lost to that today,
        // since the smallest possible hit is exactly one point's worth
        // (CombatMath floors damage at one, then scales it), but the rule is
        // stated rather than assumed so a future half-scale damage source
        // cannot quietly make the pool infinite against chip damage.
        public int Absorb(int amount)
        {
            if (!AbsorbsDamage || amount <= 0 || Current <= 0)
            {
                return 0;
            }

            int absorbed = Math.Min(Current * AbsorbPerPoint, amount);
            Current -= (absorbed + AbsorbPerPoint - 1) / AbsorbPerPoint;
            return absorbed;
        }
    }
}
