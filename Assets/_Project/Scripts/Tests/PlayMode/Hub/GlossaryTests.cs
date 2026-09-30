using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The GLOSSARY tab of the system menu, driven through the real hub scene.
    //
    // Paging arithmetic and the pane's geometry are covered without a scene by
    // GlossaryScreenTests. What is left for PlayMode is what needs one: that
    // the hub building reaches the tab, that switching category actually
    // reloads the list, that a locked entry withholds its body while still
    // showing its name, and that the Statuses category and the deep link read
    // what they should.
    public class GlossaryTests
    {
        private string _root;
        private SystemMenuController _menu;
        private GlossaryController _glossary;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-glossary-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            if (_menu != null && _menu.IsOpen) _menu.Close();
            SharedScene.AfterTest();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private GameObject InHub(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). A menu the last
        // test left open is closed first, so the next open runs OnEnable's
        // Refresh against this test's save the way a fresh hub's first open does.
        private IEnumerator TheHub()
        {
            yield return SharedScene.Ensure("Hub");

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");
            _glossary = _menu.GetComponentInChildren<GlossaryController>(includeInactive: true);
            Assert.IsNotNull(_glossary, "the glossary was never wired into the system menu");

            var events = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            Assert.IsNotNull(events, "the hub has no EventSystem");
            EventSystem.current = events;

            if (_menu.IsOpen) _menu.Close();
            Time.timeScale = 1f;
        }

        // The pane keeps its category and page across a close and reopen (only a
        // reload resets them), so after opening it this switches away from the
        // first category and back, which is what a fresh open lands on: category
        // 0, page 0, nothing selected. A test that asserts that landing itself
        // marks the scene dirty and opts out of the reset.
        private IEnumerator OpenTheGlossary(bool resetToTheFirstCategory = true)
        {
            yield return TheHub();

            _menu.Open(SystemMenuTab.Glossary);
            yield return null;
            yield return null;

            if (!resetToTheFirstCategory) yield break;
            Click("GlossaryCategory1");
            Click("GlossaryCategory0");
            yield return null;
        }

        // ---- the way in ------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheHubBuildingOpensTheMenuOnTheGlossaryTab()
        {
            SharedScene.MarkDirty("asserts the menu is closed on a freshly loaded hub, which TheHub's reset would force");
            yield return TheHub();

            Assert.IsFalse(_menu.IsOpen);

            InHub("RelicsBuilding").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(_menu.IsOpen, "the building did not open the menu");
            Assert.AreEqual(SystemMenuTab.Glossary, _menu.SelectedTab);
            Assert.IsTrue(_glossary.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator TheBuildingIsLabelledGlossaryAndStillPressable()
        {
            yield return TheHub();

            var building = InHub("RelicsBuilding");

            Assert.AreEqual("GLOSSARY", InHub("RelicsBuildingCaption").GetComponent<TMP_Text>().text);

            // It sat dark and unpressable from the day the hub was built. If it
            // is still in unbuiltButtons the click above cannot fire at all.
            Assert.IsTrue(building.GetComponent<Button>().interactable);
        }

        [UnityTest]
        public IEnumerator TheGlossaryTabIsNotRunOnlySoADescentAndAFightHaveIt()
        {
            // The tab is not run-only: a player looks things up mid-descent and
            // mid-fight (plan C25). Visibility is a property of the context.
            yield return TheHub();

            Assert.IsTrue(_menu.HasTab(SystemMenuTab.Glossary), "camp");

            var glossary = SystemMenuTabs.Def(SystemMenuTab.Glossary);
            Assert.IsFalse(glossary.RunOnly, "the glossary is hidden outside a run");
            Assert.AreEqual('G', glossary.Hotkey);
            Assert.AreEqual("LORE", glossary.ShortLabel.Format());
        }

        // ---- the rail --------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRailNamesTheCategoriesInTheSpecsOrder()
        {
            yield return OpenTheGlossary();

            var names = Enumerable.Range(0, 8).Select(i => TextOf($"GlossaryCategory{i}Caption")).ToList();

            CollectionAssert.AreEqual(
                new[] { "RELICS", "STATUSES", "MONSTERS", "SKILLS", "ITEMS", "TALENTS", "MECHANICS", "DEEDS" },
                names);
        }

        [UnityTest]
        public IEnumerator EveryCategoryHasARailButtonThatFillsTheList()
        {
            yield return OpenTheGlossary();

            for (int i = 0; i < GlossaryCatalog.Categories.Count; i++)
            {
                Click($"GlossaryCategory{i}");
                yield return null;

                Assert.IsNotEmpty(TextOf($"GlossaryCategory{i}Caption"), $"category {i} has no name on its button");

                // Every category in the game currently has content. An empty
                // one would be a content gap, not a UI bug -- but silence here
                // would hide either.
                Assert.IsTrue(Named("GlossaryRow0").activeSelf,
                    $"{GlossaryCatalog.Categories[i]} listed nothing at all");
            }
        }

        [UnityTest]
        public IEnumerator SwitchingCategoryReplacesTheList()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCategory0");
            yield return null;
            string firstRelic = TextOf("GlossaryRow0Name");

            Click("GlossaryCategory2");
            yield return null;
            string firstMonster = TextOf("GlossaryRow0Name");

            Assert.AreNotEqual(firstRelic, firstMonster, "the list did not reload");
        }

        [UnityTest]
        public IEnumerator TheRailCountsWhatIsUnlockedOfEachCategory()
        {
            yield return OpenTheGlossary();

            // Statuses: sixteen engine statuses plus speed up, speed down, the
            // transformation, rally, silence and disarm; none of them locked.
            Assert.AreEqual("22 / 22", TextOf("GlossaryCategory1Count"));

            // Mechanics: the six ability scores, the Defense curve and Honing.
            Assert.AreEqual("8 / 8", TextOf("GlossaryCategory6Count"));
        }

        // ---- the list and the detail --------------------------------------------------

        [UnityTest]
        public IEnumerator ALockedRelicShowsItsNameButWithholdsItsBody()
        {
            // The whole reason locked entries are listed rather than hidden: a
            // glossary that hides what you have not found cannot tell you what
            // there is to find.
            yield return OpenTheGlossary();

            Click("GlossaryCategory0");
            yield return null;

            // EVERY PAGE, and asserted rather than skipped: a skip that finds
            // nothing reports green while the locked plate -- the whole subject
            // here -- goes unchecked. The page count comes from the relic count
            // because one relic past a page boundary is exactly how this kind of
            // test goes blind.
            int pages = GlossaryCatalog.PageCount(ContentDatabase.Relics.Count);
            bool found = false;

            for (int page = 0; page < pages && !found; page++)
            {
                for (int i = 0; i < GlossaryCatalog.RowsPerPage; i++)
                {
                    var row = Named($"GlossaryRow{i}");
                    if (row == null || !row.activeSelf) continue;

                    Click($"GlossaryRow{i}");
                    yield return null;

                    if (Named("GlossaryDetailLockedBy").activeSelf) { found = true; break; }
                }

                if (!found)
                {
                    Click("GlossaryNextPage");
                    yield return null;
                }
            }

            Assert.IsTrue(found,
                "no relic anywhere in the glossary shows the locked plate - either every relic is " +
                "unlocked from the start, or a locked one lost the achievement name that explains it");

            Assert.IsNotEmpty(TextOf("GlossaryDetailName"), "a locked entry still has a name");
            Assert.AreEqual("Not yet found.", TextOf("GlossaryDetailBody"));
            StringAssert.Contains("Unlocked by", TextOf("GlossaryDetailLockedBy"));
        }

        // The Mechanics category is the one where every row is hand-authored
        // prose rather than swept off a ScriptableObject
        // (GlossaryEntries.Mechanics()), which is exactly the kind of category a
        // purely structural test would not catch going stale or empty.
        [UnityTest]
        public IEnumerator TheMechanicsCategoryExplainsTheAbilityScoresAndTheDefenseCurve()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCategory6");
            yield return null;

            // Eight rows: STR, DEX, CON, INT, WIS, CHA, the Defense curve, and
            // Honing -- exactly one page, so the pager stays away.
            for (int row = 0; row < 8; row++)
            {
                Assert.IsTrue(Named($"GlossaryRow{row}").activeSelf, $"Mechanics row {row} is missing");
            }

            Assert.IsFalse(Named("GlossaryNextPage").activeSelf, "a one-page category shows a pager");
            Assert.IsFalse(Named("GlossaryPageLabel").activeSelf);

            Click("GlossaryRow0");
            yield return null;

            Assert.AreEqual("Strength", TextOf("GlossaryDetailName"));
            Assert.AreEqual("Ability score", TextOf("GlossaryDetailMeta"));
            StringAssert.Contains("weapon", TextOf("GlossaryDetailBody"));

            Click("GlossaryRow2");
            yield return null;

            // Constitution: the one entry whose numbers are read off
            // AbilityDerivation's own constants (20 Health a point today).
            Assert.AreEqual("Constitution", TextOf("GlossaryDetailName"));
            StringAssert.Contains("+20 Max Health", TextOf("GlossaryDetailBody"));
        }

        [UnityTest]
        public IEnumerator SelectingARowFillsThePlate()
        {
            // "Nothing is selected yet" is about the first open of a fresh
            // hub, not about what OpenTheGlossary's category reset clears.
            SharedScene.MarkDirty("asserts nothing is selected on a fresh hub's first open, which the category reset would force");
            yield return OpenTheGlossary(resetToTheFirstCategory: false);

            Assert.IsFalse(Named("GlossaryDetailLockedBy").activeSelf, "nothing is selected yet");
            Assert.AreEqual("Choose an entry to read it.", TextOf("GlossaryDetailBody"));

            Click("GlossaryRow0");
            yield return null;

            Assert.IsNotEmpty(TextOf("GlossaryDetailName"));
        }

        [UnityTest]
        public IEnumerator SwitchingCategoryClearsTheSelection()
        {
            // A selection describing an entry from the previous category would
            // be wrong rather than merely stale.
            yield return OpenTheGlossary();

            Click("GlossaryRow0");
            yield return null;
            Assert.IsNotEmpty(TextOf("GlossaryDetailName"));

            Click("GlossaryCategory2");
            yield return null;

            Assert.IsEmpty(TextOf("GlossaryDetailName"), "the plate still describes the old category");
        }

        // FOCUS IS SELECTION: moving the pad or keyboard onto a row describes it,
        // and onto a category lists it, with no press.
        [UnityTest]
        public IEnumerator FocusingARowDescribesItAndFocusingACategoryListsIt()
        {
            yield return OpenTheGlossary();
            var system = EventSystem.current;
            Assert.IsNotNull(system, "fixture: no EventSystem to move focus with");

            system.SetSelectedGameObject(Named("GlossaryRow2"));
            yield return null;

            Assert.AreEqual(TextOf("GlossaryRow2Name"), TextOf("GlossaryDetailName"),
                "focusing a row did not describe it");

            system.SetSelectedGameObject(Named("GlossaryCategory3"));
            yield return null;

            Assert.AreEqual(3, _glossary.CategoryIndex, "focusing a category did not list it");
            Assert.IsEmpty(TextOf("GlossaryDetailName"), "the plate still describes the old category");
        }

        // ---- statuses ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheStatusesCategoryListsTheFightsBadgesAlphabetically()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCategory1");
            yield return null;

            var names = Enumerable.Range(0, 8).Select(i => TextOf($"GlossaryRow{i}Name")).ToList();
            CollectionAssert.AreEqual(
                new[] { "Bleed", "Burn", "Chilled", "Disarmed", "Empowered", "Feared", "Fortified", "Marked" }, names);
            Assert.AreEqual("1 / 3", TextOf("GlossaryPageLabel"), "22 statuses are three pages of eight");
        }

        [UnityTest]
        public IEnumerator AStatusReadsAsTheSentenceTheFightShowsWithTheNumberLeftOpen()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCategory1");
            Click("GlossaryRow0");
            yield return null;

            Assert.AreEqual("Bleed", TextOf("GlossaryDetailName"));
            Assert.AreEqual("BLD  ·  Detriment", TextOf("GlossaryDetailMeta"));
            Assert.AreEqual(
                "Bleed -- X physical damage each turn start, before armour. Separate applications add together.",
                TextOf("GlossaryDetailBody"));
        }

        [UnityTest]
        public IEnumerator AStatusShowsItsBadgeArtWhenThereIsSomeAndNothingWhenThereIsNot()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCategory1");
            Click("GlossaryRow0");
            yield return null;

            var icon = Named("GlossaryDetailIcon").GetComponent<Image>();
            Assert.IsTrue(icon.enabled, "Bleed has a badge in Resources/Status and the detail did not show it");
            Assert.AreEqual("bleed", icon.sprite.name);

            // The transformation has no badge art yet (StatusHud says so): the
            // icon goes away rather than showing a white quad.
            Click("GlossaryNextPage");
            Click("GlossaryNextPage");
            yield return null;
            Click("GlossaryRow4");
            yield return null;

            Assert.AreEqual("Transformed", TextOf("GlossaryDetailName"));
            Assert.IsFalse(icon.enabled, "a status with no art still shows an icon");
        }

        // ---- deep link ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpenWithAnEntryTargetSelectsItsCategoryPagesToItAndFocusesItsRow()
        {
            yield return TheHub();

            _menu.Open(SystemMenuTab.Glossary, MenuTarget.Glossary("poison"));
            yield return null;
            yield return null;

            // Poison is the ninth status: page two, first row.
            Assert.AreEqual(1, _glossary.CategoryIndex, "the Statuses category was not selected");
            Assert.AreEqual(1, _glossary.PageIndex, "the pane did not turn to the entry's page");
            Assert.AreEqual("Poison", TextOf("GlossaryDetailName"));
            Assert.AreEqual("2 / 3", TextOf("GlossaryPageLabel"));
            Assert.AreEqual("GlossaryRow0", EventSystem.current.currentSelectedGameObject.name,
                "focus did not land on the entry's row");
        }

        [UnityTest]
        public IEnumerator OpenWithAnEntryThatIsNowhereLeavesThePaneAlone()
        {
            yield return TheHub();

            Assert.DoesNotThrow(() => _menu.Open(SystemMenuTab.Glossary, MenuTarget.Glossary("nothing_here")));
            yield return null;

            Assert.AreEqual(SystemMenuTab.Glossary, _menu.SelectedTab);
            Assert.IsFalse(_glossary.ShowEntry("nothing_here"));
        }

        [UnityTest]
        public IEnumerator ClosingTheMenuClosesTheGlossaryAndLeavesTheHubStanding()
        {
            yield return OpenTheGlossary();

            _menu.Close();
            yield return null;

            Assert.IsFalse(_menu.IsOpen);
            Assert.IsFalse(_glossary.gameObject.activeInHierarchy);
            Assert.AreEqual(1f, Time.timeScale, 0.0001f, "the glossary left the game paused");
            Assert.IsNotNull(InHub("HubPanel"));
        }
    }
}
