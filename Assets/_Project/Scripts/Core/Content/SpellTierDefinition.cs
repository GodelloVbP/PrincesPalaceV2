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
        // THE ONE WAY IN, and it is a setter nothing outside the Editor can
        // call. `data` was a public field: every one of the ~330 reads across
        // the tree could equally have been a write, and eight of them being
        // ContentBuilder was a convention rather than a rule. Private field,
        // public getter, internal setter -- Core grants InternalsVisibleTo to
        // PrincesPalace.Editor and to nothing else, so ContentBuilder can still
        // write it and no runtime or test assembly can.
        //
        // WHAT THIS DOES NOT BUY, said plainly because a chokepoint that is
        // half a chokepoint is worse read as a whole one: the record is a
        // class with public fields, so `definition.Data.Id = "x"` still compiles.
        // What is closed is whole-record REPLACEMENT -- swapping the catalogue
        // entry out from under everyone holding it. Field writes are covered by
        // ContentIsolationTests (behaviourally, per fight) and flagged by
        // ContentOwnershipLintTests (textually, as a diagnostic).
        [SerializeField] private ResolvedSpellTier data = new ResolvedSpellTier();

        public ResolvedSpellTier Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedSpellTier value) => data = value ?? new ResolvedSpellTier();

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
