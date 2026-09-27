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

        // See RawRelicEntry.bearer. Empty means every holder gets the effect.
        public string Bearer = "";

        // See RawRelicEntry.draftable. The initializer is load-bearing: an
        // asset built before this field existed deserializes to TRUE, not to
        // bool's default, so no shipped relic silently leaves the draft.
        public bool Draftable = true;

        // See RawRelicEntry.vfx. None when not authored.
        public SpellPresentation Vfx = new SpellPresentation();

        // Whether this relic's EFFECT reaches a combatant with this character
        // id. Modifiers are not asked -- the resolver refuses a bearer on a
        // relic that has any (RelicEntryResolver).
        public bool ReachesCharacter(string characterId) =>
            string.IsNullOrEmpty(Bearer) || string.Equals(Bearer, characterId, StringComparison.Ordinal);

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
                             bool requiresConvergenceAbility = false,
                             string bearer = "", bool draftable = true)
        {
            Bearer = bearer ?? "";
            Draftable = draftable;
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
