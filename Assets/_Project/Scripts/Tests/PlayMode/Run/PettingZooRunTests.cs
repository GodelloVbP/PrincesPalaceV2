using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // THE REAL petting_zoo THROUGH THE REAL RUN (docs/PLAN_PETTING_ZOO.md,
    // "Tests"): RunOrchestrator.ChooseEventOption against a save on disk, so
    // "reload" means the file. The content rules themselves (the page graph,
    // every counter branch, the peacock's pay) are pinned on the fast host in
    // PettingZooEventTests; this file pins what only the run can show -- HP
    // moving, the counter and relic landing once, and what survives a reload.
    //
    // One choice per visit (owner, 2026-09-25): a step page's "Say goodbye"
    // closes the event, a petter row concludes it with a result, and only
    // "Leave the sheep be" goes back to zoo.
    //
    // NEEDS BUILT CONTENT: petting_zoo must be in Resources/Content.
    public class PettingZooRunTests
    {
        private const string Zoo = "petting_zoo";
        private const string Counter = "zoo_sheep";

        // Authored positions on the pages (events.json); PettingZooEventTests
        // pins every page's row texts in this order.
        private const int PetTheSheep = 0;
        private const int SayGoodbye = 0;
        private const int BjornPets = 0;
        private const int OdettePets = 1;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-petting-zoo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- fixture ---------------------------------------------------------------------

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static RunSnapshot Run => RunManager.Run;

        // Squad, HP and counter are set BEFORE the open, whose own save puts
        // them on disk -- so a reload starts from exactly this state.
        private static void OpenTheZoo(string[] squad, int counter = 0, params (string id, int hp)[] health)
        {
            RunManager.StartRun(11UL);
            Save.selectedCharacterIds = squad.ToList();
            Save.SetEventCounter(Counter, counter);
            Run.relicIds.Clear();
            foreach (var (id, hp) in health) SetHp(id, hp);

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Zoo), "fixture: petting_zoo is not in the built content");
            Assert.AreEqual("zoo", Run.eventPageId);
        }

        private static void Reload()
        {
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        private static void SetHp(string characterId, int hp)
        {
            Run.currentHealth.RemoveAll(e => e != null && e.characterId == characterId);
            Run.currentHealth.Add(new RunHealthEntry { characterId = characterId, hp = hp });
        }

        private static int Hp(string characterId) => Run.currentHealth.First(e => e.characterId == characterId).hp;

        private static int MaxHp(string characterId) =>
            ContentDatabase.EffectiveStats(Save.roster.First(c => c.definitionId == characterId)).maxHealth;

        private static EventChoiceView Shown(int index) =>
            RunOrchestrator.CurrentEvent().Choices.First(c => c.Index == index);

        private static void AssertOnDisk(string page, int counter)
        {
            Reload();
            Assert.IsTrue(RunOrchestrator.EventIsOpen, "the zoo is still open after a reload");
            Assert.AreEqual(Zoo, Run.eventId);
            Assert.AreEqual(page, Run.eventPageId);
            Assert.AreEqual(counter, Save.EventCounter(Counter));
        }

        // ---- petting with Shawn ----------------------------------------------------------

        [Test]
        public void PettingWithShawnAndOdette_HealsShawnToFull_CountsOnce_AndShowsThePairScene()
        {
            OpenTheZoo(new[] { "sheep", "owl", "bear" }, 0, ("sheep", 10), ("owl", 10));

            var result = EventPicks.OnCurrentPage(PetTheSheep);

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual("step1_pair", Run.eventPageId);
            Assert.AreEqual(1, Save.EventCounter(Counter));
            Assert.AreEqual(MaxHp("sheep"), Hp("sheep"));
            Assert.AreEqual(10, Hp("owl"), "the heal is Shawn's alone");
            Assert.AreEqual("Shawn fully healed", result.EffectsLine);
            Assert.AreEqual("", result.ResultText, "the step page's lines are what the player reads");

            // The scene plays on the stage: Shawn and Odette, then narration.
            var lines = RunOrchestrator.CurrentEvent().Lines;
            CollectionAssert.AreEqual(new[] { "sheep", "owl", "sheep", "owl", "sheep", "" },
                lines.Select(l => l.IsNarration ? "" : l.SpeakerId).ToArray());
            Assert.IsTrue(lines.Last().IsNarration);
        }

        [Test]
        public void PettingWithOdetteBenched_ShowsTheSoloScene_AndStillCounts()
        {
            OpenTheZoo(new[] { "sheep", "bear" });

            EventPicks.OnCurrentPage(PetTheSheep);

            Assert.AreEqual("step1_solo", Run.eventPageId);
            Assert.AreEqual(1, Save.EventCounter(Counter));
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Lines.All(l => l.IsNarration || l.SpeakerId == "sheep"),
                "the solo scene is Shawn's alone");
        }

        [TestCase(8, "step9", false)]
        [TestCase(9, "step10", true)]
        [TestCase(10, "post_arc", true)]
        public void KinshipJoinsTheRunOnTheTenthPetAndAfter_NotTheNinth(int before, string page, bool kinship)
        {
            OpenTheZoo(new[] { "sheep", "owl", "bear" }, before);

            var result = EventPicks.OnCurrentPage(PetTheSheep);

            Assert.AreEqual(page, Run.eventPageId);
            Assert.AreEqual(before + 1, Save.EventCounter(Counter));
            Assert.AreEqual(kinship ? 1 : 0, Run.relicIds.Count(id => id == "kinship"));
            Assert.AreEqual(kinship ? "Shawn fully healed  ·  Relic: Kinship" : "Shawn fully healed", result.EffectsLine);
        }

        // ---- Shawn absent or down --------------------------------------------------------

        [Test]
        public void WithShawnBenched_ThePetGoesToPetter_AndNothingMoves()
        {
            OpenTheZoo(new[] { "owl", "bear" }, 3, ("owl", 10));

            var result = EventPicks.OnCurrentPage(PetTheSheep);

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual("petter", Run.eventPageId);
            Assert.AreEqual(3, Save.EventCounter(Counter));
            Assert.AreEqual(10, Hp("owl"));
            Assert.AreEqual("", result.EffectsLine);
        }

        // Heals never revive: a downed Shawn goes to petter, stays at 0 through
        // an ally's pet, and the ally's heal is the ally's alone. The ally's
        // pet is the visit's one choice, so the event concludes on its result.
        [Test]
        public void WithShawnDowned_ThePetGoesToPetter_AndAnAllysPetNeverRaisesHim()
        {
            OpenTheZoo(new[] { "sheep", "owl", "bear" }, 0, ("sheep", 0), ("bear", 5));

            EventPicks.OnCurrentPage(PetTheSheep);
            Assert.AreEqual("petter", Run.eventPageId);
            Assert.AreEqual(0, Save.EventCounter(Counter));

            var result = EventPicks.OnCurrentPage(BjornPets);

            Assert.AreEqual("", Run.eventPageId, "concluded: the result and a single Leave");
            Assert.IsTrue(RunOrchestrator.EventIsOpen);
            Assert.AreEqual("Bjorn scratches the sheep behind the ears with one enormous claw. It leans right into it.",
                result.ResultText);
            Assert.AreEqual(MaxHp("bear"), Hp("bear"));
            Assert.AreEqual(0, Hp("sheep"));
            Assert.AreEqual("Bjorn fully healed", result.EffectsLine);
        }

        [Test]
        public void ADownedAllysPetterRow_IsHidden_AndRefused()
        {
            OpenTheZoo(new[] { "owl", "bear" }, 0, ("bear", 0));
            EventPicks.OnCurrentPage(PetTheSheep);

            Assert.IsFalse(Shown(BjornPets).Visible);
            Assert.IsTrue(Shown(OdettePets).Visible);
            Assert.AreEqual(EventRefusal.Locked, EventPicks.OnCurrentPage(BjornPets).Reason);
            Assert.AreEqual(0, Hp("bear"));
            Assert.AreEqual("petter", Run.eventPageId);
        }

        // Declining is not a choice: back to the opening page, nothing moved,
        // and the sheep row is still there.
        [Test]
        public void LeaveTheSheepBe_ChangesNothing_AndGoesBackToTheZoo()
        {
            OpenTheZoo(new[] { "owl", "bear" }, 2, ("owl", 7), ("bear", 9));
            EventPicks.OnCurrentPage(PetTheSheep);
            int gold = Run.gold;

            int leaveBe = RunOrchestrator.CurrentEvent().Choices.First(c => c.Text == "Leave the sheep be").Index;
            var result = EventPicks.OnCurrentPage(leaveBe);

            Assert.AreEqual("zoo", Run.eventPageId);
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Choices.Any(c => c.Text == "Pet the sheep"));
            Assert.AreEqual("", result.EffectsLine);
            Assert.AreEqual(2, Save.EventCounter(Counter));
            Assert.AreEqual(7, Hp("owl"));
            Assert.AreEqual(9, Hp("bear"));
            Assert.AreEqual(gold, Run.gold);
        }

        // ---- reload ------------------------------------------------------------------------

        [Test]
        public void AReloadAfterEachStepOfAShawnPet_LandsOnTheSamePage_WithEverythingAppliedOnce()
        {
            OpenTheZoo(new[] { "sheep", "owl", "bear" }, 0, ("sheep", 10));
            int max = MaxHp("sheep");

            AssertOnDisk("zoo", 0);
            Assert.AreEqual(10, Hp("sheep"));

            EventPicks.OnCurrentPage(PetTheSheep);
            AssertOnDisk("step1_pair", 1);
            Assert.AreEqual(max, Hp("sheep"));

            // A second reload re-applies nothing.
            AssertOnDisk("step1_pair", 1);

            // Saying goodbye closes the event: the reload finds no zoo, and
            // the pet it already paid for stays paid once.
            var goodbye = EventPicks.OnCurrentPage(SayGoodbye);
            Assert.AreEqual(EventChoiceOutcome.Ok, goodbye.Outcome);
            Reload();
            Assert.IsFalse(RunOrchestrator.EventIsOpen, "the visit is over");
            Assert.AreEqual(1, Save.EventCounter(Counter));
            Assert.AreEqual(max, Hp("sheep"));
        }

        [Test]
        public void AReloadAfterEachStepOfAnAllysPet_LandsOnTheSamePage_WithEverythingAppliedOnce()
        {
            OpenTheZoo(new[] { "owl", "bear" }, 4, ("owl", 6));
            int max = MaxHp("owl");

            EventPicks.OnCurrentPage(PetTheSheep);
            AssertOnDisk("petter", 4);
            Assert.AreEqual(6, Hp("owl"));

            EventPicks.OnCurrentPage(OdettePets);
            AssertOnDisk("", 4);
            Assert.AreEqual(max, Hp("owl"));
            Assert.AreEqual("Odette preens a tuft of the sheep's wool back into place. The sheep allows this.",
                Run.eventResult, "the concluded result survives the reload");

            // A second reload re-applies nothing.
            AssertOnDisk("", 4);
            Assert.AreEqual(max, Hp("owl"));
        }

        // A save that fails leaves the file as it was, so after a reload the
        // pet never happened: same page, counter and HP as before the choice.
        // The failure is a real IO error, not a hook: a directory sits where
        // SaveSystem writes its temp file, so File.WriteAllText throws.
        [Test]
        public void ASaveThatThrows_LeavesNothingHalfAppliedAfterAReload()
        {
            OpenTheZoo(new[] { "sheep", "owl", "bear" }, 5, ("sheep", 10));

            string blocker = Path.Combine(_root, $"save_slot_{SaveSlotManager.CurrentSlot}.json.tmp");
            Directory.CreateDirectory(blocker);
            LogAssert.Expect(LogType.Error, new Regex("could not be written"));

            var result = EventPicks.OnCurrentPage(PetTheSheep);
            Assert.AreEqual(EventChoiceOutcome.AppliedNotPersisted, result.Outcome);

            Directory.Delete(blocker);
            AssertOnDisk("zoo", 5);
            Assert.AreEqual(10, Hp("sheep"));
            CollectionAssert.DoesNotContain(Run.relicIds, "kinship");
        }
    }
}
