namespace PrincesPalace.Domain.Dungeon
{
    // What the chip on a fight node pays on top of its enemies' exp and gold
    // (docs/PLAN_RUN_MAP.md section 2, Prizes). An enum so the generator needs
    // no content. Serialized as an int in the saved RealmProfile: append new
    // kinds, never renumber.
    public enum PrizeKind
    {
        // No chip: every non-fight node, and an anchor whose profile pays exp
        // and gold only.
        None = 0,
        Gold = 1,
        Consumable = 2,

        // Shown as "Gear". The name stays Item so saved profiles and the item
        // pipeline keep one word (docs/PLAN_ITEMS.md section 5, amendment 1).
        Item = 3,
        Book = 4,
        Relic = 5,
    }
}
