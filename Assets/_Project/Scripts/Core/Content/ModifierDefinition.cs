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
        public ResolvedModifier data = new ResolvedModifier();

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
