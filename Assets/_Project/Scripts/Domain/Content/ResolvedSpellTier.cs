using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated row of the Skill power/cost curve -- and the shape
    // SpellTierDefinition now STORES rather than restates.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedSpellTier
    {
        // The character level this tier applies from.
        public int Level;
        public string DisplayName = "";

        // Skill's mana cost at this tier, before any talent-based reduction.
        public int ManaCost;

        // Multiplies the caster's Attack when computing Skill damage here.
        public float PowerMultiplier;

        // Authoring index, not the listing key. SpellTierEntryResolver stamps
        // this as the JSON order and only THEN sorts the resolved list by
        // level, so the two agree exactly as long as spells.json happens to be
        // written in level order -- which is why SpellTierDefinition.SortOrder
        // reads Level instead. Recorded in architecture_audit.md F14.
        public int SortOrder;

        // Which ability scores this tier's spell rides. None for a tier that
        // does not say, which is exactly how every tier behaved before.
        public ScalingProfile Scaling;

        // What a character needs before THIS tier is the one their Skill
        // uses -- see RawSpellTierEntry.requires's own comment. The level-1
        // tier must have none; content validation enforces it, since Skill has
        // to be castable from the very first fight.
        public AbilityScoreBlock Requirements;

        // For the serializer only.
        public ResolvedSpellTier()
        {
        }

        public ResolvedSpellTier(int level, string displayName, int manaCost, float powerMultiplier, int sortOrder,
            ScalingProfile scaling = default, AbilityScoreBlock requirements = default)
        {
            Level = level;
            DisplayName = displayName ?? "";
            ManaCost = manaCost;
            PowerMultiplier = powerMultiplier;
            SortOrder = sortOrder;
            Scaling = scaling;
            Requirements = requirements;
        }
    }
}
