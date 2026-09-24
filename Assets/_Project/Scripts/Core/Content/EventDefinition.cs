using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One event: a small graph of pages, authored in
    // Assets/_Project/ContentData/events.json and generated into an asset by
    // ContentBuilder -- never hand-edited under Resources/Content, which
    // ContentBuilder deletes wholesale on every build.
    //
    // Same "one field, private setter, Editor-only writer" chokepoint as
    // RelicDefinition -- see that type's own header for what it does and
    // does not buy.
    public class EventDefinition : ScriptableObject, IOrderedContent
    {
        [SerializeField] private ResolvedEventDefinition data = new ResolvedEventDefinition();

        public ResolvedEventDefinition Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedEventDefinition value) => data = value ?? new ResolvedEventDefinition();

        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it, not
        // Resources.LoadAll's alphabetical filename order -- same reason
        // every other content type carries this.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
