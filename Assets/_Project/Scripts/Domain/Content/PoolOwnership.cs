using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // WHICH OWNERS THE SKILL RESOLVER HAS TO BE TOLD ABOUT, derived once.
    //
    // skills.json cannot be checked against itself alone any more. Two of its
    // rules turn on the pool the skill's OWNER carries rather than on anything
    // written on the skill:
    //
    //   * a bookOnly skill owned by somebody whose pool refuses books is
    //     unreachable content (plan P6, gate 5)
    //   * a free player-selectable skill owned by somebody whose pool opens a
    //     fight at zero is the only way that character can act on turn one
    //     (plan attack point 1)
    //
    // SkillEntryResolver takes both sets as parameters because Domain resolvers
    // never look catalogues up for themselves. This is the one place the two
    // sets are COMPUTED, and it lives here rather than inside ContentBuilder
    // because ContentBuilder is Editor-only: ContentStampIdsTests and
    // RewardTrackContentPinTests resolve the same three files under plain
    // `dotnet test`, with no Editor at all, and a private copy of this
    // derivation in each of them is exactly the drift that would let a green
    // test suite disagree with what a real build refuses.
    //
    // A CHARACTER WHOSE primaryPoolId NAMES NOTHING is in neither set. That
    // cannot happen past CharacterEntryResolver, which refuses an unknown id
    // outright -- so the null arm is degradation for a caller resolving the
    // two catalogues out of order, not a rule.
    public static class PoolOwnership
    {
        // The owners whose primary pool refuses spell books.
        public static List<string> BookRefusers(
            IEnumerable<ResolvedPool> pools, IEnumerable<ResolvedCharacter> characters) =>
            OwnersWhere(pools, characters, pool => !SpellBooks.CanHold(pool));

        // The owners whose primary pool holds nothing when a fight begins.
        public static List<string> ZeroStartOwners(
            IEnumerable<ResolvedPool> pools, IEnumerable<ResolvedCharacter> characters) =>
            OwnersWhere(pools, characters, pool => pool != null && pool.StartRule == PoolStartRule.Zero);

        // The owners a poolTiers skill may be authored against -- every real
        // CHARACTER (PrimaryPoolId always names a row, defaulting to "mana"
        // -- ResolvedCharacter.PrimaryPoolId). What this set actually excludes
        // is anyone NOT in the character catalogue at all: a monster-owned
        // skill has no entry here regardless of what pools.json contains,
        // because SkillEntryResolver cannot otherwise tell "this id is a
        // character with a pool" from "this id is a typo" -- the same
        // reachability question ContentDatabase.Validation's own
        // "skill owned by no character" check asks from the loaded
        // catalogue's side.
        public static List<string> PrimaryPoolOwners(
            IEnumerable<ResolvedPool> pools, IEnumerable<ResolvedCharacter> characters) =>
            OwnersWhere(pools, characters, pool => pool != null);

        private static List<string> OwnersWhere(
            IEnumerable<ResolvedPool> pools, IEnumerable<ResolvedCharacter> characters,
            System.Func<ResolvedPool, bool> predicate)
        {
            var byId = (pools ?? Enumerable.Empty<ResolvedPool>())
                .Where(pool => pool != null)
                .ToDictionary(pool => pool.Id, pool => pool);

            return (characters ?? Enumerable.Empty<ResolvedCharacter>())
                .Where(character => character != null)
                .Where(character => predicate(
                    byId.TryGetValue(character.PrimaryPoolId ?? "", out var pool) ? pool : null))
                .Select(character => character.Id)
                .ToList();
        }
    }
}
