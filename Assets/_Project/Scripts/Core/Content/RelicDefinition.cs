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
        public ResolvedRelic data = new ResolvedRelic();

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
