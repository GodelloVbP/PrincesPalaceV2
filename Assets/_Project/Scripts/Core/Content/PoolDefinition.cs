using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One combat resource pool: mana today, and whatever a character trades
    // mana for tomorrow. Authored in Assets/_Project/ContentData/pools.json
    // and generated into an asset by ContentBuilder -- never hand-edited
    // under Resources/Content, which ContentBuilder deletes wholesale on
    // every build.
    //
    // A WRAPPER ROUND ONE VALUE, the CharacterDefinition/ModifierDefinition
    // shape: the asset STORES the resolved record rather than restating its
    // fields under different names, because a rename in the middle of a
    // mechanical per-field copy is exactly what makes a dropped line hard to
    // see.
    public class PoolDefinition : ScriptableObject, IOrderedContent
    {
        // THE ONE WAY IN, and it is a setter nothing outside the Editor can
        // call. Private field, public getter, internal setter -- Core grants
        // InternalsVisibleTo to PrincesPalace.Editor and to nothing else, so
        // ContentBuilder can still write it and no runtime or test assembly
        // can. See CharacterDefinition's own header for what this does and
        // does not buy.
        [SerializeField] private ResolvedPool data = new ResolvedPool();

        public ResolvedPool Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedPool value) => data = value ?? new ResolvedPool();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`. A character's
        // primaryPoolId resolves against it, so never renamed once authored.
        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it.
        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order -- see IOrderedContent's own header, and
        // CLAUDE.md gotcha 4.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
