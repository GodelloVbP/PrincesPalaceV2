using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One row of the character level cost table — see Assets/_Project/
    // ContentData/level_curve.json (the actual authored data; this asset type
    // is generated from it, never hand-edited, same as SpellTierDefinition).
    //
    // A WRAPPER ROUND ONE VALUE, like every other definition here: the field
    // list lives in ResolvedLevelCost and the asset stores it. Same private-
    // field/internal-setter chokepoint SpellTierDefinition documents, and for
    // the same reason.
    public class LevelCurveDefinition : ScriptableObject, IOrderedContent
    {
        [SerializeField] private ResolvedLevelCost data = new ResolvedLevelCost();

        public ResolvedLevelCost Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedLevelCost value) => data = value ?? new ResolvedLevelCost();

        // BY LEVEL, which is the only key this type has. A cost table read
        // out of alphabetical filename order ("level_10" before "level_2")
        // would be a plausible-but-wrong ladder — exactly the failure
        // IOrderedContent exists to make impossible.
        public int SortOrder => data != null ? data.Level : 0;
    }
}
