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

        public readonly int Cost;
        public readonly int SortOrder;

        // Editor-time path to this exact tier's art, or empty when the piece
        // has no sheet. Resolved here rather than at the Unity end so the
        // tier-to-level mapping is one engine-free function with tests rather
        // than an expression buried in ContentBuilder.
        public readonly string IconPath;

        // The base this tier was generated from (docs/PLAN_ITEMS.md 2.3):
        // the id a copy will carry once content holds one asset per base.
        public readonly string BaseId;

        // Every tier of the base, as one curve; what this tier grants is Curve.At(Tier).
        public readonly GearCurve Curve;

        // What this tier grants, read off the curve: the one source.
        public GearStats At => Curve.At(Tier);

        public ResolvedSetPiece(string id, string displayName, string description, EquipmentSlot slot,
            string setId, string setDisplayName, string pieceId, int tier,
            int cost, int sortOrder, string iconPath = "",
            string baseId = "", GearCurve curve = default)
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
            Cost = cost;
            SortOrder = sortOrder;
            BaseId = baseId ?? "";
            Curve = curve;
        }
    }
}
