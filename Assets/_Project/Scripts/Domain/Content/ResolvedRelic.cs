using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // One validated relic, and the shape RelicDefinition stores rather than
    // restates. [Serializable] class with public fields for the reason
    // ResolvedSkill records; System.Serializable is BCL, so Domain stays
    // engine-free.
    [Serializable]
    public sealed class ResolvedRelic
    {
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public int SortOrder;
        public string IconPath = "";
        public RelicRarity Rarity;
        public string UnlockedBy = "";

        // What the relic does: stats and at most one mechanic, each an
        // EffectKind a relic may hold (RelicEntryResolver). An ARRAY because
        // that is what serializes; consumers read it as the interface.
        public Effect[] Effects = Array.Empty<Effect>();

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

        // Whether this relic's effects reach a combatant with this character
        // id: all of them, or none (EffectSet.WithRelics).
        public bool ReachesCharacter(string characterId) =>
            string.IsNullOrEmpty(Bearer) || string.Equals(Bearer, characterId, StringComparison.Ordinal);

        // The relic's one mechanic kind, or None for a relic that is only
        // numbers (or nothing yet). What the one-relic-per-mechanic rule, the
        // HUD's "which relic did this" and a relic's own beat look up by.
        public EffectKind Mechanic
        {
            get
            {
                if (Effects == null) return EffectKind.None;
                foreach (var effect in Effects)
                {
                    if (EffectCatalog.Has(effect.Kind) && EffectCatalog.Of(effect.Kind).IsMechanic)
                    {
                        return effect.Kind;
                    }
                }

                return EffectKind.None;
            }
        }

        // A relic has to DO something to be worth offering. Asked here so the
        // draft and the glossary tell a real relic from a placeholder by one
        // rule.
        public bool HasBehaviour => Effects != null && Effects.Length > 0;

        // Empty UnlockedBy means available from the first run. Expressed as a
        // property rather than left for each caller to test the string, so
        // "is this locked" has one definition.
        public bool IsUnlockedFromTheStart => string.IsNullOrEmpty(UnlockedBy);

        // For the serializer only.
        public ResolvedRelic()
        {
        }

        public ResolvedRelic(string id, string displayName, string description, IReadOnlyList<Effect> effects,
                             int sortOrder, string iconPath = "", RelicRarity rarity = RelicRarity.Common,
                             string unlockedBy = "", bool requiresConvergenceAbility = false,
                             string bearer = "", bool draftable = true)
        {
            Bearer = bearer ?? "";
            Draftable = draftable;
            Rarity = rarity;
            UnlockedBy = unlockedBy ?? "";
            Effects = ToArray(effects);
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            SortOrder = sortOrder;
            IconPath = iconPath ?? "";
            RequiresConvergenceAbility = requiresConvergenceAbility;
        }

        private static Effect[] ToArray(IReadOnlyList<Effect> effects)
        {
            if (effects == null || effects.Count == 0) return Array.Empty<Effect>();
            var copy = new Effect[effects.Count];
            for (int i = 0; i < effects.Count; i++) copy[i] = effects[i];
            return copy;
        }
    }
}
