using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // THE EVENT PANEL, in the real Map scene, driven the way a player drives
    // it: a walk into the room, pointer clicks on rows, and pad input through
    // the real NavigationInputModule (ShopGamepadNavigationTests' rig).
    //
    // The event is demo_wishing_well, the only authored one, so a pool of one
    // always picks it. Its words are pinned as literals here on purpose --
    // this is the file that notices when the panel shows something other than
    // what the author wrote.
    public class EventPanelTests
    {
        private const string DemoCounter = "wishing_well_tosses";
        private const string TossText = "Toss a coin (5 gold)";
        private const string ShawnText = "Call on Shawn's luck";
        private const string LevelText = "Recite a well-worn prayer";

        private string _root;
        private ScriptedBaseInput _input;
        private MapController _map;
        private EventController _panel;
        private DescentNode _eventNode;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-panel-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- fixture ------------------------------------------------------------------

        private static ulong SeedWithAnEventInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Event)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated an event in the first column.");
        }

        // The map, with no event open yet: the party stands at the entry.
        //
        // THE MAP IS SHARED ACROSS THIS FIXTURE (SharedScene). Each test starts
        // a new run on a fresh save root ([SetUp]), and the map reads the run
        // live, so putting the scene back is: close whatever the last test
        // left up (the system menu, the event panel), repaint the map for the
        // new run -- which also snaps the walker and the camera back to the
        // entry -- and hand the input module a fresh scripted input. On a
        // fresh load all of it finds nothing to undo.
        private IEnumerator LoadTheMap()
        {
            RunManager.StartRun(SeedWithAnEventInTheFirstColumn());
            _eventNode = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Event);
            RunManager.Run.gold = 100;
            SaveSlotManager.SaveCurrent();

            yield return SharedScene.Ensure("Map");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the map scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            var stale = module.GetComponent<ScriptedBaseInput>();
            if (stale != null) Object.DestroyImmediate(stale);
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            _panel = Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_panel, "the Map scene has no EventController");

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu.IsOpen) menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false); // OnDisable pops its nav context
            Assert.IsFalse(_map.IsWalking, "fixture: the previous test left a walk in flight");
            _map.Refresh();
            EventSystem.current.SetSelectedGameObject(null);

            yield return null;
            yield return null;
        }

        // How much faster than real time the walk into the room runs. The walk
        // (MapController.WalkAndArrive: MapWalk.DurationFor, then the
        // MapWalk.PanSeconds camera pan) is a scaled-time animation of about
        // 1.4s, and it was 12.6s of this fixture's 18.5s. Nothing here tests
        // its length -- MapWalk's own tests do -- only that it ends in Arrive.
        private const float WalkClockScale = 40f;

        // Into the event room by the real door: a click on its node, the walk,
        // and Arrive -> Arrival.Event -> OpenEvent. The walk runs on a fast
        // clock and is polled, not waited out; the timeout is real seconds.
        private IEnumerator WalkIntoTheEvent()
        {
            yield return LoadTheMap();

            Time.timeScale = WalkClockScale;
            NodeButton(_eventNode).onClick.Invoke();
            for (float waited = 0f; waited < 5f && _map.IsWalking; waited += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1f;
            Assert.IsFalse(_map.IsWalking, "the walk should have finished well inside 5 seconds");

            yield return null;
            yield return null;
        }

        private Button NodeButton(DescentNode node)
        {
            var column = RunManager.Map.AtDepth(node.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == node.Id);
            var go = _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == $"MapNode{MapLayout.IndexFor(node.Depth, slot)}")?.gameObject;
            Assert.IsNotNull(go, $"the map has no node button for room {node.Id}");
            return go.GetComponent<Button>();
        }

        private Button[] ActiveRows() =>
            Enumerable.Range(0, EventScreen.ChoiceRowCount)
                .Select(i => Find($"EventChoice{i}")?.GetComponent<Button>())
                .Where(b => b != null && b.gameObject.activeInHierarchy)
                .ToArray();

        private GameObject Find(string name) =>
            _panel.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private TMP_Text Text(string name) => Find(name).GetComponent<TMP_Text>();

        private static string RowText(Button row) => row.transform.Find(row.name + "Text").GetComponent<TMP_Text>().text;

        private static TMP_Text RowLock(Button row) => row.transform.Find(row.name + "Lock").GetComponent<TMP_Text>();

        private Button Row(string text)
        {
            var row = ActiveRows().FirstOrDefault(b => RowText(b) == text);
            Assert.IsNotNull(row, $"no visible row reads '{text}'; rows read: " +
                                  string.Join(" | ", ActiveRows().Select(RowText)));
            return row;
        }

        // A real pointer click: Button.OnPointerClick asks IsInteractable()
        // first, which onClick.Invoke() would skip.
        private static IEnumerator Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator MoveDown()
        {
            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        // ---- arrival --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WalkingIntoAnEventRoom_OpensThePanelOnItsFirstPage()
        {
            yield return WalkIntoTheEvent();

            Assert.IsTrue(_panel.IsOpen, "arriving at an event room should open its panel");
            Assert.AreEqual("A Wishing Well", Text("EventTitle").text);
            StringAssert.StartsWith("A mossy well sits at the crossing", Text("EventBody").text);
            Assert.AreEqual("", Text("EventEffects").text, "nothing has been chosen yet");
            Assert.AreEqual(4, ActiveRows().Length, "the well page authors four visible choices");
        }

        // ---- locks ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ALockedChoice_ShowsItsReason_AndCannotBeClickedOrReachedByThePad()
        {
            yield return WalkIntoTheEvent();

            var locked = Row(LevelText);
            Assert.IsFalse(locked.interactable, "fixture: a fresh party has no level 15 member, so this is locked");

            var reason = RowLock(locked);
            Assert.IsTrue(reason.gameObject.activeSelf, "a locked row shows why");
            Assert.AreEqual("Requires a level 15 party member", reason.text);
            Assert.IsFalse(RowLock(Row(TossText)).gameObject.activeSelf, "an open row carries no lock caption");

            string pageBefore = RunManager.Run.eventPageId;
            string bodyBefore = Text("EventBody").text;
            yield return Click(locked);
            Assert.AreEqual(pageBefore, RunManager.Run.eventPageId, "a click on a locked row changed the page");
            Assert.AreEqual(bodyBefore, Text("EventBody").text, "a click on a locked row repainted a result");

            // Walk the whole rail and back round: the locked row is never
            // where the pad lands.
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject, "the panel should select its entry");
            for (int step = 0; step < EventScreen.ChoiceRowCount * 2; step++)
            {
                Assert.AreNotEqual(locked.gameObject, EventSystem.current.currentSelectedGameObject,
                    $"the pad reached the locked row after {step} Down presses");
                yield return MoveDown();
            }
        }

        // ---- choosing -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ChoosingRepaintsTheResultInPlaceOfTheBody_WithTheEffectsLine()
        {
            yield return WalkIntoTheEvent();

            yield return Click(Row(TossText));

            Assert.IsTrue(_panel.IsOpen, "a toss loops back to the well; the panel stays up");
            Assert.AreEqual("The coin sinks without a sound.", Text("EventBody").text);
            Assert.AreEqual("-5 gold", Text("EventEffects").text);
            Assert.AreEqual(95, RunManager.Run.gold);
            Assert.AreEqual(4, ActiveRows().Length, "the well's own four choices come back");
        }

        [UnityTest]
        public IEnumerator AHiddenChoiceTakesNoRow()
        {
            yield return WalkIntoTheEvent();
            SaveSlotManager.CurrentSave.SetEventCounter(DemoCounter, 9);

            // The tenth toss: its own page, whose second choice is
            // hiddenUntilMet behind CHA 20.
            yield return Click(Row(TossText));

            var view = RunOrchestrator.CurrentEvent();
            Assert.AreEqual("wish_granted", view.PageId);
            Assert.AreEqual(2, view.Choices.Count, "fixture: the page authors two choices");
            Assert.IsFalse(view.Choices[1].Visible, "fixture: no starting member has 20 CHA, so it stays hidden");

            var rows = ActiveRows();
            Assert.AreEqual(1, rows.Length, "the hidden choice took a row");
            Assert.AreEqual("Leave", RowText(rows[0]));
            Assert.AreEqual("On the tenth coin the well glimmers gold -- your wish is granted.", Text("EventBody").text);
            Assert.AreEqual("-5 gold  ·  +50 XP", Text("EventEffects").text);
        }

        // ---- concluding and leaving --------------------------------------------------

        [UnityTest]
        public IEnumerator AConcludedEventShowsOnlyLeave_AndLeaveClosesThePanelAndRedrawsTheMap()
        {
            yield return WalkIntoTheEvent();
            CollectionAssert.Contains(SaveSlotManager.CurrentSave.ActiveSquadIds(), "sheep",
                "fixture: Shawn starts in the squad, so his choice is open");

            yield return Click(Row(ShawnText));

            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded, "fixture: Shawn's choice leaves with a result");
            var rows = ActiveRows();
            Assert.AreEqual(1, rows.Length, "a concluded event offers exactly one thing to do");
            Assert.AreEqual("Leave", RowText(rows[0]));
            Assert.AreEqual("Shawn's luck pays off -- a few coins surface.", Text("EventBody").text);
            Assert.AreEqual("+10 gold", Text("EventEffects").text);
            Assert.AreEqual("A Wishing Well", Text("EventTitle").text,
                "the header stays on the page the player chose from");
            Assert.AreEqual(rows[0].gameObject, EventSystem.current.currentSelectedGameObject,
                "the pad lands on Leave, the only thing left");

            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, _eventNode.Id,
                "fixture: the room clears on Leave, not before");

            // The map's own gold line is painted by MapController.Refresh and
            // nothing else, so it still shows the purse from before Shawn's
            // coins until the panel hands the map its Finished.
            var mapGold = GameObject.Find("MapGoldLabel").GetComponent<TMP_Text>();
            Assert.AreEqual("100 GOLD", mapGold.text, "fixture: the map has not repainted while the event is up");

            yield return Click(rows[0]);

            Assert.IsFalse(_panel.IsOpen, "Leave should close the panel");
            Assert.IsFalse(RunOrchestrator.EventIsOpen);
            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, _eventNode.Id, "Leave clears the room");
            Assert.AreEqual("110 GOLD", mapGold.text, "Leave did not hand control back to MapController.Refresh");
        }

        // ---- input ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CancelDoesNothing()
        {
            yield return WalkIntoTheEvent();
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "fixture: the panel selects its entry");

            _input.CancelDown = true;
            yield return DriveFrame();
            yield return null;

            Assert.IsTrue(_panel.IsOpen, "Cancel closed the event panel");
            Assert.IsTrue(RunOrchestrator.EventIsOpen, "Cancel left the event");
            Assert.AreEqual(selected, EventSystem.current.currentSelectedGameObject, "Cancel moved the selection");
        }

        [UnityTest]
        public IEnumerator StartOpensTheSystemMenuOverTheEvent()
        {
            yield return WalkIntoTheEvent();
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsFalse(menu.IsOpen, "fixture: the menu starts closed");

            _input.SystemMenuDown = true;
            yield return DriveFrame();

            Assert.IsTrue(menu.IsOpen, "Start over the event panel should open the system menu");
            Assert.IsTrue(_panel.IsOpen, "the event stays up underneath the menu");
        }

        [UnityTest]
        public IEnumerator SubmitOnARowPicksIt()
        {
            yield return WalkIntoTheEvent();
            EventSystem.current.SetSelectedGameObject(Row(TossText).gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual("The coin sinks without a sound.", Text("EventBody").text);
        }

        // ---- art and text -------------------------------------------------------------

        // The demo authors no art, which is the case under test: the frame
        // stays, the picture does not draw.
        [UnityTest]
        public IEnumerator MissingArtHidesTheImageAndKeepsTheFrame()
        {
            yield return WalkIntoTheEvent();
            Assert.AreEqual("", RunOrchestrator.CurrentEvent().ArtKey, "fixture: the demo page has no art");

            Assert.IsFalse(Find("EventArt").GetComponent<Image>().enabled, "a missing picture drew anyway");
            Assert.IsTrue(Find("EventArtFrame").activeInHierarchy, "the frame should hold the layout");
        }

        // THE CONTENT BUILD'S CAP, MEASURED IN THE BOX IT HAS TO FIT. The real
        // emitted label -- its font, its role's line spacing, its wrap width
        // -- at its full authored size with auto-size off, so "fits" means
        // readable rather than "TMP shrank it until it went". The box is
        // fixed-size, so it is the same number of canvas units at every
        // audit frame; the reference frame is the smallest of them.
        [UnityTest]
        public IEnumerator ABodyAtTheContentCapFitsTheBodyBox()
        {
            yield return LoadTheMap();

            var body = Text("EventBody");
            var rect = (RectTransform)body.transform;
            Assert.AreEqual(EventScreen.BodyWidth, rect.rect.width, 0.5f);
            Assert.AreEqual(EventScreen.BodyHeight, rect.rect.height, 0.5f);

            const string Prose = "Rain has pooled in the cracked basin, and the reflection in it is not quite " +
                                 "yours. Someone has scratched a tally into the stone rim; ";
            string atCap = string.Concat(Enumerable.Repeat(Prose, 10)).Substring(0, EventEntryResolver.MaxBodyLength);
            Assert.AreEqual(EventEntryResolver.MaxBodyLength, atCap.Length);

            bool autoSize = body.enableAutoSizing;
            float size = body.fontSize;
            try
            {
                body.enableAutoSizing = false;
                body.fontSize = EventScreen.BodyFontSize;
                var preferred = body.GetPreferredValues(atCap, rect.rect.width, 0f);

                Assert.LessOrEqual(preferred.y, rect.rect.height + 1f,
                    $"a {atCap.Length}-character body needs {preferred.y:F0}px at font {EventScreen.BodyFontSize}; " +
                    $"the box is {rect.rect.height:F0}px tall");
            }
            finally
            {
                body.enableAutoSizing = autoSize;
                body.fontSize = size;
            }
        }
    }
}
