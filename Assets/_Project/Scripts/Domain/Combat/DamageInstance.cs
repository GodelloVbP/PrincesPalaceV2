using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // One packet of typed damage inside a single attack.
    //
    // A spell is not necessarily one element. Frost Flare is 25 Fire AND 25
    // Frost in one cast, and each half is checked against the target's
    // weakness and resistance SEPARATELY — so against something that resists
    // Frost but burns to Fire, the same cast is simultaneously weak and
    // strong. Rolling that into one number before checking effectiveness
    // would throw the whole interaction away.
    //
    // This is why effectiveness moved off the caster. It used to be read from
    // the ACTOR's single attackType, which cannot express a cast that carries
    // two elements at once.
    [Serializable]
    public struct DamageInstance
    {
        public DamageType type;
        public int amount;

        public DamageInstance(DamageType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }
}
