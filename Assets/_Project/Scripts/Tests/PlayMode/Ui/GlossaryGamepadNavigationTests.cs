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
    // glossary, one of the three Hub-covering modals that did not push their
    // own NavContext. GlossaryController.RefreshNavigation (called at the
    // end of every Paint()) is the mechanism -- two vertical Lists side by
    // side (the category rail, the row list, both clamped) rather than one
    // of them mis-declared a Rail because AUDIT.md's own prose calls it
    // "the rail". Driven through the REAL production dispatcher (scripted
    // BaseInput via inputOverride, `yield return null`, assert resulting
    // state), never a direct SelectCategory/SelectRow call standing in for
    // a press.
    public class GlossaryGamepadNavigationTests
    {
        private string _root;
        private HubController _hub;
        private GlossaryController _glossary;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-glossary-nav-" + System.Guid.NewGuid().ToString("N"));
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

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The glossary
        // keeps its category, page and selected row across a close by design
        // (only OnEnable's Refresh runs on reopen, and it clamps rather than
        // resets), so a reused hub would open it wherever the last test left
        // it -- a row already picked, the detail plate already filled. The
        // category-0 button's own click is the one production path that puts
        // all three back (SelectCategory: category 0, page 0, no row), and it
        // is pressed here before the glossary opens. NavSceneReuse puts back
        // the rest.
        private IEnumerator OpenTheGlossary()
        {
            yield return SharedScene.Ensure("Hub");

            _hub = NavSceneReuse.CloseHubModals();

            _glossary = _hub.GetComponentInChildren<GlossaryController>(includeInactive: true);
            Assert.IsNotNull(_glossary, "the glossary was never wired into the hub");

            if (NavSceneReuse.Reused) Named("GlossaryCategory0").GetComponent<Button>().onClick.Invoke();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;
            yield return null;

            Named("RelicsBuilding").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // The last GlossaryRow that is actually active on the page showing --
        // found rather than hardcoded, since which row is last depends on
        // how many entries category 0 has on its first page, a content fact
        // this file has no business pinning a literal index against.
        private GameObject LastActiveRow()
        {
            GameObject last = null;
            for (int i = 0; i < 10; i++)
            {
                var row = Named($"GlossaryRow{i}");
                if (row != null && row.activeSelf) last = row;
            }
            return last;
        }

        [UnityTest]
        public IEnumerator EntryIsTheFirstCategory_AsSoonAsTheGlossaryOpens()
        {
            SharedScene.MarkDirty("asserts where a never-opened glossary starts, which the category-0 reset would otherwise supply");
            yield return OpenTheGlossary();

            Assert.AreEqual(Named("GlossaryCategory0"), EventSystem.current.currentSelectedGameObject,
                "a freshly-opened GlossaryController starts on category 0, RefreshNavigation's own entry");
        }

        [UnityTest]
        public IEnumerator Right_FromTheCategory_ReachesTheFirstRow()
        {
            yield return OpenTheGlossary();

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named("GlossaryRow0"), EventSystem.current.currentSelectedGameObject,
                "Right from the selected category should reach the row list's own first member");
        }

        [UnityTest]
        public IEnumerator Left_FromTheFirstRow_ReturnsToTheCategory()
        {
            yield return OpenTheGlossary();

            EventSystem.current.SetSelectedGameObject(Named("GlossaryRow0"));
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named("GlossaryCategory0"), EventSystem.current.currentSelectedGameObject,
                "Left from the first row should hand back to the selected category (the explicit return link)");
        }

        [UnityTest]
        public IEnumerator Down_ThroughCategories_ReachesTheSecondCategory()
        {
            yield return OpenTheGlossary();

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("GlossaryCategory1"), EventSystem.current.currentSelectedGameObject,
                "the category rail is a vertical List (RailX/RailTop/RailPitch are a column, not a row) -- " +
                "Down from the first should step to the second");
        }

        [UnityTest]
        public IEnumerator Down_FromTheLastRow_ReachesThePager()
        {
            yield return OpenTheGlossary();

            var lastRow = LastActiveRow();
            Assert.IsNotNull(lastRow, "fixture: category 0 should list at least one entry");

            EventSystem.current.SetSelectedGameObject(lastRow);
            yield return null;

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("GlossaryPrevPage"), EventSystem.current.currentSelectedGameObject,
                "Down from the last row should reach the pager, the explicit link RefreshNavigation adds " +
                "since a clamped List has nothing below its own last member");
        }

        [UnityTest]
        public IEnumerator Submit_OnARow_SelectsItAndFillsThePlate()
        {
            yield return OpenTheGlossary();

            EventSystem.current.SetSelectedGameObject(Named("GlossaryRow0"));
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsNotEmpty(Named("GlossaryDetailName").GetComponent<TMPro.TMP_Text>().text,
                "Submit on a row should select it exactly once, same as a click -- SelectRow's own plate " +
                "paint should have run");
        }

        [UnityTest]
        public IEnumerator Cancel_ClosesTheGlossary_AndTheGateIsReselected()
        {
            yield return OpenTheGlossary();

            var gate = Named("StartRunGate");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_glossary.gameObject.activeSelf,
                "Cancel should close the glossary, the same action Close() (the mouse path) takes");
            Assert.IsTrue(_hub.gameObject.activeInHierarchy, "the hub should still be standing underneath");

            // One more frame: NavigationInputModule's post-dispatch
            // reselection rule re-resolves the CURRENT top (now the hub
            // again) rather than the just-removed glossary context.
            yield return null;

            Assert.AreEqual(gate, EventSystem.current.currentSelectedGameObject,
                "closing the glossary should fall back to the hub's own entry (the gate) -- HubController " +
                "never calls NavContext.Remember, so there is no per-node memory to restore, only Entry");
        }
    }
}
