using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // An achievement: a permanent thing the profile has done, and the gate a
    // relic can hang behind.
    //
    // Authored in Assets/_Project/ContentData/achievements.json and generated
    // by ContentBuilder -- never hand-edited under Resources/Content, which
    // ContentBuilder deletes wholesale on every build.
    //
    // A WRAPPER ROUND ONE VALUE. This type already carried a ToResolved() that
    // restated its own field list -- the half-precedent the rest of the
    // collapse generalised. The asset stores the resolved value now, so there
    // is no copy left to keep in step.
    public class AchievementDefinition : ScriptableObject, IOrderedContent
    {
        public ResolvedAchievement data = new ResolvedAchievement();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it.
        // Resources.LoadAll returns filename order, not authoring order.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
