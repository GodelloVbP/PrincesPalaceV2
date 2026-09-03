using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // SaveData.TestSquadOfThreeEnabled: the single switch that lets a save
    // field a three-member squad (Shawn + placeholder_brawler +
    // placeholder_caster) without touching the live game's own solo default.
    // See the comment on SaveData.BaseMaxSquadSize for why that default is
    // deliberate, and BalanceBotRunner.RunBatch for the one place that flips
    // this switch on (and back off) today.
    //
    // Runs through Unity, not `dotnet test` -- SaveData lives outside the
    // engine-free Domain assembly, so this class is on the Compile Remove
    // list in tools/domain-tests/PrincesPalace.Domain.Tests.csproj.
    public class SaveDataSquadOfThreeTests
    {
        [TearDown]
        public void ResetSwitch()
        {
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [Test]
        public void EffectiveMaxSquadSize_WithTheSwitchOn_Returns3()
        {
            SaveData.TestSquadOfThreeEnabled = true;
            var save = new SaveData();

            Assert.AreEqual(3, save.EffectiveMaxSquadSize());
        }

        [Test]
        public void EffectiveMaxSquadSize_WithTheSwitchOff_StaysAtTheLiveDefault()
        {
            SaveData.TestSquadOfThreeEnabled = false;
            var save = new SaveData();

            // Pinned at 1, not derived, so this fails loudly if the switch
            // ever leaks into the live default.
            Assert.AreEqual(1, save.EffectiveMaxSquadSize());
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
            var visitedIds = new System.Collections.Generic.List<string> { squad[index].definitionId };
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
