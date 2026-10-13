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

        // On a kill by the wearer, this percent of their Attack splashes onto
        // the victim's neighbours (FightSession.ApplyModifierKillSplash), apart
        // from a talent's own splash.
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

        // Every Transform the bearer enters lasts Magnitude turns longer.
        BellwethersBell,

        // On the bearer's 3rd, 6th, 9th... opened turn a ghost flock hits every
        // living enemy for Magnitude percent of the bearer's attack, more if
        // transformed (FightSession.TollOfTheFlock).
        TollOfTheFlock,
    }
}
