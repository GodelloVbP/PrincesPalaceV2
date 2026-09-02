namespace PrincesPalace.Domain.Content
{
    // What a relic actually DOES. An enum dispatch rather than a data-driven
    // effect system on purpose: three relics with three unrelated mechanics
    // (a second attack, a consumed damage shield, an extra turn on a kill)
    // have nothing numeric in common to generalize over, and pretending
    // otherwise would be a fake abstraction over three special cases. Each
    // value is handled by its own block in FightController, the same
    // "content concepts belong in Core, not Domain" reasoning CLAUDE.md
    // already gives for role-based Skill effects.
    //
    // No mirror enum on the Content side (contrast ItemKind/ResolvedItemKind,
    // which mirror each other only because Domain cannot see the
    // Content-assembly type) — this enum has no Content-side twin to begin
    // with, so RelicDefinition references it directly.
    public enum RelicEffect
    {
        // NO MECHANIC. A relic that exists as a name, a rarity and an unlock
        // condition, and does nothing in combat yet.
        //
        // This is what makes relics authorable without touching C#. Adding a
        // relic used to require an enum value AND a branch in FightSession
        // before content could so much as name it; with None, a relic is a line
        // of JSON and stays one until somebody decides what it does.
        //
        // The one-relic-per-effect rule in RelicEntryResolver EXEMPTS this
        // value, and has to -- it exists precisely so that many relics can
        // share it. Every other value stays unique for the original reason:
        // two relics with the same real mechanic make one of them a dead
        // choice in a draft.
        //
        // Zero, so a relic entry with no `effect` field at all resolves here
        // rather than accidentally becoming DualWield.
        None = 0,

        // Whenever the character makes a plain Attack, it hits the same
        // target a second time.
        DualWield,

        // After the character casts (either the basic Skill action or a
        // character skill), their next incoming hit is reduced 50%. Refresh-
        // not-stack, same rule StatusEffects.Apply already gives every
        // status — recasting while the shield still stands does not
        // compound it.
        MagicalShield,

        // After the character's action kills an enemy, they get an extra
        // turn (capped — see FightTuning.MaxBloodlustChain).
        Bloodlust,

        // Every 4th cast lands with half its own base potency again on top.
        //
        // ON THE BASE, NOT ON THE TOTAL, and that distinction is the whole
        // design of it. See FightSession.Potency: the bonus is computed from
        // the figure the spell produces on its own and added AFTER everything
        // that multiplies -- so it cannot turn a cast that is already tripled
        // into one that is four and a half times, and it is worth the same
        // whether the target happens to be weak to the element or not.
        ChargingCrystal,

        // Every plain attack makes the wearer permanently faster for the rest
        // of the fight, to a ceiling. Warming up rather than a burst.
        BallerinasSlippers,

        // A cast leaves the wearer quicker until their next turn. The opposite
        // shape to the Slippers: sharp, brief, and it rewards casting rather
        // than swinging.
        TinFoilPipe,

        // The lower the wearer's health, the harder they hit and the faster
        // they act -- reaching full value at a quarter health. The relic that
        // makes being nearly dead a position rather than only a problem.
        ToothedNecklace,

        // Killing something pays, scaling with the level of what died.
        BountyHunterContract,

        // After a cast lands on an enemy, one free plain attack follows on the
        // same target -- a spell, then steel.
        SwordInABox,

        // On every plain attack, one of three effects fires at random: a small
        // heal-and-refill, splash onto every other enemy, or a slow on the
        // target. Re-rolled per swing, so Dual Wield's second hit gets its own
        // independent roll.
        LuckyDeck,

        // A damaging spell marks the target it lands on; a plain attack against
        // a marked target consumes the mark for bonus damage.
        DrownedLantern,

        // After a single-target damaging cast lands, an identical free copy of
        // it lands again on the same target.
        FirstRune,

        // A plain attack takes a turn off everything the actor is waiting on.
        //
        // Worth nothing at all in a kit with no cooldowns, and worth more the
        // longer they are -- which is the point. It is the relic that makes
        // "swing while the big one comes back" a real line of play rather than
        // the thing you do because there is nothing else.
        SaltLedger,

        // Every 3rd plain attack hits for 40% of its own base again.
        //
        // The same rule as ChargingCrystal in every respect except which
        // action it counts, and deliberately so: two relics that count
        // different things should not also disagree about what a percentage
        // means.
        LongCount,

        // ── the balance pass -- see FightSession.BalanceRelics ──────────

        // Every spell the wearer lands marks its target (Marks.Apply).
        // Hitting a marked enemy with a plain ATTACK consumes the mark
        // (Marks.ConsumeMark) and restores 20% of the actor's own missing
        // primary resource. Independent of Drowned Lantern's own bespoke
        // mark -- see Marks' own header.
        MagicMarker,

        // Marks every enemy on the field the instant combat begins.
        JarOfBearUrine,

        // The instant the wearer's health CROSSES below 30%, every enemy on
        // the field is Feared for one turn. Re-arms the moment the wearer
        // goes back above 30% -- see FightSession.BalanceRelics'
        // WorldEndersCrownArmed for the crossing-detection.
        WorldEndersCrown,

        // Every hit the wearer lands on an enemy stacks a 3-turn resistance
        // shred on it (FallingOffStacks), 3% per stack, capped at 15%
        // (mechanic c).
        CursedIdol,

        // Every kill the wearer scores (summons excluded) grants the whole
        // RUN +2% damage for the rest of it (mechanic d), read back by
        // every fight in that run through FightSession.
        // RunWideBonusDamagePercent.
        AmassingStar,

        // Casting a convergence/ultimate ability (a Transform skill) grants
        // 50% damage reduction for 2 turns. Gated on
        // ConvergenceGate.HasConvergenceAbility at draft time (mechanic g)
        // -- offered only to a party that actually has one.
        RampagingBullsHorn,
    }
}
