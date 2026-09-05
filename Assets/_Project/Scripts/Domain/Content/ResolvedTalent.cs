using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated talent — and the shape TalentDefinition now STORES rather
    // than restates.
    //
    // PREREQUISITES STAY AS IDS. The asset used to hold TalentDefinition
    // object references, wired in a second ContentBuilder pass because the
    // assets they point at may not exist yet on the first; the only thing
    // anything ever read off those references was `.id`, so holding the ids
    // directly deletes the second pass and the reference graph with it.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedTalent
    {
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public string CharacterId = "";
        public int Column;
        public int Row;

        // ANY ONE of these unlocked is enough (OR, not AND) -- a chain node
        // names exactly one parent, so this only matters for the two
        // convergence nodes per path, which name all three.
        public string[] Prerequisites = Array.Empty<string>();

        public StatBlock StatBonus;
        public AbilityScoreBlock AbilityScoreBonus;
        public int MaxManaBonus;
        public int SkillManaCostReduction;
        public int SignatureCapacityBonus;
        public int SignaturePerTurnBonus;
        public string GrantsStartingItemId = "";
        public int SortOrder;
        public string IconPath = "";

        // Point-gate: also requires at least this many points already spent in
        // this talent's own path (same CharacterId + Column), on top of the
        // prerequisite check. 0 means no gate.
        public int MinSpent;

        // The triggered/conditional rules this talent grants. Never null;
        // empty for every node still using the old additive vocabulary.
        public TalentEffect[] Effects = Array.Empty<TalentEffect>();

        // A skills.json id this talent puts on the owner's combat strip, or
        // empty — see RawTalentEntry.grantsSkillId.
        public string GrantsSkillId = "";

        // True when this node is available to everyone rather than owned by
        // one character.
        public bool IsShared => string.IsNullOrEmpty(CharacterId);

        // For the serializer only.
        public ResolvedTalent()
        {
        }

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
            Effects = ToArray(effects);
            GrantsSkillId = grantsSkillId ?? "";
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            CharacterId = characterId ?? "";
            Column = column;
            Row = row;
            Prerequisites = ToArray(prerequisites);
            StatBonus = statBonus;
            AbilityScoreBonus = abilityScoreBonus;
            MaxManaBonus = maxManaBonus;
            SkillManaCostReduction = skillManaCostReduction;
            SignatureCapacityBonus = signatureCapacityBonus;
            SignaturePerTurnBonus = signaturePerTurnBonus;
            GrantsStartingItemId = grantsStartingItemId ?? "";
            SortOrder = sortOrder;
            IconPath = iconPath ?? "";
            MinSpent = minSpent;
        }

        private static T[] ToArray<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++) copy[i] = source[i];
            return copy;
        }
    }
}
