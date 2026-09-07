using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // Prince's Favor reaching the loot roll.
    //
    // NONE OF THIS WAS COVERED before the reward track needed it.
    // ItemOfferRoll.SquadFavor had exactly one caller and no test, and its one
    // caller -- CurrentSquadFavor, which is what FightController hands to the
    // roll -- had none either. So the max-not-sum rule, which is the whole
    // reason Favor is a reason to field a particular character, was resting on
    // a comment.
    //
    // PlayMode rather than EditMode because ItemOfferRoll is Core: the EditMode
    // assembly references Domain and nothing else. Nothing here needs a scene.
    public class ItemOfferFavorTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-favor-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CharacterDefinition DefinitionFor(Character character) =>
            ContentDatabase.Characters.FirstOrDefault(d => d != null && d.id == character.definitionId);

        private static int AuthoredFavorOf(Character character) =>
            DefinitionFor(character)?.Data.PrincesFavor ?? 0;

        [Test]
        public void AuthoredFavorStillCountsWhenNoneHasBeenEarned()
        {
            var squad = SaveSlotManager.CurrentSave.ActiveSquad();
            int authoredBest = squad.Max(AuthoredFavorOf);

            Assert.AreEqual(authoredBest, ItemOfferRoll.CurrentSquadFavor(),
                "a squad that has earned nothing should still bring what it was authored with");
        }

        // The rule the whole stat exists for, now that there are two sources
        // for it. Summing would make Favor scale with squad size, so the
        // decision would become "bring more bodies" rather than "bring the
        // character who has it".
        [Test]
        public void TheSquadBringsItsHighestFavor_NotTheSum()
        {
            Assert.AreEqual(9, ItemOfferRoll.SquadFavor(new[] { 4, 9, 2 }));
            Assert.AreEqual(0, ItemOfferRoll.SquadFavor(new int[0]));
            Assert.AreEqual(0, ItemOfferRoll.SquadFavor(null));
        }

        // Favor is not a free dial: LootLadder caps the per-rung step chance at
        // MaxStep, and each encounter class reaches that cap at a different
        // Favor. This pins the ceilings rather than the arithmetic, because the
        // track's Favor nodes are spread across all ten bands on the strength
        // of them -- back-loading would buy nothing for two of the three
        // classes.
        //
        // BOSS CAPS AT 29, not the 28 the handover records. 0.38 + 28 * 0.006
        // is 0.548, which is still under the 0.55 cap; 29 is the first Favor
        // that reaches it. Elite (42) and normal (55) in that document are
        // right. Pinned here as one-below/at-cap pairs so the boundary itself
        // is asserted and not just the plateau past it.
        [TestCase(Domain.Rewards.EncounterClass.Boss, 29)]
        [TestCase(Domain.Rewards.EncounterClass.Elite, 42)]
        [TestCase(Domain.Rewards.EncounterClass.Normal, 55)]
        public void FavorStopsBuyingAnythingOnceTheLadderCaps(
            Domain.Rewards.EncounterClass encounter, int favorAtCap)
        {
            float justBelow = Domain.Rewards.LootLadder.StepChanceFor(encounter, favorAtCap - 1);
            float atCap = Domain.Rewards.LootLadder.StepChanceFor(encounter, favorAtCap);
            float wellPast = Domain.Rewards.LootLadder.StepChanceFor(encounter, 500);

            Assert.Less(justBelow, Domain.Rewards.LootLadder.MaxStep,
                $"{encounter} was already capped one Favor below {favorAtCap}");
            Assert.AreEqual(Domain.Rewards.LootLadder.MaxStep, atCap, 0.0001f,
                $"{encounter} had not reached the cap at {favorAtCap} Favor");
            Assert.AreEqual(atCap, wellPast, 0.0001f,
                "Favor past the cap still moved the step chance, so the cap is not holding");
        }
    }
}
