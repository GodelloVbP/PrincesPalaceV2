using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // THE PAGE GUARD ON A PICK (RunOrchestrator.ChooseEventOption). A pick
    // names the page its index was read off; a second press that lands
    // before the repaint names the page the first one LEFT, and is refused
    // as StalePage without touching anything -- rather than being read
    // against the page the first press opened, which picked a row the
    // player never saw.
    //
    // One fixture event so the guard is proven without authored content,
    // and the real petting_zoo, where the double press was found.
    public class EventStalePageTests
    {
        private const string FixtureId = "stale_page_fixture";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-stale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            ContentDatabase.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static RunSnapshot Run => RunManager.Run;

        // Two pages whose index 0 is open on both and pays different gold,
        // so a stale press that got through would show in the purse.
        private static RawEventEntry TwoPageFixture() =>
            new RawEventEntry
            {
                id = FixtureId,
                floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "first",
                        title = "First",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Onward",
                                effects = new[] { new RawEventEffect { kind = "gold", amount = 5 } },
                                outcomes = new[] { new RawEventOutcome { goTo = "second" } },
                            },
                        },
                    },
                    new RawEventPage
                    {
                        id = "second",
                        title = "Second",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Back",
                                effects = new[] { new RawEventEffect { kind = "gold", amount = 7 } },
                                outcomes = new[] { new RawEventOutcome { goTo = "first" } },
                            },
                            new RawEventChoice
                            {
                                text = "Leave",
                                outcomes = new[] { new RawEventOutcome { result = "Gone.", goTo = "Leave" } },
                            },
                        },
                    },
                },
            };

        private static void OpenFixture()
        {
            FixtureEvents.Append(TwoPageFixture());
            RunManager.StartRun(11UL);
            Run.gold = 100;
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(FixtureId), "fixture: the event did not open");
            Assert.AreEqual("first", Run.eventPageId);
        }

        // ---- the guard ---------------------------------------------------------------

        [Test]
        public void ADoubleChooseOnOnePaintedPage_RefusesTheSecond_AndLeavesThePageTheFirstOpened()
        {
            OpenFixture();
            string painted = RunOrchestrator.CurrentEvent().PageId;

            var first = RunOrchestrator.ChooseEventOption(0, painted);
            var second = RunOrchestrator.ChooseEventOption(0, painted);

            Assert.AreEqual(EventChoiceOutcome.Ok, first.Outcome);
            Assert.AreEqual(EventChoiceOutcome.Refused, second.Outcome);
            Assert.AreEqual(EventRefusal.StalePage, second.Reason);
            Assert.AreEqual("second", Run.eventPageId, "the page the first press opened stays");
            Assert.AreEqual(105, Run.gold, "only the first press paid; the second never reached page 'second'");
            Assert.AreEqual("+5 gold", RunOrchestrator.CurrentEvent().EffectsLine, "the first press's line still shows");
        }

        [Test]
        public void TheRefusal_WritesNothingToDisk()
        {
            OpenFixture();
            RunOrchestrator.ChooseEventOption(0, "first");
            RunOrchestrator.ChooseEventOption(0, "first");

            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            Assert.AreEqual("second", Run.eventPageId);
            Assert.AreEqual(105, Run.gold);
        }

        [Test]
        public void APickOnTheCurrentPage_StillApplies()
        {
            OpenFixture();
            RunOrchestrator.ChooseEventOption(0, "first");

            var back = RunOrchestrator.ChooseEventOption(0, "second");

            Assert.AreEqual(EventChoiceOutcome.Ok, back.Outcome);
            Assert.AreEqual("first", Run.eventPageId);
            Assert.AreEqual(112, Run.gold);
        }

        // Concluded is eventPageId "": a press left over from the page it
        // concluded from is stale too, never a pick.
        [Test]
        public void APressLeftOverFromAPage_IsStaleOnTheConcludedState()
        {
            OpenFixture();
            RunOrchestrator.ChooseEventOption(0, "first");
            RunOrchestrator.ChooseEventOption(1, "second");
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded, "fixture: Leave with a result concludes");

            Assert.AreEqual(EventRefusal.StalePage, RunOrchestrator.ChooseEventOption(1, "second").Reason);
            Assert.IsTrue(RunOrchestrator.EventIsOpen, "the concluded event waits for its own Leave");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("FIRST")]
        [TestCase("nowhere")]
        public void AnyPageButTheCurrentOne_IsStale(string pageId)
        {
            OpenFixture();
            Assert.AreEqual(EventRefusal.StalePage, RunOrchestrator.ChooseEventOption(0, pageId).Reason);
            Assert.AreEqual("first", Run.eventPageId);
            Assert.AreEqual(100, Run.gold);
        }

        // ---- the real petting_zoo --------------------------------------------------------

        // On `petter`, "Bjorn pets the sheep" is index 0 and concludes the
        // event on its result (one choice per visit). The second press
        // arrives after the event has concluded, is refused as StalePage,
        // and spends nothing.
        // NEEDS BUILT CONTENT: petting_zoo in Resources/Content.
        [Test]
        public void PettingZoo_ADoubleBjornPets_IsRefusedAfterTheEventConcludes()
        {
            const int bjornPets = 0;

            RunManager.StartRun(11UL);
            Save.selectedCharacterIds = new[] { "sheep", "owl", "bear" }.ToList();
            SetHp("sheep", 0);
            SetHp("bear", 5);
            Run.gold = 100;
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug("petting_zoo"), "fixture: petting_zoo is not in the built content");

            // A downed Shawn sends the pet to petter.
            EventPicks.OnCurrentPage(0);
            Assert.AreEqual("petter", Run.eventPageId, "fixture: a downed Shawn's pet goes to petter");

            string painted = RunOrchestrator.CurrentEvent().PageId;
            int counterBefore = Save.EventCounter("zoo_sheep");

            var first = RunOrchestrator.ChooseEventOption(bjornPets, painted);
            Assert.AreEqual(EventChoiceOutcome.Ok, first.Outcome);
            Assert.AreEqual("", Run.eventPageId);
            string resultAfterFirst = Run.eventResult;
            int effectsAfterFirst = Run.eventResultEffects.Count;

            var second = RunOrchestrator.ChooseEventOption(bjornPets, painted);

            Assert.AreEqual(EventRefusal.StalePage, second.Reason);
            Assert.AreEqual("", Run.eventPageId, "the page stays on what the first press opened");
            Assert.AreEqual(100, Run.gold, "the second press spent nothing");
            Assert.AreEqual(resultAfterFirst, Run.eventResult);
            Assert.AreEqual(effectsAfterFirst, Run.eventResultEffects.Count, "the first press's effects line stands");
            Assert.AreEqual(counterBefore, Save.EventCounter("zoo_sheep"));
            Assert.IsTrue(RunOrchestrator.EventIsOpen);
        }

        private static void SetHp(string characterId, int hp)
        {
            Run.currentHealth.RemoveAll(e => e != null && e.characterId == characterId);
            Run.currentHealth.Add(new RunHealthEntry { characterId = characterId, hp = hp });
        }
    }
}
