using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 9: the
    // smallest of the five, and the one this plan's own status header says
    // depends on wherever segment 8 ends -- reconstructed here from the Hub
    // directly (the state segment 8's own Reckoning Continue returns to on a
    // win, and the state every fresh descent starts from) rather than
    // chained onto that file's own class, for the same cross-class-order
    // reason every segment in this suite reconstructs its own precondition
    // (JourneyFightRoundTests' own header).
    //
    // The system menu's "Main Menu" tab is ExitsScreen (ExitsController) --
    // three ways out, none on one press (that file's own header). This
    // segment takes "Title": arm, then fire, the same two-press rule the
    // mouse path takes through ExitTitle's own onClick.
    public class JourneySystemMenuToMainMenuTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-sysmenu-mainmenu-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator FromTheHub_CancelOpensTheMenu_TabPrevReachesMainMenu_SubmitTwiceLeavesToTheTitle()
        {
            SaveSlotManager.EnterSlot(0);
            Assert.IsFalse(RunManager.HasRun, "fixture: outside a run, so the menu opens on its default tab");

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");
            Assert.IsFalse(menu.IsOpen, "fixture: the menu should start closed");

            yield return PressSystemMenu(); // HubController's own systemMenu handler -> SystemMenuController.OpenFromRoot

            Assert.IsTrue(menu.IsOpen, "Start should open the system menu from the hub (the owner's 2026-09-19 call moved this off Cancel)");

            yield return PressTabPrev(); // wraps from the first visible tab to the last one, MainMenu

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.MainMenu), menu.SelectedIndex,
                "one TabPrev from the default tab should wrap to the last visible tab, MainMenu, outside a run");

            AssertSelectedName("ExitTitle",
                "the shoulder shortcut should land inside the new pane on its own entry -- Title, the first " +
                "row of ExitsScreen's List");

            yield return PressSubmit(); // ExitsController.Press(0) -- arms Title, does not yet leave
            Assert.IsTrue(menu.IsOpen, "the first press should only ARM leaving, not fire it");

            yield return PressSubmit(); // Press(0) again -- Fire(0): EndRun (a no-op outside a run), CloseMenu, Navigation.Go(MainMenu)

            yield return WaitForScene("MainMenu", 5f,
                "two Submits on Title should arm then fire the exit, loading the Main Menu for real");
            yield return null;
            yield return null;
            TakeOverInput();

            // ContinueButton, not PlayButton: SaveSlotManager.EnterSlot(0)
            // above already wrote a save to this throwaway root, so by the
            // time the exit reaches the Main Menu a save exists to resume --
            // MainMenuGamepadNavigationTests' own SeedASave is the same
            // precondition, reached here as a side effect of the journey
            // rather than deliberately seeded.
            AssertSelectedName("ContinueButton",
                "with a save on disk (this slot's own EnterSlot above wrote one), Continue is the Main Menu's own entry");
            Assert.AreEqual(1, NavigationInputModule.Contexts.Count,
                "leaving through the exit should not have left the hub's own context (or the menu's) " +
                "behind underneath the Main Menu's -- exactly one context should be on the stack");
        }
    }
}
