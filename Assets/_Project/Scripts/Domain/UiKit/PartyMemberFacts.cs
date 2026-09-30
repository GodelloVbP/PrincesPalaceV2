namespace PrincesPalace.Domain.UiKit
{
    // What decides where a character should stand, as plain values (plan C22):
    // how hurt they are and how well they take a blow. Core reads the pieces
    // off the save, the run and the fight (PartyController.FactsFor) and this
    // file owns which source wins in which context, so a seat and the carry
    // preview cannot disagree.
    public readonly struct PartyMemberFacts
    {
        public readonly int Health;
        public readonly int MaxHealth;
        public readonly int PhysicalDefense;
        public readonly int MagicalDefense;

        public PartyMemberFacts(int health, int maxHealth, int physicalDefense, int magicalDefense)
        {
            MaxHealth = maxHealth < 0 ? 0 : maxHealth;
            Health = health < 0 ? 0 : (health > MaxHealth ? MaxHealth : health);
            PhysicalDefense = physicalDefense;
            MagicalDefense = magicalDefense;
        }

        // 0..1 for the bar; a character with no maximum reads empty.
        public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;

        // C4's three sources, one per context:
        //  Camp     nobody is hurt between runs: full.
        //  Descent  the health the run carries between fights; a character the
        //           run has no entry for is at full (EncounterRoll reads the same).
        //  Fight    what the HUD is showing (the fight's facts), and its stat
        //           deltas on the defences. The run's carried figure is written
        //           only when the fight ends, so it is stale for the whole
        //           fight. A member the fight is not showing (benched, or
        //           carried at zero and never fielded) took no part in it, so
        //           the carried figure is not stale for them.
        //
        // `maxHealth` and the defences are the character's own effective stats;
        // `carriedHealth` is null when the run holds no entry; `fight` is null
        // outside a fight or for a member the fight is not showing.
        public static PartyMemberFacts Resolve(MenuContext context, int maxHealth, int physicalDefense,
                                               int magicalDefense, int? carriedHealth, FightMemberFacts fight)
        {
            if (context == MenuContext.Fight && fight != null)
            {
                return new PartyMemberFacts(
                    fight.Health,
                    fight.MaxHealth,
                    Floor(physicalDefense + fight.DeltaFor(SheetStat.PhysicalDefense)),
                    Floor(magicalDefense + fight.DeltaFor(SheetStat.MagicalDefense)));
            }

            int health = maxHealth;
            if (context != MenuContext.Camp && carriedHealth.HasValue) health = carriedHealth.Value;

            return new PartyMemberFacts(health, maxHealth, physicalDefense, magicalDefense);
        }

        private static int Floor(int value) => value < 0 ? 0 : value;
    }
}
