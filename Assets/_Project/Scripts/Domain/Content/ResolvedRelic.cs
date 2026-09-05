using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // One validated relic — and the shape RelicDefinition now STORES rather
    // than restates.
    //
    // [Serializable] class with public fields for the reason ResolvedSkill
    // records: the definition beside it carried a second copy of these fields
    // plus a RelicModifierEntry mirror of RelicModifier, ContentBuilder copied
    // one into the other and FightEncounterAdapter copied it back. Three field
    // lists for one set of facts. System.Serializable is BCL, so Domain stays
    // engine-free.
    [Serializable]
    public sealed class ResolvedRelic
    {
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public RelicEffect Effect;
        public int SortOrder;
        public string IconPath = "";
        public RelicRarity Rarity;
        public string UnlockedBy = "";

        // Numeric changes. Authored in JSON; no C# needed. A relic may carry
        // these AND an effect. An ARRAY rather than IReadOnlyList because that
        // is what serializes, and every consumer reads it as the interface.
        public RelicModifier[] Modifiers = Array.Empty<RelicModifier>();

        // Mechanic (g). See RawRelicEntry.requiresConvergenceAbility.
        public bool RequiresConvergenceAbility;

        // A relic has to DO something, one way or the other, to be worth
        // offering. Asked here so the draft and the glossary can both tell a
        // real relic from a placeholder without re-deriving the rule.
        public bool HasBehaviour =>
            Effect != RelicEffect.None || (Modifiers != null && Modifiers.Length > 0);

        // Empty UnlockedBy means available from the first run. Expressed as a
        // property rather than left for each caller to test the string, so
        // "is this locked" has one definition.
        public bool IsUnlockedFromTheStart => string.IsNullOrEmpty(UnlockedBy);

        // For the serializer only.
        public ResolvedRelic()
        {
        }

        public ResolvedRelic(string id, string displayName, string description, RelicEffect effect, int sortOrder,
                             string iconPath = "", RelicRarity rarity = RelicRarity.Common, string unlockedBy = "",
                             IReadOnlyList<RelicModifier> modifiers = null,
                             bool requiresConvergenceAbility = false)
        {
            Rarity = rarity;
            UnlockedBy = unlockedBy ?? "";
            Modifiers = ToArray(modifiers);
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            Effect = effect;
            SortOrder = sortOrder;
            IconPath = iconPath ?? "";
            RequiresConvergenceAbility = requiresConvergenceAbility;
        }

        private static RelicModifier[] ToArray(IReadOnlyList<RelicModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0) return Array.Empty<RelicModifier>();
            var copy = new RelicModifier[modifiers.Count];
            for (int i = 0; i < modifiers.Count; i++) copy[i] = modifiers[i];
            return copy;
        }
    }
}
