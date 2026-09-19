using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 9's mouse-only
    // regression. Opening the menu still has no click equivalent (this
    // plan's own brief names this exact case, and JourneySystemMenuMidFight
    // MouseTests' header says why the mouse's own ESC key covers it) -- but
    // reaching the Main Menu tab does: SystemMenuController's own tab
    // buttons are real Buttons, so this clicks SystemTabMainMenu directly
    // rather than pressing the shoulder shortcut, and clicks ExitTitle twice
    // rather than pressing Submit twice on whatever Move walked onto.
    public class JourneySystemMenuToMainMenuMouseTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-sysmenu-mainmenu-mouse-" + System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator FromTheHub_CancelOpensTheMenu_ClickMainMenuTab_ClickTitleTwiceLeavesToTheTitle_MouseOnly()
        {
            SaveSlotManager.EnterSlot(0);
            Assert.IsFalse(RunManager.HasRun, "fixture: outside a run, so the menu opens on its default tab");

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");
            Assert.IsFalse(menu.IsOpen, "fixture: the menu should start closed");

            yield return PressSystemMenu(); // no click equivalent -- HubController's own systemMenu handler, the mouse's own ESC key

            Assert.IsTrue(menu.IsOpen, "Start should open the system menu from the hub (the owner's 2026-09-19 call moved this off Cancel)");

            yield return Click(Node("SystemTabMainMenu")); // the tab strip's own real Button, not the shoulder shortcut

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.MainMenu), menu.SelectedIndex,
                "clicking the Main Menu tab should select it directly");

            yield return Click(Node("ExitTitle")); // ExitsController.Press(0) -- arms Title, does not yet leave
            Assert.IsTrue(menu.IsOpen, "the first click should only ARM leaving, not fire it");

            yield return Click(Node("ExitTitle")); // Press(0) again -- Fire(0): EndRun (a no-op outside a run), CloseMenu, Navigation.Go(MainMenu)

            yield return WaitForScene("MainMenu", 5f,
                "two clicks on Title should arm then fire the exit, loading the Main Menu for real");
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, NavigationInputModule.Contexts.Count,
                "leaving through the exit should not have left the hub's own context (or the menu's) behind " +
                "underneath the Main Menu's -- exactly one context should be on the stack, mouse-only same as " +
                "on the pad");
        }
    }
}
