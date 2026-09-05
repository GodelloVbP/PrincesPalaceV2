using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // SaveData.TestSquadOfThreeEnabled: the single switch that decides
    // whether a save fields a three-member squad. Null (its default) means
    // "derive from content" -- see the comment on SaveData.SquadOfThreeReady
    // -- and true/false force it either way. BalanceBotRunner.RunBatch is the
    // one place that forces it on (and back to null) today.
    //
    // WHO THE THREE ARE IS READ FROM CONTENT HERE, not spelled out. These
    // assertions used to carry the literal list { sheep, placeholder_brawler,
    // owl } and describe it as "characters.json's own file order" -- which was
    // true, and was the bug: a character appended to the file was in the
    // roster and unfieldable, one inserted third silently benched somebody,
    // and this test failed on the id rather than on the rule. characters.json
    // flags its three starters now (startsInSquad/squadSlot, refused by
    // CharacterEntryResolver unless exactly three do), so what is asserted is
    // that a fresh save fields WHOEVER IS FLAGGED, in slot order. Adding a
    // character stops being a test edit.
    //
    // PlayMode, not EditMode: SaveData and ContentDatabase are both Core
    // types, and PrincesPalace.Domain.Tests.asmdef (EditMode) references only
    // the engine-free Domain assembly. tools/domain-tests's exclusion list has
    // its own comment pointing here for the same reason.
    public class SaveDataSquadOfThreeTests
    {
        [TearDown]
        public void ResetSwitch()
        {
            SaveData.TestSquadOfThreeEnabled = null;
        }

        [Test]
        public void EffectiveMaxSquadSize_WithTheSwitchForcedOn_Returns3()
        {
            SaveData.TestSquadOfThreeEnabled = true;
            var save = new SaveData();

            Assert.AreEqual(3, save.EffectiveMaxSquadSize());
        }

        [Test]
        public void EffectiveMaxSquadSize_WithTheSwitchForcedOff_StaysPinnedAtOne()
        {
            SaveData.TestSquadOfThreeEnabled = false;
            var save = new SaveData();

            // Pinned at 1, not derived, so this fails loudly if an explicit
            // false override ever stops actually overriding.
            Assert.AreEqual(1, save.EffectiveMaxSquadSize());
        }

        [Test]
        public void EffectiveMaxSquadSize_WithTheSwitchAtItsDefault_Returns3BecauseContentFlagsThree()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            var save = new SaveData();

            // The live behaviour this whole switch exists for: nobody has to
            // flip anything once three characters carry startsInSquad, which
            // they do (characters.json) and which the resolver enforces.
            Assert.AreEqual(3, save.EffectiveMaxSquadSize());
        }

        // The starting squad as content states it: flagged characters, in
        // squadSlot order. Read the same way SaveData reads it, which is the
        // only way this can be a test of the rule rather than a second copy
        // of the list.
        private static List<string> FlaggedStarters() =>
            ContentDatabase.Characters
                .Where(c => c != null && c.data.StartsInSquad)
                .OrderBy(c => c.data.SquadSlot)
                .Select(c => c.id)
                .ToList();

        [Test]
        public void ContentFlagsExactlyThreeStartersWithDistinctSlots()
        {
            // The premise every other test here rests on, asserted rather
            // than assumed. CharacterEntryResolver refuses a file that breaks
            // it, so this failing means an asset reached Resources/Content
            // without passing through the resolver -- CLAUDE.md's
            // [CreateAssetMenu] hazard, which is exactly the case the
            // resolver cannot see.
            var starters = ContentDatabase.Characters.Where(c => c != null && c.data.StartsInSquad).ToList();

            Assert.AreEqual(3, starters.Count,
                "characters.json must flag exactly three: " +
                string.Join(", ", starters.Select(c => c.id)));

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, starters.Select(c => c.data.SquadSlot).ToList(),
                "the three slots are 1, 2 and 3, each used once");
        }

        [Test]
        public void CreateNew_WithTheSwitchOn_FieldsWhoeverContentFlagsInSlotOrder()
        {
            SaveData.TestSquadOfThreeEnabled = true;
            var save = SaveData.CreateNew();

            var ids = save.ActiveSquadIds();

            Assert.AreEqual(3, ids.Count);
            CollectionAssert.AreEqual(FlaggedStarters(), ids,
                "a fresh profile fields the characters content flags, in squadSlot order -- " +
                "not the first three rows of the file");
        }

        // EXISTING SAVES KEEP THEIR SQUAD. Reconcile only ever removes ids
        // content no longer has and tops up empty slots; it never reorders or
        // drops a selection the player made. A profile written before
        // startsInSquad existed therefore loads with exactly the squad it had,
        // even when that squad is not the one a fresh profile would get.
        [Test]
        public void ReconcileLeavesAnExistingSelectionAloneEvenWhenItIsNotTheDefault()
        {
            SaveData.TestSquadOfThreeEnabled = true;

            var save = SaveData.CreateNew();
            var reversed = FlaggedStarters().AsEnumerable().Reverse().ToList();
            save.selectedCharacterIds = new List<string>(reversed);

            save.Reconcile();

            CollectionAssert.AreEqual(reversed, save.ActiveSquadIds(),
                "a squad the player already has is theirs -- the content default is what a NEW profile gets");
        }

        // And the top-up, which is the one place the new order does reach an
        // existing save: a profile short a member gains the one a fresh
        // profile would, rather than whichever roster row sorts first.
        [Test]
        public void ReconcileTopsUpAShortSquadFromTheContentDefault()
        {
            SaveData.TestSquadOfThreeEnabled = true;

            var save = SaveData.CreateNew();
            var starters = FlaggedStarters();
            save.selectedCharacterIds = new List<string> { starters[2] };

            save.Reconcile();

            Assert.AreEqual(3, save.ActiveSquadIds().Count);
            Assert.AreEqual(starters[2], save.ActiveSquadIds()[0],
                "what was already selected stays where it was");
            CollectionAssert.AreEquivalent(starters, save.ActiveSquadIds(),
                "and the empty seats are filled from content's own starting squad");
        }

        [Test]
        public void DossierPaging_CyclesThroughAllThreeSquadMembersAndBackToTheFirst()
        {
            // Pins the exact arithmetic CharacterDossierController.Step uses
            // -- (_index + by + squad.Count) % squad.Count -- over the real
            // three-member squad a save actually produces.
            SaveData.TestSquadOfThreeEnabled = true;
            var save = SaveData.CreateNew();
            var squad = save.ActiveSquad();

            Assert.AreEqual(3, squad.Count);

            int index = 0;
            var visitedIds = new List<string> { squad[index].definitionId };
            for (int step = 0; step < 3; step++)
            {
                index = (index + 1 + squad.Count) % squad.Count;
                visitedIds.Add(squad[index].definitionId);
            }

            var starters = FlaggedStarters();
            CollectionAssert.AreEqual(
                new[] { starters[0], starters[1], starters[2], starters[0] }, visitedIds);
        }
    }
}
