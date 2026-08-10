using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One generated armour piece at one TIER — the shape ContentBuilder
    // turns into an ItemDefinition asset.
    //
    // Tier is which coif this is: its icon, its base stats, its rarity band.
    // It is baked, one asset per tier. The other axis a player sees, PLUS,
    // is deliberately not here — plus lives on the item instance (see
    // InventoryEntry) so honing a piece does not need an asset of its own.
    // Eleven tiers times eleven pluses would be 1,815 armour assets; eleven
    // tiers is 165.
    //
    // Deliberately a finished item rather than a recipe. All the set's
    // cleverness (interpolation, naming, costing) happens in the resolver, so
    // by the time anything Unity-side sees a piece it is indistinguishable
    // from a hand-authored item. That is what lets equipment, the inventory,
    // the paperdoll and the save file stay completely unaware that sets exist.
    public readonly struct ResolvedSetPiece
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly EquipmentSlot Slot;

        // Which set this came from, kept so a future set BONUS ("wearing four
        // pieces of steel grants...") has something to group on without
        // re-parsing item ids.
        public readonly string SetId;
        public readonly string SetDisplayName;
        public readonly string PieceId;
        public readonly int Tier;

        public readonly StatBlock StatBonus;
        public readonly AbilityScoreBlock AbilityScoreBonus;
        public readonly int Cost;
        public readonly int SortOrder;

        // Editor-time path to this exact tier's art, or empty when the piece
        // has no sheet. Resolved here rather than at the Unity end so the
        // tier-to-level mapping is one engine-free function with tests rather
        // than an expression buried in ContentBuilder.
        public readonly string IconPath;

        // What a character needs, from everything ELSE worn plus base scores
        // and talents, before this piece counts as worn — see
        // RequirementResolver.
        public readonly AbilityScoreBlock Requirements;

        public ResolvedSetPiece(string id, string displayName, string description, EquipmentSlot slot,
            string setId, string setDisplayName, string pieceId, int tier,
            StatBlock statBonus, AbilityScoreBlock abilityScoreBonus, int cost, int sortOrder,
            string iconPath = "", AbilityScoreBlock requirements = default)
        {
            IconPath = iconPath;
            Id = id;
            DisplayName = displayName;
            Description = description;
            Slot = slot;
            SetId = setId;
            SetDisplayName = setDisplayName;
            PieceId = pieceId;
            Tier = tier;
            StatBonus = statBonus;
            AbilityScoreBonus = abilityScoreBonus;
            Cost = cost;
            SortOrder = sortOrder;
            Requirements = requirements;
        }
    }
}
