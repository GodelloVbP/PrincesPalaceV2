using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // "WHICH POOL DOES THIS ID SPEND FROM?" ASKED OF SOMETHING THAT IS NOT A
    // CHARACTER.
    //
    // ContentDatabase.PrimaryPoolFor falls back to the mana row when a pool id
    // resolves to nothing, and says why: "the content build already refuses
    // [an unresolvable id], so reaching the fallback means the catalogue was
    // swapped out from under a save". PrimaryPoolOf is the same lookup from
    // the other end -- a CHARACTER id rather than a pool id -- and it inherited
    // that fallback for a case it does not describe: an id that is not a
    // character at all comes back holding mana.
    //
    // A skill's owner is allowed to be an enemy (ContentDatabase.Validation
    // permits it, and nine shipped monster skills use it), so this is reachable
    // rather than defensive. GlossaryEntries.SpellCost is the consumer that
    // notices: it prints "{cost} {tag}" and falls back to a bare number on a
    // blank tag, with its own reason -- "a number with no unit is incomplete, a
    // number with the WRONG unit is a lie". While PrimaryPoolOf answered mana
    // for a rat, that fallback could never fire and a monster's ability priced
    // at 7 would have read "7 MP" off a pool the rat does not have.
    //
    // INERT TODAY and pinned anyway: all nine monster skills are manaCost 0, so
    // SpellCost returns "NO COST" before it ever asks. The same class was fixed
    // at the item row in b2d1debd, and the day a monster ability is priced is
    // not the day to discover this.
    public class PrimaryPoolLookupTests
    {
        // A skill whose owner is not in the character catalogue. Named off the
        // content rather than hardcoded so the test follows the roster.
        private static string AnEnemySkillOwner()
        {
            string owner = ContentDatabase.Skills
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Data.CharacterId))
                .Select(s => s.Data.CharacterId)
                .FirstOrDefault(id => ContentDatabase.GetCharacter(id) == null);

            Assert.IsNotNull(owner,
                "no skill in the catalogue is owned by a non-character, so this whole file would " +
                "pass vacuously - Validation still permits an enemy to own a skill");

            return owner;
        }

        [Test]
        public void AnIdThatIsNotACharacterHasNoPrimaryPool()
        {
            string enemy = AnEnemySkillOwner();

            Assert.IsNull(ContentDatabase.PrimaryPoolOf(enemy),
                $"'{enemy}' is not a character and was handed the mana row anyway, which makes " +
                "every consumer's \"I do not know this one\" branch unreachable");
        }

        // AND NEITHER DOES NOTHING AT ALL. The two callers that start from a
        // Character record (ItemDescription, the dossier) already guard a null
        // viewer; this is the id-shaped version of the same question.
        [Test]
        public void AnUnknownOrEmptyIdHasNoPrimaryPool()
        {
            Assert.IsNull(ContentDatabase.PrimaryPoolOf(null));
            Assert.IsNull(ContentDatabase.PrimaryPoolOf(string.Empty));
            Assert.IsNull(ContentDatabase.PrimaryPoolOf("no_such_character_id"));
        }

        // THE FALLBACK THAT STAYS. A character whose primaryPoolId no longer
        // resolves is a save whose catalogue was swapped, not an authoring
        // mistake, and the house posture is that it plays as mana rather than
        // throwing. Narrowing the lookup must not have taken that away, so
        // every real character still answers with a row.
        [Test]
        public void EveryRealCharacterStillResolvesToAPoolRow()
        {
            CollectionAssert.IsNotEmpty(ContentDatabase.Characters);

            foreach (var definition in ContentDatabase.Characters)
            {
                if (definition == null) continue;

                var pool = ContentDatabase.PrimaryPoolOf(definition.id);
                Assert.IsNotNull(pool, $"'{definition.id}' resolves to no pool at all");
                Assert.IsNotEmpty(pool.ShortTag, $"'{definition.id}' has a pool with no tag to print");
            }
        }

        // K13 IS NOT INVALIDATED, and this is the assertion that says so.
        //
        // SpellBooks.CanHold(null) is true by design -- "NULL MEANS YES, the
        // house's graceful-degradation posture" -- and mana's own row carries
        // allowsSpellBooks: true. So CanHoldSpellBooks answered true for an
        // enemy id before this change (through the mana fallback) and answers
        // true after it (through the null arm). Same answer, different route,
        // and nothing that gates on it moves.
        [Test]
        public void CanHoldSpellBooksStillAnswersYesForANonCharacter()
        {
            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks(AnEnemySkillOwner()));
            Assert.IsTrue(SpellBooks.CanHold(null), "the graceful-degradation posture itself moved");
        }
    }
}
