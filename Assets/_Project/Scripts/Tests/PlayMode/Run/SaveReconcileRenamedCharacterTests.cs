using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // A save whose squad names a character that was renamed out from under it.
    //
    // The live instance is the placeholder_brawler -> bear rename: a
    // profile written before it carries selectedCharacterIds with an id that
    // ContentDatabase no longer resolves. The question this pins is whether
    // Reconcile RE-SEATS the replacement or silently ships a squad of two --
    // the second is invisible, because a squad short a member looks exactly
    // like a squad the player chose to run short.
    //
    // Answered by two rules working together, and neither one is enough
    // alone: the roster gains every authored character it is missing, and the
    // selection is topped back up to EffectiveMaxSquadSize in TopUpOrder,
    // which puts content's flagged starters ahead of plain roster order.
    //
    // IDS COME FROM CONTENT, not from a literal list. SaveDataSquadOfThreeTests
    // records why: the literal { sheep, placeholder_brawler, owl } made adding
    // a character a test edit, and failed on the id rather than on the rule.
    // The one hardcoded id here is the GHOST, which is the point of the test
    // and names nothing that could ever resolve.
    public class SaveReconcileRenamedCharacterTests
    {
        // Any id content no longer carries reproduces the same shape.
        private const string RenamedAwayId = "placeholder_brawler";

        [SetUp]
        public void ForceThreeSeats()
        {
            // Forced rather than derived so the assertions below are about
            // the re-seat and not about how many starters content flags this
            // week.
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void ResetSwitch()
        {
            SaveData.TestSquadOfThreeEnabled = null;
        }

        private static List<string> FlaggedStarters() =>
            ContentDatabase.Characters
                .Where(c => c != null && c.Data.StartsInSquad)
                .OrderBy(c => c.Data.SquadSlot)
                .Select(c => c.id)
                .ToList();

        [Test]
        public void ASquadNamingARenamedCharacter_ReSeatsRatherThanShrinking()
        {
            var starters = FlaggedStarters();
            Assert.AreEqual(3, starters.Count,
                "this test needs content's three flagged starters to say who should be re-seated");

            Assert.IsNull(ContentDatabase.GetCharacter(RenamedAwayId),
                $"'{RenamedAwayId}' still resolves, so this fixture no longer describes a rename");

            // The save as it was written before the middle starter was
            // renamed: the old id in the roster and in the squad.
            var save = new SaveData
            {
                roster = new List<Character>
                {
                    new Character(starters[0]),
                    new Character(RenamedAwayId),
                    new Character(starters[2]),
                },
                selectedCharacterIds = new List<string>
                {
                    starters[0], RenamedAwayId, starters[2],
                },
            };

            save.Reconcile();

            Assert.AreEqual(3, save.selectedCharacterIds.Count,
                "the squad shrank to two instead of re-seating the renamed member");
            CollectionAssert.DoesNotContain(save.selectedCharacterIds, RenamedAwayId,
                "an id naming no content stayed in the squad");
            CollectionAssert.Contains(save.selectedCharacterIds, starters[1],
                "the freed seat should go to the flagged starter the rename produced");

            // THE ORDER IS PART OF THE ANSWER, not incidental: seat 0 is the
            // front rank enemy melee concentrates on (PartySeat's own header),
            // so where the top-up lands is a combat fact. It APPENDS -- the
            // two survivors keep their seats and the newcomer takes the one
            // left over, which is what "only ever ADDS, never reorders"
            // means in Reconcile's own words.
            CollectionAssert.AreEqual(
                new[] { starters[0], starters[2], starters[1] },
                save.selectedCharacterIds,
                "the top-up should append rather than restore content's own squad order");

            CollectionAssert.DoesNotContain(
                save.roster.Select(c => c.definitionId).ToList(), RenamedAwayId,
                "the roster kept a character content no longer carries");
            CollectionAssert.Contains(
                save.roster.Select(c => c.definitionId).ToList(), starters[1],
                "the roster never gained the character the rename produced");
        }

        // The other half, and the reason the first assertion above is worth
        // stating as a count: an empty selection falls back to the whole
        // roster, so a squad that silently shrank to two would still field
        // three and hide itself.
        [Test]
        public void ARenamedCharacterIsAlsoDroppedFromTheRunsCarriedHealth()
        {
            var starters = FlaggedStarters();
            Assert.AreEqual(3, starters.Count, "this test needs content's three flagged starters");

            var save = new SaveData
            {
                roster = new List<Character> { new Character(starters[0]) },
                selectedCharacterIds = new List<string> { starters[0] },
            };

            save.activeRun = new RunSnapshot
            {
                hasRun = true,
                currentHealth = new List<RunHealthEntry>
                {
                    new RunHealthEntry { characterId = starters[0], hp = 4 },
                    new RunHealthEntry { characterId = RenamedAwayId, hp = 9 },
                },
            };

            save.Reconcile();

            var ids = save.activeRun.currentHealth.Select(e => e.characterId).ToList();
            CollectionAssert.DoesNotContain(ids, RenamedAwayId,
                "a health entry against a character content no longer carries survived Reconcile");
            CollectionAssert.Contains(ids, starters[0],
                "the surviving character's carried health was thrown away with the ghost's");
        }

        // ================================================================
        // The 2 -> 3 ember migration, against the same rename.
        // ================================================================

        // MoveEmbersOntoTheRoster hands the whole pooled balance to the FIRST
        // roster member and clears the wallet. It ran BEFORE Reconcile, so the
        // roster it handed them to was the one still on disk -- and if that
        // first member was a character since renamed away, Reconcile's very
        // next act was to drop them from the roster, embers and all. Nothing
        // reported it: a wallet cleared and a character removed both look
        // exactly like themselves.
        [Test]
        public void MigratingASaveWhoseFirstRosterMemberWasRenamed_KeepsItsEmbers()
        {
            var save = new SaveData
            {
                version = 2,
                roster = new List<Character> { new Character(RenamedAwayId) },
                selectedCharacterIds = new List<string>(),
            };

            save.wallet.embers = 7;

            Assert.IsTrue(save.Migrate(), "a version 2 save should still migrate");

            Assert.AreEqual(7, save.EmberTotal(),
                "the pooled embers went to a roster member Reconcile then deleted");
        }

        // The same clearing, one step earlier: with nothing to move them onto
        // the wallet was emptied anyway, so the embers were gone before
        // Reconcile had rebuilt a roster that could have held them.
        [Test]
        public void MigratingASaveWithNoRosterAtAll_KeepsItsEmbers()
        {
            var save = new SaveData
            {
                version = 2,
                roster = new List<Character>(),
                selectedCharacterIds = new List<string>(),
            };

            save.wallet.embers = 5;

            Assert.IsTrue(save.Migrate(), "a version 2 save should still migrate");

            Assert.AreEqual(5, save.EmberTotal(),
                "the wallet was cleared with nobody to receive what was in it");
        }

        // ---- the two lists Reconcile walked past ---------------------------
        //
        // Reconcile prunes stockpiledItems, activeRun.inventory,
        // activeRun.currentHealth, learnedSpells, unassignedSpellBooks,
        // shopStock, unlockedTalentIds twice over, equipment,
        // purchasedUpgradeIds and selectedCharacterIds -- and then stopped
        // short of activeRun.relicIds and Character.unlockedSkillIds.
        //
        // Its own posture, stated where stockpiledItems is pruned: drop the
        // reference, keep the save loadable.
        //
        // The relic half is not cosmetic. FightEncounterAdapter.ResolveRelics
        // skips an unresolvable id harmlessly, but
        // RunOrchestrator.DraftHasAnotherRound counts relicIds.Count against
        // RelicPool.StartingRelicsPerDescent -- so a relic renamed mid-draft
        // ends the draft a round early, one relic short, silently.

        [Test]
        public void ARenamedRelicLeavesTheRunsList()
        {
            var save = new SaveData();
            save.activeRun.hasRun = true;
            save.activeRun.relicIds = new List<string> { "no_such_relic_was_ever_authored" };

            save.Reconcile();

            CollectionAssert.IsEmpty(save.activeRun.relicIds,
                "an id naming no relic stayed on the run, where the draft counts it as one taken");
        }

        [Test]
        public void ARealRelicSurvivesTheSamePrune()
        {
            var relic = ContentDatabase.Relics.First(r => r != null);

            var save = new SaveData();
            save.activeRun.hasRun = true;
            save.activeRun.relicIds = new List<string> { relic.id, "no_such_relic_was_ever_authored" };

            save.Reconcile();

            CollectionAssert.AreEqual(new List<string> { relic.id }, save.activeRun.relicIds,
                "the prune took a relic the player actually drafted");
        }

        [Test]
        public void ARenamedGiftedSkillLeavesTheCharacterThatHeldIt()
        {
            // unlockedSkillIds has no writer today -- the Event room's mage is
            // not built -- so this pins the plumbing rather than a reachable
            // path, the same way the modifierIds prune beside it does.
            var save = new SaveData();
            save.roster = new List<Character> { new Character(FlaggedStarters()[0]) };
            save.roster[0].unlockedSkillIds = new List<string> { "no_such_skill_was_ever_authored" };

            save.Reconcile();

            var character = save.roster.First(c => c.definitionId == FlaggedStarters()[0]);
            CollectionAssert.IsEmpty(character.unlockedSkillIds,
                "a gifted skill naming no content stayed on the character");
        }
    }
}
