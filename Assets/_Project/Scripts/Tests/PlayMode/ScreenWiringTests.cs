using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Proves the generated scene actually works when it runs.
    //
    // The build-time wiring sweep checks that no [SerializeField] reference is
    // null. That is necessary and not sufficient: a field can be non-null and
    // still point at the wrong object. These tests drive the real scene the way
    // a player does -- through public API and scene state, never by reaching
    // into controller internals, which is exactly why InternalsVisibleTo is
    // granted to the Editor assembly and NOT to this one.
    public class ScreenWiringTests
    {
        private static IEnumerator LoadMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            // Start() runs one frame after activation, not synchronously.
            yield return null;
            yield return null;
        }

        // ANY, not first. Every T this is asked for is a controller with
        // exactly one instance in the scene, so "first" was only ever a more
        // expensive way of saying "the one" -- and it ordered by instance ID,
        // which is the ordering Unity deprecated it for relying on.
        private static T Find<T>() where T : Object =>
            Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        private static GameObject FindByName(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == name && go.scene.IsValid());

        [UnityTest]
        public IEnumerator TheSceneBuildsWithItsControllersAttached()
        {
            yield return LoadMainMenu();

            Assert.IsNotNull(Find<MainMenuController>(), "MainMenuController should be on the panel root");
            Assert.IsNotNull(Find<SaveSlotController>());
            Assert.IsNotNull(Find<ResetProgressController>());
        }

        [UnityTest]
        public IEnumerator BothModalsStartHidden()
        {
            yield return LoadMainMenu();

            Assert.IsFalse(FindByName("SaveSlotPanel").activeSelf, "the save slot modal should start closed");
            Assert.IsFalse(FindByName("ManageSavesPanel").activeSelf, "the manage-saves modal should start closed");
        }

        [UnityTest]
        public IEnumerator PlayOpensTheSaveSlotModal_AndCancelClosesIt()
        {
            yield return LoadMainMenu();

            var panel = FindByName("SaveSlotPanel");
            FindByName("PlayButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(panel.activeSelf, "Play should open the save slot modal");

            FindByName("CloseSaveSlotButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(panel.activeSelf, "Cancel should close it again");
        }

        [UnityTest]
        public IEnumerator ManageSavesReplacesTheSlotList_AndBackReturnsToIt()
        {
            // Manage Saves has no door of its own on the main menu root any
            // more -- it is reached FROM the slot list, and it REPLACES that
            // list rather than layering on top of it (two panels answering the
            // same click would be ambiguous about which one it meant).
            yield return LoadMainMenu();

            var slotPanel = FindByName("SaveSlotPanel");
            var managePanel = FindByName("ManageSavesPanel");

            FindByName("PlayButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(slotPanel.activeSelf);

            // Start() runs one frame AFTER SetActive, not synchronously, so
            // SaveSlotController's own listeners -- including this button's --
            // do not exist yet on the same frame Play opened it.
            yield return null;
            yield return null;

            FindByName("ManageSavesButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(slotPanel.activeSelf, "Manage Saves should replace the slot list, not sit over it");
            Assert.IsTrue(managePanel.activeSelf);

            // THIS is Manage Saves' own first-ever activation in this test --
            // ResetProgressController lives on ManageSavesPanel, which started
            // inactive, so its Start() (and with it, backButton's listener)
            // has not run yet on the same frame the panel just opened.
            yield return null;
            yield return null;

            FindByName("CloseManageSavesButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(managePanel.activeSelf);
            Assert.IsTrue(slotPanel.activeSelf, "Back should return to the list Manage Saves was opened from");
        }

        [UnityTest]
        public IEnumerator EveryAmbientSpriteIsNonInteractive()
        {
            yield return LoadMainMenu();

            // 110-odd decorative glows sit across the whole frame, the button
            // column included. One left as a raycast target is an unclickable
            // Play button with no visible cause. The emitter clears this across
            // any Decor subtree; this asserts it actually happened.
            var ambience = FindByName("Ambience");
            Assert.IsNotNull(ambience);

            var interactive = ambience.GetComponentsInChildren<Graphic>(true)
                .Where(g => g.raycastTarget)
                .Select(g => g.name)
                .ToList();

            CollectionAssert.IsEmpty(interactive, "decorative ambience must never be a raycast target");
        }

        [UnityTest]
        public IEnumerator TheAmbientLayerIsFullyPopulated()
        {
            yield return LoadMainMenu();

            var ambience = FindByName("Ambience");
            var glows = ambience.GetComponentsInChildren<Image>(true);

            // Pinned to the measured tables: 45 stars + 16 windows + 10 lanterns
            // + 2 gazebo + 26 motes + 5 drift bands.
            Assert.AreEqual(104, glows.Length, "the ambient layer lost or gained elements");
            Assert.AreEqual(45, ambience.GetComponentsInChildren<StarTwinkle>(true).Length);
            Assert.AreEqual(10, ambience.GetComponentsInChildren<LanternFlicker>(true).Length);
            Assert.AreEqual(26, ambience.GetComponentsInChildren<MoteDrift>(true).Length);
            Assert.AreEqual(5, ambience.GetComponentsInChildren<SlowDrift>(true).Length);
        }

        // --- Hub ------------------------------------------------------------

        private static IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HubBuildsWithAllSixDestinationsWired()
        {
            yield return LoadHub();

            Assert.IsNotNull(Find<HubController>());
            foreach (var name in new[]
                     {
                         "TalentsBuilding", "PrincipalityBuilding", "CharacterSheetBuilding",
                         "RelicsBuilding", "StartRunGate", "MainMenuButton",
                     })
            {
                var go = FindByName(name);
                Assert.IsNotNull(go, $"{name} is missing from the Hub");
                Assert.IsNotNull(go.GetComponent<UnityEngine.UI.Button>(), $"{name} is not clickable");
            }
        }

        [UnityTest]
        public IEnumerator TheCurrencyLine_IsFormattedFromTheSameTemplateTheBuilderUsed()
        {
            // The v1 drift pair, closed and asserted at runtime: the builder
            // baked "Gold: 0    Relics: 0" while HubController wrote
            // its own copy. Relics stopped being a currency entirely, which is
            // why the line is two entries now and not three.
            // $"Gold: {g}    Relics: {r}" -- two hand-typed copies of a
            // four-space separator. One UiStrings entry serves both now.
            // Hermetic: the hub now READS the save on enable, so without a
            // throwaway root this asserts against whatever the developer's own
            // slot 0 happens to hold.
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "pp-hub-wiring-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            try
            {
                yield return LoadHub();

                var label = FindByName("CurrencyLabel").GetComponent<TMPro.TMP_Text>();
                Assert.AreEqual("Gold: 0    Embers: 0", label.text);

                // Three currencies now. Embers is what talents actually cost and
                // the hub never showed it.
                Find<HubController>().RefreshCurrency(340, 75);
                Assert.AreEqual("Gold: 340    Embers: 75", label.text);
            }
            finally
            {
                SaveSystem.RootOverride = null;
                SaveSlotManager.Forget();
                if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator TheHubBackgroundIsNotAClickTarget()
        {
            yield return LoadHub();

            // HubBackground, not Background: the menu tree owns a node of that
            // name too, and a name-based lookup across two trees is a coin flip.
            var background = FindByName("HubBackground");
            Assert.IsNotNull(background);
            Assert.IsFalse(background.GetComponent<UnityEngine.UI.Image>().raycastTarget,
                "the full-bleed background must never intercept a click meant for a building");
        }

        [UnityTest]
        public IEnumerator TheSceneLayerCarriesThePushIn_AndTheButtonsDoNot()
        {
            yield return LoadMainMenu();

            // The push-in must be on the PARENT of the background and ambience,
            // never the background alone, or every glow slides off its lantern.
            Assert.IsNotNull(FindByName("SceneLayer").GetComponent<KenBurnsDrift>());
            Assert.IsNull(FindByName("Background").GetComponent<KenBurnsDrift>());
            Assert.IsNull(FindByName("MenuButtons").GetComponent<KenBurnsDrift>(),
                "the UI must stay put while the painted world drifts");
        }
    }
}
