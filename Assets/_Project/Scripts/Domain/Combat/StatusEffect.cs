using System;

namespace PrincesPalace.Domain.Combat
{
    // What a status effect DOES, independent of who has it or how long.
    //
    // Started at five, not the usual JRPG dozen, and originally missing
    // Slow/Haste on purpose — see StatusEffects' own header for the history
    // of why, and Chilled's own comment below for why that changed. Poison
    // and Regen are CombatMath.ApplyDamage/Heal on a timer, Protect and
    // Vulnerable are one more multiplier in the same slot
    // EffectivenessMultiplier already occupies, and Stun reuses the exact
    // turn-skip mechanism BreakShield proved out — nothing there needed a
    // new engine hook.
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

        // Incoming Speed reduced by Magnitude percent while this stands.
        // Decays by turn count exactly like Protect/Vulnerable — no
        // spent-on-one-hit shape here, this is a standing malus.
        //
        // THE NINTH, and the first that touches Speed at all — see
        // StatusEffects' own header for why every status above this one
        // deliberately left Speed alone, and why that no longer holds.
        // Magnitude/TurnsRemaining live here exactly like every other
        // status (refresh-not-stack via StatusEffects.Apply, ticked down by
        // StatusEffects.Tick's generic per-status countdown — Chilled needs
        // no special case there, unlike Poison/Regen/Provoked). What IS
        // special is where the number actually gets read: not a new hook
        // inside SpeedScale or DamageTakenMultiplier, but the EXISTING
        // relic-driven speed-buff bookkeeping in
        // FightSession.SpeedBuffs.cs, which already knew how to grant a
        // percent-of-true-base malus and revert it exactly on expiry (Lucky
        // Deck's slow proved that arithmetic correct first). See
        // FightSession.SpeedBuffs.RefreshChilledSpeed/ApplyChilled for the
        // wiring, and FightSession.Riders.TickStatuses for where an expired
        // Chilled hands its malus back.
        Chilled,

        // The holder's plain-attack (melee) option is gone while this
        // stands; it must act through a skill (ranged) instead, or forfeit
        // the turn if none is legal. Decays by turn count exactly like
        // Protect/Vulnerable/Chilled -- a standing malus, not a spent-on-one-
        // occurrence status like Stun/Shielded/Empowered. Magnitude is
        // ignored, the same "present or absent" shape Protect/Vulnerable's
        // own Magnitude is NOT (those read it) but GuaranteedFirstAction/
        // ManaToWardOnTurnStartPercent (ModifierEffectType) already are.
        //
        // THE TENTH, item-modifier plan Phase D3, and the one enemy-only by
        // construction rather than by convention: it builds on
        // FightSession.CanReach/FightController.Input's existing
        // front-rank rule, which is itself one-directional (it only ever
        // gated a PLAYER's plain attack against an enemy target; an enemy's
        // swing at the party was never reach-checked). Nothing in the game
        // authors an enemy ability that applies Rooted to a PLAYER combatant
        // today, so the gate this gets read by
        // (FightSession.Enemies.EffectivePoolFor) only ever runs for an
        // enemy's own turn -- see that method's own comment for where the
        // plain-attack entry actually gets excluded from the draw, and
        // ResolveSkippedTurn for the no-legal-skill forfeit, which reuses
        // Stun's exact turn-skip mechanism rather than inventing a second
        // one.
        Rooted,

        // Marked. A general "something is coming for you" debuff with no
        // effect of its own — anything that USES a mark (a relic or skill
        // effect keyed on marked targets) reads it through Marks.IsMarked
        // and spends it through Marks.ConsumeMark, never through this
        // enum's usual Magnitude/duration reading. Magnitude is unused
        // (always 0); TurnsRemaining is set generously high (Marks.
        // MarkDurationTurns) so ordinary per-turn ticking cannot expire an
        // unconsumed mark first — the same "spent, not decayed" convention
        // Shielded and Empowered already use, except a mark is spent by
        // ConsumeMark rather than by DamagePipeline.
        //
        // ELEVENTH, and the first status built as a REUSABLE Domain
        // facility rather than for one relic — see Marks' own header.
        // Drowned Lantern's own "spell marks its target" already shipped
        // before this existed and keeps its private per-session HashSet
        // rather than being migrated onto it; the two are independent and
        // do not interact.
        Marked,

        // Feared. Stunned (skips the holder's turn — StatusEffects.HasStun
        // treats this exactly like Stun) AND Vulnerable (Magnitude percent
        // more damage taken — StatusEffects.DamageTakenMultiplier reads it
        // the same way it reads Vulnerable) for TurnsRemaining of the
        // holder's own turns.
        //
        // NOT spent like Stun — Fear decays by turn count, same as
        // Protect/Vulnerable/Chilled, so ResolveSkippedTurn's ConsumeStun
        // (which only ever removes StatusEffectType.Stun) leaves a Feared
        // entry standing to skip the NEXT turn too, and the turn after
        // that, until its own duration runs out — matching "duration in
        // turns" rather than "spent on one skip".
        //
        // TWELFTH. See Fear's own header for the authored Magnitude
        // (vulnerable percent) and application API.
        Feared,
    }

    // One active affliction or boon on a combatant: what it is, how strong,
    // and how many of the HOLDER'S OWN turns it has left to run — not wall
    // clock, not a round count. TurnOrder's charge scheduling already means
    // a fast combatant acts more often than a slow one; counting duration in
    // anything but the holder's own turns would make a status quietly last
    // longer in real time on a slow combatant and shorter on a fast one,
    // which is not how any of this project's other per-turn numbers work
    // (compare ResourcePool.GainPerTurn, ManaRegen).
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
