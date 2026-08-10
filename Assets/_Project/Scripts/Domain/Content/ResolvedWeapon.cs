using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One generated weapon: one family, one modifier, one TIER — the shape
    // ContentBuilder turns into an ItemDefinition asset.
    //
    // Tier is which sword this is: its attack, its scaling grades, its tier
    // adjective, its rarity band. The player-facing PLUS is a separate axis
    // carried on the item instance rather than baked here, so a +3 sword and
    // a +0 one of the same tier are one asset, not two.
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
        public readonly string ModifierId;
        public readonly int Tier;

        public readonly int AttackBonus;
        public readonly ScalingProfile Scaling;
        public readonly int Cost;
        public readonly int SortOrder;
        public readonly string IconPath;

        // What this weapon contributes to SPELL power, separate from Scaling
        // above (which is the plain-Attack axis) -- see ItemDefinition.
        // spellScaling's own comment. Appended last with a default so the
        // 12-arg positional constructor's existing call site (BuildWeapon)
        // is a one-line addition rather than a rewrite.
        public readonly ScalingProfile SpellScaling;

        // What a character needs, from everything ELSE worn plus base scores
        // and talents, before this weapon counts as worn — see
        // RequirementResolver.
        public readonly AbilityScoreBlock Requirements;

        public ResolvedWeapon(string id, string displayName, string description, EquipmentSlot slot,
            string familyId, string modifierId, int tier, int attackBonus, ScalingProfile scaling,
            int cost, int sortOrder, string iconPath, ScalingProfile spellScaling = default,
            AbilityScoreBlock requirements = default)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Slot = slot;
            FamilyId = familyId;
            ModifierId = modifierId;
            Tier = tier;
            AttackBonus = attackBonus;
            Scaling = scaling;
            Cost = cost;
            SortOrder = sortOrder;
            IconPath = iconPath;
            SpellScaling = spellScaling;
            Requirements = requirements;
        }
    }
}
