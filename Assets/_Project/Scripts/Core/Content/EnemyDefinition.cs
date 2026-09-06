using UnityEngine;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Content
{
    // An authored enemy template. Enemies reuse StatBlock (no talents, no
    // mana — they only ever basic-attack for now) rather than a parallel
    // stat shape, so combat math never needs to special-case which side of
    // the fight it's looking at.
    //
    // A WRAPPER ROUND ONE VALUE, for the reason SkillDefinition already
    // records: this restated 29 of ResolvedEnemy's fields, ContentBuilder
    // copied them across one at a time and FightEncounterAdapter copied all 34
    // arguments back. Three field lists, two of which decided nothing, and a
    // mechanical copy that long is where a dropped line hides. The asset now
    // STORES the resolved value; the conversion out is `definition.Data`.
    public class EnemyDefinition : ScriptableObject, IOrderedContent
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
        [SerializeField] private ResolvedEnemy data = new ResolvedEnemy();

        public ResolvedEnemy Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedEnemy value) => data = value ?? new ResolvedEnemy();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // Listed by the authored order ContentBuilder stamped on it.
        //
        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order. Enemy selection is randomized rather than
        // positional today, so that incidental order was never actually
        // load-bearing — but CharacterDefinition looked exactly as safe
        // right up until a new character's id happened to sort badly (see
        // ContentDatabase.Characters, commit d577703). Sorting explicitly
        // here costs nothing and closes the same trap before anything ever
        // does depend on enemy order.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
