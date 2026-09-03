using System.Collections.Generic;
using NUnit.Framework;

namespace PrincesPalace.PlayModeTests
{
    // SaveData.TestSquadOfThreeEnabled: the single switch that decides
    // whether a save fields a three-member squad (Shawn + placeholder_brawler
    // + placeholder_caster). Null (its default) means "derive from content" --
    // see the comment on SaveData.SquadOfThreeReady -- and true/false force it
    // either way. BalanceBotRunner.RunBatch is the one place that forces it on
    // (and back to null) today.
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
        public void EffectiveMaxSquadSize_WithTheSwitchAtItsDefault_Returns3BecauseBothPlaceholdersResolve()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            var save = new SaveData();

            // The live behaviour this whole switch exists for: nobody has to
            // flip anything once placeholder_brawler and placeholder_caster
            // are authored content, which they are (characters.json).
            Assert.AreEqual(3, save.EffectiveMaxSquadSize());
        }

        [Test]
        public void CreateNew_WithTheSwitchOn_FieldsAllThreeCharactersInFileOrder()
        {
            SaveData.TestSquadOfThreeEnabled = true;
            var save = SaveData.CreateNew();

            var ids = save.ActiveSquadIds();

            Assert.AreEqual(3, ids.Count);
            CollectionAssert.AreEqual(new[] { "sheep", "placeholder_brawler", "placeholder_caster" }, ids,
                "characters.json's own file order (CLAUDE.md gotcha 4)");
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

            CollectionAssert.AreEqual(
                new[] { "sheep", "placeholder_brawler", "placeholder_caster", "sheep" }, visitedIds);
        }
    }
}
