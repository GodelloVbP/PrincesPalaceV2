using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Talents;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One block in the talent tree. column is which of the 3 paths (0-2);
    // row is a slot index (0-20) into the FIXED per-path skeleton (Talent
    // Tree v2, 2026-08-02) -- see RawTalentEntry's own comment for the
    // skeleton shape. Slot 0 is the root (starter), higher slots are more
    // exclusive, same "strictly increasing" ordering as the old plain grid.
    public class TalentDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("The only character who can take this, or empty for a node every character shares. Empty was the old behaviour — before this field existed every talent was available to everyone.")]
        public string characterId;

        [Tooltip("Grid column, left to right.")]
        public int column;

        [Tooltip("Grid row. 0 is the bottom (starter) tier.")]
        public int row;

        [Tooltip("ANY ONE of these unlocked is enough (OR, not AND) -- a chain node names exactly one parent, so this only matters for the two convergence nodes per path, which name all 3.")]
        public TalentDefinition[] prerequisites = new TalentDefinition[0];

        [Tooltip("Point-gate: also requires at least this many points already spent in this talent's own path (same characterId + column), on top of the prerequisite check. 0 means no gate.")]
        public int minSpent;

        [Tooltip("Added to the character's stats while this talent is unlocked.")]
        public StatBlock statBonus = StatBlock.Zero;

        [Tooltip("Added to the character's ability scores while this talent is unlocked.")]
        public AbilityScoreBlock abilityScoreBonus = AbilityScoreBlock.Zero;

        [Tooltip("If set, a character with this talent unlocked starts every run with one of this item. Empty for most talents.")]
        public string grantsStartingItemId;

        [Tooltip("Added to the character's max mana while this talent is unlocked.")]
        public int maxManaBonus;

        [Tooltip("Subtracted from the character's Skill mana cost while this talent is unlocked (floored so Skill is never free).")]
        public int skillManaCostReduction;

        [Tooltip("Added to the owner's signature-resource capacity (Shawn's Wool) while this is unlocked.")]
        public int signatureCapacityBonus;

        [Tooltip("Added to how much signature resource the owner gains each turn while this is unlocked.")]
        public int signaturePerTurnBonus;

        [Tooltip("Triggered and conditional rules this talent grants — the payload vocabulary of the reworked Shawn tree. Empty for every node still expressed as a plain stat bonus.")]
        public TalentEffectEntry[] effects = new TalentEffectEntry[0];

        [Tooltip("A skills.json id this talent puts on the owner's combat strip (Provoke, Headbutt, Black Ram Mode). Empty for most talents.")]
        public string grantsSkillId;

        // The resolved effects as the combat layer wants them.
        //
        // A parallel [Serializable] carrier rather than storing
        // Domain.Combat.TalentEffect directly, for one blunt reason: Unity
        // will not serialise a readonly struct's fields onto a
        // ScriptableObject, and making TalentEffect mutable so an asset could
        // hold it would be letting the serialiser dictate the shape of a
        // Domain type. The conversion is one Select, right here, at the only
        // boundary that needs it.
        [System.Serializable]
        public class TalentEffectEntry
        {
            public TalentEffectType type;
            public int magnitude;
            public int threshold;
        }

        public IEnumerable<TalentEffect> ResolvedEffects()
        {
            if (effects == null)
            {
                yield break;
            }

            foreach (var entry in effects)
            {
                if (entry != null && entry.type != TalentEffectType.None)
                {
                    yield return new TalentEffect(entry.type, entry.magnitude, entry.threshold);
                }
            }
        }

        [Tooltip("Editor-time path under Assets/_Project/Art/UI/TalentTree/Icons/Processed/, loaded by SceneBuilder. Empty until the art lands — most talents share an archetype icon rather than carrying their own.")]
        public string iconPath;

        // Availability is deliberately NOT folded into
        // ContentDatabase.PrerequisitesMet, which SceneBuilder and
        // TalentController both reuse for the connector line — a node being
        // someone else's is a different question from its prerequisites being
        // met, and conflating them would make the line lie.
        public bool IsSharedByEveryCharacter => string.IsNullOrEmpty(characterId);

        public bool IsAvailableTo(Character character)
        {
            return character != null && (IsSharedByEveryCharacter || characterId == character.definitionId);
        }

        // GRID READING ORDER -- bottom row first, left to right -- which the
        // talent UI depends on and which ContentDatabase used to express as a
        // two-key sort inside its own loader.
        //
        // Equivalent to OrderBy(row).ThenBy(column) because a column IS a path
        // and TalentEntryResolver refuses any column outside 0..PathCount-1, so
        // the encoding cannot collide. Derived from PathCount rather than
        // written as 3, since the tree's width has one home.
        public int SortOrder => row * TalentPage.PathCount + column;
    }
}
