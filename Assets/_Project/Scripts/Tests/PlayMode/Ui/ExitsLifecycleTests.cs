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
    // The Main menu pane's guards under INTERRUPTION, which is the half
    // SystemMenuExitsTests stops short of.
    //
    // That file drives each control through once, cleanly: one press arms, two
    // presses leave, a whole hold abandons, an early release loses everything.
    // Every rule on this pane is a guard, and a guard is only worth what it
    // does the SECOND time -- a repeated Begin, a press after the exit has
    // already fired, a pane torn down with something half-held. None of those
    // are reachable by driving the happy path twice in a row.
    //
    // scenarios A11, B3, E1, E2, E3, E4 (docs/hunt/SCENARIOS.md).
    public class ExitsLifecycleTests
    {
        private string _root;
        private readonly List<string> _navigated = new List<string>();
        private int _quits;

        private SystemMenuController _menu;
        private ExitsController _exits;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-exits-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            _navigated.Clear();
            _quits = 0;
            Navigation.LoadOverride = scene => _navigated.Add(scene);

            // Application.Quit takes a batch-mode player down, which is what
            // the headless runner is -- see SystemMenuExitsTests' own note.
            Navigation.QuitOverride = () => _quits++;
        }

        [TearDown]
        public void Restore()
        {
            // AUDIT #52: a hold left running between tests keeps advancing on
            // the shared player loop and fires its Completed into whoever runs
            // next. Cancelled first, before anything else, exactly as
            // SystemMenuExitsTests does.
            foreach (var hold in Object.FindObjectsByType<HoldToConfirm>(FindObjectsInactive.Include))
            {
                hold.Cancel();
            }

            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- A11 / E1: a mashed key cannot extend a hold ---------------------------

        // THE GUARD IS `if (_held) return`, and what it buys is that the fill
        // keeps climbing while the key repeats. Without it, every auto-repeat
        // of the held key would reset _elapsed to zero and the bar would sit
        // at nothing forever -- a hold that can never be completed, with a
        // control that visibly responds to every press.
        [UnityTest]
        public IEnumerator ARepeatedBeginNeitherRestartsNorExtendsALiveHold()
        {
            yield return OpenThePane("Map", withRun: true);

            var hold = Hold();
            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);

            float reached = hold.Progress01;
            Assert.AreEqual(0.9f, reached, 0.01f, "fixture: the hold did not advance to where this test needs it");

            // The key repeating under a finger that never lifted.
            hold.Begin();
            hold.Begin();
            hold.Begin();

            Assert.AreEqual(reached, hold.Progress01, 0.0001f,
                "a repeated press restarted a live hold, so holding the key down can never finish it");
            Assert.IsTrue(hold.Holding, "the repeat cancelled the hold instead of being ignored");

            // And it did not run the hold PAST its end either: the remaining
            // tenth is still owed.
            CollectionAssert.IsEmpty(_navigated, "the repeats completed the hold early");

            hold.Advance(ExitsController.HoldSeconds * 0.2f);
            yield return null;

            CollectionAssert.AreEqual(new[] { Navigation.Hub }, _navigated,
                "the hold never completed after the repeats, so a mashed key locked the control out");
        }

        // ---- E2: the second hold starts from zero -----------------------------------

        // Asserted on the FILL, not only on the outcome. SystemMenuExitsTests'
        // LettingGoEarlyLosesEverything proves the run survives two nine-tenths
        // holds; this proves the reason -- the progress the player can see is
        // back at nothing between them, rather than the control merely refusing
        // to fire from a bar that looks full.
        [UnityTest]
        public IEnumerator ReleasingEarlyAndPressingAgainStartsFromNothing()
        {
            yield return OpenThePane("Map", withRun: true);

            var fill = (RectTransform)Named("ExitAbandonHoldFill").transform;
            var hold = Hold();

            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);
            yield return null;

            Assert.Greater(fill.sizeDelta.x, ExitsLayout.HoldWidth * 0.5f,
                "fixture: the fill never grew, so a fill left full would not be visible to this test");

            hold.Cancel();
            yield return null;

            Assert.AreEqual(0f, fill.sizeDelta.x, 0.01f,
                "letting go left the fill where it was, so the next press starts from a bar that " +
                "already reads most of the way through");
            Assert.AreEqual(0f, hold.Progress01, 0.0001f, "the cancelled hold kept its progress");

            hold.Begin();
            Assert.AreEqual(0f, hold.Progress01, 0.0001f, "the second hold inherited the abandoned one's progress");
        }

        // ---- E3: a press after the exit has already fired -----------------------------

        // THE SCENE DOES NOT ACTUALLY CHANGE HERE -- Navigation.LoadOverride
        // records instead of loading -- which makes this test possible and also
        // makes it the honest one: in play the pane goes away with the scene,
        // so the only thing that could ever fire twice is a pane that outlived
        // its own exit. A third press must arm a fresh confirmation rather than
        // leave again on one press.
        [UnityTest]
        public IEnumerator AThirdPressAfterLeavingDoesNotLeaveASecondTime()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            Click("ExitTitle");
            yield return null;

            CollectionAssert.AreEqual(new[] { Navigation.MainMenu }, _navigated, "fixture: two presses did not leave");
            Assert.AreEqual(-1, _exits.ArmedIndex, "firing an exit left it armed");

            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex,
                "a press after the exit fired did not re-arm, so the pane has forgotten its own rule");
            CollectionAssert.AreEqual(new[] { Navigation.MainMenu }, _navigated,
                "a third press left a second time, so one click on a spent exit navigates");
        }

        // ---- E4: an exit forgotten by its timer arms again ------------------------------

        // The second half of AnArmedExitForgetsItselfAfterAWhile, which asserts
        // that the stale press does not LEAVE and stops there. What it must do
        // instead is the thing a first press always does.
        [UnityTest]
        public IEnumerator APressOnAForgottenExitArmsItRatherThanDoingNothing()
        {
            yield return OpenThePane("Hub");

            Click("ExitTitle");
            _exits.Advance(ExitsController.ArmSeconds);
            yield return null;
            Assert.AreEqual(-1, _exits.ArmedIndex, "fixture: the arm never expired");

            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex,
                "the press after the forget timer did nothing at all, so the exit is dead until " +
                "something else re-enters the pane");
            CollectionAssert.IsEmpty(_navigated, "a stale arm let one press leave the scene");
        }

        // ---- B3: the pane torn down with everything live --------------------------------

        // SwitchingTabsForgetsAnArmedExit drives the same mechanism through the
        // tab bar. This drives the object itself, and adds the two things the
        // tab route cannot see: the abandon hold half-held across the cycle,
        // and whether OnEnable's ApplyContext ran again on the way back in.
        [UnityTest]
        public IEnumerator DisablingThePaneWithAnExitArmedAndAHoldHalfDoneArrivesBackClean()
        {
            yield return OpenThePane("Map", withRun: true);

            var fill = (RectTransform)Named("ExitAbandonHoldFill").transform;
            var hold = Hold();

            Click("ExitTitle");
            hold.Begin();
            hold.Advance(ExitsController.HoldSeconds * 0.9f);
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex, "fixture: nothing was armed");
            Assert.IsTrue(hold.Holding, "fixture: nothing was being held");

            _exits.gameObject.SetActive(false);
            yield return null;

            Assert.AreEqual(-1, _exits.ArmedIndex, "the armed exit survived the pane being closed");
            Assert.IsFalse(hold.Holding, "the abandon hold kept running on a closed pane");

            _exits.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(-1, _exits.ArmedIndex,
                "the pane came back with an exit still armed, so one press leaves");
            Assert.AreEqual(0f, hold.Progress01, 0.0001f, "the pane came back half-way through an abandon");
            Assert.AreEqual(0f, fill.sizeDelta.x, 0.01f, "the pane came back showing a part-filled hold bar");

            // Context re-applied rather than inherited: still a descent, so the
            // abandon card is still there.
            Assert.IsTrue(Named("ExitAbandonCard").activeSelf,
                "re-enabling the pane lost the abandon card the descent it is in should still offer");

            // AND THE WIRING IS NOT DOUBLED. Wire() is guarded by _wired, so a
            // second OnEnable adds no second listener -- if it did, this one
            // press would arm and then immediately fire.
            Click("ExitTitle");
            yield return null;

            Assert.AreEqual(ExitsLayout.ExitIndexTitle, _exits.ArmedIndex);
            CollectionAssert.IsEmpty(_navigated,
                "one press left the scene after a disable/re-enable, so the exit's listener was added twice");
            Assert.AreEqual(0, _quits, "the re-enable wired a quit nobody asked for");
        }

        // ---- fixture ------------------------------------------------------------------

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
            go.GetComponent<Button>().onClick.Invoke();
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
