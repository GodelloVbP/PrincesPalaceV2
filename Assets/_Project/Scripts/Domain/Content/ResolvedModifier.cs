using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // One validated item modifier -- and the shape ModifierDefinition now
    // STORES rather than restates. Mirrors ResolvedTalent's shape: no
    // prerequisites to defer to a second pass here (a modifier has none), so
    // this is the simpler of the two.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedModifier
    {
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public int SortOrder;

        // The rules this modifier grants. Never null and never empty --
        // ModifierEntryResolver rejects a modifier that resolves to zero
        // effects the same way ItemEntryResolver rejects an item that
        // grants nothing. An ARRAY rather than IReadOnlyList because that is
        // what serializes, and every consumer reads it as the interface.
        public ModifierEffect[] Effects = Array.Empty<ModifierEffect>();

        // For the serializer only.
        public ResolvedModifier()
        {
        }

        public ResolvedModifier(string id, string displayName, string description, int sortOrder,
            IReadOnlyList<ModifierEffect> effects)
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            SortOrder = sortOrder;
            if (effects == null || effects.Count == 0)
            {
                Effects = Array.Empty<ModifierEffect>();
            }
            else
            {
                Effects = new ModifierEffect[effects.Count];
                for (int i = 0; i < effects.Count; i++) Effects[i] = effects[i];
            }
        }
    }
}
