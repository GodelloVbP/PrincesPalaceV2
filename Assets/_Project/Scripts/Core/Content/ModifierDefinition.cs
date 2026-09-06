using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One item modifier ("Rift affix"): a rule an item can roll and grant
    // while worn. Authored in Assets/_Project/ContentData/modifiers.json and
    // generated into an asset by ContentBuilder — never hand-edited under
    // Resources/Content, which ContentBuilder deletes wholesale on every
    // build.
    //
    // A WRAPPER ROUND ONE VALUE. This restated ResolvedModifier's four fields
    // and carried a ModifierEffectEntry mirror of Domain's ModifierEffect,
    // with a hand-written conversion in each direction. ModifierEffect is
    // [Serializable] itself now, so the asset STORES the resolved value and
    // both conversions are gone.
    public class ModifierDefinition : ScriptableObject, IOrderedContent
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
        [SerializeField] private ResolvedModifier data = new ResolvedModifier();

        public ResolvedModifier Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedModifier value) => data = value ?? new ResolvedModifier();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`. Written into save files
        // (EquipmentSlotEntry/InventoryEntry.modifierIds), so never renamed
        // once a save exists.
        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it.
        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order -- see IOrderedContent's own header.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
