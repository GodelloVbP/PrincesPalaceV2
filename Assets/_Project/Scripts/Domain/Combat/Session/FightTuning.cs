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
    }
}
