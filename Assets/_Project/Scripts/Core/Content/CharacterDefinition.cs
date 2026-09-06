using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // The immutable template for a playable character. The mutable, per-save
    // half lives in Character (talent points spent, talents unlocked); this
    // is the part that is authored once and never changes at runtime.
    //
    // A WRAPPER ROUND ONE VALUE. This restated all 18 of ResolvedCharacter's
    // fields under different names (baseAbilityScores/AbilityScores,
    // signatureResourceId/SignatureId) and ContentBuilder copied them across
    // one at a time -- a rename in the middle of a mechanical copy being
    // exactly what makes a dropped line hard to see. The asset STORES the
    // resolved value now.
    public class CharacterDefinition : ScriptableObject, IOrderedContent
    {
        public ResolvedCharacter data = new ResolvedCharacter();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // A `portraitPath` forwarder used to sit here for ScreenRegistry, which
        // walked ContentDatabase.Characters and baked every face into the
        // scene. Nothing bakes portraits now -- CharacterPortraits loads them
        // off Resources by id -- and its one caller is gone with it. Removed
        // rather than left: a forwarder nobody calls reads as a supported way
        // in.

        // Listed by the authored order ContentBuilder stamped on it.
        //
        // Resources.LoadAll returns assets in filename order, not authoring
        // order — that accidentally matched the intended roster order until
        // a character whose id sorted early (alphabetically) was added
        // after one whose id sorts late, silently reshuffling which
        // characters land in the default squad.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
