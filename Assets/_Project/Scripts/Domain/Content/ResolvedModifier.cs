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
        public Effect[] Effects = Array.Empty<Effect>();

        // A trait: rolled once on a Legendary copy of its pool's slots,
        // wearer-scoped, a mechanic rather than a number (docs/PLAN_ITEMS.md
        // 1.3). False for an affix.
        public bool Trait;

        // Which slots roll it: the weapon slots or every other. An affix row
        // may leave it None, in which case its kind's catalog pool applies
        // (ModifierTable.PoolOf).
        public AffixPool Pool;

        // What the wearer's kit must hold for it to do anything.
        public TraitRequirement[] Requires = Array.Empty<TraitRequirement>();

        // For the serializer only.
        public ResolvedModifier()
        {
        }

        public ResolvedModifier(string id, string displayName, string description, int sortOrder,
            IReadOnlyList<Effect> effects, bool trait = false, AffixPool pool = AffixPool.None,
            IReadOnlyList<TraitRequirement> requires = null)
        {
            Trait = trait;
            Pool = pool;
            Requires = requires == null || requires.Count == 0
                ? Array.Empty<TraitRequirement>()
                : new List<TraitRequirement>(requires).ToArray();
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            SortOrder = sortOrder;
            if (effects == null || effects.Count == 0)
            {
                Effects = Array.Empty<Effect>();
            }
            else
            {
                Effects = new Effect[effects.Count];
                for (int i = 0; i < effects.Count; i++) Effects[i] = effects[i];
            }
        }
    }
}
