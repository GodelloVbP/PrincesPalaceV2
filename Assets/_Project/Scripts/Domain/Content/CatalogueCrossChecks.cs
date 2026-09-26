using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // THE COMPARISONS BEHIND ContentDatabase.ValidateContent()'s whole-catalogue
    // rules, pulled out over primitives so each one can be tested with literals.
    //
    // ValidateContent reads ContentDatabase's Resources-loaded catalogue, and
    // Core's InternalsVisibleTo names only the Editor assembly, so no test
    // assembly can build a synthetic *Definition to feed it a broken catalogue.
    // Six rules there had no test at all as a result (AUDIT #77): any of them
    // could have regressed to "never fires" and every build would still have
    // passed. The same fix shape as AchievementProgress.
    // ValidateDefeatSpecificBossParameter: the decision lives here and is
    // EditMode-tested against fixtures that break exactly one rule each;
    // ValidateContent is only the wiring that hands each predicate the real
    // catalogue, and one PlayMode test proves the wiring on shipped content.
    //
    // Every predicate returns null when the rule holds and the refusal text
    // when it does not -- the text ValidateContent has always emitted, so a
    // content author sees the same message as before.
    public static class CatalogueCrossChecks
    {
        // One id space across every catalogue: ids are what saves store, so a
        // relic and an item sharing one resolves a save to the wrong
        // definition. `seen` is shared across all catalogues on purpose --
        // that sharing is the whole rule. One message per repeat, naming the
        // catalogue the repeat was found on.
        public static List<string> DuplicateIds(IEnumerable<(string kind, IEnumerable<string> ids)> catalogues)
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var (kind, ids) in catalogues)
            {
                foreach (string id in ids)
                {
                    if (!seen.Add(id))
                    {
                        errors.Add($"Duplicate id '{id}' (on a {kind}) — every id must be unique across all content types.");
                    }
                }
            }

            return errors;
        }

        // An enemy ability naming no skill silently disarms the monster: the
        // fight adapter drops an unresolvable id with a warning. An empty id
        // is not this rule's business (the resolver refuses it).
        public static string EnemyAbilitySkill(string enemyId, string skillId, ICollection<string> skillIds)
        {
            if (string.IsNullOrEmpty(skillId) || skillIds.Contains(skillId))
            {
                return null;
            }

            return $"Enemy '{enemyId}' has an ability naming unknown skill id '{skillId}'.";
        }

        // A talent's granted skill must exist, and unless the talent is shared
        // by every character it must belong to the talent's own character --
        // TalentGrantedSkillsFor's runtime owner check would otherwise grant
        // nothing, silently.
        public static string TalentGrantedSkill(string talentId, string talentCharacterId, bool sharedByEveryCharacter,
            string grantsSkillId, IReadOnlyDictionary<string, string> skillOwnerById)
        {
            if (string.IsNullOrEmpty(grantsSkillId))
            {
                return null;
            }

            if (!skillOwnerById.TryGetValue(grantsSkillId, out string owner))
            {
                return $"Talent '{talentId}' grants unknown skill id '{grantsSkillId}'.";
            }

            if (!sharedByEveryCharacter && owner != talentCharacterId)
            {
                return $"Talent '{talentId}' belongs to '{talentCharacterId}' but grants skill " +
                       $"'{grantsSkillId}', which belongs to '{owner}'. " +
                       "A character cannot hand out another character's kit.";
            }

            return null;
        }

        public static string TalentStartingItem(string talentId, string grantsStartingItemId, ICollection<string> itemIds)
        {
            if (string.IsNullOrEmpty(grantsStartingItemId) || itemIds.Contains(grantsStartingItemId))
            {
                return null;
            }

            return $"Talent '{talentId}' grants unknown item id '{grantsStartingItemId}'.";
        }

        // A talent owned by a character who does not exist can never be taken.
        // A shared talent has no owner to check.
        public static string TalentCharacter(string talentId, string characterId, bool sharedByEveryCharacter,
            ICollection<string> characterIds)
        {
            if (sharedByEveryCharacter || (characterId != null && characterIds.Contains(characterId)))
            {
                return null;
            }

            return $"Talent '{talentId}' belongs to unknown character id '{characterId}'.";
        }

        // A skill's owner may be a character OR an enemy, but it must be one
        // of them: a typo'd owner hides the button (or the monster's move)
        // with no symptom but its absence.
        public static string SkillOwner(string skillId, string ownerId,
            ICollection<string> characterIds, ICollection<string> enemyIds)
        {
            if (ownerId != null && (characterIds.Contains(ownerId) || enemyIds.Contains(ownerId)))
            {
                return null;
            }

            return $"Skill '{skillId}' belongs to unknown owner id '{ownerId}'. " +
                   "It must name a character or an enemy.";
        }
    }
}
