using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One generated weapon: one family, one TIER — the shape ContentBuilder
    // turns into an ItemDefinition asset.
    //
    // Tier is which sword this is: its attack, its scaling grade, its tier
    // adjective, its rarity band. The player-facing PLUS is a separate axis
    // carried on the item instance rather than baked here, so a +3 sword and
    // a +0 one of the same tier are one asset, not two. Item-modifier plan
    // Phase B collapsed the second (modifier) axis this struct used to carry
    // — see RawWeaponEntry's own header — so a family generates one weapon
    // per tier now, not one per modifier per tier.
    //
    // Finished rather than a recipe, for the same reason ResolvedSetPiece is:
    // by the time anything Unity-side sees it, a generated weapon is
    // indistinguishable from a hand-authored one, so equipment, the bag, the
    // paperdoll and the save file never learn that families exist.
    public readonly struct ResolvedWeapon
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly EquipmentSlot Slot;

        public readonly string FamilyId;
        public readonly int Tier;

        public readonly int Cost;
        public readonly int SortOrder;
        public readonly string IconPath;

        // The base this tier was generated from (docs/PLAN_ITEMS.md 2.3):
        // the id a copy will carry once content holds one asset per base.
        public readonly string BaseId;

        // Every tier of the base, as one curve; what this tier grants is Curve.At(Tier).
        public readonly GearCurve Curve;

        // What this tier grants, read off the curve: the one source.
        public GearStats At => Curve.At(Tier);

        public ResolvedWeapon(string id, string displayName, string description, EquipmentSlot slot,
            string familyId, int tier,
            int cost, int sortOrder, string iconPath,
            string baseId = "", GearCurve curve = default)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Slot = slot;
            FamilyId = familyId;
            Tier = tier;
            Cost = cost;
            SortOrder = sortOrder;
            IconPath = iconPath;
            BaseId = baseId ?? "";
            Curve = curve;
        }
    }
}
