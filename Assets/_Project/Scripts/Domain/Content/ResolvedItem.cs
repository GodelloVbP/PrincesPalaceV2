using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Domain-layer mirrors of the Content-layer ItemKind/ItemEffect enums.
    // Duplicated rather than referenced on purpose: Domain must stay
    // engine-free (ItemKind/ItemEffect live in PrincesPalace.Content, which
    // is Unity-side), and ContentBuilder maps across the two by name when
    // it creates the asset. The names must stay in step — ItemEntryResolver
    // parses the author's string against THESE, so a value the Content enum
    // gains also has to be added here to be authorable.
    //
    // EquipmentSlot needs no such mirror: it lives in Domain already and is
    // used directly on both sides, the same way DamageType is.
    public enum ResolvedItemKind
    {
        Consumable,
        Weapon,
        Equipment,
    }

    public enum ResolvedItemEffect
    {
        Heal,
        RestoreMana,
    }

    // One validated item — the shape ContentBuilder needs to create an
    // ItemDefinition asset from.
    public readonly struct ResolvedItem
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly ResolvedItemKind Kind;
        public readonly ResolvedItemEffect Effect;
        public readonly int Amount;
        public readonly int AttackBonus;

        // Meaningless for a Consumable (the resolver forces it to Weapon1
        // there and rejects any authored value), so read it only alongside
        // a Kind of Weapon or Equipment.
        public readonly EquipmentSlot Slot;
        public readonly StatBlock StatBonus;
        public readonly AbilityScoreBlock AbilityScoreBonus;
        public readonly bool StartingStock;

        public readonly int Cost;
        public readonly string IconPath;
        public readonly int SortOrder;

        // What a character needs ALREADY (from everything else worn, base
        // scores and talents) before this item counts itself as worn — see
        // RequirementResolver. Zero on a score means no requirement.
        public readonly AbilityScoreBlock Requirements;

        public ResolvedItem(string id, string displayName, string description, ResolvedItemKind kind,
            ResolvedItemEffect effect, int amount, int attackBonus, EquipmentSlot slot,
            StatBlock statBonus, AbilityScoreBlock abilityScoreBonus, bool startingStock,
            int cost, string iconPath, int sortOrder, AbilityScoreBlock requirements = default)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Kind = kind;
            Effect = effect;
            Amount = amount;
            AttackBonus = attackBonus;
            Slot = slot;
            StatBonus = statBonus;
            AbilityScoreBonus = abilityScoreBonus;
            StartingStock = startingStock;
            Cost = cost;
            IconPath = iconPath;
            SortOrder = sortOrder;
            Requirements = requirements;
        }
    }
}
