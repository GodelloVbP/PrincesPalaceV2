using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHO A FRESH PROFILE OPENS WITH, refused at build time when the file
    // cannot answer it.
    //
    // The rule used to be "the first three rows of characters.json", written
    // nowhere except inside SaveData.CreateNew. The Step 0 authoring baseline
    // found it the only way it could be found -- by reading that method after
    // a newly authored character turned out to be in the roster and never in
    // the squad -- and recorded it as one of five rules nothing enforces or
    // announces. This is the enforcement half.
    //
    // Every case here is a file a person would plausibly write: they added a
    // fourth starter and forgot to unset the third, they copied a row and its
    // slot with it, they set a slot and forgot the flag. Each has to fail
    // NAMING THE IDS, because a build error that says only "the starting squad
    // is wrong" leaves an author diffing a file against itself.
    public class StartingSquadResolverTests
    {
        // Ability scores are all a plain 10 here -- there is no total budget
        // to satisfy any more (removed 2026-09-07, see
        // CharacterEntryResolver's header), but each score still has to sit
        // in the 1-30 sanity range, or a case here could fail for the wrong
        // reason -- a squad rejection that is really a range rejection
        // proves nothing about the squad.
        private static RawCharacterEntry Character(string id, bool starts, int slot)
        {
            return new RawCharacterEntry
            {
                id = id,
                displayName = id,
                role = "Tank",
                maxHealth = 30,
                speed = 5,
                attack = 5,
                physicalDefense = 5,
                magicalDefense = 3,
                strength = 10,
                dexterity = 10,
                constitution = 10,
                wisdom = 10,
                intelligence = 10,
                charisma = 10,
                startsInSquad = starts,
                squadSlot = slot,

                // Required since 2026-09-10 (the character's identity
                // colour), and stated here for the same reason the ability
                // scores are: a SQUAD rejection that is really a theme
                // rejection proves nothing about the squad.
                plateTheme = "Blue",
            };
        }

        private static List<RawCharacterEntry> AValidFile() => new List<RawCharacterEntry>
        {
            Character("a", true, 1),
            Character("b", true, 2),
            Character("c", true, 3),
            Character("bench", false, 0),
        };

        // The pool ids ContentBuilder hands the resolver, as a literal: every
        // case here is about the squad rule, so reading the real pools.json
        // would let a pool rename fail a squad test.
        private static readonly string[] KnownPools = { "mana" };

        private static string Resolve(List<RawCharacterEntry> entries)
        {
            bool ok = CharacterEntryResolver.TryResolveAll(entries, KnownPools, out var resolved, out var errors);
            return ok ? null : string.Join(" | ", errors);
        }

        [Test]
        public void ThreeFlaggedCharactersWithDistinctSlotsResolve()
        {
            Assert.IsTrue(CharacterEntryResolver.TryResolveAll(AValidFile(), KnownPools, out var resolved, out var errors),
                "a file with exactly three starters should build: " + string.Join(" | ", errors));

            var squad = resolved.Where(c => c.StartsInSquad).OrderBy(c => c.SquadSlot).Select(c => c.Id).ToList();
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, squad);

            // AND THE BENCHED ONE STILL RESOLVES. The rule is about who
            // starts, not about who exists -- refusing a roster member for
            // not being in the squad would make the roster uneditable.
            CollectionAssert.Contains(resolved.Select(c => c.Id).ToList(), "bench");
        }

        [Test]
        public void SlotOrderIsIndependentOfFileOrder()
        {
            // THE WHOLE POINT, stated as a test rather than assumed from the
            // field's existence: the squad is the slots, so shuffling the rows
            // must not shuffle the squad. Before this, file order WAS the
            // squad and this test could not have been written.
            var shuffled = new List<RawCharacterEntry>
            {
                Character("c", true, 3),
                Character("bench", false, 0),
                Character("a", true, 1),
                Character("b", true, 2),
            };

            Assert.IsTrue(CharacterEntryResolver.TryResolveAll(shuffled, KnownPools, out var resolved, out _));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" },
                resolved.Where(c => c.StartsInSquad).OrderBy(c => c.SquadSlot).Select(c => c.Id).ToList());
        }

        [Test]
        public void TooFewStartersIsRefusedAndNamesTheOnesThereAre()
        {
            var entries = AValidFile();
            entries[2].startsInSquad = false;
            entries[2].squadSlot = 0;

            string errors = Resolve(entries);
            Assert.IsNotNull(errors, "two starters is not a squad");
            StringAssert.Contains("2 character(s) set startsInSquad", errors);
            StringAssert.Contains("a", errors);
            StringAssert.Contains("b", errors);
        }

        [Test]
        public void NoStartersAtAllIsRefusedAndSaysSo()
        {
            var entries = AValidFile();
            foreach (var entry in entries)
            {
                entry.startsInSquad = false;
                entry.squadSlot = 0;
            }

            string errors = Resolve(entries);
            Assert.IsNotNull(errors);
            StringAssert.Contains("0 character(s) set startsInSquad", errors);

            // "none" rather than an empty list, because an error message that
            // trails off into nothing reads as a truncated message.
            StringAssert.Contains("none", errors);
        }

        [Test]
        public void TooManyStartersIsRefusedAndNamesAllFour()
        {
            var entries = AValidFile();
            entries[3].startsInSquad = true;
            entries[3].squadSlot = 1;

            string errors = Resolve(entries);
            Assert.IsNotNull(errors);
            StringAssert.Contains("4 character(s) set startsInSquad", errors);
            StringAssert.Contains("bench", errors);
        }

        [Test]
        public void TwoCharactersInOneSlotIsRefusedAndNamesBoth()
        {
            var entries = AValidFile();
            entries[1].squadSlot = 1;

            string errors = Resolve(entries);
            Assert.IsNotNull(errors, "two characters in slot 1 leaves the order decided by file position again");
            StringAssert.Contains("squadSlot 1", errors);
            StringAssert.Contains("a", errors);
            StringAssert.Contains("b", errors);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(-1)]
        public void AStarterWithASlotOutsideTheRangeIsRefusedByName(int slot)
        {
            var entries = AValidFile();
            entries[0].squadSlot = slot;

            string errors = Resolve(entries);
            Assert.IsNotNull(errors);
            StringAssert.Contains("character 'a'", errors);
            StringAssert.Contains("squadSlot " + slot, errors);
        }

        // A SLOT WITHOUT THE FLAG is the typo this catches: somebody wrote the
        // number and never wrote the boolean, and the character sits in the
        // roster looking authored-into-the-squad while being benched.
        [Test]
        public void ASlotWithoutTheFlagIsRefusedRatherThanIgnored()
        {
            var entries = AValidFile();
            entries[3].squadSlot = 2;

            string errors = Resolve(entries);
            Assert.IsNotNull(errors);
            StringAssert.Contains("character 'bench'", errors);
            StringAssert.Contains("without startsInSquad", errors);
        }
    }
}
