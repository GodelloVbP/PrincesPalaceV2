using System.Collections;
using System.Linq;
using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The hub's two overlay panels, opened and closed the way a session opens
    // and closes them.
    //
    // The glossary and the debug menu both wire in Start and only REFRESH in
    // OnEnable, which is the safe shape and is also the shape that is easy to
    // move. Most of what these panels do is idempotent -- selecting a category,
    // picking a filter -- so a listener added three times would be invisible in
    // almost every assertion anyone would naturally write.
    //
    // The debug menu has the exception, and it is the whole reason B9 is worth
    // a test: GIVING GOLD ADDS. A grant wired once per opening pays three times
    // and reaches disk three times, and the only sign is a number that is
    // wrong in a direction nobody complains about.
    //
    // scenarios B8, B9 (docs/hunt/SCENARIOS.md).
    public class HubPanelLifecycleTests
    {
        private const int Cycles = 3;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hubpanel-life-" + System.Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- B9: the debug menu ------------------------------------------------------

        // DebugMenuTests.GoldLandsOnTheBankedWalletAndReachesDisk presses once
        // on a freshly opened panel. This opens it three times first, which is
        // an ordinary evening with a debug menu, and then presses once.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfTheDebugMenuStillGrantOneGoldGrantPerPress()
        {
            yield return OpenTheHub();

            var debug = Object.FindAnyObjectByType<DebugMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(debug, "the hub has no DebugMenuController");

            debug.gameObject.SetActive(true);
            yield return null;
            yield return null;

            yield return Cycle(debug.gameObject);

            var save = SaveSlotManager.CurrentSave;
            int before = save.wallet.gold;

            Click("DebugGiveGoldButton");
            yield return null;

            Assert.AreEqual(before + DebugMenuController.GoldGrant, save.wallet.gold,
                "one press of Give Gold granted more than one grant, so the debug menu wired its " +
                "buttons again on every opening");

            // AND ON DISK ONCE, for the same reason the original test checks
            // disk at all: the grant persists, so a tripled grant is not a
            // display error that goes away on the next load.
            SaveSlotManager.Forget();
            Assert.AreEqual(before + DebugMenuController.GoldGrant, SaveSlotManager.CurrentSave.wallet.gold,
                "the wallet that reached disk is not the one the single press should have written");
        }

        // ---- B8: the glossary --------------------------------------------------------

        // CATEGORY SELECTION IS IDEMPOTENT, so it cannot witness a doubled
        // listener on its own -- clicking one category three times leaves the
        // same category selected. The PAGER can: StepPage moves one page per
        // press, so forward-then-back has to land on the page it started on,
        // and a tripled listener would overshoot in both directions.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfTheGlossaryStillTurnOnePagePerPress()
        {
            yield return OpenTheHub();

            var glossary = Object.FindAnyObjectByType<GlossaryController>(FindObjectsInactive.Include);
            Assert.IsNotNull(glossary, "the glossary was never wired into the hub");

            Click("RelicsBuilding");
            yield return null;
            yield return null;
            Assert.IsTrue(glossary.gameObject.activeSelf, "fixture: the glossary did not open");

            yield return Cycle(glossary.gameObject);

            // A CATEGORY DEEP ENOUGH TO TURN, found rather than named: how many
            // entries each category has is content, and a test that named one
            // would fail as a content edit rather than as a defect.
            string firstPage = null;
            for (int category = 0; category < 16; category++)
            {
                if (Named($"GlossaryCategory{category}") == null) break;

                Click($"GlossaryCategory{category}");
                yield return null;

                string page = Rows();
                Click("GlossaryNextPage");
                yield return null;

                if (Rows() != page)
                {
                    firstPage = page;
                    break;
                }
            }

            Assert.IsNotNull(firstPage,
                "no glossary category has more than one page, so the pager cannot witness a doubled " +
                "listener - this pin needs a deeper category or a different control");

            Click("GlossaryPrevPage");
            yield return null;

            Assert.AreEqual(firstPage, Rows(),
                "one press forward and one back did not land on the page it started on, so the " +
                "pager's listeners were added again on every opening of the panel");
        }

        // ---- fixture -------------------------------------------------------------------

        private static IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static IEnumerator Cycle(GameObject panel)
        {
            for (int i = 0; i < Cycles; i++)
            {
                panel.SetActive(false);
                yield return null;
                panel.SetActive(true);
                yield return null;
            }
        }

        // The list's own contents, which is what a page turn changes. Read off
        // the row NAME nodes -- GlossaryTests reads the same ones -- rather
        // than off a page number the panel does not expose, and it is the thing
        // the player is actually looking at.
        private static string Rows()
        {
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; ; i++)
            {
                var go = Named($"GlossaryRow{i}Name");
                if (go == null) break;
                names.Add(go.activeInHierarchy ? go.GetComponent<TMPro.TMP_Text>().text : "");
            }

            return string.Join("|", names);
        }

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");
            go.GetComponent<Button>().onClick.Invoke();
        }
    }
}
