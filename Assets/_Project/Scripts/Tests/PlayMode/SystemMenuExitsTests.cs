using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The system menu's Main menu pane.
    //
    // THE ONE RULE THIS PANE HAS is that nothing on it fires on a single press,
    // and it is the only rule here a screenshot cannot check. Everything else
    // about the pane is geometry the build audits; this is behaviour, and a
    // regression would look exactly like a working button right up to the
    // moment it threw somebody's descent away.
    //
    // Driven through the buttons the scene actually emits rather than through
    // the controller's fields: PlayMode tests deliberately have no access to
    // Core's internals, so a test cannot quietly reach past the wiring to make
    // itself pass.
    public class SystemMenuExitsTests
    {
        private string _root;
        private readonly List<string> _navigated = new List<string>();
        private int _quits;

        private SystemMenuController _menu;
        private ExitsController _exits;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-exits-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            _navigated.Clear();
            _quits = 0;
            Navigation.LoadOverride = scene => _navigated.Add(scene);

            // Application.Quit takes a BATCH-MODE PLAYER DOWN, which is exactly
            // what the headless runner is -- so without this seam the first test
            // that reached the quit button would end the whole suite and report
            // it as a crash.
            Navigation.QuitOverride = () => _quits++;
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            // The menu pauses by setting timeScale to zero. A test that fails
            // mid-way would otherwise leave every later test running at zero,
            // and they would fail for a reason that has nothing to do with them.
            Time.timeScale = 1f;

            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- nothing fires on one press --------------------------------------------

        [UnityTest]
        public IEnumerator OnePressOnAnExitDoesNothingButArmIt()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex,
                "one press should arm the exit");
            CollectionAssert.IsEmpty(_navigated, "one press on an exit left the scene");
        }

        [UnityTest]
        public IEnumerator TheSecondPressIsWhatLeaves()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            Click("ExitTitle");
            yield return null;

            CollectionAssert.AreEqual(new[] { Navigation.MainMenu }, _navigated,
                "two presses on RETURN TO TITLE did not go to the title");
        }

        // Two loaded buttons and no way to tell which press belongs to which is
        // how a confirmation step becomes a coin toss.
        [UnityTest]
        public IEnumerator ArmingOneExitDisarmsTheOther()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            Click("ExitQuit");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexQuit, _exits.ArmedIndex);

            // The one that was armed first must now need two presses again, so
            // this single press does nothing but re-arm it.
            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex);
            CollectionAssert.IsEmpty(_navigated,
                "switching between the two exits fired one of them");
        }

        // An exit left armed while the player went to look at their inventory
        // must not still be armed when they come back and click through it.
        [UnityTest]
        public IEnumerator AnArmedExitForgetsItselfAfterAWhile()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex);

            // Driven directly rather than waited out: the arming clock is
            // unscaled, so four real seconds is what this would otherwise cost.
            _exits.Advance(ExitsController.ArmSeconds);
            yield return null;

            Assert.AreEqual(-1, _exits.ArmedIndex, "the armed exit never disarmed");

            Click("ExitTitle");
            yield return null;
            CollectionAssert.IsEmpty(_navigated, "a stale arm let one press leave the scene");
        }

        // Leaving the pane is letting go of everything on it.
        [UnityTest]
        public IEnumerator SwitchingTabsForgetsAnArmedExit()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            _menu.Select(SystemMenuTab.Options);
            yield return null;
            _menu.Select(SystemMenuTab.MainMenu);
            yield return null;

            Assert.AreEqual(-1, _exits.ArmedIndex,
                "an exit stayed armed across a tab change, so coming back and clicking once leaves");
        }

        // ---- what each exit actually does ---------------------------------------------

        // The design asked for "back to title (run stays as it is)". RunManager
        // states the opposite as a rule with money attached: leaving a descent
        // kills the run, and EndRun is what PAYS OUT what it earned. Both
        // existing doors -- the hub's title button and the main menu's quit --
        // enforce it, and a third that parked a run would leave the player at
        // the title with a live descent in the save.
        [UnityTest]
        public IEnumerator LeavingForTheTitleEndsTheDescent()
        {
            yield return OpenThePane("Map", withRun: true);
            Assert.IsTrue(RunManager.HasRun, "the fixture did not start a run");

            Click("ExitTitle");
            Click("ExitTitle");
            yield return null;

            Assert.IsFalse(RunManager.HasRun, "returning to the title left the descent running");
            CollectionAssert.AreEqual(new[] { Navigation.MainMenu }, _navigated);
        }

        [UnityTest]
        public IEnumerator QuittingGoesThroughTheSeamRatherThanApplicationQuit()
        {
            yield return OpenThePane("Hub");

            Click("ExitQuit");
            Click("ExitQuit");
            yield return null;

            Assert.AreEqual(1, _quits, "QUIT TO DESKTOP did not reach Navigation.Quit");
            CollectionAssert.IsEmpty(_navigated, "quitting loaded a scene on its way out");
        }

        // ---- the hold ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AbandonNeedsTheWholeHold()
        {
            yield return OpenThePane("Map", withRun: true);

            var hold = Hold();
            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);
            yield return null;

            CollectionAssert.IsEmpty(_navigated, "abandon fired before the hold was done");
            Assert.IsTrue(RunManager.HasRun, "abandon ended the run before the hold was done");

            hold.Advance(ExitsController.HoldSeconds * 0.2f);
            yield return null;

            CollectionAssert.AreEqual(new[] { Navigation.Hub }, _navigated,
                "abandoning should walk back up to the hub, not out to the title");
            Assert.IsFalse(RunManager.HasRun, "abandon did not end the descent");
        }

        [UnityTest]
        public IEnumerator LettingGoEarlyLosesEverything()
        {
            yield return OpenThePane("Map", withRun: true);

            var hold = Hold();
            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);
            hold.Cancel();

            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);
            yield return null;

            Assert.IsTrue(RunManager.HasRun,
                "a released hold carried its progress into the next one, so two taps abandoned the run");
        }

        // A Button fires onClick on RELEASE whatever the press was for. Wiring
        // abandon to onClick would therefore end a descent on a tap, which is
        // the one thing this pane may not do.
        [UnityTest]
        public IEnumerator TappingAbandonDoesNothing()
        {
            yield return OpenThePane("Map", withRun: true);

            Click("ExitAbandonHold");
            yield return null;

            Assert.IsTrue(RunManager.HasRun, "a plain click on the hold button abandoned the run");
            CollectionAssert.IsEmpty(_navigated);
        }

        // ---- context and the clock ------------------------------------------------------

        // Absent, never greyed -- the tab bar's own rule, one level down.
        [UnityTest]
        public IEnumerator AbandonIsAbsentBetweenDescentsAndPresentInOne()
        {
            yield return OpenThePane("Hub");
            Assert.IsFalse(_exits.InDescent, "the hub thinks it is a descent");
            Assert.IsFalse(Named("ExitAbandonCard").activeSelf,
                "the hub offers to abandon a descent that is not happening");

            yield return OpenThePane("Map", withRun: true);
            Assert.IsTrue(_exits.InDescent);
            Assert.IsTrue(Named("ExitAbandonCard").activeSelf,
                "the map does not offer to abandon the descent it is part of");
        }

        // The menu pauses by setting timeScale to zero, and leaving through it
        // has to put the clock back -- otherwise the scene it navigates to comes
        // up frozen, which looks like a hang and is not one.
        [UnityTest]
        public IEnumerator LeavingThroughAnExitPutsTheClockBack()
        {
            yield return OpenThePane("Hub");
            Assert.AreEqual(0f, Time.timeScale, "the menu did not pause the game");

            Click("ExitTitle");
            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(1f, Time.timeScale,
                "leaving through the menu left the game paused, so the next scene comes up frozen");
        }

        // ---- fixture --------------------------------------------------------------------

        private IEnumerator OpenThePane(string scene, bool withRun = false)
        {
            if (withRun) RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, $"{scene} has no SystemMenuController");

            _menu.Open();
            _menu.Select(SystemMenuTab.MainMenu);

            // Two frames: Start() lands on the one after activation, and
            // OnEnable's context pass has to have run before anything is read.
            yield return null;
            yield return null;

            _exits = Object.FindAnyObjectByType<ExitsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_exits, $"{scene} has no ExitsController");
        }

        private GameObject Named(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string buttonName)
        {
            var go = Named(buttonName);
            Assert.IsNotNull(go, $"the Main menu pane has no '{buttonName}'");

            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{buttonName}' is not a button");
            button.onClick.Invoke();
        }

        private HoldToConfirm Hold()
        {
            var go = Named("ExitAbandonHold");
            Assert.IsNotNull(go, "the abandon card has no hold button");

            var hold = go.GetComponent<HoldToConfirm>();
            Assert.IsNotNull(hold, "the abandon button has no hold behaviour attached");
            return hold;
        }
    }
}
