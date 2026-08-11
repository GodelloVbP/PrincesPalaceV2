using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Glossary;

namespace PrincesPalace.PlayModeTests
{
    // The glossary, driven through the real hub scene.
    //
    // Paging is covered without a scene by GlossaryScreenTests. What is left
    // for PlayMode is what needs one: that the Relics building reaches it, that
    // switching category actually reloads the list, and that a locked entry
    // withholds its body while still showing its name.
    public class GlossaryTests
    {
        private string _root;
        private HubController _hub;
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        private IEnumerator OpenTheGlossary()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            _glossary = _hub.GetComponentInChildren<GlossaryController>(includeInactive: true);
            Assert.IsNotNull(_glossary, "the glossary was never wired into the hub");

            Click("RelicsBuilding");
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRelicsBuildingOpensIt()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            _glossary = _hub.GetComponentInChildren<GlossaryController>(includeInactive: true);

            Assert.IsFalse(_glossary.gameObject.activeSelf);

            Click("RelicsBuilding");
            yield return null;

            Assert.IsTrue(_glossary.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheRelicsBuildingIsNoLongerDimmedAsUnbuilt()
        {
            // It sat dark and unpressable from the day the hub was built. If it
            // is still in unbuiltButtons the click above cannot fire at all.
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();

            Assert.IsTrue(Named("RelicsBuilding").GetComponent<Button>().interactable);
        }

        [UnityTest]
        public IEnumerator EveryCategoryHasARailButtonThatFillsTheList()
        {
            yield return OpenTheGlossary();

            for (int i = 0; i < GlossaryCatalog.Categories.Count; i++)
            {
                Click($"GlossaryCategory{i}");
                yield return null;

                Assert.IsNotEmpty(TextOf($"GlossaryCategory{i}Caption"),
                    $"category {i} has no name on its button");

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

            Click("GlossaryCategory1");
            yield return null;
            string firstMonster = TextOf("GlossaryRow0Name");

            Assert.AreNotEqual(firstRelic, firstMonster, "the list did not reload");
        }

        [UnityTest]
        public IEnumerator ALockedRelicShowsItsNameButWithholdsItsBody()
        {
            // The whole reason locked entries are listed rather than hidden: a
            // glossary that hides what you have not found cannot tell you what
            // there is to find.
            yield return OpenTheGlossary();

            Click("GlossaryCategory0");
            yield return null;

            int locked = -1;
            for (int i = 0; i < GlossaryCatalog.RowsPerPage; i++)
            {
                var row = Named($"GlossaryRow{i}");
                if (row == null || !row.activeSelf) continue;

                Click($"GlossaryRow{i}");
                yield return null;

                if (Named("GlossaryDetailLockedBy").activeSelf) { locked = i; break; }
            }

            if (locked < 0) Assert.Ignore("no locked relic on the first page");

            Assert.IsNotEmpty(TextOf("GlossaryDetailName"), "a locked entry still has a name");
            StringAssert.Contains("NOT YET FOUND", TextOf("GlossaryDetailBody"));
            StringAssert.Contains("Unlocked by", TextOf("GlossaryDetailLockedBy"));
        }

        [UnityTest]
        public IEnumerator SelectingARowFillsThePlate()
        {
            yield return OpenTheGlossary();

            Assert.IsFalse(Named("GlossaryDetailLockedBy").activeSelf, "nothing is selected yet");

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

        [UnityTest]
        public IEnumerator ClosingLeavesTheHubStanding()
        {
            yield return OpenTheGlossary();

            Click("GlossaryCloseButton");
            yield return null;

            Assert.IsFalse(_glossary.gameObject.activeSelf);
            Assert.IsTrue(_hub.gameObject.activeInHierarchy);
        }
    }
}
