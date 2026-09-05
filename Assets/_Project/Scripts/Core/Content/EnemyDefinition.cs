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
    // STORES the resolved value; the conversion out is `definition.data`.
    public class EnemyDefinition : ScriptableObject, IOrderedContent
    {
        public ResolvedEnemy data = new ResolvedEnemy();

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
