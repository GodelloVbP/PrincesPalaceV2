using System;

namespace PrincesPalace.Domain.Combat
{
    // What a status effect DOES, independent of who has it or how long.
    //
    // Deliberately five, not the usual JRPG dozen, and deliberately missing
    // Slow/Haste — see StatusEffects' own header for why. Every one of these
    // five rides a hook the combat pipeline already has: Poison and Regen
    // are CombatMath.ApplyDamage/Heal on a timer, Protect and Vulnerable are
    // one more multiplier in the same slot EffectivenessMultiplier already
    // occupies, and Stun reuses the exact turn-skip mechanism BreakShield
    // proved out. Nothing here needed a new engine hook.
    //
    // Shielded is the sixth, added for the Magical Shield relic, and it did
    // NOT fit any of the five existing shapes. Protect looked closest but is
    // wrong on purpose: Protect decays by TURN COUNT and reads passively as
    // one more multiplier every hit takes; Magical Shield is spent by the
    // very next hit REGARDLESS of how many turns that takes to arrive, which
    // needed its own consume-and-remove call (StatusEffects.
    // ConsumeShieldedReduction) rather than another line in
    // DamageTakenMultiplier.
    public enum StatusEffectType
    {
        // Magnitude damage at the start of each of the holder's own turns.
        Poison,

        // Magnitude healing at the start of each of the holder's own turns.
        Regen,

        // Incoming damage reduced by Magnitude percent, floored the same way
        // every other damage-reducing source in this game is (never total
        // immunity).
        Protect,

        // Incoming damage increased by Magnitude percent.
        Vulnerable,

        // The holder's next turn is skipped entirely, then the status is
        // gone — spent, not decremented on a timer.
        Stun,

        // The next hit this combatant takes is reduced by Magnitude percent,
        // then the status is gone — spent on that ONE hit, not decremented
        // by turn count. TurnsRemaining is set generously high when this is
        // applied (see StatusEffects.Apply's caller) precisely so ordinary
        // turn-start ticking never expires it first; only
        // ConsumeShieldedReduction ever removes it.
        Shielded,

        // The holder's next attack must target whoever provoked them
        // (ActiveStatus.Source), and deals Magnitude percent less damage to
        // that target specifically.
        //
        // The seventh, added for the Black Ram's Provoke strand, and it fits
        // the existing shape better than a bespoke taunt field would: a taunt
        // is exactly "a thing on a combatant, from someone, for a while",
        // which is what this list already models. Riding the status list also
        // means Provoke is visible, tickable and clearable through machinery
        // that already exists, rather than through a second parallel system
        // the AI would have to remember to consult.
        //
        // Magnitude is 0 until Provoke T2 is bought, and a 0% reduction is a
        // correct no-op rather than a special case.
        Provoked,

        // The holder's next ATTACK deals Magnitude percent more damage, then
        // the status is gone — spent on that one swing, not decremented by
        // turn count.
        //
        // The eighth, added for the Fragile Lamb's Gift: Fury. Deliberately
        // the exact mirror of Shielded: same spent-on-the-next-occurrence
        // shape, same generous TurnsRemaining so ordinary ticking cannot
        // expire it first, same consume-and-remove call rather than another
        // line in a passive multiplier. Protect/Vulnerable were the wrong
        // shape for the same reason Shielded could not be Protect — those
        // decay by turn count and read passively on every hit, and a gift is
        // one swing whenever it happens to arrive.
        Empowered,
    }

    // One active affliction or boon on a combatant: what it is, how strong,
    // and how many of the HOLDER'S OWN turns it has left to run — not wall
    // clock, not a round count. TurnOrder's charge scheduling already means
    // a fast combatant acts more often than a slow one; counting duration in
    // anything but the holder's own turns would make a status quietly last
    // longer in real time on a slow combatant and shorter on a fast one,
    // which is not how any of this project's other per-turn numbers work
    // (compare SignatureResource.GainPerTurn, ManaRegen).
    public sealed class ActiveStatus
    {
        public readonly StatusEffectType Type;
        public int Magnitude;
        public int TurnsRemaining;

        // Who put this here, or null when nobody in particular did.
        //
        // Added for two talent engines that are paid for their own SETUP
        // rather than for what happens to them: the Fragile Lamb gains wool
        // when an ally carrying a status IT applied is hit, and the mage path
        // gains wool per enemy carrying a status IT applied. Without a source
        // both engines would pay out for any status from any origin, which
        // turns "ward the tank, the tank eats a hit, you get paid" into
        // "stand near a healer" — the agency the setup step exists to create
        // is precisely the thing being credited.
        //
        // Provoked needs it for a harder reason than credit: the taunt has to
        // know WHO to force the target onto.
        //
        // Not readonly, unlike Type: Apply refreshes an existing entry rather
        // than stacking a second one, and a refresh from a new caster should
        // re-point the credit at whoever most recently paid for it.
        public CombatantState Source;

        public ActiveStatus(StatusEffectType type, int magnitude, int turns, CombatantState source = null)
        {
            Type = type;
            Magnitude = magnitude;
            TurnsRemaining = Math.Max(1, turns);
            Source = source;
        }
    }
}
