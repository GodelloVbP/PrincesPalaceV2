namespace PrincesPalace.Domain.Dungeon
{
    // What a fight node's chip pays on top of its enemies' exp and gold
    // (docs/PLAN_RUN_MAP.md section 2, Prizes). The kind is the generator's;
    // strength scales how much, steeply. Literals from the P0 EV sheet
    // (docs/handoffs/run_map/EV_SHEET.md section 3); item phase I5 redoes
    // them once GoldIncomePermille is set.
    public static class RealmPrize
    {
        // Step-0 gold of a strength-1 gold chip. Raise this, not the
        // multipliers, if gold chips read dull (EV sheet section 3).
        public const int GoldChipBase = 5;

        // x1 / x3 / x6: reward follows risk^1.5 of the median r per band.
        private static readonly int[] GoldMultiplier = { 1, 3, 6 };

        // Potions per consumable chip. Not the sheet's 1 / 2 / 4: a potion
        // costs 15 step-0 gold and heals a flat amount that never scales, so
        // 1 / 2 / 4 paid 15 / 30 / 60 against the gold chip's 5 / 15 / 30.
        // 1 / 2 / 3 keeps every pip worth more than the last and halves the
        // gap at strength 3 (1.5x instead of 2x); one potion is the floor.
        private static readonly int[] ConsumableCount = { 1, 2, 3 };

        // Chip gold is authored net: ScaleReward rides depth, and
        // GoldIncomePermille (item I5) does not touch it.
        public static int GoldFor(int strength, int step) =>
            DifficultyCurve.ScaleReward(GoldChipBase * GoldMultiplier[Band(strength)], step);

        public static int ConsumablesFor(int strength) => ConsumableCount[Band(strength)];

        // A Book or Relic chip whose pool has nothing left to give pays gold
        // instead, and the map card reads the pool live so it says gold from
        // that moment (docs/PLAN_ITEMS.md section 1.1). Gear never empties.
        public static PrizeKind Effective(PrizeKind kind, bool poolHasAny)
        {
            if ((kind == PrizeKind.Book || kind == PrizeKind.Relic) && !poolHasAny) return PrizeKind.Gold;
            return kind;
        }

        // Strength 0 is a node with no frozen entry (NodeStrengthEntry.Find);
        // it pays the strength-1 amount rather than nothing.
        private static int Band(int strength)
        {
            if (strength < NodeStrength.Min) return 0;
            if (strength > NodeStrength.Max) return NodeStrength.Max - 1;
            return strength - 1;
        }
    }
}
