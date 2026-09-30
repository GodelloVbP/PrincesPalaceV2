using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The GLOSSARY tab's focus map (spec section 9), driven through the REAL
    // production dispatcher (scripted BaseInput via inputOverride,
    // `yield return null`, assert resulting state), never a direct call
    // standing in for a press.
    //
    //   entry            the category rail, on the category being read
    //   Right            the list; the detail follows list focus
    //   Left             back to the rail
    //   Up               from either column's first row, the selected tab
    //   Down (last row)  the pager, when the category has more than a page
    //   LB / RB          the previous / next category
    //   Cancel           closes the menu (nothing here is pending)
    public class GlossaryGamepadNavigationTests
    {
        private string _root;
        private SystemMenuController _menu;
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
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            MenuMemory.Clear();
        }

        [TearDown]
        public void Restore()
        {
            if (_menu != null && _menu.IsOpen) _menu.Close();
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            Time.timeScale = 1f;
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private GameObject Selected => EventSystem.current.currentSelectedGameObject;

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The pane keeps its
        // category and page across a close by design (only OnEnable's Refresh
        // runs on reopen, and it clamps rather than resets), so a reused hub
        // would open it wherever the last test left it. Switching to another
        // category and back is the one production path that puts category 0,
        // page 0 and no selection back. NavSceneReuse puts back the rest.
        private IEnumerator OpenTheGlossary()
        {
            yield return SharedScene.Ensure("Hub");

            NavSceneReuse.CloseHubModals();
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");
            _glossary = _menu.GetComponentInChildren<GlossaryController>(includeInactive: true);
            Assert.IsNotNull(_glossary, "the glossary was never wired into the menu");

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;
            yield return null;

            _menu.Open(SystemMenuTab.Glossary);
            yield return null;
            yield return null;

            Named("GlossaryCategory1").GetComponent<Button>().onClick.Invoke();
            Named("GlossaryCategory0").GetComponent<Button>().onClick.Invoke();
            yield return null;
        }

        // Puts focus on a control and lets the module see it.
        private IEnumerator Focus(string name)
        {
            EventSystem.current.SetSelectedGameObject(Named(name));
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;

            // A frame at rest: the module counts a press only once the stick
            // has come back below its threshold (MoveThreshold), so two Moves
            // in a row need one between them.
            yield return null;
        }

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        // The last GlossaryRow that is actually active on the page showing --
        // found rather than hardcoded, since which row is last depends on how
        // many entries the category has, a content fact this file has no
        // business pinning a literal index against.
        private GameObject LastActiveRow()
        {
            GameObject last = null;
            for (int i = 0; i < 8; i++)
            {
                var row = Named($"GlossaryRow{i}");
                if (row != null && row.activeSelf) last = row;
            }

            return last;
        }

        // ---- entering ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DownFromTheTab_LandsOnTheSelectedCategory()
        {
            yield return OpenTheGlossary();
            yield return Focus("SystemTabGlossary");

            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryCategory0", Selected.name,
                "Down from the Glossary tab should land on the category rail, on the category being read");
        }

        // The tab's Down link is the pane's entry, which is the category being
        // read: it follows the category rather than staying where the pane
        // opened.
        [UnityTest]
        public IEnumerator DownFromTheTab_FollowsTheCategoryBeingRead()
        {
            yield return OpenTheGlossary();
            Named("GlossaryCategory3").GetComponent<Button>().onClick.Invoke();
            yield return null;

            yield return Focus("SystemTabGlossary");
            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryCategory3", Selected.name,
                "the tab's Down link is still pointing at the category the pane opened on");
        }

        [UnityTest]
        public IEnumerator SteppingToTheTabFromAnotherFocusesTheCategoryRail()
        {
            yield return OpenTheGlossary();

            _menu.Select(SystemMenuTab.Party);
            yield return null;
            _menu.StepTab(-1);
            yield return null;

            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "fixture: LT from Party is Character");

            _menu.StepTab(1);
            _menu.StepTab(1);
            yield return null;

            Assert.AreEqual(SystemMenuTab.Glossary, _menu.SelectedTab);
            Assert.AreEqual("GlossaryCategory0", Selected.name,
                "stepping into the tab should land on its entry: the category rail");
        }

        // ---- the rail and the list --------------------------------------------------------------

        [UnityTest]
        public IEnumerator Right_FromTheCategory_ReachesTheFirstRow()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryCategory0");

            yield return Move(1f, 0f);

            Assert.AreEqual("GlossaryRow0", Selected.name,
                "Right from the category should reach the list's first row");
        }

        [UnityTest]
        public IEnumerator Left_FromARow_ReturnsToTheSelectedCategory()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryRow3");

            yield return Move(-1f, 0f);

            Assert.AreEqual("GlossaryCategory0", Selected.name,
                "Left from a row should hand back to the category being read");
        }

        [UnityTest]
        public IEnumerator Down_ThroughTheRail_ListsTheNextCategoryAsFocusArrives()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryCategory0");

            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryCategory1", Selected.name);
            Assert.AreEqual(1, _glossary.CategoryIndex, "focus arriving on a category did not list it");
            Assert.AreEqual("Bleed", TextOf("GlossaryRow0Name"), "the list is still the previous category's");
        }

        [UnityTest]
        public IEnumerator Down_ThroughTheList_DescribesEachRowAsFocusArrives()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryRow0");

            Assert.AreEqual(TextOf("GlossaryRow0Name"), TextOf("GlossaryDetailName"));

            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryRow1", Selected.name);
            Assert.AreEqual(TextOf("GlossaryRow1Name"), TextOf("GlossaryDetailName"),
                "the detail did not follow the list's focus");
        }

        [UnityTest]
        public IEnumerator Down_FromTheLastRow_ReachesThePager()
        {
            yield return OpenTheGlossary();

            // Relics: more than eight of them, or there is no pager to reach.
            Assert.IsTrue(Named("GlossaryNextPage").activeSelf,
                "fixture: the first category fits on one page, so the pager is hidden");

            var lastRow = LastActiveRow();
            yield return Focus(lastRow.name);

            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryPrevPage", Selected.name,
                "Down from the last row should reach the pager, the explicit link RefreshNavigation adds " +
                "since a clamped List has nothing below its own last member");
        }

        [UnityTest]
        public IEnumerator Down_FromTheLastRowOfAOnePageCategory_GoesNowhere()
        {
            yield return OpenTheGlossary();
            Named("GlossaryCategory6").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsFalse(Named("GlossaryNextPage").activeSelf, "fixture: Mechanics is one page");

            yield return Focus("GlossaryRow7");
            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryRow7", Selected.name, "the list did not clamp at its last row");
        }

        [UnityTest]
        public IEnumerator Up_FromTheFirstRowAndFromTheFirstCategory_ReachesTheSelectedTab()
        {
            yield return OpenTheGlossary();

            yield return Focus("GlossaryRow0");
            yield return Move(0f, 1f);
            Assert.AreEqual("SystemTabGlossary", Selected.name, "Up from the first row");

            yield return Focus("GlossaryCategory0");
            yield return Move(0f, 1f);
            Assert.AreEqual("SystemTabGlossary", Selected.name, "Up from the first category");
        }

        [UnityTest]
        public IEnumerator Submit_OnARow_ChangesNothing()
        {
            // Nothing on the rail or in the list does anything when pressed; the
            // footer does not offer it either (below).
            yield return OpenTheGlossary();
            yield return Focus("GlossaryRow1");

            string before = TextOf("GlossaryDetailName");
            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(before, TextOf("GlossaryDetailName"));
            Assert.AreEqual("GlossaryRow1", Selected.name);
            Assert.IsTrue(_menu.IsOpen);
        }

        // ---- the shoulders -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator RB_StepsToTheNextCategory_AndFocusStaysOnTheRail()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryCategory0");

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _glossary.CategoryIndex, "RB did not step to Statuses");
            Assert.AreEqual("GlossaryCategory1", Selected.name);
            Assert.AreEqual("Bleed", TextOf("GlossaryRow0Name"));

            _input.TabPrevDown = true;
            yield return DriveFrame();

            Assert.AreEqual(0, _glossary.CategoryIndex, "LB did not step back");
            Assert.AreEqual("GlossaryCategory0", Selected.name);
        }

        [UnityTest]
        public IEnumerator LB_OnTheFirstCategoryStaysPut()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryCategory0");

            _input.TabPrevDown = true;
            yield return DriveFrame();

            Assert.AreEqual(0, _glossary.CategoryIndex);
            Assert.AreEqual("GlossaryCategory0", Selected.name);
        }

        [UnityTest]
        public IEnumerator RB_FromTheList_ListsTheNextCategoryAndFocusesItsFirstRow()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryRow2");

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _glossary.CategoryIndex);
            Assert.AreEqual("GlossaryRow0", Selected.name, "focus was left on a row of the old category");
        }

        // The spec's press count for "what does this enemy status mean?" from
        // the open menu on GLOSSARY: RB (Statuses), Right, then Down to the row.
        [UnityTest]
        public IEnumerator FromTheRail_RBRightDownDown_DescribesTheThirdStatus()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryCategory0");

            _input.TabNextDown = true;
            yield return DriveFrame();
            yield return Move(1f, 0f);
            yield return Move(0f, -1f);
            yield return Move(0f, -1f);

            Assert.AreEqual("GlossaryRow2", Selected.name);
            Assert.AreEqual("Chilled", TextOf("GlossaryDetailName"));
        }

        // ---- cancel ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Cancel_ClosesTheMenu()
        {
            yield return OpenTheGlossary();
            yield return Focus("GlossaryRow0");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen, "Cancel should close the menu: the glossary has nothing pending");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        }

        // ---- the footer -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheFooterOffersNoSelectOnTheRailOrTheListButNamesTheCategoryPager()
        {
            yield return OpenTheGlossary();

            foreach (var name in new[] { "GlossaryCategory0", "GlossaryRow0" })
            {
                yield return Focus(name);

                var prompts = _menu.CurrentPrompts();
                CollectionAssert.AreEqual(
                    new[] { PromptButton.Cancel, PromptButton.Tabs, PromptButton.Pages },
                    prompts.Select(p => p.Button).ToList(), $"on {name}");
                Assert.AreEqual("Category", prompts.Last().Label.Format());
            }
        }

        // A turns a page on the pager and switches tab on another tab, so the
        // frame's row stands there. On the tab already showing it selects
        // nothing, and the row drops it.
        [UnityTest]
        public IEnumerator OnThePagerAndAnotherTabTheFrameOffersSelectButNotOnTheTabShowing()
        {
            yield return OpenTheGlossary();

            foreach (var name in new[] { "GlossaryNextPage", "SystemTabParty" })
            {
                yield return Focus(name);

                CollectionAssert.Contains(_menu.CurrentPrompts().Select(p => p.Button).ToList(),
                    PromptButton.Confirm, $"on {name}");
            }

            yield return Focus("SystemTabGlossary");

            CollectionAssert.DoesNotContain(_menu.CurrentPrompts().Select(p => p.Button).ToList(),
                PromptButton.Confirm, "on the Glossary tab while it is showing");
        }
    }
}
