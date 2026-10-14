namespace PrincesPalace.Domain.Content
{
    // How strong a relic is meant to be, and therefore how often it should be
    // offered.
    //
    // ORDER IS LOAD-BEARING and ascending: every draft weight, every "at least
    // this rare" filter and every sort reads the ordinal, so inserting a tier
    // in the middle re-bands every existing relic. Append only.
    //
    // Deliberately NOT the item Quality ladder. A copy's quality is derived
    // from what rolled on it; a relic has no tier and no numbers of its own --
    // its rarity is an authored statement about how rare the OFFER should be.
    // Sharing the type would tie two unrelated ideas together the first time
    // either needed a new band.
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
