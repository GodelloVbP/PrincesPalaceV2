using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3, item 1 (AUDIT.md #158): the
    // debug menu, the last of the three Hub-covering modals that did not
    // push their own NavContext. DebugMenuController.RefreshNavigation
    // (called at the end of every Refresh()) is the mechanism -- currency
    // and filter Rails, the item List, the pager Rail, chained top to bottom
    // by explicit links since a debug tool with four differently-shaped rows
    // is not one group. Driven through the REAL production dispatcher
    // (scripted BaseInput via inputOverride, `yield return null`, assert
    // resulting state), never a direct GrantRow/SetFilter call standing in
    // for a press.
    public class DebugMenuGamepadNavigationTests
    {
        private string _root;
        private HubController _hub;
        private DebugMenuController _debug;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-debug-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator OpenTheMenu()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

            _debug = _hub.GetComponentInChildren<DebugMenuController>(includeInactive: true);
            Assert.IsNotNull(_debug, "the debug menu was never wired into the hub");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _hub.SetDebugMenu(true);

            // Start() runs one frame after SetActive, so the row buttons'
            // own onClick listeners do not exist yet (CODE_STANDARDS
            // section 5, same wait DebugMenuTests.OpenTheMenu takes).
            yield return null;
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private GameObject LastActiveRow()
        {
            GameObject last = null;
            for (int i = 0; i < 12; i++)
            {
                var row = Named($"DebugRow{i}");
                if (row != null && row.activeSelf) last = row;
            }
            return last;
        }

        [UnityTest]
        public IEnumerator EntryIsTheGoldButton_AsSoonAsTheMenuOpens()
        {
            yield return OpenTheMenu();

            Assert.AreEqual(Named("DebugGiveGoldButton"), EventSystem.current.currentSelectedGameObject,
                "the first currency grant is RefreshNavigation's own entry");
        }

        [UnityTest]
        public IEnumerator Down_FromGold_ReachesTheFirstFilter()
        {
            yield return OpenTheMenu();

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DebugFilter0"), EventSystem.current.currentSelectedGameObject,
                "Down from Gold should reach the first filter, the explicit link between the two Rails");
        }

        [UnityTest]
        public IEnumerator Down_FromTheFirstFilter_ReachesTheFirstRow()
        {
            yield return OpenTheMenu();

            EventSystem.current.SetSelectedGameObject(Named("DebugFilter0"));
            yield return null;

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DebugRow0"), EventSystem.current.currentSelectedGameObject,
                "Down from the first filter should reach the item list's own first row");
        }

        [UnityTest]
        public IEnumerator Down_FromTheLastRow_ReachesThePager()
        {
            yield return OpenTheMenu();

            var lastRow = LastActiveRow();
            Assert.IsNotNull(lastRow, "fixture: content has items");

            EventSystem.current.SetSelectedGameObject(lastRow);
            yield return null;

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DebugPrevPage"), EventSystem.current.currentSelectedGameObject,
                "Down from the last row should reach the pager, the explicit link RefreshNavigation adds " +
                "since a clamped List has nothing below its own last member");
        }

        [UnityTest]
        public IEnumerator Submit_OnARow_GrantsExactlyOneItem()
        {
            yield return OpenTheMenu();

            int before = Save.stockpiledItems.Sum(e => e.count);

            EventSystem.current.SetSelectedGameObject(Named("DebugRow0"));
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count),
                "Submit on a row should grant exactly one item, same as one click -- a double dispatch " +
                "would have granted two");
        }

        [UnityTest]
        public IEnumerator Cancel_ClosesTheMenu_AndTheGateIsReselected()
        {
            yield return OpenTheMenu();

            var gate = Named("StartRunGate");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_hub.DebugMenuIsOpen,
                "Cancel should close the debug menu, the same action Close() (the mouse path) takes");
            Assert.IsTrue(_hub.gameObject.activeInHierarchy, "the hub should still be standing underneath");

            yield return null;

            Assert.AreEqual(gate, EventSystem.current.currentSelectedGameObject,
                "closing the menu should fall back to the hub's own entry (the gate) -- HubController never " +
                "calls NavContext.Remember, so there is no per-node memory to restore, only Entry");
        }
    }
}
