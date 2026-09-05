using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Talents;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One block in the talent tree. column is which of the 3 paths (0-2);
    // row is a slot index (0-20) into the FIXED per-path skeleton (Talent
    // Tree v2, 2026-08-02) -- see RawTalentEntry's own comment for the
    // skeleton shape. Slot 0 is the root (starter), higher slots are more
    // exclusive, same "strictly increasing" ordering as the old plain grid.
    //
    // A WRAPPER ROUND ONE VALUE. This restated ResolvedTalent's field list,
    // carried a TalentEffectEntry mirror of Domain's TalentEffect, and held
    // prerequisites as ASSET REFERENCES that ContentBuilder wired in a second
    // pass -- of which the only thing ever read was `.id`. TalentEffect is
    // [Serializable] itself now and prerequisites are the ids they always
    // were, so the asset STORES the resolved value and both conversions and
    // the second pass are gone.
    public class TalentDefinition : ScriptableObject, IOrderedContent
    {
        public ResolvedTalent data = new ResolvedTalent();

        // The one field the Get*(id) family and every content check read off
        // the asset itself rather than through `data`.
        public string id => data != null ? data.Id : "";

        // Availability is deliberately NOT folded into
        // ContentDatabase.PrerequisitesMet, which SceneBuilder and
        // TalentController both reuse for the connector line — a node being
        // someone else's is a different question from its prerequisites being
        // met, and conflating them would make the line lie.
        public bool IsSharedByEveryCharacter => data == null || data.IsShared;

        public bool IsAvailableTo(Character character)
        {
            return character != null
                   && (IsSharedByEveryCharacter || data.CharacterId == character.definitionId);
        }

        // GRID READING ORDER -- bottom row first, left to right -- which the
        // talent UI depends on and which ContentDatabase used to express as a
        // two-key sort inside its own loader.
        //
        // Equivalent to OrderBy(row).ThenBy(column) because a column IS a path
        // and TalentEntryResolver refuses any column outside 0..PathCount-1, so
        // the encoding cannot collide. Derived from PathCount rather than
        // written as 3, since the tree's width has one home.
        public int SortOrder =>
            data == null ? 0 : data.Row * TalentPage.PathCount + data.Column;
    }
}
