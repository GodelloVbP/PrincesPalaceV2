using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated talent — the shape ContentBuilder needs to create a
    // TalentDefinition asset from. Prerequisites stay as ids here; the
    // builder resolves them to asset references in a second pass, because
    // the assets they point at may not exist yet on the first.
    public readonly struct ResolvedTalent
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string CharacterId;
        public readonly int Column;
        public readonly int Row;
        public readonly IReadOnlyList<string> Prerequisites;
        public readonly StatBlock StatBonus;
        public readonly AbilityScoreBlock AbilityScoreBonus;
        public readonly int MaxManaBonus;
        public readonly int SkillManaCostReduction;
        public readonly int SignatureCapacityBonus;
        public readonly int SignaturePerTurnBonus;
        public readonly string GrantsStartingItemId;
        public readonly int SortOrder;
        public readonly string IconPath;
        public readonly int MinSpent;

        // The triggered/conditional rules this talent grants. Never null;
        // empty for every node still using the old additive vocabulary,
        // which is all 252 belonging to the other four characters.
        public readonly IReadOnlyList<TalentEffect> Effects;

        // A skills.json id this talent puts on the owner's combat strip, or
        // empty — see RawTalentEntry.grantsSkillId.
        public readonly string GrantsSkillId;

        // No `cost`. A talent's price in embers is derived from WHERE it sits
        // in the skeleton (ContentDatabase.OrbCost), never authored — and the
        // authored field that used to exist here was worse than redundant.
        // Nothing consulted it for what an orb actually charged, but the
        // talent screen printed it, so every node in the game displayed
        // "1pt" while silently costing 1, 2, 4 or 8. Deleting the field is
        // what makes that class of disagreement impossible rather than fixed.
        public ResolvedTalent(string id, string displayName, string description, string characterId,
            int column, int row, IReadOnlyList<string> prerequisites, StatBlock statBonus,
            AbilityScoreBlock abilityScoreBonus, int maxManaBonus, int skillManaCostReduction,
            int signatureCapacityBonus, int signaturePerTurnBonus, string grantsStartingItemId, int sortOrder,
            string iconPath = "", int minSpent = 0, IReadOnlyList<TalentEffect> effects = null,
            string grantsSkillId = "")
        {
            Effects = effects ?? new List<TalentEffect>();
            GrantsSkillId = grantsSkillId ?? "";
            Id = id;
            DisplayName = displayName;
            Description = description;
            CharacterId = characterId;
            Column = column;
            Row = row;
            Prerequisites = prerequisites;
            StatBonus = statBonus;
            AbilityScoreBonus = abilityScoreBonus;
            MaxManaBonus = maxManaBonus;
            SkillManaCostReduction = skillManaCostReduction;
            SignatureCapacityBonus = signatureCapacityBonus;
            SignaturePerTurnBonus = signaturePerTurnBonus;
            GrantsStartingItemId = grantsStartingItemId;
            SortOrder = sortOrder;
            IconPath = iconPath;
            MinSpent = minSpent;
        }

        // True when this node is available to everyone rather than owned by
        // one character.
        public bool IsShared => string.IsNullOrEmpty(CharacterId);
    }
}
