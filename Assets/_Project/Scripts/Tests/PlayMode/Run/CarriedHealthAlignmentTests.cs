using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // The health a run carried in lands on the character it belongs to.
    //
    // ApplyStartingHealth zips two lists BY INDEX: the ids the roll produced
    // and the combatants the adapter produced. Those two agree only while
    // every id resolves, since Build skips an id that names no content, in
    // place -- so one unresolvable id in the middle of a squad shifts every
    // combatant after it one place left against the id list.
    //
    // That is reachable rather than theoretical: FieldableParty passes the
    // save's squad ids through without asking ContentDatabase whether they
    // still resolve, so a renamed character id -- an ordinary content edit --
    // is enough. What it cost was the second squad-mate walking into the room
    // on the MISSING character's carried health.
    //
    // Pinned with literals rather than with whatever the roster happens to
    // hold, so the failure names the transposition instead of a number.
    public class CarriedHealthAlignmentTests
    {
        private const string GhostId = "no_such_character_id";

        private static List<string> OneEnemy() =>
            ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList();

        private static List<string> TwoRealCharacterIds()
        {
            var real = ContentDatabase.Characters
                .Where(c => c != null && !string.IsNullOrEmpty(c.id))
                .Take(2)
                .Select(c => c.id)
                .ToList();

            Assert.AreEqual(2, real.Count,
                "this test needs two authored characters to tell a transposition apart");

            return real;
        }

        [Test]
        public void AnUnresolvableSquadId_DoesNotShiftCarriedHealthOntoTheNextCharacter()
        {
            var real = TwoRealCharacterIds();

            // The shape a content rename leaves behind: a squad of three whose
            // middle member no longer names anything.
            var partyIds = new List<string> { real[0], GhostId, real[1] };

            var health = new Dictionary<string, int>
            {
                { real[0], 5 },
                { GhostId, 3 },
                { real[1], 7 },
            };

            var built = FightEncounterAdapter.Build(partyIds, OneEnemy(), new SeededRandom(11));

            Assert.IsNotNull(built, "no fight could be built from the current content");
            Assert.AreEqual(2, built.Party.Count,
                "Build should have skipped the unresolvable id and kept the other two");

            RunEncounter.ApplyStartingHealth(built, health);

            Assert.AreEqual(5, built.Party[0].CurrentHealth,
                "the first character's own carried health did not survive the trip");
            Assert.AreEqual(7, built.Party[1].CurrentHealth,
                "the missing character's 3 landed on the second character, who carried 7");
        }

        // The list the fix rests on: the ids Build actually produced, so nothing
        // downstream has to reconstruct the mapping from the list it was handed.
        [Test]
        public void ABuiltFight_NamesOnlyTheCharactersItCouldBuild()
        {
            var real = TwoRealCharacterIds();

            var built = FightEncounterAdapter.Build(
                new List<string> { real[0], GhostId, real[1] }, OneEnemy(), new SeededRandom(11));

            Assert.IsNotNull(built, "no fight could be built from the current content");
            CollectionAssert.AreEqual(new[] { real[0], real[1] }, built.PartyIds.ToList(),
                "PartyIds has to stay in step with Party, one entry per combatant built");
        }
    }
}
