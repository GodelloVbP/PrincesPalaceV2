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
using PrincesPalace.Content;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.Stats;

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

            // EVERY PAGE, not just the first, and asserted rather than skipped.
            //
            // This used to scan page one and Assert.Ignore if it found nothing,
            // which meant it had never run: exactly ONE relic is gated today
            // (forest_wardens_tooth, behind first_forest_boss), it sits at
            // index 10 of thirteen, and the page holds ten. It was the first
            // row of page TWO, one turn past everything this test looked at.
            //
            // So the skip was not protecting against a thin fixture. It was
            // reporting green while the locked plate -- the whole subject here
            // -- went unchecked, and it would have gone on doing that however
            // many locked relics were added, as long as none landed in the
            // first ten.
            //
            // Derived from the relic count rather than a literal page bound:
            // one relic past a page boundary is exactly how this test went
            // blind in the first place.
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
            StringAssert.Contains("NOT YET FOUND", TextOf("GlossaryDetailBody"));
            StringAssert.Contains("Unlocked by", TextOf("GlossaryDetailLockedBy"));
        }

        // BALANCE REDESIGN PHASE 6 (D7.4): the Mechanics category is the one
        // where every row is hand-authored prose rather than swept off a
        // ScriptableObject (GlossaryEntries.Mechanics()'s own header), which
        // is exactly the kind of category a purely structural test (every
        // other GlossaryScreenTests/GlossaryTests check here) would not
        // catch going stale or empty -- this checks the actual content
        // reaches the plate.
        [UnityTest]
        public IEnumerator TheMechanicsCategoryExplainsTheAbilityScoresAndTheDefenseCurve()
        {
            yield return OpenTheGlossary();

            int index = GlossaryCatalog.Categories.ToList().IndexOf(GlossaryCategory.Mechanics);
            Assert.GreaterOrEqual(index, 0, "GlossaryCategory.Mechanics is missing from the rail entirely");

            Click($"GlossaryCategory{index}");
            yield return null;

            // Eight rows: STR, DEX, CON, INT, WIS, CHA, the Defense curve,
            // and Honing -- the exact count GlossaryEntries.Mechanics()
            // authors. Pinned so a row silently dropped from that list (or
            // silently duplicated) shows up here rather than only in a diff
            // of hand-typed prose nobody re-reads.
            for (int row = 0; row < 8; row++)
            {
                Assert.IsTrue(Named($"GlossaryRow{row}").activeSelf, $"Mechanics row {row} is missing");
            }

            Assert.IsFalse(Named("GlossaryRow8").activeSelf, "Mechanics listed a ninth row nobody authored");

            Click("GlossaryRow0");
            yield return null;

            Assert.AreEqual("Strength", TextOf("GlossaryDetailName"));
            StringAssert.Contains("weapon", TextOf("GlossaryDetailBody"));

            Click("GlossaryRow2");
            yield return null;

            // Constitution: the one entry whose numbers are read straight off
            // AbilityDerivation's own constants rather than typed by hand --
            // if the rate is ever retuned, this line is the canary.
            Assert.AreEqual("Constitution", TextOf("GlossaryDetailName"));
            StringAssert.Contains($"+{AbilityDerivation.HealthPerPoint} Max Health", TextOf("GlossaryDetailBody"));
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
