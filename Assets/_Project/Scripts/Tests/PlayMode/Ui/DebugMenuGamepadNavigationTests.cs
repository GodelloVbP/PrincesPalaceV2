using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.DebugMenu;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3, item 1 (AUDIT.md #158): the
    // debug menu, the last of the three Hub-covering modals that did not
    // push their own NavContext. DebugMenuController.RefreshNavigation
    // (called at the end of every Refresh()) is the mechanism -- currency,
    // category and sub-filter Rails, the item Grid, the pager Rail, chained
    // top to bottom by explicit links since a debug tool with several
    // differently-shaped rows is not one group. Driven through the REAL
    // production dispatcher (scripted BaseInput via inputOverride,
    // `yield return null`, assert resulting state), never a direct
    // GrantRow/SetCategory call standing in for a press.
    //
    // REBUILT 2026-09-23 (debug menu overhaul, phase 1): the old single
    // filter Rail is a category Rail (vertical, reached by Right/Left from
    // the currency row) plus a sub-filter Rail (reached by Down from a
    // category, same as the old filter-to-row Down step). The item list is
    // a Grid now, not a List -- see RefreshNavigation's own comment for why.
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
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The debug
        // menu's category and page persist across a close by design
        // (DebugMenuTests.TheHub says so and resets them the same way), and
        // two tests here switch to RESOURCES, so the category-0 button's own
        // click puts the menu back on WEAPONS, page one, before it reopens.
        // Nothing here touches the sticky quantity or plus. NavSceneReuse
        // puts back the rest.
        private IEnumerator OpenTheMenu()
        {
            yield return SharedScene.Ensure("Hub");

            _hub = NavSceneReuse.CloseHubModals();

            _debug = _hub.GetComponentInChildren<DebugMenuController>(includeInactive: true);
            Assert.IsNotNull(_debug, "the debug menu was never wired into the hub");

            if (NavSceneReuse.Reused) Named("DebugCategory0").GetComponent<Button>().onClick.Invoke();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;

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
            for (int i = 0; i < DebugMenuCatalog.RowsPerPage; i++)
            {
                var row = Named($"DebugRow{i}");
                if (row != null && row.activeSelf) last = row;
            }
            return last;
        }

        [UnityTest]
        public IEnumerator EntryIsTheFirstCategory_AsSoonAsTheMenuOpens()
        {
            SharedScene.MarkDirty("asserts where a never-opened debug menu starts, which the category-0 reset would otherwise supply");
            yield return OpenTheMenu();

            Assert.AreEqual(Named("DebugCategory0"), EventSystem.current.currentSelectedGameObject,
                "the top of the category rail is RefreshNavigation's own entry");
        }

        [UnityTest]
        public IEnumerator Down_FromTheFirstCategory_WalksTheRail()
        {
            yield return OpenTheMenu();

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DebugCategory1"), EventSystem.current.currentSelectedGameObject,
                "the rail is a vertical List, so Down steps to the next category");
        }

        [UnityTest]
        public IEnumerator Right_FromTheFirstCategory_ReachesTheFirstSubFilter()
        {
            yield return OpenTheMenu();

            EventSystem.current.SetSelectedGameObject(Named("DebugCategory0"));
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            // Weapons (category 0) has a sub-filter row (the tier chips), so
            // Right from the rail should reach it rather than jumping
            // straight into the list.
            Assert.AreEqual(Named("DebugSubFilter0"), EventSystem.current.currentSelectedGameObject,
                "Right from the first category should reach the sub-filter row, the explicit link between " +
                "the rail and the content column");
        }

        [UnityTest]
        public IEnumerator Right_FromAnyCategory_ReachesThatTabsContent()
        {
            // Only the first tab used to link Right, so from RESOURCES (which
            // has no chip row) a pad had to climb back to WEAPONS to reach a
            // single row.
            yield return OpenTheMenu();

            int resources = (int)PrincesPalace.Domain.DebugMenu.DebugCategory.Resources;
            Named($"DebugCategory{resources}").GetComponent<Button>().onClick.Invoke();
            yield return null;

            EventSystem.current.SetSelectedGameObject(Named($"DebugCategory{resources}"));
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named("DebugRow0"), EventSystem.current.currentSelectedGameObject,
                "Right from the RESOURCES tab should land on its first row");
        }

        [UnityTest]
        public IEnumerator Left_FromTheFirstRow_ReturnsToTheTabBeingShown()
        {
            // Not to WEAPONS: the row belongs to whichever tab is open.
            yield return OpenTheMenu();

            int resources = (int)PrincesPalace.Domain.DebugMenu.DebugCategory.Resources;
            Named($"DebugCategory{resources}").GetComponent<Button>().onClick.Invoke();
            yield return null;

            EventSystem.current.SetSelectedGameObject(Named("DebugRow0"));
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named($"DebugCategory{resources}"), EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator Down_FromTheFirstSubFilter_ReachesTheFirstRow()
        {
            yield return OpenTheMenu();

            EventSystem.current.SetSelectedGameObject(Named("DebugSubFilter0"));
            yield return null;

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DebugRow0"), EventSystem.current.currentSelectedGameObject,
                "Down from the first sub-filter chip should reach the item grid's own first row");
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
                "since a clamped Grid has nothing below its own last row");
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
                "Submit on a row should grant exactly one item at the default x1, same as one click -- a " +
                "double dispatch would have granted two");
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

            // THE CLAIM SURVIVES FOCUS MEMORY LANDING (AUDIT.md #163), and
            // the reason is worth stating rather than leaving the old
            // parenthetical to rot: this used to read "HubController never
            // calls NavContext.Remember, so there is no per-node memory to
            // restore, only Entry", which was true of the mechanism and is
            // no longer -- NavigationInputModule now records the hub's own
            // focus every frame. The gate is still the right answer here, and
            // now for a stronger reason than an absent feature: nothing in
            // this test ever moves off the gate, so the gate IS what the hub
            // remembers, and memory and entry agree. A test that wanted to
            // tell the two apart would have to Move first, which is
            // FocusMemoryGamepadNavigationTests' own job.
            Assert.AreEqual(gate, EventSystem.current.currentSelectedGameObject,
                "closing the menu should land back on the gate -- the hub's remembered node, which is " +
                "also its entry, since nothing in this test moved the selection off it");
        }
    }
}
