namespace PrincesPalace.Domain.Combat.Session
{
    // Balance constants for a fight, moved out of v1's FightController.
    //
    // In Domain because they are combat rules, not presentation. A cap on how
    // many extra turns a kill can chain into is the same kind of fact as how
    // much armour softens a hit.
    public static class FightTuning
    {
        // How many turns a character can bank by holding back. A finite,
        // slow-to-earn resource: banking costs a real turn, which is why Brave
        // needs no chain cap of its own -- it can never grant more extra turns
        // than were already paid for in skipped ones.
        public const int MaxBankedActions = 2;

        // How many extra turns one Bloodlust chain can produce. A kill DOES
        // need a cap where a banked action does not, because kills are free:
        // without one, a lucky room turns into an unbounded chain.
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

        // ---- role riders -----------------------------------------------------
        //
        // Every role gets one small extra effect on top of Skill's plain
        // damage, so the squad's roles actually play differently in a fight
        // rather than only differing on the Character Sheet.

        public const float AssassinExecuteHealthFraction = 0.3f;
        public const float AssassinExecuteBonusMultiplier = 1.5f;
        public const float TankSkillLifestealFraction = 0.3f;
        public const int CrowdControlDefenseShred = 2;
        public const int SupportSkillPartyHealAmount = 150;
        public const int UtilitySkillSignatureGain = 3;

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
    }
}
