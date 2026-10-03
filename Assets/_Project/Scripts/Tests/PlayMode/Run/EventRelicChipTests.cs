using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // RELIC RECEIPTS ON THE EVENT PAGE (docs/PLAN_BUGS_QA.md C2/C3,
    // T9 T9b T9c T10): the "Gained" chips over the choice rows, on the shared
    // Map scene, driven by the pad through the production NavigationInputModule.
    //
    // The chips are visible while dialogue plays but join navigation only with
    // the rows; leaving a page acknowledges exactly what it showed.
    public class EventRelicChipTests
    {
        private const string Zoo = "petting_zoo";
        private const string ZooCounter = "zoo_sheep";
        private const int PetTheSheep = 0;

        private string _root;
        private ScriptedBaseInput _input;
        private MapController _map;
        private EventController _panel;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-chips-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void Restore()
        {
            if (_panel != null && _panel.IsOpen) _panel.gameObject.SetActive(false);
            SaveData.TestSquadOfThreeEnabled = null;
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RunSnapshot Run => RunManager.Run;

        // ---- T9: Kinship, by pad from the page's normal entry -------------------------------

        [UnityTest]
        public IEnumerator Kinship_TheChipShowsDuringDialogueButIsNotFocusable_ThenUpFromTheFirstChoiceReachesIt()
        {
            RunManager.StartRun(11UL);
            SaveSlotManager.CurrentSave.selectedCharacterIds = new List<string> { "sheep", "owl", "bear" };
            SaveSlotManager.CurrentSave.SetEventCounter(ZooCounter, 9);
            Run.relicIds.Clear();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Zoo), "fixture: petting_zoo is not in the built content");
            yield return OpenThePanel();

            yield return AdvanceToChoices();
            AssertSelected("EventChoice" + PetTheSheep, "entry is the first choice");
            yield return Submit();
            Assert.AreEqual("step10", Run.eventPageId, "fixture: the tenth pet did not reach step10");

            // DURING DIALOGUE: the chip is up and says Kinship, but it is not on
            // the rail, so Submit keeps advancing the lines.
            Assert.IsFalse(RowsShown, "precondition: the page's lines are playing");
            Assert.IsTrue(Node("EventGainedStrip").activeInHierarchy, "the chip is visible during dialogue");
            CollectionAssert.AreEqual(new[] { "kinship" }, _panel.DisplayedReceipts);
            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, Chip(0).navigation.mode, "not focusable while lines play");

            // Typing, then the line, then the next: each Submit moves one of them.
            var line = Node("StageLineText").GetComponent<TMP_Text>();
            string before = line.text + "|" + line.maxVisibleCharacters;
            yield return Submit();
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            Assert.IsFalse(Selected != null && Selected.StartsWith("EventGained"), "Submit never lands on the chip");
            Assert.IsTrue(RowsShown || line.text + "|" + line.maxVisibleCharacters != before,
                "Submit advanced the dialogue");

            yield return AdvanceToChoices();
            AssertSelected("EventChoice0", "the rows' entry is unchanged: the first choice");

            yield return Press(0f, 1f);
            AssertSelected("EventGainedChip0", "Up from the first choice is the chip rail");
            Assert.IsTrue(Node("EventGainedTooltip").activeSelf, "pad focus shows the tooltip");
            Assert.AreEqual("Kinship", Text("EventGainedTooltipName"));

            yield return Submit();
            AssertSelected("EventChoice0", "Submit on a chip returns to the first choice");

            StringAssert.DoesNotContain("Relic", RunOrchestrator.CurrentEvent().EffectsLine,
                "the text line never lists a relic: it is the chip");
            StringAssert.DoesNotContain("Relic", LineText);

            // Say goodbye: leaving the page acknowledges the chip it showed.
            yield return Submit();
            CollectionAssert.IsEmpty(Run.pendingRelicReceipts);
        }

        // ---- T9b: a relic-only Leave --------------------------------------------------------

        [UnityTest]
        public IEnumerator ARelicOnlyLeave_DoesNotCloseAtOnce_ShowsTheChip_AndLeaveAcknowledgesIt()
        {
            FixtureEvents.Append(RelicNames, new RawEventEntry
            {
                id = "chips_leave_fixture", mayReturn = true, floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "p", title = "A Gift", body = "Someone left something.",
                        choices = new[] { Choice("Take it", "Leave", "kinship") },
                    },
                },
            });
            RunManager.StartRun(11UL);
            Run.relicIds.Clear();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug("chips_leave_fixture"));
            int node = Run.currentNodeId;
            yield return OpenThePanel();

            yield return Submit();

            Assert.IsTrue(_panel.IsOpen, "a Leave with a relic and no text still has something to show");
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded);
            CollectionAssert.AreEqual(new[] { "kinship" }, _panel.DisplayedReceipts);
            Assert.IsTrue(Node("EventGainedStrip").activeInHierarchy);

            yield return Submit(); // the concluded page's own Leave

            Assert.IsFalse(_panel.IsOpen, "Leave closes it");
            CollectionAssert.IsEmpty(Run.pendingRelicReceipts, "and acknowledges the receipt it showed");
            CollectionAssert.Contains(Run.clearedNodeIds, node);
        }

        // ---- T9c: moving to another page ------------------------------------------------------

        [UnityTest]
        public IEnumerator LeavingAPage_AcknowledgesWhatItShowed_AndTheNextPageShowsItsOwnGrant()
        {
            FixtureEvents.Append(RelicNames, new RawEventEntry
            {
                id = "chips_pages_fixture", mayReturn = true, floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage { id = "zero", title = "Zero", body = "B", choices = new[] { Choice("On", "one", "kinship") } },
                    new RawEventPage { id = "one", title = "One", body = "B", choices = new[] { Choice("On", "two", "bellwethers_bell") } },
                    new RawEventPage { id = "two", title = "Two", body = "B", choices = new[] { Choice("Leave", "Leave", null) } },
                },
            });
            RunManager.StartRun(11UL);
            Run.relicIds.Clear();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug("chips_pages_fixture"));
            yield return OpenThePanel();

            yield return Submit();
            Assert.AreEqual("one", Run.eventPageId);
            CollectionAssert.AreEqual(new[] { "kinship" }, _panel.DisplayedReceipts, "page one shows page zero's grant");

            yield return Submit();
            Assert.AreEqual("two", Run.eventPageId);
            CollectionAssert.AreEqual(new[] { "bellwethers_bell" }, _panel.DisplayedReceipts,
                "page one's receipt was acknowledged on leaving it; page two shows its own grant");

            SaveSlotManager.Forget();
            CollectionAssert.AreEqual(new[] { "bellwethers_bell" }, RunManager.Run.pendingRelicReceipts,
                "the acknowledgement rode the pick's own write");
        }

        // ---- T10: settled, then reloaded before the Reckoning ever opened ---------------------

        [UnityTest]
        public IEnumerator BellwetherDefeated_ReloadedBeforeTheReckoning_TheEventPageShowsBothChips()
        {
            RunManager.StartRun(29UL);
            Run.relicIds.Clear();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug("bell_in_the_fog"));
            Assert.AreEqual(EventChoiceOutcome.Ok, EventPicks.OnCurrentPage(0).Outcome, "fixture: Touch the bell");

            var session = RunOrchestrator.BuildFight().Session;
            var shawn = session.Encounter.PlayerParty.Single();
            shawn.MaxHealth = shawn.CurrentHealth = 1_000_000;
            session.Encounter.Enemies.Single().CurrentHealth = 1;
            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 50 && !session.IsOver; i++)
            {
                session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.AreEqual(FightEndReason.Defeated, session.EndReason, "fixture: the Bellwether did not fall");
            RunOrchestrator.SettleFight(session, true);

            // The crash: nothing after settlement ran. The reload reads disk.
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            yield return OpenThePanel();

            Assert.AreEqual("break", Run.eventPageId);
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock", "bellwethers_bell" }, _panel.DisplayedReceipts);
            Assert.IsTrue(Node("EventGainedStrip").activeInHierarchy);
            Assert.AreEqual(2, Enumerable.Range(0, RelicChipTree.ChipCount).Count(i => Chip(i).gameObject.activeSelf));
        }

        // ---- fixture ------------------------------------------------------------------------

        private static readonly Dictionary<string, string> RelicNames = new Dictionary<string, string>
        {
            { "kinship", "Kinship" },
            { "bellwethers_bell", "Bellwether's Bell" },
        };

        private static RawEventChoice Choice(string text, string goTo, string relic) =>
            new RawEventChoice
            {
                text = text,
                outcomes = new[]
                {
                    new RawEventOutcome
                    {
                        goTo = goTo,
                        effects = relic == null
                            ? new RawEventEffect[0]
                            : new[] { new RawEventEffect { kind = "relic", relic = relic } },
                    },
                },
            };

        private IEnumerator OpenThePanel()
        {
            SaveSlotManager.SaveCurrent();
            yield return SharedScene.Ensure("Map");
            _input = NavSceneReuse.TakeOverInput();

            _map = UnityEngine.Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = UnityEngine.Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            var menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu.IsOpen) menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();
            _map.Refresh();
            yield return null;

            _panel.Finished = _map.Refresh;
            _panel.Open();
            yield return null;
            yield return null;
        }

        private GameObject Node(string name) =>
            _panel.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name)?.gameObject;

        private string Text(string name) => Node(name)?.GetComponent<TMP_Text>()?.text;

        private Button Chip(int index) => Node("EventGainedChip" + index).GetComponent<Button>();

        private string LineText => Text("StageLineText") ?? "";

        private bool RowsShown =>
            Enumerable.Range(0, EventScreen.ChoiceRowCount)
                .Select(i => Node($"EventChoice{i}"))
                .Any(g => g != null && g.activeInHierarchy);

        private static string Selected => EventSystem.current.currentSelectedGameObject?.name;

        private static void AssertSelected(string name, string because) => Assert.AreEqual(name, Selected, because);

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null;
        }

        private static IEnumerator WaitReal(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }

        private IEnumerator AdvanceToChoices()
        {
            for (int presses = 0; presses < 40 && !RowsShown; presses++)
            {
                yield return Submit();
                if (RowsShown) break;
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            Assert.IsTrue(RowsShown, "the rows never appeared");
            yield return null;
            yield return null;
        }
    }
}
