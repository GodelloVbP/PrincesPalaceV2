using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // One validated item modifier — the shape ContentBuilder needs to create
    // a ModifierDefinition asset from. Mirrors ResolvedTalent's shape: no
    // prerequisites to defer to a second pass here (a modifier has none), so
    // this is the simpler of the two.
    public readonly struct ResolvedModifier
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int SortOrder;

        // The rules this modifier grants. Never null and never empty —
        // ModifierEntryResolver rejects a modifier that resolves to zero
        // effects the same way ItemEntryResolver rejects an item that
        // grants nothing.
        public readonly IReadOnlyList<ModifierEffect> Effects;

        public ResolvedModifier(string id, string displayName, string description, int sortOrder,
            IReadOnlyList<ModifierEffect> effects)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            SortOrder = sortOrder;
            Effects = effects ?? new List<ModifierEffect>();
        }
    }
}
