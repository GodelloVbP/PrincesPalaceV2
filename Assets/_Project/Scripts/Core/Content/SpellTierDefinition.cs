using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One row of the Skill power/cost curve — see Assets/_Project/
    // ContentData/spells.json (the actual authored data; this asset type is
    // generated from it, never hand-edited, same as EnemyDefinition).
    // FightController resolves which tier a caster uses from their
    // Character.level; there's no separate spell-picking UI.
    //
    // A WRAPPER ROUND ONE VALUE, like every other definition here: the field
    // list lives in ResolvedSpellTier and the asset stores it.
    public class SpellTierDefinition : ScriptableObject, IOrderedContent
    {
        public ResolvedSpellTier data = new ResolvedSpellTier();

        // BY LEVEL, not by the authored `SortOrder` the resolved value also
        // carries.
        //
        // The two are not the same key and never were: SpellTierEntryResolver
        // stamps SortOrder as the JSON authoring index and only THEN sorts the
        // resolved list by level, so the two agree exactly as long as
        // spells.json happens to be written in level order. ContentDatabase has
        // always sorted these by level, and this states that where the type is
        // declared rather than leaving it as a special case in the loader.
        public int SortOrder => data != null ? data.Level : 0;
    }
}
