using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // SPLITS ONE ALREADY-CONSUMED TOTAL ACROSS AN ORDERED LIST OF TYPES
    // (plan 1.7). Ashen Reckoning is the only user today: a detonation
    // consumes a target's whole Poison pile as one figure (StatusCombos.
    // SpendPoisonIfMatched), and this divides that ONE number into packets
    // that each resolve through the ordinary damage pipeline on their own
    // affinity and resistance -- 25 Fire + 25 Frost is 37 + 12 against
    // something that burns to Fire and resists Frost, not 50 of something
    // averaged, the same independence damageInstances already give a
    // multi-packet spell.
    //
    // A STANDALONE PURE FUNCTION rather than a private method on
    // FightSession -- the arithmetic (floor, remainder to the first type) is
    // exactly the kind of thing AshenReckoningTests.
    // ASeventeenPointTotalSplitsAsNinePoisonAndEightFire pins with a literal,
    // and it needs no CombatantState, no RNG and no session to do that.
    public static class ConsumedTotalSplit
    {
        // Each type takes floor(total / k); the remainder (total mod k) goes
        // to the FIRST types in the list, one point at a time -- which is how
        // "the odd point goes to Poison" is expressed: Poison is authored
        // first in detonationSplit. A type whose share floors to zero is
        // left out of the result entirely rather than returned as a packet
        // that deals nothing.
        public static IReadOnlyList<DamageInstance> Split(int total, IReadOnlyList<DamageType> types)
        {
            var packets = new List<DamageInstance>();
            if (types == null || types.Count == 0 || total <= 0)
            {
                return packets;
            }

            int share = total / types.Count;
            int remainder = total % types.Count;

            for (int i = 0; i < types.Count; i++)
            {
                int amount = share + (i < remainder ? 1 : 0);
                if (amount > 0)
                {
                    packets.Add(new DamageInstance(types[i], amount));
                }
            }

            return packets;
        }
    }
}
