using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // WHAT A CHARACTER CAN ALREADY DEAL AT LEVEL 1 -- one rule, one home.
    //
    // Written twice before this: ContentDatabase.Validation's rule 4 (a
    // build-time content check) and RewardTrackEntryResolver
    // .RewardTrackCharacterContext.BuildAll (what actually gates a filler
    // reward row) computed the identical set independently, each walking the
    // character's own level-1 skills and folding in every DamageInstance AND
    // every element a choice skill offers -- the second half is what makes a
    // packet-only read wrong: an element the player can CHOOSE is an element
    // the character can deal, and Odette's orb says Fire on demand while its
    // authored packet says only Earth. Two copies of that walk is two places
    // to update the day a third kind of "can deal X" is authored, and the
    // build-time rule already lacked a guard the resolver never needed --
    // reading .elements off a hand-authored ScriptableObject asset that
    // predates the field can hand back null, where a ResolvedSkill built
    // through its own constructor never can (Elements defaults to
    // Array.Empty there).
    public static class SkillDamageTypes
    {
        // `level1Skills` is the CALLER'S filtered set -- this asks nothing
        // about a character id or an unlock level, because "which skills
        // count as level 1" already differs slightly in shape between a
        // ContentDatabase walk (a global list filtered by CharacterId) and a
        // resolver walk (already per-character). What both agree on, and
        // what actually varies with content, is what a level-1 skill list
        // adds up to.
        public static HashSet<DamageType> AtLevel1(DamageType attackType, IEnumerable<ResolvedSkill> level1Skills)
        {
            var types = new HashSet<DamageType> { attackType };
            if (level1Skills == null) return types;

            foreach (var skill in level1Skills)
            {
                if (skill == null) continue;

                if (skill.DamageInstances != null)
                {
                    foreach (var instance in skill.DamageInstances) types.Add(instance.type);
                }

                // AN ELEMENT A SKILL LETS THE PLAYER CHOOSE IS ONE THE
                // CHARACTER CAN DEAL. The authored packet is only one of the
                // types a choice skill can land -- reading packets alone
                // would read a character as dealing nothing but their
                // packet's own type at level 1 while an orb-shaped skill
                // lets them pick any of several on a given turn.
                if (skill.Elements != null)
                {
                    foreach (var choice in skill.Elements)
                    {
                        if (choice != null) types.Add(choice.Type);
                    }
                }
            }

            return types;
        }
    }
}
