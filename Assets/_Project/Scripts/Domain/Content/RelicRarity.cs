namespace PrincesPalace.Domain.Content
{
    // How strong a relic is meant to be, and therefore how often it should be
    // offered.
    //
    // ORDER IS LOAD-BEARING and ascending: every draft weight, every "at least
    // this rare" filter and every sort reads the ordinal, so inserting a tier
    // in the middle re-bands every existing relic. Append only.
    //
    // Deliberately NOT reusing the item Rarity enum. That one is derived from
    // an item's tier (RarityBands.For) and a tier is a power number a piece of
    // gear scales with; a relic has no tier, no plus, and no numbers at all --
    // its rarity is an authored statement about how rare the OFFER should be,
    // not a computed consequence of its stats. Sharing the type would tie two
    // unrelated ideas together the first time either needed a new band.
    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare,
        UltraRare,
        Mythic,
        Godlike,
    }
}
