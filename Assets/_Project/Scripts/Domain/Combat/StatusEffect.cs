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
    // wrong on purpose: Protect reads passively as one more multiplier every
    // hit takes, where a ward is a QUANTITY that damage is taken out of and
    // that runs out. That needed its own spend-and-remove call
    // (StatusEffects.ConsumeWard) rather than another line in
    // DamageTakenMultiplier -- and it still does, now more than ever: a pool
    // has a number left in it, which no multiplier can express.
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

        // A WARD: a pool of shield POINTS, carried on Magnitude. Incoming
        // damage comes off the pool before it comes off health, the pool
        // keeps whatever a small hit did not spend, and the status is removed
        // when it reaches zero. TurnsRemaining is a real duration counted
        // down by StatusEffects.Tick like every other status's -- two of the
        // wearer's own turns by default (FightTuning.DefaultWardTurns), or
        // StatusEffects.PermanentWardTurns for The Golden Fleece and the
        // relic wards.
        //
        // IT WAS A PERCENTAGE until 2026-09-16 (AUDIT #152) -- "the next hit
        // is Magnitude percent softer", spent whole by that hit, applied at
        // 999 turns so no clock could take it first. StatusEffects' own WARDS
        // header carries the model and the order a hit meets it in;
        // StatusEffects.ApplyWard is the only way to put one up, and
        // StatusEffects.Apply throws on this member precisely so the two
        // readings can never coexist.
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
        // FightSession.CanReachEnemy/FightController.Input's existing
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

        // Magnitude damage at the start of each of the holder's own turns,
        // like Poison -- but MITIGATED, unlike Poison. StatusEffects
        // .MitigationOf(Burn) answers AffinityOnly: a tick is reduced by
        // resistance and amplified by weakness (StatusEffects.ElementOf
        // answers Fire), with no flat defense, no ward, no Protect/
        // Vulnerable and no variance. See plan 1.5 for the snapshot rule --
        // the caster's own potency is folded in ONCE, at application, and
        // never re-read.
        //
        // THIRTEENTH, spell-expansion milestone E. Censer of Embers is its
        // one author today.
        Burn,

        // The mirror of Burn, Nature-typed instead of Fire, with a second
        // damage moment Burn does not have: FightSession's post-action hook
        // (plan 1.11) deals this same stored snapshot again after the
        // holder completes a PHYSICAL MOVE of their own -- "whenever it
        // strikes or charges" (Thorn Tithe's own tooltip). The holder is
        // punished for moving, not whoever they moved against; the hook
        // reads and damages THIS combatant, never a target.
        //
        // FOURTEENTH. Thorn Tithe is its one author today.
        Thorned,

        // Burn's shape, Physical-typed, and ARMOURED: each tick meets the
        // holder's physical defence as well as their Physical affinity
        // (StatusEffects.MitigationOf answers Armoured) -- on a hit's curve,
        // with no ward, no Protect/Vulnerable and no variance, floor 1. The
        // snapshot rule is Burn's (FightSession.ApplyStatusTo), and it
        // stacks like every other DoT.
        //
        // FIFTEENTH, Bellwether kit M2 (docs/PLAN_BELLWETHER_KIT.md 1.3/3.2).
        // Any skill, monster rider or weapon modifier applies it by name.
        Bleed,

        // Magnitude points of BOTH broad defences (Defense and Magical
        // Defense) while this stands: CombatMath.BroadDefense adds it, so a
        // hit, a preview and the Iron Retort all read one figure. A standing
        // modifier, aged at the end of the bearer's turn like Protect, and a
        // second cast refreshes it rather than doubling it.
        //
        // SIXTEENTH, Slice C of the Bjorn constellations. Hold the Line puts
        // it on the whole party. Protect could not be reused: it is a percent
        // of damage taken, not a stat, so it would not feed the Iron Retort
        // and does not read as "more armour" on the plate.
        Fortified,
    }

    // WHEN A STATUS'S COUNTER MOVES -- the one question that decides how an
    // authored duration reads to a player, and the reason there are three
    // answers rather than two.
    //
    // Before 2026-09-20 there were effectively two: Tick's start-of-turn
    // countdown, and a hand-written exemption list (IsSpentByTheTurn) for the
    // three statuses that are spent rather than aged, plus a fourth
    // arrangement for wards alone. That made "turns: 2" mean two things
    // depending on which list the member happened to be on: a Vulnerable
    // authored at 2 exposed its bearer for ONE turn, because the counter
    // reached zero and the entry was removed at the start of the second turn,
    // before that turn's action ever happened.
    //
    // StatusEffects.DurationClock is the table, and it is total -- every
    // member answers, and StatusEffectsTests.EveryStatusTypeAnswersDurationClock
    // fails rather than letting a new member default into the wrong family.
    public enum StatusClock
    {
        // Counted by the tick that does the work, at the holder's turn start.
        // N authored = N ticks dealt or healed. Poison and Regen.
        AtTick,

        // No clock at all. The counter moves at the moment the effect is
        // spent -- ConsumeStun, ConsumeProvoke, ConsumeEmpowerment -- so the
        // turn it promised always happens before the count moves.
        AtUse,

        // Counted at the END of the bearer's turn, exempting a turn the status
        // was applied during. N authored = N of the bearer's turns fully
        // covered, restriction intact through the final affected action. This
        // is the rule wards have used since 2026-09-16, generalised to every
        // standing modifier.
        AtTurnEnd,
    }

    // HOW MUCH OF A HIT'S OWN DEFENCE STACK A DAMAGING STATUS'S TICK MEETS.
    // A third table, beside DurationClock and StackingPolicy, and the one
    // that answers plan 1.5's own question for the new DoTs: Poison has
    // always ignored every defence entirely, and Burn/Thorned are not that --
    // they meet the holder's elemental affinity, and nothing else.
    //
    // StatusEffects.MitigationOf(type) is the table; only a damaging status
    // (one ElementOf answers) is ever asked, so it is not total over every
    // StatusEffectType the way DurationClock is.
    public enum StatusMitigation
    {
        // The tick's stored Magnitude reaches health unmodified. Poison's
        // own arithmetic, preserved exactly (plan 1.5).
        None,

        // The tick's stored Magnitude meets ONLY the holder's elemental
        // affinity for ElementOf(type) -- CombatMath.EffectivenessMultiplier,
        // the same weak/resist curve a typed hit meets -- and nothing else:
        // no flat defense, no typed resistance stat, no ward, no Protect/
        // Vulnerable, no variance. Burn and Thorned both answer this.
        AffinityOnly,

        // AffinityOnly plus the holder's full defence against ElementOf(type)
        // -- CombatMath.TotalDefense read on the same R/(R+100) curve a hit
        // meets (CombatMath.AfterResistance), floor 1. Still no ward, no
        // Protect/Vulnerable, no variance: armour is a property of the
        // wearer, those three are properties of a swing. Bleed answers this.
        Armoured,
    }

    // WHAT A SECOND APPLICATION DOES. Owner's decision, 2026-09-20 -- "the
    // DoTs and everything can stack of course" -- replacing the blanket
    // refresh rule StatusEffects.Apply used to hold for every member.
    //
    // A different question from StatusClock and therefore a different table:
    // Chilled and Rooted are both AtTurnEnd and they answer this one
    // differently.
    public enum StackingPolicy
    {
        // Merge into the entry already there: the stronger magnitude, the
        // longer duration, the newer source. Still the right answer wherever
        // the magnitude is not a quantity a player could add up -- a turn
        // cannot be skipped twice, and a mark is spent whole whatever is
        // underneath it.
        Refresh,

        // Add a second entry beside the first, with its own magnitude, its own
        // clock and its own Source. The holder's effect is the SUM of the live
        // entries, and they expire apart rather than together.
        Stack,
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
        // Not readonly, unlike Type: a Refresh-policy status merges a second
        // application into the entry already there (StackingPolicy), and a
        // refresh from a new caster should re-point the credit at whoever most
        // recently paid for it. A Stack-policy status never reaches that
        // branch -- each instance keeps the Source that paid for it, for the
        // life of that instance, which is what lets two casters poison one
        // target and both be paid.
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
