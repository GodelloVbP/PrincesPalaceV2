namespace PrincesPalace.Domain.Combat
{
    // THE ONE VOCABULARY of what a held thing does in a fight: relic stats,
    // relic mechanics, item affixes and the reward track's elemental terms all
    // name a member here. Per-kind facts (stacking, holders, tier scaling,
    // which fields a row reads) live in EffectCatalog, never in a second enum.
    //
    // CLOSED AND APPENDED ONLY. Content assets serialize the member as an int;
    // a member inserted mid-list would turn every authored row after it into
    // its neighbour. A new kind costs a line here, a catalog row and its hook.
    //
    // Mechanics share no numeric shape (a second swing, a consumed ward, an
    // extra turn), so each keeps one C# block at its hook, keyed off
    // CombatantState.Effects.Has; JSON picks the kind and its headline number
    // (Magnitude), and secondary numbers stay in FightTuning.
    public enum EffectKind
    {
        // Never authored. A mistyped name is refused by name rather than
        // landing on whichever member is first.
        None = 0,

        // ---- stats, folded at kit build (EffectFold) ----------------------
        //
        // Percent of the combatant's own figure, summed (not compounded)
        // across holders and applied before the flats -- see EffectFold.Apply.
        // DefensePercent/DefenseFlat reach both broad Defenses.
        AttackPercent,
        DefensePercent,

        // A talent's share (Thick Blood) is folded in ContentDatabase.EffectiveStats
        // so the hub, the run and the fight show one bar; EffectFold skips the
        // Talent holder so it is not counted twice.
        MaxHealthPercent,
        MaxManaPercent,
        SpeedPercent,

        // Flat resistance to the one DamageType the row names, or every
        // non-Physical type for "magical". Flat only: resistance already rides
        // the R/(R+100) curve, so a percent of it would be unreadable.
        ResistanceFlat,

        AttackFlat,
        DefenseFlat,
        MaxHealthFlat,

        // A relic's flat Max Mana and an affix's (Runic) share this member;
        // PoolPrecedence decides whether a pool listens at all.
        MaxManaFlat,
        SpeedFlat,

        // Flat, on the attacker, spent in CombatMath.BroadDefense. Flat only
        // for the same reason as ResistanceFlat.
        ArmorPenetrationFlat,

        // Added to the super-effective multiplier only
        // (CombatMath.EffectivenessMultiplier).
        WeaknessDamageBonusPercent,

        // Heals the hitter for this percent of a landed hit's damage
        // (FightSession.ApplyModifierOnHitRiders).
        LifestealPercent,

        // Crit points on top of CritRules' party baseline and gear stats.
        CritChanceFlat,
        CritDamageFlat,

        // ---- affix and track riders, read at their hooks ------------------

        // Bonus damage of the named element riding a landed hit. Same element
        // as the hit: a percent of the hit itself; another element: a percent
        // of Attack, mitigated as its own typed packet. Every entry fires on
        // its own (FightSession.ApplyModifierOnHitRiders).
        ElementalDamageOnHitPercent,

        // Bonus to a spell PACKET of the named element, read against the
        // packet's own type in ResolveDamageInstances, so a foreign-element
        // spell can be paid. Summed across holders
        // (FightSession.ElementalDamagePercentFor). The reward track routes
        // each element to exactly one of this and the member above.
        ElementalDamagePercent,

        // Flat cut from PHYSICAL damage taken, after the mitigation curve,
        // floored at 1 (DamagePipeline).
        FlatPhysicalDamageReduction,

        // Shrinks incoming BreakShield depletion by this percent
        // (FightSession.DepleteBreakShield); inert on a wearer with no shield.
        BreakShieldDepletionResistPercent,

        // On a kill by the holder, this percent of their Attack splashes onto
        // the victim's neighbours (FightSession.ApplyKillSplash). An affix and
        // Trample's talent are two holders of this one kind and add into one
        // splash.
        OnKillSplashPercent,

        // Percent chance per landed hit to push the target back
        // FightTuning.ModifierPushBackSlots places in turn order.
        PushBackOnHitChancePercent,

        // Flat mana regen per turn, built into the pool (BuildPrimaryPool).
        FlatManaRegenBonus,

        // A plain swing arms a discount of this percent on the wearer's next
        // skill cast; consumed by FightSession.ChargeSkillMana.
        NextSkillManaDiscountPercent,

        // A flag: at the wearer's turn start unspent mana becomes ward at
        // FightTuning.RunicWardConversionRate, a fixed rate on purpose.
        ManaToWardOnTurnStartPercent,

        // Flat Prince's Favor while worn, read live by ItemOfferRoll.FavorOf;
        // nothing stored, so unequipping removes it.
        FortunateFavorBonusFlat,

        // A dodge RATING, not a percent: CombatMath.DodgePercentFrom runs it
        // through 100R/(R+100), so no stack reaches a certain dodge. Rolled in
        // DamagePipeline.AfterDefences, the one funnel every swing and cast
        // shares; splash and DoT ticks bypass that funnel and stay undodgeable.
        DodgeRating,

        // Percent chance per landed hit to chill the target; the chill's size
        // and duration are FightTuning.ChilledOnHit* constants.
        ChilledOnHitChancePercent,

        // Percent chance per landed hit to root the target for
        // FightTuning.RootOnHitTurns.
        RootChancePercent,

        // ---- relic mechanics: one C# block each, at its hook --------------
        //
        // Magnitude is the headline number where a mechanic has one (catalog
        // UsesMagnitude); FightSession reads it through Effects.Magnitude.

        // A plain Attack hits the same target a second time.
        DualWield,

        // After a cast, the caster raises a ward of Magnitude points.
        MagicalShield,

        // The first kill of a fight by the holder grants one extra action
        // (FightSession.TryGrantBloodlust).
        Bloodlust,

        // Every FightTuning.ChargingCrystalEvery-th cast adds Magnitude percent
        // of its OWN BASE, after every multiplier (FightSession.Potency), so it
        // is worth the same against a weak or a resistant target.
        ChargingCrystal,

        // Each plain attack makes the holder Magnitude percent faster for the
        // fight, to FightTuning.SlippersCapPercent.
        BallerinasSlippers,

        // A cast leaves the holder Magnitude percent quicker until their next
        // turn.
        TinFoilPipe,

        // The lower the holder's health, the harder they hit (to Magnitude
        // percent) and the faster they act, at full value at
        // FightTuning.NecklaceFloorHealthPercent.
        ToothedNecklace,

        // A kill pays Magnitude tenths of the victim's exp reward in gold,
        // at least 1.
        BountyHunterContract,

        // After a cast lands on an enemy, one free plain attack follows on it.
        SwordInABox,

        // Each plain attack fires one of three FightTuning.LuckyDeck* effects
        // at random, rolled per swing.
        LuckyDeck,

        // A damaging spell marks its target; a plain attack on the mark spends
        // it for Magnitude percent of the attack's base.
        DrownedLantern,

        // A landed single-target damaging cast lands again as a free copy.
        FirstRune,

        // A plain attack takes Magnitude turns off every cooldown the holder
        // waits on: worthless with no cooldowns, worth more the longer they are.
        SaltLedger,

        // Every FightTuning.LongCountEvery-th plain attack adds Magnitude
        // percent of its own base -- ChargingCrystal's rule, counting swings.
        LongCount,

        // A landed spell marks its target (Marks.Apply); a plain attack on the
        // mark spends it and restores Magnitude percent of the holder's missing
        // primary resource.
        MagicMarker,

        // Every enemy is marked the instant combat begins.
        JarOfBearUrine,

        // When the holder's health crosses below Threshold percent, every enemy
        // is Feared for FightTuning.WorldEndersCrownFearTurns; re-arms above it.
        WorldEndersCrown,

        // Each landed hit stacks a Magnitude percent resistance shred on the
        // target, to FightTuning.CursedIdolMaxStacks stacks.
        CursedIdol,

        // Each non-summon kill grants the whole RUN Magnitude percent damage,
        // read back through FightSession.RunWideBonusDamagePercent.
        AmassingStar,

        // Casting a Transform skill grants Magnitude percent damage reduction.
        // Offered only to a party with a convergence ability.
        RampagingBullsHorn,

        // Each landed attack stacks a FightTuning.IceFingernailPercentPerStack
        // slow on the target, to FightTuning.IceFingernailMaxStacks stacks.
        IceFingernail,

        // Combat opens with one random living enemy stunned (seeded).
        LoadedDice,

        // The holder's plain attacks reach any enemy past the front-rank rule
        // (FightSession.CanReachEnemy); an authored rank restriction stays.
        MonkeyKingsScepter,

        // Choosing to Move grants the MOVER Magnitude percent Speed for a turn.
        SparringSaber,

        // Any action of the holder's that moves someone on the field raises a
        // ward of Magnitude points, once per turn; it pays for the displaced
        // partner too.
        SparringBuckler,

        // After an enemy dies (summons excluded), heal Magnitude percent of max
        // health.
        EssenceSiphon,

        // When an enemy summons, the holder hits the summoner for their own max
        // health.
        DisgruntledLackey,

        // Once per combat, a fallen non-summon enemy strikes another enemy.
        InconspicuousKey,

        // After acting, the holder moves one place forward in the turn order,
        // once per turn.
        DancersAnklet,

        // Taking damage lowers every active cooldown by Magnitude turns, once
        // per turn.
        BerserkersVest,

        // Fatal damage turns the holder into an egg for Magnitude turns; it
        // revives with the share of its pool that survived. Once per combat.
        PhoenixEgg,

        // The first damage packet that reaches the bearer in a fight is turned
        // aside whole, after dodge and ward. Once per fight; never drafted.
        Kinship,

        // Every Transform the holder enters lasts Magnitude turns longer
        // (FightSession.EnterTransform). Wrath's talent and the Bellwether's
        // Bell are two holders of this one kind and add (plan 2.12).
        TransformDurationBonus,


        // On the bearer's 3rd, 6th, 9th... opened turn a ghost flock hits every
        // living enemy for Magnitude percent of the bearer's attack, more if
        // transformed (FightSession.TollOfTheFlock).
        TollOfTheFlock,

        // ═══ Talents ════════════════════════════════════════════════════
        //
        // Held by a character's unlocked talent nodes (EffectHolder.Talent).
        // APPENDED, never inserted: the generated content assets store this enum
        // as an ordinal.

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
        // EffectSet.BestBelowHealth. Handoff §6.1 asked this question
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

        // A kill does not consume the action: you may act again, at most
        // Magnitude extra times per turn. Capped because a chain of kills
        // with no ceiling is unbounded by construction.
        ExtraAttackOnKill,

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
        // specifies "cannot be critically hit below 25% max HP". The spike
        // cap stands in for it and keeps the intent ("the burst that kills you
        // cannot happen") while also covering a telegraphed authored crit.
        // Swapping it for a real crit immunity is a balance decision, not a
        // follow-on of the crit system, and has not been taken.
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
        // something else here (BreakShield is a stagger meter) and a second
        // meaning in this pipeline would produce a real bug. A ward IS a
        // shield in the player's sense (a pool of points);
        // it is still not BreakShield, which is why the two names stay
        // apart.
        //
        // She has no defensive stats. Wool is her armour AND her ammunition,
        // and every point is the same live question: protection, or damage?
        // Weight of Wool pays her to HOLD wards and Shatter pays her to SPEND
        // them; neither is complete alone, and choosing between them turn by
        // turn is the path.

        // ── Fleece Ward: the construct itself ────────────────────────────

        // +Magnitude PERCENT to the size of every Ward this character puts
        // up -- a multiplier on the shield pool the skill's own row authors
        // (SkillResolution.Amount's Ward case), not a pool of its own.
        //
        // THE MEMBER NAME IS KEPT AND THE MEANING IS NOT. It used to BE the
        // ward: "the next hit is Magnitude percent softer", and the larger of
        // talent-or-authored won. Under the shield model (AUDIT #152) a percentage cannot be a shield, so the node scales
        // whatever the skill is worth instead -- which is what finally lets
        // Fleece Ward and Thicker Fleece deepen Tuck In and Bulwark too,
        // rather than being a floor the authored wards stepped over. The id
        // is kept because renaming a talents.json effect type is a content
        // migration for no behavioural gain; the DESCRIPTIONS in talents.json
        // all moved.
        //
        // The engine's once-per-warded-combatant-per-turn cap
        // (WoolWhenWardedAllyHit) is what now stops a ward that absorbs
        // several hits from paying several times -- it used to be enough that
        // a ward popped on the first hit at all, and a pool does not.
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

        // Wards stop timing out. They stand to the end of the fight.
        // Magnitude is ignored.
        //
        // The payoff to the WHOLE path rather than to one strand: Weight of
        // Wool sits at maximum for as long as the pool lasts, Shatter always
        // has ammunition, and Mending Fleece's regen is re-applied every time
        // the ward is. The same shape as the Ram's capstone — take a
        // temporary state and make it who you are — on a different resource.
        //
        // IT MEANT "WARDS STOP POPPING" until the shield model landed (AUDIT
        // #152), and it could not keep meaning that. While
        // a ward was a percentage spent whole by one hit, "never expires" and
        // "never pops" were the same sentence; against a pool of shield
        // points, a ward that never pops is literal immunity to everything
        // forever. So it is the DURATION that is permanent -- ApplyWard is
        // handed StatusEffects.PermanentWardTurns -- and the pool drains like
        // anybody else's. The node's own text in talents.json ("They last the
        // fight") was already the duration reading and needed no change.
        //
        // The engine survives it either way. Income is paid for CARRYING a
        // ward when hit, not for the ward being spent (see
        // WoolWhenWardedAllyHit's once-per-turn cap).
        WardsNeverExpire,

        // ═══ The Einherjar (Bjorn's crit-based damage constellation) ═════
        //
        // docs/PLAN_BJORN_CONSTELLATIONS.md section 3. APPENDED, never
        // inserted: the generated content assets store this enum as an ordinal.
        // Every rule is read at one named seam and switched on at fight start
        // by FightSession.ArmEngineSeams, which is why none of them has to
        // know about the others.

        // The root. Sets CombatantState.FuryEngine to Einherjar: Fury per
        // damaging action from the damage dealt over his Attack, and no flat
        // gain for swinging or being hit. Magnitude is ignored.
        FuryEngineEinherjar,

        // "Slam" below is whichever skill the node names in appliesToSkillId
        // (Bjorn's base Slam), carried on the effect as TalentEffect.SkillId
        // and read through EffectSet.BestFor. The skill is named by
        // content, never inferred from its shape and never spelled in code
        // (docs/CODE_STANDARDS.md section 10), so a second single-target
        // poolTiers skill is not upgraded by these nodes.

        // A Slam that crits restores Magnitude Fury.
        SlamCritRestoresFury,

        // A Slam that fires a Fury tier (50+ Fury) gets +Magnitude percent
        // crit chance.
        SlamCritChanceAtFury,

        // A Slam that fires the full-pool tier (100 Fury) ignores Magnitude
        // percent of the target's defense. Rides the same funnel as
        // IgnoreDefensePercent, for that one cast.
        SlamIgnoresDefenseAtFullFury,

        // A kill refunds Magnitude Fury.
        FuryOnKill,

        // The first idle turn of each fight does not drain Fury. Magnitude is
        // ignored.
        FirstIdleTurnFree,

        // A crit pays Magnitude percent more engine Fury than the hit alone
        // would (Magnitude 50 = one and a half times).
        CritFuryBonusPercent,

        // MOMENTUM, one member for its three nodes because they nest: T1 =
        // Momentum runs (stacks of crit chance), T2 = cap 8 and +5% crit
        // damage per stack, T3 = small hits no longer cost a stack. The
        // highest tier owned wins (EffectSet.Best), which is exactly
        // "owning T3 means owning T1 and T2".
        MomentumTier,

        // +Magnitude percent crit chance against a target at or below
        // Threshold percent of ITS max health (Headsplitter T2).
        CritChanceBelowTargetHealth,

        // A kill made by a finisher (a skill authored with
        // refundsSpentOnKillPercent) fills Momentum to its cap. Magnitude is
        // ignored.
        KillFillsMomentum,

        // +Magnitude percent crit chance while wearing a transformation
        // (Berserk T2).
        CritChanceWhileTransformed,

        // A kill made while transformed adds Magnitude Fury (Berserk T3).
        FuryOnKillWhileTransformed,

        // BATTLE TRANCE, tiered like Momentum: T1 = 20% of each hit is paid
        // in Fury above 50, T2 = 30% and doubled while transformed, T3 = a
        // broken trance grants Protect.
        BattleTranceTier,

        // The capstone: a full-Fury Rampage triggers a second sweep on top
        // (CombatantState.TwinRampage). Magnitude is ignored.
        TwinRampage,

        // ═══ The Juggernaut (Bjorn's low-health bruiser constellation) ═══
        //
        // docs/PLAN_BJORN_CONSTELLATIONS.md section 4. APPENDED, like the
        // Einherjar block above, and armed by FightSession.ArmEngineSeams
        // wherever a rule is a switch on a Domain seam rather than a number
        // one place reads.

        // The root. Sets CombatantState.FuryEngine to Juggernaut: per-turn
        // Fury on the health curve, no income from hits, no idle decay.
        // Magnitude is ignored.
        FuryEngineJuggernaut,

        // SKILL-SCOPED (IsSkillScoped): the holder pays Magnitude turns of
        // cooldown for the named skill instead of the row's own. The
        // Juggernaut root names Second Wind here. Set into
        // CombatantState.CooldownOverrides at fight start.
        SkillCooldownTurns,

        // Regen at the start of the holder's turn that follows the same
        // curve as the Juggernaut's Fury: Threshold percent of max health at
        // full health, rising to Magnitude percent at 25% health or less
        // (HealthCurveRegen). Thick Blood T2 is 6 over 2, T3 is 8 over 2.
        // Threshold is the floor here, not a health gate.
        HealthCurveRegen,

        // Wrath: +Magnitude hundredths of a percent to the holder's damage for
        // each 1% of his own max health that is missing (50 = half a percent
        // per point, so +37 at 25% health). Read by AttackBonusFor beside
        // every other attack bonus, so a swing, a cast and the preview agree.
        DamagePerMissingHealth,

        // SKILL-SCOPED: the named skill heals its caster for Magnitude percent
        // of the damage it deals, when that beats the skill's own authored
        // lifestealPercent. Gorge T2 (40 over the row's 30).
        SkillLifestealPercent,

        // SKILL-SCOPED: the named skill deals Magnitude percent more damage
        // while its caster is at or below Threshold percent of his OWN max
        // health. Gorge T3 (25 under 50).
        SkillDamageBonusBelowOwnHealth,

        // UNYIELDING, one member for its three nodes because they nest: T1 =
        // the passive with a 4-turn cooldown, T2 = cooldown 3, T3 = the trigger
        // also grants 20 Fury. The highest tier owned wins.
        UnyieldingTier,

        // IGNORE PAIN: Magnitude percent of every hit is paid over the next
        // Threshold turns instead of now (CombatantState.DelayedDamage).
        // Threshold is a count of turns here, not a health gate.
        DelayedDamagePercent,

        // Ignore Pain T3: a heal pays down pending delayed damage before it
        // restores health. Magnitude is ignored.
        HealReducesDelayedDamage,

        // BLOOD PRICE T1: skills the primary pool cannot cover are paid with
        // Magnitude thousandths of max health per point of shortfall
        // (CombatantState.ShortfallHealthPermille). 5 = 0.5%.
        ShortfallPaidInHealthPermille,

        // BLOOD PRICE T2: a cast paid partly in health deals Magnitude percent
        // more damage. Read by AttackBonusFor with the cast's own blood-paid
        // fact.
        BloodPaidDamagePercent,

        // BLOOD PRICE T3's second half, beside CheatDeathOncePerFight: when
        // the death save fires, the primary pool fills to its cap. Magnitude
        // is ignored.
        CheatDeathFillsPrimary,

        // ═══ The Sentinel (Bjorn's tank constellation) ═══
        //
        // docs/PLAN_BJORN_CONSTELLATIONS.md section 2. APPENDED, like the two
        // blocks above. The reactive and shield members below are flags or
        // percents that FightSession.ArmSentinelSeams copies onto
        // CombatantState.PlantedShield at fight start; the rest are read where
        // they apply.

        // The root. Sets CombatantState.FuryEngine to Sentinel: Fury per hit
        // taken, on the raw incoming figure. Magnitude is ignored.
        FuryEngineSentinel,

        // Iron Retort: the holder's Physical hits gain flat damage equal to
        // Magnitude percent of the Defense and Magical Defense he has above
        // his own base stats (CombatMath.IronRetortBonus). Elemental
        // resistances do not count.
        IronRetortPercent,

        // The planted shield (CombatantState.PlantedShield): on the
        // convergence, a broken shield's shards hit the attacker whose blow
        // broke it. Magnitude is ignored.
        PlantedShieldBreakShards,

        // Shield Bash T2: the re-place wait after a Bash is the short one.
        // Magnitude is ignored.
        ShieldBashShortWait,

        // Shieldwall (the capstone): the planted shield covers every ally as
        // one pool. Magnitude is ignored.
        ShieldwallCoversParty,

        // Thornwall T1: Magnitude percent of a physical move's damage on the
        // holder or his shield returns to the attacker.
        ThornsPercent,

        // Spellbreaker T1: Magnitude percent of a magic hit on the holder or
        // his shield is reflected to its caster.
        ReflectMagicPercent,

        // Spellbreaker T2: a spell that hits the holder or his shield silences
        // its caster. Magnitude is ignored.
        SilenceCasterOnSpellHit,

        // Spellbreaker T3: a reflection pays the holder Fury. Magnitude is
        // ignored.
        ReflectGrantsFury,

        // Thornwall T2: a physical move that hits the holder or his shield
        // leaves its attacker slowed (Chilled). Magnitude is ignored.
        SlowsAttacker,

        // Thornwall T3: a physical hit that breaks the shield disarms its
        // attacker. Magnitude is ignored.
        DisarmsOnBreak,

        // SKILL-SCOPED: the status the named skill applies lasts Magnitude
        // turns when that beats the row's own statusDuration. Hold the Line T2
        // (2 -> 3).
        SkillStatusTurns,

        // SKILL-SCOPED: casting the named skill also removes Magnitude debuffs
        // from each ally it buffs (StatusEffects.IsDebuff, oldest first). Hold
        // the Line T3 (1).
        SkillCleansesDebuffs,

        // SKILL-SCOPED: a landed hit of the named skill spills Magnitude
        // percent of what it dealt onto the target's neighbours, on the rule
        // Black Ram Mode's splash follows. Shield Bash T3 (100).
        SkillSplashPercent,

        // The holder gains Magnitude Fury (through the primary pool, so an
        // engine does not replace it) each time an ally who carries a status
        // the holder applied is hit. Hold the Line T2 (5).
        PrimaryGainWhenBuffedAllyHit,

        // Bellow T2: enemies choosing whom to hit count the holder Magnitude
        // percent more often in the draw (a standing, weighted taunt; the
        // Provoked status still overrides it). Bellow T2 (200).
        TargetPreferencePercent,

        // Bellow T3: a Provoke cast pays the holder Magnitude Fury for each
        // enemy it provoked. Bellow T3 (10).
        PrimaryGainPerProvokedEnemy,
    }
}
