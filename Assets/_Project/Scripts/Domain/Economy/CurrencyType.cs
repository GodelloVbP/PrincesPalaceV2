namespace PrincesPalace.Domain.Economy
{
    // The game's three currencies. What each one is FOR, and whether a bad run
    // can take it away from you, is the whole distinction — so it is stated
    // here rather than left to be inferred from where the field happens to sit.
    //
    //   Gold    the run's own currency, and the one thing a run can lose.
    //           Earned from fights and treasure WHILE DELVING, carried in
    //           RunState, and banked onto the save on a voluntary retreat.
    //           A wipe forfeits every unbanked coin, which is what makes
    //           turning back a real decision rather than a formality. Spent
    //           between runs in the Divine Principality.
    //
    //   Embers  the meta currency, and the reason to go deeper. Awarded ONLY
    //           for clearing a floor's boss, written straight onto the save
    //           the moment they drop, and never at risk — a run that ends in
    //           disaster after a boss still keeps what that boss paid. Spent
    //           in the Talent tree to awaken dormant orbs, which is the game's
    //           only permanent power progression.
    //
    //   Relics  dormant. Nothing awards them and nothing sells for them right
    //           now; the type is kept because the concept has a job coming and
    //           deleting it would only mean re-adding it. Do NOT wire this to
    //           a boss drop — that was the old model and Embers replaced it.
    //
    // Gold appears on BOTH wallets, which is deliberate: RunState's copy is the
    // at-risk stake and SaveData's is the banked total, and EndRun moves one
    // into the other. Embers only ever exist on the save.
    public enum CurrencyType
    {
        Gold,
        Embers,
    }

    public static class Currencies
    {
        // True for the currencies a run can never take away.
        //
        // Note this is NOT "lives on SaveData" any more -- Gold lives on both
        // sides. It answers the question that actually matters at the call
        // site: can losing this run cost me this? Embers and Relics are safe
        // the instant they are earned; Gold is a wager until it is banked.
        public static bool IsPersistent(CurrencyType type)
        {
            return type != CurrencyType.Gold;
        }

        public static string DisplayName(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Gold: return "Gold";
                case CurrencyType.Embers: return "Embers";
                default: return type.ToString();
            }
        }
    }
}
