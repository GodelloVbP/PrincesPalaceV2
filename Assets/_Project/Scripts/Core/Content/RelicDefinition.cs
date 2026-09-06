using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // A relic: a run-long combat effect, assigned to exactly one character
    // at a time. Authored in Assets/_Project/ContentData/relics.json and
    // generated into an asset by ContentBuilder — never hand-edited under
    // Resources/Content, which ContentBuilder deletes wholesale on every
    // build.
    //
    // A WRAPPER ROUND ONE VALUE. This restated ResolvedRelic's fields and
    // carried a RelicModifierEntry mirror of RelicModifier beside them, with a
    // hand-written conversion in each direction (ToModifiers here, a Select in
    // ContentBuilder). RelicModifier is [Serializable] itself now, so the asset
    // STORES the resolved value and both conversions are gone.
    public class RelicDefinition : ScriptableObject, IOrderedContent
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
        [SerializeField] private ResolvedRelic data = new ResolvedRelic();

        public ResolvedRelic Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedRelic value) => data = value ?? new ResolvedRelic();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // FORWARDED, not stored. ScreenRegistry's four icon tables read this
        // off the definition while walking ContentDatabase.Relics; a property
        // keeps that call shape without putting the string on the asset twice.
        public string iconPath => data != null ? data.IconPath : "";

        // Listed by the authored order ContentBuilder stamped on it.
        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order — the same trap Characters/Enemies/Talents/
        // Upgrades already guard against.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
