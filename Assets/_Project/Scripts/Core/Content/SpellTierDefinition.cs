using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One row of the Skill power/cost curve — see Assets/_Project/
    // ContentData/spells.json (the actual authored data; this asset type is
    // generated from it, never hand-edited, same as EnemyDefinition).
    // FightController resolves which tier a caster uses from their
    // Character.level; there's no separate spell-picking UI.
    public class SpellTierDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("The character level this tier applies from.")]
        public int level;

        public string displayName;

        [Tooltip("Skill's mana cost at this tier, before any talent-based reduction.")]
        public int manaCost;

        [Tooltip("Multiplies the caster's Attack stat when computing Skill damage at this tier.")]
        public float powerMultiplier;

        [Tooltip("Which ability scores this tier's spell rides, and how hard. Multiplies alongside powerMultiplier. Ten is neutral. All None means the spell does not scale, which is how every tier behaved before scaling existed.")]
        public ScalingProfile scaling;

        [Tooltip("Ability scores required before a caster's Skill actually reaches this tier. Zero on a score means no requirement. The level-1 tier must have none -- content validation enforces it, since Skill has to be castable from the very first fight. An unmet tier falls back to the highest tier whose requirements ARE met.")]
        public AbilityScoreBlock requirements;

        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order — sorted explicitly by ContentDatabase for the
        // same reason Characters/Enemies/Talents/Upgrades already are (see
        // EnemyDefinition.sortOrder).
        public int sortOrder;

        // BY LEVEL, not by the authored `sortOrder` this type also carries.
        //
        // The two are not the same key and never were: SpellTierEntryResolver
        // stamps sortOrder as the JSON authoring index and only THEN sorts the
        // resolved list by level, so the two agree exactly as long as spells.json
        // happens to be written in level order. ContentDatabase has always sorted
        // these by level, and this states that where the type is declared rather
        // than leaving it as a special case in the loader.
        //
        // Which leaves `sortOrder` on this type read by nothing -- ContentBuilder
        // still writes it. It is recorded in architecture_audit.md F14 rather
        // than deleted here, because removing a serialized field from a
        // ScriptableObject means regenerating the whole content tree, and that
        // does not belong in the same change as the loader.
        public int SortOrder => level;
    }
}
