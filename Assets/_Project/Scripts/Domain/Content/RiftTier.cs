namespace PrincesPalace.Domain.Content
{
    // The player-facing glow tier: how many modifier slots an item's roll
    // filled, 0-3. Completely independent of `Rarity`/tier above -- a
    // common-tier sword can roll Convergent and a Mythic one can roll
    // Ordinary, because the glow is meant to read the ROLL, not the base
    // item. See ModifierTable.RollRiftTier for how it is rolled, and
    // EquipmentSlotEntry.riftTier / InventoryEntry.riftTier for where a
    // rolled value is persisted (both store it as a plain int so an old
    // save with no such field still deserialises to 0 -- Ordinary -- without
    // a migration step; this enum is the named view of that same range).
    public enum RiftTier
    {
        Ordinary = 0,
        RiftTouched = 1,
        RiftForged = 2,
        Convergent = 3,
    }
}
