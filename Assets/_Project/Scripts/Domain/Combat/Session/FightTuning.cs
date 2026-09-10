namespace PrincesPalace.Domain.Combat.Session
{
    // Balance constants for a fight, moved out of v1's FightController.
    //
    // In Domain because they are combat rules, not presentation. A cap on how
    // many extra turns a kill can chain into is the same kind of fact as how
    // much armour softens a hit.
    public static class FightTuning
    {
        // How many extra turns one Bloodlust chain can produce. Kills are
        // free, so without a cap a lucky room turns into an unbounded chain.
        public const int MaxBloodlustChain = 2;

        // ---- counting relics -----------------------------------------------------
        //
        // "Every Nth" is counted PER FIGHT and per character, not per run: a
        // counter carried between fights would make the first cast of a fight
        // arbitrarily lucky depending on how the last one ended, which is not a
        // thing a player can plan around.
        //
        // The percentages are of the action's OWN BASE. See FightSession.Potency.
        public const int ChargingCrystalEvery = 4;
        public const int ChargingCrystalPercent = 50;

        // How many turns a swing takes off, for the Salt Ledger. One, and it
        // is a constant rather than a literal because the relic's whole value
        // is a ratio against the cooldowns it shortens -- retuning either
        // without seeing the other is how a relic becomes mandatory.
        public const int SaltLedgerTurns = 1;

        // ---- speed relics --------------------------------------------------------
        //
        // The Slippers accumulate and the Pipe does not, which is the whole
        // difference between them: one rewards a long fight of swinging, the
        // other rewards casting at the right moment. Same stat, opposite shape.
        public const int SlippersPercentPerSwing = 10;
        public const int SlippersCapPercent = 40;

        public const int PipePercent = 20;
        public const int PipeTurns = 1;

        // ---- the necklace --------------------------------------------------------
        //
        // Full value at a quarter health, nothing at full, and a straight ramp
        // between. The floor is a QUARTER rather than zero because a bonus that
        // only pays at 1hp pays on the turn you die.
        public const int NecklaceMaxDamagePercent = 30;
        public const int NecklaceMaxSpeedPercent = 20;
        public const int NecklaceFloorHealthPercent = 25;

        // What a kill pays, per level of whatever died.
        public const int BountyPerLevel = 3;

        // ---- lucky deck ------------------------------------------------------------
        //
        // Three effects, one roll per swing, equal odds -- the relic's whole
        // appeal is not knowing which one you get.
        public const int LuckyDeckHealHealthPercent = 5;
        public const int LuckyDeckHealManaPercent = 10;
        public const int LuckyDeckSplashPercent = 25;
        public const int LuckyDeckSlowPercent = 30;
        public const int LuckyDeckSlowTurns = 1;

        // ---- the drowned lantern's mark ---------------------------------------------
        //
        // Half the swing's own base again -- a meaningful payoff, because it
        // costs a whole cast to set up before an attack can cash it in.
        public const int MarkBonusPercent = 50;

        public const int LongCountEvery = 3;
        public const int LongCountPercent = 40;

        // ---- relics ----------------------------------------------------------

        // 99 turns is "for the rest of the fight" spelled as a duration. The
        // shield is refreshed rather than stacked (StatusEffects.Apply's own
        // rule), so recasting while it stands never compounds.
        public const int MagicalShieldReductionPercent = 50;
        public const int MagicalShieldDurationTurns = 99;

        // ---- item modifiers (Rift affixes, Phase C) ---------------------------

        // How many turn-order slots Hardened's on-hit push knocks a target
        // back -- the same unit TurnOrder.PushBack itself takes. One,
        // matching the Black Ram's Headbutt (the talent this reuses the
        // mechanism from): "one slot later" needs no calibration against the
        // scheduler's own arbitrary charge units (see PushBack's own header).
        public const int ModifierPushBackSlots = 1;

        // RUNIC'S MANA->WARD CONVERSION IS DELIBERATELY WEAK -- the plan's
        // own words for it. It turns a resource the wearer was merely
        // HOLDING (not spent, not committed to anything) into flat damage
        // reduction at the start of every one of their own turns, for free.
        // A generous rate here would make "hoard mana, never cast" the
        // correct answer to "how do I tank", which is backwards for a
        // resource whose entire other purpose is being spent. Percent Ward
        // granted per point of UNSPENT mana at turn start.
        public const float RunicWardConversionRate = 0.25f;

        // However deep a mana pool gets, the conversion never grants more
        // than a quarter damage reduction -- keeps a high-mana build from
        // turning "never cast" into near-immunity.
        public const int RunicWardMagnitudeCapPercent = 25;

        // ---- chilled (Phase D2, item-modifier plan) ----------------------------
        //
        // Frosty's own chill, on a successful ChilledOnHitChancePercent
        // proc. NOT authored per-modifier -- see that enum member's own
        // comment for why the chance is the one number modifiers.json
        // tunes and this pair stays a fixed constant, the same split
        // ModifierPushBackSlots already draws for Hardened's push.
        //
        // Deliberately its OWN numbers rather than reusing
        // LuckyDeckSlowPercent/LuckyDeckSlowTurns even though both procs
        // now land through the identical ApplyChilled call -- Frosty is a
        // droppable item modifier balanced against RiftTier/item tier the
        // way every other on-hit rider in this table is (Vampiric's
        // LifestealPercent, Hardened's push chance), Lucky Deck is a fixed
        // relic with its own long-shipped balance; tying the two together
        // would mean retuning one every time the other needed to move.
        public const int ChilledOnHitSpeedPercent = 20;
        public const int ChilledOnHitTurns = 2;

        // ---- rooted (Phase D3, item-modifier plan) -----------------------------
        //
        // Sylvan's own root, on a successful RootChancePercent proc. Same
        // split as ChilledOnHitSpeedPercent/ChilledOnHitTurns just above:
        // the CHANCE is the one number modifiers.json tunes, this stays a
        // fixed constant. No sibling "strength" constant -- Rooted has no
        // Magnitude of its own to author (ModifierEffectType.RootChancePercent's
        // own comment), only a duration.
        public const int RootOnHitTurns = 2;

        // ---- the balance pass -- mechanics a-g, and the relics built on them ----

        // Magic Marker: consuming a mark restores this percent of the
        // actor's own MISSING primary resource.
        public const int MagicMarkerRestorePercent = 20;

        // World Ender's Crown: crossing below this fraction of health fears
        // every enemy for FEARED turns.
        public const float WorldEndersCrownHealthFraction = 0.3f;
        public const int WorldEndersCrownFearTurns = 1;

        // Cursed Idol: each landed hit on an enemy stacks a resistance
        // shred, 3% per stack, up to 5 stacks (15%), each stack falling off
        // 3 turns after it was added.
        public const int CursedIdolPercentPerStack = 3;
        public const int CursedIdolMaxStacks = 5;
        public const int CursedIdolStackTurns = 3;
        public const string CursedIdolStackKey = "cursed_idol";

        // Amassing Star: percent damage added to the WHOLE REST OF THE RUN
        // per non-summon kill.
        public const int AmassingStarPercentPerKill = 2;

        // Pointy Nail on the End of a Stick: flat armour penetration on
        // every melee (Physical-typed) swing the wearer makes.
        public const int PointyNailArmorPenetration = 35;

        // Rampaging Bull's Horn: damage reduction after casting a
        // convergence/ultimate ability, and how long it lasts.
        public const int BullsHornReductionPercent = 50;
        public const int BullsHornDurationTurns = 2;

        // ---- balance pass 2 -- see FightSession.BalanceRelics2 ----------------

        // Ice Fingernail: 10% slow per stack (FallingOffStacks), up to 4
        // stacks (40%), each stack falling off 3 turns after it lands.
        public const int IceFingernailPercentPerStack = 10;
        public const int IceFingernailMaxStacks = 4;

        // Longer than Cursed Idol's own 3 -- a slow that fell off after
        // three of the TARGET's own turns would rarely hold more than one
        // or two stacks at once against an attacker swinging every one of
        // its own turns, since a fresh stack and a tick both land once per
        // attack/reply cycle. Five turns is what actually lets four stacks
        // (the stated cap) coexist under ordinary 1:1 swinging.
        public const int IceFingernailStackTurns = 5;
        public const string IceFingernailStackKey = "ice_fingernail";

        // Sparring Saber: choosing to Move grants this much Speed for one
        // turn.
        public const int SparringSaberSpeedPercent = 30;
        public const int SparringSaberSpeedTurns = 1;

        // Sparring Buckler: the ward's own strength, as a percent reduction
        // on the wearer's next hit -- see FightSession.RelicMechanics.
        // NoteDeliberateMove for the "ward" reading taken (this game's
        // existing Shielded status, not a flat absorb pool).
        public const int SparringBucklerWardPercent = 15;

        // Essence Siphon: percent of max health healed per non-summon kill.
        public const int EssenceSiphonHealPercent = 3;

        // Berserker's Vest: cooldown turns shaved off per hit taken, once
        // per turn.
        public const int BerserkersVestCooldownReduction = 1;

        // Phoenix Egg: how many of the wearer's own turns the shell lasts.
        public const int PhoenixEggDurationTurns = 3;

        // ---- combat-lock keys (mechanic f) --------------------------------------
        //
        // BARE NAMES NOW, and the OWNER is passed to CombatLocks separately.
        // These used to be `...LockKeyFor(string combatantId)` helpers that
        // built "name:" + the LEDGER id, because CombatLocks was one flat set
        // of strings and the owner had nowhere else to go. The ledger id was
        // the wrong thing to hand it -- it groups every combatant of one enemy
        // type under one row on purpose -- and the suffix convention that made
        // it work was a convention rather than a rule. See CombatLocks' own
        // header for both halves. What is left here is what these constants
        // always were: the NAME of the gate.
        public const string PhoenixEggLockKey = "phoenix_egg";
        public const string BerserkersVestLockKey = "berserkers_vest";
        public const string SparringBucklerLockKey = "sparring_buckler";
        public const string DancersAnkletLockKey = "dancers_anklet";
        public const string InconspicuousKeyLockKey = "inconspicuous_key";

        // The PRIMARY pool's gainOnAttack, which fires from the damage funnel
        // and therefore needs a "once per damaging action" boundary the funnel
        // itself cannot supply: it runs once per TARGET, so a three-enemy
        // sweep would otherwise pay three times.
        //
        // Once per TURN is the same boundary here, and not an approximation of
        // it: an action ends the actor's turn, so the only way to act twice in
        // one turn is to have been granted an extra one -- and an extra turn
        // runs GrantTurnStart, which resets this lock, which is exactly the
        // answer a Trample chain should get. Dual Wield's second swing shares
        // the turn and so grants nothing extra, matching the rule the
        // signature pool has always had at the Attack verb.
        //
        // THE ONE ENEMY-SIDE LOCK IN THE GAME, which is what made the ledger-id
        // owner a live hazard rather than a tidy one: three rats shared it.
        // Inert only because every shipped enemy authors gainOnAttack 0.
        public const string PrimaryPoolAttackGainLockKey = "primary_pool_attack_gain";
    }
}
