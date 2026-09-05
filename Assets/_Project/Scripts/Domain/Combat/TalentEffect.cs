using System;

namespace PrincesPalace.Domain.Combat
{
    // What a talent DOES, when what it does is not a number added to a stat.
    //
    // The old talent payload vocabulary — statBonus, abilityScoreBonus,
    // maxManaBonus, skillManaCostReduction, signature*Bonus,
    // grantsStartingItemId — could only ever express "+N of an existing
    // stat". That is exactly why Shawn's 63 nodes had become 63 stat bumps:
    // every node was commensurable with every other one, so there was no
    // build to make, only a total to accumulate (docs/handoffs/
    // shawn_talent_rework, §1). Not one of the 18 Black Ram talents in that
    // handoff fits the old vocabulary, which is why this exists.
    //
    // Lives in Domain/Combat rather than Domain/Content for the same reason
    // StatusEffectType and SkillEffect do: it is a COMBAT rule vocabulary
    // that content happens to author, so content depends on it and not the
    // other way round. Every existing Content→Combat using-directive
    // (ResolvedSkill, ResolvedEnemy, both their resolvers) is that same
    // direction.
    //
    // Deliberately a CLOSED enum rather than a scripting hook or a per-node
    // C# subclass. Every entry here is a rule the combat pipeline reads at
    // one named place, the same way StatusEffectType's six entries each ride
    // one existing hook (see StatusEffect.cs' own header). A talent that
    // needs a genuinely new hook needs a line here AND the hook — which is
    // the point: the cost of a new rule stays visible instead of hiding
    // inside content.
    //
    // Magnitude and Threshold are both plain ints, and Threshold is always
    // "percent of max health" where it is used at all. Two fields, not a bag
    // of named optionals, because every rule below needs at most "how much"
    // and "from what health". Anything needing a third number is a sign the
    // rule wants splitting in two, not that this struct wants a fourth field.
    public enum TalentEffectType
    {
        // Never authored. Exists so a mistyped effect name resolves to
        // something the resolver can reject by name rather than silently
        // landing on whichever member happens to be first.
        None = 0,

        // ── The engines: one per path root, mutually exclusive ────────────
        //
        // These are what a root orb IS. Free, one at a time across all three
        // paths (ContentDatabase.AllegianceRootOf), and the reason the rest
        // of the tree can be bought across paths without the economy
        // collapsing: three generation rules stacking would produce ~+4-5
        // wool a turn against abilities priced at 7-8.

        // Magnitude wool per HIT taken — not per point of damage, so a swarm
        // of weak attackers is not a better generator than one real threat.
        WoolOnHitTaken,

        // Magnitude wool at the start of your turn while at or below
        // Threshold percent of max health. REPLACES the baseline rather than
        // stacking with it, and the highest satisfied tier wins — see
        // TalentEffectSet.BestBelowHealth. Handoff §6.1 asked this question
        // and never answered it; replace is what the ability prices were
        // sanity-checked against, so replace is what this is.
        WoolPerTurnBelowHealth,

        // Magnitude wool when a combatant carrying a WARD you applied takes a
        // hit. The setup step is the whole point: you ward the tank, the tank
        // eats a hit, you get paid — which is what makes the Lamb a different
        // tree from the Ram rather than a second damage-taken engine.
        //
        // NARROWED to StatusEffectType.Shielded specifically. It used to read
        // "any status you applied", and that had to change before Wail could
        // exist: Wail is party-wide Protect sourced by the caster, so a broad
        // trigger would make every Wail an income engine paying up to +6 a
        // turn for two turns on top of whatever the wards were already
        // earning. The ward IS the setup step this rule exists to reward; a
        // Regen tick is not.
        //
        // CAPPED at once per combatant per turn, in FightController — a ward
        // that paid on every hit it absorbed would return more wool than it
        // cost and the economy would run backwards.
        WoolWhenWardedAllyHit,

        // Magnitude wool at the start of your turn per enemy carrying a
        // status YOU applied. Paid for board state rather than for damage in
        // either direction — spread wide early, cash in late.
        WoolPerStatusedEnemy,

        // ── Sharp Horns: armour penetration ──────────────────────────────

        // Your attacks ignore Magnitude percent of the target's defense.
        IgnoreDefensePercent,

        // Enemies you hit permanently lose Magnitude defense for the rest of
        // the fight, stacking. Turns a selfish stat into a party
        // contribution — you soften the target, everyone benefits.
        ShredDefenseOnHit,

        // ── Provoke: taunt, mitigation, and the income engine ────────────

        // An enemy you have provoked deals Magnitude percent less damage to
        // you specifically.
        ProvokedDamageReductionPercent,

        // Provoke affects EVERY enemy rather than one. Magnitude is ignored.
        ProvokeHitsEveryEnemy,

        // Magnitude wool at the start of your turn per enemy you currently
        // have provoked. Deliberately arrives at the top of the Provoke
        // strand, which is roughly where the convergence unlocks — the income
        // engine comes fully online exactly when there is something worth
        // spending it on.
        WoolPerProvokedEnemy,

        // ── Trample: execute and splash ──────────────────────────────────

        // +Magnitude percent damage against a target at or below Threshold
        // percent of ITS max health.
        ExecuteDamageBonusPercent,

        // Killing an enemy splashes Magnitude percent of your Attack onto the
        // enemies either side of it.
        KillSplashPercentOfAttack,

        // A kill does not consume the action: you may act again, at most
        // Magnitude extra times per turn. Capped because a chain of kills
        // with no ceiling is unbounded by construction, the same reason
        // FightController.MaxBloodlustChain exists.
        ExtraAttackOnKill,

        // ── Wrath: the transform as a juggernaut ─────────────────────────

        // The transform lasts Magnitude turns longer.
        TransformDurationBonus,

        // A kill during the transform extends it by Magnitude turns, to a
        // total extension of at most Threshold turns.
        TransformExtendOnKill,

        // The transform does not expire at all while you are at or below
        // Threshold percent of max health. Played correctly, the transform
        // stops being a timer — which is what makes this limb the juggernaut.
        TransformHoldsBelowHealth,

        // ── Last Stand: the safety net ───────────────────────────────────

        // +Magnitude percent defense while at or below Threshold percent of
        // max health.
        DefenseBonusPercentBelowHealth,

        // No single hit may take more than Magnitude percent of your max
        // health while you are at or below Threshold percent of it.
        //
        // This is a SUBSTITUTION, recorded here rather than only in a commit
        // message because content authors read this file. The handoff (§6.3)
        // specifies "cannot be critically hit below 25% max HP" — and this
        // game has no critical hits, in any file, so that node would have
        // been inert content the resolver could not tell from a typo. A spike
        // cap keeps the intent exactly ("the burst that kills you cannot
        // happen") on a hook that exists today. If crits land later this is
        // the node to revisit.
        DamageCapPercentBelowHealth,

        // Once per fight, a hit that would reduce you to 0 leaves you at 1.
        // Magnitude is ignored. A tree built on being wounded needs a safety
        // net or nobody commits to it.
        CheatDeathOncePerFight,

        // ── Charge: the turn queue ───────────────────────────────────────

        // Headbutt's queue push also cancels the target's telegraphed intent
        // — they lose the action outright. Magnitude is ignored.
        HeadbuttCancelsIntent,

        // Entering the transform pushes EVERY enemy back in the turn queue.
        // Magnitude is ignored. Note this also fires inside a fusion window,
        // whenever fusion lands (handoff §8).
        TransformPushesEveryEnemy,

        // ── The capstone ─────────────────────────────────────────────────

        // At or below Threshold percent of max health the transform is
        // permanent and costs nothing: it stops being a button and becomes
        // who he is.
        TransformPermanentBelowHealth,

        // ═══ The Fragile Lamb ════════════════════════════════════════════
        //
        // Her whole path is built on ONE construct: the Ward. It is
        // implemented with StatusEffectType.Shielded and it is called a Ward
        // everywhere — content, code and UI — because "shield" already means
        // two other things here (BreakShield is a stagger meter, Shielded is
        // the Magical Shield relic's per-hit reduction) and a third meaning
        // in this pipeline would produce a real bug.
        //
        // She has no defensive stats. Wool is her armour AND her ammunition,
        // and every point is the same live question: protection, or damage?
        // Weight of Wool pays her to HOLD wards and Shatter pays her to SPEND
        // them; neither is complete alone, and choosing between them turn by
        // turn is the path.

        // ── Fleece Ward: the construct itself ────────────────────────────

        // A Ward reduces its wearer's next hit by Magnitude percent, then is
        // spent. POPPING ON THE FIRST HIT IS LOAD-BEARING, not incidental: a
        // ward that absorbed several hits would pay the engine each time and
        // return more wool than it cost.
        WardReductionPercent,

        // Warding no longer consumes the action, so she can ward and attack
        // in the same turn. Magnitude is ignored.
        //
        // The single biggest quality-of-life node in the path. Before it,
        // every ward is a turn not spent doing anything else — which on a
        // frail character with no damage output feels like being punished for
        // playing her correctly.
        WardIsFreeAction,

        // Applying a Ward also applies Regen for Threshold TURNS, healing
        // Magnitude percent of the wearer's max health each turn.
        //
        // ⚠ THE SECOND EXCEPTION to "Threshold is percent of max health" —
        // here it is a turn count, as it is on TransformExtendOnKill. The
        // handoff's rule is that a THIRD exception means splitting the field
        // rather than documenting another one; The Flock avoided being that
        // third by splitting its RULE instead (see WardSpreadsToWholeParty).
        WardAlsoAppliesRegen,

        // When a Ward is SPENT, its wearer heals Magnitude percent of their
        // max health.
        //
        // Inverts how losing a ward feels. Until this node a popped ward is a
        // resource gone; after it, the ward breaking is itself the payout —
        // which matters because everything else in this path wants wards to
        // break.
        WardHealsWhenSpent,

        // ── Weight of Wool: damage from wards held ───────────────────────

        // +Magnitude percent attack damage per OTHER warded party member.
        WardDamageBonusPerAlly,

        // +Magnitude percent attack damage while YOU are warded.
        //
        // Two members rather than one with a Threshold, deliberately. "Half a
        // multiplier per ally and two on yourself" is two independent
        // numbers, and packing them into Magnitude and Threshold would
        // overload Threshold with yet another meaning. Two members read
        // correctly and compose additively without argument.
        WardDamageBonusSelf,

        // The self bonus survives Magnitude turns after your own Ward is
        // spent.
        //
        // Without it the largest damage multiplier in the tree evaporates to
        // the first stray attack, and the build reads as a trap rather than
        // as a gamble.
        WardSelfBonusPersistsTurns,

        // ── The Flock: everything applies to everyone ────────────────────

        // A Ward also lands on other party members, at Magnitude percent of
        // its strength. One extra ally by default; the whole party once
        // WardSpreadsToWholeParty is also held.
        WardSpreadsToAllies,

        // The spread covers the entire party rather than one extra ally.
        // Magnitude is ignored.
        //
        // A FLAG rather than a count on WardSpreadsToAllies' Threshold, and
        // this is the handoff's own convention #1 in action: the alternative
        // was Threshold meaning "number of extra allies, 0 = all", which
        // would have been a third distinct meaning for that field on top of
        // health-percent and turns. Splitting the rule was the cheaper half
        // of the same fix, and a two-valued thing is what a flag is for.
        WardSpreadsToWholeParty,

        // ── Wool Gift: spend your resource on someone else's turn ────────

        // Gift: Mana restores Magnitude percent of an ally's maximum mana.
        GiftManaPercent,

        // Gift: Fury makes an ally's next attack deal Magnitude percent more.
        GiftAttackBonusPercent,

        // Gift: Haste pulls an ally to the front of the turn queue.
        // Magnitude is ignored.
        //
        // The mirror of the Ram's Charge strand: he pushes enemies back, she
        // pulls allies forward. Both land on the same turn queue, and the two
        // together are the most legible statement this game makes about its
        // turn order being a thing you can manipulate.
        GiftAppliesImmediateTurn,

        // ── Shatter: spend the wards for burst ───────────────────────────

        // Detonating a Ward deals Magnitude percent of your Attack to a
        // random enemy.
        ShatterDamagePercentOfAttack,

        // Detonating YOUR OWN Ward deals Magnitude percent of the usual
        // figure — 300 for triple.
        //
        // The best decision in the path. Your own Ward is at once the largest
        // damage multiplier in the tree and the only protection a character
        // with no defensive stats has; shattering it is a burst that leaves
        // her naked. Deliberately undiluted — no compensating rider, no
        // smaller ward handed back.
        ShatterSelfWardMultiplier,

        // A shattered Ward also applies Vulnerable to whatever it hit.
        // Magnitude is ignored.
        ShatterAppliesVulnerable,

        // ── The capstone: The Golden Fleece ──────────────────────────────

        // Wards stop popping. They persist to the end of the fight.
        // Magnitude is ignored.
        //
        // The payoff to the WHOLE path rather than to one strand: Weight of
        // Wool sits permanently at maximum, Shatter always has full
        // ammunition, and Mending Fleece's regen never lapses. The same shape
        // as the Ram's capstone — take a temporary state and make it who you
        // are — on a different resource.
        //
        // The engine survives it. Income is paid for CARRYING a ward when
        // hit, not for the ward being spent (see WoolWhenWardedAllyHit's
        // once-per-turn cap), so a ward that never pops still pays once per
        // combatant per turn rather than never again. That was the handoff's
        // own stated fallback if income collapsed; it costs nothing to have
        // built it that way from the start.
        WardsNeverExpire,
    }

    // One rule a talent grants. Engine-free; the combat pipeline reads these
    // through TalentEffectSet, never one at a time.
    //
    // [Serializable] with public fields because ResolvedTalent is what the
    // TalentDefinition asset now stores. TalentDefinition used to carry a
    // parallel TalentEffectEntry carrier for exactly this reason -- "Unity
    // will not serialise a readonly struct's fields" -- with a Select in each
    // direction. Telling System.Serializable about the struct is cheaper than
    // maintaining its mirror, and System.Serializable is BCL, so Domain stays
    // engine-free. Immutable by convention, like every other DTO here.
    [Serializable]
    public struct TalentEffect
    {
        // Authored as the enum MEMBER NAME in talents.json
        // ("IgnoreDefensePercent"), not as an integer — a number in content
        // is unreadable and, worse, silently re-points at a different rule
        // the moment anyone inserts an enum member. JsonUtility cannot
        // deserialise an enum from a string, so the raw shape carries the
        // name and TalentEntryResolver parses it.
        public TalentEffectType Type;

        // How much. Percent for every *Percent member, otherwise a flat
        // count (wool, defense points, turns). Ignored outright by the four
        // flag-shaped members, which say so in their own comments.
        public int Magnitude;

        // The rule's SECOND number, and 0 for the majority that need only
        // one. For all seven health-gated members it is a percent of max
        // health the holder (or, for ExecuteDamageBonusPercent, the target)
        // must be at or below. TransformExtendOnKill is the one exception and
        // reads it as a cap in turns — a second number that is not a health
        // gate. Which of the two a member means is stated in its own comment
        // above and enforced by TalentEntryResolver.
        public int Threshold;

        public TalentEffect(TalentEffectType type, int magnitude, int threshold = 0)
        {
            Type = type;
            Magnitude = magnitude;
            Threshold = threshold;
        }
    }
}
