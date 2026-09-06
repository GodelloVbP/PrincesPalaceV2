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
        [SerializeField] private ResolvedAchievement data = new ResolvedAchievement();

        public ResolvedAchievement Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedAchievement value) => data = value ?? new ResolvedAchievement();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it.
        // Resources.LoadAll returns filename order, not authoring order.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
